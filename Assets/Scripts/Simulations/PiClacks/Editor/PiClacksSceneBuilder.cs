using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Simulations.PiClacks.Kernel;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.PiClacks.EditorTools
{
    /// <summary>
    /// Builds the <c>clack</c> scene: <c>PiClacks_Blocks_2D</c> (clack-001).
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Pi Clacks (clack) ▸ Pi Blocks (clack-001)</c>. Re-running replaces
    /// the scene and keeps an existing config asset (hand tuning survives).
    /// </summary>
    static class PiClacksSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/PiClacks_Blocks_2D.unity";
        const string ConfigPath = "Assets/Settings/Configs/PiClacksConfig_001.asset";
        const string ArtFolder = "Assets/Art/Generated";

        // The 1080x1920 canvas reference. Pixels are measured from the top edge of the screen.
        const float ReferenceWidth = 1080f;
        const float ReferenceHeight = 1920f;
        const float TopPanelBottomPx = 150f + 300f;   // CaptionCanvasBuilder: panel 300 tall, 150 down
        const float BottomPanelHeightPx = 300f;
        const float BottomPanelInsetPx = 60f;
        const float LadderTopPx = TopPanelBottomPx + 20f;
        const float LadderFontSize = 64f;
        const float LadderLineFactor = 1.17f;         // Liberation Sans line height / font size
        const float FootnoteFontSize = 54f;
        const float FootnoteHeightPx = 70f;
        const float LadderLeftPx = 90f;
        const float BlockClearancePx = 24f;           // air between the ladder footnote and the tallest block

        const float WallInsetUnits = 0.6f;            // wall face from the left edge of the frame

        [MenuItem("Simulation Lobby/2D Formats/Pi Clacks (clack)/Pi Blocks (clack-001)", false, 161)]
        static void BuildBlocks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[clack] Exit play mode before building the scene.");
                return;
            }

            PiClacksConfig config = EnsureConfig();

            Sprite square = GeneratedArt.Square();
            Sprite circle = GeneratedArt.Circle();
            Sprite glow = GeneratedArt.SoftGlow();
            Sprite blockSprite = EnsureBlockSprite();
            Sprite hatchSprite = EnsureHatchSprite();

            // --- Framing. 9:16: visible width = frameWidth, so the pixel scale is 1080 / frameWidth px per unit. ---
            float frameWidth = config.frameWidth;
            float pixelsPerUnit = ReferenceWidth / frameWidth;
            float orthographicSize = frameWidth * (ReferenceHeight / ReferenceWidth) * 0.5f;
            float wallFaceX = -frameWidth * 0.5f + WallInsetUnits;

            int rows = Mathf.Max(1, config.RoundCount);
            float ladderHeightPx = rows * LadderFontSize * LadderLineFactor;
            float footnoteTopPx = LadderTopPx + ladderHeightPx;
            float ladderBottomPx = footnoteTopPx + FootnoteHeightPx;

            float tallest = config.smallSize;
            for (int i = 0; i < config.RoundCount; i++)
            {
                tallest = Mathf.Max(tallest, config.rounds[i].bigSize);
            }

            float tallestPx = tallest * pixelsPerUnit;
            float floorPx = ladderBottomPx + BlockClearancePx + tallestPx;
            float floorY = (ReferenceHeight * 0.5f - floorPx) / pixelsPerUnit;
            float bottomPanelTopPx = ReferenceHeight - BottomPanelInsetPx - BottomPanelHeightPx;
            bool fitsAbove = floorPx <= bottomPanelTopPx - 20f;
            if (!fitsAbove)
            {
                Debug.LogWarning($"[clack] Floor at {floorPx:0} px from the top leaves under 20 px above the bottom panel " +
                                 $"({bottomPanelTopPx:0} px). Shrink the tallest block or the ladder.");
            }

            float topY = orthographicSize + 1f;

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
            BackdropThemeLibrary.AddStyler(backdropObject, runner, camera, backdrop);

            // --- HUD: caption canvas (question on top, title under it, bottom line + counter) plus the ladder ---
            CaptionHud hud = CaptionCanvasBuilder.Build(config.question, config.title, string.Empty,
                                                        CaptionCanvasBuilder.Style.Dark);
            StyleCaptionCanvas(hud);
            ResultLadderHud ladder = BuildLadder(hud.transform, rows);

            PiClacksPresenter presenter = runnerObject.AddComponent<PiClacksPresenter>();
            presenter.Bind(simulation, hud, ladder, rig);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            string roundList = string.Empty;
            for (int i = 0; i < config.RoundCount; i++)
            {
                PiClacksRound r = config.rounds[i];
                roundList += $"    {i + 1}. {r.massLabel}, big {r.bigSize:0.##}, gaps {r.smallGap:0.##}/{r.bigGap:0.##}, speed {r.speed:0.##}, " +
                             $"expect {BlockCollisionKernel.ExpectedCount(System.Math.Pow(100.0, r.massExponent))}, " +
                             $"run {r.expectedRunSeconds:0.0}s\n";
            }

            float ladderLeftUnits = LadderLeftPx;
            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  config {ConfigPath}\n" +
                $"  {config.RoundCount} rounds, series {config.SeriesSeconds:0.0}s (estimate)\n" +
                roundList +
                $"  framing: ortho size {orthographicSize:0.000} (9:16 view {frameWidth:0.0} x {orthographicSize * 2f:0.0} units, {pixelsPerUnit:0.0} px/unit at 1080 wide), " +
                $"wall face x {wallFaceX:0.00}, floor y {floorY:0.00} ({floorPx:0} px from the top)\n" +
                $"  ladder: left {ladderLeftUnits:0} px, rows {LadderTopPx:0}-{footnoteTopPx:0} px, footnote to {ladderBottomPx:0} px; " +
                $"tallest block {tallest:0.0} units = {tallestPx:0} px, top edge at {floorPx - tallestPx:0} px " +
                $"({(fitsAbove ? "clears the bottom panel" : "TOO TALL")}; bottom panel starts {bottomPanelTopPx:0} px)\n" +
                "  Seed is random per Play and logged at start (cosmetic only).");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>Keeps an existing asset so hand tuning survives; a fresh one starts from the code defaults.</summary>
        static PiClacksConfig EnsureConfig()
        {
            Directory.CreateDirectory("Assets/Settings/Configs");
            var existing = AssetDatabase.LoadAssetAtPath<PiClacksConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<PiClacksConfig>();
            config.intent = "clack-001 Pi Blocks. One question (how many times will they clack?) asked seven times, " +
                            "the big block 100x heavier each time: 3, 31, 314, 3141, 31415, 314159, 3141592. " +
                            "The seed is cosmetic only: colours, spark heights and click pitch vary, the maths is exact.";
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

        static PiClacksPresenter.BlockView CreateBlock(Transform parent, string name, Sprite body, Sprite shadow, string label)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            SpriteRenderer shadowRenderer = CreateSprite(root.transform, "Shadow", shadow, 8);
            shadowRenderer.drawMode = SpriteDrawMode.Sliced;
            shadowRenderer.size = Vector2.one;
            shadowRenderer.color = new Color(0f, 0f, 0f, 0.45f);

            SpriteRenderer bodyRenderer = CreateSprite(root.transform, "Body", body, 10);
            bodyRenderer.drawMode = SpriteDrawMode.Sliced;
            bodyRenderer.size = Vector2.one;

            // Arena decal: world-space TextMeshPro on the block face, authored at one unit and scaled with the
            // block by the presenter. Auto-fit runs once per label change.
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            var tmp = labelObject.AddComponent<TextMeshPro>();
            tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tmp.rectTransform.sizeDelta = new Vector2(0.86f, 0.5f);
            tmp.text = label;
            tmp.color = new Color(0.07f, 0.08f, 0.12f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.5f;
            tmp.fontSizeMax = 4f;
            tmp.sortingOrder = 12;

            return new PiClacksPresenter.BlockView { root = root.transform, body = bodyRenderer, shadow = shadowRenderer, label = tmp };
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

        static ResultLadderHud BuildLadder(Transform canvas, int rows)
        {
            var host = new GameObject("Result Ladder", typeof(RectTransform));
            host.transform.SetParent(canvas, false);
            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = new Vector2(0f, 1f);
            hostRect.anchorMax = new Vector2(0f, 1f);
            hostRect.pivot = new Vector2(0f, 1f);
            hostRect.anchoredPosition = new Vector2(LadderLeftPx, -LadderTopPx);
            hostRect.sizeDelta = new Vector2(ReferenceWidth - LadderLeftPx - 40f, rows * LadderFontSize * LadderLineFactor + FootnoteHeightPx);

            TMP_Text rowsText = CreateLadderText(hostRect, "Rows", LadderFontSize, FontStyles.Bold, new Color(0.92f, 0.95f, 1f, 1f),
                                                 new Vector2(0f, 1f), new Vector2(0f, 0f), rows * LadderFontSize * LadderLineFactor);
            TMP_Text footnote = CreateLadderText(hostRect, "Footnote", FootnoteFontSize, FontStyles.Bold, new Color(1f, 0.82f, 0.25f, 1f),
                                                 new Vector2(0f, 1f), new Vector2(0f, -rows * LadderFontSize * LadderLineFactor), FootnoteHeightPx);
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
        /// Block body: a 9-sliced rounded square (border 24 of 64) with a vertical gradient baked into the border
        /// bands, light on top and darker toward the floor. Tinted per block by the presenter.
        /// </summary>
        static Sprite EnsureBlockSprite()
        {
            return EnsureSprite("ClackBlock_64.png", 64, new Vector4(24f, 24f, 24f, 24f), false, (x, y) =>
            {
                const float radius = 24f;
                float px = x + 0.5f;
                float py = y + 0.5f;
                float cx = Mathf.Clamp(px, radius, 64f - radius);
                float cy = Mathf.Clamp(py, radius, 64f - radius);
                float distance = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                float alpha = Mathf.Clamp01(Mathf.InverseLerp(radius, radius - 1.5f, distance));

                // y = 0 is the bottom row of the texture.
                float shade;
                if (y >= 40)
                {
                    shade = Mathf.Lerp(0.93f, 1f, (y - 40) / 23f);
                }
                else if (y > 24)
                {
                    shade = 0.93f;
                }
                else
                {
                    shade = Mathf.Lerp(0.62f, 0.93f, y / 24f);
                }

                return new Color(shade, shade, shade, alpha);
            });
        }

        /// <summary>Diagonal hatching, white stripes on clear, repeating (needs a tiled SpriteRenderer).</summary>
        static Sprite EnsureHatchSprite()
        {
            return EnsureSprite("ClackHatch_64.png", 64, Vector4.zero, true, (x, y) =>
            {
                int phase = (x + y) % 16;
                float edge = phase < 6 ? 1f : 0f;
                return new Color(1f, 1f, 1f, edge);
            });
        }

        static Sprite EnsureSprite(string fileName, int size, Vector4 border, bool tiled, System.Func<int, int, Color> pixel)
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
            settings.spriteMeshType = SpriteMeshType.FullRect; // sliced and tiled draw modes need it
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
