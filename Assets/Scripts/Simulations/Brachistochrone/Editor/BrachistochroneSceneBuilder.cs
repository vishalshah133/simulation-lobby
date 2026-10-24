using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.Brachistochrone.EditorTools
{
    /// <summary>
    /// Builds the <c>brach</c> scene: <c>Brachistochrone_FastestPath_2D</c> (brach-001).
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Brachistochrone (brach) ▸ Fastest Path (brach-001)</c>. Re-running
    /// replaces the scene and keeps an existing config asset (hand tuning survives). The tracks, balls, labels, gate
    /// and flag are created by the presenter on Play, so the saved scene holds only the camera, backdrop, runner and HUD.
    /// </summary>
    static class BrachistochroneSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Brachistochrone_FastestPath_2D.unity";
        const string ConfigPath = "Assets/Settings/Configs/BrachistochroneConfig_001.asset";
        const string ArtFolder = "Assets/Art/Generated";

        // Caption canvas geometry at the 1080x1920 reference (pixels from the screen edge).
        const float BottomPanelInsetPx = 70f;
        const float BottomPanelHeightPx = 230f;

        [MenuItem("Simulation Lobby/2D Formats/Brachistochrone (brach)/Fastest Path (brach-001)", false, 181)]
        static void BuildFastestPath()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[brach] Exit play mode before building the scene.");
                return;
            }

            BrachistochroneConfig config = EnsureConfig();

            Sprite square = GeneratedArt.Square();
            Sprite circle = GeneratedArt.Circle();
            Sprite glow = GeneratedArt.SoftGlow();
            Sprite ring = GeneratedArt.RingSprite();
            Sprite checker = EnsureCheckerSprite();

            float frameWidth = config.frameWidth;
            float orthographicSize = frameWidth * (1920f / 1080f) * 0.5f;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera (the presenter refits it to each round) ---
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
            backdrop.fieldSize = new Vector2(frameWidth * 1.6f, orthographicSize * 2.4f);
            backdrop.particleCount = 90;
            backdrop.maxAlpha = 0.13f;
            backdrop.tint = new Color(0.55f, 0.65f, 1f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- Runner + simulation + presenter ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            BrachistochroneSimulation simulation = runnerObject.AddComponent<BrachistochroneSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;
            BackdropThemeLibrary.AddStyler(backdropObject, runner, camera, backdrop);

            // --- HUD: question + small title on top, one bottom line on a dark plate ---
            CaptionHud hud = CaptionCanvasBuilder.Build(config.question, config.title, string.Empty,
                                                        CaptionCanvasBuilder.Style.Dark);
            StyleCaptionCanvas(hud);

            BrachistochronePresenter presenter = runnerObject.AddComponent<BrachistochronePresenter>();
            presenter.Bind(simulation, hud, new BrachistochronePresenter.SceneRig
            {
                camera = camera,
                circle = circle,
                glow = glow,
                square = square,
                ring = ring,
                checker = checker
            });

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            string roundList = string.Empty;
            for (int i = 0; i < config.RoundCount; i++)
            {
                BrachRound r = config.rounds[i];
                string names = string.Empty;
                for (int t = 0; t < r.tracks.Length; t++)
                {
                    names += (t > 0 ? ", " : string.Empty) + r.tracks[t].label;
                }

                roundList += $"    {i + 1}. {r.tracks.Length} tracks ({names}), B ({r.finishX:0.##}, {r.finishY:0.##}), " +
                             $"\"{r.caption}\" -> \"{r.result}\"\n";
            }

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  config {ConfigPath}\n" +
                $"  {config.RoundCount} rounds, series {config.SeriesSeconds:0.0}s (estimate)\n" +
                roundList +
                "  Tracks, balls, labels, gate and flag are created on Play. Seed is random per Play and logged at start (cosmetic only).");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>Keeps an existing asset so hand tuning survives; a fresh one starts from the code defaults.</summary>
        static BrachistochroneConfig EnsureConfig()
        {
            Directory.CreateDirectory("Assets/Settings/Configs");
            var existing = AssetDatabase.LoadAssetAtPath<BrachistochroneConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<BrachistochroneConfig>();
            config.intent = "brach-001 Fastest Path. One question (which ball wins?) asked five times as tracks are added " +
                            "and the finish moves; the cycloid wins every round. The seed is cosmetic only (palette start).";
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        // ---------------------------------------------------------------- HUD

        /// <summary>
        /// Question at 100 px (the hook, up from frame zero) with a small title under it. The bottom line is one
        /// bold line, 80-96 px (auto-fit, wraps to two lines only if it must), on a dark plate.
        /// </summary>
        static void StyleCaptionCanvas(CaptionHud hud)
        {
            foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                switch (text.name)
                {
                    case "Caption":
                        text.fontSize = 100f;
                        text.characterSpacing = 0f;
                        text.textWrappingMode = TextWrappingModes.Normal;
                        break;
                    case "Subline":
                        text.fontSize = 40f;
                        break;
                    case "Footer":
                        text.fontStyle = FontStyles.Bold;
                        text.color = Color.white;
                        text.alignment = TextAlignmentOptions.Center;
                        text.textWrappingMode = TextWrappingModes.Normal;
                        text.richText = true;
                        text.enableAutoSizing = true;
                        text.fontSizeMin = 80f;
                        text.fontSizeMax = 96f;
                        text.margin = new Vector4(24f, 8f, 24f, 8f);

                        var panel = (RectTransform)text.transform.parent;
                        panel.anchoredPosition = new Vector2(0f, BottomPanelInsetPx);
                        panel.sizeDelta = new Vector2(-60f, BottomPanelHeightPx);
                        var plate = panel.gameObject.AddComponent<Image>();
                        plate.sprite = GeneratedArt.RoundedRect();
                        plate.type = Image.Type.Sliced;
                        plate.color = new Color(0.02f, 0.03f, 0.06f, 0.62f);
                        plate.raycastTarget = false;
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- generated sprite

        /// <summary>A 6 x 4 checkered flag, 1.5 x 1 world units, point-filtered so the squares stay crisp.</summary>
        static Sprite EnsureCheckerSprite()
        {
            const string fileName = "BrachChecker_48x32.png";
            string path = $"{ArtFolder}/{fileName}";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(ArtFolder);
            const int width = 48;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool white = ((x / 8) + (y / 8)) % 2 == 0;
                    pixels[y * width + x] = white ? new Color32(250, 250, 250, 255) : new Color32(18, 18, 22, 255);
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
            importer.spritePixelsPerUnit = 32f; // 48 x 32 px = 1.5 x 1 units
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
