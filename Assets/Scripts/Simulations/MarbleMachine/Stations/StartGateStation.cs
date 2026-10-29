using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// The start/finish station of the lap race (<c>mach-002</c>): a gently sloped starting platform with a grid of
    /// <see cref="SlotCount"/> slots, a retractable gate (one plate plus the lane dividers, a single kinematic body that sinks into
    /// the floor), the lap line and the chequered arch. The platform is also the landing pad of the recycle cannon, and the
    /// closing connector of the loop enters it from behind, so a racer's lap ends by rolling over the lap line.
    /// </summary>
    /// <remarks>
    /// Grid geometry is stored in station-local space (built by <c>StartGateBuilder</c>): slot 0 is front-left (row 0 against the
    /// gate, column 0 on the -X side looking down the flow), row-major, so slot = row * columns + column. A slot is the position of
    /// a ball CENTRE resting on the floor. The gate moves from sim time only: <see cref="Release"/> arms it and the next
    /// <see cref="Step"/> stamps the release time, so a seed replays exactly.
    /// </remarks>
    public sealed class StartGateStation : MachineStation, IStartGate
    {
        [Header("Gate (set by StartGateBuilder)")]
        [Tooltip("Kinematic body carrying the gate plate and lane dividers as child colliders. Sinks straight down when released.")]
        public Rigidbody gate;
        [Tooltip("Where the gate body sits while closed, station-local.")]
        public Vector3 gateClosedLocal;
        [Tooltip("How far the gate sinks, metres (more than the plate's height, so it ends under the floor).")]
        [Min(0.1f)] public float gateSinkDistance = 0.8f;
        [Tooltip("Seconds the gate takes to drop.")]
        [Min(0.01f)] public float releaseSeconds = 0.25f;

        [Header("Grid (station-local)")]
        public int columns = 4;
        public int rows = 4;
        public float pitchX = 0.5f;
        public float pitchZ = 0.42f;
        [Tooltip("Ball-centre position of slot 0 (front-left).")]
        public Vector3 slot0Local;
        [Tooltip("Unit vector along the floor toward the front (+Z, tilted down by the platform slope).")]
        public Vector3 floorForwardLocal = Vector3.forward;
        [Tooltip("Where the cannon aims, station-local (just above the platform).")]
        public Vector3 cannonTargetLocal;
        [Tooltip("A point on the lap line (on the floor), station-local.")]
        public Vector3 lapLineLocal;

        [Header("Boost")]
        [Tooltip("Optional launch pad behind the lap line; its speed is taken from the config.")]
        public BoostPad[] pads;

        bool _releaseArmed;
        float _releaseStart = -1f;

        public bool IsReleased => _releaseStart >= 0f || _releaseArmed;
        public int SlotCount => Mathf.Max(0, columns * rows);

        public Vector3 CannonTarget => transform.TransformPoint(cannonTargetLocal);
        public Vector3 LapLinePoint => transform.TransformPoint(lapLineLocal);

        /// <summary>The flow direction across the lap line, horizontal, in world space.</summary>
        public Vector3 LapLineNormal
        {
            get
            {
                Vector3 f = transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        public Vector3 GridSlot(int slot)
        {
            slot = Mathf.Clamp(slot, 0, Mathf.Max(0, SlotCount - 1));
            int row = slot / Mathf.Max(1, columns);
            int col = slot % Mathf.Max(1, columns);
            // Rows step back from the gate (-Z along the floor), columns step to the right (+X) from the left-most.
            Vector3 local = slot0Local + Vector3.right * (col * pitchX) - floorForwardLocal * (row * pitchZ);
            return transform.TransformPoint(local);
        }

        public void Release()
        {
            _releaseArmed = true;
        }

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            _releaseArmed = false;
            _releaseStart = -1f;
            if (pads != null)
            {
                foreach (BoostPad pad in pads) if (pad != null) pad.targetSpeed = config.boosterSurfaceSpeed;
            }

            PoseGate(0f);
        }

        public override void Step(float simTime, float dt)
        {
            if (_releaseArmed && _releaseStart < 0f)
            {
                _releaseStart = simTime;
                RaisePulse(transform.TransformPoint(gateClosedLocal), 1f);
            }

            float k = _releaseStart < 0f ? 0f : Mathf.SmoothStep(0f, 1f, (simTime - _releaseStart) / releaseSeconds);
            PoseGate(k);
        }

        void PoseGate(float k)
        {
            if (gate == null) return;
            Vector3 world = transform.TransformPoint(gateClosedLocal + Vector3.down * (gateSinkDistance * k));
            gate.MovePosition(world);
            gate.MoveRotation(transform.rotation);
        }
    }
}
