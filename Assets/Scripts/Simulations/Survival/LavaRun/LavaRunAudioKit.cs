using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.Survival.LavaRun
{
    /// <summary>
    /// Every sound Lava Run makes, generated at runtime with <see cref="ProceduralToneBank"/>: nothing sampled.
    /// Owns its AudioSources (created in code). Presentation only: the presenter calls the methods below.
    /// </summary>
    /// <remarks>
    /// Throttles use real (unscaled) time and a deterministic counter for pitch jitter, so a re-render sounds
    /// like the take it came from.
    /// </remarks>
    public sealed class LavaRunAudioKit : MonoBehaviour
    {
        [Header("Volumes")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float rumbleVolume = 0.55f;
        [Range(0f, 1f)] public float ambientVolume = 0.12f;
        [Range(0f, 1f)] public float gateVolume = 0.2f;
        [Range(0f, 1f)] public float deathVolume = 0.18f;
        [Range(0f, 1f)] public float eruptionVolume = 0.95f;
        [Range(0f, 1f)] public float doorVolume = 0.9f;
        [Range(0f, 1f)] public float ringVolume = 0.8f;
        [Range(0f, 1f)] public float sweeperVolume = 0.35f;
        [Range(0f, 1f)] public float eliminationVolume = 0.8f;
        [Range(0f, 1f)] public float winVolume = 0.7f;
        [Range(0f, 1f)] public float tickVolume = 0.4f;

        [Header("Throttles")]
        [Min(1f)] public float maxGatesPerSecond = 22f;
        [Min(1f)] public float maxDeathsPerSecond = 14f;

        [Header("Tuning")]
        [Tooltip("Lava gap (metres) at which the rumble is at its floor; it rises to full as the gap closes to zero.")]
        [Min(1f)] public float rumbleFarMeters = 30f;
        [Tooltip("Sweeper speed (degrees/s) that counts as full whoosh.")]
        [Min(1f)] public float sweeperFullDegrees = 70f;
        [Min(1)] public int voices = 16;

        const int ChimeDegrees = 14;

        AudioSource[] _voices;
        int _nextVoice;
        AudioSource[] _winVoices;
        AudioSource _rumble;
        AudioSource _bed;
        AudioSource _sweeper;

        AudioClip[] _chimes;
        AudioClip[] _thuds;
        AudioClip _hiss;
        AudioClip _sizzle;
        AudioClip _boom;
        AudioClip _boomNoise;
        AudioClip _doorThud;
        AudioClip _doorCrack;
        AudioClip _ringCrack;
        AudioClip _ringThud;
        AudioClip _stingBoom;
        AudioClip _stingShatter;
        AudioClip _tick;
        AudioClip _go;
        AudioClip[] _chord;

        float _lastGate = -10f;
        float _lastDeath = -10f;
        float _rumbleTarget;
        float _rumbleLevel;
        float _sweeperTarget;
        float _sweeperPitchTarget = 1f;
        int _counter;
        bool _built;

        /// <summary>Creates the clips and sources. Called from the presenter's Start.</summary>
        public void Build()
        {
            if (_built)
            {
                return;
            }

            _built = true;

            _chimes = ProceduralToneBank.CreateScale(ChimeDegrees, 220f, 0.9f);
            _thuds = ProceduralToneBank.CreateThudBank(3, 1.1f, 0.16f, 3100);
            _hiss = ProceduralToneBank.CreateWhoosh(0.4f, 301, "LavaHiss");
            _sizzle = ProceduralToneBank.CreateSquelch(0.6f, 302, "LavaSizzle");
            _boom = ProceduralToneBank.CreateImpactThud(0.22f, 1.2f, 303, "EruptionBoom");
            _boomNoise = ProceduralToneBank.CreateShatter(1.4f, 304, "EruptionRoar");
            _doorThud = ProceduralToneBank.CreateImpactThud(0.35f, 0.7f, 305, "DoorSlam");
            _doorCrack = ProceduralToneBank.CreateClick(0.6f, 306, "DoorClang");
            _ringCrack = ProceduralToneBank.CreateShatter(1.1f, 307, "RingCrack");
            _ringThud = ProceduralToneBank.CreateImpactThud(0.3f, 0.9f, 308, "RingGroan");
            _stingBoom = ProceduralToneBank.CreateImpactThud(0.4f, 0.55f, 309, "EliminationBoom");
            _stingShatter = ProceduralToneBank.CreateShatter(0.9f, 310, "EliminationShatter");
            _tick = ProceduralToneBank.CreateTone(660f, 0.2f, "CountTick");
            _go = ProceduralToneBank.CreateTone(1320f, 0.5f, "CountGo");
            _chord = ProceduralToneBank.CreateScale(6, 392f, 1.6f);

            _voices = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = NewSource();
            }

            _winVoices = new AudioSource[_chord.Length];
            for (int i = 0; i < _winVoices.Length; i++)
            {
                _winVoices[i] = NewSource();
            }

            _bed = NewSource();
            _bed.clip = ProceduralToneBank.CreateAmbientBed(8f, 49f, "LavaBed");
            _bed.loop = true;
            _bed.volume = ambientVolume * masterVolume;
            _bed.Play();

            _rumble = NewSource();
            _rumble.clip = ProceduralToneBank.CreateRumbleLoop(3f, 311, "LavaRumble");
            _rumble.loop = true;
            _rumble.volume = 0f;
            _rumble.pitch = 0.75f;
            _rumble.Play();

            _sweeper = NewSource();
            _sweeper.clip = ProceduralToneBank.CreateRumbleLoop(2f, 312, "SweeperWhoosh");
            _sweeper.loop = true;
            _sweeper.volume = 0f;
            _sweeper.Play();
        }

        AudioSource NewSource()
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        void Update()
        {
            if (!_built)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            _bed.volume = ambientVolume * masterVolume;

            _rumbleLevel = Mathf.MoveTowards(_rumbleLevel, _rumbleTarget, dt * 0.6f);
            _rumble.volume = _rumbleLevel * rumbleVolume * masterVolume;
            _rumble.pitch = Mathf.Lerp(0.7f, 1.05f, _rumbleLevel);

            _sweeper.volume = Mathf.MoveTowards(_sweeper.volume, _sweeperTarget * sweeperVolume * masterVolume, dt * 0.5f);
            _sweeper.pitch = Mathf.Lerp(_sweeper.pitch, _sweeperPitchTarget, dt * 2f);
        }

        void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (!_built || clip == null)
            {
                return;
            }

            AudioSource source = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume * masterVolume);
            source.clip = clip;
            source.Play();
        }

        float Jitter()
        {
            _counter++;
            return 1f + (((_counter * 37) % 11) - 5) * 0.012f;
        }

        static bool Throttle(ref float last, float perSecond)
        {
            float now = Time.unscaledTime;
            if (now - last < 1f / perSecond)
            {
                return false;
            }

            last = now;
            return true;
        }

        /// <summary>Looping lava rumble: silent while <paramref name="active"/> is false, louder as the gap closes.</summary>
        public void SetLavaGap(bool active, float gapMeters)
        {
            _rumbleTarget = active ? Mathf.Clamp01(1f - gapMeters / rumbleFarMeters) * 0.85f + 0.15f : 0f;
        }

        /// <summary>Constant low rumble for the lake (island phase), independent of any gap.</summary>
        public void SetLakeRumble(float level01)
        {
            _rumbleTarget = Mathf.Clamp01(level01);
        }

        /// <summary>Gate chime. Pitch climbs the scale with log2 of the colour's live count.</summary>
        public void PlayGate(int aliveCount, int multiplier)
        {
            if (!_built || !Throttle(ref _lastGate, maxGatesPerSecond))
            {
                return;
            }

            int degree = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(Mathf.Max(1, aliveCount), 2f)), 0, ChimeDegrees - 1);
            float volume = gateVolume * (multiplier >= 3 ? 1.3f : 1f);
            Play(_chimes[degree], volume);
        }

        /// <summary>Hiss plus a soft thud: a marble lost to lava or a fall.</summary>
        public void PlayDeath(bool lava)
        {
            if (!_built || !Throttle(ref _lastDeath, maxDeathsPerSecond))
            {
                return;
            }

            _counter++;
            Play(_hiss, deathVolume, Jitter() * (lava ? 1.15f : 1.4f));
            Play(_thuds[_counter % _thuds.Length], deathVolume * 0.7f, Jitter());
        }

        /// <summary>Slower, bigger sizzle for a colour's marbles sinking out at the door.</summary>
        public void PlaySink()
        {
            Play(_sizzle, deathVolume * 1.4f, 0.8f);
        }

        public void PlayEruption()
        {
            Play(_boom, eruptionVolume);
            Play(_boomNoise, eruptionVolume * 0.6f);
        }

        public void PlayDoorSlam()
        {
            Play(_doorThud, doorVolume);
            Play(_doorCrack, doorVolume * 0.7f);
        }

        public void PlayRingCrack()
        {
            Play(_ringCrack, ringVolume);
            Play(_ringThud, ringVolume * 0.8f);
        }

        /// <summary>Sweeper arm whoosh: loudness and pitch follow its speed. 0 silences it.</summary>
        public void SetSweeper(float degreesPerSecond)
        {
            float u = Mathf.Clamp01(degreesPerSecond / sweeperFullDegrees);
            _sweeperTarget = degreesPerSecond > 0.01f ? Mathf.Lerp(0.3f, 1f, u) : 0f;
            _sweeperPitchTarget = Mathf.Lerp(0.8f, 1.5f, u);
        }

        public void PlayElimination()
        {
            Play(_stingBoom, eliminationVolume);
            Play(_stingShatter, eliminationVolume * 0.5f);
        }

        public void PlayCountdownTick(bool go)
        {
            Play(go ? _go : _tick, tickVolume);
        }

        public void PlayWinner()
        {
            if (!_built)
            {
                return;
            }

            // Dedicated sources so the chord is not stolen by the round-robin pool.
            double start = AudioSettings.dspTime + 0.05d;
            int[] notes = { 0, 2, 4, 5 };
            for (int i = 0; i < notes.Length; i++)
            {
                AudioSource source = _winVoices[i];
                source.clip = _chord[notes[i]];
                source.volume = Mathf.Clamp01(winVolume * masterVolume);
                source.pitch = 1f;
                source.PlayScheduled(start + i * 0.03d);
            }
        }
    }
}
