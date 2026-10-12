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

namespace SimulationLobby.Simulations.Cricket.EditorTools
{
    /// <summary>
    /// Builds the <c>cric</c> scene from code. Menu: <c>Simulation Lobby ▸ Build Scene ▸ Cricket_SuperOver_2D</c>.
    /// Layout comes from the config, so after changing rows, lanes, spacing or ball size, rebuild.
    /// Teams are assets: change <c>teamA</c> / <c>teamB</c> on the config and press Play; no rebuild needed.
    /// </summary>
    static class CricketSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Cricket_SuperOver_2D.unity";
        const string ConfigPath = "Assets/Settings/Configs/CricketConfig_001.asset";
        const string TeamFolder = "Assets/Settings/Cricket/Teams";
        const string AudioFolder = "Assets/Audio/Cricket";

        // Bump when the default layout moves, so existing config assets pick it up once.
        const int LayoutVersion = 3;

        // Pos Y of the caption HUD's top panel, in canvas pixels (the shared default is -150). Higher on screen.
        const float TopPanelOffset = -50f;
        const string KitPath = "Assets/Settings/Cricket/CricketAudioKit.asset";
        const string BallMaterialPath = "Assets/Settings/Configs/CricketBall.physicsMaterial2D";
        const string PegMaterialPath = "Assets/Settings/Configs/CricketPeg.physicsMaterial2D";

        // Same 9:16 framing as the other 2D formats: half-height 9.6 gives a 10.8-wide frame. Top 3 units are
        // the score, bottom 1.2 the footer; the ground and the strip share what is between.
        const float OrthographicSize = 9.6f;

        /// <summary>Colours only: country name, three letters, two flag-adjacent colours. No crests, no kits.</summary>
        static readonly (string name, string code, Color primary, Color secondary)[] Teams =
        {
            ("INDIA", "IND", new Color(0.12f, 0.42f, 0.95f), new Color(1f, 0.6f, 0.15f)),
            ("AUSTRALIA", "AUS", new Color(1f, 0.82f, 0.1f), new Color(0.05f, 0.45f, 0.25f)),
            ("ENGLAND", "ENG", new Color(0.85f, 0.12f, 0.2f), new Color(0.1f, 0.15f, 0.45f)),
            ("PAKISTAN", "PAK", new Color(0.1f, 0.62f, 0.3f), new Color(0.9f, 0.95f, 0.9f)),
            ("SOUTH AFRICA", "RSA", new Color(0.1f, 0.55f, 0.3f), new Color(1f, 0.78f, 0.1f)),
            ("NEW ZEALAND", "NZL", new Color(0.12f, 0.12f, 0.14f), new Color(0.9f, 0.9f, 0.92f)),
            ("SRI LANKA", "SRI", new Color(0.12f, 0.25f, 0.7f), new Color(1f, 0.78f, 0.1f)),
            ("WEST INDIES", "WIN", new Color(0.55f, 0.1f, 0.2f), new Color(1f, 0.78f, 0.1f)),
            ("BANGLADESH", "BAN", new Color(0.05f, 0.5f, 0.3f), new Color(0.85f, 0.15f, 0.2f)),
            ("AFGHANISTAN", "AFG", new Color(0.1f, 0.35f, 0.75f), new Color(0.85f, 0.15f, 0.2f)),
        };

        [MenuItem("Simulation Lobby/Cricket/Create Team Assets")]
        static void CreateTeams()
        {
            EnsureTeams();
            AssetDatabase.SaveAssets();
            Debug.Log($"[cric] {Teams.Length} team assets are in {TeamFolder}. Drag any two onto a CricketConfig's Team A / Team B.");
        }

        [MenuItem("Simulation Lobby/Cricket/Fill Audio Kit")]
        static void FillKitMenu()
        {
            EnsureKit();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Simulation Lobby/Build Scene/Cricket_SuperOver_2D")]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[cric] Exit play mode before building the scene.");
                return;
            }

            CricketConfig config = EnsureConfig();
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[cric] Config '{config.name}' is invalid: {error}. Scene not built.", config);
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
            camera.backgroundColor = new Color(0.36f, 0.7f, 0.26f, 1f);
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- A sunlit outfield seen from above: mown stripes across the whole frame, a warm sun glow, and
            //     cloud shadows drifting over the grass. No particles. ---
            Sprite square = GeneratedArt.Square();
            Sprite glow = GeneratedArt.SoftGlow();
            var background = new GameObject("Sunny Outfield");
            const float stripeHeight = 1.5f;
            int stripes = Mathf.CeilToInt(OrthographicSize * 2f / stripeHeight) + 2;
            for (int i = 0; i < stripes; i++)
            {
                float y = -OrthographicSize - stripeHeight + i * stripeHeight;
                AddRect(background.transform, $"Stripe_{i:00}", square, new Vector2(OrthographicSize * 2f, stripeHeight),
                    i % 2 == 0 ? new Color(0.36f, 0.7f, 0.26f, 1f) : new Color(0.42f, 0.77f, 0.3f, 1f), -40)
                    .transform.position = new Vector3(0f, y + stripeHeight * 0.5f, 0f);
            }

            // Sun: a big warm bloom from the upper right, over the grass and under everything else.
            AddDisc(background.transform, "Sun Glow", glow, 20f, new Color(1f, 0.95f, 0.65f, 0.6f), -38)
                .transform.position = new Vector3(4.5f, 8f, 0f);
            AddDisc(background.transform, "Sun Core", glow, 6f, new Color(1f, 1f, 0.85f, 0.7f), -37)
                .transform.position = new Vector3(5.2f, 8.6f, 0f);

            var shadowItems = new Transform[3];
            Vector2[] shadowPlacement = { new Vector2(-8f, 5f), new Vector2(-3f, -1.5f), new Vector2(2f, -7f) };
            Vector2[] shadowSize = { new Vector2(9f, 4.5f), new Vector2(11f, 5f), new Vector2(8f, 4f) };
            for (int i = 0; i < shadowItems.Length; i++)
            {
                SpriteRenderer cloud = AddDisc(background.transform, $"Cloud Shadow {i + 1}", glow, 1f, new Color(0.02f, 0.18f, 0.05f, 0.2f), -36);
                cloud.transform.localScale = new Vector3(shadowSize[i].x, shadowSize[i].y, 1f);
                cloud.transform.position = new Vector3(shadowPlacement[i].x, shadowPlacement[i].y, 0f);
                shadowItems[i] = cloud.transform;
            }

            DriftingShadows drift = background.AddComponent<DriftingShadows>();
            drift.Bind(shadowItems);

            // --- The ground: grass disc, darker apron, pitch strip and the wagon wheel on top. It is kept
            //     well clear of the score above it and the strip below it (see CricketConfig). ---
            Vector2 center = config.groundCenter;
            var ground = new GameObject("Ground");
            ground.transform.position = new Vector3(center.x, center.y, 0f);
            AddDisc(ground.transform, "Rope Shadow", circle, config.groundRadius * 2f + 0.55f, new Color(0.02f, 0.15f, 0.06f, 0.55f), -12);
            AddDisc(ground.transform, "Rope", circle, config.groundRadius * 2f + 0.3f, new Color(0.97f, 0.96f, 0.9f, 1f), -11);
            AddDisc(ground.transform, "Grass", circle, config.groundRadius * 2f, new Color(0.08f, 0.36f, 0.18f, 1f), -10);
            AddDisc(ground.transform, "Infield", circle, config.groundRadius, new Color(0.11f, 0.44f, 0.23f, 1f), -9);
            AddRect(ground.transform, "Pitch", rounded ?? circle, new Vector2(0.36f, 1.7f), new Color(0.82f, 0.72f, 0.5f, 1f), -8);
            AddDisc(ground.transform, "Batter", circle, 0.22f, Color.white, -7);

            var chartObject = new GameObject("Wagon Wheel");
            chartObject.transform.position = new Vector3(center.x, center.y, 0f);
            RadialHitChart chart = chartObject.AddComponent<RadialHitChart>();
            chart.radius = config.groundRadius;
            chart.sortingOrder = 6;
            chart.Build(Color.white);

            // --- The strip: a dark panel under the pegs, so gate labels stay readable against anything.
            //     It is a plain rect below the ground and never overlaps it. ---
            float halfW = config.StripHalfWidth;
            float halfH = config.StripHalfHeight;
            Vector2 stripCenter = config.stripCenter;
            AddRect(null, "Strip Frame", rounded ?? circle, new Vector2(halfW * 2f + 0.6f, halfH * 2f + 0.8f),
                new Color(0.97f, 0.96f, 0.9f, 1f), -7).transform.position = new Vector3(stripCenter.x, stripCenter.y, 0f);
            AddRect(null, "Strip Backing", rounded ?? circle, new Vector2(halfW * 2f + 0.3f, halfH * 2f + 0.5f),
                new Color(0.04f, 0.1f, 0.18f, 0.96f), -6).transform.position = new Vector3(stripCenter.x, stripCenter.y, 0f);

            // The readout sits on a dark pill, so the big number reads against bright grass.
            SpriteRenderer readoutPill = AddRect(null, "Readout Pill", rounded ?? circle, new Vector2(7.6f, 1.45f),
                new Color(0.04f, 0.1f, 0.18f, 0.88f), -5);
            readoutPill.transform.position = new Vector3(config.readoutCenter.x, config.readoutCenter.y, 0f);

            PhysicsMaterial2D ballMaterial = EnsureMaterial(BallMaterialPath, "CricketBall", config.ballBounciness, 0.1f);
            PhysicsMaterial2D pegMaterial = EnsureMaterial(PegMaterialPath, "CricketPeg", 0.6f, 0.05f);

            // --- Walls: a box around the strip, so the ball cannot leave it ---
            var walls = new GameObject("Walls");
            float wallThickness = 0.3f;
            (string name, Vector2 position, Vector2 size)[] wallDefs =
            {
                ("Wall_Top", new Vector2(stripCenter.x, stripCenter.y + halfH + wallThickness * 0.5f), new Vector2(halfW * 2f + wallThickness * 2f, wallThickness)),
                ("Wall_Bottom", new Vector2(stripCenter.x, stripCenter.y - halfH - wallThickness * 0.5f), new Vector2(halfW * 2f + wallThickness * 2f, wallThickness)),
                ("Wall_Right", new Vector2(stripCenter.x + halfW + wallThickness * 0.5f, stripCenter.y), new Vector2(wallThickness, halfH * 2f + wallThickness * 2f)),
                ("Wall_Left", new Vector2(stripCenter.x - halfW - wallThickness * 0.5f, stripCenter.y), new Vector2(wallThickness, halfH * 2f + wallThickness * 2f)),
            };
            foreach (var def in wallDefs)
            {
                var wall = new GameObject(def.name);
                wall.transform.SetParent(walls.transform, false);
                wall.transform.position = new Vector3(def.position.x, def.position.y, 0f);
                var box = wall.AddComponent<BoxCollider2D>();
                box.size = def.size;
                box.sharedMaterial = pegMaterial;
                AddRect(wall.transform, "Visual", rounded ?? circle, def.size, new Color(0.6f, 0.9f, 0.7f, 0.45f), 0);
            }

            // --- Pegs behind each column of gates ---
            var pegs = new GameObject("Pegs");
            for (int column = 0; column < config.ColumnCount; column++)
            {
                for (int line = 0; line < config.pegLinesPerColumn; line++)
                {
                    int count = config.PegsInLine(line);
                    for (int i = 0; i < count; i++)
                    {
                        var peg = new GameObject($"Peg_{column}_{line}_{i}");
                        peg.transform.SetParent(pegs.transform, false);
                        peg.transform.position = new Vector3(config.PegLineX(column, line), config.PegY(line, i), 0f);
                        peg.transform.localScale = Vector3.one * (config.pegRadius * 2f);

                        var pegRenderer = peg.AddComponent<SpriteRenderer>();
                        pegRenderer.sprite = circle;
                        pegRenderer.color = new Color(0.72f, 0.88f, 0.78f, 0.9f);
                        pegRenderer.sortingOrder = 2;

                        var collider = peg.AddComponent<CircleCollider2D>();
                        collider.radius = 0.5f;
                        collider.sharedMaterial = pegMaterial;
                    }
                }
            }

            // --- Gates: a trigger bar per lane, column-major, lanes top to bottom ---
            var gatesRoot = new GameObject("Gates");
            int lanes = config.Lanes;
            var zones = new TriggerZone2D[config.ColumnCount * lanes];
            var bars = new SpriteRenderer[zones.Length];
            for (int column = 0; column < config.ColumnCount; column++)
            {
                for (int lane = 0; lane < lanes; lane++)
                {
                    int i = column * lanes + lane;
                    var gate = new GameObject($"Gate_c{column + 1}_l{lane + 1}");
                    gate.transform.SetParent(gatesRoot.transform, false);
                    gate.transform.position = new Vector3(config.ColumnX(column), config.LaneCenterY(lane), 0f);

                    var trigger = gate.AddComponent<BoxCollider2D>();
                    trigger.isTrigger = true;
                    trigger.size = new Vector2(config.gateThickness, config.laneHeight);
                    zones[i] = gate.AddComponent<TriggerZone2D>();

                    bars[i] = AddRect(gate.transform, "Bar", rounded ?? circle,
                        new Vector2(config.gateThickness, config.laneHeight * 0.94f), Color.white, 1);
                }
            }

            // --- The ball: one body, reused every delivery, inactive between balls ---
            var ball = new GameObject("Ball");
            var body = ball.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = config.linearDamping;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var ballCollider = ball.AddComponent<CircleCollider2D>();
            ballCollider.radius = config.ballRadius;
            ballCollider.sharedMaterial = ballMaterial;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(ball.transform, false);
            visual.transform.localScale = Vector3.one * (config.ballRadius * 2f);
            var visualRenderer = visual.AddComponent<SpriteRenderer>();
            visualRenderer.sprite = circle;
            visualRenderer.color = Color.white;
            visualRenderer.sortingOrder = 10;
            ball.SetActive(false);

            // --- Debris for gate pops and shots ---
            var shatterObject = new GameObject("Shatter VFX");
            ShatterBurst2D shatter = shatterObject.AddComponent<ShatterBurst2D>();
            shatter.shardsPerBurst = 6;
            shatter.sparksPerBurst = 5;
            shatter.Apply(
                new Texture[] { GeneratedArt.Diamond(), GeneratedArt.Star(), GeneratedArt.Shard(), GeneratedArt.Sliver() },
                GeneratedArt.SoftDot(), GeneratedArt.Ring(), 12);

            // --- Runner + simulation ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            CricketSimulation simulation = runnerObject.AddComponent<CricketSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            // Random by default: lane order, drop point and shot angle all come from the seed, so each
            // Play is a different match. The drawn seed is logged, so a good one can be re-rendered.
            runner.randomizeSeedOnStart = true;

            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("_ball").objectReferenceValue = body;
            SerializedProperty zonesProperty = serialized.FindProperty("_gateZones");
            zonesProperty.arraySize = zones.Length;
            for (int i = 0; i < zones.Length; i++)
            {
                zonesProperty.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            WorldLabelLayer labels = BuildLabelLayer(camera);

            CaptionHud hud = CaptionCanvasBuilder.Build(
                $"{config.teamA.shortName} 0/0", "SUPER OVER", string.Empty, CaptionCanvasBuilder.Style.Studio);

            // The score sits higher than the shared default; set here so other formats keep theirs.
            var topPanel = hud.transform.Find("TopPanel") as RectTransform;
            if (topPanel != null)
            {
                topPanel.anchoredPosition = new Vector2(topPanel.anchoredPosition.x, TopPanelOffset);
            }

            CricketPresenter presenter = runnerObject.AddComponent<CricketPresenter>();
            // Confetti: cannons at the sides of the readout band fire up and inward; rain falls from the top.
            var confettiObject = new GameObject("Confetti");
            ConfettiBurst2D confetti = confettiObject.AddComponent<ConfettiBurst2D>();
            confetti.leftCannon = new Vector2(-OrthographicSize * 0.5625f + 0.3f, config.readoutCenter.y);
            confetti.rightCannon = new Vector2(OrthographicSize * 0.5625f - 0.3f, config.readoutCenter.y);
            confetti.rainY = OrthographicSize + 0.9f;
            confetti.rainHalfWidth = OrthographicSize * 0.5625f + 0.5f;

            presenter.Bind(simulation, hud, labels, bars, chart, visual.transform, visualRenderer, shatter, EnsureKit(),
                readoutPill, confetti);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            var log = new StringBuilder();
            log.AppendLine($"Built {ScenePath}: {config.TeamName(0)} vs {config.TeamName(1)}, {config.oversPerInnings} over(s) " +
                           $"({config.BallsPerInnings} balls), {config.wicketsPerInnings} wickets, {config.ColumnCount} columns x {lanes} lanes.");
            for (int column = 0; column < config.ColumnCount; column++)
            {
                var line = new StringBuilder($"  column {column + 1} (x {config.ColumnX(column):0.0}, top to bottom):");
                foreach (GateDef gate in config.columns[column].gates)
                {
                    line.Append(' ').Append(gate.kind == GateKind.Pass ? "-" : gate.Label);
                }

                log.AppendLine(line.ToString());
            }

            log.Append("  Random seed each Play (logged). Run mix and length are untuned: watch a few balls, then adjust " +
                       "the gates, run bands and pacing in the config. Swap teams on the config asset any time.");
            Debug.Log(log.ToString());
        }

        static SpriteRenderer AddDisc(Transform parent, string name, Sprite circle, float diameter, Color color, int order)
        {
            var disc = new GameObject(name);
            if (parent != null)
            {
                disc.transform.SetParent(parent, false);
            }

            disc.transform.localScale = new Vector3(diameter, diameter, 1f);
            var renderer = disc.AddComponent<SpriteRenderer>();
            renderer.sprite = circle;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        static SpriteRenderer AddRect(Transform parent, string name, Sprite sprite, Vector2 size, Color color, int order)
        {
            var rect = new GameObject(name);
            if (parent != null)
            {
                rect.transform.SetParent(parent, false);
            }

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
            // Under the HUD canvas, so the score is never behind a gate label.
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

        /// <summary>
        /// Creates the audio kit if needed and fills empty slots from clips in Assets/Audio/Cricket whose
        /// file name contains the slot's name (cheer_small, cheer_big, cheer_roar, cheer_final, groan,
        /// applause, applause_soft, bat, stumps). Slots already set by hand are left alone.
        /// </summary>
        static CricketAudioKit EnsureKit()
        {
            Directory.CreateDirectory(AudioFolder);
            Directory.CreateDirectory("Assets/Settings/Cricket");
            var kit = AssetDatabase.LoadAssetAtPath<CricketAudioKit>(KitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<CricketAudioKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }

            var clips = new System.Collections.Generic.List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null)
                {
                    clips.Add(clip);
                }
            }

            var missing = new StringBuilder();
            AudioClip Find(string key, AudioClip current)
            {
                if (current != null)
                {
                    return current;
                }

                // Longest-match wins, so "applause_soft" is never taken for "applause".
                AudioClip best = null;
                foreach (AudioClip clip in clips)
                {
                    string name = clip.name.ToLowerInvariant();
                    if (name.Contains(key) && (best == null || name.Length < best.name.Length) &&
                        (key != "applause" || !name.Contains("soft")))
                    {
                        best = clip;
                    }
                }

                if (best == null)
                {
                    missing.Append(' ').Append(key);
                }

                return best;
            }

            kit.cheerSmall = Find("cheer_small", kit.cheerSmall);
            kit.cheerBig = Find("cheer_big", kit.cheerBig);
            kit.cheerRoar = Find("cheer_roar", kit.cheerRoar);
            kit.cheerFinal = Find("cheer_final", kit.cheerFinal);
            kit.groan = Find("groan", kit.groan);
            kit.applause = Find("applause", kit.applause);
            kit.applauseSoft = Find("applause_soft", kit.applauseSoft);
            kit.bat = Find("bat", kit.bat);
            kit.stumps = Find("stumps", kit.stumps);
            EditorUtility.SetDirty(kit);

            Debug.Log(clips.Count == 0
                ? $"[cric] No clips in {AudioFolder} yet, so the crowd will be silent. See the README there for what to add."
                : $"[cric] Audio kit filled from {clips.Count} clip(s) in {AudioFolder}. Empty slots:{(missing.Length == 0 ? " none" : missing.ToString())}. " +
                  "(bat and stumps fall back to procedural knocks.)");
            return kit;
        }

        static CricketTeamDef[] EnsureTeams()
        {
            Directory.CreateDirectory(TeamFolder);
            var result = new CricketTeamDef[Teams.Length];
            for (int i = 0; i < Teams.Length; i++)
            {
                string path = $"{TeamFolder}/{Teams[i].name.Replace(' ', '_')}.asset";
                var team = AssetDatabase.LoadAssetAtPath<CricketTeamDef>(path);
                if (team == null)
                {
                    team = ScriptableObject.CreateInstance<CricketTeamDef>();
                    team.displayName = Teams[i].name;
                    team.shortName = Teams[i].code;
                    team.primary = Teams[i].primary;
                    team.secondary = Teams[i].secondary;
                    AssetDatabase.CreateAsset(team, path);
                }

                result[i] = team;
            }

            return result;
        }

        static CricketConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CricketConfig>(ConfigPath);
            CricketTeamDef[] teams = EnsureTeams();
            if (existing != null)
            {
                // A config that lost its teams (asset deleted, never assigned) gets the defaults back.
                if (existing.teamA == null)
                {
                    existing.teamA = teams[0];
                }

                if (existing.teamB == null)
                {
                    existing.teamB = teams[1];
                }

                if (existing.layoutVersion < 2)
                {
                    // Version 2 moved the ground up and added a readout band between ground and strip.
                    var defaults = ScriptableObject.CreateInstance<CricketConfig>();
                    existing.groundCenter = defaults.groundCenter;
                    existing.groundRadius = defaults.groundRadius;
                    existing.stripCenter = new Vector2(existing.stripCenter.x, -5.05f);
                    UnityEngine.Object.DestroyImmediate(defaults);
                }

                if (existing.layoutVersion < 3)
                {
                    // Version 3: the readout sits at y -2.5, and the whole strip (pegs, gates, ball launch)
                    // moves down 1 from wherever it is now. Only those two change; every other tweak is left alone.
                    existing.stripCenter = new Vector2(existing.stripCenter.x, existing.stripCenter.y - 1f);
                    existing.readoutCenter = new Vector2(existing.readoutCenter.x, -2.5f);
                }

                if (existing.layoutVersion < LayoutVersion)
                {
                    existing.layoutVersion = LayoutVersion;
                    EditorUtility.SetDirty(existing);
                }

                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var config = ScriptableObject.CreateInstance<CricketConfig>();
            config.fixedTimestep = 1f / 60f;
            config.maxDurationSeconds = 240f;
            config.teamA = teams[0];
            config.teamB = teams[1];
            config.layoutVersion = LayoutVersion;
            config.intent = "cric-001: super over (6 balls, 2 wickets), India vs Australia, 5 lanes x 4 columns " +
                            "(x1.5-x3, HIT, OUT, pass), base 6 m, bands 8/14/20/30/60 m = 1/2/3/4/6, random seed per Play.";
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
