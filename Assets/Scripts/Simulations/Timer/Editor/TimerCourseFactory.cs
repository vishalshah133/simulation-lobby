using System.Collections.Generic;
using System.IO;
using SimulationLobby.Shared.EditorTools;
using SimulationLobby.Shared.Tracks;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Simulations.Timer.EditorTools
{
    /// <summary>
    /// The <c>timer</c> course as data: a fast banked run of about 340 m from a hopper pen, down a plunge, through two funnelled
    /// peg fields and a helix, to a run-out that drops into the glass collector.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Speed arithmetic.</b> Marbles run under linear damping d (config <c>marbleLinearDamping</c>, 0.1) and angular damping a (0.05).
    /// A solid sphere rolling down a slope t reaches a terminal speed <c>v = g sin(t) / (d + 0.4 a)</c> (angular damping acts as 0.4 a of
    /// extra linear drag; rolling inertia only slows the approach, so it divides the acceleration by 1.4 and not the terminal speed).
    /// With g = 9.81 that is <c>v = 81.8 sin(t)</c>: 4 deg gives 5.7 m/s, 5 deg 7.1, 5.5 deg 7.8, 6 deg 8.5. The runs are 5.5 deg.
    /// </para>
    /// <para>
    /// The approach takes time (about 1/0.12 = 8 s), so the plunge exists to give the stream its speed up front: 24 deg for 8 m puts a
    /// marble near 7.5 m/s, and the 5.5 deg runs then hold it there. Slopes ease 7, 15, 24, 15, 8, 5.5 deg so no crease in the mesh is
    /// sharper than 9 deg (a bare 7 to 24 deg crease makes fast marbles hop). <see cref="EstimateTravelSeconds"/> integrates that model over
    /// the centreline: about 43 s gate to finish, a mean of about 7.7 m/s.
    /// </para>
    /// <para>
    /// <b>Banks.</b> A bend of radius r needs tan(bank) = v^2 / (r g) to hold a marble at speed v without the rail: at 7.5 m/s and r = 8 m
    /// that is 36 deg, which is the bank on every bend (<see cref="Bank"/>). Bends alternate left and right so the course zig-zags, and one
    /// 360 deg helix (r 8 m, 4.8 m of drop per turn) sits mid-course. A half turn is 25 m of track, so each bend has a straight either side.
    /// </para>
    /// <para>
    /// <b>Peg fields.</b> Each is a flare (2.0 to 4.2 m wide), a lane with funnel guide walls (0.55 m high, built by the scene builder, closing to a 1.2 m mouth 0.5 m before the first peg row), then the
    /// pegs. Pegs are 0.11 m radius at 1.2 m spacing with <c>pegWallClearance</c> 0.4 m (the old 1.12 m / 0.3 m left a 0.31 m wedge between the outer
    /// peg and the rail where marbles stuck). Every gap is now at least 0.79 m, and wall deflector wedges steer wall-huggers inward.
    /// </para>
    /// </remarks>
    public static class TimerCourseFactory
    {
        public const string Folder = "Assets/Settings/Timer";
        public const string LayoutPath = Folder + "/TimerTrackLayout_001.asset";

        /// <summary>Written to <see cref="TrackLayout.version"/>. Bump when the course below changes shape; older assets are rebuilt.</summary>
        public const int CourseVersion = 2004;

        public const float StartWidth = 2.0f;
        public const float FieldWidth = 4.2f;
        public const float RailHeight = 0.55f;
        public const float RunSlope = 5.5f;
        public const float BendRadius = 8f;
        /// <summary>
        /// Bend bank, sized for the slowest marbles, not the fastest: at 36 deg the marbles leaving a peg field (3-5 m/s) rolled into the
        /// inner floor/rail corner and jammed the whole stream (2026-11-12). 16 deg holds 7 m/s marbles with the rails' help.
        /// </summary>
        public const float Bank = 16f;

        /// <summary>Bends run steeper than the straights so a marble pressed against a rail keeps its speed.</summary>
        public const float BendSlope = 7f;

        const float Gravity = 9.81f;
        const float RollingInertiaFactor = 0.4f;
        const float RollingAcceleration = 1f + RollingInertiaFactor;

        // Section indices used by Obstacles(). Keep the two lists in step.
        public const int FunnelLaneA = 10, FunnelLaneB = 18;
        const int PegFieldA = 11, PegFieldB = 19;

        /// <summary>
        /// Peg field layout. The Shared builder insets pegs by clearance + radius - 0.12 from the rail and centres them at this spacing, so with
        /// a 4.2 m lane: 3 pegs on wide rows (0, +-1.2 m), 2 on offset rows (+-0.6 m). Gaps: peg to peg 0.98 m, outer peg to rail 0.79 m,
        /// both well over a marble (0.4 m); nothing in a field is a pocket a marble can wedge in. Wall deflectors (scene builder) stop wall-huggers.
        /// </summary>
        public const float PegWallClearance = 0.4f, PegSpacing = 1.2f, PegRadius = 0.11f;

        /// <summary>No gap in a peg field may be below this (1.4 marble diameters): a gap in 0.05 m..this is a trap, so the build refuses.</summary>
        public const float MinFieldGap = 0.56f;

        /// <summary>Wall deflector wedge: depth into the lane, length along the wall (about 42 deg to the flow), height.</summary>
        public const float DeflectorDepth = 0.5f, DeflectorLength = 0.55f;

        /// <summary>The funnel walls are built by the scene builder (taller than the Shared kind): this long, closing to this mouth width.</summary>
        public const float FunnelLength = 6f, FunnelMouth = 1.2f;

        public static TrackLayout EnsureLayout(bool force = false)
        {
            Directory.CreateDirectory(Folder);
            var layout = AssetDatabase.LoadAssetAtPath<TrackLayout>(LayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<TrackLayout>();
                AssetDatabase.CreateAsset(layout, LayoutPath);
                force = true;
            }

            if (force || layout.version != CourseVersion)
            {
                Apply(layout);
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssets();
                Debug.Log($"[timer] Wrote course {LayoutPath} (version {CourseVersion}, {layout.sections.Length} sections, " +
                          $"{layout.obstacles.Length} obstacles).", layout);
            }

            return layout;
        }

        public static void Apply(TrackLayout layout)
        {
            layout.startWidth = StartWidth;
            layout.gateDistance = 3.5f;
            layout.runOutLength = 12f;
            layout.pegWallClearance = PegWallClearance;
            layout.defaultRailHeight = RailHeight;
            layout.sections = Sections();
            layout.obstacles = Obstacles();
            layout.version = CourseVersion;
        }

        public static TrackSection[] Sections()
        {
            return new[]
            {
                // Pen and plunge
                Straight("Start pen",        7f,   7f),
                Straight("Lip",              4f,   15f),
                Straight("Plunge",           8f,   24f),
                Straight("Foot",             4f,   15f),
                Straight("Ease",             5f,   8f),
                Straight("Catch run",        18f,  RunSlope),

                // Serpentine
                Bend("Bend 1",               180f),
                Straight("Run 1",            22f,  RunSlope),
                Bend("Bend 2",               -180f),

                // Peg field A: flare, funnel lane, pegs, exit
                Straight("Flare A",          4f,   RunSlope, FieldWidth),
                Straight("Funnel lane A",    6f,   RunSlope),
                Straight("Peg field A",      12f,  RunSlope),
                Straight("Exit A",           5f,   RunSlope, StartWidth),
                Straight("Run 2",            16f,  RunSlope),

                // The helix
                Bend("Bend 3",               180f, 8f, 9f),
                Bend("Helix",                -360f),
                Straight("Run 3",            12f,  RunSlope),

                // Peg field B
                Straight("Flare B",          4f,   RunSlope, FieldWidth),
                Straight("Funnel lane B",    6f,   RunSlope),
                Straight("Peg field B",      12f,  RunSlope),
                Straight("Exit B",           5f,   RunSlope, StartWidth),
                Straight("Run 4",            15f,  RunSlope),

                // Home straight, finish line, then the run-out that drops into the collector
                Bend("Bend 4",               -180f, 8f, 9f),
                Straight("Home straight",    24f,  RunSlope),
                Straight("Run-out",          12f,  RunSlope)
            };
        }

        public static TrackObstacle[] Obstacles()
        {
            return new[]
            {
                Obstacle("Pegs pyramid A", ObstacleKind.PegPyramid, PegFieldA, 0.5f, 0f, 3, PegSpacing, PegRadius),
                Obstacle("Pegs plinko A", ObstacleKind.PegGrid, PegFieldA, 4.8f, 0f, 6, PegSpacing, PegRadius),
                Obstacle("Pegs plinko B", ObstacleKind.PegGrid, PegFieldB, 0.5f, 0f, 10, PegSpacing, PegRadius)
            };
        }

        /// <summary>Peg row distances (offset within the section) between wide rows, where a wall deflector tip sits: the odd rows of each peg obstacle.</summary>
        public static List<KeyValuePair<int, float>> DeflectorSites()
        {
            var sites = new List<KeyValuePair<int, float>>();
            foreach (TrackObstacle o in Obstacles())
            {
                float pitch = o.spacing * 0.85f;
                for (int row = 1; row < o.count; row += 2)
                {
                    sites.Add(new KeyValuePair<int, float>(o.section, o.offset + row * pitch));
                }
            }

            return sites;
        }

        /// <summary>Course metres from the gate to the finish line.</summary>
        public static float CourseLength(List<TrackMeshBuilder.Sample> samples, TrackLayout layout)
        {
            return samples[samples.Count - 1].distance - layout.runOutLength - layout.gateDistance;
        }

        /// <summary>
        /// The terminal-speed model integrated over the centreline: seconds for a marble starting near rest at the gate to reach the finish.
        /// A model, not a measurement (it ignores peg, funnel and rail drag, so the real figure is a little higher).
        /// </summary>
        public static float EstimateTravelSeconds(List<TrackMeshBuilder.Sample> samples, TrackLayout layout, TimerConfig config, out float meanSpeed)
        {
            float drag = config.marbleLinearDamping + RollingInertiaFactor * config.marbleAngularDamping;
            float finish = samples[samples.Count - 1].distance - layout.runOutLength;
            float speed = 0.5f;
            float seconds = 0f;
            float metres = 0f;
            for (int i = 1; i < samples.Count; i++)
            {
                float step = samples[i].distance - samples[i - 1].distance;
                float sine = Mathf.Max(0f, -samples[i].forward.y);
                const int sub = 4;
                for (int s = 0; s < sub; s++)
                {
                    float ds = step / sub;
                    float dt = ds / Mathf.Max(speed, 0.5f);
                    speed = Mathf.Max(0f, speed + (Gravity * sine - drag * speed) / RollingAcceleration * dt);
                    if (samples[i].distance > layout.gateDistance && samples[i].distance < finish)
                    {
                        seconds += dt;
                        metres += ds;
                    }
                }
            }

            meanSpeed = seconds > 0f ? metres / seconds : 0f;
            return seconds;
        }

        static TrackSection Straight(string label, float length, float slope, float widthEnd = 0f)
        {
            return new TrackSection
            {
                label = label,
                kind = TrackSectionKind.Straight,
                length = length,
                slopeDegrees = slope,
                widthEnd = widthEnd
            };
        }

        static TrackSection Bend(string label, float arc) => Bend(label, arc, Bank, BendSlope);

        /// <summary>A bend after a peg field gets a flatter bank and a steeper slope: the marbles arrive slow.</summary>
        static TrackSection Bend(string label, float arc, float bank, float slope)
        {
            return new TrackSection
            {
                label = label,
                kind = TrackSectionKind.Curve,
                radius = BendRadius,
                arcDegrees = arc,
                slopeDegrees = slope,
                bankDegrees = bank
            };
        }

        static TrackObstacle Obstacle(string label, ObstacleKind kind, int section, float offset, float length, int count,
            float spacing, float size)
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
                size = size
            };
        }
    }
}
