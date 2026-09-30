using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.Survival.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Scenes/Survival_WallVsBall_3D.unity</c> from code.
    /// Menu: <c>Simulation Lobby ▸ Build Scene ▸ Survival_WallVsBall_3D</c>.
    /// </summary>
    /// <remarks>
    /// Scene-as-code for the same reason <c>EscapeSceneBuilder</c> is — a video must stay
    /// re-renderable, and this format's determinism-in-3D claim is unproven until a real scene
    /// exercises it (see <c>KnowledgeBase/formats/survival-wall.md</c>).
    /// <para>Re-running replaces the scene, so it is safe to iterate on.</para>
    /// </remarks>
    static class WallSurvivalSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Survival_WallVsBall_3D.unity";
        const string ConfigPath = "Assets/Settings/Configs/WallSurvivalConfig_001.asset";

        /// <summary>
        /// Re-applies the tuning baseline to the existing config asset, discarding hand-tuning.
        /// Menu: <c>Simulation Lobby ▸ Reset Config ▸ Wall Survival (tuning baseline)</c>.
        /// </summary>
        /// <remarks>
        /// Needed because the scene builder deliberately never overwrites an existing config — it
        /// would throw away tuning every time the scene was rebuilt. That leaves no way to pick up a
        /// changed baseline short of deleting the asset, which also loses its GUID and unwires every
        /// scene pointing at it.
        /// </remarks>
        [MenuItem("Simulation Lobby/Reset Config/Wall Survival (tuning baseline)")]
        static void ResetConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<WallSurvivalConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogWarning($"[surv] No config at {ConfigPath} — build the scene first.");
                return;
            }

            ApplyDefaults(config);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[surv] Reset {ConfigPath} to the tuning baseline.", config);
            EditorGUIUtility.PingObject(config);
        }

        [MenuItem("Simulation Lobby/Build Scene/Survival_WallVsBall_3D")]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[surv] Exit play mode before building the scene — a new scene cannot " +
                               "replace the running one.");
                return;
            }

            WallSurvivalConfig config = EnsureConfig();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Spacing, not blockSize: the wall carries an air gap between blocks (see
            // WallSurvivalConfig.blockGapFraction), and framing it by bare block size crops the edges.
            float fieldWidth = config.columns * config.BlockSpacing;
            float fieldHeight = config.rows * config.BlockSpacing;

            // --- Field centre: origin, facing world +Z. Ball spawns in front along +Z and launches
            //     back toward -Z, hitting the field. Kept as its own transform (rather than baked
            //     into the ball's own spawn math) so a future video can rotate/move the whole field
            //     without touching WallSurvivalSimulation. ---
            var fieldCenter = new GameObject("FieldCenter");
            fieldCenter.transform.position = Vector3.zero;
            fieldCenter.transform.rotation = Quaternion.identity;

            var blocksParent = new GameObject("Blocks");
            blocksParent.transform.SetParent(fieldCenter.transform, false);

            // --- Camera: perspective, pulled back BEYOND the ball's own spawn point and angled across
            //    the field, so the ball's entire flight from spawn to wall sits inside the frustum.
            //    (Two earlier drafts got this wrong in opposite directions: first the camera sat behind
            //    the wall facing away from the ball; then it sat *between* the wall and the ball's
            //    spawn point (z = spawnDistance*0.55 < spawnDistance), which put the ball's spawn
            //    behind the camera's effective view — the ball would only become visible once it was
            //    already nearly at the wall, reading as "it starts right next to the blocks." Fixed by
            //    placing the camera past the spawn point instead of short of it.) ---
            // The rig is the authored framing; the camera is its child and carries only the push-in
            // and shake offsets, so re-running this builder never has to undo accumulated motion.
            var cameraRigObject = new GameObject("Camera Rig");

            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(cameraRigObject.transform, false);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.11f, 1f);

            float camLateral = Mathf.Max(fieldWidth, fieldHeight) * 0.9f;
            float camDepth = config.ballSpawnDistance + Mathf.Max(fieldWidth, fieldHeight) * 1.5f;
            Vector3 camPosition = new Vector3(camLateral, fieldHeight * 0.9f, camDepth);
            cameraObject.transform.localPosition = camPosition;
            // Look roughly a third of the way from the wall to the ball's spawn — keeps both endpoints
            // of the flight comfortably inside frame rather than centring on one and clipping the other.
            cameraObject.transform.LookAt(new Vector3(0f, fieldHeight * 0.15f, config.ballSpawnDistance * 0.35f));
            cameraObject.AddComponent<AudioListener>();

            ImpactCameraRig cameraRig = cameraRigObject.AddComponent<ImpactCameraRig>();
            // Push-in scaled to the shot: a fixed number would be a shove in a small scene and
            // invisible in a large one.
            cameraRig.pushInDistance = config.ballSpawnDistance * 0.35f;
            cameraRig.shakeAmplitude = config.blockSize * 0.9f;

            // The hand-placed angle above is only a starting point. Seen across from the right, the
            // wall's near (right-hand) end lands on the LEFT of a narrow 9:16 frame, and the
            // starting angle put it just outside it — then the push-in carried it further out as the
            // run went on. Solve the framing instead of guessing it, and before Bind, which records
            // the camera's rest position.
            FrameShot(camera, cameraRig.pushInDistance, fieldCenter.transform.position, new[]
            {
                new Vector3(-fieldWidth * 0.5f, -fieldHeight * 0.5f, 0f),
                new Vector3(-fieldWidth * 0.5f, fieldHeight * 0.5f, 0f),
                new Vector3(fieldWidth * 0.5f, -fieldHeight * 0.5f, 0f),
                new Vector3(fieldWidth * 0.5f, fieldHeight * 0.5f, 0f),
                new Vector3(0f, 0f, config.ballSpawnDistance) // the ball's launch point for a centre shot
            });
            camPosition = cameraObject.transform.localPosition;

            cameraRig.Bind(cameraObject.transform, fieldCenter.transform);

            // Slow-motion lives on the rig object too, so everything that stretches a moment on screen
            // is in one place in the hierarchy.
            SlowMotionDirector slowMotion = cameraRigObject.AddComponent<SlowMotionDirector>();
            slowMotion.slowScale = 0.28f;

            // --- Depth cue: flat fog reads as a void with nothing behind the field. A subtle fade to
            //    the background colour gives the frame a sense of scale a single directional light
            //    alone can't. ---
            float camDistanceToField = Vector3.Distance(camPosition, Vector3.zero);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = camDistanceToField * 0.9f;
            RenderSettings.fogEndDistance = camDistanceToField * 2.2f;

            // --- Lighting: key + a cooler fill/rim so the field doesn't read as flat-lit silhouettes
            //    against black — the single directional light the first draft used. ---
            var keyLightObject = new GameObject("Key Light");
            Light keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.3f;
            keyLight.shadows = LightShadows.Soft;
            keyLightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var fillLightObject = new GameObject("Fill Light");
            Light fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.35f;
            fillLight.color = new Color(0.6f, 0.72f, 1f, 1f);
            fillLightObject.transform.rotation = Quaternion.Euler(20f, 150f, 0f);

            // --- Floor: grounds the field with a shadow-catching surface instead of blocks floating
            //    in a void — the single biggest thing a bare-primitives-on-black scene is missing. ---
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            float floorScale = Mathf.Max(fieldWidth, config.ballSpawnDistance) * 0.35f;
            floor.transform.position = new Vector3(0f, -fieldHeight * 0.75f, 0f);
            floor.transform.localScale = new Vector3(floorScale, 1f, floorScale);
            floor.GetComponent<Renderer>().sharedMaterial =
                EnsureMaterial("Assets/Settings/Configs/WallSurvivalFloorMaterial.mat", new Color(0.09f, 0.1f, 0.15f, 1f));
            Object.DestroyImmediate(floor.GetComponent<MeshCollider>());

            // --- Ambient dust: gives the frame quiet motion in the hold beat before each hit — visual
            //    ASMR, the same "depth during the quiet stretches" job AmbientDriftField does for esc. ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(0f, fieldHeight * 0.3f, config.ballSpawnDistance * 0.3f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(fieldWidth * 1.6f, config.ballSpawnDistance * 1.3f);
            backdrop.particleCount = 70;
            backdrop.maxAlpha = 0.1f;
            backdrop.tint = new Color(0.6f, 0.7f, 1f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(EnsureDotTexture());

            // --- Ball: a fixed scene object WallSurvivalSimulation repositions/relaunches each run — its
            //    count never varies by config, unlike the block field, so there's nothing that needs
            //    spawning fresh (see WallSurvivalSimulation's remarks). ---
            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Ball";
            var ballRenderer = ballObject.GetComponent<Renderer>();
            Material ballMaterial = EnsureMaterial("Assets/Settings/Configs/WallSurvivalBallMaterial.mat",
                new Color(1f, 0.25f, 0.42f, 1f));
            ballRenderer.sharedMaterial = ballMaterial;

            Object.DestroyImmediate(ballObject.GetComponent<SphereCollider>());
            var ballCollider = ballObject.AddComponent<SphereCollider>();
            ballCollider.radius = 0.5f; // primitive sphere is 1 world unit across; matches blockSize scale
            ballCollider.sharedMaterial = EnsureBallPhysicsMaterial();

            var ballBody = ballObject.AddComponent<Rigidbody>();
            ballBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            ballBody.interpolation = RigidbodyInterpolation.Interpolate;
            ballBody.useGravity = false; // a stray gravity pull before launch would break reproducibility

            CollisionDetector3D ballDetector = ballObject.AddComponent<CollisionDetector3D>();

            // --- Runner + simulation + presenter ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            WallSurvivalSimulation simulation = runnerObject.AddComponent<WallSurvivalSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;

            Material blockMaterial = EnsureMaterial("Assets/Settings/Configs/WallSurvivalBlockMaterial.mat",
                new Color(0.55f, 0.72f, 0.95f, 1f));

            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("_ball").objectReferenceValue = ballBody;
            serialized.FindProperty("_ballDetector").objectReferenceValue = ballDetector;
            serialized.FindProperty("_fieldCenter").objectReferenceValue = fieldCenter.transform;
            serialized.FindProperty("_blocksParent").objectReferenceValue = blocksParent.transform;
            serialized.FindProperty("_blockMaterial").objectReferenceValue = blockMaterial;
            serialized.FindProperty("_ballMaterial").objectReferenceValue = ballMaterial;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // --- Audio: whoosh on launch, round-robin knocks on every ball/block collision. Both
            //    generated in code (ProceduralToneBank) — nothing to licence, same rule esc follows. ---
            var audioObject = new GameObject("Wall Survival Audio");
            ImpactAudioPlayer audioPlayer = audioObject.AddComponent<ImpactAudioPlayer>();
            // The riser is generated to this length, so matching it to the hold makes the sweep peak
            // exactly as the ball launches instead of stopping early or being cut off.
            audioPlayer.riserDuration = Mathf.Max(0.2f, config.preLaunchHoldSeconds);

            // --- HUD: the storyline layer. WALL vs BALL framed as a contest, with the guess as the
            //    question, a live hit tally (no total — the total is the answer) and a per-hit result
            //    line. Same shared canvas esc uses. ---
            VersusScoreboardHud hud = ScoreboardCanvasBuilder.Build(
                new ScoreboardCanvasBuilder.Labels
                {
                    title = "WALL vs BALL",
                    question = WallSurvivalPresenter.QuestionText(config.BlockCount),
                    leftName = "WALL",
                    rightName = "BALL",
                    round = "HITS  0",
                    counter = $"{config.BlockCount}  STANDING"
                },
                ScoreboardCanvasBuilder.Palette.Default);

            WallSurvivalPresenter presenter = runnerObject.AddComponent<WallSurvivalPresenter>();
            presenter.Bind(simulation, config, audioPlayer, hud, slowMotion, cameraRig);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  wall {config.columns}x{config.rows} = {config.BlockCount} blocks, " +
                $"{fieldWidth:0.0}x{fieldHeight:0.0} world units\n" +
                $"  hit 1 ball r={config.ballRadius:0.00} m={config.ballMass:0}, " +
                $"hit 10 r={config.RadiusForHit(9):0.00} m={config.MassForHit(9):0}, " +
                $"hit 20 r={config.RadiusForHit(19):0.00} m={config.MassForHit(19):0}\n" +
                $"  open-ended until the wall is gone; safety cap {config.maxHits} shots, worst case " +
                $"{config.WorstCaseDurationSeconds:0}s simulated before slow-motion\n" +
                "\n  Press Play to run seed 1. The run's hit count is the video's answer.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>Shorts frame — the narrowest aspect the scene renders at, so it bounds the framing.</summary>
        const float FramingAspect = 1080f / 1920f;

        /// <summary>Viewport margin kept clear on each side, so camera shake cannot push the wall out.</summary>
        const float FramingMargin = 0.07f;

        /// <summary>
        /// Turns the camera to centre <paramref name="points"/> horizontally, then pulls it straight back
        /// until they fit a 9:16 frame inside <see cref="FramingMargin"/>. Checked at FULL push-in — the
        /// tightest the shot ever gets, since <see cref="ImpactCameraRig"/> moves the camera toward
        /// <paramref name="pushTarget"/> without rotating it. Fitting the rest position alone would leave
        /// the wall cut off late in the run, which is exactly the bug this replaced.
        /// </summary>
        static void FrameShot(Camera camera, float pushInDistance, Vector3 pushTarget, Vector3[] points)
        {
            Transform t = camera.transform;
            camera.aspect = FramingAspect;

            float horizontalFov = 2f * Mathf.Atan(Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * FramingAspect) *
                                  Mathf.Rad2Deg;

            for (int i = 0; i < 60; i++)
            {
                Vector3 rest = t.position;
                t.position = rest + (pushTarget - rest).normalized * pushInDistance;

                float minX = float.MaxValue;
                float maxX = float.MinValue;
                foreach (Vector3 point in points)
                {
                    float x = camera.WorldToViewportPoint(point).x;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                }

                t.position = rest;

                float offset = (minX + maxX) * 0.5f - 0.5f;
                bool centred = Mathf.Abs(offset) < 0.005f;
                bool fits = maxX - minX <= 1f - 2f * FramingMargin;
                if (centred && fits)
                {
                    break;
                }

                // Positive offset = the points sit right of centre, so yaw right (positive about up).
                t.Rotate(Vector3.up, offset * horizontalFov, Space.World);

                if (!fits)
                {
                    t.position -= t.forward * (Vector3.Distance(rest, pushTarget) * 0.03f);
                }
            }

            camera.ResetAspect(); // back to following the Game view, so 16:9 renders use their own width
        }

        /// <summary>
        /// Loads the config asset, creating it at the tuning baseline if missing. Never overwrites an
        /// existing one — that would throw away hand-tuning on every rebuild; use the Reset Config menu
        /// to pick up a changed baseline.
        /// </summary>
        static WallSurvivalConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WallSurvivalConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var config = ScriptableObject.CreateInstance<WallSurvivalConfig>();
            ApplyDefaults(config);
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        /// <summary>Tuning baseline from the plan (plans/videos/surv-001-wall-vs-ball.md).</summary>
        static void ApplyDefaults(WallSurvivalConfig config)
        {
            config.fixedTimestep = 1f / 60f;
            config.maxDurationSeconds = 180f;
            config.columns = 25;
            config.rows = 12;
            config.blockSize = 0.4f;
            config.blockMass = 1f;
            config.blockGapFraction = 0.1f;
            config.positionJitterFraction = 0.05f;
            config.maxDepenetrationVelocity = 1.5f;
            config.blockLinearDamping = 1.5f;
            config.maxHits = 25;
            config.openingHoldSeconds = 3f;
            config.preLaunchHoldSeconds = 1f;
            config.maxHitFlightSeconds = 5f;
            config.hitResultHoldSeconds = 0.9f;
            config.finalHoldSeconds = 4f;
            config.eliminationsPerTick = 1.5f;
            config.lastStandBlocks = 12;
            config.radiusGrowthPerHit = 1.07f;
            config.speedGrowthPerHit = 1.03f;
            config.massGrowthPerHit = 1.25f;
            config.ballRadius = 0.405f;
            config.ballMass = 8f;
            config.ballSpeedMin = 11f;
            config.ballSpeedMax = 14f;
            config.ballSpawnDistance = 12f;
            config.aimJitterBallRadii = 0.5f;
            config.unseededAim = false;
            config.settleVelocityThreshold = 0.05f;
            config.settleTicksRequired = 15;
            config.eliminationDistanceBlocks = 0.2f;
            config.eliminationTiltDegrees = 25f;
            config.minNeighboursToStand = 1;
            config.slowMotionOnEveryHit = true;
            config.slowMotionSeconds = 0.9f;
            config.lastStandSlowMotionBonus = 0.8f;
            config.wipeoutSlowMotionSeconds = 2.2f;
            config.intent = "surv-001. 300 blocks, one wall that is never rebuilt, hit by an escalating " +
                            "ball until nothing stands. Hook: guess how many hits. Target 90-170s; " +
                            "reject takes that hit the shot cap.";
        }

        static PhysicsMaterial EnsureBallPhysicsMaterial()
        {
            const string path = "Assets/Settings/Configs/WallSurvivalBall.physicsMaterial";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var material = new PhysicsMaterial("WallSurvivalBall")
            {
                bounciness = 0.2f,
                dynamicFriction = 0.3f,
                staticFriction = 0.3f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Soft round texture for the ambient dust motes. Generated independently of any other
        /// format's art (rather than reusing esc's <c>Circle_256.png</c>) so this scene doesn't
        /// implicitly require esc's been built first — formats never depend on each other.
        /// </summary>
        static Texture2D EnsureDotTexture()
        {
            const string path = "Assets/Art/Generated/WallSurvivalDot_64.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Art/Generated");
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    float edge = Mathf.InverseLerp(radius, radius - 1.5f, distance);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(edge) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material EnsureMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
