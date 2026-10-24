using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SimulationLobby.Simulations.RingRoyale.EditorTools
{
    /// <summary>
    /// Builds <c>RingRoyale_FourColour_2D</c> (royale-001) from code.
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Color Rumble (royale) ▸ Color Rumble (royale-001)</c>.
    /// Re-running replaces the scene; an existing config asset is kept.
    /// </summary>
    static class RingRoyaleSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/RingRoyale_FourColour_2D.unity";
        const string ConfigPath = "Assets/Settings/Configs/RingRoyaleConfig_001.asset";
        const string MaterialPath = "Assets/Settings/Configs/PerfectlyElastic.physicsMaterial2D";

        // 9:16 at 1080x1920: half-width is 6.19 at this size, enough for the ring (5.2 outer) plus its decals.
        // The camera is nudged up so the ring sits low: the title + share bar fit above it, the question below.
        const float OrthographicSize = 11f;
        const float CameraY = 1.25f;

        static readonly Color WallColor = new Color(0.78f, 0.8f, 0.86f, 1f);
        static readonly Color RimColor = new Color(0.04f, 0.04f, 0.07f, 1f);

        [MenuItem("Simulation Lobby/2D Formats/Color Rumble (royale)/Color Rumble (royale-001)", false, 195)]
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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, CameraY, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.09f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient backdrop ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(0f, 0f, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(OrthographicSize * 1.4f, OrthographicSize * 2.2f);
            backdrop.particleCount = 90;
            backdrop.maxAlpha = 0.13f;
            backdrop.tint = new Color(0.55f, 0.65f, 1f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- Ring: every segment built, the gap's switched off, so sudden death widens it live ---
            var boundaryObject = new GameObject("Ring");
            CircularBoundary2D boundary = boundaryObject.AddComponent<CircularBoundary2D>();
            boundary.radius = config.ringRadius;
            float thicknessForSpeed = config.ballSpeed * 4f * config.fixedTimestep / 0.5f;
            boundary.thickness = Mathf.Max(0.6f, thicknessForSpeed);
            // 1 degree per segment, so the exit's 3 deg/s growth opens the wall visibly every third of a second.
            boundary.segmentCount = 360;
            boundary.keepGapSegments = true;
            boundary.gapDegrees = config.exitStartDegrees;
            boundary.gapCenterDegrees = config.exitCenterDegrees;
            boundary.wallSprite = square;
            boundary.wallColor = WallColor;
            boundary.wallSortingOrder = 0;
            boundary.Rebuild();

            float midRadius = boundary.radius + boundary.thickness * 0.5f;
            float outerRadius = boundary.radius + boundary.thickness;

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

            // --- Red edging on the exit: two bars the presenter slides outward as the gap widens ---
            SpriteRenderer edgeLow = BuildExitEdge("Exit Edge Low", boundaryObject.transform, square,
                boundary.thickness + 0.24f, midRadius, config.exitCenterDegrees - config.exitStartDegrees * 0.5f);
            SpriteRenderer edgeHigh = BuildExitEdge("Exit Edge High", boundaryObject.transform, square,
                boundary.thickness + 0.24f, midRadius, config.exitCenterDegrees + config.exitStartDegrees * 0.5f);

            // --- Decals: world-space TextMeshPro on the ring, just outside it ---
            float labelRadius = outerRadius + 0.75f;
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
            // Discrete is enough at 5 u/s and a 0.6 wall (0.05 per step).
            body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var collider = template.AddComponent<CircleCollider2D>();
            collider.radius = config.ballRadius;
            collider.sharedMaterial = EnsureBouncyMaterial();

            // Dark rim behind a bright fill: the silhouette survives any colour next to any other.
            var rim = new GameObject("Rim");
            rim.transform.SetParent(template.transform, false);
            rim.transform.localScale = Vector3.one * config.ballRadius * 2f;
            var rimRenderer = rim.AddComponent<SpriteRenderer>();
            rimRenderer.sprite = circle;
            rimRenderer.color = RimColor;
            rimRenderer.sortingOrder = 5;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(template.transform, false);
            visual.transform.localScale = Vector3.one * config.ballRadius * 2f * 0.78f;
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
            BackdropThemeLibrary.AddStyler(backdropObject, runner, camera, backdrop);

            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("_ballTemplate").objectReferenceValue = template;
            serialized.FindProperty("_ballParent").objectReferenceValue = marbles.transform;
            serialized.FindProperty("_boundary").objectReferenceValue = boundary;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // --- HUD: title on top, share bar under it, question / event line at the bottom ---
            CaptionHud caption = CaptionCanvasBuilder.Build("COLOR RUMBLE", string.Empty, "WHICH COLOR SURVIVES?",
                CaptionCanvasBuilder.Style.Dark);
            var canvasRect = (RectTransform)caption.transform;
            StyleCaptions(canvasRect);

            ShareBarHud shareBar = ShareBarCanvasBuilder.BuildFramed(canvasRect, config.ColourCount);
            shareBar.minAliveShare = 0.1f;
            shareBar.minLabelWidth = 60f;
            var colours = new Color[config.ColourCount];
            var names = new string[config.ColourCount];
            for (int c = 0; c < colours.Length; c++)
            {
                colours[c] = config.colours[c];
                names[c] = config.NameOf(c);
            }

            shareBar.Configure(colours.Length, colours, names);
            shareBar.SetCounts(new int[colours.Length]);
            var barRect = (RectTransform)shareBar.transform;
            barRect.anchorMin = new Vector2(0.03f, 0f);
            barRect.anchorMax = new Vector2(0.97f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 1420f);
            barRect.sizeDelta = new Vector2(0f, 118f);

            // --- Confetti: cannons and rain placed for the 9:16 frame ---
            var confettiObject = new GameObject("Confetti");
            confettiObject.AddComponent<ParticleSystem>();
            ConfettiBurst2D confetti = confettiObject.AddComponent<ConfettiBurst2D>();
            confetti.leftCannon = new Vector2(-5.6f, -4.2f);
            confetti.rightCannon = new Vector2(5.6f, -4.2f);
            confetti.rainY = CameraY + OrthographicSize + 1f;
            confetti.rainHalfWidth = 6.2f;

            // --- Presenter ---
            RingRoyalePresenter presenter = runnerObject.AddComponent<RingRoyalePresenter>();
            presenter.Bind(simulation, shareBar, caption, confetti);
            presenter.BindRingDressing(x2Segments, edgeLow, edgeHigh, midRadius,
                new[] { exitLabel.transform, x2Label.transform });

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
                $"  ring radius {boundary.radius}, wall {boundary.thickness:0.00} thick, {boundary.segmentCount} segments\n" +
                $"  exit {config.exitStartDegrees}° (chord {chord:0.00}, marble {config.ballRadius * 2f:0.00}), " +
                $"x2 arc {config.x2ArcDegrees}° opposite, spin {config.ringAngularSpeed}°/s\n" +
                $"  {config.ColourCount} colours x {config.marblesPerColour}, cap {config.bodyCap}, pool {config.BodyBudget} bodies\n" +
                $"  sudden death at {config.suddenDeathAtSeconds}s, +{config.suddenDeathDegreesPerSecond}°/s\n" +
                $"  speed {config.ballSpeed} (safe ceiling {safeSpeed:0.0})\n" +
                "  Seed is random per Play and logged at start.");

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(config);
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

        /// <summary>Arena decal: world-space TextMeshPro parented to the ring. The presenter keeps it upright.</summary>
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
            tmp.fontSize = 8f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            tmp.sortingOrder = 20;
            return tmp;
        }

        /// <summary>One-line title up top (clear of the share bar), the question and event line big at the bottom.</summary>
        static void StyleCaptions(RectTransform canvasRect)
        {
            Transform top = canvasRect.Find("TopPanel");
            if (top != null)
            {
                SetFont(top.Find("Caption"), 96f, FontStyles.Bold);
                SetFont(top.Find("Subline"), 52f, FontStyles.Bold);
            }

            Transform bottom = canvasRect.Find("BottomPanel");
            if (bottom != null)
            {
                var rect = (RectTransform)bottom;
                rect.anchoredPosition = new Vector2(0f, 40f);
                rect.sizeDelta = new Vector2(-80f, 230f);
                SetFont(bottom.Find("Footer"), 76f, FontStyles.Bold);
            }
        }

        static void SetFont(Transform text, float size, FontStyles style)
        {
            if (text != null && text.TryGetComponent(out TMP_Text tmp))
            {
                tmp.fontSize = size;
                tmp.fontStyle = style;
                tmp.textWrappingMode = TextWrappingModes.Normal;
            }
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
            config.intent = "royale-001 first build. 4 colours x 5 marbles; x2 arc 40 deg (clone on wall hit) " +
                            "opposite an EXIT of 16 deg; sudden death widens the exit 3 deg/s from 40 s. " +
                            "Modelled ~55-65 s without ball-ball collisions; collisions should shorten it.";
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
