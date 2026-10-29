using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    public enum RacePhase
    {
        /// <summary>Grid is set, nothing moves. The hook shot.</summary>
        Grid,
        Countdown,
        Racing,
        /// <summary>The leader is on the last lap.</summary>
        FinalLap,
        /// <summary>Someone has won; the outro runs.</summary>
        Finished
    }

    /// <summary>
    /// Read-only view of a running lap race for presentation (camera, HUD, audio). Implemented by <c>MachineRaceSimulation</c>.
    /// Events fire during <c>FixedUpdate</c>: listeners record them and react in <c>Update</c>/<c>LateUpdate</c>, never
    /// writing back. Contract file for mach-002.
    /// </summary>
    /// <remarks>
    /// A racer is a <see cref="MachineBall"/> (id = racer index, colour = racer index). Racer numbers are index + 1.
    /// <see cref="Progress"/> is the single ranking quantity: laps completed plus the fraction of the current lap, in
    /// 0..Laps. It can go DOWN (a ski-jump miss is recycled by the cannon back to the start), which is the drama.
    /// </remarks>
    public interface IRaceState
    {
        float SimTime { get; }
        RacePhase Phase { get; }

        int RacerCount { get; }
        int Laps { get; }
        /// <summary>1-based number shown on the ball and the board.</summary>
        int RacerNumber(int racer);
        string RacerName(int racer);
        Color RacerColour(int racer);
        Vector3 RacerPosition(int racer);

        /// <summary>Total progress 0..Laps (see remarks). Finished racers report exactly <see cref="Laps"/>.</summary>
        float Progress(int racer);
        /// <summary>1-based lap the racer is currently on (1..Laps); Laps once finished.</summary>
        int LapOf(int racer);
        /// <summary>1-based rank, finished racers first in finishing order, then by progress.</summary>
        int RankOf(int racer);
        /// <summary>Racer index holding 1-based <paramref name="rank"/>.</summary>
        int RacerAtRank(int rank);
        /// <summary>Name of the station the racer is in or nearest to (for the camera and captions); "" if unknown.</summary>
        string StationNameOf(int racer);

        int LeaderIndex { get; }
        int LeadChanges { get; }
        /// <summary>-1 until someone finishes.</summary>
        int WinnerIndex { get; }
        int FinishedCount { get; }
        /// <summary>Sim time the racer crossed the line, or -1.</summary>
        float FinishTime(int racer);
        /// <summary>Times the racer went through the recycle cannon.</summary>
        int Recycles(int racer);

        /// <summary>Every station in flow order, start gate first.</summary>
        IReadOnlyList<MachineStation> Stations { get; }

        event Action<RacePhase> PhaseChanged;
        event Action<int> LeaderChanged;
        /// <summary>Racer finished lap n (1-based): racer, lap, world position of the lap gate.</summary>
        event Action<int, int> LapCompleted;
        /// <summary>Racer crossed the finish line: racer, place (1-based).</summary>
        event Action<int, int> RacerFinished;
        event Action<int> Won;
        /// <summary>A racer was fired from the recycle cannon: racer, world position.</summary>
        event Action<int, Vector3> Recycled;
    }

    /// <summary>The start gate and lap line. Implemented by <c>StartGateStation</c>.</summary>
    public interface IStartGate
    {
        /// <summary>World position of grid slot <paramref name="slot"/> (0 = front-left), where a racer's ball centre is placed.</summary>
        Vector3 GridSlot(int slot);
        int SlotCount { get; }
        /// <summary>Drop the gate plate: the racers are released. Called once by the simulation after the countdown.</summary>
        void Release();
        /// <summary>World position the cannon aims at: just above the start platform, so a recycled racer lands on the grid.</summary>
        Vector3 CannonTarget { get; }
        /// <summary>The lap line: a racer whose centre crosses this plane in the flow direction has completed a lap.</summary>
        Vector3 LapLinePoint { get; }
        Vector3 LapLineNormal { get; }
    }
}
