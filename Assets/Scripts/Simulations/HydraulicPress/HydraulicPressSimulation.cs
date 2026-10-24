using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Simulations.HydraulicPress.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.HydraulicPress
{
    /// <summary>Where the run is inside a round.</summary>
    public enum PressPhase
    {
        /// <summary>One ball on the floor, the press above it showing its tonnage, caption asking the question.</summary>
        Hold = 0,

        /// <summary>The kernel steps until the press has stopped.</summary>
        Run = 1,

        /// <summary>Result line up; nothing moves.</summary>
        Result = 2,

        /// <summary>The press lifts, the pile clears, the label flips and a fresh ball arrives (kernel swaps at the midpoint).</summary>
        Transition = 3,

        /// <summary>After the last round: the final pile at rest under the press, ladder and footnote.</summary>
        EndCard = 4,

        Done = 5
    }

    /// <summary>
    /// The round shell for <c>press</c>: Hold, Run, Result per round (Transition between), then an end card.
    /// All pile physics and the load model live in <see cref="PressKernel"/>, stepped once per fixed tick.
    /// </summary>
    /// <remarks>
    /// Determinism: every random draw comes from the run's <see cref="SeededRandom"/> (one draw per round, the
    /// kernel's own seed); phases are counted in ticks and the kernel is pure double maths, so the same seed gives
    /// bit-identical counts and positions. Display state is in world units over the floor top (y = 0).
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class HydraulicPressSimulation : MonoBehaviour, ISimulation
    {
        /// <summary>Pops kept for the presenter to read back by serial. Must be a power of two.</summary>
        const int PopRingSize = 4096;

        /// <summary>Share of the Transition spent lifting the press; the kernel and label swap right after it.</summary>
        const float TransitionSwapAt = 0.5f;

        RunRecorder _recorder;
        SeededRandom _random;
        PressKernel _kernel;
        float _dt;

        readonly PopEvent[] _ring = new PopEvent[PopRingSize];
        readonly List<int> _completed = new List<int>(8);
        Action<PopEvent> _onPop;

        int _phaseTick;
        int _phaseTicks;
        int _runTicks;
        int _maxRunTicks;
        float _liftFromY;
        bool _swapped;
        string _nextLabel = string.Empty;

        public string FormatSlug => "press";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        public HydraulicPressConfig Config { get; private set; }

        /// <summary>The run's seed (also logged by the runner).</summary>
        public int SeedUsed { get; private set; }

        public PressPhase Phase { get; private set; }

        /// <summary>0..1 through the current phase (hold, run vs its cap, result, transition, end card).</summary>
        public float PhaseProgress { get; private set; }

        /// <summary>
        /// 0-based current round. Advances at the start of the Transition into it (while the old pile is still
        /// the one on screen), like <c>clack</c>.
        /// </summary>
        public int RoundIndex { get; private set; }

        public int RoundTotal => Config != null ? Config.RoundCount : 0;

        public PressRound CurrentRound => Config != null ? Config.rounds[RoundIndex] : default;

        /// <summary>
        /// The live pile. Replaced each round: the old one stays alive (stopped, frozen) through the first half
        /// of the Transition so the presenter can fade it, and the new one is swapped in at the midpoint, the
        /// same tick <see cref="PressLabel"/> flips.
        /// </summary>
        public PressKernel Kernel => _kernel;

        /// <summary>Plate bottom, world units over the floor. Lifts during the first half of the Transition.</summary>
        public float DisplayPressY { get; private set; }

        /// <summary>The plate's decal. Flips to the next round's label at the transition's midpoint.</summary>
        public string PressLabel { get; private set; } = string.Empty;

        /// <summary>Balls in the live pile, counted from the kernel, so it is never ahead of the picture.</summary>
        public int BallCount => _kernel != null ? _kernel.Count : 0;

        /// <summary>True from the tick the press has stopped until the next round's Transition.</summary>
        public bool RoundFinished { get; private set; }

        /// <summary>Every pop this run, monotonic. The presenter diffs it per frame and reads them via <see cref="TryGetPop"/>.</summary>
        public long PopSerial { get; private set; }

        /// <summary>Final ball count of each finished round, appended on the tick <see cref="RoundFinished"/> flips.</summary>
        public IReadOnlyList<int> CompletedCounts => _completed;

        /// <summary>
        /// Pop number <paramref name="serial"/> (0-based, run-wide). False once it has fallen out of the ring (the last
        /// 4096 are kept) or hasn't happened yet. The ids inside refer to the kernel of the round it happened in.
        /// </summary>
        public bool TryGetPop(long serial, out PopEvent pop)
        {
            if (serial < 0 || serial >= PopSerial || serial < PopSerial - PopRingSize)
            {
                pop = default;
                return false;
            }

            pop = _ring[(int)(serial & (PopRingSize - 1))];
            return true;
        }

        public void Initialize(int seed, SimulationConfig config)
        {
            Config = (HydraulicPressConfig)config;
            SeedUsed = seed;
            _dt = Config.fixedTimestep;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 1024);
            _onPop = OnPop; // cached once so the cascade never allocates

            _recorder.SetMetric("rounds_planned", Config.RoundCount);
            _recorder.SetMetric("series_seconds_estimate", Config.SeriesSeconds);

            PopSerial = 0;
            _completed.Clear();
            IsComplete = false;

            RoundIndex = 0;
            BuildKernel(0);
            PressLabel = Config.rounds[0].pressLabel;
            _nextLabel = PressLabel;
            EnterPhase(PressPhase.Hold);
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
                case PressPhase.Hold:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    DisplayPressY = (float)_kernel.PressY;
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(PressPhase.Run);
                    }

                    break;

                case PressPhase.Run:
                    TickRun(fixedDelta);
                    break;

                case PressPhase.Result:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    if (_phaseTick >= _phaseTicks)
                    {
                        AfterResult();
                    }

                    break;

                case PressPhase.Transition:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    TransitionVisuals();
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(PressPhase.Hold);
                    }

                    break;

                case PressPhase.EndCard:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(PressPhase.Done);
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

        /// <summary>Per-pop work, kept O(1): ring write and serial.</summary>
        void OnPop(PopEvent pop)
        {
            _ring[(int)(PopSerial & (PopRingSize - 1))] = pop;
            PopSerial++;
        }

        void BuildKernel(int index)
        {
            PressRound r = Config.rounds[index];
            int kernelSeed = _random.NextInt(int.MaxValue); // one draw per round, in a fixed order
            _kernel = new PressKernel(Config.MakeKernelSettings(r), kernelSeed);
            RoundFinished = false;
            _runTicks = 0;
            _maxRunTicks = Mathf.Max(1, TicksFor(r.maxRunSeconds));
            DisplayPressY = (float)_kernel.PressY;
        }

        void TickRun(float fixedDelta)
        {
            _runTicks++;
            _kernel.Step(fixedDelta, _onPop);
            DisplayPressY = (float)_kernel.PressY;
            PhaseProgress = Mathf.Clamp01((float)_runTicks / _maxRunTicks);

            if (_kernel.IsStopped)
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
            int count = _kernel.Count;
            _completed.Add(count);

            _recorder.LogEvent($"round_{round}_done", count);
            _recorder.SetMetric($"round_{round}_balls", count);
            _recorder.SetMetric($"round_{round}_run_seconds", _runTicks * _dt);
            _recorder.SetMetric($"round_{round}_max_generation", _kernel.MaxGeneration);
            _recorder.SetMetric($"round_{round}_minradius_hits", _kernel.MinRadiusHits);

            if (timedOut)
            {
                Debug.LogError($"[press] round {round}: still running after {_runTicks * _dt:0.0}s (maxRunSeconds) " +
                               $"with {count} balls. Retune the round's cap or the solver.");
            }

            if (_kernel.MinRadiusHits > 0)
            {
                Debug.LogWarning($"[press] round {round}: {_kernel.MinRadiusHits} ball(s) hit minRadius and could not split " +
                                 "(a tuning failure).");
            }

            if (_kernel.Overflow)
            {
                Debug.LogError($"[press] round {round}: reached maxBalls ({Config.maxBalls}); pops were blocked.");
            }

            CheckInsideChamber(round);
            EnterPhase(PressPhase.Result);
        }

        /// <summary>Logs an error if any ball ended outside the chamber (it never should).</summary>
        void CheckInsideChamber(int round)
        {
            double hw = Config.chamberWidth * 0.5;
            double[] x = _kernel.X, y = _kernel.Y, r = _kernel.R;
            for (int i = 0; i < _kernel.Count; i++)
            {
                if (x[i] < -hw + r[i] - 1e-4 || x[i] > hw - r[i] + 1e-4 || y[i] < r[i] - 1e-4 ||
                    y[i] > Config.chamberHeight + 1e-4)
                {
                    Debug.LogError($"[press] round {round}: ball {i} ended outside the chamber ({x[i]:0.00}, {y[i]:0.00}).");
                    return;
                }
            }
        }

        void AfterResult()
        {
            if (RoundIndex + 1 < Config.RoundCount)
            {
                BeginTransition();
            }
            else
            {
                EnterPhase(PressPhase.EndCard);
            }
        }

        void BeginTransition()
        {
            RoundIndex++;
            _liftFromY = (float)_kernel.PressY;
            _swapped = false;
            _nextLabel = Config.rounds[RoundIndex].pressLabel; // PressLabel keeps the old label until the midpoint
            EnterPhase(PressPhase.Transition);
        }

        /// <summary>First half: the press lifts. At the midpoint: the new kernel (one fresh ball) and the new label.</summary>
        void TransitionVisuals()
        {
            float p = PhaseProgress;
            if (!_swapped)
            {
                float u = Mathf.Clamp01(p / TransitionSwapAt);
                float e = u * u * (3f - 2f * u);
                DisplayPressY = Mathf.Lerp(_liftFromY, Config.chamberHeight, e);
                if (p >= TransitionSwapAt)
                {
                    SwapToNextRound();
                }
            }
            else
            {
                DisplayPressY = (float)_kernel.PressY;
            }
        }

        void SwapToNextRound()
        {
            _swapped = true;
            BuildKernel(RoundIndex);
            PressLabel = _nextLabel;
        }

        void EnterPhase(PressPhase phase)
        {
            Phase = phase;
            _phaseTick = 0;
            PhaseProgress = 0f;
            int round = RoundIndex + 1;

            switch (phase)
            {
                case PressPhase.Hold:
                    _phaseTicks = TicksFor(RoundIndex == 0 ? Config.firstHoldSeconds : Config.holdSeconds);
                    DisplayPressY = (float)_kernel.PressY;
                    _recorder.LogEvent($"round_{round}_hold");
                    break;

                case PressPhase.Run:
                    _phaseTicks = _maxRunTicks;
                    _runTicks = 0;
                    _recorder.LogEvent($"round_{round}_press_drop");
                    break;

                case PressPhase.Result:
                    _phaseTicks = TicksFor(Config.resultHoldSeconds);
                    break;

                case PressPhase.Transition:
                    _phaseTicks = TicksFor(Config.transitionSeconds);
                    break;

                case PressPhase.EndCard:
                    _phaseTicks = TicksFor(Config.endCardSeconds);
                    _recorder.LogEvent("end_card");
                    break;

                case PressPhase.Done:
                    FinishRun();
                    return;
            }

            // A zero-length phase passes straight through (Run is never zero: maxRunTicks >= 1).
            if (_phaseTicks <= 0 && phase != PressPhase.Run)
            {
                PhaseProgress = 1f;
                switch (phase)
                {
                    case PressPhase.Hold:
                        EnterPhase(PressPhase.Run);
                        break;
                    case PressPhase.Result:
                        AfterResult();
                        break;
                    case PressPhase.Transition:
                        if (!_swapped)
                        {
                            SwapToNextRound();
                        }

                        DisplayPressY = (float)_kernel.PressY;
                        EnterPhase(PressPhase.Hold);
                        break;
                    case PressPhase.EndCard:
                        EnterPhase(PressPhase.Done);
                        break;
                }
            }
        }

        void FinishRun()
        {
            Phase = PressPhase.Done;
            PhaseProgress = 1f;
            _recorder.SetMetric("pops_total", PopSerial);
            _recorder.Finish(CompletionReason.Finished);
            IsComplete = true;
        }
    }
}
