using SimulationLobby.Core;
using SimulationLobby.Simulations.BellCurve.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.BellCurve
{
    /// <summary>Where the run is inside a round.</summary>
    public enum BellPhase
    {
        /// <summary>Empty board, the question and "GUESS?" up.</summary>
        Hold = 0,

        /// <summary>Marbles leave the spout; the pile builds.</summary>
        Run = 1,

        /// <summary>Everything has landed; the result line is up.</summary>
        Result = 2,

        /// <summary>The pile drains, the pegs re-rig, the next round's caption is up.</summary>
        Transition = 3,

        /// <summary>After the last round: the final pile stays, the why-line is up.</summary>
        EndCard = 4,

        Done = 5
    }

    /// <summary>
    /// The round shell for <c>bell</c>: Hold, Run, Result per round (Transition between), then an end card.
    /// At the start of a round every marble's path is drawn from the seeded generator (one flip per peg row), which
    /// fixes its bin, its spawn time and the moment it lands; after that, ticking is just a clock. The presenter
    /// reads the arrays and places each marble with <see cref="BoardGeometry.TryPose"/>.
    /// </summary>
    /// <remarks>
    /// Determinism: all randomness is the coin flips, drawn from a per-round branch of the run's
    /// <see cref="SeededRandom"/>; phases are counted in ticks. Same seed, same config, same piles.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class BellCurveSimulation : MonoBehaviour, ISimulation
    {
        RunRecorder _recorder;
        SeededRandom _random;
        BoardGeometry _geometry;
        float _dt;

        // Per-marble data of the round whose pile is on screen (indexed in spawn order).
        float[] _spawnSeconds = new float[0];
        float[] _landSeconds = new float[0];
        uint[] _pathBits = new uint[0];
        int[] _bin = new int[0];
        int _marbleCount;
        int _spawnedCount;
        int _landPtr;
        int[] _landed;
        BellRound _dataRound;
        float _heightPerMarble;

        int _phaseTick;
        int _phaseTicks;
        int _runTicks;
        int _settleTicks;
        float _leanFrom;
        float _leanTo;
        readonly System.Collections.Generic.List<string> _completedRows = new System.Collections.Generic.List<string>(8);

        public string FormatSlug => "bell";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        public BellCurveConfig Config { get; private set; }

        public int Seed { get; private set; }

        public BellPhase Phase { get; private set; }

        /// <summary>0..1 through the current phase.</summary>
        public float PhaseProgress { get; private set; }

        /// <summary>0-based round whose text is current (advances at the start of the Transition into it).</summary>
        public int RoundIndex { get; private set; }

        public int RoundTotal => Config != null ? Config.RoundCount : 0;

        public BellRound CurrentRound => Config != null ? Config.rounds[RoundIndex] : default;

        /// <summary>The round the pile on screen belongs to (differs from <see cref="CurrentRound"/> during a Transition).</summary>
        public BellRound DataRound => _dataRound;

        public BoardGeometry Geometry => _geometry;

        /// <summary>Seconds since the round's first marble could leave the spout (0 outside Run/Result).</summary>
        public float RunSeconds { get; private set; }

        public int MarbleCount => _marbleCount;

        /// <summary>Marbles that have left the spout (a prefix of the arrays).</summary>
        public int SpawnedCount => _spawnedCount;

        /// <summary>Marbles that have landed (a prefix of the arrays; landing times are non-decreasing).</summary>
        public int LandedCount => _landPtr;

        /// <summary>Per-bin landed counts. Do not write.</summary>
        public int[] Landed => _landed;

        public float[] SpawnSeconds => _spawnSeconds;

        public float[] LandSeconds => _landSeconds;

        public uint[] PathBits => _pathBits;

        public int[] BinOf => _bin;

        /// <summary>Bar height per landed marble for the pile on screen.</summary>
        public float BarHeightPerMarble => _heightPerMarble;

        /// <summary>How far the pegs are drawn leaning, -1..1, easing between rounds during a Transition.</summary>
        public float PegLean { get; private set; }

        /// <summary>0 = full pile, 1 = drained. Rises through the first part of a Transition.</summary>
        public float ClearProgress { get; private set; }

        /// <summary>True from the tick the last marble has landed and settled, until the next round's transition.</summary>
        public bool RoundFinished { get; private set; }

        /// <summary>Ladder rows of the finished rounds, appended on the tick <see cref="RoundFinished"/> flips.</summary>
        public System.Collections.Generic.IReadOnlyList<string> CompletedRows => _completedRows;

        public void Initialize(int seed, SimulationConfig config)
        {
            Config = (BellCurveConfig)config;
            Seed = seed;
            _dt = Config.fixedTimestep;
            _geometry = Config.Geometry;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 512);
            _recorder.SetMetric("rounds_planned", Config.RoundCount);
            _recorder.SetMetric("series_seconds_estimate", Config.SeriesSeconds);

            _completedRows.Clear();
            IsComplete = false;
            RoundIndex = 0;
            BuildRoundData(0);
            PegLean = Config.rounds[0].pegLean;
            EnterPhase(BellPhase.Hold);
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
                case BellPhase.Hold:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(BellPhase.Run);
                    }

                    break;

                case BellPhase.Run:
                    TickRun();
                    break;

                case BellPhase.Result:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    if (_phaseTick >= _phaseTicks)
                    {
                        if (RoundIndex + 1 < Config.RoundCount)
                        {
                            EnterTransition();
                        }
                        else
                        {
                            EnterPhase(BellPhase.EndCard);
                        }
                    }

                    break;

                case BellPhase.Transition:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    ClearProgress = Smooth(Mathf.Clamp01(PhaseProgress / 0.6f));
                    PegLean = Mathf.Lerp(_leanFrom, _leanTo, Smooth(PhaseProgress));
                    if (_phaseTick >= _phaseTicks)
                    {
                        BuildRoundData(RoundIndex);
                        EnterPhase(BellPhase.Hold);
                    }

                    break;

                case BellPhase.EndCard:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(BellPhase.Done);
                    }

                    break;
            }
        }

        static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }

        float Progress()
        {
            return _phaseTicks > 0 ? Mathf.Clamp01((float)_phaseTick / _phaseTicks) : 1f;
        }

        int TicksFor(float seconds)
        {
            return Mathf.Max(0, Mathf.RoundToInt(seconds / _dt));
        }

        /// <summary>Draws every marble's coin flips and works out when it spawns and lands.</summary>
        void BuildRoundData(int index)
        {
            BellRound round = Config.rounds[index];
            _dataRound = round;
            _marbleCount = round.marbles;
            _heightPerMarble = Config.HeightPerMarble(round);
            _spawnedCount = 0;
            _landPtr = 0;
            RunSeconds = 0f;
            ClearProgress = 0f;
            RoundFinished = false;

            if (_spawnSeconds.Length < _marbleCount)
            {
                _spawnSeconds = new float[_marbleCount];
                _landSeconds = new float[_marbleCount];
                _pathBits = new uint[_marbleCount];
                _bin = new int[_marbleCount];
            }

            int bins = _geometry.Bins;
            _landed = new int[bins];
            var rank = new int[bins];
            SeededRandom flips = _random.Branch(index + 1);
            float floorY = Config.TrayFloorY;
            float topY = floorY + Config.trayHeight * 1.15f;
            float previousLand = 0f;

            for (int i = 0; i < _marbleCount; i++)
            {
                uint bits = 0u;
                for (int r = 0; r < _geometry.Rows; r++)
                {
                    if (flips.Chance(round.rightProbability))
                    {
                        bits |= 1u << r;
                    }
                }

                int bin = BoardGeometry.RightsInFirst(bits, _geometry.Rows);
                float target = Mathf.Min(topY, floorY + (rank[bin] + 1) * _heightPerMarble);
                rank[bin]++;

                float spawn = i / round.spawnPerSecond;
                float land = Mathf.Max(previousLand, spawn + _geometry.LandAge(target));
                previousLand = land;

                _spawnSeconds[i] = spawn;
                _landSeconds[i] = land;
                _pathBits[i] = bits;
                _bin[i] = bin;
            }

            float peak = 0f;
            for (int k = 0; k < bins; k++)
            {
                peak = Mathf.Max(peak, rank[k]);
            }

            if (peak * _heightPerMarble > Config.trayHeight * 1.15f)
            {
                Debug.LogWarning($"[bell] round {index + 1}: tallest bin holds {peak:0} of {_marbleCount} marbles " +
                                 $"({peak / _marbleCount:P0}), past the tray. Raise peakFraction or lower the lean.");
            }
        }

        void EnterTransition()
        {
            RoundIndex++;
            _leanFrom = PegLean;
            _leanTo = Config.rounds[RoundIndex].pegLean;
            EnterPhase(BellPhase.Transition);
        }

        void EnterPhase(BellPhase phase)
        {
            Phase = phase;
            _phaseTick = 0;
            PhaseProgress = 0f;
            int round = RoundIndex + 1;

            switch (phase)
            {
                case BellPhase.Hold:
                    _phaseTicks = TicksFor(RoundIndex == 0 ? Config.firstHoldSeconds : Config.preDropHoldSeconds);
                    PegLean = Config.rounds[RoundIndex].pegLean;
                    ClearProgress = 0f;
                    _recorder.LogEvent($"round_{round}_hold");
                    break;

                case BellPhase.Run:
                    _runTicks = 0;
                    _settleTicks = -1;
                    _phaseTicks = int.MaxValue;
                    _recorder.LogEvent($"round_{round}_drop", _marbleCount);
                    break;

                case BellPhase.Result:
                    _phaseTicks = TicksFor(Config.resultHoldSeconds);
                    break;

                case BellPhase.Transition:
                    _phaseTicks = TicksFor(Config.transitionSeconds);
                    break;

                case BellPhase.EndCard:
                    _phaseTicks = TicksFor(Config.endCardSeconds);
                    _recorder.LogEvent("end_card");
                    break;

                case BellPhase.Done:
                    FinishRun();
                    return;
            }

            // A zero-length phase passes straight through.
            if (_phaseTicks <= 0 && phase != BellPhase.Run)
            {
                PhaseProgress = 1f;
                switch (phase)
                {
                    case BellPhase.Hold:
                        EnterPhase(BellPhase.Run);
                        break;
                    case BellPhase.Result:
                        if (RoundIndex + 1 < Config.RoundCount)
                        {
                            EnterTransition();
                        }
                        else
                        {
                            EnterPhase(BellPhase.EndCard);
                        }

                        break;
                    case BellPhase.Transition:
                        BuildRoundData(RoundIndex);
                        EnterPhase(BellPhase.Hold);
                        break;
                    case BellPhase.EndCard:
                        EnterPhase(BellPhase.Done);
                        break;
                }
            }
        }

        void TickRun()
        {
            _runTicks++;
            RunSeconds = _runTicks * _dt;

            while (_spawnedCount < _marbleCount && _spawnSeconds[_spawnedCount] <= RunSeconds)
            {
                _spawnedCount++;
            }

            while (_landPtr < _marbleCount && _landSeconds[_landPtr] <= RunSeconds)
            {
                _landed[_bin[_landPtr]]++;
                _landPtr++;
            }

            PhaseProgress = _marbleCount > 0 ? (float)_landPtr / _marbleCount : 1f;

            if (_landPtr >= _marbleCount)
            {
                if (_settleTicks < 0)
                {
                    _settleTicks = 0;
                }

                _settleTicks++;
                if (_settleTicks * _dt >= Config.settleSeconds)
                {
                    FinishRound();
                }
            }
        }

        void FinishRound()
        {
            int round = RoundIndex + 1;
            RoundFinished = true;
            _completedRows.Add(_dataRound.ladderRow);

            int bins = _landed.Length;
            double mean = 0.0;
            for (int k = 0; k < bins; k++)
            {
                mean += k * (double)_landed[k];
            }

            mean /= System.Math.Max(1, _marbleCount);
            double variance = 0.0;
            int peakBin = 0;
            for (int k = 0; k < bins; k++)
            {
                variance += (k - mean) * (k - mean) * _landed[k];
                if (_landed[k] > _landed[peakBin])
                {
                    peakBin = k;
                }
            }

            variance /= System.Math.Max(1, _marbleCount);
            _recorder.SetMetric($"round_{round}_mean_bin", (float)mean);
            _recorder.SetMetric($"round_{round}_sd_bins", (float)System.Math.Sqrt(variance));
            _recorder.SetMetric($"round_{round}_peak_bin", peakBin);
            _recorder.SetMetric($"round_{round}_peak_count", _landed[peakBin]);
            _recorder.SetMetric($"round_{round}_run_seconds", RunSeconds);
            _recorder.LogEvent($"round_{round}_done", _marbleCount);

            EnterPhase(BellPhase.Result);
        }

        void FinishRun()
        {
            Phase = BellPhase.Done;
            PhaseProgress = 1f;
            _recorder.Finish(CompletionReason.Finished);
            IsComplete = true;
        }
    }
}
