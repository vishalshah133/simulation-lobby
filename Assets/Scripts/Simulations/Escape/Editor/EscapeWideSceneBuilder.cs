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
    /// Builds the 16:9 <c>esc</c> segment for comp-001 (segment 2, "Shrinking Gap"). A separate file
    /// from <see cref="EscapeSceneBuilder"/> on purpose: the 9:16 builder and its published scenes stay
    /// untouched, and this is the pilot for how every other format's Wide path is laid out.
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Escape (esc) ▸ Wide: Shrinking Gap (comp-001)</c>.
    /// </summary>
    /// <remarks>
    /// Same arena, ball, collision safety and sim as the Shorts. What changes is only the frame:
    /// camera ortho size and x offset (<see cref="WideFrameLayout"/>), the HUD (the shared segment
    /// card instead of the portrait scoreboard) and a delayed run start so the card and the explainer
    /// play before round 1.
    /// </remarks>
    static class EscapeWideSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Comp001_02_ShrinkingGap_Wide.unity";
        const string ConfigPath = "Assets/Settings/Configs/EscapeConfig_008.asset";
        const string CardPath = "Assets/Settings/SegmentCards/comp001_02.asset";
        const string MelodyPath = "Assets/Settings/Melodies/OdeToJoy.asset";
        const string CircleSpritePath = "Assets/Art/Generated/Circle_256.png";
        const string SquareSpritePath = "Assets/Art/Generated/Square_64.png";

        const string Title = "Shrinking Gap";
        const string Hook = "CAN THE BALL ESCAPE THE SPINNING GAP?";
        const string Explainer = "The ball grows with every bounce. The gap spins and shrinks each round. Can it still get out before it is too big?";

        // Same arena as the Shorts, so the tuned bounce physics carries over unchanged.
        const float ArenaRadius = 4.2f;

        // The arena fills this fraction of the 75% band WideFrameLayout reserves, i.e. ~80% of frame height.
        const float ArenaFill = 0.9375f;

        // Backdrop theme index in BackdropThemeLibrary.All(): 0 Midnight, 1 Ember, 2 Frost, ... The
        // published Shorts draw a theme per seed; Wide pins Frost (cold teal) against the pink ball.
        const int FrostTheme = 2;

        // Wall-clock seconds before round 1: segment card (1.5) + 3s of the explainer.
        const float StartDelaySeconds = 4.5f;

        [MenuItem("Simulation Lobby/2D Formats/Escape (esc)/Wide: Shrinking Gap (comp-001)", false, 107)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[esc-wide] Exit play mode before building the scene.");
                return;
            }

            Sprite circle = EnsureSprite(CircleSpritePath, 256, true);
            Sprite square = EnsureSprite(SquareSpritePath, 64, false);
            EscapeConfig config = EnsureConfig();

            var song = AssetDatabase.LoadAssetAtPath<MelodySequence>(MelodyPath);
            if (song == null)
            {
                Debug.LogError($"[esc-wide] Melody not found at {MelodyPath}. Scene not built.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Wall thickness first: the frame is sized from the *outer* ring so 80% is what is drawn.
            float thicknessForSpeed = config.speedMax * 4f * config.fixedTimestep / 0.5f;
            float thickness = Mathf.Max(0.55f, thicknessForSpeed);
            float outerDiameter = 2f * (ArenaRadius + thickness);
            float orthoSize = WideFrameLayout.OrthoSizeForArena(outerDiameter, outerDiameter, AspectMode.Wide, ArenaFill);
            float cameraX = WideFrameLayout.ArenaCameraOffsetX(orthoSize, AspectMode.Wide);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(cameraX, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthoSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.06f, 0.075f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient backdrop, sized to the wide view ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(cameraX, 0f, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(orthoSize * 2f * WideFrameLayout.Aspect(AspectMode.Wide) * 1.1f, orthoSize * 2.2f);
            backdrop.particleCount = 110;
            backdrop.maxAlpha = 0.13f;
            backdrop.tint = new Color(0.78f, 0.95f, 1f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- Boundary ---
            var boundaryObject = new GameObject("Boundary");
            CircularBoundary2D boundary = boundaryObject.AddComponent<CircularBoundary2D>();
            boundary.radius = ArenaRadius;
            boundary.thickness = thickness;
            boundary.segmentCount = 96;
            boundary.gapDegrees = config.gapDegrees;
            boundary.gapCenterDegrees = config.gapCenterDegrees;
            boundary.wallSprite = square;
            boundary.wallSortingOrder = 0;
            boundary.Rebuild();

            // --- Gap marker (EscapePresenter keeps its width matched to the shrinking gap) ---
            var marker = new GameObject("GapMarker");
            marker.transform.SetParent(boundaryObject.transform, false);
            float markerAngle = config.gapCenterDegrees * Mathf.Deg2Rad;
            marker.transform.localPosition =
                new Vector3(Mathf.Cos(markerAngle), Mathf.Sin(markerAngle), 0f) * (ArenaRadius + boundary.thickness * 0.5f);
            marker.transform.localRotation = Quaternion.Euler(0f, 0f, config.gapCenterDegrees + 90f);
            marker.transform.localScale = new Vector3(boundary.GapChordWidth, 0.16f, 1f);
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
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var ballCollider = ballObject.AddComponent<CircleCollider2D>();
            ballCollider.radius = 0.5f;
            ballCollider.sharedMaterial = EnsureBouncyMaterial();
            ballObject.AddComponent<BounceDetector2D>();

            // --- Runner + simulation. autoStart off: EscapeWideSegment begins the run after the card. ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            EscapeSimulation simulation = runnerObject.AddComponent<EscapeSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = false;
            runner.randomizeSeedOnStart = false;

            // Fixed theme, no jitter: ten segments need a stable, deliberate look.
            BackdropStyler styler = BackdropThemeLibrary.AddStyler(backdropObject, runner, camera, backdrop);
            styler.pickThemeFromSeed = false;
            styler.fixedTheme = FrostTheme;
            styler.hueJitterDegrees = 0f;
            styler.flowJitterDegrees = 0f;

            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("_ball").objectReferenceValue = body;
            serialized.FindProperty("_ballCollider").objectReferenceValue = ballCollider;
            serialized.FindProperty("_bounceDetector").objectReferenceValue = ballObject.GetComponent<BounceDetector2D>();
            serialized.FindProperty("_boundary").objectReferenceValue = boundary;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // --- Audio: Ode to Joy carries across rounds; the Shorts used no song or Baa Baa / Fur Elise ---
            var audioObject = new GameObject("Bounce Audio");
            BounceMelodyPlayer melody = audioObject.AddComponent<BounceMelodyPlayer>();
            melody.rootHz = 220f;
            melody.noteCount = 16;
            melody.noteDuration = 1.1f;
            melody.voiceCount = 8;
            melody.volume = 0.55f;
            melody.melody = song;

            // Presenter without a portrait HUD: audio + gap-marker upkeep only. The wide HUD is below.
            EscapePresenter presenter = runnerObject.AddComponent<EscapePresenter>();
            presenter.Bind(simulation, null, melody);
            presenter.SetCopy(Title, Hook, restartMelodyEachRound: false);

            // --- Wide HUD: the shared segment card + explainer, fed by EscapeWideSegment ---
            SegmentCardConfig card = SegmentCardFactory.CreateConfigAsset(CardPath, 6, 10, Title, Hook, Explainer);
            SegmentCardPresenter cardPresenter = SegmentCardFactory.CreateInScene(card, AspectMode.Wide);
            EscapeWideSegment segment = runnerObject.AddComponent<EscapeWideSegment>();
            segment.Bind(runner, simulation, cardPresenter, StartDelaySeconds, randomizeSeed: true, seed: 1);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            float safeSpeed = ConstantSpeed2D.MaxSafeSpeed(config.fixedTimestep, boundary.thickness);
            string speedNote = config.speedMax > safeSpeed
                ? $"\n  WARNING: speed {config.speedMax} exceeds the safe ceiling {safeSpeed:0.0} (tunneling risk)."
                : $"\n  speed up to {config.speedMax} is within the safe ceiling {safeSpeed:0.0}.";

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  16:9, ortho {orthoSize:0.00}, camera x {cameraX:0.00}, arena outer {outerDiameter:0.0} " +
                $"({outerDiameter / (2f * orthoSize) * 100f:0}% of frame height)\n" +
                $"  {config.attemptCount} rounds, gap {config.gapDegrees}° -> {config.finalGapDegrees}°, " +
                $"spin {Mathf.Abs(config.gapAngularSpeed)} -> {config.finalGapAngularSpeed}°/s, " +
                $"up to {config.maxAttemptSeconds}s each, {config.interAttemptSeconds}s result hold" +
                speedNote +
                $"\n  Press Play: card + explainer for {StartDelaySeconds}s, then a random seed is drawn and logged.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>
        /// EscapeConfig_006 (shrinking exit) as the base, ramped harder and rounds raised to 5 so the
        /// last round is a 14° needle. Only used when the asset is missing; the asset is the truth.
        /// </summary>
        static EscapeConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<EscapeConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var c = ScriptableObject.CreateInstance<EscapeConfig>();
            c.fixedTimestep = 0.01f;
            c.initialSizeFractionMin = 1f / 13f;
            c.initialSizeFractionMax = 1f / 11f;
            c.randomizeSizePerAttempt = false;
            // Faster than 006's 6-13: bounce rate sets the pace, and 45s for five rounds needs ~2 bounces/s.
            c.speedMin = 11f;
            c.speedMax = 13f;
            c.attemptCount = 5;
            // Plan said ~9s. A ball must be allowed to run until it is too big, or the on-screen "/ ~N"
            // ceiling lies; worst case round 1 is ~17s at 1.9 bounces/s, so 18s only guards stalls.
            c.maxAttemptSeconds = 18f;
            // The minimum hold: a round that ends in a second still gets its result on screen this long.
            c.interAttemptSeconds = 2f;
            c.trappedMarginFraction = 0.1f;
            // x1.045 (006: x1.035): five rounds at ~7-30 bounces instead of ~14-41 keeps the series near 45s.
            c.growth.mode = EscalationMode.Multiplicative;
            c.growth.magnitude = 1.045f;
            c.growth.startValue = 1f;
            c.growth.minValue = 0.1f;
            c.growth.maxValue = 6f;
            c.gapDegrees = 36f;
            c.finalGapDegrees = 14f;
            c.gapCenterDegrees = 90f;
            c.gapAngularSpeed = 20f;
            c.finalGapAngularSpeed = 36f;
            c.gapDirectionSeeded = true;
            c.nearMissGapWidths = 1f;
            c.maxDurationSeconds = c.SeriesSeconds;
            c.intent = "esc-008 (comp-001 #2, Wide): 006's shrinking exit, 5 rounds, gap 36 -> 14deg while the gap spin " +
                       "ramps 20 -> 36deg/s, speed 11-13, growth x1.045. Frost backdrop, Ode to Joy. ~45-55s estimated, not measured.";
            AssetDatabase.CreateAsset(c, ConfigPath);
            return c;
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
            var material = new PhysicsMaterial2D("PerfectlyElastic") { bounciness = 1f, friction = 0f };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Loads the shared generated sprite, writing it first if no esc scene has been built yet.</summary>
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
            importer.spritePixelsPerUnit = size;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
