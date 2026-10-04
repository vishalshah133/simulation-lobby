using System.IO;
using System.Text;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.Breaker.EditorTools
{
    /// <summary>
    /// Builds the <c>brk</c> scene from code. Menu: <c>Simulation Lobby ▸ Build Scene ▸ Breaker_Rings_2D_Song</c>.
    /// Ring geometry comes from the config, so after changing ring count/size/segments, rebuild.
    /// </summary>
    static class BreakerSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Breaker_Rings_2D_Song.unity";
        const string ConfigPath = "Assets/Settings/Configs/BreakerConfig_001.asset";
        const string MelodyPath = "Assets/Settings/Melodies/FurElise.asset";
        const string BouncyPath = "Assets/Settings/Configs/PerfectlyElastic.physicsMaterial2D";

        // Same 9:16 framing as esc: half-height 9.6 gives a 10.8-wide frame, so rings up to ~5 units
        // of radius fit with a margin while the top and bottom HUD panels stay clear.
        const float OrthographicSize = 9.6f;

        /// <summary>Innermost first. Saturated and far apart in hue so each ring reads on its own at 480px.</summary>
        static readonly Color[] RingPalette =
        {
            new Color(0.25f, 0.88f, 1f),    // cyan
            new Color(0.55f, 1f, 0.29f),    // lime
            new Color(1f, 0.82f, 0.24f),    // amber
            new Color(1f, 0.5f, 0.24f),     // orange
            new Color(1f, 0.3f, 0.6f),      // pink
            new Color(0.69f, 0.45f, 1f),    // violet
            new Color(0.3f, 0.6f, 1f),      // blue
        };

        [MenuItem("Simulation Lobby/Build Scene/Breaker_Rings_2D_Song")]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[brk] Exit play mode before building the scene.");
                return;
            }

            BreakerConfig config = EnsureConfig();
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[brk] Config '{config.name}' is invalid: {error}. Scene not built.", config);
                return;
            }

            var song = AssetDatabase.LoadAssetAtPath<MelodySequence>(MelodyPath);
            if (song == null)
            {
                Debug.LogError($"[brk] Melody not found at {MelodyPath}. Scene not built.");
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

            // --- Rings ---
            // One material for every segment: Sprites/Default multiplies vertex colour (the ring's
            // colour, baked into the mesh) by _Color (the presenter's per-segment tint).
            var segmentMaterial = new Material(Shader.Find("Sprites/Default")) { name = "BreakerSegment" };
            PhysicsMaterial2D bouncy = EnsureBouncyMaterial();

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

                // Kinematic body so spinning the ring moves one body instead of re-creating every
                // static collider each step: measured 11 s/seed in a scan without it.
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

            // --- Debris ---
            var shatterObject = new GameObject("Shatter VFX");
            ShatterBurst2D shatter = shatterObject.AddComponent<ShatterBurst2D>();
            // One debris shape per ring, innermost first: triangle shards, slivers, curved ring
            // pieces, diamonds, and sparkle stars for the finale.
            shatter.Apply(
                new Texture[] { GeneratedArt.Shard(), GeneratedArt.Sliver(), GeneratedArt.ArcChunk(), GeneratedArt.Diamond(), GeneratedArt.Star() },
                GeneratedArt.SoftDot(), GeneratedArt.Ring(), 6);

            // --- Runner + simulation ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            BreakerSimulation simulation = runnerObject.AddComponent<BreakerSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;

            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("_ball").objectReferenceValue = body;
            serialized.FindProperty("_ballCollider").objectReferenceValue = ballCollider;
            serialized.FindProperty("_bounceDetector").objectReferenceValue = detector;
            SerializedProperty ringsProperty = serialized.FindProperty("_rings");
            ringsProperty.arraySize = rings.Length;
            for (int i = 0; i < rings.Length; i++)
            {
                ringsProperty.GetArrayElementAtIndex(i).objectReferenceValue = rings[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            // --- HUD ---
            string question = config.HasClock
                ? $"BREAK ALL {config.ringCount} RINGS IN {config.timeLimitSeconds:0} SECONDS?"
                : $"CAN IT BREAK ALL {config.ringCount} RINGS?";
            int clock = Mathf.CeilToInt(config.timeLimitSeconds);
            VersusScoreboardHud hud = ScoreboardCanvasBuilder.Build(
                new ScoreboardCanvasBuilder.Labels
                {
                    title = "GUESS THE SONG",
                    question = question,
                    leftName = "RINGS",
                    rightName = "BALL",
                    round = $"RING 1 / {config.ringCount}",
                    counter = config.HasClock ? $"{clock / 60}:{clock % 60:00}" : "0  HITS"
                },
                new ScoreboardCanvasBuilder.Palette
                {
                    leftAccent = RingPalette[0],
                    rightAccent = Color.white
                });

            // --- Audio: one note of the song per touch ---
            var audioObject = new GameObject("Bounce Audio");
            BounceMelodyPlayer melody = audioObject.AddComponent<BounceMelodyPlayer>();
            melody.rootHz = 220f;
            melody.noteDuration = 1.1f;
            melody.voiceCount = 10;
            melody.volume = 0.55f;
            melody.melody = song;

            BreakerPresenter presenter = runnerObject.AddComponent<BreakerPresenter>();
            presenter.Bind(simulation, hud, melody, shatter, camera, "GUESS THE SONG", question);
            runnerObject.AddComponent<SongReveal>().Bind(simulation, hud, melody);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            var log = new StringBuilder();
            log.AppendLine($"Built {ScenePath} ({config.ringCount} rings, song {song.songTitle}, {song.Count} notes)");
            for (int i = 0; i < config.ringCount; i++)
            {
                log.AppendLine($"  ring {i + 1}: r {config.RingInnerRadius(i):0.00}-{config.RingOuterRadius(i):0.00}, " +
                               $"{config.SegmentsInRing(i)} segments x {config.HitPointsForRing(i)} hp, " +
                               $"hole {config.HoleChord(i):0.00} for ball {config.ballRadius * 2f:0.00}, " +
                               $"speed {config.SpeedForRing(i):0.0}");
            }

            log.Append($"  top speed {config.MaxSpeed:0.0} vs safe {ConstantSpeed2D.MaxSafeSpeed(config.fixedTimestep, config.ringThickness):0.0}. " +
                       "Press Play to run seed 1, or scan (Simulation Lobby ▸ Seed Scan).");
            Debug.Log(log.ToString());
        }

        static BreakerConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BreakerConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var config = ScriptableObject.CreateInstance<BreakerConfig>();
            config.fixedTimestep = 0.01f;
            config.maxDurationSeconds = 150f;
            // Balanced from two real runs (2026-10-04): 1->3 touches at 4.5 u/s took ~122s; 1 touch at
            // 5 u/s broke free in 20-30s with two rings gone in 5s. A second touch makes a ring take
            // 5-10x longer, so: 2 touches everywhere, fewer extra segments outward, a faster ball.
            // Target ~55s median (rings ~7/8/10/15/14s): a little more wins than losses against 60s.
            config.timeLimitSeconds = 60f;
            // Then 15% slower on the user's ear (the song sounded rushed), with 2 fewer segments per
            // ring so the run doesn't stretch past the clock.
            config.innerHitPoints = 2;
            config.outerHitPoints = 2;
            config.innerSegments = 14;
            config.extraSegmentsPerRing = 1;
            config.startSpeed = 6.8f;
            config.speedPerRing = 0.4f;
            config.intent = "brk-001: 5 rings of 14-18 segments, 2 touches each, spinning 18deg/s alternating; " +
                            "ball 6.8 u/s +0.4 per ring; 60s clock. Fur Elise A section, one note per touch.";
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
