using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// A speed-boost pad: arrows painted on a chute floor over a trigger volume. A ball inside it that is slower than
    /// <see cref="targetSpeed"/> along the pad's forward gets pushed up toward it, a little each physics step. Runs inside the
    /// physics step on fixed time, so a seed replays exactly. Built by <c>BoostPadBuilder</c>; speed set by the owning station.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BoostPad : MonoBehaviour
    {
        [Tooltip("Speed along the pad that balls are pushed up to, m/s (set from MachineConfig.boosterSurfaceSpeed).")]
        public float targetSpeed = 7f;
        [Tooltip("Strongest push, in m/s per second, so the boost is a surge rather than a teleport.")]
        public float acceleration = 45f;

        void OnTriggerStay(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body == null || body.isKinematic) return;
            Vector3 forward = transform.forward;
            float along = Vector3.Dot(body.linearVelocity, forward);
            if (along >= targetSpeed) return;
            float step = Mathf.Min(targetSpeed - along, acceleration * Time.fixedDeltaTime);
            body.AddForce(forward * step, ForceMode.VelocityChange);
        }
    }
}
