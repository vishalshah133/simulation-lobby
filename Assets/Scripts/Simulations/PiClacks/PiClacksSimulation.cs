using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Simulations.PiClacks.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.PiClacks
{
    /// <summary>Where the run is inside a round.</summary>
    public enum PiClacksPhase
    {
        /// <summary>Blocks static at the round's start layout, caption asking the question.</summary>
        Hold = 0,

        /// <summary>The kernel steps until no clack is possible.</summary>
        Run = 1,

        /// <summary>Result line up; the blocks keep gliding with their final velocities (no clacks possible).</summary>
        Result = 2,

        /// <summary>Blocks glide to the next round's start, the big block grows, its label flips at the midpoint.</summary>
        Transition = 3,

        /// <summary>After the last round: blocks settle at the start layout, ladder and pi line.</summary>
        EndCard = 4,

        Done = 5
    }

    /// <summary>
    /// The round shell for <c>clack</c>: Hold, Run, Result per round (Transition between), then an end card.
    /// All collision maths lives in <see cref="BlockCollisionKernel"/>, stepped once per fixed tick.
    /// </summary>
    /// <remarks>
    /// Determinism: no random draw affects anything (the seed is cosmetic and only recorded); phases are counted
    /// in ticks and the kernel is pure double maths. The per-clack work is O(1) (two counters and a ring write)
    /// because round 7 produces over three million clacks, half of them inside a single tick.
    /// Display state is in world units relative to the wall face (x = 0) and floor top (y = 0).
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class PiClacksSimulation : MonoBehaviour, ISimulation
    {
        /// <summary>Clacks kept for the presenter to read back by serial. Must be a power of two.</summary>
        const int ClackRingSize = 256;

        RunRecorder _recorder;
        SeededRandom _random;
        BlockCollisionKernel _kernel;
        float _dt;

        readonly ClackEvent[] _ring = new ClackEvent[ClackRingSize];
        readonly List<long> _completed = new List<long>(8);
        Action<ClackEvent> _onClack;

        int _phaseTick;
        int _phaseTicks;
        int _runTicks;
        int _maxRunTicks;
        long _roundClacks;

        // Glide endpoints for Transition and the end card (display floats).
        float _fromSmallX;
        float _fromBigX;
        float _fromBigSize;
        float _toSmallX;
        float _toBigX;
        float _toBigSize;
        string _nextLabel = string.Empty;

        public string FormatSlug => "clack";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        public PiClacksConfig Config { get; private set; }

        /// <summary>The run's seed. Cosmetic only: the outcome never depends on it.</summary>
        public int Seed { get; private set; }

        public PiClacksPhase Phase { get; private set; }

        /// <summary>0..1 through the current phase (hold, run vs its cap, transition glide, result, end card).</summary>
        public float PhaseProgress { get; private set; }

        /// <summary>0-based current round (advances at the start of the Transition into it).</summary>
        public int RoundIndex { get; private set; }

        public int RoundTotal => Config != null ? Config.RoundCount : 0;

        public PiClacksRound CurrentRound => Config != null ? Config.rounds[RoundIndex] : default;

        public float SmallX { get; private set; }

        public float SmallSize => Config != null ? Config.smallSize : 1f;

        public float BigX { get; private set; }

        public float BigSize { get; private set; }

        /// <summary>The big block's decal. Flips to the next round's label at the transition's midpoint.</summary>
        public string BigMassLabel { get; private set; } = string.Empty;

        /// <summary>Clacks this round, live. Counted per kernel event, so it is never ahead of the picture.</summary>
        public long ClackCount => _roundClacks;

        /// <summary>N(k) for the current round: ceil(pi / atan(sqrt(m / M))) - 1.</summary>
        public long ExpectedCount { get; private set; }

        /// <summary>True from the tick the kernel is done (no clack possible) until the next round's transition.</summary>
        public bool RoundFinished { get; private set; }

        /// <summary>Every clack this run, monotonic. The presenter diffs it per frame and reads them via <see cref="TryGetClack"/>.</summary>
        public long ClackSerial { get; private set; }

        /// <summary>Final count of each finished round, appended on the tick <see cref="RoundFinished"/> flips.</summary>
        public IReadOnlyList<long> CompletedCounts => _completed;

        /// <summary>
        /// Clack number <paramref name="serial"/> (0-based, run-wide). False once it has fallen out of the ring
        /// (the last 256 are kept) or hasn't happened yet.
        /// </summary>
        public bool TryGetClack(long serial, out ClackEvent clack)
        {
            if (serial < 0 || serial >= ClackSerial || serial < ClackSerial - ClackRingSize)
            {
                clack = default;
                return false;
            }

            clack = _ring[(int)(serial & (ClackRingSize - 1))];
            return true;
        }

        public void Initialize(int seed, SimulationConfig config)
        {
            Config = (PiClacksConfig)config;
            Seed = seed;
            _dt = Config.fixedTimestep;
            _random = new SeededRandom(seed); // never drawn from: the seed is cosmetic here
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 1024);
            _onClack = OnClack; // cached once so the 3-million-event inner loop never allocates

            _recorder.SetMetric("rounds_planned", Config.RoundCount);
            _recorder.SetMetric("series_seconds_estimate", Config.SeriesSeconds);

            ClackSerial = 0;
            _completed.Clear();
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
                case PiClacksPhase.Hold:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(PiClacksPhase.Run);
                    }

                    break;

                case PiClacksPhase.Run:
                    TickRun(fixedDelta);
                    break;

                case PiClacksPhase.Result:
                    // No clack can happen now, so this only carries the blocks along with their final velocities.
                    _kernel.Step(fixedDelta, _onClack);
                    CacheKernelPositions();
                    _phaseTick++;
                    PhaseProgress = Progress();
                    if (_phaseTick >= _phaseTicks)
                    {
                        if (RoundIndex + 1 < Config.RoundCount)
                        {
                            SetupNextRound();
                        }
                        else
                        {
                            EnterPhase(PiClacksPhase.EndCard);
                        }
                    }

                    break;

                case PiClacksPhase.Transition:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    Glide(PhaseProgress);
                    if (PhaseProgress >= 0.5f)
                    {
                        BigMassLabel = _nextLabel;
                    }

                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(PiClacksPhase.Hold);
                    }

                    break;

                case PiClacksPhase.EndCard:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    // The blocks settle at the last round's start layout over the first second of the card.
                    Glide(Mathf.Clamp01(_phaseTick * _dt));
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(PiClacksPhase.Done);
                    }

                    break;
            }
        }

        float Progress()
        {
            return _phaseTicks > 0 ? Mathf.Clamp01((float)_phaseTick / _phaseTicks) : 1f;
        }

        int TicksFor(float seconds)
        {
            return Mathf.Max(0, Mathf.RoundToInt(seconds / _dt));
        }

        /// <summary>Per-clack work, kept O(1): count, ring write, serial.</summary>
        void OnClack(ClackEvent clack)
        {
            _ring[(int)(ClackSerial & (ClackRingSize - 1))] = clack;
            ClackSerial++;
            _roundClacks++;
        }

        /// <summary>First round: build its kernel at the start layout and hold.</summary>
        void SetupRound(int index)
        {
            RoundIndex = index;
            BuildKernel(index);
            BigMassLabel = Config.rounds[index].massLabel;
            _nextLabel = BigMassLabel;
            SetStartLayout(index);
            EnterPhase(PiClacksPhase.Hold);
        }

        /// <summary>Later rounds: new kernel and counters, then a glide from wherever the blocks ended up.</summary>
        void SetupNextRound()
        {
            int next = RoundIndex + 1;
            RoundIndex = next;
            BuildKernel(next);

            _fromSmallX = SmallX;
            _fromBigX = BigX;
            _fromBigSize = BigSize;
            PiClacksRound r = Config.rounds[next];
            _toSmallX = r.smallGap;
            _toBigX = StartBigX(r);
            _toBigSize = r.bigSize;
            _nextLabel = r.massLabel; // BigMassLabel keeps the old label until the midpoint

            EnterPhase(PiClacksPhase.Transition);
        }

        void BuildKernel(int index)
        {
            PiClacksRound r = Config.rounds[index];
            double ratio = Math.Pow(100.0, r.massExponent);
            _kernel = new BlockCollisionKernel(1.0, ratio, Config.smallSize, r.smallGap, r.bigGap, r.speed);
            ExpectedCount = BlockCollisionKernel.ExpectedCount(ratio);
            _roundClacks = 0;
            RoundFinished = false;
            _runTicks = 0;
            _maxRunTicks = Mathf.Max(1, TicksFor(r.maxRunSeconds));
        }

        float StartBigX(PiClacksRound r)
        {
            return r.smallGap + Config.smallSize + r.bigGap;
        }

        void SetStartLayout(int index)
        {
            PiClacksRound r = Config.rounds[index];
            SmallX = r.smallGap;
            BigX = StartBigX(r);
            BigSize = r.bigSize;
        }

        void CacheKernelPositions()
        {
            SmallX = (float)_kernel.SmallX;
            BigX = (float)_kernel.BigX;
        }

        /// <summary>Smoothstep from the glide's start to its end layout.</summary>
        void Glide(float t)
        {
            float e = t * t * (3f - 2f * t);
            SmallX = Mathf.Lerp(_fromSmallX, _toSmallX, e);
            BigX = Mathf.Lerp(_fromBigX, _toBigX, e);
            BigSize = Mathf.Lerp(_fromBigSize, _toBigSize, e);
        }

        void EnterPhase(PiClacksPhase phase)
        {
            Phase = phase;
            _phaseTick = 0;
            PhaseProgress = 0f;
            int round = RoundIndex + 1;

            switch (phase)
            {
                case PiClacksPhase.Hold:
                    _phaseTicks = TicksFor(RoundIndex == 0 ? Config.firstHoldSeconds : Config.preLaunchHoldSeconds);
                    SetStartLayout(RoundIndex);
                    BigMassLabel = Config.rounds[RoundIndex].massLabel;
                    _recorder.LogEvent($"round_{round}_hold");
                    break;

                case PiClacksPhase.Run:
                    _phaseTicks = _maxRunTicks;
                    _runTicks = 0;
                    _recorder.LogEvent($"round_{round}_launch");
                    break;

                case PiClacksPhase.Result:
                    _phaseTicks = TicksFor(Config.resultHoldSeconds);
                    break;

                case PiClacksPhase.Transition:
                    _phaseTicks = TicksFor(Config.transitionSeconds);
                    break;

                case PiClacksPhase.EndCard:
                    _phaseTicks = TicksFor(Config.endCardSeconds);
                    // Settle at the last round's own start layout, from wherever the blocks drifted to.
                    _fromSmallX = SmallX;
                    _fromBigX = BigX;
                    _fromBigSize = BigSize;
                    PiClacksRound last = Config.rounds[RoundIndex];
                    _toSmallX = last.smallGap;
                    _toBigX = StartBigX(last);
                    _toBigSize = last.bigSize;
                    _recorder.LogEvent("end_card");
                    break;

                case PiClacksPhase.Done:
                    FinishRun();
                    return;
            }

            // A zero-length phase passes straight through (Run is never zero: maxRunTicks >= 1).
            if (_phaseTicks <= 0 && phase != PiClacksPhase.Run)
            {
                PhaseProgress = 1f;
                switch (phase)
                {
                    case PiClacksPhase.Hold:
                        EnterPhase(PiClacksPhase.Run);
                        break;
                    case PiClacksPhase.Result:
                        if (RoundIndex + 1 < Config.RoundCount)
                        {
                            SetupNextRound();
                        }
                        else
                        {
                            EnterPhase(PiClacksPhase.EndCard);
                        }

                        break;
                    case PiClacksPhase.Transition:
                        Glide(1f);
                        BigMassLabel = _nextLabel;
                        EnterPhase(PiClacksPhase.Hold);
                        break;
                    case PiClacksPhase.EndCard:
                        EnterPhase(PiClacksPhase.Done);
                        break;
                }
            }
        }

        void TickRun(float fixedDelta)
        {
            _runTicks++;
            _kernel.Step(fixedDelta, _onClack);
            CacheKernelPositions();
            PhaseProgress = Mathf.Clamp01((float)_runTicks / _maxRunTicks);

            if (_kernel.IsDone)
            {
                FinishRound(false);
            }
            else if (_runTicks >= _maxRunTicks)
            {
                FinishRound(true);
            }
        }

        void FinishRound(bool timedOut)
        {
            int round = RoundIndex + 1;
            RoundFinished = true;
            _completed.Add(_roundClacks);

            _recorder.LogEvent($"round_{round}_done", _roundClacks);
            _recorder.SetMetric($"round_{round}_clacks", _roundClacks);
            _recorder.SetMetric($"round_{round}_run_seconds", _runTicks * _dt);
            _recorder.SetMetric($"round_{round}_last_clack_seconds", (float)_kernel.LastClackTime);

            if (timedOut)
            {
                Debug.LogError($"[clack] round {round}: still running after {_runTicks * _dt:0.0}s " +
                               $"(maxRunSeconds). Retune the round's gaps or speed.");
            }

            if (_roundClacks != ExpectedCount)
            {
                Debug.LogError($"[clack] round {round}: counted {_roundClacks}, expected {ExpectedCount}");
            }

            EnterPhase(PiClacksPhase.Result);
        }

        void FinishRun()
        {
            Phase = PiClacksPhase.Done;
            PhaseProgress = 1f;
            _recorder.SetMetric("clacks_total", ClackSerial);
            _recorder.Finish(CompletionReason.Finished);
            IsComplete = true;
        }
    }
}
