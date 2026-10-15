using UnityEngine;

namespace SimulationLobby.Simulations.Territory
{
    /// <summary>Where the run is in its arc. Presentation reads this to pick the caption and the footer.</summary>
    public enum TerritoryPhase
    {
        /// <summary>Setup beat: everything sits still while the rules are on screen.</summary>
        Hold = 0,

        /// <summary>Boards drop, cannons fire.</summary>
        Battle = 1,

        /// <summary>Two or fewer colours left.</summary>
        Finale = 2,

        /// <summary>A winner is decided; the end hold is running.</summary>
        Complete = 3
    }

    /// <summary>What a <see cref="TerritoryEvent"/> records.</summary>
    public enum TerritoryEventKind
    {
        /// <summary>slot = slot index; before/after = bank before and after the slot's effect. other: 0 = applied, 2 = ignored, team was firing (Multiply/Release slot on a locked board; before == after).</summary>
        SlotHit,

        /// <summary>before = queued ammo before; after = the amount released; other = 1 if the bank hit bankCap and auto-released (autoReleaseAtCap), 0 for an R slot or spin.</summary>
        Released,

        /// <summary>slot = shot index; before = the shot's health (config.shotHealth for a normal shot: one count = one ball); after = ammo still queued.</summary>
        ShotFired,

        /// <summary>slot = face index on the reel.</summary>
        SpinStarted,

        /// <summary>slot = face index; other = 1 if the team was eliminated meanwhile and nothing was applied.</summary>
        SpinResolved,

        /// <summary>other = the capturing team (the new owner of the base cell).</summary>
        Eliminated,

        /// <summary>slot = escalation level.</summary>
        Escalated,

        /// <summary>slot = pool index; other = 0 stuck, 1 fell below the board, 2 left the board.</summary>
        BallRecovered,

        /// <summary>other = 1 if decided on tile count at the forced decision.</summary>
        Winner,

        // ---- v4 power faces (appended so earlier values do not shift) ----

        /// <summary>A bomb shot hit. slot = grid cell index of the blast centre (the cell the bomb was in, see MoveShots); after = cells changed by the disc.</summary>
        BombExploded,

        /// <summary>A + LINE resolved. slot = grid cell index of the target; after = cells changed (row + column); other = 1 if there was no enemy cell and the team's base row/column was painted instead.</summary>
        LinePainted,

        /// <summary>A SELF DESTRUCT resolved. slot = grid cell index of the blast centre, or -1 if the team had no eligible cell (bank still wiped); before = bank before the wipe; after = cells turned Unowned.</summary>
        SelfDestructed,

        // ---- v7 timed boosts ----

        /// <summary>A Boost spin face resolved. slot = the factor (kept if a larger boost was already running); after = boost ticks now left.</summary>
        BoostStarted,

        /// <summary>A team's boost ran out (not raised on elimination; the boost is just cleared).</summary>
        BoostEnded
    }

    /// <summary>What a spin-reel face does. Economy faces use <c>SpinFace.effect</c>; the others are territory-specific.</summary>
    public enum SpinFaceKind
    {
        /// <summary>Applies <c>SpinFace.effect</c> to the bank (Multiply, Add or Release). Must stay 0 so older assets load as Economy.</summary>
        Economy = 0,

        /// <summary>Fires bombShots bomb shots at once (value &gt; 0 overrides the count).</summary>
        Bomb = 1,

        /// <summary>Paints a row and column through a seeded enemy cell (value &gt; 0 overrides lineThickness).</summary>
        PlusLine = 2,

        /// <summary>Wipes the bank and queued ammo, and blasts a hole in the team's own frontier (value &gt; 0 overrides selfDestructRadius).</summary>
        SelfDestruct = 3,

        /// <summary>Timed boost: for config.boostSeconds the team's Multiply slots use value as the factor instead of their own. Appended so earlier values do not shift.</summary>
        Boost = 4
    }

    /// <summary>One thing that happened, stamped with its tick. The list is append-only.</summary>
    public readonly struct TerritoryEvent
    {
        public readonly int tick;
        public readonly TerritoryEventKind kind;
        public readonly int team, slot, other;
        public readonly long before, after;

        public TerritoryEvent(int tick, TerritoryEventKind kind, int team, int slot = 0, int other = 0,
            long before = 0, long after = 0)
        {
            this.tick = tick;
            this.kind = kind;
            this.team = team;
            this.slot = slot;
            this.other = other;
            this.before = before;
            this.after = after;
        }
    }

    /// <summary>One colour's live state. Read by presentation; only <see cref="TerritorySimulation"/> writes it.</summary>
    public sealed class TerritoryTeam
    {
        public int index;
        public string name;
        public Color color;
        public Vector2Int baseCell;

        /// <summary>Ammo banked by the Plinko board, not yet released.</summary>
        public long Bank;

        /// <summary>Ammo released and waiting to be fired.</summary>
        public long Queued;

        public bool Eliminated;
        public int Tiles;
        public float Share;

        /// <summary>Current cannon aim in grid-space degrees (0 = +x, 90 = +y).</summary>
        public float AimDegrees;

        public int LiveShots;
    }
}
