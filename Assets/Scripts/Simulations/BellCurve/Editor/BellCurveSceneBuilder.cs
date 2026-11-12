using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.BellCurve.EditorTools
{
    /// <summary>
    /// Builds the <c>bell</c> scene: <c>BellCurve_Drop_2D</c> (bell-001).
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Bell Curve Drop (bell) ▸ Bell Curve Drop (bell-001)</c>. Re-running
    /// replaces the scene and keeps an existing config asset (hand tuning survives). The presenter builds the pegs,
    /// bars and marbles itself from the config, so this only frames the camera and hands over sprites and the HUD.
    /// </summary>
    static class BellCurveSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/BellCurve_Drop_2D.unity";
        const string ConfigPath = "Assets/Settings/Configs/BellCurveConfig_001.asset";

        // The 1080x1920 canvas reference. Pixels are measured from the top edge of the screen.
        const float ReferenceWidth = 1080f;
        const float ReferenceHeight = 1920f;
        const float TopPanelBottomPx = 150f + 300f;   // CaptionCanvasBuilder: panel 300 tall, 150 down
        const float BottomPanelHeightPx = 300f;
        const float BottomPanelInsetPx = 60f;
        const float LadderTopPx = TopPanelBottomPx + 20f;
        const float LadderFontSize = 52f;
        const float LadderLineFactor = 1.17f;
        const float FootnoteFontSize = 46f;
        const float FootnoteHeightPx = 60f;
        const float LadderLeftPx = 90f;

        /// <summary>Where the first peg's marble height sits, px from the top. Leaves room for the ladder and the spout above it.</summary>
        const float FirstPegPx = 790f;

        [MenuItem("Simulation Lobby/2D Formats/Bell Curve Drop (bell)/Bell Curve Drop (bell-001)", false, 199)]
        static void BuildDrop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[bell] Exit play mode before building the scene.");
                return;
            }

            BellCurveConfig config = EnsureConfig();

            Sprite square = GeneratedArt.Square();
            Sprite circle = GeneratedArt.Circle();
            Sprite glow = GeneratedArt.SoftGlow();

            // --- Framing. 9:16: visible width = frameWidth. ---
            float frameWidth = config.frameWidth;
            float pixelsPerUnit = ReferenceWidth / frameWidth;
            float orthographicSize = ReferenceHeight * 0.5f / pixelsPerUnit;
            float originY = (ReferenceHeight * 0.5f - FirstPegPx) / pixelsPerUnit;

            float trayFloorPx = FirstPegPx + config.trayDepth * pixelsPerUnit;
            float trayTopPx = trayFloorPx - config.trayHeight * pixelsPerUnit;
            float bottomPanelTopPx = ReferenceHeight - BottomPanelInsetPx - BottomPanelHeightPx;
            bool fitsBelow = trayFloorPx <= bottomPanelTopPx - 10f;
            if (!fitsBelow)
            {
                Debug.LogWarning($"[bell] Tray floor at {trayFloorPx:0} px leaves under 10 px above the bottom panel " +
                                 $"({bottomPanelTopPx:0} px). Reduce trayDepth.");
            }

            float boardHalfWidth = (config.rows + 1) * config.binSpacing * 0.5f;
            if (boardHalfWidth > frameWidth * 0.5f - 0.1f)
            {
                Debug.LogWarning($"[bell] Board half-width {boardHalfWidth:0.00} does not fit a {frameWidth:0.0}-wide frame. " +
                                 "Reduce rows or binSpacing.");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.09f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient backdrop ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(0f, 0f, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(frameWidth * 1.2f, orthographicSize * 2.2f);
            backdrop.particleCount = 90;
            backdrop.maxAlpha = 0.13f;
            backdrop.tint = new Color(0.55f, 0.65f, 1f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- Arena parent; the presenter fills it ---
            var arena = new GameObject("Arena");
            var rig = new BellCurvePresenter.SceneRig
            {
                arena = arena.transform,
                boardOrigin = new Vector2(0f, originY),
                circleSprite = circle,
                squareSprite = square,
                glowSprite = glow
            };

            // --- Runner + simulation + presenter ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            BellCurveSimulation simulation = runnerObject.AddComponent<BellCurveSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;
            BackdropThemeLibrary.AddStyler(backdropObject, runner, camera, backdrop);

            // --- HUD: caption canvas (question on top, title under it, bottom line + counter) plus the ladder ---
            CaptionHud hud = CaptionCanvasBuilder.Build(config.question, config.title, string.Empty,
                                                        CaptionCanvasBuilder.Style.Dark);
            StyleCaptionCanvas(hud);
            ResultLadderHud ladder = BuildLadder(hud.transform, config.RoundCount);

            BellCurvePresenter presenter = runnerObject.AddComponent<BellCurvePresenter>();
            presenter.Bind(simulation, hud, ladder, rig);

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
                $"  config {ConfigPath}\n" +
                $"  {config.RoundCount} rounds, series ~{config.SeriesSeconds:0}s (estimate)\n" +
                roundList +
                $"  framing: ortho size {orthographicSize:0.000} ({frameWidth:0.0} x {orthographicSize * 2f:0.0} units, {pixelsPerUnit:0.0} px/unit), " +
                $"first peg {FirstPegPx:0} px from the top, tray {trayTopPx:0}-{trayFloorPx:0} px " +
                $"({(fitsBelow ? "clears the bottom panel" : "TOO LOW")}; bottom panel starts {bottomPanelTopPx:0} px)\n" +
                "  Seed is random per Play and logged at start; paste it into the runner's Seed to re-render a take.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>Keeps an existing asset so hand tuning survives; a fresh one starts from the code defaults.</summary>
        static BellCurveConfig EnsureConfig()
        {
            Directory.CreateDirectory("Assets/Settings/Configs");
            var existing = AssetDatabase.LoadAssetAtPath<BellCurveConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<BellCurveConfig>();
            config.intent = "bell-001 Bell Curve Drop. One question (what shape will the marbles make?) asked three times: " +
                            "100 marbles on a fair board (a rough mound), 1,000 on a fair board (a bell), 1,000 on a rigged board " +
                            "(still a bell, just shifted). Every peg is a seeded coin flip, so the seed decides every pile.";
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        // ---------------------------------------------------------------- HUD

        static void StyleCaptionCanvas(CaptionHud hud)
        {
            foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                switch (text.name)
                {
                    case "Caption":
                        text.fontSize = 84f;
                        text.characterSpacing = 2f;
                        text.textWrappingMode = TextWrappingModes.Normal;
                        break;
                    case "Subline":
                        text.fontSize = 46f;
                        break;
                    case "Footer":
                        text.fontSize = 60f;
                        text.fontStyle = FontStyles.Bold;
                        text.color = Color.white;
                        text.alignment = TextAlignmentOptions.Center;
                        text.textWrappingMode = TextWrappingModes.Normal;
                        text.richText = true;
                        var panel = (RectTransform)text.transform.parent;
                        panel.anchoredPosition = new Vector2(0f, BottomPanelInsetPx);
                        panel.sizeDelta = new Vector2(-80f, BottomPanelHeightPx);
                        break;
                }
            }
        }

        static ResultLadderHud BuildLadder(Transform canvas, int rows)
        {
            float rowsHeight = rows * LadderFontSize * LadderLineFactor;
            var host = new GameObject("Result Ladder", typeof(RectTransform));
            host.transform.SetParent(canvas, false);
            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = new Vector2(0f, 1f);
            hostRect.anchorMax = new Vector2(0f, 1f);
            hostRect.pivot = new Vector2(0f, 1f);
            hostRect.anchoredPosition = new Vector2(LadderLeftPx, -LadderTopPx);
            hostRect.sizeDelta = new Vector2(ReferenceWidth - LadderLeftPx - 40f, rowsHeight + FootnoteHeightPx);

            TMP_Text rowsText = CreateLadderText(hostRect, "Rows", LadderFontSize, FontStyles.Bold, new Color(0.92f, 0.95f, 1f, 1f),
                                                 new Vector2(0f, 1f), new Vector2(0f, 0f), rowsHeight);
            TMP_Text footnote = CreateLadderText(hostRect, "Footnote", FootnoteFontSize, FontStyles.Bold, new Color(1f, 0.82f, 0.25f, 1f),
                                                 new Vector2(0f, 1f), new Vector2(0f, -rowsHeight), FootnoteHeightPx);
            footnote.text = string.Empty;

            ResultLadderHud ladder = host.AddComponent<ResultLadderHud>();
            ladder.Bind(rowsText, footnote);
            return ladder;
        }

        static TMP_Text CreateLadderText(RectTransform parent, string name, float fontSize, FontStyles style, Color color,
                                         Vector2 pivot, Vector2 anchoredPosition, float height)
        {
            TMP_Text text = ScoreboardCanvasBuilder.CreateText(parent, name, string.Empty, fontSize, color, style);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(0f, height);
            return text;
        }
    }
}
