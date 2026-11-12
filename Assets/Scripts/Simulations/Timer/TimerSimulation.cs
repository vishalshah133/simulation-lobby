using System;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using SimulationLobby.Shared.Tracks;
using UnityEngine;

namespace SimulationLobby.Simulations.Timer
{
    /// <summary>
    /// <c>timer</c> shell: a hopper releases a steady, seeded stream of marbles down the course into a collector, scheduled so
    /// the last one lands around 0:00. The clock is a function of the tick count, never of physics, so it is exact on every seed.
    /// </summary>
    /// <remarks>
    /// Determinism: the only seeded draws are one lateral release jitter per marble, all taken in <see cref="Initialize"/>.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class TimerSimulation : MonoBehaviour, ISimulation, ITimerState
    {
        const float MaxAngularVelocity = 80f;
        const int RespawnBack = 8;

        // Fail-safe for a marble that comes to rest on the course (a stuck marble is the one thing that ruins the footage).
        const float StuckSpeed = 0.3f;
        const float StuckSeconds = 1.2f;
        const float NudgeSpeed = 1.5f;
        const float NudgeInward = 0.4f;

        [Header("Scene wiring")]
        [SerializeField] TimerConfig config;
        [SerializeField] TrackLayout courseLayout;
        [SerializeField] TrackPath path;
        [SerializeField] Rigidbody[] pool = Array.Empty<Rigidbody>();
        [Tooltip("Where marbles enter the course; its right axis is the jitter axis.")]
        [SerializeField] Transform hopperRelease;
        [Tooltip("Optional visual reference for the finish line.")]
        [SerializeField] Transform finishLine;
        [Tooltip("Optional. Deactivated at the release.")]
        [SerializeField] Transform hopperGate;

        SeededRandom _random;
        RunRecorder _recorder;

        float _dt;
        int _holdTicks;
        int _durationTicks;
        int _tailTicks;
        int _ticksSinceRelease;
        float _startDistance;
        float _finishDistance;

        int[] _releaseTick = Array.Empty<int>();
        float[] _jitter = Array.Empty<float>();
        int[] _pathIndex = Array.Empty<int>();
        float[] _distance = Array.Empty<float>();
        float[] _releaseTime = Array.Empty<float>();
        float[] _travel = Array.Empty<float>();
        bool[] _released = Array.Empty<bool>();
        bool[] _collected = Array.Empty<bool>();

        int _nextRelease;
        int _releasedCount;
        int _collectedCount;
        int _respawnCount;
        int _nudgeCount;
        int _stuckTicksLimit;
        int[] _slowTicks = Array.Empty<int>();
        int _maxOnTrack;
        int _headIndex = -1;
        int _tailIndex = -1;
        float _headProgress;
        float _tailProgress;
        float _fillAtZero;
        bool _clockZeroed;

        public string FormatSlug => "timer";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        public TimerConfig Config => config;

        public TrackLayout CourseLayout => courseLayout;

        public TrackPath Path => path;

        public Transform HopperRelease => hopperRelease;

        public Transform FinishLine => finishLine;

        public Transform HopperGate => hopperGate;

        /// <summary>Metres a marble travels, gate to finish.</summary>
        public float CourseLength => path != null && courseLayout != null
            ? path.TotalLength - courseLayout.runOutLength - courseLayout.gateDistance
            : 0f;

        /// <summary>Ticks from Initialize to the release (the setup hold).</summary>
        public int HoldTicks => _holdTicks;

        /// <summary>Ticks from the release to 0:00.</summary>
        public int DurationTicks => _durationTicks;

        public Rigidbody MarbleBody(int i) => pool[i];

        // --- ITimerState: read by presentation. Observes; never writes. ---

        public float Duration => config != null ? config.durationSeconds : 0f;

        public float Elapsed => _ticksSinceRelease >= _durationTicks && _durationTicks > 0
            ? Duration
            : Mathf.Min(_ticksSinceRelease * _dt, Duration);

        public float Remaining => Mathf.Max(0f, Duration - Elapsed);

        public float SetupRemaining
        {
            get
            {
                if (Released || _recorder == null)
                {
                    return Released ? 0f : (config != null ? config.setupHoldSeconds : 0f);
                }

                return Mathf.Max(0f, config.setupHoldSeconds - _recorder.CurrentTick * _dt);
            }
        }

        public bool Released { get; private set; }

        public TimerPhase Phase { get; private set; }

        public int TotalMarbles => pool.Length;

        public int ReleasedCount => _releasedCount;

        public int CollectedCount => _collectedCount;

        public int OnTrackCount => _releasedCount - _collectedCount;

        public float Fill => pool.Length > 0 ? (float)_collectedCount / pool.Length : 0f;

        public Vector3 MarblePosition(int i) => pool[i].transform.position;

        public bool MarbleOnTrack(int i) => _released.Length > i && _released[i] && !_collected[i];

        public Color MarbleColor(int i)
        {
            Color[] colors = config.colors;
            return colors[i % colors.Length];
        }

        public float StreamHeadProgress => _headIndex >= 0 ? _headProgress : 0f;

        public float StreamTailProgress => _tailIndex >= 0 ? _tailProgress : 0f;

        public Vector3 StreamHead => _headIndex >= 0 ? pool[_headIndex].transform.position : HopperPosition;

        public Vector3 StreamTail => _tailIndex >= 0 ? pool[_tailIndex].transform.position : HopperPosition;

        Vector3 HopperPosition => hopperRelease != null ? hopperRelease.position : Vector3.zero;

        /// <summary>Scene-builder wiring by direct assignment, so the references serialize like SimulationRunner.config does.</summary>
        public void Wire(TimerConfig timerConfig, TrackLayout layout, TrackPath coursePath, Rigidbody[] marblePool,
            Transform hopper, Transform finish, Transform gate)
        {
            config = timerConfig;
            courseLayout = layout;
            path = coursePath;
            pool = marblePool;
            hopperRelease = hopper;
            finishLine = finish;
            hopperGate = gate;
        }

        public void Initialize(int seed, SimulationConfig simulationConfig)
        {
            config = (TimerConfig)simulationConfig;
            if (courseLayout == null || path == null || pool == null || pool.Length == 0 || hopperRelease == null)
            {
                throw new InvalidOperationException(
                    "TimerSimulation is not wired (courseLayout, path, pool or hopperRelease missing). Rebuild the scene: " +
                    "Simulation Lobby > 3D Formats > Marble Run Timer (timer).");
            }

            _dt = config.fixedTimestep;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 64);

            int n = pool.Length;
            _holdTicks = Mathf.CeilToInt(config.setupHoldSeconds / _dt);
            _durationTicks = Mathf.RoundToInt(config.durationSeconds / _dt);
            _tailTicks = Mathf.CeilToInt(config.finishTailSeconds / _dt);
            _ticksSinceRelease = 0;
            _startDistance = courseLayout.gateDistance;
            _finishDistance = path.TotalLength - courseLayout.runOutLength;

            Released = false;
            IsComplete = false;
            Phase = TimerPhase.Setup;
            _nextRelease = 0;
            _releasedCount = 0;
            _collectedCount = 0;
            _respawnCount = 0;
            _nudgeCount = 0;
            _stuckTicksLimit = Mathf.Max(1, Mathf.CeilToInt(StuckSeconds / _dt));
            _maxOnTrack = 0;
            _headIndex = -1;
            _tailIndex = -1;
            _fillAtZero = 0f;
            _clockZeroed = false;

            _releaseTick = new int[n];
            _jitter = new float[n];
            _pathIndex = new int[n];
            _distance = new float[n];
            _releaseTime = new float[n];
            _travel = new float[n];
            _released = new bool[n];
            _slowTicks = new int[n];
            _collected = new bool[n];

            int burst = Mathf.Clamp(config.releaseBurst, 1, n);
            float window = config.durationSeconds - config.expectedTravelSeconds;
            float interval = Mathf.Max(_dt, window / n) * burst;
            for (int k = 0; k < n; k++)
            {
                _releaseTick[k] = Mathf.RoundToInt(k / burst * interval / _dt);
                _jitter[k] = _random.NextFloat(-config.releaseJitterLateral, config.releaseJitterLateral);
            }

            if (hopperGate != null)
            {
                hopperGate.gameObject.SetActive(true);
            }

            Vector3 hopper = hopperRelease.position;
            for (int i = 0; i < n; i++)
            {
                Rigidbody body = pool[i];
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.position = hopper;
                body.transform.position = hopper;
                body.gameObject.SetActive(false);
            }

            _recorder.SetMetric("duration_s", config.durationSeconds);
            _recorder.SetMetric("course_length_m", CourseLength);
        }

        public void Tick(float fixedDelta)
        {
            if (IsComplete || _recorder == null)
            {
                return;
            }

            _recorder.AdvanceTick();

            if (!Released)
            {
                if (_recorder.CurrentTick <= _holdTicks)
                {
                    return;
                }

                Released = true;
                Phase = TimerPhase.Running;
                if (hopperGate != null)
                {
                    hopperGate.gameObject.SetActive(false);
                }

                _recorder.LogEvent("release");
            }

            _ticksSinceRelease++;
            ReleaseDue(_ticksSinceRelease - 1);
            StepMarbles();

            if (!_clockZeroed && _ticksSinceRelease >= _durationTicks)
            {
                _clockZeroed = true;
                Phase = TimerPhase.Finale;
                _fillAtZero = Fill;
                _recorder.LogEvent("clock_zero", _fillAtZero);
            }

            if (_ticksSinceRelease >= _durationTicks + _tailTicks)
            {
                Complete();
            }
        }

        void ReleaseDue(int tick)
        {
            if (_ticksSinceRelease > _durationTicks)
            {
                return;
            }

            while (_nextRelease < pool.Length && _releaseTick[_nextRelease] <= tick && _releaseTick[_nextRelease] < _durationTicks)
            {
                ReleaseMarble(_nextRelease, tick);
                _nextRelease++;
            }
        }

        void ReleaseMarble(int k, int tick)
        {
            Rigidbody body = pool[k];
            Vector3 position = hopperRelease.position + hopperRelease.right * _jitter[k];
            body.gameObject.SetActive(true);
            body.isKinematic = false;
            body.mass = config.marbleMass;
            body.linearDamping = config.marbleLinearDamping;
            body.angularDamping = config.marbleAngularDamping;
            body.maxDepenetrationVelocity = config.maxDepenetrationVelocity;
            body.maxAngularVelocity = MaxAngularVelocity;
            body.sleepThreshold = 0f;
            body.position = position;
            body.rotation = Quaternion.identity;
            body.transform.SetPositionAndRotation(position, Quaternion.identity);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            ClearTrail(body);

            _pathIndex[k] = path.Nearest(position, 0, 0, 300, out _);
            _distance[k] = path.ProgressAt(position, _pathIndex[k]);
            _releaseTime[k] = tick * _dt;
            _slowTicks[k] = 0;
            _released[k] = true;
            _releasedCount++;
        }

        void StepMarbles()
        {
            float span = Mathf.Max(1e-3f, _finishDistance - _startDistance);
            float limit = config.offTrackDistance;
            float limitSqr = limit * limit;
            float headDistance = float.NegativeInfinity;
            float tailDistance = float.PositiveInfinity;
            _headIndex = -1;
            _tailIndex = -1;

            for (int i = 0; i < _nextRelease; i++)
            {
                if (_collected[i])
                {
                    continue;
                }

                Rigidbody body = pool[i];
                Vector3 position = body.position;
                int index = path.Nearest(position, _pathIndex[i], 6, 60, out float sqrDistance);
                _pathIndex[i] = index;
                float distance = path.ProgressAt(position, index);
                _distance[i] = distance;

                if (distance >= _finishDistance)
                {
                    _collected[i] = true;
                    _collectedCount++;
                    _travel[i] = _ticksSinceRelease * _dt - _releaseTime[i];
                    body.sleepThreshold = Physics.sleepThreshold;
                    continue;
                }

                if (sqrDistance > limitSqr || position.y < path.PointAt(index).y - limit)
                {
                    Respawn(i);
                    position = body.position;
                    distance = _distance[i];
                }

                CheckStuck(i, body, index, position);

                if (distance > headDistance)
                {
                    headDistance = distance;
                    _headIndex = i;
                }

                if (distance < tailDistance)
                {
                    tailDistance = distance;
                    _tailIndex = i;
                }
            }

            if (_headIndex >= 0)
            {
                _headProgress = Mathf.Clamp01((headDistance - _startDistance) / span);
                _tailProgress = Mathf.Clamp01((tailDistance - _startDistance) / span);
            }

            int onTrack = OnTrackCount;
            if (onTrack > _maxOnTrack)
            {
                _maxOnTrack = onTrack;
            }
        }

        /// <summary>
        /// Deterministic fail-safe, no RNG: a marble that stays under <see cref="StuckSpeed"/> for <see cref="StuckSeconds"/> is set moving along the
        /// path tangent with a little push toward the track centre. Counted in the "nudges" metric.
        /// </summary>
        void CheckStuck(int i, Rigidbody body, int index, Vector3 position)
        {
            if (body.linearVelocity.sqrMagnitude >= StuckSpeed * StuckSpeed)
            {
                _slowTicks[i] = 0;
                return;
            }

            if (++_slowTicks[i] < _stuckTicksLimit)
            {
                return;
            }

            _slowTicks[i] = 0;
            Vector3 tangent = path.TangentAt(index);
            Vector3 toCentre = path.PointAt(index) - position;
            toCentre -= tangent * Vector3.Dot(toCentre, tangent);
            toCentre.y = 0f;
            float inward = toCentre.magnitude;
            Vector3 velocity = tangent * NudgeSpeed;
            if (inward > 1e-3f)
            {
                velocity += toCentre * (NudgeInward / inward);
            }

            body.linearVelocity = velocity;
            _nudgeCount++;
        }

        void Respawn(int i)
        {
            int index = Mathf.Max(0, _pathIndex[i] - RespawnBack);
            Vector3 point = path.PointAt(index) + Vector3.up * (config.marbleRadius + 0.05f);
            Rigidbody body = pool[i];
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = point;
            body.transform.position = point;
            _pathIndex[i] = index;
            _distance[i] = path.DistanceAt(index);
            _respawnCount++;
            ClearTrail(body);
            _recorder.LogEvent("respawn", i);
        }

        static void ClearTrail(Rigidbody body)
        {
            var trail = body.GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.Clear();
            }
        }

        void Complete()
        {
            int count = 0;
            float sum = 0f;
            for (int i = 0; i < _collected.Length; i++)
            {
                if (_collected[i])
                {
                    sum += _travel[i];
                    count++;
                }
            }

            float mean = 0f;
            float p95 = 0f;
            if (count > 0)
            {
                mean = sum / count;
                var times = new float[count];
                int w = 0;
                for (int i = 0; i < _collected.Length; i++)
                {
                    if (_collected[i])
                    {
                        times[w++] = _travel[i];
                    }
                }

                Array.Sort(times);
                p95 = times[Mathf.Min(count - 1, Mathf.CeilToInt(count * 0.95f) - 1)];
            }

            _recorder.SetMetric("released", _releasedCount);
            _recorder.SetMetric("collected", _collectedCount);
            _recorder.SetMetric("fill_at_zero", _fillAtZero);
            _recorder.SetMetric("respawns", _respawnCount);
            _recorder.SetMetric("nudges", _nudgeCount);
            _recorder.SetMetric("max_on_track", _maxOnTrack);
            _recorder.SetMetric("mean_travel_s", mean);
            _recorder.SetMetric("p95_travel_s", p95);
            _recorder.LogEvent("done", _collectedCount);
            _recorder.Finish(CompletionReason.Finished);
            Phase = TimerPhase.Done;
            IsComplete = true;
        }
    }
}
