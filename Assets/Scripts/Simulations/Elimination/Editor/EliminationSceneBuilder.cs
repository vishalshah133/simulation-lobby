using System.Collections.Generic;
using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared;
using SimulationLobby.Shared.EditorTools;
using SimulationLobby.Shared.Tracks;
using SimulationLobby.Simulations.Race;
using SimulationLobby.Simulations.Race.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SimulationLobby.Simulations.Elimination.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Scenes/Elimination_Show_3D.unity</c>: sixteen named marbles, six heats on six different
    /// courses, one champion. Six race rigs (built by the Race editor tools, far apart so no camera sees another),
    /// a board stage with an overview camera, the tile board and the caption canvas, and an orchestrator that
    /// switches the rigs on one at a time.
    /// Menu: <c>Simulation Lobby ▸ 3D Formats ▸ Elimination (elim) ▸ Elimination Show (elim-001)</c>.
    /// </summary>
    /// <remarks>
    /// Re-running replaces the scene and the rig meshes but never the config or layout assets, so tuning survives.
    /// The runtime assembly does not reference Race: only this editor assembly does, to build the rigs.
    /// </remarks>
    static class EliminationSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Elimination_Show_3D.unity";
        const string Folder = "Assets/Settings/Configs/Elimination";
        const string MeshFolder = "Assets/Settings/Studio/Meshes/Elimination";
        const string MaterialFolder = "Assets/Settings/Studio";
        const string ConfigPath = Folder + "/EliminationConfig_001.asset";
        const string HeatRaceConfigPath = Folder + "/ElimHeatRaceConfig.asset";

        /// <summary>Distance between rig origins along x. Each track is under ~60 m wide, so neighbours sit well past the fog.</summary>
        const float RigSpacing = 400f;
        const float FogStart = 45f;
        const float FogEnd = 190f;
        const float FloorScale = 3400f;

        /// <summary>Seconds of a typical Grand Prix race (race-001: 2:05 video, 12 marbles), used to scale the layout model into real time.</summary>
        const float GrandPrixRealSeconds = 105f;
        const float RaceHoldAndGraceSeconds = 5.5f;

        const int LeaderboardRows = 16;

        [MenuItem("Simulation Lobby/3D Formats/Elimination (elim)/Elimination Show (elim-001)", false, 271)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[elim] Exit play mode before building the scene.");
                return;
            }

            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(MeshFolder);
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory("Assets/Scenes");

            // --- Assets ---
            TrackLayout grandPrix = EnsureGrandPrixLayout();
            var extra = new TrackLayout[EliminationLayouts.Count];
            for (int i = 0; i < extra.Length; i++)
            {
                extra[i] = EnsureLayout(i);
            }

            RaceConfig heatRace = EnsureHeatRaceConfig(grandPrix);
            EliminationConfig config = EnsureConfig(grandPrix, extra, heatRace);
            if (!config.Validate(out string configError))
            {
                Debug.LogError($"[elim] The config is not playable: {configError}", config);
                return;
            }

            int heatCount = config.heats.Length;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Stage: the board beats' backdrop and camera, one slot west of heat 1. ---
            Vector3 stageCentre = new Vector3(-RigSpacing, 0f, 0f);
            Camera overview = BuildOverviewCamera(stageCentre);
            BuildStage(stageCentre, config, out Transform ring, out Renderer[] stageMarbles);

            // --- Rigs: one per heat, built by the Race tools, parked far apart and switched off. ---
            var rigParent = new GameObject("Heat Rigs").transform;
            var rigs = new RaceRig[heatCount];
            var rigRoots = new GameObject[heatCount];
            for (int h = 0; h < heatCount; h++)
            {
                EliminationHeat heat = config.heats[h];
                Vector3 origin = new Vector3(h * RigSpacing, 0f, 0f);
                string prefix = $"Elim{h + 1}";
                rigs[h] = RaceRigBuilder.BuildRig(heat.layout, heatRace, origin, rigParent, heat.title, MeshFolder, prefix, LeaderboardRows);
                rigs[h].root.name = $"Heat {h + 1} Rig - {heat.title}";
                rigRoots[h] = rigs[h].root;

                int crossings = TrackMeshBuilder.CountCrossings(rigs[h].samples, out string where);
                if (crossings > 0)
                {
                    Debug.LogWarning($"[elim] Heat {h + 1} '{heat.title}' passes through itself {crossings} times, first {where}. Loosen a radius or add drop.");
                }
            }

            // --- Environment, once. The Race look is built for one camera; give each rig its own reflection probe. ---
            Bounds all = rigs[0].bounds;
            for (int h = 1; h < heatCount; h++)
            {
                all.Encapsulate(rigs[h].bounds);
            }

            Bounds stageBounds = new Bounds(stageCentre + Vector3.up * 1.5f, new Vector3(16f, 6f, 16f));
            all.Encapsulate(stageBounds);

            RaceStudioEnvironment.Build(overview, rigs[0].bounds, all.center, FloorScale, FogStart, FogEnd);
            var heatProbes = new ReflectionProbe[heatCount];
            var known = new HashSet<ReflectionProbe>(Object.FindObjectsByType<ReflectionProbe>());
            heatProbes[0] = FirstOf(known);
            for (int h = 1; h < heatCount; h++)
            {
                RaceStudioEnvironment.BuildReflections(rigs[h].bounds);
                heatProbes[h] = NewProbe(known);
            }

            RaceStudioEnvironment.BuildReflections(stageBounds);

            // Probes are Realtime / OnAwake, but a rig is switched off at Awake, so its probe would only ever see the
            // empty floor. Heat probes render on demand (the presenter does it once the rig is on).
            foreach (ReflectionProbe probe in heatProbes)
            {
                if (probe != null)
                {
                    probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                }
            }

            // Rigs go dark; each camera's listener stays off until the presenter hands it the ears.
            for (int h = 0; h < heatCount; h++)
            {
                AudioListener listener = rigs[h].camera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }

                rigRoots[h].SetActive(false);
            }

            CheckSeparation(rigs, config);

            // --- Show root: orchestrator + presenter + runner. ---
            var show = new GameObject("Elimination Show");
            var simulation = show.AddComponent<EliminationSimulation>();
            simulation.Bind(rigRoots);
            var presenter = show.AddComponent<EliminationPresenter>();
            var sfx = show.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;

            var confettiObject = new GameObject("Confetti");
            confettiObject.transform.SetParent(show.transform, false);
            ConfettiBurst3D confetti = confettiObject.AddComponent<ConfettiBurst3D>();

            var runner = show.AddComponent<SimulationRunner>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            // --- Canvases. ---
            EliminationBoardHud board = BuildBoardCanvas();
            BuildCaptionCanvas(out CaptionHud caption, out TMP_Text captionText, out CanvasGroup plate);

            presenter.Bind(board, caption, captionText, plate, overview, ring, stageMarbles, sfx, confetti, heatProbes);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Report(config, rigs);
        }

        [MenuItem("Simulation Lobby/3D Formats/Elimination (elim)/Reset Elimination Layouts (default tracks)", false, 291)]
        static void ResetLayouts()
        {
            Directory.CreateDirectory(Folder);
            for (int i = 0; i < EliminationLayouts.Count; i++)
            {
                TrackLayout layout = EnsureLayout(i);
                EliminationLayouts.Apply(layout, i);
                EditorUtility.SetDirty(layout);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[elim] Reset the {EliminationLayouts.Count} elimination layouts in {Folder} to their default courses. Rebuild the scene to bake them.");
        }

        // ------------------------------------------------------------------ assets

        static string LayoutPath(int index)
        {
            return $"{Folder}/{EliminationLayouts.FileNames[index]}.asset";
        }

        /// <summary>Heat 1 reuses the Grand Prix course exactly. Loaded, never modified; created from the default course only if the race scene was never built.</summary>
        /// <summary>
        /// Heat 1's own copy of the Grand Prix course, with the peg garden made a plinko field
        /// (<see cref="EliminationLayouts.ApplyGrandPrix"/>). The race's published layout is never loaded or changed.
        /// </summary>
        static TrackLayout EnsureGrandPrixLayout()
        {
            string path = Folder + "/" + EliminationLayouts.GrandPrixFileName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TrackLayout>(path);
            if (existing != null)
            {
                if (existing.version < TrackLayout.CurrentVersion ||
                    existing.pegWallClearance < EliminationLayouts.PegWallClearance)
                {
                    EliminationLayouts.ApplyGrandPrix(existing);
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[elim] Upgraded {path} (plinko peg garden).", existing);
                }

                return existing;
            }

            Directory.CreateDirectory(Folder);
            var layout = ScriptableObject.CreateInstance<TrackLayout>();
            EliminationLayouts.ApplyGrandPrix(layout);
            AssetDatabase.CreateAsset(layout, path);
            Debug.Log($"[elim] Created {path}: the Grand Prix course with a plinko peg garden.");
            return layout;
        }

        static TrackLayout EnsureLayout(int index)
        {
            string path = LayoutPath(index);
            var existing = AssetDatabase.LoadAssetAtPath<TrackLayout>(path);
            if (existing != null)
            {
                // pegWallClearance 0 means the layout predates the peg-trap fix: rebuild it from the defaults.
                if (existing.version < TrackLayout.CurrentVersion ||
                    existing.pegWallClearance < EliminationLayouts.PegWallClearance)
                {
                    EliminationLayouts.Apply(existing, index);
                    EditorUtility.SetDirty(existing);
                    Debug.Log($"[elim] Upgraded {path} to course version {TrackLayout.CurrentVersion} (peg clearance {EliminationLayouts.PegWallClearance:0.00} m).", existing);
                }

                return existing;
            }

            var layout = ScriptableObject.CreateInstance<TrackLayout>();
            EliminationLayouts.Apply(layout, index);
            AssetDatabase.CreateAsset(layout, path);
            return layout;
        }

        static RaceConfig EnsureHeatRaceConfig(TrackLayout grandPrix)
        {
            var config = AssetDatabase.LoadAssetAtPath<RaceConfig>(HeatRaceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<RaceConfig>();
                config.fixedTimestep = 1f / 120f;
                config.maxDurationSeconds = 300f;
                config.maxEntrants = 16;
                config.intent = "elim-001 heats: the race defaults, sized for a 16-marble field. Entrants and safe count come from the show; " +
                                "the roster here only sizes the leaderboard.";
                HeatEntrant[] roster = EliminationConfig.DefaultRoster();
                config.roster = new RaceContender[roster.Length];
                for (int i = 0; i < roster.Length; i++)
                {
                    config.roster[i] = new RaceContender(roster[i].name, roster[i].color);
                }

                AssetDatabase.CreateAsset(config, HeatRaceConfigPath);
            }

            if (config.track == null)
            {
                config.track = grandPrix;
                EditorUtility.SetDirty(config);
            }

            return config;
        }

        static EliminationConfig EnsureConfig(TrackLayout grandPrix, TrackLayout[] extra, RaceConfig heatRace)
        {
            var config = AssetDatabase.LoadAssetAtPath<EliminationConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<EliminationConfig>();
                config.fixedTimestep = 1f / 120f;
                config.maxDurationSeconds = 1800f;
                config.intent = "elim-001: 16 marbles, 6 races, 1 champion. The rules are fixed (cut the last, one last chance at the " +
                                "midpoint, a final); only the theme and the seed change between episodes.";
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            if (config.roster == null || config.roster.Length != 16)
            {
                config.roster = EliminationConfig.DefaultRoster();
            }

            if (config.heats == null || config.heats.Length != 6)
            {
                config.heats = EliminationConfig.DefaultHeats();
            }

            // The layouts are references, so they are re-pointed every build; titles, rules and counts are left as authored.
            config.heats[0].layout = grandPrix;
            for (int h = 1; h < config.heats.Length && h - 1 < extra.Length; h++)
            {
                config.heats[h].layout = extra[h - 1];
            }

            config.heatSimulationConfig = heatRace;
            EditorUtility.SetDirty(config);
            return config;
        }

        // ------------------------------------------------------------------ stage

        static Camera BuildOverviewCamera(Vector3 stageCentre)
        {
            var go = new GameObject("Overview Camera") { tag = "MainCamera" };
            Camera camera = go.AddComponent<Camera>();
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 400f;
            go.AddComponent<AudioListener>();
            go.transform.position = stageCentre + new Vector3(0f, 3.6f, -11f);
            go.transform.LookAt(stageCentre + new Vector3(0f, 1.1f, 0f));
            RaceRigBuilder.ConfigureCamera(camera);
            return camera;
        }

        /// <summary>A slow ring of sixteen glass marbles behind the board: bright when in, dark and small when out.</summary>
        static void BuildStage(Vector3 centre, EliminationConfig config, out Transform ring, out Renderer[] marbles)
        {
            var stage = new GameObject("Stage");
            stage.transform.position = centre;
            var ringObject = new GameObject("Marble Ring");
            ringObject.transform.SetParent(stage.transform, false);
            ring = ringObject.transform;

            Material material = EnsureMaterial("EliminationStageMarble", "Universal Render Pipeline/Complex Lit", m =>
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Metallic", 0.1f);
                m.SetFloat("_Smoothness", 0.9f);
                if (m.HasProperty("_ClearCoat"))
                {
                    m.SetFloat("_ClearCoat", 1f);
                    m.SetFloat("_ClearCoatMask", 1f);
                    m.SetFloat("_ClearCoatSmoothness", 0.98f);
                    m.EnableKeyword("_CLEARCOAT");
                }
            });

            int count = config.roster.Length;
            marbles = new Renderer[count];
            const float radius = 5.2f;
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                var marble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marble.name = $"Stage Marble {config.roster[i].name}";
                marble.transform.SetParent(ring, false);
                marble.transform.localPosition = new Vector3(Mathf.Sin(angle) * radius, 0.5f, Mathf.Cos(angle) * radius);
                marble.transform.localScale = Vector3.one;
                Object.DestroyImmediate(marble.GetComponent<Collider>());
                var renderer = marble.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                marbles[i] = renderer;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", config.roster[i].color);
                renderer.SetPropertyBlock(block);
            }
        }

        static ReflectionProbe FirstOf(HashSet<ReflectionProbe> probes)
        {
            foreach (ReflectionProbe probe in probes)
            {
                return probe;
            }

            return null;
        }

        /// <summary>The probe that appeared since <paramref name="known"/> was taken; adds it to the set.</summary>
        static ReflectionProbe NewProbe(HashSet<ReflectionProbe> known)
        {
            foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>())
            {
                if (known.Add(probe))
                {
                    return probe;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ canvases

        static EliminationBoardHud BuildBoardCanvas()
        {
            var canvasObject = new GameObject("Board Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            ConfigureScaler(canvasObject);
            return canvasObject.AddComponent<EliminationBoardHud>();
        }

        /// <summary>
        /// The caption: one line, bottom-centre, white bold text on a dark plate. Sorts above every rig's leaderboard
        /// (which is at the left edge and the top right), and sits clear of the track action.
        /// </summary>
        static void BuildCaptionCanvas(out CaptionHud hud, out TMP_Text text, out CanvasGroup plateGroup)
        {
            var canvasObject = new GameObject("Caption Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            ConfigureScaler(canvasObject);
            hud = canvasObject.AddComponent<CaptionHud>();

            var plate = new GameObject("Plate", typeof(RectTransform));
            plate.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)plate.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 120f);

            var image = plate.AddComponent<Image>();
            image.color = new Color(0.04f, 0.04f, 0.06f, 0.88f);
            image.raycastTarget = false;
            plateGroup = plate.AddComponent<CanvasGroup>();
            plateGroup.alpha = 0f;
            plateGroup.blocksRaycasts = false;

            var layout = plate.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(70, 70, 26, 34);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = plate.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textObject = new GameObject("Caption", typeof(RectTransform));
            textObject.transform.SetParent(plate.transform, false);
            var tmp = textObject.AddComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.fontSize = 112f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.characterSpacing = 6f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            text = tmp;

            hud.Bind(tmp, null, null);
        }

        static void ConfigureScaler(GameObject canvasObject)
        {
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
        }

        // ------------------------------------------------------------------ checks + report

        /// <summary>No rig camera may see another rig: the gap between neighbouring tracks must exceed the fog's far distance.</summary>
        static void CheckSeparation(RaceRig[] rigs, EliminationConfig config)
        {
            for (int a = 0; a < rigs.Length; a++)
            {
                for (int b = a + 1; b < rigs.Length; b++)
                {
                    float gap = FlatDistance(rigs[a].bounds, rigs[b].bounds);
                    if (gap < FogEnd + 40f)
                    {
                        Debug.LogWarning($"[elim] Heats {a + 1} and {b + 1} are only {gap:0}m apart (fog ends at {FogEnd:0}m). " +
                                         "A camera could see the other rig. Raise RigSpacing or narrow the track.");
                    }
                }
            }
        }

        static float FlatDistance(Bounds a, Bounds b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
            float dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static void Report(EliminationConfig config, RaceRig[] rigs)
        {
            // Scale the layout model (rolling-sphere physics, no obstacles) into real time by the Grand Prix, whose
            // real race time is known: heat time = hold + model * (GP real / GP model).
            float scale = -1f;
            for (int h = 0; h < rigs.Length; h++)
            {
                if (config.heats[h].layout != null && config.heats[h].layout.name == "RaceTrackLayout_GrandPrix")
                {
                    float model = TrackMeshBuilder.EstimateSeconds(rigs[h].samples, rigs[h].finishDistance);
                    scale = model > 0.1f ? GrandPrixRealSeconds / model : -1f;
                }
            }

            if (scale <= 0f)
            {
                scale = 3.5f;
            }

            float total = config.introSeconds + config.twistSeconds + config.payoffSeconds;
            var lines = new System.Text.StringBuilder();
            for (int h = 0; h < rigs.Length; h++)
            {
                EliminationHeat heat = config.heats[h];
                float model = TrackMeshBuilder.EstimateSeconds(rigs[h].samples, rigs[h].finishDistance);
                float race = model * scale * (heat.rule == HeatRule.CutLast ? 0.95f : 1f);
                float heatSeconds = config.heatIntroSeconds + RaceHoldAndGraceSeconds + race + config.heatOutroSeconds + config.resultSeconds;
                total += heatSeconds;
                lines.AppendLine($"  Heat {h + 1} {heat.title,-14} {rigs[h].totalLength,6:0}m  race ~{race,4:0}s  heat ~{heatSeconds,4:0}s");
            }

            string verdict = total < 600f || total > 900f ? " WARNING: outside the 10-15 minute band." : "";
            Debug.Log($"[elim] Built {ScenePath}: {rigs.Length} heats, {config.roster.Length} marbles.\n{lines}" +
                      $"  Estimated length ~{total / 60f:0.0} min ({total:0}s; target 11-12, band 10-15){verdict}\n" +
                      "  The race times scale the layout model by the Grand Prix's known time. A model, not a measurement: read the real " +
                      "times off the first Play and lengthen the tracks, not the holds, if it runs short.");
            if (total < 600f || total > 900f)
            {
                Debug.LogWarning($"[elim] Estimated length {total / 60f:0.0} min is outside the 10-15 minute band.");
            }
        }

        static Material EnsureMaterial(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[elim] Shader '{shaderName}' not found for {name}.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) { name = name };
            setup(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
