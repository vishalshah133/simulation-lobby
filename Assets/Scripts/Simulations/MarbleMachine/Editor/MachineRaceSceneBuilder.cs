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
    using Spec = MachineSceneBuilder.Spec;
    using Probe = MachineSceneBuilder.Probe;

    /// <summary>
    /// Builds <c>Assets/Scenes/MarbleMachine_GrandPrix_3D.unity</c> (mach-002): sixteen numbered racers, three laps, a closed loop of nine
    /// stations (Start Gate, Helix Slide, Screw Lift 1, Peg Wall, Bucket Wheel, Ski Jump, Vortex Funnel, Screw Lift 2) joined by
    /// continuous chutes, plus the recycle cannon that fires ski-jump misses back onto the start platform.
    /// Menu: <c>Simulation Lobby ▸ 3D Formats ▸ Marble Machine (mach) ▸ Grand Prix (mach-002)</c>.
    /// </summary>
    /// <remarks>
    /// Reuses the mach-001 planner pieces (station probes, footprints, overlap push, <c>ChainPath</c>, <c>BuildConnector</c>,
    /// <c>Lengthen</c>) through <see cref="MachineSceneBuilder"/>'s internal surface. What is new here: the layout, a height planner that
    /// treats the course as a CYCLE (the closing connector from the screw lift back into the start gate must slope down too, so the lifts
    /// have to repay every drop of the lap), the cannon placed for the start platform, and the race wiring.
    /// <para>
    /// Heights are the one thing that can go wrong: a closed loop only exists if the two lifts raise at least as much as the stations and
    /// connectors drop in one lap. The planner checks this and, if the loop cannot close, logs an error with the shortfall in metres and builds
    /// the open-loop version (closing connector uphill) so the rest of the course can still be inspected.
    /// </para>
    /// </remarks>
    static class MachineRaceSceneBuilder
    {
        const string Tag = "[mach-002]";
        const string ScenePath = "Assets/Scenes/MarbleMachine_GrandPrix_3D.unity";
        const string ConfigFolder = "Assets/Settings/Configs/MarbleMachine";
        const string ConfigPath = ConfigFolder + "/MachineRaceConfig_001.asset";

        /// <summary>Steepest slope the planner lets a connector reach before it lifts the next station (race courses may run steeper than mach-001).</summary>
        const float MaxPlanSlopeDegrees = 26f;

        /// <summary>Cannon deck position relative to the ski jump's gutter end: same numbers as mach-001 (forward, then toward the gutter side).</summary>
        const float CannonRun = 4.5f;
        const float CannonSide = 1.2f;

        /// <summary>Screw lift exit to start gate entry, world X (the closing connector's lateral reach), and the nearest the front row may sit in Z.</summary>
        const float CloseX = 5.5f;
        const float CloseMinZ = -4.5f;
        /// <summary>Screw entry sits this far below the vortex exit in Z, so the vortex to screw connector has room to turn.</summary>
        const float FrontRowDropZ = 4.5f;

        const float HeightCap = 150f;

        /// <summary>Spare lift (metres) the screw lift is sized to leave over one lap's drops and chute-slope cost.</summary>
        const float LiftMargin = 3f;
        const float MaxScrewRise = 18f;
        /// <summary>The ski outlet runs straight on into the vortex, so it needs no 9.5 m lead to swing past the cannon (3.3 m clear of it already).</summary>
        const float RaceSkiLead = 2.5f;
        /// <summary>Straight run into each screw lift's hopper, so the last bend is wide enough for the chute's 2.1 m width (radius over about 1.2 m).</summary>
        const float RaceScrewApproach = 6f;
        /// <summary>Straight run into the start gate's back port, for the same reason.</summary>
        const float RaceGateApproach = 4.5f;
        /// <summary>Corner radius of every chute, metres. Over half the 2.1 m chute width, so the inner wall never folds.</summary>
        const float RaceFilletRadius = 2.0f;
        /// <summary>Straight run out of each station: with the fillet, a 90 degree corner next to a leg this long keeps a radius of about 1.35 m.</summary>
        const float RaceLead = 3f;

        // ------------------------------------------------------------------ layout

        /*
         * LAYOUT (world, camera on -Z looking +Z, X to the right). The course is the mirror of mach-001's: it runs up the LEFT edge, across
         * the back to the right, down the RIGHT edge and back along the front to the start gate at front-left.
         * (The brief calls this "counter-clockwise"; viewed from above with X right and Z up it is clockwise, the opposite of mach-001, which
         * climbs the right edge. The ski jump's gutter always lies on the right of its heading, which is the INSIDE of this loop, so the
         * cannon stands inside the loop beside the ski jump instead of behind the machine.)
         *
         *   Start Gate    yaw   0  (+Z)   front-left, origin
         *   Helix Slide   yaw   0  (+Z)   up the left edge
         *   Screw Lift 1  yaw   0  (+Z)   lift up the left edge (keeps the drops after the gate short: the course never stacks 11 m of drops)
         *   Peg Wall      yaw  90  (+X)   back-left corner, then along the back
         *   Bucket Wheel  yaw  90  (+X)   lift
         *   Ski Jump      yaw 180  (-Z)   back-right corner, heading toward the camera; gutter and cannon on the inside (-X)
         *   Vortex Funnel yaw 180  (-Z)   down the right edge, past the cannon
         *   Screw Lift 2  yaw 270  (-X)   lift along the front; its exit sits 5.5 m right of and ~4.5 m in front of the gate entry
         *   closing connector -> Start Gate entry
         */
        static List<Spec> Layout()
        {
            return new List<Spec>
            {
                new Spec { name = "Start Gate", yaw = 0f, offset = Vector2.zero, minEntryY = 0.3f, build = (p, pl, k, c) => StartGateBuilder.Build(p, pl, k, c) },
                new Spec { name = "Helix Slide", yaw = 0f, offset = new Vector2(0f, 3.0f), minEntryY = 0.3f, build = (p, pl, k, c) => HelixSlideBuilder.Build(p, pl, k, c) },
                new Spec { name = "Screw Lift 1", yaw = 0f, offset = new Vector2(0f, 3.0f), minEntryY = 0.3f, build = (p, pl, k, c) => BuildLift(p, pl, k, c, 1) },
                new Spec { name = "Peg Wall", yaw = 90f, offset = new Vector2(3.5f, 3.5f), minEntryY = 0.3f, build = (p, pl, k, c) => PegWallBuilder.Build(p, pl, k, c) },
                new Spec { name = "Bucket Wheel", yaw = 90f, offset = new Vector2(3.0f, 0f), minEntryY = 0.3f, build = (p, pl, k, c) => BucketWheelBuilder.Build(p, pl, k, c) },
                new Spec { name = "Ski Jump", yaw = 180f, offset = new Vector2(3.5f, -3.5f), minEntryY = 0.3f, build = (p, pl, k, c) => SkiJumpBuilder.Build(p, pl, k, c) },
                // The ski jump's outlet runs 9.5 m straight (past the cannon); the vortex entry is that far on, plus the 1.5 m approach.
                new Spec { name = "Vortex Funnel", yaw = 180f, offset = new Vector2(0f, -(RaceSkiLead + MachineSceneBuilder.ApproachLength)), minEntryY = 0.3f, build = (p, pl, k, c) => VortexFunnelBuilder.Build(p, pl, k, c) },
                // Anchored backward from the gate (see PlanHorizontal); its own offset is nominal. Wave Floor and Tipping Tower are left out:
                // their drops (about 1 m and 3 m) cost more lift than a single racer's drama is worth.
                new Spec { name = "Screw Lift 2", yaw = 270f, offset = new Vector2(-4f, -4f), minEntryY = 0.3f, build = (p, pl, k, c) => BuildLift(p, pl, k, c, 2) },
            };
        }

        // ------------------------------------------------------------------ menu

        [MenuItem("Simulation Lobby/3D Formats/Marble Machine (mach)/Reset Race Config (tuning baseline)", false, 332)]
        static void ResetConfig()
        {
            Directory.CreateDirectory(ConfigFolder);
            var existing = AssetDatabase.LoadAssetAtPath<MachineRaceConfig>(ConfigPath);
            if (existing == null)
            {
                EnsureConfig();
                Debug.Log($"{Tag} Created {ConfigPath} with defaults.");
                return;
            }

            MachineRaceConfig fresh = ScriptableObject.CreateInstance<MachineRaceConfig>();
            ApplyBaseline(fresh);
            EditorUtility.CopySerialized(fresh, existing);
            UnityEngine.Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Reset {ConfigPath} to the tuning baseline (16 racers, 3 laps, slope 12, ski gap 1.8).", existing);
        }

        static MachineStation BuildLift(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config, int index)
        {
            // A group per lift gives each its own hierarchy name (port mesh assets are named from it) and mesh-asset suffix.
            Transform group = MachineGeometry.Group(parent, "Lift " + index).transform;
            ScrewLiftStation lift = ScrewLiftBuilder.Build(group, placement, kit, config);
            lift.displayName = "Screw Lift " + index;
            return lift;
        }

        [MenuItem("Simulation Lobby/3D Formats/Marble Machine (mach)/Grand Prix (mach-002)", false, 312)]
        static void Build()
        {
            MachineSceneBuilder.SkiLeadOverride = RaceSkiLead;
            MachineSceneBuilder.ApproachOverrides = new System.Collections.Generic.Dictionary<string, float> { { "Screw Lift 2", RaceScrewApproach }, { "Start Gate", RaceGateApproach } };
            MachineConnectorBuilder.FilletRadius = RaceFilletRadius;
            MachineSceneBuilder.LeadAllOverride = RaceLead;
            try
            {
                BuildScene();
            }
            finally
            {
                MachineSceneBuilder.SkiLeadOverride = -1f;
                MachineSceneBuilder.ApproachOverrides = null;
                MachineConnectorBuilder.FilletRadius = 0f;
                MachineSceneBuilder.LeadAllOverride = -1f;
            }
        }

        static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{Tag} Exit play mode before building the scene.");
                return;
            }

            Directory.CreateDirectory(ConfigFolder);
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(MachineGeometry.MeshFolder);
            MachineConnectorBuilder.BeginScene();
            MachineSceneBuilder._connectorTracks.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load the config only after NewScene (a Single-mode NewScene unloads unreferenced assets).
            MachineRaceConfig config = EnsureConfig();
            if (!config.Validate(out string configError))
            {
                Debug.LogError($"{Tag} Config invalid: {configError} Run Reset Race Config (tuning baseline), then build again.", config);
                return;
            }

            // Null config: the race does not use the satin ball materials; the racers get their own glossy ones below.
            MachineKit kit = MachineNatureMaterials.EnsureKit(null); // nature kit: wood, grass, moss stone (mach-001 keeps the industrial one)
            Material[] racerMaterials = MachineMaterials.EnsureRacerMaterials(config);

            List<Spec> specs = Layout();
            int n = specs.Count;
            Probe[] probes = new Probe[n];
            for (int i = 0; i < n; i++)
            {
                probes[i] = MachineSceneBuilder.ProbeStation(specs[i], kit, config, false);
            }

            int ski = specs.FindIndex(sp => sp.name == "Ski Jump");
            Probe cannonProbe = ProbeCannon(kit, config, specs[ski].yaw);

            // ---- Plan: horizontal positions, then heights (as a cycle). ----
            // Size the screw lift: raise config.screwLiftRise until the lap's lifts repay its drops and chute slopes with LiftMargin to spare.
            for (int pass = 0; pass < 8; pass++)
            {
                Vector3[] trial = PlanHorizontal(specs, probes, cannonProbe, false);
                PlanHeights(specs, probes, trial, config, out _, out _, out _, out bool closes, out float shortfall, LiftMargin);
                if (closes)
                {
                    break;
                }

                // Two lifts share screwLiftRise, so each rise step counts twice.
                float newRise = Mathf.Min(MaxScrewRise, config.screwLiftRise + shortfall * 0.5f + 0.05f);
                Debug.Log($"{Tag} Sizing the screw lifts: lap is {shortfall:0.0} m short of lift (incl. {LiftMargin:0.0} m margin); screwLiftRise (each) {config.screwLiftRise:0.0} -> {newRise:0.0} m.");
                if (newRise <= config.screwLiftRise + 0.01f)
                {
                    Debug.LogWarning($"{Tag} screwLiftRise is at its {MaxScrewRise:0} m cap and the lap is still {shortfall:0.0} m short (margin included): shorten the course or drop a station.");
                    break;
                }

                config.screwLiftRise = newRise;
                EditorUtility.SetDirty(config);
                for (int i = 0; i < n; i++)
                {
                    if (specs[i].name.StartsWith("Screw Lift"))
                    {
                        probes[i] = MachineSceneBuilder.ProbeStation(specs[i], kit, config, false);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Vector3[] entries = PlanHorizontal(specs, probes, cannonProbe, true);
            PlanHeights(specs, probes, entries, config, out float[] slopes, out float[] lengthsPlan, out float[] floors,
                out bool loopClosed, out float deficit, 0f);

            // ---- Build for real. ----
            var machine = new GameObject("Machine");
            Transform stationRoot = MachineGeometry.Group(machine.transform, "Stations").transform;
            Transform connectorRoot = MachineGeometry.Group(machine.transform, "Connectors").transform;
            Transform ballRoot = MachineGeometry.Group(machine.transform, "Balls").transform;

            var built = new MachineStation[n];
            for (int i = 0; i < n; i++)
            {
                built[i] = specs[i].build(stationRoot, new StationPlacement(entries[i], specs[i].yaw), kit, config);
                if (built[i] == null)
                {
                    Debug.LogError($"{Tag} Builder for '{specs[i].name}' returned null; aborting.");
                    return;
                }
            }

            var gate = built[0] as StartGateStation;
            if (gate == null)
            {
                Debug.LogError($"{Tag} Station 0 is not a StartGateStation; aborting.");
                return;
            }

            // ---- Chain connectors, including the closing one. ----
            var links = new List<Link>();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                string name = $"Chute {i + 1:00} {specs[i].name} to {specs[j].name}";
                MachineSceneBuilder.BuildConnector(connectorRoot, name, built[i].exit, built[i], built[j].entry, specs[j].yaw, kit, config);
                links.Add(new Link { name = name, exit = built[i].exit, entry = built[j].entry, track = MachineSceneBuilder._connectorTracks[MachineSceneBuilder._connectorTracks.Count - 1] });
            }

            int connectorCount = n;

            // ---- Recycle: the cannon beside the ski jump, aimed at the start platform. ----
            CannonStation cannon = BuildCannon(stationRoot, connectorRoot, gate, built[ski], kit, config, ref connectorCount);

            // ---- Studio, camera. ----
            Bounds machineBounds = MachineSceneBuilder.RendererBounds(machine.transform);
            VerifyLayout(specs, built, machineBounds, cannon);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            MachineNatureEnvironment.Build(cam, machineBounds); // sky, water, sun, haze (mach-001 keeps the grey studio)
            TourCameraRig rig = cameraObject.AddComponent<TourCameraRig>();
            rig.Bind(cam);
            // Opening frame: the whole loop (the race camera director refines it from frame one).
            MachineSceneBuilder.PlaceEstablishingShot(cam, rig, machineBounds);

            // ---- Wiring. ----
            var runner = machine.AddComponent<SimulationRunner>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            var simulation = machine.AddComponent<MachineRaceSimulation>();
            simulation.Wire(new List<MachineStation>(built), gate, cannon, ballRoot, kit.ballMesh, racerMaterials);

            // ---- HUD. ----
            CaptionHud caption = CaptionCanvasBuilder.Build($"WHICH MARBLE WINS {config.laps} LAPS?",
                $"{config.racerCount} coloured marbles · {config.laps} laps · one machine", "SIMULATION LOBBY");
            // Every racer on the board, in race order. The rows tighten to fit 16 in the 1080 reference height.
            LeaderboardHud leaderboard = LeaderboardCanvasBuilder.Build("RACE ORDER", config.racerCount);
            leaderboard.fullSizeRowLimit = 8;
            leaderboard.compactRowHeight = 46f;
            MachineSceneBuilder.MoveBoardToRight(leaderboard);

            var presenterObject = new GameObject("Presenter");
            var presenter = presenterObject.AddComponent<RacePresenter>();
            presenter.boardRows = config.racerCount;
            presenter.boardTitle = "RACE ORDER";
            presenter.countdownSeconds = config.countdownSeconds;
            presenter.Bind(simulation, leaderboard, caption, rig);

            // Prove the track is continuous: roll a racer-sized ball down every chute and check every joint. FAIL lines mean a gap.
            Debug.Log(TrackContinuityTest.Run());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            LogSummary(specs, built, entries, slopes, floors, machineBounds, cannon, gate, links, connectorCount, config, loopClosed, deficit);
        }

        struct Link
        {
            public string name;
            public Transform exit;
            public Transform entry;
            public MachineSceneBuilder.ConnectorTrack track;
        }

        // ------------------------------------------------------------------ probes

        static Probe ProbeCannon(MachineKit kit, MachineConfig config, float yaw)
        {
            var holder = new GameObject("__probe").transform;
            var placement = new StationPlacement(new Vector3(0f, 2f, 0f), yaw);
            MachineStation station = CannonBuilder.Build(holder, placement, kit, config);
            Probe probe = MachineSceneBuilder.Measure(holder, station, placement);
            UnityEngine.Object.DestroyImmediate(holder.gameObject);
            return probe;
        }

        // ------------------------------------------------------------------ horizontal planning

        struct CannonLayout
        {
            public Vector2 xz;
            public Vector2 outDir;
            public Vector2 side;
        }

        /// <summary>Where the cannon rim centre goes for a ski jump entry: ahead of the gutter end, one step toward the gutter side (as mach-001).</summary>
        static CannonLayout CannonAt(Vector2 skiEntry, Probe skiProbe, float skiYaw)
        {
            Vector2 outDir = MachineSceneBuilder.YawDirection(skiYaw);
            Vector2 off = new Vector2(skiProbe.recycleOffset.x, skiProbe.recycleOffset.z);
            Vector2 right = MachineSceneBuilder.YawDirection(skiYaw + 90f);
            Vector2 side = Vector2.Dot(right, off) < 0f ? -right : right;
            Vector2 feeder = skiEntry + off;
            return new CannonLayout { xz = feeder + outDir * CannonRun + side * CannonSide, outDir = outDir, side = side };
        }

        static Vector2 ExitOffset(Probe p) => new Vector2(p.exitOffset.x, p.exitOffset.z);

        /// <summary>
        /// Forward from the gate to the vortex (the ski jump is pushed on until its cannon clears everything before it), then backward from the
        /// gate for the screw lift (the screw exit is tied to the gate entry, so the closing connector stays short; the vortex to
        /// screw connector absorbs the slack). Finally re-centred: the loop on x = 0, the front-most footprint on z = 0.
        /// </summary>
        static Vector3[] PlanHorizontal(List<Spec> specs, Probe[] probes, Probe cannonProbe, bool log)
        {
            int n = specs.Count;
            int ski = specs.FindIndex(sp => sp.name == "Ski Jump");
            int vortex = specs.FindIndex(sp => sp.name == "Vortex Funnel");
            int screw = n - 1;

            // Index n is the cannon, a pseudo-station so later stations are pushed clear of it.
            var xz = new Vector2[n + 1];
            var placed = new bool[n + 1];
            var all = new Probe[n + 1];
            Array.Copy(probes, all, n);
            all[n] = cannonProbe;

            xz[0] = Vector2.zero;
            placed[0] = true;

            for (int i = 1; i <= vortex; i++)
            {
                Vector2 exitXZ = xz[i - 1] + ExitOffset(probes[i - 1]);
                Vector2 offset = specs[i].offset;
                Vector2 dir = offset.sqrMagnitude > 1e-4f ? offset.normalized : MachineSceneBuilder.YawDirection(specs[i].yaw);
                Vector2 e = exitXZ + offset;

                if (i == ski)
                {
                    int guard = 0;
                    while (guard++ < 160 && (MachineSceneBuilder.OverlapsEarlier(i, e, xz, placed, all) || CannonHitsEarlier(i, e, xz, placed, all, probes, specs)))
                    {
                        e += dir * 0.25f;
                    }

                    if (log && guard > 1)
                    {
                        Debug.Log($"{Tag} 'Ski Jump' pushed {(guard - 1) * 0.25f:0.0} m to clear earlier stations and its cannon.");
                    }

                    xz[i] = e;
                    placed[i] = true;
                    xz[n] = CannonAt(e, probes[ski], specs[ski].yaw).xz;
                    placed[n] = true;
                }
                else
                {
                    xz[i] = MachineSceneBuilder.PushClear(i, e, dir, xz, placed, all, specs[i].name);
                    placed[i] = true;
                }
            }

            // Backward from the gate: the screw exit sits CloseX to the right of the gate entry; the front row sits low enough in Z that the
            // vortex exit can turn into the screw entry.
            Vector2 gateEntry = xz[0];
            Vector2 vortexExit = xz[vortex] + ExitOffset(probes[vortex]);

            Vector2 FrontEntry(float cz) => gateEntry + new Vector2(CloseX, cz) - ExitOffset(probes[screw]);

            float cz0 = Mathf.Min(CloseMinZ, (vortexExit.y - FrontRowDropZ) - FrontEntry(0f).y);
            xz[screw] = MachineSceneBuilder.PushClear(screw, FrontEntry(cz0), new Vector2(1f, 0f), xz, placed, all, specs[screw].name);
            placed[screw] = true;

            float dx = vortexExit.x - xz[screw].x;
            float dz = vortexExit.y - xz[screw].y;
            if (log) Debug.Log($"{Tag} Front row: screw exit is ({xz[screw].x + probes[screw].exitOffset.x - gateEntry.x:0.0}, {xz[screw].y + probes[screw].exitOffset.z - gateEntry.y:0.0}) m from the gate entry; " +
                      $"vortex exit to screw entry is ({-dx:0.0}, {-dz:0.0}) m.");
            if (log && (dx < MachineSceneBuilder.ApproachFor("Screw Lift") + 1.5f || dz < MachineSceneBuilder.LeadLength + 1.5f))
            {
                Debug.LogWarning($"{Tag} The vortex exit and the screw entry are too close to turn smoothly ({dx:0.0} m across, {dz:0.0} m down): widen the course (Layout offsets).");
            }

            // Re-centre.
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue;
            for (int i = 0; i <= n; i++)
            {
                if (!placed[i]) continue;
                Rect r = MachineSceneBuilder.Footprint(all[i], xz[i], 0f);
                minX = Mathf.Min(minX, r.xMin);
                maxX = Mathf.Max(maxX, r.xMax);
                minZ = Mathf.Min(minZ, r.yMin);
            }

            Vector2 shift = new Vector2(-(minX + maxX) * 0.5f, -minZ);
            var entries = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                entries[i] = new Vector3(xz[i].x + shift.x, 0f, xz[i].y + shift.y);
            }

            return entries;
        }

        static bool CannonHitsEarlier(int ski, Vector2 skiEntry, Vector2[] xz, bool[] placed, Probe[] all, Probe[] probes, List<Spec> specs)
        {
            int n = specs.Count;
            CannonLayout c = CannonAt(skiEntry, probes[ski], specs[ski].yaw);
            Rect mine = MachineSceneBuilder.Footprint(all[n], c.xz, MachineSceneBuilder.OverlapMargin * 0.5f);
            for (int j = 0; j < ski; j++)
            {
                if (!placed[j]) continue;
                if (mine.Overlaps(MachineSceneBuilder.Footprint(all[j], xz[j], MachineSceneBuilder.OverlapMargin * 0.5f)))
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ height planning (a cycle)

        /// <summary>
        /// Heights as the least fixpoint of lower bounds on each entry y, now around the WHOLE loop: station i's exit feeds station (i+1) mod n,
        /// the last link being the closing connector into the gate. Bounds are the station's own floor (its depth below the entry), the backward
        /// bound (next entry plus a connector at the shallowest slope: <c>targetSlopeDegrees</c>, or the ski outlet's steeper one) and the forward
        /// bound (previous exit minus a connector at the steepest planned slope). If the loop has more drop than lift the bounds chase each
        /// other upward without end; that is detected, the shortfall is reported, and the open-loop heights are used (closing link uphill).
        /// </summary>
        static void PlanHeights(List<Spec> specs, Probe[] probes, Vector3[] entries, MachineRaceConfig config,
            out float[] slopes, out float[] lengths, out float[] floors, out bool loopClosed, out float deficit, float margin = 0f)
        {
            int n = specs.Count;
            float targetDeg = Mathf.Max(config.targetSlopeDegrees, MachineConnectorBuilder.MinSlopeDegrees);
            float tanMin = Mathf.Tan(targetDeg * Mathf.Deg2Rad);
            float tanMax = Mathf.Tan(Mathf.Max(MaxPlanSlopeDegrees, targetDeg + 2f) * Mathf.Deg2Rad);
            var tanMinOf = new float[n];
            for (int i = 0; i < n; i++)
            {
                tanMinOf[i] = specs[i].name == "Ski Jump" ? Mathf.Tan(Mathf.Max(MachineSceneBuilder.SkiOutletSlopeDegrees, targetDeg) * Mathf.Deg2Rad) : tanMin;
            }

            // Local arrays: out parameters cannot be captured by the local solver below.
            var len = new float[n];
            var floor = new float[n];
            var a = new float[n];
            for (int i = 0; i < n; i++)
            {
                float depth = Mathf.Clamp(-probes[i].activityMinRel, 0f, 8f);
                floor[i] = Mathf.Max(specs[i].minEntryY, depth + 0.1f);

                int j = (i + 1) % n;
                Vector3 exitPos = entries[i] + new Vector3(probes[i].exitOffset.x, 0f, probes[i].exitOffset.z);
                MachineSceneBuilder.ChainPath(exitPos, probes[i].exitDir, entries[j], specs[j].yaw, out Vector3 from, out Vector3 to, out List<Vector3> via,
                    MachineSceneBuilder.ApproachFor(specs[j].name), MachineSceneBuilder.LeadFor(specs[i].name));
                len[i] = MachineConnectorBuilder.HorizontalLength(from, to, via);
                a[i] = probes[i].exitOffset.y - MachineSceneBuilder.LipDrop - MachineSceneBuilder.EntryClearance - probes[j].entryLift;
                if (j == 0) a[i] -= margin; // spare lift demanded of the lap (sizing only)
            }

            // The recycle cannon hangs under the ski jump's gutter: its rim must stay above CannonMinRimY.
            int ski = specs.FindIndex(sp => sp.name == "Ski Jump");
            if (ski >= 0 && probes[ski].recycleRelY < 0f)
            {
                CannonLayout c = CannonAt(new Vector2(entries[ski].x, entries[ski].z), probes[ski], specs[ski].yaw);
                float chute = RecycleChuteLength(new Vector2(entries[ski].x + probes[ski].recycleOffset.x, entries[ski].z + probes[ski].recycleOffset.z), c);
                float need = MachineSceneBuilder.CannonMinRimY + chute * tanMin - probes[ski].recycleRelY;
                floor[ski] = Mathf.Max(floor[ski], need);
            }

            var y = new float[n];
            bool Solve(bool closeLoop)
            {
                Array.Copy(floor, y, n);
                for (int iter = 0; iter < 2000; iter++)
                {
                    bool changed = false;
                    for (int i = 0; i < n; i++)
                    {
                        int j = (i + 1) % n;
                        if (j == 0 && !closeLoop) continue;

                        float back = y[j] - a[i] + len[i] * tanMinOf[i];
                        if (back > y[i] + 1e-4f)
                        {
                            y[i] = back;
                            changed = true;
                        }

                        float fwd = y[i] + a[i] - len[i] * tanMax;
                        if (fwd > y[j] + 1e-4f)
                        {
                            y[j] = fwd;
                            changed = true;
                        }
                    }

                    if (!changed) return true;
                    for (int i = 0; i < n; i++)
                    {
                        if (y[i] > HeightCap) return false;
                    }
                }

                return false;
            }

            loopClosed = Solve(true);
            deficit = 0f;
            if (!loopClosed)
            {
                Solve(false);
                // Missing lift: how much higher the screw exit would have to be for the closing connector to reach the gate at the target slope.
                deficit = (y[0] - a[n - 1] + len[n - 1] * tanMinOf[n - 1]) - y[n - 1];
            }

            for (int i = 0; i < n; i++)
            {
                entries[i].y = y[i];
            }

            lengths = len;
            floors = floor;
            slopes = new float[n];
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float drop = y[i] + probes[i].exitOffset.y - MachineSceneBuilder.LipDrop - (y[j] + MachineSceneBuilder.EntryClearance + probes[j].entryLift);
                slopes[i] = Mathf.Atan2(drop, Mathf.Max(0.01f, len[i])) * Mathf.Rad2Deg;
            }
        }

        /// <summary>Horizontal length of the gutter-end to cannon-rim chute (lead out of the lip, then a straight approach into the rim centre).</summary>
        static float RecycleChuteLength(Vector2 feederXZ, CannonLayout c)
        {
            var from = new Vector3(feederXZ.x, 0f, feederXZ.y);
            var to = new Vector3(c.xz.x, 0f, c.xz.y);
            var via = new List<Vector3>
            {
                from + new Vector3(c.outDir.x, 0f, c.outDir.y) * (MachineSceneBuilder.LipAhead + MachineSceneBuilder.LeadLength),
                to - new Vector3(c.outDir.x, 0f, c.outDir.y) * MachineSceneBuilder.ApproachLength
            };
            return MachineConnectorBuilder.HorizontalLength(from, to, via);
        }

        // ------------------------------------------------------------------ cannon

        /// <summary>
        /// The recycle cannon stands beside the ski jump's gutter end (the inside of the loop), its rim at the end of a chute that falls at
        /// least <c>targetSlopeDegrees</c>. It lobs onto the start platform (<see cref="IStartGate.CannonTarget"/>) and scatters only ~0.5 m
        /// so a recycled racer lands inside the 2.2 m wide platform.
        /// </summary>
        static CannonStation BuildCannon(Transform stationRoot, Transform connectorRoot, StartGateStation gate, MachineStation ski,
            MachineKit kit, MachineRaceConfig config, ref int connectorCount)
        {
            Transform feeder = ski.recycleExit != null ? ski.recycleExit : ski.exit;
            Vector3 f = feeder.forward;
            Vector2 outDir = new Vector2(f.x, f.z);
            outDir = outDir.sqrMagnitude > 0.25f ? outDir.normalized : MachineSceneBuilder.YawDirection(ski.transform.eulerAngles.y);
            Vector3 right3 = ski.transform.right;
            Vector2 side = new Vector2(right3.x, right3.z).normalized;
            if (Vector2.Dot(side, new Vector2(feeder.position.x - ski.transform.position.x, feeder.position.z - ski.transform.position.z)) < 0f)
            {
                side = -side;
            }

            Vector2 exitXZ = new Vector2(feeder.position.x, feeder.position.z);
            Vector2 cannonXZ = exitXZ + outDir * CannonRun + side * CannonSide;
            Vector3 out3 = new Vector3(outDir.x, 0f, outDir.y);

            Vector3 from = feeder.position + out3 * MachineSceneBuilder.LipAhead + Vector3.down * MachineSceneBuilder.LipDrop;
            var via = new List<Vector3>
            {
                feeder.position + out3 * (MachineSceneBuilder.LipAhead + MachineSceneBuilder.LeadLength),
                new Vector3(cannonXZ.x, 0f, cannonXZ.y) - out3 * MachineSceneBuilder.ApproachLength
            };
            float run = MachineConnectorBuilder.HorizontalLength(from, new Vector3(cannonXZ.x, from.y, cannonXZ.y), via);
            float rimY = from.y - run * Mathf.Tan(Mathf.Max(config.targetSlopeDegrees, MachineConnectorBuilder.MinSlopeDegrees) * Mathf.Deg2Rad) - MachineSceneBuilder.EntryClearance;
            if (rimY < MachineSceneBuilder.CannonMinRimY)
            {
                Debug.LogWarning($"{Tag} Cannon rim would be at y {rimY:0.00} (< {MachineSceneBuilder.CannonMinRimY} m): its deck would sink into the floor. Raise the ski jump.");
            }

            float yaw = Mathf.Atan2(outDir.x, outDir.y) * Mathf.Rad2Deg;
            Vector3 entry = new Vector3(cannonXZ.x, Mathf.Max(rimY, MachineSceneBuilder.CannonMinRimY), cannonXZ.y);
            CannonStation cannon = CannonBuilder.Build(stationRoot, new StationPlacement(entry, yaw), kit, config);
            cannon.scatterRadius = 0.5f;

            var aim = new GameObject("Cannon Target").transform;
            aim.SetParent(stationRoot.parent, false);
            aim.position = gate.CannonTarget;
            cannon.aimTarget = aim;

            Vector3 to = cannon.entry.position + Vector3.up * MachineSceneBuilder.EntryClearance;
            MachineSceneBuilder.Lengthen(from, to, via, config);
            const string name = "Recycle Ski Jump to Cannon";
            MachineConnectorBuilder.Build(connectorRoot, name, from, to, kit, config, via);
            MachineSceneBuilder._connectorTracks.Add(new MachineSceneBuilder.ConnectorTrack { name = name, from = from, to = to, via = via, owner = null });
            connectorCount++;

            float drop = from.y - to.y;
            float length = MachineConnectorBuilder.HorizontalLength(from, to, via);
            Debug.Log($"{Tag} Cannon rim at {MachineSceneBuilder.Fmt(cannon.entry.position)}; recycle chute {length:0.0} m at {Mathf.Atan2(drop, Mathf.Max(0.01f, length)) * Mathf.Rad2Deg:0.0} deg; " +
                      $"it lobs to the start platform at {MachineSceneBuilder.Fmt(gate.CannonTarget)} ({Vector3.Distance(cannon.entry.position, gate.CannonTarget):0.0} m).");
            return cannon;
        }

        // ------------------------------------------------------------------ config

        static MachineRaceConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<MachineRaceConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            Directory.CreateDirectory(ConfigFolder);
            config = ScriptableObject.CreateInstance<MachineRaceConfig>();
            ApplyBaseline(config);
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static void ApplyBaseline(MachineRaceConfig config)
        {
            config.fixedTimestep = 1f / 100f;
            config.maxDurationSeconds = 900f;
            config.racerCount = 16;
            config.ballCount = 16;
            config.laps = 3;
            config.ballRadius = 0.2f;
            config.ballMass = 0.2f;
            config.tubeCapacity = 1;
            config.chuteWidth = 1.8f;
            config.tipperThreshold = 2;
            config.targetSlopeDegrees = 12f;
            config.skiJumpGap = 1.8f;
            config.screwDegreesPerSecond = 330f;
            config.screwLiftRise = 10f; // the build raises this until the lap's lifts repay its drops (see Build)
            config.holdSeconds = 6f;
            config.countdownSeconds = 3f;
            config.gridColumns = 4;
            config.killPlaneY = -2f;
            config.maxBallSpeed = 14f;
            config.colours = MachineRaceConfig.DefaultRacerColours();
            config.intent = "mach-002: 16 numbered marbles loop the marble machine 3 times. Which number crosses the line first? " +
                            "Tune laps so a lap is 70-110 s and the winner lands at 5:00-7:00; tune skiJumpGap for 10-20% misses.";
        }

        // ------------------------------------------------------------------ checks + logs

        static void VerifyLayout(List<Spec> specs, MachineStation[] built, Bounds machineBounds, CannonStation cannon)
        {
            int n = built.Length;
            var bounds = new Bounds[n + 1];
            var names = new string[n + 1];
            for (int i = 0; i < n; i++)
            {
                bounds[i] = MachineSceneBuilder.RendererBounds(built[i].transform);
                names[i] = specs[i].name;
            }

            bounds[n] = MachineSceneBuilder.RendererBounds(cannon.transform);
            names[n] = "Cannon";

            int overlaps = 0;
            for (int i = 0; i <= n; i++)
            {
                for (int j = i + 1; j <= n; j++)
                {
                    Bounds a = bounds[i];
                    a.Expand(MachineSceneBuilder.OverlapMargin);
                    if (a.Intersects(bounds[j]))
                    {
                        overlaps++;
                        Debug.LogWarning($"{Tag} '{names[i]}' and '{names[j]}' bounds overlap (margin {MachineSceneBuilder.OverlapMargin} m): {a} vs {bounds[j]}.");
                    }
                }
            }

            int clashes = 0;
            foreach (MachineSceneBuilder.ConnectorTrack c in MachineSceneBuilder._connectorTracks)
            {
                for (int i = 0; i <= n; i++)
                {
                    if (MachineSceneBuilder.PolylineEntersBounds(c, bounds[i]))
                    {
                        clashes++;
                        Debug.LogWarning($"{Tag} Connector '{c.name}' passes through the bounds of '{names[i]}'.");
                    }
                }
            }

            // Camera side: the camera sits on -Z, so a cannon standing in the front third of the course could block the view.
            Bounds cb = bounds[n];
            float frontThird = machineBounds.min.z + machineBounds.size.z / 3f;
            if (cb.center.z < frontThird)
            {
                Debug.LogWarning($"{Tag} The cannon (centre z {cb.center.z:0.0}) is in the front third of the course (z < {frontThird:0.0}): it may block the camera.");
            }

            if (overlaps + clashes > 0)
            {
                Debug.LogWarning($"{Tag} Layout check: {overlaps} station overlaps, {clashes} connector clashes. Tune the offset table in MachineRaceSceneBuilder.Layout.");
            }
            else
            {
                Debug.Log($"{Tag} Layout check: no station overlaps, no connector clashes.");
            }
        }

        static void LogSummary(List<Spec> specs, MachineStation[] built, Vector3[] entries, float[] slopes, float[] floors, Bounds machineBounds,
            CannonStation cannon, StartGateStation gate, List<Link> links, int connectorCount, MachineRaceConfig config, bool loopClosed, float deficit)
        {
            int n = built.Length;
            var sb = new StringBuilder();
            sb.AppendLine($"{Tag} Station table (world metres)");
            sb.AppendLine("  #  station          entry y  floor  exit y  top y   entry (x,z)        slope to next");
            float tallest = 0f;
            string tallestName = "";
            for (int i = 0; i < n; i++)
            {
                Bounds b = MachineSceneBuilder.RendererBounds(built[i].transform);
                if (b.max.y > tallest)
                {
                    tallest = b.max.y;
                    tallestName = specs[i].name;
                }

                Vector3 e = built[i].entry != null ? built[i].entry.position : entries[i];
                Vector3 x = built[i].exit != null ? built[i].exit.position : e;
                sb.AppendLine($"  {i + 1,2} {specs[i].name,-16} {e.y,6:0.00}  {floors[i],5:0.00}  {x.y,6:0.00}  {b.max.y,5:0.00}   ({e.x:0.0}, {e.z:0.0})   {slopes[i]:0.0} deg{(i == n - 1 ? " (closing)" : string.Empty)}");
            }

            Bounds cb = MachineSceneBuilder.RendererBounds(cannon.transform);
            sb.AppendLine($"   R {"Cannon",-16} {cannon.entry.position.y,6:0.00}        -       -  {cb.max.y,5:0.00}   ({cannon.entry.position.x:0.0}, {cannon.entry.position.z:0.0})   recycle");
            Debug.Log(sb.ToString());

            // ---- Continuity + slope per connector. ----
            float targetDeg = config.targetSlopeDegrees;
            var cs = new StringBuilder();
            cs.AppendLine($"{Tag} Connectors (continuity: start/end distance to the station anchors; slope; horizontal length)");
            float minSlope = float.MaxValue, maxSlope = float.MinValue, totalChute = 0f, total3D = 0f;
            int bad = 0;
            foreach (Link l in links)
            {
                MachineSceneBuilder.ConnectorTrack t = l.track;
                float startGap = Vector3.Distance(t.from, l.exit.position);
                float endGap = Vector3.Distance(t.to, l.entry.position);
                float len = MachineConnectorBuilder.HorizontalLength(t.from, t.to, t.via);
                float drop = t.from.y - t.to.y;
                float slope = Mathf.Atan2(drop, Mathf.Max(0.01f, len)) * Mathf.Rad2Deg;
                string flag = string.Empty;
                if (startGap > 0.02f || endGap > 0.02f) flag += " GAP";
                if (drop <= 0f) flag += " UPHILL";
                else if (slope < targetDeg - 0.5f) flag += " SHALLOW";
                if (slope > MaxPlanSlopeDegrees + 2f) flag += " STEEP";
                if (flag.Length > 0) bad++;
                minSlope = Mathf.Min(minSlope, slope);
                maxSlope = Mathf.Max(maxSlope, slope);
                totalChute += len;
                total3D += Mathf.Sqrt(len * len + drop * drop);
                cs.AppendLine($"  {l.name,-52} {len,5:0.0} m  {slope,5:0.0} deg  drop {drop,5:0.00}  gaps {startGap:0.000}/{endGap:0.000}{flag}");
            }

            cs.AppendLine($"  slope min {minSlope:0.0} deg, max {maxSlope:0.0} deg (target >= {targetDeg:0.0}); {links.Count} connectors, {totalChute:0.0} m of chute; " +
                          (bad == 0 ? "continuity OK (no gaps, all downhill, none shallow)." : $"{bad} connector(s) flagged above."));
            if (bad == 0) Debug.Log(cs.ToString()); else Debug.LogWarning(cs.ToString());

            // ---- Lap length + rough lap time. ----
            float stationPath = 0f;
            for (int i = 0; i < n; i++)
            {
                if (built[i].entry != null && built[i].exit != null)
                {
                    stationPath += Vector3.Distance(built[i].entry.position, built[i].exit.position);
                }
            }

            const float helixCoils = 2f * (2f * Mathf.PI * 2.6f); // two turns round a 2.6 m radius, beyond the entry-to-exit chord
            float lapLength = total3D + stationPath + helixCoils;
            float screwRise = 0f;
            for (int i = 0; i < n; i++)
            {
                if (specs[i].name.StartsWith("Screw Lift")) screwRise += built[i].exit.position.y - built[i].entry.position.y;
            }
            float lapSeconds = total3D / 4.5f + (stationPath + helixCoils) / 3f + 8f + screwRise / 0.65f;
            Debug.Log($"{Tag} Lap estimate: chutes {total3D:0.0} m + station chords {stationPath:0.0} m + helix coils {helixCoils:0.0} m = ~{lapLength:0.0} m per lap; " +
                      $"rough lap time ~{lapSeconds:0} s ({config.laps} laps ~{lapSeconds * config.laps / 60f:0.0} min) at 4.5 m/s on chutes, 3 m/s in stations, plus 8 s wheel and the screw's " +
                      $"{screwRise:0.0} m climb at 0.65 m/s. Target lap 70-110 s; raise laps if short. Real lap time comes from the run.");

            // ---- Lap line vs closing connector. ----
            Link closing = links[links.Count - 1];
            Vector3 lapPoint = gate.LapLinePoint;
            Vector3 screwEnd = closing.track.from;
            float dH = Vector2.Distance(new Vector2(lapPoint.x, lapPoint.z), new Vector2(screwEnd.x, screwEnd.z));
            float d3 = Vector3.Distance(lapPoint, screwEnd);
            string lapVerdict = d3 <= 10f ? "within 10 m" : "OVER 10 m";
            string line = $"{Tag} Lap line {MachineSceneBuilder.Fmt(lapPoint)} is {d3:0.0} m ({dH:0.0} m horizontal) from the screw-lift end of the closing connector {MachineSceneBuilder.Fmt(screwEnd)}: {lapVerdict}.";
            if (d3 <= 10f) Debug.Log(line); else Debug.LogWarning(line);

            // ---- Height closure. ----
            if (loopClosed)
            {
                Debug.Log($"{Tag} Height loop CLOSES: every connector (including the closing one) slopes down at >= {targetDeg:0.0} deg.");
            }
            else
            {
                Debug.LogError($"{Tag} HEIGHT LOOP DOES NOT CLOSE. In one lap the stations and connectors drop more than the two lifts raise, so the closing connector " +
                               $"(Screw Lift to Start Gate) would have to climb ~{deficit:0.0} m. Built open-loop: that connector runs UPHILL and racers cannot finish a lap. " +
                               "Fixes: a taller screw lift (ScrewLiftBuilder.RiseTarget is a fixed 6.1 m), more lift stations in Layout, fewer/lower-drop stations, a lower targetSlopeDegrees, " +
                               "or a shorter course (every metre of chute costs tan(slope) metres of height).");
            }

            Debug.Log($"{Tag} Tallest point {tallest:0.0} m ({tallestName}); machine bounds centre {MachineSceneBuilder.Fmt(machineBounds.center)} size {MachineSceneBuilder.Fmt(machineBounds.size)}; " +
                      $"start gate entry y {entries[0].y:0.0}.");

            int warnings = MachineConnectorBuilder.WarningCount;
            Debug.Log($"{Tag} Built {ScenePath}: {n + 1} stations (incl. cannon), {config.racerCount} racers x {config.laps} laps, " +
                      $"{connectorCount} connectors ({warnings} warnings). Seed is random each Play; the runner logs it.");
        }
    }
}
