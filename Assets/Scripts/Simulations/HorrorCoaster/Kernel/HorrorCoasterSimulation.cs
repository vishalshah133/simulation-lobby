using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// The <c>coast</c> (Haunted Coaster) simulation: a cart on a rail, a distance-triggered scare schedule and a rider
    /// counter. Drive it from <see cref="SimulationRunner"/> (see <see cref="HorrorCoasterHost"/>); presentation reads it
    /// through <see cref="ICoasterState"/> and never writes.
    /// </summary>
    /// <remarks>
    /// Determinism: ride speed is a function of the rail and the config only (the seed never changes it), scare phases are
    /// entered by distance and speed, and the seed drives the wobble phases and the order riders are taken.
    /// Same seed gives the same ride at any render frame rate because the runner steps a fixed delta.
    /// </remarks>
    public sealed class HorrorCoasterSimulation : ISimulation, ICoasterState
    {
        /// <summary>Seats ahead of the viewer.</summary>
        public const int Seats = 5;

        // Wobble: four channels (vertical, roll, lateral, pitch), three low-frequency sines each, phases drawn from the seed.
        static readonly float[,] WobbleHz =
        {
            { 0.31f, 0.77f, 1.43f },
            { 0.23f, 0.59f, 1.10f },
            { 0.41f, 0.89f, 1.61f },
            { 0.27f, 0.67f, 1.27f }
        };
        static readonly float[] WobbleWeight = { 0.5f, 0.3f, 0.2f };

        HorrorCoasterConfig _cfg;
        SeededRandom _random;
        RunRecorder _recorder;
        CoasterRail _rail;
        CoasterDynamics _dyn;
        ScareEvent[] _events = new ScareEvent[0];
        IReadOnlyList<ScareEvent> _eventsView = Array.AsReadOnly(new ScareEvent[0]);
        int[] _eventSeat = new int[0];
        readonly bool[] _present = new bool[Seats];
        readonly IReadOnlyList<bool> _presentView;
        readonly float[,] _phase = new float[4, 3];
        ScareEvent _current;
        int _tick;
        float _dt;
        int _next;
        int _ridersLeft;
        float _fear;
        int _arrivedTicks;
        bool _complete;
        float _maxSpeed;
        float _wobbleVert, _wobbleRoll, _wobbleLat, _wobblePitch;

        public HorrorCoasterSimulation()
        {
            _presentView = Array.AsReadOnly(_present);
        }

        // ---- ISimulation ----

        public string FormatSlug => "coast";
        public bool IsComplete => _complete;
        public RunResult Result => _recorder?.Result;

        // ---- extras (not on the contract) ----

        public HorrorCoasterConfig Config => _cfg;
        public int Seed { get; private set; }

        /// <summary>True for events that are a genuine scare (not a fake-out and not a safe treat door).</summary>
        public static bool IsRealScare(ScareKind kind)
        {
            switch (kind)
            {
                case ScareKind.FakeBat:
                case ScareKind.FakeLantern:
                case ScareKind.FakeWheelchair:
                case ScareKind.FakeEmptySeat:
                case ScareKind.TreatDoor:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>A named, rider-taking-or-finale scare: real, and not one of the short micro jumps.</summary>
        public static bool IsBigScare(ScareKind kind) => IsRealScare(kind) && !kind.IsMicro();

        // ---- ICoasterState ----

        public ICoasterRail Rail => _rail;
        public float SimTime { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public bool OnLift { get; private set; }
        public Vector3 CartPosition { get; private set; }
        public Vector3 CartForward { get; private set; } = Vector3.forward;
        public Vector3 CartUp { get; private set; } = Vector3.up;
        public ActId Act { get; private set; }
        public int RidersLeft => _ridersLeft;
        public IReadOnlyList<bool> RiderPresent => _presentView;
        public int NextEventIndex => _next;
        public IReadOnlyList<ScareEvent> Events => _eventsView;
        public ScarePhase Phase { get; private set; }
        public ScareEvent CurrentEvent => _current;
        public int CurrentEventIndex { get; private set; } = -1;
        public float PhaseTime { get; private set; }
        public float Fear01 => _fear;

        /// <summary>
        /// The seat of the rider the current event takes. Known from the start of PreQuiet (so a view may foreshadow it);
        /// RiderPresent flips and RidersLeft drops at the Sting. -1 when idle or when the event takes nobody.
        /// </summary>
        public int TakenSeat
        {
            get
            {
                if (Phase == ScarePhase.Idle || CurrentEventIndex < 0 || !_current.takesRider) return -1;
                return _eventSeat[CurrentEventIndex];
            }
        }

        // ---- lifecycle ----

        public void Initialize(int seed, SimulationConfig config)
        {
            _cfg = (HorrorCoasterConfig)config;
            Seed = seed;
            _dt = _cfg.fixedTimestep;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, _cfg, _random, 256);

            _rail = HorrorCoasterRail.FromConfig(_cfg);
            _dyn = new CoasterDynamics(_rail, _cfg);

            // Events sorted by distance (stable insertion sort; the authored order breaks ties).
            _events = _cfg.events != null ? (ScareEvent[])_cfg.events.Clone() : new ScareEvent[0];
            for (int i = 1; i < _events.Length; i++)
            {
                ScareEvent x = _events[i];
                int j = i - 1;
                while (j >= 0 && _events[j].distance > x.distance)
                {
                    _events[j + 1] = _events[j];
                    j--;
                }
                _events[j + 1] = x;
            }
            _eventsView = Array.AsReadOnly(_events);

            // Seat order: seats 1..4 in a seeded order for the first takes, the last take is always seat 0.
            var order = new List<int> { 1, 2, 3, 4 };
            _random.Shuffle(order);
            int takes = 0;
            for (int i = 0; i < _events.Length; i++) if (_events[i].takesRider) takes++;
            _eventSeat = new int[_events.Length];
            int seen = 0;
            for (int i = 0; i < _events.Length; i++)
            {
                _eventSeat[i] = -1;
                if (!_events[i].takesRider) continue;
                if (seen == takes - 1) _eventSeat[i] = 0;
                else if (seen < order.Count) _eventSeat[i] = order[seen];
                seen++;
            }

            for (int c = 0; c < 4; c++)
                for (int k = 0; k < 3; k++)
                    _phase[c, k] = _random.NextFloat(0f, Mathf.PI * 2f);

            for (int i = 0; i < Seats; i++) _present[i] = true;
            _ridersLeft = _cfg.ridersAtStart;
            _tick = 0;
            SimTime = 0f;
            _next = 0;
            Phase = ScarePhase.Idle;
            PhaseTime = 0f;
            CurrentEventIndex = -1;
            _current = default;
            _fear = 0f;
            _arrivedTicks = 0;
            _complete = false;
            _maxSpeed = 0f;

            Refresh();

            _recorder.SetMetric("riders_start", _cfg.ridersAtStart);
            _recorder.SetMetric("rail_length_m", _rail.Length);
            _recorder.SetMetric("events", _events.Length);
        }

        public void Tick(float fixedDelta)
        {
            if (_cfg == null || _complete) return;

            _tick++;
            _recorder.AdvanceTick();
            SimTime = _tick * _dt;

            _dyn.Step(fixedDelta);
            Refresh();
            UpdateScare(fixedDelta);
            UpdateFear(fixedDelta);

            if (_dyn.AtEnd || (Distance >= _rail.Length - 0.5f && Speed < 1f))
            {
                _arrivedTicks++;
                if (_arrivedTicks * _dt >= _cfg.endHoldSeconds) Complete();
            }
        }

        void Complete()
        {
            _complete = true;
            _recorder.SetMetric("riders_left", _ridersLeft);
            _recorder.SetMetric("ride_seconds", SimTime);
            _recorder.SetMetric("max_speed", _maxSpeed);
            _recorder.AddStanding($"riders left: {_ridersLeft}");
            _recorder.Finish(CompletionReason.Finished);
        }

        // ---- internals ----

        void Refresh()
        {
            Distance = _dyn.Distance;
            Speed = _dyn.Speed;
            OnLift = _dyn.OnLift;
            if (Speed > _maxSpeed) _maxSpeed = Speed;

            Act = ActAt(Distance);

            _rail.Sample(Distance, out Vector3 pos, out Vector3 fwd, out Vector3 up);
            ComputeWobble();
            Vector3 right = Vector3.Cross(up, fwd);
            pos += up * _wobbleVert + right * _wobbleLat;
            float pitch = _wobblePitch * Mathf.Deg2Rad;
            Vector3 f2 = CoasterMath.Rotate(fwd, right, pitch);
            Vector3 u2 = CoasterMath.Rotate(up, right, pitch);
            float roll = _wobbleRoll * Mathf.Deg2Rad;
            u2 = CoasterMath.Rotate(u2, f2, roll);
            CartPosition = pos;
            CartForward = f2;
            CartUp = CoasterMath.OrthoNormalize(u2, f2, up);
        }

        ActId ActAt(float d)
        {
            ActSpan[] acts = _cfg.acts;
            ActId result = acts != null && acts.Length > 0 ? acts[0].act : ActId.Station;
            if (acts == null) return result;
            for (int i = 0; i < acts.Length; i++)
            {
                if (acts[i].startDistance <= d) result = acts[i].act;
            }
            return result;
        }

        void ComputeWobble()
        {
            float amp = _cfg.wobbleAmplitude;
            float s = Mathf.Clamp01(Speed / 3f);
            float scale = s * s * (3f - 2f * s) * (0.4f + 0.6f * Mathf.Clamp01(Speed / 15f));
            float t = SimTime * Mathf.PI * 2f;
            _wobbleVert = Channel(0, t) * amp * scale;
            _wobbleRoll = Channel(1, t) * amp * 30f * scale;
            _wobbleLat = Channel(2, t) * amp * 0.5f * scale;
            _wobblePitch = Channel(3, t) * amp * 10f * scale;
        }

        float Channel(int c, float t)
        {
            float sum = 0f;
            for (int k = 0; k < 3; k++) sum += WobbleWeight[k] * Mathf.Sin(t * WobbleHz[c, k] + _phase[c, k]);
            return sum;
        }

        void UpdateScare(float dt)
        {
            PhaseTime += dt;

            // 1. Stings fire by distance. If one step carries the cart past several, every one of them still fires (in order).
            while (_next < _events.Length && Distance >= _events[_next].distance - 1e-4f)
            {
                Sting(_next);
                _next++;
            }

            // 2. Phase timeouts.
            if (Phase == ScarePhase.Sting && PhaseTime >= _cfg.stingSeconds)
            {
                Phase = ScarePhase.Aftermath;
                PhaseTime = 0f;
            }
            else if (Phase == ScarePhase.Aftermath && PhaseTime >= _cfg.aftermathSeconds)
            {
                Phase = ScarePhase.Idle;
                PhaseTime = 0f;
            }

            // 3. PreQuiet starts when the cart is preQuietSeconds away from the next event at its current speed.
            if ((Phase == ScarePhase.Idle || Phase == ScarePhase.Aftermath) && _next < _events.Length)
            {
                ScareEvent ev = _events[_next];
                float pre = ev.preQuietSeconds;
                if (pre > 0f && ev.distance - Distance <= pre * Mathf.Max(Speed, 0.05f))
                {
                    Phase = ScarePhase.PreQuiet;
                    CurrentEventIndex = _next;
                    _current = ev;
                    PhaseTime = 0f;
                }
            }
        }

        void Sting(int index)
        {
            ScareEvent ev = _events[index];
            _current = ev;
            CurrentEventIndex = index;
            Phase = ScarePhase.Sting;
            PhaseTime = 0f;
            if (ev.takesRider)
            {
                int seat = _eventSeat[index];
                if (seat >= 0 && seat < Seats) _present[seat] = false;
                if (_ridersLeft > 0) _ridersLeft--;
            }
            _recorder.LogEvent(ev.kind.ToString(), ev.distance);
        }

        void UpdateFear(float dt)
        {
            // Big scares build over fearRiseSeconds; micro jumps over the short microFearRiseSeconds.
            float target = 0f;
            bool big = false, micro = false;
            for (int i = _next; i < _events.Length && !(big && micro); i++)
            {
                ScareKind k = _events[i].kind;
                if (!IsRealScare(k)) continue;
                bool isMicro = k.IsMicro();
                if (isMicro ? micro : big) continue;
                if (isMicro) micro = true; else big = true;
                float rise = isMicro ? _cfg.microFearRiseSeconds : _cfg.fearRiseSeconds;
                float timeToGo = (_events[i].distance - Distance) / Mathf.Max(Speed, 0.5f);
                target = Mathf.Max(target, CoasterMath.SmoothStep01(1f - timeToGo / rise));
            }
            if (Phase == ScarePhase.Sting && IsRealScare(_current.kind)) target = 1f;

            float tau = target > _fear ? 0.35f : 2f;
            _fear += (target - _fear) * (1f - Mathf.Exp(-dt / tau));
            _fear = Mathf.Clamp01(_fear);
        }
    }
}
