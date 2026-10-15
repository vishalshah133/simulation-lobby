using System.IO;
using System.Text;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.ReleaseOrMultiply.EditorTools
{
    /// <summary>
    /// Builds the <c>rom</c> scene from code. Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Release or Multiply (rom) ▸ Board</c>.
    /// Layout comes from the config, so after changing rows, lanes, spacing or marble size, rebuild.
    /// </summary>
    static class RomSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Release_Or_Multiply_2D.unity";
        const string ConfigPath = "Assets/Settings/Configs/RomConfig_001.asset";
        const string MarbleMaterialPath = "Assets/Settings/Configs/RomMarble.physicsMaterial2D";
        const string PegMaterialPath = "Assets/Settings/Configs/RomPeg.physicsMaterial2D";

        // Same 9:16 framing as esc and brk: half-height 9.6 gives a 10.8-wide frame.
        const float OrthographicSize = 9.6f;

        [MenuItem("Simulation Lobby/2D Formats/Release or Multiply (rom)/Board", false, 131)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[rom] Exit play mode before building the scene.");
                return;
            }

            RomConfig config = EnsureConfig();
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[rom] Config '{config.name}' is invalid: {error}. Scene not built.", config);
                return;
            }

            Sprite circle = GeneratedArt.Circle();
            Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.075f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient backdrop ---
            var backdropObject = new GameObject("Ambient Backdrop");
            backdropObject.transform.position = new Vector3(0f, 0f, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(OrthographicSize * 1.4f, OrthographicSize * 2.2f);
            backdrop.particleCount = 80;
            backdrop.maxAlpha = 0.1f;
            backdrop.tint = new Color(0.6f, 0.7f, 1f, 1f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            PhysicsMaterial2D marbleMaterial = EnsureMaterial(MarbleMaterialPath, "RomMarble", config.marbleBounciness, 0.1f);
            PhysicsMaterial2D pegMaterial = EnsureMaterial(PegMaterialPath, "RomPeg", 0.6f, 0.05f);

            // --- Walls: keep marbles inside the field ---
            float halfWidth = config.FieldHalfWidth;
            float top = config.spawnY + 1.5f;
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
                AddRect(wall.transform, "Visual", rounded ?? circle, new Vector2(0.12f, top - bottom),
                    new Color(0.5f, 0.6f, 0.9f, 0.35f), 0);
            }

            // --- Pegs between gate rows ---
            var pegs = new GameObject("Pegs");
            for (int row = 0; row < config.RowCount; row++)
            {
                for (int line = 0; line < config.pegLinesPerRow; line++)
                {
                    int count = config.PegsInLine(line);
                    for (int i = 0; i < count; i++)
                    {
                        var peg = new GameObject($"Peg_{row}_{line}_{i}");
                        peg.transform.SetParent(pegs.transform, false);
                        peg.transform.position = new Vector3(config.PegX(line, i), config.PegLineY(row, line), 0f);
                        peg.transform.localScale = Vector3.one * (config.pegRadius * 2f);

                        var pegRenderer = peg.AddComponent<SpriteRenderer>();
                        pegRenderer.sprite = circle;
                        pegRenderer.color = new Color(0.62f, 0.7f, 0.95f, 0.9f);
                        pegRenderer.sortingOrder = 2;

                        var collider = peg.AddComponent<CircleCollider2D>();
                        collider.radius = 0.5f;
                        collider.sharedMaterial = pegMaterial;
                    }
                }
            }

            // --- Gates: a trigger band per lane, row-major ---
            var gatesRoot = new GameObject("Gates");
            int lanes = config.Lanes;
            var zones = new TriggerZone2D[config.RowCount * lanes];
            var bars = new SpriteRenderer[zones.Length];
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
                        new Vector2(config.laneWidth * 0.94f, config.gateThickness), Color.white, 1);
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

            var visual = new GameObject("Visual");
            visual.transform.SetParent(template.transform, false);
            visual.transform.localScale = Vector3.one * (config.marbleRadius * 2f);
            var visualRenderer = visual.AddComponent<SpriteRenderer>();
            visualRenderer.sprite = circle;
            visualRenderer.color = Color.white;
            visualRenderer.sortingOrder = 10;

            Marble2D marble = template.AddComponent<Marble2D>();
            marble.Bind(visual.transform, visualRenderer);
            template.SetActive(false);

            // --- Debris for gate pops ---
            var shatterObject = new GameObject("Shatter VFX");
            ShatterBurst2D shatter = shatterObject.AddComponent<ShatterBurst2D>();
            shatter.shardsPerBurst = 6;
            shatter.sparksPerBurst = 5;
            shatter.Apply(
                new Texture[] { GeneratedArt.Diamond(), GeneratedArt.Star(), GeneratedArt.Shard(), GeneratedArt.Sliver() },
                GeneratedArt.SoftDot(), GeneratedArt.Ring(), 6);

            // --- Runner + simulation ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            RomSimulation simulation = runnerObject.AddComponent<RomSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            // Random by default: the lane order and every drop come from the seed, so each Play is a
            // different course. The drawn seed is logged, so a take worth keeping can still be re-rendered.
            runner.randomizeSeedOnStart = true;

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

            // --- Gate labels: screen-space text that follows the gates ---
            WorldLabelLayer labels = BuildLabelLayer(camera);

            // --- HUD: the count is the hook ---
            string question = "HOW MANY WILL REACH BOTTOM?";
            CaptionHud hud = CaptionCanvasBuilder.Build(
                CountFormat.Compact(config.startMarbles), question, string.Empty, CaptionCanvasBuilder.Style.Dark);

            RomPresenter presenter = runnerObject.AddComponent<RomPresenter>();
            presenter.Bind(simulation, hud, labels, bars, shatter, camera, question);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            var log = new StringBuilder();
            log.AppendLine($"Built {ScenePath}: {config.RowCount} rows x {lanes} lanes, {config.maxBodies} body cap, " +
                           $"{config.startMarbles} marbles released every {config.dropIntervalSeconds:0.0}s.");
            for (int row = 0; row < config.RowCount; row++)
            {
                var line = new StringBuilder($"  row {row + 1} (y {config.GateY(row):0.0}):");
                foreach (GateDef gate in config.rows[row].gates)
                {
                    line.Append(' ').Append(gate.Label);
                }

                log.AppendLine(line.ToString());
            }

            log.Append("  Random seed each Play (logged). Run length is untuned: watch one run, then adjust " +
                       "startMarbles / dropIntervalSeconds / gravityScale in the config.");
            Debug.Log(log.ToString());
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

        static WorldLabelLayer BuildLabelLayer(Camera camera)
        {
            var canvasObject = new GameObject("Gate Label Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Under the HUD canvas, so the count is never behind a gate label.
            canvas.sortingOrder = -1;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            WorldLabelLayer layer = canvasObject.AddComponent<WorldLabelLayer>();
            layer.Bind(camera, (RectTransform)canvasObject.transform);
            return layer;
        }

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
            config.maxDurationSeconds = 120f;
            // First guess, never watched: 8 marbles dropped 0.9s apart is ~7s of releases, plus ~10-12s
            // of falling through 6 rows, so ~20-25s. Slow gravity or add drops to lengthen.
            config.startMarbles = 8;
            config.dropIntervalSeconds = 0.9f;
            config.gravityScale = 0.4f;
            config.intent = "rom-001: 4 lanes x 6 rows (x2..x100, +n, /2, x0), 8 marbles dropped 0.9s apart, 400-body cap, " +
                            "seeded lane order every run, random seed per Play.";
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
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
