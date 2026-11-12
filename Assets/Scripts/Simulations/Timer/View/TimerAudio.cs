using System;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.Timer
{
    [Serializable]
    public sealed class TimerAudioSettings
    {
        [Range(0f, 1f)] public float tickVolume = 0.45f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        public float rootHz = 261.63f;

        [Header("Ticks")]
        [Tooltip("Ticks per second at 10-5 s remaining, then under 5 s.")]
        public int urgentSubdivisions = 2;
        public int finalSubdivisions = 4;
        [Min(1f)] public float urgentSeconds = 10f;
        public float riserSeconds = 2.5f;

        [Header("Collector plinks")]
        [Range(0f, 1f)] public float plinkVolume = 0.12f;
        [Tooltip("At most this many plinks per second (real time).")]
        public float plinkMaxPerSecond = 2f;
        public int plinkNotes = 10;

        [Header("0:00")]
        [Range(0f, 1f)] public float bellVolume = 0.85f;
        public int bellCount = 3;
        public float bellSpacing = 0.22f;

        [Header("All in (every marble collected)")]
        [Range(0f, 1f)] public float fanfareVolume = 0.7f;
        [Tooltip("Gap between arpeggio notes, seconds (real time).")]
        public float arpeggioSpacing = 0.14f;
        [Range(0f, 1f)] public float softBellVolume = 0.3f;
    }

    /// <summary>
    /// Procedural sound for <c>timer</c>, all from ProceduralToneBank: ticks keyed to the simulated clock (never
    /// wall time), gentle sine plinks (at most 2 a second) as marbles reach the collector, a release
    /// whoosh, and a cartoon bell with a cheer-like swell at 0:00. Reads the run; never writes it.
    /// </summary>
    public sealed class TimerAudio : MonoBehaviour
    {
        const int UrgentBank = 10;

        [SerializeField] TimerSimulation sim;
        [SerializeField] TimerAudioSettings settings = new TimerAudioSettings();

        AudioSource _tickSource;
        AudioSource _sfxSource;
        AudioSource _plinkSource;

        AudioClip _tick;
        AudioClip _tock;
        AudioClip _bigTock;
        AudioClip[] _urgent;
        AudioClip[] _plinks;
        AudioClip _bell;
        AudioClip _bellHigh;
        AudioClip _swell;
        AudioClip _cheer;
        AudioClip _whoosh;
        AudioClip _riser;
        AudioClip[] _arp;
        AudioClip[] _chord;
        AudioClip _allInCheer;
        AudioClip _allInSwell;

        float _nextTick;
        float _lastElapsed;
        bool _wasReleased;
        bool _risen;
        bool _finaleDone;
        int _bellsLeft;
        float _nextBellAt;
        bool _allInDone;
        int _arpPlayed;
        float _nextArpAt;
        int _plinkSeen;
        int _plinkNote;
        float _plinkClock;
        float _nextPlinkAt;

        public void Bind(TimerSimulation simulation)
        {
            sim = simulation;
        }

        void Start()
        {
            float root = settings.rootHz;
            _tick = ProceduralToneBank.CreateClick(1f, 7, "TimerTick");
            _tock = ProceduralToneBank.CreateImpactThud(1.4f, 0.14f, 31, "TimerTock");
            _bigTock = ProceduralToneBank.CreateImpactThud(0.85f, 0.32f, 32, "TimerBigTock");
            _urgent = new AudioClip[UrgentBank];
            for (int i = 0; i < UrgentBank; i++)
            {
                _urgent[i] = ProceduralToneBank.CreateClick(1.1f + 0.1f * i, 40 + i, "TimerUrgent" + i);
            }

            int notes = Mathf.Max(3, settings.plinkNotes);
            _plinks = ProceduralToneBank.CreateScale(notes, root * 2f, 0.55f);
            _bell = ProceduralToneBank.CreateTone(ProceduralToneBank.DegreeToFrequency(root * 2f, 5), 1.8f, "TimerBell");
            _bellHigh = ProceduralToneBank.CreateTone(ProceduralToneBank.DegreeToFrequency(root * 2f, 8), 2.2f, "TimerBellHigh");
            _swell = ProceduralToneBank.CreateRiser(1.6f, 220f, 620f, 11, "TimerSwell");
            _cheer = ProceduralToneBank.CreateWhoosh(2.2f, 5, "TimerCheer");
            _whoosh = ProceduralToneBank.CreateWhoosh(0.9f, 2, "ReleaseWhoosh");
            _riser = ProceduralToneBank.CreateRiser(settings.riserSeconds, 80f, 260f, 7, "SetupRiser");

            // Fanfare: a rising C-major arpeggio (1, 3, 5, 8 of the major scale, just intonation) and a held chord, about 2 s.
            float high = root * 2f;
            float[] arpRatios = { 1f, 1.25f, 1.5f, 2f };
            _arp = new AudioClip[arpRatios.Length];
            for (int i = 0; i < arpRatios.Length; i++)
            {
                _arp[i] = ProceduralToneBank.CreateTone(high * arpRatios[i], 0.7f, "TimerFanfareArp" + i);
            }

            float[] chordRatios = { 1f, 1.25f, 1.5f, 2f };
            _chord = new AudioClip[chordRatios.Length];
            for (int i = 0; i < chordRatios.Length; i++)
            {
                _chord[i] = ProceduralToneBank.CreateTone(high * 2f * chordRatios[i] * 0.5f, 1.6f, "TimerFanfareChord" + i);
            }

            _allInCheer = ProceduralToneBank.CreateWhoosh(2.6f, 9, "TimerAllInCheer");
            _allInSwell = ProceduralToneBank.CreateRiser(1.2f, 300f, 900f, 13, "TimerAllInSwell");

            _tickSource = gameObject.AddComponent<AudioSource>();
            _tickSource.playOnAwake = false;
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _plinkSource = gameObject.AddComponent<AudioSource>();
            _plinkSource.playOnAwake = false;

            ResetState();
        }

        void ResetState()
        {
            _nextTick = 1f;
            _lastElapsed = 0f;
            _wasReleased = false;
            _risen = false;
            _finaleDone = false;
            _bellsLeft = 0;
            _allInDone = false;
            _arpPlayed = 0;
            _plinkSeen = 0;
            _plinkNote = 0;
            _plinkClock = 0f;
            _nextPlinkAt = 0f;
        }

        void Update()
        {
            if (sim == null || _tickSource == null)
            {
                return;
            }

            ITimerState state = sim;
            if (state.Elapsed + 0.5f < _lastElapsed || (_wasReleased && !state.Released))
            {
                ResetState();
            }

            _plinkClock += Time.unscaledDeltaTime;

            if (!state.Released)
            {
                UpdateRiser(state);
                return;
            }

            if (!_wasReleased)
            {
                _wasReleased = true;
                _sfxSource.PlayOneShot(_whoosh, settings.sfxVolume);
            }

            UpdateTicks(state);
            UpdatePlinks(state);
            UpdateFinale(state);
            _lastElapsed = state.Elapsed;
        }

        void UpdateRiser(ITimerState state)
        {
            if (_risen || state.SetupRemaining <= 0f || state.SetupRemaining > settings.riserSeconds)
            {
                return;
            }

            _risen = true;
            _sfxSource.PlayOneShot(_riser, settings.sfxVolume * 0.5f);
        }

        void UpdateTicks(ITimerState state)
        {
            float duration = state.Duration;
            float elapsed = state.Elapsed;
            float lastAt = -1f;
            while (_nextTick <= elapsed + 1e-4f && _nextTick < duration - 1e-3f)
            {
                lastAt = _nextTick;
                _nextTick += 1f / SubdivisionsAt(duration - lastAt);
            }

            if (lastAt >= 0f)
            {
                PlayTick(lastAt, duration);
            }
        }

        int SubdivisionsAt(float remaining)
        {
            if (remaining > settings.urgentSeconds)
            {
                return 1;
            }

            return remaining > settings.urgentSeconds * 0.5f
                ? Mathf.Max(1, settings.urgentSubdivisions)
                : Mathf.Max(1, settings.finalSubdivisions);
        }

        void PlayTick(float at, float duration)
        {
            float remaining = duration - at;
            int whole = Mathf.RoundToInt(at);
            bool onSecond = Mathf.Abs(at - whole) < 1e-3f;

            if (onSecond && whole % 60 == 0)
            {
                _tickSource.PlayOneShot(_bigTock, settings.tickVolume * 1.3f);
            }
            else if (onSecond && whole % 10 == 0)
            {
                _tickSource.PlayOneShot(_tock, settings.tickVolume * 1.1f);
            }
            else if (remaining <= settings.urgentSeconds)
            {
                float u = Mathf.Clamp01(1f - remaining / settings.urgentSeconds);
                int k = Mathf.Clamp(Mathf.FloorToInt(u * UrgentBank), 0, UrgentBank - 1);
                _tickSource.PlayOneShot(_urgent[k], settings.tickVolume * Mathf.Lerp(0.9f, 1.4f, u));
            }
            else
            {
                _tickSource.PlayOneShot(_tick, settings.tickVolume);
            }
        }

        // One soft pentatonic plink per marble that reaches the collector, rate-limited so a burst becomes a shimmer.
        void UpdatePlinks(ITimerState state)
        {
            int collected = state.CollectedCount;
            if (collected < _plinkSeen)
            {
                _plinkSeen = collected;
            }

            if (collected == _plinkSeen || _plinkClock < _nextPlinkAt)
            {
                return;
            }

            _plinkSeen = collected;
            _nextPlinkAt = _plinkClock + 1f / Mathf.Max(1f, settings.plinkMaxPerSecond);
            _plinkNote = (_plinkNote * 5 + 3 + collected) % _plinks.Length;
            _plinkSource.PlayOneShot(_plinks[_plinkNote], settings.plinkVolume * (0.7f + 0.3f * state.Fill));
        }

        void UpdateFinale(ITimerState state)
        {
            UpdateAllIn(state);

            bool finished = state.Phase == TimerPhase.Finale || state.Phase == TimerPhase.Done || state.Remaining <= 0f;
            if (!_finaleDone && finished)
            {
                _finaleDone = true;
                _nextBellAt = _plinkClock;
                if (_allInDone)
                {
                    // The celebration already happened: only one soft bell at 0:00.
                    _bellsLeft = 1;
                }
                else
                {
                    _bellsLeft = Mathf.Max(1, settings.bellCount);
                    _sfxSource.PlayOneShot(_swell, settings.sfxVolume * 0.5f);
                    _sfxSource.PlayOneShot(_cheer, settings.sfxVolume * 0.45f);
                }
            }

            if (_bellsLeft > 0 && _plinkClock >= _nextBellAt)
            {
                int index = Mathf.Max(1, settings.bellCount) - _bellsLeft;
                if (_allInDone)
                {
                    _bellsLeft = 0;
                    _sfxSource.PlayOneShot(_bell, settings.softBellVolume);
                    return;
                }

                _bellsLeft--;
                _nextBellAt = _plinkClock + settings.bellSpacing;
                AudioClip clip = index % 2 == 0 ? _bell : _bellHigh;
                _sfxSource.PlayOneShot(clip, settings.bellVolume);
            }
        }

        // Every marble in the bucket: a swell and cheer, a rising arpeggio, then a chord.
        void UpdateAllIn(ITimerState state)
        {
            if (!_allInDone)
            {
                if (state.TotalMarbles <= 0 || state.CollectedCount < state.TotalMarbles)
                {
                    return;
                }

                _allInDone = true;
                _arpPlayed = 0;
                _nextArpAt = _plinkClock;
                _sfxSource.PlayOneShot(_allInSwell, settings.sfxVolume * 0.4f);
                _sfxSource.PlayOneShot(_allInCheer, settings.sfxVolume * 0.5f);
            }

            if (_arpPlayed > _arp.Length || _plinkClock < _nextArpAt)
            {
                return;
            }

            if (_arpPlayed < _arp.Length)
            {
                _sfxSource.PlayOneShot(_arp[_arpPlayed], settings.fanfareVolume);
                _arpPlayed++;
                _nextArpAt = _plinkClock + Mathf.Max(0.05f, settings.arpeggioSpacing) * (_arpPlayed == _arp.Length ? 1.6f : 1f);
            }
            else
            {
                for (int i = 0; i < _chord.Length; i++)
                {
                    _sfxSource.PlayOneShot(_chord[i], settings.fanfareVolume * 0.55f);
                }

                _arpPlayed++;
            }
        }
    }
}
