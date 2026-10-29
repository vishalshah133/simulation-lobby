using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>A banked corkscrew around a chrome column. Gravity and the bank do the work; a boost pad at its start gets balls moving.</summary>
    public sealed class HelixSlideStation : MachineStation
    {
        [Tooltip("Speed-boost pad at the start of the helix.")]
        public BoostPad[] pads;

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            if (pads == null) return;
            foreach (BoostPad pad in pads) if (pad != null) pad.targetSpeed = config.boosterSurfaceSpeed;
        }

        public override void Step(float simTime, float dt)
        {
        }
    }
}
