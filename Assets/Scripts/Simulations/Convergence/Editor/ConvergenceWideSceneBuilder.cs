using System.IO;
using System.Text;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.Convergence.EditorTools
{
    /// <summary>
    /// Builds the 16:9 comp-001 segment 4 scene: <c>Comp001_04_NeedleEllipse_Wide</c>. A very eccentric
    /// ellipse (0.95) spanning ~85% of the frame width, 600 rainbow balls, three rounds
    /// (meet, miss, meet). Reuses <see cref="ConvergenceSimulation"/> and <see cref="ConvergencePresenter"/>
    /// unchanged; the HUD is the wide SegmentCardPresenter, fed by <see cref="ConvergenceWideBinder"/>.
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Math Convergence (conv) ▸ Wide: Needle Ellipse (comp-001)</c>.
    /// </summary>
    static class ConvergenceWideSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Comp001_04_NeedleEllipse_Wide.unity";
        const string ConfigPath = "Assets/Settings/Configs/EllipseFocusConfig_Comp001_04.asset";
        const string CardPath = "Assets/Settings/SegmentCards/comp001_04.asset";
        const string LineMaterialPath = "Assets/Settings/Configs/ConvergenceLine.mat";

        const string Title = "Needle Ellipse";
        const string Hook = "600 BALLS LEAVE RED. HOW MANY HIT BLUE?";
        const string Explainer = "Every ball leaves one dot and, after one bounce, passes through the other dot. Move the first dot and they scatter.";

        // Ellipse: eccentricity 0.95 (c / a), semi-major axis a chosen so the full width is ~85% of the frame.
        const float Eccentricity = 0.95f;
        const float SemiMajor = 8.5f;
        const float ArenaWidthFraction = 0.85f;
        const float Speed = 8f;
        const int Balls = 600;
        const float TrailSeconds = 1.5f;

        [MenuItem("Simulation Lobby/2D Formats/Math Convergence (conv)/Wide: Needle Ellipse (comp-001)", false, 144)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[conv] Exit play mode before building the scene.");
                return;
            }

            Sprite circle = GeneratedArt.Circle();
            Sprite glow = GeneratedArt.SoftGlow();
            Sprite ring = GeneratedArt.RingSprite();
            EllipseFocusConfig config = EnsureConfig();
            Material lineMaterial = EnsureLineMaterial();

            SegmentCardConfig cardConfig = SegmentCardFactory.CreateConfigAsset(CardPath, 4, 10, Title, Hook, Explainer);

            // --- Framing: the ellipse spans ArenaWidthFraction of the 16:9 frame, centred ---
            float semiMinor = SemiMajor * Mathf.Sqrt(1f - Eccentricity * Eccentricity);
            float frameWidth = 2f * SemiMajor / ArenaWidthFraction;
            float orthographicSize = frameWidth / (2f * WideFrameLayout.Aspect(AspectMode.Wide));
            float frameHeight = orthographicSize * 2f;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera: near-black so the trails and caustic glow ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f, 0.012f, 0.025f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Boundary ---
            var boundaryObject = new GameObject("Boundary");
            LineRenderer boundary = boundaryObject.AddComponent<LineRenderer>();
            boundary.sharedMaterial = lineMaterial;
            boundary.sortingOrder = 1;
            boundary.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            boundary.receiveShadows = false;

            // --- Dots ---
            SpriteRenderer startDot = CreateSprite("Start Dot", circle);
            SpriteRenderer startRing = CreateSprite("Start Dot Ring", ring);
            SpriteRenderer targetDot = CreateSprite("Target Dot", circle);
            SpriteRenderer targetRing = CreateSprite("Target Dot Ring", ring);
            SpriteRenderer ghostRing = CreateSprite("Start Ghost Ring", ring);

            // --- Body template: inactive, cloned by the presenter (pool prewarmed to 600) ---
            var bodies = new GameObject("Bodies");
            var template = new GameObject("Body");
            template.transform.SetParent(bodies.transform, false);
            var bodyRenderer = template.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = circle;
            bodyRenderer.color = Color.white;
            bodyRenderer.sortingOrder = 10;

            var trail = template.AddComponent<TrailRenderer>();
            trail.emitting = true;
            trail.autodestruct = false;
            trail.alignment = LineAlignment.View;
            trail.time = TrailSeconds;
            trail.minVertexDistance = 0.03f;
            trail.widthMultiplier = 0.1f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);
            trail.sortingOrder = 9;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var trailGradient = new Gradient();
            trailGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = trailGradient;
            trail.sharedMaterial = lineMaterial;
            template.SetActive(false);

            // --- Caustic layer (painted during the last round, shown at the end) ---
            var causticObject = new GameObject("Caustic");
            SpriteRenderer causticRenderer = causticObject.AddComponent<SpriteRenderer>();

            // --- Runner + simulation + presenter ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            ConvergenceSimulation simulation = runnerObject.AddComponent<ConvergenceSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            ConvergencePresenter presenter = runnerObject.AddComponent<ConvergencePresenter>();
            // No portrait scoreboard (hud null): the wide card presenter below shows the text.
            presenter.Bind(simulation, null, template, boundary, startDot, startRing, targetDot, targetRing, ghostRing,
                           glow, ring, null, null);

            var caustic = runnerObject.AddComponent<CausticAccumulator>();
            caustic.Bind(simulation, causticRenderer, new Vector2(SemiMajor + 0.6f, semiMinor + 0.6f), 100f, 16f);

            // --- Wide HUD: hook top-left, counter bottom-left (the ellipse spans the right margin), caption bottom ---
            SegmentCardPresenter card = SegmentCardFactory.CreateInScene(cardConfig, AspectMode.Wide);
            Image counterPlate = MoveCounterBottomLeft(card);
            var binder = runnerObject.AddComponent<ConvergenceWideBinder>();
            binder.Bind(simulation, card, counterPlate);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var rounds = new StringBuilder();
            for (int i = 0; i < config.RoundCount; i++)
            {
                ConvergenceRoundInfo info = config.GetRound(i);
                rounds.AppendLine(
                    $"    {i + 1}. \"{info.caption}\" -> \"{info.result}\": expect {info.expected}, run {info.runSeconds:0.0}s, " +
                    $"{config.RoundBallCount(i)} balls, trail {info.trailSeconds:0.0}s, {config.DescribeRound(i)}");
            }

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  config {ConfigPath} (delete it to reset to the builder's defaults)\n" +
                $"  segment card {CardPath}\n" +
                $"  {config.RoundCount} rounds, series {config.SeriesSeconds:0.0}s (estimated from config; includes the 7.5s card + explainer lead-in)\n" +
                rounds +
                $"  framing: ortho {orthographicSize:0.00} (16:9 view {frameWidth:0.0} x {frameHeight:0.0} units), ellipse {SemiMajor * 2f:0.0} x {semiMinor * 2f:0.0} " +
                $"= {SemiMajor * 2f / frameWidth * 100f:0}% of frame width, e={Eccentricity:0.00}, foci at +-{SemiMajor * Eccentricity:0.00}\n" +
                "  Seed is random per Play and logged at start.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>
        /// The ellipse is wider than the right-margin counter allows, so the counter plate moves to the
        /// bottom-left corner, just above the caption strip (clear of the thin ellipse). Returns its plate.
        /// </summary>
        static Image MoveCounterBottomLeft(SegmentCardPresenter card)
        {
            Transform plate = card.transform.Find("RightMargin");
            if (plate == null)
            {
                Debug.LogWarning("[conv] SegmentCardPresenter has no RightMargin panel; counter left where the presenter put it.");
                return null;
            }

            var rect = (RectTransform)plate;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(36f, 104f + 14f);
            rect.sizeDelta = new Vector2(620f, 110f);
            return plate.GetComponent<Image>();
        }

        static EllipseFocusConfig EnsureConfig()
        {
            Directory.CreateDirectory("Assets/Settings/Configs");
            var existing = AssetDatabase.LoadAssetAtPath<EllipseFocusConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<EllipseFocusConfig>();
            config.intent = "comp-001 segment 4, Needle Ellipse (wide 16:9). One question (600 balls from red: how many hit blue?) " +
                            "in a very thin ellipse (e=0.95). Round 1 red on the focus: all hit. Round 2 red nudged along the axis: " +
                            "most miss. Round 3 red back: all hit again. Verdict is first pass only. Seed is cosmetic only.";
            config.title = Title.ToUpperInvariant();
            config.question = Hook;
            config.endTitle = string.Empty;
            config.holdCaption = "GUESS?";
            config.counterLabel = "HIT BLUE";
            config.endCard = "A BOUNCE OFF AN ELLIPSE SENDS FOCUS TO FOCUS.";
            config.idealSpotLabel = string.Empty;

            config.ballCount = Balls;
            config.ballRadius = 0.07f;
            config.hueSpan = 1f;
            config.showBodiesDuringHold = false;
            config.showStartPoint = true;
            config.maxAngleOffsetDegrees = 1f;

            // The first hold covers the segment card (1.5s) and the explainer (6s) so the action starts after them.
            config.firstHoldSeconds = 7.5f;
            config.preLaunchHoldSeconds = 0.8f;
            config.transitionSeconds = 1.2f;
            config.resultHoldSeconds = 1.8f;
            config.endCardSeconds = 5f;

            config.rounds = new[]
            {
                Round("WATCH BLUE", "ALL {n}. AT THE SAME INSTANT.", ConvergenceAnswer.Meet, 8f, Vector2.zero),
                Round("NOW MOVE RED A LITTLE...", "ONLY {n} HIT.", ConvergenceAnswer.Miss, 10f, new Vector2(-1.2f, 0f)),
                Round("PUT IT BACK...", "ALL {n} AGAIN.", ConvergenceAnswer.Meet, 6f, Vector2.zero)
            };

            config.fixedTimestep = 1f / 120f;
            config.maxDurationSeconds = config.SeriesSeconds;
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static EllipseFocusConfig.EllipseRound Round(string caption, string result, ConvergenceAnswer expected,
            float runSeconds, Vector2 startOffset)
        {
            float semiMinor = SemiMajor * Mathf.Sqrt(1f - Eccentricity * Eccentricity);
            return new EllipseFocusConfig.EllipseRound
            {
                info = new ConvergenceRoundInfo
                {
                    caption = caption, result = result, expected = expected,
                    runSeconds = runSeconds, trailSeconds = TrailSeconds, ballCount = Balls
                },
                halfWidth = SemiMajor, halfHeight = semiMinor, startOffset = startOffset, targetOffset = Vector2.zero, speed = Speed
            };
        }

        static SpriteRenderer CreateSprite(string name, Sprite sprite)
        {
            var go = new GameObject(name);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return renderer;
        }

        static Material EnsureLineMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(LineMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var material = new Material(Shader.Find("Sprites/Default")) { name = "ConvergenceLine" };
            AssetDatabase.CreateAsset(material, LineMaterialPath);
            return material;
        }
    }
}
