using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.HydraulicPress.EditorTools
{
    /// <summary>
    /// Builds the <c>press</c> scene: <c>HydraulicPress_Split_2D</c> (press-001).
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Hydraulic Press (press) ▸ Split Press (press-001)</c>. Re-running
    /// replaces the scene and keeps an existing config asset (hand tuning survives).
    /// </summary>
    static class HydraulicPressSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/HydraulicPress_Split_2D.unity";
        const string ConfigPath = "Assets/Settings/Configs/HydraulicPressConfig_001.asset";
        const string ArtFolder = "Assets/Art/Generated";

        // The 1080x1920 canvas reference. Pixels are measured from the top edge of the screen.
        const float ReferenceWidth = 1080f;
        const float ReferenceHeight = 1920f;
        const float TopPanelBottomPx = 150f + 300f;   // CaptionCanvasBuilder: panel 300 tall, 150 down
        const float BottomPanelHeightPx = 300f;
        const float BottomPanelInsetPx = 60f;
        const float LadderTopPx = TopPanelBottomPx + 20f;
        const float LadderFontSize = 38f;             // two columns either side of the piston housing
        const float LadderColumnGapPx = 290f;         // kept clear in the middle for the housing and rod
        const float LadderLineFactor = 1.17f;         // Liberation Sans line height / font size
        const float FootnoteFontSize = 36f;
        const float FootnoteHeightPx = 92f;           // two wrapped lines
        const float LadderLeftPx = 50f;
        const float FloorPx = 1440f;                  // floor top, measured from the top of the screen
        const float SubCaptionTopPx = FloorPx + 42f;  // under the floor slab, above the bottom panel
        const float SubCaptionHeightPx = 56f;
        const float SideMarginPx = 20f;

        [MenuItem("Simulation Lobby/2D Formats/Hydraulic Press (press)/Split Press (press-001)", false, 191)]
        static void BuildSplitPress()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[press] Exit play mode before building the scene.");
                return;
            }

            HydraulicPressConfig config = EnsureConfig();

            Sprite square = GeneratedArt.Square();
            Sprite circle = GeneratedArt.Circle();
            Sprite hazard = EnsureSprite("PressHazard_64.png", 64, Vector4.zero, true, (x, y) =>
                (x + y) % 16 < 8 ? new Color32(255, 200, 24, 255) : new Color32(24, 24, 28, 255));
            Sprite chrome = EnsureSprite("PressChrome_32.png", 32, Vector4.zero, false, (x, y) =>
            {
                // A horizontal cylinder profile: dark edges, a bright band left of centre, a soft rim light.
                float u = (x + 0.5f) / 32f;
                float body = 0.42f + 0.5f * Mathf.Exp(-Mathf.Pow((u - 0.34f) / 0.16f, 2f));
                body += 0.12f * Mathf.Exp(-Mathf.Pow((u - 0.86f) / 0.07f, 2f));
                byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(body) * 255f);
                return new Color32(g, g, g, 255);
            });

            // --- Framing: the press housing starts right under the top panel, and the ladder sits in two columns either
            // side of it (the band above the plate's highest point holds no balls). Chamber down to the sub-caption. ---
            int rows = Mathf.Max(1, config.rounds != null ? config.rounds.Length : 6);
            int rowsPerColumn = (rows + 1) / 2;
            float footnoteHeightPx = string.IsNullOrEmpty(config.ladderFootnote) ? 0f : FootnoteHeightPx;
            float ladderHeightPx = rowsPerColumn * LadderFontSize * LadderLineFactor + footnoteHeightPx;
            float ladderBottomPx = LadderTopPx + ladderHeightPx;
            float regionTopPx = LadderTopPx;
            float totalUnits = config.chamberHeight + HydraulicPressPresenter.HousingGap + HydraulicPressPresenter.HousingHeight;
            float pixelsPerUnitHeight = (FloorPx - regionTopPx) / totalUnits;
            float pixelsPerUnitWidth = (ReferenceWidth - SideMarginPx * 2f) /
                                       (config.chamberWidth + 2f * HydraulicPressPresenter.WallThickness);
            float pixelsPerUnit = Mathf.Min(pixelsPerUnitHeight, pixelsPerUnitWidth);
            float orthographicSize = ReferenceHeight * 0.5f / pixelsPerUnit;
            float frameWidth = ReferenceWidth / pixelsPerUnit;
            float floorY = (ReferenceHeight * 0.5f - FloorPx) / pixelsPerUnit;
            float chamberPx = config.chamberWidth * pixelsPerUnit;
            float housingBandPx = (HydraulicPressPresenter.HousingGap + HydraulicPressPresenter.HousingHeight) * pixelsPerUnit;
            if (ladderHeightPx > housingBandPx + 10f)
            {
                Debug.LogWarning($"[press] The ladder ({ladderHeightPx:0} px) is taller than the band above the plate " +
                                 $"({housingBandPx:0} px), so its last row may overlap the plate at the top of a transition.");
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

            // --- Arena art: the presenter sizes, places and colours every piece ---
            var art = new GameObject("Arena");
            var rig = new HydraulicPressPresenter.SceneRig
            {
                cameraTransform = cameraObject.transform,
                floorY = floorY,
                pixelsPerUnit = pixelsPerUnit
            };

            rig.backPanel = CreateSprite(art.transform, "Chamber Back", square, 1);
            rig.wallLeft = CreateSprite(art.transform, "Wall Left", square, 5);
            rig.wallRight = CreateSprite(art.transform, "Wall Right", square, 5);
            rig.edgeLeft = CreateSprite(art.transform, "Wall Left Bevel", square, 6);
            rig.edgeRight = CreateSprite(art.transform, "Wall Right Bevel", square, 6);
            rig.floorBody = CreateSprite(art.transform, "Floor", square, 5);
            rig.floorLine = CreateSprite(art.transform, "Floor Bevel", square, 6);

            var press = new GameObject("Press");
            press.transform.SetParent(art.transform, false);
            var plateRoot = new GameObject("Plate");
            plateRoot.transform.SetParent(press.transform, false);
            rig.plateRoot = plateRoot.transform;
            rig.plateBody = CreateSprite(plateRoot.transform, "Plate Body", square, 30);
            rig.plateStripes = CreateSprite(plateRoot.transform, "Hazard Stripes", hazard, 31);
            rig.plateStripes.drawMode = SpriteDrawMode.Tiled;
            rig.plateStripes.tileMode = SpriteTileMode.Continuous;
            rig.plateShine = CreateSprite(plateRoot.transform, "Plate Shine", square, 32);
            rig.plateLabel = CreateTonnageDecal(plateRoot.transform, config.rounds != null && config.rounds.Length > 0
                ? config.rounds[0].pressLabel
                : "2 TONS");
            rig.rod = CreateSprite(press.transform, "Piston Rod", chrome, 25);
            rig.housing = CreateSprite(press.transform, "Cylinder Housing", chrome, 26);
            rig.housingShine = CreateSprite(press.transform, "Housing Shine", square, 27);

            // --- Runner + simulation + presenter ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            HydraulicPressSimulation simulation = runnerObject.AddComponent<HydraulicPressSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;
            BackdropThemeLibrary.AddStyler(backdropObject, runner, camera, backdrop);

            // --- HUD: caption canvas (question on top, title under it, bottom line + counter) plus the ladder ---
            CaptionHud hud = CaptionCanvasBuilder.Build(config.question, config.title, string.Empty,
                                                        CaptionCanvasBuilder.Style.Dark);
            StyleCaptionCanvas(hud);
            ResultLadderHud ladder = BuildLadder(hud.transform, rowsPerColumn, footnoteHeightPx, false);
            ResultLadderHud ladderRight = BuildLadder(hud.transform, rowsPerColumn, 0f, true);
            rig.subCaption = BuildSubCaption(hud.transform, config.subCaption);

            HydraulicPressPresenter presenter = runnerObject.AddComponent<HydraulicPressPresenter>();
            presenter.Bind(simulation, hud, ladder, ladderRight, rig);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            string roundList = string.Empty;
            if (config.rounds != null)
            {
                for (int i = 0; i < config.rounds.Length; i++)
                {
                    PressRound r = config.rounds[i];
                    roundList += $"    {i + 1}. {r.pressLabel} ({r.pressTons:0.##} t), run ~{r.expectedRunSeconds:0.0}s, cap {r.maxRunSeconds:0.0}s\n";
                }
            }

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  config {ConfigPath}\n" +
                $"  {rows} rounds, series {config.SeriesSeconds:0.0}s (estimate)\n" +
                roundList +
                $"  framing: ortho size {orthographicSize:0.000} (9:16 view {frameWidth:0.0} x {orthographicSize * 2f:0.0} units, " +
                $"{pixelsPerUnit:0.0} px/unit at 1080 wide, limited by {(pixelsPerUnitHeight < pixelsPerUnitWidth ? "height" : "width")}), " +
                $"floor y {floorY:0.00} ({FloorPx:0} px from the top)\n" +
                $"  chamber {config.chamberWidth:0.0} x {config.chamberHeight:0.0} units = {chamberPx:0} x {config.chamberHeight * pixelsPerUnit:0} px; " +
                $"ladder (2 columns) {LadderTopPx:0}-{ladderBottomPx:0} px beside a {housingBandPx:0} px housing band, " +
                $"housing top at {FloorPx - totalUnits * pixelsPerUnit:0} px\n" +
                "  Seed is random per Play and logged at start.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>Keeps an existing asset so hand tuning survives; a fresh one starts from the config type's defaults.</summary>
        static HydraulicPressConfig EnsureConfig()
        {
            Directory.CreateDirectory("Assets/Settings/Configs");
            var existing = AssetDatabase.LoadAssetAtPath<HydraulicPressConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<HydraulicPressConfig>();
            config.intent = "press-001 Split Press. One question (how many balls before the press stops?) asked six times, " +
                            "the press 2x stronger each round. Every crushed ball splits in 2 smaller balls; each holds 1 ton. " +
                            "The seed only moves the starting ball and the split axes; colours are cosmetic.";
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        // ---------------------------------------------------------------- scene pieces

        static SpriteRenderer CreateSprite(Transform parent, string name, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>Arena decal: world-space TextMeshPro on the plate face. The presenter sizes it and sets the text.</summary>
        static TextMeshPro CreateTonnageDecal(Transform parent, string label)
        {
            var labelObject = new GameObject("Tonnage Decal");
            labelObject.transform.SetParent(parent, false);
            var tmp = labelObject.AddComponent<TextMeshPro>();
            tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tmp.rectTransform.sizeDelta = new Vector2(4f, 0.5f);
            tmp.text = label;
            tmp.color = new Color(1f, 0.9f, 0.45f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.5f;
            tmp.fontSizeMax = 6f;
            tmp.sortingOrder = 36;
            return tmp;
        }

        // ---------------------------------------------------------------- HUD

        /// <summary>
        /// The question is the big line (smaller than the studio default, it is a sentence); the bottom panel is
        /// taller and holds two lines: the round's caption and the live counter.
        /// </summary>
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

        static TMP_Text BuildSubCaption(Transform canvas, string content)
        {
            TMP_Text text = ScoreboardCanvasBuilder.CreateText((RectTransform)canvas, "Sub Caption", content, 42f,
                                                               new Color(1f, 0.82f, 0.25f, 1f), FontStyles.Bold);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -SubCaptionTopPx);
            rect.sizeDelta = new Vector2(-40f, SubCaptionHeightPx);
            return text;
        }

        static ResultLadderHud BuildLadder(Transform canvas, int rows, float footnoteHeightPx, bool rightColumn)
        {
            float rowsHeight = rows * LadderFontSize * LadderLineFactor;
            float columnWidth = (ReferenceWidth - LadderLeftPx * 2f - LadderColumnGapPx) * 0.5f;
            var host = new GameObject(rightColumn ? "Result Ladder (right)" : "Result Ladder", typeof(RectTransform));
            host.transform.SetParent(canvas, false);
            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = new Vector2(0f, 1f);
            hostRect.anchorMax = new Vector2(0f, 1f);
            hostRect.pivot = new Vector2(0f, 1f);
            float left = rightColumn ? ReferenceWidth - LadderLeftPx - columnWidth : LadderLeftPx;
            hostRect.anchoredPosition = new Vector2(left, -LadderTopPx);
            hostRect.sizeDelta = new Vector2(columnWidth, rowsHeight + footnoteHeightPx);

            TMP_Text rowsText = CreateLadderText(hostRect, "Rows", LadderFontSize, FontStyles.Bold, new Color(0.92f, 0.95f, 1f, 1f),
                                                 new Vector2(0f, 1f), new Vector2(0f, 0f), rowsHeight);
            TMP_Text footnote = CreateLadderText(hostRect, "Footnote", FootnoteFontSize, FontStyles.Bold, new Color(1f, 0.82f, 0.25f, 1f),
                                                 new Vector2(0f, 1f), new Vector2(0f, -rowsHeight), Mathf.Max(footnoteHeightPx, 1f));
            footnote.textWrappingMode = TextWrappingModes.Normal;
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

        // ---------------------------------------------------------------- generated sprites

        /// <summary>
        /// Writes a small PNG once and reuses it after that. The pixel functions above never change shape for
        /// these file names; if one is edited, bump its file name so a stale import can't be picked up.
        /// </summary>
        static Sprite EnsureSprite(string fileName, int size, Vector4 border, bool tiled, System.Func<int, int, Color32> pixel)
        {
            string path = $"{ArtFolder}/{fileName}";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(ArtFolder);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = pixel(x, y);
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
            importer.spritePixelsPerUnit = size; // one sprite == one world unit
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // tiled draw mode needs it
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
