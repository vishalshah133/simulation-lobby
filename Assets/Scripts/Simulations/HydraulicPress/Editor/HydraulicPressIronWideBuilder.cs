using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.HydraulicPress.EditorTools
{
    /// <summary>
    /// Builds the 16:9 <c>press</c> segment for comp-001: <c>Comp001_05_IronPress_Wide</c>.
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Hydraulic Press (press) ▸ Wide: Iron Press (comp-001)</c>.
    /// A wide, low steel chamber, iron balls (2 t each), three rounds at 8 / 32 / 128 t, a molten pile.
    /// Reuses the press sim and presenter unchanged apart from the additive <c>EnableIronWide</c> path.
    /// Re-running replaces the scene and keeps an existing config asset (hand tuning survives).
    /// </summary>
    static class HydraulicPressIronWideBuilder
    {
        const string ScenePath = "Assets/Scenes/Comp001_05_IronPress_Wide.unity";
        const string ConfigPath = "Assets/Settings/Configs/HydraulicPressConfig_IronWide.asset";
        const string CardPath = "Assets/Settings/SegmentCards/comp001_05.asset";
        const string ArtFolder = "Assets/Art/Generated";

        const string Title = "Iron Press";
        const string Hook = "HOW MANY BALLS CAN A 128-TON PRESS CRUSH?";
        const string Explainer = "Each ball holds 2 tons. A crushed ball splits in two. The press stops once enough balls share its weight.";

        // Canvas reference is 1920x1080 (WideFrameLayout, Wide). Pixels measured from the top-left.
        const float RefW = 1920f;
        const float RefH = 1080f;
        const float TopBarPx = 104f;
        const float BottomStripPx = 104f;
        const float RightMarginPx = 420f;       // counter plate (390) plus a gutter
        const float LeftPadPx = 24f;
        const float LadderTopPx = 650f;         // under the counter plate (it ends at y 630)
        const float LadderFont = 40f;
        const float LadderLine = 1.17f;

        [MenuItem("Simulation Lobby/2D Formats/Hydraulic Press (press)/Wide: Iron Press (comp-001)", false, 192)]
        static void BuildIronWide()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[press] Exit play mode before building the scene.");
                return;
            }

            HydraulicPressConfig config = EnsureConfig();
            SegmentCardConfig cardConfig = SegmentCardFactory.CreateConfigAsset(CardPath, 5, 10, Title, Hook, Explainer);
            if (cardConfig.explainerSeconds > 4f)
            {
                // The hold before round 1 is 3.5 s (1.5 s card, then 2 s lit); keep the explainer from covering round 1's result.
                cardConfig.explainerSeconds = 4f;
                EditorUtility.SetDirty(cardConfig);
            }

            Sprite square = GeneratedArt.Square();
            Sprite circle = GeneratedArt.Circle();
            Sprite glow = GeneratedArt.SoftGlow();
            Sprite hazard = EnsureSprite("PressHazard_64.png", 64, true, (x, y) =>
                (x + y) % 16 < 8 ? new Color32(255, 200, 24, 255) : new Color32(24, 24, 28, 255));
            Sprite chrome = EnsureSprite("PressChrome_32.png", 32, false, (x, y) =>
            {
                float u = (x + 0.5f) / 32f;
                float body = 0.42f + 0.5f * Mathf.Exp(-Mathf.Pow((u - 0.34f) / 0.16f, 2f));
                body += 0.12f * Mathf.Exp(-Mathf.Pow((u - 0.86f) / 0.07f, 2f));
                byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(body) * 255f);
                return new Color32(g, g, g, 255);
            });

            // --- Framing: the arena (chamber + housing band + floor) fills the area between the top bar, the bottom strip and
            // the right margin; the camera shifts right so it sits centred in that area. ---
            float arenaUnitsW = config.chamberWidth + 2f * HydraulicPressPresenter.WallThickness;
            float arenaUnitsH = config.chamberHeight + HydraulicPressPresenter.HousingGap + HydraulicPressPresenter.HousingHeight +
                                HydraulicPressPresenter.FloorThickness;
            float usableW = RefW - RightMarginPx - LeftPadPx;
            float usableH = RefH - TopBarPx - BottomStripPx - 40f;
            float ppu = Mathf.Min(usableW / arenaUnitsW, usableH / arenaUnitsH);       // canvas px per world unit
            float orthographicSize = RefH * 0.5f / ppu;
            float usableCentreX = LeftPadPx + usableW * 0.5f;
            float cameraX = (usableCentreX - RefW * 0.5f) / ppu * -1f;                  // arena at x 0 lands at usableCentreX
            float arenaCentreFromFloor = (config.chamberHeight + HydraulicPressPresenter.HousingGap + HydraulicPressPresenter.HousingHeight -
                                          HydraulicPressPresenter.FloorThickness) * 0.5f;
            float floorY = -arenaCentreFromFloor;                                       // arena centred vertically on y 0
            float frameWidth = RefW / ppu;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(cameraX, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient embers behind everything ---
            var backdropObject = new GameObject("Ambient Embers");
            backdropObject.transform.position = new Vector3(cameraX, 0f, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(frameWidth * 1.2f, orthographicSize * 2.2f);
            backdrop.particleCount = 70;
            backdrop.maxAlpha = 0.12f;
            backdrop.tint = new Color(1f, 0.55f, 0.2f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- Arena art: the presenter sizes, places and colours every piece ---
            var art = new GameObject("Arena");
            var rig = new HydraulicPressPresenter.SceneRig
            {
                cameraTransform = cameraObject.transform,
                floorY = floorY,
                pixelsPerUnit = ppu
            };

            rig.backPanel = CreateSprite(art.transform, "Chamber Back", square, 1);
            rig.wallLeft = CreateSprite(art.transform, "Wall Left", square, 5);
            rig.wallRight = CreateSprite(art.transform, "Wall Right", square, 5);
            rig.edgeLeft = CreateSprite(art.transform, "Wall Left Bevel", square, 6);
            rig.edgeRight = CreateSprite(art.transform, "Wall Right Bevel", square, 6);
            rig.floorBody = CreateSprite(art.transform, "Floor", square, 5);
            rig.floorLine = CreateSprite(art.transform, "Floor Bevel", square, 6);

            // Static warm glow low in the chamber so the pile reads as molten (cosmetic, behind the balls).
            SpriteRenderer pileGlow = CreateSprite(art.transform, "Pile Glow", glow, 8);
            pileGlow.color = new Color(1f, 0.45f, 0.1f, 0.45f);
            pileGlow.transform.position = new Vector3(0f, floorY + 0.9f, 0f);
            pileGlow.transform.localScale = new Vector3(config.chamberWidth * 1.05f, 3.2f, 1f);

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
            rig.plateLabel = CreateTonnageDecal(plateRoot.transform, config.rounds[0].pressLabel);
            rig.rod = CreateSprite(press.transform, "Piston Rod", chrome, 25);
            rig.housing = CreateSprite(press.transform, "Cylinder Housing", chrome, 26);
            rig.housingShine = CreateSprite(press.transform, "Housing Shine", square, 27);
            // rig.subCaption stays null: the explainer panel and bottom strip carry the words.

            // --- Runner + simulation + presenter ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            HydraulicPressSimulation simulation = runnerObject.AddComponent<HydraulicPressSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            // --- HUD: segment card canvas (hook, counter, caption, explainer) plus the ladder under the counter ---
            SegmentCardPresenter card = SegmentCardFactory.CreateInScene(cardConfig, AspectMode.Wide);
            card.SetTimeSource(() => runner.SimulatedSeconds);
            ResultLadderHud ladder = BuildLadder(config.rounds.Length);

            HydraulicPressPresenter presenter = runnerObject.AddComponent<HydraulicPressPresenter>();
            presenter.Bind(simulation, null, ladder, null, rig);
            presenter.EnableIronWide(card);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            string roundList = string.Empty;
            for (int i = 0; i < config.rounds.Length; i++)
            {
                PressRound r = config.rounds[i];
                roundList += $"    {i + 1}. {r.pressLabel} ({r.pressTons:0.##} t), run ~{r.expectedRunSeconds:0.0}s, cap {r.maxRunSeconds:0.0}s\n";
            }

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  config {ConfigPath}, card {CardPath}\n" +
                $"  {config.rounds.Length} rounds, series {config.SeriesSeconds:0.0}s (estimate from harness medians)\n" +
                roundList +
                $"  framing: ortho {orthographicSize:0.000}, {ppu:0.0} px/unit on the 1920x1080 canvas, camera x {cameraX:0.00}, floor y {floorY:0.00}; " +
                $"chamber {config.chamberWidth:0.0} x {config.chamberHeight:0.0} units = {config.chamberWidth * ppu:0} x {config.chamberHeight * ppu:0} px\n" +
                "  Seed is random per Play and logged at start.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>Keeps an existing asset so hand tuning survives; a fresh one is filled with the Iron Press values.</summary>
        static HydraulicPressConfig EnsureConfig()
        {
            Directory.CreateDirectory("Assets/Settings/Configs");
            var existing = AssetDatabase.LoadAssetAtPath<HydraulicPressConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var c = ScriptableObject.CreateInstance<HydraulicPressConfig>();
            c.intent = "comp-001 #5 Iron Press. Wide low steel chamber, iron balls (2 t each), 3 rounds at 8 / 32 / 128 t (4x steps). " +
                       "Each crushed ball splits in two; the press stops when enough balls share its weight.";

            c.chamberWidth = 14f;
            c.chamberHeight = 5.5f;
            c.startRadius = 2.5f;       // diameter 5.0 < height 5.5: the first ball is a big iron ball
            c.startOffset = 0.6f;
            c.ballStrengthTons = 2f;
            c.maxBalls = 5000;          // safety only; the harness lands ~1,500 at 128 t

            c.firstHoldSeconds = 3.5f;  // 1.5 s title card, then the chamber sits lit for 2 s before the first drop
            c.holdSeconds = 0.6f;
            c.resultHoldSeconds = 1.2f;
            c.transitionSeconds = 1.6f;
            c.endCardSeconds = 6f;

            c.title = "IRON PRESS";
            c.question = Hook;
            c.subCaption = string.Empty;
            c.counterLabel = "BALLS";
            c.endLine = "EACH BALL SPLITS IN TWO, SO THE COUNT DOUBLES EVERY SQUEEZE";
            c.ladderFootnote = string.Empty;

            // Harness (kernel only, 14 x 5.5, r 2.5, 2 t/ball, dt 1/60, 4 seeds): 8 t 4-6 balls 1.7-2.3 s; 32 t 101-117 balls
            // 5.3-7.9 s; 128 t 1,467-1,504 balls 11.6-16.3 s.
            c.rounds = new[]
            {
                Round(8f, "8 TONS. GUESS?", "{n} BALLS.", 8f, 2.2f),
                Round(32f, "4X STRONGER: 32 TONS. GUESS?", "{n} BALLS!", 18f, 6.5f),
                Round(128f, "4X AGAIN: 128 TONS. GUESS?", "{n} BALLS!!", 40f, 14f)
            };

            AssetDatabase.CreateAsset(c, ConfigPath);
            return c;
        }

        static PressRound Round(float tons, string caption, string result, float maxRunSeconds, float expectedRunSeconds)
        {
            return new PressRound
            {
                pressTons = tons,
                pressLabel = $"{tons:0} TONS",
                caption = caption,
                result = result,
                maxRunSeconds = maxRunSeconds,
                expectedRunSeconds = expectedRunSeconds
            };
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

        // ---------------------------------------------------------------- HUD: the ladder column

        /// <summary>A small overlay canvas (below the segment card, which is order 100) holding the ladder in the right margin.</summary>
        static ResultLadderHud BuildLadder(int rows)
        {
            var root = new GameObject("Ladder Canvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = WideFrameLayout.ReferenceResolution(AspectMode.Wide);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = WideFrameLayout.MatchWidthOrHeight(AspectMode.Wide);

            float rowsHeight = rows * LadderFont * LadderLine;
            var host = new GameObject("Result Ladder", typeof(RectTransform));
            host.transform.SetParent(root.transform, false);
            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = new Vector2(1f, 1f);
            hostRect.anchorMax = new Vector2(1f, 1f);
            hostRect.pivot = new Vector2(1f, 1f);
            hostRect.anchoredPosition = new Vector2(-14f, -LadderTopPx);
            hostRect.sizeDelta = new Vector2(400f, rowsHeight + 8f);

            TMP_Text rowsText = ScoreboardCanvasBuilder.CreateText(hostRect, "Rows", string.Empty, LadderFont,
                                                                   new Color(0.92f, 0.95f, 1f, 1f), FontStyles.Bold);
            rowsText.alignment = TextAlignmentOptions.TopRight;
            rowsText.textWrappingMode = TextWrappingModes.NoWrap;
            rowsText.overflowMode = TextOverflowModes.Overflow;
            rowsText.richText = true;
            RectTransform r = rowsText.rectTransform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = new Vector2(0f, rowsHeight);

            TMP_Text footnote = ScoreboardCanvasBuilder.CreateText(hostRect, "Footnote", string.Empty, 30f, Color.white, FontStyles.Bold);
            footnote.rectTransform.sizeDelta = new Vector2(0f, 1f);

            ResultLadderHud ladder = host.AddComponent<ResultLadderHud>();
            ladder.Bind(rowsText, footnote);
            return ladder;
        }

        // ---------------------------------------------------------------- generated sprites

        /// <summary>Writes a small PNG once and reuses it (the same file names the Split Press builder uses).</summary>
        static Sprite EnsureSprite(string fileName, int size, bool tiled, System.Func<int, int, Color32> pixel)
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
            importer.spritePixelsPerUnit = size;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
