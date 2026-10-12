using System.Collections.Generic;
using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.Race.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Scenes/Race_GrandPrix_3D.unity</c>: a field of jewel-coloured glass marbles racing
    /// down a piano-black lacquer course with brass rails, on a pale polished studio floor. 16:9, for long form.
    /// Menu: <c>Simulation Lobby ▸ Build Scene ▸ Race_GrandPrix_3D</c>.
    /// </summary>
    /// <remarks>
    /// Everything is generated from primitives and swept meshes, so there are no model files to import.
    /// Re-running replaces the scene but never the config, layout or material assets, so tuning survives.
    /// </remarks>
    static class RaceSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Race_GrandPrix_3D.unity";
        const string ConfigFolder = "Assets/Settings/Configs/Race";
        const string LayoutPath = ConfigFolder + "/RaceTrackLayout_GrandPrix.asset";
        const string ConfigPath = ConfigFolder + "/RaceConfig_001.asset";
        const string MaterialFolder = "Assets/Settings/Studio";
        const int ReflectionOnlyLayer = 31;
        const int LeaderboardRows = 14;

        /// <summary>Champagne brass: the video's one metal, echoed in the key light and the backdrop halo.</summary>
        static readonly Color Hue = new Color(0.93f, 0.71f, 0.38f, 1f);

        [MenuItem("Simulation Lobby/Reset Config/Race Track Layout (default course)")]
        static void ResetLayout()
        {
            Directory.CreateDirectory(ConfigFolder);
            RaceTrackLayout layout = EnsureLayout();
            layout.ResetToDefault();
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
            Debug.Log($"[race] Reset {LayoutPath} to the default course ({layout.sections.Length} sections, {layout.obstacles.Length} obstacles).", layout);
        }

        [MenuItem("Simulation Lobby/Build Scene/Race_GrandPrix_3D")]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[race] Exit play mode before building the scene.");
                return;
            }

            Directory.CreateDirectory(ConfigFolder);
            Directory.CreateDirectory(MaterialFolder);
            RaceTrackLayout layout = EnsureLayout();
            RaceConfig config = EnsureConfig(layout);
            PhysicsMaterial marblePhysics = EnsurePhysicsMaterial("RaceMarble", 0.3f, 0.35f, PhysicsMaterialCombine.Average, 0.32f);
            PhysicsMaterial trackPhysics = EnsurePhysicsMaterial("RaceTrack", 0.32f, 0.4f, PhysicsMaterialCombine.Average, 0.25f);
            PhysicsMaterial pegPhysics = EnsurePhysicsMaterial("RacePeg", 0.2f, 0.25f, PhysicsMaterialCombine.Average, 0.5f);

            // Sample once to find the drop, then again lifted so the lowest point clears the floor.
            List<TrackMeshBuilder.Sample> samples = TrackMeshBuilder.SampleCentreline(layout, Vector3.zero);
            float lowest = float.MaxValue;
            foreach (TrackMeshBuilder.Sample sample in samples)
            {
                lowest = Mathf.Min(lowest, sample.position.y);
            }

            float lift = 1.5f - lowest;
            samples = TrackMeshBuilder.SampleCentreline(layout, new Vector3(0f, lift, 0f));
            float totalLength = samples[samples.Count - 1].distance;
            float finishDistance = Mathf.Max(1f, totalLength - layout.runOutLength);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera. Placed near the start; the rig takes it from there. ---
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 400f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = samples[0].position + new Vector3(-6f, 4f, -8f);
            cameraObject.transform.LookAt(samples[0].position);
            ChaseCameraRig rig = cameraObject.AddComponent<ChaseCameraRig>();
            rig.Bind(camera);

            // --- Studio: post, camera setup, ambient, then replace the small-subject rig with a course-sized one. ---
            Bounds bounds = BoundsOf(samples);
            StudioSetBuilder.Materials materials = StudioSetBuilder.Build(camera, new StudioSetBuilder.Options
            {
                hue = Hue,
                glassName = "RaceGlass",
                subjectCenter = bounds.center,
                subjectHeight = bounds.size.y
            });
            DestroyByName("Cyclorama");
            DestroyByName("Studio Lights");
            DestroyByName("Softbox Cards (reflection only)");
            DestroyByName("Studio Reflection Probe");

            camera.cullingMask &= ~(1 << ReflectionOnlyLayer);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 190f;

            BuildFloor();
            BuildLights();
            BuildReflections(bounds);

            // --- The course. ---
            var course = new GameObject("Course").transform;
            Mesh bodyMesh = SaveMeshFresh(TrackMeshBuilder.BuildBody(samples), "RaceTrackBody");
            Mesh inlayMesh = SaveMeshFresh(TrackMeshBuilder.BuildInlay(samples), "RaceTrackInlay");

            var track = new GameObject("Track");
            track.transform.SetParent(course, false);
            track.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            var trackRenderer = track.AddComponent<MeshRenderer>();
            trackRenderer.sharedMaterials = new[] { materials.lacquer, materials.brass };
            var trackCollider = track.AddComponent<MeshCollider>();
            trackCollider.sharedMesh = bodyMesh;
            trackCollider.sharedMaterial = trackPhysics;

            var centreline = new Vector3[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                centreline[i] = samples[i].position;
            }

            TrackPath path = track.AddComponent<TrackPath>();
            path.Set(centreline);

            var inlay = new GameObject("Brass Pinstripes");
            inlay.transform.SetParent(course, false);
            inlay.AddComponent<MeshFilter>().sharedMesh = inlayMesh;
            var inlayRenderer = inlay.AddComponent<MeshRenderer>();
            inlayRenderer.sharedMaterial = materials.brass;
            inlayRenderer.shadowCastingMode = ShadowCastingMode.Off;

            TrackMeshBuilder.Sample gateSample = TrackMeshBuilder.At(samples, layout.gateDistance);
            TrackMeshBuilder.Sample finishSample = TrackMeshBuilder.At(samples, finishDistance);

            Transform startOrigin = NewFrame("Start Origin", course, gateSample);
            GameObject gate = BuildGate(course, gateSample, materials.brass);
            BuildBackWall(course, samples[0], materials.lacquer);
            Transform finishLine = NewFrame("Finish Line", course, finishSample);
            BuildFinish(course, finishSample, materials);
            BuildEndWall(course, samples[samples.Count - 1], materials);
            BuildSupports(course, samples, materials.chrome);
            Transform obstacleRoot = ObstacleBuilder.Build(course, layout, samples, new ObstacleBuilder.Kit
            {
                chrome = materials.chrome,
                brass = materials.brass,
                lacquer = materials.lacquer,
                peg = pegPhysics,
                surface = trackPhysics
            });

            // --- Marbles: one inactive template; the simulation clones it from the config roster. ---
            GameObject template = BuildMarbleTemplate(marblePhysics);
            var marbleParent = new GameObject("Marbles").transform;

            // --- Simulation. Random seed per Play, logged by the runner; paste it back to re-render. ---
            var simulationObject = new GameObject("Simulation");
            RaceSimulation simulation = simulationObject.AddComponent<RaceSimulation>();
            var runner = simulationObject.GetComponent<SimulationRunner>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;
            simulation.Bind(template, marbleParent, startOrigin, gate, finishLine, path, obstacleRoot);
            RacePresenter presenter = simulationObject.AddComponent<RacePresenter>();

            // --- Audio + HUD. ---
            var audioObject = new GameObject("Audio");
            audioObject.AddComponent<AudioSource>();
            MaterialContactAudio audio = audioObject.AddComponent<MaterialContactAudio>();
            var rollObject = new GameObject("Marble Roll");
            rollObject.transform.SetParent(audioObject.transform, false);
            AudioSource rollSource = rollObject.AddComponent<AudioSource>();
            rollSource.playOnAwake = false;

            LeaderboardHud hud = LeaderboardCanvasBuilder.Build(presenter.title, LeaderboardRows);
            presenter.Bind(hud, rig, audio, rollSource);

            if (config.roster.Length > LeaderboardRows)
            {
                Debug.LogWarning($"[race] The roster has {config.roster.Length} contenders but the leaderboard shows {LeaderboardRows}.");
            }

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Report(samples, totalLength, finishDistance, lift);
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

        static GameObject BuildGate(Transform parent, TrackMeshBuilder.Sample sample, Material material)
        {
            var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gate.name = "Start Gate";
            gate.transform.SetParent(parent, false);
            gate.transform.rotation = Quaternion.LookRotation(sample.forward, sample.up);
            gate.transform.position = sample.position + sample.up * 0.3f;
            gate.transform.localScale = new Vector3(sample.width, 0.6f, 0.06f);
            gate.GetComponent<MeshRenderer>().sharedMaterial = material;
            return gate;
        }

        static void BuildBackWall(Transform parent, TrackMeshBuilder.Sample sample, Material lacquer)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Start Back Wall";
            wall.transform.SetParent(parent, false);
            wall.transform.rotation = Quaternion.LookRotation(sample.forward, sample.up);
            wall.transform.position = sample.position - sample.forward * 0.06f + sample.up * 0.25f;
            wall.transform.localScale = new Vector3(sample.width + 0.4f, 0.6f, 0.12f);
            wall.GetComponent<MeshRenderer>().sharedMaterial = lacquer;
        }

        static void BuildEndWall(Transform parent, TrackMeshBuilder.Sample sample, StudioSetBuilder.Materials materials)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "End Wall";
            wall.transform.SetParent(parent, false);
            wall.transform.rotation = Quaternion.LookRotation(sample.forward, sample.up);
            wall.transform.position = sample.position + sample.forward * 0.08f + sample.up * 0.3f;
            wall.transform.localScale = new Vector3(sample.width + 0.4f, 0.7f, 0.16f);
            wall.GetComponent<MeshRenderer>().sharedMaterial = materials.lacquer;

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = "End Wall Cap";
            cap.transform.SetParent(parent, false);
            cap.transform.rotation = wall.transform.rotation;
            cap.transform.position = wall.transform.position + sample.up * 0.36f;
            cap.transform.localScale = new Vector3(sample.width + 0.4f, 0.03f, 0.17f);
            cap.GetComponent<MeshRenderer>().sharedMaterial = materials.brass;
            Object.DestroyImmediate(cap.GetComponent<Collider>());
        }

        /// <summary>A brass-and-chrome gantry over the line, a smoked-glass banner under its beam, and a brass stripe across the track.</summary>
        static void BuildFinish(Transform parent, TrackMeshBuilder.Sample sample, StudioSetBuilder.Materials materials)
        {
            var root = new GameObject("Finish Gantry").transform;
            root.SetParent(parent, false);
            Quaternion rotation = Quaternion.LookRotation(sample.forward, sample.up);
            const float postHeight = 3.4f;
            float reach = sample.width * 0.5f + RailOffset();

            for (int side = -1; side <= 1; side += 2)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = side < 0 ? "Post Left" : "Post Right";
                post.transform.SetParent(root, false);
                post.transform.position = sample.position + sample.right * (side * reach) + Vector3.up * (postHeight * 0.5f);
                post.transform.localScale = new Vector3(0.14f, postHeight * 0.5f, 0.14f);
                post.GetComponent<MeshRenderer>().sharedMaterial = materials.chrome;
                Object.DestroyImmediate(post.GetComponent<Collider>());
            }

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Beam";
            beam.transform.SetParent(root, false);
            beam.transform.rotation = rotation;
            beam.transform.position = sample.position + Vector3.up * postHeight;
            beam.transform.localScale = new Vector3(reach * 2f + 0.3f, 0.16f, 0.2f);
            beam.GetComponent<MeshRenderer>().sharedMaterial = materials.brass;
            Object.DestroyImmediate(beam.GetComponent<Collider>());

            var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = "Glass Banner";
            banner.transform.SetParent(root, false);
            banner.transform.rotation = rotation;
            banner.transform.position = sample.position + Vector3.up * (postHeight - 0.5f);
            banner.transform.localScale = new Vector3(reach * 2f - 0.1f, 0.7f, 0.03f);
            banner.GetComponent<MeshRenderer>().sharedMaterial = materials.smokedGlass;
            Object.DestroyImmediate(banner.GetComponent<Collider>());

            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Finish Stripe";
            stripe.transform.SetParent(root, false);
            stripe.transform.rotation = rotation;
            stripe.transform.position = sample.position + sample.up * 0.006f;
            stripe.transform.localScale = new Vector3(sample.width, 0.008f, 0.14f);
            stripe.GetComponent<MeshRenderer>().sharedMaterial = materials.brass;
            Object.DestroyImmediate(stripe.GetComponent<Collider>());
        }

        static float RailOffset() => TrackMeshBuilder.RailThickness + 0.2f;

        /// <summary>Slim chrome columns from the floor to the underside of the track, skipping any that would pierce a lower stretch.</summary>
        static void BuildSupports(Transform parent, List<TrackMeshBuilder.Sample> samples, Material chrome)
        {
            var root = new GameObject("Supports").transform;
            root.SetParent(parent, false);
            const float spacing = 11f;
            float next = spacing;
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].distance < next)
                {
                    continue;
                }

                next += spacing;
                float underside = samples[i].position.y - TrackMeshBuilder.FloorThickness;
                if (underside < 0.6f || TrackMeshBuilder.IsObstructedBelow(samples, i))
                {
                    continue;
                }

                var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                column.name = "Column";
                column.transform.SetParent(root, false);
                column.transform.position = new Vector3(samples[i].position.x, underside * 0.5f, samples[i].position.z);
                column.transform.localScale = new Vector3(0.2f, underside * 0.5f, 0.2f);
                column.GetComponent<MeshRenderer>().sharedMaterial = chrome;
                Object.DestroyImmediate(column.GetComponent<Collider>());
            }
        }

        static GameObject BuildMarbleTemplate(PhysicsMaterial physics)
        {
            var parent = new GameObject("Templates").transform;
            var marble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marble.name = "Marble Template";
            marble.transform.SetParent(parent, false);

            var renderer = marble.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = EnsureMarbleMaterial();

            var collider = marble.GetComponent<SphereCollider>();
            collider.sharedMaterial = physics;

            marble.AddComponent<Rigidbody>();
            marble.AddComponent<CollisionDetector3D>();
            marble.AddComponent<ContenderIdentity>();

            var trail = marble.AddComponent<TrailRenderer>();
            trail.sharedMaterial = EnsureMaterial("RaceTrail", "Sprites/Default", m => { });
            trail.time = 0.8f;
            trail.minVertexDistance = 0.06f;
            trail.widthMultiplier = 0.1f;
            trail.numCornerVertices = 2;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            marble.SetActive(false);
            return marble;
        }

        // ------------------------------------------------------------------ studio

        static void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Studio Floor";
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(500f, 0.1f, 500f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.AddComponent<BoxCollider>();
            floor.GetComponent<MeshRenderer>().sharedMaterial = EnsureMaterial("RaceFloor", "Universal Render Pipeline/Lit", m =>
            {
                // Pale champagne, polished: the dark course reads against it, and its sheen picks up the probe.
                m.SetColor("_BaseColor", new Color(0.80f, 0.76f, 0.69f, 1f));
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0.8f);
            });
        }

        static void BuildLights()
        {
            var root = new GameObject("Race Lights").transform;

            // Key: warm, high and to the front-left; the only light that casts. Shadow distance is 50m
            // from the camera in PC_RPAsset, which is plenty for a chase shot.
            var key = NewDirectional(root, "Key", new Color(1f, 0.95f, 0.88f), 1.05f, Quaternion.Euler(48f, 148f, 0f));
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.55f;
            key.shadowResolution = LightShadowResolution.VeryHigh;

            // Fill and rim: cooler, no shadows, from the opposite side and behind, to lift the dark lacquer's edges.
            NewDirectional(root, "Fill", new Color(0.85f, 0.9f, 1f), 0.35f, Quaternion.Euler(30f, -35f, 0f));
            NewDirectional(root, "Rim", new Color(1f, 0.97f, 0.92f), 0.4f, Quaternion.Euler(18f, 330f, 0f));
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

        /// <summary>
        /// One box-projected probe over the whole course, plus large emissive cards around it on a layer the
        /// camera does not draw. The cards are what give the glass and lacquer their long soft highlights.
        /// </summary>
        static void BuildReflections(Bounds bounds)
        {
            var root = new GameObject("Reflections").transform;

            var probeObject = new GameObject("Course Reflection Probe");
            probeObject.transform.SetParent(root, false);
            probeObject.transform.position = bounds.center + Vector3.up * 2f;
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 512;
            probe.hdr = true;
            probe.boxProjection = true;
            probe.size = bounds.size + new Vector3(60f, 40f, 60f);
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.80f, 0.74f, 0.64f, 1f);
            probe.nearClipPlane = 0.1f;
            probe.farClipPlane = 250f;
            probe.importance = 10;

            Material card = StudioSetBuilder.EnsureCardMaterial();
            card.SetColor("_BaseColor", new Color(1.8f, 1.8f, 1.8f, 1f));
            EditorUtility.SetDirty(card);

            float ring = Mathf.Max(bounds.extents.x, bounds.extents.z) + 30f;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"Softbox {i + 1}";
                quad.transform.SetParent(root, false);
                quad.transform.position = bounds.center + direction * ring + Vector3.up * (bounds.extents.y * 0.8f + 6f);
                quad.transform.rotation = Quaternion.LookRotation(quad.transform.position - bounds.center);
                quad.transform.localScale = new Vector3(40f, 14f, 1f);
                quad.layer = ReflectionOnlyLayer;
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = card;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        // ------------------------------------------------------------------ assets

        /// <summary>
        /// Saves a generated mesh as a brand-new asset, deleting any earlier file at the same path first.
        /// <c>StudioSetBuilder.SaveMesh</c> overwrites the existing asset in place, which is fine for a mesh
        /// that never changes shape. The course changes shape whenever the layout does, and an in-place
        /// overwrite left the editor drawing the previous build's mesh while physics and the obstacles used
        /// the new one: pegs and marbles hovered over a track that was no longer where it was drawn.
        /// </summary>
        static Mesh SaveMeshFresh(Mesh mesh, string name)
        {
            string folder = $"{StudioSetBuilder.Folder}/Meshes";
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        static RaceTrackLayout EnsureLayout()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RaceTrackLayout>(LayoutPath);
            if (existing != null)
            {
                // A layout saved before obstacles existed is rebuilt from the current default once. After
                // that its version matches and any hand edits are left alone.
                if (existing.version < RaceTrackLayout.CurrentVersion)
                {
                    existing.ResetToDefault();
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[race] Upgraded {LayoutPath} to course version {RaceTrackLayout.CurrentVersion} (obstacles added).", existing);
                }

                return existing;
            }

            var layout = ScriptableObject.CreateInstance<RaceTrackLayout>();
            layout.version = RaceTrackLayout.CurrentVersion;
            AssetDatabase.CreateAsset(layout, LayoutPath);
            return layout;
        }

        static RaceConfig EnsureConfig(RaceTrackLayout layout)
        {
            var config = AssetDatabase.LoadAssetAtPath<RaceConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<RaceConfig>();
                config.fixedTimestep = 1f / 120f;
                config.maxDurationSeconds = 240f;
                config.intent = "race-001: twelve jewel-glass marbles down a black-lacquer course with brass rails. " +
                                "Tune for lead changes first, length second.";
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            if (config.track != layout && config.track == null)
            {
                config.track = layout;
                EditorUtility.SetDirty(config);
            }

            return config;
        }

        static PhysicsMaterial EnsurePhysicsMaterial(string name, float dynamicFriction, float staticFriction,
            PhysicsMaterialCombine combine, float bounciness)
        {
            string path = $"{ConfigFolder}/{name}.physicsMaterial";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new PhysicsMaterial(name)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounciness,
                frictionCombine = combine,
                bounceCombine = combine
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material EnsureMarbleMaterial()
        {
            return EnsureMaterial("RaceMarble", "Universal Render Pipeline/Complex Lit", m =>
            {
                // The roster colour arrives per marble through a property block. A lacquered glass bead:
                // saturated base under a mirror clear coat.
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Metallic", 0.1f);
                m.SetFloat("_Smoothness", 0.88f);
                if (m.HasProperty("_ClearCoat"))
                {
                    m.SetFloat("_ClearCoat", 1f);
                    m.SetFloat("_ClearCoatMask", 1f);
                    m.SetFloat("_ClearCoatSmoothness", 0.98f);
                    m.EnableKeyword("_CLEARCOAT");
                }
            });
        }

        static Material EnsureMaterial(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[race] Shader '{shaderName}' not found for {name}.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) { name = name };
            setup(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ------------------------------------------------------------------ utilities

        static Bounds BoundsOf(List<TrackMeshBuilder.Sample> samples)
        {
            var bounds = new Bounds(samples[0].position, Vector3.zero);
            foreach (TrackMeshBuilder.Sample sample in samples)
            {
                bounds.Encapsulate(sample.position);
            }

            return bounds;
        }

        static void DestroyByName(string name)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
            {
                Object.DestroyImmediate(found);
            }
        }

        static void Report(List<TrackMeshBuilder.Sample> samples, float totalLength, float finishDistance, float lift)
        {
            float estimate = TrackMeshBuilder.EstimateSeconds(samples, finishDistance);
            int crossings = TrackMeshBuilder.CountCrossings(samples, out string where);
            float drop = samples[0].position.y - samples[samples.Count - 1].position.y;
            Debug.Log($"[race] Built {ScenePath}: {totalLength:0}m of track, {drop:0.0}m drop, start {lift:0.0}m up. " +
                      $"Rough race time ~{estimate:0}s (a model, not a measurement: read the real one off the first Play).");
            if (crossings > 0)
            {
                Debug.LogWarning($"[race] The layout passes through itself {crossings} times, first {where}. " +
                                 "Loosen a radius or add drop before using it.");
            }
        }
    }
}
