using UnityEngine;

namespace SimulationLobby.Simulations.Survival.LavaRun
{
    /// <summary>Where the run is. Presentation reads this to pick the caption, the footer and the camera.</summary>
    public enum LavaRunPhase
    {
        /// <summary>Stage 1 anticipation beat: pens full, nothing moves.</summary>
        Hold = 0,

        /// <summary>Stages 2-3: survivors sit in the new pens before the gate opens.</summary>
        PenSettle = 1,

        /// <summary>Pen gate open, marbles racing. The lava front starts once <c>HasErupted</c>.</summary>
        Running = 2,

        /// <summary>The front reached the basin; the door is sliding shut.</summary>
        DoorClosing = 3,

        /// <summary>Door shut, counts frozen, elimination announced.</summary>
        StageResult = 4,

        /// <summary>Trapdoor open, survivors falling toward the next stage's pens.</summary>
        Drop = 5,

        /// <summary>Stage 4: the sinking island.</summary>
        Island = 6,

        /// <summary>A winner is decided; the payoff hold is running.</summary>
        Payoff = 7,

        Complete = 8
    }

    public enum LavaRunDeathCause
    {
        Lava = 0,
        Fall = 1,

        /// <summary>Removed with an eliminated colour at a stage door.</summary>
        Eliminated = 2
    }

    public enum LavaRunEventKind
    {
        /// <summary>The pen gate opened. stage = stage.</summary>
        Released,

        /// <summary>The crater erupted; the front starts moving. stage = stage.</summary>
        Erupted,

        /// <summary>A marble entered a gate. team, stage, index = gate index, value = clones spawned (0 for a x1 lane), position = marble.</summary>
        GateTriggered,

        /// <summary>Only fires when config.maxLivePerTeam > 0: a gate wanted more clones than that cap allows. team, stage, index = gate, value = clones skipped.</summary>
        GateCapped,

        /// <summary>Lava killed a marble (including stragglers outside the basin at door close). team, position.</summary>
        Swallowed,

        /// <summary>A marble fell below the kill plane or into the lake. team, position.</summary>
        FellIn,

        /// <summary>A marble was removed because its colour was eliminated at the door. team, position.</summary>
        Sunk,

        /// <summary>The door started to close. stage = stage.</summary>
        DoorClosing,

        /// <summary>The door finished closing; the basin is decided. stage = stage.</summary>
        DoorClosed,

        /// <summary>A colour was eliminated at the door. team, stage, value = its count when it lost.</summary>
        TeamEliminated,

        /// <summary>A colour hit zero live marbles. team, stage (3 = island).</summary>
        TeamOut,

        /// <summary>A stage is current and its pens are filled. stage = stage (0..2).</summary>
        StageStarted,

        /// <summary>The trapdoor opened under the survivors. stage = the stage that just ended.</summary>
        TrapdoorOpened,

        /// <summary>Stage 4 began. stage = 3.</summary>
        IslandStarted,

        /// <summary>A ring starts to tremble. index = ring (0 = outermost).</summary>
        RingSinking,

        /// <summary>A ring finished sinking. index = ring.</summary>
        RingSunk,

        /// <summary>Fewer than slowdownBelow marbles remain on the island.</summary>
        SlowdownStarted,

        /// <summary>maxRunSeconds reached with no winner; the colour with the most marbles wins.</summary>
        ForcedDecision,

        /// <summary>team = the winning colour.</summary>
        Winner
    }

    /// <summary>One thing that happened, stamped with its tick. The list is append-only.</summary>
    public readonly struct LavaRunEvent
    {
        public readonly int tick;
        public readonly LavaRunEventKind kind;
        public readonly int team;
        public readonly int stage;
        public readonly int index;
        public readonly int value;
        public readonly Vector3 position;

        public LavaRunEvent(int tick, LavaRunEventKind kind, int team = -1, int stage = 0, int index = 0,
            int value = 0, Vector3 position = default)
        {
            this.tick = tick;
            this.kind = kind;
            this.team = team;
            this.stage = stage;
            this.index = index;
            this.value = value;
            this.position = position;
        }
    }

    public enum LavaRingState
    {
        Intact = 0,
        Trembling = 1,
        Sinking = 2,
        Sunk = 3
    }
}
