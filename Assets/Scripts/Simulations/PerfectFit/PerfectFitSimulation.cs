using SimulationLobby.Core;
using SimulationLobby.Simulations.PerfectFit.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.PerfectFit
{
    /// <summary>Where the run is.</summary>
    public enum PerfectFitPhase
    {
        /// <summary>Wall turning, shape hanging out of step in front of it, the question up.</summary>
        Hold = 0,

        /// <summary>The shape flies in, locks onto the wall's spin, and slides through.</summary>
        Flight = 1,

        /// <summary>It is through. The result line is up and the shape keeps turning in step.</summary>
        Result = 2,

        /// <summary>After the last round: the tally and the one-line why.</summary>
        EndCard = 3,

        Done = 4
    }

    /// <summary>
    /// Everything the picture needs for one round at one moment. Wall at the origin, the camera on the -Z side, the
    /// shape moving toward +Z. Angles in degrees, distances in metres.
    /// </summary>
    public struct FitPose
    {
        public float wallAngle;
        public float shapeAngle;
        public float tiltX;
        public float tiltY;
        public float shapeZ;

        /// <summary>0..1 through the flight.</summary>
        public float flightProgress;

        /// <summary>0..1 camera push toward the hole: up on the approach, held through the pass, relaxed in the result.</summary>
        public float focus;

        public PerfectFitPhase phase;

        /// <summary>The shape has locked onto the wall's spin (misalignment is exactly zero from here on).</summary>
        public bool aligned;

        /// <summary>The shape's leading face has reached the wall's front face.</summary>
        public bool entered;

        /// <summary>The shape's trailing face has left the wall's back face: it is through.</summary>
        public bool cleared;
    }

    /// <summary>
    /// The round shell for <c>fit</c>. Kinematic and closed-form: a round's whole motion is a pure function of
    /// (round, seconds), seeded once in <see cref="Initialize"/>, so the picture is identical at any frame rate and the
    /// same seed always renders the same take. The shape cannot touch the wall by construction (the hole is the
    /// shape's outline grown by the gap); <see cref="Initialize"/> measures that gap and logs it per round.
    /// </summary>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class PerfectFitSimulation : MonoBehaviour, ISimulation
    {
        RunRecorder _recorder;
        SeededRandom _random;
        float _dt;
        int _roundTick;
        int _endTick;
        int _endTicks;

        float[] _spinSign;
        float[] _phase0;
        float[] _delta0;
        float[] _tiltX0;
        float[] _tiltY0;
        float[] _wobblePhase;

        float _zStart;
        float _zEnd;
        float _contactS;
        float _clearS;
        float _alignS;

        public string FormatSlug => "fit";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        public PerfectFitConfig Config { get; private set; }

        public int Seed { get; private set; }

        public PerfectFitPhase Phase { get; private set; }

        /// <summary>0-based round the clock is in. Advances the tick the previous round's result ends.</summary>
        public int RoundIndex { get; private set; }

        public int RoundTotal => Config != null ? Config.RoundCount : 0;

        /// <summary>Seconds since this round began. Keeps counting through the end card so the last wall keeps turning.</summary>
        public float RoundSeconds => _roundTick * _dt;

        /// <summary>0..1 through the end card.</summary>
        public float EndProgress => _endTicks > 0 ? Mathf.Clamp01((float)_endTick / _endTicks) : 1f;

        public float FixedStep => _dt;

        /// <summary>Seconds into a round's flight until the shape's leading face meets the wall (the riser's length).</summary>
        public float ContactSeconds => _contactS * (Config != null ? Config.flightSeconds : 0f);

        public PerfectFitRound CurrentRound => Config != null ? Config.rounds[RoundIndex] : default;

        public void Initialize(int seed, SimulationConfig config)
        {
            Config = (PerfectFitConfig)config;
            Seed = seed;
            _dt = Config.fixedTimestep;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 256);

            int n = Config.RoundCount;
            _spinSign = new float[n];
            _phase0 = new float[n];
            _delta0 = new float[n];
            _tiltX0 = new float[n];
            _tiltY0 = new float[n];
            _wobblePhase = new float[n];
            for (int i = 0; i < n; i++)
            {
                _spinSign[i] = _random.NextBool() ? 1f : -1f;
                _delta0[i] = (_random.NextBool() ? 1f : -1f) *
                             _random.NextFloat(Config.startOffsetMinDegrees, Config.startOffsetMaxDegrees);
                _tiltX0[i] = _random.NextFloat(-Config.tiltMaxDegrees, Config.tiltMaxDegrees);
                _tiltY0[i] = _random.NextFloat(-Config.tiltMaxDegrees, Config.tiltMaxDegrees);
                _phase0[i] = _random.NextFloat(0f, 360f);
                _wobblePhase[i] = _random.NextFloat(0f, 2f * Mathf.PI);
            }

            ComputeFlightGeometry();
            MeasureGaps();

            _recorder.SetMetric("rounds_planned", n);
            _recorder.SetMetric("series_seconds_estimate", Config.SeriesSeconds);

            RoundIndex = 0;
            _roundTick = 0;
            _endTick = 0;
            _endTicks = Mathf.Max(1, Mathf.RoundToInt(Config.endCardSeconds / _dt));
            IsComplete = false;
            Phase = PerfectFitPhase.Hold;
            _recorder.LogEvent("round_1_hold");
        }

        public void Tick(float fixedDelta)
        {
            if (IsComplete || _recorder == null)
            {
                return;
            }

            _recorder.AdvanceTick();
            _roundTick++;

            if (Phase == PerfectFitPhase.EndCard)
            {
                _endTick++;
                if (_endTick >= _endTicks)
                {
                    FinishRun();
                }

                return;
            }

            float seconds = RoundSeconds;
            if (seconds >= Config.RoundSeconds(RoundIndex))
            {
                _recorder.LogEvent($"round_{RoundIndex + 1}_done");
                if (RoundIndex + 1 < Config.RoundCount)
                {
                    RoundIndex++;
                    _roundTick = 0;
                    Phase = PerfectFitPhase.Hold;
                    _recorder.LogEvent($"round_{RoundIndex + 1}_hold");
                }
                else
                {
                    Phase = PerfectFitPhase.EndCard;
                    _recorder.LogEvent("end_card");
                }

                return;
            }

            PerfectFitPhase phase = PhaseAt(RoundIndex, seconds);
            if (phase != Phase)
            {
                Phase = phase;
                if (phase == PerfectFitPhase.Flight)
                {
                    _recorder.LogEvent($"round_{RoundIndex + 1}_launch");
                }
            }
        }

        /// <summary>Length of one round's hold + flight + result, for the presenter's slide-out of the previous wall.</summary>
        public float RoundDuration(int round)
        {
            return Config.RoundSeconds(round);
        }

        PerfectFitPhase PhaseAt(int round, float seconds)
        {
            float hold = Config.HoldFor(round);
            if (seconds < hold)
            {
                return PerfectFitPhase.Hold;
            }

            return seconds < hold + Config.flightSeconds ? PerfectFitPhase.Flight : PerfectFitPhase.Result;
        }

        /// <summary>
        /// The pose of <paramref name="round"/> <paramref name="seconds"/> after it began. A pure function of its two
        /// arguments and the seeded draws, so the presenter can ask for the outgoing round too while it slides away.
        /// </summary>
        public FitPose PoseAt(int round, float seconds)
        {
            float hold = Config.HoldFor(round);
            float flight = Config.flightSeconds;
            float s = Mathf.Clamp01((seconds - hold) / flight);
            float eased = 1f - (1f - s) * (1f - s); // decelerates to rest: the pass through the wall is the slow part

            // The shape locks on (all misalignment gone) a little before it reaches the wall.
            float e = Smooth(Mathf.Clamp01(s / _alignS));
            float remaining = 1f - e;
            float wobble = Config.wobbleDegrees * remaining;

            float wallAngle = _phase0[round] + _spinSign[round] * Config.rounds[round].spinDegreesPerSecond * seconds;

            float push = Smooth(Mathf.Clamp01((s - (_contactS - 0.25f)) / 0.23f));
            float relax = Smooth(Mathf.Clamp01((seconds - hold - flight) / Mathf.Max(0.01f, Config.resultSeconds)));

            return new FitPose
            {
                wallAngle = wallAngle,
                shapeAngle = wallAngle + _delta0[round] * remaining,
                tiltX = _tiltX0[round] * remaining + wobble * Mathf.Sin(seconds * 8f + _wobblePhase[round]),
                tiltY = _tiltY0[round] * remaining + wobble * Mathf.Cos(seconds * 6.5f + _wobblePhase[round]),
                shapeZ = Mathf.Lerp(_zStart, _zEnd, eased),
                flightProgress = s,
                focus = push * (1f - relax),
                phase = PhaseAt(round, seconds),
                aligned = s >= _alignS,
                entered = s >= _contactS,
                cleared = s >= _clearS
            };
        }

        void ComputeFlightGeometry()
        {
            float halfWall = Config.wallThickness * 0.5f;
            float halfDepth = Config.shapeDepth * 0.5f;
            _zStart = -(halfWall + Config.startDistance);
            _zEnd = halfWall + Config.exitDistance;
            _contactS = FlightProgressAt(-halfWall - halfDepth);
            _clearS = FlightProgressAt(halfWall + halfDepth);
            _alignS = Mathf.Max(0.02f, _contactS - Config.alignLeadFraction);
        }

        /// <summary>Inverse of z = zStart + (zEnd - zStart) * (1 - (1 - s)^2).</summary>
        float FlightProgressAt(float z)
        {
            float fraction = Mathf.Clamp01((z - _zStart) / (_zEnd - _zStart));
            return 1f - Mathf.Sqrt(1f - fraction);
        }

        static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }

        /// <summary>Measures each round's real gap (shape outline against hole outline) and logs it with the result.</summary>
        void MeasureGaps()
        {
            for (int i = 0; i < Config.RoundCount; i++)
            {
                PerfectFitRound r = Config.rounds[i];
                float gap = r.gapMillimetres * 0.001f;
                Vector2[] shape = FitProfile.Create(r.kind, Config.shapeRadius);
                Vector2[] hole = FitProfile.Offset(shape, gap);
                float measured = FitProfile.MinClearance(shape, hole) * 1000f;
                _recorder.SetMetric($"round_{i + 1}_gap_mm", r.gapMillimetres);
                _recorder.SetMetric($"round_{i + 1}_measured_gap_mm", measured);
                if (measured < r.gapMillimetres * 0.97f)
                {
                    Debug.LogError($"[fit] round {i + 1} ({r.shapeName}): measured gap {measured:0.000} mm is under the " +
                                   $"{r.gapMillimetres:0.###} mm the caption promises.");
                }
            }
        }

        void FinishRun()
        {
            Phase = PerfectFitPhase.Done;
            _recorder.SetMetric("touches", 0f);
            _recorder.Finish(CompletionReason.Finished);
            IsComplete = true;
        }
    }
}
