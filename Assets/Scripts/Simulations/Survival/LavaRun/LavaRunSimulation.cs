using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Survival.LavaRun
{
    /// <summary>
    /// Lava Run (surv-002). Four colours multiply through x2/x3/x5 gates down three stages of track ahead of a lava
    /// front; at each of stages 1-2 a door shuts at the basin, everything outside dies and the smallest colour is out.
    /// Stage 3 is a railless bridge onto the island, where rings sink and a sweeper turns until one colour is left.
    /// </summary>
    /// <remarks>
    /// Per-tick order (the determinism contract, see plans/videos/surv-002-lava-run.md). Marbles are PhysX bodies; PhysX
    /// steps after FixedUpdate, so trigger events are drained here on the following tick and sorted by a stable key
    /// (gate index, then slot = pool index * team count + team), never by callback order.
    ///  1. advance the clocks and the phase machine (hold, pen settle, eruption, front, door, result, drop, island)
    ///  2. step kinematic pieces that are pure functions of sim time (pen gate, door, trapdoor, rings, sweeper)
    ///  3. world step (Running / DoorClosing / Island only):
    ///       a. lake trigger drain          -> FellIn
    ///       b. per-marble pass in slot order: kill plane -> FellIn; path progress; lava -> Swallowed
    ///       c. basin trigger drain         -> record basin-entry ticks
    ///       d. gate trigger drain          -> clones (dead marbles skipped, one trigger per marble per gate)
    ///       e. wipe-out / winner check, lead tracking, forced decision
    /// All randomness comes from branches of the run's seeded <see cref="SeededRandom"/> (pens, clones, sweeper phase),
    /// so a stream never shifts because another consumer drew more.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class LavaRunSimulation : MonoBehaviour, ISimulation
    {
        [Header("Scene wiring (filled by the scene builder)")]
        [SerializeField] LavaRunRig _rig;

        struct GateHit
        {
            public int gate, slot;
        }

        static readonly Comparison<GateHit> GateHitOrder = CompareGateHits;

        LavaRunConfig _config;
        SeededRandom _random;
        SeededRandom _penRandom;
        SeededRandom _cloneRandom;
        RunRecorder _recorder;

        readonly List<LavaRunEvent> _events = new List<LavaRunEvent>();
        readonly List<Collider> _drain = new List<Collider>();
        readonly List<int> _slotScratch = new List<int>();
        readonly List<GateHit> _gateHits = new List<GateHit>();
        readonly List<GameObject> _spawnedClones = new List<GameObject>();
        readonly List<int> _eliminationOrder = new List<int>();

        // Marbles. Slot = pool index * team count + team, so the stride is the constant team count and a team's
        // pool can grow without renumbering any slot. The per-slot arrays double when a spawned slot outgrows them.
        BodyPool3D[] _pools;
        int _teamCount;
        int _hardCap;
        GameObject[] _go;
        Rigidbody[] _body;
        bool[] _live;
        int[] _hint;
        uint[] _gateMask;
        int[] _basinTick;
        readonly Dictionary<GameObject, int> _slotOf = new Dictionary<GameObject, int>();
        int[] _alive;
        int[] _prevAlive;
        bool[] _out;
        int _aliveTeams;
        int _totalAlive;

        Collider[] _basinColliders;
        readonly List<KinematicSpinner> _spinners = new List<KinematicSpinner>();
        int[] _lastDeathTick;
        int[][] _stageStartAlive;

        // Island.
        LavaMover[] _ringMovers;
        LavaRingState[] _ringState;
        float[] _ringClock;
        int _nextRing;
        float _ringTimer;
        float _islandClock;
        float _sweeperAngle;
        float _sweeperSpeed;
        bool _slowdown;

        // Clocks / phase.
        float _dt;
        float _runClock;
        float _phaseClock;
        float _eruptClock;
        float _allInClock;
        float _penGateT;
        float _doorT;
        float _trapT;
        float _holdLength;
        int _stage;
        bool _erupted;
        bool _armed;
        float _front;
        float _lavaSpeed;
        int _winner = -1;
        bool _failed;
        bool _forced;
        int _leader = -1;
        int _leadChanges;

        // Pack stats (written by the lava pass, read by presentation).
        float _packMin, _packMax, _gap;
        Vector3 _packCentroid;
        Vector3 _packLeaderPosition;

        // Metrics.
        int _deathsLava, _deathsFall, _deathsEliminated;
        int _gateTriggers, _clonesSpawned, _gateCapped;
        int _peakLive;
        int[] _peakLiveTeam;
        int[][] _stageAlive;
        int[] _eliminatedAtStage;
        float _islandSeconds;
        int _ringsSunk;

        /// <summary>Scene wiring, called by the scene builder.</summary>
        public void Bind(LavaRunRig rig)
        {
            _rig = rig;
        }

        public string FormatSlug => "surv-lava";
        public bool IsComplete { get; private set; }
        public RunResult Result => _recorder?.Result;

        // ------------------------------------------------------------------ presenter-facing state

        public LavaRunPhase Phase { get; private set; } = LavaRunPhase.Hold;
        public LavaRunConfig Config => _config;
        public LavaRunRig Rig => _rig;
        public IReadOnlyList<LavaRunEvent> Events => _events;

        /// <summary>0..2 for the three track stages, 3 for the island.</summary>
        public int CurrentStage => _stage;

        public bool IsIsland => _stage >= LavaRunConfig.StageCount;
        public bool HasErupted => _erupted;

        /// <summary>Metres along the current stage's TrackPath (config.lavaStartDistance until it erupts). 0 on the island.</summary>
        public float LavaFrontDistance => IsIsland ? 0f : _front;

        public float LavaSpeed => _lavaSpeed;

        public Vector3 LavaFrontPosition =>
            !IsIsland && _rig != null && _rig.stages[_stage].path != null
                ? _rig.stages[_stage].path.PositionAtDistance(Mathf.Max(0f, _front))
                : Vector3.zero;

        /// <summary>Path progress of the rearmost / foremost live marble on the current stage (0 when none).</summary>
        public float PackMinDistance => _packMin;
        public float PackMaxDistance => _packMax;
        public Vector3 PackCentroid => _packCentroid;
        public Vector3 PackLeaderPosition => _packLeaderPosition;

        /// <summary>Metres between the lava front and the rearmost live marble (large when nobody is behind it). Drives the rumble.</summary>
        public float LavaGapMeters => _gap;

        public int TeamCount => _config != null ? _config.teams.Length : 0;
        public int AliveCount(int team) => _alive != null && team >= 0 && team < _alive.Length ? _alive[team] : 0;
        public int TotalAlive => _totalAlive;
        public int PeakLive(int team) => _peakLiveTeam != null && team >= 0 && team < _peakLiveTeam.Length ? _peakLiveTeam[team] : 0;

        /// <summary>True once a colour has been eliminated at a door or has no marbles left.</summary>
        public bool IsEliminated(int team) => _out != null && team >= 0 && team < _out.Length && _out[team];

        /// <summary>The stage (0..3) at which the colour went out, or -1.</summary>
        public int EliminatedAtStage(int team) =>
            _eliminatedAtStage != null && team >= 0 && team < _eliminatedAtStage.Length ? _eliminatedAtStage[team] : -1;

        public int AliveTeams => _aliveTeams;

        /// <summary>The winning colour, or -1 until decided.</summary>
        public int Winner => _winner;

        public bool WasForcedDecision => _forced;

        public int Leader => _leader;
        public int LeadChanges => _leadChanges;

        public int RingCount => _ringState != null ? _ringState.Length : 0;
        public LavaRingState RingStateOf(int ring) => _ringState[ring];

        /// <summary>0..1 through the tremble (0 to 0.5) and the lowering (0.5 to 1) of a ring; 0 intact, 1 sunk.</summary>
        public float RingProgress(int ring)
        {
            switch (_ringState[ring])
            {
                case LavaRingState.Intact: return 0f;
                case LavaRingState.Trembling:
                    return 0.5f * Mathf.Clamp01(_ringClock[ring] / Mathf.Max(1e-4f, _config.ringTrembleSeconds));
                case LavaRingState.Sinking:
                    return 0.5f + 0.5f * Mathf.Clamp01((_ringClock[ring] - _config.ringTrembleSeconds) / _config.ringSinkSeconds);
                default: return 1f;
            }
        }

        /// <summary>Current sweeper speed in degrees per second (already slowed in the finale).</summary>
        public float SweeperDegreesPerSecond => _sweeperSpeed;

        public bool SlowdownActive => _slowdown;

        /// <summary>0..1 of the open (pen gate), shut (door) and open (trapdoor) animations of the current stage.</summary>
        public float PenGateProgress => _penGateT;
        public float DoorProgress => _doorT;
        public float TrapdoorProgress => _trapT;

        /// <summary>Seconds left in the stage-1 anticipation hold (0 once released).</summary>
        public float HoldSecondsRemaining => Phase == LavaRunPhase.Hold ? Mathf.Max(0f, _holdLength - _phaseClock) : 0f;

        /// <summary>Seconds into the stage 1 Hold. The first config.trackPreviewSeconds of it are the course flyover.</summary>
        public float HoldSecondsElapsed => Phase == LavaRunPhase.Hold ? Mathf.Min(_phaseClock, _holdLength) : 0f;

        public float RunSeconds => _runClock;

        /// <summary>The marble GameObject for a slot (pool index * team count + team), or null when dead. For debugging and VFX only.</summary>
        public GameObject MarbleAt(int team, int poolIndex)
        {
            int slot = poolIndex * _teamCount + team;
            return _live != null && team >= 0 && team < _teamCount && slot >= 0 && slot < _live.Length && _live[slot] ? _go[slot] : null;
        }

        // ------------------------------------------------------------------ lifecycle

        void OnDestroy()
        {
            DestroyClones();
        }

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (LavaRunConfig)config;
            _random = new SeededRandom(seed);
            _penRandom = _random.Branch(10);
            _cloneRandom = _random.Branch(20);
            SeededRandom sweeperRandom = _random.Branch(30);
            SeededRandom spinnerRandom = _random.Branch(40);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 16384);
            _dt = config.fixedTimestep;

            IsComplete = false;
            _failed = false;
            _events.Clear();
            _eliminationOrder.Clear();
            _slotOf.Clear();
            _slotScratch.Clear();
            _drain.Clear();
            _gateHits.Clear();

            Phase = LavaRunPhase.Hold;
            _stage = 0;
            _runClock = 0f;
            _phaseClock = 0f;
            _eruptClock = 0f;
            _allInClock = 0f;
            _penGateT = 0f;
            _doorT = 0f;
            _trapT = 0f;
            _erupted = false;
            _armed = false;
            _front = _config.lavaStartDistance;
            _lavaSpeed = 0f;
            _winner = -1;
            _forced = false;
            _leader = -1;
            _leadChanges = 0;
            _packMin = _packMax = 0f;
            _gap = 999f;
            _packCentroid = Vector3.zero;
            _packLeaderPosition = Vector3.zero;
            _deathsLava = _deathsFall = _deathsEliminated = 0;
            _gateTriggers = _clonesSpawned = _gateCapped = 0;
            _peakLive = 0;
            _islandSeconds = 0f;
            _ringsSunk = 0;
            _islandClock = 0f;
            _sweeperAngle = 0f;
            _sweeperSpeed = 0f;
            _slowdown = false;
            _nextRing = 0;
            _ringTimer = 0f;
            _holdLength = _config.trackPreviewSeconds + _config.preLaunchHoldSeconds;

            if (!ValidateRig())
            {
                Fail();
                return;
            }

            int teamCount = _config.teams.Length;
            _teamCount = teamCount;
            _hardCap = _config.maxLivePerTeam;
            int total = teamCount * Mathf.Max(1, PoolPrewarm());
            _go = new GameObject[total];
            _body = new Rigidbody[total];
            _live = new bool[total];
            _hint = new int[total];
            _gateMask = new uint[total];
            _basinTick = new int[total];
            _alive = new int[teamCount];
            _prevAlive = new int[teamCount];
            _out = new bool[teamCount];
            _peakLiveTeam = new int[teamCount];
            _eliminatedAtStage = new int[teamCount];
            for (int t = 0; t < teamCount; t++)
            {
                _eliminatedAtStage[t] = -1;
            }

            _stageAlive = new int[LavaRunConfig.StageCount + 1][];
            for (int s = 0; s < _stageAlive.Length; s++)
            {
                _stageAlive[s] = new int[teamCount];
                for (int t = 0; t < teamCount; t++)
                {
                    _stageAlive[s][t] = -1;
                }
            }

            _aliveTeams = teamCount;
            _totalAlive = 0;

            BuildPools(teamCount);
            ResetMovers();

            _basinColliders = new Collider[_rig.stages.Length];
            for (int s = 0; s < _rig.stages.Length; s++)
            {
                _basinColliders[s] = _rig.stages[s].basinZone.GetComponent<Collider>();
            }

            // Island: rings as movers, sweeper phase seeded (always drawn so the stream does not depend on rings).
            int ringCount = _rig.island.rings != null ? _rig.island.rings.Length : 0;
            _ringMovers = new LavaMover[ringCount];
            _ringState = new LavaRingState[ringCount];
            _ringClock = new float[ringCount];
            for (int r = 0; r < ringCount; r++)
            {
                _ringMovers[r] = new LavaMover(_rig.island.rings[r], Vector3.down * _config.ringSinkDepth);
                _ringMovers[r].Snap(0f);
            }

            float sweeperPhase = sweeperRandom.NextFloat(0f, 360f);
            if (_rig.island.sweeper != null)
            {
                // The sim integrates the angle itself (so the speed can ramp), so the spinner runs at 1 deg per "second".
                _rig.island.sweeper.degreesPerSecond = 1f;
                _rig.island.sweeper.SetPhase(sweeperPhase);
                _rig.island.sweeper.Step(0f);
            }

            // Obstacle spinners: seeded start phase, in stage then array order.
            _spinners.Clear();
            for (int s = 0; s < _rig.stages.Length; s++)
            {
                KinematicSpinner[] list = _rig.stages[s].spinners;
                for (int i = 0; list != null && i < list.Length; i++)
                {
                    if (list[i] == null)
                    {
                        continue;
                    }

                    list[i].SetPhase(spinnerRandom.NextFloat(0f, 360f));
                    _spinners.Add(list[i]);
                }
            }

            _lastDeathTick = new int[teamCount];
            _stageStartAlive = new int[LavaRunConfig.StageCount + 1][];
            for (int s = 0; s < _stageStartAlive.Length; s++)
            {
                _stageStartAlive[s] = new int[teamCount];
            }

            ClearAllZones();
            SeatStageOne();
            RecountAll();
            SnapshotStageStart(0);
            Emit(LavaRunEventKind.StageStarted, -1, 0);
            _recorder.LogEvent("stage_started", 0);
            RecountAll();
        }

        bool ValidateRig()
        {
            if (_rig == null)
            {
                Debug.LogError("[surv-lava] No LavaRunRig bound. Rebuild the scene.", this);
                return false;
            }

            int teamCount = _config.teams.Length;
            if (_rig.stages == null || _rig.stages.Length != LavaRunConfig.StageCount)
            {
                Debug.LogError("[surv-lava] Rig needs exactly 3 stages.", this);
                return false;
            }

            if (_rig.marbleTemplates == null || _rig.marbleTemplates.Length != teamCount)
            {
                Debug.LogError("[surv-lava] Rig needs one marble template per team.", this);
                return false;
            }

            for (int t = 0; t < teamCount; t++)
            {
                if (_rig.marbleTemplates[t] == null)
                {
                    Debug.LogError($"[surv-lava] marbleTemplates[{t}] is missing.", this);
                    return false;
                }
            }

            if (_rig.lavaLakeZone == null)
            {
                Debug.LogError("[surv-lava] Rig has no lavaLakeZone.", this);
                return false;
            }

            for (int s = 0; s < _rig.stages.Length; s++)
            {
                LavaStageRig stage = _rig.stages[s];
                string label = $"stage {s + 1} ({stage?.name})";
                if (stage == null || stage.path == null || stage.path.Count < 2)
                {
                    Debug.LogError($"[surv-lava] {label} needs a TrackPath with at least 2 points.", this);
                    return false;
                }

                if (stage.basinZone == null || stage.basinZone.GetComponent<Collider>() == null)
                {
                    Debug.LogError($"[surv-lava] {label} needs a basinZone with a collider.", this);
                    return false;
                }

                if (stage.pens == null || stage.pens.Length != teamCount)
                {
                    Debug.LogError($"[surv-lava] {label} needs one pen per team.", this);
                    return false;
                }

                for (int t = 0; t < teamCount; t++)
                {
                    if (stage.pens[t] == null || stage.pens[t].spawnPoints == null || stage.pens[t].spawnPoints.Length == 0)
                    {
                        Debug.LogError($"[surv-lava] {label} pen {t} has no spawn points.", this);
                        return false;
                    }
                }

                if (stage.gates != null && stage.gates.Length > 32)
                {
                    Debug.LogError($"[surv-lava] {label} has more than 32 gates.", this);
                    return false;
                }

                if (stage.gates != null)
                {
                    for (int g = 0; g < stage.gates.Length; g++)
                    {
                        if (stage.gates[g] == null || stage.gates[g].zone == null)
                        {
                            Debug.LogError($"[surv-lava] {label} gate {g} has no zone.", this);
                            return false;
                        }
                    }
                }

                if (!stage.endsOnIsland && (!stage.door.IsValid || !stage.trapdoor.IsValid))
                {
                    Debug.LogError($"[surv-lava] {label} needs a door and a trapdoor (or endsOnIsland).", this);
                    return false;
                }
            }

            if (_rig.island == null)
            {
                Debug.LogError("[surv-lava] Rig has no island.", this);
                return false;
            }

            return true;
        }

        /// <summary>Marbles each colour's pool clones up front: the hard cap if set, else the prewarm count.</summary>
        int PoolPrewarm()
        {
            if (_hardCap > 0)
            {
                return _hardCap;
            }

            return Mathf.Max(_config.poolPrewarmPerTeam, _config.startMarblesPerTeam, 1);
        }

        /// <summary>Doubles the per-slot arrays until <paramref name="slot"/> fits, keeping their contents.</summary>
        void EnsureSlot(int slot)
        {
            if (slot < _live.Length)
            {
                return;
            }

            int length = _live.Length;
            while (length <= slot)
            {
                length *= 2;
            }

            System.Array.Resize(ref _go, length);
            System.Array.Resize(ref _body, length);
            System.Array.Resize(ref _live, length);
            System.Array.Resize(ref _hint, length);
            System.Array.Resize(ref _gateMask, length);
            System.Array.Resize(ref _basinTick, length);
        }

        void BuildPools(int teamCount)
        {
            DestroyClones();
            _pools = new BodyPool3D[teamCount];
            Transform parent = _rig.poolParent != null ? _rig.poolParent : _rig.transform;
            for (int t = 0; t < teamCount; t++)
            {
                int before = parent.childCount;
                _pools[t] = new BodyPool3D(_rig.marbleTemplates[t], parent, PoolPrewarm(), _hardCap <= 0);
                for (int c = before; c < parent.childCount; c++)
                {
                    _spawnedClones.Add(parent.GetChild(c).gameObject);
                }
            }
        }

        void DestroyClones()
        {
            for (int i = 0; i < _spawnedClones.Count; i++)
            {
                if (_spawnedClones[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(_spawnedClones[i]);
                }
                else
                {
                    DestroyImmediate(_spawnedClones[i]);
                }
            }

            _spawnedClones.Clear();
        }

        void ResetMovers()
        {
            for (int s = 0; s < _rig.stages.Length; s++)
            {
                LavaStageRig stage = _rig.stages[s];
                stage.penGate.Snap(0f);
                stage.door.Snap(0f);
                stage.trapdoor.Snap(0f);
            }
        }

        void Fail()
        {
            _failed = true;
            IsComplete = true;
            _recorder?.Finish(CompletionReason.Failed);
        }

        // ------------------------------------------------------------------ the tick

        public void Tick(float fixedDelta)
        {
            _recorder.AdvanceTick();
            if (_failed || IsComplete)
            {
                return;
            }

            _runClock += _dt;

            // Spinners turn from tick zero through every phase so the course looks alive in the reveal.
            for (int i = 0; i < _spinners.Count; i++)
            {
                _spinners[i].Step(_runClock);
            }

            switch (Phase)
            {
                case LavaRunPhase.Hold:
                    _phaseClock += _dt;
                    if (_phaseClock >= _holdLength)
                    {
                        OpenPenGate();
                    }

                    ClearAllZones();
                    break;

                case LavaRunPhase.PenSettle:
                    _phaseClock += _dt;
                    if (_phaseClock >= _config.penSettleSeconds)
                    {
                        OpenPenGate();
                    }

                    ClearAllZones();
                    break;

                case LavaRunPhase.Running:
                    TickRunning();
                    break;

                case LavaRunPhase.DoorClosing:
                    TickDoorClosing();
                    break;

                case LavaRunPhase.StageResult:
                    ClearAllZones();
                    _phaseClock += _dt;
                    if (_phaseClock >= _config.stageResultHoldSeconds)
                    {
                        OpenTrapdoor();
                    }

                    break;

                case LavaRunPhase.Drop:
                    TickDrop();
                    break;

                case LavaRunPhase.Island:
                    TickIsland();
                    break;

                case LavaRunPhase.Payoff:
                    ClearAllZones();
                    _phaseClock += _dt;
                    if (_phaseClock >= _config.payoffHoldSeconds)
                    {
                        Phase = LavaRunPhase.Complete;
                        PublishMetrics();
                        IsComplete = true;
                        _recorder.Finish(CompletionReason.Finished);
                    }

                    break;
            }
        }

        // ------------------------------------------------------------------ stage machine

        void OpenPenGate()
        {
            Phase = LavaRunPhase.Running;
            _phaseClock = 0f;
            _eruptClock = 0f;
            _allInClock = 0f;
            _armed = false;
            _packMin = _packMax = 0f;
            Emit(LavaRunEventKind.Released, -1, _stage);
            _recorder.LogEvent("released", _stage);
        }

        void TickRunning()
        {
            LavaStageRig stage = _rig.stages[_stage];

            _phaseClock += _dt;
            if (_penGateT < 1f)
            {
                _penGateT = Mathf.Min(1f, _penGateT + _dt / _config.penGateOpenSeconds);
                stage.penGate.Apply(_penGateT, Vector3.zero);
            }

            // Gate the eruption on the outcome (the pack has left the pens), not just a timer. The timeout is a
            // safety net that surfaces a broken scene in the console instead of a fake winner.
            if (!_armed)
            {
                bool left = _packMax >= _config.eruptionArmDistance;
                bool timedOut = _phaseClock >= _config.eruptionArmTimeoutSeconds;
                if (left || timedOut)
                {
                    _armed = true;
                    if (!left)
                    {
                        Debug.LogWarning($"[surv-lava] Stage {_stage + 1}: no marble left the pens within " +
                                         $"{_config.eruptionArmTimeoutSeconds:0.#} s - check pen slope/gate", this);
                    }
                }
            }

            if (_armed)
            {
                _eruptClock += _dt;
            }

            if (_armed && !_erupted && _eruptClock >= _config.EruptionDelay(_stage))
            {
                _erupted = true;
                _eruptClock = 0f; // now "seconds since eruption", which drives the in-stage speed ramp
                Emit(LavaRunEventKind.Erupted, -1, _stage);
                _recorder.LogEvent("erupted", _stage);
            }

            bool frontArrived = false;
            if (_erupted)
            {
                // Ramped base speed, or catch-up toward (rearmost marble - allowed gap), whichever is larger.
                // Only sim state feeds it (the pack minimum from the last pass), so it stays deterministic.
                float baseSpeed = _config.LavaSpeed(_stage, _eruptClock);
                float speed = baseSpeed;
                if (_totalAlive > 0)
                {
                    float target = _packMin - _config.LavaMaxGap(_stage, _eruptClock);
                    speed = Mathf.Max(speed, (target - _front) * _config.lavaCatchUpGain);
                }

                _lavaSpeed = Mathf.Min(speed, Mathf.Max(_config.lavaMaxSpeed, baseSpeed));
                _front += _lavaSpeed * _dt;
                if (_front >= stage.basinEntryDistance)
                {
                    _front = stage.basinEntryDistance;
                    frontArrived = true;
                }
            }

            if (WorldStep())
            {
                return;
            }

            bool closeNow = frontArrived;
            if (!closeNow && _config.earlyCloseDelaySeconds >= 0f && _totalAlive > 0 && AllLiveInBasin(stage))
            {
                _allInClock += _dt;
                closeNow = _allInClock >= _config.earlyCloseDelaySeconds;
            }
            else if (!frontArrived)
            {
                _allInClock = 0f;
            }

            if (!closeNow)
            {
                return;
            }

            _lavaSpeed = 0f;
            if (stage.endsOnIsland)
            {
                ResolveStageEnd();
                return;
            }

            Phase = LavaRunPhase.DoorClosing;
            _phaseClock = 0f;
            Emit(LavaRunEventKind.DoorClosing, -1, _stage);
        }

        void TickDoorClosing()
        {
            LavaStageRig stage = _rig.stages[_stage];
            _phaseClock += _dt;
            _doorT = Mathf.Min(1f, _phaseClock / _config.doorCloseSeconds);
            stage.door.Apply(_doorT, Vector3.zero);

            if (WorldStep())
            {
                return;
            }

            if (_doorT >= 1f)
            {
                Emit(LavaRunEventKind.DoorClosed, -1, _stage);
                _recorder.LogEvent("door_closed", _stage);
                ResolveStageEnd();
            }
        }

        /// <summary>Door shut (or bridge end reached): the basin decides who lives, then the smallest colour is out.</summary>
        void ResolveStageEnd()
        {
            LavaStageRig stage = _rig.stages[_stage];
            int teamCount = _config.teams.Length;
            Array.Copy(_alive, _prevAlive, teamCount);

            // Everything outside the safe zone is gone (the lava has the rest). Slot order = deterministic.
            for (int slot = 0; slot < _live.Length; slot++)
            {
                if (!_live[slot])
                {
                    continue;
                }

                Vector3 position = _body[slot].position;
                if (InBasin(_stage, position))
                {
                    if (_basinTick[slot] < 0)
                    {
                        _basinTick[slot] = _recorder.CurrentTick;
                    }
                }
                else
                {
                    Kill(slot, LavaRunDeathCause.Lava, position);
                }
            }

            MarkWipeouts();

            // A colour the lava already wiped out this stage is the stage's casualty: the door takes no one else.
            if (!stage.endsOnIsland && _config.Eliminates(_stage) && _aliveTeams >= 2 && !AnyWipedOutThisStage())
            {
                int loser = PickLoser();
                int count = _alive[loser];
                for (int slot = 0; slot < _live.Length; slot++)
                {
                    if (_live[slot] && slot % _teamCount == loser)
                    {
                        Kill(slot, LavaRunDeathCause.Eliminated, _body[slot].position);
                    }
                }

                _out[loser] = true;
                _eliminatedAtStage[loser] = _stage;
                _eliminationOrder.Add(loser);
                _aliveTeams--;
                Emit(LavaRunEventKind.TeamEliminated, loser, _stage, 0, count);
                _recorder.LogEvent("team_eliminated", loser);
            }

            for (int t = 0; t < teamCount; t++)
            {
                _stageAlive[_stage][t] = _alive[t];
            }

            UpdateLeader();
            _recorder.LogEvent("stage_end_total_alive", _totalAlive);

            if (_aliveTeams <= 1)
            {
                BeginPayoff(ResolveSoleSurvivor());
                return;
            }

            if (stage.endsOnIsland)
            {
                StartIsland();
                return;
            }

            Phase = LavaRunPhase.StageResult;
            _phaseClock = 0f;
        }

        /// <summary>Smallest live count loses; a tie goes to the colour whose last marble reached the basin latest.</summary>
        int PickLoser()
        {
            int teamCount = _config.teams.Length;
            int best = -1;
            int bestCount = int.MaxValue;
            int bestLatest = -1;
            for (int t = 0; t < teamCount; t++)
            {
                if (_out[t])
                {
                    continue;
                }

                int latest = -1;
                int poolSize = _pools[t].Capacity;
                for (int i = 0; i < poolSize; i++)
                {
                    int slot = i * _teamCount + t;
                    if (slot < _live.Length && _live[slot] && _basinTick[slot] > latest)
                    {
                        latest = _basinTick[slot];
                    }
                }

                bool worse = best < 0 || _alive[t] < bestCount || (_alive[t] == bestCount && latest > bestLatest);
                if (worse)
                {
                    best = t;
                    bestCount = _alive[t];
                    bestLatest = latest;
                }
            }

            return best;
        }

        void OpenTrapdoor()
        {
            Phase = LavaRunPhase.Drop;
            _phaseClock = 0f;
            _trapT = 0f;
            Emit(LavaRunEventKind.TrapdoorOpened, -1, _stage);
        }

        void TickDrop()
        {
            LavaStageRig stage = _rig.stages[_stage];
            ClearAllZones();
            _phaseClock += _dt;
            _trapT = Mathf.Min(1f, _phaseClock / _config.trapdoorOpenSeconds);
            stage.trapdoor.Apply(_trapT, Vector3.zero);

            if (_phaseClock >= _config.dropSeconds)
            {
                BeginStage(_stage + 1);
            }
        }

        /// <summary>Re-seat the survivors into the next stage's pens and start its settle timer.</summary>
        void BeginStage(int stageIndex)
        {
            _stage = stageIndex;
            LavaStageRig stage = _rig.stages[_stage];
            stage.penGate.Snap(0f);

            for (int t = 0; t < _config.teams.Length; t++)
            {
                int seated = 0;
                int poolSize = _pools[t].Capacity;
                for (int i = 0; i < poolSize; i++)
                {
                    int slot = i * _teamCount + t;
                    if (slot >= _live.Length || !_live[slot])
                    {
                        continue;
                    }

                    Vector3 position = PenPosition(stage, t, seated++);
                    Rigidbody body = _body[slot];
                    _go[slot].transform.position = position;
                    body.position = position;
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    _hint[slot] = 0;
                    _gateMask[slot] = 0;
                    _basinTick[slot] = -1;
                }
            }

            _front = _config.lavaStartDistance;
            _lavaSpeed = 0f;
            _erupted = false;
            _penGateT = 0f;
            _doorT = 0f;
            _trapT = 0f;
            _eruptClock = 0f;
            _allInClock = 0f;
            _phaseClock = 0f;
            Phase = LavaRunPhase.PenSettle;
            SnapshotStageStart(_stage);
            Emit(LavaRunEventKind.StageStarted, -1, _stage);
            _recorder.LogEvent("stage_started", _stage);
        }

        void SnapshotStageStart(int stage)
        {
            Array.Copy(_alive, _stageStartAlive[stage], _alive.Length);
        }

        void StartIsland()
        {
            _stage = LavaRunConfig.StageCount;
            Phase = LavaRunPhase.Island;
            _phaseClock = 0f;
            _islandClock = 0f;
            _ringTimer = 0f;
            _nextRing = 0;
            _lavaSpeed = 0f;
            SnapshotStageStart(_stage);
            Emit(LavaRunEventKind.IslandStarted, -1, _stage);
            _recorder.LogEvent("island_started", _totalAlive);
        }

        void TickIsland()
        {
            ClearAllZones();
            _islandSeconds += _dt;
            _islandClock += _dt;

            if (!_slowdown && _totalAlive < _config.slowdownBelow)
            {
                _slowdown = true;
                Emit(LavaRunEventKind.SlowdownStarted, -1, _stage, 0, _totalAlive);
                _recorder.LogEvent("slowdown_started", _totalAlive);
            }

            float rate = _slowdown ? _config.finaleSlowdown : 1f;
            float scaledDt = _dt * rate;

            // Sweeper: integrate the angle ourselves so the speed can ramp and slow without a jump.
            float ramp = Mathf.Clamp01(_islandClock / _config.sweeperRampSeconds);
            _sweeperSpeed = Mathf.Lerp(_config.sweeperDegreesStart, _config.sweeperDegreesEnd, ramp) * rate;
            _sweeperAngle += _sweeperSpeed * _dt;
            if (_rig.island.sweeper != null)
            {
                _rig.island.sweeper.Step(_sweeperAngle);
            }

            // Rings: outermost first; the innermost never sinks.
            int sinkable = _ringState.Length - 1;
            _ringTimer += scaledDt;
            if (_nextRing < sinkable && _ringTimer >= RingInterval(_nextRing))
            {
                _ringTimer = 0f;
                _ringState[_nextRing] = LavaRingState.Trembling;
                _ringClock[_nextRing] = 0f;
                Emit(LavaRunEventKind.RingSinking, -1, _stage, _nextRing);
                _recorder.LogEvent("ring_sinking", _nextRing);
                _nextRing++;
            }

            for (int r = 0; r < sinkable; r++)
            {
                if (_ringState[r] == LavaRingState.Intact || _ringState[r] == LavaRingState.Sunk)
                {
                    continue;
                }

                _ringClock[r] += scaledDt;
                float clock = _ringClock[r];
                if (clock < _config.ringTrembleSeconds)
                {
                    float amp = _config.ringTrembleAmplitude * (0.4f + 0.6f * clock / Mathf.Max(1e-4f, _config.ringTrembleSeconds));
                    Vector3 shake = new Vector3(Mathf.Sin(clock * 47f), Mathf.Sin(clock * 61f) * 0.5f, Mathf.Cos(clock * 53f)) * amp;
                    _ringMovers[r].Apply(0f, shake);
                }
                else
                {
                    _ringState[r] = LavaRingState.Sinking;
                    float t = Mathf.Clamp01((clock - _config.ringTrembleSeconds) / _config.ringSinkSeconds);
                    _ringMovers[r].Apply(t * t * (3f - 2f * t), Vector3.zero);
                    if (t >= 1f)
                    {
                        _ringState[r] = LavaRingState.Sunk;
                        _ringsSunk++;
                        Emit(LavaRunEventKind.RingSunk, -1, _stage, r);
                        _recorder.LogEvent("ring_sunk", r);
                    }
                }
            }

            WorldStep();
        }

        float RingInterval(int ring)
        {
            int sinkable = _ringState.Length - 1;
            float frac = sinkable > 1 ? ring / (float)(sinkable - 1) : 0f;
            return Mathf.Lerp(_config.ringSinkIntervalStart, _config.ringSinkIntervalEnd, frac);
        }

        // ------------------------------------------------------------------ world step (deaths, gates, winner)

        /// <summary>Returns true when the run moved to the payoff (callers stop for the tick).</summary>
        bool WorldStep()
        {
            int teamCount = _config.teams.Length;
            Array.Copy(_alive, _prevAlive, teamCount);
            LavaStageRig stage = IsIsland ? null : _rig.stages[_stage];

            ClearInactiveZones();

            // a. lake
            if (_rig.lavaLakeZone != null)
            {
                _drain.Clear();
                _rig.lavaLakeZone.Drain(_drain);
                CollectSlots(_drain, _slotScratch);
                for (int i = 0; i < _slotScratch.Count; i++)
                {
                    int slot = _slotScratch[i];
                    if (_live[slot])
                    {
                        Kill(slot, LavaRunDeathCause.Fall, _body[slot].position);
                    }
                }
            }

            // b. per-marble pass
            bool lavaActive = stage != null && _erupted && Phase != LavaRunPhase.Island;
            bool trackStats = stage != null;
            float killY = stage != null ? Mathf.Max(_rig.killPlaneY, stage.killPlaneY) : _rig.killPlaneY;
            float minS = float.MaxValue;
            float maxS = float.MinValue;
            Vector3 sum = Vector3.zero;
            int counted = 0;
            Vector3 leaderPos = Vector3.zero;
            for (int slot = 0; slot < _live.Length; slot++)
            {
                if (!_live[slot])
                {
                    continue;
                }

                Vector3 position = _body[slot].position;
                if (position.y < killY)
                {
                    Kill(slot, LavaRunDeathCause.Fall, position);
                    continue;
                }

                if (!trackStats)
                {
                    continue;
                }

                TrackPath path = stage.path;
                int index = path.Nearest(position, _hint[slot], _config.progressWindowBack,
                    _config.progressWindowForward, out float _);
                _hint[slot] = index;
                float s = path.ProgressAt(position, index);

                if (lavaActive && s < _front + _config.lavaKillMargin && !InBasin(_stage, position))
                {
                    Kill(slot, LavaRunDeathCause.Lava, position);
                    continue;
                }

                if (s < minS)
                {
                    minS = s;
                }

                if (s > maxS)
                {
                    maxS = s;
                    leaderPos = position;
                }

                sum += position;
                counted++;
            }

            if (trackStats)
            {
                if (counted > 0)
                {
                    _packMin = minS;
                    _packMax = maxS;
                    _packCentroid = sum / counted;
                    _packLeaderPosition = leaderPos;
                    _gap = minS - _front;
                }
                else
                {
                    _packMin = _packMax = 0f;
                    _gap = 999f;
                }
            }

            if (stage != null)
            {
                // c. basin entries (first entry only)
                _drain.Clear();
                stage.basinZone.Drain(_drain);
                CollectSlots(_drain, _slotScratch);
                for (int i = 0; i < _slotScratch.Count; i++)
                {
                    int slot = _slotScratch[i];
                    if (_live[slot] && _basinTick[slot] < 0)
                    {
                        _basinTick[slot] = _recorder.CurrentTick;
                    }
                }

                // d. gates (only while the pens are open; ordered by gate then slot)
                ProcessGates(stage);
            }

            // e. outcome
            MarkWipeouts();
            UpdateLeader();

            if (_aliveTeams <= 1)
            {
                BeginPayoff(ResolveSoleSurvivor());
                return true;
            }

            if (_runClock >= _config.maxRunSeconds)
            {
                _forced = true;
                Emit(LavaRunEventKind.ForcedDecision, -1, _stage);
                _recorder.LogEvent("forced_decision", _runClock);
                BeginPayoff(MostMarbles());
                return true;
            }

            return false;
        }

        void ProcessGates(LavaStageRig stage)
        {
            _gateHits.Clear();
            LavaGate[] gates = stage.gates;
            if (gates == null)
            {
                return;
            }

            for (int g = 0; g < gates.Length; g++)
            {
                _drain.Clear();
                gates[g].zone.Drain(_drain);
                CollectSlots(_drain, _slotScratch);
                for (int i = 0; i < _slotScratch.Count; i++)
                {
                    _gateHits.Add(new GateHit { gate = g, slot = _slotScratch[i] });
                }
            }

            if (_gateHits.Count == 0)
            {
                return;
            }

            _gateHits.Sort(GateHitOrder);

            for (int i = 0; i < _gateHits.Count; i++)
            {
                GateHit hit = _gateHits[i];
                if (!_live[hit.slot])
                {
                    continue;
                }

                uint bit = 1u << hit.gate;
                if ((_gateMask[hit.slot] & bit) != 0)
                {
                    continue;
                }

                _gateMask[hit.slot] |= bit;
                TriggerGate(stage, hit.gate, hit.slot);
            }
        }

        void TriggerGate(LavaStageRig stage, int gateIndex, int parentSlot)
        {
            int team = parentSlot % _teamCount;
            int multiplier = Mathf.Max(1, stage.gates[gateIndex].multiplier);
            int want = multiplier - 1;
            Rigidbody parent = _body[parentSlot];
            Vector3 parentPosition = parent.position;
            Vector3 parentVelocity = parent.linearVelocity;
            int parentHint = _hint[parentSlot];

            _gateTriggers++;

            // Draws first, for every wanted clone, so a capped pool never shifts the stream.
            Vector3 direction = stage.path.TangentAt(parentHint);
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, flat).normalized;

            int room = _hardCap > 0 ? Mathf.Max(0, _hardCap - _alive[team]) : int.MaxValue;
            int spawn = Mathf.Min(want, room);
            int spawned = 0;
            for (int k = 0; k < want; k++)
            {
                float lateral = _cloneRandom.NextFloat(-1f, 1f);
                if (k >= spawn)
                {
                    continue;
                }

                Vector3 position = parentPosition - direction * (_config.cloneBackOffset + k * _config.cloneSpacing)
                                   + right * (lateral * _config.cloneLateralJitter);
                Vector3 velocity = parentVelocity + right * (lateral * _config.cloneLateralSpeed);
                if (SpawnMarble(team, position, velocity, parentHint, 1u << gateIndex))
                {
                    spawned++;
                }
            }

            _clonesSpawned += spawned;
            Emit(LavaRunEventKind.GateTriggered, team, _stage, gateIndex, spawned, parentPosition);

            if (spawned < want)
            {
                _gateCapped++;
                Emit(LavaRunEventKind.GateCapped, team, _stage, gateIndex, want - spawned, parentPosition);
                _recorder.LogEvent("gate_capped", team);
            }
        }

        static int CompareGateHits(GateHit a, GateHit b)
        {
            if (a.gate != b.gate)
            {
                return a.gate.CompareTo(b.gate);
            }

            return a.slot.CompareTo(b.slot);
        }

        /// <summary>Maps drained colliders to sorted, de-duplicated live slots.</summary>
        void CollectSlots(List<Collider> colliders, List<int> into)
        {
            into.Clear();
            for (int i = 0; i < colliders.Count; i++)
            {
                Collider c = colliders[i];
                GameObject body = c.attachedRigidbody != null ? c.attachedRigidbody.gameObject : c.gameObject;
                if (_slotOf.TryGetValue(body, out int slot))
                {
                    into.Add(slot);
                }
            }

            into.Sort();
            for (int i = into.Count - 1; i > 0; i--)
            {
                if (into[i] == into[i - 1])
                {
                    into.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------ marbles

        void SeatStageOne()
        {
            LavaStageRig stage = _rig.stages[0];
            for (int t = 0; t < _config.teams.Length; t++)
            {
                for (int i = 0; i < _config.startMarblesPerTeam; i++)
                {
                    SpawnMarble(t, PenPosition(stage, t, i), Vector3.zero, 0, 0u);
                }
            }
        }

        /// <summary>Seeded pen position for the <paramref name="n"/>-th marble of a colour. Always draws two floats.</summary>
        Vector3 PenPosition(LavaStageRig stage, int team, int n)
        {
            Transform[] points = stage.pens[team].spawnPoints;
            Transform point = points[n % points.Length];
            int layer = n / points.Length;
            float jx = _penRandom.NextFloat(-1f, 1f) * _config.penJitter;
            float jz = _penRandom.NextFloat(-1f, 1f) * _config.penJitter;
            return point.position + new Vector3(jx, layer * _config.penStackHeight, jz);
        }

        bool SpawnMarble(int team, Vector3 position, Vector3 velocity, int hint, uint gateMask)
        {
            int capacityBefore = _pools[team].Capacity;
            GameObject marble = _pools[team].TryGet(position);
            if (marble == null)
            {
                return false;
            }

            // An uncapped pool grew: track the new clone so DestroyClones removes it on the next Initialize.
            if (_pools[team].Capacity > capacityBefore)
            {
                _spawnedClones.Add(marble);
            }

            int index = _pools[team].IndexOf(marble);
            if (index < 0)
            {
                _pools[team].Release(marble);
                return false;
            }

            int slot = index * _teamCount + team;
            EnsureSlot(slot);
            _go[slot] = marble;
            if (!marble.TryGetComponent(out _body[slot]))
            {
                _pools[team].Release(marble);
                Debug.LogError("[surv-lava] Marble template has no Rigidbody.", this);
                return false;
            }

            _body[slot].linearVelocity = velocity;
            _body[slot].angularVelocity = Vector3.zero;
            _live[slot] = true;
            _hint[slot] = hint;
            _gateMask[slot] = gateMask;
            _basinTick[slot] = -1;
            _slotOf[marble] = slot;
            _alive[team]++;
            _totalAlive++;

            if (_alive[team] > _peakLiveTeam[team])
            {
                _peakLiveTeam[team] = _alive[team];
            }

            if (_totalAlive > _peakLive)
            {
                _peakLive = _totalAlive;
            }

            return true;
        }

        void Kill(int slot, LavaRunDeathCause cause, Vector3 position)
        {
            int team = slot % _teamCount;
            GameObject marble = _go[slot];
            _live[slot] = false;
            _slotOf.Remove(marble);
            _pools[team].Release(marble);
            _go[slot] = null;
            _alive[team]--;
            _totalAlive--;
            _lastDeathTick[team] = _recorder.CurrentTick;

            switch (cause)
            {
                case LavaRunDeathCause.Lava:
                    _deathsLava++;
                    Emit(LavaRunEventKind.Swallowed, team, _stage, 0, 0, position);
                    break;
                case LavaRunDeathCause.Fall:
                    _deathsFall++;
                    Emit(LavaRunEventKind.FellIn, team, _stage, 0, 0, position);
                    break;
                default:
                    _deathsEliminated++;
                    Emit(LavaRunEventKind.Sunk, team, _stage, 0, 0, position);
                    break;
            }
        }

        void RecountAll()
        {
            UpdateLeader();
        }

        bool InBasin(int stageIndex, Vector3 position)
        {
            Collider zone = _basinColliders[stageIndex];
            Vector3 closest = zone.ClosestPoint(position);
            return (closest - position).sqrMagnitude < 1e-8f;
        }

        bool AllLiveInBasin(LavaStageRig stage)
        {
            for (int slot = 0; slot < _live.Length; slot++)
            {
                if (_live[slot] && !InBasin(_stage, _body[slot].position))
                {
                    return false;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------ outcome

        /// <summary>A colour at zero live marbles is out immediately.</summary>
        void MarkWipeouts()
        {
            for (int t = 0; t < _alive.Length; t++)
            {
                if (_out[t] || _alive[t] > 0)
                {
                    continue;
                }

                _out[t] = true;
                _eliminatedAtStage[t] = _stage;
                _eliminationOrder.Add(t);
                _aliveTeams--;
                Emit(LavaRunEventKind.TeamOut, t, _stage);
                _recorder.LogEvent("team_out", t);
            }
        }

        /// <summary>True if a colour hit zero marbles during the current stage (door eliminations are recorded after this is read).</summary>
        bool AnyWipedOutThisStage()
        {
            for (int t = 0; t < _eliminatedAtStage.Length; t++)
            {
                if (_eliminatedAtStage[t] == _stage)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The one colour left, or (everyone died on the same tick) the one that had the most a tick ago.</summary>
        int ResolveSoleSurvivor()
        {
            for (int t = 0; t < _out.Length; t++)
            {
                if (!_out[t])
                {
                    return t;
                }
            }

            // Everyone died: the colour whose last marble died latest wins (that is what the viewer saw).
            // Ties: more marbles at the start of the stage, then the lower index.
            int best = 0;
            for (int t = 1; t < _lastDeathTick.Length; t++)
            {
                if (_lastDeathTick[t] > _lastDeathTick[best] ||
                    (_lastDeathTick[t] == _lastDeathTick[best] &&
                     _stageStartAlive[Mathf.Min(_stage, _stageStartAlive.Length - 1)][t] >
                     _stageStartAlive[Mathf.Min(_stage, _stageStartAlive.Length - 1)][best]))
                {
                    best = t;
                }
            }

            // The fallback winner was marked out above; clear it so the standings list is consistent.
            _eliminationOrder.Remove(best);
            return best;
        }

        int MostMarbles()
        {
            int best = -1;
            for (int t = 0; t < _alive.Length; t++)
            {
                if (_out[t])
                {
                    continue;
                }

                if (best < 0 || _alive[t] > _alive[best])
                {
                    best = t;
                }
            }

            return best >= 0 ? best : ResolveSoleSurvivor();
        }

        void UpdateLeader()
        {
            int best = -1;
            for (int t = 0; t < _alive.Length; t++)
            {
                if (_out[t])
                {
                    continue;
                }

                if (best < 0 || _alive[t] > _alive[best])
                {
                    best = t;
                }
            }

            if (best < 0 || _totalAlive == 0)
            {
                return;
            }

            if (_leader < 0 || _out[_leader])
            {
                _leader = best;
                return;
            }

            // A change needs a strictly larger count; a tie keeps the current leader.
            if (_alive[best] > _alive[_leader])
            {
                _leader = best;
                _leadChanges++;
                _recorder.LogEvent("lead_change", best);
            }
        }

        void BeginPayoff(int winner)
        {
            _winner = winner;
            _stage = Mathf.Min(_stage, LavaRunConfig.StageCount);
            Phase = LavaRunPhase.Payoff;
            _phaseClock = 0f;
            _lavaSpeed = 0f;
            _stageAlive[Mathf.Min(_stage, _stageAlive.Length - 1)][winner] = _alive[winner];
            ClearAllZones();
            Emit(LavaRunEventKind.Winner, winner, _stage);
            _recorder.LogEvent("winner", winner);
        }

        // ------------------------------------------------------------------ zones / events / metrics

        void ClearAllZones()
        {
            if (_rig == null)
            {
                return;
            }

            for (int s = 0; s < _rig.stages.Length; s++)
            {
                ClearStageZones(_rig.stages[s]);
            }

            if (_rig.lavaLakeZone != null)
            {
                _rig.lavaLakeZone.Clear();
            }
        }

        /// <summary>Drop buffered entries of every stage except the current one, so nothing stale is drained later.</summary>
        void ClearInactiveZones()
        {
            for (int s = 0; s < _rig.stages.Length; s++)
            {
                if (s != _stage || IsIsland)
                {
                    ClearStageZones(_rig.stages[s]);
                }
            }
        }

        static void ClearStageZones(LavaStageRig stage)
        {
            if (stage.basinZone != null)
            {
                stage.basinZone.Clear();
            }

            if (stage.gates == null)
            {
                return;
            }

            for (int g = 0; g < stage.gates.Length; g++)
            {
                if (stage.gates[g] != null && stage.gates[g].zone != null)
                {
                    stage.gates[g].zone.Clear();
                }
            }
        }

        void Emit(LavaRunEventKind kind, int team, int stage, int index = 0, int value = 0, Vector3 position = default)
        {
            _events.Add(new LavaRunEvent(_recorder.CurrentTick, kind, team, stage, index, value, position));
        }

        void PublishMetrics()
        {
            int teamCount = _config.teams.Length;
            for (int s = 0; s < _stageAlive.Length; s++)
            {
                for (int t = 0; t < teamCount; t++)
                {
                    if (_stageAlive[s][t] >= 0)
                    {
                        _recorder.SetMetric($"stage{s + 1}_alive_{_config.teams[t].name}", _stageAlive[s][t]);
                    }
                }
            }

            for (int t = 0; t < teamCount; t++)
            {
                _recorder.SetMetric($"eliminated_stage_{_config.teams[t].name}", _eliminatedAtStage[t] + 1);
                _recorder.SetMetric($"peak_live_{_config.teams[t].name}", _peakLiveTeam[t]);
            }

            _recorder.SetMetric("deaths_lava", _deathsLava);
            _recorder.SetMetric("deaths_fall", _deathsFall);
            _recorder.SetMetric("deaths_eliminated", _deathsEliminated);
            _recorder.SetMetric("gate_triggers", _gateTriggers);
            _recorder.SetMetric("clones_spawned", _clonesSpawned);
            _recorder.SetMetric("gate_capped", _gateCapped);
            _recorder.SetMetric("peak_live_total", _peakLive);
            _recorder.SetMetric("lead_changes", _leadChanges);
            _recorder.SetMetric("island_seconds", _islandSeconds);
            _recorder.SetMetric("rings_sunk", _ringsSunk);
            _recorder.SetMetric("forced_decision", _forced ? 1f : 0f);
            _recorder.SetMetric("run_seconds", _runClock);
            _recorder.SetMetric("winner", _winner);

            // Standings: the winner, then colours in reverse order of going out (last out = second).
            _recorder.AddStanding(_config.teams[_winner].name);
            for (int i = _eliminationOrder.Count - 1; i >= 0; i--)
            {
                if (_eliminationOrder[i] != _winner)
                {
                    _recorder.AddStanding(_config.teams[_eliminationOrder[i]].name);
                }
            }
        }
    }
}
