using System;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Cricket
{
    public enum GateKind
    {
        /// <summary>Nothing happens. A lane the ball can fall through untouched.</summary>
        Pass = 0,

        /// <summary>The distance the ball would carry is multiplied.</summary>
        Multiply = 1,

        /// <summary>The batter hits it. The ball leaves the board and the distance so far becomes runs.</summary>
        Release = 2,

        /// <summary>A wicket.</summary>
        Out = 3
    }

    [Serializable]
    public struct GateDef
    {
        public GateKind kind;

        [Tooltip("The multiplier for a Multiply gate. Ignored by the others.")]
        [Min(1f)] public float value;

        public GateDef(GateKind kind, float value = 1f)
        {
            this.kind = kind;
            this.value = value;
        }

        public string Label
        {
            get
            {
                switch (kind)
                {
                    case GateKind.Multiply: return $"×{value:0.#}";
                    case GateKind.Release: return "HIT";
                    case GateKind.Out: return "OUT";
                    default: return string.Empty;
                }
            }
        }
    }

    /// <summary>One column of gates across the strip, listed top to bottom. The ball meets the columns in order.</summary>
    [Serializable]
    public sealed class GateColumn
    {
        public GateDef[] gates;

        public GateColumn(params GateDef[] gates)
        {
            this.gates = gates;
        }
    }

    /// <summary>The shortest distance (metres) that earns these runs. Bands are read from the top down.</summary>
    [Serializable]
    public struct RunBand
    {
        [Min(0f)] public float minDistance;

        [Tooltip("0, 1, 2, 3, 4 or 6.")]
        public int runs;

        public RunBand(float minDistance, int runs)
        {
            this.minDistance = minDistance;
            this.runs = runs;
        }
    }

    /// <summary>
    /// Tunables for the cricket format. A published video is this asset + the scene + a seed.
    /// The two teams are assigned here, so a new matchup is two drag-and-drops in the inspector.
    /// </summary>
    /// <remarks>
    /// The board is a short, wide strip under the ground. The ball is launched at the right end and
    /// flows left through columns of gates and pegs: a multiplier gate scales the distance it carries,
    /// a release gate turns it into runs and an out gate ends the batter's innings. Layout is baked
    /// into the scene by the builder from these values, so changing geometry here means rebuilding the
    /// scene. Lane order is redrawn from the seed for every delivery, so no two balls face the same board.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "CricketConfig",
        menuName = "Simulation Lobby/Configs/Cricket (cric)",
        order = 13)]
    public sealed class CricketConfig : SimulationConfig
    {
        [Header("Teams — side A bats first, side B chases")]
        public CricketTeamDef teamA;
        public CricketTeamDef teamB;

        [Header("Match")]
        [Tooltip("1 is a super over. Each over is Balls Per Over deliveries.")]
        [Range(1, 5)] public int oversPerInnings = 1;
        [Range(1, 10)] public int ballsPerOver = 6;
        [Tooltip("An innings ends at this many wickets or when the balls run out. A super over has 2.")]
        [Range(1, 10)] public int wicketsPerInnings = 2;

        [Header("Board — one column per line of gates, in the order the ball meets them; every column needs the same number")]
        public GateColumn[] columns =
        {
            new GateColumn(new GateDef(GateKind.Multiply, 2f), new GateDef(GateKind.Multiply, 1.5f), new GateDef(GateKind.Pass), new GateDef(GateKind.Release), new GateDef(GateKind.Pass)),
            new GateColumn(new GateDef(GateKind.Multiply, 2f), new GateDef(GateKind.Multiply, 3f), new GateDef(GateKind.Release), new GateDef(GateKind.Pass), new GateDef(GateKind.Pass)),
            new GateColumn(new GateDef(GateKind.Multiply, 2f), new GateDef(GateKind.Multiply, 3f), new GateDef(GateKind.Release), new GateDef(GateKind.Pass), new GateDef(GateKind.Pass)),
            // The last column is the pay-off, and the only one with a wicket in it.
            new GateColumn(new GateDef(GateKind.Multiply, 3f), new GateDef(GateKind.Release), new GateDef(GateKind.Release), new GateDef(GateKind.Out), new GateDef(GateKind.Multiply, 2f)),
        };

        [Tooltip("Draw each column's lane order from the seed for every delivery. Off keeps the authored order.")]
        public bool shuffleLanesPerDelivery = true;

        [Header("Runs — distance in metres to runs")]
        [Tooltip("The distance every ball starts with, before any multiplier.")]
        [Min(1f)] public float baseDistance = 6f;

        [Tooltip("Ascending by distance. A release at or past a band's distance scores its runs; below the first it is a dot.")]
        public RunBand[] bands =
        {
            new RunBand(0f, 0),
            new RunBand(8f, 1),
            new RunBand(14f, 2),
            new RunBand(20f, 3),
            new RunBand(30f, 4),
            new RunBand(60f, 6),
        };

        [Header("Shot direction")]
        [Tooltip("Where the ball leaves the board decides the angle of the shot: the board edges map to ± this many degrees off straight.")]
        [Range(30f, 180f)] public float maxShotAngle = 150f;

        [Tooltip("Seeded wobble added to the angle, so two balls through the same lane do not draw one line.")]
        [Range(0f, 30f)] public float angleJitter = 9f;

        [Header("Ball")]
        [Min(0.03f)] public float ballRadius = 0.14f;
        [Range(0f, 1f)] public float ballBounciness = 0.5f;

        [Tooltip("How hard the strip pulls the ball toward the gates, in units/s^2. Higher = a faster delivery.")]
        [Range(0.5f, 12f)] public float flowAcceleration = 4f;
        [Range(0f, 2f)] public float linearDamping = 0.05f;
        [Min(1f)] public float maxSpeed = 6.5f;

        [Tooltip("A ball this slow for this long is nudged (seeded), so none sits on a peg forever.")]
        [Min(0.2f)] public float stuckSeconds = 1.2f;

        [Tooltip("How much of the strip's height the ball may be launched across, 0-1.")]
        [Range(0.1f, 1f)] public float launchSpread = 0.7f;

        [Header("Board layout (rebuild the scene after changing)")]
        [Tooltip("Centre of the strip. It sits low in a 9:16 frame, under the distance readout and clear of the footer text.")]
        public Vector2 stripCenter = new Vector2(0f, -6.05f);
        [Min(4f)] public float stripWidth = 10.4f;
        [Min(0.3f)] public float laneHeight = 0.76f;
        [Min(1f)] public float columnPitch = 2f;
        [Min(0.2f)] public float gateThickness = 0.7f;

        [Tooltip("Gap from the right wall to the first column of gates: the run-up where the ball is launched.")]
        [Min(0.5f)] public float entryMargin = 2f;

        [Range(0, 4)] public int pegLinesPerColumn = 2;
        [Min(0.03f)] public float pegRadius = 0.12f;
        [Min(0.3f)] public float pegSpacing = 0.76f;

        [Header("Ground (the wagon wheel)")]
        [Tooltip("Centre of the ground. Keep it clear of the score at the top and the distance readout below it.")]
        public Vector2 groundCenter = new Vector2(0f, 2.5f);
        [Min(1f)] public float groundRadius = 3.6f;

        [Tooltip("Where the big distance / result readout sits: the clear band between the ground and the strip.")]
        public Vector2 readoutCenter = new Vector2(0f, -2.5f);

        [Tooltip("Written by the scene builder so an old asset gets the current layout once. Leave alone.")]
        public int layoutVersion;

        [Header("Pacing")]
        [Tooltip("Seconds the ball waits at the top before it drops, so the board and the score can be read.")]
        [Min(0f)] public float preBallHoldSeconds = 0.7f;

        [Tooltip("Seconds the result of a ball holds, while its shot line draws and the crowd reacts.")]
        [Min(0f)] public float resultHoldSeconds = 2f;

        [Tooltip("Seconds between the innings, with the target on screen.")]
        [Min(0f)] public float inningsBreakSeconds = 2.5f;

        [Tooltip("Seconds the result holds at the end.")]
        [Min(0f)] public float finishHoldSeconds = 4f;

        // --- Helpers shared by the scene builder and the simulation so they cannot disagree ---

        public int ColumnCount => columns != null ? columns.Length : 0;

        public int Lanes => ColumnCount > 0 && columns[0] != null && columns[0].gates != null ? columns[0].gates.Length : 0;

        public int BallsPerInnings => oversPerInnings * ballsPerOver;

        public float StripHalfWidth => stripWidth * 0.5f;

        public float StripHeight => Lanes * laneHeight;

        public float StripHalfHeight => StripHeight * 0.5f;

        /// <summary>Where the ball is launched: just inside the right wall.</summary>
        public float SpawnX => stripCenter.x + StripHalfWidth - 0.5f;

        /// <summary>x of a column of gates. Column 0 is met first, on the right; later columns sit further left.</summary>
        public float ColumnX(int column) => stripCenter.x + StripHalfWidth - entryMargin - column * columnPitch;

        /// <summary>y of a lane's centre. Lane 0 is at the top.</summary>
        public float LaneCenterY(int lane) => stripCenter.y + StripHalfHeight - (lane + 0.5f) * laneHeight;

        /// <summary>Past this (to the left of the last column) the ball is released at whatever it has carried.</summary>
        public float ExitX => ColumnX(Mathf.Max(0, ColumnCount - 1)) - columnPitch * 0.9f;

        public float PegLineX(int column, int line) => ColumnX(column) - gateThickness * 0.5f - 0.5f - line * 0.5f;

        public int PegsInLine(int line) => Mathf.Max(1, Mathf.FloorToInt(StripHeight / pegSpacing) - (line % 2));

        public float PegY(int line, int index)
        {
            float offset = (line % 2) * pegSpacing * 0.5f;
            return stripCenter.y - StripHalfHeight + pegSpacing * 0.5f + offset + index * pegSpacing;
        }

        /// <summary>The outcome a release at this distance scores.</summary>
        public BallOutcome OutcomeFor(float distance)
        {
            int runs = 0;
            for (int i = 0; i < bands.Length; i++)
            {
                if (distance >= bands[i].minDistance)
                {
                    runs = bands[i].runs;
                }
            }

            BallOutcomes.TryFromRuns(runs, out BallOutcome outcome);
            return outcome;
        }

        public string TeamName(int side)
        {
            CricketTeamDef team = side == 0 ? teamA : teamB;
            return team != null ? team.displayName : (side == 0 ? "TEAM A" : "TEAM B");
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (teamA == null || teamB == null)
            {
                error = "assign both teams (side A bats first).";
                return false;
            }

            if (ColumnCount < 1 || Lanes < 2)
            {
                error = "the board needs at least one column with two or more gates.";
                return false;
            }

            bool hasRelease = false;
            for (int column = 0; column < ColumnCount; column++)
            {
                if (columns[column] == null || columns[column].gates == null || columns[column].gates.Length != Lanes)
                {
                    error = $"column {column + 1} must have {Lanes} gates, the same as column 1.";
                    return false;
                }

                for (int lane = 0; lane < Lanes; lane++)
                {
                    GateDef gate = columns[column].gates[lane];
                    hasRelease |= gate.kind == GateKind.Release;
                    if (gate.kind == GateKind.Multiply && gate.value <= 1f)
                    {
                        error = $"column {column + 1}, gate {lane + 1}: a Multiply gate needs a value above 1.";
                        return false;
                    }
                }
            }

            if (!hasRelease)
            {
                error = "the board has no Release gate, so no ball could ever be hit before the bottom.";
                return false;
            }

            if (bands == null || bands.Length == 0)
            {
                error = "add at least one run band.";
                return false;
            }

            for (int i = 0; i < bands.Length; i++)
            {
                if (!BallOutcomes.TryFromRuns(bands[i].runs, out _))
                {
                    error = $"run band {i + 1} scores {bands[i].runs}; cricket only has 0, 1, 2, 3, 4 and 6.";
                    return false;
                }

                if (i > 0 && bands[i].minDistance <= bands[i - 1].minDistance)
                {
                    error = $"run band {i + 1} must start further out than band {i}.";
                    return false;
                }
            }

            float gap = pegSpacing - pegRadius * 2f;
            if (pegLinesPerColumn > 0 && gap < ballRadius * 2f * 1.6f)
            {
                error = $"the gap between pegs ({gap:0.00}) is too tight for the ball " +
                        $"({ballRadius * 2f:0.00}) -- it would wedge. Widen pegSpacing or shrink the ball.";
                return false;
            }

            float safe = ConstantSpeed2D.MaxSafeSpeed(fixedTimestep, Mathf.Min(gateThickness, pegRadius * 2f));
            if (maxSpeed > safe)
            {
                error = $"maxSpeed {maxSpeed:0.0} exceeds the safe ceiling {safe:0.0} at timestep " +
                        $"{fixedTimestep} -- the ball could skip through a gate or peg.";
                return false;
            }

            float rightmost = ColumnX(0) + gateThickness * 0.5f;
            if (rightmost >= stripCenter.x + StripHalfWidth - 0.6f)
            {
                error = "entryMargin is too small: the first column leaves no run-up for the ball to launch into.";
                return false;
            }

            if (ExitX <= stripCenter.x - StripHalfWidth + 0.3f)
            {
                error = "the columns do not fit in stripWidth: raise stripWidth or lower columnPitch / the column count.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
