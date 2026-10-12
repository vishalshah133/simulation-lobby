using SimulationLobby.Presentation;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Territory
{
    /// <summary>
    /// Every sound the territory war makes, generated at runtime with <see cref="ProceduralToneBank"/>: nothing
    /// sampled, nothing to license. Owns a small round-robin pool of 2D AudioSources plus an ambient bed.
    /// </summary>
    /// <remarks>
    /// Presentation only. Throttles use real (unscaled) time and a deterministic counter for pitch jitter,
    /// so a re-render sounds like the take it came from.
    /// </remarks>
    public sealed class TerritoryAudioKit : MonoBehaviour
    {
        [Header("Volumes")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float flipVolume = 0.16f;
        [Range(0f, 1f)] public float shotVolume = 0.28f;
        [Range(0f, 1f)] public float pegVolume = 0.22f;
        [Range(0f, 1f)] public float slotVolume = 0.5f;
        [Range(0f, 1f)] public float releaseVolume = 0.55f;
        [Range(0f, 1f)] public float reelVolume = 0.35f;
        [Range(0f, 1f)] public float eliminationVolume = 0.8f;
        [Range(0f, 1f)] public float winVolume = 0.7f;
        [Range(0f, 1f)] public float ambientVolume = 0.1f;

        [Header("Throttles")]
        [Min(1f)] public float maxFlipsPerSecond = 25f;
        [Min(1f)] public float maxShotsPerSecond = 30f;
        [Min(1f)] public float maxPegsPerSecond = 40f;

        [Header("Tuning")]
        [Min(0f)] public float minPegSpeed = 0.6f;
        [Tooltip("A release of at least this many shots-worth of ammo also plays the riser.")]
        [Min(1)] public long riserThreshold = 512;
        [Min(1)] public int voices = 14;

        const int ChimeDegrees = 14;

        AudioSource[] _voices;
        int _nextVoice;
        AudioSource _bed;
        AudioSource[] _winVoices;

        AudioClip[] _flip;
        AudioClip[] _thuds;
        AudioClip[] _clinks;
        AudioClip[] _chimes;
        AudioClip _whoosh;
        AudioClip _riser;
        AudioClip _click;
        AudioClip _landLow;
        AudioClip _landHigh;
        AudioClip _shatter;
        AudioClip _boom;
        AudioClip[] _arpeggio;
        AudioClip _bedClip;
        AudioClip _releaseChime;
        AudioClip _spinChime;
        AudioClip _goldLow;
        AudioClip _goldHigh;
        AudioClip _blastBoom;
        AudioClip _blastCrack;
        AudioClip _lineSweep;

        float _lastFlip = -10f;
        float _lastShot = -10f;
        float _lastPeg = -10f;
        int _counter;
        int _thudCursor;
        bool _built;

        /// <summary>Creates the clips and sources. Called from the presenter's Start.</summary>
        public void Build()
        {
            if (_built)
            {
                return;
            }

            _built = true;

            // Per-team flip ticks on their own pentatonic degree, high and short so a swarm reads as a texture.
            _flip = new AudioClip[4];
            int[] degrees = { 5, 7, 9, 11 };
            for (int i = 0; i < _flip.Length; i++)
            {
                float hz = ProceduralToneBank.DegreeToFrequency(261.63f, degrees[i]);
                _flip[i] = ProceduralToneBank.CreateTone(hz, 0.16f, $"Flip_{i}");
            }

            _thuds = ProceduralToneBank.CreateThudBank(4, 1f, 0.2f, 2100);
            _clinks = new AudioClip[4];
            float[] clinkPitches = { 1.0f, 1.12f, 1.26f, 1.5f };
            for (int i = 0; i < _clinks.Length; i++)
            {
                _clinks[i] = ProceduralToneBank.CreateGlassClink(clinkPitches[i], 0.35f, 40 + i, $"Peg_{i}");
            }

            _chimes = ProceduralToneBank.CreateScale(ChimeDegrees, 220f, 0.9f);
            _whoosh = ProceduralToneBank.CreateWhoosh(0.9f, 12, "ReleaseWhoosh");
            _riser = ProceduralToneBank.CreateRiser(1.3f, 70f, 260f, 21, "ReleaseRiser");
            _click = ProceduralToneBank.CreateClick(1f, 7, "ReelClick");
            _landLow = ProceduralToneBank.CreateTone(784f, 1.1f, "ReelLandA");
            _landHigh = ProceduralToneBank.CreateTone(1174.66f, 1.1f, "ReelLandB");
            _shatter = ProceduralToneBank.CreateShatter(1f, 31, "EliminationShatter");
            _boom = ProceduralToneBank.CreateImpactThud(0.45f, 0.55f, 77, "EliminationBoom");
            _arpeggio = ProceduralToneBank.CreateScale(6, 392f, 1.4f);
            _releaseChime = ProceduralToneBank.CreateTone(196f, 0.6f, "ReleaseTick");
            _spinChime = ProceduralToneBank.CreateTone(1318.5f, 0.8f, "SpinTick");
            _goldLow = ProceduralToneBank.CreateTone(1568f, 0.9f, "GoldChimeA");
            _goldHigh = ProceduralToneBank.CreateTone(2093f, 1.1f, "GoldChimeB");
            _blastBoom = ProceduralToneBank.CreateImpactThud(0.3f, 0.7f, 91, "BlastBoom");
            _blastCrack = ProceduralToneBank.CreateShatter(0.8f, 53, "BlastCrack");
            _lineSweep = ProceduralToneBank.CreateRiser(0.4f, 300f, 1200f, 63, "LineSweep");
            _bedClip = ProceduralToneBank.CreateAmbientBed(8f, 55f, "TerritoryBed");

            _voices = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = NewSource();
            }

            _winVoices = new AudioSource[_arpeggio.Length];
            for (int i = 0; i < _winVoices.Length; i++)
            {
                _winVoices[i] = NewSource();
            }

            _bed = NewSource();
            _bed.clip = _bedClip;
            _bed.loop = true;
            _bed.volume = ambientVolume * masterVolume;
            _bed.Play();
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
            if (_bed != null)
            {
                _bed.volume = ambientVolume * masterVolume;
            }
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

        /// <summary>Deterministic jitter in roughly +-6%.</summary>
        float Jitter()
        {
            _counter++;
            return 1f + (((_counter * 37) % 11) - 5) * 0.012f;
        }

        bool Throttle(ref float last, float perSecond)
        {
            float now = Time.unscaledTime;
            if (now - last < 1f / perSecond)
            {
                return false;
            }

            last = now;
            return true;
        }

        public void PlayFlip(int team)
        {
            if (!_built || !Throttle(ref _lastFlip, maxFlipsPerSecond))
            {
                return;
            }

            Play(_flip[Mathf.Clamp(team, 0, _flip.Length - 1)], flipVolume, Jitter());
        }

        public void PlayShot(int team, int health)
        {
            if (!_built || !Throttle(ref _lastShot, maxShotsPerSecond))
            {
                return;
            }

            // Heavier shots thud lower and a little louder.
            float weight = Mathf.Log(Mathf.Max(1, health), 2f);
            float pitch = Mathf.Clamp(1.25f - 0.09f * weight, 0.45f, 1.25f) * Jitter();
            float volume = shotVolume * Mathf.Lerp(0.8f, 1.25f, Mathf.Clamp01(weight / 8f));
            _thudCursor = (_thudCursor + 1) % _thuds.Length;
            Play(_thuds[_thudCursor], volume, pitch);
        }

        public void PlayPeg(float speed)
        {
            if (!_built || speed < minPegSpeed || !Throttle(ref _lastPeg, maxPegsPerSecond))
            {
                return;
            }

            float loudness = Mathf.Clamp01(speed / 6f);
            _counter++;
            AudioClip clip = _clinks[_counter % _clinks.Length];
            Play(clip, pegVolume * Mathf.Lerp(0.35f, 1f, loudness), Jitter());
        }

        public void PlaySlot(SlotEffectKind kind, long bankAfter)
        {
            if (!_built)
            {
                return;
            }

            switch (kind)
            {
                case SlotEffectKind.Multiply:
                case SlotEffectKind.Add:
                    // Rises with log2 of the bank, so a long chain of doublings climbs the scale.
                    int degree = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(Mathf.Max(1L, bankAfter), 2f)), 0, ChimeDegrees - 1);
                    Play(_chimes[degree], slotVolume);
                    break;
                case SlotEffectKind.Release:
                    Play(_releaseChime, slotVolume * 0.7f);
                    break;
                case SlotEffectKind.Bonus:
                    Play(_spinChime, slotVolume * 0.8f);
                    break;
            }
        }

        /// <summary>Bright two-note chime for a ball landing in the SPIN slot.</summary>
        public void PlayGold()
        {
            if (!_built)
            {
                return;
            }

            Play(_goldLow, slotVolume);
            Play(_goldHigh, slotVolume * 0.85f);
        }

        /// <summary>Low boom plus a shatter, for a bomb or a self-destruct.</summary>
        public void PlayBoom()
        {
            if (!_built)
            {
                return;
            }

            Play(_blastBoom, eliminationVolume * 0.8f);
            Play(_blastCrack, eliminationVolume * 0.5f);
        }

        /// <summary>Short rising sweep for a + LINE.</summary>
        public void PlayLine()
        {
            Play(_lineSweep, releaseVolume);
        }

        public void PlayRelease(long amount)
        {
            if (!_built)
            {
                return;
            }

            Play(_whoosh, releaseVolume);
            if (amount >= riserThreshold)
            {
                Play(_riser, releaseVolume * 0.8f);
            }
        }

        public void PlayReelClick()
        {
            Play(_click, reelVolume, Jitter());
        }

        public void PlayReelLand()
        {
            Play(_landLow, reelVolume);
            Play(_landHigh, reelVolume * 0.8f);
        }

        public void PlayElimination()
        {
            Play(_shatter, eliminationVolume);
            Play(_boom, eliminationVolume);
        }

        public void PlayWin()
        {
            if (!_built)
            {
                return;
            }

            // Dedicated sources: a scheduled note must not be stolen by the round-robin pool.
            double start = AudioSettings.dspTime + 0.05d;
            for (int i = 0; i < _arpeggio.Length; i++)
            {
                AudioSource source = _winVoices[i];
                source.clip = _arpeggio[i];
                source.volume = Mathf.Clamp01(winVolume * masterVolume);
                source.pitch = 1f;
                source.PlayScheduled(start + i * 0.14d);
            }
        }
    }
}
