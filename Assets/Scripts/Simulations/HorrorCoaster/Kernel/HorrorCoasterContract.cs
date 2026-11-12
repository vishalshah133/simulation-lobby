using System;
using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>Which set the cart is in. Order = ride order.</summary>
    public enum ActId
    {
        Station, ChainLift, TrickOrTreatStreet, Graveyard, DeadForest, TunnelOfFaces,
        Trip, Mansion, Asylum, FinalClimb, FinalDrop, StationReturn
    }

    /// <summary>What a scheduled event is. "Fake" kinds fire the build-up and quiet but nothing takes a rider.</summary>
    public enum ScareKind
    {
        FakeBat,          // chain lift fake-out
        TreatDoor,        // trick-or-treat door that is a TREAT: candy shower, safe
        DoorGhoul,        // trick-or-treat door that is the TRICK (scare 1)
        GraveHand,        // scare 2
        FakeLantern,      // graveyard fake-out: a lantern goes out, nothing
        GraveyardGhost,   // scare 3 (no rider)
        CrowBurst,        // scare 4
        TunnelFace,       // scare 5: last face is behind the cart
        PaintingLunge,    // scare 6
        FakeWheelchair,   // asylum fake-out
        AsylumFigure,     // scare 7 (no rider)
        FakeEmptySeat,    // final climb silence fake-out
        SkullFinale,      // scare 8, the big one (no rider)

        // ---- Signature scares: each one a different SHAPE of fear (not "something lunges at the lens"). Big, real, long pre-quiet. ----
        SprinterLeap,     // a beast sprints at the cart from 80 m away down the track, then leaps onto the camera (no rider)
        SideLeap,         // a ghoul crouched on a high perch to one side drops across the cart, an arm's length from the lens (no rider)
        SnakeStrike,      // a giant cobra rises beside the train, strikes a rider ahead and swallows them (takes a rider)
        SpikeWall,        // a wall beside the track fires spikes across the cars, impaling a rider; a second volley stops short of the viewer (takes a rider)
        BatFlood,         // the giant pumpkin's mouth pours out a flood of bats as the cart enters (no rider)

        // ---- Micro jump scares: short, sudden, anywhere on the ride, never take a rider. Short PreQuiet (0.3-0.9 s). ----
        SpiderDrop,       // giant spider drops on a thread to eye level, in front of the camera
        WindowFace,       // a pale face slams against the side of the cart from the dark (left or right)
        SkeletonPop,      // a skeleton springs up from beside the rail, arm swiping at the camera
        ClawHand,         // a hand grabs at the lap bar / the viewer's hands from below
        BatSwarm,         // a cloud of bats rushes at and over the camera
        CoffinLid,        // a coffin beside the rail bursts open, a corpse lunges out
        ShadowRush,       // a black silhouette with glowing eyes sprints along the rail toward the camera and past it
        MirrorFace        // a face appears in a cracked mirror dead ahead, then smashes toward the lens
    }

    public static class ScareKindExt
    {
        public static bool IsMicro(this ScareKind k) => k >= ScareKind.SpiderDrop;
    }

    /// <summary>One authored event along the rail. Fires when the cart's distance reaches <see cref="distance"/>.</summary>
    [Serializable]
    public struct ScareEvent
    {
        [Tooltip("Rail arc length (m) at which the sting fires. The quiet starts earlier, see preQuietSeconds.")]
        public float distance;
        public ScareKind kind;
        [Tooltip("True removes one rider at the sting.")]
        public bool takesRider;
        [Tooltip("Seconds of near-silence and visual cue before the sting. 0 for treat doors.")]
        public float preQuietSeconds;
        [Tooltip("Free text caption shown on screen at the cue (e.g. TRICK?). Empty = none.")]
        public string cueCaption;

        public bool IsFake => kind == ScareKind.FakeBat || kind == ScareKind.FakeLantern ||
                              kind == ScareKind.FakeWheelchair || kind == ScareKind.FakeEmptySeat;
    }

    /// <summary>Where an act starts along the rail (it ends where the next begins).</summary>
    [Serializable]
    public struct ActSpan
    {
        public ActId act;
        public float startDistance;
    }

    /// <summary>Phases of the scare the director is currently in.</summary>
    public enum ScarePhase { Idle, PreQuiet, Sting, Aftermath }

    /// <summary>Everything Presentation/View may read. Implemented by <c>HorrorCoasterSimulation</c>. Read-only by contract.</summary>
    public interface ICoasterState
    {
        ICoasterRail Rail { get; }
        float SimTime { get; }
        float Distance { get; }
        float Speed { get; }
        bool OnLift { get; }
        /// <summary>Cart pose (the POV car), from the rail at Distance plus the seeded wobble.</summary>
        Vector3 CartPosition { get; }
        Vector3 CartForward { get; }
        Vector3 CartUp { get; }
        ActId Act { get; }
        /// <summary>Counter shown on screen. Starts at 6 (five riders ahead + the viewer).</summary>
        int RidersLeft { get; }
        /// <summary>True for each of the five seats ahead (index 0 = nearest) while its rider is still aboard.</summary>
        IReadOnlyList<bool> RiderPresent { get; }
        /// <summary>Index into the schedule of the next event not yet stung; == count when none left.</summary>
        int NextEventIndex { get; }
        IReadOnlyList<ScareEvent> Events { get; }
        ScarePhase Phase { get; }
        /// <summary>The event Phase refers to (valid when Phase != Idle).</summary>
        ScareEvent CurrentEvent { get; }
        int CurrentEventIndex { get; }
        /// <summary>Seconds since the current Phase began.</summary>
        float PhaseTime { get; }
        /// <summary>0..1: how near the next real scare is (drives heartbeat, grain, FOV). Smooth, seeded-wobble free.</summary>
        float Fear01 { get; }
        /// <summary>Which seat (index into RiderPresent) the rider taken by the current event sat in; -1 if none.</summary>
        int TakenSeat { get; }
        bool IsComplete { get; }
    }
}
