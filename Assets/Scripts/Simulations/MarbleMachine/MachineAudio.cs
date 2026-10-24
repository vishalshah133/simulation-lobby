using System.Collections.Generic;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// All sound for the marble machine, procedural and nothing sampled: a clatter bed that follows ball activity near
    /// the camera, a pentatonic note per sorted ball, a one-shot per station pulse, a quiet pad bed and the win sting.
    /// Events fire in <c>FixedUpdate</c>, so they are recorded into small rings and played from <c>Update</c>.
    /// Reads simulation state only.
    /// </summary>
    public sealed class MachineAudio : MonoBehaviour
    {
        enum Kind
        {
            Other,
            Flood,
            Bucket,
            Trapdoor,
            Tipper,
            Cannon,
            Flap
        }

        const int RingSize = 32;
        const int SampleRate = 44100;

        [Header("Wiring")]
        [SerializeField] MachineSimulation _simulation;
        [SerializeField] Transform _listener;

        [Header("Clatter")]
        [Range(0f, 2f)] public float clatterGain = 0.8f;
        [Min(0.1f)] public float movingSpeed = 0.3f;
        [Min(1f)] public float falloffDistance = 8f;
        [Tooltip("Clicks per second added per unit of weighted ball activity (compressed towards 3000).")]
        [Min(0.0001f)] public float densitySlope = 0.006f;
        [Min(0.5f)] public float rampPerSecond = 3f;

        [Header("Notes and pulses")]
        [Range(0f, 1f)] public float noteVolume = 0.35f;
        [Min(1)] public int maxNotesPerFrame = 3;
        [Min(1)] public int maxPulsesPerFrame = 3;
        [Range(0f, 1f)] public float pulseVolume = 0.55f;
        [Min(0f)] public float stationRetriggerSeconds = 0.08f;

        [Header("Pad and win")]
        [Range(0f, 0.3f)] public float padVolume = 0.05f;
        [Range(0f, 1f)] public float winVolume = 0.5f;
        [Min(1f)] public float padChangeSeconds = 16f;

        IMachineState _state;
        ClatterBed _clatter;
        AudioSource _notes;
        AudioSource _oneShots;
        AudioSource[] _pads;

        // Clatter activity.
        Vector3[] _min;
        Vector3[] _max;
        float[] _stationBrightness;
        Kind[] _kinds;
        float[] _lastPulse;
        int[] _counts;
        MachineStation[] _stations;
        float _density;
        float _brightness = 0.4f;
        float _swell;

        // Clips.
        AudioClip[] _scale;
        AudioClip _thud;
        AudioClip _deepThud;
        AudioClip _clack;
        AudioClip _slosh;
        AudioClip _shortWhoosh;
        AudioClip _floodWhoosh;
        AudioClip _flapClick;
        AudioClip[] _chord;
        AudioClip _applause;
        AudioClip[] _padClips;

        // Event rings (written in FixedUpdate, drained in Update).
        readonly int[] _noteColour = new int[RingSize];
        int _noteHead;
        int _noteTail;

        readonly int[] _pulseStation = new int[RingSize];
        readonly Vector3[] _pulsePos = new Vector3[RingSize];
        readonly float[] _pulseStrength = new float[RingSize];
        int _pulseHead;
        int _pulseTail;

        readonly Dictionary<MachineStation, int> _stationIndex = new Dictionary<MachineStation, int>();
        readonly List<System.Action<MachineStation, Vector3, float>> _handlers = new List<System.Action<MachineStation, Vector3, float>>();

        int _wonColour = -1;
        bool _winPlayed;
        float _winTime;
        int _chordIndex;

        int _padIndex;
        float _padClock;
        float _padFade = 1f;
        bool _started;

        public void Bind(MachineSimulation simulation, Transform listener)
        {
            _simulation = simulation;
            _listener = listener;
        }

        void Start()
        {
            _state = _simulation as IMachineState;
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

            _state.BallSorted += OnBallSorted;
            _state.Won += OnWon;
            _started = true;
            SetupStations();
        }

        void OnDestroy()
        {
            if (_state != null)
            {
                _state.BallSorted -= OnBallSorted;
                _state.Won -= OnWon;
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
            _pads = new[] { MakeSource("PadA"), MakeSource("PadB") };
            for (int i = 0; i < _pads.Length; i++)
            {
                _pads[i].loop = true;
                _pads[i].volume = 0f;
            }

            _padIndex = 0;
            _pads[0].clip = _padClips[0];
            _pads[0].Play();
            _pads[1].clip = _padClips[1];
            _pads[1].Play();
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
            _scale = ProceduralToneBank.CreateScale(7, 261.63f, 1.5f);
            _thud = ProceduralToneBank.CreateImpactThud(0.9f, 0.22f, 11, "MachineThud");
            _deepThud = ProceduralToneBank.CreateImpactThud(0.45f, 0.4f, 12, "MachineDeepThud");
            _clack = ProceduralToneBank.CreateImpactThud(1.8f, 0.1f, 13, "MachineClack");
            _slosh = ProceduralToneBank.CreateWhoosh(0.4f, 14, "MachineSlosh");
            _shortWhoosh = ProceduralToneBank.CreateWhoosh(0.7f, 15, "MachineWhoosh");
            _floodWhoosh = ProceduralToneBank.CreateWhoosh(2.4f, 16, "MachineFloodWhoosh");
            _flapClick = ProceduralToneBank.CreateClick(1.1f, 17, "MachineFlap");

            // Rising major chord: C5 E5 G5 C6.
            _chord = new[]
            {
                ProceduralToneBank.CreateTone(523.25f, 2.2f, "WinC5"),
                ProceduralToneBank.CreateTone(659.25f, 2.2f, "WinE5"),
                ProceduralToneBank.CreateTone(783.99f, 2.2f, "WinG5"),
                ProceduralToneBank.CreateTone(1046.5f, 2.6f, "WinC6")
            };

            _applause = BuildApplause(4.5f, 91);

            // Two slow chords (Cmaj9 and Am9) that crossfade.
            _padClips = new[]
            {
                BuildPad(new[] { 130.81f, 196f, 246.94f, 329.63f, 392f }, 8f, "PadCmaj9"),
                BuildPad(new[] { 110f, 164.81f, 196f, 261.63f, 329.63f }, 8f, "PadAm9")
            };
        }

        void SetupStations()
        {
            IReadOnlyList<MachineStation> list = _state.Stations;
            int n = list != null ? list.Count : 0;
            _stations = new MachineStation[n];
            _min = new Vector3[n];
            _max = new Vector3[n];
            _stationBrightness = new float[n];
            _kinds = new Kind[n];
            _lastPulse = new float[n];
            _counts = new int[n];

            for (int i = 0; i < n; i++)
            {
                MachineStation s = list[i];
                _stations[i] = s;
                _stationIndex[s] = i;
                _min[i] = s.activityBounds.min;
                _max[i] = s.activityBounds.max;
                _lastPulse[i] = -10f;
                Classify(s, out _kinds[i], out _stationBrightness[i]);

                int captured = i;
                System.Action<MachineStation, Vector3, float> handler = (st, pos, strength) => OnPulse(captured, pos, strength);
                _handlers.Add(handler);
                s.Pulse += handler;
            }
        }

        static void Classify(MachineStation s, out Kind kind, out float brightness)
        {
            string name = (s.displayName ?? string.Empty).ToLowerInvariant();
            kind = Kind.Other;
            brightness = 0.35f;

            if (s is ICannon || name.Contains("cannon"))
            {
                kind = Kind.Cannon;
            }
            else if (s is ISilo || name.Contains("silo"))
            {
                kind = Kind.Flood;
                brightness = 0.8f;
            }
            else if (s is ISorter || name.Contains("sorter"))
            {
                kind = Kind.Flap;
                brightness = 0.7f;
            }
            else if (name.Contains("bucket"))
            {
                kind = Kind.Bucket;
                brightness = 0.55f;
            }
            else if (name.Contains("trapdoor"))
            {
                kind = Kind.Trapdoor;
            }
            else if (name.Contains("tipping") || name.Contains("tipper") || name.Contains("tower"))
            {
                kind = Kind.Tipper;
            }

            if (name.Contains("screw") || name.Contains("peg") || name.Contains("tube"))
            {
                brightness = 0.85f;
            }
            else if (name.Contains("vortex") || name.Contains("ski"))
            {
                brightness = 0.6f;
            }
        }

        // Events ----------------------------------------------------------------------------------------------------

        void OnBallSorted(int colour, Vector3 position)
        {
            _noteColour[_noteHead] = colour;
            _noteHead = (_noteHead + 1) % RingSize;
            if (_noteHead == _noteTail)
            {
                _noteTail = (_noteTail + 1) % RingSize;
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

        void OnWon(int colour) { _wonColour = colour; }

        // Per frame -------------------------------------------------------------------------------------------------

        void Update()
        {
            if (!_started || _clatter == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            UpdateClatter(dt);
            DrainNotes();
            DrainPulses();
            UpdatePad(dt);
            UpdateWin(dt);
        }

        void UpdateClatter(float dt)
        {
            int n = _stations.Length;
            for (int i = 0; i < n; i++)
            {
                _counts[i] = 0;
            }

            IReadOnlyList<MachineBall> balls = _state.Balls;
            float minSpeedSqr = movingSpeed * movingSpeed;
            if (balls != null && n > 0)
            {
                for (int b = 0; b < balls.Count; b++)
                {
                    MachineBall ball = balls[b];
                    if (ball == null || ball.counted || ball.body == null)
                    {
                        continue;
                    }

                    Vector3 p = ball.body.position;
                    for (int s = 0; s < n; s++)
                    {
                        Vector3 lo = _min[s];
                        Vector3 hi = _max[s];
                        if (p.x < lo.x || p.x > hi.x || p.y < lo.y || p.y > hi.y || p.z < lo.z || p.z > hi.z)
                        {
                            continue;
                        }

                        if (ball.body.linearVelocity.sqrMagnitude > minSpeedSqr)
                        {
                            _counts[s]++;
                        }

                        break;
                    }
                }
            }

            Vector3 cam = _listener != null ? _listener.position : Vector3.zero;
            float activity = 0f;
            float brightSum = 0f;
            for (int s = 0; s < n; s++)
            {
                if (_counts[s] == 0)
                {
                    continue;
                }

                // Distance to the nearest point of the station's box, so a close camera inside it hears it fully.
                Vector3 closest = new Vector3(
                    Mathf.Clamp(cam.x, _min[s].x, _max[s].x),
                    Mathf.Clamp(cam.y, _min[s].y, _max[s].y),
                    Mathf.Clamp(cam.z, _min[s].z, _max[s].z));
                float d = (closest - cam).magnitude / falloffDistance;
                float w = _counts[s] / (1f + d * d);
                activity += w;
                brightSum += w * _stationBrightness[s];
            }

            _swell = Mathf.MoveTowards(_swell, 0f, dt * 1.5f);
            float targetDensity = 3000f * (1f - Mathf.Exp(-(activity) * densitySlope)) + _swell * 700f;
            float targetBright = activity > 0.01f ? brightSum / activity : 0.4f;

            float k = 1f - Mathf.Exp(-rampPerSecond * dt);
            _density += (targetDensity - _density) * k;
            _brightness += (targetBright - _brightness) * k;

            _clatter.Density = _density;
            _clatter.Brightness = _brightness;
            _clatter.Gain = clatterGain;
        }

        void DrainNotes()
        {
            int played = 0;
            while (_noteTail != _noteHead)
            {
                int colour = _noteColour[_noteTail];
                _noteTail = (_noteTail + 1) % RingSize;
                if (played >= maxNotesPerFrame)
                {
                    continue;
                }

                played++;
                int index = Mathf.Clamp(colour, 0, _scale.Length - 1);
                _notes.PlayOneShot(_scale[index], noteVolume);
            }
        }

        void DrainPulses()
        {
            Vector3 cam = _listener != null ? _listener.position : Vector3.zero;
            float now = Time.time;
            int played = 0;
            while (_pulseTail != _pulseHead)
            {
                int s = _pulseStation[_pulseTail];
                Vector3 pos = _pulsePos[_pulseTail];
                float strength = _pulseStrength[_pulseTail];
                _pulseTail = (_pulseTail + 1) % RingSize;

                if (played >= maxPulsesPerFrame || s < 0 || s >= _kinds.Length)
                {
                    continue;
                }

                if (now - _lastPulse[s] < stationRetriggerSeconds)
                {
                    continue;
                }

                _lastPulse[s] = now;
                float d = (pos - cam).magnitude / 10f;
                float gain = Mathf.Max(0.08f, 1f / (1f + d * d)) * Mathf.Lerp(0.5f, 1f, strength) * pulseVolume;
                PlayPulse(_kinds[s], gain);
                played++;
            }
        }

        void PlayPulse(Kind kind, float gain)
        {
            switch (kind)
            {
                case Kind.Bucket:
                    _oneShots.PlayOneShot(_thud, gain);
                    _swell = Mathf.Max(_swell, 0.6f);
                    break;
                case Kind.Trapdoor:
                    _oneShots.PlayOneShot(_clack, gain);
                    break;
                case Kind.Tipper:
                    _oneShots.PlayOneShot(_slosh, gain);
                    _swell = Mathf.Max(_swell, 0.5f);
                    break;
                case Kind.Cannon:
                    _oneShots.PlayOneShot(_deepThud, gain);
                    _oneShots.PlayOneShot(_shortWhoosh, gain * 0.7f);
                    break;
                case Kind.Flood:
                    _oneShots.PlayOneShot(_floodWhoosh, gain);
                    _swell = 1f;
                    break;
                case Kind.Flap:
                    _oneShots.PlayOneShot(_flapClick, gain * 0.35f);
                    break;
                default:
                    _oneShots.PlayOneShot(_thud, gain * 0.5f);
                    break;
            }
        }

        void UpdatePad(float dt)
        {
            _padClock += dt;
            if (_padClock >= padChangeSeconds)
            {
                _padClock = 0f;
                _padIndex = 1 - _padIndex;
                _padFade = 0f;
            }

            // Crossfade over a third of the interval.
            _padFade = Mathf.MoveTowards(_padFade, 1f, dt / (padChangeSeconds * 0.33f));
            float inVol = Mathf.Sin(_padFade * Mathf.PI * 0.5f) * padVolume;
            float outVol = Mathf.Cos(_padFade * Mathf.PI * 0.5f) * padVolume;
            _pads[_padIndex].volume = inVol;
            _pads[1 - _padIndex].volume = outVol;
        }

        void UpdateWin(float dt)
        {
            if (_wonColour >= 0 && !_winPlayed)
            {
                _winPlayed = true;
                _winTime = 0f;
                _chordIndex = 0;
                _oneShots.PlayOneShot(_applause, winVolume * 0.8f);
            }

            if (!_winPlayed || _chordIndex >= _chord.Length)
            {
                return;
            }

            _winTime += dt;
            while (_chordIndex < _chord.Length && _winTime >= _chordIndex * 0.14f)
            {
                _notes.PlayOneShot(_chord[_chordIndex], winVolume * 0.7f);
                _chordIndex++;
            }
        }

        // Synthesis -------------------------------------------------------------------------------------------------

        /// <summary>A seamless pad loop: sines snapped to whole cycles per loop, each with a slow tremolo.</summary>
        static AudioClip BuildPad(float[] freqs, float duration, string clipName)
        {
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            for (int f = 0; f < freqs.Length; f++)
            {
                float hz = Mathf.Max(1f, Mathf.Round(freqs[f] * duration)) / duration;
                float trem = (1 + f % 3) / duration;
                float phase = f * 1.7f;
                float amp = 0.5f / (1f + f * 0.4f);
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)SampleRate;
                    float tremolo = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * trem * t + phase);
                    samples[i] += Mathf.Sin(2f * Mathf.PI * hz * t) * amp * tremolo;
                }
            }

            Normalise(samples, 0.5f);
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
                lowpass += ((float)rng.NextDouble() * 2f - 1f - lowpass) * 0.08f;
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
                float amp = 0.25f + 0.5f * (float)rng.NextDouble();
                float prev = 0f;
                for (int i = 0; i < length && start + i < count; i++)
                {
                    float n = (float)rng.NextDouble() * 2f - 1f;
                    float high = n - prev * 0.6f;
                    prev = n;
                    samples[start + i] += high * amp * Mathf.Exp(-5f * i / length);
                }
            }

            Normalise(samples, 0.6f);
            var clip = AudioClip.Create("MachineApplause", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static void Normalise(float[] samples, float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < samples.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(samples[i]));
            }

            float scale = peak / max;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= scale;
            }
        }
    }
}
