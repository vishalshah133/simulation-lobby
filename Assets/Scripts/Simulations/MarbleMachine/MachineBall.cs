using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// One ball. Created by <c>MachineSimulation</c> at Initialize (all balls exist from tick 0). Contract file for mach-001.
    /// Only the simulation writes these fields; stations and presentation read them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MachineBall : MonoBehaviour
    {
        /// <summary>Stable index into the simulation's ball list.</summary>
        public int id;

        /// <summary>Index into <see cref="MachineConfig.colours"/>.</summary>
        public int colour;

        public Rigidbody body;

        /// <summary>True once the ball has been counted into a tube. It never moves again in the contest.</summary>
        public bool counted;

        /// <summary>Tube it was counted into, or -1.</summary>
        public int tube = -1;

        /// <summary>Times this ball went through the cannon.</summary>
        public int recycles;

        /// <summary>Sim time at which the ball was last seen moving (for stall detection).</summary>
        public float lastMovingTime;
    }
}
