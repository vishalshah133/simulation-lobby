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

namespace SimulationLobby.Simulations.HauntedPumpkin.EditorTools
{
    /// <summary>
    /// Builds <c>HauntedPumpkin_Pumpkin_2D</c> (hall-001) from code.
    /// Menu: <c>Simulation Lobby ▸ 2D Formats ▸ Haunted Pumpkin (hall) ▸ Pumpkin</c>. Re-running replaces the scene
    /// and keeps the existing config asset, so tuning survives a rebuild. Ring geometry comes from the config, so
    /// after changing ring fields, rebuild.
    /// </summary>
    static class HauntedPumpkinSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/HauntedPumpkin_Pumpkin_2D.unity";
        const string ConfigPath = "Assets/Settings/Configs/HauntedPumpkinConfig_001.asset";
        const string MelodyPath = "Assets/Settings/Melodies/ToccataAndFugue.asset";
        const string BouncyPath = "Assets/Settings/Configs/PerfectlyElastic.physicsMaterial2D";

        // Same 9:16 framing as esc/brk/mve: half-height 9.6 gives a 10.8-wide frame.
        const float OrthographicSize = 10.5f;

        static readonly Color Background = new Color(0.045f, 0.015f, 0.08f, 1f);
        static readonly Color GhostGreen = new Color(0.35f, 1f, 0.45f, 1f);

        /// <summary>Innermost first: bone, candy corn, cobweb. High contrast against the orange pumpkin and each other.</summary>
        static readonly Color[] RingPalette =
        {
            new Color(0.97f, 0.94f, 0.82f),   // bone
            new Color(1f, 0.78f, 0.12f),      // candy-corn yellow
            new Color(0.74f, 0.8f, 1f)        // cobweb
        };

        [MenuItem("Simulation Lobby/2D Formats/Haunted Pumpkin (hall)/Pumpkin", false, 207)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[hall] Exit play mode before building the scene.");
                return;
            }

            HauntedPumpkinConfig config = EnsureConfig();
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[hall] Config '{config.name}' is invalid: {error}. Scene not built.", config);
                return;
            }

            MelodySequence song = EnsureMelody();

            Sprite circle = GeneratedArt.Circle();
            Sprite square = GeneratedArt.Square();
            Sprite glowSprite = GeneratedArt.SoftGlow();
            PhysicsMaterial2D bouncy = EnsureBouncyMaterial();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene unloads unused assets, which destroys a config/melody created a moment ago on a first build
            // (SetDirty then threw MissingReferenceException). Re-load both from disk so the scene references live assets.
            config = AssetDatabase.LoadAssetAtPath<HauntedPumpkinConfig>(ConfigPath);
            song = AssetDatabase.LoadAssetAtPath<MelodySequence>(MelodyPath);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.nearClipPlane = 0.01f;
            cameraObject.AddComponent<AudioListener>();

            // --- Ambient embers drifting up ---
            var backdropObject = new GameObject("Ambient Embers");
            backdropObject.transform.position = new Vector3(0f, 0f, 2f);
            backdropObject.AddComponent<ParticleSystem>();
            AmbientDriftField backdrop = backdropObject.AddComponent<AmbientDriftField>();
            backdrop.fieldSize = new Vector2(OrthographicSize * 1.4f, OrthographicSize * 2.2f);
            backdrop.particleCount = 70;
            backdrop.maxAlpha = 0.2f;
            backdrop.tint = new Color(1f, 0.5f, 0.1f, 1f);
            backdrop.flow = new Vector2(0f, 0.25f);
            backdrop.sortingOrder = -50;
            backdrop.Apply(circle.texture);

            // --- The pumpkin: glow, body, face, stem. All behind the rings and ghosts. ---
            float outer = config.RingOuterRadius(config.ringCount - 1);
            var pumpkin = new GameObject("Pumpkin");

            SpriteRenderer glow = MakeSprite("Glow", pumpkin.transform, glowSprite, Vector3.zero,
                Vector3.one * (outer + 3.4f) * 2f, new Color(1f, 0.45f, 0.05f, 0.6f), -20);
            MakeSprite("Body", pumpkin.transform, circle, Vector3.zero,
                Vector3.one * (outer + 0.28f) * 2f, new Color(0.46f, 0.15f, 0.02f, 1f), -10);
            float faceScale = outer / 3.7f;
            Color faceColor = new Color(0.07f, 0.02f, 0.09f, 0.92f);
            SpriteRenderer eyeLeft = MakeSprite("EyeLeft", pumpkin.transform, square,
                new Vector3(-1.5f, 1.35f, 0f) * faceScale, Vector3.one * 0.95f * faceScale, faceColor, -9);
            eyeLeft.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            SpriteRenderer eyeRight = MakeSprite("EyeRight", pumpkin.transform, square,
                new Vector3(1.5f, 1.35f, 0f) * faceScale, Vector3.one * 0.95f * faceScale, faceColor, -9);
            eyeRight.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            MakeSprite("Mouth", pumpkin.transform, square,
                new Vector3(0f, -1.45f, 0f) * faceScale, new Vector3(3.4f, 0.6f, 1f) * faceScale, faceColor, -9);
            MakeSprite("Stem", pumpkin.transform, square,
                new Vector3(0f, outer + 0.5f, 0f), new Vector3(0.5f, 0.55f, 1f), new Color(0.3f, 0.5f, 0.12f, 1f), -9);

            // --- Rings: innermost first. Kinematic body per ring so spinning moves one body, not N colliders. ---
            var segmentMaterial = new Material(Shader.Find("Sprites/Default")) { name = "HallSegment" };
            var ringsRoot = new GameObject("Rings");
            var rings = new SegmentedRing2D[config.ringCount];
            for (int i = 0; i < config.ringCount; i++)
            {
                var ringObject = new GameObject($"Ring_{i + 1}");
                ringObject.transform.SetParent(ringsRoot.transform, false);
                SegmentedRing2D ring = ringObject.AddComponent<SegmentedRing2D>();
                ring.innerRadius = config.RingInnerRadius(i);
                ring.thickness = config.ringThickness;
                ring.segmentCount = config.SegmentsInRing(i);
                ring.arcSteps = 5;
                ring.visualGapFraction = 0.12f;
                ring.color = RingPalette[i % RingPalette.Length];
                ring.visualMaterial = segmentMaterial;
                ring.sortingOrder = 0;
                ring.Rebuild();

                var ringBody = ringObject.AddComponent<Rigidbody2D>();
                ringBody.bodyType = RigidbodyType2D.Kinematic;
                ringBody.interpolation = RigidbodyInterpolation2D.None;

                for (int s = 0; s < ring.SegmentCount; s++)
                {
                    ring.GetCollider(s).sharedMaterial = bouncy;
                }

                rings[i] = ring;
            }

            // --- Ghost template: inactive, cloned by the pool ---
            var ghosts = new GameObject("Ghosts");
            var template = new GameObject("Ghost");
            template.transform.SetParent(ghosts.transform, false);

            var body = template.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.freezeRotation = true;
            // Discrete is enough at 6 u/s and a 0.3 wall (0.06 per step); continuous on 400 bodies costs
            // frame rate for no gain. The simulation checks for leaks anyway.
            body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var collider = template.AddComponent<CircleCollider2D>();
            collider.radius = config.ghostRadius;
            collider.sharedMaterial = bouncy;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(template.transform, false);
            visual.transform.localScale = Vector3.one * config.ghostRadius * 2f;
            var ghostRenderer = visual.AddComponent<SpriteRenderer>();
            ghostRenderer.sprite = circle;
            ghostRenderer.color = GhostGreen;
            ghostRenderer.sortingOrder = 6;

            // A soft halo child (not scaled by the presenter) makes a small green dot read as a glowing ghost.
            var halo = new GameObject("Halo");
            halo.transform.SetParent(template.transform, false);
            halo.transform.localScale = Vector3.one * config.ghostRadius * 5.2f;
            var haloRenderer = halo.AddComponent<SpriteRenderer>();
            haloRenderer.sprite = glowSprite;
            haloRenderer.color = new Color(0.3f, 1f, 0.45f, 0.5f);
            haloRenderer.sortingOrder = 5;

            GhostBody ghostBody = template.AddComponent<GhostBody>();
            ghostBody.Bind(visual.transform, ghostRenderer);
            template.SetActive(false);

            // --- Debris: one signature shape per ring (bone slivers, candy diamonds, web arcs) ---
            var shatterObject = new GameObject("Shatter VFX");
            ShatterBurst2D shatter = shatterObject.AddComponent<ShatterBurst2D>();
            shatter.Apply(
                new Texture[] { GeneratedArt.Sliver(), GeneratedArt.Diamond(), GeneratedArt.ArcChunk() },
                GeneratedArt.SoftDot(), GeneratedArt.Ring(), 8);

            // --- Runner + simulation ---
            var runnerObject = new GameObject("Simulation");
            SimulationRunner runner = runnerObject.AddComponent<SimulationRunner>();
            HauntedPumpkinSimulation simulation = runnerObject.AddComponent<HauntedPumpkinSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("_ghostTemplate").objectReferenceValue = template;
            serialized.FindProperty("_ghostParent").objectReferenceValue = ghosts.transform;
            SerializedProperty ringsProperty = serialized.FindProperty("_rings");
            ringsProperty.arraySize = rings.Length;
            for (int i = 0; i < rings.Length; i++)
            {
                ringsProperty.GetArrayElementAtIndex(i).objectReferenceValue = rings[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            // --- HUD ---
            HauntedPumpkinHud hud = BuildHud(config, square, circle);

            // --- Audio: one note of the tune per break ---
            var audioObject = new GameObject("Break Audio");
            BounceMelodyPlayer melody = audioObject.AddComponent<BounceMelodyPlayer>();
            melody.melody = song;
            melody.noteDuration = 1f;
            melody.voiceCount = 8;
            melody.volume = 0.55f;

            HauntedPumpkinPresenter presenter = runnerObject.AddComponent<HauntedPumpkinPresenter>();
            presenter.Bind(simulation, hud, melody, shatter, glow, circle, square);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            var log = new StringBuilder();
            log.AppendLine($"Built {ScenePath} ({config.ringCount} rings, song {song.songTitle}, {song.Count} notes)");
            for (int i = 0; i < config.ringCount; i++)
            {
                log.AppendLine($"  ring {i + 1}: r {config.RingInnerRadius(i):0.00}-{config.RingOuterRadius(i):0.00}, " +
                               $"{config.SegmentsInRing(i)} segments x {config.HitPointsForRing(i)} hp, " +
                               $"hole {config.HoleChord(i):0.00} for ghost {config.ghostRadius * 2f:0.00}");
            }

            log.AppendLine($"  split chance {config.cloneChance:0.00}, candle {config.timeLimitSeconds:0}s, " +
                           $"cap {config.bodyCap} bodies");
            log.Append($"  speed {config.ghostSpeed} (safe ceiling {ConstantSpeed2D.MaxSafeSpeed(config.fixedTimestep, config.ringThickness):0.0}). " +
                       "Seed is random per Play and logged at start.");
            Debug.Log(log.ToString());
        }

        static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Vector3 position,
                                         Vector3 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        static HauntedPumpkinHud BuildHud(HauntedPumpkinConfig config, Sprite square, Sprite circle)
        {
            var canvasObject = new GameObject("Haunted Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // Match height: the Shorts frame is height-dominant (project On-screen UI rule).
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            HauntedPumpkinHud hud = canvasObject.AddComponent<HauntedPumpkinHud>();

            // Full-screen wash first, so it sits under all text.
            var overlayObject = new GameObject("Wash", typeof(RectTransform));
            overlayObject.transform.SetParent(canvasObject.transform, false);
            var overlayRect = (RectTransform)overlayObject.transform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlay = overlayObject.AddComponent<Image>();
            overlay.sprite = square;
            overlay.color = Color.clear;
            overlay.raycastTarget = false;

            // --- Top panel: the hook, stated as a question, and the song prompt ---
            RectTransform top = ScoreboardCanvasBuilder.CreatePanel(canvasObject.transform, "TopPanel",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -28f), new Vector2(-80f, 200f));
            VerticalLayout(top, TextAnchor.UpperCenter, 16f);
            TMP_Text caption = ScoreboardCanvasBuilder.CreateText(top, "Caption",
                "ESCAPE BEFORE THE CANDLE DIES?", 50f, new Color(1f, 0.66f, 0.2f, 1f), FontStyles.Bold);
            TMP_Text subline = ScoreboardCanvasBuilder.CreateText(top, "Subline", "GUESS THE SONG", 36f,
                new Color(0.78f, 0.62f, 1f, 1f), FontStyles.Bold);

            // --- Bottom panel: counters, then the result and the one-line why ---
            RectTransform bottom = ScoreboardCanvasBuilder.CreatePanel(canvasObject.transform, "BottomPanel",
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 28f), new Vector2(-80f, 460f));
            VerticalLayout(bottom, TextAnchor.LowerCenter, 8f);
            TMP_Text ghosts = ScoreboardCanvasBuilder.CreateText(bottom, "Ghosts", "GHOSTS  1", 52f,
                new Color(0.4f, 1f, 0.5f, 1f), FontStyles.Bold);
            TMP_Text rings = ScoreboardCanvasBuilder.CreateText(bottom, "Rings", $"RINGS LEFT  {config.ringCount}", 72f,
                new Color(1f, 0.62f, 0.2f, 1f), FontStyles.Bold);
            // Empty until the pumpkin breaks: bats are the payoff, not a target to count toward.
            TMP_Text bats = ScoreboardCanvasBuilder.CreateText(bottom, "Bats", string.Empty, 48f,
                new Color(0.8f, 0.5f, 1f, 1f), FontStyles.Bold);
            TMP_Text status = ScoreboardCanvasBuilder.CreateText(bottom, "Status", string.Empty, 64f,
                new Color(1f, 0.82f, 0.4f, 1f), FontStyles.Bold);
            TMP_Text why = ScoreboardCanvasBuilder.CreateText(bottom, "Why", string.Empty, 38f,
                new Color(0.86f, 0.84f, 0.92f, 1f), FontStyles.Italic);

            // --- Candle: a vertical wax bar on the right edge, in the margin beside the pumpkin ---
            var candle = new GameObject("Candle", typeof(RectTransform));
            candle.transform.SetParent(canvasObject.transform, false);
            var candleRect = (RectTransform)candle.transform;
            candleRect.anchorMin = new Vector2(1f, 0.5f);
            candleRect.anchorMax = new Vector2(1f, 0.5f);
            candleRect.pivot = new Vector2(1f, 0.5f);
            candleRect.anchoredPosition = new Vector2(-14f, -30f);
            candleRect.sizeDelta = new Vector2(52f, 700f);
            var candleBack = candle.AddComponent<Image>();
            candleBack.sprite = square;
            candleBack.color = new Color(0.16f, 0.07f, 0.24f, 0.9f);
            candleBack.raycastTarget = false;

            var fillObject = new GameObject("Wax", typeof(RectTransform));
            fillObject.transform.SetParent(candle.transform, false);
            var fillRect = (RectTransform)fillObject.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(5f, 5f);
            fillRect.offsetMax = new Vector2(-5f, -5f);
            var fill = fillObject.AddComponent<Image>();
            fill.sprite = square;
            fill.color = new Color(1f, 0.93f, 0.74f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            var flameObject = new GameObject("Flame", typeof(RectTransform));
            flameObject.transform.SetParent(candle.transform, false);
            var flameRect = (RectTransform)flameObject.transform;
            flameRect.anchorMin = new Vector2(0.5f, 1f);
            flameRect.anchorMax = new Vector2(0.5f, 1f);
            flameRect.pivot = new Vector2(0.5f, 0f);
            flameRect.anchoredPosition = Vector2.zero;
            flameRect.sizeDelta = new Vector2(60f, 96f);
            var flame = flameObject.AddComponent<Image>();
            flame.sprite = circle;
            flame.color = new Color(1f, 0.82f, 0.25f, 1f);
            flame.raycastTarget = false;

            hud.Bind(caption, subline, ghosts, rings, bats, status, why, fill, flameRect, flame, overlay);
            return hud;
        }

        static void VerticalLayout(RectTransform panel, TextAnchor alignment, float spacing)
        {
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = alignment;
            layout.spacing = spacing;
        }

        static HauntedPumpkinConfig EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HauntedPumpkinConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var config = ScriptableObject.CreateInstance<HauntedPumpkinConfig>();
            config.fixedTimestep = 0.01f;
            config.intent = "hall-001. 5 rings of 10-18 segments (1/3/6/11/16 hits), ghosts at 2.7 u/s, a break splits " +
                            "the ghost with 40% chance; the first ghost through the outer ring beats a 56.5s candle. " +
                            "Point model: breakout 45-59s (median 50). Tune timeLimitSeconds from logged ring-open times.";
            config.maxDurationSeconds = config.RunSeconds + 2f;
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static MelodySequence EnsureMelody()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MelodySequence>(MelodyPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Melodies");
            var melody = ScriptableObject.CreateInstance<MelodySequence>();
            melody.songTitle = "TOCCATA AND FUGUE IN D MINOR";
            melody.source = "J. S. Bach, Toccata and Fugue in D minor (BWV 565, c. 1704), opening phrase, " +
                            "then the same phrase an octave lower. Public domain worldwide.";
            melody.tonicHz = 293.66f;   // D4

            // Opening: A G A (held), G F E D, C# D (held). Semitones above D.
            AddNote(melody, 7, 0.5f, false);
            AddNote(melody, 5, 0.5f, false);
            AddNote(melody, 7, 2f, false);
            AddNote(melody, 5, 0.5f, false);
            AddNote(melody, 3, 0.5f, false);
            AddNote(melody, 2, 0.5f, false);
            AddNote(melody, 0, 0.5f, false);
            AddNote(melody, -1, 1f, false);
            AddNote(melody, 0, 2f, true);

            // The same line an octave lower, as in the piece.
            AddNote(melody, -5, 0.5f, false);
            AddNote(melody, -7, 0.5f, false);
            AddNote(melody, -5, 2f, false);
            AddNote(melody, -7, 0.5f, false);
            AddNote(melody, -9, 0.5f, false);
            AddNote(melody, -10, 0.5f, false);
            AddNote(melody, -12, 0.5f, false);
            AddNote(melody, -13, 1f, false);
            AddNote(melody, -12, 2f, true);

            AssetDatabase.CreateAsset(melody, MelodyPath);
            return melody;
        }

        static void AddNote(MelodySequence melody, int semitone, float beats, bool endsPhrase)
        {
            melody.notes.Add(new MelodyNote { semitone = semitone, beats = beats, endsPhrase = endsPhrase });
        }

        static PhysicsMaterial2D EnsureBouncyMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(BouncyPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings/Configs");
            var material = new PhysicsMaterial2D("PerfectlyElastic") { bounciness = 1f, friction = 0f };
            AssetDatabase.CreateAsset(material, BouncyPath);
            return material;
        }
    }
}
