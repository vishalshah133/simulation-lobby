using System.IO;
using System.Text;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.ReleaseOrMultiply.EditorTools
{
    /// <summary>
    /// Builds comp-001 segment 3, "Gate Funnel": one gold marble falls through five rows of six gates in a
    /// 16:9 frame. Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Release or Multiply (rom) ▸ Wide: Gate Funnel (comp-001)</c>.
    /// Additive: it never touches <see cref="RomSceneBuilder"/>, <c>RomConfig_001</c> or the 9:16 scene.
    /// </summary>
    static class RomWideSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Comp001_03_GateFunnel_Wide.unity";
        const string ConfigPath = "Assets/Settings/Configs/RomConfig_Comp001_03.asset";
        const string WideSettingsPath = "Assets/Settings/Configs/RomWide_Comp001_03.asset";
        const string CardPath = "Assets/Settings/SegmentCards/comp001_03.asset";
        const string MarbleMaterialPath = "Assets/Settings/Configs/RomWideMarble.physicsMaterial2D";
        const string PegMaterialPath = "Assets/Settings/Configs/RomWidePeg.physicsMaterial2D";

        const string CardTitle = "Gate Funnel";
        const string CardHook = "1 BALL. HOW MANY SURVIVE THE GATES?";
        const string CardExplainer = "One marble drops through five rows of gates. x3 triples it, +3 adds, " +
                                     "half halves it, x0 wipes it out. How many reach the bottom?";

        const AspectMode Mode = AspectMode.Wide;

        // Depth under the last gate that the gold pile fills (the marbles are banked at BottomY).
        const float PileDepth = 2.4f;

        static readonly Color Background = new Color(0.02f, 0.022f, 0.045f, 1f);

        [MenuItem("Simulation Lobby/2D Formats/Release or Multiply (rom)/Wide: Gate Funnel (comp-001)", false, 132)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[rom-wide] Exit play mode before building the scene.");
                return;
            }

            RomConfig config = EnsureConfig();
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[rom-wide] Config '{config.name}' is invalid: {error}. Scene not built.", config);
                return;
            }

            RomWideSettings settings = EnsureSettings();
            SegmentCardConfig cardConfig = SegmentCardFactory.CreateConfigAsset(CardPath, 9, 10, CardTitle, CardHook, CardExplainer);
            if (settings.startDelaySeconds < cardConfig.cardSeconds)
            {
                Debug.LogWarning($"[rom-wide] startDelaySeconds ({settings.startDelaySeconds:0.0}) is shorter than the card " +
                                 $"({cardConfig.cardSeconds:0.0}s): the first drop would happen behind the card.", settings);
            }

            Sprite circle = GeneratedArt.Circle();
            Sprite square = GeneratedArt.Square();
            Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Frame: arena rectangle -> ortho size and camera offset from the shared wide-frame maths ---
            float halfWidth = config.FieldHalfWidth;
            float top = config.spawnY + 0.6f;
            float pileFloor = config.BottomY - PileDepth;
            float arenaWidth = halfWidth * 2f + 0.6f;
            float arenaHeight = top - (pileFloor - 0.2f);
            float arenaCenterY = (top + pileFloor - 0.2f) * 0.5f;
            float ortho = WideFrameLayout.OrthoSizeForArena(arenaWidth, arenaHeight, Mode, 1.02f);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(WideFrameLayout.ArenaCameraOffsetX(ortho, Mode), arenaCenterY, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = ortho;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient backdrop (sparse: the gates are the picture, not the background) ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(0f, arenaCenterY, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(ortho * 3.2f, ortho * 2.2f);
            backdrop.particleCount = 60;
            backdrop.maxAlpha = 0.08f;
            backdrop.tint = new Color(1f, 0.85f, 0.5f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            PhysicsMaterial2D marbleMaterial = EnsureMaterial(MarbleMaterialPath, "RomWideMarble", config.marbleBounciness, 0.1f);
            PhysicsMaterial2D pegMaterial = EnsureMaterial(PegMaterialPath, "RomWidePeg", 0.6f, 0.05f);

            // --- Walls ---
            float bottom = config.BottomY - 1f;
            var walls = new GameObject("Walls");
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = new GameObject(side < 0 ? "Wall_L" : "Wall_R");
                wall.transform.SetParent(walls.transform, false);
                wall.transform.position = new Vector3(side * (halfWidth + 0.15f), (top + bottom) * 0.5f, 0f);
                var box = wall.AddComponent<BoxCollider2D>();
                box.size = new Vector2(0.3f, top - bottom);
                box.sharedMaterial = pegMaterial;
                // Visual runs on down to the pile floor, so the gold pile sits in a bucket.
                float visualTop = top;
                float visualBottom = pileFloor - 0.2f;
                var visual = AddRect(walls.transform, side < 0 ? "WallVisual_L" : "WallVisual_R", rounded ?? circle,
                    new Vector2(0.2f, visualTop - visualBottom), new Color(0.55f, 0.65f, 0.95f, 0.5f), 0);
                visual.transform.position = new Vector3(side * (halfWidth + 0.15f), (visualTop + visualBottom) * 0.5f, 0f);
            }

            var floorRect = AddRect(walls.transform, "PileFloor", square, new Vector2(halfWidth * 2f + 0.5f, 0.2f),
                new Color(1f, 0.78f, 0.2f, 0.6f), 0);
            floorRect.transform.position = new Vector3(0f, pileFloor - 0.1f, 0f);

            // --- Lane dividers: visual only, so the six lanes read as a funnel of channels ---
            var dividers = new GameObject("Lane Dividers");
            float dividerTop = config.GateY(0) + config.gateThickness * 0.5f;
            float dividerBottom = config.GateY(config.RowCount - 1) - config.gateThickness * 0.5f;
            for (int lane = 1; lane < config.Lanes; lane++)
            {
                float x = -halfWidth + lane * config.laneWidth;
                var line = AddRect(dividers.transform, $"Divider_{lane}", square, new Vector2(0.04f, dividerTop - dividerBottom),
                    new Color(0.6f, 0.7f, 1f, 0.14f), 0);
                line.transform.position = new Vector3(x, (dividerTop + dividerBottom) * 0.5f, 0f);
            }

            // --- Pegs between gate rows: one sparse line each ---
            var pegs = new GameObject("Pegs");
            for (int row = 0; row < config.RowCount; row++)
            {
                for (int pegLine = 0; pegLine < config.pegLinesPerRow; pegLine++)
                {
                    int count = config.PegsInLine(pegLine);
                    for (int i = 0; i < count; i++)
                    {
                        var peg = new GameObject($"Peg_{row}_{pegLine}_{i}");
                        peg.transform.SetParent(pegs.transform, false);
                        peg.transform.position = new Vector3(config.PegX(pegLine, i), config.PegLineY(row, pegLine), 0f);
                        peg.transform.localScale = Vector3.one * (config.pegRadius * 2f);

                        var pegRenderer = peg.AddComponent<SpriteRenderer>();
                        pegRenderer.sprite = circle;
                        pegRenderer.color = new Color(0.62f, 0.7f, 0.95f, 0.55f);
                        pegRenderer.sortingOrder = 2;

                        var collider = peg.AddComponent<CircleCollider2D>();
                        collider.radius = 0.5f;
                        collider.sharedMaterial = pegMaterial;
                    }
                }
            }

            // --- Gates: trigger band + big world-space TMP decal per lane, row-major ---
            var gatesRoot = new GameObject("Gates");
            int lanes = config.Lanes;
            var zones = new TriggerZone2D[config.RowCount * lanes];
            var bars = new SpriteRenderer[zones.Length];
            var labels = new TMP_Text[zones.Length];
            for (int row = 0; row < config.RowCount; row++)
            {
                for (int lane = 0; lane < lanes; lane++)
                {
                    int i = row * lanes + lane;
                    var gate = new GameObject($"Gate_r{row + 1}_l{lane + 1}");
                    gate.transform.SetParent(gatesRoot.transform, false);
                    gate.transform.position = new Vector3(config.LaneCenterX(lane), config.GateY(row), 0f);

                    var trigger = gate.AddComponent<BoxCollider2D>();
                    trigger.isTrigger = true;
                    trigger.size = new Vector2(config.laneWidth, config.gateThickness);
                    zones[i] = gate.AddComponent<TriggerZone2D>();

                    bars[i] = AddRect(gate.transform, "Bar", rounded ?? circle,
                        new Vector2(config.laneWidth * 0.94f, config.gateThickness), new Color(1f, 1f, 1f, 0f), 1);
                    labels[i] = CreateGateDecal(gate.transform, config.laneWidth * 0.9f, config.gateThickness);
                }
            }

            // --- Marble template: inactive, cloned by the pool ---
            var parent = new GameObject("Marbles");
            var template = new GameObject("Marble");
            template.transform.SetParent(parent.transform, false);

            var body = template.AddComponent<Rigidbody2D>();
            body.gravityScale = config.gravityScale;
            body.linearDamping = config.linearDamping;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var marbleCollider = template.AddComponent<CircleCollider2D>();
            marbleCollider.radius = config.marbleRadius;
            marbleCollider.sharedMaterial = marbleMaterial;

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(template.transform, false);
            visualObject.transform.localScale = Vector3.one * (config.marbleRadius * 2f);
            var visualRenderer = visualObject.AddComponent<SpriteRenderer>();
            visualRenderer.sprite = circle;
            visualRenderer.color = new Color(1f, 0.93f, 0.7f, 1f);
            visualRenderer.sortingOrder = 10;

            Marble2D marble = template.AddComponent<Marble2D>();
            marble.Bind(visualObject.transform, visualRenderer);
            template.SetActive(false);

            // --- Debris for gate pops ---
            var shatterObject = new GameObject("Shatter VFX");
            ShatterBurst2D shatter = shatterObject.AddComponent<ShatterBurst2D>();
            shatter.shardsPerBurst = 6;
            shatter.sparksPerBurst = 5;
            shatter.Apply(
                new Texture[] { GeneratedArt.Diamond(), GeneratedArt.Star(), GeneratedArt.Shard(), GeneratedArt.Sliver() },
                GeneratedArt.SoftDot(), GeneratedArt.Ring(), 6);

            // --- Runner + simulation. autoStart off: RomWideStarter begins the run after the card. ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            RomSimulation simulation = runnerObject.AddComponent<RomSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = false;
            runner.randomizeSeedOnStart = false;

            RomWideStarter starter = runnerObject.AddComponent<RomWideStarter>();
            starter.runner = runner;
            starter.settings = settings;
            starter.randomizeSeed = true;
            starter.seed = 1;

            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("_marbleTemplate").objectReferenceValue = template;
            serialized.FindProperty("_marbleParent").objectReferenceValue = parent.transform;
            SerializedProperty zonesProperty = serialized.FindProperty("_gateZones");
            zonesProperty.arraySize = zones.Length;
            for (int i = 0; i < zones.Length; i++)
            {
                zonesProperty.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            // --- HUD: segment card + wide-frame zones, plus the huge final number ---
            SegmentCardPresenter card = SegmentCardFactory.CreateInScene(cardConfig, Mode);
            BuildFinalNumber(settings, rounded, out RectTransform finalRoot, out CanvasGroup finalGroup,
                out TMP_Text finalNumber, out TMP_Text finalNote);

            RomWidePresenter presenter = runnerObject.AddComponent<RomWidePresenter>();
            presenter.Bind(simulation, settings, card, starter, camera, bars, labels, shatter, circle,
                config.startMarbles, new Vector2(0f, pileFloor), halfWidth * 2f - 0.8f,
                finalRoot, finalGroup, finalNumber, finalNote);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            var log = new StringBuilder();
            log.AppendLine($"[rom-wide] Built {ScenePath}: {config.RowCount} rows x {lanes} lanes, {config.startMarbles} starting marble, " +
                           $"{config.maxBodies} body cap, ortho {ortho:0.0}, 16:9 wide frame.");
            for (int row = 0; row < config.RowCount; row++)
            {
                var line = new StringBuilder($"  row {row + 1} (y {config.GateY(row):0.0}):");
                foreach (GateDef gate in config.rows[row].gates)
                {
                    line.Append(' ').Append(gate.Label);
                }

                log.AppendLine(line.ToString());
            }

            log.Append("  Lane order and every drop are random per Play (seed logged). Run length is a guess, never watched: " +
                       "est. 30-45 s. Tune RomConfig_Comp001_03 gravityScale / linearDamping / finishHoldSeconds and " +
                       "RomWide_Comp001_03 startDelaySeconds / captionHoldSeconds, then rebuild only if layout fields change.");
            Debug.Log(log.ToString());
        }

        /// <summary>Arena decal: a world-space TextMeshPro on the gate band, sized to fit its object.</summary>
        static TMP_Text CreateGateDecal(Transform parent, float width, float height)
        {
            var labelObject = new GameObject("Gate Decal");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            var tmp = labelObject.AddComponent<TextMeshPro>();
            tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tmp.rectTransform.sizeDelta = new Vector2(width, height);
            tmp.text = string.Empty;
            tmp.color = new Color(0.03f, 0.04f, 0.08f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 1f;
            tmp.fontSizeMax = 9f;
            tmp.sortingOrder = 6;
            return tmp;
        }

        /// <summary>Overlay canvas for the ending image. Hidden until the run is final; the presenter places and fades it.</summary>
        static void BuildFinalNumber(RomWideSettings settings, Sprite plateSprite, out RectTransform root, out CanvasGroup group,
                                     out TMP_Text number, out TMP_Text note)
        {
            var canvasObject = new GameObject("Final Number Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // above the HUD zones' content, below the full-screen card (100)
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = WideFrameLayout.ReferenceResolution(Mode);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = WideFrameLayout.MatchWidthOrHeight(Mode);

            var rootObject = new GameObject("Final Number", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            rootObject.transform.SetParent(canvasObject.transform, false);
            root = (RectTransform)rootObject.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.39f, 0.52f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(1080f, 560f);
            group = rootObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var plate = rootObject.GetComponent<Image>();
            plate.sprite = plateSprite;
            plate.type = Image.Type.Sliced;
            plate.color = new Color(0.01f, 0.012f, 0.03f, 0.72f);
            plate.raycastTarget = false;

            number = CreateUiText(root, "Number", settings.finalNumberSize, new Color(1f, 0.82f, 0.25f, 1f),
                new Vector2(0f, 0.34f), new Vector2(1f, 1f));
            number.enableAutoSizing = true;
            number.fontSizeMax = settings.finalNumberSize;
            number.fontSizeMin = settings.finalNumberSize * 0.4f;
            number.outlineWidth = 0.18f;
            number.outlineColor = new Color32(40, 24, 0, 255);

            note = CreateUiText(root, "Note", 64f, Color.white, new Vector2(0f, 0.04f), new Vector2(1f, 0.34f));
        }

        static TMP_Text CreateUiText(RectTransform parent, string name, float size, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(20f, 0f);
            rect.offsetMax = new Vector2(-20f, 0f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        static SpriteRenderer AddRect(Transform parent, string name, Sprite sprite, Vector2 size, Color color, int order)
        {
            var rect = new GameObject(name);
            rect.transform.SetParent(parent, false);
            var renderer = rect.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;

            if (sprite != null && sprite.border != Vector4.zero)
            {
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.size = size;
            }
            else
            {
                rect.transform.localScale = new Vector3(size.x, size.y, 1f);
            }

            return renderer;
        }

        /// <summary>
        /// Loads the segment config if it exists (so hand tuning survives a rebuild), else creates it.
        /// Layout fields feed the scene, so rebuild after changing them; run-feel fields apply on the next Play.
        /// </summary>
        static RomConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RomConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var config = ScriptableObject.CreateInstance<RomConfig>();
            config.fixedTimestep = 1f / 60f;
            config.maxDurationSeconds = 150f;

            // One marble, so the count visibly compounds. Never watched in Unity: length is an estimate.
            config.startMarbles = 1;
            config.dropIntervalSeconds = 1f;
            config.dropSpread = 0.35f;
            config.shuffleGatesPerRun = true;

            // Rows are shuffled per run. Wipes and halves stay out of the top two rows so a lone marble is
            // not simply deleted at the start; the last row has the highest variance (x100 or x0).
            config.rows = new[]
            {
                Row(M(2), M(2), M(3), A(2), M(2), M(3)),
                Row(M(3), M(2), A(3), M(2), H(), M(4)),
                Row(M(3), M(2), A(5), H(), M(5), W()),
                Row(M(4), M(2), H(), M(10), A(10), W()),
                Row(M(100), M(20), M(5), H(), W(), W()),
            };

            config.countCap = 1_000_000_000_000L;
            config.maxBodies = 300;
            config.maxClonesPerGate = 24;

            // Slow, floaty fall so the compounding tide takes ~half a minute to reach the bottom.
            config.marbleRadius = 0.2f;
            config.marbleBounciness = 0.45f;
            config.gravityScale = 0.07f;
            config.linearDamping = 0.15f;
            config.maxFallSpeed = 6.5f;
            config.stuckSeconds = 1.2f;

            config.laneWidth = 3f;
            config.rowPitch = 1.9f;
            config.gateThickness = 1f;
            config.firstGateY = 3.6f;
            config.spawnY = 5f;
            config.pegLinesPerRow = 1;
            config.pegRadius = 0.12f;
            config.pegSpacing = 1.5f;

            config.finishHoldSeconds = 6f;
            config.intent = "comp-001 #3 Gate Funnel (wide): 6 lanes x 5 rows (x n, +n, half, x0), ONE starting marble, " +
                            "300-body cap, slow gravity for ~45 s total. Never run in Unity; length is an estimate.";
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static GateRow Row(params GateDef[] gates) => new GateRow(gates);

        static GateDef M(int value) => new GateDef(GateKind.Multiply, value);

        static GateDef A(int value) => new GateDef(GateKind.Add, value);

        static GateDef H() => new GateDef(GateKind.Halve, 2);

        static GateDef W() => new GateDef(GateKind.Wipe, 0);

        static RomWideSettings EnsureSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RomWideSettings>(WideSettingsPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var settings = ScriptableObject.CreateInstance<RomWideSettings>();
            AssetDatabase.CreateAsset(settings, WideSettingsPath);
            return settings;
        }

        static PhysicsMaterial2D EnsureMaterial(string path, string name, float bounciness, float friction)
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var material = new PhysicsMaterial2D(name) { bounciness = bounciness, friction = friction };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
