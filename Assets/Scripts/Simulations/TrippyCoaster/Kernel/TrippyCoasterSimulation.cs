using SimulationLobby.Core;
using SimulationLobby.Shared.Coaster;
using UnityEngine;

namespace SimulationLobby.Simulations.TrippyCoaster
{
    /// <summary>Everything the view may read. Implemented by <see cref="TrippyCoasterSimulation"/>. Read-only by contract.</summary>
    public interface ITripState
    {
        ICoasterRail Rail { get; }
        TrippyCoasterConfig Config { get; }
        float SimTime { get; }
        float Distance { get; }
        float Speed { get; }
        bool OnLift { get; }
        /// <summary>Cart pose with the seeded wobble applied.</summary>
        Vector3 CartPosition { get; }
        Vector3 CartForward { get; }
        Vector3 CartUp { get; }
        int WorldIndex { get; }
        int WorldCount { get; }
        /// <summary>0..1 through the current world.</summary>
        float WorldProgress { get; }
        /// <summary>0..1 through the whole ride.</summary>
        float RideProgress { get; }
        /// <summary>Seconds since the current world began (drives the title card).</summary>
        float WorldTime { get; }
        bool IsComplete { get; }
    }

    /// <summary>
    /// The <c>trip</c> (Trippy Coaster) simulation: one cart on one rail through a sequence of worlds. There is no scoring and no
    /// riders; the seed only drives the cart's wobble phases, so the ride is identical at any frame rate.
    /// </summary>
    public sealed class TrippyCoasterSimulation : ISimulation, ITripState
    {
        static readonly float[,] WobbleHz =
        {
            { 0.29f, 0.71f, 1.37f },
            { 0.23f, 0.61f, 1.13f },
            { 0.41f, 0.83f, 1.57f }
        };
        static readonly float[] WobbleWeight = { 0.5f, 0.3f, 0.2f };

        TrippyCoasterConfig _cfg;
        SeededRandom _random;
        RunRecorder _recorder;
        CoasterRail _rail;
        RideDynamics _dyn;
        readonly float[,] _phase = new float[3, 3];
        int _tick;
        int _endTicks;
        int _lastWorld = -1;
        float _worldEnteredAt;
        float _maxSpeed;
        bool _complete;

        public string FormatSlug => "trip";
        public bool IsComplete => _complete;
        public RunResult Result => _recorder?.Result;

        public ICoasterRail Rail => _rail;
        public TrippyCoasterConfig Config => _cfg;
        public float SimTime { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public bool OnLift { get; private set; }
        public Vector3 CartPosition { get; private set; }
        public Vector3 CartForward { get; private set; }
        public Vector3 CartUp { get; private set; }
        public int WorldIndex { get; private set; }
        public int WorldCount => _cfg != null ? _cfg.worlds.Length : 0;
        public float WorldProgress { get; private set; }
        public float RideProgress => _rail != null && _rail.Length > 0f ? Distance / _rail.Length : 0f;
        public float WorldTime => SimTime - _worldEnteredAt;

        public void Initialize(int seed, SimulationConfig config)
        {
            _cfg = (TrippyCoasterConfig)config;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, _cfg, _random, 64);
            _rail = _cfg.BuildRail();
            _dyn = new RideDynamics(_rail, _cfg.RideParameters);

            for (int c = 0; c < 3; c++)
                for (int k = 0; k < 3; k++)
                    _phase[c, k] = _random.NextFloat(0f, Mathf.PI * 2f);

            _tick = 0;
            _endTicks = 0;
            _lastWorld = -1;
            _worldEnteredAt = 0f;
            _maxSpeed = 0f;
            _complete = false;
            SimTime = 0f;
            Refresh();

            _recorder.SetMetric("rail_length_m", _rail.Length);
            _recorder.SetMetric("worlds", _cfg.worlds.Length);
        }

        public void Tick(float fixedDelta)
        {
            if (_cfg == null || _complete) return;

            _tick++;
            _recorder.AdvanceTick();
            SimTime = _tick * _cfg.fixedTimestep;
            _dyn.Step(_cfg.fixedTimestep);
            Refresh();
            _maxSpeed = Mathf.Max(_maxSpeed, Speed);

            if (_dyn.AtEnd)
            {
                _endTicks++;
                if (_endTicks * _cfg.fixedTimestep >= _cfg.endHoldSeconds) Finish();
            }
        }

        void Refresh()
        {
            Distance = _dyn.Distance;
            Speed = _dyn.Speed;
            OnLift = _dyn.OnLift;

            int w = _cfg.WorldAt(Distance);
            if (w != _lastWorld)
            {
                if (_lastWorld >= 0) _recorder.LogEvent("world:" + _cfg.worlds[w].name, Distance);
                _lastWorld = w;
                _worldEnteredAt = SimTime;
            }
            WorldIndex = w;
            float a = _cfg.worlds[w].startDistance;
            float b = _cfg.WorldEnd(w, _rail.Length);
            WorldProgress = Mathf.Clamp01((Distance - a) / Mathf.Max(1f, b - a));

            _rail.Sample(Distance, out Vector3 p, out Vector3 f, out Vector3 u);
            float amp = _cfg.wobbleDegrees * Mathf.Deg2Rad;
            float roll = Wobble(0) * amp;
            float pitch = Wobble(1) * amp * 0.6f;
            float lat = Wobble(2) * 0.04f;
            Vector3 right = Vector3.Cross(u, f).normalized;
            Vector3 f2 = CoasterMath.Rotate(f, right, pitch);
            Vector3 u2 = CoasterMath.OrthoNormalize(CoasterMath.Rotate(u, f2, roll), f2, u);
            CartPosition = p + right * lat;
            CartForward = f2;
            CartUp = u2;
        }

        float Wobble(int channel)
        {
            float v = 0f;
            for (int k = 0; k < 3; k++)
                v += WobbleWeight[k] * Mathf.Sin(SimTime * WobbleHz[channel, k] * Mathf.PI * 2f + _phase[channel, k]);
            return v;
        }

        void Finish()
        {
            _complete = true;
            _recorder.SetMetric("ride_seconds", SimTime);
            _recorder.SetMetric("max_speed", _maxSpeed);
            _recorder.AddStanding("ride complete");
            _recorder.Finish(CompletionReason.Finished);
        }

    }
}
