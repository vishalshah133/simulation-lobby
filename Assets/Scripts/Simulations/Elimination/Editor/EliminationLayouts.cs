using SimulationLobby.Shared.Tracks;

namespace SimulationLobby.Simulations.Elimination.EditorTools
{
    /// <summary>
    /// The five elimination-show courses as <see cref="TrackLayout"/> data (heat 1 reuses the Grand Prix layout).
    /// Each has one signature obstacle set and a different silhouette from above, so no two heats look alike:
    /// PEG FALL a long straight waterfall of pegs, LAST CHANCE a banked serpentine, PINWHEELS a staircase of
    /// right angles, THE HELIX two towers with a fork between, THE GAUNTLET the longest, with one of everything.
    /// Written to the layout assets only when they do not exist (or by the Reset tools item); the scene builder
    /// never overwrites a hand-tuned asset.
    /// </summary>
    static class EliminationLayouts
    {
        /// <summary>Layout index 0..4 = PEG FALL, LAST CHANCE, PINWHEELS, THE HELIX, THE GAUNTLET.</summary>
        public const int Count = 5;

        public static readonly string[] Titles = { "PEG FALL", "LAST CHANCE", "PINWHEELS", "THE HELIX", "THE GAUNTLET" };
        public static readonly string[] FileNames =
        {
            "ElimTrack_PegFall", "ElimTrack_LastChance", "ElimTrack_Pinwheels", "ElimTrack_Helix", "ElimTrack_Gauntlet"
        };

        /// <summary>Short signature description, logged by the builder.</summary>
        public static readonly string[] Signatures =
        {
            "plinko grid + peg pyramids on a wide straight waterfall",
            "banked serpentine, fast, with a bottleneck",
            "four pinwheels + a chicane on a staircase of right angles",
            "two 720-degree helix towers with a fork divider between",
            "helix, pegs, two pinwheels, bottleneck and bumps in one long run"
        };

        /// <summary>Rail-to-peg gap: ~1.8 marble diameters, so no marble can wedge beside an outer peg.</summary>
        public const float PegWallClearance = 0.4f;

        /// <summary>Peg sections fall like a plinko board: steep enough that marbles cascade through instead of piling up.</summary>
        public const float PegSlope = 12f;

        public const string GrandPrixFileName = "ElimTrack_GrandPrix";

        /// <summary>
        /// Heat 1's course: the race Grand Prix (TrackLayout defaults) with the peg garden made a plinko field. The
        /// published RaceTrackLayout_GrandPrix asset is never touched, so race-001's seed still replays.
        /// </summary>
        public static void ApplyGrandPrix(TrackLayout layout)
        {
            layout.version = TrackLayout.CurrentVersion;
            layout.startWidth = 1.6f;
            layout.gateDistance = 4.4f;
            layout.runOutLength = 8f;
            layout.defaultRailHeight = TrackLayout.FallbackRailHeight;
            layout.pegWallClearance = PegWallClearance;
            layout.sections = TrackLayout.DefaultSections();
            layout.obstacles = TrackLayout.DefaultObstacles();
            for (int i = 0; i < layout.sections.Length; i++)
            {
                if (layout.sections[i].label == "Peg garden")
                {
                    layout.sections[i].slopeDegrees = PegSlope;
                }
            }

            for (int i = 0; i < layout.obstacles.Length; i++)
            {
                if (layout.obstacles[i].kind == ObstacleKind.PegPyramid)
                {
                    layout.obstacles[i].count = 5;
                    layout.obstacles[i].spacing = 0.65f;
                }
                else if (layout.obstacles[i].kind == ObstacleKind.PegGrid)
                {
                    layout.obstacles[i].spacing = 0.70f;
                }
            }
        }

        public static void Apply(TrackLayout layout, int index)
        {
            layout.version = TrackLayout.CurrentVersion;
            layout.startWidth = 1.6f;
            layout.gateDistance = 4.4f;
            layout.runOutLength = 8f;
            layout.defaultRailHeight = TrackLayout.FallbackRailHeight;
            layout.pegWallClearance = PegWallClearance;
            switch (index)
            {
                case 0: PegFall(layout); break;
                case 1: LastChance(layout); break;
                case 2: Pinwheels(layout); break;
                case 3: Helix(layout); break;
                default: Gauntlet(layout); break;
            }
        }

        // ------------------------------------------------------------------ PEG FALL

        static void PegFall(TrackLayout l)
        {
            l.sections = new[]
            {
                S("Start pen",     TrackSectionKind.Straight, 5f,  0f, 0f, 4f, 0f, 0f),     // 0
                S("Opening run",   TrackSectionKind.Straight, 10f, 0f, 0f, 7f, 0f, 0f),     // 1
                S("Waterfall flare", TrackSectionKind.Straight, 8f, 0f, 0f, 7f, 0f, 4.4f),   // 2
                S("Pyramid field", TrackSectionKind.Straight, 24f, 0f, 0f, PegSlope, 0f, 0f),     // 3
                S("Plinko field",  TrackSectionKind.Straight, 22f, 0f, 0f, PegSlope, 0f, 0f),     // 4
                S("Waterfall neck", TrackSectionKind.Straight, 8f, 0f, 0f, 6f, 0f, 1.9f),   // 5
                S("Jog left",      TrackSectionKind.Curve,    0f,  5f, 70f, 6f, 8f, 0f),     // 6
                S("Jog right",     TrackSectionKind.Curve,    0f,  5f, -70f, 6f, 8f, 0f),    // 7
                S("Lower flare",   TrackSectionKind.Straight, 6f, 0f, 0f, 6f, 0f, 3.2f),     // 8
                S("Lower pegs",    TrackSectionKind.Straight, 14f, 0f, 0f, PegSlope, 0f, 0f),     // 9
                S("Home straight", TrackSectionKind.Straight, 10f, 0f, 0f, 6f, 0f, 1.8f),   // 10
                S("Run-out",       TrackSectionKind.Straight, 8f,  0f, 0f, 3f, 0f, 0f)      // 11
            };
            l.obstacles = new[]
            {
                O("Rumble strip",    ObstacleKind.Bumps,      1,  4f,  0f, 4, 0.9f,  0.03f, 0f, 0, 0f),
                O("Pegs pyramid A",  ObstacleKind.PegPyramid, 3,  2f,    0f, 6,  0.70f, 0.05f, 0f, 0, 0f),
                O("Pegs pyramid C",  ObstacleKind.PegPyramid, 3,  14f,   0f, 6,  0.70f, 0.05f, 0f, 0, 0f),
                O("Plinko grid",     ObstacleKind.PegGrid,    4,  4f,    0f, 10, 0.75f, 0.05f, 0f, 0, 0f),
                O("Lower pyramid",   ObstacleKind.PegPyramid, 9,  2f,    0f, 5,  0.70f, 0.05f, 0f, 0, 0f)
            };
        }

        // ------------------------------------------------------------------ LAST CHANCE

        static void LastChance(TrackLayout l)
        {
            l.sections = new[]
            {
                S("Start pen",     TrackSectionKind.Straight, 5f,  0f, 0f,    4f, 0f,  0f),   // 0
                S("Opening run",   TrackSectionKind.Straight, 12f, 0f, 0f,    8f, 0f,  0f),   // 1
                S("Bank left",     TrackSectionKind.Curve,    0f,  6f, 160f,  8f, 20f, 0f),   // 2
                S("Link",          TrackSectionKind.Straight, 8f,  0f, 0f,    8f, 0f,  0f),   // 3
                S("Bank right",    TrackSectionKind.Curve,    0f,  6f, -170f, 8f, 20f, 0f),   // 4
                S("Pinch run",     TrackSectionKind.Straight, 12f, 0f, 0f,    8f, 0f,  0f),   // 5
                S("Bank left 2",   TrackSectionKind.Curve,    0f,  7f, 150f,  8f, 20f, 0f),   // 6
                S("Home straight", TrackSectionKind.Straight, 14f, 0f, 0f,    7f, 0f,  0f),   // 7
                S("Run-out",       TrackSectionKind.Straight, 8f,  0f, 0f,    3f, 0f,  0f)    // 8
            };
            l.obstacles = new[]
            {
                O("Rumble strip", ObstacleKind.Bumps,      1, 5f,  0f, 4, 0.9f, 0.03f, 0f, 0, 0f),
                O("Bottleneck",   ObstacleKind.Bottleneck, 5, 3f,  9f, 0, 0f,   0.46f, 0f, 0, 0f),
                O("Exit bumps",   ObstacleKind.Bumps,      7, 4f,  0f, 4, 0.9f, 0.03f, 0f, 0, 0f)
            };
        }

        // ------------------------------------------------------------------ PINWHEELS

        static void Pinwheels(TrackLayout l)
        {
            l.sections = new[]
            {
                S("Start pen",      TrackSectionKind.Straight, 5f,  0f, 0f,   4f, 0f,  0f),    // 0
                S("Opening run",    TrackSectionKind.Straight, 10f, 0f, 0f,   6f, 0f,  2.4f),  // 1
                S("Wheel hall A",   TrackSectionKind.Straight, 14f, 0f, 0f,   5f, 0f,  0f),    // 2
                S("Turn left",      TrackSectionKind.Curve,    0f,  5f, 90f,  5f, 10f, 0f),    // 3
                S("Chicane hall",   TrackSectionKind.Straight, 16f, 0f, 0f,   5f, 0f,  0f),    // 4
                S("Turn right",     TrackSectionKind.Curve,    0f,  5f, -90f, 5f, 10f, 0f),    // 5
                S("Wheel hall B",   TrackSectionKind.Straight, 14f, 0f, 0f,   5f, 0f,  0f),    // 6
                S("Turn right 2",   TrackSectionKind.Curve,    0f,  5f, -90f, 5f, 10f, 0f),    // 7
                S("Wheel hall C",   TrackSectionKind.Straight, 14f, 0f, 0f,   5f, 0f,  0f),    // 8
                S("Turn left 2",    TrackSectionKind.Curve,    0f,  5f, 90f,  5f, 10f, 0f),    // 9
                S("Home straight",  TrackSectionKind.Straight, 12f, 0f, 0f,   5f, 0f,  0f),    // 10
                S("Run-out",        TrackSectionKind.Straight, 8f,  0f, 0f,   3f, 0f,  0f)     // 11
            };
            l.obstacles = new[]
            {
                O("Wheel A",        ObstacleKind.Pinwheel, 2,  6f,  0f, 3, 0f,   0f,    110f, 0, 0f),
                O("Baffles",        ObstacleKind.Chicane,  4,  2f,  0f, 4, 3.2f, 0.55f, 0f,   0, 0f),
                O("Wheel B",        ObstacleKind.Pinwheel, 6,  6f,  0f, 4, 0f,   0f,    -125f, 0, 0f),
                O("Wheel C",        ObstacleKind.Pinwheel, 8,  4f,  0f, 3, 0f,   0f,    140f, 0, 0f),
                O("Wheel D",        ObstacleKind.Pinwheel, 10, 8f,  0f, 2, 0f,   0f,    -160f, 0, 0f)
            };
        }

        // ------------------------------------------------------------------ THE HELIX

        static void Helix(TrackLayout l)
        {
            l.sections = new[]
            {
                S("Start pen",     TrackSectionKind.Straight, 5f,  0f,   0f,    4f, 0f,  0f),    // 0
                S("Opening run",   TrackSectionKind.Straight, 10f, 0f,   0f,    7f, 0f,  0f),    // 1
                S("Tower one",     TrackSectionKind.Curve,    0f,  4.2f, 720f,  9f, 16f, 1.5f),  // 2
                S("Fork flare",    TrackSectionKind.Straight, 6f,  0f,   0f,    6f, 0f,  2.2f),  // 3
                S("Fork",          TrackSectionKind.Straight, 14f, 0f,   0f,    6f, 0f,  0f),    // 4
                S("Fork exit",     TrackSectionKind.Straight, 8f,  0f,   0f,    6f, 0f,  1.5f),  // 5
                S("Tower two",     TrackSectionKind.Curve,    0f,  3.2f, -720f, 9f, 16f, 1.4f),  // 6
                S("Home straight", TrackSectionKind.Straight, 12f, 0f,   0f,    6f, 0f,  1.8f),  // 7
                S("Run-out",       TrackSectionKind.Straight, 8f,  0f,   0f,    3f, 0f,  0f)     // 8
            };
            l.obstacles = new[]
            {
                O("Fork divider", ObstacleKind.Divider, 4, 1.5f, 11f, 0, 0f,   0.08f, 0f, 0, 0.22f),
                O("Fork bumps",   ObstacleKind.Bumps,   4, 3f,   0f,  5, 0.8f, 0.03f, 0f, 1, 0f),
                O("Exit bumps",   ObstacleKind.Bumps,   7, 5f,   0f,  4, 0.9f, 0.03f, 0f, 0, 0f)
            };
        }

        // ------------------------------------------------------------------ THE GAUNTLET

        static void Gauntlet(TrackLayout l)
        {
            l.sections = new[]
            {
                S("Start pen",      TrackSectionKind.Straight, 5f,  0f,   0f,    4f, 0f,  0f),    // 0
                S("Opening run",    TrackSectionKind.Straight, 14f, 0f,   0f,    6f, 0f,  0f),    // 1
                S("Left sweep",     TrackSectionKind.Curve,    0f,  6f,   180f,  6f, 10f, 0f),    // 2
                S("Garden flare",   TrackSectionKind.Straight, 8f,  0f,   0f,    5f, 0f,  3.2f),  // 3
                S("Peg garden",     TrackSectionKind.Straight, 18f, 0f,   0f,    PegSlope, 0f, 0f),  // 4
                S("Garden exit",    TrackSectionKind.Straight, 8f,  0f,   0f,    5f, 0f,  1.7f),  // 5
                S("Right sweep",    TrackSectionKind.Curve,    0f,  6f,   -180f, 6f, 10f, 0f),    // 6
                S("Wheel hall",     TrackSectionKind.Straight, 18f, 0f,   0f,    5f, 0f,  2.2f),  // 7
                S("Helix",          TrackSectionKind.Curve,    0f,  3.6f, 360f,  8f, 14f, 1.5f),  // 8
                S("Pinch run",      TrackSectionKind.Straight, 22f, 0f,   0f,    6f, 0f,  1.7f),  // 9
                S("Final bend",     TrackSectionKind.Curve,    0f,  6f,   -200f, 5f, 12f, 0f),    // 10
                S("Bump alley",     TrackSectionKind.Straight, 20f, 0f,   0f,    5f, 0f,  0f),    // 11
                S("Home straight",  TrackSectionKind.Straight, 14f, 0f,   0f,    5f, 0f,  0f),    // 12
                S("Run-out",        TrackSectionKind.Straight, 8f,  0f,   0f,    3f, 0f,  0f)     // 13
            };
            l.obstacles = new[]
            {
                O("Rumble strip",  ObstacleKind.Bumps,      1,  8f,  0f, 5, 0.9f,  0.03f, 0f,   0, 0f),
                O("Pegs pyramid",  ObstacleKind.PegPyramid, 4,  1f,  0f, 5, 0.65f, 0.05f, 0f,   0, 0f),
                O("Pegs plinko",   ObstacleKind.PegGrid,    4,  8f,  0f, 5, 0.70f, 0.05f, 0f,   0, 0f),
                O("Wheel one",     ObstacleKind.Pinwheel,   7,  5f,  0f, 3, 0f,    0f,    120f, 0, 0f),
                O("Wheel two",     ObstacleKind.Pinwheel,   7,  13f, 0f, 3, 0f,    0f,    -140f, 0, 0f),
                O("Bottleneck",    ObstacleKind.Bottleneck, 9,  5f,  9f, 0, 0f,    0.46f, 0f,   0, 0f),
                O("Bump alley",    ObstacleKind.Bumps,      11, 3f,  0f, 8, 0.9f,  0.04f, 0f,   0, 0f),
                O("Last spinner",  ObstacleKind.Pinwheel,   12, 8f,  0f, 2, 0f,    0f,    -150f, 0, 0f)
            };
        }

        // ------------------------------------------------------------------ factories

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
