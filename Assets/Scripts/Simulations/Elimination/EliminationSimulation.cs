using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Elimination
{
    /// <summary>Where the show is in its arc. Presentation reads this to pick the camera, the board and the caption.</summary>
    public enum ElimPhase
    {
        /// <summary>The board and the question; no heat on screen.</summary>
        Intro = 0,

        /// <summary>The heat's rig is on and its field is in the pen (not ticking yet): the fly-in and the rule captions.</summary>
        HeatIntro = 1,

        /// <summary>The heat is racing. Ends a short outro after the heat is called.</summary>
        Heat = 2,

        /// <summary>The board returns and stamps the heat's outcome.</summary>
        HeatResult = 3,

        /// <summary>The midpoint beat: the out marbles are about to get a last chance.</summary>
        Twist = 4,

        /// <summary>The champion and the board.</summary>
        Payoff = 5,

        Done = 6
    }

    public enum ContenderState
    {
        Alive = 0,
        Out = 1,

        /// <summary>Won the last chance and is back in. Counts as alive for every later heat.</summary>
        Returned = 2,
        Champion = 3
    }

    /// <summary>
    /// The elimination show: an orchestrator that runs a fixed table of heats through <see cref="IHeatSimulation"/>
    /// and keeps score of who is still in. It never knows which format a heat is; a rig is just a GameObject with
    /// an <see cref="IHeatSimulation"/> on it. One rig per heat, built by the scene builder, all inactive until
    /// their turn.
    /// </summary>
    /// <remarks>
    /// Everything happens in <see cref="Tick"/>, in fixed steps. The run's <see cref="SeededRandom"/> is used for
    /// exactly one thing, the seed of each heat, drawn when the heat is set up, so the same show seed gives the
    /// same field order in every heat and (physics permitting) the same champion.
    /// </remarks>
    public sealed class EliminationSimulation : MonoBehaviour, ISimulation
    {
        [SerializeField] GameObject[] _heatRigs = new GameObject[0];

        EliminationConfig _config;
        SeededRandom _random;
        RunRecorder _recorder;
        float _fixedDelta;

        IHeatSimulation[] _heatSims;
        ContenderState[] _states;
        readonly List<int> _outOrder = new List<int>();
        readonly List<int> _outThisHeat = new List<int>();
        readonly List<int> _heatEntrants = new List<int>();
        readonly Dictionary<string, int> _byName = new Dictionary<string, int>();

        ElimPhase _phase = ElimPhase.Intro;
        int _phaseTicks;
        int _phaseTotalTicks;
        int _heatIndex = -1;
        int _heatTicks;
        bool _heatCalled;
        int _calledTicks;
        int _returnedIndex = -1;
        int _championIndex = -1;
        int _lastSafeIndex = -1;
        int _heatWinnerIndex = -1;
        int _heatsPlayed;
        bool _timedOut;

        public string FormatSlug => "elim";
        public bool IsComplete { get; private set; }
        public RunResult Result => _recorder?.Result;

        public ElimPhase Phase => _phase;

        /// <summary>Seconds into the current phase (simulated).</summary>
        public float PhaseElapsedSeconds => _phaseTicks * _fixedDelta;

        /// <summary>Length of the current phase in seconds; 0 for the heat phase, whose length is the race's.</summary>
        public float PhaseSeconds => _phaseTotalTicks * _fixedDelta;

        /// <summary>0..1 through a timed phase. During a heat it is 0 until the heat is called, then runs through the outro.</summary>
        public float PhaseProgress
        {
            get
            {
                if (_phase == ElimPhase.Heat)
                {
                    float outro = OutroTicks();
                    return _heatCalled && outro > 0 ? Mathf.Clamp01(_calledTicks / outro) : 0f;
                }

                return _phaseTotalTicks > 0 ? Mathf.Clamp01(_phaseTicks / (float)_phaseTotalTicks) : 0f;
            }
        }

        /// <summary>The show's hook question, for the opening caption.</summary>
        public string Question => _config != null && !string.IsNullOrEmpty(_config.question) ? _config.question : "WHICH MARBLE SURVIVES?";

        public int HeatIndex => _heatIndex;
        public int HeatCount => _config != null ? _config.heats.Length : _heatRigs.Length;

        public EliminationHeat CurrentHeat =>
            _config != null && _heatIndex >= 0 && _heatIndex < _config.heats.Length ? _config.heats[_heatIndex] : default;

        public IHeatSimulation CurrentHeatSim =>
            _heatSims != null && _heatIndex >= 0 && _heatIndex < _heatSims.Length ? _heatSims[_heatIndex] : null;

        /// <summary>The rig root for heat <paramref name="i"/>.</summary>
        public GameObject HeatRig(int i) => i >= 0 && i < _heatRigs.Length ? _heatRigs[i] : null;

        public IReadOnlyList<HeatEntrant> Roster => _config != null ? _config.roster : Array.Empty<HeatEntrant>();

        /// <summary>True from the tick the current heat is called until the next heat is set up.</summary>
        public bool HeatCalled => _heatCalled;

        /// <summary>Roster indices of the marbles in the current heat, in roster order.</summary>
        public IReadOnlyList<int> HeatEntrants => _heatEntrants;

        /// <summary>CutLast: roster index of the last marble to take a safe spot. -1 until the heat is called.</summary>
        public int LastSafeIndex => _lastSafeIndex;

        /// <summary>Roster index of the heat's first finisher. -1 until the heat is called.</summary>
        public int HeatWinnerIndex => _heatWinnerIndex;

        public int AliveCount
        {
            get
            {
                int n = 0;
                if (_states != null)
                {
                    foreach (ContenderState s in _states)
                    {
                        if (s != ContenderState.Out)
                        {
                            n++;
                        }
                    }
                }

                return n;
            }
        }

        public ContenderState GetState(int rosterIndex)
        {
            return _states != null && rosterIndex >= 0 && rosterIndex < _states.Length ? _states[rosterIndex] : ContenderState.Alive;
        }

        /// <summary>Roster index of the i-th marble knocked out by the last heat (best finisher first).</summary>
        public int OutThisHeat(int i) => i >= 0 && i < _outThisHeat.Count ? _outThisHeat[i] : -1;

        public int OutThisHeatCount => _outThisHeat.Count;

        /// <summary>Roster index brought back by the last chance; -1 unless a LastChance heat has just finished.</summary>
        public int ReturnedIndex => _returnedIndex;

        public int ChampionIndex => _championIndex;

        /// <summary>Wire one rig per heat, in heat order. Each root must carry an <see cref="IHeatSimulation"/>.</summary>
        public void Bind(GameObject[] heatRigs)
        {
            _heatRigs = heatRigs ?? new GameObject[0];
        }

        // ------------------------------------------------------------------ lifecycle

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (EliminationConfig)config;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random);
            _fixedDelta = config.fixedTimestep;

            if (_heatRigs.Length != _config.heats.Length)
            {
                throw new InvalidOperationException(
                    $"The config has {_config.heats.Length} heats but {_heatRigs.Length} rigs are bound. Rebuild the scene.");
            }

            _heatSims = new IHeatSimulation[_heatRigs.Length];
            for (int i = 0; i < _heatRigs.Length; i++)
            {
                if (_heatRigs[i] == null)
                {
                    throw new InvalidOperationException($"Heat rig {i + 1} is missing. Rebuild the scene.");
                }

                _heatSims[i] = _heatRigs[i].GetComponent<IHeatSimulation>();
                if (_heatSims[i] == null)
                {
                    throw new InvalidOperationException($"Heat rig '{_heatRigs[i].name}' has no IHeatSimulation on its root.");
                }

                _heatRigs[i].SetActive(false);
            }

            _states = new ContenderState[_config.roster.Length];
            _byName.Clear();
            for (int i = 0; i < _config.roster.Length; i++)
            {
                _byName[_config.roster[i].name] = i;
            }

            _outOrder.Clear();
            _outThisHeat.Clear();
            _heatEntrants.Clear();
            _heatIndex = -1;
            _heatsPlayed = 0;
            _heatCalled = false;
            _returnedIndex = -1;
            _championIndex = -1;
            _lastSafeIndex = -1;
            _heatWinnerIndex = -1;
            _timedOut = false;
            IsComplete = false;

            EnterPhase(ElimPhase.Intro, _config.introSeconds);
            _recorder.SetMetric("contenders", _config.roster.Length);
            _recorder.SetMetric("heats", _config.heats.Length);
            _recorder.LogEvent("show_start", _config.roster.Length);
        }

        public void Tick(float fixedDelta)
        {
            if (IsComplete)
            {
                return;
            }

            _recorder.AdvanceTick();
            _phaseTicks++;

            switch (_phase)
            {
                case ElimPhase.Intro:
                    if (_phaseTicks >= _phaseTotalTicks)
                    {
                        BeginHeat(0);
                    }

                    break;

                case ElimPhase.HeatIntro:
                    if (_phaseTicks >= _phaseTotalTicks)
                    {
                        EnterPhase(ElimPhase.Heat, 0f);
                    }

                    break;

                case ElimPhase.Heat:
                    TickHeat(fixedDelta);
                    break;

                case ElimPhase.HeatResult:
                    if (_phaseTicks >= _phaseTotalTicks)
                    {
                        AfterResult();
                    }

                    break;

                case ElimPhase.Twist:
                    if (_phaseTicks >= _phaseTotalTicks)
                    {
                        BeginHeat(_heatIndex + 1);
                    }

                    break;

                case ElimPhase.Payoff:
                    if (_phaseTicks >= _phaseTotalTicks)
                    {
                        FinishShow();
                    }

                    break;
            }
        }

        // ------------------------------------------------------------------ phases

        void EnterPhase(ElimPhase phase, float seconds)
        {
            _phase = phase;
            _phaseTicks = 0;
            _phaseTotalTicks = seconds > 0f ? Mathf.Max(1, Mathf.CeilToInt(seconds / _fixedDelta)) : 0;
        }

        float OutroTicks()
        {
            return Mathf.Max(1, Mathf.CeilToInt(_config.heatOutroSeconds / _fixedDelta));
        }

        /// <summary>Switch to heat <paramref name="index"/>: its rig on, everything else off, its field in the pen.</summary>
        void BeginHeat(int index)
        {
            _heatIndex = index;
            EliminationHeat heat = _config.heats[index];
            IHeatSimulation sim = _heatSims[index];

            for (int i = 0; i < _heatRigs.Length; i++)
            {
                _heatRigs[i].SetActive(i == index);
            }

            _heatEntrants.Clear();
            for (int i = 0; i < _states.Length; i++)
            {
                bool inThisHeat = heat.rule == HeatRule.LastChance
                    ? _states[i] == ContenderState.Out
                    : _states[i] != ContenderState.Out;
                if (inThisHeat)
                {
                    _heatEntrants.Add(i);
                }
            }

            var entrants = new List<HeatEntrant>(_heatEntrants.Count);
            foreach (int rosterIndex in _heatEntrants)
            {
                HeatEntrant source = _config.roster[rosterIndex];
                entrants.Add(new HeatEntrant(source.name, source.color, rosterIndex));
            }

            int safeCount = heat.EffectiveSafeCount;
            sim.ConfigureHeat(entrants, safeCount);
            int heatSeed = _random.NextInt(int.MaxValue);
            sim.Initialize(heatSeed, _config.heatSimulationConfig);

            _outThisHeat.Clear();
            _returnedIndex = -1;
            _lastSafeIndex = -1;
            _heatWinnerIndex = -1;
            _heatCalled = false;
            _calledTicks = 0;
            _heatTicks = 0;

            _recorder.LogEvent($"heat_{index + 1}_start", entrants.Count);
            EnterPhase(ElimPhase.HeatIntro, _config.heatIntroSeconds);
        }

        void TickHeat(float fixedDelta)
        {
            IHeatSimulation sim = _heatSims[_heatIndex];

            if (!_heatCalled)
            {
                _heatTicks++;
                sim.Tick(fixedDelta);
                if (sim.IsComplete)
                {
                    CallHeat(sim);
                }
                else if (_heatTicks * _fixedDelta > _config.heatMaxSeconds)
                {
                    Debug.LogError($"[elim] Heat {_heatIndex + 1} '{CurrentHeat.title}' did not finish in {_config.heatMaxSeconds:0}s. Stopping the show.");
                    _timedOut = true;
                    IsComplete = true;
                    _recorder.Finish(CompletionReason.TimedOut);
                }

                return;
            }

            // The heat is called: the rig stays up for the outro so the camera sees the last safe spot.
            _calledTicks++;
            if (_calledTicks >= OutroTicks())
            {
                EndHeat();
            }
        }

        /// <summary>The heat's race is over: apply the rule and keep score.</summary>
        void CallHeat(IHeatSimulation sim)
        {
            _heatCalled = true;
            _calledTicks = 0;

            EliminationHeat heat = _config.heats[_heatIndex];
            IReadOnlyList<string> standings = sim.Result != null ? sim.Result.standings : null;
            var order = new List<int>(_heatEntrants.Count);
            if (standings != null)
            {
                foreach (string name in standings)
                {
                    if (_byName.TryGetValue(name, out int index) && _heatEntrants.Contains(index) && !order.Contains(index))
                    {
                        order.Add(index);
                    }
                }
            }

            // A heat that returned a short or mismatched list is a bug upstream; keep the show consistent anyway.
            if (order.Count != _heatEntrants.Count)
            {
                Debug.LogError($"[elim] Heat {_heatIndex + 1} returned {order.Count} usable standings for {_heatEntrants.Count} entrants.");
                foreach (int index in _heatEntrants)
                {
                    if (!order.Contains(index))
                    {
                        order.Add(index);
                    }
                }
            }

            _heatWinnerIndex = order[0];
            int safe = heat.EffectiveSafeCount;
            _lastSafeIndex = order[Mathf.Min(safe, order.Count) - 1];
            _heatsPlayed++;
            _recorder.LogEvent($"heat_{_heatIndex + 1}_winner_{_config.roster[order[0]].name}", sim.FinishedCount);

            switch (heat.rule)
            {
                case HeatRule.CutLast:
                    EliminateBeyond(order, safe);
                    break;

                case HeatRule.LastChance:
                    _returnedIndex = order[0];
                    _states[_returnedIndex] = ContenderState.Returned;
                    _outOrder.Remove(_returnedIndex);
                    _recorder.LogEvent($"returned_{_config.roster[_returnedIndex].name}", _heatIndex + 1);
                    break;

                default:
                    _championIndex = order[0];
                    _states[_championIndex] = ContenderState.Champion;
                    EliminateBeyond(order, 1);
                    _recorder.LogEvent($"champion_{_config.roster[_championIndex].name}", _championIndex);
                    break;
            }

            _recorder.SetMetric($"heat_{_heatIndex + 1}_finished", sim.FinishedCount);
        }

        /// <summary>Everyone from place <paramref name="safe"/> + 1 down is out. Logged and ordered worst first, so the best of the outs is the latest elimination.</summary>
        void EliminateBeyond(List<int> order, int safe)
        {
            for (int place = order.Count - 1; place >= safe; place--)
            {
                int index = order[place];
                _states[index] = ContenderState.Out;
                _outOrder.Add(index);
                _recorder.LogEvent($"heat_{_heatIndex + 1}_out_{_config.roster[index].name}", place + 1);
            }

            // Presented best finisher first.
            for (int place = safe; place < order.Count; place++)
            {
                _outThisHeat.Add(order[place]);
            }
        }

        void EndHeat()
        {
            _heatRigs[_heatIndex].SetActive(false);
            EnterPhase(ElimPhase.HeatResult, _config.resultSeconds);
        }

        void AfterResult()
        {
            int next = _heatIndex + 1;
            if (next >= _config.heats.Length)
            {
                EnterPhase(ElimPhase.Payoff, _config.payoffSeconds);
                return;
            }

            if (_config.heats[next].rule == HeatRule.LastChance)
            {
                EnterPhase(ElimPhase.Twist, _config.twistSeconds);
                return;
            }

            BeginHeat(next);
        }

        void FinishShow()
        {
            _phase = ElimPhase.Done;

            // Champion first, then everyone else, the most recently knocked out ahead of the earliest.
            var standings = new List<string>(_states.Length);
            if (_championIndex >= 0)
            {
                standings.Add(_config.roster[_championIndex].name);
            }

            for (int i = _outOrder.Count - 1; i >= 0; i--)
            {
                standings.Add(_config.roster[_outOrder[i]].name);
            }

            for (int i = 0; i < _states.Length; i++)
            {
                string name = _config.roster[i].name;
                if (!standings.Contains(name))
                {
                    standings.Add(name);
                }
            }

            _recorder.SetStandings(standings);
            _recorder.SetMetric("heats_played", _heatsPlayed);
            _recorder.SetMetric("champion_index", _championIndex);
            _recorder.LogEvent("show_end", _championIndex);
            _recorder.Finish(_timedOut ? CompletionReason.TimedOut : CompletionReason.Finished);
            IsComplete = true;
        }
    }
}
