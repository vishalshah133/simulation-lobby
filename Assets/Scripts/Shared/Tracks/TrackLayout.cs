using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SimulationLobby.Shared.Tracks
{
    [MovedFrom(true, "SimulationLobby.Simulations.Race", "SimulationLobby.Simulations.Race", "TrackSectionKind")]
    public enum TrackSectionKind
    {
        /// <summary>A straight chute. Width can ease to <see cref="TrackSection.widthEnd"/> (a pinch or a flare).</summary>
        Straight = 0,

        /// <summary>A banked arc of <see cref="TrackSection.arcDegrees"/> at <see cref="TrackSection.radius"/>. Past 360 degrees it is a helix.</summary>
        Curve = 1
    }

    /// <summary>One piece of track, laid end to end from the previous one.</summary>
    [Serializable]
    [MovedFrom(true, "SimulationLobby.Simulations.Race", "SimulationLobby.Simulations.Race", "TrackSection")]
    public struct TrackSection
    {
        public string label;
        public TrackSectionKind kind;

        [Tooltip("Straight: length in metres along the slope. Ignored for curves (arc length comes from radius and angle).")]
        public float length;

        [Tooltip("Curve: centreline radius in metres.")]
        public float radius;

        [Tooltip("Curve: degrees turned. Positive turns left, negative turns right. 720 is a two-loop helix.")]
        public float arcDegrees;

        [Tooltip("Descent along the path, in degrees. The main speed control.")]
        public float slopeDegrees;

        [Tooltip("Curve: inward banking at the middle of the arc. Eased in and out so sections join flat.")]
        public float bankDegrees;

        [Tooltip("Track width at the end of this section, in metres. 0 keeps the current width.")]
        public float widthEnd;

        [Tooltip("Rail height in metres for this section. 0 uses the layout's default rail height.")]
        public float railHeight;

        [Tooltip("No rails on this section: a bare floor you can fall off (a bridge). Overrides railHeight.")]
        public bool railless;
    }

    [MovedFrom(true, "SimulationLobby.Simulations.Race", "SimulationLobby.Simulations.Race", "ObstacleKind")]
    public enum ObstacleKind
    {
        /// <summary>Triangle of pegs, apex upstream: each row one peg wider. count = rows, spacing = peg gap, size = peg radius.</summary>
        PegPyramid = 0,

        /// <summary>Plinko grid, alternate rows offset. count = rows, spacing = peg gap, size = peg radius.</summary>
        PegGrid = 1,

        /// <summary>A rotating bar assembly across the track. count = arms, size = arm length (0 fits the width), speed = degrees/second (negative reverses).</summary>
        Pinwheel = 2,

        /// <summary>A lengthwise wall that splits the track into two lanes (a fork). length = wall length, size = thickness, lateral = sideways offset (makes one lane narrower).</summary>
        Divider = 3,

        /// <summary>Walls close in to a gap and open out again. length = total length, size = gap width.</summary>
        Bottleneck = 4,

        /// <summary>Alternating baffles from each wall that force a zig-zag. count = baffles, spacing = distance between them, size = reach as a fraction of the width.</summary>
        Chicane = 5,

        /// <summary>Low rounded humps across the track that make marbles hop. count = humps, spacing = distance between them, size = hump height in metres (0.03 default; 0.06 stops a marble dead).</summary>
        Bumps = 6,

        /// <summary>Two guide walls close in from the rails and stop at a gap, concentrating the pack on the centre line (no re-opening). length = taper length, size = exit gap width.</summary>
        Funnel = 7
    }

    /// <summary>
    /// A hazard placed on the track. Positions are relative to a section (section index plus metres from its
    /// start), so reshaping the track does not strand the obstacles. Which fields apply depends on
    /// <see cref="ObstacleKind"/>; the unused ones are ignored.
    /// </summary>
    [Serializable]
    [MovedFrom(true, "SimulationLobby.Simulations.Race", "SimulationLobby.Simulations.Race", "TrackObstacle")]
    public struct TrackObstacle
    {
        public string label;
        public ObstacleKind kind;

        [Tooltip("Index into the sections list.")]
        public int section;

        [Tooltip("Metres from the start of that section.")]
        public float offset;

        public float length;
        public int count;
        public float spacing;
        public float size;
        public float speed;

        [Tooltip("Pegs and bumps only: -1 left half, 0 full width, +1 right half. For hazards that live in one lane of a fork.")]
        public int lane;

        [Tooltip("Divider: sideways shift in metres. Positive moves it right, so the right lane is narrower.")]
        public float lateral;
    }

    /// <summary>
    /// The shape of a race track as data: an ordered list of sections plus the obstacles on them. The scene
    /// builder sweeps this into meshes, colliders and a centreline path. Editing it means rebuilding the
    /// scene, since the meshes are baked.
    /// </summary>
    [CreateAssetMenu(fileName = "TrackLayout", menuName = "Simulation Lobby/Configs/Track Layout", order = 31)]
    [MovedFrom(true, "SimulationLobby.Simulations.Race", "SimulationLobby.Simulations.Race", "RaceTrackLayout")]
    public sealed class TrackLayout : ScriptableObject
    {
        /// <summary>Bump when the default course changes shape. The scene builder upgrades older assets.</summary>
        public const int CurrentVersion = 3;

        [Tooltip("Written by the scene builder. Older than the current default means the course is rebuilt from the default.")]
        public int version;

        [Min(0.5f)] public float startWidth = 1.6f;

        [Tooltip("Where the grid sits and the gate closes, in metres along the first section.")]
        [Min(0.5f)] public float gateDistance = 4.4f;

        [Tooltip("Metres before the end of the course where the finish line is drawn. The sections after it are the run-out.")]
        [Min(1f)] public float runOutLength = 8f;

        [Tooltip("Pegs only: the gap in metres kept between the rails and the nearest peg's surface. Set it wider " +
                 "than a marble (about 1.6 diameters) so a marble can't wedge between the outer peg and the wall. " +
                 "0 = the original packing, which keeps older courses (and their logged seeds) unchanged.")]
        [Min(0f)] public float pegWallClearance;

        [Tooltip("Rail height in metres for sections that do not set their own. 0 or less means 0.34.")]
        public float defaultRailHeight = 0.34f;

        public TrackSection[] sections = DefaultSections();
        public TrackObstacle[] obstacles = DefaultObstacles();

        /// <summary>Rail height used when neither the section nor the layout sets one.</summary>
        public const float FallbackRailHeight = 0.34f;

        /// <summary>The rail height in force on a section: 0 when railless, else its override, else the layout default.</summary>
        public float RailHeightFor(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= sections.Length)
            {
                return defaultRailHeight > 0f ? defaultRailHeight : FallbackRailHeight;
            }

            TrackSection section = sections[sectionIndex];
            if (section.railless)
            {
                return 0f;
            }

            if (section.railHeight > 0f)
            {
                return section.railHeight;
            }

            return defaultRailHeight > 0f ? defaultRailHeight : FallbackRailHeight;
        }

        [ContextMenu("Reset To Default Course")]
        public void ResetToDefault()
        {
            startWidth = 1.6f;
            defaultRailHeight = FallbackRailHeight;
            gateDistance = 4.4f;
            runOutLength = 8f;
            sections = DefaultSections();
            obstacles = DefaultObstacles();
            version = CurrentVersion;
        }

        // Section indices used by DefaultObstacles. Keep the two lists in step.
        const int Pen = 0, OpeningRun = 1, SweeperLeft = 2, GardenFlare = 3, PegGarden = 4, GardenExit = 5,
            SweeperRight = 6, SpinLink = 7, Helix = 8, ForkFlare = 9, Fork = 10, ForkExit = 11,
            ReleaseTurn = 12, LongRun = 13, FinalTurn = 14, HomeStraight = 15, RunOut = 16;

        public static TrackSection[] DefaultSections()
        {
            return new[]
            {
                S("Start pen",       TrackSectionKind.Straight, 5f,  0f,   0f,    4f, 0f,  0f),
                S("Opening run",     TrackSectionKind.Straight, 20f, 0f,   0f,    7f, 0f,  0f),
                S("Sweeper left",    TrackSectionKind.Curve,    0f,  5f,   180f,  6f, 10f, 0f),
                S("Garden flare",    TrackSectionKind.Straight, 8f,  0f,   0f,    6f, 0f,  3.4f),
                S("Peg garden",      TrackSectionKind.Straight, 16f, 0f,   0f,    5f, 0f,  0f),
                S("Garden exit",     TrackSectionKind.Straight, 8f,  0f,   0f,    5f, 0f,  1.5f),
                S("Sweeper right",   TrackSectionKind.Curve,    0f,  5f,   -180f, 6f, 10f, 0f),
                S("Spinner link",    TrackSectionKind.Straight, 14f, 0f,   0f,    6f, 0f,  0f),
                S("Helix",           TrackSectionKind.Curve,    0f,  3.4f, 720f,  8f, 15f, 1.3f),
                S("Fork flare",      TrackSectionKind.Straight, 6f,  0f,   0f,    5f, 0f,  2.1f),
                S("Fork",            TrackSectionKind.Straight, 14f, 0f,   0f,    5f, 0f,  0f),
                S("Fork exit",       TrackSectionKind.Straight, 8f,  0f,   0f,    5f, 0f,  1.4f),
                S("Release turn",    TrackSectionKind.Curve,    0f,  6f,   -150f, 6f, 8f,  1.5f),
                S("Long run",        TrackSectionKind.Straight, 24f, 0f,   0f,    7f, 0f,  0f),
                S("Final bowl turn", TrackSectionKind.Curve,    0f,  4.5f, 270f,  7f, 12f, 0f),
                S("Home straight",   TrackSectionKind.Straight, 26f, 0f,   0f,    7f, 0f,  0f),
                S("Run-out",         TrackSectionKind.Straight, 8f,  0f,   0f,    3f, 0f,  0f)
            };
        }

        public static TrackObstacle[] DefaultObstacles()
        {
            return new[]
            {
                O("Rumble strip",   ObstacleKind.Bumps,      OpeningRun,  7f,  0f,   6, 0.9f,  0.03f,  0f,   0, 0f),
                O("Pegs pyramid",   ObstacleKind.PegPyramid, PegGarden,   1f,  0f,   6, 0.46f, 0.05f,  0f,   0, 0f),
                O("Pegs plinko",    ObstacleKind.PegGrid,    PegGarden,   8.5f, 0f,  5, 0.50f, 0.05f,  0f,   0, 0f),
                O("Pinwheel",       ObstacleKind.Pinwheel,   SpinLink,    6f,  0f,   3, 0f,    0f,     110f, 0, 0f),
                O("Fork divider",   ObstacleKind.Divider,    Fork,        1.5f, 9f,  0, 0f,    0.08f,  0f,   0, 0.22f),
                O("Fork bumps",     ObstacleKind.Bumps,      Fork,        3f,  0f,   5, 0.8f,  0.03f,  0f,   1, 0f),
                O("Baffles",        ObstacleKind.Chicane,    LongRun,     3f,  0f,   4, 2.6f,  0.58f,  0f,   0, 0f),
                O("Bottleneck",     ObstacleKind.Bottleneck, LongRun,     17.5f, 4f, 0, 0f,    0.5f,   0f,   0, 0f),
                O("Reverse spinner", ObstacleKind.Pinwheel,  HomeStraight, 9f, 0f,   2, 0f,    0f,     -140f, 0, 0f)
            };
        }

        static TrackSection S(string label, TrackSectionKind kind, float length, float radius, float arc,
            float slope, float bank, float widthEnd)
        {
            return new TrackSection
            {
                label = label,
                kind = kind,
                length = length,
                radius = radius,
                arcDegrees = arc,
                slopeDegrees = slope,
                bankDegrees = bank,
                widthEnd = widthEnd
            };
        }

        static TrackObstacle O(string label, ObstacleKind kind, int section, float offset, float length, int count,
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
