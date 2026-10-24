using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Simulations.Convergence.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.Convergence
{
    /// <summary>Where the run is inside a round.</summary>
    public enum ConvergencePhase
    {
        /// <summary>Empty arena; the boundary morphs from the last round's shape to this one's.</summary>
        Transition = 0,

        /// <summary>Static beat before launch, start point pulsing.</summary>
        Hold = 1,

        Running = 2,

        /// <summary>Bodies frozen, result caption up.</summary>
        Result = 3,

        /// <summary>After the last round: end card.</summary>
        EndCard = 4,

        Done = 5
    }

    /// <summary>
    /// The round shell for the <c>conv</c> genre: Transition, Hold, Running, Result per round, then an
    /// end card. All geometry and motion live in an <see cref="IConvergenceKernel"/> built by the config.
    /// </summary>
    /// <remarks>
    /// Determinism: phases are counted in ticks, the kernel is pure double maths, and the only random
    /// draws are one cosmetic launch-fan rotation per round, taken up front in round order.
    /// Bodies are not drawn (ParticleCount is 0) during Transition and EndCard. In Hold they are drawn
    /// frozen at their launch layout only when <see cref="ConvergenceConfig.showBodiesDuringHold"/> is on
    /// (no Step runs); otherwise they appear at launch. They stay frozen in place through Result.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class ConvergenceSimulation : MonoBehaviour, ISimulation
    {
        /// <summary>Outline points per round, fixed so one round's shape morphs into the next point by point.</summary>
        public const int OutlineSamples = 256;

        const int WallHitRingSize = 4096;

        RunRecorder _recorder;
        SeededRandom _random;
        IConvergenceKernel _kernel;
        float _dt;
        double[] _angleOffsets;

        int _phaseTick;
        int _phaseTicks;
        int _runTick;
        int _prevAtTarget;
        int _windowMax;
        bool[] _departed = System.Array.Empty<bool>();
        int _maxBalls;
        long _totalWallHits;

        Vector2[] _positions = System.Array.Empty<Vector2>();
        int _particleCount;

        readonly List<Double2> _outlineScratch = new List<Double2>(OutlineSamples);
        readonly Vector2[] _fromOutline = new Vector2[OutlineSamples];
        readonly Vector2[] _toOutline = new Vector2[OutlineSamples];
        readonly List<Vector2> _outline = new List<Vector2>(OutlineSamples);
        Vector2 _fromStart;
        Vector2 _fromTarget;
        Vector2 _fromIdeal;
        Vector2 _toIdeal;
        Vector2 _toStart;
        Vector2 _toTarget;

        readonly List<WallHit> _hitScratch = new List<WallHit>(256);
        readonly WallHit[] _hitRing = new WallHit[WallHitRingSize];

        public string FormatSlug => "conv";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        public ConvergenceConfig Config { get; private set; }

        public ConvergencePhase Phase { get; private set; }

        /// <summary>0..1 through the current phase (hold pulse, transition morph, end card fade).</summary>
        public float PhaseProgress { get; private set; }

        /// <summary>0-based current round.</summary>
        public int RoundIndex { get; private set; }

        public int RoundTotal => Config != null ? Config.RoundCount : 0;

        public ConvergenceRoundInfo CurrentRound => Config != null ? Config.GetRound(RoundIndex) : default;

        public int MeetScore { get; private set; }

        public int MissScore { get; private set; }

        /// <summary>Bodies in play this round (0 during transition).</summary>
        public int ParticleCount => _particleCount;

        public Vector2 GetParticlePosition(int index) => _positions[index];

        /// <summary>Bodies launched this round (the round's own count, or the config's).</summary>
        public int RoundBallCount => Config != null ? Config.RoundBallCount(RoundIndex) : 0;

        /// <summary>Bodies needed at the target at once for this round to be a MEET.</summary>
        public int RoundMeetThreshold => Config != null ? Config.RoundMeetThreshold(RoundIndex) : 0;

        /// <summary>
        /// Bodies within the meet radius of the target right now. Only bodies that have first left the
        /// target's neighbourhood count, so a round that starts on its target (the circle) reads empty at launch.
        /// </summary>
        public int AtTargetCount { get; private set; }

        /// <summary>
        /// What the counter shows: the live <see cref="AtTargetCount"/> until the verdict closes, then
        /// <see cref="VerdictCount"/>, so a late bunch can't contradict the result line.
        /// </summary>
        public int DisplayCount => HasVerdict ? VerdictCount : AtTargetCount;

        /// <summary>The verdict count: the most bodies at the target at once at any time in the first pass (launch to expected meet plus the window).</summary>
        public int VerdictCount { get; private set; }

        /// <summary>Set once the verdict window has closed this round.</summary>
        public bool HasVerdict { get; private set; }

        /// <summary>The measured answer for the current round. Valid once <see cref="HasVerdict"/>.</summary>
        public ConvergenceAnswer Verdict { get; private set; }

        /// <summary>Rises by one every time the at-target count crosses the meet threshold upward. Drives the meet flash and chord.</summary>
        public int MeetSerial { get; private set; }

        /// <summary>Rises by one when a round's verdict closes as a miss. Drives the miss sound.</summary>
        public int MissSerial { get; private set; }

        public Vector2 StartPoint { get; private set; }

        /// <summary>Round 1's start point: where a moved start point began and returns to.</summary>
        public Vector2 HomeStartPoint { get; private set; }

        /// <summary>
        /// Where the start point would have to be for the theorem to hold (the focus), glided with the
        /// outline during a transition. Differs from <see cref="StartPoint"/> only when red is off it.
        /// </summary>
        public Vector2 IdealStartPoint { get; private set; }

        public Vector2 TargetPoint { get; private set; }

        /// <summary>
        /// The boundary to draw right now, as a fixed number of points. During a transition it's the
        /// blend of the previous and next round's outlines.
        /// </summary>
        public IReadOnlyList<Vector2> Outline => _outline;

        public bool OutlineClosed { get; private set; }

        /// <summary>Total wall hits so far this run. Hits are kept in a ring buffer; see <see cref="TryGetWallHit"/>.</summary>
        public long WallHitCount { get; private set; }

        /// <summary>
        /// Wall hit number <paramref name="serial"/> (0-based, run-wide). False once it has fallen out of
        /// the ring buffer (the last 4096 are kept).
        /// </summary>
        public bool TryGetWallHit(long serial, out WallHit hit)
        {
            if (serial < 0 || serial >= WallHitCount || serial < WallHitCount - WallHitRingSize)
            {
                hit = default;
                return false;
            }

            hit = _hitRing[(int)(serial % WallHitRingSize)];
            return true;
        }

        public void Initialize(int seed, SimulationConfig config)
        {
            Config = (ConvergenceConfig)config;
            _dt = Config.fixedTimestep;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 4096);

            int rounds = Config.RoundCount;
            _angleOffsets = new double[rounds];
            float maxOffset = Config.maxAngleOffsetDegrees;
            for (int i = 0; i < rounds; i++)
            {
                _angleOffsets[i] = _random.NextFloat(-maxOffset, maxOffset) * Mathf.Deg2Rad;
            }

            _recorder.SetMetric("rounds_planned", rounds);
            _recorder.SetMetric("ball_count", Config.ballCount);
            _recorder.SetMetric("ball_count_max", Config.RoundCount > 0 ? MaxRoundBalls() : Config.ballCount);
            _recorder.SetMetric("meet_threshold", Config.MeetThreshold);

            _maxBalls = 0;
            for (int i = 0; i < rounds; i++)
            {
                _maxBalls = Mathf.Max(_maxBalls, Config.RoundBallCount(i));
            }

            _departed = new bool[_maxBalls];

            MeetScore = 0;
            MissScore = 0;
            MeetSerial = 0;
            MissSerial = 0;
            WallHitCount = 0;
            _totalWallHits = 0;
            IsComplete = false;

            SetupRound(0);
        }

        public void Tick(float fixedDelta)
        {
            if (IsComplete || _recorder == null)
            {
                return;
            }

            _recorder.AdvanceTick();

            switch (Phase)
            {
                case ConvergencePhase.Transition:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    BlendOutline(PhaseProgress);
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(ConvergencePhase.Hold);
                    }

                    break;

                case ConvergencePhase.Hold:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(ConvergencePhase.Running);
                    }

                    break;

                case ConvergencePhase.Running:
                    TickRunning(fixedDelta);
                    break;

                case ConvergencePhase.Result:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    if (_phaseTick >= _phaseTicks)
                    {
                        if (RoundIndex + 1 < Config.RoundCount)
                        {
                            SetupRound(RoundIndex + 1);
                        }
                        else
                        {
                            EnterPhase(ConvergencePhase.EndCard);
                        }
                    }

                    break;

                case ConvergencePhase.EndCard:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(ConvergencePhase.Done);
                    }

                    break;
            }
        }

        int MaxRoundBalls()
        {
            int max = 0;
            for (int i = 0; i < Config.RoundCount; i++)
            {
                max = Mathf.Max(max, Config.RoundBallCount(i));
            }

            return max;
        }

        float Progress()
        {
            return _phaseTicks > 0 ? Mathf.Clamp01((float)_phaseTick / _phaseTicks) : 1f;
        }

        int TicksFor(float seconds)
        {
            return Mathf.Max(0, Mathf.RoundToInt(seconds / _dt));
        }

        /// <summary>Build the kernel and outline for round <paramref name="index"/> and enter its first phase.</summary>
        void SetupRound(int index)
        {
            RoundIndex = index;
            _kernel = Config.CreateKernel(index);
            _kernel.SampleOutline(OutlineSamples, _outlineScratch);
            OutlineClosed = _kernel.OutlineClosed;

            bool first = index == 0;
            if (!first)
            {
                for (int i = 0; i < OutlineSamples; i++)
                {
                    _fromOutline[i] = _toOutline[i];
                }

                _fromStart = _toStart;
                _fromTarget = _toTarget;
                _fromIdeal = _toIdeal;
            }

            for (int i = 0; i < OutlineSamples; i++)
            {
                Double2 p = _outlineScratch[i];
                _toOutline[i] = new Vector2((float)p.X, (float)p.Y);
            }

            _toStart = ToVector(_kernel.Start);
            _toIdeal = ToVector(_kernel.IdealStart);
            if (first)
            {
                HomeStartPoint = _toStart;
                _fromIdeal = _toIdeal;
            }

            _toTarget = ToVector(_kernel.Target);

            HasVerdict = false;
            VerdictCount = 0;
            AtTargetCount = 0;
            _windowMax = 0;
            _particleCount = 0;

            EnterPhase(first ? ConvergencePhase.Hold : ConvergencePhase.Transition);
        }

        void EnterPhase(ConvergencePhase phase)
        {
            Phase = phase;
            _phaseTick = 0;
            PhaseProgress = 0f;

            switch (phase)
            {
                case ConvergencePhase.Transition:
                    _particleCount = 0;
                    _phaseTicks = TicksFor(Config.transitionSeconds);
                    BlendOutline(0f);
                    break;

                case ConvergencePhase.Hold:
                    _particleCount = 0;
                    if (Config.showBodiesDuringHold)
                    {
                        // Frozen launch layout: no Step runs in Hold, and Running re-launches fresh.
                        LaunchBodies();
                    }

                    _phaseTicks = TicksFor(RoundIndex == 0 ? Config.firstHoldSeconds : Config.preLaunchHoldSeconds);
                    ShowTarget();
                    _recorder.LogEvent($"round_{RoundIndex + 1}_hold");
                    break;

                case ConvergencePhase.Running:
                    LaunchBodies();
                    _phaseTicks = TicksFor(CurrentRound.runSeconds);
                    _runTick = 0;
                    _recorder.LogEvent($"round_{RoundIndex + 1}_launch", Config.RoundBallCount(RoundIndex));
                    break;

                case ConvergencePhase.Result:
                    _phaseTicks = TicksFor(Config.resultHoldSeconds);
                    break;

                case ConvergencePhase.EndCard:
                    _particleCount = 0;
                    _phaseTicks = TicksFor(Config.endCardSeconds);
                    _recorder.LogEvent("end_card");
                    break;

                case ConvergencePhase.Done:
                    FinishRun();
                    return;
            }

            // A zero-length phase passes straight through (Running is never zero: runSeconds >= 0.5).
            if (_phaseTicks <= 0 && phase != ConvergencePhase.Running)
            {
                PhaseProgress = 1f;
                switch (phase)
                {
                    case ConvergencePhase.Transition:
                        EnterPhase(ConvergencePhase.Hold);
                        break;
                    case ConvergencePhase.Hold:
                        EnterPhase(ConvergencePhase.Running);
                        break;
                    case ConvergencePhase.Result:
                        if (RoundIndex + 1 < Config.RoundCount)
                        {
                            SetupRound(RoundIndex + 1);
                        }
                        else
                        {
                            EnterPhase(ConvergencePhase.EndCard);
                        }

                        break;
                    case ConvergencePhase.EndCard:
                        EnterPhase(ConvergencePhase.Done);
                        break;
                }
            }
        }

        void ShowTarget()
        {
            BlendOutline(1f);
        }

        /// <summary>Write the point-by-point blend of the previous and current outlines into <see cref="Outline"/>.</summary>
        void BlendOutline(float t)
        {
            bool hasFrom = RoundIndex > 0;
            float e = t * t * (3f - 2f * t);
            _outline.Clear();
            for (int i = 0; i < OutlineSamples; i++)
            {
                _outline.Add(hasFrom ? Vector2.Lerp(_fromOutline[i], _toOutline[i], e) : _toOutline[i]);
            }

            if (hasFrom)
            {
                StartPoint = Vector2.Lerp(_fromStart, _toStart, e);
                TargetPoint = Vector2.Lerp(_fromTarget, _toTarget, e);
                IdealStartPoint = Vector2.Lerp(_fromIdeal, _toIdeal, e);
            }
            else
            {
                StartPoint = _toStart;
                TargetPoint = _toTarget;
                IdealStartPoint = _toIdeal;
            }
        }

        void LaunchBodies()
        {
            _kernel.Launch(Config.RoundBallCount(RoundIndex), _angleOffsets[RoundIndex]);
            _particleCount = _kernel.Count;
            if (_positions.Length < _particleCount)
            {
                _positions = new Vector2[_particleCount];
            }

            for (int i = 0; i < _departed.Length; i++)
            {
                _departed[i] = false;
            }

            CachePositions();
            _windowMax = 0;
            AtTargetCount = CountAtTarget();
            _prevAtTarget = AtTargetCount;
        }

        /// <summary>Departed bodies within the meet radius of the target. Marks departures as it goes.</summary>
        int CountAtTarget()
        {
            Double2 target = _kernel.Target;
            double r2 = (double)Config.meetRadius * Config.meetRadius;
            int n = 0;
            for (int i = 0; i < _particleCount; i++)
            {
                Double2 p = _kernel.GetPosition(i);
                double ex = p.X - target.X;
                double ey = p.Y - target.Y;
                bool within = ex * ex + ey * ey <= r2;
                if (!within)
                {
                    _departed[i] = true;
                }
                else if (_departed[i])
                {
                    n++;
                }
            }

            return n;
        }

        void CachePositions()
        {
            for (int i = 0; i < _particleCount; i++)
            {
                _positions[i] = ToVector(_kernel.GetPosition(i));
            }
        }

        void TickRunning(float fixedDelta)
        {
            _runTick++;
            _hitScratch.Clear();
            _kernel.Step(fixedDelta, _hitScratch);
            CachePositions();

            for (int i = 0; i < _hitScratch.Count; i++)
            {
                _hitRing[(int)(WallHitCount % WallHitRingSize)] = _hitScratch[i];
                WallHitCount++;
            }

            _totalWallHits += _hitScratch.Count;

            int threshold = Config.RoundMeetThreshold(RoundIndex);
            AtTargetCount = CountAtTarget();

            // After a Miss verdict the counter is locked, so a late bunch near the target must not flash.
            bool lateBunch = HasVerdict && Verdict == ConvergenceAnswer.Miss;
            if (!lateBunch && _prevAtTarget < threshold && AtTargetCount >= threshold)
            {
                MeetSerial++;
                _recorder.LogEvent($"round_{RoundIndex + 1}_meet", AtTargetCount);
            }

            _prevAtTarget = AtTargetCount;

            double t = _runTick * (double)_dt;
            double expected = _kernel.ExpectedMeetSeconds;
            double window = Config.verdictWindowSeconds;
            if (!HasVerdict)
            {
                // Peak over the whole first pass (launch to expected meet + window), not just around E.
                if (AtTargetCount > _windowMax)
                {
                    _windowMax = AtTargetCount;
                }

                if (t > expected + window)
                {
                    CloseVerdict();
                }
            }

            PhaseProgress = _phaseTicks > 0 ? Mathf.Clamp01((float)_runTick / _phaseTicks) : 1f;
            if (_runTick >= _phaseTicks)
            {
                if (!HasVerdict)
                {
                    CloseVerdict();
                }

                EnterPhase(ConvergencePhase.Result);
            }
        }

        void CloseVerdict()
        {
            HasVerdict = true;
            VerdictCount = _windowMax;
            Verdict = VerdictCount >= Config.RoundMeetThreshold(RoundIndex) ? ConvergenceAnswer.Meet : ConvergenceAnswer.Miss;

            if (Verdict == ConvergenceAnswer.Meet)
            {
                MeetScore++;
            }
            else
            {
                MissScore++;
                MissSerial++;
            }

            int round = RoundIndex + 1;
            _recorder.LogEvent($"round_{round}_{Verdict.ToString().ToLowerInvariant()}", VerdictCount);
            _recorder.SetMetric($"round_{round}_verdict_count", VerdictCount);
            _recorder.SetMetric($"round_{round}_wall_hits_total", _totalWallHits);
            _recorder.SetMetric("meet_score", MeetScore);
            _recorder.SetMetric("miss_score", MissScore);

            if (Verdict != CurrentRound.expected)
            {
                Debug.LogError($"[conv] Round {round} measured {Verdict} ({VerdictCount} at target, threshold " +
                               $"{Config.RoundMeetThreshold(RoundIndex)}) but the config expects {CurrentRound.expected}. Retune the round.");
            }
        }

        void FinishRun()
        {
            Phase = ConvergencePhase.Done;
            PhaseProgress = 1f;
            _particleCount = 0;
            _recorder.SetMetric("wall_hits_total", _totalWallHits);
            _recorder.Finish(CompletionReason.Finished);
            IsComplete = true;
        }

        static Vector2 ToVector(Double2 p)
        {
            return new Vector2((float)p.X, (float)p.Y);
        }
    }
}
