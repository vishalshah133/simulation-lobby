using System.Collections.Generic;
using System.IO;
using SimulationLobby.Presentation.EditorTools;
using SimulationLobby.Shared.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// The light-grey studio the marble machine sits in: a seamless curved cyclorama (no horizon line, no collider),
    /// a soft key with a cool fill and a rim, a reflection probe over the machine, and a restrained post volume.
    /// Scene-only: nothing here touches project-wide settings or the URP asset.
    /// </summary>
    /// <remarks>
    /// Exposure budget (URP Lit, linear: colour = albedo x (ambient + sum of intensity x NdotL)). Ambient values below are
    /// sRGB colours, so 0.40 / 0.36 / 0.28 are 0.133 / 0.107 / 0.063 linear.
    /// Floor (albedo #C4C5C7 = 0.55): key 0.85 x sin52 (0.79) = 0.67, fill 0.25 x sin28 (0.47) = 0.12, rim 0.35 x sin22
    /// (0.37) = 0.13, sky ambient 0.13, total 1.05, so 0.58 linear = sRGB ~#C8 (target #C0-#CD). In key shadow
    /// (strength 0.7 keeps 30 percent of the key) 0.55 x 0.58 = 0.32 linear = ~#99, a soft visible shadow.
    /// Back wall (albedo #DCDCDE = 0.72): key 0.85 x 0.54 = 0.46, fill 0.25 x 0.66 = 0.16, rim 0 (back-lit side),
    /// equator ambient 0.11, total 0.73, so 0.52 linear = ~#BF. The old rig summed 2.25 of direct light plus 0.5 ambient
    /// on near-white albedo (about 1.9 linear on the floor), which clipped. Neutral tonemapping only compresses above
    /// ~0.76 linear, so the cyclorama passes through unchanged and only highlights roll off.
    /// </remarks>
    public static class MachineStudioEnvironment
    {
        /// <summary>Layer the reflection-only softboxes live on. The camera culls it; the probe sees it.</summary>
        public const int ReflectionOnlyLayer = 30;

        const string PostPath = MachineMaterials.Root + "/MachinePost.asset";
        const string CycloramaMaterialPath = MachineMaterials.MaterialFolder + "/Machine_Cyclorama.mat";

        static readonly Color Top = new Color32(0xDC, 0xDC, 0xDE, 0xFF);
        static readonly Color Floor = new Color32(0xC4, 0xC5, 0xC7, 0xFF);

        public static void Build(Camera cam, Bounds machineBounds)
        {
            ConfigureCamera(cam);
            ConfigureEnvironment(cam);
            BuildCyclorama(machineBounds);
            BuildLights(machineBounds);
            BuildReflections(machineBounds);
            BuildPostVolume();
        }

        // ------------------------------------------------------------------ camera + ambient

        static void ConfigureCamera(Camera cam)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            // Mid-way down the cyclorama gradient, so anything past it (and the fog) blends in rather than going black.
            cam.backgroundColor = new Color32(0xC4, 0xC5, 0xC8, 0xFF);
            cam.allowHDR = true;
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 250f;
            cam.cullingMask &= ~(1 << ReflectionOnlyLayer);
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        static void ConfigureEnvironment(Camera cam)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.40f, 0.41f, 0.43f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.36f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.28f, 0.29f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = cam.backgroundColor;
            RenderSettings.fogStartDistance = 100f;
            RenderSettings.fogEndDistance = 245f;
        }

        // ------------------------------------------------------------------ cyclorama

        static void BuildCyclorama(Bounds b)
        {
            const float radius = 28f;
            const float wallHeight = 80f;
            float zStart = b.center.z - 170f;
            float zBack = b.max.z + 45f;
            float halfWidth = 200f;
            float cx = b.center.x;

            // Profile in (z, y): flat floor, a quarter-circle sweep, then the vertical back wall. No horizon line.
            var profile = new List<Vector2> { new Vector2(zStart, 0f), new Vector2(zBack - radius, 0f) };
            const int arcSteps = 24;
            for (int i = 1; i <= arcSteps; i++)
            {
                float a = i / (float)arcSteps * Mathf.PI * 0.5f;
                profile.Add(new Vector2(zBack - radius + radius * Mathf.Sin(a), radius * (1f - Mathf.Cos(a))));
            }

            profile.Add(new Vector2(zBack, wallHeight));

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            const float gradientHeight = 60f;
            for (int i = 0; i < profile.Count; i++)
            {
                float v = Mathf.Clamp01(profile[i].y / gradientHeight);
                verts.Add(new Vector3(cx - halfWidth, profile[i].y, profile[i].x));
                verts.Add(new Vector3(cx + halfWidth, profile[i].y, profile[i].x));
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));
            }

            for (int i = 0; i < profile.Count - 1; i++)
            {
                int l0 = i * 2, r0 = l0 + 1, l1 = l0 + 2, r1 = l0 + 3;
                tris.Add(l0); tris.Add(l1); tris.Add(r0);
                tris.Add(r0); tris.Add(l1); tris.Add(r1);
            }

            var mesh = new Mesh { name = "MachineCyclorama" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Mesh saved = TrackMeshAssets.SaveMeshFresh(mesh, "MachineCyclorama", MachineGeometry.MeshFolder);

            Texture2D gradient = MachineMaterials.EnsureTexture("CycloramaGradient", 4, false, false,
                (u, v) => Color.Lerp(Floor, Top, v), null, 256, false);
            Material material = EnsureCycloramaMaterial(gradient);

            var go = new GameObject("Cyclorama");
            go.AddComponent<MeshFilter>().sharedMesh = saved;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        static Material EnsureCycloramaMaterial(Texture2D gradient)
        {
            Directory.CreateDirectory(MachineMaterials.MaterialFolder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(CycloramaMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Machine_Cyclorama" };
                AssetDatabase.CreateAsset(material, CycloramaMaterialPath);
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", gradient);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.08f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ lights

        static void BuildLights(Bounds b)
        {
            var root = new GameObject("Studio Lights").transform;
            root.position = b.center;

            // Key: from front-left and high (the camera looks along +Z); the only shadow caster. Soft shadows.
            Light key = NewDirectional(root, "Key", new Color(1f, 0.97f, 0.92f), 0.85f, Quaternion.Euler(52f, 28f, 0f));
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.7f;
            key.shadowResolution = LightShadowResolution.VeryHigh;
            key.shadowBias = 0.8f;
            key.shadowNormalBias = 0.6f;

            // Fill: cool, from the right, no shadows.
            NewDirectional(root, "Fill", new Color(0.80f, 0.88f, 1f), 0.25f, Quaternion.Euler(28f, -42f, 0f));

            // Rim: from behind the machine, to give chrome and glass a bright edge.
            NewDirectional(root, "Rim", new Color(1f, 0.98f, 0.94f), 0.35f, Quaternion.Euler(22f, 198f, 0f));
        }

        static Light NewDirectional(Transform parent, string name, Color colour, float intensity, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = rotation;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = colour;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        // ------------------------------------------------------------------ reflections

        static void BuildReflections(Bounds b)
        {
            var root = new GameObject("Reflections").transform;

            var probeObject = new GameObject("Machine Reflection Probe");
            probeObject.transform.SetParent(root, false);
            probeObject.transform.position = b.center;
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 512;
            probe.hdr = true;
            probe.boxProjection = true;
            probe.size = b.size + new Vector3(40f, 20f, 40f);
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.55f, 0.56f, 0.58f, 1f);
            probe.nearClipPlane = 0.1f;
            probe.farClipPlane = 250f;
            probe.importance = 10;

            // Big soft cards the camera never draws but chrome and glass reflect: the long highlights that sell the metal.
            Material card = EnsureSoftboxMaterial();
            float ring = Mathf.Max(b.extents.x, b.extents.z) + 35f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"Softbox {i + 1}";
                quad.transform.SetParent(root, false);
                quad.transform.position = b.center + direction * ring + Vector3.up * (b.extents.y + 10f);
                quad.transform.rotation = Quaternion.LookRotation(quad.transform.position - b.center);
                quad.transform.localScale = new Vector3(40f, 14f, 1f);
                quad.layer = ReflectionOnlyLayer;
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = card;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        // Own material: the shared StudioSoftbox is re-tuned by other formats. Moderate HDR so chrome keeps long highlights.
        static Material EnsureSoftboxMaterial()
        {
            const string path = MachineMaterials.MaterialFolder + "/Machine_Softbox.mat";
            Directory.CreateDirectory(MachineMaterials.MaterialFolder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Machine_Softbox" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", new Color(1.6f, 1.6f, 1.6f, 1f));
            material.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ post

        static void BuildPostVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostPath);
            if (profile == null)
            {
                Directory.CreateDirectory(MachineMaterials.Root);
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostPath);
            }

            // Get-or-add every override on every build, so tuning changes reach an existing profile asset.
            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1.8f);
            bloom.intensity.Override(0.25f);
            bloom.scatter.Override(0.6f);
            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.5f);
            ColorAdjustments adjust = GetOrAdd<ColorAdjustments>(profile);
            adjust.postExposure.Override(-0.1f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volumeObject = new GameObject("Studio Post");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            EditorUtility.SetDirty(component);
            return component;
        }
    }
}
