using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Simulations.TwinDrop.Kernel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.TwinDrop.EditorTools
{
    /// <summary>
    /// Builds the <c>twin</c> scene (<c>TwinDrop_2D</c>, twin-001) from code.
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Twin Drop (twin) ▸ Twin Drop (twin-001)</c>.
    /// Re-running replaces the scene and keeps an existing config asset (delete it to reset to the defaults).
    /// </summary>
    static class TwinDropSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/TwinDrop_2D.unity";
        const string ConfigPath = "Assets/Settings/Configs/TwinDropConfig_001.asset";
        const string LineMaterialPath = "Assets/Settings/Configs/TwinDropLine.mat";

        // Panels sit where they always do (title and tally at the top, gap line and verdict at the bottom), but
        // the bottom panel is lifted so the last 15% of the frame (288 px at 1080x1920) stays empty:
        // Instagram and Shorts draw comments and captions there. The board is shortened to fit between them.
        const float ReferenceHeight = 1920f;
        const float ReferenceWidth = 1080f;
        const float TopPanelHeight = 300f;
        const float BottomPanelHeight = 190f;
        const float PanelInset = 20f;
        const float BottomBlank = 288f;
        const float Margin = 0.45f;

        [MenuItem("Simulation Lobby/2D Formats/Twin Drop (twin)/Twin Drop (twin-001)", false, 152)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[twin] Exit play mode before building the scene.");
                return;
            }

            Sprite circle = GeneratedArt.Circle();
            Sprite glow = GeneratedArt.SoftGlow();
            Sprite square = GeneratedArt.Square();
            TwinDropConfig config = EnsureConfig();
            Material lineMaterial = EnsureLineMaterial();

            // --- Framing: the clear band between the trimmed panels must hold the whole board ---
            float top = PanelInset + TopPanelHeight;
            float bottom = BottomBlank + BottomPanelHeight;
            float gapFraction = (ReferenceHeight - top - bottom) / ReferenceHeight;
            float boardTop = config.topRowY + config.releaseHeight + 0.7f;
            float boardBottom = -0.3f;
            float boardHeight = boardTop - boardBottom;
            float orthographicSize = Mathf.Ceil((boardHeight + 2f * Margin) / (2f * gapFraction) * 10f) / 10f;
            float widthNeeded = (config.halfWidth + Margin) * ReferenceHeight / ReferenceWidth;
            orthographicSize = Mathf.Max(orthographicSize, Mathf.Ceil(widthNeeded * 10f) / 10f);
            float halfViewWidth = orthographicSize * ReferenceWidth / ReferenceHeight;
            float bandCentrePixels = (bottom - top) * 0.5f;
            float cameraY = (boardTop + boardBottom) * 0.5f - bandCentrePixels / ReferenceHeight * 2f * orthographicSize;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, cameraY, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.035f, 0.07f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(0f, cameraY, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(orthographicSize * 1.4f, orthographicSize * 2.2f);
            backdrop.particleCount = 70;
            backdrop.maxAlpha = 0.1f;
            backdrop.tint = new Color(0.6f, 0.7f, 1f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- Board: floor panel, wall lines, pegs, slot dividers and slot highlights ---
            var board = new GameObject("Board");
            float boardCentreY = (config.topRowY + config.releaseHeight + 0.7f) * 0.5f;
            SpriteRenderer floor = NewSprite("Panel", square, board.transform, -5);
            floor.transform.position = new Vector3(0f, boardCentreY, 0f);
            floor.transform.localScale = new Vector3(config.halfWidth * 2f, config.topRowY + config.releaseHeight + 0.7f, 1f);
            floor.color = new Color(0.065f, 0.08f, 0.14f, 1f);

            float wallTop = config.topRowY + config.releaseHeight + 0.7f;
            NewLine("Left Wall", board.transform, new[] { new Vector3(-config.halfWidth, 0f, 0f), new Vector3(-config.halfWidth, wallTop, 0f) }, lineMaterial, 0.07f, new Color(0.55f, 0.8f, 1f, 1f), 2);
            NewLine("Right Wall", board.transform, new[] { new Vector3(config.halfWidth, 0f, 0f), new Vector3(config.halfWidth, wallTop, 0f) }, lineMaterial, 0.07f, new Color(0.55f, 0.8f, 1f, 1f), 2);
            NewLine("Floor Line", board.transform, new[] { new Vector3(-config.halfWidth, 0f, 0f), new Vector3(config.halfWidth, 0f, 0f) }, lineMaterial, 0.07f, new Color(0.55f, 0.8f, 1f, 1f), 2);

            PegBoardKernel kernel = config.CreateKernel();
            for (int i = 0; i < kernel.PegCount; i++)
            {
                SpriteRenderer peg = NewSprite("Peg", circle, board.transform, 3);
                peg.transform.position = new Vector3((float)kernel.PegX(i), (float)kernel.PegY(i), 0f);
                peg.transform.localScale = Vector3.one * (config.pegRadius * 2f);
                peg.color = new Color(0.8f, 0.86f, 1f, 1f);
            }

            float binWidth = config.halfWidth * 2f / config.bins;
            var binGlow = new SpriteRenderer[config.bins];
            for (int b = 0; b < config.bins; b++)
            {
                float cx = -config.halfWidth + (b + 0.5f) * binWidth;
                SpriteRenderer g = NewSprite($"Slot {b + 1} Glow", square, board.transform, 1);
                g.transform.position = new Vector3(cx, 0.55f, 0f);
                g.transform.localScale = new Vector3(binWidth - 0.08f, 1.1f, 1f);
                g.enabled = false;
                binGlow[b] = g;

                if (b > 0)
                {
                    float dx = -config.halfWidth + b * binWidth;
                    NewLine($"Divider {b}", board.transform, new[] { new Vector3(dx, 0f, 0f), new Vector3(dx, 1.1f, 0f) }, lineMaterial, 0.05f, new Color(0.55f, 0.8f, 1f, 0.7f), 2);
                }
            }

            // --- The twins: blue on top, red slightly bigger behind ---
            var balls = new SpriteRenderer[2];
            var trails = new TrailRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                SpriteRenderer sr = NewSprite(i == 0 ? "Blue Twin" : "Red Twin", circle, null, i == 0 ? 12 : 11);
                sr.enabled = false;
                var trail = sr.gameObject.AddComponent<TrailRenderer>();
                trail.emitting = false;
                trail.autodestruct = false;
                trail.alignment = LineAlignment.View;
                trail.time = 0.5f;
                trail.minVertexDistance = 0.03f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.sortingOrder = i == 0 ? 9 : 8;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.sharedMaterial = lineMaterial;
                balls[i] = sr;
                trails[i] = trail;
            }

            SpriteRenderer releaseGlow = NewSprite("Release Glow", glow, null, 8);
            releaseGlow.enabled = false;

            // --- Runner + simulation + presenter ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            TwinDropSimulation simulation = runnerObject.AddComponent<TwinDropSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            VersusScoreboardHud hud = ScoreboardCanvasBuilder.Build(
                new ScoreboardCanvasBuilder.Labels
                {
                    title = config.title,
                    question = config.question,
                    leftName = config.sameName,
                    rightName = config.apartName,
                    round = string.Empty,
                    counter = string.Empty
                },
                new ScoreboardCanvasBuilder.Palette
                {
                    leftAccent = new Color(0.35f, 1f, 0.55f, 1f),
                    rightAccent = new Color(1f, 0.45f, 0.5f, 1f)
                });
            TrimPanels(hud.transform);

            TwinDropPresenter presenter = runnerObject.AddComponent<TwinDropPresenter>();
            presenter.Bind(simulation, hud, balls, trails, binGlow, releaseGlow);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);

            Debug.Log(
                $"Built {ScenePath}\n  config {ConfigPath}\n" +
                $"  {config.RoundCount} rounds, {config.TotalPairs} pairs, {config.bins} slots, {kernel.PegCount} pegs; expect about 80-95s (worst case {config.WorstCaseSeconds:0}s)\n" +
                $"  framing: ortho size {orthographicSize:0.0}, 9:16 view {halfViewWidth * 2f:0.0} x {orthographicSize * 2f:0.0} units, board {config.halfWidth * 2f:0.0} x {boardHeight:0.0}\n" +
                "  Seed is random per Play and logged at start; it only picks where each pair is released. Record it with the published video.");
        }

        /// <summary>
        /// Trims the two scoreboard panels to the text this scene shows (title, tally, gap line, verdict) and
        /// lifts the bottom one clear of the bottom 15%. The second title line and the pair counter are hidden.
        /// </summary>
        static void TrimPanels(Transform hudRoot)
        {
            Transform top = hudRoot.Find("TopPanel");
            Transform bottom = hudRoot.Find("BottomPanel");
            if (top == null || bottom == null)
            {
                Debug.LogWarning("[twin] Scoreboard panels not found; leaving the default layout.");
                return;
            }

            var topRect = (RectTransform)top;
            topRect.anchoredPosition = new Vector2(0f, -PanelInset);
            topRect.sizeDelta = new Vector2(topRect.sizeDelta.x, TopPanelHeight);
            var bottomRect = (RectTransform)bottom;
            bottomRect.anchoredPosition = new Vector2(0f, BottomBlank);
            bottomRect.sizeDelta = new Vector2(bottomRect.sizeDelta.x, BottomPanelHeight);

            Transform question = top.Find("Question");
            if (question != null)
            {
                question.gameObject.SetActive(false);
            }

            Transform counter = bottom.Find("Counter");
            if (counter != null)
            {
                counter.gameObject.SetActive(false);
            }

            foreach (var text in top.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (text.name == "LeftScore" || text.name == "RightScore")
                {
                    text.fontSize = 96f;
                }
            }

            // The verdict line shrinks to stay on one line, so a long caption can never wrap into the line above.
            foreach (var text in bottom.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (text.name == "Result")
                {
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 36f;
                    text.fontSizeMax = 72f;
                    text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                }
            }
        }

        static TwinDropConfig EnsureConfig()
        {
            Directory.CreateDirectory("Assets/Settings/Configs");
            var existing = AssetDatabase.LoadAssetAtPath<TwinDropConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<TwinDropConfig>();
            config.intent = "twin-001 butterfly effect as a peg board. Two marbles, blue and red, are released through the same board: " +
                            "first from exactly the same spot (always the same slot), then with a gap of 1 cm, a hair, a bacterium, " +
                            "a molecule and an atom. The tally shows SAME SLOT vs DIFFERENT. Seed only picks the release positions.";
            config.fixedTimestep = 1f / 120f;
            config.maxDurationSeconds = config.WorstCaseSeconds;
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static SpriteRenderer NewSprite(string name, Sprite sprite, Transform parent, int order)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        static void NewLine(string name, Transform parent, Vector3[] points, Material material, float width, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        static Material EnsureLineMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(LineMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var material = new Material(Shader.Find("Sprites/Default")) { name = "TwinDropLine" };
            AssetDatabase.CreateAsset(material, LineMaterialPath);
            return material;
        }
    }
}
