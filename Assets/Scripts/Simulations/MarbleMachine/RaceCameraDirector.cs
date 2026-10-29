using System.Collections.Generic;
using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// Directs the camera for the mach-002 lap race. A thin adapter: it reads <see cref="IRaceState"/> every <c>LateUpdate</c>,
    /// hands it to the pure <see cref="RaceCameraCore"/> (a drone rig following an arc-length focus along a smoothed course rail,
    /// see plans/videos/mach-002-camera-rework.md), poses the <see cref="TourCameraRig"/> with the result, and probes the world
    /// for obstructions, which only ever bias the next frame's target (the camera position is never clamped).
    /// </summary>
    /// <remarks>
    /// Reads simulation state only; never writes it. Counterpart tools: <c>RaceCameraHarness</c> (offline smoothness test) and
    /// <see cref="RaceTraceRecorder"/>.
    /// </remarks>
    [DefaultExecutionOrder(-50)] // pose the camera before the number tags read it
    public sealed class RaceCameraDirector : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] MonoBehaviour _simulation; // must implement IRaceState
        [SerializeField] TourCameraRig _rig;
        [SerializeField] Camera _camera;

        [Header("Camera")]
        [Tooltip("Every tunable of the race camera (springs, limits, zone shots). Defaults are tuned by the offline harness.")]
        public RaceCameraSettings settings = new RaceCameraSettings();
        [Tooltip("Layers the obstruction probe treats as solid. Marbles, triggers and glass-named colliders are ignored anyway.")]
        public LayerMask obstructionMask = Physics.DefaultRaycastLayers;
        [Tooltip("Logs max acceleration, angular velocity and pops of the live camera every 10 seconds.")]
        public bool logLiveMetrics;

        IRaceState _state;
        RaceCameraCore _core;
        bool _started;
        bool _blocked;

        // Reused input buffers.
        RaceCameraInput _input;

        // Obstruction probe buffers.
        readonly RaycastHit[] _hits = new RaycastHit[16];
        readonly Collider[] _overlaps = new Collider[16];

        // Live metrics.
        Vector3 _prevPos;
        Vector3 _prevVel;
        Vector3 _prevFwd;
        float _prevAngVel;
        float _prevDt;
        int _metricFrames;
        float _metricWindow;
        float _maxAcc;
        float _maxAng;
        float _maxAngAcc;
        int _pops;

        public void Bind(MonoBehaviour simulation, TourCameraRig rig, Camera camera)
        {
            _simulation = simulation;
            _rig = rig;
            _camera = camera;
        }

        void Start()
        {
            _state = _simulation as IRaceState;
            if (_camera == null && _rig != null)
            {
                _camera = _rig.GetComponent<Camera>();
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_rig != null && _camera != null)
            {
                _rig.Bind(_camera);
                _rig.SetLookTracking(null);
                _rig.SetDrift(0f, 0f);
            }
        }

        void LateUpdate()
        {
            if (_state == null || _rig == null || _camera == null)
            {
                return;
            }

            IReadOnlyList<MachineStation> stations = _state.Stations;
            int count = _state.RacerCount;
            if (stations == null || stations.Count == 0 || count <= 0)
            {
                return;
            }

            if (!_started)
            {
                Begin(stations, count);
            }

            if (_input.pos == null || _input.pos.Length != count)
            {
                _input.count = count;
                _input.pos = new Vector3[count];
                _input.progress = new float[count];
                _input.recycles = new int[count];
                _input.finished = new bool[count];
            }

            _input.phase = _state.Phase;
            _input.simTime = _state.SimTime;
            _input.winner = _state.WinnerIndex;
            _input.laps = _state.Laps;
            for (int i = 0; i < count; i++)
            {
                _input.pos[i] = _state.RacerPosition(i);
                _input.progress[i] = _state.Progress(i);
                _input.recycles[i] = _state.Recycles(i);
                _input.finished[i] = _state.FinishTime(i) >= 0f;
            }

            float dt = Time.deltaTime;
            CameraPose pose = _core.Step(_input, dt, _camera.aspect, _blocked);
            _rig.SnapTo(pose.position, pose.aim);
            _blocked = Probe(pose);

            if (logLiveMetrics && dt > 0f)
            {
                TrackMetrics(pose, dt);
            }
        }

        // Setup -----------------------------------------------------------------------------------------------------

        void Begin(IReadOnlyList<MachineStation> stations, int count)
        {
            _started = true;
            var progress = new RaceProgress(stations);
            var vertices = new Vector3[progress.SegmentCount];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = progress.Vertex(i);
            }

            var track = new CourseTrack(vertices);

            var sources = new ZoneSource[stations.Count];
            for (int i = 0; i < stations.Count; i++)
            {
                MachineStation st = stations[i];
                if (st == null)
                {
                    continue;
                }

                Vector3 f = st.transform.forward;
                f.y = 0f;
                sources[i] = new ZoneSource
                {
                    name = !string.IsNullOrEmpty(st.displayName) ? st.displayName : st.name,
                    forward = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward,
                    focus = st.cameraFocus != null ? st.cameraFocus.position : st.activityBounds.center,
                    s0 = track.VertexDistance(2 * i),
                    s1 = track.VertexDistance(2 * i + 1),
                };
            }

            ZoneDef[] zones = RaceCameraCore.BuildZones(sources, settings, track.Length);

            // Opening shot over the start grid.
            MachineStation gateStation = stations[0];
            var gate = gateStation as IStartGate;
            int slotCount = gate != null ? Mathf.Max(1, gate.SlotCount) : 0;
            var slots = new Vector3[Mathf.Max(1, slotCount)];
            if (gate != null)
            {
                for (int i = 0; i < slotCount; i++)
                {
                    slots[i] = gate.GridSlot(i);
                }
            }
            else
            {
                slots[0] = _state.RacerPosition(0);
            }

            Vector3 gateForward = gateStation != null ? gateStation.transform.forward : Vector3.forward;
            Vector3 offset = gateStation != null ? gateStation.cameraOffset : Vector3.zero;
            bool hasFocus = gateStation != null && gateStation.cameraFocus != null;
            float fov = _camera.fieldOfView;
            RaceCameraCore.ComputeOpening(slots, gateForward, offset, hasFocus, fov, _camera.aspect, settings.openingMargin,
                out Vector3 centre, out Vector3 dir, out float distance);

            _core = new RaceCameraCore(track, settings, zones, centre, dir, distance, fov);

            var sb = new System.Text.StringBuilder();
            sb.Append("[RaceCam] L=").Append(track.Length.ToString("0.0")).Append(" interiorRight=").Append(track.InteriorOnRight)
              .Append(" fov=").Append(fov.ToString("0.#")).Append(" opening d=").Append(distance.ToString("0.0")).Append(" zones:");
            for (int i = 0; i < zones.Length; i++)
            {
                sb.Append(' ').Append(zones[i].name).Append(' ').Append(zones[i].lo.ToString("0.0")).Append("..").Append(zones[i].hi.ToString("0.0")).Append(';');
            }

            Debug.Log(sb.ToString(), this);
            if (Mathf.Abs(fov - 38f) > 0.5f)
            {
                Debug.LogWarning("[RaceCam] The camera FOV is " + fov + ", the camera was tuned for 38 degrees vertical; distances are fitted to the actual FOV.", this);
            }
        }

        // Obstruction -----------------------------------------------------------------------------------------------

        /// <summary>
        /// True when something solid (not a marble, trigger or glass) sits between the look point and the camera, or at the camera.
        /// The core turns this into a slow rise in pitch and a slight pull-in; the pose itself is never clamped.
        /// </summary>
        bool Probe(CameraPose pose)
        {
            Vector3 toCam = pose.position - pose.look;
            float dist = toCam.magnitude;
            if (dist < 2.5f)
            {
                return false;
            }

            Vector3 dir = toCam / dist;
            int n = Physics.SphereCastNonAlloc(pose.look + dir * 1.5f, 0.35f, dir, _hits, dist - 2f, obstructionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (IsObstacle(_hits[i].collider))
                {
                    return true;
                }
            }

            int m = Physics.OverlapSphereNonAlloc(pose.position, 0.5f, _overlaps, obstructionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < m; i++)
            {
                if (IsObstacle(_overlaps[i]))
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsObstacle(Collider c)
        {
            if (c == null || c.isTrigger)
            {
                return false;
            }

            Rigidbody rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                return false; // a marble
            }

            return c.gameObject.name.IndexOf("glass", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        // Live metrics ----------------------------------------------------------------------------------------------

        void TrackMetrics(CameraPose pose, float dt)
        {
            Vector3 fwd = pose.rotation * Vector3.forward;
            if (_metricFrames >= 1)
            {
                Vector3 vel = (pose.position - _prevPos) / dt;
                float ang = Mathf.Atan2(Vector3.Cross(fwd, _prevFwd).magnitude, Vector3.Dot(fwd, _prevFwd)) * Mathf.Rad2Deg / dt;
                if (_metricFrames >= 2)
                {
                    float mean = 0.5f * (dt + _prevDt);
                    float acc = (vel - _prevVel).magnitude / mean;
                    float angAcc = Mathf.Abs(ang - _prevAngVel) / mean;
                    _maxAcc = Mathf.Max(_maxAcc, acc);
                    _maxAngAcc = Mathf.Max(_maxAngAcc, angAcc);
                    if (acc > 20f || angAcc > 200f || (pose.position - _prevPos - _prevVel * dt).magnitude > 0.6f)
                    {
                        _pops++;
                    }
                }

                _maxAng = Mathf.Max(_maxAng, ang);
                _prevVel = vel;
                _prevAngVel = ang;
            }

            _prevPos = pose.position;
            _prevFwd = fwd;
            _prevDt = dt;
            _metricFrames++;
            _metricWindow += dt;
            if (_metricWindow >= 10f)
            {
                RaceCameraDebug dbg = _core.Debug;
                Debug.Log("[RaceCam] 10s: max accel " + _maxAcc.ToString("0.0") + " m/s2, max angular " + _maxAng.ToString("0.0") + " deg/s, max angular accel "
                          + _maxAngAcc.ToString("0") + " deg/s2, pops " + _pops + ", mode " + dbg.mode + ", s " + dbg.s.ToString("0.0") + ", d " + dbg.distance.ToString("0.0")
                          + ", occ " + dbg.occ.ToString("0.00"), this);
                _metricWindow = 0f;
                _maxAcc = 0f;
                _maxAng = 0f;
                _maxAngAcc = 0f;
                _pops = 0;
            }
        }
    }
}
