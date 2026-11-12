using System.IO;
using SimulationLobby.Core;
using SimulationLobby.Presentation;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Simulations.PerfectFit.View;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SimulationLobby.Simulations.PerfectFit.EditorTools
{
    /// <summary>
    /// Builds the <c>fit</c> scene: <c>PerfectFit_SpinningWall_3D</c> (fit-001).
    /// Menu: <c>Simulation Lobby ▸ 3D Formats ▸ Perfect Fit (fit) ▸ Spinning Wall (fit-001)</c>. Re-running replaces
    /// the scene and keeps the config and material assets, so hand tuning survives.
    /// </summary>
    /// <remarks>
    /// The look: a dark stage, a pearl-white wall (so its silhouette reads on the dark), a teal enamel shape, and a hot
    /// amber lamp behind the wall. The lamp is the legibility trick: the hole glows before the shape arrives, and once
    /// it is in, the gap is the only thing still glowing, a hairline that gets thinner every round.
    /// The wall and shape meshes are built at runtime from the config by <see cref="PerfectFitPresenter"/>.
    /// </remarks>
    static class PerfectFitSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/PerfectFit_SpinningWall_3D.unity";
        const string ConfigPath = "Assets/Settings/Configs/PerfectFitConfig_001.asset";
        const string Folder = "Assets/Settings/PerfectFit";

        static readonly Color Background = new Color(0.025f, 0.03f, 0.05f, 1f);
        static readonly Color Hue = new Color(0.05f, 0.78f, 0.86f, 1f);

        [MenuItem("Simulation Lobby/3D Formats/Perfect Fit (fit)/Spinning Wall (fit-001)", false, 321)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[fit] Exit play mode before building the scene.");
                return;
            }

            PerfectFitConfig config = EnsureConfig();
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[fit] Config is invalid: {error}");
                return;
            }

            Directory.CreateDirectory(Folder);
            Material wallMaterial = EnsureMaterial("FitWall", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.93f, 0.94f, 0.97f, 1f));
                m.SetFloat("_Smoothness", 0.55f);
                m.SetFloat("_Metallic", 0f);
            });
            Material shapeMaterial = EnsureMaterial("FitShape", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", Hue);
                m.SetFloat("_Smoothness", 0.92f);
                m.SetFloat("_Metallic", 0.15f);
            });
            Material floorMaterial = EnsureMaterial("FitFloor", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(0.02f, 0.025f, 0.04f, 1f));
                m.SetFloat("_Smoothness", 0.88f);
                m.SetFloat("_Metallic", 0f);
            });
            Material lampMaterial = EnsureMaterial("FitLamp", "Universal Render Pipeline/Unlit", m =>
            {
                // HDR amber, far above the bloom threshold, so the light squeezing through a hairline gap blooms.
                m.SetColor("_BaseColor", new Color(3.6f, 2.5f, 1.0f, 1f));
                m.SetFloat("_Cull", 0f);
            });

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera: placed by the presenter every frame; this is its wide starting pose. ---
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = config.fieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.allowHDR = true;
            cameraObject.transform.position = config.widePosition;
            cameraObject.transform.rotation = Quaternion.LookRotation(config.wideTarget - config.widePosition, Vector3.up);
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            // SMAA: a thin lit gap crawls without it (MSAA is off in PC_RPAsset).
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;

            BuildStage(config, floorMaterial);
            BuildPostVolume();

            // --- Stations: two walls with their shapes and lamps. The presenter swaps them round by round. ---
            PerfectFitPresenter.Station a = BuildStation("Station A", config, wallMaterial, shapeMaterial, lampMaterial);
            PerfectFitPresenter.Station b = BuildStation("Station B", config, wallMaterial, shapeMaterial, lampMaterial);
            b.root.gameObject.SetActive(false);

            // --- Runner + simulation + presenter + audio ---
            var simulationObject = new GameObject("Simulation");
            SimulationRunner runner = simulationObject.AddComponent<SimulationRunner>();
            PerfectFitSimulation simulation = simulationObject.AddComponent<PerfectFitSimulation>();
            runner.config = config;
            runner.seed = 1;
            runner.autoStart = true;
            runner.randomizeSeedOnStart = true;

            var audioObject = new GameObject("Audio");
            AudioSource bed = audioObject.AddComponent<AudioSource>();
            AudioSource effects = audioObject.AddComponent<AudioSource>();
            bed.playOnAwake = false;
            effects.playOnAwake = false;
            PerfectFitAudio audio = audioObject.AddComponent<PerfectFitAudio>();
            audio.Bind(bed, effects);

            // --- HUD: question + gap in the top panel. The bottom 15% stays empty (platform overlays). ---
            PerfectFitRound first = config.rounds[0];
            CaptionHud hud = CaptionCanvasBuilder.Build($"WILL THE {first.shapeName} FIT?",
                                                        $"GAP {first.gapMillimetres:0.##} MM", string.Empty,
                                                        CaptionCanvasBuilder.Style.Dark);
            StyleCaptionCanvas(hud);

            PerfectFitPresenter presenter = simulationObject.AddComponent<PerfectFitPresenter>();
            presenter.Bind(simulation, hud, camera, audio, a, b);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            string roundList = string.Empty;
            for (int i = 0; i < config.RoundCount; i++)
            {
                PerfectFitRound r = config.rounds[i];
                roundList += $"    {i + 1}. {r.shapeName}: gap {r.gapMillimetres:0.##} mm, spin {r.spinDegreesPerSecond:0} deg/s\n";
            }

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  config {ConfigPath}\n" +
                $"  {config.RoundCount} rounds, series {config.SeriesSeconds:0.0}s at {1f / config.fixedTimestep:0} Hz\n" +
                roundList +
                "  Set the Game view to 1080x1920 (9:16) before judging framing. Seed is random per Play and logged at start.");
            EditorGUIUtility.PingObject(config);
        }

        [MenuItem("Simulation Lobby/3D Formats/Perfect Fit (fit)/Reset Config (storyboard defaults)", false, 341)]
        static void ResetConfig()
        {
            PerfectFitConfig config = EnsureConfig();
            config.rounds = PerfectFitConfig.DefaultRounds();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"[fit] Reset the rounds in {ConfigPath} to the storyboard defaults.", config);
        }

        static PerfectFitConfig EnsureConfig()
        {
            Directory.CreateDirectory("Assets/Settings/Configs");
            var existing = AssetDatabase.LoadAssetAtPath<PerfectFitConfig>(ConfigPath);
            if (existing != null)
            {
                return existing;
            }

            var config = ScriptableObject.CreateInstance<PerfectFitConfig>();
            config.intent = "fit-001 Perfect Fit. One question (will it fit?) asked seven times: a shape flies at a spinning " +
                            "wall with a hole cut exactly to its outline, locks onto the spin just before it arrives, and " +
                            "slides through. The gap shrinks from 30 mm to 0.5 mm and the spin climbs from 40 to 210 deg/s. " +
                            "The seed only picks each round's spin direction, start offset and tumble.";
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        // ---------------------------------------------------------------- stage

        static void BuildStage(PerfectFitConfig config, Material floorMaterial)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.14f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Background;
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 38f;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -config.wallRadius - 0.65f, 0f);
            floor.transform.localScale = new Vector3(5f, 1f, 5f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            var lights = new GameObject("Lights").transform;

            // Key: high, front-left, warm. The one light that casts, so the shape's shadow crosses the wall on approach.
            Light key = CreateLight(lights, "Key", LightType.Directional, new Color(1f, 0.95f, 0.88f), 1.5f);
            key.transform.rotation = Quaternion.Euler(38f, 28f, 0f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.85f;

            // Rim: from behind and above, cool, drawing a bright edge down the shape's sides.
            Light rim = CreateLight(lights, "Rim", LightType.Directional, new Color(0.7f, 0.9f, 1f), 0.9f);
            rim.transform.rotation = Quaternion.Euler(28f, -155f, 0f);

            // Fill: low, from the camera's right, so the shadow side is never black.
            Light fill = CreateLight(lights, "Fill", LightType.Directional, new Color(0.6f, 0.7f, 1f), 0.35f);
            fill.transform.rotation = Quaternion.Euler(10f, -30f, 0f);
        }

        static Light CreateLight(Transform parent, string name, LightType type, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Light light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        static void BuildPostVolume()
        {
            string path = $"{Folder}/PerfectFitPost.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);

                Tonemapping tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.Neutral);

                // Only the lamp (HDR 3.6) blooms: the glow squeezing through the gap is the legibility trick.
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(1.4f);
                bloom.intensity.Override(0.9f);
                bloom.scatter.Override(0.55f);

                Vignette vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.3f);
                vignette.smoothness.Override(0.5f);

                foreach (VolumeComponent component in profile.components)
                {
                    component.name = component.GetType().Name;
                    AssetDatabase.AddObjectToAsset(component, profile);
                }

                EditorUtility.SetDirty(profile);
            }

            var volumeObject = new GameObject("Post");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        // ---------------------------------------------------------------- station

        static PerfectFitPresenter.Station BuildStation(string name, PerfectFitConfig config, Material wallMaterial,
                                                         Material shapeMaterial, Material lampMaterial)
        {
            var root = new GameObject(name).transform;

            Transform wall = CreateMeshObject(root, "Wall", wallMaterial, out MeshFilter wallFilter);
            Transform shape = CreateMeshObject(root, "Shape", shapeMaterial, out MeshFilter shapeFilter);

            // Lamp: a bright quad hidden behind the wall (it is smaller than the wall), seen only through the hole.
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lamp.name = "Lamp";
            lamp.transform.SetParent(root, false);
            float lampZ = config.wallThickness * 0.5f + config.exitDistance + config.shapeDepth * 0.5f + 0.35f;
            lamp.transform.localPosition = new Vector3(0f, 0f, lampZ);
            lamp.transform.localScale = Vector3.one * Mathf.Min(2f, config.wallRadius * 1.6f);
            var lampRenderer = lamp.GetComponent<MeshRenderer>();
            lampRenderer.sharedMaterial = lampMaterial;
            lampRenderer.shadowCastingMode = ShadowCastingMode.Off;
            Object.DestroyImmediate(lamp.GetComponent<Collider>());

            // A real light too, so the lamp warms the shape's back and the floor under the wall.
            var lightObject = new GameObject("Lamp Light");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = new Vector3(0f, 0f, lampZ - 0.3f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.4f);
            light.intensity = 4f;
            light.range = 4.5f;
            light.shadows = LightShadows.None;

            return new PerfectFitPresenter.Station
            {
                root = root,
                wall = wall,
                shape = shape,
                wallFilter = wallFilter,
                shapeFilter = shapeFilter
            };
        }

        static Transform CreateMeshObject(Transform parent, string name, Material material, out MeshFilter filter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            filter = go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go.transform;
        }

        // ---------------------------------------------------------------- HUD

        /// <summary>Smaller than the studio default so a two-word shape name still fits a one- or two-line question.</summary>
        static void StyleCaptionCanvas(CaptionHud hud)
        {
            foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                switch (text.name)
                {
                    case "Caption":
                        text.fontSize = 90f;
                        text.characterSpacing = 2f;
                        text.textWrappingMode = TextWrappingModes.Normal;
                        break;
                    case "Subline":
                        text.fontSize = 58f;
                        text.fontStyle = FontStyles.Bold;
                        text.color = new Color(1f, 0.82f, 0.4f, 1f);
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- materials

        static Material EnsureMaterial(string name, string shaderName, System.Action<Material> setup)
        {
            string path = $"{Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            setup(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
