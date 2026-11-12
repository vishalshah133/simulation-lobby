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
    /// comp-001 segment 8, "Three Rings": the <c>brk</c> sim re-framed 16:9 with 3 fat rings, a 45 s clock and a
    /// fast public-domain tune. Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Ring Breaker (brk) ▸ Wide: Three Rings (comp-001)</c>.
    /// Sim code is untouched; this is a new config asset, a new scene and a HUD.
    /// </summary>
    static class BreakerWideSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Comp001_08_ThreeRings_Wide.unity";
        const string ConfigPath = "Assets/Settings/Configs/BreakerConfig_Comp001_08.asset";
        const string CardPath = "Assets/Settings/SegmentCards/comp001_08.asset";
        const string MelodyPath = "Assets/Settings/Melodies/HallOfTheMountainKing.asset";
        const string BouncyPath = "Assets/Settings/Configs/PerfectlyElastic.physicsMaterial2D";

        const string Hook = "CAN IT BREAK 3 RINGS IN 45 SECONDS?";
        const string Explainer = "Each hit cracks a ring piece. Break one open and the ball escapes, shattering the ring. Clear all 3 before time runs out.";

        static readonly Color Navy = new Color(0.03f, 0.05f, 0.15f, 1f);
        static readonly Color BallCyan = new Color(0.2f, 0.95f, 1f, 1f);

        /// <summary>Innermost first: champagne, gold, bronze. Close in hue, so lightness and the ring order carry the read.</summary>
        static readonly Color[] RingPalette =
        {
            new Color(1f, 0.93f, 0.62f),
            new Color(1f, 0.78f, 0.12f),
            new Color(0.95f, 0.5f, 0.08f),
        };

        [MenuItem("Simulation Lobby/2D Formats/Ring Breaker (brk)/Wide: Three Rings (comp-001)", false, 112)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[brk-wide] Exit play mode before building the scene.");
                return;
            }

            BreakerConfig config = EnsureConfig();
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[brk-wide] Config '{config.name}' is invalid: {error}. Scene not built.", config);
                return;
            }

            var song = AssetDatabase.LoadAssetAtPath<MelodySequence>(MelodyPath);
            if (song == null)
            {
                Debug.LogError($"[brk-wide] Melody not found at {MelodyPath}. Scene not built.");
                return;
            }

            SegmentCardConfig cardConfig = SegmentCardFactory.CreateConfigAsset(CardPath, 2, 10, "Three Rings", Hook, Explainer);

            Sprite circle = GeneratedArt.Circle();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera: rings centred in the area left of the right margin ---
            float outerEdge = config.RingOuterRadius(config.ringCount - 1);
            float arena = (outerEdge + 0.4f) * 2f;
            float ortho = WideFrameLayout.OrthoSizeForArena(arena, arena, AspectMode.Wide);
            float cameraX = WideFrameLayout.ArenaCameraOffsetX(ortho, AspectMode.Wide);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(cameraX, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = ortho;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Navy;
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient backdrop: faint gold dust on navy ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(cameraX, 0f, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(ortho * WideFrameLayout.Aspect(AspectMode.Wide) * 2.2f, ortho * 2.2f);
            backdrop.particleCount = 90;
            backdrop.maxAlpha = 0.1f;
            backdrop.tint = new Color(1f, 0.88f, 0.55f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- Rings ---
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
                ring.visualGapFraction = 0.08f;
                ring.color = RingPalette[i % RingPalette.Length];
                ring.visualMaterial = segmentMaterial;
                ring.sortingOrder = 0;
                ring.Rebuild();

                var ringBody = ringObject.AddComponent<Rigidbody2D>();
                ringBody.bodyType = RigidbodyType2D.Kinematic;
                ringBody.interpolation = RigidbodyInterpolation2D.None;

                for (int s = 0; s < ring.SegmentCount; s++)
                {
                    ring.GetCollider(s).sharedMaterial = bouncy;
                }

                rings[i] = ring;
            }

            // --- Ball: cyan, the only cool colour in a gold-on-navy frame ---
            var ballObject = new GameObject("Ball");
            var ballRenderer = ballObject.AddComponent<SpriteRenderer>();
            ballRenderer.sprite = circle;
            ballRenderer.color = BallCyan;
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
            trail.time = 0.25f;
            trail.widthMultiplier = config.ballRadius * 1.6f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.minVertexDistance = 0.02f;
            trail.sortingOrder = 9;
            var trailGradient = new Gradient();
            trailGradient.SetKeys(
                new[] { new GradientColorKey(BallCyan, 0f), new GradientColorKey(new Color(0.1f, 0.5f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = trailGradient;
            trail.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "BallTrail" };

            // --- Debris: one shape per ring (shards, curved chunks, sparkle stars for the last) ---
            var shatterObject = new GameObject("Shatter VFX");
            ShatterBurst2D shatter = shatterObject.AddComponent<ShatterBurst2D>();
            shatter.Apply(
                new Texture[] { GeneratedArt.Shard(), GeneratedArt.ArcChunk(), GeneratedArt.Star() },
                GeneratedArt.SoftDot(), GeneratedArt.Ring(), 6);

            // --- Runner + simulation. autoStart is off: BreakerWideHud begins the run once the title card has faded,
            //     so the simulated 45 s clock does not burn under it. ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            BreakerSimulation simulation = runnerObject.AddComponent<BreakerSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = false;

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

            // --- Audio: one note of the tune per touch ---
            var audioObject = new GameObject("Bounce Audio");
            BounceMelodyPlayer melody = audioObject.AddComponent<BounceMelodyPlayer>();
            melody.noteDuration = 0.55f;
            melody.voiceCount = 12;
            melody.volume = 0.55f;
            melody.melody = song;

            // Effects + camera shake + melody per touch come from the existing presenter. It has no scoreboard
            // here (hud = null is handled); the wide HUD below owns every on-screen string.
            BreakerPresenter presenter = runnerObject.AddComponent<BreakerPresenter>();
            presenter.Bind(simulation, null, melody, shatter, camera, "THREE RINGS", Hook);

            // --- HUD: segment card (hook, counter, caption, explainer) + clock panel ---
            SegmentCardPresenter card = SegmentCardFactory.CreateInScene(cardConfig, AspectMode.Wide);
            BreakerWideHud.Build(runner, simulation, card, cardConfig.cardSeconds, config.timeLimitSeconds);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            var log = new StringBuilder();
            log.AppendLine($"[brk-wide] Built {ScenePath} ({config.ringCount} rings, {config.timeLimitSeconds:0}s clock, song {song.songTitle}, {song.Count} notes). Press Play: random seed is logged.");
            for (int i = 0; i < config.ringCount; i++)
            {
                log.AppendLine($"  ring {i + 1}: r {config.RingInnerRadius(i):0.00}-{config.RingOuterRadius(i):0.00}, " +
                               $"{config.SegmentsInRing(i)} segments x {config.HitPointsForRing(i)} hp, " +
                               $"hole {config.HoleChord(i):0.00} for ball {config.ballRadius * 2f:0.00}, speed {config.SpeedForRing(i):0.0}");
            }

            log.Append("  Win/lose is UNVERIFIED: run several takes. Too easy -> lower speedPerRing/startSpeed or raise hit points; " +
                       $"too hard -> the reverse. Config: {ConfigPath} (segment/ring geometry changes need a rebuild).");
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
            config.maxDurationSeconds = 90f;

            // Fat rings: thicker and further apart than brk-001, three of them.
            config.ringCount = 3;
            config.innerRadius = 1.7f;
            config.ringSpacing = 1f;
            config.ringThickness = 0.42f;
            config.innerSegments = 10;
            config.extraSegmentsPerRing = 2;

            // TUNE HERE. Hit points interpolate inner -> outer, rounded: 3 / 4 / 4. A rough billiard model (not
            // Unity) put the median near 65s for 3/4/4 at 7 u/s +1.2 and the real game ran ~1.4x faster than that
            // model for brk-001, so the target is ~45s median, a coin flip against the 45s clock. Unmeasured.
            config.innerHitPoints = 3;
            config.outerHitPoints = 4;
            config.ringAngularSpeed = 20f;
            config.ballRadius = 0.18f;
            config.startSpeed = 7f;
            config.speedPerRing = 1.2f;
            config.timeLimitSeconds = 45f;
            config.finishHoldSeconds = 2.5f;
            config.intent = "comp-001 #8 Three Rings: 3 fat rings of 10/12/14 segments, 3/4/4 touches, spin 20deg/s alternating; " +
                            "ball 7.0 u/s +1.2 per ring; 45s clock; Hall of the Mountain King, one note per touch.";
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
