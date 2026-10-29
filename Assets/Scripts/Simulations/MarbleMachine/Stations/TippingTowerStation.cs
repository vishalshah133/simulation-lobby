using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// A stack of trough tippers (three tiers). Each rests tilted, tips when enough balls are inside, holds, then returns.
    /// The tip is triggered by a deterministic overlap count, not a hinge joint.
    /// </summary>
    public sealed class TippingTowerStation : MachineStation
    {
        // Tip speed is a trade: the trough's back end rises at 0.75 m x angular speed, and 0.35 s launched balls ~6 m/s straight
        // up, while 1.1 s throttled the flow too much. 0.7 s over an 85 deg tip peaks at ~2.5 m/s: balls ride the floor.
        const float TipSeconds = 1.2f;
        const float HoldSeconds = 0.3f;
        const float ReturnSeconds = 0.9f;

        [Tooltip("One kinematic trough per tier, top to bottom. The body sits at the pivot (the pour lip).")]
        public Rigidbody[] bodies;
        [Tooltip("Pivot of each trough in station-local space.")]
        public Vector3[] pivotLocal;
        [Tooltip("Local yaw of each trough, 0 pours toward +X, 180 toward -X.")]
        public float[] yawDegrees;
        [Tooltip("Resting tilt (catching) and tipped angle, degrees about the trough's local Z.")]
        public float restDegrees = 8f;
        public float tipDegrees = -100f;
        [Tooltip("Catch volume in trough space.")]
        public Vector3 catchCentre = new Vector3(-0.4f, 0.2f, 0f);
        public Vector3 catchHalfExtents = new Vector3(0.38f, 0.2f, 0.33f);

        [Header("Small-pack mode")]
        [Tooltip("Applies only when the configured threshold is at most this: a trough holding at least one ball for maxWaitSeconds tips anyway, " +
                 "so a lone racer (or the last one) is never parked forever waiting for company. Above it the tower behaves exactly as in mach-001.")]
        [Min(1)] public int patientTipMaxThreshold = 4;
        [Min(0.1f)] public float maxWaitSeconds = 1.5f;

        int _threshold = 25;
        bool _patient;
        float[] _occupiedSince;   // sim time the trough first held a ball since it last returned, or -1
        int[] _state;          // 0 resting, 1 tipping, 2 holding, 3 returning
        float[] _stateStart;
        readonly Collider[] _buffer = new Collider[512];

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            _threshold = Mathf.Max(1, config.tipperThreshold);
            _patient = _threshold <= patientTipMaxThreshold;
            int n = bodies != null ? bodies.Length : 0;
            _state = new int[n];
            _stateStart = new float[n];
            _occupiedSince = new float[n];
            for (int i = 0; i < n; i++) _occupiedSince[i] = -1f;
        }

        public override void Step(float simTime, float dt)
        {
            if (bodies == null || _state == null) return;
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == null) continue;
                float el = simTime - _stateStart[i];
                float angle = restDegrees;
                switch (_state[i])
                {
                    case 0:
                        int inside = Count(i);
                        bool patientTip = false;
                        if (_patient)
                        {
                            if (inside <= 0) _occupiedSince[i] = -1f;
                            else if (_occupiedSince[i] < 0f) _occupiedSince[i] = simTime;
                            else patientTip = simTime - _occupiedSince[i] >= maxWaitSeconds;
                        }

                        if (inside >= _threshold || patientTip)
                        {
                            RaisePulse(transform.TransformPoint(pivotLocal[i]), Mathf.Max(inside, 1) / (_threshold * 2f));
                            _occupiedSince[i] = -1f;
                            SetState(i, 1, simTime);
                            el = 0f;
                            goto case 1;
                        }

                        break;
                    case 1:
                        {
                            float k = Mathf.SmoothStep(0f, 1f, el / TipSeconds);
                            angle = Mathf.Lerp(restDegrees, tipDegrees, k);
                            if (el >= TipSeconds) SetState(i, 2, simTime);
                            break;
                        }
                    case 2:
                        angle = tipDegrees;
                        if (el >= HoldSeconds) SetState(i, 3, simTime);
                        break;
                    default:
                        {
                            float k = Mathf.SmoothStep(0f, 1f, el / ReturnSeconds);
                            angle = Mathf.Lerp(tipDegrees, restDegrees, k);
                            if (el >= ReturnSeconds) SetState(i, 0, simTime);
                            break;
                        }
                }

                Pose(i, angle);
            }
        }

        void SetState(int i, int state, float simTime)
        {
            _state[i] = state;
            _stateStart[i] = simTime;
        }

        void Pose(int i, float angle)
        {
            bodies[i].MovePosition(transform.TransformPoint(pivotLocal[i]));
            bodies[i].MoveRotation(transform.rotation * Quaternion.Euler(0f, yawDegrees[i], 0f) * Quaternion.Euler(0f, 0f, angle));
        }

        int Count(int i)
        {
            Quaternion rot = transform.rotation * Quaternion.Euler(0f, yawDegrees[i], 0f) * Quaternion.Euler(0f, 0f, restDegrees);
            Vector3 centre = transform.TransformPoint(pivotLocal[i]) + rot * catchCentre;
            int hits = Physics.OverlapBoxNonAlloc(centre, catchHalfExtents, _buffer, rot, ~0, QueryTriggerInteraction.Ignore);
            int count = 0;
            for (int h = 0; h < hits; h++)
            {
                Rigidbody rb = _buffer[h].attachedRigidbody;
                if (rb != null && !rb.isKinematic) count++;
            }

            return count;
        }
    }
}
