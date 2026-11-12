using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Simulations.PiClacks.Kernel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.PiClacks.EditorTools
{
    /// <summary>
    /// 16:9 build of the <c>clack</c> scene for compilation <c>comp-001</c>, segment 1 "Digit Jump": rounds skip
    /// so the big block is 1, 10^4, 10^8, 10^12 times the small one (3, 314, 31415, 3141592 clacks), neon on black,
    /// a fixed block colour pair, the digit ladder on the right edge. Shares the sprite/block helpers of the
    /// portrait builder (same partial class) and reuses the presenter and simulation untouched.
    /// </summary>
    static partial class PiClacksSceneBuilder
    {
        const string WideScenePath = "Assets/Scenes/Comp001_01_DigitJump_Wide.unity";
        const string WideConfigPath = "Assets/Settings/Configs/PiClacksConfig_comp001_01.asset";
        const string WideCardPath = "Assets/Settings/SegmentCards/comp001_01.asset";

        const string WideHook = "HOW MANY CLACKS? IT JUMPS 10,000x HEAVIER EACH TIME";
        const string WideExplainer = "A heavy block slams a light one into a wall. Every bounce is a clack. " +
                                     "Heavier blocks clack more, and the count spells pi.";

        [MenuItem("Simulation Lobby/2D Formats/Pi Clacks (clack)/Wide: Digit Jump (comp-001)", false, 162)]
        static void BuildDigitJumpWide()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[clack] Exit play mode before building the scene.");
                return;
            }

            PiClacksConfig config = EnsureWideConfig();
            SegmentCardConfig cardConfig = SegmentCardFactory.CreateConfigAsset(WideCardPath, 8, 10, "Digit Jump", WideHook, WideExplainer);
            cardConfig.cardSeconds = 1.5f;
            cardConfig.explainerSeconds = 5f;
            EditorUtility.SetDirty(cardConfig);

            Sprite square = GeneratedArt.Square();
            Sprite circle = GeneratedArt.Circle();
            Sprite glow = GeneratedArt.SoftGlow();
            Sprite blockSprite = EnsureBlockSprite();
            Sprite hatchSprite = EnsureHatchSprite();

            // --- Framing. The arena fills the area left of the 22% right margin; camera stays at x = 0, so the
            // wall sits 0.6 units in from the left edge of the frame (config.frameWidth = visible world width). ---
            const AspectMode mode = AspectMode.Wide;
            float aspect = WideFrameLayout.Aspect(mode);
            float arenaWidth = config.frameWidth * (1f - WideFrameLayout.WideRightMarginFraction);
            float tallest = config.smallSize;
            for (int i = 0; i < config.RoundCount; i++)
            {
                tallest = Mathf.Max(tallest, config.rounds[i].bigSize);
            }

            float arenaHeight = tallest + 3.5f;
            float orthographicSize = WideFrameLayout.OrthoSizeForArena(arenaWidth, arenaHeight, mode);
            float visibleWidth = orthographicSize * 2f * aspect;
            float wallFaceX = -visibleWidth * 0.5f + WallInsetUnits;
            float floorY = -orthographicSize * 0.5f; // lower-centre: floor line 75% of the way down the frame
            float topY = orthographicSize + 1f;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera: black ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f, 0.01f, 0.02f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Faint neon dust (fixed, not theme-styled: this segment's palette is fixed) ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(0f, 0f, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(visibleWidth * 1.2f, orthographicSize * 2.2f);
            backdrop.particleCount = 70;
            backdrop.maxAlpha = 0.10f;
            backdrop.tint = new Color(0.45f, 0.95f, 1f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- Arena art ---
            var art = new GameObject("Arena");
            var rig = new PiClacksPresenter.SceneRig
            {
                wallFaceX = wallFaceX,
                floorY = floorY,
                topY = topY,
                glowSprite = glow,
                squareSprite = square
            };

            rig.floorFill = CreateSprite(art.transform, "Floor Fill", square, 3);
            rig.floorLine = CreateSprite(art.transform, "Floor Line", square, 4);
            rig.wallBar = CreateSprite(art.transform, "Wall", square, 5);
            rig.wallHatch = CreateSprite(art.transform, "Wall Hatching", hatchSprite, 6);
            rig.wallHatch.drawMode = SpriteDrawMode.Tiled;
            rig.wallHatch.tileMode = SpriteTileMode.Continuous;
            rig.wallEdge = CreateSprite(art.transform, "Wall Edge", square, 7);
            rig.seamGlow = CreateSprite(art.transform, "Seam Glow", glow, 9);
            rig.seamGlow.enabled = false;

            rig.small = CreateBlock(art.transform, "Small Block", blockSprite, blockSprite, config.smallMassLabel);
            rig.big = CreateBlock(art.transform, "Big Block", blockSprite, blockSprite, config.rounds[0].massLabel);

            // --- Runner + simulation + presenter ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            PiClacksSimulation simulation = runnerObject.AddComponent<PiClacksSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            PiClacksPresenter presenter = runnerObject.AddComponent<PiClacksPresenter>();
            presenter.Bind(simulation, null, null, rig);
            presenter.ApplyWideLook(
                new Color(1f, 0.22f, 0.8f, 1f),      // small block: hot magenta
                new Color(0.15f, 0.95f, 1f, 1f),     // big block: electric cyan
                new Color(0.16f, 0.12f, 0.3f, 1f),   // wall: deep violet
                new Color(0.55f, 0.35f, 1f, 0.5f),   // hatching: neon violet
                new Color(0.75f, 0.55f, 1f, 1f),     // wall edge
                new Color(0.2f, 1f, 0.9f, 0.95f),    // floor line: neon aqua
                new Color(0.2f, 1f, 0.9f, 0.05f),
                90, 0.8f, 2.3f, 0.25f, 0.65f);       // heavy buzz: 90 Hz base

            // --- HUD: shared segment card (hook top-left, counter right margin, caption bottom, explainer) ---
            SegmentCardPresenter card = SegmentCardFactory.CreateInScene(cardConfig, mode);
            PiClacksWideHud wideHud = runnerObject.AddComponent<PiClacksWideHud>();
            wideHud.Bind(card);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, WideScenePath);

            string roundList = string.Empty;
            for (int i = 0; i < config.RoundCount; i++)
            {
                PiClacksRound r = config.rounds[i];
                roundList += $"    {i + 1}. {r.massLabel}, big {r.bigSize:0.##}, expect " +
                             $"{BlockCollisionKernel.ExpectedCount(System.Math.Pow(100.0, r.massExponent))}, run {r.expectedRunSeconds:0.0}s\n";
            }

            Debug.Log(
                $"Built {WideScenePath}\n" +
                $"  config {WideConfigPath}, card {WideCardPath}\n" +
                $"  {config.RoundCount} rounds, series {config.SeriesSeconds:0.0}s (estimate, not measured; segment target ~45s)\n" +
                roundList +
                $"  framing (16:9): ortho {orthographicSize:0.000}, view {visibleWidth:0.0} x {orthographicSize * 2f:0.0} units, " +
                $"wall face x {wallFaceX:0.00}, floor y {floorY:0.00}, tallest block {tallest:0.0}\n" +
                "  Seed is random per Play and logged at start (cosmetic only).");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>Keeps an existing asset so hand tuning survives; a fresh one starts from the segment's rounds.</summary>
        static PiClacksConfig EnsureWideConfig()
        {
            Directory.CreateDirectory("Assets/Settings/Configs");
            var existing = AssetDatabase.LoadAssetAtPath<PiClacksConfig>(WideConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<PiClacksConfig>();
            config.intent = "comp-001 segment 1, Digit Jump (16:9). One question (how many clacks?) asked four times, " +
                            "the big block 10,000x heavier each time: 3, 314, 31415, 3141592. Neon on black, fixed " +
                            "colours; the seed is cosmetic only.";
            config.rounds = new[]
            {
                WideRound(0, "1 kg", 1.00f, 3.00f, "EQUAL MASS. COUNT THE CLACKS", "GUESS: HOW MANY CLACKS?", "{n} CLACKS."),
                WideRound(2, "10,000 kg", 1.70f, 7.90f, "10,000x HEAVIER...", "", "{n} CLACKS. 3, 314..."),
                WideRound(4, "100,000,000 kg", 2.35f, 3.08f, "10,000x HEAVIER AGAIN...", "", "{n}. STILL DIGITS OF PI."),
                WideRound(6, "1,000,000,000,000 kg", 3.00f, 3.53f, "A TRILLION kg...", "", "{n}! 3.141592")
            };
            config.smallSize = 1f;
            config.smallMassLabel = "1 kg";
            config.frameWidth = 18f;
            config.firstHoldSeconds = 7f;        // title card + explainer (1.5 + 5 s) then 0.5 s of GUESS
            config.preLaunchHoldSeconds = 1.2f;
            config.transitionSeconds = 1.5f;
            config.resultHoldSeconds = 1.8f;
            config.endCardSeconds = 5f;
            config.title = "DIGIT JUMP";
            config.question = "HOW MANY CLACKS?";
            config.holdCaption = "GUESS: HOW MANY CLACKS?";
            config.counterLabel = "CLACKS";
            config.endCard = "COLLIDING BLOCKS ENCODE PI";
            config.endLine = "PI = 3.141592";
            AssetDatabase.CreateAsset(config, WideConfigPath);
            return config;
        }

        static PiClacksRound WideRound(int exponent, string label, float bigSize, float runSeconds,
                                       string caption, string holdCaption, string result)
        {
            return new PiClacksRound
            {
                massExponent = exponent,
                massLabel = label,
                bigSize = bigSize,
                smallGap = 2f,
                bigGap = 2f,
                speed = 2f,
                expectedRunSeconds = runSeconds,
                maxRunSeconds = 12f,
                caption = caption,
                holdCaption = holdCaption,
                result = result
            };
        }
    }
}
