using System.Collections.Generic;
using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using SimulationLobby.Shared.EditorTools;
using SimulationLobby.Shared.Tracks;
using SimulationLobby.Simulations.Survival.LavaRun;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Sample = SimulationLobby.Shared.EditorTools.TrackMeshBuilder.Sample;

namespace SimulationLobby.Simulations.Survival.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Scenes/Survival_LavaRun_3D.unity</c> (surv-002): a volcano with three stages of track stacked down its
    /// face, a lava lake at the base and a sinking island at the end of a railless bridge.
    /// Menu: <c>Simulation Lobby ▸ 3D Formats ▸ Survival (surv) ▸ Lava Run (surv-002)</c>.
    /// </summary>
    /// <remarks>
    /// Re-running replaces the scene but never the config, layout, material or texture assets, so tuning survives.
    /// The geometry is placed top-down in the plane (each stage starts beneath the previous stage's basin) and
    /// bottom-up in height (the island top fixes stage 3, which fixes stage 2, which fixes stage 1), so a changed layout
    /// moves the whole stack coherently instead of leaving a gap.
    /// </remarks>
    static partial class LavaRunSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Survival_LavaRun_3D.unity";
        const string ConfigFolder = "Assets/Settings/Configs/Survival";
        const string ConfigPath = ConfigFolder + "/LavaRunConfig_001.asset";

        // Island and stack dimensions (metres).
        const float IslandTopY = 1.5f;
        const float IslandRadius = 15f;
        const float RingThickness = 1.2f;
        const float LipHeight = 0.05f;
        const float LipWidth = 0.18f;
        const float DiscRadius = 3f;
        const float GapDrop = 6.5f;
        const float CraterRise = 10.5f;
        const float ConeSlope = 0.6f;
        const float MarbleLinearDamping = 0.08f;

        /// <summary>Polar angle (degrees, atan2(z, x)) where stage 1's first leg starts. -90 is the front of the volcano.</summary>
        const float Stage1StartAngle = -131.3f;

        static readonly float[] RingEdges = { 15f, 12.6f, 10.2f, 7.8f, 5.4f, 3f };

        sealed class StageData
        {
            public int index;
            public string title;
            public TrackLayout layout;
            public List<Sample> local;
            public List<Sample> world;
            public Vector2 leadPoint;
            public Vector2 leadDirection;
            public float offsetY;
            public int basinSection = -1;
            public int boundary;
            public int bodyEnd;
            public float minY;
            public float basinStartDistance;
            public Transform root;
            public TrackPath path;
            public Vector3 cameraTarget;
            public Vector3 cameraPosition;
            public Vector2 textUp;
        }

        static LavaRunConfig _config;
        static LavaRunMaterials.Kit _kit;
        static StageData[] _stages;
        static Vector3 _islandCentre;
        static Vector3 _bridgeDirection;
        static float _rimY;
        static float _coneBaseRadius;
        static VolcanoTerrain _terrain;
        static int _warnings;
        static int _obstacleErrors;
        static Renderer _lakeRenderer;
        static Transform _craterPoint;

        // ------------------------------------------------------------------ menu

        [MenuItem("Simulation Lobby/3D Formats/Survival (surv)/Lava Run (surv-002)", false, 232)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[surv-lava] Exit play mode before building the scene.");
                return;
            }

            _warnings = 0;
            _obstacleErrors = 0;
            Directory.CreateDirectory(ConfigFolder);
            Directory.CreateDirectory(LavaRunMaterials.Folder);
            Directory.CreateDirectory(LavaRunMaterials.MeshFolder);
            Directory.CreateDirectory("Assets/Scenes");

            _config = EnsureConfig();
            var layouts = new TrackLayout[LavaRunConfig.StageCount];
            for (int i = 0; i < layouts.Length; i++)
            {
                layouts[i] = EnsureLayout(i, false);
            }

            _kit = LavaRunMaterials.Ensure(_config);
            PlaceStages(layouts);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Lava Run Rig").transform;
            var rig = root.gameObject.AddComponent<LavaRunRig>();

            Camera camera = BuildCamera();
            Transform environment = new GameObject("Environment").transform;
            BuildTerrain(environment);
            BuildLake(environment, rig);
            BuildCrater(environment, out Transform craterPoint);

            var stageRigs = new LavaStageRig[LavaRunConfig.StageCount];
            for (int i = 0; i < stageRigs.Length; i++)
            {
                stageRigs[i] = BuildStage(_stages[i], root);
            }

            rig.stages = stageRigs;
            BuildIsland(root, rig, stageRigs[LavaRunConfig.StageCount - 1]);
            rig.killPlaneY = -20f;
            BuildMarbles(root, rig, out GameObject[] ghosts);

            BuildLighting(environment, camera, out Vector3 widePosition, out Vector3 wideTarget);
            BuildEmbers(environment);

            // --- Camera poses the presenter drives. ---
            var poseRoot = new GameObject("Camera Poses").transform;
            var poses = new Transform[4];
            poses[0] = NewPose(poseRoot, "Pose Stage 1", _stages[0].cameraPosition, _stages[0].cameraTarget);
            poses[1] = NewPose(poseRoot, "Pose Stage 2", _stages[1].cameraPosition, _stages[1].cameraTarget);
            poses[2] = NewPose(poseRoot, "Pose Stage 3 (bridge)", _stages[2].cameraPosition, _stages[2].cameraTarget);
            IslandPose(out Vector3 islandPosition, out Vector3 islandTarget);
            poses[3] = NewPose(poseRoot, "Pose Island", islandPosition, islandTarget);
            Transform widePose = NewPose(poseRoot, "Pose Wide (volcano + island)", widePosition, wideTarget);

            Transform hookPose = NewPose(poseRoot, "Pose Hook (pens + crater)", HookPosition(out Vector3 hookTarget), hookTarget);

            FocusCameraRig focus = camera.gameObject.AddComponent<FocusCameraRig>();
            focus.Bind(camera);
            focus.SetHome(widePosition, wideTarget);

            // --- HUD ---
            BuildHud(out CaptionHud caption, out ShareBarHud counterBar, out TMP_Text question);

            // --- Simulation + presentation ---
            var simulationObject = new GameObject("Simulation");
            LavaRunSimulation simulation = simulationObject.AddComponent<LavaRunSimulation>();
            var runner = simulationObject.GetComponent<SimulationRunner>();
            runner.config = _config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;
            simulation.Bind(rig);

            BuildPresentation(simulation, focus, poses, widePose, caption, counterBar, question, craterPoint, ghosts, hookPose);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Report(layouts);
        }

        [MenuItem("Simulation Lobby/3D Formats/Survival (surv)/Reset Lava Run Layouts (default stages)", false, 253)]
        static void ResetLayouts()
        {
            Directory.CreateDirectory(ConfigFolder);
            for (int i = 0; i < LavaRunConfig.StageCount; i++)
            {
                TrackLayout layout = EnsureLayout(i, true);
                Debug.Log($"[surv-lava] Reset {ConfigFolder}/{LavaRunLayouts.FileName(i)}.asset ({layout.sections.Length} sections, " +
                          $"{layout.obstacles.Length} obstacles). Rebuild the scene to see it.", layout);
            }

            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ assets

        static TrackLayout EnsureLayout(int stage, bool reset)
        {
            string path = $"{ConfigFolder}/{LavaRunLayouts.FileName(stage)}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TrackLayout>(path);
            if (existing != null)
            {
                if (!reset && (existing.sections == null || existing.sections.Length != LavaRunLayouts.ExpectedSectionCount(stage)))
                {
                    // Written by an older builder with a different section list; the gate and basin indices would not match.
                    reset = true;
                    Debug.LogWarning($"[surv-lava] {path} has {existing.sections?.Length ?? 0} sections, expected " +
                                     $"{LavaRunLayouts.ExpectedSectionCount(stage)}: re-applying the default layout.", existing);
                }

                if (reset)
                {
                    LavaRunLayouts.Apply(existing, stage);
                    EditorUtility.SetDirty(existing);
                }
                else if (stage == 2 && existing.sections != null && existing.sections.Length > LavaRunLayouts.BridgeSection &&
                         existing.sections[LavaRunLayouts.BridgeSection].railless &&
                         existing.sections[LavaRunLayouts.BridgeSection].length > 100f)
                {
                    // An earlier build wrote a 190 m bridge; shorten it in place without touching other hand edits.
                    TrackSection bridge = existing.sections[LavaRunLayouts.BridgeSection];
                    bridge.length = LavaRunLayouts.BridgeLength;
                    existing.sections[LavaRunLayouts.BridgeSection] = bridge;
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[surv-lava] Shortened the stage 3 bridge in {path} to {LavaRunLayouts.BridgeLength:0} m.", existing);
                }

                return existing;
            }

            var layout = ScriptableObject.CreateInstance<TrackLayout>();
            LavaRunLayouts.Apply(layout, stage);
            AssetDatabase.CreateAsset(layout, path);
            return layout;
        }

        static LavaRunConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LavaRunConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<LavaRunConfig>();
            config.fixedTimestep = 1f / 60f;
            config.maxDurationSeconds = 900f;
            config.intent = "surv-002: four colours multiply down a volcano ahead of the lava; the smallest colour is out at each door; " +
                            "survivors cross a railless bridge onto a sinking island. Tune for lead changes and a 10-12 min run.";
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        // ------------------------------------------------------------------ placement

        static void PlaceStages(TrackLayout[] layouts)
        {
            _stages = new StageData[LavaRunConfig.StageCount];
            for (int i = 0; i < _stages.Length; i++)
            {
                var data = new StageData
                {
                    index = i,
                    title = LavaRunLayouts.StageNames[i],
                    layout = layouts[i],
                    basinSection = LavaRunLayouts.BasinSection(i)
                };
                data.local = TrackMeshBuilder.SampleCentreline(data.layout, Vector3.zero);
                data.boundary = data.basinSection >= 0 ? BoundaryIndex(data.local, data.basinSection) : data.local.Count - 1;
                _stages[i] = data;
            }

            // Plan view, first pass (height zero): stage 1 starts its first leg at a chosen angle around the volcano axis; each
            // later stage starts beneath the previous stage's basin, running back the way it came.
            StageData s1 = _stages[0];
            StageData s2 = _stages[1];
            StageData s3 = _stages[2];

            float r1 = Mathf.Max(5f, s1.layout.sections[LavaRunLayouts.LeadSection].radius);
            float a1 = Stage1StartAngle * Mathf.Deg2Rad;
            s1.leadPoint = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r1;
            s1.leadDirection = ContourDirection(a1, s1.layout.sections[LavaRunLayouts.LeadSection].arcDegrees);
            Place(s1, 0f);

            Sample e1 = s1.world[s1.boundary];
            float r2 = Mathf.Max(5f, s2.layout.sections[LavaRunLayouts.LeadSection].radius);
            float a2 = Mathf.Atan2(e1.position.z, e1.position.x) - 6f / r2;
            s2.leadPoint = new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * r2;
            s2.leadDirection = ContourDirection(a2, s2.layout.sections[LavaRunLayouts.LeadSection].arcDegrees);
            Place(s2, 0f);

            Sample e2 = s2.world[s2.boundary];
            Vector2 h2 = new Vector2(e2.forward.x, e2.forward.z).normalized;
            Vector2 out2 = new Vector2(e2.position.x, e2.position.z).normalized;
            s3.leadPoint = new Vector2(e2.position.x, e2.position.z) - h2 * 6f + out2 * 7f;
            s3.leadDirection = -h2;
            Place(s3, 0f);

            // Height, bottom-up: the island top fixes stage 3's pen floor, whose pens sit GapDrop under stage 2's basin floor, etc.
            float y3 = IslandTopY - s3.local[s3.local.Count - 1].position.y;
            float y2 = y3 + GapDrop - s2.local[s2.boundary].position.y;
            float y1 = y2 + GapDrop - s1.local[s1.boundary].position.y;
            Place(s3, y3);
            Place(s2, y2);
            Place(s1, y1);

            _rimY = y1 + CraterRise;
            _coneBaseRadius = 19f + _rimY / ConeSlope;

            for (int i = 0; i < _stages.Length; i++)
            {
                StageData data = _stages[i];
                data.bodyEnd = data.boundary;
                data.minY = float.MaxValue;
                foreach (Sample sample in data.world)
                {
                    data.minY = Mathf.Min(data.minY, sample.position.y);
                }

                data.basinStartDistance = data.world[data.boundary].distance;
            }

            // Island and bridge direction come from where stage 3 actually ends.
            Sample end = s3.world[s3.world.Count - 1];
            _bridgeDirection = new Vector3(end.forward.x, 0f, end.forward.z).normalized;
            _islandCentre = new Vector3(end.position.x, IslandTopY, end.position.z) + _bridgeDirection * (IslandRadius - 0.1f);

            PlanCameras();
        }

        static void Place(StageData s, float offsetY)
        {
            float yaw = Mathf.Atan2(s.leadDirection.x, s.leadDirection.y) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.AngleAxis(yaw, Vector3.up);
            Vector3 lead = rotation * s.local[BoundaryIndex(s.local, LavaRunLayouts.LeadSection)].position;
            s.offsetY = offsetY;
            var offset = new Vector3(s.leadPoint.x - lead.x, offsetY, s.leadPoint.y - lead.z);
            s.world = Transformed(s.local, rotation, offset);
        }

        static List<Sample> Transformed(List<Sample> local, Quaternion rotation, Vector3 offset)
        {
            var result = new List<Sample>(local.Count);
            foreach (Sample sample in local)
            {
                Sample t = sample;
                t.position = rotation * sample.position + offset;
                t.forward = rotation * sample.forward;
                t.right = rotation * sample.right;
                t.up = rotation * sample.up;
                result.Add(t);
            }

            return result;
        }

        /// <summary>Tangent of a circle about the volcano axis at polar angle <paramref name="angle"/>, in the turning sense of <paramref name="arcDegrees"/>.</summary>
        static Vector2 ContourDirection(float angle, float arcDegrees)
        {
            // A left turn (positive arc) circles the axis counter-clockwise in (x, z); a right turn circles it clockwise.
            return arcDegrees >= 0f
                ? new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle))
                : new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
        }

        /// <summary>Index of the last sample of the section before <paramref name="section"/>: exactly where the section begins.</summary>
        static int BoundaryIndex(List<Sample> samples, int section)
        {
            if (section <= 0)
            {
                return 0;
            }

            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].section >= section)
                {
                    return Mathf.Max(0, i - 1);
                }
            }

            return samples.Count - 1;
        }

        static float SectionStart(List<Sample> samples, int section)
        {
            return samples[BoundaryIndex(samples, section)].distance;
        }

        // ------------------------------------------------------------------ camera framing

        static void PlanCameras()
        {
            for (int i = 0; i < 2; i++)
            {
                StageData s = _stages[i];
                Vector3 sum = Vector3.zero;
                int count = 0;
                int first = BoundaryIndex(s.world, LavaRunLayouts.LeadSection);
                for (int k = first; k <= s.boundary; k++)
                {
                    sum += s.world[k].position;
                    count++;
                }

                Vector3 target = sum / Mathf.Max(1, count);
                Vector3 outward = new Vector3(target.x, 0f, target.z).normalized;
                Vector3 offset = Quaternion.AngleAxis(20f, Vector3.up) * outward * 40f + Vector3.up * 26f;
                s.cameraTarget = target;
                s.cameraPosition = target + offset.normalized * 50f;
                s.textUp = new Vector2(-outward.x, -outward.z);
            }

            StageData bridge = _stages[2];
            Sample neck = bridge.world[BoundaryIndex(bridge.world, LavaRunLayouts.NeckSection)];
            Sample focus = TrackMeshBuilder.At(bridge.world, neck.distance + 40f);
            Vector3 b = _bridgeDirection;
            Vector3 side = Vector3.Cross(Vector3.up, b);
            Vector3 view = -b * 34f + Vector3.up * 26f + side * 14f;
            bridge.cameraTarget = focus.position;
            bridge.cameraPosition = focus.position + view.normalized * 50f;
            bridge.textUp = new Vector2(b.x, b.z);
        }

        static void IslandPose(out Vector3 position, out Vector3 target)
        {
            Vector3 side = Vector3.Cross(Vector3.up, _bridgeDirection);
            Vector3 view = -_bridgeDirection * 34f + Vector3.up * 28f + side * 20f;
            target = _islandCentre + Vector3.up;
            position = target + view.normalized * 50f;
        }

        /// <summary>Close on the four stage-1 pens from the outside, so the crater glows behind and above them.</summary>
        static Vector3 HookPosition(out Vector3 target)
        {
            StageData s = _stages[0];
            Vector3 pad = TrackMeshBuilder.At(s.world, LavaRunLayouts.PenLength * 0.5f).position;
            Vector3 crater = new Vector3(0f, _rimY - 2.5f, 0f);
            Vector3 outward = new Vector3(pad.x, 0f, pad.z).normalized;
            Vector3 forward = new Vector3(s.world[0].forward.x, 0f, s.world[0].forward.z).normalized;
            // Behind the back wall and out toward the cone's edge: the four pens read left to right, the crater sits up-left beyond them.
            Vector3 position = pad - forward * 3.5f + outward * 3.5f + Vector3.up * 2.7f;
            target = Vector3.Lerp(pad, crater, 0.03f);
            return position;
        }

        static Transform NewPose(Transform parent, string name, Vector3 position, Vector3 target)
        {
            var pose = new GameObject(name).transform;
            pose.SetParent(parent, false);
            pose.position = position;
            pose.rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
            return pose;
        }

        // ------------------------------------------------------------------ stage

        static LavaStageRig BuildStage(StageData s, Transform rigRoot)
        {
            var root = new GameObject($"Stage {s.index + 1} - {s.title}").transform;
            root.SetParent(rigRoot, false);
            s.root = root;

            // Body: the channel up to the basin (stages 1-2) or all the way to the island edge (stage 3).
            List<Sample> bodySamples = s.world.GetRange(0, s.bodyEnd + 1);
            Mesh bodyMesh = LavaRunGeometry.SaveMeshFresh(TrackMeshBuilder.BuildBody(bodySamples), $"LavaRun_Stage{s.index + 1}Body");

            var track = new GameObject("Track");
            track.transform.SetParent(root, false);
            track.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            var trackRenderer = track.AddComponent<MeshRenderer>();
            trackRenderer.sharedMaterials = new[] { _kit.track, _kit.trim };
            var trackCollider = track.AddComponent<MeshCollider>();
            trackCollider.sharedMesh = bodyMesh;
            trackCollider.sharedMaterial = _kit.trackPhysics;

            // Centreline for lava-front distance and marble progress. Stage 3 continues across the island so progress is defined there.
            var points = new List<Vector3>(s.world.Count + 8);
            foreach (Sample sample in s.world)
            {
                points.Add(sample.position);
            }

            if (s.basinSection < 0)
            {
                Vector3 last = s.world[s.world.Count - 1].position;
                for (int k = 1; k <= 5; k++)
                {
                    points.Add(last + _bridgeDirection * (IslandRadius * k / 5f));
                }
            }

            TrackPath path = track.AddComponent<TrackPath>();
            path.Set(points.ToArray());
            s.path = path;

            var kit = new ObstacleBuilder.Kit
            {
                chrome = _kit.chrome,
                brass = _kit.ringLip,
                lacquer = _kit.track,
                peg = _kit.pegPhysics,
                surface = _kit.trackPhysics
            };
            Transform obstacleRoot = ObstacleBuilder.Build(root, s.layout, s.world, kit);

            var stageRig = new LavaStageRig
            {
                name = $"Stage {s.index + 1}: {s.title}",
                path = path,
                endsOnIsland = s.basinSection < 0
            };

            // Pinwheels in hierarchy order (stable), for the simulation to step.
            stageRig.spinners = obstacleRoot.GetComponentsInChildren<KinematicSpinner>(true);
            stageRig.widthByDistance = BakeWidthCurve(s);

            BuildPens(s, root, stageRig);
            BuildGates(s, root, stageRig);
            if (s.basinSection >= 0)
            {
                BuildBasin(s, root, stageRig);
            }

            stageRig.killPlaneY = s.basinSection >= 0 ? s.minY - 3f : -1000f;
            BuildSupports(s, root);
            return stageRig;
        }

        // ------------------------------------------------------------------ width curve

        /// <summary>Path distance to usable lava width (metres): keys at section ends and every ~2.5 m of a width ramp, linear tangents.</summary>
        static AnimationCurve BakeWidthCurve(StageData s)
        {
            var keys = new List<Keyframe>();
            float lastKey = float.MinValue;
            for (int i = 0; i < s.world.Count; i++)
            {
                Sample sample = s.world[i];
                bool first = i == 0;
                bool last = i == s.world.Count - 1;
                bool sectionEnd = !last && s.world[i + 1].section != sample.section;
                bool ramp = !first && !last && Mathf.Abs(sample.width - s.world[i - 1].width) > 1e-4f && sample.distance - lastKey >= 2.5f;
                if (first || last || sectionEnd || ramp)
                {
                    keys.Add(new Keyframe(sample.distance, LavaWidth(sample.width)));
                    lastKey = sample.distance;
                }
            }

            if (s.basinSection < 0)
            {
                // Across the island the path continues; widen to the island's diameter.
                Sample end = s.world[s.world.Count - 1];
                keys.Add(new Keyframe(end.distance + IslandRadius, IslandRadius * 2f));
            }

            // Drop duplicate distances (a section end shares its distance with the next section's first sample).
            var curve = new AnimationCurve();
            float previous = float.MinValue;
            foreach (Keyframe key in keys)
            {
                if (key.time > previous + 1e-4f)
                {
                    curve.AddKey(key);
                    previous = key.time;
                }
            }

            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }

            return curve;
        }

        static float LavaWidth(float trackWidth)
        {
            return trackWidth > 2f ? trackWidth - 0.4f : trackWidth * 0.95f;
        }

        // ------------------------------------------------------------------ pens

        static void BuildPens(StageData s, Transform root, LavaStageRig stageRig)
        {
            var pens = new GameObject("Pens").transform;
            pens.SetParent(root, false);

            Sample start = s.world[0];
            Quaternion rotation = Quaternion.LookRotation(start.forward, start.up);
            float rail = LavaRunLayouts.PenRail;
            float width = LavaRunLayouts.PenWidth;
            float divider = LavaRunLayouts.PenDivider;
            float penWidth = LavaRunLayouts.PenCellWidth;

            // Back wall.
            AddBox(pens, "Pen Back Wall", start.position - start.forward * 0.07f + start.up * (rail * 0.5f), rotation,
                new Vector3(width + 0.4f, rail, 0.14f), _kit.track, true);

            // Three dividers between four pens.
            Sample middle = TrackMeshBuilder.At(s.world, LavaRunLayouts.PenLength * 0.5f);
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * (penWidth + divider);
                AddBox(pens, $"Pen Divider {i + 1}", middle.position + middle.right * x + middle.up * (rail * 0.5f), rotation,
                    new Vector3(divider, rail, LavaRunLayouts.PenLength - 0.1f), _kit.track, true);
            }

            // The gate slab closing the front of all four pens. Rest = closed; opening sinks it into the floor.
            Sample gateFrame = TrackMeshBuilder.At(s.world, LavaRunLayouts.PenLength - 0.05f);
            GameObject slab = AddBox(pens, "Pen Gate", gateFrame.position + gateFrame.up * (rail * 0.5f),
                Quaternion.LookRotation(gateFrame.forward, gateFrame.up), new Vector3(width, rail, 0.2f), _kit.door, true);
            var slabBody = slab.AddComponent<Rigidbody>();
            slabBody.isKinematic = true;
            slabBody.interpolation = RigidbodyInterpolation.Interpolate;
            stageRig.penGate = new LavaMover(slab.transform, -gateFrame.up * (rail + 0.05f));

            // Spawn points: a grid per colour, left to right RED, BLUE, GREEN, YELLOW.
            const int columns = 4;
            const int rows = 6;
            stageRig.pens = new LavaTeamPen[LavaRunConfig.TeamCount];
            for (int team = 0; team < LavaRunConfig.TeamCount; team++)
            {
                float centre = (team - 1.5f) * (penWidth + divider);
                var teamRoot = new GameObject($"Pen {team + 1} Spawns ({_config.teams[team].name})").transform;
                teamRoot.SetParent(pens, false);
                var points = new Transform[columns * rows];
                int n = 0;
                for (int r = 0; r < rows; r++)
                {
                    Sample frame = TrackMeshBuilder.At(s.world, 0.35f + r * 0.4f);
                    for (int c = 0; c < columns; c++)
                    {
                        var point = new GameObject($"Spawn {r:00}-{c:00}").transform;
                        point.SetParent(teamRoot, false);
                        point.position = frame.position + frame.right * (centre + (c - (columns - 1) * 0.5f) * 0.28f) + frame.up * 0.16f;
                        points[n++] = point;
                    }
                }

                stageRig.pens[team] = new LavaTeamPen { spawnPoints = points };
            }
        }

        // ------------------------------------------------------------------ gates

        static void BuildGates(StageData s, Transform root, LavaStageRig stageRig)
        {
            var gatesRoot = new GameObject("Gates").transform;
            gatesRoot.SetParent(root, false);

            List<LavaGateSpec> specs = LavaRunLayouts.Gates(s.index);
            var gates = new List<(float distance, LavaGate gate)>();
            foreach (LavaGateSpec spec in specs)
            {
                float distance = SectionStart(s.world, spec.section) + spec.offset;
                Sample frame = TrackMeshBuilder.At(s.world, distance);
                float half = frame.width * 0.5f;
                float min = spec.FullWidth ? -half + 0.1f : spec.laneMin;
                float max = spec.FullWidth ? half - 0.1f : spec.laneMax;
                LavaGate gate = BuildGate(s, gatesRoot, frame, min, max, spec.multiplier, gates.Count);
                gates.Add((distance, gate));
            }

            // Path order, as the rig asks for. A stable sort keeps the fork pair together.
            for (int i = 1; i < gates.Count; i++)
            {
                var item = gates[i];
                int j = i - 1;
                while (j >= 0 && gates[j].distance > item.distance)
                {
                    gates[j + 1] = gates[j];
                    j--;
                }

                gates[j + 1] = item;
            }

            stageRig.gates = new LavaGate[gates.Count];
            for (int i = 0; i < gates.Count; i++)
            {
                stageRig.gates[i] = gates[i].gate;
            }
        }

        static LavaGate BuildGate(StageData s, Transform parent, Sample frame, float laneMin, float laneMax, int multiplier, int ordinal)
        {
            float laneWidth = laneMax - laneMin;
            float centre = (laneMin + laneMax) * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(frame.forward, frame.up);
            Color hue = GateColor(multiplier);
            string label = $"x{multiplier}";
            var gateRoot = new GameObject($"Gate {ordinal + 1:00} ({label})").transform;
            gateRoot.SetParent(parent, false);
            gateRoot.position = frame.position + frame.right * centre;
            gateRoot.rotation = rotation;

            // Trigger spanning the lane.
            var zoneObject = new GameObject("Trigger");
            zoneObject.transform.SetParent(gateRoot, false);
            zoneObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var box = zoneObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(laneWidth, 1.2f, 0.5f);
            TriggerZone3D zone = zoneObject.AddComponent<TriggerZone3D>();

            // Chrome arch.
            const float archHeight = 1.7f;
            for (int side = -1; side <= 1; side += 2)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = side < 0 ? "Post Left" : "Post Right";
                post.transform.SetParent(gateRoot, false);
                post.transform.localPosition = new Vector3(side * (laneWidth * 0.5f + 0.05f), archHeight * 0.5f, 0f);
                post.transform.localScale = new Vector3(0.07f, archHeight * 0.5f, 0.07f);
                post.GetComponent<MeshRenderer>().sharedMaterial = _kit.chrome;
                Object.DestroyImmediate(post.GetComponent<Collider>());
            }

            GameObject beam = AddBox(gateRoot, "Beam", Vector3.zero, Quaternion.identity, new Vector3(laneWidth + 0.2f, 0.12f, 0.14f), _kit.chrome, false);
            beam.transform.localRotation = Quaternion.identity;
            beam.transform.localPosition = new Vector3(0f, archHeight, 0f);

            // The glass curtain the flash plays on (visual only, in the team-neutral gate colour).
            Material glass = LavaRunMaterials.EnsureMaterial($"LavaRunGatePanel_{label}", "Universal Render Pipeline/Unlit", m =>
            {
                m.SetColor("_BaseColor", new Color(hue.r * 1.5f, hue.g * 1.5f, hue.b * 1.5f, 0.2f));
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            });
            GameObject panel = AddBox(gateRoot, "Flash Panel", Vector3.zero, Quaternion.identity, new Vector3(laneWidth, 1.0f, 0.02f), glass, false);
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localPosition = new Vector3(0f, archHeight - 0.56f, 0f);

            // Decals: boards on the arch for anyone coming down the track or looking back up it, and ink on the floor.
            float glyph = Mathf.Clamp(laneWidth * 0.5f, 0.35f, 0.95f);
            Vector3 boardPosition = gateRoot.position + frame.up * (archHeight + 0.2f + glyph * 0.5f);
            Decal(gateRoot, "Board (uphill)", label, boardPosition, rotation, glyph, hue, new Vector2(3f, 1.4f));
            Decal(gateRoot, "Board (downhill)", label, boardPosition, Quaternion.LookRotation(-frame.forward, frame.up), glyph, hue, new Vector2(3f, 1.4f));

            if (laneWidth >= 1.0f)
            {
                Vector3 textUp = Vector3.ProjectOnPlane(new Vector3(s.textUp.x, 0f, s.textUp.y), frame.up);
                if (textUp.sqrMagnitude < 1e-4f)
                {
                    textUp = frame.forward;
                }

                Decal(gateRoot, "Floor Ink", label, gateRoot.position + frame.up * 0.014f + frame.forward * 0.9f,
                    Quaternion.LookRotation(-frame.up, textUp.normalized), Mathf.Min(1.5f, laneWidth * 0.55f),
                    new Color(hue.r, hue.g, hue.b, 0.85f), new Vector2(3f, 1.6f));
            }

            var anchor = new GameObject("Flash Anchor").transform;
            anchor.SetParent(gateRoot, false);
            anchor.localPosition = new Vector3(0f, archHeight - 0.4f, 0f);

            return new LavaGate { zone = zone, multiplier = multiplier, flashAnchor = anchor };
        }

        static Color GateColor(int multiplier)
        {
            switch (multiplier)
            {
                case 1: return new Color(0.62f, 0.72f, 0.85f, 1f);
                case 2: return new Color(0.35f, 0.95f, 1f, 1f);
                case 3: return new Color(1f, 0.82f, 0.2f, 1f);
                default: return new Color(1f, 0.35f, 0.9f, 1f);
            }
        }

        static TextMeshPro Decal(Transform parent, string name, string text, Vector3 position, Quaternion rotation, float glyphHeight,
            Color color, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = rotation;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = size;
            tmp.text = text.Replace("x", "×");
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableAutoSizing = false;
            tmp.fontSize = glyphHeight * 10f;
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 0.9f);
            return tmp;
        }

        // ------------------------------------------------------------------ basin

        static void BuildBasin(StageData s, Transform root, LavaStageRig stageRig)
        {
            var basin = new GameObject("Basin").transform;
            basin.SetParent(root, false);

            Sample start = s.world[s.boundary];
            Sample ahead = s.world[Mathf.Min(s.world.Count - 1, s.boundary + 1)];
            Vector3 forward = new Vector3(ahead.forward.x, 0f, ahead.forward.z).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 origin = start.position;
            float width = LavaRunLayouts.BasinWidth;
            float length = LavaRunLayouts.BasinLength;
            float rail = LavaRunLayouts.BasinRail;

            // Fixed sill the door rises out of, side walls and the end wall.
            AddBox(basin, "Sill", origin + forward * 0.4f - Vector3.up * 0.06f, rotation, new Vector3(width + 0.24f, 0.12f, 0.8f), _kit.track, true);
            for (int side = -1; side <= 1; side += 2)
            {
                AddBox(basin, side < 0 ? "Wall Left" : "Wall Right",
                    origin + forward * (length * 0.5f) + right * (side * (width * 0.5f + 0.06f)) + Vector3.up * (rail * 0.5f), rotation,
                    new Vector3(0.12f, rail, length), _kit.track, true);
            }

            AddBox(basin, "End Wall", origin + forward * (length + 0.07f) + Vector3.up * (rail * 0.5f), rotation,
                new Vector3(width + 0.24f, rail, 0.14f), _kit.track, true);

            // Door: rest = open (sunk below the floor), moves up to shut.
            const float doorHeight = 1.3f;
            GameObject door = AddBox(basin, "Door", origin + forward * 0.4f - Vector3.up * (0.02f + doorHeight * 0.5f), rotation,
                new Vector3(width, doorHeight, 0.3f), _kit.door, true);
            var doorBody = door.AddComponent<Rigidbody>();
            doorBody.isKinematic = true;
            doorBody.interpolation = RigidbodyInterpolation.Interpolate;
            stageRig.door = new LavaMover(door.transform, Vector3.up * (doorHeight + 0.01f));

            // Trapdoor: a slab hinged along the left wall that swings down. Rest = shut.
            float slabLength = length - 0.8f;
            var hinge = new GameObject("Trapdoor Hinge").transform;
            hinge.SetParent(basin, false);
            hinge.position = origin + forward * (0.8f + slabLength * 0.5f) - right * (width * 0.5f) - Vector3.up * 0.06f;
            hinge.rotation = rotation;
            var hingeBody = hinge.gameObject.AddComponent<Rigidbody>();
            hingeBody.isKinematic = true;
            hingeBody.interpolation = RigidbodyInterpolation.Interpolate;
            GameObject slab = AddBox(hinge, "Trapdoor", Vector3.zero, Quaternion.identity, new Vector3(width, 0.12f, slabLength), _kit.track, true);
            slab.transform.localRotation = Quaternion.identity;
            slab.transform.localPosition = new Vector3(width * 0.5f, 0f, 0f);
            slab.GetComponent<Collider>().sharedMaterial = _kit.trackPhysics;
            stageRig.trapdoor = new LavaMover { target = hinge, moveWorld = Vector3.zero, rotateEuler = new Vector3(0f, 0f, -90f) };

            // Safe-area trigger: the floor behind the door.
            float zoneLength = length - 1.0f;
            var zoneObject = new GameObject("Basin Zone");
            zoneObject.transform.SetParent(basin, false);
            zoneObject.transform.position = origin + forward * (0.9f + zoneLength * 0.5f) + Vector3.up * 0.9f;
            zoneObject.transform.rotation = rotation;
            var box = zoneObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(width - 0.1f, 1.8f, zoneLength);
            stageRig.basinZone = zoneObject.AddComponent<TriggerZone3D>();
            stageRig.basinEntryDistance = s.basinStartDistance + 0.4f;
        }

        // ------------------------------------------------------------------ supports

        static void BuildSupports(StageData s, Transform root)
        {
            var supports = new GameObject("Supports").transform;
            supports.SetParent(root, false);

            float spacing = 14f;
            float next = spacing;
            int placed = 0;
            for (int i = 0; i <= s.bodyEnd; i++)
            {
                Sample sample = s.world[i];
                if (sample.distance < next)
                {
                    continue;
                }

                bool bridge = s.basinSection < 0 && sample.section >= LavaRunLayouts.NeckSection;
                next += bridge ? 24f : spacing;

                float underside = sample.position.y - TrackMeshBuilder.FloorThickness;
                float ground = _terrain != null ? _terrain.Height(sample.position.x, sample.position.z) : 0f;
                if (ground < 0f)
                {
                    ground = -1.2f; // standing in the lava
                }

                float height = underside - ground;
                if (height < 0.8f || ObstructedBelow(s, sample))
                {
                    continue;
                }

                var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                column.name = "Column";
                column.transform.SetParent(supports, false);
                column.transform.position = new Vector3(sample.position.x, ground + height * 0.5f, sample.position.z);
                column.transform.localScale = new Vector3(bridge ? 0.5f : 0.3f, height * 0.5f, bridge ? 0.5f : 0.3f);
                column.GetComponent<MeshRenderer>().sharedMaterial = _kit.chrome;
                Object.DestroyImmediate(column.GetComponent<Collider>());
                placed++;
            }

            if (placed == 0)
            {
                Object.DestroyImmediate(supports.gameObject);
            }
        }

        /// <summary>True when any other stretch of track sits under this point, so a column would pierce it.</summary>
        static bool ObstructedBelow(StageData own, Sample sample)
        {
            foreach (StageData other in _stages)
            {
                for (int j = 0; j < other.world.Count; j += 3)
                {
                    Sample o = other.world[j];
                    if (other == own && Mathf.Abs(o.distance - sample.distance) < 6f)
                    {
                        continue;
                    }

                    Vector3 delta = o.position - sample.position;
                    if (delta.y < -0.3f && new Vector2(delta.x, delta.z).magnitude < sample.width * 0.5f + o.width * 0.5f + 0.5f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ island

        static void BuildIsland(Transform rigRoot, LavaRunRig rig, LavaStageRig bridgeStage)
        {
            var island = new GameObject("Island").transform;
            island.SetParent(rigRoot, false);
            island.position = _islandCentre;

            var centre = new GameObject("Island Centre").transform;
            centre.SetParent(island, false);

            // Rings, outermost first. Each is its own kinematic body so it can sink.
            int ringCount = RingEdges.Length - 1;
            var rings = new Transform[ringCount];
            for (int i = 0; i < ringCount; i++)
            {
                Mesh mesh = LavaRunGeometry.SaveMeshFresh(
                    LavaRunGeometry.BuildRing($"LavaRun_IslandRing{i + 1}", RingEdges[i + 1], RingEdges[i], RingThickness, LipHeight, LipWidth, 72),
                    $"LavaRun_IslandRing{i + 1}");
                rings[i] = BuildRingObject(island, $"Island Ring {i + 1}{(i == 0 ? " (outer)" : string.Empty)}", mesh, true);
            }

            Mesh disc = LavaRunGeometry.SaveMeshFresh(
                LavaRunGeometry.BuildRing("LavaRun_IslandDisc", 0f, DiscRadius, RingThickness, LipHeight, LipWidth, 48), "LavaRun_IslandDisc");
            BuildRingObject(island, "Island Disc (never sinks)", disc, false);

            // The sweeper: a hub and two chrome arms, driven by the simulation through its KinematicSpinner.
            var sweeperObject = new GameObject("Island Sweeper");
            sweeperObject.transform.SetParent(island, false);
            var sweeperBody = sweeperObject.AddComponent<Rigidbody>();
            sweeperBody.isKinematic = true;
            sweeperBody.interpolation = RigidbodyInterpolation.Interpolate;
            KinematicSpinner spinner = sweeperObject.AddComponent<KinematicSpinner>();
            spinner.localAxis = Vector3.up;
            spinner.degreesPerSecond = 20f;

            float armLength = RingEdges[0] - 0.6f;
            for (int arm = 0; arm < 2; arm++)
            {
                GameObject bar = AddBox(sweeperObject.transform, $"Arm {arm + 1}", Vector3.zero, Quaternion.identity,
                    new Vector3(0.3f, 0.72f, armLength), _kit.chrome, true);
                float yaw = arm * 180f;
                bar.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                bar.transform.localPosition = bar.transform.localRotation * new Vector3(0f, 0.42f, 0.5f + armLength * 0.5f);
                bar.GetComponent<Collider>().sharedMaterial = _kit.ringPhysics;
            }

            var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Hub";
            hub.transform.SetParent(sweeperObject.transform, false);
            hub.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            hub.transform.localScale = new Vector3(1.0f, 0.55f, 1.0f);
            hub.GetComponent<MeshRenderer>().sharedMaterial = _kit.chrome;
            hub.GetComponent<Collider>().sharedMaterial = _kit.ringPhysics;

            // The island landing zone is the bridge stage's "basin": a box over the whole top.
            var zoneObject = new GameObject("Island Landing Zone");
            zoneObject.transform.SetParent(island, false);
            zoneObject.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var box = zoneObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(IslandRadius * 2f + 1f, 3f, IslandRadius * 2f + 1f);
            bridgeStage.basinZone = zoneObject.AddComponent<TriggerZone3D>();

            // The lava stops at the bridge's end.
            StageData bridge = _stages[LavaRunConfig.StageCount - 1];
            bridgeStage.basinEntryDistance = SectionStart(bridge.world, LavaRunLayouts.LandingSection);

            rig.island = new LavaIslandRig { rings = rings, sweeper = spinner, centre = centre };

            // A warm glow over the island for the finale.
            var glow = new GameObject("Island Glow").AddComponent<Light>();
            glow.transform.SetParent(island, false);
            glow.transform.localPosition = new Vector3(0f, 7f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.5f, 0.18f);
            glow.range = 55f;
            glow.intensity = 6f;
            glow.shadows = LightShadows.None;
        }

        static Transform BuildRingObject(Transform parent, string name, Mesh mesh, bool kinematicBody)
        {
            var ring = new GameObject(name);
            ring.transform.SetParent(parent, false);
            ring.AddComponent<MeshFilter>().sharedMesh = mesh;
            ring.AddComponent<MeshRenderer>().sharedMaterials = new[] { _kit.ring, _kit.ringLip };
            var collider = ring.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.sharedMaterial = _kit.ringPhysics;
            if (kinematicBody)
            {
                var body = ring.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }

            return ring.transform;
        }

        // ------------------------------------------------------------------ marbles

        static void BuildMarbles(Transform rigRoot, LavaRunRig rig, out GameObject[] ghosts)
        {
            var templates = new GameObject("Marble Templates").transform;
            templates.SetParent(rigRoot, false);
            var pool = new GameObject("Marbles").transform;
            pool.SetParent(rigRoot, false);

            rig.marbleTemplates = new GameObject[LavaRunConfig.TeamCount];
            ghosts = new GameObject[LavaRunConfig.TeamCount];
            float diameter = LavaRunLayouts.MarbleRadius * 2f;
            for (int team = 0; team < LavaRunConfig.TeamCount; team++)
            {
                string teamName = _config.teams[team].name;

                var marble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marble.name = $"Marble {teamName}";
                marble.transform.SetParent(templates, false);
                marble.transform.localScale = Vector3.one * diameter;
                marble.GetComponent<MeshRenderer>().sharedMaterial = _kit.marbles[team];
                marble.GetComponent<SphereCollider>().sharedMaterial = _kit.marblePhysics;
                var body = marble.AddComponent<Rigidbody>();
                body.mass = 1f;
                body.linearDamping = MarbleLinearDamping;
                body.angularDamping = 0.05f;
                body.sleepThreshold = 0.0001f; // a briefly slow marble on a gentle slope must not be frozen by PhysX sleep
                body.maxAngularVelocity = 100f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                marble.SetActive(false);
                rig.marbleTemplates[team] = marble;

                var ghost = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ghost.name = $"Ghost {teamName}";
                ghost.transform.SetParent(templates, false);
                ghost.transform.localScale = Vector3.one * diameter;
                ghost.GetComponent<MeshRenderer>().sharedMaterial = _kit.marbles[team];
                Object.DestroyImmediate(ghost.GetComponent<Collider>());
                ghost.SetActive(false);
                ghosts[team] = ghost;
            }

            rig.poolParent = pool;
        }

        // ------------------------------------------------------------------ small helpers

        static GameObject AddBox(Transform parent, string name, Vector3 worldPosition, Quaternion rotation, Vector3 scale,
            Material material, bool collider)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = worldPosition;
            box.transform.rotation = rotation;
            box.transform.localScale = scale;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (collider)
            {
                box.GetComponent<Collider>().sharedMaterial = _kit.trackPhysics;
            }
            else
            {
                Object.DestroyImmediate(box.GetComponent<Collider>());
            }

            return box;
        }
    }
}
