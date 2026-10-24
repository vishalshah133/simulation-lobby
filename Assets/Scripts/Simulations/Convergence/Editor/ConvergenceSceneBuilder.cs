using System.IO;
using System.Text;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.Convergence.EditorTools
{
    /// <summary>
    /// Builds the <c>conv</c> scenes from code: <c>Convergence_EllipseFocus_2D</c> (conv-001),
    /// <c>Convergence_EllipseStretch_2D</c> (conv-002) and <c>Convergence_ParabolaFocus_2D</c> (conv-003).
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Math Convergence (conv)</c>.
    /// Re-running replaces the scene. All share one build path and differ only by <see cref="Preset"/>.
    /// </summary>
    static class ConvergenceSceneBuilder
    {
        /// <summary>What differs between the conv videos: where they live and how a fresh config is made.</summary>
        readonly struct Preset
        {
            public readonly string ScenePath;
            public readonly string ConfigPath;
            public readonly System.Func<ConvergenceConfig> CreateFresh;
            public readonly System.Func<ConvergenceConfig, bool> IsStale;

            public Preset(string scenePath, string configPath, System.Func<ConvergenceConfig> createFresh,
                          System.Func<ConvergenceConfig, bool> isStale)
            {
                ScenePath = scenePath;
                ConfigPath = configPath;
                CreateFresh = createFresh;
                IsStale = isStale;
            }
        }

        const string LineMaterialPath = "Assets/Settings/Configs/ConvergenceLine.mat";

        // Scoreboard geometry at the 1080x1920 reference (ScoreboardCanvasBuilder): each panel is 420 px
        // tall and sits 28 px in from its screen edge.
        const float ReferenceHeight = 1920f;
        const float ReferenceWidth = 1080f;
        const float TopPanelPixels = 28f + 420f;
        const float BottomPanelPixels = 28f + 420f;

        /// <summary>Clear world space kept between the tallest ellipse and each panel / side edge.</summary>
        const float Margin = 0.4f;

        [MenuItem("Simulation Lobby/2D Formats/Math Convergence (conv)/Ellipse Focus (conv-001)", false, 141)]
        static void BuildEllipseFocus()
        {
            Build(new Preset(
                "Assets/Scenes/Convergence_EllipseFocus_2D.unity",
                "Assets/Settings/Configs/EllipseFocusConfig_001.asset",
                CreateConv001,
                // Only the first-cut asset (4 rounds, no result lines) is replaced; none of its values carry over.
                existing => !(existing is EllipseFocusConfig ellipse) || ellipse.rounds == null ||
                            ellipse.rounds.Length == 0 || string.IsNullOrEmpty(ellipse.rounds[0].info.result)));
        }

        [MenuItem("Simulation Lobby/2D Formats/Math Convergence (conv)/Ellipse Stretch (conv-002)", false, 142)]
        static void BuildEllipseStretch()
        {
            Build(new Preset(
                "Assets/Scenes/Convergence_EllipseStretch_2D.unity",
                "Assets/Settings/Configs/EllipseFocusConfig_002.asset",
                CreateConv002,
                existing => false));
        }

        [MenuItem("Simulation Lobby/2D Formats/Math Convergence (conv)/Parabola Focus (conv-003)", false, 143)]
        static void BuildParabolaFocus()
        {
            Build(new Preset(
                "Assets/Scenes/Convergence_ParabolaFocus_2D.unity",
                "Assets/Settings/Configs/ParabolaMirrorConfig_003.asset",
                CreateConv003,
                existing => false));
        }

        static void Build(Preset preset)
        {
            string ScenePath = preset.ScenePath;
            string ConfigPath = preset.ConfigPath;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[conv] Exit play mode before building the scene.");
                return;
            }

            Sprite circle = GeneratedArt.Circle();
            Sprite glow = GeneratedArt.SoftGlow();
            Sprite ring = GeneratedArt.RingSprite();
            ConvergenceConfig config = EnsureConfig(preset);
            Material lineMaterial = EnsureLineMaterial();

            // --- Framing: derive the orthographic size from the rounds and the HUD panels ---
            // Extents come from each round's kernel: the sampled outline plus the start and target points.
            float maxHalfHeight = 0f;
            float maxHalfWidth = 0f;
            var outlineScratch = new System.Collections.Generic.List<Kernel.Double2>(ConvergenceSimulation.OutlineSamples);
            for (int i = 0; i < config.RoundCount; i++)
            {
                Kernel.IConvergenceKernel kernel = config.CreateKernel(i);
                kernel.SampleOutline(ConvergenceSimulation.OutlineSamples, outlineScratch);
                for (int s = 0; s < outlineScratch.Count; s++)
                {
                    maxHalfWidth = Mathf.Max(maxHalfWidth, Mathf.Abs((float)outlineScratch[s].X));
                    maxHalfHeight = Mathf.Max(maxHalfHeight, Mathf.Abs((float)outlineScratch[s].Y));
                }

                maxHalfWidth = Mathf.Max(maxHalfWidth, Mathf.Abs((float)kernel.Start.X), Mathf.Abs((float)kernel.Target.X));
                maxHalfHeight = Mathf.Max(maxHalfHeight, Mathf.Abs((float)kernel.Start.Y), Mathf.Abs((float)kernel.Target.Y));
            }

            // The gap between the panels is (1920 - top - bottom) px of a 1920 px frame that spans 2*size
            // world units, so it holds gapFraction * 2 * size units. Solve for the size that fits the
            // tallest ellipse plus a margin top and bottom, rounded up to the next 0.1.
            float gapFraction = (ReferenceHeight - TopPanelPixels - BottomPanelPixels) / ReferenceHeight;
            float neededHeight = 2f * (maxHalfHeight + Margin);
            float orthographicSize = Mathf.Ceil(neededHeight / (2f * gapFraction) * 10f) / 10f;

            // Panels are symmetric today; if they ever differ, shift the camera so the arena (at the
            // origin) stays centred in the clear band.
            float bandCentrePixels = (BottomPanelPixels - TopPanelPixels) * 0.5f;
            float cameraYOffset = -bandCentrePixels / ReferenceHeight * 2f * orthographicSize;

            float halfViewWidth = orthographicSize * ReferenceWidth / ReferenceHeight;
            float clearTall = gapFraction * 2f * orthographicSize;

            // The widest ellipse must fit the portrait frame with the same margin as the tallest has.
            float widthSlack = halfViewWidth - maxHalfWidth;
            bool widthFits = widthSlack >= Margin;
            if (!widthFits)
            {
                Debug.LogWarning($"[conv] Widest ellipse {maxHalfWidth * 2f:0.0} units leaves under {Margin} spare each side of the 9:16 view " +
                                 $"({halfViewWidth * 2f:0.0} wide). Reduce a round's halfWidth.");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, cameraYOffset, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.09f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient backdrop ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(0f, cameraYOffset, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(orthographicSize * 1.4f, orthographicSize * 2.2f);
            backdrop.particleCount = 90;
            backdrop.maxAlpha = 0.13f;
            backdrop.tint = new Color(0.55f, 0.65f, 1f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- Boundary: the presenter writes the points, colour and width ---
            var boundaryObject = new GameObject("Boundary");
            LineRenderer boundary = boundaryObject.AddComponent<LineRenderer>();
            boundary.sharedMaterial = lineMaterial;
            boundary.sortingOrder = 1;
            boundary.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            boundary.receiveShadows = false;

            // --- Dots: start (red) and target (blue) discs with white rings, plus the ghost ring.
            //     The presenter places, colours, sizes and sorts them. ---
            SpriteRenderer startDot = CreateSprite("Start Dot", circle);
            SpriteRenderer startRing = CreateSprite("Start Dot Ring", ring);
            SpriteRenderer targetDot = CreateSprite("Target Dot", circle);
            SpriteRenderer targetRing = CreateSprite("Target Dot Ring", ring);
            SpriteRenderer ghostRing = CreateSprite("Start Ghost Ring", ring);
            TextMeshPro ghostLabel = CreateGhostLabel();

            // --- Body template: inactive, cloned by the presenter ---
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
            trail.time = 0.4f;
            trail.minVertexDistance = 0.02f;
            trail.widthMultiplier = 0.1f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.sortingOrder = 9;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var trailGradient = new Gradient();
            trailGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = trailGradient;
            trail.sharedMaterial = lineMaterial;
            template.SetActive(false);

            // --- Runner + simulation + presenter ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            ConvergenceSimulation simulation = runnerObject.AddComponent<ConvergenceSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;
            BackdropThemeLibrary.AddStyler(backdropObject, runner, camera, backdrop);

            // Reuses the scoreboard HUD (top question, bottom counter and result line). The presenter hides
            // its versus row and round label; no score in this format.
            VersusScoreboardHud hud = ScoreboardCanvasBuilder.Build(
                new ScoreboardCanvasBuilder.Labels
                {
                    title = config.title,
                    question = config.question,
                    leftName = string.Empty,
                    rightName = string.Empty,
                    round = string.Empty,
                    counter = $"{config.counterLabel}  0 / {config.ballCount}"
                },
                ScoreboardCanvasBuilder.Palette.Default);
            hud.SetScoreVisible(false);

            ConvergencePresenter presenter = runnerObject.AddComponent<ConvergencePresenter>();
            presenter.Bind(simulation, hud, template, boundary, startDot, startRing, targetDot, targetRing, ghostRing,
                           glow, ring, null, ghostLabel);

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
                $"  config {ConfigPath}\n" +
                $"  {config.RoundCount} rounds, {config.ballCount} bodies, series {config.SeriesSeconds:0.0}s, HUD: question + caption/result + counter, no score\n" +
                rounds +
                $"  framing: ortho size {orthographicSize:0.0} (9:16 view {halfViewWidth * 2f:0.0} x {orthographicSize * 2f:0.0} units), " +
                $"clear band between panels {clearTall:0.0} units for tallest ellipse {maxHalfHeight * 2f:0.0} + {Margin} margin each side, " +
                $"widest {maxHalfWidth * 2f:0.0} of {halfViewWidth * 2f:0.0} ({(widthFits ? "fits" : "TOO WIDE")}, {widthSlack:0.0} units spare each side), camera y {cameraYOffset:0.00}\n" +
                "  Seed is random per Play and logged at start.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>
        /// Keeps an existing asset so hand tuning survives; creates a fresh one only when none exists (or the
        /// preset calls the existing one stale). The topic defaults reach the asset only at creation.
        /// </summary>
        static ConvergenceConfig EnsureConfig(Preset preset)
        {
            Directory.CreateDirectory("Assets/Settings/Configs");
            var existing = AssetDatabase.LoadAssetAtPath<ConvergenceConfig>(preset.ConfigPath);
            if (existing != null)
            {
                // An asset of the wrong concrete type is stale too: the preset rebuilds it.
                ConvergenceConfig probe = preset.CreateFresh();
                bool wrongType = existing.GetType() != probe.GetType();
                Object.DestroyImmediate(probe);

                if (!wrongType && !preset.IsStale(existing))
                {
                    return existing;
                }

                AssetDatabase.DeleteAsset(preset.ConfigPath);
            }

            ConvergenceConfig config = preset.CreateFresh();
            config.fixedTimestep = 1f / 120f;
            config.maxDurationSeconds = config.SeriesSeconds;
            AssetDatabase.CreateAsset(config, preset.ConfigPath);
            return config;
        }

        static ConvergenceConfig CreateConv001()
        {
            var config = ScriptableObject.CreateInstance<EllipseFocusConfig>();
            config.intent = "conv-001 ellipse focus. One question (120 balls from red: how many hit blue?), two " +
                            "visible dots, one visible twist. Start on the focus: all hit. Slide red 0.7 sideways: " +
                            "only a handful hit. Slide it back: all hit again. Only the focus works. The seed is " +
                            "cosmetic only: colours and firing-angle jitter vary, the maths is exact.";
            return config;
        }

        /// <summary>
        /// conv-002: the same question nine times, one visible change per round. Round 5's dots stay where
        /// round 4's foci were: c = sqrt(hh^2 - hw^2), so 4.9054 (3.0 x 5.75) vs 5.3910 (2.0 x 5.75).
        /// </summary>
        static ConvergenceConfig CreateConv002()
        {
            var config = ScriptableObject.CreateInstance<EllipseFocusConfig>();
            config.intent = "conv-002 ellipse stretch. One repeated question (how many balls from red hit blue?), nine " +
                            "rounds, one visible change each: move red (miss), put it back, stretch the ellipse taller " +
                            "(still all hit), stretch it again while the dots stay put (miss), squash it wide, nearly a " +
                            "line, a circle, then 600 balls. Only the foci work. Seed is cosmetic only.";
            config.question = "HOW MANY BALLS FROM RED HIT BLUE?";
            config.holdCaption = "GUESS?";
            config.endCard = "ONLY THE FOCUS WORKS. EVEN WHEN YOU STRETCH IT.";
            config.counterLabel = "HIT BLUE";
            config.firstHoldSeconds = 1.5f;
            config.preLaunchHoldSeconds = 1.0f;
            config.transitionSeconds = 1.5f;
            config.resultHoldSeconds = 1.5f;
            config.endCardSeconds = 3f;

            // Round 5: foci at +-c. Dots stay where round 4's foci were (+-c4), so the offsets are c4 - c5.
            double c4 = System.Math.Sqrt(5.75 * 5.75 - 3.0 * 3.0);
            double c5 = System.Math.Sqrt(5.75 * 5.75 - 2.0 * 2.0);
            float dotOffset = (float)(c5 - c4);

            config.rounds = new[]
            {
                Round("WATCH BLUE", "ALL {n}. AT THE SAME TIME.", ConvergenceAnswer.Meet, 2.08f, 0.4f, 0, 3.75f, 5f, Vector2.zero, Vector2.zero),
                Round("NOW MOVE RED A LITTLE...", "ONLY {n} HIT.", ConvergenceAnswer.Miss, 6f, 1.5f, 0, 3.75f, 5f, new Vector2(0.7f, 0f), Vector2.zero),
                Round("PUT IT BACK...", "ALL {n} AGAIN.", ConvergenceAnswer.Meet, 2.08f, 0.4f, 0, 3.75f, 5f, Vector2.zero, Vector2.zero),
                Round("STRETCH IT TALLER...", "STILL ALL {n}!", ConvergenceAnswer.Meet, 2.38f, 0.4f, 0, 3f, 5.75f, Vector2.zero, Vector2.zero),
                Round("STRETCH AGAIN... BUT THE DOTS STAY", "ONLY {n} HIT.", ConvergenceAnswer.Miss, 2.45f, 0.6f, 0, 2f, 5.75f, new Vector2(0f, -dotOffset), new Vector2(0f, dotOffset)),
                Round("SQUASH IT WIDE...", "ALL {n}. SIDEWAYS TOO.", ConvergenceAnswer.Meet, 1.92f, 0.4f, 0, 4.6f, 2.6f, Vector2.zero, Vector2.zero),
                Round("ALMOST A LINE...", "STILL ALL {n}!", ConvergenceAnswer.Meet, 2.38f, 0.4f, 0, 1f, 5.75f, Vector2.zero, Vector2.zero),
                Round("MAKE IT A CIRCLE...", "ALL {n} BACK TO THE MIDDLE.", ConvergenceAnswer.Meet, 1.58f, 0.4f, 0, 3.75f, 3.75f, Vector2.zero, Vector2.zero),
                Round("NOW 600 BALLS...", "ALL {n}!", ConvergenceAnswer.Meet, 2.08f, 0f, 600, 3.75f, 5f, Vector2.zero, Vector2.zero)
            };
            return config;
        }

        /// <summary>
        /// conv-003: a rainbow row of balls falls into a parabola, every one crosses the focus at the same
        /// instant, then rises back out as a flat vertical line. Rounds are the config's own defaults
        /// (5, 10, 50, 100, 500, 1000 balls); only the shell text and pacing are set here.
        /// </summary>
        static ConvergenceConfig CreateConv003()
        {
            var config = ScriptableObject.CreateInstance<ParabolaMirrorConfig>();
            config.intent = "conv-003 parabola focus. One repeated question (how many balls hit the dot?): a rainbow row " +
                            "falls into a parabola, all cross the focus at the same instant, then rise back out as a flat " +
                            "vertical line. 5 balls, then 10, 50, 100, 500, 1000. Seed is cosmetic only.";
            config.title = "THE PERFECT CURVE";
            config.endTitle = "IT'S CALLED A PARABOLA";
            config.question = "HOW MANY BALLS WILL HIT THE DOT?";
            config.holdCaption = string.Empty;
            config.counterLabel = "HIT THE DOT";
            config.endCard = "EVERY PATH IS THE SAME LENGTH. THE DOT IS THE FOCUS.";
            config.idealSpotLabel = string.Empty;
            config.ballCount = 5;
            config.ballRadius = 0.1f;
            config.hueSpan = 0.85f;
            config.showBodiesDuringHold = true;
            config.showStartPoint = false;
            config.maxAngleOffsetDegrees = 0f;
            config.firstHoldSeconds = 2.0f;
            config.preLaunchHoldSeconds = 1.5f;
            config.transitionSeconds = 0.5f;
            config.resultHoldSeconds = 1.4f;
            config.endCardSeconds = 4f;
            return config;
        }

        static EllipseFocusConfig.EllipseRound Round(string caption, string result, ConvergenceAnswer expected,
            float runSeconds, float trailSeconds, int balls, float hw, float hh, Vector2 startOffset, Vector2 targetOffset)
        {
            return new EllipseFocusConfig.EllipseRound
            {
                info = new ConvergenceRoundInfo
                {
                    caption = caption, result = result, expected = expected,
                    runSeconds = runSeconds, trailSeconds = trailSeconds, ballCount = balls
                },
                halfWidth = hw, halfHeight = hh, startOffset = startOffset, targetOffset = targetOffset, speed = 5f
            };
        }

        /// <summary>
        /// World-space decal beside the ghost ring ("MAGIC POINT"). It belongs to an arena marker, so it's a
        /// TextMeshPro decal per CLAUDE.md, not HUD text. Fixed size (the presenter measures it once and
        /// picks the side of the ring that fits). Hidden until the ghost shows.
        /// </summary>
        static TextMeshPro CreateGhostLabel()
        {
            var go = new GameObject("Ghost Label");
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tmp.rectTransform.sizeDelta = new Vector2(3.2f, 0.6f);
            tmp.text = "MAGIC POINT";
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableAutoSizing = false;
            // Untested size: check it reads at 480px wide on first Play and adjust here.
            tmp.fontSize = 4.5f;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            tmp.sortingOrder = 6;
            tmp.enabled = false;
            return tmp;
        }

        static SpriteRenderer CreateSprite(string name, Sprite sprite)
        {
            var go = new GameObject(name);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return renderer;
        }

        /// <summary>Unlit alpha-capable material shared by the boundary line and the body trails.</summary>
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
