using System.IO;
using System.Text;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.ReachCore.EditorTools
{
    /// <summary>
    /// Builds the <c>core</c> scene from code. Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Reach the Core (core) ▸ Reach the Core (core-001)</c>.
    /// Portrait 9:16 at 1080x1920: set the Game view to that resolution before judging framing.
    /// Ring geometry comes from the config, so after changing ring count/size/segments, rebuild.
    /// </summary>
    static class CoreSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/ReachCore_Rings_2D.unity";
        const string ConfigPath = "Assets/Settings/Configs/CoreConfig_001.asset";
        const string MelodyPath = "Assets/Settings/Melodies/OdeToJoy.asset";
        const string BouncyPath = "Assets/Settings/Configs/PerfectlyElastic.physicsMaterial2D";

        // Same 9:16 framing as brk: half-height 9.6 gives a 10.8-wide frame, so a wall at ~5.2 fits
        // with a margin while the top and bottom HUD panels stay clear.
        const float OrthographicSize = 9.6f;

        /// <summary>World radius of the ring sprite's bright band at scale 1 (the band sits at 0.9 of the half-width).</summary>
        const float WallSpriteRadius = 0.45f;

        /// <summary>Outermost first. Cool outside, hot inside: the colour walks toward the core.</summary>
        static readonly Color[] RingPalette =
        {
            new Color(0.25f, 0.88f, 1f),    // cyan
            new Color(0.55f, 1f, 0.29f),    // lime
            new Color(1f, 0.82f, 0.24f),    // amber
            new Color(1f, 0.5f, 0.24f),     // orange
            new Color(1f, 0.3f, 0.6f),      // pink
            new Color(0.69f, 0.45f, 1f),    // violet
            new Color(0.3f, 0.6f, 1f),      // blue
            new Color(0.9f, 0.9f, 0.9f),    // white
        };

        static readonly Color CoreGold = new Color(1f, 0.84f, 0.3f, 1f);

        [MenuItem("Simulation Lobby/2D Formats/Reach the Core (core)/Reach the Core (core-001)", false, 205)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[core] Exit play mode before building the scene.");
                return;
            }

            CoreConfig config = EnsureConfig();
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[core] Config '{config.name}' is invalid: {error}. Scene not built.", config);
                return;
            }

            var song = AssetDatabase.LoadAssetAtPath<MelodySequence>(MelodyPath);
            if (song == null)
            {
                Debug.LogError($"[core] Melody not found at {MelodyPath}. Scene not built.");
                return;
            }

            Sprite circle = GeneratedArt.Circle();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.075f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient backdrop ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(0f, 0f, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(OrthographicSize * 1.4f, OrthographicSize * 2.2f);
            backdrop.particleCount = 80;
            backdrop.maxAlpha = 0.1f;
            backdrop.tint = new Color(0.6f, 0.7f, 1f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            PhysicsMaterial2D bouncy = EnsureBouncyMaterial();

            // --- Core: the prize. A soft halo behind a solid disc ---
            var coreRoot = new GameObject("Core");
            var haloObject = new GameObject("Core Halo");
            haloObject.transform.SetParent(coreRoot.transform, false);
            SpriteRenderer halo = haloObject.AddComponent<SpriteRenderer>();
            halo.sprite = GeneratedArt.SoftGlow();
            halo.color = new Color(CoreGold.r, CoreGold.g, CoreGold.b, 0.2f);
            halo.sortingOrder = -2;
            haloObject.transform.localScale = Vector3.one * config.coreRadius * 4.4f;

            var bodyObject = new GameObject("Core Body");
            bodyObject.transform.SetParent(coreRoot.transform, false);
            SpriteRenderer coreBody = bodyObject.AddComponent<SpriteRenderer>();
            coreBody.sprite = circle;
            coreBody.color = CoreGold;
            coreBody.sortingOrder = -1;
            bodyObject.transform.localScale = Vector3.one * config.coreRadius * 2f;

            // --- Containment wall: the circle that closes in on every ring clear ---
            var wallObject = new GameObject("Containment Wall");
            var wall = wallObject.AddComponent<EdgeCollider2D>();
            wall.sharedMaterial = bouncy;

            var wallVisualObject = new GameObject("Wall Visual");
            wallVisualObject.transform.SetParent(wallObject.transform, false);
            SpriteRenderer wallVisual = wallVisualObject.AddComponent<SpriteRenderer>();
            wallVisual.sprite = GeneratedArt.RingSprite();
            wallVisual.color = new Color(0.85f, 0.92f, 1f, 0.55f);
            wallVisual.sortingOrder = -3;
            float wallScale = config.WallRadius(0) / WallSpriteRadius;
            wallVisualObject.transform.localScale = new Vector3(wallScale, wallScale, 1f);

            // --- Rings, outermost first. One material for every segment: Sprites/Default multiplies
            // vertex colour (the ring's colour, baked into the mesh) by _Color (the presenter's tint). ---
            var segmentMaterial = new Material(Shader.Find("Sprites/Default")) { name = "CoreSegment" };

            var ringsRoot = new GameObject("Rings");
            var rings = new SegmentedRing2D[config.ringCount];
            for (int i = 0; i < config.ringCount; i++)
            {
                var ringObject = new GameObject($"Ring_{i + 1}");
                ringObject.transform.SetParent(ringsRoot.transform, false);
                SegmentedRing2D ring = ringObject.AddComponent<SegmentedRing2D>();
                ring.innerRadius = config.RingInnerRadius(i);
                ring.thickness = config.ringThickness;
                ring.segmentCount = config.SegmentsInRing(i);
                ring.arcSteps = 5;
                ring.visualGapFraction = 0.1f;
                ring.color = RingPalette[i % RingPalette.Length];
                ring.visualMaterial = segmentMaterial;
                ring.sortingOrder = 0;
                ring.Rebuild();

                // Kinematic body so spinning the ring moves one body instead of re-creating every static collider.
                var ringBody = ringObject.AddComponent<Rigidbody2D>();
                ringBody.bodyType = RigidbodyType2D.Kinematic;
                ringBody.interpolation = RigidbodyInterpolation2D.None;

                for (int s = 0; s < ring.SegmentCount; s++)
                {
                    ring.GetCollider(s).sharedMaterial = bouncy;
                }

                rings[i] = ring;
            }

            // --- Ball: white, so it reads against every ring colour ---
            var ballObject = new GameObject("Ball");
            var ballRenderer = ballObject.AddComponent<SpriteRenderer>();
            ballRenderer.sprite = circle;
            ballRenderer.color = Color.white;
            ballRenderer.sortingOrder = 10;

            var body = ballObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var ballCollider = ballObject.AddComponent<CircleCollider2D>();
            ballCollider.radius = 0.5f;
            ballCollider.sharedMaterial = bouncy;
            var detector = ballObject.AddComponent<BounceDetector2D>();
            ballObject.transform.localScale = Vector3.one * config.ballRadius * 2f;

            var trail = ballObject.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.widthMultiplier = config.ballRadius * 1.6f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.minVertexDistance = 0.02f;
            trail.sortingOrder = 9;
            var trailGradient = new Gradient();
            trailGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.85f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = trailGradient;
            trail.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "BallTrail" };

            // --- Debris: one shape per ring, outermost first (index 4 doubles as the core burst) ---
            var shatterObject = new GameObject("Shatter VFX");
            ShatterBurst2D shatter = shatterObject.AddComponent<ShatterBurst2D>();
            shatter.Apply(
                new Texture[] { GeneratedArt.Shard(), GeneratedArt.Sliver(), GeneratedArt.ArcChunk(), GeneratedArt.Diamond(), GeneratedArt.Star() },
                GeneratedArt.SoftDot(), GeneratedArt.Ring(), 6);

            // --- Runner + simulation ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            CoreSimulation simulation = runnerObject.AddComponent<CoreSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;

            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("_ball").objectReferenceValue = body;
            serialized.FindProperty("_ballCollider").objectReferenceValue = ballCollider;
            serialized.FindProperty("_bounceDetector").objectReferenceValue = detector;
            serialized.FindProperty("_wall").objectReferenceValue = wall;
            SerializedProperty ringsProperty = serialized.FindProperty("_rings");
            ringsProperty.arraySize = rings.Length;
            for (int i = 0; i < rings.Length; i++)
            {
                ringsProperty.GetArrayElementAtIndex(i).objectReferenceValue = rings[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            // --- HUD. The score row is hidden by the presenter: the only numbers are rings left and the clock. ---
            const string question = "CAN IT BREAK IN TO THE CORE?";
            int clock = Mathf.CeilToInt(config.timeLimitSeconds);
            VersusScoreboardHud hud = ScoreboardCanvasBuilder.Build(
                new ScoreboardCanvasBuilder.Labels
                {
                    title = "GUESS THE SONG",
                    question = question,
                    leftName = "RINGS",
                    rightName = "BALL",
                    round = $"{config.ringCount} RINGS LEFT",
                    counter = config.HasClock ? $"{clock / 60}:{clock % 60:00}" : "0  HITS"
                },
                new ScoreboardCanvasBuilder.Palette
                {
                    leftAccent = RingPalette[0],
                    rightAccent = CoreGold
                });

            // --- Audio: one note of the song per touch ---
            var audioObject = new GameObject("Bounce Audio");
            BounceMelodyPlayer melody = audioObject.AddComponent<BounceMelodyPlayer>();
            melody.rootHz = 220f;
            melody.noteDuration = 1.1f;
            melody.voiceCount = 10;
            melody.volume = 0.55f;
            melody.melody = song;

            CorePresenter presenter = runnerObject.AddComponent<CorePresenter>();
            presenter.Bind(simulation, hud, melody, shatter, camera, coreBody, halo, wallVisual, WallSpriteRadius,
                "GUESS THE SONG", question);
            runnerObject.AddComponent<SongReveal>().Bind(simulation, hud, melody);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            var log = new StringBuilder();
            log.AppendLine($"Built {ScenePath} ({config.ringCount} rings, song {song.songTitle}, {song.Count} notes). " +
                           "Set the Game view to 1080x1920 (9:16) before judging framing.");
            for (int i = 0; i < config.ringCount; i++)
            {
                log.AppendLine($"  ring {i + 1}: r {config.RingInnerRadius(i):0.00}-{config.RingOuterRadius(i):0.00}, " +
                               $"{config.SegmentsInRing(i)} segments x {config.HitPointsForRing(i)} hp, " +
                               $"hole {config.HoleChord(i):0.00} for ball {config.ballRadius * 2f:0.00}, " +
                               $"speed {config.SpeedForRing(i):0.0}, wall {config.WallRadius(i):0.00}");
            }

            log.Append($"  core r {config.coreRadius:0.00}, last chamber r {config.FinalWallRadius:0.00}, " +
                       $"top speed {config.MaxSpeed:0.0} vs safe {ConstantSpeed2D.MaxSafeSpeed(config.fixedTimestep, config.ringThickness):0.0}. " +
                       "Press Play (seed is logged), or scan (Simulation Lobby ▸ Tools ▸ Seed Scan).");
            Debug.Log(log.ToString());
        }

        static CoreConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CoreConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var config = ScriptableObject.CreateInstance<CoreConfig>();
            config.fixedTimestep = 0.01f;
            config.maxDurationSeconds = 150f;
            // Portrait Short: 60-120 s end to end. A 100 s clock caps it near 105 s; toughness (1 touch on the
            // outside, 4 on the inside) is what keeps most wins past ~60 s. Pacing is UNMEASURED: tune from Plays.
            config.timeLimitSeconds = 100f;
            config.outerHitPoints = 1;
            config.innerHitPoints = 6;
            config.startSpeed = 3.6f;
            config.speedPerRing = 0.2f;
            config.intent = "core-001: 5 rings of 20-12 segments, 1 touch outside to 6 inside, spinning 18deg/s alternating; " +
                            "ball 3.6 u/s +0.2 per ring; 100s clock; the arena wall closes in on each ring clear. " +
                            "Ode to Joy, one note per touch. Portrait 9:16.";
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static PhysicsMaterial2D EnsureBouncyMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(BouncyPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var material = new PhysicsMaterial2D("PerfectlyElastic") { bounciness = 1f, friction = 0f };
            AssetDatabase.CreateAsset(material, BouncyPath);
            return material;
        }
    }
}
