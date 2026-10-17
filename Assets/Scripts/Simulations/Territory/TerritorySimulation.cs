using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Territory
{
    /// <summary>
    /// Four-colour territory war. Each colour owns a quadrant of a tiled board, a cannon, and a Plinko box
    /// whose slots multiply its ammo bank or release it as a barrage. Shots flip enemy tiles; lose your
    /// cannon's tile and you are out.
    /// </summary>
    /// <remarks>
    /// The per-tick order below is the determinism contract (see plans/videos/terr-001). Board balls are PhysX
    /// rigidbodies: PhysX steps after FixedUpdate, so slot triggers are drained here on the next tick, sorted by
    /// (team, slot, pool index). Everything else is plain maths on <see cref="OwnershipGrid"/>.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class TerritorySimulation : MonoBehaviour, ISimulation
    {
        [Header("Scene wiring (filled by the scene builder)")]
        [SerializeField] TerritoryTeamRig[] _rigs;
        [SerializeField] Transform _gridOrigin;

        struct SlotHit
        {
            public int team, slot, poolIndex;
        }

        static readonly Comparison<SlotHit> HitOrder = CompareHits;

        TerritoryConfig _config;
        SeededRandom _random;
        SeededRandom _spinRandom;
        SeededRandom[] _dropRandom;
        RunRecorder _recorder;

        OwnershipGrid _grid;
        GridFrame _frame;
        readonly List<TerritoryTeam> _teams = new List<TerritoryTeam>();
        readonly List<TerritoryEvent> _events = new List<TerritoryEvent>();

        CountBank[] _banks;
        BodyPool3D[] _pools;
        SweepAim[] _aims;
        GameObject[][] _ballObjects;
        CollisionDetector3D[][] _detectors;
        int[][] _spawnTicks;
        int[] _slotHits;
        int[] _spinFaceCounts;
        bool[] _protected;
        Func<int, bool> _isProtected;
        int[][] _plateCells;   // per team: grid indices of the base plate (the target an enemy ball must reach)
        readonly List<int> _plateScratch = new List<int>();
        bool _showdown;
        bool _heavyShots;
        int[] _pendingBombs;
        bool[] _shotIsBomb;
        int _bombsExploded;
        int _linesPainted;
        int _selfDestructs;
        int _cannonStrikes;
        int _rainDropsLanded;
        int _cellsRained;
        int _slotLogEveryTicks;
        bool _gravitySaved;
        Vector3 _savedGravity;
        readonly List<GameObject> _spawnedClones = new List<GameObject>();

        float[] _dropTimer;
        float[] _fireTimer;
        long[] _queued;
        int[] _boostTicksLeft;
        int[] _boostFactor;
        int[] _cannonDownLeft;
        int[] _cannonDownTotal;
        int[] _rainPending;
        bool[] _rainClear;
        Func<int, bool> _isRainProtected;

        GridBall[] _shots = new GridBall[0];
        int _shotsPerTeam;
        int[] _liveShots;

        readonly List<int> _spinQueue = new List<int>();
        int _spinTicksLeft;
        int _spinTeam = -1;
        int _spinFace;
        bool _spinActive;

        readonly List<int> _eliminationOrder = new List<int>();
        readonly List<SlotHit> _hits = new List<SlotHit>();
        readonly List<Collider> _drain = new List<Collider>();

        float _dt;
        int _holdTicks;
        bool[] _gateOpen;
        int _battleTicks;
        int _endTicks;
        int _escalationLevel;
        int _multiplierBonus;
        int _winner = -1;
        bool _decidedOnTiles;
        bool _failed;
        int _leader = -1;
        int _leadChanges;
        long _peakBank;
        int _totalReleases;
        int _ballsRecovered;
        int _aliveTeams;

        /// <summary>Scene wiring, called by the scene builder.</summary>
        public void Bind(TerritoryTeamRig[] rigs, Transform gridOrigin)
        {
            _rigs = rigs;
            _gridOrigin = gridOrigin;
        }

        public string FormatSlug => "terr";
        public bool IsComplete { get; private set; }
        public RunResult Result => _recorder?.Result;

        public TerritoryPhase Phase { get; private set; } = TerritoryPhase.Hold;
        public OwnershipGrid Grid => _grid;
        public GridFrame Frame => _frame;
        public TerritoryConfig Config => _config;
        public IReadOnlyList<TerritoryTeam> Teams => _teams;
        public IReadOnlyList<TerritoryTeamRig> Rigs => _rigs;

        /// <summary>Fixed capacity (teams x maxLiveShotsPerTeam). Render where <c>alive</c>.</summary>
        public IReadOnlyList<GridBall> Shots => _shots;

        public IReadOnlyList<TerritoryEvent> Events => _events;

        public bool SpinActive => _spinActive;
        public int SpinTeam => _spinTeam;
        public int SpinFace => _spinFace;
        public float SpinSecondsRemaining => _spinActive ? _spinTicksLeft * _dt : 0f;

        /// <summary>Battle seconds so far (zero during the hold).</summary>
        public float ElapsedSeconds => _battleTicks * _dt;

        public float SecondsUntilStart => Phase == TerritoryPhase.Hold ? Mathf.Max(0, _holdTicks) * _dt : 0f;

        /// <summary>-1 until decided.</summary>
        public int WinnerIndex => _winner;

        public int AliveTeams => _aliveTeams;

        public int EscalationLevel => _escalationLevel;

        /// <summary>Added to every multiplier when it is applied (board slots, spinner faces, boosts). Steps up with the multiplier ramp.</summary>
        public int MultiplierBonus => _multiplierBonus;

        /// <summary>Two teams left and config.showdownPlateRadiusCells set: plates grown, SPIN lanes open.</summary>
        public bool IsShowdown => _showdown;

        /// <summary>Every cell a normal ball paints splashes (config.heavyShotsAfterSeconds reached).</summary>
        public bool HeavyShotsActive => _heavyShots;

        /// <summary>Cannon sweep and fire-rate factor: 1 + config.cannonSpeedupPerElimination per team eliminated so far.</summary>
        public float CannonSpeedup => 1f + _config.cannonSpeedupPerElimination * _eliminationOrder.Count;
        public int LeadChanges => _leadChanges;
        public long PeakBank => _peakBank;
        public int TotalReleases => _totalReleases;
        public int QueuedSpins => _spinQueue.Count;

        /// <summary>Tile-count leader (ties keep the current leader). -1 before the run starts.</summary>
        public int Leader => _leader;

        /// <summary>Pool capacity of a team's board.</summary>
        public int BoardBallCapacity(int team)
        {
            return _ballObjects != null && team >= 0 && team < _ballObjects.Length ? _ballObjects[team].Length : 0;
        }

        /// <summary>The live board ball at a pool index, or null when that pool slot is free.</summary>
        public GameObject GetBoardBall(int team, int poolIndex)
        {
            return _ballObjects[team][poolIndex];
        }

        /// <summary>True when the shot in this global slot (index into <see cref="Shots"/>) is a live bomb.</summary>
        public bool IsBombShot(int slot)
        {
            return _shotIsBomb != null && slot >= 0 && slot < _shotIsBomb.Length && _shotIsBomb[slot] &&
                   slot < _shots.Length && _shots[slot].alive;
        }

        void OnDisable()
        {
            RestoreGravity();
        }

        void OnDestroy()
        {
            RestoreGravity();
        }

        void RestoreGravity()
        {
            if (_gravitySaved)
            {
                Physics.gravity = _savedGravity;
                _gravitySaved = false;
            }
        }

        public CollisionDetector3D GetBoardBallDetector(int team, int poolIndex)
        {
            return _detectors[team][poolIndex];
        }

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = (TerritoryConfig)config;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 16384);
            _dt = config.fixedTimestep;

            IsComplete = false;
            _failed = false;
            Phase = TerritoryPhase.Hold;
            _battleTicks = 0;
            _endTicks = 0;
            _escalationLevel = 0;
            _multiplierBonus = 0;
            _showdown = false;
            _heavyShots = false;
            _winner = -1;
            _decidedOnTiles = false;
            _leader = -1;
            _leadChanges = 0;
            _peakBank = 0;
            _totalReleases = 0;
            _ballsRecovered = 0;
            _spinActive = false;
            _spinTeam = -1;
            _spinFace = 0;
            _spinTicksLeft = 0;
            _spinQueue.Clear();
            _eliminationOrder.Clear();
            _events.Clear();
            _teams.Clear();

            ReleaseBoardBalls();

            // The boards are built visually flat (the reference look), so the slope is faked by tilting gravity
            // toward -Z, the camera. Board balls are the only rigidbodies in the scene, so nothing else feels it.
            if (!_gravitySaved)
            {
                _savedGravity = Physics.gravity;
                _gravitySaved = true;
            }

            float tilt = _config.boardTiltDegrees * Mathf.Deg2Rad;
            Physics.gravity = 9.81f * new Vector3(0f, -Mathf.Cos(tilt), -Mathf.Sin(tilt));
            _bombsExploded = 0;
            _linesPainted = 0;
            _selfDestructs = 0;
            _cannonStrikes = 0;
            _rainDropsLanded = 0;
            _cellsRained = 0;
            _rainDrops.Clear();
            _rainActive.Clear();
            _slotHits = new int[_config.boardSlots.Length];
            _spinFaceCounts = new int[_config.spinFaces.Length];
            _slotLogEveryTicks = Mathf.Max(1, Mathf.RoundToInt(60f / _dt));

            int teamCount = _config.teams.Length;
            if (_rigs == null || _rigs.Length != teamCount || _gridOrigin == null)
            {
                Debug.LogError("[terr] Needs one TerritoryTeamRig per team and a grid origin. Rebuild the scene.", this);
                Fail();
                return;
            }

            for (int i = 0; i < teamCount; i++)
            {
                TerritoryTeamRig rig = _rigs[i];
                if (rig == null || rig.ballTemplate == null || rig.dropPoint == null || rig.slots == null ||
                    rig.slots.Length != _config.boardSlots.Length)
                {
                    Debug.LogError($"[terr] Team {i} rig is incomplete (template, drop point, or slot count != boardSlots).", this);
                    Fail();
                    return;
                }
            }

            int width = _config.gridWidth;
            int height = _config.gridHeight;
            _grid = new OwnershipGrid(width, height, teamCount);
            int halfW = width / 2;
            int halfH = height / 2;
            _grid.FillRect(0, halfH, halfW, height - halfH, 0);
            _grid.FillRect(halfW, halfH, width - halfW, height - halfH, 1);
            _grid.FillRect(0, 0, halfW, halfH, 2);
            _grid.FillRect(halfW, 0, width - halfW, halfH, 3);
            _frame = GridFrame.FromTransform(_gridOrigin, _config.cellSize);

            // Protected cells: every team's base cell and its 8 neighbours. No painter call may change them.
            _protected = new bool[_grid.CellCount];
            for (int i = 0; i < teamCount; i++)
            {
                Vector2Int bc = _config.BaseCell(i);
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int px = bc.x + dx;
                        int py = bc.y + dy;
                        if (_grid.InBounds(px, py))
                        {
                            _protected[_grid.Index(px, py)] = true;
                        }
                    }
                }
            }

            // Base plates: the cells an enemy ball must paint to knock a team out. Protected too, so only a ball can reach them.
            _plateCells = new int[teamCount][];
            for (int i = 0; i < teamCount; i++)
            {
                SetPlate(i, _config.basePlateRadiusCells, -1);
            }

            _isProtected = index => _protected[index];

            // Rain clearance zone: within rainCannonClearance (Chebyshev) of any team's base cell, eliminated or not.
            _rainClear = new bool[_grid.CellCount];
            int clearance = Mathf.Max(1, _config.rainCannonClearance);
            for (int i = 0; i < teamCount; i++)
            {
                Vector2Int bc = _config.BaseCell(i);
                for (int dy = -clearance; dy <= clearance; dy++)
                {
                    for (int dx = -clearance; dx <= clearance; dx++)
                    {
                        int px = bc.x + dx;
                        int py = bc.y + dy;
                        if (_grid.InBounds(px, py))
                        {
                            _rainClear[_grid.Index(px, py)] = true;
                        }
                    }
                }
            }

            _isRainProtected = index => _protected[index] || _rainClear[index];

            _banks = new CountBank[teamCount];
            _gateOpen = new bool[teamCount];
            for (int i = 0; i < teamCount; i++)
            {
                if (_rigs[i].laneGate != null)
                {
                    _rigs[i].laneGate.enabled = true;   // every run starts closed
                }
            }

            _pools = new BodyPool3D[teamCount];
            _aims = new SweepAim[teamCount];
            _ballObjects = new GameObject[teamCount][];
            _detectors = new CollisionDetector3D[teamCount][];
            _spawnTicks = new int[teamCount][];
            _dropTimer = new float[teamCount];
            _fireTimer = new float[teamCount];
            _queued = new long[teamCount];
            _boostTicksLeft = new int[teamCount];
            _boostFactor = new int[teamCount];
            _cannonDownLeft = new int[teamCount];
            _cannonDownTotal = new int[teamCount];
            _rainPending = new int[teamCount];
            _liveShots = new int[teamCount];
            _dropRandom = new SeededRandom[teamCount];

            _spinRandom = _random.Branch(7);
            _shotsPerTeam = _config.maxLiveShotsPerTeam;
            _shots = new GridBall[teamCount * _shotsPerTeam];
            _shotIsBomb = new bool[_shots.Length];
            _pendingBombs = new int[teamCount];

            for (int i = 0; i < teamCount; i++)
            {
                TerritoryTeamDef def = _config.teams[i];
                var team = new TerritoryTeam
                {
                    index = i,
                    name = def.name,
                    color = def.color,
                    baseCell = _config.BaseCell(i)
                };

                _banks[i] = new CountBank(_config.bankStart, _config.bankCap);
                team.Bank = _banks[i].Value;
                _dropRandom[i] = _random.Branch(100 + i);
                Vector2 range = _config.SweepRange(i);
                float phase = _random.Branch(300 + i).NextFloat();
                _aims[i] = SweepAim.Create(range.x, range.y, _config.sweepDegreesPerSecond, phase);
                team.AimDegrees = _aims[i].angle;

                TerritoryTeamRig rig = _rigs[i];
                Transform parent = rig.ballParent != null ? rig.ballParent : rig.transform;
                int before = parent.childCount;
                _pools[i] = new BodyPool3D(rig.ballTemplate, parent, _config.ballsPerBoard);
                for (int c = before; c < parent.childCount; c++)
                {
                    _spawnedClones.Add(parent.GetChild(c).gameObject);
                }

                int capacity = _pools[i].Capacity;
                _ballObjects[i] = new GameObject[capacity];
                _detectors[i] = new CollisionDetector3D[capacity];
                _spawnTicks[i] = new int[capacity];

                team.Tiles = _grid.CountOf(i);
                team.Share = _grid.ShareOf(i);
                _teams.Add(team);
            }

            _aliveTeams = teamCount;
            _peakBank = _config.bankStart;
            _holdTicks = Mathf.Max(1, Mathf.CeilToInt(_config.preStartHoldSeconds / _dt));

            _recorder.SetMetric("teams", teamCount);
            _recorder.SetMetric("grid_cells", _grid.CellCount);
            _recorder.LogEvent("field_set", teamCount);
        }

        /// <summary>True while team's lane gate is gone (balls can reach SPIN). Always closed during the hold.</summary>
        public bool IsLaneGateOpen(int team)
        {
            return _gateOpen != null && team >= 0 && team < _gateOpen.Length && _gateOpen[team];
        }

        /// <summary>Seconds until the team's gate next changes state (open to closed or closed to open).</summary>
        public float LaneGateSecondsToToggle(int team)
        {
            if (_config == null || _dt <= 0f || _rigs == null || team < 0 || team >= _rigs.Length || _showdown)
            {
                return 0f;
            }

            GateTicks(out int closedTicks, out int cycleTicks);
            int pos = GatePhaseTicks(team, cycleTicks);
            int left = pos >= closedTicks ? cycleTicks - pos : closedTicks - pos;
            float seconds = left * _dt;
            // During the hold the gate stays closed until the battle starts, so its first toggle is later.
            return Phase == TerritoryPhase.Hold ? seconds + SecondsUntilStart : seconds;
        }

        void GateTicks(out int closedTicks, out int cycleTicks)
        {
            closedTicks = Mathf.Max(1, Mathf.RoundToInt(_config.laneGateClosedSeconds / _dt));
            // Progression: each elimination so far widens the open window, so the endgame spins more often.
            float openSeconds = _config.laneGateOpenSeconds + _config.laneGateOpenPerElimination * _eliminationOrder.Count;
            int openTicks = Mathf.Max(1, Mathf.RoundToInt(openSeconds / _dt));
            cycleTicks = closedTicks + openTicks;
        }

        int GatePhaseTicks(int team, int cycleTicks)
        {
            int teamCount = _rigs.Length;
            int offset = (int)((long)team * cycleTicks / teamCount);
            return (_battleTicks + offset) % cycleTicks;
        }

        void UpdateLaneGates()
        {
            if (!_config.laneGateEnabled || _gateOpen == null)
            {
                return;
            }

            GateTicks(out int closedTicks, out int cycleTicks);
            for (int t = 0; t < _rigs.Length; t++)
            {
                Collider gate = _rigs[t].laneGate;
                if (gate == null)
                {
                    continue;
                }

                // A firing team's board is locked: the gate stays closed whatever the cycle says.
                // In the final showdown the lane stays open (still closed while that team fires).
                bool open = Phase != TerritoryPhase.Hold && _queued[t] <= 0 &&
                            (_showdown || GatePhaseTicks(t, cycleTicks) >= closedTicks);
                if (open != _gateOpen[t])
                {
                    _gateOpen[t] = open;
                    gate.enabled = !open;
                }
            }
        }

        public void Tick(float fixedDelta)
        {
            _recorder.AdvanceTick();
            if (_failed || IsComplete)
            {
                return;
            }

            int tick = _recorder.CurrentTick;

            UpdateLaneGates();

            // 1. Phase clock.
            if (Phase == TerritoryPhase.Hold)
            {
                _holdTicks--;
                if (_holdTicks <= 0)
                {
                    StartBattle();
                }

                return;
            }

            if (Phase == TerritoryPhase.Complete)
            {
                _endTicks--;
                if (_endTicks <= 0)
                {
                    PublishMetrics();
                    IsComplete = true;
                    _recorder.Finish(CompletionReason.Finished);
                }

                return;
            }

            _battleTicks++;
            if (_battleTicks % _slotLogEveryTicks == 0)
            {
                LogSlotHits();
            }

            TickBoosts();
            TickCannons();
            DropBalls(fixedDelta);           // 2
            ProcessSlots(tick);              // 3
            RecoverBalls(tick);              // 4
            StepSpinner(tick);               // 5
            LandRain(tick);
            Fire(fixedDelta, tick);          // 6
            MoveShots(fixedDelta, tick);     // 7
            UpdateTeamStats();
            CheckEliminations(tick);         // 8
            UpdateProgression(tick);
            Escalate(tick);                  // 9
            RampMultipliers();
            CheckEnd(tick);                  // 10

            if (_battleTicks % 600 == 0)
            {
                PublishMetrics();
            }
        }

        // ------------------------------------------------------------------ setup / teardown

        void Fail()
        {
            _failed = true;
            IsComplete = true;
            _recorder.Finish(CompletionReason.Failed);
        }

        void ReleaseBoardBalls()
        {
            if (_pools != null)
            {
                for (int i = 0; i < _pools.Length; i++)
                {
                    _pools[i]?.ReleaseAll();
                }
            }

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
            _pools = null;
        }

        void StartBattle()
        {
            Phase = TerritoryPhase.Battle;
            float interval = CurrentDropInterval();
            int count = _teams.Count;
            for (int i = 0; i < count; i++)
            {
                // Staggered phases so the four boards do not drop in lockstep.
                _dropTimer[i] = (i / (float)count) * interval;
                _fireTimer[i] = 0f;
            }

            UpdateTeamStats();
            _leader = FindLeader(-1);
            _recorder.LogEvent("battle_start", 0f);
        }

        // ------------------------------------------------------------------ 2. drops

        float CurrentDropInterval()
        {
            float interval = _config.dropIntervalSeconds * Mathf.Pow(_config.escalateDropFactor, _escalationLevel);
            return Mathf.Max(_config.minDropIntervalSeconds, interval);
        }

        void DropBalls(float dt)
        {
            for (int t = 0; t < _teams.Count; t++)
            {
                if (_teams[t].Eliminated)
                {
                    continue;
                }

                _dropTimer[t] -= dt;
                if (_dropTimer[t] > 0f)
                {
                    continue;
                }

                _dropTimer[t] += CurrentDropInterval();

                TerritoryTeamRig rig = _rigs[t];

                // Drawn even when the pool is empty so a skipped drop never shifts the stream.
                int spreadSamples = Mathf.Max(1, _config.dropSpreadSamples);
                float spreadSum = 0f;
                for (int k = 0; k < spreadSamples; k++)
                {
                    spreadSum += _dropRandom[t].NextFloat() * 2f - 1f;
                }

                float x = rig.dropHalfWidth * _config.dropSpreadFraction * (spreadSum / spreadSamples);
                Vector3 position = rig.dropPoint.position + rig.dropPoint.right * x;
                GameObject ball = _pools[t].TryGet(position);
                if (ball == null)
                {
                    continue;
                }

                int index = _pools[t].IndexOf(ball);
                if (index < 0)
                {
                    _pools[t].Release(ball);
                    continue;
                }

                _ballObjects[t][index] = ball;
                _spawnTicks[t][index] = _recorder.CurrentTick;
                CollisionDetector3D detector = _detectors[t][index];
                if (detector == null)
                {
                    detector = ball.GetComponent<CollisionDetector3D>();
                    _detectors[t][index] = detector;
                }

                if (detector != null)
                {
                    detector.Reset();
                }
            }
        }

        // ------------------------------------------------------------------ 3. slots

        static int CompareHits(SlotHit a, SlotHit b)
        {
            if (a.team != b.team)
            {
                return a.team.CompareTo(b.team);
            }

            if (a.slot != b.slot)
            {
                return a.slot.CompareTo(b.slot);
            }

            return a.poolIndex.CompareTo(b.poolIndex);
        }

        void ProcessSlots(int tick)
        {
            _hits.Clear();
            for (int t = 0; t < _teams.Count; t++)
            {
                TriggerZone3D[] slots = _rigs[t].slots;
                for (int s = 0; s < slots.Length; s++)
                {
                    _drain.Clear();
                    if (slots[s] == null)
                    {
                        continue;
                    }

                    slots[s].Drain(_drain);
                    for (int c = 0; c < _drain.Count; c++)
                    {
                        Collider collider = _drain[c];
                        if (collider == null)
                        {
                            continue;
                        }

                        Rigidbody body = collider.attachedRigidbody;
                        GameObject ball = body != null ? body.gameObject : collider.gameObject;
                        int index = _pools[t].IndexOf(ball);
                        if (index < 0 || _ballObjects[t][index] != ball)
                        {
                            continue;
                        }

                        _hits.Add(new SlotHit { team = t, slot = s, poolIndex = index });
                    }
                }
            }

            if (_hits.Count > 1)
            {
                _hits.Sort(HitOrder);
            }

            for (int h = 0; h < _hits.Count; h++)
            {
                SlotHit hit = _hits[h];
                GameObject ball = _ballObjects[hit.team][hit.poolIndex];
                if (ball == null)
                {
                    continue; // already consumed by an earlier hit this tick
                }

                _pools[hit.team].Release(ball);
                _ballObjects[hit.team][hit.poolIndex] = null;
                _slotHits[hit.slot]++;

                if (_teams[hit.team].Eliminated)
                {
                    continue; // finished rolling, slot ignored
                }

                long before = _banks[hit.team].Value;
                SlotEffect slotEffect = _config.boardSlots[hit.slot];
                if ((slotEffect.kind == SlotEffectKind.Multiply || slotEffect.kind == SlotEffectKind.Release)
                    && IsBoardLocked(hit.team))
                {
                    // Board locked while firing: ball consumed, no effect.
                    Emit(TerritoryEventKind.SlotHit, hit.team, hit.slot, 2, before, before);
                    continue;
                }

                if (slotEffect.kind == SlotEffectKind.Multiply && _boostTicksLeft[hit.team] > 0)
                {
                    slotEffect = SlotEffect.Multiply(_boostFactor[hit.team]);   // timed boost replaces the slot's own factor
                }

                ApplyEffect(hit.team, slotEffect, tick);
                long after = _banks[hit.team].Value;
                Emit(TerritoryEventKind.SlotHit, hit.team, hit.slot, 0, before, after);
            }
        }

        void ApplyEffect(int team, SlotEffect effect, int tick)
        {
            CountBank bank = _banks[team];
            switch (effect.kind)
            {
                case SlotEffectKind.Multiply:
                    bank.Multiply(effect.value + _multiplierBonus);   // the ramp lifts every ×n, boosted slots included
                    NoteBank(team);
                    AutoReleaseIfMaxed(team);
                    break;
                case SlotEffectKind.Add:
                    bank.Add(effect.value);
                    NoteBank(team);
                    AutoReleaseIfMaxed(team);
                    break;
                case SlotEffectKind.Release:
                    DoRelease(team, 0);
                    break;
                case SlotEffectKind.Bonus:
                    EnqueueSpin(team);
                    break;
            }
        }

        /// <summary>A bank that reaches the cap fires at once (Released with other = 1) instead of pinning there.</summary>
        void AutoReleaseIfMaxed(int team)
        {
            if (_config.autoReleaseAtCap && _banks[team].Value >= _config.bankCap)
            {
                DoRelease(team, 1);
            }
        }

        void NoteBank(int team)
        {
            long value = _banks[team].Value;
            _teams[team].Bank = value;
            if (value > _peakBank)
            {
                _peakBank = value;
            }
        }

        void DoRelease(int team, int other)
        {
            CountBank bank = _banks[team];
            if (bank.Value > _peakBank)
            {
                _peakBank = bank.Value;
            }

            long amount = bank.Drain(_config.bankStart);
            _teams[team].Bank = bank.Value;

            long previouslyQueued = _queued[team];
            _queued[team] += amount;

            _teams[team].Queued = _queued[team];
            _totalReleases++;
            Emit(TerritoryEventKind.Released, team, 0, other, previouslyQueued, amount);
            _recorder.LogEvent("release", amount);
        }

        void EnqueueSpin(int team)
        {
            int already = 0;
            for (int i = 0; i < _spinQueue.Count; i++)
            {
                if (_spinQueue[i] == team)
                {
                    already++;
                }
            }

            if (already < _config.maxQueuedSpins)
            {
                _spinQueue.Add(team);
            }
        }

        // ------------------------------------------------------------------ 4. stuck / lost balls

        void RecoverBalls(int tick)
        {
            int stuckTicks = Mathf.CeilToInt(_config.ballStuckSeconds / _dt);
            float lowest = _config.boardLength * Mathf.Sin(_config.boardTiltDegrees * Mathf.Deg2Rad) + _config.ballRecoverMargin;
            float farSqr = (_config.boardLength + _config.boardWidth) * (_config.boardLength + _config.boardWidth);

            for (int t = 0; t < _teams.Count; t++)
            {
                GameObject[] balls = _ballObjects[t];
                Vector3 origin = _rigs[t].dropPoint.position;
                for (int i = 0; i < balls.Length; i++)
                {
                    GameObject ball = balls[i];
                    if (ball == null)
                    {
                        continue;
                    }

                    int reason = -1;
                    Vector3 position = ball.transform.position;
                    if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
                    {
                        reason = 2;
                    }
                    else if (position.y < origin.y - lowest)
                    {
                        reason = 1;
                    }
                    else
                    {
                        Vector3 flat = position - origin;
                        flat.y = 0f;
                        if (flat.sqrMagnitude > farSqr)
                        {
                            reason = 2;
                        }
                    }

                    if (reason < 0 && tick - _spawnTicks[t][i] > stuckTicks)
                    {
                        reason = 0;
                    }

                    if (reason < 0)
                    {
                        continue;
                    }

                    _pools[t].Release(ball);
                    balls[i] = null;
                    _ballsRecovered++;
                    Emit(TerritoryEventKind.BallRecovered, t, i, reason, 0, 0);
                    _recorder.LogEvent(reason == 0 ? "ball_stuck" : "ball_lost", t);
                }
            }
        }

        // ------------------------------------------------------------------ 5. spinner

        void StepSpinner(int tick)
        {
            if (_spinActive)
            {
                _spinTicksLeft--;
                if (_spinTicksLeft <= 0)
                {
                    ResolveSpin(tick);
                }

                return;
            }

            while (_spinQueue.Count > 0)
            {
                int team = _spinQueue[0];
                _spinQueue.RemoveAt(0);
                if (_teams[team].Eliminated)
                {
                    continue;
                }

                _spinActive = true;
                _spinTeam = team;
                _spinFace = DrawSpinFace();
                _spinTicksLeft = Mathf.Max(1, Mathf.CeilToInt(_config.spinSeconds / _dt));
                Emit(TerritoryEventKind.SpinStarted, team, _spinFace, 0, 0, 0);
                _recorder.LogEvent("spin_start", team);
                break;
            }
        }

        int DrawSpinFace()
        {
            int total = _config.SpinWeightTotal();
            int pick = _spinRandom.NextInt(total);
            for (int i = 0; i < _config.spinFaces.Length; i++)
            {
                pick -= Mathf.Max(0, _config.spinFaces[i].weight);
                if (pick < 0)
                {
                    return i;
                }
            }

            return _config.spinFaces.Length - 1;
        }

        void ResolveSpin(int tick)
        {
            int team = _spinTeam;
            bool out_ = _teams[team].Eliminated;
            Emit(TerritoryEventKind.SpinResolved, team, _spinFace, out_ ? 1 : 0, 0, 0);
            _spinActive = false;
            if (!out_)
            {
                SpinFace face = _config.spinFaces[_spinFace];
                _spinFaceCounts[_spinFace]++;
                switch (face.kind)
                {
                    case SpinFaceKind.Bomb:
                        _pendingBombs[team] += face.value > 0 ? face.value : _config.bombShots;
                        break;
                    case SpinFaceKind.PlusLine:
                        DoPlusLine(team, face.value > 0 ? face.value : _config.lineThickness, tick);
                        break;
                    case SpinFaceKind.Boost:
                        StartBoost(team, Math.Max(1, face.value));
                        break;
                    case SpinFaceKind.SelfDestruct:
                        DoSelfDestruct(team, face.value > 0 ? face.value : _config.selfDestructRadius, tick);
                        break;
                    case SpinFaceKind.CannonStrike:
                        DoCannonStrike(team, face.value > 0 ? face.value : _config.cannonDownSeconds, tick);
                        break;
                    case SpinFaceKind.Rain:
                        DoRain(team, face.value > 0 ? face.value : _config.rainDrops, tick);
                        break;
                    default:
                        ApplyEffect(team, face.effect, tick);
                        break;
                }

                _recorder.LogEvent("spin_resolved", _spinFace);
            }
        }

        /// <summary>A new boost keeps the larger factor and always restarts the timer.</summary>
        void StartBoost(int team, int factor)
        {
            _boostFactor[team] = _boostTicksLeft[team] > 0 ? Math.Max(_boostFactor[team], factor) : factor;
            _boostTicksLeft[team] = Math.Max(1, Mathf.CeilToInt(_config.boostSeconds / _dt));
            Emit(TerritoryEventKind.BoostStarted, team, _boostFactor[team], 0, 0, _boostTicksLeft[team]);
            _recorder.LogEvent("boost", _boostFactor[team]);
        }

        void TickBoosts()
        {
            for (int t = 0; t < _boostTicksLeft.Length; t++)
            {
                if (_boostTicksLeft[t] > 0 && --_boostTicksLeft[t] == 0)
                {
                    _boostFactor[t] = 1;
                    Emit(TerritoryEventKind.BoostEnded, t, 0, 0, 0, 0);
                }
            }
        }

        /// <summary>True while the team is firing (queued ammo > 0): lane gate shut, Multiply and Release slots inert.</summary>
        public bool IsBoardLocked(int team)
        {
            return _queued != null && team >= 0 && team < _queued.Length && _queued[team] > 0;
        }

        /// <summary>Multiply factor applied to this team's Multiply slots by a running boost (1 when none).</summary>
        public int BoostFactor(int team)
        {
            return _boostTicksLeft != null && team >= 0 && team < _boostTicksLeft.Length && _boostTicksLeft[team] > 0
                ? _boostFactor[team] : 1;
        }

        public float BoostSecondsLeft(int team)
        {
            return _boostTicksLeft != null && team >= 0 && team < _boostTicksLeft.Length
                ? _boostTicksLeft[team] * _dt : 0f;
        }

        // ------------------------------------------------------------------ terr-002: cannon strike + rain (contract; see plans/videos/terr-002)

        readonly List<RainDrop> _rainDrops = new List<RainDrop>();

        /// <summary>The runner's current tick (the tick the last Tick ran). Presentation times rain falls against it.</summary>
        public int CurrentTick => _recorder != null ? _recorder.CurrentTick : 0;

        /// <summary>Seconds per simulation tick (the config's fixed timestep).</summary>
        public float TickSeconds => _dt;

        /// <summary>Every rain drop ever scheduled, append-only, in schedule order. A drop whose team was eliminated before its landTick never paints.</summary>
        public IReadOnlyList<RainDrop> RainDrops => _rainDrops;

        /// <summary>True while the team's cannon is down from a BOOM (CannonStrike) face.</summary>
        public bool IsCannonDown(int team)
        {
            return _cannonDownLeft != null && team >= 0 && team < _cannonDownLeft.Length && _cannonDownLeft[team] > 0;
        }

        /// <summary>Seconds until the downed cannon is repaired (0 when it is up).</summary>
        public float CannonDownSecondsLeft(int team)
        {
            return IsCannonDown(team) ? _cannonDownLeft[team] * _dt : 0f;
        }

        /// <summary>Remaining down time as a fraction of the full down time: 1 just struck, 0 repaired.</summary>
        public float CannonDownFraction(int team)
        {
            return IsCannonDown(team) ? _cannonDownLeft[team] / (float)Math.Max(1, _cannonDownTotal[team]) : 0f;
        }

        /// <summary>True from RainStarted until the team's last drop lands (RainEnded).</summary>
        public bool IsRaining(int team)
        {
            return _rainPending != null && team >= 0 && team < _rainPending.Length && _rainPending[team] > 0;
        }

        readonly List<int> _rainActive = new List<int>();   // indices into _rainDrops not yet landed or cancelled, schedule order
        readonly List<int> _rainCandidates = new List<int>();
        readonly List<int> _strikeCandidates = new List<int>();

        /// <summary>
        /// BOOM: picks the target per config, then (re)starts its down timer. No live target means the face
        /// fizzles with no event. A strike on a cannon that is already down restarts the full timer.
        /// </summary>
        void DoCannonStrike(int spinner, float seconds, int tick)
        {
            int target = -1;
            if (_config.cannonStrikeTarget == CannonStrikeTarget.Self)
            {
                target = spinner;
            }
            else
            {
                _strikeCandidates.Clear();
                for (int t = 0; t < _teams.Count; t++)
                {
                    if (t != spinner && !_teams[t].Eliminated)
                    {
                        _strikeCandidates.Add(t);
                    }
                }

                if (_strikeCandidates.Count > 0)
                {
                    if (_config.cannonStrikeTarget == CannonStrikeTarget.RandomEnemy)
                    {
                        target = _strikeCandidates[_spinRandom.NextInt(_strikeCandidates.Count)];
                    }
                    else
                    {
                        // Leader: most tiles right now, ties to the lower index (strict > keeps the first).
                        int bestTiles = -1;
                        for (int i = 0; i < _strikeCandidates.Count; i++)
                        {
                            int tiles = _grid.CountOf(_strikeCandidates[i]);
                            if (tiles > bestTiles)
                            {
                                bestTiles = tiles;
                                target = _strikeCandidates[i];
                            }
                        }
                    }
                }
            }

            if (target < 0)
            {
                return;
            }

            int downTicks = Math.Max(1, Mathf.CeilToInt(seconds / _dt));
            _cannonDownLeft[target] = downTicks;
            _cannonDownTotal[target] = downTicks;
            _cannonStrikes++;
            Emit(TerritoryEventKind.CannonDestroyed, target, 0, spinner, 0, downTicks);
            _recorder.LogEvent("cannon_destroyed", target);
        }

        void TickCannons()
        {
            for (int t = 0; t < _cannonDownLeft.Length; t++)
            {
                if (_cannonDownLeft[t] > 0 && --_cannonDownLeft[t] == 0)
                {
                    Emit(TerritoryEventKind.CannonRepaired, t, 0, 0, 0, 0);
                    _recorder.LogEvent("cannon_repaired", t);
                }
            }
        }

        /// <summary>
        /// RAIN: samples up to the requested count of cells without replacement (partial Fisher-Yates on the
        /// spin stream) from cells the team does not own and that sit outside the cannon clearance zone, and
        /// schedules one drop per cell, evenly spread over rainSeconds after rainLeadSeconds.
        /// </summary>
        void DoRain(int team, int requested, int tick)
        {
            _rainCandidates.Clear();
            for (int i = 0; i < _grid.CellCount; i++)
            {
                if (_grid.GetOwner(i) != team && !_rainClear[i])
                {
                    _rainCandidates.Add(i);
                }
            }

            int n = Math.Min(Math.Max(0, requested), _rainCandidates.Count);
            if (n == 0)
            {
                Emit(TerritoryEventKind.RainStarted, team, 0, 0, 0, tick);
                _recorder.LogEvent("rain", 0);
                return;
            }

            int leadTicks = Mathf.RoundToInt(_config.rainLeadSeconds / _dt);
            int rainTicks = Mathf.RoundToInt(_config.rainSeconds / _dt);
            int span = Math.Max(1, n - 1);
            int last = tick;
            for (int i = 0; i < n; i++)
            {
                int j = i + _spinRandom.NextInt(_rainCandidates.Count - i);
                int cell = _rainCandidates[j];
                _rainCandidates[j] = _rainCandidates[i];
                _rainCandidates[i] = cell;

                int land = Math.Max(tick + 1, tick + leadTicks + (int)((long)i * rainTicks / span));
                _rainActive.Add(_rainDrops.Count);
                _rainDrops.Add(new RainDrop(team, cell, tick, land));
                last = land;
            }

            _rainPending[team] += n;
            Emit(TerritoryEventKind.RainStarted, team, n, 0, 0, last);
            _recorder.LogEvent("rain", n);
        }

        /// <summary>Paints the drops whose land tick is this tick, in schedule order, and closes a team's rain after its last drop.</summary>
        void LandRain(int tick)
        {
            if (_rainActive.Count == 0)
            {
                return;
            }

            int keep = 0;
            for (int a = 0; a < _rainActive.Count; a++)
            {
                int index = _rainActive[a];
                RainDrop drop = _rainDrops[index];
                if (drop.landTick != tick)
                {
                    _rainActive[keep++] = index;
                    continue;
                }

                if (_teams[drop.team].Eliminated)
                {
                    continue;   // cancelled (normally already removed by CheckEliminations)
                }

                _grid.Coords(drop.cell, out int cx, out int cy);
                int changed = GridPainter.PaintDisc(_grid, cx, cy, _config.rainSplashRadius, drop.team, tick, _isRainProtected);
                _rainDropsLanded++;
                _cellsRained += changed;
                Emit(TerritoryEventKind.RainDropLanded, drop.team, drop.cell, 0, 0, changed);
                if (--_rainPending[drop.team] == 0)
                {
                    Emit(TerritoryEventKind.RainEnded, drop.team, 0, 0, 0, 0);
                }
            }

            _rainActive.RemoveRange(keep, _rainActive.Count - keep);
        }

        /// <summary>Elimination: clears the down timer (no Repaired event) and cancels the team's unlanded rain.</summary>
        void ClearTerr002State(int team)
        {
            _cannonDownLeft[team] = 0;
            _cannonDownTotal[team] = 0;
            if (_rainPending[team] > 0)
            {
                int keep = 0;
                for (int a = 0; a < _rainActive.Count; a++)
                {
                    int index = _rainActive[a];
                    if (_rainDrops[index].team != team)
                    {
                        _rainActive[keep++] = index;
                    }
                }

                _rainActive.RemoveRange(keep, _rainActive.Count - keep);
                _rainPending[team] = 0;
                Emit(TerritoryEventKind.RainEnded, team, 0, 1, 0, 0);
            }
        }

        /// <summary>
        /// + LINE: a uniformly random enemy-owned, non-protected cell (spin stream), then its full row and column
        /// painted to the team. With no such cell, the team's base row and column are painted instead.
        /// </summary>
        void DoPlusLine(int team, int thickness, int tick)
        {
            int count = 0;
            for (int i = 0; i < _grid.CellCount; i++)
            {
                int owner = _grid.GetOwner(i);
                if (owner >= 0 && owner != team && !_protected[i])
                {
                    count++;
                }
            }

            int target;
            int fallback = 0;
            if (count > 0)
            {
                int pick = _spinRandom.NextInt(count);
                target = 0;
                for (int i = 0; i < _grid.CellCount; i++)
                {
                    int owner = _grid.GetOwner(i);
                    if (owner >= 0 && owner != team && !_protected[i] && pick-- == 0)
                    {
                        target = i;
                        break;
                    }
                }
            }
            else
            {
                Vector2Int bc = _teams[team].baseCell;
                target = _grid.Index(bc.x, bc.y);
                fallback = 1;
            }

            _grid.Coords(target, out int tx, out int ty);
            int changed = GridPainter.PaintRow(_grid, ty, thickness, team, tick, _isProtected) +
                          GridPainter.PaintColumn(_grid, tx, thickness, team, tick, _isProtected);
            _linesPainted++;
            Emit(TerritoryEventKind.LinePainted, team, target, fallback, 0, changed);
            _recorder.LogEvent("line", changed);
        }

        /// <summary>
        /// SELF DESTRUCT: bank back to bankStart, queued ammo cleared (live shots and pending bombs kept), then a
        /// disc turns Unowned around a seeded own cell on the frontier (an own, non-protected cell with a 4-neighbour
        /// owned by another team); failing that any own non-protected cell; failing that no blast.
        /// </summary>
        void DoSelfDestruct(int team, int radius, int tick)
        {
            long bankBefore = _banks[team].Value;
            _banks[team].Drain(_config.bankStart);
            NoteBank(team);
            _queued[team] = 0;
            _teams[team].Queued = 0;

            int frontier = CountOwnCells(team, true);
            bool useFrontier = frontier > 0;
            int candidates = useFrontier ? frontier : CountOwnCells(team, false);

            int centre = -1;
            int changed = 0;
            if (candidates > 0)
            {
                int pick = _spinRandom.NextInt(candidates);
                for (int i = 0; i < _grid.CellCount; i++)
                {
                    if (IsSelfDestructCandidate(team, i, useFrontier) && pick-- == 0)
                    {
                        centre = i;
                        break;
                    }
                }

                _grid.Coords(centre, out int cx, out int cy);
                changed = GridPainter.PaintDisc(_grid, cx, cy, radius, OwnershipGrid.Unowned, tick, _isProtected);
            }

            _selfDestructs++;
            Emit(TerritoryEventKind.SelfDestructed, team, centre, 0, bankBefore, changed);
            _recorder.LogEvent("self_destruct", changed);
        }

        int CountOwnCells(int team, bool frontierOnly)
        {
            int n = 0;
            for (int i = 0; i < _grid.CellCount; i++)
            {
                if (IsSelfDestructCandidate(team, i, frontierOnly))
                {
                    n++;
                }
            }

            return n;
        }

        bool IsSelfDestructCandidate(int team, int index, bool frontierOnly)
        {
            if (_grid.GetOwner(index) != team || _protected[index])
            {
                return false;
            }

            if (!frontierOnly)
            {
                return true;
            }

            _grid.Coords(index, out int x, out int y);
            return IsOther(x - 1, y, team) || IsOther(x + 1, y, team) || IsOther(x, y - 1, team) || IsOther(x, y + 1, team);
        }

        bool IsOther(int x, int y, int team)
        {
            if (!_grid.InBounds(x, y))
            {
                return false;
            }

            int owner = _grid.GetOwner(x, y);
            return owner >= 0 && owner != team;
        }

        // ------------------------------------------------------------------ 6. fire

        void Fire(float dt, int tick)
        {
            float speed = _config.shotSpeedCellsPerSecond;
            float speedup = CannonSpeedup;
            for (int t = 0; t < _teams.Count; t++)
            {
                TerritoryTeam team = _teams[t];
                if (team.Eliminated || _cannonDownLeft[t] > 0)
                {
                    continue;   // a downed cannon holds fire: aim frozen, ammo and pending bombs wait
                }

                _aims[t].Step(dt * speedup);   // progression: faster sweep per elimination
                team.AimDegrees = _aims[t].angle;

                // Idle time carries at most one tick of credit, so the first shot after a release is
                // immediate and the long-run rate is exactly 1 / fireIntervalSeconds.
                _fireTimer[t] = Mathf.Max(_fireTimer[t] - dt, -dt);
                bool bomb = _pendingBombs[t] > 0;
                if (_fireTimer[t] > 0f || (!bomb && (_queued[t] <= 0 || _liveShots[t] >= _shotsPerTeam)))
                {
                    continue;
                }

                int slot = -1;
                int first = t * _shotsPerTeam;
                for (int i = 0; i < _shotsPerTeam; i++)
                {
                    if (!_shots[first + i].alive)
                    {
                        slot = first + i;
                        break;
                    }
                }

                if (slot < 0)
                {
                    continue;
                }

                _fireTimer[t] += _config.fireIntervalSeconds / speedup;

                // A pending bomb goes before normal ammo, health 1, and does not consume ammo.
                int health;
                if (bomb)
                {
                    health = 1;
                    _pendingBombs[t]--;
                }
                else
                {
                    health = (int)Math.Min(_config.shotHealth, _queued[t]);
                    _queued[t] -= health;
                    team.Queued = _queued[t];
                }

                _shotIsBomb[slot] = bomb;

                Vector2 baseCell = new Vector2(team.baseCell.x + 0.5f, team.baseCell.y + 0.5f);
                _shots[slot] = new GridBall
                {
                    position = baseCell,
                    velocity = _aims[t].Direction * speed,
                    owner = t,
                    health = health,
                    alive = true,
                    age = 0f
                };

                _liveShots[t]++;
                Emit(TerritoryEventKind.ShotFired, t, slot - first, 0, health, _queued[t]);
            }
        }

        // ------------------------------------------------------------------ 7. move shots

        void MoveShots(float dt, int tick)
        {
            for (int i = 0; i < _shots.Length; i++)
            {
                if (!_shots[i].alive)
                {
                    continue;
                }

                int painted = GridBouncer.Step(ref _shots[i], _grid, dt, tick);
                if (painted > 0 && _shotIsBomb[i])
                {
                    // Blast centre = the cell the bomb occupies after the step. GridBouncer keeps a ball in its
                    // previous cell when it paints, so this is the bomb's own-side cell next to what it hit.
                    int cx = Mathf.Clamp((int)Mathf.Floor(_shots[i].position.x), 0, _grid.Width - 1);
                    int cy = Mathf.Clamp((int)Mathf.Floor(_shots[i].position.y), 0, _grid.Height - 1);
                    int owner = _shots[i].owner;
                    int changed = GridPainter.PaintDisc(_grid, cx, cy, _config.bombRadius, owner, tick, _isProtected);
                    _shots[i].alive = false;
                    _bombsExploded++;
                    Emit(TerritoryEventKind.BombExploded, owner, _grid.Index(cx, cy), 0, 0, changed);
                }
                else if (painted > 0 && _heavyShots)
                {
                    // Heavy shot: the hit splashes. Centred on the ball's own-side cell next to the hit, like a bomb, so
                    // radius 1 covers the hit cell and its neighbours. Plates stay protected: only the ball itself kills.
                    int cx = Mathf.Clamp((int)Mathf.Floor(_shots[i].position.x), 0, _grid.Width - 1);
                    int cy = Mathf.Clamp((int)Mathf.Floor(_shots[i].position.y), 0, _grid.Height - 1);
                    GridPainter.PaintDisc(_grid, cx, cy, _config.heavyShotRadius, _shots[i].owner, tick, _isProtected);
                }

                if (_shots[i].alive && _shots[i].age > _config.shotMaxLifetimeSeconds)
                {
                    _shots[i].alive = false;
                }

                if (!_shots[i].alive)
                {
                    _liveShots[i / _shotsPerTeam]--;
                }
            }
        }

        // ------------------------------------------------------------------ stats / eliminations

        void UpdateTeamStats()
        {
            for (int t = 0; t < _teams.Count; t++)
            {
                TerritoryTeam team = _teams[t];
                team.Tiles = _grid.CountOf(t);
                team.Share = _grid.ShareOf(t);
                team.LiveShots = _liveShots[t];
                team.Bank = _banks[t].Value;
                team.Queued = _queued[t];
            }

            int leader = FindLeader(_leader);
            if (leader != _leader)
            {
                if (_leader >= 0 && Phase != TerritoryPhase.Hold)
                {
                    _leadChanges++;
                    _recorder.LogEvent("lead_change", leader);
                }

                _leader = leader;
            }
        }

        int FindLeader(int current)
        {
            int best = current >= 0 ? current : 0;
            int bestTiles = _grid.CountOf(best);
            for (int t = 0; t < _teams.Count; t++)
            {
                int tiles = _grid.CountOf(t);
                if (tiles > bestTiles)
                {
                    best = t;
                    bestTiles = tiles;
                }
            }

            return best;
        }

        void CheckEliminations(int tick)
        {
            for (int t = 0; t < _teams.Count; t++)
            {
                TerritoryTeam team = _teams[t];
                if (team.Eliminated)
                {
                    continue;
                }

                // Out when an enemy has painted any cell of the base plate (terr-001: the plate is the base cell alone).
                int owner = t;
                int[] plate = _plateCells[t];
                for (int c = 0; c < plate.Length && owner == t; c++)
                {
                    owner = _grid.GetOwner(plate[c]);
                }

                if (owner == t && team.Tiles > 0)
                {
                    continue;
                }

                team.Eliminated = true;
                _eliminationOrder.Add(t);
                _aliveTeams--;
                KillShots(t);
                _queued[t] = 0;
                team.Queued = 0;
                if (_boostTicksLeft[t] > 0)
                {
                    _boostTicksLeft[t] = 0;
                    _boostFactor[t] = 1;
                    Emit(TerritoryEventKind.BoostEnded, t, 0, 0, 0, 0);
                }

                ClearTerr002State(t);
                _spinQueue.RemoveAll(x => x == t);

                Emit(TerritoryEventKind.Eliminated, t, 0, owner, 0, 0);
                _recorder.LogEvent("eliminated_" + team.name, owner);
                _recorder.SetMetric("eliminated_" + team.name + "_tick", tick);
                RewardKnockout(owner, t);
            }
        }

        /// <summary>Progression: the team that reached the plate takes the loser's bank and a free spin.</summary>
        void RewardKnockout(int taker, int loser)
        {
            if (!_config.knockoutReward || taker < 0 || taker >= _teams.Count || taker == loser || _teams[taker].Eliminated)
            {
                return;
            }

            CountBank bank = _banks[taker];
            long before = bank.Value;
            long loot = _banks[loser].Drain(0);
            _teams[loser].Bank = _banks[loser].Value;
            bank.Add(loot);
            NoteBank(taker);
            long after = bank.Value;

            int queuedBefore = _spinQueue.Count;
            EnqueueSpin(taker);
            int spin = _spinQueue.Count > queuedBefore ? 1 : 0;

            Emit(TerritoryEventKind.KnockoutReward, taker, loser, spin, before, after);
            _recorder.LogEvent("knockout_reward", loot);
            AutoReleaseIfMaxed(taker);
        }

        /// <summary>Per-tick progression switches: the final showdown at two teams left, and heavy shots after a set time.</summary>
        void UpdateProgression(int tick)
        {
            if (!_showdown && _config.showdownPlateRadiusCells > 0f && _teams.Count > 2 && _aliveTeams == 2)
            {
                _showdown = true;
                int claimed = 0;
                for (int t = 0; t < _teams.Count; t++)
                {
                    if (!_teams[t].Eliminated)
                    {
                        claimed += SetPlate(t, Mathf.Max(_config.showdownPlateRadiusCells, _config.basePlateRadiusCells), tick);
                    }
                }

                Emit(TerritoryEventKind.ShowdownStarted, 0, claimed, 0, 0, 0);
                _recorder.LogEvent("showdown", claimed);
            }

            if (!_heavyShots && _config.heavyShotsAfterSeconds > 0f && ElapsedSeconds >= _config.heavyShotsAfterSeconds)
            {
                _heavyShots = true;
                Emit(TerritoryEventKind.HeavyShotsStarted, 0, _config.heavyShotRadius, 0, 0, 0);
                _recorder.LogEvent("heavy_shots", _config.heavyShotRadius);
            }
        }

        /// <summary>
        /// (Re)builds the team's base plate at <paramref name="radiusCells"/> and protects its cells. With
        /// <paramref name="claimTick"/> &gt;= 0 the plate also claims every cell under it for the team (the showdown
        /// growth must not knock anyone out by landing on enemy ground). Returns the cells claimed.
        /// </summary>
        int SetPlate(int team, float radiusCells, int claimTick)
        {
            _plateScratch.Clear();
            int claimed = 0;
            Vector2Int bc = _config.BaseCell(team);
            int reach = Mathf.CeilToInt(radiusCells);
            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int px = bc.x + dx;
                    int py = bc.y + dy;
                    if (!_grid.InBounds(px, py) || !_config.OnBasePlate(team, px, py, radiusCells))
                    {
                        continue;
                    }

                    int index = _grid.Index(px, py);
                    _plateScratch.Add(index);
                    _protected[index] = true;
                    if (claimTick >= 0 && _grid.GetOwner(index) != team)
                    {
                        _grid.SetOwner(px, py, team, claimTick);
                        claimed++;
                    }
                }
            }

            _plateCells[team] = _plateScratch.ToArray();
            return claimed;
        }

        void KillShots(int team)
        {
            int first = team * _shotsPerTeam;
            for (int i = 0; i < _shotsPerTeam; i++)
            {
                _shots[first + i].alive = false;
                _shotIsBomb[first + i] = false;
            }

            _pendingBombs[team] = 0;

            _liveShots[team] = 0;
            _teams[team].LiveShots = 0;
        }

        // ------------------------------------------------------------------ 9. escalation

        /// <summary>Every multiplierRampSeconds of battle, every multiplier gains multiplierRampStep (0 seconds = off).</summary>
        void RampMultipliers()
        {
            if (_config.multiplierRampSeconds <= 0f)
            {
                return;
            }

            int level = _multiplierBonus / Math.Max(1, _config.multiplierRampStep);
            if (ElapsedSeconds < (level + 1) * _config.multiplierRampSeconds)
            {
                return;
            }

            _multiplierBonus += Math.Max(1, _config.multiplierRampStep);
            Emit(TerritoryEventKind.MultipliersRaised, 0, _multiplierBonus, 0, 0, 0);
            _recorder.LogEvent("multipliers_raised", _multiplierBonus);
        }

        void Escalate(int tick)
        {
            float next = _config.escalateAfterSeconds + _escalationLevel * _config.escalateEverySeconds;
            if (ElapsedSeconds < next)
            {
                return;
            }

            _escalationLevel++;
            Emit(TerritoryEventKind.Escalated, 0, _escalationLevel, 0, 0, 0);
            _recorder.LogEvent("escalation", _escalationLevel);
        }

        // ------------------------------------------------------------------ 10. end

        void CheckEnd(int tick)
        {
            if (_aliveTeams <= 1)
            {
                int winner = -1;
                for (int t = 0; t < _teams.Count; t++)
                {
                    if (!_teams[t].Eliminated)
                    {
                        winner = t;
                    }
                }

                if (winner < 0)
                {
                    winner = MostTilesAmong(false);
                }

                Decide(winner, false);
                return;
            }

            if (ElapsedSeconds >= _config.decisionAtSeconds)
            {
                Decide(MostTilesAmong(true), true);
                return;
            }

            if (_aliveTeams <= 2 && Phase == TerritoryPhase.Battle)
            {
                Phase = TerritoryPhase.Finale;
                _recorder.LogEvent("finale", _aliveTeams);
            }
        }

        int MostTilesAmong(bool aliveOnly)
        {
            int best = -1;
            for (int t = 0; t < _teams.Count; t++)
            {
                if (aliveOnly && _teams[t].Eliminated)
                {
                    continue;
                }

                if (best < 0 || _teams[t].Tiles > _teams[best].Tiles)
                {
                    best = t;
                }
            }

            return best < 0 ? 0 : best;
        }

        void Decide(int winner, bool onTiles)
        {
            _winner = winner;
            _decidedOnTiles = onTiles;
            Phase = TerritoryPhase.Complete;
            _spinActive = false;
            _spinQueue.Clear();
            for (int t = 0; t < _teams.Count; t++)
            {
                KillShots(t);
                _queued[t] = 0;
                _teams[t].Queued = 0;
            }

            var standings = new List<string> { _teams[winner].name };

            var survivors = new List<int>();
            for (int t = 0; t < _teams.Count; t++)
            {
                if (t != winner && !_teams[t].Eliminated)
                {
                    survivors.Add(t);
                }
            }

            // Several survivors only happens on a forced decision: rank them by tiles, ties to the lower index.
            survivors.Sort((a, b) =>
            {
                int c = _teams[b].Tiles.CompareTo(_teams[a].Tiles);
                return c != 0 ? c : a.CompareTo(b);
            });

            for (int i = 0; i < survivors.Count; i++)
            {
                standings.Add(_teams[survivors[i]].name);
            }

            for (int i = _eliminationOrder.Count - 1; i >= 0; i--)
            {
                if (_eliminationOrder[i] != winner)
                {
                    standings.Add(_teams[_eliminationOrder[i]].name);
                }
            }

            _recorder.SetStandings(standings);
            Emit(TerritoryEventKind.Winner, winner, 0, onTiles ? 1 : 0, 0, 0);
            _recorder.LogEvent(onTiles ? "winner_on_tiles" : "winner", winner);
            LogSlotHits();
            PublishMetrics();

            _endTicks = Mathf.Max(1, Mathf.CeilToInt(_config.endHoldSeconds / _dt));
        }

        /// <summary>Console diagnostic for slot odds. Reads counters only: no outcome or RNG effect.</summary>
        void LogSlotHits()
        {
            if (_slotHits == null)
            {
                return;
            }

            int total = 0;
            for (int i = 0; i < _slotHits.Length; i++)
            {
                total += _slotHits[i];
            }

            int seconds = Mathf.RoundToInt(ElapsedSeconds);
            var sb = new System.Text.StringBuilder();
            sb.Append("[terr] slot hits @").Append(seconds / 60).Append(':').Append((seconds % 60).ToString("00")).Append(" — ");
            for (int i = 0; i < _slotHits.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(" | ");
                }

                sb.Append(_config.boardSlots[i].Label).Append(' ').Append(_slotHits[i]);
                sb.Append(" (").Append(total > 0 ? Mathf.RoundToInt(100f * _slotHits[i] / total) : 0).Append("%)");
            }

            Debug.Log(sb.ToString());

            if (_spinFaceCounts != null)
            {
                var faces = new System.Text.StringBuilder();
                faces.Append("[terr] spin faces @").Append(seconds / 60).Append(':').Append((seconds % 60).ToString("00")).Append(" — ");
                for (int i = 0; i < _spinFaceCounts.Length; i++)
                {
                    if (i > 0)
                    {
                        faces.Append(" | ");
                    }

                    faces.Append(_config.spinFaces[i].Label).Append(' ').Append(_spinFaceCounts[i]);
                }

                Debug.Log(faces.ToString());
            }
        }

        void PublishMetrics()
        {
            if (_slotHits != null)
            {
                for (int i = 0; i < _slotHits.Length; i++)
                {
                    _recorder.SetMetric("slot_hits_" + i, _slotHits[i]);
                }

            }

            if (_spinFaceCounts != null)
            {
                for (int i = 0; i < _spinFaceCounts.Length; i++)
                {
                    _recorder.SetMetric("spin_face_" + i, _spinFaceCounts[i]);
                }
            }

            _recorder.SetMetric("lead_changes", _leadChanges);
            _recorder.SetMetric("peak_bank", _peakBank);
            _recorder.SetMetric("total_releases", _totalReleases);
            _recorder.SetMetric("eliminations", _eliminationOrder.Count);
            _recorder.SetMetric("escalation_level", _escalationLevel);
            _recorder.SetMetric("multiplier_bonus", _multiplierBonus);
            _recorder.SetMetric("balls_recovered", _ballsRecovered);
            _recorder.SetMetric("bombs_exploded", _bombsExploded);
            _recorder.SetMetric("lines_painted", _linesPainted);
            _recorder.SetMetric("self_destructs", _selfDestructs);
            _recorder.SetMetric("cannon_strikes", _cannonStrikes);
            _recorder.SetMetric("rain_drops_landed", _rainDropsLanded);
            _recorder.SetMetric("cells_rained", _cellsRained);
            _recorder.SetMetric("battle_seconds", ElapsedSeconds);
            _recorder.SetMetric("winner", _winner);
            _recorder.SetMetric("decided_on_tiles", _decidedOnTiles ? 1f : 0f);
            for (int t = 0; t < _teams.Count; t++)
            {
                _recorder.SetMetric("tiles_" + _teams[t].name, _teams[t].Tiles);
            }
        }

        void Emit(TerritoryEventKind kind, int team, int slot, int other, long before, long after)
        {
            _events.Add(new TerritoryEvent(_recorder.CurrentTick, kind, team, slot, other, before, after));
        }
    }
}
