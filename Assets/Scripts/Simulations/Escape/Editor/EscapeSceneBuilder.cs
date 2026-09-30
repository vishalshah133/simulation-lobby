using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.Escape.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Scenes/Escape_Circle_2D.unity</c> from code.
    /// Menu: <c>Simulation Lobby ▸ Build Scene ▸ Escape_Circle_2D</c>.
    /// </summary>
    /// <remarks>
    /// Scene-as-code for the same reason seeds and configs are committed: a video must stay
    /// re-renderable months later. A hand-assembled scene is a pile of state nobody can diff, and the
    /// collision-safety setup (wall thickness vs. ball radius, continuous detection, zero damping) is
    /// exactly the kind of thing that silently rots when clicked in by hand.
    /// <para>Re-running replaces the scene, so it is safe to iterate on.</para>
    /// </remarks>
    static class EscapeSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Escape_Circle_2D.unity";
        const string ConfigPath = "Assets/Settings/Configs/EscapeConfig_001.asset";
        const string CircleSpritePath = "Assets/Art/Generated/Circle_256.png";
        const string SquareSpritePath = "Assets/Art/Generated/Square_64.png";

        // 9:16 for Shorts. Orthographic size is half the vertical extent, so the arena has to fit the
        // narrower horizontal axis with headroom above for the bounce counter.
        const float ArenaRadius = 4.2f;
        const float OrthographicSize = 9.6f;

        [MenuItem("Simulation Lobby/Build Scene/Escape_Circle_2D")]
        static void Build()
        {
            // Building a scene replaces the open one, which the editor forbids mid-play. Fail with the
            // fix rather than an InvalidOperationException from deep inside EditorSceneManager.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[esc] Exit play mode before building the scene — a new scene cannot " +
                               "replace the running one.");
                return;
            }

            Sprite circle = EnsureSprite(CircleSpritePath, 256, true);
            Sprite square = EnsureSprite(SquareSpritePath, 64, false);
            EscapeConfig config = EnsureConfig();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera: orthographic, framed for 9:16, dark background so the ball reads hot ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.09f, 1f);
            camera.nearClipPlane = 0.01f;
            // A camera built in code has no AudioListener, and without one the scene is silent with no
            // warning that anything is wrong.
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient backdrop, behind everything ---
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

            // --- Boundary ---
            var boundaryObject = new GameObject("Boundary");
            CircularBoundary2D boundary = boundaryObject.AddComponent<CircularBoundary2D>();
            boundary.radius = ArenaRadius;
            float maxBallRadius = ArenaRadius * config.initialSizeFractionMax * config.growth.maxValue;
            // Derived from what actually causes tunneling — distance travelled per step — rather than
            // from the block's "2x object radius" rule of thumb. That rule assumes an object as fast as
            // it is big; here the ball grows to 6x but never exceeds 10 u/s, so 2x its radius would mean
            // a wall as thick as the arena is wide, burying the arena in a donut for no physical gain.
            // 4x margin over the config's fastest speed, floored for visual weight.
            float thicknessForSpeed = config.speedMax * 4f * config.fixedTimestep / 0.5f;
            boundary.thickness = Mathf.Max(0.55f, thicknessForSpeed);
            boundary.segmentCount = 96;
            boundary.gapDegrees = config.gapDegrees;
            boundary.gapCenterDegrees = config.gapCenterDegrees;
            boundary.wallSprite = square;
            boundary.wallSortingOrder = 0;
            boundary.Rebuild();

            // --- Gap marker: the tension only works if the viewer can see the target ---
            var marker = new GameObject("GapMarker");
            marker.transform.SetParent(boundaryObject.transform, false);
            float markerAngle = config.gapCenterDegrees * Mathf.Deg2Rad;
            marker.transform.localPosition =
                new Vector3(Mathf.Cos(markerAngle), Mathf.Sin(markerAngle), 0f) * (ArenaRadius + boundary.thickness * 0.5f);
            // +90 makes the bar tangent to the circle so it reads as a doorway spanning the opening;
            // at the raw gap angle it points outward and reads as an unrelated tick mark.
            marker.transform.localRotation = Quaternion.Euler(0f, 0f, config.gapCenterDegrees + 90f);
            marker.transform.localScale = new Vector3(boundary.GapChordWidth, 0.12f, 1f);
            var markerRenderer = marker.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = square;
            markerRenderer.color = new Color(1f, 0.85f, 0.2f, 1f);
            markerRenderer.sortingOrder = 1;

            // --- Ball ---
            var ballObject = new GameObject("Ball");
            var ballRenderer = ballObject.AddComponent<SpriteRenderer>();
            ballRenderer.sprite = circle;
            ballRenderer.color = new Color(1f, 0.25f, 0.42f, 1f);
            ballRenderer.sortingOrder = 5;

            var body = ballObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;                    // zero gravity: energy must never bleed off
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var ballCollider = ballObject.AddComponent<CircleCollider2D>();
            ballCollider.radius = 0.5f;                // sprite is 1 world unit across
            ballCollider.sharedMaterial = EnsureBouncyMaterial();
            ballObject.AddComponent<BounceDetector2D>();

            // --- Runner + simulation ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            EscapeSimulation simulation = runnerObject.AddComponent<EscapeSimulation>();
            runner.config = config;
            runner.seed = 12345;
            runner.autoStart = true;

            // Private [SerializeField] wiring, done through SerializedObject so the fields stay private.
            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("_ball").objectReferenceValue = body;
            serialized.FindProperty("_ballCollider").objectReferenceValue = ballCollider;
            serialized.FindProperty("_bounceDetector").objectReferenceValue =
                ballObject.GetComponent<BounceDetector2D>();
            serialized.FindProperty("_boundary").objectReferenceValue = boundary;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            VersusScoreboardHud hud = BuildScoreboard();

            // --- Bounce audio: tones are generated at runtime, so there is no audio asset ---
            var audioObject = new GameObject("Bounce Audio");
            BounceMelodyPlayer melody = audioObject.AddComponent<BounceMelodyPlayer>();
            melody.rootHz = 220f;
            melody.noteCount = 16;
            melody.noteDuration = 1.1f;
            melody.voiceCount = 8;
            melody.volume = 0.55f;

            EscapePresenter presenter = runnerObject.AddComponent<EscapePresenter>();
            presenter.Bind(simulation, hud, melody);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Physics2D settings are part of determinism — flag rather than silently change them.
            float safeSpeed = ConstantSpeed2D.MaxSafeSpeed(config.fixedTimestep, boundary.thickness);
            string speedNote = config.speedMax > safeSpeed
                ? $"\n  WARNING: speed {config.speedMax} exceeds the safe ceiling {safeSpeed:0.0} for " +
                  $"timestep {config.fixedTimestep:0.0000} and wall thickness {boundary.thickness:0.00} — tunneling risk."
                : $"\n  speed up to {config.speedMax} is within the safe ceiling {safeSpeed:0.0}.";

            float trappedDiameter = boundary.GapChordWidth * (1f + config.trappedMarginFraction);
            float minStartDiameter = ArenaRadius * config.initialSizeFractionMin * 2f;
            // Bounces to reach the trapped threshold, so the tuning's pacing is visible without a run.
            float bouncesToTrapped = config.growth.magnitude > 1f
                ? Mathf.Log(trappedDiameter / minStartDiameter) / Mathf.Log(config.growth.magnitude)
                : -1f;

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  arena radius {ArenaRadius}, wall thickness {boundary.thickness:0.00}, " +
                $"gap {config.gapDegrees}° (chord {boundary.GapChordWidth:0.00})\n" +
                $"  start diameter {minStartDiameter:0.00}-{ArenaRadius * config.initialSizeFractionMax * 2f:0.00}, " +
                $"trapped past {trappedDiameter:0.00} (~{bouncesToTrapped:0} bounces)\n" +
                $"  {config.attemptCount} attempts, up to {config.maxAttemptSeconds}s each" +
                speedNote +
                "\n  Press Play to run seed 12345.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>
        /// Scoreboard canvas — built by the shared <see cref="ScoreboardCanvasBuilder"/> rather than
        /// here, since <c>surv</c> needs the identical canvas and a second copy of the layout rules
        /// is how they drift apart. Only the copy and the two sides' colours are esc's own.
        /// </summary>
        static VersusScoreboardHud BuildScoreboard()
        {
            return ScoreboardCanvasBuilder.Build(
                new ScoreboardCanvasBuilder.Labels
                {
                    title = "WALL VS BALL",
                    question = "WHO WILL WIN?",
                    leftName = "WALL",
                    rightName = "BALL",
                    round = "ROUND 1 / 3",
                    counter = "0  BOUNCES"
                },
                ScoreboardCanvasBuilder.Palette.Default);
        }



        static EscapeConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EscapeConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var config = ScriptableObject.CreateInstance<EscapeConfig>();
            // Starting values from the plan's tuning table — deliberately the plan's numbers, not new ones.
            config.fixedTimestep = 0.01f;
            config.attemptCount = 7;
            config.maxAttemptSeconds = 24f;
            config.interAttemptSeconds = 0.8f;
            config.growth.magnitude = 1.035f;
            // Display only — MaxTicks derives the real cap from the series.
            config.maxDurationSeconds = config.SeriesSeconds;
            config.initialSizeFractionMin = 1f / 13f;
            config.initialSizeFractionMax = 1f / 11f;
            config.randomizeSizePerAttempt = false;
            config.speedMin = 8f;
            config.speedMax = 10f;
            config.trappedMarginFraction = 0.1f;
            config.gapDegrees = 24f;
            config.gapCenterDegrees = 90f;
            config.growth.mode = EscalationMode.Multiplicative;
            config.growth.maxValue = 6f;
            config.intent = "esc-001 first build. 7 rounds, growth x1.035/bounce (~29 bounces/round), " +
                            "gap 24deg, trapped at +10% over the gap. Tuning baseline.";
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static PhysicsMaterial2D EnsureBouncyMaterial()
        {
            const string path = "Assets/Settings/Configs/PerfectlyElastic.physicsMaterial2D";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var material = new PhysicsMaterial2D("PerfectlyElastic")
            {
                bounciness = 1f,
                friction = 0f
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Generates a white circle or square sprite, sized so one sprite is exactly one world unit.
        /// Generated rather than imported so the scene has no external art dependency.
        /// </summary>
        static Sprite EnsureSprite(string path, int size, bool circle)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte alpha = 255;
                    if (circle)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                        // One-pixel feather: a hard-edged circle aliases badly once it fills the frame.
                        float edge = Mathf.InverseLerp(radius, radius - 1.5f, distance);
                        alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(edge) * 255f);
                    }

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
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;   // one sprite == one world unit
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
