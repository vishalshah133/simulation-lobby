using System;
using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// One station of the machine. Built by an editor builder with its entry anchor at the station's local origin
    /// and flow along local +Z, then placed by the scene builder. Contract file for mach-001.
    /// </summary>
    /// <remarks>
    /// <see cref="Step"/> is called from <see cref="ISimulation.Tick"/> (inside <c>FixedUpdate</c>) before PhysX steps.
    /// Moving parts are kinematic rigidbodies moved with <c>MovePosition</c>/<c>MoveRotation</c>, posed from
    /// <paramref name="simTime"/> alone, so a seed replays exactly. Never read <c>Time.time</c> or
    /// <c>UnityEngine.Random</c>.
    /// </remarks>
    public abstract class MachineStation : MonoBehaviour
    {
        [Tooltip("Readable name for the camera director and logs, e.g. \"Vortex Funnel\".")]
        public string displayName;

        [Tooltip("Where balls arrive. The previous connector ends here.")]
        public Transform entry;

        [Tooltip("Where balls leave. The next connector starts here.")]
        public Transform exit;

        [Tooltip("Optional second exit for rejects (the Ski Jump's miss net, the Sorter's leftovers). Leads to the cannon.")]
        public Transform recycleExit;

        [Tooltip("What the tour camera looks at.")]
        public Transform cameraFocus;

        [Tooltip("Suggested camera position relative to cameraFocus, in world axes.")]
        public Vector3 cameraOffset = new Vector3(0f, 3f, -6f);

        [Tooltip("How long a tour shot lingers here.")]
        [Min(1f)] public float tourSeconds = 8f;

        [Tooltip("World-space box the audio samples for ball activity. Set by the builder.")]
        public Bounds activityBounds;

        /// <summary>
        /// A discrete mechanical event (a bucket tips, a trapdoor slams, the cannon fires) at a world position with a
        /// strength in 0..1. Raised during <c>FixedUpdate</c>. Presentation listens; it must not write back.
        /// </summary>
        public event Action<MachineStation, Vector3, float> Pulse;

        /// <summary>Called once from <see cref="ISimulation.Initialize"/>. Seed any per-station phase from <paramref name="rng"/>.</summary>
        public virtual void Initialize(MachineConfig config, SeededRandom rng) { }

        /// <summary>Pose every moving part for <paramref name="simTime"/>. Called every tick in flow order.</summary>
        public abstract void Step(float simTime, float dt);

        protected void RaisePulse(Vector3 worldPosition, float strength)
        {
            Pulse?.Invoke(this, worldPosition, Mathf.Clamp01(strength));
        }
    }
}
