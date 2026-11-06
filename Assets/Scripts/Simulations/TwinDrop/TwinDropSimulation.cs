using SimulationLobby.Core;
using SimulationLobby.Simulations.TwinDrop.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.TwinDrop
{
    public enum TwinPhase
    {
        /// <summary>Both marbles stacked at the release point, frozen, before the drop.</summary>
        Hold = 0,
        Falling = 1,
        /// <summary>Both landed; the verdict (same slot or not) is on screen.</summary>
        Result = 2,
        EndCard = 3,
        Done = 4
    }

    /// <summary>
    /// <c>twin</c> shell: for each pair of each round, Hold, Falling, Result; then an end card. Motion lives in
    /// <see cref="PegBoardKernel"/>.
    /// </summary>
    /// <remarks>
    /// Determinism: the only seeded draws are the release x of each pair, taken up front in order. Everything
    /// else is tick-counted doubles, so a take re-renders identically from its seed.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class TwinDropSimulation : MonoBehaviour, ISimulation
    {
        RunRecorder _recorder;
        SeededRandom _random;
        PegBoardKernel _kernel;
        float _dt;
        double[] _releaseX = System.Array.Empty<double>();
        int[] _roundOfPair = System.Array.Empty<int>();
        int[] _indexInRound = System.Array.Empty<int>();

        int _pair;
        int _phaseTick;
        int _phaseTicks;
        int _fallTick;
        int _landedSeen;

        public string FormatSlug => "twin";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        public TwinDropConfig Config { get; private set; }

        public TwinPhase Phase { get; private set; }

        public int RoundIndex { get; private set; }

        public int PairInRound { get; private set; }

        public int PairsInRound => Config != null ? Config.rounds[RoundIndex].pairs : 0;

        public TwinDropConfig.TwinRound CurrentRound => Config.rounds[RoundIndex];

        /// <summary>Marbles on screen: 2 while a pair is out, 0 on the end card.</summary>
        public int BallCount { get; private set; }

        public Vector2 GetBallPosition(int i) => new Vector2((float)_kernel.BallX(i), (float)_kernel.BallY(i));

        public bool BallLanded(int i) => _kernel.Landed(i);

        public int BallSlot(int i) => _kernel.Slot(i);

        public Vector2 ReleasePoint { get; private set; }

        /// <summary>Pairs that landed in the same slot, run-wide.</summary>
        public int SameCount { get; private set; }

        /// <summary>Pairs that landed in different slots, run-wide.</summary>
        public int ApartCount { get; private set; }

        /// <summary>Pairs that started with a gap (round gap above zero), and how many of those landed apart.</summary>
        public int NudgedPairs { get; private set; }

        public int NudgedApart { get; private set; }

        /// <summary>True once both marbles of the current pair have landed.</summary>
        public bool PairDone { get; private set; }

        public bool PairWasSame { get; private set; }

        /// <summary>Rises by one each time a marble lands. Drives the thud and the slot flash.</summary>
        public int LandSerial { get; private set; }

        /// <summary>Contacts made by the first marble so far this pair. Drives the one-note-per-bounce melody.</summary>
        public long LeaderHits => _kernel != null ? _kernel.LeaderHits : 0;

        public void Initialize(int seed, SimulationConfig config)
        {
            Config = (TwinDropConfig)config;
            _dt = Config.fixedTimestep;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 1024);
            _kernel = Config.CreateKernel();

            int total = Config.TotalPairs;
            _releaseX = new double[total];
            _roundOfPair = new int[total];
            _indexInRound = new int[total];
            int n = 0;
            for (int r = 0; r < Config.RoundCount; r++)
            {
                for (int p = 0; p < Config.rounds[r].pairs; p++)
                {
                    _roundOfPair[n] = r;
                    _indexInRound[n] = p;
                    _releaseX[n] = _random.NextFloat(-Config.releaseRange, Config.releaseRange);
                    n++;
                }
            }

            SameCount = 0;
            ApartCount = 0;
            NudgedPairs = 0;
            NudgedApart = 0;
            LandSerial = 0;
            IsComplete = false;
            _recorder.SetMetric("pairs_planned", total);
            BeginPair(0);
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
                case TwinPhase.Hold:
                    if (++_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(TwinPhase.Falling);
                    }

                    break;

                case TwinPhase.Falling:
                    TickFalling(fixedDelta);
                    break;

                case TwinPhase.Result:
                    if (++_phaseTick >= _phaseTicks)
                    {
                        if (_pair + 1 < Config.TotalPairs)
                        {
                            BeginPair(_pair + 1);
                        }
                        else
                        {
                            EnterPhase(TwinPhase.EndCard);
                        }
                    }

                    break;

                case TwinPhase.EndCard:
                    if (++_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(TwinPhase.Done);
                    }

                    break;
            }
        }

        int TicksFor(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds / _dt));

        void BeginPair(int pair)
        {
            _pair = pair;
            RoundIndex = _roundOfPair[pair];
            PairInRound = _indexInRound[pair];
            PairDone = false;
            _landedSeen = 0;
            _fallTick = 0;
            BallCount = 2;
            float releaseY = Config.topRowY + Config.releaseHeight;
            _kernel.Release(_releaseX[pair], Config.GapUnits(RoundIndex), releaseY);
            ReleasePoint = new Vector2((float)_releaseX[pair], releaseY);
            EnterPhase(TwinPhase.Hold);
        }

        void EnterPhase(TwinPhase phase)
        {
            Phase = phase;
            _phaseTick = 0;
            switch (phase)
            {
                case TwinPhase.Hold:
                    float hold = _pair == 0 ? Config.firstHoldSeconds
                               : PairInRound == 0 ? Config.roundHoldSeconds : Config.pairHoldSeconds;
                    _phaseTicks = TicksFor(hold);
                    _recorder.LogEvent($"pair_{_pair + 1}_hold", (float)Config.rounds[RoundIndex].gapMeters);
                    break;

                case TwinPhase.Falling:
                    _recorder.LogEvent($"pair_{_pair + 1}_drop", (float)_releaseX[_pair]);
                    break;

                case TwinPhase.Result:
                    _phaseTicks = TicksFor(Config.resultHoldSeconds);
                    break;

                case TwinPhase.EndCard:
                    BallCount = 0;
                    _phaseTicks = TicksFor(Config.endCardSeconds);
                    _recorder.LogEvent("end_card");
                    break;

                case TwinPhase.Done:
                    _recorder.SetMetric("same_pairs", SameCount);
                    _recorder.SetMetric("apart_pairs", ApartCount);
                    _recorder.SetMetric("nudged_pairs", NudgedPairs);
                    _recorder.SetMetric("nudged_apart", NudgedApart);
                    _recorder.Finish(CompletionReason.Finished);
                    IsComplete = true;
                    break;
            }
        }

        void TickFalling(float fixedDelta)
        {
            _fallTick++;
            _kernel.Step(fixedDelta);

            int landed = (_kernel.Landed(0) ? 1 : 0) + (_kernel.Landed(1) ? 1 : 0);
            if (landed != _landedSeen)
            {
                LandSerial += landed - _landedSeen;
                _landedSeen = landed;
            }

            bool timedOut = _fallTick >= TicksFor(Config.maxFallSeconds);
            if (_kernel.AllLanded || timedOut)
            {
                if (timedOut && !_kernel.AllLanded)
                {
                    Debug.LogError($"[twin] Pair {_pair + 1} had not landed after {Config.maxFallSeconds:0}s. Check the board for a wedge.");
                }

                bool same = _kernel.AllLanded && _kernel.Slot(0) == _kernel.Slot(1);
                PairWasSame = same;
                PairDone = true;
                if (same)
                {
                    SameCount++;
                }
                else
                {
                    ApartCount++;
                }

                if (Config.rounds[RoundIndex].gapMeters > 0.0)
                {
                    NudgedPairs++;
                    if (!same)
                    {
                        NudgedApart++;
                    }
                }

                _recorder.LogEvent($"pair_{_pair + 1}_{(same ? "same" : "apart")}", _kernel.Slot(0));
                EnterPhase(TwinPhase.Result);
            }
        }
    }
}
