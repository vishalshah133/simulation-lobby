using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// Sits in a sorter guide funnel as a trigger the shape of the funnel. A ball inside it is pushed toward the funnel's axis and
    /// downward, a little each physics step, up to <see cref="inwardSpeed"/> and <see cref="downSpeed"/>, so it drops into the
    /// bin faster instead of lingering on the cone. Fixed time, inside the physics step, so a seed replays exactly.
    /// </summary>
    public sealed class FunnelBoost : MonoBehaviour
    {
        [Tooltip("Speed toward the funnel's axis that balls are pushed up to, m/s.")]
        public float inwardSpeed = 2.5f;
        [Tooltip("Downward speed that balls are pushed up to, m/s.")]
        public float downSpeed = 3f;
        [Tooltip("Strongest push, m/s per second.")]
        public float acceleration = 60f;

        void OnTriggerStay(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body == null || body.isKinematic) return;

            float maxStep = acceleration * Time.fixedDeltaTime;
            Vector3 toAxis = transform.position - body.position;
            toAxis.y = 0f;
            Vector3 velocity = body.linearVelocity;
            if (toAxis.sqrMagnitude > 1e-4f)
            {
                Vector3 dir = toAxis.normalized;
                float along = Vector3.Dot(velocity, dir);
                if (along < inwardSpeed) body.AddForce(dir * Mathf.Min(inwardSpeed - along, maxStep), ForceMode.VelocityChange);
            }

            if (velocity.y > -downSpeed) body.AddForce(Vector3.down * Mathf.Min(downSpeed + velocity.y, maxStep), ForceMode.VelocityChange);
        }
    }
}
