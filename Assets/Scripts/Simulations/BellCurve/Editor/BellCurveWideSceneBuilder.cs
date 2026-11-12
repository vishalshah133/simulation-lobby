using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.BellCurve.EditorTools
{
    /// <summary>
    /// Builds the 16:9 <c>bell</c> segment for comp-001 (#9, "Rigged Board"): 18 rows on a wide board, 500 fair marbles,
    /// then 1,500 on a board rigged hard left. Additive: the 9:16 builder, config and scene are untouched.
    /// Menu: Simulation Lobby > 2D Formats > Bell Curve Drop (bell) > Wide: Rigged Board (comp-001).
    /// </summary>
    static class BellCurveWideSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Comp001_09_RiggedBoard_Wide.unity";
        const string ConfigPath = "Assets/Settings/Configs/BellCurveConfig_Comp001_09_Wide.asset";
        const string CardPath = "Assets/Settings/SegmentCards/comp001_09.asset";

        const string Hook = "WHAT SHAPE DO 1,500 MARBLES MAKE ON A BOARD THAT LEANS LEFT?";
        const string Explainer = "Each peg is a coin flip: left or right. Marbles pile into a bell curve. Then the pegs are rigged to push left.";

        const AspectMode Mode = AspectMode.Wide;

        [MenuItem("Simulation Lobby/2D Formats/Bell Curve Drop (bell)/Wide: Rigged Board (comp-001)", false, 200)]
        static void BuildWide()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[bell] Exit play mode before building the scene.");
                return;
            }

            BellCurveConfig config = EnsureConfig();
            SegmentCardConfig cardConfig = SegmentCardFactory.CreateConfigAsset(CardPath, 3, 10, "Rigged Board", Hook, Explainer);

            Sprite square = GeneratedArt.Square();
            Sprite circle = GeneratedArt.Circle();
            Sprite glow = GeneratedArt.SoftGlow();

            // Framing: the arena (spout to tray floor, full board width) sits in the middle band, left of the right margin.
            float boardWidth = (config.rows + 1) * config.binSpacing;
            float top = config.dropHeight + config.marbleRadius + 0.4f;       // spout top
            float bottom = -config.trayDepth - 0.15f;                          // under the tray floor
            float arenaHeight = top - bottom;
            float arenaCenterY = (top + bottom) * 0.5f;
            float orthographicSize = WideFrameLayout.OrthoSizeForArena(boardWidth + 0.4f, arenaHeight, Mode, 1f);
            float cameraX = WideFrameLayout.ArenaCameraOffsetX(orthographicSize, Mode);

            float maxHalf = config.frameWidth * 0.5f;
            if (boardWidth * 0.5f > maxHalf)
            {
                Debug.LogWarning($"[bell] Board half-width {boardWidth * 0.5f:0.0} exceeds frameWidth/2 {maxHalf:0.0}; raise frameWidth or lower binSpacing.");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(cameraX, arenaCenterY, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.07f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(cameraX, arenaCenterY, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(orthographicSize * 2f * WideFrameLayout.Aspect(Mode) * 1.2f, orthographicSize * 2.2f);
            backdrop.particleCount = 90;
            backdrop.maxAlpha = 0.12f;
            backdrop.tint = new Color(1f, 0.7f, 0.45f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            var arena = new GameObject("Arena");
            var rig = new BellCurvePresenter.SceneRig
            {
                arena = arena.transform,
                boardOrigin = Vector2.zero,
                circleSprite = circle,
                squareSprite = square,
                glowSprite = glow
            };

            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            BellCurveSimulation simulation = runnerObject.AddComponent<BellCurveSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;
            BackdropThemeLibrary.AddStyler(backdropObject, runner, camera, backdrop);

            // HUD: segment card (hook, counter, caption, explainer) plus the bell readouts.
            SegmentCardPresenter card = SegmentCardFactory.CreateInScene(cardConfig, Mode);
            BellWideHud hud = runnerObject.AddComponent<BellWideHud>();
            BuildReadouts(hud, simulation, card);

            BellCurvePresenter presenter = runnerObject.AddComponent<BellCurvePresenter>();
            presenter.Bind(simulation, null, null, rig);
            presenter.ConfigureWide(WarmGradient(), 720, 130f);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            string roundList = string.Empty;
            for (int i = 0; i < config.RoundCount; i++)
            {
                BellRound r = config.rounds[i];
                roundList += $"    {i + 1}. {r.marbles} marbles at {r.spawnPerSecond:0}/s, P(right) {r.rightProbability:0.00}, " +
                             $"lean {r.pegLean:0.0}, run ~{config.RunSeconds(r):0.0}s\n";
            }

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  config {ConfigPath}, card {CardPath}\n" +
                $"  {config.RoundCount} rounds, series ~{config.SeriesSeconds:0}s (estimate, not measured; the card and explainer overlay the first {cardConfig.cardSeconds + cardConfig.explainerSeconds:0.0}s)\n" +
                roundList +
                $"  framing: ortho {orthographicSize:0.00}, camera x {cameraX:0.00}, board {boardWidth:0.0} wide, {config.rows} rows, {config.rows + 1} bins\n" +
                "  Seed is random per Play and logged at start; paste it into the runner's Seed to re-render a take.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        static Gradient WarmGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.80f, 0.12f, 0.15f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.10f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.88f, 0.30f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        /// <summary>Keeps an existing asset so hand tuning survives; a fresh one is filled with the segment's values.</summary>
        static BellCurveConfig EnsureConfig()
        {
            Directory.CreateDirectory("Assets/Settings/Configs");
            var existing = AssetDatabase.LoadAssetAtPath<BellCurveConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<BellCurveConfig>();
            config.intent = "comp-001 #9 Rigged Board (16:9). 18 peg rows, 19 wide bins. Round 1: 500 marbles on a fair board. " +
                            "Round 2: 1,500 marbles on a board rigged left (P(right) 0.25). By the central limit theorem round 2 is still a bell, shifted left.";
            config.rows = 18;
            config.binSpacing = 1.0f;
            config.rowSpacing = 0.4f;
            config.hopSeconds = 0.16f;
            config.hopBump = 0.08f;
            config.dropHeight = 1.4f;
            config.gravity = 18f;
            config.marbleRadius = 0.14f;
            config.pegRadius = 0.09f;
            config.trayDepth = 9.3f;
            config.trayHeight = 1.8f;
            config.peakFraction = 0.25f;
            config.frameWidth = 20f;
            config.firstHoldSeconds = 2.5f;
            config.preDropHoldSeconds = 1.5f;
            config.settleSeconds = 0.4f;
            config.resultHoldSeconds = 3f;
            config.transitionSeconds = 1.4f;
            config.endCardSeconds = 4f;
            config.title = "EVERY PEG IS A COIN FLIP";
            config.question = Hook;
            config.endQuestion = "STILL A BELL";
            config.holdCaption = "GUESS THE SHAPE?";
            config.counterLabel = "MARBLES";
            config.endCard = "STILL A BELL, JUST SHIFTED.";
            config.endLine = "THE CENTRAL LIMIT THEOREM";
            config.rounds = new[]
            {
                new BellRound
                {
                    marbles = 500, spawnPerSecond = 60f, rightProbability = 0.5f, pegLean = 0f, showCurve = true,
                    ladderRow = "500 FAIR: BELL",
                    caption = "500 MARBLES. EVERY PEG A COIN FLIP...",
                    holdCaption = "FAIR BOARD. GUESS THE SHAPE?",
                    result = "A BELL CURVE."
                },
                new BellRound
                {
                    marbles = 1500, spawnPerSecond = 150f, rightProbability = 0.25f, pegLean = -0.9f, showCurve = true,
                    ladderRow = "1,500 RIGGED: BELL",
                    caption = "RIGGED: EVERY PEG PUSHES LEFT...",
                    holdCaption = "RIGGED LEFT. STILL A BELL?",
                    result = "STILL A BELL, JUST SHIFTED."
                }
            };
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        // ---------------------------------------------------------------- HUD (right margin, above and below the counter)

        static void BuildReadouts(BellWideHud hud, BellCurveSimulation simulation, SegmentCardPresenter card)
        {
            var root = new GameObject("Bell Wide Canvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;   // under the segment card (100) so the title card covers it
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = WideFrameLayout.ReferenceResolution(Mode);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = WideFrameLayout.MatchWidthOrHeight(Mode);

            var plate = new Color(0.03f, 0.04f, 0.07f, 0.78f);

            // Fitted-curve readout: above the counter plate (which spans ~370-630 px from the top).
            RectTransform readout = Panel(root.transform, "Fit Readout", new Vector2(-10f, -130f), new Vector2(390f, 220f), plate, out CanvasGroup readoutGroup);
            TMP_Text readoutText = Text(readout, "Fit Text", 30f, new Color(1f, 0.82f, 0.25f), TextAlignmentOptions.TopLeft);
            Inset(readoutText.rectTransform, 18f, 12f);

            // Ladder + the why line: below the counter, above the explainer strip (which starts ~930 px from the top).
            RectTransform ladder = Panel(root.transform, "Ladder", new Vector2(-10f, -650f), new Vector2(390f, 270f), plate, out CanvasGroup ladderGroup);
            TMP_Text ladderText = Text(ladder, "Ladder Rows", 32f, Color.white, TextAlignmentOptions.TopLeft);
            ladderText.textWrappingMode = TextWrappingModes.NoWrap;
            ladderText.enableAutoSizing = true;
            ladderText.fontSizeMax = 32f;
            ladderText.fontSizeMin = 22f;
            RectTransform ladderRect = ladderText.rectTransform;
            ladderRect.anchorMin = new Vector2(0f, 1f);
            ladderRect.anchorMax = new Vector2(1f, 1f);
            ladderRect.pivot = new Vector2(0.5f, 1f);
            ladderRect.anchoredPosition = new Vector2(0f, -12f);
            ladderRect.sizeDelta = new Vector2(-36f, 120f);
            TMP_Text noteText = Text(ladder, "Why Line", 26f, new Color(0.85f, 0.9f, 1f), TextAlignmentOptions.TopLeft);
            noteText.textWrappingMode = TextWrappingModes.Normal;
            RectTransform noteRect = noteText.rectTransform;
            noteRect.anchorMin = new Vector2(0f, 0f);
            noteRect.anchorMax = new Vector2(1f, 0f);
            noteRect.pivot = new Vector2(0.5f, 0f);
            noteRect.anchoredPosition = new Vector2(0f, 10f);
            noteRect.sizeDelta = new Vector2(-36f, 125f);

            hud.Bind(simulation, card, readoutText, readoutGroup, ladderText, noteText, ladderGroup);
        }

        static RectTransform Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color color, out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            return rect;
        }

        static TMP_Text Text(Transform parent, string name, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.color = color;
            t.fontStyle = FontStyles.Bold;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        static void Inset(RectTransform r, float x, float y)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(x, y);
            r.offsetMax = new Vector2(-x, -y);
        }
    }
}
