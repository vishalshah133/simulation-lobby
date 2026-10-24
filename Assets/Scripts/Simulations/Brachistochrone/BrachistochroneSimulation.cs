using System;
using SimulationLobby.Core;
using SimulationLobby.Simulations.Brachistochrone.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.Brachistochrone
{
    /// <summary>Where the run is inside a round.</summary>
    public enum BrachPhase
    {
        /// <summary>Balls at A, tracks drawn, caption asking the question.</summary>
        Hold = 0,

        /// <summary>The race clock runs; balls are placed from <see cref="BeadRun.DistanceAt"/>.</summary>
        Run = 1,

        /// <summary>Every ball has finished and rests at B; the result line is up.</summary>
        Result = 2,

        /// <summary>Balls glide back to A, new tracks grow in, existing tracks morph to the next finish.</summary>
        Transition = 3,

        /// <summary>After the last round: the winning track is the answer.</summary>
        EndCard = 4,

        Done = 5
    }

    /// <summary>
    /// The round shell for <c>brach</c>: Hold, Run, Result per round (Transition between), then an end card.
    /// All maths lives in <see cref="CurvePath"/> and <see cref="BeadRun"/>; the balls are analytic, there is no
    /// physics engine in the loop.
    /// </summary>
    /// <remarks>
    /// Determinism: the outcome is set by geometry. The seed only picks a cosmetic palette start. Phases are counted
    /// in ticks; the race clock and slow motion are driven by simulation state alone (leader distance and finish-time
    /// gap), never by wall time.
    /// Display state is in world units with A at the origin and y = drop (down).
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class BrachistochroneSimulation : MonoBehaviour, ISimulation
    {
        // Slow motion eases toward its target at this many 1/s, so the rate never snaps.
        const float RateEase = 10f;
        const float BallGlideShare = 0.7f;

        RunRecorder _recorder;
        SeededRandom _random;
        float _dt;

        // Precomputed per round and track.
        CurvePath[][] _paths;
        BeadRun[][] _runs;
        int[][] _order;          // track indices by finish time (ties by index)

        // Current round.
        double _clock;
        double _rate = 1.0;
        bool _slowLatched;
        bool _slowFired;
        int _finishedCount;
        int[] _place = new int[0];
        double _lastFinishTime;

        int _phaseTick;
        int _phaseTicks;
        int _runTicks;
        int _maxRunTicks;
        int _tickIndex;

        // Transition endpoints.
        float _fromFinishX, _fromFinishY;
        float _toFinishX, _toFinishY;
        float[] _fromBallX = new float[0];
        float[] _fromBallY = new float[0];
        int _prevRound = -1;

        // Display morph cache.
        CurvePath[] _display = new CurvePath[0];
        int _displayTick = -1;

        public string FormatSlug => "brach";

        public bool IsComplete { get; private set; }

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        public BrachistochroneConfig Config { get; private set; }

        /// <summary>The run's seed. Cosmetic only: the outcome never depends on it.</summary>
        public int Seed { get; private set; }

        /// <summary>A cosmetic palette start (0-5) drawn from the seeded RNG. Presentation may use it or not.</summary>
        public int PaletteStart { get; private set; }

        public BrachPhase Phase { get; private set; }

        /// <summary>0..1 through the current phase.</summary>
        public float PhaseProgress { get; private set; }

        /// <summary>0-based current round (advances at the start of the Transition into it).</summary>
        public int RoundIndex { get; private set; }

        public int RoundTotal => Config != null ? Config.RoundCount : 0;

        public BrachRound CurrentRound => Config != null ? Config.rounds[RoundIndex] : default;

        public int TrackCount => Config != null ? Config.rounds[RoundIndex].tracks.Length : 0;

        /// <summary>Current round's geometry (during Transition: the next round's, i.e. the final shape).</summary>
        public CurvePath GetPath(int track)
        {
            return _paths[RoundIndex][track];
        }

        /// <summary>
        /// What to draw right now. Equals <see cref="GetPath"/> except during a Transition, where existing tracks
        /// are morphed from the previous finish to the new one (a fresh path each tick; do not hold on to it).
        /// </summary>
        public CurvePath GetDisplayPath(int track)
        {
            if (Phase != BrachPhase.Transition || IsTrackNew(track) || _prevRound < 0)
            {
                return GetPath(track);
            }

            if (_displayTick != _tickIndex)
            {
                _displayTick = _tickIndex;
                for (int i = 0; i < _display.Length; i++)
                {
                    _display[i] = null;
                }
            }

            if (_display[track] == null)
            {
                _display[track] = CurvePath.Lerp(_paths[_prevRound][track], _paths[RoundIndex][track], Morph());
            }

            return _display[track];
        }

        /// <summary>0..1: how much of the track is drawn (new tracks grow in A to B during the Transition).</summary>
        public float GetTrackReveal(int track)
        {
            if (Phase != BrachPhase.Transition || !IsTrackNew(track))
            {
                return 1f;
            }

            return Smooth(Mathf.Clamp01((PhaseProgress - 0.2f) / 0.75f));
        }

        /// <summary>True for tracks added this round (never in round 1: it is drawn in full from frame 0).</summary>
        public bool IsTrackNew(int track)
        {
            if (RoundIndex == 0)
            {
                return false;
            }

            BrachTrack[] prev = Config.rounds[RoundIndex - 1].tracks;
            return track >= prev.Length || prev[track].kind != Config.rounds[RoundIndex].tracks[track].kind;
        }

        /// <summary>B in world units. Lerped from the old finish to the new one during the Transition.</summary>
        public float FinishX { get; private set; }

        public float FinishY { get; private set; }

        /// <summary>Seconds since launch, slow motion applied.</summary>
        public float RaceClock => (float)_clock;

        /// <summary>1, or slowMoRate (eased) during a photo finish.</summary>
        public float ClockRate => (float)_rate;

        /// <summary>True once slow motion has fired in the current round.</summary>
        public bool SlowMoFired => _slowFired;

        public void GetBall(int track, out float x, out float y, out float angle, out float speed)
        {
            CurvePath path = _paths[RoundIndex][track];
            double px, py, pa;

            switch (Phase)
            {
                case BrachPhase.Run:
                {
                    BeadRun run = _runs[RoundIndex][track];
                    path.PointAt(run.DistanceAt(_clock), out px, out py, out pa);
                    x = (float)px;
                    y = (float)py;
                    angle = (float)pa;
                    speed = (float)run.SpeedAt(_clock);
                    return;
                }

                case BrachPhase.Result:
                case BrachPhase.EndCard:
                case BrachPhase.Done:
                    path.PointAt(path.Length, out px, out py, out pa);
                    x = (float)px;
                    y = (float)py;
                    angle = (float)pa;
                    speed = 0f;
                    return;

                case BrachPhase.Transition:
                {
                    path.PointAt(0.0, out px, out py, out pa);
                    angle = (float)pa;
                    speed = 0f;
                    if (track < _fromBallX.Length && !IsTrackNew(track))
                    {
                        float e = Smooth(Mathf.Clamp01(PhaseProgress / BallGlideShare));
                        x = Mathf.Lerp(_fromBallX[track], 0f, e);
                        y = Mathf.Lerp(_fromBallY[track], 0f, e);
                    }
                    else
                    {
                        x = 0f;
                        y = 0f;
                    }

                    return;
                }

                default: // Hold
                    path.PointAt(0.0, out px, out py, out pa);
                    x = 0f;
                    y = 0f;
                    angle = (float)pa;
                    speed = 0f;
                    return;
            }
        }

        /// <summary>0 until the track's ball has finished, then 1, 2, 3 ... (ties break by track index).</summary>
        public int GetPlace(int track)
        {
            return track >= 0 && track < _place.Length ? _place[track] : 0;
        }

        /// <summary>The first finisher's track index; -1 until the first finish.</summary>
        public int WinnerTrack { get; private set; } = -1;

        /// <summary>True from the tick every ball has finished until the next transition.</summary>
        public bool RoundFinished { get; private set; }

        public float GetLength(int track)
        {
            return (float)_paths[RoundIndex][track].Length;
        }

        /// <summary>Exact finish time (race-clock seconds) of a track's ball in the current round.</summary>
        public float GetFinishTime(int track)
        {
            return (float)_runs[RoundIndex][track].FinishTime;
        }

        /// <summary>
        /// The bottom line for the current phase: the hold caption (or the config's), the caption (second half of
        /// the transition and the run use caption2 when set), the result (result2 from 40% in), then the end card.
        /// </summary>
        public string BottomLine
        {
            get
            {
                if (Config == null)
                {
                    return string.Empty;
                }

                BrachRound r = CurrentRound;
                switch (Phase)
                {
                    case BrachPhase.Hold:
                        return string.IsNullOrEmpty(r.holdCaption) ? Config.holdCaption : r.holdCaption;
                    case BrachPhase.Transition:
                        return !string.IsNullOrEmpty(r.caption2) && PhaseProgress >= 0.5f ? r.caption2 : r.caption;
                    case BrachPhase.Run:
                        return !string.IsNullOrEmpty(r.caption2) ? r.caption2 : r.caption;
                    case BrachPhase.Result:
                        return !string.IsNullOrEmpty(r.result2) && PhaseProgress >= 0.4f ? r.result2 : r.result;
                    default:
                        return Config.endCard;
                }
            }
        }

        public void Initialize(int seed, SimulationConfig config)
        {
            Config = (BrachistochroneConfig)config;
            Seed = seed;
            _dt = Config.fixedTimestep;
            _random = new SeededRandom(seed);
            PaletteStart = _random.NextInt(6); // the only draw: cosmetic
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 1024);

            _recorder.SetMetric("rounds_planned", Config.RoundCount);
            _recorder.SetMetric("series_seconds_estimate", Config.SeriesSeconds);

            BuildGeometry();
            IsComplete = false;
            _tickIndex = 0;
            SetupRound(0);
        }

        public void Tick(float fixedDelta)
        {
            if (IsComplete || _recorder == null)
            {
                return;
            }

            _tickIndex++;
            _recorder.AdvanceTick();

            switch (Phase)
            {
                case BrachPhase.Hold:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(BrachPhase.Run);
                    }

                    break;

                case BrachPhase.Run:
                    TickRun(fixedDelta);
                    break;

                case BrachPhase.Result:
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
                            EnterPhase(BrachPhase.EndCard);
                        }
                    }

                    break;

                case BrachPhase.Transition:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    ApplyFinishLerp();
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(BrachPhase.Hold);
                    }

                    break;

                case BrachPhase.EndCard:
                    _phaseTick++;
                    PhaseProgress = Progress();
                    if (_phaseTick >= _phaseTicks)
                    {
                        EnterPhase(BrachPhase.Done);
                    }

                    break;
            }
        }

        // ------------------------------------------------------------------ setup

        void BuildGeometry()
        {
            int rounds = Config.RoundCount;
            _paths = new CurvePath[rounds][];
            _runs = new BeadRun[rounds][];
            _order = new int[rounds][];
            double g = Config.gravity;

            for (int r = 0; r < rounds; r++)
            {
                BrachRound round = Config.rounds[r];
                int n = round.tracks.Length;
                _paths[r] = new CurvePath[n];
                _runs[r] = new BeadRun[n];
                var times = new double[n];
                var order = new int[n];
                for (int i = 0; i < n; i++)
                {
                    BrachTrack t = round.tracks[i];
                    _paths[r][i] = CurvePath.Build(t.kind, round.finishX, round.finishY, t.shape, Config.pathSamples);
                    _runs[r][i] = new BeadRun(_paths[r][i], g);
                    times[i] = _runs[r][i].FinishTime;
                    order[i] = i;
                }

                // Insertion sort by (finish time, track index): deterministic tie-break.
                for (int a = 1; a < n; a++)
                {
                    int cur = order[a];
                    int b = a - 1;
                    while (b >= 0 && (times[order[b]] > times[cur] ||
                                      (times[order[b]] == times[cur] && order[b] > cur)))
                    {
                        order[b + 1] = order[b];
                        b--;
                    }

                    order[b + 1] = cur;
                }

                _order[r] = order;
            }

            _display = new CurvePath[0];
        }

        void SetupRound(int index)
        {
            RoundIndex = index;
            ResetRoundState();
            FinishX = Config.rounds[index].finishX;
            FinishY = Config.rounds[index].finishY;
            EnterPhase(BrachPhase.Hold);
        }

        void SetupNextRound()
        {
            int prev = RoundIndex;
            int next = prev + 1;
            BrachRound pr = Config.rounds[prev];
            BrachRound nr = Config.rounds[next];

            _prevRound = prev;
            _fromBallX = new float[pr.tracks.Length];
            _fromBallY = new float[pr.tracks.Length];
            for (int i = 0; i < pr.tracks.Length; i++)
            {
                _fromBallX[i] = pr.finishX;
                _fromBallY[i] = pr.finishY;
            }

            _fromFinishX = pr.finishX;
            _fromFinishY = pr.finishY;
            _toFinishX = nr.finishX;
            _toFinishY = nr.finishY;

            RoundIndex = next;
            ResetRoundState();
            _display = new CurvePath[nr.tracks.Length];
            _displayTick = -1;
            EnterPhase(BrachPhase.Transition);
        }

        void ResetRoundState()
        {
            int n = Config.rounds[RoundIndex].tracks.Length;
            _place = new int[n];
            _finishedCount = 0;
            _clock = 0.0;
            _rate = 1.0;
            _slowLatched = false;
            _slowFired = false;
            WinnerTrack = -1;
            RoundFinished = false;
            _runTicks = 0;
            _maxRunTicks = Mathf.Max(1, TicksFor(Config.rounds[RoundIndex].maxRunSeconds));
            int last = _order[RoundIndex][n - 1];
            _lastFinishTime = _runs[RoundIndex][last].FinishTime;
        }

        // ------------------------------------------------------------------ phases

        float Progress()
        {
            return _phaseTicks > 0 ? Mathf.Clamp01((float)_phaseTick / _phaseTicks) : 1f;
        }

        int TicksFor(float seconds)
        {
            return Mathf.Max(0, Mathf.RoundToInt(seconds / _dt));
        }

        static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }

        /// <summary>Morph amount for the finish slide and the track redraw during a Transition.</summary>
        float Morph()
        {
            return Smooth(Mathf.Clamp01((PhaseProgress - 0.1f) / 0.8f));
        }

        void ApplyFinishLerp()
        {
            float e = Morph();
            FinishX = Mathf.Lerp(_fromFinishX, _toFinishX, e);
            FinishY = Mathf.Lerp(_fromFinishY, _toFinishY, e);
        }

        void EnterPhase(BrachPhase phase)
        {
            Phase = phase;
            _phaseTick = 0;
            PhaseProgress = 0f;
            int round = RoundIndex + 1;

            switch (phase)
            {
                case BrachPhase.Hold:
                    _phaseTicks = TicksFor(RoundIndex == 0 ? Config.firstHoldSeconds : Config.preLaunchHoldSeconds);
                    FinishX = Config.rounds[RoundIndex].finishX;
                    FinishY = Config.rounds[RoundIndex].finishY;
                    _recorder.LogEvent($"round_{round}_hold");
                    break;

                case BrachPhase.Run:
                    _phaseTicks = _maxRunTicks;
                    _runTicks = 0;
                    _recorder.LogEvent($"round_{round}_launch");
                    break;

                case BrachPhase.Result:
                    _phaseTicks = TicksFor(Config.ResultSecondsOf(Config.rounds[RoundIndex]));
                    break;

                case BrachPhase.Transition:
                    _phaseTicks = TicksFor(Config.transitionSeconds);
                    ApplyFinishLerp();
                    _recorder.LogEvent($"round_{round}_transition");
                    break;

                case BrachPhase.EndCard:
                    _phaseTicks = TicksFor(Config.endCardSeconds);
                    _recorder.LogEvent("end_card");
                    break;

                case BrachPhase.Done:
                    FinishRun();
                    return;
            }

            // A zero-length phase passes straight through (Run is never zero: maxRunTicks >= 1).
            if (_phaseTicks <= 0 && phase != BrachPhase.Run)
            {
                PhaseProgress = 1f;
                switch (phase)
                {
                    case BrachPhase.Hold:
                        EnterPhase(BrachPhase.Run);
                        break;
                    case BrachPhase.Result:
                        if (RoundIndex + 1 < Config.RoundCount)
                        {
                            SetupNextRound();
                        }
                        else
                        {
                            EnterPhase(BrachPhase.EndCard);
                        }

                        break;
                    case BrachPhase.Transition:
                        FinishX = _toFinishX;
                        FinishY = _toFinishY;
                        EnterPhase(BrachPhase.Hold);
                        break;
                    case BrachPhase.EndCard:
                        EnterPhase(BrachPhase.Done);
                        break;
                }
            }
        }

        void TickRun(float fixedDelta)
        {
            _runTicks++;

            // Slow motion is a pure function of state: where the two leaders are and how close their times are.
            double target = SlowMoTarget();
            _rate += (target - _rate) * Math.Min(1.0, fixedDelta * RateEase);
            if (_rate < Config.slowMoRate)
            {
                _rate = Config.slowMoRate;
            }

            _clock += fixedDelta * _rate;

            int[] order = _order[RoundIndex];
            for (int k = _finishedCount; k < order.Length; k++)
            {
                int track = order[k];
                if (_clock < _runs[RoundIndex][track].FinishTime)
                {
                    break;
                }

                _finishedCount++;
                _place[track] = _finishedCount;
                if (_finishedCount == 1)
                {
                    WinnerTrack = track;
                    CheckWinner(track);
                }
            }

            PhaseProgress = _lastFinishTime > 0.0 ? Mathf.Clamp01((float)(_clock / _lastFinishTime)) : 1f;

            if (_finishedCount >= order.Length)
            {
                FinishRound(false);
            }
            else if (_runTicks >= _maxRunTicks)
            {
                FinishRound(true);
            }
        }

        double SlowMoTarget()
        {
            int[] order = _order[RoundIndex];
            if (order.Length < 2)
            {
                return 1.0;
            }

            int a = order[0];
            int b = order[1];
            BeadRun runA = _runs[RoundIndex][a];
            BeadRun runB = _runs[RoundIndex][b];

            // Latch released once the runner-up is home.
            if (_clock >= runB.FinishTime)
            {
                _slowLatched = false;
                return 1.0;
            }

            if (!_slowLatched)
            {
                if (runB.FinishTime - runA.FinishTime < Config.slowMoGap &&
                    DistanceToFinish(a) < Config.slowMoRadius && DistanceToFinish(b) < Config.slowMoRadius)
                {
                    _slowLatched = true;
                    _slowFired = true;
                    _recorder.LogEvent($"round_{RoundIndex + 1}_slowmo", (float)_clock);
                }
            }

            return _slowLatched ? Config.slowMoRate : 1.0;
        }

        double DistanceToFinish(int track)
        {
            CurvePath path = _paths[RoundIndex][track];
            double px, py, pa;
            path.PointAt(_runs[RoundIndex][track].DistanceAt(_clock), out px, out py, out pa);
            double dx = Config.rounds[RoundIndex].finishX - px;
            double dy = Config.rounds[RoundIndex].finishY - py;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        void CheckWinner(int track)
        {
            BrachTrack t = Config.rounds[RoundIndex].tracks[track];
            if (t.kind != CurveKind.Cycloid)
            {
                Debug.LogError($"[brach] round {RoundIndex + 1}: winner is {t.kind}, expected the cycloid");
            }
        }

        void FinishRound(bool timedOut)
        {
            int round = RoundIndex + 1;
            int[] order = _order[RoundIndex];
            RoundFinished = true;

            _recorder.LogEvent($"round_{round}_done", WinnerTrack);
            _recorder.SetMetric($"round_{round}_winner_track", WinnerTrack);
            _recorder.SetMetric($"round_{round}_run_seconds", _runTicks * _dt);
            _recorder.SetMetric($"round_{round}_slowmo", _slowFired ? 1f : 0f);
            for (int k = 0; k < order.Length; k++)
            {
                int track = order[k];
                _recorder.SetMetric($"round_{round}_finish_{k + 1}_track", track);
                _recorder.SetMetric($"round_{round}_finish_{k + 1}_seconds",
                                    (float)_runs[RoundIndex][track].FinishTime);
            }

            if (timedOut)
            {
                Debug.LogError($"[brach] round {round}: still running after {_runTicks * _dt:0.0}s " +
                               "(maxRunSeconds). Raise gravity or the cap.");
            }

            EnterPhase(BrachPhase.Result);
        }

        void FinishRun()
        {
            Phase = BrachPhase.Done;
            PhaseProgress = 1f;
            _recorder.SetMetric("rounds_played", Config.RoundCount);
            _recorder.Finish(CompletionReason.Finished);
            IsComplete = true;
        }
    }
}
