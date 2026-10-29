using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// The marble machine (<c>mach</c>): thousands of balls pre-packed in the silo, released on a schedule, carried
    /// through the stations, sorted into colour tubes. First tube to <see cref="MachineConfig.tubeCapacity"/> wins.
    /// Counting, recycling and the safety nets live here; each station only moves its own parts.
    /// </summary>
    /// <remarks>
    /// Scale (16,800 balls): a ball only becomes a GameObject with a collider and Rigidbody when it wakes. Until then it is a
    /// matrix in a GPU-instanced batch (the frozen stack, one low-poly draw per 1023 balls per colour), which moves as a block
    /// when the pile drains. Balls counted into a tube are frozen and join a second batch, so neither the frozen stack nor
    /// the tubes cost a renderer, a physics body or a loop iteration. <see cref="Balls"/> is the active (moving) list.
    /// </remarks>
    public sealed class MachineSimulation : MonoBehaviour, ISimulation, IMachineState
    {
        [Header("Stations")]
        public SiloStation silo;
        public CannonStation cannon;
        public SorterStation sorter;
        public TubeRack tubeRack;
        [Tooltip("Flow order: silo, peg wall, ..., sorter, tube rack, cannon.")]
        public List<MachineStation> stations = new List<MachineStation>();

        [Header("Balls")]
        public Transform ballRoot;
        public Mesh ballMesh;
        [Tooltip("Optional 80-triangle ball for the frozen stack. Left empty, one is generated at Initialize.")]
        public Mesh ballMeshLow;
        public Material[] ballMaterials;

        [Header("Performance")]
        [Tooltip("Every ball is a real rigidbody from frame one (asleep until the pile is disturbed). Slow in real time, so render offline " +
                 "at a fixed timestep. Off: only Active Silo Target balls are physical and the rest is a frozen picture woken in chunks.")]
        public bool simulateAllBalls = true;
        [Tooltip("All-balls mode: the pile is held rigid (kinematic) until the gate opens, then released from the bottom up at this " +
                 "speed, m/s. A fully live 29-layer pile compresses and sprays balls out of the silo; a rising front doesn't.")]
        [Min(0.05f)] public float pileReleaseSpeed = 0.35f;
        [Tooltip("Moving balls cast shadows onto the track. Thousands of casters through a high-resolution soft shadow map are a " +
                 "big cost; turn on for final renders.")]
        public bool castBallShadows = false;
        [Tooltip("Most physics steps run in one frame. When the machine is too heavy for real time the video slows down smoothly " +
                 "instead of stuttering through a burst of catch-up steps.")]
        [Range(1, 8)] public int maxStepsPerFrame = 3;
        [Tooltip("Dynamic balls kept in the silo and cone. The rest of the starting stack is frozen and woken a chunk at a time.")]
        [Min(50)] public int activeSiloTarget = 350;
        [Tooltip("Hard cap on moving balls: no new balls wake above it, and the flood gate eases shut as it nears.")]
        [Min(200)] public int maxActiveBalls = 2000;
        [Tooltip("Balls woken per step while the silo pile is below its target.")]
        [Min(1)] public int wakeChunk = 80;
        [Tooltip("Speed the frozen block slides down a layer when one is used up, m/s (capped at 0.5).")]
        [Min(0.1f)] public float stackLowerSpeed = 0.4f;
        [Tooltip("Mass-normalised sleep energy for balls. 0.01 is about 0.14 m/s: queued balls sleep, rolling ones don't.")]
        [Min(0f)] public float ballSleepThreshold = 0.01f;

        // Balls still packed in the silo are kept cheap (low-poly, no shadow, discrete collision, no interpolation, checked
        // every PileStride ticks) until the first tick they are seen outside it.
        const int PileStride = 8;
        int _releaseCursor;
        float _releaseFront;

        const float StallSpeedSqr = 0.02f * 0.02f;
        const int StallStride = 10;
        const float FreezeSpeedSqr = 0.05f * 0.05f;
        const float FreezeAfterSeconds = 0.5f;
        const float WakeInterval = 0.1f;
        const float RiderSpeedSqr = 0.15f * 0.15f;
        const float RiderRestSeconds = 0.5f;
        const float OutletRadius = 0.7f;
        const float FlowWindowSeconds = 1f;
        const float TrimIntervalSeconds = 5f;
        const float FlowSettleSeconds = 6f;

        MachineConfig _config;
        SeededRandom _rng;
        SeededRandom _nudgeRng;
        SeededRandom _cannonRng;
        SeededRandom _dropRng;
        RunRecorder _recorder;

        ISilo _silo;
        ICannon _cannon;
        ISorter _sorter;
        ITubeRack _rack;

        readonly List<string> _standings = new List<string>();
        int[] _counts;
        int[] _order;

        // Per-ball tables, indexed by ball id (= lattice index). Ball objects exist only for woken balls.
        int _placed;
        int _created;
        int[] _colourOf;
        MachineBall[] _ballOf;
        MeshRenderer[] _rendererOf;
        SphereCollider[] _colliderOf;
        int _colliderCursor;
        float _restoreMaxDelta = -1f;
        const int PileColliderMargin = 4;
        float[] _slowSince;
        bool[] _outPassed;
        bool[] _inPile;
        int[] _lossByStation;
        int[] _stuckByStation;
        float _nextLossReport;
        Mesh _lowMesh;
        bool _swapMesh;
        float _visualScale;
        PhysicsMaterial _ballPhysics;

        // Active (dynamic, uncounted) balls; swap-removed. Counted balls wait in _settling until they can be frozen.
        readonly List<MachineBall> _active = new List<MachineBall>();
        readonly List<MachineBall> _settling = new List<MachineBall>();
        int[] _activeIndex;
        int _settleCursor;
        int _peakActive;

        // Dormant silo stack: frozen lattice (instanced, no bodies), then riders (recycled balls resting on the lid).
        Vector3[] _basePos;
        int[] _layerStart;
        float[] _layerY;
        int _layerCount;
        float _layerH;
        int _latticeCursor;
        int _cylFirstLayer;
        readonly List<MachineBall> _riders = new List<MachineBall>();
        int _riderCursor;
        int[] _frozenSlot;
        BallBatch _frozen;
        BallBatch _settled;
        float _stackOffset;
        float _appliedOffset;
        float _nextWake;
        int _siloActive;
        Bounds _siloZone;
        Bounds _trayBounds;
        Rigidbody _capBody;
        float _capRestY;
        Vector3 _capCentre;
        float _capRadius;

        // Feed regulation: the commanded rate is trimmed so the measured outlet flow matches the schedule.
        float _feedTarget;
        float _feedTrim = 1f;
        float _flowEma;
        int _outCount;
        float _nextFlowSample;
        float _nextTrim;
        float _feedChangedAt;
        float _outletY;

        bool _initialized;
        float _simTime;
        int _tick;
        MachinePhase _phase = MachinePhase.Hold;
        int _leader = -1;
        int _leadChanges;
        int _winner = -1;
        float _winTime = -1f;
        float _completeAt;
        int _totalSorted;
        int _lost;
        int _nudges;
        int _wrongTube;
        int _absorbed;
        float _appliedFeed = -1f;
        int _appliedFlood = -1;
        float _tubeMaxY;
        Bounds _storageExpanded;
        Bounds _breechExpanded;
        Bounds _trayExpanded;

        public string FormatSlug => "mach";
        public bool IsComplete { get; private set; }

        public RunResult Result
        {
            get
            {
                if (_recorder == null) return null;
                if (!IsComplete) RefreshResult();
                return _recorder.Result;
            }
        }

        // ---------- IMachineState ----------

        public float SimTime => _simTime;
        public MachinePhase Phase => _phase;
        public int ColourCount => _config != null ? _config.ColourCount : 0;
        public string ColourName(int colour) => _config.colours[colour].name;
        public Color ColourValue(int colour) => _config.colours[colour].color;
        public int TubeCount(int colour) => _counts != null && colour >= 0 && colour < _counts.Length ? _counts[colour] : 0;
        public int TubeCapacity => _config != null ? _config.tubeCapacity : 0;
        public int LeaderIndex => _leader;
        public int LeadChanges => _leadChanges;
        public int WinnerIndex => _winner;
        public int TotalSorted => _totalSorted;
        public int Recycled => cannon != null ? cannon.FiredCount : 0;

        /// <summary>The active (dynamic, uncounted) balls only. Frozen stack and tube balls are not listed: they don't move.</summary>
        public IReadOnlyList<MachineBall> Balls => _active;

        public IReadOnlyList<MachineStation> Stations => stations;
        public Vector3 TubeTop(int colour) => _rack.TubeTop(colour);
        public Vector3 TubeFillLine(int colour) => _rack.TubeFillLine(colour);

        /// <summary>Balls still frozen in the silo stack (instanced, no body).</summary>
        public int FrozenRemaining => _placed - _latticeCursor;

        public event Action<MachinePhase> PhaseChanged;
        public event Action<int> LeaderChanged;
        public event Action<int, Vector3> BallSorted;
        public event Action<int> Won;

        // ---------- ISimulation ----------

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = config as MachineConfig;
            _rng = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _rng);

            if (_restoreMaxDelta < 0f) _restoreMaxDelta = Time.maximumDeltaTime;
            Time.maximumDeltaTime = Mathf.Max(0.02f, config.fixedTimestep * maxStepsPerFrame);

            if (_config == null)
            {
                Debug.LogError("[MachineSimulation] Config is not a MachineConfig.", this);
                Fail();
                return;
            }

            if (!_config.Validate(out string error))
            {
                Debug.LogError($"[MachineSimulation] Invalid config: {error}", this);
                Fail();
                return;
            }

            _silo = silo;
            _cannon = cannon;
            _sorter = sorter as ISorter;
            _rack = tubeRack as ITubeRack;
            if (silo == null || cannon == null || _rack == null)
            {
                Debug.LogError("[MachineSimulation] silo, cannon and tubeRack must be assigned.", this);
                Fail();
                return;
            }

            if (ballMesh == null || ballMaterials == null || ballMaterials.Length < _config.ColourCount)
            {
                Debug.LogError("[MachineSimulation] ballMesh / ballMaterials missing or fewer than colours.", this);
                Fail();
                return;
            }

            _nudgeRng = _rng.Branch(901);
            _cannonRng = _rng.Branch(902);
            _dropRng = _rng.Branch(903);

            _counts = new int[_config.ColourCount];
            _order = new int[_config.ColourCount];
            _simTime = 0f;
            _tick = 0;
            _phase = MachinePhase.Hold;
            _leader = -1;
            _leadChanges = 0;
            _winner = -1;
            _winTime = -1f;
            _totalSorted = _lost = _nudges = _wrongTube = _absorbed = 0;
            _appliedFeed = -1f;
            _appliedFlood = -1;
            _feedTarget = 0f;
            _feedTrim = 1f;
            _flowEma = 0f;
            _outCount = 0;
            _nextFlowSample = FlowWindowSeconds;
            _nextTrim = TrimIntervalSeconds;
            _feedChangedAt = 0f;
            _peakActive = 0;

            _tubeMaxY = float.NegativeInfinity;
            for (int t = 0; t < _rack.TubeCount; t++)
            {
                _tubeMaxY = Mathf.Max(_tubeMaxY, _rack.TubeTop(t).y);
            }

            _tubeMaxY += 0.1f;
            Bounds sb = silo.StorageBounds;
            float zoneBottom = silo.ConeBottomWorld.y - 0.35f;
            _siloZone = new Bounds(Vector3.zero, Vector3.zero);
            _siloZone.SetMinMax(new Vector3(sb.min.x - 0.1f, zoneBottom, sb.min.z - 0.1f),
                new Vector3(sb.max.x + 0.1f, sb.max.y + 1f, sb.max.z + 0.1f));
            _storageExpanded = _siloZone;
            _storageExpanded.Expand(1f);
            _breechExpanded = cannon.Bounds;
            _breechExpanded.Expand(1f);
            _trayBounds = silo.TrayBounds;
            _trayExpanded = silo.activityBounds;
            _trayExpanded.Expand(0.3f);
            _outletY = silo.OutletWorldY;
            cannon.scatterRadius = silo.InnerRadius * 0.45f;

            CreateBalls();

            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i] != null) stations[i].Initialize(_config, _rng.Branch(i + 1));
            }

            _initialized = true;
            _recorder.SetMetric("balls", _placed);
            _recorder.LogEvent("field_set", _placed);
        }

        public void Tick(float fixedDelta)
        {
            if (!_initialized || IsComplete) return;

            _recorder.AdvanceTick();
            _tick++;
            _simTime += fixedDelta;

            ApplySchedule();
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i] != null) stations[i].Step(_simTime, fixedDelta);
            }

            _sorter?.StepSort(_simTime, _active);
            _cannon.StepFire(_simTime, _active, _silo.CannonTarget, _cannonRng);

            ProcessBalls();
            SettleCounted();
            ReleasePile(fixedDelta);
            ReportDiagnostics();
            UpdateStack(fixedDelta);
            UpdateFeedTrim();
            UpdateContest();

            if (_active.Count > _peakActive) _peakActive = _active.Count;

            if (_winner >= 0 && _simTime >= _completeAt)
            {
                RefreshResult();
                IsComplete = true;
                _recorder.Finish(CompletionReason.Finished);
            }
        }

        void OnDestroy()
        {
            if (_restoreMaxDelta > 0f) Time.maximumDeltaTime = _restoreMaxDelta;
        }

        /// <summary>Draws the frozen stack and the settled tube balls (instanced; presentation only).</summary>
        void LateUpdate()
        {
            if (!_initialized) return;
            _frozen?.Draw();
            _settled?.Draw();
        }

        // ---------- setup ----------

        void Fail()
        {
            IsComplete = true;
            _recorder.Finish(CompletionReason.Failed);
        }

        void CreateBalls()
        {
            int wanted = _config.ballCount;
            float r = _config.ballRadius;
            float s = 2.05f * r;
            float rowH = s * 0.8660254f;
            float layerH = s * 0.8164966f;
            Bounds bounds = silo.StorageBounds;
            Vector3 centre = bounds.center;
            float cylLattice = silo.InnerRadius - r * 1.05f;
            float coneBottomY = silo.ConeBottomWorld.y;
            float slope = Mathf.Max(0.1f, silo.coneSlope);
            float sinWall = slope / Mathf.Sqrt(1f + slope * slope);
            float wallClear = r * 1.05f / sinWall;

            // The lattice fills the cone as well as the cylinder, so the silo is full from frame one. Each layer's radius
            // follows the cone wall (a ball needs 1.05 r of normal clearance from a wall that leans at atan(slope)).
            var positions = new Vector3[wanted];
            int placed = 0;
            var layerStarts = new List<int>();
            var layerYs = new List<float>();
            float y0 = coneBottomY + r * 1.5f;
            for (int layer = 0; placed < wanted; layer++)
            {
                float y = y0 + layer * layerH;
                if (y > bounds.max.y - r) break;
                float lim = y < bounds.min.y
                    ? Mathf.Min(cylLattice, silo.throatRadius + (y - coneBottomY) / slope - wallClear)
                    : cylLattice;
                layerStarts.Add(placed);
                layerYs.Add(y);
                if (lim < r) continue;
                bool odd = (layer & 1) == 1;
                float offX = odd ? s * 0.5f : 0f;
                float offZ = odd ? rowH / 3f : 0f;
                int rows = Mathf.CeilToInt(lim / rowH) + 1;
                int cols = Mathf.CeilToInt(lim / s) + 1;
                for (int j = -rows; j <= rows && placed < wanted; j++)
                {
                    float z = j * rowH + offZ;
                    if (Mathf.Abs(z) > lim) continue;
                    float rowOff = (j & 1) != 0 ? s * 0.5f : 0f;
                    for (int i = -cols; i <= cols && placed < wanted; i++)
                    {
                        float x = i * s + rowOff + offX;
                        if (x * x + z * z > lim * lim) continue;
                        positions[placed++] = new Vector3(centre.x + x, y, centre.z + z);
                    }
                }
            }

            layerStarts.Add(placed);
            _cylFirstLayer = layerYs.Count;
            for (int i = 0; i < layerYs.Count; i++)
            {
                if (layerYs[i] >= bounds.min.y)
                {
                    _cylFirstLayer = i;
                    break;
                }
            }

            _layerStart = layerStarts.ToArray();
            _layerY = layerYs.ToArray();
            _layerCount = layerYs.Count;
            _layerH = layerH;
            _basePos = positions;
            _placed = placed;

            if (placed < wanted)
            {
                Debug.LogError($"[MachineSimulation] Silo too small: only {placed} of {wanted} balls fit the lattice. " +
                               "Lower ballCount or enlarge the silo.", this);
            }
            else
            {
                float topBall = _layerCount > 0 ? _layerY[_layerCount - 1] + r : coneBottomY;
                Debug.Log($"[mach] Silo lattice: {placed} balls in {_layerCount} layers, top ball at {topBall - bounds.max.y:0.00} m " +
                          $"relative to the rim ({bounds.max.y - topBall:0.00} m free).");
            }

            // Equal split over the colours, then shuffled.
            int colours = _config.ColourCount;
            _colourOf = new int[placed];
            int per = placed / colours;
            int rem = placed % colours;
            int k = 0;
            for (int c = 0; c < colours; c++)
            {
                int n = per + (c < rem ? 1 : 0);
                for (int m = 0; m < n; m++) _colourOf[k++] = c;
            }

            _rng.Shuffle(_colourOf);

            if (ballRoot == null)
            {
                ballRoot = new GameObject("Balls").transform;
                ballRoot.SetParent(transform, false);
            }

            for (int c = ballRoot.childCount - 1; c >= 0; c--)
            {
                Destroy(ballRoot.GetChild(c).gameObject);
            }

            Vector3 ext = ballMesh.bounds.extents;
            float meshRadius = Mathf.Max(0.001f, ext.x, ext.y, ext.z);
            _visualScale = r / meshRadius;
            _ballPhysics = new PhysicsMaterial("MachineBall")
            {
                bounciness = _config.ballBounciness,
                dynamicFriction = _config.ballFriction,
                staticFriction = _config.ballFriction,
                bounceCombine = PhysicsMaterialCombine.Average,
                frictionCombine = PhysicsMaterialCombine.Average
            };

            for (int c = 0; c < colours; c++) ballMaterials[c].enableInstancing = true;

            // Instanced batches: the frozen stack (low-poly, sized for the starting colour counts) and settled tube balls.
            var frozenCap = new int[colours];
            for (int i = 0; i < placed; i++) frozenCap[_colourOf[i]]++;
            var settledCap = new int[colours];
            for (int c = 0; c < colours; c++) settledCap[c] = Mathf.Max(64, _config.tubeCapacity);
            Mesh lowMesh = ballMeshLow != null ? ballMeshLow : BuildLowPolyBall();
            _lowMesh = lowMesh;
            // Swapping meshes keeps the sphere collider (it scales with the transform), so only do it when both meshes are the same size.
            float lowR = Mathf.Max(lowMesh.bounds.extents.x, lowMesh.bounds.extents.y, lowMesh.bounds.extents.z);
            _swapMesh = Mathf.Abs(lowR - meshRadius) < 0.01f * meshRadius;
            _inPile = new bool[placed];
            _lossByStation = null;
            _stuckByStation = null;
            _releaseCursor = 0;
            _releaseFront = float.NegativeInfinity;
            _nextLossReport = 0f;
            var drawBounds = new Bounds(centre, new Vector3(400f, 400f, 400f));
            _frozenSlot = new int[placed];
            _frozen = new BallBatch(colours, frozenCap, lowMesh, ballMaterials, _frozenSlot, drawBounds, ShadowCastingMode.Off);
            _settled = new BallBatch(colours, settledCap, ballMesh, ballMaterials, null, drawBounds, ShadowCastingMode.Off);

            _active.Clear();
            _settling.Clear();
            _riders.Clear();
            _riderCursor = 0;
            _ballOf = new MachineBall[placed];
            _rendererOf = new MeshRenderer[placed];
            _colliderOf = new SphereCollider[placed];
            _colliderCursor = 0;
            _slowSince = new float[placed];
            _outPassed = new bool[placed];
            _activeIndex = new int[placed];
            _created = 0;
            _settleCursor = 0;

            // Only the bottom of the stack is dynamic; the rest is a frozen instanced block woken from the bottom up.
            int dynamicBalls = simulateAllBalls ? placed : Mathf.Min(placed, activeSiloTarget);
            _latticeCursor = 0;
            for (int i = 0; i < placed; i++) _slowSince[i] = -1f;
            for (int i = 0; i < dynamicBalls; i++)
            {
                MachineBall b = CreateBall(i, positions[i]);
                AddActive(b);
                // The lattice is in static equilibrium: start asleep so the hold costs nothing, and the pile wakes by contact.
                if (simulateAllBalls) MakePileBall(b);
            }

            _latticeCursor = dynamicBalls;
            Vector3 lowExt = lowMesh.bounds.extents;
            Vector3 scale3 = Vector3.one * (r / Mathf.Max(0.001f, lowExt.x, lowExt.y, lowExt.z));
            // In all-balls mode the pile is drawn from the instanced batch too (a handful of draw calls instead of 16,800
            // renderers); a ball gets its own renderer back the moment it is released.
            for (int i = simulateAllBalls ? 0 : dynamicBalls; i < placed; i++)
            {
                _frozenSlot[i] = _frozen.Add(_colourOf[i], i, Matrix4x4.TRS(positions[i], Quaternion.identity, scale3));
            }

            if (simulateAllBalls) EnablePileColliders(PileColliderMargin);

            _siloActive = dynamicBalls;
            _stackOffset = _appliedOffset = 0f;
            _nextWake = 0f;
            CreateCap(r);
        }

        /// <summary>Index of the station whose activity bounds are closest to the point.</summary>
        int NearestStation(Vector3 at, out float distance)
        {
            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i] == null) continue;
                float d = stations[i].activityBounds.SqrDistance(at);
                if (d < best)
                {
                    best = d;
                    nearest = i;
                }
            }

            distance = Mathf.Sqrt(best);
            return nearest;
        }

        /// <summary>Diagnostics: where balls leave the machine. Logs the first few falls; <see cref="ReportDiagnostics"/> tallies.</summary>
        void LogLoss(Vector3 at)
        {
            int nearest = NearestStation(at, out float away);
            if (_lossByStation == null || _lossByStation.Length != stations.Count) _lossByStation = new int[stations.Count];
            _lossByStation[nearest]++;
            if (_lost <= 25)
            {
                Debug.Log($"[mach] ball fell off near '{stations[nearest].displayName}' ({away:0.0} m from its bounds) at {at} t={_simTime:0.0}s");
            }
        }

        void CountStuck(Vector3 at)
        {
            int nearest = NearestStation(at, out _);
            if (_stuckByStation == null || _stuckByStation.Length != stations.Count) _stuckByStation = new int[stations.Count];
            _stuckByStation[nearest]++;
        }

        /// <summary>Every 30 s of sim time: throughput, falls and stuck-ball nudges, each by nearest station.</summary>
        void ReportDiagnostics()
        {
            if (_simTime < _nextLossReport) return;
            _nextLossReport = _simTime + 30f;
            var sb = new System.Text.StringBuilder($"[mach] t={_simTime:0}s sorted={_totalSorted} active={_active.Count} lost={_lost} nudged={_nudges}");
            AppendTally(sb, " | fell off:", _lossByStation);
            AppendTally(sb, " | stuck (nudged):", _stuckByStation);
            Debug.Log(sb.ToString());
        }

        void AppendTally(System.Text.StringBuilder sb, string label, int[] tally)
        {
            sb.Append(label);
            if (tally == null) return;
            for (int i = 0; i < tally.Length && i < stations.Count; i++)
            {
                if (tally[i] > 0) sb.Append($" {stations[i].displayName}={tally[i]}");
            }
        }

        void MakePileBall(MachineBall b)
        {
            _inPile[b.id] = true;
            Rigidbody rb = b.body;
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.interpolation = RigidbodyInterpolation.None;
            MeshRenderer mr = _rendererOf[b.id];
            mr.shadowCastingMode = ShadowCastingMode.Off;
            if (_swapMesh) mr.GetComponent<MeshFilter>().sharedMesh = _lowMesh;
            mr.enabled = false;
            _colliderOf[b.id].enabled = false;
        }

        /// <summary>A kinematic ball never moves, so only the layers near the release front need colliders at all.</summary>
        void EnablePileColliders(int layersAhead)
        {
            int upTo = Mathf.Min(_layerCount, _releaseCursor + layersAhead);
            while (_colliderCursor < upTo)
            {
                for (int id = _layerStart[_colliderCursor]; id < _layerStart[_colliderCursor + 1]; id++)
                {
                    if (_colliderOf[id] != null) _colliderOf[id].enabled = true;
                }

                _colliderCursor++;
            }
        }

        /// <summary>Once the gate is open, frees the pile layer by layer from the bottom up (see <see cref="pileReleaseSpeed"/>).</summary>
        void ReleasePile(float dt)
        {
            if (!simulateAllBalls || _releaseCursor >= _layerCount || _appliedFlood != 1) return;
            if (float.IsNegativeInfinity(_releaseFront)) _releaseFront = _layerY[0];
            _releaseFront += pileReleaseSpeed * dt;
            while (_releaseCursor < _layerCount && _layerY[_releaseCursor] <= _releaseFront)
            {
                for (int id = _layerStart[_releaseCursor]; id < _layerStart[_releaseCursor + 1]; id++)
                {
                    MachineBall b = _ballOf[id];
                    if (b == null || !_inPile[id]) continue;
                    Rigidbody rb = b.body;
                    rb.isKinematic = false;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    rb.linearVelocity = Vector3.zero;
                    b.lastMovingTime = _simTime;
                    _frozen.Remove(b.colour, _frozenSlot[id]);
                    _rendererOf[id].enabled = true;
                }

                _releaseCursor++;
            }

            EnablePileColliders(PileColliderMargin);
        }

        void ReleaseFromPile(MachineBall b)
        {
            _inPile[b.id] = false;
            Rigidbody rb = b.body;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.solverIterations = _config.ballSolverIterations;
            MeshRenderer mr = _rendererOf[b.id];
            mr.shadowCastingMode = castBallShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (_swapMesh) mr.GetComponent<MeshFilter>().sharedMesh = ballMesh;
            b.lastMovingTime = _simTime;
        }

        MachineBall CreateBall(int id, Vector3 position)
        {
            var go = new GameObject("Ball " + id);
            go.transform.SetParent(ballRoot, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * _visualScale;

            go.AddComponent<MeshFilter>().sharedMesh = ballMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ballMaterials[_colourOf[id]];
            if (!castBallShadows) renderer.shadowCastingMode = ShadowCastingMode.Off;

            var col = go.AddComponent<SphereCollider>();
            col.radius = Mathf.Max(0.001f, ballMesh.bounds.extents.x, ballMesh.bounds.extents.y, ballMesh.bounds.extents.z);
            col.sharedMaterial = _ballPhysics;
            _colliderOf[id] = col;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = _config.ballMass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.solverIterations = _config.ballSolverIterations;
            rb.maxLinearVelocity = _config.maxBallSpeed;
            rb.sleepThreshold = ballSleepThreshold;
            rb.maxDepenetrationVelocity = 2f;

            var ball = go.AddComponent<MachineBall>();
            ball.id = id;
            ball.colour = _colourOf[id];
            ball.body = rb;
            ball.tube = -1;
            ball.lastMovingTime = _simTime;
            _ballOf[id] = ball;
            _rendererOf[id] = renderer;
            _created++;
            return ball;
        }

        void CreateCap(float r)
        {
            var go = new GameObject("Silo Stack Cap");
            go.transform.SetParent(ballRoot, false);
            const int sides = 16;
            _capRadius = silo.InnerRadius - 0.01f;
            float radius = _capRadius;
            var verts = new Vector3[sides * 2];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                verts[i] = new Vector3(Mathf.Cos(a) * radius, -0.03f, Mathf.Sin(a) * radius);
                verts[i + sides] = new Vector3(Mathf.Cos(a) * radius, 0.03f, Mathf.Sin(a) * radius);
            }

            // A convex MeshCollider still needs real triangles: fan the bottom and top discs, quad the rim.
            var tris = new int[(sides - 2) * 6 + sides * 6];
            int k = 0;
            for (int i = 1; i < sides - 1; i++)
            {
                tris[k++] = 0; tris[k++] = i; tris[k++] = i + 1;
                tris[k++] = sides; tris[k++] = sides + i + 1; tris[k++] = sides + i;
            }

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                tris[k++] = i; tris[k++] = sides + i; tris[k++] = j;
                tris[k++] = j; tris[k++] = sides + i; tris[k++] = sides + j;
            }

            var mesh = new Mesh { name = "StackCap", vertices = verts, triangles = tris };
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = true;
            col.sharedMaterial = new PhysicsMaterial("MachineCap") { bounciness = 0.1f, dynamicFriction = 0.3f, staticFriction = 0.3f };
            _capBody = go.AddComponent<Rigidbody>();
            _capBody.isKinematic = true;
            _capBody.useGravity = false;
            Bounds sb = silo.StorageBounds;
            _capCentre = sb.center;
            _capRestY = (_layerCount > 0 ? _layerY[_layerCount - 1] : sb.max.y) + r + 0.03f;
            go.transform.position = new Vector3(sb.center.x, _capRestY, sb.center.z);
            go.SetActive(_latticeCursor < _placed);
        }

        void AddActive(MachineBall b)
        {
            _activeIndex[b.id] = _active.Count;
            _active.Add(b);
        }

        void RemoveActive(MachineBall b)
        {
            int idx = _activeIndex[b.id];
            int last = _active.Count - 1;
            MachineBall moved = _active[last];
            _active[idx] = moved;
            _activeIndex[moved.id] = idx;
            _active.RemoveAt(last);
        }

        // ---------- per tick ----------

        void ApplySchedule()
        {
            float feed = 0f;
            bool flood = false;
            MachinePhase target = MachinePhase.Hold;
            if (_simTime >= _config.holdSeconds)
            {
                ReleaseStage[] stages = _config.stages;
                int found = -1;
                for (int i = 0; i < stages.Length; i++)
                {
                    if (stages[i].startSeconds <= _simTime) found = i;
                }

                if (found >= 0)
                {
                    feed = stages[found].ballsPerSecond;
                    flood = stages[found].flood;
                    target = stages[found].phase;
                }
            }

            if (feed != _appliedFeed)
            {
                _appliedFeed = feed;
                _feedTarget = feed;
                _feedChangedAt = _simTime;
                _flowEma = feed;
                _silo.SetFeedRate(feed * _feedTrim);
            }

            int floodInt = flood ? 1 : 0;
            if (floodInt != _appliedFlood)
            {
                _appliedFlood = floodInt;
                _silo.SetFloodGate(flood);
                _feedChangedAt = _simTime;
                if (flood) _recorder.LogEvent("flood_open", _simTime);
            }

            if (_phase < MachinePhase.Finale) SetPhase(target);
        }

        void SetPhase(MachinePhase phase)
        {
            if (phase == _phase) return;
            _phase = phase;
            _recorder.LogEvent("phase_" + phase, _simTime);
            PhaseChanged?.Invoke(phase);
        }

        void ProcessBalls()
        {
            Vector3 hopper = _cannon.HopperDrop;
            float killY = _config.killPlaneY;
            float stall = _config.stallSeconds;
            bool hold = _phase == MachinePhase.Hold;
            bool counting = _winner < 0;
            bool lidOn = _capBody != null && _capBody.gameObject.activeSelf;
            float lidTop = _capRestY + _appliedOffset + 0.03f;
            float outletBottom = _outletY - 0.6f;
            Vector3 axis = silo.transform.position;
            int siloCount = 0;

            for (int a = 0; a < _active.Count;)
            {
                MachineBall b = _active[a];
                if (_inPile[b.id] && ((b.id + _tick) % PileStride) != 0)
                {
                    siloCount++;
                    a++;
                    continue;
                }

                Rigidbody rb = b.body;
                Vector3 p = rb.position;

                if (p.y < killY)
                {
                    Vector3 lostAt = p;
                    Vector3 drop = hopper + new Vector3(_dropRng.NextFloat(-0.4f, 0.4f), _dropRng.NextFloat(0f, 0.3f),
                        _dropRng.NextFloat(-0.4f, 0.4f));
                    rb.position = drop;
                    b.transform.position = drop;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    b.lastMovingTime = _simTime;
                    _outPassed[b.id] = false;
                    _slowSince[b.id] = -1f;
                    _lost++;
                    LogLoss(lostAt);
                    a++;
                    continue;
                }

                int tubeIdx = -1;
                if (p.y <= _tubeMaxY)
                {
                    tubeIdx = _rack.TubeIndexAt(p);
                    if (tubeIdx >= 0 && counting)
                    {
                        b.counted = true;
                        b.tube = tubeIdx;
                        b.lastMovingTime = _simTime;
                        if (tubeIdx == b.colour)
                        {
                            _counts[b.colour]++;
                            _totalSorted++;
                            BallSorted?.Invoke(b.colour, p);
                        }
                        else
                        {
                            _wrongTube++;
                        }

                        RemoveActive(b);
                        _settling.Add(b);
                        continue;
                    }
                }

                bool inSilo = _siloZone.Contains(p);
                if (!inSilo && _inPile[b.id]) ReleaseFromPile(b);
                if (inSilo)
                {
                    siloCount++;
                    _outPassed[b.id] = false;
                }
                else if (p.y < _outletY && p.y > outletBottom && !_outPassed[b.id])
                {
                    // Crossed the plane under the star feeder: counts toward the measured release rate.
                    float dx = p.x - axis.x, dz = p.z - axis.z;
                    if (dx * dx + dz * dz < OutletRadius * OutletRadius)
                    {
                        _outPassed[b.id] = true;
                        _outCount++;
                    }
                }


                if (hold)
                {
                    b.lastMovingTime = _simTime;
                    a++;
                    continue;
                }

                if (((b.id + _tick) % StallStride) == 0)
                {
                    float speedSqr = rb.linearVelocity.sqrMagnitude;
                    if (speedSqr > StallSpeedSqr)
                    {
                        b.lastMovingTime = _simTime;
                    }

                    // A recycled ball that has come to rest on the stack's lid joins the stack (kinematic rider).
                    if (lidOn && p.y >= lidTop && p.y <= lidTop + 1.5f && OnLid(p))
                    {
                        if (speedSqr >= RiderSpeedSqr)
                        {
                            _slowSince[b.id] = -1f;
                        }
                        else if (_slowSince[b.id] < 0f)
                        {
                            _slowSince[b.id] = _simTime;
                        }
                        else if (_simTime - _slowSince[b.id] >= RiderRestSeconds)
                        {
                            MakeRider(b);
                            continue;
                        }
                    }
                    else if (speedSqr <= StallSpeedSqr && _simTime - b.lastMovingTime >= stall)
                    {
                        Vector3 toHopper = p - hopper;
                        if (tubeIdx >= 0 || _storageExpanded.Contains(p) || _breechExpanded.Contains(p) || _trayExpanded.Contains(p) ||
                            toHopper.sqrMagnitude < 1.6f * 1.6f)
                        {
                            b.lastMovingTime = _simTime;
                        }
                        else
                        {
                            Vector3 kick = new Vector3(_nudgeRng.NextFloat(-1f, 1f), 0f, _nudgeRng.NextFloat(-1f, 1f)) * 0.6f;
                            kick.y = 1.6f;
                            rb.AddForce(kick, ForceMode.VelocityChange);
                            b.lastMovingTime = _simTime;
                            _nudges++;
                            CountStuck(p);
                        }
                    }
                }

                a++;
            }

            _siloActive = siloCount;
        }

        bool OnLid(Vector3 p)
        {
            float dx = p.x - _capCentre.x, dz = p.z - _capCentre.z;
            float r = _capRadius - 0.04f;
            return dx * dx + dz * dz < r * r;
        }

        void MakeRider(MachineBall b)
        {
            Rigidbody rb = b.body;
            RemoveActive(b);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.isKinematic = true;
            _riders.Add(b);
            _absorbed++;
        }

        /// <summary>Counted balls that have come to rest in a tube are frozen (collider kept) and drawn from the instanced batch.</summary>
        void SettleCounted()
        {
            int n = _settling.Count;
            if (n == 0) return;
            int budget = n / 10 + 1;
            for (int k = 0; k < budget && _settling.Count > 0; k++)
            {
                if (_settleCursor >= _settling.Count) _settleCursor = 0;
                MachineBall b = _settling[_settleCursor];
                Rigidbody rb = b.body;
                if (rb.linearVelocity.sqrMagnitude >= FreezeSpeedSqr)
                {
                    b.lastMovingTime = _simTime;
                    _settleCursor++;
                }
                else if (_simTime - b.lastMovingTime >= FreezeAfterSeconds)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.interpolation = RigidbodyInterpolation.None;
                    rb.isKinematic = true;
                    _settled.Add(b.colour, -1, Matrix4x4.TRS(rb.position, rb.rotation, Vector3.one * _visualScale));
                    MeshRenderer mr = _rendererOf[b.id];
                    if (mr != null) mr.enabled = false;
                    int last = _settling.Count - 1;
                    _settling[_settleCursor] = _settling[last];
                    _settling.RemoveAt(last);
                }
                else
                {
                    _settleCursor++;
                }
            }
        }

        /// <summary>
        /// Wakes the dormant stack a chunk at a time (bottom of the lattice first, then the riders) whenever the silo pile is
        /// under its target and the machine has room, and steps the frozen cylinder block down as its layers are used up.
        /// </summary>
        void UpdateStack(float dt)
        {
            bool lattice = _latticeCursor < _placed;
            bool riders = _riderCursor < _riders.Count;
            if (!lattice && !riders)
            {
                if (_capBody != null && _capBody.gameObject.activeSelf) _capBody.gameObject.SetActive(false);
                return;
            }

            if (_siloActive < activeSiloTarget && _simTime >= _nextWake && _active.Count + wakeChunk <= maxActiveBalls)
            {
                int woken = lattice ? WakeLattice(wakeChunk) : WakeRiders(wakeChunk);
                _siloActive += woken;
                _nextWake = _simTime + WakeInterval;
                lattice = _latticeCursor < _placed;
                if (!lattice && _riderCursor >= _riders.Count && _capBody != null) _capBody.gameObject.SetActive(false);
            }

            if (!lattice) return;

            // The lattice fills the (opaque) cone first, and those balls never move: they wake where they are and drop onto the
            // pile. Once the wake cursor is in the glass cylinder the frozen block steps down one layer each time a layer is
            // used up, so its bottom layer stays pinned at the cylinder's foot (a rigid block can't enter the narrowing cone).
            int cursorLayer = LayerOf(_latticeCursor);
            float target = cursorLayer > _cylFirstLayer ? -(cursorLayer - _cylFirstLayer) * _layerH : 0f;
            _stackOffset = Mathf.MoveTowards(_stackOffset, target, Mathf.Min(stackLowerSpeed, 0.5f) * dt);
            float delta = _stackOffset - _appliedOffset;
            if (Mathf.Abs(delta) >= 0.005f)
            {
                _appliedOffset = _stackOffset;
                _frozen.ShiftY(delta);
                Vector3 shift = new Vector3(0f, delta, 0f);
                for (int i = _riderCursor; i < _riders.Count; i++)
                {
                    Rigidbody rb = _riders[i].body;
                    rb.position += shift;
                    _riders[i].transform.position = rb.position;
                }

                _capBody.MovePosition(new Vector3(_capCentre.x, _capRestY + _appliedOffset, _capCentre.z));
            }
        }

        int WakeLattice(int count)
        {
            int woken = 0;
            Vector3 shift = new Vector3(0f, _appliedOffset, 0f);
            while (woken < count && _latticeCursor < _placed)
            {
                int id = _latticeCursor++;
                _frozen.Remove(_colourOf[id], _frozenSlot[id]);
                MachineBall b = CreateBall(id, _basePos[id] + shift);
                b.lastMovingTime = _simTime;
                AddActive(b);
                woken++;
            }

            return woken;
        }

        int WakeRiders(int count)
        {
            int woken = 0;
            while (woken < count && _riderCursor < _riders.Count)
            {
                MachineBall b = _riders[_riderCursor++];
                Rigidbody rb = b.body;
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                b.lastMovingTime = _simTime;
                _slowSince[b.id] = -1f;
                AddActive(b);
                woken++;
            }

            if (_riderCursor >= _riders.Count)
            {
                _riders.Clear();
                _riderCursor = 0;
            }

            return woken;
        }

        /// <summary>Layer holding lattice index <paramref name="n"/> (the largest layer starting at or before it).</summary>
        int LayerOf(int n)
        {
            if (_layerCount == 0) return 0;
            n = Mathf.Clamp(n, 0, _placed);
            int lo = 0, hi = _layerCount - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (_layerStart[mid] <= n) lo = mid;
                else hi = mid - 1;
            }

            return lo;
        }

        /// <summary>
        /// Trims the commanded feeder rate so the measured flow under the star feeder matches the schedule. Samples a 1 s
        /// flow into a slow average (a pocket dumps ~20 balls at once, so short windows are lumpy) and nudges the trim
        /// every few seconds; paused during the flood and for a few seconds after any schedule change.
        /// </summary>
        void UpdateFeedTrim()
        {
            if (_simTime >= _nextFlowSample)
            {
                _flowEma += (_outCount / FlowWindowSeconds - _flowEma) * 0.12f;
                _outCount = 0;
                _nextFlowSample += FlowWindowSeconds;
            }

            if (_simTime < _nextTrim) return;
            _nextTrim += TrimIntervalSeconds;
            bool measurable = _phase != MachinePhase.Hold && _appliedFlood == 0 && _feedTarget > 0.5f && _siloActive >= 30 &&
                              _simTime - _feedChangedAt >= FlowSettleSeconds;
            if (!measurable) return;

            float ratio = Mathf.Clamp(_feedTarget / Mathf.Max(0.5f, _flowEma), 0.5f, 2f);
            _feedTrim = Mathf.Clamp(_feedTrim * Mathf.Pow(ratio, 0.35f), 0.5f, 3f);
            _silo.SetFeedRate(_feedTarget * _feedTrim);
        }

        void UpdateContest()
        {
            int best = _leader;
            for (int c = 0; c < _counts.Length; c++)
            {
                if (best < 0 ? _counts[c] > 0 : _counts[c] > _counts[best]) best = c;
            }

            if (best != _leader)
            {
                if (_leader >= 0)
                {
                    _leadChanges++;
                    _recorder.LogEvent("lead_change", best);
                }

                _leader = best;
                LeaderChanged?.Invoke(_leader);
            }

            if (_leader < 0) return;

            int leaderCount = _counts[_leader];
            if (_phase < MachinePhase.Finale &&
                leaderCount >= Mathf.CeilToInt(_config.finaleFraction * _config.tubeCapacity))
            {
                SetPhase(MachinePhase.Finale);
            }

            if (_winner < 0 && leaderCount >= _config.tubeCapacity)
            {
                _winner = _leader;
                _winTime = _simTime;
                _completeAt = _simTime + _config.outroSeconds;
                _recorder.LogEvent("win", _winner);
                _rack.MarkWinner(_winner);
                Won?.Invoke(_winner);
                SetPhase(MachinePhase.Won);
            }
        }

        // ---------- result ----------

        void RefreshResult()
        {
            int top = _winner >= 0 ? _winner : Mathf.Max(0, _leader);
            int topCount = _counts[top];
            int runnerUp = 0;
            for (int c = 0; c < _counts.Length; c++)
            {
                if (c != top && _counts[c] > runnerUp) runnerUp = _counts[c];
            }

            _recorder.SetMetric("winner", _winner);
            _recorder.SetMetric("winner_count", topCount);
            _recorder.SetMetric("runner_up_count", runnerUp);
            _recorder.SetMetric("margin", topCount - runnerUp);
            _recorder.SetMetric("lead_changes", _leadChanges);
            _recorder.SetMetric("total_sorted", _totalSorted);
            _recorder.SetMetric("recycled", cannon != null ? cannon.FiredCount : 0);
            _recorder.SetMetric("lost", _lost);
            _recorder.SetMetric("nudges", _nudges);
            _recorder.SetMetric("wrong_tube", _wrongTube);
            _recorder.SetMetric("win_time", _winTime);
            _recorder.SetMetric("peak_active", _peakActive);
            _recorder.SetMetric("absorbed", _absorbed);
            _recorder.SetMetric("bodies", _created);
            _recorder.SetMetric("feed_trim", _feedTrim);

            // Standings: colour names, most balls first (stable on ties).
            for (int c = 0; c < _order.Length; c++) _order[c] = c;
            for (int a = 1; a < _order.Length; a++)
            {
                int v = _order[a];
                int b = a - 1;
                while (b >= 0 && _counts[_order[b]] < _counts[v])
                {
                    _order[b + 1] = _order[b];
                    b--;
                }

                _order[b + 1] = v;
            }

            _standings.Clear();
            for (int c = 0; c < _order.Length; c++) _standings.Add(_config.colours[_order[c]].name);
            _recorder.SetStandings(_standings);
        }

        // ---------- instanced ball drawing ----------

        /// <summary>80-triangle icosphere, diameter 1, smooth normals. Same size as the editor-built ball mesh.</summary>
        static Mesh BuildLowPolyBall()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            var cache = new Dictionary<long, int>();
            var tris = new List<int>();
            for (int i = 0; i < f.Length; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Midpoint(verts, cache, a, b), bc = Midpoint(verts, cache, b, c), ca = Midpoint(verts, cache, c, a);
                tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            var normals = new Vector3[verts.Count];
            var positions = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                normals[i] = verts[i];
                positions[i] = verts[i] * 0.5f;
            }

            var mesh = new Mesh { name = "Machine_BallLow", vertices = positions, normals = normals };
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static int Midpoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            index = verts.Count - 1;
            cache[key] = index;
            return index;
        }

        /// <summary>Per-colour matrix lists drawn with <c>Graphics.RenderMeshInstanced</c>, 1023 instances per call.</summary>
        sealed class BallBatch
        {
            const int MaxPerCall = 1023;

            readonly Matrix4x4[][] _matrices;
            readonly int[][] _ids;
            readonly int[] _count;
            readonly RenderParams[] _params;
            readonly int[] _slotOf;
            readonly Mesh _mesh;

            public BallBatch(int colours, int[] capacity, Mesh mesh, Material[] materials, int[] slotOf, Bounds bounds, ShadowCastingMode shadows)
            {
                _mesh = mesh;
                _slotOf = slotOf;
                _matrices = new Matrix4x4[colours][];
                _ids = new int[colours][];
                _count = new int[colours];
                _params = new RenderParams[colours];
                for (int c = 0; c < colours; c++)
                {
                    _matrices[c] = new Matrix4x4[Mathf.Max(16, capacity[c])];
                    _ids[c] = new int[_matrices[c].Length];
                    _params[c] = new RenderParams(materials[c])
                    {
                        shadowCastingMode = shadows,
                        receiveShadows = true,
                        worldBounds = bounds
                    };
                }
            }

            /// <summary>Appends an instance and returns its slot.</summary>
            public int Add(int colour, int id, Matrix4x4 matrix)
            {
                int n = _count[colour];
                if (n == _matrices[colour].Length)
                {
                    Array.Resize(ref _matrices[colour], n * 2);
                    Array.Resize(ref _ids[colour], n * 2);
                }

                _matrices[colour][n] = matrix;
                _ids[colour][n] = id;
                _count[colour] = n + 1;
                return n;
            }

            /// <summary>Swap-removes a slot, keeping the moved instance's slot table entry correct.</summary>
            public void Remove(int colour, int slot)
            {
                int last = _count[colour] - 1;
                if (slot != last)
                {
                    _matrices[colour][slot] = _matrices[colour][last];
                    int moved = _ids[colour][last];
                    _ids[colour][slot] = moved;
                    if (_slotOf != null && moved >= 0) _slotOf[moved] = slot;
                }

                _count[colour] = last;
            }

            public void ShiftY(float dy)
            {
                for (int c = 0; c < _count.Length; c++)
                {
                    Matrix4x4[] m = _matrices[c];
                    int n = _count[c];
                    for (int i = 0; i < n; i++) m[i].m13 += dy;
                }
            }

            public void Draw()
            {
                for (int c = 0; c < _count.Length; c++)
                {
                    int n = _count[c];
                    for (int start = 0; start < n; start += MaxPerCall)
                    {
                        Graphics.RenderMeshInstanced(_params[c], _mesh, 0, _matrices[c], Mathf.Min(MaxPerCall, n - start), start);
                    }
                }
            }
        }
    }
}
