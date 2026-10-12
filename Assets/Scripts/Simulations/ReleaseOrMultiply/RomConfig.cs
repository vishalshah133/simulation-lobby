using System;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.ReleaseOrMultiply
{
    public enum GateKind
    {
        /// <summary>The marble's count is multiplied. Real clones appear while the body budget lasts.</summary>
        Multiply = 0,

        /// <summary>Adds to the marble's count.</summary>
        Add = 1,

        /// <summary>Halves it. A lone marble survives on a seeded coin flip.</summary>
        Halve = 2,

        /// <summary>The marble and everything it stood for is gone.</summary>
        Wipe = 3
    }

    [Serializable]
    public struct GateDef
    {
        public GateKind kind;

        [Tooltip("Multiplier or amount. Ignored by Halve and Wipe.")]
        [Min(0)] public int value;

        public GateDef(GateKind kind, int value)
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
                    case GateKind.Multiply: return $"×{value}";
                    case GateKind.Add: return $"+{value}";
                    case GateKind.Halve: return "÷2";
                    default: return "×0";
                }
            }
        }
    }

    [Serializable]
    public sealed class GateRow
    {
        public GateDef[] gates;

        public GateRow(params GateDef[] gates)
        {
            this.gates = gates;
        }
    }

    /// <summary>
    /// Tunables for "Release or Multiply". A published video is this asset + the scene + a seed.
    /// Layout is baked into the scene by the builder from these values, so changing geometry here
    /// means rebuilding the scene. Which gate sits in which lane is drawn from the seed each run.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RomConfig",
        menuName = "Simulation Lobby/Configs/Release or Multiply (rom)",
        order = 12)]
    public sealed class RomConfig : SimulationConfig
    {
        [Header("Release")]
        [Tooltip("Marbles dropped at the start. The count the viewer sees first.")]
        [Range(1, 50)] public int startMarbles = 3;

        [Tooltip("Seconds between drops.")]
        [Min(0.02f)] public float dropIntervalSeconds = 0.4f;

        [Tooltip("How much of the field width drops may land across, 0-1.")]
        [Range(0.1f, 1f)] public float dropSpread = 0.6f;

        [Header("Gates — one row per line of gates; every row needs the same number")]
        public GateRow[] rows =
        {
            new GateRow(new GateDef(GateKind.Multiply, 2), new GateDef(GateKind.Add, 3), new GateDef(GateKind.Halve, 2), new GateDef(GateKind.Multiply, 2)),
            new GateRow(new GateDef(GateKind.Multiply, 3), new GateDef(GateKind.Multiply, 2), new GateDef(GateKind.Add, 5), new GateDef(GateKind.Halve, 2)),
            new GateRow(new GateDef(GateKind.Multiply, 2), new GateDef(GateKind.Multiply, 3), new GateDef(GateKind.Halve, 2), new GateDef(GateKind.Add, 8)),
            new GateRow(new GateDef(GateKind.Multiply, 5), new GateDef(GateKind.Multiply, 2), new GateDef(GateKind.Halve, 2), new GateDef(GateKind.Multiply, 3)),
            new GateRow(new GateDef(GateKind.Multiply, 10), new GateDef(GateKind.Multiply, 3), new GateDef(GateKind.Halve, 2), new GateDef(GateKind.Add, 20)),
            // The last gate is the payoff: highest variance, so the run can still be lost or made.
            new GateRow(new GateDef(GateKind.Multiply, 100), new GateDef(GateKind.Wipe, 0), new GateDef(GateKind.Multiply, 10), new GateDef(GateKind.Wipe, 0)),
        };

        [Tooltip("Draw each row's lane order from the seed, so every run is a different course. Off keeps the authored order.")]
        public bool shuffleGatesPerRun = true;

        [Header("Count")]
        [Tooltip("Counts saturate here. Keeps the number inside what a long can hold and what a viewer can read.")]
        [Min(1000)] public long countCap = 1_000_000_000_000L;

        [Header("Body budget — the hard ceiling on physical marbles")]
        [Tooltip("Live physics bodies, never more. Past this, a multiplier makes the marble heavier instead " +
                 "of adding clones, and the displayed count keeps going.")]
        [Range(20, 1500)] public int maxBodies = 400;

        [Tooltip("Most clones a single gate crossing may add, so a x100 reads as a burst, not a wall.")]
        [Range(1, 100)] public int maxClonesPerGate = 24;

        [Header("Marble")]
        [Min(0.03f)] public float marbleRadius = 0.13f;
        [Range(0f, 1f)] public float marbleBounciness = 0.45f;
        [Range(0f, 2f)] public float gravityScale = 0.55f;
        [Range(0f, 2f)] public float linearDamping = 0.1f;
        [Min(1f)] public float maxFallSpeed = 6.5f;

        [Tooltip("A marble this slow for this long is nudged sideways (seeded), so none sits on a peg forever.")]
        [Min(0.2f)] public float stuckSeconds = 1.2f;

        [Header("Layout (rebuild the scene after changing)")]
        [Min(1f)] public float laneWidth = 2.2f;
        [Min(1f)] public float rowPitch = 2.1f;
        [Min(0.2f)] public float gateThickness = 0.55f;
        public float firstGateY = 4.9f;
        public float spawnY = 6.5f;

        [Range(0, 4)] public int pegLinesPerRow = 2;
        [Min(0.03f)] public float pegRadius = 0.12f;
        [Min(0.3f)] public float pegSpacing = 1.1f;

        [Header("Pacing")]
        [Tooltip("Seconds the final number holds after the last marble lands.")]
        [Min(0f)] public float finishHoldSeconds = 3f;

        // --- Layout helpers, shared by the scene builder and the simulation so they cannot disagree ---

        public int RowCount => rows != null ? rows.Length : 0;

        public int Lanes => RowCount > 0 && rows[0] != null && rows[0].gates != null ? rows[0].gates.Length : 0;

        public float FieldHalfWidth => Lanes * laneWidth * 0.5f;

        public float GateY(int row) => firstGateY - row * rowPitch;

        public float LaneCenterX(int lane) => -FieldHalfWidth + (lane + 0.5f) * laneWidth;

        /// <summary>Below this a marble has reached the end and its count is banked.</summary>
        public float BottomY => GateY(Mathf.Max(0, RowCount - 1)) - rowPitch * 0.9f;

        public float PegLineY(int row, int line) => GateY(row) - gateThickness * 0.5f - 0.5f - line * 0.5f;

        public int PegsInLine(int line) => Mathf.Max(1, Mathf.FloorToInt(FieldHalfWidth * 2f / pegSpacing) - (line % 2));

        public float PegX(int line, int index)
        {
            float offset = (line % 2) * pegSpacing * 0.5f;
            return -FieldHalfWidth + pegSpacing * 0.5f + offset + index * pegSpacing;
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (RowCount < 1 || Lanes < 2)
            {
                error = "needs at least one row with two or more gates.";
                return false;
            }

            for (int row = 0; row < RowCount; row++)
            {
                if (rows[row] == null || rows[row].gates == null || rows[row].gates.Length != Lanes)
                {
                    error = $"row {row + 1} must have {Lanes} gates, the same as row 1.";
                    return false;
                }

                for (int lane = 0; lane < Lanes; lane++)
                {
                    GateDef gate = rows[row].gates[lane];
                    if ((gate.kind == GateKind.Multiply && gate.value < 2) || (gate.kind == GateKind.Add && gate.value < 1))
                    {
                        error = $"row {row + 1}, gate {lane + 1}: {gate.kind} needs a value of " +
                                $"{(gate.kind == GateKind.Multiply ? 2 : 1)} or more.";
                        return false;
                    }
                }
            }

            if (maxBodies < startMarbles)
            {
                error = $"maxBodies ({maxBodies}) is below startMarbles ({startMarbles}).";
                return false;
            }

            float gap = pegSpacing - pegRadius * 2f;
            if (pegLinesPerRow > 0 && gap < marbleRadius * 2f * 1.6f)
            {
                error = $"the gap between pegs ({gap:0.00}) is too tight for the marbles " +
                        $"({marbleRadius * 2f:0.00}) -- they would wedge. Widen pegSpacing or shrink the marble.";
                return false;
            }

            float safe = ConstantSpeed2D.MaxSafeSpeed(fixedTimestep, Mathf.Min(gateThickness, pegRadius * 2f));
            if (maxFallSpeed > safe)
            {
                error = $"maxFallSpeed {maxFallSpeed:0.0} exceeds the safe ceiling {safe:0.0} at timestep " +
                        $"{fixedTimestep} -- a marble could skip through a gate or peg.";
                return false;
            }

            if (firstGateY >= spawnY)
            {
                error = "spawnY must be above the first gate.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
