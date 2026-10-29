using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// The marble machine as a lap race (<c>mach-002</c>): <see cref="MachineRaceConfig.racerCount"/> numbered racers start on
    /// the start gate's grid, loop the stations <see cref="MachineRaceConfig.laps"/> times, first across the lap line wins.
    /// Presentation reads it through <see cref="IRaceState"/>. Sixteen real rigidbodies, nothing instanced.
    /// </summary>
    /// <remarks>
    /// <b>Wiring (called by the scene builder, at edit time; the values are serialized fields so they persist in the scene):</b>
    /// <code>
    /// public void Wire(IList&lt;MachineStation&gt; stationsInFlowOrder, MachineStation startGate, MachineStation cannon,
    ///                  Transform ballRoot = null, Mesh ballMesh = null, Material[] racerMaterials = null)
    /// </code>
    /// <c>stationsInFlowOrder</c> includes the start gate FIRST and ends with the last station before the lap line (the screw lift);
    /// the cannon is NOT in the list. <c>startGate</c> must also implement <see cref="IStartGate"/>, <c>cannon</c> must implement
    /// <see cref="ICannon"/> (<c>CannonStation</c>). <c>ballMesh</c> null = Unity's built-in sphere. <c>racerMaterials</c> null (or
    /// shorter than racerCount) = a glossy URP Lit material per racer is created at runtime from the config colour (Smoothness 0.92);
    /// pass the builder's own materials to keep them as assets. Racer GameObjects are named "Racer N" and parented under ballRoot.
    /// <para>
    /// Assumptions: the start gate's entry/exit transforms span the grid-to-platform run, the lap line sits on the loop closure,
    /// <see cref="IStartGate.GridSlot"/> positions are ball centres, and <see cref="IStartGate.Release"/> removes whatever
    /// physically holds the racers (the sim itself holds them kinematic until then, then makes them dynamic).
    /// </para>
    /// </remarks>
    public sealed class MachineRaceSimulation : MonoBehaviour, ISimulation, IRaceState
    {
        [Header("Wiring (set by the scene builder via Wire)")]
        public List<MachineStation> stations = new List<MachineStation>();
        public MachineStation startGateStation;
        public MachineStation cannonStation;
        public Transform ballRoot;
        public Mesh ballMesh;
        [Tooltip("One per racer. Empty = created at runtime (glossy URP Lit from the config colours).")]
        public Material[] racerMaterials;

        [Header("Rendering")]
        public bool castBallShadows = true;

        const float StallSpeedSqr = 0.02f * 0.02f;
        const int StallStride = 10;
        const float FarDistance = 2.5f;
        /// <summary>A racer this far from the course for <see cref="OffCourseSeconds"/> has left the track and goes back via the cannon.</summary>
        const float OffCourseDistance = 6f;
        const float OffCourseSeconds = 2f;
        /// <summary>A cannon shot flies ~3 s far above the course. Off-course recovery is blind to a racer for this long after a shot, or it would recall every shot mid-air.</summary>
        const float CannonFlightGraceSeconds = 6f;
        /// <summary>Only a racer moving slower than this can count as off the course (a fast one is in flight or falling).</summary>
        const float OffCourseMaxSpeed = 2f;
        const float RelocateAfterSeconds = 2f;
        const float RelocateAccept = 4f;
        const float LapLineRadius = 10f;
        const float RankEpsilon = 0.002f;
        const float LeadArmProgress = 0.03f;
        const float MaxFraction = 0.999f;

        MachineRaceConfig _config;
        SeededRandom _rng;
        SeededRandom _nudgeRng;
        SeededRandom _dropRng;
        SeededRandom _cannonRng;
        RunRecorder _recorder;
        IStartGate _gate;
        ICannon _cannon;
        CannonStation _cannonStation;
        RaceProgress _course;
        PhysicsMaterial _ballPhysics;
        Material[] _runtimeMaterials;
        Mesh _mesh;

        int _n;
        MachineBall[] _balls;
        Color[] _colours;
        readonly List<MachineBall> _ballList = new List<MachineBall>();

        // Per racer.
        int[] _lapsDone;
        float[] _frac;
        float[] _progress;
        int[] _seg;
        int[] _stationIdx;
        bool[] _cleared;
        float[] _prevSide;
        float[] _farTime;
        float[] _lastDist;
        int[] _lastRecycles;
        float[] _recycleTime;
        float[] _finishTime;
        int[] _place;
        int[] _rank;     // racer at rank i (0-based)
        int[] _rankOf;

        bool _initialized;
        bool _released;
        float _simTime;
        RacePhase _phase = RacePhase.Grid;
        int _leader = -1;
        int _leadChanges;
        bool _leadArmed;
        int _winner = -1;
        int _finished;
        float _completeAt;
        int _lost;
        int _nudges;
        float _winTime = -1f;

        public string FormatSlug => "mach";
        public bool IsComplete { get; private set; }

        public RunResult Result
        {
            get
            {
                if (_recorder == null) return null;
                if (!IsComplete && _initialized) RefreshResult();
                return _recorder.Result;
            }
        }

        // ---------- wiring ----------

        /// <summary>See the class remarks for the contract.</summary>
        public void Wire(IList<MachineStation> stationsInFlowOrder, MachineStation startGate, MachineStation cannon,
            Transform ballRoot = null, Mesh ballMesh = null, Material[] racerMaterials = null)
        {
            stations = new List<MachineStation>(stationsInFlowOrder);
            startGateStation = startGate;
            cannonStation = cannon;
            this.ballRoot = ballRoot;
            this.ballMesh = ballMesh;
            this.racerMaterials = racerMaterials;
        }

        // ---------- IRaceState ----------

        public float SimTime => _simTime;
        public RacePhase Phase => _phase;
        public int RacerCount => _n;
        public int Laps => _config != null ? _config.laps : 0;
        public int RacerNumber(int racer) => racer + 1;
        public string RacerName(int racer) => _config.colours[racer].name;
        public Color RacerColour(int racer) => _colours[racer];
        public Vector3 RacerPosition(int racer) => _balls[racer].transform.position;
        public float Progress(int racer) => _progress[racer];
        public int LapOf(int racer) => IsFinished(racer) ? _config.laps : Mathf.Min(_config.laps, _lapsDone[racer] + 1);
        public int RankOf(int racer) => _rankOf[racer] + 1;
        public int RacerAtRank(int rank) => _rank[Mathf.Clamp(rank, 1, _n) - 1];
        public string StationNameOf(int racer) => _course != null ? _course.StationName(_stationIdx[racer]) : "";
        public int LeaderIndex => _leader;
        public int LeadChanges => _leadChanges;
        public int WinnerIndex => _winner;
        public int FinishedCount => _finished;
        public float FinishTime(int racer) => _finishTime[racer];
        public int Recycles(int racer) => _balls[racer].recycles;
        public IReadOnlyList<MachineStation> Stations => stations;

        /// <summary>The racers, list index = racer index.</summary>
        public IReadOnlyList<MachineBall> Balls => _ballList;

        public event Action<RacePhase> PhaseChanged;
        public event Action<int> LeaderChanged;
        public event Action<int, int> LapCompleted;
        public event Action<int, int> RacerFinished;
        public event Action<int> Won;
        public event Action<int, Vector3> Recycled;

        bool IsFinished(int racer) => _place[racer] > 0;

        // ---------- ISimulation ----------

        public void Initialize(int seed, SimulationConfig config)
        {
            _config = config as MachineRaceConfig;
            _rng = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _rng);

            if (_config == null)
            {
                Debug.LogError("[MachineRaceSimulation] Config is not a MachineRaceConfig.", this);
                Fail();
                return;
            }

            if (!_config.Validate(out string error))
            {
                Debug.LogError($"[MachineRaceSimulation] Invalid config: {error}", this);
                Fail();
                return;
            }

            _gate = startGateStation as IStartGate;
            _cannon = cannonStation as ICannon;
            _cannonStation = cannonStation as CannonStation;
            if (_gate == null || _cannon == null || stations == null || stations.Count < 2)
            {
                Debug.LogError("[MachineRaceSimulation] Call Wire(): need >= 2 stations, a start gate (IStartGate) and a cannon (ICannon).", this);
                Fail();
                return;
            }

            if (_gate.SlotCount < _config.racerCount)
            {
                Debug.LogError($"[MachineRaceSimulation] Start gate has {_gate.SlotCount} slots for {_config.racerCount} racers.", this);
                Fail();
                return;
            }

            _nudgeRng = _rng.Branch(901);
            _cannonRng = _rng.Branch(902);
            _dropRng = _rng.Branch(903);
            SeededRandom gridRng = _rng.Branch(904);

            _n = _config.racerCount;
            _simTime = 0f;
            _phase = RacePhase.Grid;
            _released = false;
            _leader = -1;
            _leadChanges = 0;
            _leadArmed = false;
            _winner = -1;
            _winTime = -1f;
            _finished = 0;
            _lost = _nudges = 0;

            _course = new RaceProgress(stations);
            CreateRacers(gridRng);

            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i] != null) stations[i].Initialize(_config, _rng.Branch(i + 1));
            }

            // The cannon sits outside the flow-order list, so it is initialised here: without this its config stays null and
            // the first recycled racer makes StepFire throw, which stops the whole run (the screw lift freezes with it).
            if (_cannonStation != null) _cannonStation.Initialize(_config, _rng.Branch(stations.Count + 1));

            UpdateRanking();
            _initialized = true;
            _recorder.SetMetric("racers", _n);
            _recorder.LogEvent("field_set", _n);
        }

        public void Tick(float fixedDelta)
        {
            if (!_initialized || IsComplete) return;

            _recorder.AdvanceTick();
            _simTime += fixedDelta;

            UpdatePhaseClock();
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i] != null) stations[i].Step(_simTime, fixedDelta);
            }

            _cannon.StepFire(_simTime, _ballList, _gate.CannonTarget, _cannonRng);
            if (_cannonStation != null) _cannonStation.Step(_simTime, fixedDelta);

            ProcessRacers(fixedDelta);
            UpdateRanking();
            UpdateLeader();

            if (_winner >= 0 && _simTime >= _completeAt)
            {
                RefreshResult();
                IsComplete = true;
                _recorder.Finish(CompletionReason.Finished);
            }
        }

        void OnDestroy()
        {
            if (_runtimeMaterials == null) return;
            for (int i = 0; i < _runtimeMaterials.Length; i++)
            {
                if (_runtimeMaterials[i] != null) Destroy(_runtimeMaterials[i]);
            }
        }

        // ---------- setup ----------

        void Fail()
        {
            IsComplete = true;
            _recorder.Finish(CompletionReason.Failed);
        }

        void CreateRacers(SeededRandom gridRng)
        {
            float r = _config.ballRadius;
            if (ballRoot == null)
            {
                ballRoot = new GameObject("Racers").transform;
                ballRoot.SetParent(transform, false);
            }

            for (int c = ballRoot.childCount - 1; c >= 0; c--) Destroy(ballRoot.GetChild(c).gameObject);

            _mesh = ballMesh;
            if (_mesh == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _mesh = temp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(temp);
            }

            Vector3 ext = _mesh.bounds.extents;
            float meshRadius = Mathf.Max(0.001f, ext.x, ext.y, ext.z);
            float visualScale = r / meshRadius;
            _ballPhysics = new PhysicsMaterial("RacerBall")
            {
                bounciness = _config.ballBounciness,
                dynamicFriction = _config.ballFriction,
                staticFriction = _config.ballFriction,
                bounceCombine = PhysicsMaterialCombine.Average,
                frictionCombine = PhysicsMaterialCombine.Average
            };

            Material[] mats = racerMaterials;
            if (mats == null || mats.Length < _n)
            {
                mats = BuildMaterials();
                _runtimeMaterials = mats;
            }

            // Seeded grid shuffle: slots[i] is the grid slot racer i starts in.
            var slots = new int[_n];
            for (int i = 0; i < _n; i++) slots[i] = i;
            gridRng.Shuffle(slots);

            _balls = new MachineBall[_n];
            _colours = new Color[_n];
            _ballList.Clear();
            _lapsDone = new int[_n];
            _frac = new float[_n];
            _progress = new float[_n];
            _seg = new int[_n];
            _stationIdx = new int[_n];
            _cleared = new bool[_n];
            _prevSide = new float[_n];
            _farTime = new float[_n];
            _lastDist = new float[_n];
            _lastRecycles = new int[_n];
            _recycleTime = new float[_n];
            _finishTime = new float[_n];
            _place = new int[_n];
            _rank = new int[_n];
            _rankOf = new int[_n];

            for (int i = 0; i < _n; i++)
            {
                _colours[i] = _config.colours[i].color;
                _finishTime[i] = -1f;
                _prevSide[i] = float.NaN;
                _rank[i] = i;
                _rankOf[i] = i;

                Vector3 pos = _gate.GridSlot(slots[i]);
                var go = new GameObject("Racer " + (i + 1));
                go.transform.SetParent(ballRoot, false);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * visualScale;

                go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mats[i];
                mr.shadowCastingMode = castBallShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

                var col = go.AddComponent<SphereCollider>();
                col.radius = meshRadius;
                col.sharedMaterial = _ballPhysics;

                var rb = go.AddComponent<Rigidbody>();
                rb.mass = _config.ballMass;
                rb.isKinematic = true; // held on the grid until the gate drops
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.solverIterations = _config.ballSolverIterations;
                rb.maxLinearVelocity = _config.maxBallSpeed;
                rb.maxDepenetrationVelocity = 2f;

                var ball = go.AddComponent<MachineBall>();
                ball.id = i;
                ball.colour = i;
                ball.body = rb;
                ball.tube = -1;
                ball.lastMovingTime = 0f;
                _balls[i] = ball;
                _ballList.Add(ball);

                // Starting segment and progress from the grid position.
                int s = _course.Locate(pos, 0, true, 3, out _, out float t, out float f);
                _seg[i] = s;
                _stationIdx[i] = _course.StationOfSegment(s, t);
                _frac[i] = Mathf.Min(f, MaxFraction);
                _progress[i] = _frac[i];
            }
        }

        Material[] BuildMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mats = new Material[_n];
            for (int i = 0; i < _n; i++)
            {
                var m = new Material(shader) { name = "Racer_" + (i + 1) };
                Color c = _config.colours[i].color;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.92f);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.92f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                mats[i] = m;
            }

            return mats;
        }

        // ---------- phases ----------

        void SetPhase(RacePhase phase)
        {
            if (phase == _phase) return;
            _phase = phase;
            _recorder.LogEvent("phase_" + phase, _simTime);
            PhaseChanged?.Invoke(phase);
        }

        void UpdatePhaseClock()
        {
            if (_phase == RacePhase.Grid && _simTime >= _config.holdSeconds) SetPhase(RacePhase.Countdown);
            if (_phase == RacePhase.Countdown && _simTime >= _config.holdSeconds + _config.countdownSeconds)
            {
                ReleaseRacers();
                SetPhase(RacePhase.Racing);
                if (_config.laps == 1 && _config.finaleOnLastLap) SetPhase(RacePhase.FinalLap);
            }
        }

        void ReleaseRacers()
        {
            if (_released) return;
            _released = true;
            _gate.Release();
            for (int i = 0; i < _n; i++)
            {
                Rigidbody rb = _balls[i].body;
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.WakeUp();
                _balls[i].lastMovingTime = _simTime;
            }
        }

        // ---------- per tick ----------

        void ProcessRacers(float dt)
        {
            if (!_released) return;

            Vector3 hopper = _cannon.HopperDrop;
            Bounds breech = default;
            bool haveBreech = false;
            if (_cannonStation != null)
            {
                breech = _cannonStation.Bounds;
                breech.Expand(0.6f);
                haveBreech = true;
            }

            float killY = _config.killPlaneY;
            Vector3 linePoint = _gate.LapLinePoint;
            Vector3 lineNormal = _gate.LapLineNormal;
            int tick = _recorder.CurrentTick;

            for (int i = 0; i < _n; i++)
            {
                MachineBall b = _balls[i];
                Rigidbody rb = b.body;
                Vector3 p = rb.position;

                // A fresh pass through the cannon sends the racer back to the start of the lap.
                if (b.recycles != _lastRecycles[i])
                {
                    _lastRecycles[i] = b.recycles;
                    _recycleTime[i] = _simTime;
                    _frac[i] = 0f;
                    _seg[i] = 0;
                    _cleared[i] = false;
                    _farTime[i] = 0f;
                    _prevSide[i] = float.NaN;
                    if (!IsFinished(i)) _progress[i] = _lapsDone[i];
                    _recorder.LogEvent("recycle", i);
                    Recycled?.Invoke(i, p);
                }

                if (p.y < killY)
                {
                    SendToHopper(i);
                    continue;
                }

                if (((b.id + tick) % StallStride) == 0)
                {
                    float speedSqr = rb.linearVelocity.sqrMagnitude;
                    if (speedSqr > StallSpeedSqr)
                    {
                        b.lastMovingTime = _simTime;
                    }
                    else if (_simTime - b.lastMovingTime >= _config.stallSeconds)
                    {
                        bool atCannon = (haveBreech && breech.Contains(p)) || (p - hopper).sqrMagnitude < 1.6f * 1.6f;
                        if (!atCannon && _lastDist[i] > FarDistance && _simTime - _recycleTime[i] >= CannonFlightGraceSeconds)
                        {
                            // Stopped well away from the track (it came off, or landed on the floor): back through the cannon.
                            SendToHopper(i);
                            continue;
                        }

                        if (!atCannon)
                        {
                            Vector3 kick = new Vector3(_nudgeRng.NextFloat(-1f, 1f), 0f, _nudgeRng.NextFloat(-1f, 1f)) * 0.6f;
                            kick.y = 1.6f;
                            rb.AddForce(kick, ForceMode.VelocityChange);
                            _nudges++;
                            _recorder.LogEvent("nudge", i);
                        }

                        b.lastMovingTime = _simTime;
                    }
                }

                UpdateProgress(i, p, dt);
                CheckLapLine(i, p, linePoint, lineNormal);
            }
        }

        /// <summary>Puts a racer that left the track (or fell below the kill plane) into the cannon hopper; the cannon fires it back to the start.</summary>
        void SendToHopper(int i)
        {
            MachineBall b = _balls[i];
            Rigidbody rb = b.body;
            Vector3 drop = _cannon.HopperDrop + new Vector3(_dropRng.NextFloat(-0.4f, 0.4f), _dropRng.NextFloat(0f, 0.3f),
                _dropRng.NextFloat(-0.4f, 0.4f));
            rb.position = drop;
            b.transform.position = drop;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            b.lastMovingTime = _simTime;
            _farTime[i] = 0f;
            _lastDist[i] = 0f;
            _lost++;
            _recorder.LogEvent("lost", i);
            _prevSide[i] = float.NaN;
        }

        void UpdateProgress(int i, Vector3 p, float dt)
        {
            if (IsFinished(i)) return;

            int s = _course.Locate(p, _seg[i], true, 3, out float d, out float t, out float f);
            _lastDist[i] = d;
            if (d > FarDistance)
            {
                _farTime[i] += dt;
                bool inFlight = _simTime - _recycleTime[i] < CannonFlightGraceSeconds || _balls[i].body.linearVelocity.sqrMagnitude > OffCourseMaxSpeed * OffCourseMaxSpeed;
                if (inFlight)
                {
                    _farTime[i] = 0f;
                    return;
                }

                if (d > OffCourseDistance && _farTime[i] >= OffCourseSeconds)
                {
                    SendToHopper(i);
                    return;
                }

                if (_farTime[i] < RelocateAfterSeconds) return;
                s = _course.Locate(p, 0, false, 0, out d, out t, out f);
                if (d > RelocateAccept || (s == _course.SegmentCount - 1 && !_cleared[i])) return;
            }

            _farTime[i] = 0f;
            _seg[i] = s;
            _stationIdx[i] = _course.StationOfSegment(s, t);
            if (s >= _course.LastStationSegment) _cleared[i] = true;
            // Never decreases within a lap; only a trip through the cannon resets it (see ProcessRacers).
            float clamped = Mathf.Min(f, MaxFraction);
            if (clamped > _frac[i]) _frac[i] = clamped;
            _progress[i] = _lapsDone[i] + _frac[i];
        }

        void CheckLapLine(int i, Vector3 p, Vector3 point, Vector3 normal)
        {
            if (IsFinished(i)) return;

            float side = Vector3.Dot(p - point, normal);
            float prev = _prevSide[i];
            _prevSide[i] = side;
            if (float.IsNaN(prev)) return;
            if (!(prev < 0f && side >= 0f)) return;
            if (!_cleared[i]) return;
            if ((p - point).sqrMagnitude > LapLineRadius * LapLineRadius) return;

            _lapsDone[i]++;
            _frac[i] = 0f;
            _seg[i] = 0;
            _cleared[i] = false;
            _farTime[i] = 0f;
            _recorder.LogEvent("lap", i * 100 + _lapsDone[i]);

            if (_lapsDone[i] >= _config.laps)
            {
                _place[i] = ++_finished;
                _finishTime[i] = _simTime;
                _progress[i] = _config.laps;
                LapCompleted?.Invoke(i, _lapsDone[i]);
                RacerFinished?.Invoke(i, _place[i]);
                _recorder.LogEvent("finish", i * 100 + _place[i]);
                if (_winner < 0)
                {
                    _winner = i;
                    _winTime = _simTime;
                    _completeAt = _simTime + _config.outroSeconds;
                    _recorder.LogEvent("win", i);
                    UpdateRanking();
                    Won?.Invoke(i);
                    SetPhase(RacePhase.Finished);
                }
            }
            else
            {
                _progress[i] = _lapsDone[i];
                LapCompleted?.Invoke(i, _lapsDone[i]);
                if (_config.finaleOnLastLap && _phase == RacePhase.Racing && _lapsDone[i] >= _config.laps - 1)
                {
                    SetPhase(RacePhase.FinalLap);
                }
            }
        }

        // ---------- ranking ----------

        /// <summary>
        /// Finished racers first (finishing order), then progress descending. Adjacent bubble swaps only, and a racer must lead by
        /// <see cref="RankEpsilon"/> of a lap to overtake, so near-ties do not flicker. Ties keep the lower index first.
        /// </summary>
        void UpdateRanking()
        {
            for (int pass = 0; pass < _n; pass++)
            {
                bool swapped = false;
                for (int k = _n - 1; k > 0; k--)
                {
                    int a = _rank[k - 1];
                    int b = _rank[k];
                    if (!Overtakes(b, a)) continue;
                    _rank[k - 1] = b;
                    _rank[k] = a;
                    swapped = true;
                }

                if (!swapped) break;
            }

            for (int k = 0; k < _n; k++) _rankOf[_rank[k]] = k;
        }

        /// <summary>True when <paramref name="b"/> (currently behind <paramref name="a"/>) should move ahead of it.</summary>
        bool Overtakes(int b, int a)
        {
            bool fa = IsFinished(a), fb = IsFinished(b);
            if (fa != fb) return fb;
            if (fa) return _place[b] < _place[a];
            return _progress[b] > _progress[a] + RankEpsilon;
        }

        void UpdateLeader()
        {
            int top = _rank[0];
            if (!_leadArmed)
            {
                _leader = top;
                if (_phase >= RacePhase.Racing && _progress[top] >= LeadArmProgress) _leadArmed = true;
                return;
            }

            if (top == _leader) return;
            _leader = top;
            _leadChanges++;
            _recorder.LogEvent("lead_change", top);
            LeaderChanged?.Invoke(top);
        }

        // ---------- result ----------

        void RefreshResult()
        {
            int recycles = 0;
            for (int i = 0; i < _n; i++) recycles += _balls[i].recycles;

            _recorder.SetMetric("winner", _winner >= 0 ? _winner + 1 : -1);
            _recorder.SetMetric("win_time", _winTime);
            _recorder.SetMetric("finished", _finished);
            _recorder.SetMetric("lead_changes", _leadChanges);
            _recorder.SetMetric("laps", _config.laps);
            _recorder.SetMetric("recycles", recycles);
            _recorder.SetMetric("lost", _lost);
            _recorder.SetMetric("nudges", _nudges);
            if (_winner >= 0)
            {
                // Top three finish times: a cheap determinism tripwire next to the standings.
                for (int k = 1; k <= 3 && k <= _n; k++)
                {
                    for (int i = 0; i < _n; i++)
                    {
                        if (_place[i] == k) _recorder.SetMetric("finish_time_" + k, _finishTime[i]);
                    }
                }
            }

            var standings = new List<string>(_n);
            for (int k = 0; k < _n; k++) standings.Add(_config.colours[_rank[k]].name);
            _recorder.SetStandings(standings);
        }
    }
}
