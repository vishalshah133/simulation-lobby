using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// Read-only view of a running machine for presentation (camera, HUD, audio). Implemented by
    /// <c>MachineSimulation</c>. Events fire during <c>FixedUpdate</c>: listeners record them and react in
    /// <c>Update</c>/<c>LateUpdate</c>, never writing back. Contract file for mach-001.
    /// </summary>
    public interface IMachineState
    {
        float SimTime { get; }
        MachinePhase Phase { get; }

        int ColourCount { get; }
        string ColourName(int colour);
        Color ColourValue(int colour);

        /// <summary>Balls counted into tube <paramref name="colour"/> so far.</summary>
        int TubeCount(int colour);
        int TubeCapacity { get; }

        /// <summary>Colour with the most balls counted; -1 before the first sort. Ties keep the previous leader.</summary>
        int LeaderIndex { get; }
        int LeadChanges { get; }

        /// <summary>-1 until a tube fills.</summary>
        int WinnerIndex { get; }

        int TotalSorted { get; }
        int Recycled { get; }

        IReadOnlyList<MachineBall> Balls { get; }

        /// <summary>Every station in flow order (silo first, tube rack last, cannon at the end).</summary>
        IReadOnlyList<MachineStation> Stations { get; }

        /// <summary>World position of the top-centre of a tube, for the camera and VFX.</summary>
        Vector3 TubeTop(int colour);

        /// <summary>World position of the fill line of a tube (where the winning ball lands).</summary>
        Vector3 TubeFillLine(int colour);

        event Action<MachinePhase> PhaseChanged;
        event Action<int> LeaderChanged;

        /// <summary>A ball was counted: colour, world position.</summary>
        event Action<int, Vector3> BallSorted;

        event Action<int> Won;
    }
}
