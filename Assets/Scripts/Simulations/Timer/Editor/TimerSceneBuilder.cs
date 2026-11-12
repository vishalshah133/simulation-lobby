using System.Collections.Generic;
using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using SimulationLobby.Shared.EditorTools;
using SimulationLobby.Shared.Tracks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SimulationLobby.Simulations.Timer.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Scenes/Timer_MarbleRun.unity</c>: a cartoon marble run (toon shading, outlines, sky and grass) with a hopper, a
    /// pool of 400 marbles and a glass collector. 16:9, for long form. Menu:
    /// <c>Simulation Lobby ▸ 3D Formats ▸ Marble Run Timer (timer) ▸ Marble Run Timer (timer-001)</c>.
    /// </summary>
    /// <remarks>
    /// Re-running replaces the scene and the generated meshes, never the config or layout, so tuning survives (the layout is rebuilt only
    /// when <see cref="TimerCourseFactory.CourseVersion"/> moves). The HUD, camera director, audio and finale effects come from
    /// <see cref="TimerHudBuilder"/>, <see cref="TimerCameraDirector"/>, <see cref="TimerAudio"/> and <see cref="TimerFinaleFx"/>.
    /// </remarks>
    static class TimerSceneBuilder
    {
        const string MenuRoot = "Simulation Lobby/3D Formats/Marble Run Timer (timer)/";
        const string ScenePath = "Assets/Scenes/Timer_MarbleRun.unity";
        const string ConfigPath = TimerCourseFactory.Folder + "/TimerConfig_001.asset";
        const string MeshFolder = TimerCourseFactory.Folder + "/Meshes";
        const string PostPath = TimerCourseFactory.Folder + "/TimerPost.asset";
        const int ConfigDefaultsVersion = 6;

        // Collector: 1.5 m radius, packed marbles (about 0.64 solid) of radius 0.2 reach about 3.0 m of the 3.2 m above its floor at 400.
        const float CollectorRadius = 1.5f;
        const float CollectorHeight = 3.5f;
        const float CollectorFloor = 0.3f;
        const int CollectorWalls = 24;

        /// <summary>Metres the track's last floor point sits above the collector rim, and the horizontal distance to the collector axis.</summary>
        const float RimGap = 0.8f;
        const float CollectorOffset = 3.0f;

        // Hopper (local to the pen frame, metres above the pen floor).
        const float SpoutHalf = 0.55f;
        const float SpoutBottom = 0.95f;
        const float SpoutTop = 1.3f;
        const float HopperTopHalf = 1.7f;
        const float HopperTop = 3.4f;
        const float HopperAt = 2.8f;

        [MenuItem(MenuRoot + "Marble Run Timer (timer-001)", false, 351)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[timer] Exit play mode before building the scene.");
                return;
            }

            Directory.CreateDirectory(TimerCourseFactory.Folder);
            TimerConfig config = EnsureConfig();
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[timer] {config.name} is not valid: {error}", config);
                return;
            }

            TrackLayout layout = TimerCourseFactory.EnsureLayout();
            TimerPalette palette = TimerMaterials.Palette(config.theme);
            TimerLook look = TimerMaterials.Build(palette, config);

            // The course sits with its last floor point RimGap above the collector rim, so the run-out drops into the open top.
            List<TrackMeshBuilder.Sample> samples = TrackMeshBuilder.SampleCentreline(layout, Vector3.zero);
            float lowest = float.MaxValue;
            foreach (TrackMeshBuilder.Sample sample in samples)
            {
                lowest = Mathf.Min(lowest, sample.position.y);
            }

            float lift = CollectorHeight + RimGap - lowest;
            samples = TrackMeshBuilder.SampleCentreline(layout, new Vector3(0f, lift, 0f));
            float totalLength = samples[samples.Count - 1].distance;
            float finishDistance = Mathf.Max(1f, totalLength - layout.runOutLength);
            Bounds bounds = BoundsOf(samples);
            TrackMeshBuilder.Sample gateSample = TrackMeshBuilder.At(samples, layout.gateDistance);
            TrackMeshBuilder.Sample finishSample = TrackMeshBuilder.At(samples, finishDistance);
            TrackMeshBuilder.Sample endSample = samples[samples.Count - 1];
            Vector3 collectorCentre = CollectorCentre(endSample);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = BuildCamera(samples, gateSample, palette);
            BuildWorld(bounds, samples, collectorCentre, palette, look);

            PhysicsMaterial trackPhysics = TimerMaterials.PhysicsMat("TimerTrackSlick", 0.18f, 0.2f, 0.1f);
            PhysicsMaterial pegPhysics = TimerMaterials.PhysicsMat("TimerPeg", 0.2f, 0.25f, 0.4f);
            PhysicsMaterial marblePhysics = TimerMaterials.PhysicsMat("TimerMarbleToy", 0.4f, 0.45f, 0.25f);
            PhysicsMaterial glassPhysics = TimerMaterials.PhysicsMat("TimerGlass", 0.15f, 0.15f, 0.2f);

            // --- Course. ---
            var root = new GameObject("Simulation");
            var course = new GameObject("Course").transform;
            course.SetParent(root.transform, false);

            Mesh bodyMesh = TrackMeshAssets.SaveMeshFresh(TrackMeshBuilder.BuildBody(samples), "TimerTrackBody", MeshFolder);
            Mesh railMesh = TrackMeshAssets.SaveMeshFresh(TimerRailBuilder.BuildRailMesh(samples), "TimerRails", MeshFolder);

            var track = new GameObject("Track");
            track.transform.SetParent(course, false);
            track.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            track.AddComponent<MeshRenderer>().sharedMaterials = new[] { look.track, look.cap };
            var trackCollider = track.AddComponent<MeshCollider>();
            trackCollider.sharedMesh = bodyMesh;
            trackCollider.sharedMaterial = trackPhysics;
            AddOutlineCopy(track.transform, "Track Outline", bodyMesh, look.outlineCourse, 2);

            var centreline = new Vector3[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                centreline[i] = samples[i].position;
            }

            TrackPath path = track.AddComponent<TrackPath>();
            path.Set(centreline);

            var rails = new GameObject("Rails");
            rails.transform.SetParent(course, false);
            rails.AddComponent<MeshFilter>().sharedMesh = railMesh;
            rails.AddComponent<MeshRenderer>().sharedMaterials = new[] { look.rail, look.cap };
            AddOutlineCopy(rails.transform, "Rails Outline", railMesh, look.outlineThin, 2);

            Transform finishLine = NewFrame("Finish Line", course, finishSample);
            BuildStartBackWall(course, samples[0], look);
            BuildHopper(course, samples, look, out Transform hopperRelease, out Transform hopperGate);
            BuildFinish(course, finishSample, look);
            BuildSupports(course, samples, collectorCentre, look);

            BuildFunnel(course, samples, TimerCourseFactory.FunnelLaneA, look, trackPhysics);
            BuildFunnel(course, samples, TimerCourseFactory.FunnelLaneB, look, trackPhysics);

            Transform obstacles = ObstacleBuilder.Build(course, layout, samples, new ObstacleBuilder.Kit
            {
                chrome = look.peg,
                brass = look.pegCap,
                lacquer = look.funnel,
                peg = pegPhysics,
                surface = trackPhysics
            });
            foreach (MeshRenderer renderer in obstacles.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name != "Wall Cap")
                {
                    TimerMaterials.AddOutline(renderer, look.outlineThin);
                }
            }

            // Wall deflectors between the peg rows, then prove no gap in any peg field can hold a marble.
            var deflectors = new List<Deflector>();
            BuildDeflectors(course, samples, look, trackPhysics, deflectors);
            if (!CheckPegFieldGaps(obstacles, samples, deflectors, out float minPegGap, out string minPegWhere))
            {
                Debug.LogError($"[timer] Build refused: a peg-field gap of {minPegGap:0.000} m ({minPegWhere}) is under the " +
                               $"{TimerCourseFactory.MinFieldGap:0.00} m limit, so a marble could wedge there. Scene not saved.");
                return;
            }

            Debug.Log($"[timer] Peg fields: minimum gap {minPegGap:0.000} m ({minPegWhere}); limit {TimerCourseFactory.MinFieldGap:0.00} m (marble 0.4 m). " +
                      $"{deflectors.Count} wall deflectors.");

            Transform mouth = BuildCollector(course, collectorCentre, look, glassPhysics);

            // --- Marbles: a pool, inactive until the simulation releases them. ---
            var marbles = new GameObject("Marbles").transform;
            marbles.SetParent(root.transform, false);
            var pool = new Rigidbody[config.marbleCount];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = BuildMarble(marbles, i, config, look, marblePhysics, hopperRelease.position);
            }

            // --- Simulation + runner: a random seed per Play, logged by the runner; paste it back to re-render. ---
            var sim = root.AddComponent<TimerSimulation>();
            var runner = root.GetComponent<SimulationRunner>();
            if (runner == null)
            {
                runner = root.AddComponent<SimulationRunner>();
            }

            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;
            sim.Wire(config, layout, path, pool, hopperRelease, finishLine, hopperGate);
            EditorUtility.SetDirty(sim);

            // --- Camera director, audio, finale FX, HUD. ---
            var director = camera.gameObject.AddComponent<TimerCameraDirector>();
            SetReference(director, "sim", sim);
            SetReference(director, "cam", camera);

            var audioObject = new GameObject("Audio");
            SetReference(audioObject.AddComponent<TimerAudio>(), "sim", sim);

            var finaleObject = new GameObject("Finale FX");
            finaleObject.transform.SetParent(mouth, false);
            var finale = finaleObject.AddComponent<TimerFinaleFx>();
            SetReference(finale, "sim", sim);
            SetReference(finale, "anchor", mouth);

            TimerHudBuilder.Build(sim);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Report(samples, layout, config, lift);
        }

        [MenuItem(MenuRoot + "Reset Config (storyboard defaults)", false, 371)]
        static void ResetConfig()
        {
            Directory.CreateDirectory(TimerCourseFactory.Folder);
            TimerConfig defaults = CreateDefaults();
            var existing = AssetDatabase.LoadAssetAtPath<TimerConfig>(ConfigPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(defaults, ConfigPath);
                existing = defaults;
            }
            else
            {
                // Copied in place so the scene's reference to the asset keeps its GUID.
                EditorUtility.CopySerialized(defaults, existing);
                Object.DestroyImmediate(defaults);
                EditorUtility.SetDirty(existing);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[timer] Reset {ConfigPath} to the storyboard defaults ({existing.durationSeconds:0}s clock, " +
                      $"{existing.marbleCount} marbles, {existing.theme} theme).", existing);
        }

        [MenuItem(MenuRoot + "Reset Course (rebuild layout)", false, 373)]
        static void ResetCourse()
        {
            TrackLayout layout = TimerCourseFactory.EnsureLayout(true);
            Debug.Log($"[timer] Reset {TimerCourseFactory.LayoutPath}. Rebuild the scene to bake the meshes.", layout);
        }

        // ------------------------------------------------------------------ assets

        static TimerConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<TimerConfig>(ConfigPath);
            if (config == null)
            {
                config = CreateDefaults();
                AssetDatabase.CreateAsset(config, ConfigPath);
                return config;
            }

            if (config.defaultsVersion != ConfigDefaultsVersion)
            {
                // The premise changed (race the clock became a marble stream), so stale tuning would leave the fields meaningless.
                TimerConfig defaults = CreateDefaults();
                EditorUtility.CopySerialized(defaults, config);
                Object.DestroyImmediate(defaults);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"[timer] {ConfigPath} was behind the builder (defaults v{ConfigDefaultsVersion}); rewrote it to the storyboard defaults.", config);
            }

            return config;
        }

        static TimerConfig CreateDefaults()
        {
            var config = ScriptableObject.CreateInstance<TimerConfig>();
            config.fixedTimestep = 1f / 60f;
            config.defaultsVersion = ConfigDefaultsVersion;
            config.maxDurationSeconds = config.setupHoldSeconds + config.durationSeconds + config.finishTailSeconds + 10f;
            config.intent = "timer-001: a countdown that ends at 0:00 while a steady stream of marbles runs down a cartoon marble run " +
                            "into a glass collector that is nearly full at the end. Duration is the clock; the stream is paced to it.";
            return config;
        }

        static void SetReference(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty found = serialized.FindProperty(property);
            if (found == null)
            {
                Debug.LogWarning($"[timer] {target.GetType().Name} has no serialized field '{property}'; wire it by hand.", target);
                return;
            }

            found.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ camera and world

        static Camera BuildCamera(List<TrackMeshBuilder.Sample> samples, TrackMeshBuilder.Sample gateSample, TimerPalette palette)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = palette.skyHorizon;
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            Vector3 flat = Flat(gateSample.forward);
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            Vector3 target = TrackMeshBuilder.At(samples, gateSample.distance + 8f).position;
            cameraObject.transform.position = gateSample.position - flat * 12f + right * 7f + Vector3.up * 7f;
            cameraObject.transform.LookAt(target);
            return camera;
        }

        /// <summary>Lights, ambient, fog, post, a sky sphere, a grass floor and a little scenery.</summary>
        static void BuildWorld(Bounds bounds, List<TrackMeshBuilder.Sample> samples, Vector3 collectorCentre, TimerPalette palette, TimerLook look)
        {
            // Bright, even light: a warm key that casts soft shadows and a cool fill, over a bright trilight ambient.
            var lights = new GameObject("Timer Lights").transform;
            Light key = NewDirectional(lights, "Key", palette.key, 1.15f, Quaternion.Euler(52f, 148f, 0f));
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.75f;
            key.shadowResolution = LightShadowResolution.VeryHigh;
            NewDirectional(lights, "Fill", palette.fill, 0.35f, Quaternion.Euler(35f, 330f, 0f));

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = palette.ambientSky;
            RenderSettings.ambientEquatorColor = palette.ambientEquator;
            RenderSettings.ambientGroundColor = palette.ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = palette.skyHorizon;
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 520f;

            BuildPost();

            var world = new GameObject("World").transform;

            // Sky: an inverted sphere with a vertical gradient. The floor and the fog both end in its horizon colour, so the seam disappears.
            var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sky.name = "Sky";
            sky.transform.SetParent(world, false);
            sky.transform.position = bounds.center;
            sky.transform.localScale = Vector3.one * 1600f;
            Object.DestroyImmediate(sky.GetComponent<Collider>());
            var skyRenderer = sky.GetComponent<MeshRenderer>();
            skyRenderer.sharedMaterial = look.sky;
            skyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            skyRenderer.receiveShadows = false;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Grass Floor";
            floor.transform.SetParent(world, false);
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(1400f, 0.1f, 1400f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.AddComponent<BoxCollider>();
            floor.GetComponent<MeshRenderer>().sharedMaterial = look.floor;

            BuildScenery(world, samples, collectorCentre, bounds, look);
        }

        static Light NewDirectional(Transform parent, string name, Color color, float intensity, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = rotation;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>A global volume: neutral tonemapping, a mild bloom, a touch of saturation and vignette. Profile created once, values re-applied.</summary>
        static void BuildPost()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostPath);

                profile.Add<Tonemapping>(true);
                profile.Add<Bloom>(true);
                profile.Add<ColorAdjustments>(true);
                profile.Add<Vignette>(true);
                foreach (VolumeComponent component in profile.components)
                {
                    component.name = component.GetType().Name;
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
            }

            if (profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping.mode.Override(TonemappingMode.Neutral);
            }

            if (profile.TryGet(out Bloom bloom))
            {
                bloom.threshold.Override(1.2f);
                bloom.intensity.Override(0.25f);
                bloom.scatter.Override(0.6f);
            }

            if (profile.TryGet(out ColorAdjustments colour))
            {
                colour.saturation.Override(10f);
                colour.contrast.Override(8f);
            }

            if (profile.TryGet(out Vignette vignette))
            {
                vignette.intensity.Override(0.15f);
                vignette.smoothness.Override(0.5f);
            }

            EditorUtility.SetDirty(profile);

            var volumeObject = new GameObject("Timer Post");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        /// <summary>Round trees on the ground clear of the course and the collector, and cloud puffs high up. Seeded, so rebuilds look the same.</summary>
        static void BuildScenery(Transform parent, List<TrackMeshBuilder.Sample> samples, Vector3 collectorCentre, Bounds bounds, TimerLook look)
        {
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(parent, false);
            var random = new System.Random(7);

            int placed = 0;
            for (int attempt = 0; attempt < 400 && placed < 46; attempt++)
            {
                float angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
                float reach = 20f + (float)random.NextDouble() * 95f;
                var position = new Vector3(bounds.center.x + Mathf.Cos(angle) * (bounds.extents.x + reach * 0.6f),
                    0f, bounds.center.z + Mathf.Sin(angle) * (bounds.extents.z + reach * 0.6f));
                if (!ClearOfCourse(samples, position, 9f) || Vector2.Distance(new Vector2(position.x, position.z), new Vector2(collectorCentre.x, collectorCentre.z)) < 10f)
                {
                    continue;
                }

                placed++;
                float size = 0.8f + (float)random.NextDouble() * 1.1f;
                var tree = new GameObject("Tree").transform;
                tree.SetParent(scenery, false);
                tree.position = position;
                Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 1.2f * size, 0f), Quaternion.identity, new Vector3(0.7f * size, 1.2f * size, 0.7f * size), look.trunk, look.outlineThin);
                Prim(PrimitiveType.Sphere, "Crown", tree, new Vector3(0f, 3.6f * size, 0f), Quaternion.identity, Vector3.one * (4.2f * size), look.foliage, look.outlineCourse);
            }

            for (int i = 0; i < 9; i++)
            {
                float angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
                float distance = 120f + (float)random.NextDouble() * 220f;
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(scenery, false);
                cloud.position = new Vector3(bounds.center.x + Mathf.Cos(angle) * distance, 90f + (float)random.NextDouble() * 70f, bounds.center.z + Mathf.Sin(angle) * distance);
                for (int puff = 0; puff < 4; puff++)
                {
                    float s = 18f + (float)random.NextDouble() * 14f;
                    GameObject ball = Prim(PrimitiveType.Sphere, "Puff", cloud, new Vector3(puff * 14f - 21f, (float)random.NextDouble() * 5f, (float)random.NextDouble() * 8f),
                        Quaternion.identity, new Vector3(s, s * 0.6f, s * 0.8f), look.cloud);
                    ball.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        static bool ClearOfCourse(List<TrackMeshBuilder.Sample> samples, Vector3 position, float clearance)
        {
            for (int i = 0; i < samples.Count; i += 4)
            {
                float dx = samples[i].position.x - position.x;
                float dz = samples[i].position.z - position.z;
                if (dx * dx + dz * dz < clearance * clearance)
                {
                    return false;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------ course pieces

        static Transform NewFrame(string name, Transform parent, TrackMeshBuilder.Sample sample)
        {
            var frame = new GameObject(name).transform;
            frame.SetParent(parent, false);
            frame.position = sample.position;
            frame.rotation = Quaternion.LookRotation(sample.forward, sample.up);
            return frame;
        }

        /// <summary>A primitive with its collider removed (unless asked to keep it), the given materials and an optional outline.</summary>
        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 scale,
            Material material, Material outline = null, bool collider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            TimerMaterials.AddOutline(renderer, outline);
            if (!collider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }

            return go;
        }

        static GameObject WorldPrim(PrimitiveType type, string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale,
            Material material, Material outline = null, bool collider = false)
        {
            GameObject go = Prim(type, name, parent, Vector3.zero, Quaternion.identity, scale, material, outline, collider);
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        /// <summary>A child that draws the same mesh with the outline material only (every submesh), so the outline follows the mesh exactly.</summary>
        static void AddOutlineCopy(Transform parent, string name, Mesh mesh, Material outline, int submeshes)
        {
            if (outline == null)
            {
                return;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var materials = new Material[submeshes];
            for (int i = 0; i < submeshes; i++)
            {
                materials[i] = outline;
            }

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>A solid back wall behind the pen, so marbles dropped from the hopper cannot bounce out of the back of the track.</summary>
        static void BuildStartBackWall(Transform parent, TrackMeshBuilder.Sample first, TimerLook look)
        {
            Quaternion rotation = Quaternion.LookRotation(first.forward, first.up);
            WorldPrim(PrimitiveType.Cube, "Start Back Wall", parent, first.position - first.forward * 0.06f + first.up * 0.3f, rotation,
                new Vector3(first.width + 0.4f, 0.7f, 0.12f), look.rail, look.outlineThin, collider: true);
        }

        /// <summary>
        /// Hopper above the pen: a platform under the track, four legs, an inverted-pyramid body with a spout, a rim and a gate plate across the
        /// spout. Visual only (no colliders). The release transform sits inside the spout above the plate, forward pointing down-track.
        /// </summary>
        static void BuildHopper(Transform parent, List<TrackMeshBuilder.Sample> samples, TimerLook look,
            out Transform release, out Transform gate)
        {
            var root = new GameObject("Hopper").transform;
            root.SetParent(parent, false);

            TrackMeshBuilder.Sample at = TrackMeshBuilder.AtExact(samples, HopperAt);
            Quaternion tilted = Quaternion.LookRotation(at.forward, at.up);
            Vector3 flat = Flat(at.forward);
            Quaternion upright = Quaternion.LookRotation(flat, Vector3.up);

            // Platform: a slab under the whole pen, tilted with the track.
            WorldPrim(PrimitiveType.Cube, "Hopper Platform", root, at.position - at.up * (TrackMeshBuilder.FloorThickness + 0.17f), tilted,
                new Vector3(4.4f, 0.34f, 8f), look.platform, look.outlineCourse);

            // Legs at the four corners of the rim, from the platform up to the rim.
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 corner = at.position + upright * new Vector3(sx * (HopperTopHalf + 0.1f), 0f, sz * (HopperTopHalf + 0.1f));
                    float bottom = at.position.y - 0.3f;
                    float height = at.position.y + HopperTop - bottom;
                    WorldPrim(PrimitiveType.Cylinder, "Hopper Leg", root, new Vector3(corner.x, bottom + height * 0.5f, corner.z), Quaternion.identity,
                        new Vector3(0.24f, height * 0.5f, 0.24f), look.support, look.outlineThin);
                }
            }

            // Body: spout and frustum as one mesh, rim as four slim bars, flat-shaded.
            Mesh hopperMesh = TrackMeshAssets.SaveMeshFresh(TimerRailBuilder.BuildHopperMesh(SpoutHalf, HopperTopHalf, SpoutBottom, SpoutTop, HopperTop),
                "TimerHopper", MeshFolder);
            var body = new GameObject("Hopper Body");
            body.transform.SetParent(root, false);
            body.transform.SetPositionAndRotation(at.position, upright);
            body.AddComponent<MeshFilter>().sharedMesh = hopperMesh;
            var bodyRenderer = body.AddComponent<MeshRenderer>();
            bodyRenderer.sharedMaterial = look.hopper;

            for (int side = 0; side < 4; side++)
            {
                Quaternion turn = Quaternion.Euler(0f, side * 90f, 0f);
                Vector3 direction = turn * Vector3.forward;
                WorldPrim(PrimitiveType.Cube, "Hopper Rim", root, at.position + upright * direction * HopperTopHalf + Vector3.up * HopperTop, upright * turn,
                    new Vector3(HopperTopHalf * 2f + 0.3f, 0.2f, 0.22f), look.hopperRim, look.outlineThin);
            }

            Vector3 spoutBase = at.position + Vector3.up * SpoutBottom;
            GameObject plate = WorldPrim(PrimitiveType.Cube, "Hopper Gate", root, spoutBase, upright, new Vector3(SpoutHalf * 2f + 0.5f, 0.07f, SpoutHalf * 2f + 0.5f),
                look.hopperRim, look.outlineThin);
            gate = plate.transform;

            var releaseObject = new GameObject("Hopper Release");
            releaseObject.transform.SetParent(root, false);
            releaseObject.transform.SetPositionAndRotation(spoutBase + Vector3.up * 0.25f, Quaternion.LookRotation(at.forward, Vector3.up));
            release = releaseObject.transform;
        }

        /// <summary>
        /// Two chevron guide walls at the start of a funnel lane: rail height, box colliders, a round toon tube along the top edge, closing
        /// from the flared width to <see cref="TimerCourseFactory.FunnelMouth"/> centred on the track.
        /// </summary>
        static void BuildFunnel(Transform parent, List<TrackMeshBuilder.Sample> samples, int section, TimerLook look, PhysicsMaterial physics)
        {
            float start = -1f;
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].section == section)
                {
                    start = samples[i - 1].distance;
                    break;
                }
            }

            if (start < 0f)
            {
                Debug.LogWarning($"[timer] Funnel section {section} not found; no funnel built.");
                return;
            }

            var root = new GameObject($"Funnel (section {section})").transform;
            root.SetParent(parent, false);
            TrackMeshBuilder.Sample from = TrackMeshBuilder.AtExact(samples, start);
            TrackMeshBuilder.Sample to = TrackMeshBuilder.AtExact(samples, start + TimerCourseFactory.FunnelLength);
            const float height = TimerCourseFactory.RailHeight;
            const float thickness = 0.12f;
            float tube = TimerRailBuilder.CapRadius * 2f;

            for (int side = -1; side <= 1; side += 2)
            {
                // The upstream end is pushed into the rail so nothing slips behind the wall.
                Vector3 a = from.position + from.right * (side * (from.width * 0.5f + 0.03f));
                Vector3 b = to.position + to.right * (side * TimerCourseFactory.FunnelMouth * 0.5f);
                Vector3 along = b - a;
                float length = along.magnitude;
                Quaternion rotation = Quaternion.LookRotation(along / length, from.up);
                Vector3 middle = (a + b) * 0.5f;

                GameObject wall = WorldPrim(PrimitiveType.Cube, "Funnel Wall", root, middle + from.up * (height * 0.5f), rotation,
                    new Vector3(thickness, height, length), look.funnel, look.outlineThin, collider: true);
                wall.GetComponent<Collider>().sharedMaterial = physics;

                Quaternion axis = Quaternion.FromToRotation(Vector3.up, along / length);
                WorldPrim(PrimitiveType.Cylinder, "Funnel Rail", root, middle + from.up * height, axis, new Vector3(tube, length * 0.5f, tube), look.cap, look.outlineThin);
                WorldPrim(PrimitiveType.Sphere, "Funnel Rail End", root, a + from.up * height, Quaternion.identity, Vector3.one * tube, look.cap, look.outlineThin);
                WorldPrim(PrimitiveType.Sphere, "Funnel Rail End", root, b + from.up * height, Quaternion.identity, Vector3.one * tube, look.cap, look.outlineThin);
            }
        }

        struct Deflector
        {
            public TrackMeshBuilder.Sample frame;
            public int side;
        }

        static float SectionStart(List<TrackMeshBuilder.Sample> samples, int section)
        {
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].section == section)
                {
                    return samples[i - 1].distance;
                }
            }

            return -1f;
        }

        /// <summary>
        /// Small triangular wedges on both rails between peg rows: a ramp about 42 deg to the flow, so a marble sliding along the wall is
        /// turned back toward the centre instead of bypassing the pegs. Each is a convex prism of rail height with a toon body and an outline;
        /// the tip sits level with an offset row, where the nearest pegs are 0.6+ m away (checked by <see cref="CheckPegFieldGaps"/>).
        /// </summary>
        static void BuildDeflectors(Transform parent, List<TrackMeshBuilder.Sample> samples, TimerLook look, PhysicsMaterial physics,
            List<Deflector> built)
        {
            var root = new GameObject("Wall Deflectors").transform;
            root.SetParent(parent, false);
            float depth = TimerCourseFactory.DeflectorDepth, length = TimerCourseFactory.DeflectorLength;
            const float sink = 0.03f;
            float height = TimerCourseFactory.RailHeight;

            foreach (KeyValuePair<int, float> site in TimerCourseFactory.DeflectorSites())
            {
                float start = SectionStart(samples, site.Key);
                if (start < 0f)
                {
                    continue;
                }

                TrackMeshBuilder.Sample frame = TrackMeshBuilder.AtExact(samples, start + site.Value);
                for (int side = -1; side <= 1; side += 2)
                {
                    float wall = frame.width * 0.5f;
                    Vector3 tip = frame.position + frame.right * (side * (wall - depth));
                    var go = new GameObject($"Deflector {(side < 0 ? "L" : "R")} (section {site.Key} +{site.Value:0.00})");
                    go.transform.SetParent(root, false);
                    go.transform.SetPositionAndRotation(tip, Quaternion.LookRotation(frame.forward, frame.up));

                    Mesh mesh = WedgeMesh(side, depth + sink, length, height);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = look.funnel;
                    var col = go.AddComponent<MeshCollider>();
                    col.sharedMesh = mesh;
                    col.convex = true;
                    col.sharedMaterial = physics;
                    TimerMaterials.AddOutline(renderer, look.outlineThin);
                    built.Add(new Deflector { frame = frame, side = side });
                }
            }
        }

        /// <summary>Triangular prism in the deflector's frame (x right, y up, z forward): tip at the origin, back corner at x = side * depth, ramp start length behind it.</summary>
        static Mesh WedgeMesh(int side, float depth, float length, float height)
        {
            var tri = new[] { new Vector2(0f, 0f), new Vector2(side * depth, 0f), new Vector2(side * depth, -length) };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 centre = new Vector3(side * depth * 0.667f, height * 0.5f, -length * 0.333f);

            void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - centre) >= 0f)
                {
                    triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                }
                else
                {
                    triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                }
            }

            Vector3 Bottom(int k) => new Vector3(tri[k].x, -0.02f, tri[k].y);
            Vector3 Top(int k) => new Vector3(tri[k].x, height, tri[k].y);
            Face(Bottom(0), Bottom(1), Bottom(2));
            Face(Top(0), Top(1), Top(2));
            for (int k = 0; k < 3; k++)
            {
                int n = (k + 1) % 3;
                Face(Bottom(k), Bottom(n), Top(n));
                Face(Bottom(k), Top(n), Top(k));
            }

            var mesh = new Mesh { name = "TimerDeflector" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>
        /// Measures every gap a marble could rest in, from the pegs actually built: peg to peg, peg to rail, peg to deflector wedge and peg to
        /// funnel mouth tip. A gap between 0.05 m and <see cref="TimerCourseFactory.MinFieldGap"/> is a wedge a 0.4 m marble sticks in.
        /// </summary>
        static bool CheckPegFieldGaps(Transform obstacles, List<TrackMeshBuilder.Sample> samples, List<Deflector> deflectors, out float minGap, out string where)
        {
            var pegs = new List<Vector3>();
            var radii = new List<float>();
            foreach (Transform t in obstacles.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Peg")
                {
                    pegs.Add(t.position);
                    radii.Add(t.lossyScale.x * 0.5f);
                }
            }

            float lowest = float.MaxValue;
            string lowestWhere = pegs.Count == 0 ? "no pegs" : "";
            float limit = TimerCourseFactory.MinFieldGap;
            bool ok = pegs.Count > 0;

            void Note(float gap, string what)
            {
                if (gap < lowest)
                {
                    lowest = gap;
                    lowestWhere = what;
                }

                if (gap < limit && gap > 0.05f)
                {
                    ok = false;
                }
            }

            for (int i = 0; i < pegs.Count; i++)
            {
                for (int j = i + 1; j < pegs.Count; j++)
                {
                    Note(Vector3.Distance(pegs[i], pegs[j]) - radii[i] - radii[j], $"peg to peg at {pegs[i]:0.0}");
                }

                int best = 0;
                float bestSq = float.MaxValue;
                for (int k = 0; k < samples.Count; k++)
                {
                    float sq = (samples[k].position - pegs[i]).sqrMagnitude;
                    if (sq < bestSq)
                    {
                        bestSq = sq;
                        best = k;
                    }
                }

                TrackMeshBuilder.Sample near = samples[best];
                float lateral = Mathf.Abs(Vector3.Dot(pegs[i] - near.position, near.right));
                Note(near.width * 0.5f - lateral - radii[i], $"peg to rail at {pegs[i]:0.0}");
            }

            foreach (Deflector d in deflectors)
            {
                float wall = d.frame.width * 0.5f;
                Vector2 tip = new Vector2(wall - TimerCourseFactory.DeflectorDepth, 0f);
                Vector2 back = new Vector2(wall, 0f);
                Vector2 start = new Vector2(wall, -TimerCourseFactory.DeflectorLength);
                for (int i = 0; i < pegs.Count; i++)
                {
                    Vector3 v = pegs[i] - d.frame.position;
                    Vector2 p = new Vector2(Vector3.Dot(v, d.frame.right) * d.side, Vector3.Dot(v, d.frame.forward));
                    if (Mathf.Abs(p.y) > 3f)
                    {
                        continue;
                    }

                    float distance = Mathf.Min(SegmentDistance(p, tip, back), Mathf.Min(SegmentDistance(p, back, start), SegmentDistance(p, start, tip)));
                    Note(distance - radii[i], $"peg to deflector at {d.frame.position:0.0}");
                }
            }

            foreach (int lane in new[] { TimerCourseFactory.FunnelLaneA, TimerCourseFactory.FunnelLaneB })
            {
                float start = SectionStart(samples, lane);
                if (start < 0f)
                {
                    continue;
                }

                TrackMeshBuilder.Sample mouth = TrackMeshBuilder.AtExact(samples, start + TimerCourseFactory.FunnelLength);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 tip = mouth.position + mouth.right * (side * TimerCourseFactory.FunnelMouth * 0.5f);
                    for (int i = 0; i < pegs.Count; i++)
                    {
                        Vector3 flatPeg = pegs[i] - mouth.up * Vector3.Dot(pegs[i] - tip, mouth.up);
                        Note(Vector3.Distance(flatPeg, tip) - radii[i] - 0.06f, $"peg to funnel mouth at {tip:0.0}");
                    }
                }
            }

            minGap = lowest;
            where = lowestWhere;
            return ok;
        }

        static void BuildFinish(Transform parent, TrackMeshBuilder.Sample finish, TimerLook look)
        {
            var root = new GameObject("Finish Gantry").transform;
            root.SetParent(parent, false);

            const float height = 2.8f;
            Vector3 flat = Flat(finish.forward);
            Quaternion upright = Quaternion.LookRotation(flat, Vector3.up);
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            float reach = finish.width * 0.5f + TrackMeshBuilder.RailThickness + 0.14f;

            for (int side = -1; side <= 1; side += 2)
            {
                float bottom = finish.position.y - 0.3f;
                float postHeight = finish.position.y + height - bottom;
                Vector3 foot = finish.position + right * (side * reach);
                WorldPrim(PrimitiveType.Cylinder, side < 0 ? "Post Left" : "Post Right", root, new Vector3(foot.x, bottom + postHeight * 0.5f, foot.z),
                    Quaternion.identity, new Vector3(0.28f, postHeight * 0.5f, 0.28f), look.rail, look.outlineThin);
            }

            Vector3 beamCentre = finish.position + Vector3.up * height;
            WorldPrim(PrimitiveType.Cube, "Beam", root, beamCentre, upright, new Vector3(reach * 2f + 0.5f, 0.24f, 0.28f), look.hopperRim, look.outlineThin);
            Vector3 boardCentre = beamCentre + Vector3.up * 0.6f;
            WorldPrim(PrimitiveType.Cube, "Board", root, boardCentre, upright, new Vector3(reach * 2f + 0.2f, 0.9f, 0.12f), look.board, look.outlineThin);

            Vector2 size = new Vector2(reach * 2f - 0.1f, 0.8f);
            Decal(root, "Label", "FINISH", boardCentre - flat * 0.08f, upright, size, Color.white);
            Decal(root, "Label (back)", "FINISH", boardCentre + flat * 0.08f, Quaternion.LookRotation(-flat, Vector3.up), size, Color.white);

            Quaternion tilted = Quaternion.LookRotation(finish.forward, finish.up);
            WorldPrim(PrimitiveType.Cube, "Finish Stripe", root, finish.position + finish.up * 0.008f, tilted, new Vector3(finish.width, 0.01f, 0.3f), look.cap);
        }

        static void Decal(Transform parent, string name, string text, Vector3 position, Quaternion rotation, Vector2 size, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = size;
            tmp.text = text;
            tmp.color = colour;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableAutoSizing = false;
            tmp.fontSize = 6f;
        }

        /// <summary>Cartoon pillars from the floor to the underside of the track: a foot, a column and a collar. Skips spots a lower stretch or the collector is in the way of.</summary>
        static void BuildSupports(Transform parent, List<TrackMeshBuilder.Sample> samples, Vector3 collectorCentre, TimerLook look)
        {
            var root = new GameObject("Supports").transform;
            root.SetParent(parent, false);
            const float spacing = 8f;
            float next = 1.5f;
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].distance < next)
                {
                    continue;
                }

                next += spacing;
                // On a banked bend the floor drops toward the inside, so the collar (radius 0.4) would poke up through it: drop the top by the bank's fall across it.
                float underside = samples[i].position.y - TrackMeshBuilder.FloorThickness - Mathf.Abs(samples[i].right.y) * 0.45f - 0.12f;
                Vector2 flatDelta = new Vector2(samples[i].position.x - collectorCentre.x, samples[i].position.z - collectorCentre.z);
                if (underside < 0.8f || flatDelta.magnitude < CollectorRadius + 1.6f || TrackMeshBuilder.IsObstructedBelow(samples, i))
                {
                    continue;
                }

                var pillar = new GameObject("Pillar").transform;
                pillar.SetParent(root, false);
                pillar.position = new Vector3(samples[i].position.x, 0f, samples[i].position.z);
                Prim(PrimitiveType.Cylinder, "Column", pillar, new Vector3(0f, underside * 0.5f, 0f), Quaternion.identity, new Vector3(0.5f, underside * 0.5f, 0.5f), look.support, look.outlineThin);
                Prim(PrimitiveType.Cylinder, "Foot", pillar, new Vector3(0f, 0.12f, 0f), Quaternion.identity, new Vector3(1.1f, 0.12f, 1.1f), look.platform, look.outlineThin);
                Prim(PrimitiveType.Cylinder, "Collar", pillar, new Vector3(0f, underside - 0.1f, 0f), Quaternion.identity, new Vector3(0.8f, 0.1f, 0.8f), look.platform, look.outlineThin);
            }
        }

        // ------------------------------------------------------------------ collector

        static Vector3 CollectorCentre(TrackMeshBuilder.Sample end)
        {
            Vector3 flat = Flat(end.forward);
            return new Vector3(end.position.x + flat.x * CollectorOffset, 0f, end.position.z + flat.z * CollectorOffset);
        }

        /// <summary>
        /// The collector: a platter, a glass tube (a mesh for looks, 24 box segments and a floor for physics), a rim and base ring and a few
        /// ribs. Returns the anchor at the middle of its open mouth.
        /// </summary>
        static Transform BuildCollector(Transform parent, Vector3 centre, TimerLook look, PhysicsMaterial glassPhysics)
        {
            var root = new GameObject("Collector").transform;
            root.SetParent(parent, false);
            root.position = centre;

            Prim(PrimitiveType.Cylinder, "Platter", root, new Vector3(0f, CollectorFloor * 0.5f, 0f), Quaternion.identity,
                new Vector3((CollectorRadius + 0.6f) * 2f, CollectorFloor * 0.5f, (CollectorRadius + 0.6f) * 2f), look.platform, look.outlineCourse);

            var floor = new GameObject("Collector Floor");
            floor.transform.SetParent(root, false);
            floor.transform.localPosition = new Vector3(0f, CollectorFloor * 0.5f - 0.1f, 0f);
            var floorBox = floor.AddComponent<BoxCollider>();
            floorBox.size = new Vector3(CollectorRadius * 2f + 0.8f, CollectorFloor + 0.2f, CollectorRadius * 2f + 0.8f);
            floorBox.sharedMaterial = glassPhysics;

            float wallHeight = CollectorHeight - CollectorFloor;
            float step = 360f / CollectorWalls;
            float width = 2f * (CollectorRadius + 0.2f) * Mathf.Tan(step * 0.5f * Mathf.Deg2Rad) + 0.04f;
            var walls = new GameObject("Collector Walls").transform;
            walls.SetParent(root, false);
            for (int i = 0; i < CollectorWalls; i++)
            {
                Quaternion turn = Quaternion.Euler(0f, i * step, 0f);
                var wall = new GameObject($"Wall {i:00}");
                wall.transform.SetParent(walls, false);
                wall.transform.localRotation = turn;
                wall.transform.localPosition = turn * Vector3.forward * (CollectorRadius + 0.1f) + Vector3.up * (CollectorFloor + wallHeight * 0.5f);
                var box = wall.AddComponent<BoxCollider>();
                box.size = new Vector3(width, wallHeight, 0.2f);
                box.sharedMaterial = glassPhysics;
            }

            Mesh glassMesh = TrackMeshAssets.SaveMeshFresh(TimerRailBuilder.BuildCylinderWall(CollectorRadius + 0.04f, wallHeight, CollectorFloor, 48,
                "TimerCollectorGlass"), "TimerCollectorGlass", MeshFolder);
            var glass = new GameObject("Collector Glass");
            glass.transform.SetParent(root, false);
            glass.AddComponent<MeshFilter>().sharedMesh = glassMesh;
            var glassRenderer = glass.AddComponent<MeshRenderer>();
            glassRenderer.sharedMaterial = look.glass;
            glassRenderer.shadowCastingMode = ShadowCastingMode.Off;

            Mesh rimMesh = TrackMeshAssets.SaveMeshFresh(TimerRailBuilder.BuildRing(CollectorRadius + 0.04f, 0.13f, 48, 10, "TimerCollectorRim"),
                "TimerCollectorRim", MeshFolder);
            AddRing(root, "Collector Rim", rimMesh, CollectorHeight, look.hopperRim, look.outlineThin);
            AddRing(root, "Collector Base Ring", rimMesh, CollectorFloor + 0.1f, look.hopperRim, look.outlineThin);

            for (int i = 0; i < 6; i++)
            {
                Quaternion turn = Quaternion.Euler(0f, i * 60f + 30f, 0f);
                Prim(PrimitiveType.Cube, "Collector Rib", root, turn * Vector3.forward * (CollectorRadius + 0.08f) + Vector3.up * (CollectorFloor + wallHeight * 0.5f),
                    turn, new Vector3(0.12f, wallHeight, 0.12f), look.rail, look.outlineThin);
            }

            var mouth = new GameObject("Collector Mouth").transform;
            mouth.SetParent(root, false);
            mouth.localPosition = Vector3.up * CollectorHeight;
            return mouth;
        }

        static void AddRing(Transform parent, string name, Mesh mesh, float height, Material material, Material outline)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * height;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            TimerMaterials.AddOutline(renderer, outline);
        }

        // ------------------------------------------------------------------ marbles

        static Rigidbody BuildMarble(Transform parent, int index, TimerConfig config, TimerLook look, PhysicsMaterial physics, Vector3 position)
        {
            GameObject marble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marble.name = $"Marble {index:000}";
            marble.transform.SetParent(parent, false);
            marble.transform.position = position;
            marble.transform.localScale = Vector3.one * (config.marbleRadius * 2f);

            var renderer = marble.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = look.marbles[index % look.marbles.Length];
            TimerMaterials.AddOutline(renderer, look.outlineMarble);
            marble.GetComponent<SphereCollider>().sharedMaterial = physics;

            var body = marble.AddComponent<Rigidbody>();
            body.mass = config.marbleMass;
            body.linearDamping = config.marbleLinearDamping;
            body.angularDamping = config.marbleAngularDamping;
            body.maxAngularVelocity = 100f;
            body.maxDepenetrationVelocity = config.maxDepenetrationVelocity;
            body.sleepThreshold = 0f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            marble.SetActive(false);
            return body;
        }

        // ------------------------------------------------------------------ utilities

        static Vector3 Flat(Vector3 forward)
        {
            var flat = new Vector3(forward.x, 0f, forward.z);
            return flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
        }

        static Bounds BoundsOf(List<TrackMeshBuilder.Sample> samples)
        {
            var bounds = new Bounds(samples[0].position, Vector3.zero);
            foreach (TrackMeshBuilder.Sample sample in samples)
            {
                bounds.Encapsulate(sample.position);
            }

            return bounds;
        }

        static void Report(List<TrackMeshBuilder.Sample> samples, TrackLayout layout, TimerConfig config, float lift)
        {
            float length = TimerCourseFactory.CourseLength(samples, layout);
            float estimate = TimerCourseFactory.EstimateTravelSeconds(samples, layout, config, out float meanSpeed);
            float drop = samples[0].position.y - samples[samples.Count - 1].position.y;
            int crossings = TrackMeshBuilder.CountCrossings(samples, out string where);
            Debug.Log($"[timer] Built {ScenePath}: {length:0.0} m gate to finish, {drop:0.0} m drop, start {lift:0.0} m up. " +
                      $"Terminal-speed model: mean {meanSpeed:0.0} m/s, travel {estimate:0} s (config expectedTravelSeconds {config.expectedTravelSeconds:0}; " +
                      "a model, run the Stream Test for the measured figure). Collector " +
                      $"{CollectorRadius * 2f:0.0} m wide, {CollectorHeight:0.0} m tall. " +
                      "Seed: randomizeSeedOnStart is on, the runner logs the drawn seed at Play; paste it into the runner's seed to re-render.");
            if (crossings > 0)
            {
                Debug.LogWarning($"[timer] The course passes through itself {crossings} times, first {where}. Loosen a radius or add drop.");
            }
        }
    }
}
