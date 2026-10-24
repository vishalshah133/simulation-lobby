using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>A near-vertical pachinko wall: static pegs plus two counter-rotating pinwheels.</summary>
    public sealed class PegWallStation : MachineStation
    {
        [Tooltip("The two pinwheels, built with a kinematic body. Even indices turn one way, odd the other.")]
        public KinematicSpinner[] spinners;

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            if (spinners == null) return;
            SeededRandom own = rng.Branch(4201);
            for (int i = 0; i < spinners.Length; i++)
            {
                if (spinners[i] == null) continue;
                spinners[i].degreesPerSecond = (i % 2 == 0 ? 1f : -1f) * config.pegWallSpinnerDegreesPerSecond;
                spinners[i].SetPhase(own.NextFloat(0f, 360f));
            }
        }

        public override void Step(float simTime, float dt)
        {
            if (spinners == null) return;
            for (int i = 0; i < spinners.Length; i++)
            {
                if (spinners[i] != null) spinners[i].Step(simTime);
            }
        }
    }
}
