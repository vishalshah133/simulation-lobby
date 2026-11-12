using System.IO;
using SimulationLobby.Shared.Coaster;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SimulationLobby.Simulations.TrippyCoaster.EditorTools
{
    /// <summary>
    /// Builds <c>Coaster_Trippy_3D</c> (trip-001): the seven worlds along the rail, the neon track, a backdrop sky, the single cart
    /// nose, orbiting palette lights, the shader clock, post FX, procedural audio and the HUD. Re-running replaces the scene and
    /// keeps an existing config (hand tuning survives); <b>Reset Config</b> rewrites it to the storyboard defaults.
    /// Set the Game view to 1080x1920 (Shorts) or 1920x1080 (long form) before Play.
    /// </summary>
    static class TrippyCoasterSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Coaster_Trippy_3D.unity";
        const string ConfigFolder = "Assets/Settings/Configs/TrippyCoaster";
        const string ConfigPath = ConfigFolder + "/TrippyCoasterConfig_001.asset";

        [MenuItem("Simulation Lobby/3D Formats/Coasters/Trippy Coaster (trip-001)", false, 211)]
        static void BuildTrip()
        {
            Build();
        }

        [MenuItem("Simulation Lobby/3D Formats/Coasters/Trippy: Reset Config (storyboard defaults)", false, 235)]
        static void ResetConfig()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[trip] Exit play mode before resetting the config.");
                return;
            }
            TrippyCoasterConfig cfg = EnsureConfig(true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[trip] Config reset to the storyboard defaults: {ConfigPath} ({cfg.worlds.Length} worlds, {cfg.railPoints.Length} rail points)");
        }

        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[trip] Exit play mode before building the scene.");
                return;
            }

            TrippyCoasterConfig cfg = EnsureConfig(false);
            if (!cfg.Validate(out string error))
            {
                Debug.LogError($"[trip] Config {ConfigPath} does not validate: {error}");
                return;
            }

            CoasterRail rail = cfg.BuildRail();
            TripClearance.Warn(rail, cfg);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera: HDR, URP post, SMAA. TripCameraRig drives pose and fov.
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            rail.Sample(0f, out Vector3 startPos, out Vector3 startFwd, out Vector3 startUp);
            cameraObject.transform.SetPositionAndRotation(startPos + startUp * 1.15f, Quaternion.LookRotation(startFwd, startUp));
            Camera cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.0f, 0.03f, 1f);
            cam.allowHDR = true;
            cam.fieldOfView = cfg.baseFov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1400f;
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData cameraData = cam.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;

            // --- Environment: near-black void, dim key so the cart body reads, fog as the depth cue.
            var key = new GameObject("Dim Key");
            key.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            Light keyLight = key.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.6f, 0.55f, 1f);
            keyLight.intensity = 0.35f;
            keyLight.shadows = LightShadows.None;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.03f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.02f, 0.0f, 0.05f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 800f;

            var root = new GameObject("Trippy Coaster");

            // --- Host: owns the simulation and the runner. Everything else reads its state.
            var hostGo = new GameObject("Trip Host");
            hostGo.transform.SetParent(root.transform, false);
            var host = hostGo.AddComponent<TrippyCoasterHost>();
            host.SetConfig(cfg);

            // --- World geometry along the rail.
            Material[] backdrops = TripWorldBuilder.Build(root.transform, rail, cfg);
            BuildBackdrop(root.transform, host, backdrops);

            // --- Cart nose: what the viewer sits in. Parented to the camera, so it is always exactly where the sim says the cart is.
            BuildCartNose(cameraObject.transform, cfg);

            // --- Runtime rigs.
            var rigs = new GameObject("Trip Rigs");
            rigs.transform.SetParent(root.transform, false);
            rigs.AddComponent<TripShaderGlobals>().host = host;
            cameraObject.AddComponent<TripCameraRig>().host = host;
            cameraObject.AddComponent<TripPostFx>().host = host;
            cameraObject.AddComponent<TripAudio>().host = host;

            var lightRig = rigs.AddComponent<TripLightRig>();
            lightRig.host = host;
            lightRig.lights = new Light[3];
            for (int i = 0; i < 3; i++)
            {
                var lg = new GameObject("Orbit Light " + i);
                lg.transform.SetParent(rigs.transform, false);
                Light l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 24f;
                l.intensity = 6f;
                l.shadows = LightShadows.None;
                lightRig.lights[i] = l;
            }

            TripHud.Build(root.transform, host);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(cfg);

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  config {ConfigPath}: {cfg.worlds.Length} worlds, rail {rail.Length:F0} m\n" +
                $"  materials {TripAssets.MaterialFolder}, meshes {TripAssets.MeshFolder}\n" +
                "  Set the Game view to 1080x1920 (Shorts) or 1920x1080 (long form) before Play. Seed is random per Play and logged at start: record it in the video plan.");
        }

        static void BuildBackdrop(Transform root, TrippyCoasterHost host, Material[] mats)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Backdrop Sky";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localScale = Vector3.one * 2400f;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mats[0];
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var bd = go.AddComponent<TripBackdrop>();
            bd.host = host;
            bd.perWorld = mats;
            bd.target = mr;
        }

        static void BuildCartNose(Transform camera, TrippyCoasterConfig cfg)
        {
            var nose = new GameObject("Cart Nose");
            nose.transform.SetParent(camera, false);
            Material body = TripAssets.CartBody();
            Material rim = TripAssets.Trip("Trip_CartRim", cfg.worlds[0], TripPattern.Glow, 1.6f, false, 0f);

            // Low, narrow tub that sits BEHIND and below the eye: the viewer is at the front edge, so only a thin glowing lip shows at the bottom of the frame.
            Part(nose.transform, PrimitiveType.Cube, "Tub", new Vector3(0f, -0.78f, -0.3f), new Vector3(1.15f, 0.4f, 1.8f), body);
            Part(nose.transform, PrimitiveType.Cube, "Rim L", new Vector3(-0.58f, -0.56f, -0.3f), new Vector3(0.04f, 0.04f, 1.8f), rim);
            Part(nose.transform, PrimitiveType.Cube, "Rim R", new Vector3(0.58f, -0.56f, -0.3f), new Vector3(0.04f, 0.04f, 1.8f), rim);
            Part(nose.transform, PrimitiveType.Cube, "Rim Front", new Vector3(0f, -0.56f, 0.6f), new Vector3(1.15f, 0.04f, 0.04f), rim);
        }

        static void Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        static TrippyCoasterConfig EnsureConfig(bool reset)
        {
            Directory.CreateDirectory(ConfigFolder);
            var cfg = AssetDatabase.LoadAssetAtPath<TrippyCoasterConfig>(ConfigPath);
            bool created = cfg == null;
            if (created) cfg = ScriptableObject.CreateInstance<TrippyCoasterConfig>();
            // a config written by older defaults (a different course or speeds) is re-filled, so a rebuild never ships a stale rail
            bool stale = !created && cfg.defaultsVersion != TrippyCoasterDefaults.Version;
            if (stale) Debug.Log($"[trip] config defaults are version {cfg.defaultsVersion}, code is {TrippyCoasterDefaults.Version}: re-applying the storyboard defaults.");
            if (created || reset || stale) TrippyCoasterDefaults.Apply(cfg);
            if (created) AssetDatabase.CreateAsset(cfg, ConfigPath);
            else EditorUtility.SetDirty(cfg);
            return cfg;
        }
    }
}
