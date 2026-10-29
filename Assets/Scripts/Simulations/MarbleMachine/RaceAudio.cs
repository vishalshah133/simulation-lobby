using System.Collections.Generic;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// All sound for the mach-002 lap race, procedural and nothing sampled: a clatter bed driven by how fast the racers are moving
    /// near the camera, a countdown beep and GO tone, a lap-complete chime, a thump and whoosh when the cannon recycles a racer,
    /// a swish on every lead change, and a winner chord with applause. Events fire in <c>FixedUpdate</c>, so they are recorded into
    /// small rings and played from <c>Update</c>. Reads simulation state only.
    /// </summary>
    public sealed class RaceAudio : MonoBehaviour
    {
        const int RingSize = 32;
        const int SampleRate = 44100;

        [Header("Wiring")]
        [SerializeField] MonoBehaviour _simulation; // must implement IRaceState
        [SerializeField] Transform _listener;

        [Header("Clatter")]
        [Range(0f, 2f)] public float clatterGain = 0.3f;
        [Tooltip("A racer slower than this adds nothing to the clatter (m/s).")]
        [Min(0.1f)] public float movingSpeed = 0.5f;
        [Tooltip("Speed at which a racer counts as full activity (m/s).")]
        [Min(0.5f)] public float fullSpeed = 5f;
        [Min(1f)] public float falloffDistance = 9f;
        [Tooltip("Clicks per second added per unit of weighted racer activity (compressed towards 3000).")]
        [Min(0.0001f)] public float densitySlope = 0.012f;
        [Min(0.5f)] public float rampPerSecond = 3f;
        [Tooltip("Low = muffled, soft clicks. 0.22 keeps the clatter warm rather than rattly.")]
        [Range(0f, 1f)] public float brightness = 0.22f;
        [Tooltip("A one-frame move longer than this (the cannon) is a teleport, not speed.")]
        [Min(0.5f)] public float teleportMetres = 3f;

        [Header("Cues")]
        [Min(0.5f)] public float countdownSeconds = 3f;
        [Range(0f, 1f)] public float beepVolume = 0.3f;
        [Range(0f, 1f)] public float lapVolume = 0.35f;
        [Range(0f, 1f)] public float cannonVolume = 0.28f;
        [Range(0f, 1f)] public float pulseVolume = 0.14f;
        [Range(0f, 1f)] public float swishVolume = 0.08f;
        [Range(0f, 1f)] public float finishVolume = 0.25f;
        [Range(0f, 1f)] public float winVolume = 0.32f;
        [Min(0f)] public float stationRetriggerSeconds = 0.3f;
        [Tooltip("Level of the slow ambient pad that runs under the whole race.")]
        [Range(0f, 0.5f)] public float padVolume = 0.12f;
        [Min(0.5f)] public float padFadeInSeconds = 4f;
        [Min(1)] public int maxEventsPerFrame = 3;

        IRaceState _state;
        ClatterBed _clatter;
        AudioSource _notes;
        AudioSource _oneShots;
        AudioSource _padLow;
        AudioSource _padHigh;
        float _padLevel;
        bool _started;

        // Clips.
        AudioClip[] _scale;
        AudioClip _beep;
        AudioClip _goTone;
        AudioClip _thud;
        AudioClip _deepThud;
        AudioClip _clack;
        AudioClip _slosh;
        AudioClip _shortWhoosh;
        AudioClip _swish;
        AudioClip _click;
        AudioClip[] _chord;
        AudioClip _applause;

        // Clatter activity.
        Vector3[] _lastPos;
        bool _hasLast;
        float _density;
        float _brightness = 0.5f;
        float _swell;

        // Station pulses.
        MachineStation[] _stations;
        float[] _lastPulse;
        readonly List<System.Action<MachineStation, Vector3, float>> _handlers = new List<System.Action<MachineStation, Vector3, float>>();
        readonly int[] _pulseStation = new int[RingSize];
        readonly Vector3[] _pulsePos = new Vector3[RingSize];
        readonly float[] _pulseStrength = new float[RingSize];
        int _pulseHead;
        int _pulseTail;
        bool[] _skipPulse;
        bool[] _gatePulse;
        bool[] _tipPulse;

        // Recycled (racer, position).
        readonly Vector3[] _recyclePos = new Vector3[RingSize];
        int _recycleHead;
        int _recycleTail;

        // Lap completions (lap) and finishes (place).
        readonly int[] _lapRacer = new int[RingSize];
        readonly int[] _lapNumber = new int[RingSize];
        int _lapHead;
        int _lapTail;
        readonly int[] _finishPlace = new int[RingSize];
        int _finishHead;
        int _finishTail;
        int _bestLapChimed;

        // Phase and lead.
        RacePhase _recordedPhase = RacePhase.Grid;
        RacePhase _handledPhase = RacePhase.Grid;
        float _phaseStartSimTime;
        int _beepsPlayed;
        int _swishes;
        int _swishesPlayed;
        float _lastSwish = -10f;
        int _wonRacer = -1;
        bool _winPlayed;
        float _winTime;
        int _chordIndex;

        public void Bind(MonoBehaviour simulation, Transform listener)
        {
            _simulation = simulation;
            _listener = listener;
        }

        void Start()
        {
            _state = _simulation as IRaceState;
            if (_listener == null && Camera.main != null)
            {
                _listener = Camera.main.transform;
            }

            BuildClips();
            BuildSources();

            if (_state == null)
            {
                return;
            }

            _state.PhaseChanged += OnPhaseChanged;
            _state.LeaderChanged += OnLeaderChanged;
            _state.LapCompleted += OnLapCompleted;
            _state.RacerFinished += OnRacerFinished;
            _state.Won += OnWon;
            _state.Recycled += OnRecycled;
            _started = true;
            SetupStations();
        }

        void OnDestroy()
        {
            if (_state != null)
            {
                _state.PhaseChanged -= OnPhaseChanged;
                _state.LeaderChanged -= OnLeaderChanged;
                _state.LapCompleted -= OnLapCompleted;
                _state.RacerFinished -= OnRacerFinished;
                _state.Won -= OnWon;
                _state.Recycled -= OnRecycled;
            }

            if (_stations != null)
            {
                for (int i = 0; i < _stations.Length; i++)
                {
                    if (_stations[i] != null && i < _handlers.Count)
                    {
                        _stations[i].Pulse -= _handlers[i];
                    }
                }
            }
        }

        // Setup -----------------------------------------------------------------------------------------------------

        void BuildSources()
        {
            var bed = new GameObject("ClatterBed");
            bed.transform.SetParent(transform, false);
            _clatter = bed.AddComponent<ClatterBed>();
            _notes = MakeSource("Notes");
            _oneShots = MakeSource("OneShots");

            // A slow open-fifth pad (C + G, E + B: a soft major seventh) that loops under the whole race.
            _padLow = MakeSource("PadLow");
            _padLow.clip = ProceduralToneBank.CreateAmbientBed(8f, 130.81f, "RacePadLow");
            _padLow.loop = true;
            _padLow.volume = 0f;
            _padLow.Play();
            _padHigh = MakeSource("PadHigh");
            _padHigh.clip = ProceduralToneBank.CreateAmbientBed(8f, 164.81f, "RacePadHigh");
            _padHigh.loop = true;
            _padHigh.volume = 0f;
            _padHigh.Play();
        }

        AudioSource MakeSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        void BuildClips()
        {
            // Soft bells on a major pentatonic scale (C D E G A C D): nothing here can clash, whatever the notes land on.
            float[] pentatonic = { 261.63f, 293.66f, 329.63f, 392f, 440f, 523.25f, 587.33f };
            _scale = new AudioClip[pentatonic.Length];
            for (int i = 0; i < pentatonic.Length; i++)
            {
                _scale[i] = BuildBell(pentatonic[i], 1.8f, "RaceBell" + i);
            }

            _beep = BuildBell(392f, 0.55f, "RaceBeep");
            _goTone = BuildBell(523.25f, 1.6f, "RaceGo");
            _thud = ProceduralToneBank.CreateImpactThud(0.65f, 0.24f, 11, "RaceThud");
            _deepThud = ProceduralToneBank.CreateImpactThud(0.4f, 0.4f, 12, "RaceCannonThud");
            _clack = ProceduralToneBank.CreateImpactThud(0.85f, 0.14f, 13, "RaceClack");
            _slosh = ProceduralToneBank.CreateWhoosh(0.4f, 14, "RaceSlosh");
            _shortWhoosh = ProceduralToneBank.CreateWhoosh(0.7f, 15, "RaceWhoosh");
            _swish = ProceduralToneBank.CreateWhoosh(0.3f, 18, "RaceSwish");
            _click = ProceduralToneBank.CreateClick(0.8f, 17, "RaceClick");

            // Rising major arpeggio of bells: C5 E5 G5 C6.
            _chord = new[]
            {
                BuildBell(523.25f, 3f, "WinC5"),
                BuildBell(659.25f, 3f, "WinE5"),
                BuildBell(783.99f, 3f, "WinG5"),
                BuildBell(1046.5f, 3.4f, "WinC6")
            };

            _applause = BuildApplause(4.5f, 91);
        }

        void SetupStations()
        {
            IReadOnlyList<MachineStation> list = _state.Stations;
            int n = list != null ? list.Count : 0;
            _stations = new MachineStation[n];
            _lastPulse = new float[n];
            _skipPulse = new bool[n];
            _gatePulse = new bool[n];
            _tipPulse = new bool[n];
            for (int i = 0; i < n; i++)
            {
                MachineStation s = list[i];
                _stations[i] = s;
                _lastPulse[i] = -10f;
                string name = (s.displayName ?? string.Empty).ToLowerInvariant();
                _skipPulse[i] = s is ICannon || name.Contains("cannon"); // the Recycled event covers the cannon
                _gatePulse[i] = s is IStartGate || name.Contains("gate");
                _tipPulse[i] = name.Contains("tipping") || name.Contains("tower") || name.Contains("tipper");

                int captured = i;
                System.Action<MachineStation, Vector3, float> handler = (st, pos, strength) => OnPulse(captured, pos, strength);
                _handlers.Add(handler);
                s.Pulse += handler;
            }
        }

        // Events ----------------------------------------------------------------------------------------------------

        void OnPhaseChanged(RacePhase phase)
        {
            _recordedPhase = phase;
            _phaseStartSimTime = _state.SimTime;
        }

        void OnLeaderChanged(int racer) { _swishes++; }

        void OnLapCompleted(int racer, int lap)
        {
            _lapRacer[_lapHead] = racer;
            _lapNumber[_lapHead] = lap;
            _lapHead = (_lapHead + 1) % RingSize;
            if (_lapHead == _lapTail)
            {
                _lapTail = (_lapTail + 1) % RingSize;
            }
        }

        void OnRacerFinished(int racer, int place)
        {
            _finishPlace[_finishHead] = place;
            _finishHead = (_finishHead + 1) % RingSize;
            if (_finishHead == _finishTail)
            {
                _finishTail = (_finishTail + 1) % RingSize;
            }
        }

        void OnWon(int racer) { _wonRacer = racer; }

        void OnRecycled(int racer, Vector3 position)
        {
            _recyclePos[_recycleHead] = position;
            _recycleHead = (_recycleHead + 1) % RingSize;
            if (_recycleHead == _recycleTail)
            {
                _recycleTail = (_recycleTail + 1) % RingSize;
            }
        }

        void OnPulse(int stationIndex, Vector3 position, float strength)
        {
            _pulseStation[_pulseHead] = stationIndex;
            _pulsePos[_pulseHead] = position;
            _pulseStrength[_pulseHead] = strength;
            _pulseHead = (_pulseHead + 1) % RingSize;
            if (_pulseHead == _pulseTail)
            {
                _pulseTail = (_pulseTail + 1) % RingSize;
            }
        }

        // Per frame -------------------------------------------------------------------------------------------------

        void Update()
        {
            if (!_started || _clatter == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            UpdateClatter(dt);
            UpdatePad(dt);
            UpdateCountdown();
            DrainLaps();
            DrainFinishes();
            DrainRecycles();
            DrainPulses();
            UpdateSwish();
            UpdateWin(dt);
        }

        float Attenuation(Vector3 position, float scale)
        {
            Vector3 cam = _listener != null ? _listener.position : Vector3.zero;
            float d = (position - cam).magnitude / scale;
            return Mathf.Max(0.08f, 1f / (1f + d * d));
        }

        void UpdateClatter(float dt)
        {
            int count = _state.RacerCount;
            if (_lastPos == null || _lastPos.Length != count)
            {
                _lastPos = new Vector3[count];
                _hasLast = false;
            }

            Vector3 cam = _listener != null ? _listener.position : Vector3.zero;
            float activity = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = _state.RacerPosition(i);
                if (_hasLast && dt > 0f)
                {
                    Vector3 delta = p - _lastPos[i];
                    if (delta.sqrMagnitude <= teleportMetres * teleportMetres)
                    {
                        float speed = delta.magnitude / dt;
                        if (speed > movingSpeed)
                        {
                            float d = (p - cam).magnitude / falloffDistance;
                            activity += Mathf.Clamp01(speed / fullSpeed) / (1f + d * d);
                        }
                    }
                }

                _lastPos[i] = p;
            }

            _hasLast = true;

            _swell = Mathf.MoveTowards(_swell, 0f, dt * 1.5f);
            float targetDensity = 3000f * (1f - Mathf.Exp(-activity * densitySlope)) + _swell * 500f;
            float k = 1f - Mathf.Exp(-rampPerSecond * dt);
            _density += (targetDensity - _density) * k;
            _brightness += (brightness - _brightness) * k;

            _clatter.Density = _density;
            _clatter.Brightness = _brightness;
            _clatter.Gain = clatterGain;
        }

        void UpdatePad(float dt)
        {
            _padLevel = Mathf.MoveTowards(_padLevel, padVolume, padVolume * dt / Mathf.Max(0.5f, padFadeInSeconds));
            if (_padLow != null)
            {
                _padLow.volume = _padLevel;
            }

            if (_padHigh != null)
            {
                _padHigh.volume = _padLevel * 0.8f;
            }
        }

        void UpdateCountdown()
        {
            if (_recordedPhase != _handledPhase)
            {
                RacePhase previous = _handledPhase;
                _handledPhase = _recordedPhase;
                if (_handledPhase == RacePhase.Countdown)
                {
                    _beepsPlayed = 0;
                }
                else if (_handledPhase == RacePhase.Racing && previous == RacePhase.Countdown)
                {
                    _oneShots.PlayOneShot(_goTone, beepVolume);
                    _oneShots.PlayOneShot(_shortWhoosh, beepVolume * 0.25f);
                    _swell = 1f;
                }
                else if (_handledPhase == RacePhase.FinalLap)
                {
                    // Two rising notes: the last lap has begun.
                    _notes.PlayOneShot(_scale[4], lapVolume);
                    _notes.PlayOneShot(_scale[6], lapVolume * 0.8f);
                }
            }

            if (_handledPhase == RacePhase.Countdown)
            {
                int due = Mathf.Min(Mathf.CeilToInt(countdownSeconds), Mathf.FloorToInt(_state.SimTime - _phaseStartSimTime) + 1);
                while (_beepsPlayed < due)
                {
                    _oneShots.PlayOneShot(_beep, beepVolume);
                    _beepsPlayed++;
                }
            }
        }

        void DrainLaps()
        {
            int played = 0;
            while (_lapTail != _lapHead)
            {
                int racer = _lapRacer[_lapTail];
                int lap = _lapNumber[_lapTail];
                _lapTail = (_lapTail + 1) % RingSize;
                if (played >= maxEventsPerFrame)
                {
                    continue;
                }

                played++;
                if (lap > _bestLapChimed)
                {
                    // First racer round: the chime, a note higher each lap.
                    _bestLapChimed = lap;
                    int index = Mathf.Clamp(lap + 1, 0, _scale.Length - 1);
                    _notes.PlayOneShot(_scale[index], lapVolume);
                    _notes.PlayOneShot(_scale[Mathf.Min(index + 2, _scale.Length - 1)], lapVolume * 0.6f);
                    _swell = Mathf.Max(_swell, 0.7f);
                }
                else
                {
                    float gain = Attenuation(_state.RacerPosition(racer), 10f) * lapVolume * 0.5f;
                    _oneShots.PlayOneShot(_click, gain);
                }
            }
        }

        void DrainFinishes()
        {
            int played = 0;
            while (_finishTail != _finishHead)
            {
                int place = _finishPlace[_finishTail];
                _finishTail = (_finishTail + 1) % RingSize;
                if (played >= maxEventsPerFrame || place <= 1)
                {
                    continue; // the winner has the chord
                }

                played++;
                _notes.PlayOneShot(_scale[Mathf.Clamp(6 - place, 0, _scale.Length - 1)], finishVolume);
            }
        }

        void DrainRecycles()
        {
            int played = 0;
            while (_recycleTail != _recycleHead)
            {
                Vector3 pos = _recyclePos[_recycleTail];
                _recycleTail = (_recycleTail + 1) % RingSize;
                if (played >= maxEventsPerFrame)
                {
                    continue;
                }

                played++;
                float gain = Attenuation(pos, 10f) * cannonVolume;
                _oneShots.PlayOneShot(_deepThud, gain);
                _oneShots.PlayOneShot(_shortWhoosh, gain * 0.4f);
                _swell = Mathf.Max(_swell, 0.5f);
            }
        }

        void DrainPulses()
        {
            float now = Time.time;
            int played = 0;
            while (_pulseTail != _pulseHead)
            {
                int s = _pulseStation[_pulseTail];
                Vector3 pos = _pulsePos[_pulseTail];
                float strength = _pulseStrength[_pulseTail];
                _pulseTail = (_pulseTail + 1) % RingSize;

                if (played >= maxEventsPerFrame || s < 0 || s >= _stations.Length || _skipPulse[s])
                {
                    continue;
                }

                if (now - _lastPulse[s] < stationRetriggerSeconds)
                {
                    continue;
                }

                _lastPulse[s] = now;
                float gain = Attenuation(pos, 10f) * Mathf.Lerp(0.5f, 1f, strength) * pulseVolume;
                if (_gatePulse[s])
                {
                    _oneShots.PlayOneShot(_clack, gain * 1.4f);
                }
                else if (_tipPulse[s])
                {
                    _oneShots.PlayOneShot(_slosh, gain);
                    _swell = Mathf.Max(_swell, 0.4f);
                }
                else
                {
                    _oneShots.PlayOneShot(_thud, gain * 0.6f);
                }

                played++;
            }
        }

        void UpdateSwish()
        {
            if (_swishes == _swishesPlayed)
            {
                return;
            }

            _swishesPlayed = _swishes;
            float now = Time.time;
            if (_handledPhase >= RacePhase.Racing && now - _lastSwish > 1.5f)
            {
                _lastSwish = now;
                _oneShots.PlayOneShot(_swish, swishVolume);
            }
        }

        void UpdateWin(float dt)
        {
            if (!_winPlayed && (_wonRacer >= 0 || _state.WinnerIndex >= 0))
            {
                _winPlayed = true;
                _winTime = 0f;
                _chordIndex = 0;
                _oneShots.PlayOneShot(_applause, winVolume * 0.5f);
            }

            if (!_winPlayed || _chordIndex >= _chord.Length)
            {
                return;
            }

            _winTime += dt;
            while (_chordIndex < _chord.Length && _winTime >= _chordIndex * 0.24f)
            {
                _notes.PlayOneShot(_chord[_chordIndex], winVolume * 0.7f);
                _chordIndex++;
            }
        }

        // Synthesis -------------------------------------------------------------------------------------------------

        /// <summary>
        /// A soft bell: a fundamental with a faint octave and a faint inharmonic partial, a 12 ms attack (no click) and a smooth
        /// exponential decay.
        /// </summary>
        static AudioClip BuildBell(float frequency, float duration, string clipName)
        {
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            float decay = 5.5f / duration;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float attack = 1f - Mathf.Exp(-t / 0.012f);
                float env = attack * Mathf.Exp(-t * decay);
                float w = 2f * Mathf.PI * frequency * t;
                float v = Mathf.Sin(w) + 0.22f * Mathf.Sin(2f * w) * Mathf.Exp(-t * decay * 0.8f) + 0.07f * Mathf.Sin(2.76f * w) * Mathf.Exp(-t * decay * 1.6f);
                samples[i] = v * env * 0.45f;
            }

            var clip = AudioClip.Create(clipName, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>A crowd cheer built as applause: many short noise claps that swell and fade, over a soft roar.</summary>
        static AudioClip BuildApplause(float duration, int seed)
        {
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            var rng = new System.Random(seed);

            float lowpass = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float swell = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((duration - t) / 1.6f);
                lowpass += ((float)rng.NextDouble() * 2f - 1f - lowpass) * 0.04f;
                samples[i] += lowpass * 0.5f * swell;
            }

            int claps = Mathf.RoundToInt(duration * 160f);
            for (int c = 0; c < claps; c++)
            {
                float t = (float)rng.NextDouble() * duration;
                float swell = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((duration - t) / 1.6f);
                if (rng.NextDouble() > swell)
                {
                    continue;
                }

                int start = Mathf.RoundToInt(t * SampleRate);
                int length = Mathf.RoundToInt(SampleRate * (0.006f + 0.008f * (float)rng.NextDouble()));
                float amp = 0.12f + 0.25f * (float)rng.NextDouble();
                float prev = 0f;
                for (int i = 0; i < length && start + i < count; i++)
                {
                    float n = (float)rng.NextDouble() * 2f - 1f;
                    float high = n - prev * 0.6f;
                    prev = n;
                    samples[start + i] += high * amp * Mathf.Exp(-5f * i / length);
                }
            }

            float max = 0.0001f;
            for (int i = 0; i < count; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(samples[i]));
            }

            float scale = 0.45f / max;
            for (int i = 0; i < count; i++)
            {
                samples[i] *= scale;
            }

            var clip = AudioClip.Create("RaceApplause", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
