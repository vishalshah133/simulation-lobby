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
    /// Builds <c>MultiplyEscape_Circle_2D</c> (mve-001) from code.
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Multiply vs Escape (mve) ▸ Circle</c>. Re-running replaces the scene.
    /// </summary>
    static class MultiplyEscapeSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/MultiplyEscape_Circle_2D.unity";
        const string ConfigPath = "Assets/Settings/Configs/MultiplyEscapeConfig_001.asset";
        const string MaterialPath = "Assets/Settings/Configs/PerfectlyElastic.physicsMaterial2D";

        // Same framing as esc, so the arena reads as the rotating-gap circle viewers already know.
        const float ArenaRadius = 4.2f;
        const float OrthographicSize = 9.6f;

        [MenuItem("Simulation Lobby/2D Formats/Multiply vs Escape (mve)/Circle", false, 121)]
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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.09f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient backdrop ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(0f, 0f, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(OrthographicSize * 1.4f, OrthographicSize * 2.2f);
            backdrop.particleCount = 90;
            backdrop.maxAlpha = 0.13f;
            backdrop.tint = new Color(0.55f, 0.65f, 1f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- Wall: every segment built, the gap's switched off, so the exit can widen per escape ---
            var boundaryObject = new GameObject("Boundary");
            CircularBoundary2D boundary = boundaryObject.AddComponent<CircularBoundary2D>();
            boundary.radius = ArenaRadius;
            float thicknessForSpeed = config.ballSpeed * 4f * config.fixedTimestep / 0.5f;
            boundary.thickness = Mathf.Max(0.6f, thicknessForSpeed);
            // 2° per segment, so a +1.1° step opens the wall visibly every escape or two.
            boundary.segmentCount = 180;
            boundary.keepGapSegments = true;
            boundary.gapDegrees = config.startGapDegrees;
            boundary.gapCenterDegrees = config.gapCenterDegrees;
            boundary.wallSprite = square;
            boundary.wallColor = new Color(0.62f, 0.66f, 0.78f, 1f);
            boundary.wallSortingOrder = 0;
            boundary.Rebuild();

            // --- Ball template: inactive, cloned by the pool ---
            var balls = new GameObject("Balls");
            var template = new GameObject("Ball");
            template.transform.SetParent(balls.transform, false);

            var body = template.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.freezeRotation = true;
            // Discrete is enough at 5 u/s and a 0.6 wall (0.05 per step); continuous on 600 bodies costs
            // frame rate for no gain.
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
            renderer.color = new Color(1f, 0.3f, 0.45f, 1f);
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
            BackdropThemeLibrary.AddStyler(backdropObject, runner, camera, backdrop);

            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("_ballTemplate").objectReferenceValue = template;
            serialized.FindProperty("_ballParent").objectReferenceValue = balls.transform;
            serialized.FindProperty("_boundary").objectReferenceValue = boundary;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            VersusScoreboardHud hud = ScoreboardCanvasBuilder.Build(
                new ScoreboardCanvasBuilder.Labels
                {
                    title = "MULTIPLY VS ESCAPE",
                    question = "WILL IT FILL UP?",
                    leftName = "ESCAPE",
                    rightName = "MULTIPLY",
                    round = $"ROUND 1 / {config.roundCount}",
                    counter = $"INSIDE  1 / {config.fillTarget}"
                },
                new ScoreboardCanvasBuilder.Palette
                {
                    leftAccent = new Color(1f, 0.85f, 0.2f, 1f),
                    rightAccent = new Color(0.35f, 0.85f, 1f, 1f)
                });

            // --- Audio: a bright climbing note per clone, a low one per escape ---
            BounceMelodyPlayer cloneAudio = BuildMelody("Clone Audio", 330f, 0.35f, 0.4f);
            BounceMelodyPlayer escapeAudio = BuildMelody("Escape Audio", 110f, 0.6f, 0.5f);

            MultiplyEscapePresenter presenter = runnerObject.AddComponent<MultiplyEscapePresenter>();
            presenter.Bind(simulation, hud, cloneAudio, escapeAudio);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            float safeSpeed = ConstantSpeed2D.MaxSafeSpeed(config.fixedTimestep, boundary.thickness);
            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  arena radius {ArenaRadius}, wall {boundary.thickness:0.00} thick, {boundary.segmentCount} segments\n" +
                $"  exit {config.startGapDegrees}° (+{config.gapDegreesPerEscape}° per escape), spin {config.gapAngularSpeed}°/s\n" +
                $"  {config.roundCount} rounds, FULL at {config.fillTarget}, pool {config.BodyBudget} bodies\n" +
                $"  speed {config.ballSpeed} (safe ceiling {safeSpeed:0.0})\n" +
                "  Seed is random per Play and logged at start.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        static BounceMelodyPlayer BuildMelody(string name, float rootHz, float noteDuration, float volume)
        {
            var audioObject = new GameObject(name);
            BounceMelodyPlayer melody = audioObject.AddComponent<BounceMelodyPlayer>();
            melody.rootHz = rootHz;
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
            config.intent = "mve-001 first build. Every wall bounce adds a ball, every escape widens the exit " +
                            "+1.1deg from 16deg. FULL at 500. Modelled ~50/50 without ball-ball collisions; " +
                            "crowding favours EMPTY, so lower gapDegreesPerEscape if FULL never lands.";
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
