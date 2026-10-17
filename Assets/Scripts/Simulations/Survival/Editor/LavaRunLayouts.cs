using System.Collections.Generic;
using SimulationLobby.Shared.Tracks;
using UnityEngine;

namespace SimulationLobby.Simulations.Survival.EditorTools
{
    /// <summary>One multiplier gate, positioned relative to a section so reshaping the track does not strand it.</summary>
    struct LavaGateSpec
    {
        public int section;
        public float offset;

        /// <summary>Sideways range in metres from the centreline. Both NaN = the whole floor.</summary>
        public float laneMin;
        public float laneMax;
        public int multiplier;
        public string label;

        public bool FullWidth => float.IsNaN(laneMin) || float.IsNaN(laneMax);
    }

    /// <summary>
    /// The three Lava Run stage courses as <see cref="TrackLayout"/> data, plus the gate positions that belong with them.
    /// Stages 1 and 2 are zig-zags that follow the contour of the volcano (concentric arcs joined by tight hairpins);
    /// stage 3 turns out of the volcano and runs a railless bridge over the lake. Written to the layout assets only
    /// when they do not exist yet; the scene builder never overwrites a hand-tuned asset.
    /// </summary>
    static class LavaRunLayouts
    {
        /// <summary>Same radius as the Race marble, so the shared obstacle kit (0.3 m pegs and walls) is the right size.</summary>
        public const float MarbleRadius = 0.11f;

        public const float TrackWidth = 3.0f;
        public const float PenDivider = 0.1f;
        public const float PenCellWidth = 1.3f;
        public const float PenWidth = 4f * PenCellWidth + 3f * PenDivider;
        public const float PenLength = 3f;
        public const float PenSlope = 7f;
        public const float FunnelSlope = 6f;
        public const float BasinWidth = 5.0f;
        public const float BasinLength = 8f;
        public const float PenRail = 0.33f;
        public const float BasinRail = 1.2f;

        /// <summary>Long enough to string the pack out and jam at the neck, short enough that falls are not pure attrition.</summary>
        public const float BridgeLength = 60f;

        /// <summary>2.2 marble diameters: wide enough to flow, narrow enough to jam.</summary>
        public const float BridgeWidth = 2.2f * 2f * MarbleRadius;

        // Section indices. Every stage starts Pens (0), Funnel (1), then its first leg / turn (2).
        public const int PensSection = 0;
        public const int FunnelSection = 1;
        public const int LeadSection = 2;

        public static int BasinSection(int stage)
        {
            return stage == 0 ? 11 : stage == 1 ? 14 : -1;
        }

        /// <summary>Stage 3 sections: Landing is the last, the Bridge the one before.</summary>
        public const int BridgeSection = 5;
        public const int LandingSection = 6;
        public const int NeckSection = 4;

        public static string[] StageNames = { "THE SLOPES", "THE SWITCHBACKS", "THE BRIDGE" };

        public static string FileName(int stage)
        {
            return $"LavaRun_Stage{stage + 1}";
        }

        public static void Apply(TrackLayout layout, int stage)
        {
            layout.version = TrackLayout.CurrentVersion;
            layout.startWidth = PenWidth;
            layout.gateDistance = PenLength;
            layout.runOutLength = 8f;
            layout.defaultRailHeight = 0.45f;
            switch (stage)
            {
                case 0:
                    layout.sections = Stage1Sections();
                    layout.obstacles = Stage1Obstacles();
                    break;
                case 1:
                    layout.sections = Stage2Sections();
                    layout.obstacles = Stage2Obstacles();
                    break;
                default:
                    layout.sections = Stage3Sections();
                    layout.obstacles = Stage3Obstacles();
                    break;
            }
        }

        // ------------------------------------------------------------------ stage 1: THE SLOPES

        /// <summary>Slope of the gentle part of a leg, and of the part that carries pegs, baffles and pinwheels.</summary>
        public const float EasySlope = 3f;
        public const float ObstacleSlope = 5f;
        public const float HairpinSlope = 3f;

        /// <summary>Legs and run-ins are laterally level so the pack spreads across the width; hairpins lean just a little.</summary>
        public const float HairpinBank = 1.5f;

        /// <summary>
        /// Peg pitch across the track. Every peg field is a 3-row pyramid (rows of 1, 2, 3 pegs): its widest row puts the outer
        /// peg 0.60 m off the centre line, leaving 0.85 m to each rail on a 3 m track (rule: at least 0.55 m, 2.5 marble diameters)
        /// and 0.50 m between pegs. A 4-row pyramid also fits at this pitch (outer peg 0.90 m out, 0.55 m to the rail).
        /// </summary>
        public const float PegSpacing = 0.6f;

        public const int PegRows = 3;

        /// <summary>
        /// Every peg pyramid is preceded by a funnel (ObstacleKind.Funnel): two guide walls close in from the rails over
        /// <see cref="FunnelTaper"/> m to a <see cref="FunnelGap"/> m exit (about 6 marble diameters, wide enough that a big
        /// pack cannot arch in it), so the pack reaches the pyramid's single apex peg concentrated on the centre line. The funnel
        /// starts 3.4 m before its pyramid, which puts the exit 0.4 m short of the apex peg. The pyramid does the spreading.
        /// </summary>
        public const float FunnelTaper = 3f;
        public const float FunnelGap = 1.4f;

        /// <summary>Pinwheel arm length on the 3 m track: 0.35 m (1.6 marble diameters) from tip to rail.</summary>
        public const float PinwheelArm = 1.15f;

        /// <summary>The fork pinwheel's arm: 0.36 m from tip to the divider (see <see cref="ForkWidth"/>).</summary>
        public const float ForkPinwheelArm = 1.0f;

        /// <summary>
        /// The fork widens the track to 4.4 m so both lanes stay clear: divider 1.4 m right of centre leaves a 0.76 m safe lane
        /// (3.4 marble diameters, no two-abreast arch), a 3.56 m left lane, and 0.36 m between the pinwheel tip (1.0 m arms
        /// on the centre line) and the divider (1.4 m - 0.04 m half-thickness - 1.0 m; 1.6 marble diameters, enough not to pinch).
        /// </summary>
        public const float ForkWidth = 4.4f;
        public const float ForkDividerOffset = 1.4f;

        static TrackSection Wider(TrackSection section, float widthEnd)
        {
            section.widthEnd = widthEnd;
            return section;
        }

        public static int ExpectedSectionCount(int stage)
        {
            return stage == 0 ? 12 : stage == 1 ? 15 : 7;
        }

        // Each leg is two arcs of the same radius (so it stays concentric): a short run-in, then the stretch with the obstacles, which
        // starts about 10-15 m after the previous hairpin or the funnel so the first pyramid meets a pack still bunched mid-track.
        static TrackSection[] Stage1Sections()
        {
            return new[]
            {
                Straight("Pens", PenLength, PenSlope, 0f, PenRail),
                Straight("Funnel", 8f, FunnelSlope, TrackWidth, 0.45f),
                Curve("Switchback 1 (run-in)", 36f, 15f, EasySlope, 0f),
                Curve("Switchback 1 (obstacles)", 36f, 75f, ObstacleSlope, 0f),
                Curve("Hairpin 1", 6f, -180f, HairpinSlope, HairpinBank),
                Wider(Curve("Switchback 2 (run-in)", 48f, -15f, EasySlope, 0f), ForkWidth),
                Wider(Curve("Switchback 2 (obstacles)", 48f, -75f, ObstacleSlope, 0f), ForkWidth),
                Wider(Curve("Hairpin 2", 6f, 180f, HairpinSlope, HairpinBank), TrackWidth),
                Curve("Switchback 3 (run-in)", 60f, 10f, EasySlope, 0f),
                Curve("Switchback 3 (obstacles)", 60f, 80f, ObstacleSlope, 0f),
                Straight("Flare", 6f, 0.8f, BasinWidth, 0.9f),
                Straight("Basin", BasinLength, 0f, 0f, BasinRail)
            };
        }

        static TrackObstacle[] Stage1Obstacles()
        {
            return new[]
            {
                Obstacle("Funnel to Peg garden A", ObstacleKind.Funnel, 3, 0.6f, FunnelTaper, 0, 0f, FunnelGap, 0f, 0, 0f),
                Obstacle("Peg garden A", ObstacleKind.PegPyramid, 3, 4f, 0f, PegRows, PegSpacing, 0.05f, 0f, 0, 0f),
                Obstacle("Pinwheel A", ObstacleKind.Pinwheel, 3, 16f, 0f, 3, 0f, PinwheelArm, 110f, 0, 0f),
                Obstacle("Funnel to Pegs entry", ObstacleKind.Funnel, 6, 0.6f, FunnelTaper, 0, 0f, FunnelGap, 0f, 0, 0f),
                Obstacle("Pegs entry", ObstacleKind.PegPyramid, 6, 4f, 0f, PegRows, PegSpacing, 0.05f, 0f, 0, 0f),
                // The fork: a divider shifted right so the left lane is wide (x3 gate + pinwheel) and the right is a narrow safe lane (x1).
                Obstacle("Fork divider", ObstacleKind.Divider, 6, 14f, 9f, 0, 0f, 0.08f, 0f, 0, ForkDividerOffset),
                Obstacle("Fork pinwheel", ObstacleKind.Pinwheel, 6, 20f, 0f, 3, 0f, ForkPinwheelArm, 120f, 0, 0f),
                Obstacle("Funnel to Pegs pyramid", ObstacleKind.Funnel, 6, 36.6f, FunnelTaper, 0, 0f, FunnelGap, 0f, 0, 0f),
                Obstacle("Pegs pyramid", ObstacleKind.PegPyramid, 6, 40f, 0f, PegRows, PegSpacing, 0.05f, 0f, 0, 0f),
                Obstacle("Funnel to Peg garden C", ObstacleKind.Funnel, 9, 0.6f, FunnelTaper, 0, 0f, FunnelGap, 0f, 0, 0f),
                Obstacle("Peg garden C", ObstacleKind.PegPyramid, 9, 4f, 0f, PegRows, PegSpacing, 0.05f, 0f, 0, 0f),
                Obstacle("Pinwheel", ObstacleKind.Pinwheel, 9, 28f, 0f, 3, 0f, PinwheelArm, -130f, 0, 0f),
                Obstacle("Pinwheel B", ObstacleKind.Pinwheel, 9, 46f, 0f, 3, 0f, PinwheelArm, 125f, 0, 0f)
            };
        }

        // ------------------------------------------------------------------ stage 2: THE SWITCHBACKS

        static TrackSection[] Stage2Sections()
        {
            return new[]
            {
                Straight("Pens", PenLength, PenSlope, 0f, PenRail),
                Straight("Funnel", 8f, FunnelSlope, TrackWidth, 0.45f),
                Curve("Switchback 1 (run-in)", 66f, -8f, EasySlope, 0f),
                Curve("Switchback 1 (obstacles)", 66f, -52f, ObstacleSlope, 0f),
                Curve("Hairpin 1", 4f, 180f, HairpinSlope, HairpinBank),
                Curve("Switchback 2 (run-in)", 74f, 8f, EasySlope, 0f),
                Curve("Switchback 2 (obstacles)", 74f, 52f, ObstacleSlope, 0f),
                Curve("Hairpin 2", 4f, -180f, HairpinSlope, HairpinBank),
                Curve("Switchback 3 (run-in)", 82f, -8f, EasySlope, 0f),
                Curve("Switchback 3 (obstacles)", 82f, -52f, ObstacleSlope, 0f),
                Curve("Hairpin 3", 4f, 180f, HairpinSlope, HairpinBank),
                Curve("Switchback 4 (run-in)", 90f, 8f, EasySlope, 0f),
                Curve("Switchback 4 (obstacles)", 90f, 52f, ObstacleSlope, 0f),
                Straight("Flare", 6f, 0.8f, BasinWidth, 0.9f),
                Straight("Basin", BasinLength, 0f, 0f, BasinRail)
            };
        }

        static TrackObstacle[] Stage2Obstacles()
        {
            return new[]
            {
                Obstacle("Funnel to Peg garden A", ObstacleKind.Funnel, 3, 0.6f, FunnelTaper, 0, 0f, FunnelGap, 0f, 0, 0f),
                Obstacle("Peg garden A", ObstacleKind.PegPyramid, 3, 4f, 0f, PegRows, PegSpacing, 0.05f, 0f, 0, 0f),
                Obstacle("Pinwheel A", ObstacleKind.Pinwheel, 3, 22f, 0f, 3, 0f, PinwheelArm, 120f, 0, 0f),
                Obstacle("Funnel to Pegs entry", ObstacleKind.Funnel, 6, 0.6f, FunnelTaper, 0, 0f, FunnelGap, 0f, 0, 0f),
                Obstacle("Pegs entry", ObstacleKind.PegPyramid, 6, 4f, 0f, PegRows, PegSpacing, 0.05f, 0f, 0, 0f),
                Obstacle("Bottleneck A", ObstacleKind.Bottleneck, 6, 14f, 10f, 0, 0f, 0.7f, 0f, 0, 0f),
                Obstacle("Pinwheel B", ObstacleKind.Pinwheel, 9, 18f, 0f, 3, 0f, PinwheelArm, -140f, 0, 0f),
                Obstacle("Bottleneck B", ObstacleKind.Bottleneck, 9, 36f, 12f, 0, 0f, 0.9f, 0f, 0, 0f),
                Obstacle("Funnel to Peg garden B", ObstacleKind.Funnel, 12, 0.6f, FunnelTaper, 0, 0f, FunnelGap, 0f, 0, 0f),
                Obstacle("Peg garden B", ObstacleKind.PegPyramid, 12, 4f, 0f, PegRows, PegSpacing, 0.05f, 0f, 0, 0f),
                Obstacle("Pinwheel C", ObstacleKind.Pinwheel, 12, 20f, 0f, 3, 0f, PinwheelArm, 130f, 0, 0f)
            };
        }

        // ------------------------------------------------------------------ stage 3: THE BRIDGE

        static TrackSection[] Stage3Sections()
        {
            return new[]
            {
                Straight("Pens", PenLength, PenSlope, 0f, PenRail),
                Straight("Funnel", 8f, FunnelSlope, TrackWidth, 0.45f),
                Curve("Turn out", 14f, 50f, 3f, 0f),
                Straight("Approach", 32f, 3f, 0f, 0f),
                Straight("Neck", 10f, 3f, BridgeWidth, 0f),
                Railless("Bridge", BridgeLength, 3f),
                Railless("Landing", 3f, 0f)
            };
        }

        static TrackObstacle[] Stage3Obstacles()
        {
            return new TrackObstacle[0];
        }

        // ------------------------------------------------------------------ gates

        public static List<LavaGateSpec> Gates(int stage)
        {
            var list = new List<LavaGateSpec>();
            switch (stage)
            {
                case 0:
                    list.Add(Gate(2, 5f, 2));
                    list.Add(Gate(3, 30f, 2));
                    // The fork pair: the wide lane (left of the divider) pays x3 behind a pinwheel, the narrow lane is a safe x1.
                    list.Add(Gate(6, 16.5f, 3, -2.15f, 1.32f));
                    list.Add(Gate(6, 16.5f, 1, 1.46f, 2.15f));
                    list.Add(Gate(6, 50f, 2));
                    list.Add(Gate(8, 6f, 2));
                    break;
                case 1:
                    list.Add(Gate(2, 5f, 2));
                    list.Add(Gate(5, 5f, 2));
                    list.Add(Gate(6, 50f, 2));
                    list.Add(Gate(8, 6f, 2));
                    list.Add(Gate(11, 6f, 2));
                    break;
                default:
                    list.Add(Gate(3, 25f, 5)); // a few metres before the neck (section 4)
                    break;
            }

            return list;
        }

        static LavaGateSpec Gate(int section, float offset, int multiplier)
        {
            return Gate(section, offset, multiplier, float.NaN, float.NaN);
        }

        static LavaGateSpec Gate(int section, float offset, int multiplier, float laneMin, float laneMax)
        {
            return new LavaGateSpec
            {
                section = section,
                offset = offset,
                laneMin = laneMin,
                laneMax = laneMax,
                multiplier = multiplier,
                label = $"x{multiplier}"
            };
        }

        // ------------------------------------------------------------------ builders

        static TrackSection Straight(string label, float length, float slope, float widthEnd, float rail)
        {
            return new TrackSection
            {
                label = label,
                kind = TrackSectionKind.Straight,
                length = length,
                slopeDegrees = slope,
                widthEnd = widthEnd,
                railHeight = rail
            };
        }

        static TrackSection Curve(string label, float radius, float arc, float slope, float bank)
        {
            return new TrackSection
            {
                label = label,
                kind = TrackSectionKind.Curve,
                radius = radius,
                arcDegrees = arc,
                slopeDegrees = slope,
                bankDegrees = bank
            };
        }

        static TrackSection Railless(string label, float length, float slope)
        {
            return new TrackSection
            {
                label = label,
                kind = TrackSectionKind.Straight,
                length = length,
                slopeDegrees = slope,
                railless = true
            };
        }

        static TrackObstacle Obstacle(string label, ObstacleKind kind, int section, float offset, float length, int count,
            float spacing, float size, float speed, int lane, float lateral)
        {
            return new TrackObstacle
            {
                label = label,
                kind = kind,
                section = section,
                offset = offset,
                length = length,
                count = count,
                spacing = spacing,
                size = size,
                speed = speed,
                lane = lane,
                lateral = lateral
            };
        }
    }
}
