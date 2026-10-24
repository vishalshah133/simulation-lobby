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
        const float TipSeconds = 0.35f;
        const float HoldSeconds = 0.35f;
        const float ReturnSeconds = 0.45f;

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

        int _threshold = 25;
        int[] _state;          // 0 resting, 1 tipping, 2 holding, 3 returning
        float[] _stateStart;
        readonly Collider[] _buffer = new Collider[192];

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            _threshold = Mathf.Max(1, config.tipperThreshold);
            int n = bodies != null ? bodies.Length : 0;
            _state = new int[n];
            _stateStart = new float[n];
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
                        if (Count(i) >= _threshold)
                        {
                            RaisePulse(transform.TransformPoint(pivotLocal[i]), Count(i) / (_threshold * 2f));
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
