using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// ASMR audio for a collision-driven format: a rising whoosh as the impactor approaches, then a
    /// bank of round-robin knock sounds as it plows through the field. The wall-survival (<c>surv</c>) counterpart
    /// to <see cref="BounceMelodyPlayer"/> — same reasons apply (generated, so nothing to licence;
    /// round-robin voices so overlapping hits don't cut each other off).
    /// </summary>
    /// <remarks>
    /// Purely presentational — something else decides when to call <see cref="PlayWhoosh"/> and
    /// <see cref="PlayImpact"/>; this never touches simulation state.
    /// </remarks>
    public sealed class ImpactAudioPlayer : MonoBehaviour
    {
        [Header("Thud bank")]
        [Tooltip("Distinct knock samples in rotation. More = less obviously repetitive at high hit rates.")]
        [Range(3, 16)] public int thudVariationCount = 8;

        [Range(0.05f, 1f)] public float thudDuration = 0.16f;

        [Tooltip("Base pitch multiplier for the knock. Kept well above 1 on purpose: at 1 the knock " +
                 "sits around 55 Hz, which phone speakers simply do not reproduce — the hit reads as " +
                 "silence on exactly the device most of this content is watched on.")]
        [Range(0.3f, 4f)] public float thudBasePitch = 2.2f;

        [Tooltip("Random pitch jitter per hit, as a fraction of base pitch. 0 = every hit identical.")]
        [Range(0f, 0.5f)] public float thudPitchJitter = 0.15f;

        [Range(2, 16)] public int thudVoiceCount = 10;
        [Range(0f, 1f)] public float thudVolume = 0.75f;

        [Tooltip("How much impact speed varies knock loudness. 0 = every knock equally loud.")]
        [Range(0f, 1f)] public float velocitySensitivity = 0.4f;

        [Header("Whoosh")]
        [Range(0.2f, 3f)] public float whooshDuration = 0.9f;
        [Range(0f, 1f)] public float whooshVolume = 0.5f;

        [Header("Riser — the tension cue under an anticipation hold")]
        [Tooltip("Length of the sweep. Set this to the hold beat's length so it peaks on the launch.")]
        [Range(0.2f, 4f)] public float riserDuration = 1.4f;

        [Range(0f, 1f)] public float riserVolume = 0.35f;

        [Header("Chime — the elimination cascade")]
        [Tooltip("Notes in the pentatonic ladder. A cascade walks up these, so more = a longer climb " +
                 "before it wraps back to the bottom.")]
        [Range(3, 24)] public int chimeNoteCount = 12;

        [Tooltip("Root of the chime ladder in Hz. Lower reads warmer on a phone speaker.")]
        [Range(110f, 660f)] public float chimeRootHz = 392f;

        [Range(0.1f, 2f)] public float chimeDuration = 0.7f;
        [Range(0f, 1f)] public float chimeVolume = 0.32f;
        [Range(2, 24)] public int chimeVoiceCount = 12;

        [Header("Scatter rumble — debris in motion")]
        [Tooltip("A noise bed whose loudness follows how much of the wall is actually moving, so a " +
                 "scatter is audible while it happens and fades as it settles. Off = the run is " +
                 "silent between discrete knocks, which is the single biggest thing missing from a " +
                 "field of 200 objects visibly tumbling.")]
        public bool playScatterRumble = true;

        [Range(0f, 1f)] public float scatterVolume = 0.45f;

        [Tooltip("Seconds the rumble takes to follow a change in motion. Short enough to feel " +
                 "reactive, long enough not to flutter frame to frame.")]
        [Range(0.02f, 1f)] public float scatterSmoothing = 0.12f;

        [Header("Ambient bed — the ASMR room tone under the whole run")]
        [Tooltip("Off = silence between waves, which reads as dead air over a 60-90s runtime.")]
        public bool playAmbientBed = true;

        [Range(0f, 1f)] public float ambientVolume = 0.18f;

        AudioClip[] _thudBank;
        AudioClip _whooshClip;
        AudioClip _riserClip;
        AudioClip[] _chimeLadder;
        AudioClip _ambientClip;
        AudioClip _rumbleClip;
        AudioSource[] _thudVoices;
        AudioSource[] _chimeVoices;
        AudioSource _whooshVoice;
        AudioSource _riserVoice;
        AudioSource _ambientVoice;
        AudioSource _rumbleVoice;
        float _scatterTarget;
        float _scatterCurrent;
        int _nextThudVoice;
        int _nextThudClip;
        int _nextChimeVoice;

        void Awake()
        {
            _thudBank = ProceduralToneBank.CreateThudBank(thudVariationCount, thudBasePitch, thudDuration);
            _whooshClip = ProceduralToneBank.CreateWhoosh(whooshDuration);

            _thudVoices = new AudioSource[thudVoiceCount];
            for (int i = 0; i < thudVoiceCount; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _thudVoices[i] = source;
            }

            _chimeLadder = ProceduralToneBank.CreateScale(chimeNoteCount, chimeRootHz, chimeDuration);
            _chimeVoices = new AudioSource[chimeVoiceCount];
            for (int i = 0; i < chimeVoiceCount; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _chimeVoices[i] = source;
            }

            _whooshVoice = CreateVoice();
            _riserVoice = CreateVoice();

            _riserClip = ProceduralToneBank.CreateRiser(riserDuration);

            _rumbleVoice = CreateVoice();
            _rumbleClip = ProceduralToneBank.CreateRumbleLoop();
            _rumbleVoice.clip = _rumbleClip;
            _rumbleVoice.loop = true;
            _rumbleVoice.volume = 0f;

            _ambientVoice = CreateVoice();
            _ambientClip = ProceduralToneBank.CreateAmbientBed();
            _ambientVoice.clip = _ambientClip;
            _ambientVoice.loop = true;
            _ambientVoice.volume = ambientVolume;
        }

        void Start()
        {
            if (playAmbientBed)
            {
                StartAmbientBed();
            }

            if (playScatterRumble && _rumbleVoice != null)
            {
                _rumbleVoice.Play();
            }
        }

        void Update()
        {
            if (_rumbleVoice == null)
            {
                return;
            }

            // Unscaled: a slow-motion impact should still sound like it is happening, not drop to a
            // crawl along with the picture.
            float step = scatterSmoothing <= 0f
                ? 1f
                : Mathf.Clamp01(Time.unscaledDeltaTime / scatterSmoothing);

            _scatterCurrent = Mathf.Lerp(_scatterCurrent, _scatterTarget, step);
            _rumbleVoice.volume = _scatterCurrent * scatterVolume;
            // Faster debris reads brighter as well as louder.
            _rumbleVoice.pitch = Mathf.Lerp(0.75f, 1.35f, _scatterCurrent);
        }

        /// <summary>
        /// How much of the field is in motion, 0..1. Drives the scatter rumble; call it every frame
        /// from whatever is watching the simulation.
        /// </summary>
        public void SetScatterIntensity(float intensity01)
        {
            _scatterTarget = Mathf.Clamp01(intensity01);
        }

        AudioSource CreateVoice()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>Play the approach cue. Call once, right as the impactor is launched.</summary>
        public void PlayWhoosh()
        {
            if (_whooshClip == null)
            {
                return;
            }

            _whooshVoice.PlayOneShot(_whooshClip, whooshVolume);
        }

        /// <summary>
        /// Play the next knock. <paramref name="velocity01"/> is normalised impact strength; pass 1
        /// if the caller does not track it.
        /// </summary>
        public void PlayImpact(float velocity01 = 1f)
        {
            if (_thudBank == null || _thudBank.Length == 0)
            {
                return;
            }

            AudioClip clip = _thudBank[_nextThudClip];
            _nextThudClip = (_nextThudClip + 1) % _thudBank.Length;

            AudioSource source = _thudVoices[_nextThudVoice];
            _nextThudVoice = (_nextThudVoice + 1) % _thudVoices.Length;

            float pitchJitter = 1f + Random.Range(-thudPitchJitter, thudPitchJitter);
            source.pitch = pitchJitter;

            float gain = Mathf.Lerp(1f - velocitySensitivity, 1f, Mathf.Clamp01(velocity01));
            source.PlayOneShot(clip, thudVolume * gain);
        }

        /// <summary>
        /// Start the rising tension cue. Call on the first tick of a wave's anticipation hold — the
        /// clip is generated to <see cref="riserDuration"/>, so it peaks as the hold ends.
        /// </summary>
        /// <param name="intensity01">
        /// How far into the run this wave is, 0..1. Later waves play the riser louder and a touch
        /// higher, so the build escalates across the video rather than resetting every wave.
        /// </param>
        public void PlayRiser(float intensity01 = 0f)
        {
            if (_riserClip == null)
            {
                return;
            }

            _riserVoice.pitch = Mathf.Lerp(0.9f, 1.25f, Mathf.Clamp01(intensity01));
            _riserVoice.PlayOneShot(_riserClip, riserVolume * Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(intensity01)));
        }

        /// <summary>Cut the riser short — for a wave that launches early or a run that ends mid-hold.</summary>
        public void StopRiser()
        {
            if (_riserVoice != null)
            {
                _riserVoice.Stop();
            }
        }

        /// <summary>
        /// Play the <paramref name="degree"/>-th note of the pentatonic ladder. Walk the degree upward
        /// across a burst of eliminations and the result is a rising cascade — the satisfying payoff
        /// note the knocks alone don't provide. Degrees wrap, so a caller can pass a raw running count.
        /// </summary>
        public void PlayChime(int degree)
        {
            if (_chimeLadder == null || _chimeLadder.Length == 0)
            {
                return;
            }

            AudioClip clip = _chimeLadder[((degree % _chimeLadder.Length) + _chimeLadder.Length) % _chimeLadder.Length];

            AudioSource source = _chimeVoices[_nextChimeVoice];
            _nextChimeVoice = (_nextChimeVoice + 1) % _chimeVoices.Length;
            source.pitch = 1f;
            source.PlayOneShot(clip, chimeVolume);
        }

        public void StartAmbientBed()
        {
            if (_ambientVoice != null && _ambientClip != null && !_ambientVoice.isPlaying)
            {
                _ambientVoice.Play();
            }
        }

        public void StopAmbientBed()
        {
            if (_ambientVoice != null)
            {
                _ambientVoice.Stop();
            }
        }
    }
}
