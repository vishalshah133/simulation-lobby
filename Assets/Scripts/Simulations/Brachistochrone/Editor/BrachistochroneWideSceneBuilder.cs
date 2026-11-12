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
    /// Builds the comp-001 #7 scene: <c>Comp001_07_Racetrack_Wide</c> (16:9, light blueprint backdrop).
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Brachistochrone (brach) ▸ Wide: Racetrack (comp-001)</c>.
    /// The scene holds the camera, runner, simulation, wide presenter and the segment-card HUD (re-skinned dark on
    /// light). Tracks, balls, grid and ladder are created on Play. An existing config asset is kept so hand tuning
    /// survives; delete it to regenerate from <see cref="BrachistochroneWidePreset"/>.
    /// </summary>
    static class BrachistochroneWideSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Comp001_07_Racetrack_Wide.unity";
        const string ConfigPath = "Assets/Settings/Configs/BrachistochroneConfig_Wide_001.asset";
        const string CardPath = "Assets/Settings/SegmentCards/comp001_07.asset";

        const string Explainer = "Every ball starts at the same spot and races to the flag down its own slide. " +
                                 "The curved slide wins, even though it is longer.";

        static readonly Color Paper = new Color(0.955f, 0.94f, 0.88f, 1f);
        static readonly Color Plate = new Color(0.97f, 0.95f, 0.88f, 0.92f);
        static readonly Color Ink = new Color(0.07f, 0.09f, 0.17f, 1f);
        static readonly Color Red = new Color(0.78f, 0.08f, 0.15f, 1f);

        [MenuItem("Simulation Lobby/2D Formats/Brachistochrone (brach)/Wide: Racetrack (comp-001)", false, 182)]
        static void BuildRacetrack()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[brach] Exit play mode before building the scene.");
                return;
            }

            BrachistochroneConfig config = EnsureConfig();

            Sprite circle = GeneratedArt.Circle();
            Sprite ring = GeneratedArt.RingSprite();
            Sprite square = GeneratedArt.Square();
            Sprite checker = BrachistochroneSceneBuilder.EnsureCheckerSprite();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(7f, -2.45f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = WideFrameLayout.OrthoSizeForArena(12.3f, 6.5f, AspectMode.Wide);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Paper;
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            BrachistochroneSimulation simulation = runnerObject.AddComponent<BrachistochroneSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            SegmentCardConfig card = SegmentCardFactory.CreateConfigAsset(CardPath, 7, 10, "Racetrack",
                                                                           config.question, Explainer);
            SegmentCardPresenter hud = SegmentCardFactory.CreateInScene(card, AspectMode.Wide);
            ApplyPaperTheme(hud);

            BrachistochroneWidePresenter presenter = runnerObject.AddComponent<BrachistochroneWidePresenter>();
            presenter.Bind(simulation, hud, new BrachistochroneWidePresenter.Rig
            {
                camera = camera,
                circle = circle,
                ring = ring,
                square = square,
                checker = checker
            });

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  config {ConfigPath}, card {CardPath}\n" +
                $"  {config.RoundCount} rounds, series ~{config.SeriesSeconds:0.0}s (ESTIMATE, not measured: run lengths come from the kernel)\n" +
                "  Tracks, balls, grid and finish ladder are created on Play. Seed is random per Play and logged at start (cosmetic only).");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        static BrachistochroneConfig EnsureConfig()
        {
            Directory.CreateDirectory("Assets/Settings/Configs");
            var existing = AssetDatabase.LoadAssetAtPath<BrachistochroneConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<BrachistochroneConfig>();
            BrachistochroneWidePreset.Apply(config);
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        /// <summary>
        /// The card presenter ships white-on-dark plates. Re-skin everything except the full-screen "Card" subtree to
        /// ink-on-paper, by name, without touching Presentation.
        /// </summary>
        static void ApplyPaperTheme(SegmentCardPresenter hud)
        {
            foreach (Image image in hud.GetComponentsInChildren<Image>(true))
            {
                if (InCard(image.transform, hud.transform))
                {
                    continue;
                }

                switch (image.name)
                {
                    case "TopBar":
                    case "RightMargin":
                    case "BottomStrip":
                    case "Explainer":
                        image.color = Plate;
                        break;
                    case "AccentBar":
                        image.color = Red;
                        break;
                }
            }

            foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                if (InCard(text.transform, hud.transform))
                {
                    continue;
                }

                switch (text.name)
                {
                    case "Hook":
                    case "Caption":
                    case "Body":
                        text.color = Ink;
                        break;
                    case "Counter":
                    case "Heading":
                        text.color = Red;
                        break;
                }
            }

            EditorUtility.SetDirty(hud.gameObject);
        }

        static bool InCard(Transform t, Transform root)
        {
            for (Transform p = t; p != null && p != root; p = p.parent)
            {
                if (p.name == "Card")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
