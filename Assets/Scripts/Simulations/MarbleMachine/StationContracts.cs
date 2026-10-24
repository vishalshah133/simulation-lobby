using System.Collections.Generic;
using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    // Cross-work-package contracts for mach-001. The simulation (WP1a) talks to the sorter and tube rack (WP1b)
    // only through these, so both can be built in parallel. Contract file: don't edit inside a work package.

    /// <summary>The colour sorter. Implemented by <c>SorterStation</c>.</summary>
    public interface ISorter
    {
        /// <summary>
        /// Called every tick after <see cref="MachineStation.Step"/>. Looks at the balls in its lane and opens flap i
        /// for a ball of colour i inside gate window i. Raises <see cref="MachineStation.Pulse"/> on each flap
        /// and <see cref="Scanned"/> when a ball passes the scanner arch.
        /// </summary>
        void StepSort(float simTime, IReadOnlyList<MachineBall> balls);

        /// <summary>A ball passed the scanner arch: colour, world position. Fires during FixedUpdate.</summary>
        event System.Action<int, Vector3> Scanned;
    }

    /// <summary>The seven tubes. Implemented by <c>TubeRack</c>.</summary>
    public interface ITubeRack
    {
        int TubeCount { get; }

        /// <summary>Index of the tube whose interior (below its rim) contains <paramref name="worldPosition"/>, or -1.</summary>
        int TubeIndexAt(Vector3 worldPosition);

        Vector3 TubeTop(int tube);
        Vector3 TubeFillLine(int tube);

        /// <summary>Presentation-only state change (lights the fill ring). Called by the simulation on the win.</summary>
        void MarkWinner(int tube);
    }

    /// <summary>The silo. Implemented by <c>SiloStation</c> (WP1a; listed here so the scene builder can rely on it).</summary>
    public interface ISilo
    {
        /// <summary>World-space box the simulation fills with the starting ball lattice.</summary>
        Bounds StorageBounds { get; }

        /// <summary>Inner radius of the glass cylinder, for the lattice.</summary>
        float InnerRadius { get; }

        /// <summary>Open top of the silo, where the cannon aims.</summary>
        Vector3 CannonTarget { get; }

        void SetFeedRate(float ballsPerSecond);
        void SetFloodGate(bool open);
    }

    /// <summary>The recycle cannon. Implemented by <c>CannonStation</c> (WP1a).</summary>
    public interface ICannon
    {
        /// <summary>Where recycled and lost balls are put to be fired (inside the hopper).</summary>
        Vector3 HopperDrop { get; }

        /// <summary>Called every tick: fires at most one ball from the breech per interval, at <paramref name="target"/>.</summary>
        void StepFire(float simTime, IReadOnlyList<MachineBall> balls, Vector3 target, SeededRandom rng);
    }
}
