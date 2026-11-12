using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.MultiplyEscape.EditorTools
{
    /// <summary>
    /// Builds <c>Comp001_10_FillOrEscape_Wide</c>: comp-001 finale, 16:9, one long round of mve.
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Multiply vs Escape (mve) ▸ Wide: Fill or Escape (comp-001)</c>.
    /// Re-running replaces the scene; an existing config asset keeps your tuning.
    /// </summary>
    static class MultiplyEscapeWideSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Comp001_10_FillOrEscape_Wide.unity";
        const string ConfigPath = "Assets/Settings/Configs/MultiplyEscapeConfig_comp001.asset";
        const string CardPath = "Assets/Settings/SegmentCards/comp001_10.asset";
        const string MaterialPath = "Assets/Settings/Configs/PerfectlyElastic.physicsMaterial2D";

        const float ArenaRadius = 4.2f;
        const AspectMode Mode = AspectMode.Wide;

        // Finale palette: near-black frame, steel wall, cyan = inside, amber = out.
        static readonly Color Background = new Color(0.012f, 0.014f, 0.024f, 1f);
        static readonly Color WallColor = new Color(0.42f, 0.48f, 0.6f, 1f);
        static readonly Color InsideColor = new Color(0.2f, 0.85f, 1f, 1f);

        [MenuItem("Simulation Lobby/2D Formats/Multiply vs Escape (mve)/Wide: Fill or Escape (comp-001)", false, 122)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[mve] Exit play mode before building the scene.");
                return;
            }

            Sprite circle = GeneratedArt.Circle();
            Sprite square = GeneratedArt.Square();
            MultiplyEscapeConfig config = EnsureConfig();

            SegmentCardConfig cardConfig = SegmentCardFactory.CreateConfigAsset(
                CardPath, 1, 10, "Fill or Escape", "FILL THE RING WITH 300, OR ALL ESCAPE?",
                "Most wall bounces clone the ball. Every ball that escapes widens the exit. Fill the ring to 300, or watch it empty out?");
            cardConfig.accent = InsideColor;
            EditorUtility.SetDirty(cardConfig);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera: arena (wall included) centred left of the right margin ---
            float outer = ArenaRadius + Mathf.Max(0.6f, config.ballSpeed * 4f * config.fixedTimestep / 0.5f);
            float ortho = WideFrameLayout.OrthoSizeForArena(outer * 2f, outer * 2f, Mode, 1.05f);
            float offsetX = WideFrameLayout.ArenaCameraOffsetX(ortho, Mode);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(offsetX, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = ortho;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient backdrop (fixed palette: no theme styler, the finale keeps one look) ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(offsetX, 0f, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(ortho * 2f * WideFrameLayout.Aspect(Mode), ortho * 2f);
            backdrop.particleCount = 70;
            backdrop.maxAlpha = 0.08f;
            backdrop.tint = new Color(0.45f, 0.55f, 0.8f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- Wall ---
            var boundaryObject = new GameObject("Boundary");
            CircularBoundary2D boundary = boundaryObject.AddComponent<CircularBoundary2D>();
            boundary.radius = ArenaRadius;
            boundary.thickness = Mathf.Max(0.6f, config.ballSpeed * 4f * config.fixedTimestep / 0.5f);
            boundary.segmentCount = 180;
            boundary.keepGapSegments = true;
            boundary.gapDegrees = config.startGapDegrees;
            boundary.gapCenterDegrees = config.gapCenterDegrees;
            boundary.wallSprite = square;
            boundary.wallColor = WallColor;
            boundary.wallSortingOrder = 0;
            boundary.Rebuild();

            // --- Ball template ---
            var balls = new GameObject("Balls");
            var template = new GameObject("Ball");
            template.transform.SetParent(balls.transform, false);

            var body = template.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var collider = template.AddComponent<CircleCollider2D>();
            collider.radius = config.ballRadius;
            collider.sharedMaterial = EnsureBouncyMaterial();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(template.transform, false);
            visual.transform.localScale = Vector3.one * config.ballRadius * 2f;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = circle;
            renderer.color = InsideColor;
            renderer.sortingOrder = 5;

            SwarmBall swarmBall = template.AddComponent<SwarmBall>();
            swarmBall.Bind(renderer);
            template.SetActive(false);

            // --- Runner + simulation ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            MultiplyEscapeSimulation simulation = runnerObject.AddComponent<MultiplyEscapeSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("_ballTemplate").objectReferenceValue = template;
            serialized.FindProperty("_ballParent").objectReferenceValue = balls.transform;
            serialized.FindProperty("_boundary").objectReferenceValue = boundary;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // --- HUD: card + hook + IN counter (right) + caption; OUT panel (left) from the binder ---
            SegmentCardPresenter card = SegmentCardFactory.CreateInScene(cardConfig, Mode);

            // --- Audio: two different voices. Clone = bright rising scale, escape = low rising scale. ---
            BounceMelodyPlayer cloneAudio = BuildMelody("Clone Audio", 392f, 0.3f, 0.35f);
            BounceMelodyPlayer escapeAudio = BuildMelody("Escape Audio", 130f, 0.7f, 0.5f);

            MultiplyEscapeWidePresenter presenter = runnerObject.AddComponent<MultiplyEscapeWidePresenter>();
            presenter.Bind(simulation, card, cloneAudio, escapeAudio);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            float safeSpeed = ConstantSpeed2D.MaxSafeSpeed(config.fixedTimestep, boundary.thickness);
            float x0 = config.startGapDegrees / 360f;
            float c = config.cloneChance;
            float knifeEdgePeak = 1f + 360f * (c * Mathf.Log(c / x0) - c + x0) / config.gapDegreesPerEscape;
            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  16:9, ortho {ortho:0.00}, camera x +{offsetX:0.00}, arena radius {ArenaRadius}\n" +
                $"  1 round, FULL at {config.fillTarget}, clone chance {config.cloneChance}, exit {config.startGapDegrees}° +{config.gapDegreesPerEscape}° per escape\n" +
                $"  model peak (swarm size when the exit catches up) = {knifeEdgePeak:0}; crowding favours EMPTY, see KnowledgeBase/formats/multiply-vs-escape.md\n" +
                $"  speed {config.ballSpeed} (safe ceiling {safeSpeed:0.0}); length estimated ~45s, MEASURE IT on first run.\n" +
                "  Seed is random per Play and logged at start.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        static BounceMelodyPlayer BuildMelody(string name, float rootHz, float noteDuration, float volume)
        {
            var audioObject = new GameObject(name);
            BounceMelodyPlayer melody = audioObject.AddComponent<BounceMelodyPlayer>();
            melody.rootHz = Mathf.Clamp(rootHz, 110f, 523f);
            melody.noteCount = 16;
            melody.noteDuration = noteDuration;
            melody.voiceCount = 10;
            melody.volume = volume;
            return melody;
        }

        static MultiplyEscapeConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MultiplyEscapeConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var config = ScriptableObject.CreateInstance<MultiplyEscapeConfig>();
            config.fixedTimestep = 0.01f;

            config.ballRadius = 0.14f;
            config.ballSpeed = 5f;
            config.cloneTurnDegrees = new Vector2(20f, 70f); // wider than mve-001: clones fan out instead of clumping
            config.fillTarget = 300;
            // 10-28: at cloneChance 1 the swarm gained a ball on EVERY bounce and could never lose (net growth
            // 1 - p per bounce, always positive), so FULL was certain. With a clone chance c, growth stalls once the
            // exit's share p of the wall passes c, and the peak is 1 + (c*ln(c/p0) - c + p0) / (delta/360),
            // independent of bounce rate. A Python event model (c .7, gap 24, delta .8..1.0) gave FULL 20-75%
            // across +-3 deg of effective-gap error, ~30% near-misses (peak 240+), 18-30 s. Tune delta only.
            config.cloneChance = 0.6f;
            config.startGapDegrees = 24f;
            config.gapDegreesPerEscape = 1.5f;
            config.gapAngularSpeed = 25f;
            config.gapCenterDegrees = 90f;

            config.roundCount = 1;
            config.maxRoundSeconds = 38f;
            config.preLaunchHoldSeconds = 2f;   // covers the 1.5s segment card
            config.interRoundSeconds = 4.5f;    // result caption, then the one-line why
            config.recycleDistance = 5.3f;      // escaped balls vanish before the margins/bars (wide frame)

            config.intent = "comp-001 #1. One round: FILL 300 or ESCAPE. 60% of bounces clone, start gap 24deg, +1.5deg per escape. " +
                            "Calibrated from run logs. Tune gapDegreesPerEscape only: FULL every time -> raise; " +
                            "EMPTY with peak under ~200 -> lower.";
            config.maxDurationSeconds = config.SeriesSeconds;
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static PhysicsMaterial2D EnsureBouncyMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var material = new PhysicsMaterial2D("PerfectlyElastic") { bounciness = 1f, friction = 0f };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
