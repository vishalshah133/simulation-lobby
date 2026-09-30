using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// The sound of a material: a glass clink that crossfades into a wet squelch as softness rises,
    /// a rubbery creak while the surface stretches, a low wobble while it jiggles, plus the whoosh,
    /// ambient bed and caption notes around them. All synthesised (<see cref="ProceduralToneBank"/>).
    /// </summary>
    /// <remarks>
    /// Format-agnostic and strictly read-only: a format's presenter pushes numbers in (impact speed,
    /// softness, strain rate, wobble energy) and this never sees a simulation. Removing it cannot
    /// change a run.
    /// <para>
    /// One-shots are generated on demand and cached by quantised parameters. The continuous voices
    /// (creak, wobble) are synthesised per sample in <see cref="OnAudioFilterRead"/>, from targets the
    /// main thread sets and the audio thread glides toward, so they follow the body without zipper
    /// noise. Noise inside them comes from a seeded generator, so a re-render sounds the same.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class MaterialContactAudio : MonoBehaviour
    {
        [Header("Contact")]
        [Range(0f, 1f)] public float contactVolume = 0.9f;

        [Tooltip("Impact speed (m/s) that plays at full volume. Slower hits scale down.")]
        [Min(0.1f)] public float fullVolumeImpactSpeed = 3f;

        [Tooltip("Clink pitch multiplier. Lower reads as a heavier object.")]
        [Range(0.4f, 2f)] public float clinkPitch = 0.9f;

        [Range(2, 16)] public int voiceCount = 10;

        [Header("Continuous voices")]
        [Range(0f, 1f)] public float creakVolume = 0.22f;
        [Range(60f, 400f)] public float creakLowHz = 170f;
        [Range(200f, 1200f)] public float creakHighHz = 520f;
        [Range(0f, 1f)] public float wobbleVolume = 0.35f;
        [Range(40f, 160f)] public float wobbleHz = 72f;

        [Header("Whoosh")]
        [Range(0f, 1f)] public float whooshVolume = 0.28f;

        [Header("Caption note — rises one degree per take")]
        [Range(110f, 880f)] public float noteRootHz = 523.25f;
        [Range(0.3f, 3f)] public float noteDuration = 1.6f;
        [Range(0f, 1f)] public float noteVolume = 0.3f;

        [Header("Ambient bed")]
        [Range(0f, 1f)] public float ambientVolume = 0.22f;
        [Tooltip("Fraction of the bed removed while ducked (the hush before contact).")]
        [Range(0f, 1f)] public float duckDepth = 0.75f;
        [Range(0.02f, 2f)] public float duckSeconds = 0.25f;
        [Range(0.05f, 4f)] public float unduckSeconds = 1.2f;

        readonly Dictionary<int, AudioClip> _clinks = new Dictionary<int, AudioClip>();
        readonly Dictionary<int, AudioClip> _squelches = new Dictionary<int, AudioClip>();
        readonly Dictionary<int, AudioClip> _notes = new Dictionary<int, AudioClip>();
        readonly Dictionary<int, AudioClip> _whooshes = new Dictionary<int, AudioClip>();

        AudioSource[] _voices;
        AudioSource _ambient;
        int _nextVoice;
        int _contactCount;
        float _duck;
        bool _ducked;

        // Audio-thread state. Targets are written on the main thread; everything else is private to
        // OnAudioFilterRead.
        volatile float _creakTarget;
        volatile float _creakPitchTarget;
        volatile float _wobbleTarget;
        float _creakGain;
        float _creakPitch;
        float _wobbleGain;
        double _creakPhase;
        double _wobblePhase;
        float _grain;
        float _outputRate = 48000f;
        System.Random _audioNoise;

        void Awake()
        {
            _outputRate = AudioSettings.outputSampleRate;
            _audioNoise = new System.Random(911);

            // The continuous voices ride on this object's own source: a silent loop keeps the filter
            // callback alive, and the callback writes the voices over it.
            AudioSource carrier = GetComponent<AudioSource>();
            carrier.clip = AudioClip.Create("Carrier (silent)", 44100, 1, 44100, false);
            carrier.loop = true;
            carrier.playOnAwake = false;
            carrier.spatialBlend = 0f;
            carrier.volume = 1f;
            carrier.Play();

            _voices = new AudioSource[Mathf.Max(2, voiceCount)];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = CreateSource($"Voice {i}");
            }

            _ambient = CreateSource("Ambient");
            _ambient.clip = ProceduralToneBank.CreateAmbientBed(8f, 55f, "AmbientBed");
            _ambient.loop = true;
            _ambient.volume = ambientVolume;
            _ambient.Play();
        }

        AudioSource CreateSource(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        void Update()
        {
            float target = _ducked ? duckDepth : 0f;
            float seconds = _ducked ? duckSeconds : unduckSeconds;
            _duck = Mathf.MoveTowards(_duck, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
            if (_ambient != null)
            {
                _ambient.volume = ambientVolume * (1f - _duck);
            }
        }

        /// <summary>
        /// One contact. Clink and squelch are both played, weighted by softness — at 50% you hear
        /// both, which is what makes a sweep audibly progress rather than switch.
        /// </summary>
        /// <param name="impactSpeed">Approach speed in m/s.</param>
        /// <param name="softness01">0 = glass, 1 = jelly.</param>
        public void PlayContact(float impactSpeed, float softness01)
        {
            float loudness = Mathf.Clamp01(impactSpeed / fullVolumeImpactSpeed);
            loudness = Mathf.Sqrt(loudness); // perceived loudness is closer to the root of energy
            softness01 = Mathf.Clamp01(softness01);
            _contactCount++;

            float hard = Mathf.Pow(1f - softness01, 0.8f);
            float soft = Mathf.Pow(softness01, 0.8f);

            if (hard > 0.02f)
            {
                // Softer glass rings shorter and a touch lower — the ring is damped by the give.
                int key = Mathf.RoundToInt(softness01 * 20f) * 16 + _contactCount % 4;
                if (!_clinks.TryGetValue(key, out AudioClip clink))
                {
                    clink = ProceduralToneBank.CreateGlassClink(
                        clinkPitch * (1f - 0.18f * softness01) * (1f + 0.03f * (_contactCount % 4)),
                        Mathf.Lerp(1.6f, 0.5f, softness01), 30 + key, $"Clink_{key}");
                    _clinks[key] = clink;
                }

                Play(clink, contactVolume * hard * loudness);
            }

            if (soft > 0.02f)
            {
                int key = Mathf.RoundToInt(softness01 * 20f) * 16 + _contactCount % 3;
                if (!_squelches.TryGetValue(key, out AudioClip squelch))
                {
                    squelch = ProceduralToneBank.CreateSquelch(softness01, 70 + key, $"Squelch_{key}");
                    _squelches[key] = squelch;
                }

                Play(squelch, contactVolume * soft * loudness);
            }

            SetDucked(false);
        }

        /// <summary>The fall. Length should match the time to contact so it peaks on the hit.</summary>
        public void PlayWhoosh(float seconds)
        {
            int key = Mathf.RoundToInt(Mathf.Clamp(seconds, 0.15f, 3f) * 100f);
            if (!_whooshes.TryGetValue(key, out AudioClip whoosh))
            {
                whoosh = ProceduralToneBank.CreateWhoosh(key / 100f, 2, $"Whoosh_{key}");
                _whooshes[key] = whoosh;
            }

            Play(whoosh, whooshVolume);
        }

        /// <summary>A bell note on the pentatonic ladder. Rising one degree per take gives a sweep a melody.</summary>
        public void PlayNote(int degree)
        {
            if (!_notes.TryGetValue(degree, out AudioClip note))
            {
                note = ProceduralToneBank.CreateTone(ProceduralToneBank.DegreeToFrequency(noteRootHz, degree),
                    noteDuration, $"Note_{degree}");
                _notes[degree] = note;
            }

            Play(note, noteVolume);
        }

        /// <summary>Hush the ambient bed (before contact) or let it back in (after).</summary>
        public void SetDucked(bool ducked)
        {
            _ducked = ducked;
        }

        /// <summary>Rubber creak. <paramref name="amount01"/> = how hard it is stretching right now.</summary>
        public void SetStretch(float amount01, float strainLevel01)
        {
            _creakTarget = Mathf.Clamp01(amount01) * creakVolume;
            _creakPitchTarget = Mathf.Lerp(creakLowHz, creakHighHz, Mathf.Clamp01(strainLevel01));
        }

        /// <summary>Low wobble, felt more than heard. 0 = still.</summary>
        public void SetWobble(float amount01)
        {
            _wobbleTarget = Mathf.Clamp01(amount01) * wobbleVolume;
        }

        /// <summary>Stop the continuous voices — between takes, so one take's jiggle never bleeds into the next.</summary>
        public void Silence()
        {
            _creakTarget = 0f;
            _wobbleTarget = 0f;
        }

        void Play(AudioClip clip, float volume)
        {
            if (clip == null || volume <= 0.001f || _voices == null)
            {
                return;
            }

            // Round-robin: a new sound takes the oldest voice rather than cutting off the newest.
            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.Stop();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(volume);
            voice.Play();
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (_audioNoise == null)
            {
                return;
            }

            float rate = _outputRate;
            // ~15ms glide on every parameter: fast enough to track a squash, slow enough not to click.
            float glide = 1f - Mathf.Exp(-1f / (0.015f * rate));
            float creakTarget = _creakTarget;
            float pitchTarget = _creakPitchTarget > 1f ? _creakPitchTarget : creakLowHz;
            float wobbleTarget = _wobbleTarget;
            double twoPi = 2.0 * System.Math.PI;

            for (int i = 0; i < data.Length; i += channels)
            {
                _creakGain += (creakTarget - _creakGain) * glide;
                _creakPitch += (pitchTarget - _creakPitch) * glide;
                _wobbleGain += (wobbleTarget - _wobbleGain) * glide;

                float value = 0f;

                if (_creakGain > 1e-5f)
                {
                    // Rubber squeak: a buzzy tone (odd harmonics) with a rough, grainy amplitude —
                    // stick-slip friction is a stream of tiny catches, not a smooth tone.
                    _creakPhase += twoPi * _creakPitch / rate;
                    if (_creakPhase > twoPi)
                    {
                        _creakPhase -= twoPi;
                    }

                    float p = (float)_creakPhase;
                    float tone = Mathf.Sin(p) + 0.35f * Mathf.Sin(3f * p) + 0.15f * Mathf.Sin(5f * p);
                    float noise = (float)(_audioNoise.NextDouble() * 2.0 - 1.0);
                    _grain += (Mathf.Abs(noise) - _grain) * 0.02f;
                    value += tone * (0.55f + 0.9f * _grain) * _creakGain * 0.5f;
                }

                if (_wobbleGain > 1e-5f)
                {
                    _wobblePhase += twoPi * wobbleHz / rate;
                    if (_wobblePhase > twoPi)
                    {
                        _wobblePhase -= twoPi;
                    }

                    float p = (float)_wobblePhase;
                    // The 2nd and 3rd harmonics carry the wobble on phone speakers that can't play 70 Hz.
                    value += (Mathf.Sin(p) + 0.5f * Mathf.Sin(2f * p) + 0.2f * Mathf.Sin(3f * p)) * _wobbleGain * 0.5f;
                }

                // Soft clip: the continuous voices can stack, and a hard clip is the ugliest failure.
                value = value / (1f + Mathf.Abs(value));

                for (int c = 0; c < channels; c++)
                {
                    data[i + c] += value;
                }
            }
        }
    }
}
