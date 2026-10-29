using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Scenes/MarbleMachine_ColourSorter_3D.unity</c> (mach-001): thirteen stations placed in flow
    /// order as a loop, joined by chutes, with the recycle cannon, studio, HUD and wiring.
    /// Menu: <c>Simulation Lobby ▸ 3D Formats ▸ Marble Machine (mach) ▸ Colour Sorter (mach-001)</c>.
    /// </summary>
    /// <remarks>
    /// Each station is first built alone as a throwaway probe so the layout can be planned from real sizes and exit
    /// offsets (a station's legs depend on its height, so it cannot simply be moved after the fact). The plan then
    /// fixes the horizontal position of every entry from the authored offset table in <see cref="Layout"/>, pushes a
    /// station along its offset until it clears the earlier ones, and works the heights out backwards from the sorter
    /// so every connector slopes down. Re-running replaces the scene, never the config asset.
    /// </remarks>
    static class MachineSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/MarbleMachine_ColourSorter_3D.unity";
        const string ConfigFolder = "Assets/Settings/Configs/MarbleMachine";
        const string ConfigPath = ConfigFolder + "/MachineConfig_001.asset";

        // Connector geometry: how far the chute runs straight out of a lip and straight into a catch zone.
        // The track is continuous: a chute starts exactly at the previous station's exit lip (no drop, no gap) and ends exactly at
        // the next station's entry port (same width, same floor level), so these three are zero. The straights are long enough
        // that the smoothed bends stay wider than the 2x-wide channel (a bend tighter than half the width folds its inner wall).
        internal const float LipDrop = 0f;
        internal const float LipAhead = 0f;
        internal const float EntryClearance = 0f;
        internal const float LeadLength = 1.5f;
        internal const float ApproachLength = 1.5f;
        /// <summary>The tipping tower takes its stream along a longer straight, so the last bend is well clear of the first trough.</summary>
        const float TowerApproachLength = 2.8f;
        /// <summary>The authored offset table was laid out for the 0.9 m track; the 1.8 m track and its bigger stations need more room.</summary>
        const float OffsetScale = 1.5f;

        /// <summary>
        /// The ski jump's outlet runs straight on past the recycle cannon (which sits just ahead of the ski jump, on the tower's side of
        /// the line) before it turns toward the tower; a diagonal from the exit cuts through the cannon's deck, hopper and turret.
        /// </summary>
        internal const float SkiLeadLength = 9.5f;

        /// <summary>mach-002 sets this (>= 0) while it builds, to shorten the ski outlet; stays -1 (mach-001 value) otherwise.</summary>
        internal static float SkiLeadOverride = -1f;

        /// <summary>Straight run out of every station except the ski jump, or -1 for the default. The race sets it longer so corners can have a real radius.</summary>
        internal static float LeadAllOverride = -1f;

        internal static float LeadFor(string stationName) => stationName == "Ski Jump" ? (SkiLeadOverride >= 0f ? SkiLeadOverride : SkiLeadLength) : (LeadAllOverride > 0f ? LeadAllOverride : LeadLength);

        /// <summary>Straight run into a screw lift's hopper, or -1 for the default. The race sets it long: a short straight leaves a ~90 degree corner of about 0.5 m radius, which folds the inner wall of the 2.1 m wide chute and throws fast racers off.</summary>
        internal static Dictionary<string, float> ApproachOverrides;

        internal static float ApproachFor(string stationName)
        {
            if (ApproachOverrides != null && stationName != null && ApproachOverrides.TryGetValue(stationName, out float over))
            {
                return over;
            }

            return stationName == "Tipping Tower" ? TowerApproachLength : ApproachLength;
        }
        /// <summary>Shallowest connector slope. Was 5; 8 lifts the silo several metres and hands every station a faster stream.</summary>
        const float TargetSlopeDegrees = 8f;
        /// <summary>The ski jump's outlet is a long run that must hand the first tipper a fast stream.</summary>
        internal const float SkiOutletSlopeDegrees = 16f;
        /// <summary>Steepest slope the height planner allows between stations before it lifts the next one.</summary>
        internal const float MaxPlanSlopeDegrees = 24f;
        internal const float MaxSlopeBeforeDogleg = 28f;
        internal const float OverlapMargin = 0.9f;

        /// <summary>Tallest permitted ball-carrying point (the silo top), metres.</summary>
        const float HeightBudget = 40f; // steeper connectors and bigger stations lift the whole chain; this only flags runaway growth
        /// <summary>Clear gap between the end of the tube row and the silo wall, so the opening shot holds both.</summary>
        const float SiloGap = 1.6f;
        /// <summary>The cannon deck hangs 2.3 m under its hopper rim (CannonBuilder), so the rim must stay above this.</summary>
        internal const float CannonMinRimY = 2.4f;

        /// <summary>The sorter's tube rack hangs off its right-hand side (local +X); the camera is on world -Z, so the sorter heads +X.</summary>
        const float SorterYaw = 90f;

        internal sealed class Spec
        {
            public string name;
            public float yaw;
            public Vector2 offset;
            public float minEntryY;
            public Func<Transform, StationPlacement, MachineKit, MachineConfig, MachineStation> build;
        }

        /*
         * HEIGHT ARITHMETIC (wave 2; metres; entry y -> exit y relative drop, nominal, the probes measure the real values)
         *   Silo top is the highest point: top = silo exit + ~5.5 (package S) so the silo exit must be <= ~5.5.
         *   Section 1 (silo exit -> bucket wheel entry):  connector 0.35 + peg wall 2.2 + connector 0.3 + vortex 1.5 +
         *                connector 0.3 + wave floor ~0.3 + connector 0.3 = ~5.2   (peg wall and vortex are the drops I own)
         *   Wheel lifts >= 6.0.
         *   Section 2 (wheel exit -> screw entry): connector 0.3 + ski jump 2.3 + connector 0.3 + tipping tower 2.7 +
         *                connector 0.3 + trapdoor run (its own depth, W) + elastic connector (L x tan 5 deg)
         *   Screw lifts >= 6.0.
         *   Section 3 (screw exit -> sorter): connector 0.3 + helix 2.15 + connector 0.3, sorter root >= 4.08 (T).
         *   The planner runs a fixpoint over "y(i+1) <= y(i) + exit_i.y - lip - L tan 5" (backward) and
         *   "y(i+1) >= y(i) + exit_i.y - lip - L tan 18" (forward) with each station's own floor (its depth below the
         *   entry, from its activity bounds), so a lift that rises too little raises its own entry just enough.
         *   It logs the table and warns when the tallest point passes 11 m.
         */

        /// <summary>
        /// The authored layout, in flow order. <c>offset</c> is the world XZ step from the previous station's exit to this
        /// station's entry (the builder pushes along it if the station would overlap one already placed). Yaw is the flow
        /// heading: 0 = +Z (away from the camera), 90 = +X, 180 = -Z, 270 = -X. The loop: silo front-right beside the tubes,
        /// up the right edge (peg wall, vortex), along the back to the left (wave floor, wheel, ski jump), down the left edge
        /// (tower, trapdoors), then right along the front (screw, helix) to the sorter and its tubes. The silo and the
        /// sorter are anchored to each other; the left-edge pair (tower, trapdoors) is shifted to close the loop.
        /// </summary>
        static List<Spec> Layout(MachineConfig config)
        {
            var specs = new List<Spec>
            {
                new Spec { name = "Silo", yaw = 0f, offset = Vector2.zero, minEntryY = 0f, build = (p, pl, k, c) => SiloBuilder.Build(p, pl, k, c) },
                new Spec { name = "Peg Wall", yaw = 0f, offset = new Vector2(0f, 1.6f), minEntryY = 0.3f, build = (p, pl, k, c) => PegWallBuilder.Build(p, pl, k, c) },
                new Spec { name = "Vortex Funnel", yaw = 0f, offset = new Vector2(0f, 1.6f), minEntryY = 0.3f, build = (p, pl, k, c) => VortexFunnelBuilder.Build(p, pl, k, c) },
                new Spec { name = "Wave Floor", yaw = 270f, offset = new Vector2(-2.2f, 1.8f), minEntryY = 0.3f, build = (p, pl, k, c) => WaveFloorBuilder.Build(p, pl, k, c) },
                new Spec { name = "Bucket Wheel", yaw = 270f, offset = new Vector2(-2.0f, 0f), minEntryY = 0.3f, build = (p, pl, k, c) => BucketWheelBuilder.Build(p, pl, k, c) },
                new Spec { name = "Ski Jump", yaw = 270f, offset = new Vector2(-2.5f, 0f), minEntryY = 0.3f, build = (p, pl, k, c) => SkiJumpBuilder.Build(p, pl, k, c) },
                new Spec { name = "Tipping Tower", yaw = 180f, offset = new Vector2(-2.5f, -1.8f), minEntryY = 0.3f, build = (p, pl, k, c) => TippingTowerBuilder.Build(p, pl, k, c) },
                new Spec { name = "Trapdoor Run", yaw = 180f, offset = new Vector2(0f, -1.8f), minEntryY = 0.3f, build = (p, pl, k, c) => TrapdoorRunBuilder.Build(p, pl, k, c) },
                new Spec { name = "Screw Lift", yaw = 90f, offset = new Vector2(2.5f, -2.0f), minEntryY = 0.3f, build = (p, pl, k, c) => ScrewLiftBuilder.Build(p, pl, k, c) },
                new Spec { name = "Helix Slide", yaw = 90f, offset = new Vector2(2.0f, 0f), minEntryY = 0.3f, build = (p, pl, k, c) => HelixSlideBuilder.Build(p, pl, k, c) },
                new Spec { name = "Sorter", yaw = SorterYaw, offset = new Vector2(2.2f, 0f), minEntryY = 4f, build = (p, pl, k, c) => SorterBuilder.Build(p, pl, k, c) },
            };

            foreach (Spec spec in specs)
            {
                spec.offset *= OffsetScale;
            }

            return specs;
        }

        // ------------------------------------------------------------------ menu

        [MenuItem("Simulation Lobby/3D Formats/Marble Machine (mach)/Reset Config (tuning baseline)", false, 331)]
        static void ResetConfig()
        {
            Directory.CreateDirectory(ConfigFolder);
            var existing = AssetDatabase.LoadAssetAtPath<MachineConfig>(ConfigPath);
            if (existing == null)
            {
                EnsureConfig();
                Debug.Log($"[mach] Created {ConfigPath} with defaults.");
                return;
            }

            MachineConfig fresh = ScriptableObject.CreateInstance<MachineConfig>();
            ApplyBaseline(fresh);
            EditorUtility.CopySerialized(fresh, existing);
            UnityEngine.Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            Debug.Log($"[mach] Reset {ConfigPath} to the tuning baseline (fixedTimestep 1/100, maxDurationSeconds 900).", existing);
        }

        [MenuItem("Simulation Lobby/3D Formats/Marble Machine (mach)/Colour Sorter (mach-001)", false, 311)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[mach] Exit play mode before building the scene.");
                return;
            }

            Directory.CreateDirectory(ConfigFolder);
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(MachineGeometry.MeshFolder);
            MachineConnectorBuilder.BeginScene();
            _connectorTracks.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load the config only after NewScene: a Single-mode NewScene unloads unreferenced assets, which left an
            // earlier-loaded config "destroyed" (== null) while its managed fields still read fine.
            MachineConfig config = EnsureConfig();
            MachineKit kit = MachineMaterials.EnsureKit(config);
            if (kit.ballMaterials == null || kit.ballMaterials.Length != config.ColourCount || config.ColourCount < 2)
            {
                Debug.LogError($"[mach] Config has {config.ColourCount} colours but the kit made " +
                               $"{(kit.ballMaterials == null ? 0 : kit.ballMaterials.Length)} ball materials. " +
                               "Run Reset Config (tuning baseline), then build again.", config);
                return;
            }

            List<Spec> specs = Layout(config);
            Probe[] probes = new Probe[specs.Count];
            for (int i = 0; i < specs.Count; i++)
            {
                probes[i] = ProbeStation(specs[i], kit, config, i == specs.Count - 1);
            }

            Probe cannonProbe = ProbeCannon(kit, config);

            // The sorter's lane floor at its last flap must clear the tube rims (T: SorterBuilder.MinFloorAtLastFlap); its own
            // warning fires below that. The lane floor sits about 0.2 m above the exit anchor's height.
            int last = specs.Count - 1;
            specs[last].minEntryY = Mathf.Max(specs[last].minEntryY,
                TubeRackBuilder.TubeHeight(config) + 0.45f - (probes[last].exitOffset.y + 0.2f) + 0.02f);

            // ---- Plan: horizontal positions, then heights. ----
            Vector3[] entries = PlanHorizontal(specs, probes);
            PlanHeights(specs, probes, entries, config, out float[] slopes, out float[] lengthsPlan, out float[] floors);

            // ---- Build for real. ----
            var machine = new GameObject("Machine");
            Transform stationRoot = MachineGeometry.Group(machine.transform, "Stations").transform;
            Transform connectorRoot = MachineGeometry.Group(machine.transform, "Connectors").transform;
            Transform ballRoot = MachineGeometry.Group(machine.transform, "Balls").transform;

            var built = new MachineStation[specs.Count];
            for (int i = 0; i < specs.Count; i++)
            {
                built[i] = specs[i].build(stationRoot, new StationPlacement(entries[i], specs[i].yaw), kit, config);
                if (built[i] == null)
                {
                    Debug.LogError($"[mach] Builder for '{specs[i].name}' returned null; aborting.");
                    return;
                }
            }

            var silo = (SiloStation)built[0];
            var sorter = (SorterStation)built[specs.Count - 1];
            TubeRack tubeRack = TubeRackBuilder.Build(stationRoot, sorter, kit, config);

            // ---- Chain connectors. ----
            int connectorCount = 0;
            for (int i = 0; i < specs.Count - 1; i++)
            {
                BuildConnector(connectorRoot, $"Chute {i + 1:00} {specs[i].name} to {specs[i + 1].name}",
                    built[i].exit, built[i], built[i + 1].entry, specs[i + 1].yaw, kit, config);
                connectorCount++;
            }

            // ---- Recycle: the cannon behind the ski jump. ----
            int skiIndex = specs.FindIndex(sp => sp.name == "Ski Jump");
            CannonStation cannon = BuildCannon(stationRoot, connectorRoot, silo, built[skiIndex], cannonProbe, kit, config, ref connectorCount);

            // ---- Studio, camera. ----
            Bounds machineBounds = RendererBounds(machine.transform);
            VerifyLayout(specs, built, entries, machineBounds, tubeRack, cannon);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            MachineStudioEnvironment.Build(cam, machineBounds);
            TourCameraRig rig = cameraObject.AddComponent<TourCameraRig>();
            rig.Bind(cam);
            // Opening frame: the seven tubes and the whole silo beside them (L's camera director refines it).
            Bounds opening = RendererBounds(tubeRack.transform);
            opening.Encapsulate(RendererBounds(silo.transform));
            PlaceEstablishingShot(cam, rig, opening);

            // ---- Wiring. ----
            var runner = machine.AddComponent<SimulationRunner>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            var simulation = machine.AddComponent<MachineSimulation>();
            simulation.silo = silo;
            simulation.cannon = cannon;
            simulation.sorter = sorter;
            simulation.tubeRack = tubeRack;
            simulation.stations = new List<MachineStation>(built) { tubeRack, cannon };
            simulation.ballRoot = ballRoot;
            simulation.ballMesh = kit.ballMesh;
            simulation.ballMaterials = kit.ballMaterials;

            // ---- HUD. ----
            CaptionHud caption = CaptionCanvasBuilder.Build("WHICH COLOUR FILLS ITS TUBE FIRST?",
                $"{config.ballCount:N0} balls · {config.ColourCount} colours · one machine", "SIMULATION LOBBY");
            LeaderboardHud leaderboard = LeaderboardCanvasBuilder.Build("TUBES", config.ColourCount);
            MoveBoardToRight(leaderboard);
            for (int i = 0; i < config.ColourCount; i++)
            {
                leaderboard.SetContender(i, config.colours[i].name, config.colours[i].color);
            }

            var presenterObject = new GameObject("Presenter");
            var presenter = presenterObject.AddComponent<MachinePresenter>();
            presenter.Bind(simulation, leaderboard, caption, rig);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            LogSummary(specs, built, entries, slopes, floors, machineBounds, tubeRack, cannon, connectorCount, config);
        }

        // ------------------------------------------------------------------ probes

        internal struct Probe
        {
            public Bounds rel;          // renderer bounds relative to the entry anchor (a station standing on the floor reaches y = 0)
            public Vector3 exitOffset;  // exit anchor minus entry anchor
            public Vector2 exitDir;     // horizontal direction balls leave in
            public bool reachesFloor;
            public float activityMinRel; // lowest ball-carrying y relative to the entry anchor (negative = below)
            public float recycleRelY;     // recycle exit y relative to the entry (0 if the station has none)
            public float entryLift;       // entry anchor y above the placement entry (a station that wants its chute to end higher)
            public Vector2 rackCentreRel; // sorter only: tube row centre (xz) relative to the entry
            public Vector3 recycleOffset; // recycle exit position minus the entry anchor (zero if the station has none)
        }

        const float ProbeHeight = 20f;

        internal static Probe ProbeStation(Spec spec, MachineKit kit, MachineConfig config, bool isSorter)
        {
            var holder = new GameObject("__probe").transform;
            var placement = new StationPlacement(new Vector3(0f, ProbeHeight, 0f), spec.yaw);
            MachineStation station = spec.build(holder, placement, kit, config);
            if (isSorter)
            {
                TubeRackBuilder.Build(holder, (SorterStation)station, kit, config);
            }

            Probe probe = Measure(holder, station, placement);
            if (isSorter)
            {
                TubeRack rack = holder.GetComponentInChildren<TubeRack>();
                if (rack != null)
                {
                    probe.rackCentreRel = new Vector2(rack.transform.position.x - placement.entry.x, rack.transform.position.z - placement.entry.z);
                }
            }

            UnityEngine.Object.DestroyImmediate(holder.gameObject);
            return probe;
        }

        static Probe ProbeCannon(MachineKit kit, MachineConfig config)
        {
            var holder = new GameObject("__probe").transform;
            var placement = new StationPlacement(new Vector3(0f, 2f, 0f), 270f);
            MachineStation station = CannonBuilder.Build(holder, placement, kit, config);
            Probe probe = Measure(holder, station, placement);
            UnityEngine.Object.DestroyImmediate(holder.gameObject);
            return probe;
        }

        internal static Probe Measure(Transform holder, MachineStation station, StationPlacement placement)
        {
            Bounds world = RendererBounds(holder);
            var probe = new Probe
            {
                rel = new Bounds(world.center - placement.entry, world.size),
                reachesFloor = world.min.y <= 0.15f,
                activityMinRel = station.activityBounds.size.sqrMagnitude > 0f ? station.activityBounds.min.y - placement.entry.y : -0.3f
            };

            if (station.recycleExit != null && station.recycleExit != station.exit)
            {
                probe.recycleRelY = station.recycleExit.position.y - placement.entry.y;
                probe.recycleOffset = station.recycleExit.position - placement.entry;
            }

            if (station.entry != null)
            {
                probe.entryLift = station.entry.position.y - placement.entry.y;
            }

            if (station.exit != null)
            {
                probe.exitOffset = station.exit.position - placement.entry;
                Vector3 f = station.exit.forward;
                Vector2 d = new Vector2(f.x, f.z);
                probe.exitDir = d.sqrMagnitude > 0.25f ? d.normalized : YawDirection(placement.yawDegrees);
            }
            else
            {
                probe.exitDir = YawDirection(placement.yawDegrees);
            }

            return probe;
        }

        internal static Vector2 YawDirection(float yaw)
        {
            float r = yaw * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

        internal static Bounds RendererBounds(Transform root)
        {
            bool any = false;
            var bounds = new Bounds(root.position, Vector3.zero);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return bounds;
        }

        // ------------------------------------------------------------------ planning

        internal static Rect Footprint(Probe probe, Vector2 entryXZ, float margin)
        {
            Vector2 centre = entryXZ + new Vector2(probe.rel.center.x, probe.rel.center.z);
            Vector2 half = new Vector2(probe.rel.extents.x + margin, probe.rel.extents.z + margin);
            return new Rect(centre - half, half * 2f);
        }

        static Vector3[] PlanHorizontal(List<Spec> specs, Probe[] probes)
        {
            int n = specs.Count;
            var xz = new Vector2[n];
            var placed = new bool[n];
            int last = n - 1;
            int tower = specs.FindIndex(sp => sp.name == "Tipping Tower");
            int trap = specs.FindIndex(sp => sp.name == "Trapdoor Run");
            int screw = specs.FindIndex(sp => sp.name == "Screw Lift");

            // 1. The sorter (tubes) is the anchor; 2. the silo goes right beside the end of the tube row, at the same depth.
            xz[last] = Vector2.zero;
            placed[last] = true;
            float tubesRight = xz[last].x + probes[last].rel.max.x;
            xz[0] = new Vector2(tubesRight + SiloGap - probes[0].rel.min.x, xz[last].y + probes[last].rackCentreRel.y);
            placed[0] = true;

            // 3. Forward from the silo up to the trapdoor run.
            for (int i = 1; i <= trap; i++)
            {
                Vector2 exitXZ = xz[i - 1] + new Vector2(probes[i - 1].exitOffset.x, probes[i - 1].exitOffset.z);
                Vector2 offset = specs[i].offset;
                Vector2 dir = offset.sqrMagnitude > 1e-4f ? offset.normalized : YawDirection(specs[i].yaw);
                xz[i] = PushClear(i, exitXZ + offset, dir, xz, placed, probes, specs[i].name);
                placed[i] = true;
            }

            // 4. Backward from the sorter down to the screw lift: entry(i-1) = entry(i) - offset(i) - exitOffset(i-1).
            for (int i = last; i > screw; i--)
            {
                Vector2 exitPrev = xz[i] - specs[i].offset;
                Vector2 dir = specs[i].offset.sqrMagnitude > 1e-4f ? -specs[i].offset.normalized : -YawDirection(specs[i].yaw);
                Vector2 e = exitPrev - new Vector2(probes[i - 1].exitOffset.x, probes[i - 1].exitOffset.z);
                // Pushing back moves the previous station away from the next one; the exit offset shifts with it.
                xz[i - 1] = PushClear(i - 1, e, dir, xz, placed, probes, specs[i - 1].name);
                placed[i - 1] = true;
            }

            // 5. Close the loop: the left-edge pair (tower, trapdoors) shifts so the trapdoor exit sits 3 m left of and 3.5 m
            //    behind the screw entry. The ski-jump and trapdoor-to-screw connectors absorb the difference.
            Vector2 trapExit = xz[trap] + new Vector2(probes[trap].exitOffset.x, probes[trap].exitOffset.z);
            Vector2 wanted = xz[screw] + new Vector2(-4.5f, 5.25f);
            Vector2 shift = wanted - trapExit;
            for (int i = tower; i <= trap; i++)
            {
                xz[i] += shift;
            }

            Vector2 skiExit = xz[tower - 1] + new Vector2(probes[tower - 1].exitOffset.x, probes[tower - 1].exitOffset.z);
            Debug.Log($"[mach] Loop closure: left-edge pair shifted by ({shift.x:0.0}, {shift.y:0.0}) m. " +
                      $"Ski exit to tower entry: ({xz[tower].x - skiExit.x:0.0}, {xz[tower].y - skiExit.y:0.0}) m.");
            if (xz[tower].x > skiExit.x - 1.5f)
            {
                Debug.LogWarning("[mach] The tower entry is not clear of the ski-jump exit after loop closure: widen the back row (Layout offsets).");
            }

            // Re-centre: the tube row on x = 0, the front-most footprint on z = 0.
            float minZ = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                minZ = Mathf.Min(minZ, Footprint(probes[i], xz[i], 0f).yMin);
            }

            Vector2 centre = xz[last] + probes[last].rackCentreRel;
            Vector2 recentre = new Vector2(-centre.x, -minZ);
            var entries = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                entries[i] = new Vector3(xz[i].x + recentre.x, 0f, xz[i].y + recentre.y);
            }

            return entries;
        }

        internal static Vector2 PushClear(int i, Vector2 start, Vector2 dir, Vector2[] xz, bool[] placed, Probe[] probes, string name)
        {
            Vector2 e = start;
            int guard = 0;
            while (OverlapsEarlier(i, e, xz, placed, probes) && guard++ < 160)
            {
                e += dir * 0.25f;
            }

            if (guard > 0)
            {
                Debug.Log($"[mach] '{name}' pushed {guard * 0.25f:0.0} m along {dir} to clear stations already placed.");
            }

            return e;
        }

        internal static bool OverlapsEarlier(int i, Vector2 entry, Vector2[] xz, bool[] placed, Probe[] probes)
        {
            Rect mine = Footprint(probes[i], entry, OverlapMargin * 0.5f);
            for (int j = 0; j < xz.Length; j++)
            {
                if (j == i || !placed[j]) continue;
                if (mine.Overlaps(Footprint(probes[j], xz[j], OverlapMargin * 0.5f)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The polyline a chain connector follows (horizontal only): straight out of the lip, a straight run into the catch zone.</summary>
        internal static void ChainPath(Vector3 exitPos, Vector2 exitDir, Vector3 entryPos, float entryYaw, out Vector3 from, out Vector3 to, out List<Vector3> via,
            float approachLength = ApproachLength, float leadLength = LeadLength)
        {
            Vector2 inDir = YawDirection(entryYaw);
            from = exitPos + new Vector3(exitDir.x, 0f, exitDir.y) * LipAhead + Vector3.down * LipDrop;
            Vector3 dirOut = new Vector3(exitDir.x, 0f, exitDir.y);
            Vector3 dirIn = new Vector3(inDir.x, 0f, inDir.y);
            Vector3 lead = from + dirOut * leadLength;
            Vector3 approach = entryPos - dirIn * approachLength;
            if (approachLength > ApproachLength || leadLength > LeadLength)
            {
                // Long straights give corners a big radius, but the approach point must still lie AHEAD of the lead point, or the path
                // doubles back on itself (a hairpin the walls fold at). Shorten the straights, approach first, until they fit.
                Vector3 chord = entryPos - from;
                chord.y = 0f;
                bool fitted = false;
                for (float a = approachLength; a >= ApproachLength - 0.01f && !fitted; a -= 0.5f)
                {
                    for (float l = leadLength; l >= LeadLength - 0.01f && !fitted; l -= 0.5f)
                    {
                        Vector3 ap = entryPos - dirIn * a;
                        Vector3 ld = from + dirOut * l;
                        Vector3 mid = ap - ld;
                        mid.y = 0f;
                        if (Vector3.Dot(mid, chord) > 0f && mid.magnitude > 1.2f)
                        {
                            approach = ap;
                            lead = ld;
                            fitted = true;
                        }
                    }
                }

                if (!fitted)
                {
                    lead = from + dirOut * LeadLength;
                    approach = entryPos - dirIn * ApproachLength;
                }
            }

            to = entryPos + Vector3.up * EntryClearance;
            via = new List<Vector3> { lead, approach };
        }

        /// <summary>
        /// Heights as a fixpoint of lower bounds on each entry y: its own floor (spec minimum or its depth below the entry,
        /// from the probe's activity bounds), the backward bound (the next station's entry plus a connector at the shallowest
        /// slope) and the forward bound (the previous exit minus a connector at the steepest planned slope; skipped into
        /// the sorter, whose height is fixed by the tube rims). Lifts therefore sit on their floors unless the drops after
        /// them need more rise, and the silo (first station) lands as low as the chain allows.
        /// </summary>
        static void PlanHeights(List<Spec> specs, Probe[] probes, Vector3[] entries, MachineConfig config,
            out float[] slopes, out float[] lengths, out float[] floors)
        {
            int n = specs.Count;
            float tanMin = Mathf.Tan(TargetSlopeDegrees * Mathf.Deg2Rad);
            float tanMax = Mathf.Tan(MaxPlanSlopeDegrees * Mathf.Deg2Rad);
            var tanMinOf = new float[n];
            for (int i = 0; i < n; i++)
            {
                // The helix hands the sorter its stream: that chute runs at the sorter hopper's own tilt, so it is collinear with the
                // hopper's entry port and the balls meet no change of slope (and lose no speed) at the joint.
                tanMinOf[i] = specs[i].name == "Ski Jump" ? Mathf.Tan(SkiOutletSlopeDegrees * Mathf.Deg2Rad)
                    : specs[i].name == "Helix Slide" ? Mathf.Tan(SorterBuilder.HopperTiltDegrees * Mathf.Deg2Rad) : tanMin;
            }

            var y = new float[n];
            lengths = new float[n];
            floors = new float[n];
            for (int i = 0; i < n; i++)
            {
                float depth = Mathf.Clamp(-probes[i].activityMinRel, 0f, 8f);
                floors[i] = Mathf.Max(specs[i].minEntryY, depth + 0.1f);
                y[i] = floors[i];
            }

            // The recycle cannon hangs under the ski jump's net: its rim must stay above CannonMinRimY.
            int ski = specs.FindIndex(sp => sp.name == "Ski Jump");
            if (ski >= 0 && probes[ski].recycleRelY < 0f)
            {
                float need = CannonMinRimY + LipDrop + EntryClearance + 4f * tanMin - probes[ski].recycleRelY;
                floors[ski] = Mathf.Max(floors[ski], need);
                y[ski] = floors[ski];
            }

            var a = new float[n];
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 exitPos = entries[i] + new Vector3(probes[i].exitOffset.x, 0f, probes[i].exitOffset.z);
                ChainPath(exitPos, probes[i].exitDir, entries[i + 1], specs[i + 1].yaw, out Vector3 from, out Vector3 to, out List<Vector3> via,
                    ApproachFor(specs[i + 1].name), LeadFor(specs[i].name));
                lengths[i] = MachineConnectorBuilder.HorizontalLength(from, to, via);
                a[i] = probes[i].exitOffset.y - LipDrop - EntryClearance - probes[i + 1].entryLift;
            }

            for (int iter = 0; iter < 400; iter++)
            {
                bool changed = false;
                for (int i = 0; i < n - 1; i++)
                {
                    float back = y[i + 1] - a[i] + lengths[i] * tanMinOf[i];
                    if (back > y[i] + 1e-4f)
                    {
                        y[i] = back;
                        changed = true;
                    }

                    if (i + 1 < n - 1)
                    {
                        float fwd = y[i] + a[i] - lengths[i] * tanMax;
                        if (fwd > y[i + 1] + 1e-4f)
                        {
                            y[i + 1] = fwd;
                            changed = true;
                        }
                    }
                }

                if (!changed) break;
            }

            for (int i = 0; i < n; i++)
            {
                entries[i].y = y[i];
            }

            slopes = new float[n];
            for (int i = 0; i < n - 1; i++)
            {
                float drop = y[i] + probes[i].exitOffset.y - LipDrop - (y[i + 1] + EntryClearance + probes[i + 1].entryLift);
                slopes[i] = Mathf.Atan2(drop, Mathf.Max(0.01f, lengths[i])) * Mathf.Rad2Deg;
            }
        }

        // ------------------------------------------------------------------ connectors

        internal static void BuildConnector(Transform parent, string name, Transform exit, MachineStation owner, Transform entry, float entryYaw,
            MachineKit kit, MachineConfig config)
        {
            Vector3 f = exit.forward;
            Vector2 dir = new Vector2(f.x, f.z);
            dir = dir.sqrMagnitude > 0.25f ? dir.normalized : YawDirection(owner.transform.eulerAngles.y);
            ChainPath(exit.position, dir, entry.position, entryYaw, out Vector3 from, out Vector3 to, out List<Vector3> via,
                ApproachFor(entry.GetComponentInParent<MachineStation>() != null ? entry.GetComponentInParent<MachineStation>().displayName : (entry.parent != null ? entry.parent.name : string.Empty)), LeadFor(owner != null ? owner.displayName : string.Empty));
            Lengthen(from, to, via, config);
            // The long straight out of the ski jump gets three speed-boost pads.
            float[] boosts = name.IndexOf("Ski Jump to Tipping Tower", System.StringComparison.OrdinalIgnoreCase) >= 0 ? new[] { 0.10f, 0.20f, 0.30f } : null;
            MachineConnectorBuilder.Build(parent, name, from, to, kit, config, via, boosts);
            _connectorTracks.Add(new ConnectorTrack { name = name, from = from, to = to, via = via, owner = owner });
        }

        /// <summary>If the drop is steep for the run, add an S-bend (a dog-leg) so the slope stays under the limit.</summary>
        internal static void Lengthen(Vector3 from, Vector3 to, List<Vector3> via, MachineConfig config)
        {
            for (int tries = 0; tries < 6; tries++)
            {
                float length = MachineConnectorBuilder.HorizontalLength(from, to, via);
                float drop = from.y - to.y;
                if (drop <= 0f || Mathf.Atan2(drop, length) * Mathf.Rad2Deg <= MaxSlopeBeforeDogleg)
                {
                    return;
                }

                Vector3 a = via[0];
                Vector3 b = via[via.Count - 1];
                Vector3 mid = Vector3.Lerp(a, b, 0.5f);
                Vector3 along = b - a;
                along.y = 0f;
                Vector3 side = along.sqrMagnitude < 1e-4f ? Vector3.right : Vector3.Cross(Vector3.up, along.normalized);
                float amp = 3.2f * (1 + tries / 2) * (tries % 2 == 0 ? 1f : -1f);
                via.Insert(1, mid + side * amp);
                via.Insert(2, mid - side * amp);
            }
        }

        internal struct ConnectorTrack
        {
            public string name;
            public Vector3 from;
            public Vector3 to;
            public List<Vector3> via;
            public MachineStation owner;
        }

        internal static readonly List<ConnectorTrack> _connectorTracks = new List<ConnectorTrack>();

        // ------------------------------------------------------------------ cannon

        /// <summary>
        /// The recycle cannon sits behind the ski jump's far end, on the side the net's gutter exits to (the back of the
        /// machine), so the ~2 balls/s of misses reach its hopper over a short 5 degree chute. It is far from the silo (outside
        /// its footprint by construction) and lobs across the machine at the silo top. Sorter leftovers roll off the lane end
        /// and are caught by the kill plane (the simulation teleports them to the hopper).
        /// </summary>
        static CannonStation BuildCannon(Transform stationRoot, Transform connectorRoot, SiloStation silo, MachineStation ski,
            Probe probe, MachineKit kit, MachineConfig config, ref int connectorCount)
        {
            Vector3 target = silo.CannonTarget;
            Transform feeder = ski.recycleExit != null ? ski.recycleExit : ski.exit;
            Vector3 f = feeder.forward;
            Vector2 outDir = new Vector2(f.x, f.z);
            outDir = outDir.sqrMagnitude > 0.25f ? outDir.normalized : YawDirection(ski.transform.eulerAngles.y);
            Vector3 right3 = ski.transform.right;
            Vector2 side = new Vector2(right3.x, right3.z).normalized;
            if (Vector2.Dot(side, new Vector2(feeder.position.x - ski.transform.position.x, feeder.position.z - ski.transform.position.z)) < 0f)
            {
                side = -side;
            }

            const float run = 4.5f;
            float tan = Mathf.Tan(TargetSlopeDegrees * Mathf.Deg2Rad);
            Vector2 exitXZ = new Vector2(feeder.position.x, feeder.position.z);
            Vector2 cannonXZ = exitXZ + outDir * run + side * 1.2f;
            float rimY = feeder.position.y - LipDrop - run * tan - EntryClearance;
            if (rimY < CannonMinRimY)
            {
                Debug.LogWarning($"[mach] Cannon rim would be at y {rimY:0.00} (< {CannonMinRimY} m): its deck would sink into the floor. Raise the ski jump.");
            }

            float yaw = Mathf.Atan2(outDir.x, outDir.y) * Mathf.Rad2Deg;
            Vector3 entry = new Vector3(cannonXZ.x, Mathf.Max(rimY, CannonMinRimY), cannonXZ.y);
            CannonStation cannon = CannonBuilder.Build(stationRoot, new StationPlacement(entry, yaw), kit, config);

            var aim = new GameObject("Cannon Target").transform;
            aim.SetParent(stationRoot.parent, false);
            aim.position = target;
            cannon.aimTarget = aim;

            Vector3 from = feeder.position + new Vector3(outDir.x, 0f, outDir.y) * LipAhead + Vector3.down * LipDrop;
            Vector3 to = cannon.entry.position + Vector3.up * EntryClearance;
            var via = new List<Vector3>
            {
                feeder.position + new Vector3(outDir.x, 0f, outDir.y) * (LipAhead + LeadLength),
                new Vector3(cannonXZ.x, 0f, cannonXZ.y) - new Vector3(outDir.x, 0f, outDir.y) * ApproachLength
            };
            Lengthen(from, to, via, config);
            const string name = "Recycle Ski Jump to Cannon";
            MachineConnectorBuilder.Build(connectorRoot, name, from, to, kit, config, via);
            _connectorTracks.Add(new ConnectorTrack { name = name, from = from, to = to, via = via, owner = null });
            connectorCount++;

            Vector2 siloXZ = new Vector2(silo.transform.position.x, silo.transform.position.z);
            float clearance = Vector2.Distance(siloXZ, cannonXZ) - silo.InnerRadius - 1.2f;
            string verdict = clearance >= 1.5f ? "ok" : "TOO CLOSE";
            if (clearance < 1.5f) Debug.LogWarning($"[mach] Cannon is only {clearance:0.0} m clear of the silo footprint.");
            Debug.Log($"[mach] Cannon rim at {Fmt(cannon.entry.position)}, {clearance:0.0} m outside the silo footprint ({verdict}); " +
                      $"it lobs {Vector2.Distance(siloXZ, cannonXZ):0.0} m across the machine to {Fmt(target)}.");
            return cannon;
        }

        // ------------------------------------------------------------------ camera + HUD

        internal static void PlaceEstablishingShot(Camera cam, TourCameraRig rig, Bounds b)
        {
            float vHalf = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * (16f / 9f));
            float distanceW = (b.extents.x * 1.1f) / Mathf.Tan(hHalf);
            float distanceH = (b.extents.y * 1.15f) / Mathf.Tan(vHalf);
            float distance = Mathf.Max(distanceW, distanceH) + b.extents.z;
            Vector3 look = new Vector3(b.center.x, b.center.y * 0.9f, b.center.z);
            Vector3 position = new Vector3(b.center.x, Mathf.Max(1.8f, look.y * 0.8f), b.center.z - distance);
            cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position));
            rig.SnapTo(position, look);
        }

        internal static void MoveBoardToRight(LeaderboardHud hud)
        {
            Transform board = hud.transform.Find("Board");
            if (board == null)
            {
                return;
            }

            var rect = (RectTransform)board;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-48f, -190f);
        }

        // ------------------------------------------------------------------ config

        static MachineConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<MachineConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            Directory.CreateDirectory(ConfigFolder);
            config = ScriptableObject.CreateInstance<MachineConfig>();
            ApplyBaseline(config);
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static void ApplyBaseline(MachineConfig config)
        {
            config.fixedTimestep = 1f / 100f;
            config.maxDurationSeconds = 900f;
            config.intent = "mach-001: 14,000 balls (2,000 of each of 7 colours) through a marble machine into a colour sorter. " +
                            "Which colour fills its tube first? Tune tubeCapacity for a winner at 10:00-10:45.";
        }

        // ------------------------------------------------------------------ checks + logs

        static void VerifyLayout(List<Spec> specs, MachineStation[] built, Vector3[] entries, Bounds machineBounds,
            TubeRack tubeRack, CannonStation cannon)
        {
            int n = built.Length;
            var bounds = new Bounds[n + 2];
            var names = new string[n + 2];
            for (int i = 0; i < n; i++)
            {
                bounds[i] = RendererBounds(built[i].transform);
                names[i] = specs[i].name;
            }

            bounds[n - 1].Encapsulate(RendererBounds(tubeRack.transform));
            bounds[n] = RendererBounds(cannon.transform);
            names[n] = "Cannon";

            int overlaps = 0;
            for (int i = 0; i <= n; i++)
            {
                for (int j = i + 1; j <= n; j++)
                {
                    Bounds a = bounds[i];
                    a.Expand(OverlapMargin);
                    if (a.Intersects(bounds[j]))
                    {
                        overlaps++;
                        Debug.LogWarning($"[mach] '{names[i]}' and '{names[j]}' bounds overlap (margin {OverlapMargin} m): {a} vs {bounds[j]}.");
                    }
                }
            }

            int clashes = 0;
            foreach (ConnectorTrack c in _connectorTracks)
            {
                for (int i = 0; i < n; i++)
                {
                    if (PolylineEntersBounds(c, bounds[i]))
                    {
                        clashes++;
                        Debug.LogWarning($"[mach] Connector '{c.name}' passes through the bounds of '{names[i]}'.");
                    }
                }
            }

            _connectorTracks.Clear();
            if (overlaps + clashes > 0)
            {
                Debug.LogWarning($"[mach] Layout check: {overlaps} station overlaps, {clashes} connector clashes. Tune the offset table in MachineSceneBuilder.Layout.");
            }
        }

        internal static bool PolylineEntersBounds(ConnectorTrack c, Bounds bounds)
        {
            // Skip the two stations the chute joins: it starts and ends inside their catch zones.
            bool startsHere = (c.from - bounds.ClosestPoint(c.from)).sqrMagnitude < 4f;
            bool endsHere = (c.to - bounds.ClosestPoint(c.to)).sqrMagnitude < 4f;
            if (startsHere || endsHere)
            {
                return false;
            }

            var points = new List<Vector3> { c.from };
            points.AddRange(c.via);
            points.Add(c.to);
            float total = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                total += Vector3.Distance(points[i], points[i - 1]);
            }

            float run = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                float segment = Vector3.Distance(points[i], points[i - 1]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(segment / 0.5f));
                for (int s = 0; s < steps; s++)
                {
                    Vector3 p = Vector3.Lerp(points[i - 1], points[i], s / (float)steps);
                    p.y = Mathf.Lerp(c.from.y, c.to.y, (run + segment * s / steps) / Mathf.Max(0.01f, total));
                    if (bounds.Contains(p))
                    {
                        return true;
                    }
                }

                run += segment;
            }

            return false;
        }

        static void LogSummary(List<Spec> specs, MachineStation[] built, Vector3[] entries, float[] slopes, float[] floors, Bounds machineBounds,
            TubeRack tubeRack, CannonStation cannon, int connectorCount, MachineConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[mach] Station table (world metres)");
            sb.AppendLine("  #  station          entry y  floor  exit y  top y   entry (x,z)        slope to next");
            float tallest = 0f;
            string tallestName = "";
            for (int i = 0; i < built.Length; i++)
            {
                Bounds b = RendererBounds(built[i].transform);
                if (b.max.y > tallest)
                {
                    tallest = b.max.y;
                    tallestName = specs[i].name;
                }

                Vector3 e = built[i].entry != null ? built[i].entry.position : entries[i];
                Vector3 x = built[i].exit != null ? built[i].exit.position : e;
                string slope = i < slopes.Length - 1 ? $"{slopes[i]:0.0} deg" : "-";
                sb.AppendLine($"  {i + 1,2} {specs[i].name,-16} {e.y,6:0.00}  {floors[i],5:0.00}  {x.y,6:0.00}  {b.max.y,5:0.00}   ({e.x:0.0}, {e.z:0.0})   {slope}");
            }

            Bounds cb = RendererBounds(cannon.transform);
            sb.AppendLine($"   R {"Cannon",-16} {cannon.entry.position.y,6:0.00}        -       -  {cb.max.y,5:0.00}   ({cannon.entry.position.x:0.0}, {cannon.entry.position.z:0.0})   recycle");
            Debug.Log(sb.ToString());

            Bounds rackBounds = RendererBounds(tubeRack.transform);
            if (rackBounds.max.y > tallest)
            {
                tallest = rackBounds.max.y;
                tallestName = "Tube Rack";
            }

            Bounds siloBounds = RendererBounds(built[0].transform);
            float siloTop = siloBounds.max.y;
            Debug.Log($"[mach] Tallest point {tallest:0.0} m ({tallestName}); silo top {siloTop:0.0} m; whole machine height {machineBounds.max.y:0.0} m (budget {HeightBudget:0} m).");
            if (machineBounds.max.y > HeightBudget || siloTop > HeightBudget)
            {
                Debug.LogWarning($"[mach] Height budget exceeded: machine {machineBounds.max.y:0.0} m, silo top {siloTop:0.0} m > {HeightBudget:0} m. " +
                                 "Look at the floor column of the table: a station whose entry sits above its floor was lifted by the chain behind it " +
                                 "(a lift that rises too little, or a drop after it that is too deep).");
            }

            int warnings = MachineConnectorBuilder.WarningCount;
            Debug.Log($"[mach] Built {ScenePath}: {built.Length + 2} stations (incl. tube rack and cannon), {config.ballCount} balls, " +
                      $"{connectorCount} connectors ({warnings} warnings), machine bounds centre {Fmt(machineBounds.center)} size {Fmt(machineBounds.size)}, " +
                      $"silo entry y {entries[0].y:0.0}.");
        }

        internal static string Fmt(Vector3 v) => $"({v.x:0.0}, {v.y:0.0}, {v.z:0.0})";
    }
}
