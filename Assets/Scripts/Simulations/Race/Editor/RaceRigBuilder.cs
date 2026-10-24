using System.Collections.Generic;
using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using SimulationLobby.Shared.EditorTools;
using SimulationLobby.Shared.Tracks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SimulationLobby.Simulations.Race.EditorTools
{
    /// <summary>Everything one race (or one heat of a bigger show) needs, under a single root.</summary>
    public sealed class RaceRig
    {
        /// <summary>
        /// The rig's root. It carries the <see cref="RaceSimulation"/> (an <c>IHeatSimulation</c>) and the
        /// <see cref="RacePresenter"/> itself, and has the course, marbles, template, camera, audio and
        /// leaderboard canvas as children. Deactivating it hides the whole rig; there is no <c>SimulationRunner</c>.
        /// </summary>
        public GameObject root;

        public RaceSimulation simulation;
        public Camera camera;
        public Transform startOrigin, finishLine;

        public RacePresenter presenter;
        public LeaderboardHud hud;
        public ChaseCameraRig cameraRig;

        /// <summary>World bounds of the track centreline.</summary>
        public Bounds bounds;

        public List<TrackMeshBuilder.Sample> samples;
        public float totalLength;
        public float finishDistance;

        /// <summary>How far the track start was lifted so its lowest point clears the floor.</summary>
        public float lift;
    }

    /// <summary>
    /// Builds one marble-race rig: the black-lacquer course with brass rails, start pen and gate, finish gantry,
    /// marble template, simulation, presenter, chase camera, leaderboard canvas and audio. The Grand Prix scene
    /// builds through this; so does every heat of the elimination show.
    /// </summary>
    public static class RaceRigBuilder
    {
        /// <summary>Champagne brass: the race look's one metal.</summary>
        public static readonly Color Hue = new Color(0.93f, 0.71f, 0.38f, 1f);

        public const string GlassName = "RaceGlass";
        public const int ReflectionOnlyLayer = 31;

        /// <summary>
        /// Builds one rig under <paramref name="parent"/> with its track start at <paramref name="origin"/>.
        /// </summary>
        /// <param name="parent">Must have an identity transform (the rig is built in world coordinates). Null for scene root.</param>
        /// <param name="title">Shown at the top of the leaderboard (<c>RacePresenter.title</c>).</param>
        /// <param name="meshFolder">Where <c>SaveMeshFresh</c> writes this rig's meshes. Unique per rig, or use a unique <paramref name="meshPrefix"/>.</param>
        /// <param name="meshPrefix">Prefix of the mesh asset names, so rigs sharing a folder do not overwrite each other.</param>
        /// <param name="leaderboardRows">Row capacity of the leaderboard; 0 = enough for the config's roster and <c>maxEntrants</c>.</param>
        /// <remarks>
        /// Does NOT add a <c>SimulationRunner</c> (a standalone scene adds one to <c>rig.root</c>; the orchestrator
        /// ticks a heat). The lights, floor and environment are scene-level: build them once (see
        /// <see cref="RaceStudioEnvironment"/>).
        /// </remarks>
        public static RaceRig BuildRig(TrackLayout layout, RaceConfig config, Vector3 origin, Transform parent,
            string title, string meshFolder, string meshPrefix = "Race", int leaderboardRows = 0)
        {
            Directory.CreateDirectory(RaceAssets.ConfigFolder);
            Directory.CreateDirectory(RaceAssets.MaterialFolder);

            PhysicsMaterial marblePhysics = RaceAssets.EnsurePhysicsMaterial("RaceMarble", 0.3f, 0.35f, PhysicsMaterialCombine.Average, 0.32f);
            PhysicsMaterial trackPhysics = RaceAssets.EnsurePhysicsMaterial("RaceTrack", 0.32f, 0.4f, PhysicsMaterialCombine.Average, 0.25f);
            PhysicsMaterial pegPhysics = RaceAssets.EnsurePhysicsMaterial("RacePeg", 0.2f, 0.25f, PhysicsMaterialCombine.Average, 0.5f);

            // The start pen must hold maxEntrants (4 columns x 4 rows = 16). The Grand Prix pen (4.4 m) already does.
            float penNeeded = RequiredPenLength(config);
            float gateDistance = layout.gateDistance;
            if (gateDistance < penNeeded)
            {
                Debug.LogWarning($"[race] '{title}': the layout's gateDistance {gateDistance:0.0}m is shorter than the {penNeeded:0.0}m " +
                                 $"a {config.maxEntrants}-marble pen needs. Using {penNeeded:0.0}m; lengthen the layout's start straight if the gate looks off.");
                gateDistance = penNeeded;
            }

            // Sample once to find the drop, then again lifted so the lowest point clears the floor.
            List<TrackMeshBuilder.Sample> samples = TrackMeshBuilder.SampleCentreline(layout, Vector3.zero);
            float lowest = float.MaxValue;
            foreach (TrackMeshBuilder.Sample sample in samples)
            {
                lowest = Mathf.Min(lowest, sample.position.y);
            }

            float lift = 1.5f - lowest;
            samples = TrackMeshBuilder.SampleCentreline(layout, origin + new Vector3(0f, lift, 0f));
            float totalLength = samples[samples.Count - 1].distance;
            float finishDistance = Mathf.Max(1f, totalLength - layout.runOutLength);
            Bounds bounds = BoundsOf(samples);

            StudioSetBuilder.Materials materials = StudioSetBuilder.EnsureMaterials(new StudioSetBuilder.Options
            {
                hue = Hue,
                glassName = GlassName,
                subjectCenter = bounds.center,
                subjectHeight = bounds.size.y
            });

            var root = new GameObject($"Race Rig - {title}");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            // --- Camera. Placed near the start; the presenter takes it from there. ---
            var cameraObject = new GameObject("Rig Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 400f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = samples[0].position + new Vector3(-6f, 4f, -8f);
            cameraObject.transform.LookAt(samples[0].position);
            ChaseCameraRig cameraRig = cameraObject.AddComponent<ChaseCameraRig>();
            cameraRig.Bind(camera);
            ConfigureCamera(camera);

            // --- The course. ---
            var course = new GameObject("Course").transform;
            course.SetParent(root.transform, false);
            Mesh bodyMesh = TrackMeshAssets.SaveMeshFresh(TrackMeshBuilder.BuildBody(samples), $"{meshPrefix}TrackBody", meshFolder);
            Mesh inlayMesh = TrackMeshAssets.SaveMeshFresh(TrackMeshBuilder.BuildInlay(samples), $"{meshPrefix}TrackInlay", meshFolder);

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

            TrackMeshBuilder.Sample gateSample = TrackMeshBuilder.At(samples, gateDistance);
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

            // --- Marbles: one inactive template; the simulation clones it from the roster or the heat's entrants. ---
            GameObject template = BuildMarbleTemplate(marblePhysics);
            template.transform.parent.SetParent(root.transform, false);
            var marbleParent = new GameObject("Marbles").transform;
            marbleParent.SetParent(root.transform, false);

            // --- Simulation + presenter live on the root, so the root alone identifies a heat. ---
            RaceSimulation simulation = root.AddComponent<RaceSimulation>();
            simulation.Bind(template, marbleParent, startOrigin, gate, finishLine, path, obstacleRoot);
            RacePresenter presenter = root.AddComponent<RacePresenter>();
            presenter.title = title;

            // --- Audio + HUD. ---
            var audioObject = new GameObject("Audio");
            audioObject.transform.SetParent(root.transform, false);
            audioObject.AddComponent<AudioSource>();
            MaterialContactAudio audio = audioObject.AddComponent<MaterialContactAudio>();
            var rollObject = new GameObject("Marble Roll");
            rollObject.transform.SetParent(audioObject.transform, false);
            AudioSource rollSource = rollObject.AddComponent<AudioSource>();
            rollSource.playOnAwake = false;

            int rows = leaderboardRows > 0 ? leaderboardRows : Mathf.Max(config.roster.Length, config.maxEntrants);
            LeaderboardHud hud = LeaderboardCanvasBuilder.Build(title, rows);
            hud.transform.SetParent(root.transform, false);
            presenter.Bind(hud, cameraRig, audio, rollSource);

            if (config.roster.Length > rows)
            {
                Debug.LogWarning($"[race] The roster has {config.roster.Length} contenders but the leaderboard shows {rows}.");
            }

            return new RaceRig
            {
                root = root,
                simulation = simulation,
                camera = camera,
                startOrigin = startOrigin,
                finishLine = finishLine,
                presenter = presenter,
                hud = hud,
                cameraRig = cameraRig,
                bounds = bounds,
                samples = samples,
                totalLength = totalLength,
                finishDistance = finishDistance,
                lift = lift
            };
        }

        /// <summary>How far behind the gate the rear row of a full pen sits, plus a margin.</summary>
        public static float RequiredPenLength(RaceConfig config)
        {
            int columns = Mathf.Max(1, config.gridColumns);
            int rows = Mathf.CeilToInt(Mathf.Max(config.roster.Length, config.maxEntrants) / (float)columns);
            float radius = config.marbleRadius;
            return radius * 2f + 0.04f + (rows - 1) * config.gridRowSpacing + radius * config.gridJitter + 0.3f;
        }

        /// <summary>The camera settings the studio look gives its camera, so a rig camera matches whichever one the studio build touched.</summary>
        public static void ConfigureCamera(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.80f, 0.74f, 0.64f, 1f);
            camera.allowHDR = true;
            camera.cullingMask &= ~(1 << ReflectionOnlyLayer);
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.requiresColorOption = CameraOverrideOption.On;
            data.requiresDepthOption = CameraOverrideOption.On;
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
            renderer.sharedMaterial = RaceAssets.EnsureMarbleMaterial();

            var collider = marble.GetComponent<SphereCollider>();
            collider.sharedMaterial = physics;

            marble.AddComponent<Rigidbody>();
            marble.AddComponent<CollisionDetector3D>();
            marble.AddComponent<ContenderIdentity>();

            var trail = marble.AddComponent<TrailRenderer>();
            trail.sharedMaterial = RaceAssets.EnsureMaterial("RaceTrail", "Sprites/Default", m => { });
            trail.time = 0.8f;
            trail.minVertexDistance = 0.06f;
            trail.widthMultiplier = 0.1f;
            trail.numCornerVertices = 2;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            marble.SetActive(false);
            return marble;
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
    }
}
