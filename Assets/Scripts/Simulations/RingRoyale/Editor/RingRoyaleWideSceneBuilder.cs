using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.RingRoyale.EditorTools
{
    /// <summary>
    /// Builds <c>Comp001_06_SixColours_Wide</c>: 16:9 "Six Colours" segment of comp-001. Six neon colours x 3
    /// marbles, a narrow x2 arc, a small starting EXIT, sudden death at 15s, a vertical share column in the
    /// right margin and a slow zoom on the last two colours. Additive: the 9:16 builder is untouched.
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Color Rumble (royale) ▸ Wide: Six Colours (comp-001)</c>.
    /// Re-running replaces the scene; an existing config asset is kept.
    /// </summary>
    static class RingRoyaleWideSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Comp001_06_SixColours_Wide.unity";
        const string ConfigPath = "Assets/Settings/Configs/RingRoyaleConfig_Comp001_06.asset";
        const string CardPath = "Assets/Settings/SegmentCards/comp001_06.asset";
        const string MaterialPath = "Assets/Settings/Configs/PerfectlyElastic.physicsMaterial2D";

        const string Title = "Six Colours";
        const string Hook = "WHICH OF 6 COLOURS SURVIVES THE RING?";
        const string Explainer = "Six colours of marbles spin in a ring. The gold arc clones any marble that hits it; the EXIT drops them out. Last colour left wins.";

        static readonly string[] ColourNames = { "RED", "YELLOW", "GREEN", "CYAN", "BLUE", "PINK" };
        static readonly string[] Glyphs = { "R", "Y", "G", "C", "B", "P" };

        // Six neon hues 60 degrees apart on the colour wheel, saturated and bright so each reads on black.
        static readonly Color[] Palette =
        {
            Color.HSVToRGB(0f / 360f, 0.88f, 1f),
            Color.HSVToRGB(58f / 360f, 0.95f, 1f),
            Color.HSVToRGB(125f / 360f, 0.85f, 1f),
            Color.HSVToRGB(185f / 360f, 0.9f, 1f),
            Color.HSVToRGB(232f / 360f, 0.75f, 1f),
            Color.HSVToRGB(305f / 360f, 0.85f, 1f)
        };

        static readonly Color WallColor = new Color(0.78f, 0.8f, 0.86f, 1f);
        static readonly Color RimColor = new Color(0.02f, 0.02f, 0.04f, 1f);

        [MenuItem("Simulation Lobby/2D Formats/Color Rumble (royale)/Wide: Six Colours (comp-001)", false, 196)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[royale] Exit play mode before building the scene.");
                return;
            }

            Sprite circle = GeneratedArt.Circle();
            Sprite square = GeneratedArt.Square();
            RingRoyaleConfig config = EnsureConfig();
            SegmentCardConfig cardConfig = SegmentCardFactory.CreateConfigAsset(CardPath, 10, 10, Title, Hook, Explainer);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ring + dressing sit centred on the origin; the camera shifts so the ring clears the right margin.
            float thickness = Mathf.Max(0.6f, config.ballSpeed * 4f * config.fixedTimestep / 0.5f);
            float outerRadius = config.ringRadius + thickness;
            float arenaSize = 2f * (outerRadius + 0.35f);
            float ortho = WideFrameLayout.OrthoSizeForArena(arenaSize, arenaSize, AspectMode.Wide);
            float cameraX = WideFrameLayout.ArenaCameraOffsetX(ortho, AspectMode.Wide);

            // --- Camera: pure black so the neon reads ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(cameraX, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = ortho;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.005f, 0.005f, 0.012f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient backdrop: faint, so it never competes with the marbles ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(cameraX, 0f, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(ortho * 2f * WideFrameLayout.Aspect(AspectMode.Wide) * 1.3f, ortho * 2.4f);
            backdrop.particleCount = 70;
            backdrop.maxAlpha = 0.08f;
            backdrop.tint = new Color(0.55f, 0.65f, 1f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- Ring: every segment built, the gap's switched off, so sudden death widens it live ---
            var boundaryObject = new GameObject("Ring");
            CircularBoundary2D boundary = boundaryObject.AddComponent<CircularBoundary2D>();
            boundary.radius = config.ringRadius;
            boundary.thickness = thickness;
            boundary.segmentCount = 360;
            boundary.keepGapSegments = true;
            boundary.gapDegrees = config.exitStartDegrees;
            boundary.gapCenterDegrees = config.exitCenterDegrees;
            boundary.wallSprite = square;
            boundary.wallColor = WallColor;
            boundary.wallSortingOrder = 0;
            boundary.Rebuild();

            float midRadius = boundary.radius + boundary.thickness * 0.5f;

            // --- Gold x2 arc: sprite segments overlaid on the wall, children of the ring so it rotates ---
            var x2Root = new GameObject("X2 Arc");
            x2Root.transform.SetParent(boundaryObject.transform, false);
            int x2Count = Mathf.Max(2, Mathf.RoundToInt(config.x2ArcDegrees));
            float x2Step = config.x2ArcDegrees / x2Count;
            var x2Segments = new SpriteRenderer[x2Count];
            for (int i = 0; i < x2Count; i++)
            {
                float angle = config.x2CenterDegrees - config.x2ArcDegrees * 0.5f + (i + 0.5f) * x2Step;
                float radians = angle * Mathf.Deg2Rad;
                var piece = new GameObject($"X2_{i:000}");
                piece.transform.SetParent(x2Root.transform, false);
                piece.transform.localPosition = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * midRadius;
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                float arcLength = 2f * Mathf.PI * midRadius * (x2Step / 360f);
                piece.transform.localScale = new Vector3(boundary.thickness + 0.04f, arcLength * 1.5f, 1f);
                SpriteRenderer pieceRenderer = piece.AddComponent<SpriteRenderer>();
                pieceRenderer.sprite = square;
                pieceRenderer.color = new Color(1f, 0.72f, 0.1f, 1f);
                pieceRenderer.sortingOrder = 1;
                x2Segments[i] = pieceRenderer;
            }

            // --- Red edging on the exit ---
            SpriteRenderer edgeLow = BuildExitEdge("Exit Edge Low", boundaryObject.transform, square,
                boundary.thickness + 0.24f, midRadius, config.exitCenterDegrees - config.exitStartDegrees * 0.5f);
            SpriteRenderer edgeHigh = BuildExitEdge("Exit Edge High", boundaryObject.transform, square,
                boundary.thickness + 0.24f, midRadius, config.exitCenterDegrees + config.exitStartDegrees * 0.5f);

            // --- Decals: world-space TextMeshPro on the ring, just outside it ---
            float labelRadius = outerRadius + 0.7f;
            TextMeshPro exitLabel = BuildDecal("EXIT Decal", boundaryObject.transform, "EXIT",
                new Color(1f, 0.32f, 0.28f, 1f), config.exitCenterDegrees, labelRadius);
            TextMeshPro x2Label = BuildDecal("x2 Decal", boundaryObject.transform, "x2",
                new Color(1f, 0.78f, 0.2f, 1f), config.x2CenterDegrees, labelRadius);

            // --- Marble template: inactive, cloned by the pool ---
            var marbles = new GameObject("Marbles");
            var template = new GameObject("Marble");
            template.transform.SetParent(marbles.transform, false);

            var body = template.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var collider = template.AddComponent<CircleCollider2D>();
            collider.radius = config.ballRadius;
            collider.sharedMaterial = EnsureBouncyMaterial();

            // Dark rim behind a bright fill (the per-colour letter is added at runtime by the wide presenter).
            var rim = new GameObject("Rim");
            rim.transform.SetParent(template.transform, false);
            rim.transform.localScale = Vector3.one * config.ballRadius * 2f;
            var rimRenderer = rim.AddComponent<SpriteRenderer>();
            rimRenderer.sprite = circle;
            rimRenderer.color = RimColor;
            rimRenderer.sortingOrder = 5;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(template.transform, false);
            visual.transform.localScale = Vector3.one * config.ballRadius * 2f * 0.84f;
            var fillRenderer = visual.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = circle;
            fillRenderer.color = config.colours[0];
            fillRenderer.sortingOrder = 6;

            template.AddComponent<WallHitSensor2D>();
            RoyaleMarble marble = template.AddComponent<RoyaleMarble>();
            marble.Bind(fillRenderer);
            template.SetActive(false);

            // --- Runner + simulation ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            RingRoyaleSimulation simulation = runnerObject.AddComponent<RingRoyaleSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("_ballTemplate").objectReferenceValue = template;
            serialized.FindProperty("_ballParent").objectReferenceValue = marbles.transform;
            serialized.FindProperty("_boundary").objectReferenceValue = boundary;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // --- HUD: segment card (hook / counter / caption / explainer) + vertical share column ---
            SegmentCardPresenter card = SegmentCardFactory.CreateInScene(cardConfig, AspectMode.Wide);
            Transform counterPlate = card.transform.Find("RightMargin");
            if (counterPlate != null)
            {
                // Counter rides the top of the right margin; the share column fills the rest.
                var counterRect = (RectTransform)counterPlate;
                counterRect.anchoredPosition = new Vector2(-205f, 360f);
                counterRect.sizeDelta = new Vector2(390f, 130f);
            }

            BuildShareColumn(config, out RectTransform column, out Image[] segments, out TMP_Text[] labels);

            // --- Confetti: cannons at the ring's flanks, rain across the arena ---
            var confettiObject = new GameObject("Confetti");
            confettiObject.AddComponent<ParticleSystem>();
            ConfettiBurst2D confetti = confettiObject.AddComponent<ConfettiBurst2D>();
            confetti.leftCannon = new Vector2(-5.2f, -3.6f);
            confetti.rightCannon = new Vector2(5.2f, -3.6f);
            confetti.rainY = ortho + 1f;
            confetti.rainHalfWidth = 5.5f;

            // --- Presenters: the existing one for audio / tint / dressing, the wide one for the rest ---
            RingRoyalePresenter presenter = runnerObject.AddComponent<RingRoyalePresenter>();
            presenter.Bind(simulation, null, null, confetti);
            presenter.BindRingDressing(x2Segments, edgeLow, edgeHigh, midRadius,
                new[] { exitLabel.transform, x2Label.transform });

            RingRoyaleWidePresenter widePresenter = runnerObject.AddComponent<RingRoyaleWidePresenter>();
            widePresenter.Bind(simulation, card, camera, column, segments, labels);
            widePresenter.SetGlyphs(Glyphs);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            float chord = boundary.GapChordWidth;
            if (config.ballRadius * 2f >= chord)
            {
                Debug.LogError($"[royale] Marble diameter {config.ballRadius * 2f:0.00} does not fit the starting " +
                               $"exit chord {chord:0.00}. Widen exitStartDegrees or shrink ballRadius.");
            }

            float safeSpeed = ConstantSpeed2D.MaxSafeSpeed(config.fixedTimestep, boundary.thickness);
            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  16:9, ortho {ortho:0.00}, camera x {cameraX:0.00} (ring clears the right margin)\n" +
                $"  ring radius {boundary.radius}, wall {boundary.thickness:0.00} thick\n" +
                $"  exit {config.exitStartDegrees} deg (chord {chord:0.00}, marble {config.ballRadius * 2f:0.00}), " +
                $"x2 arc {config.x2ArcDegrees} deg opposite, spin {config.ringAngularSpeed} deg/s\n" +
                $"  {config.ColourCount} colours x {config.marblesPerColour}, cap {config.bodyCap}\n" +
                $"  sudden death at {config.suddenDeathAtSeconds}s, +{config.suddenDeathDegreesPerSecond} deg/s\n" +
                $"  speed {config.ballSpeed} (safe ceiling {safeSpeed:0.0})\n" +
                "  Estimated run ~45s (collision-free model); a seed scan is needed to confirm.\n" +
                "  Seed is random per Play and logged at start.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
        }

        /// <summary>Right-margin column: dark plate, six stacked colour segments (sized live by the presenter).</summary>
        static void BuildShareColumn(RingRoyaleConfig config, out RectTransform column, out Image[] segments,
                                     out TMP_Text[] labels)
        {
            var canvasObject = new GameObject("Share Column Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = WideFrameLayout.ReferenceResolution(AspectMode.Wide);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = WideFrameLayout.MatchWidthOrHeight(AspectMode.Wide);

            // Plate: right margin, below the counter (y +290 .. -380 from frame centre), clear of the explainer.
            var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(canvasObject.transform, false);
            var plateRect = (RectTransform)plate.transform;
            plateRect.anchorMin = plateRect.anchorMax = new Vector2(1f, 0.5f);
            plateRect.pivot = new Vector2(0.5f, 0.5f);
            plateRect.anchoredPosition = new Vector2(-205f, -45f);
            plateRect.sizeDelta = new Vector2(390f, 670f);
            var plateImage = plate.GetComponent<Image>();
            plateImage.color = new Color(0.03f, 0.04f, 0.07f, 0.78f);
            plateImage.raycastTarget = false;

            var columnObject = new GameObject("Column", typeof(RectTransform));
            columnObject.transform.SetParent(plate.transform, false);
            column = (RectTransform)columnObject.transform;
            column.anchorMin = Vector2.zero;
            column.anchorMax = Vector2.one;
            column.offsetMin = new Vector2(12f, 12f);
            column.offsetMax = new Vector2(-12f, -12f);

            int n = config.ColourCount;
            segments = new Image[n];
            labels = new TMP_Text[n];
            for (int i = 0; i < n; i++)
            {
                var segmentObject = new GameObject($"Segment {config.NameOf(i)}", typeof(RectTransform), typeof(Image));
                segmentObject.transform.SetParent(column, false);
                var image = segmentObject.GetComponent<Image>();
                image.color = config.colours[i];
                image.raycastTarget = false;
                segments[i] = image;

                var labelObject = new GameObject("Label", typeof(RectTransform));
                labelObject.transform.SetParent(segmentObject.transform, false);
                var label = labelObject.AddComponent<TextMeshProUGUI>();
                label.text = string.Empty;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(0.03f, 0.03f, 0.05f, 1f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 54f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(6f, 2f);
                labelRect.offsetMax = new Vector2(-6f, -2f);
                labels[i] = label;

                // Initial layout so the edited scene shows six equal bands before Play.
                var rect = (RectTransform)segmentObject.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                float h = 646f / n;
                rect.anchoredPosition = new Vector2(0f, -h * i);
                rect.sizeDelta = new Vector2(0f, h);
                label.text = $"{Glyphs[i]}  {config.marblesPerColour}";
            }
        }

        static SpriteRenderer BuildExitEdge(string name, Transform ring, Sprite square, float length,
                                            float midRadius, float localDegrees)
        {
            var edge = new GameObject(name);
            edge.transform.SetParent(ring, false);
            float radians = localDegrees * Mathf.Deg2Rad;
            edge.transform.localPosition = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * midRadius;
            edge.transform.localRotation = Quaternion.Euler(0f, 0f, localDegrees);
            edge.transform.localScale = new Vector3(length, 0.16f, 1f);
            SpriteRenderer renderer = edge.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(1f, 0.15f, 0.12f, 1f);
            renderer.sortingOrder = 3;
            return renderer;
        }

        static TextMeshPro BuildDecal(string name, Transform ring, string text, Color color, float localDegrees,
                                      float radius)
        {
            var decal = new GameObject(name);
            decal.transform.SetParent(ring, false);
            float radians = localDegrees * Mathf.Deg2Rad;
            decal.transform.localPosition = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * radius;

            var tmp = decal.AddComponent<TextMeshPro>();
            tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tmp.rectTransform.sizeDelta = new Vector2(3f, 1f);
            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = 7f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            tmp.sortingOrder = 20;
            return tmp;
        }

        static RingRoyaleConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RingRoyaleConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var config = ScriptableObject.CreateInstance<RingRoyaleConfig>();
            config.fixedTimestep = 0.01f;
            config.colours = (Color[])Palette.Clone();
            config.colourNames = (string[])ColourNames.Clone();
            config.marblesPerColour = 3;
            config.ballRadius = 0.26f;
            config.ballSpeed = 5f;
            config.ringRadius = 4.6f;
            config.exitStartDegrees = 10f;
            config.x2ArcDegrees = 24f;
            config.ringAngularSpeed = 60f;
            config.suddenDeathAtSeconds = 15f;
            config.suddenDeathDegreesPerSecond = 4f;
            config.maxGameSeconds = 100f;
            config.preLaunchHoldSeconds = 2.5f;
            config.resultHoldSeconds = 2.5f;
            config.intent = "comp-001 #6 'Six Colours' (16:9). 6 hues 60 deg apart x 3 marbles; x2 arc 24 deg opposite a " +
                            "10 deg EXIT; ring 60 deg/s; sudden death at 15 s, +4 deg/s. Collision-free model: game " +
                            "~35-42 s (+2.5 s hold, ~0.9 s seal, 2.5 s result) = ~45 s clip. Needs a seed scan to confirm.";
            config.maxDurationSeconds = config.TotalSeconds;
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static PhysicsMaterial2D EnsureBouncyMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var material = new PhysicsMaterial2D("PerfectlyElastic") { bounciness = 1f, friction = 0f };
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
