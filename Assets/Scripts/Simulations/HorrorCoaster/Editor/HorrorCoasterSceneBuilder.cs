using System.Collections.Generic;
using System.IO;
using SimulationLobby.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Builds <c>Coaster_Haunted_3D</c> (coast-001, 16:9): the rail and track meshes, night lighting, the twelve act sets, the train,
    /// the host and the viewing rig. Rail length is whatever the config says (~3.8 km): set builders scale by act span and group dressing into ~100 m chunks that a ChunkCuller switches by cart distance. Re-running replaces the scene and keeps an existing config asset (hand tuning survives);
    /// <b>Reset Config</b> rewrites it to the storyboard defaults. Set the Game view to 1920x1080 before Play.
    /// </summary>
    static class HorrorCoasterSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Coaster_Haunted_3D.unity";
        const string ConfigFolder = "Assets/Settings/Configs/HorrorCoaster";
        const string ConfigPath = ConfigFolder + "/HorrorCoasterConfig_001.asset";
        const string RootName = "Haunted Coaster";

        [MenuItem("Simulation Lobby/3D Formats/Coasters/Haunted Coaster (coast-001)", false, 210)]
        static void BuildHaunted()
        {
            Build();
        }

        [MenuItem("Simulation Lobby/3D Formats/Coasters/Haunted: Rebuild Track Meshes", false, 230)]
        static void RebuildTrack()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[coast] Exit play mode before rebuilding the track.");
                return;
            }

            var host = Object.FindAnyObjectByType<HorrorCoasterHost>();
            if (host == null || host.Config == null)
            {
                Debug.LogError("[coast] Open the Haunted Coaster scene first (no HorrorCoasterHost with a config found).");
                return;
            }

            HorrorCoasterConfig cfg = host.Config;
            CoasterRail rail = HorrorCoasterRail.FromConfig(cfg);
            Transform root = host.transform.parent != null ? host.transform.parent : host.transform;
            Transform old = root.Find("Track");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            GameObject track = BuildTrack(root, rail, cfg);
            track.transform.SetSiblingIndex(Mathf.Min(1, root.childCount - 1));
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log($"[coast] Track meshes rebuilt under {CoasterTrackBuilder.MeshFolder}. Save the scene to keep the new references.");
        }

        [MenuItem("Simulation Lobby/3D Formats/Coasters/Haunted: Reset Config (storyboard defaults)", false, 231)]
        static void ResetConfig()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[coast] Exit play mode before resetting the config.");
                return;
            }

            HorrorCoasterConfig cfg = EnsureConfig(true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[coast] Config reset to the storyboard defaults: {ConfigPath} ({cfg.events.Length} events, {cfg.acts.Length} acts)");
        }

        // ---------------------------------------------------------------- build

        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[coast] Exit play mode before building the scene.");
                return;
            }

            HorrorCoasterConfig cfg = EnsureConfig(true);
            if (!cfg.Validate(out string error))
            {
                Debug.LogWarning($"[coast] Config {ConfigPath} does not validate: {error}");
            }

            CoasterRail rail = HorrorCoasterRail.FromConfig(cfg);
            float groundY = CoasterKit.GroundY(rail);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera: HDR, URP post, SMAA. The rig added at the end drives pose, fov and effects.
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            rail.Sample(0f, out Vector3 startPos, out Vector3 startFwd, out Vector3 startUp);
            cameraObject.transform.SetPositionAndRotation(startPos + startUp * 1.1f, Quaternion.LookRotation(startFwd, startUp));
            Camera cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.025f, 0.075f, 1f);
            cam.allowHDR = true;
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 900f;
            UniversalAdditionalCameraData cameraData = cam.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;

            BuildLighting(cam);

            var root = new GameObject(RootName);

            // --- Host: owns the simulation and the runner. Presentation reads its state.
            var hostGo = new GameObject("Coaster Host");
            hostGo.transform.SetParent(root.transform, false);
            var host = hostGo.AddComponent<HorrorCoasterHost>();
            host.SetConfig(cfg);

            BuildGround(root.transform, rail, groundY);
            BuildTrack(root.transform, rail, cfg);

            // --- Acts, in ride order. Mine first, then the second half.
            var sets = new GameObject("Sets");
            sets.transform.SetParent(root.transform, false);
            StationSetBuilder.Build(sets.transform, rail, cfg);
            ChainLiftSetBuilder.Build(sets.transform, rail, cfg);
            StreetSetBuilder.Build(sets.transform, rail, cfg);
            GraveyardSetBuilder.Build(sets.transform, rail, cfg);
            DeadForestSetBuilder.Build(sets.transform, rail, cfg);
            TunnelSetBuilder.Build(sets.transform, rail, cfg);
            TripSetBuilder.Build(sets.transform, rail, cfg);
            MansionSetBuilder.Build(sets.transform, rail, cfg);
            AsylumSetBuilder.Build(sets.transform, rail, cfg);
            FinalSetBuilder.Build(sets.transform, rail, cfg);
            StationReturnSetBuilder.Build(sets.transform, rail, cfg);
            MicroScareBuilder.Build(sets.transform, rail, cfg);
            SignatureScareBuilder.Build(sets.transform, rail, cfg);
            AtmosphereBuilder.Build(sets.transform, rail, cfg);

            RiderRosterBuilder.Build(root.transform, cam, rail, cfg);
            BuildDust(root.transform);

            // --- Camera rig, post volume, audio, HUD and the presenter that binds the sets.
            CoasterRigBuilder.Build(root, cam, host);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(cfg);

            Debug.Log(
                $"Built {ScenePath}\n" +
                $"  config {ConfigPath}: {cfg.events.Length} events, {cfg.acts.Length} acts, rail {rail.Length:F0} m\n" +
                $"  materials Assets/Art/Generated/HorrorCoaster/Materials, meshes {CoasterTrackBuilder.MeshFolder}\n" +
                "  Set the Game view to 1920x1080 (16:9) before Play or recording. Seed is random per Play and logged at start: record it in the video plan.");
        }

        // ---------------------------------------------------------------- pieces

        static GameObject BuildTrack(Transform root, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            float groundY = CoasterKit.GroundY(rail);
            var ranges = new List<Vector2>();
            AddRange(ranges, cfg, rail, ActId.Station);
            AddRange(ranges, cfg, rail, ActId.TunnelOfFaces);
            AddRange(ranges, cfg, rail, ActId.Trip);
            AddRange(ranges, cfg, rail, ActId.Mansion);
            AddRange(ranges, cfg, rail, ActId.Asylum);
            AddRange(ranges, cfg, rail, ActId.StationReturn);
            return CoasterTrackBuilder.Build(root, rail, groundY, ranges);
        }

        static void AddRange(List<Vector2> ranges, HorrorCoasterConfig cfg, ICoasterRail rail, ActId act)
        {
            float a = cfg.ActStart(act);
            if (a >= rail.Length)
            {
                return;
            }

            ranges.Add(new Vector2(a, Mathf.Min(cfg.ActEnd(act), rail.Length)));
        }

        static void BuildLighting(Camera cam)
        {
            // Moonlit key with soft shadows, an orange fill from the other side, a cool rim from behind.
            CreateLight("Moon Key", new Color(0.62f, 0.72f, 1f), 0.6f, Quaternion.Euler(48f, -35f, 0f), LightShadows.Soft);
            CreateLight("Orange Fill", new Color(1f, 0.55f, 0.25f), 0.2f, Quaternion.Euler(18f, 150f, 0f), LightShadows.None);
            CreateLight("Cool Rim", new Color(0.45f, 0.4f, 0.9f), 0.28f, Quaternion.Euler(-28f, 20f, 0f), LightShadows.None);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.08f, 0.17f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.04f, 0.055f, 0.10f);
            RenderSettings.fogStartDistance = 3f;
            RenderSettings.fogEndDistance = 95f;
        }

        static void CreateLight(string name, Color colour, float intensity, Quaternion rotation, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.rotation = rotation;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = colour;
            light.intensity = intensity;
            light.shadows = shadows;
            light.shadowStrength = 0.85f;
        }

        static void BuildGround(Transform root, ICoasterRail rail, float groundY)
        {
            Vector3 min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, 0f, float.MinValue);
            for (float d = 0f; d <= rail.Length; d += 5f)
            {
                rail.Sample(d, out Vector3 p, out _, out _);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            Vector3 centre = (min + max) * 0.5f;
            float w = (max.x - min.x) + 500f;
            float h = (max.z - min.z) + 500f;
            GameObject ground = CoasterKit.Prim(PrimitiveType.Cube, root, "Ground", new Vector3(centre.x, groundY - 0.25f, centre.z), Quaternion.identity,
                                                new Vector3(w, 0.5f, h), CoasterLook.Ground, false);
            ground.GetComponent<MeshRenderer>().receiveShadows = true;
        }

        /// <summary>Drifting embers that travel with the cart: the ambient depth cue (the flat AmbientDriftField suits a 2D frame, not a ride).</summary>
        static void BuildDust(Transform root)
        {
            var go = new GameObject("Ambient Embers");
            go.transform.SetParent(root, false);
            go.AddComponent<CartFollow>().offset = new Vector3(0f, 2f, 22f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = 7717;
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 10f;
            main.prewarm = true;
            main.startLifetime = 7f;
            main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = new Color(1f, 0.6f, 0.25f, 0.55f);
            main.maxParticles = 420;
            main.gravityModifier = -0.01f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 70f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 9f, 60f);
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.2f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = CoasterLook.Dust;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
        }

        // ---------------------------------------------------------------- config

        static HorrorCoasterConfig EnsureConfig(bool reset)
        {
            Directory.CreateDirectory(ConfigFolder);
            var cfg = AssetDatabase.LoadAssetAtPath<HorrorCoasterConfig>(ConfigPath);
            bool created = cfg == null;
            if (created)
            {
                cfg = ScriptableObject.CreateInstance<HorrorCoasterConfig>();
            }

            if (created || reset)
            {
                HorrorCoasterDefaults.Apply(cfg);
            }

            if (created)
            {
                AssetDatabase.CreateAsset(cfg, ConfigPath);
            }
            else
            {
                EditorUtility.SetDirty(cfg);
            }

            return cfg;
        }
    }
}
