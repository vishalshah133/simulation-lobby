using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// mach-002 outdoor setting in place of the grey studio: a cloud-and-gradient skybox, a glossy teal water plane at the old floor
    /// height (no collider, so physics is untouched), a warm sun with a cool fill and a rim, sky-tinted ambient, hazy blue-green
    /// linear fog as the depth cue, a realtime reflection probe so chrome and water mirror the sky, and a restrained post volume.
    /// Scene-only; every saved asset lives under <see cref="MachineNatureMaterials.Root"/>.
    /// </summary>
    public static class MachineNatureEnvironment
    {
        const string PostPath = MachineNatureMaterials.Root + "/NaturePost.asset";
        const string SkyboxPath = MachineNatureMaterials.MaterialFolder + "/Nature_Sky.mat";

        // Horizon haze doubles as the fog colour so the water fades into the sky with no seam.
        static readonly Color Haze = new Color(0.66f, 0.80f, 0.82f);
        static readonly Color Zenith = new Color(0.24f, 0.47f, 0.80f);
        static readonly Color Mid = new Color(0.45f, 0.68f, 0.90f);

        public static void Build(Camera cam, Bounds machineBounds)
        {
            ConfigureCamera(cam);
            Material sky = BuildSkybox();
            ConfigureEnvironment(sky);
            BuildWater(machineBounds);
            BuildLights(machineBounds);
            BuildReflections(machineBounds);
            BuildPostVolume();
        }

        static void ConfigureCamera(Camera cam)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Haze;
            cam.allowHDR = true;
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 250f;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        static void ConfigureEnvironment(Material sky)
        {
            RenderSettings.skybox = sky;
            // Trilight, not Skybox ambient: it needs no baked environment update and stays identical in every render.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.64f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.60f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.36f, 0.36f); // bounce off the water
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Haze;
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 245f;
        }

        // ------------------------------------------------------------------ sky

        static Material BuildSkybox()
        {
            // 360 degree lat-long panorama: vertical gradient, hazy lower hemisphere (it is what the far water fades into), soft clouds.
            Texture2D panorama = MachineNatureMaterials.EnsureTexture("Nature_SkyPanorama", false, SkyPixel, 2048, 1024, false, TextureWrapMode.Repeat);
            Shader shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                Debug.LogWarning("[mach] Skybox/Panoramic not found; falling back to Skybox/Procedural.");
                return MachineNatureMaterials.EnsureMaterial("Nature_Sky", "Skybox/Procedural", m =>
                {
                    m.SetColor("_SkyTint", new Color(0.5f, 0.6f, 0.75f));
                    m.SetColor("_GroundColor", Haze);
                    m.SetFloat("_AtmosphereThickness", 0.8f);
                    m.SetFloat("_Exposure", 1.1f);
                });
            }

            return MachineNatureMaterials.EnsureMaterial("Nature_Sky", "Skybox/Panoramic", m =>
            {
                m.SetTexture("_MainTex", panorama);
                m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f)); // 0.5 grey = neutral for this shader
                m.SetFloat("_Exposure", 1f);
                m.SetFloat("_Rotation", 0f);
                m.SetFloat("_Mapping", 1f);   // latitude-longitude
                m.SetFloat("_ImageType", 0f); // 360 degrees
                m.SetFloat("_MirrorOnBack", 0f);
            });
        }

        static Color SkyPixel(float u, float v)
        {
            float elevation = Mathf.Sin((v - 0.5f) * Mathf.PI); // -1 down .. +1 up
            Color c;
            if (elevation >= 0f)
            {
                float t = Mathf.Pow(elevation, 0.55f);
                c = t < 0.4f ? Color.Lerp(Haze, Mid, t / 0.4f) : Color.Lerp(Mid, Zenith, (t - 0.4f) / 0.6f);

                // A few soft cumulus: low-frequency noise, thresholded, fading in above the horizon haze and thinning to the zenith.
                float n = MachineNatureMaterials.Fbm(u, Mathf.Clamp01(elevation) * 0.9f + 0.05f, 7, 3, 5, 407);
                float cover = Mathf.SmoothStep(0.54f, 0.72f, n);
                float band = Mathf.SmoothStep(0.03f, 0.22f, elevation) * (1f - 0.6f * Mathf.SmoothStep(0.6f, 1f, elevation));
                float shade = 0.82f + 0.18f * MachineNatureMaterials.Noise(u, elevation, 28, 8, 91);
                Color cloud = Color.Lerp(new Color(0.78f, 0.85f, 0.93f), Color.white, shade);
                c = Color.Lerp(c, cloud, cover * band * 0.92f);
            }
            else
            {
                c = Color.Lerp(Haze, new Color(0.50f, 0.68f, 0.70f), Mathf.Clamp01(-elevation * 3f));
            }

            c.a = 1f;
            return c;
        }

        // ------------------------------------------------------------------ water

        static void BuildWater(Bounds b)
        {
            Texture2D ripples = MachineNatureMaterials.EnsureTexture("Nature_WaterRipple", true, RippleNormal);
            Material water = MachineNatureMaterials.EnsureLit("Nature_Water", m =>
            {
                MachineNatureMaterials.Opaque(m, new Color(0.07f, 0.31f, 0.38f), null, 0f, 0.93f);
                m.SetTextureScale("_BaseMap", new Vector2(180f, 180f)); // matches NatureWaterScroller.tiling
                m.SetTexture("_BumpMap", ripples);
                m.SetFloat("_BumpScale", 0.45f);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_SpecularHighlights", 1f);
                m.SetFloat("_EnvironmentReflections", 1f);
            });

            var go = GameObject.CreatePrimitive(PrimitiveType.Plane); // 10 x 10 m at scale 1
            go.name = "Water";
            Object.DestroyImmediate(go.GetComponent<Collider>()); // presentation only: physics identical, killPlaneY still recycles
            go.transform.position = new Vector3(b.center.x, 0f, b.center.z);
            go.transform.localScale = new Vector3(90f, 1f, 90f); // 900 m: past the fog end in every direction
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = water;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            go.AddComponent<NatureWaterScroller>();
        }

        // Tileable ripple heightfield turned into a tangent-space normal map by finite differences.
        static Color RippleNormal(float u, float v)
        {
            const float e = 1f / 512f;
            float hx0 = RippleHeight(u - e, v), hx1 = RippleHeight(u + e, v);
            float hy0 = RippleHeight(u, v - e), hy1 = RippleHeight(u, v + e);
            var n = new Vector3((hx0 - hx1) * 14f, (hy0 - hy1) * 14f, 1f).normalized;
            return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }

        static float RippleHeight(float u, float v)
        {
            u = Mathf.Repeat(u, 1f);
            v = Mathf.Repeat(v, 1f);
            return MachineNatureMaterials.Fbm(u, v, 8, 8, 4, 311) * 0.7f + MachineNatureMaterials.Noise(u, v, 32, 32, 313) * 0.3f;
        }

        // ------------------------------------------------------------------ lights

        static void BuildLights(Bounds b)
        {
            var root = new GameObject("Nature Lights").transform;
            root.position = b.center;

            // Sun (key): warm, from front-left and high; the only shadow caster. Soft shadows.
            Light sun = NewDirectional(root, "Sun", new Color(1f, 0.95f, 0.84f), 1.25f, Quaternion.Euler(46f, 32f, 0f));
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            sun.shadowResolution = LightShadowResolution.VeryHigh;
            sun.shadowBias = 0.8f;
            sun.shadowNormalBias = 0.6f;
            RenderSettings.sun = sun;

            // Fill: sky blue, from the right, no shadows.
            NewDirectional(root, "Sky Fill", new Color(0.66f, 0.80f, 1f), 0.45f, Quaternion.Euler(28f, -42f, 0f));

            // Rim: from behind the machine, to give chrome, glass and the balls a bright edge.
            NewDirectional(root, "Rim", new Color(1f, 0.96f, 0.88f), 0.28f, Quaternion.Euler(22f, 198f, 0f));
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
            var probeObject = new GameObject("Machine Reflection Probe");
            probeObject.transform.position = b.center;
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 512;
            probe.hdr = true;
            probe.boxProjection = false; // the sky is infinitely far; box projection would smear it
            probe.size = b.size + new Vector3(40f, 20f, 40f);
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.nearClipPlane = 0.1f;
            probe.farClipPlane = 250f;
            probe.importance = 10;
        }

        // ------------------------------------------------------------------ post

        static void BuildPostVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostPath);
            if (profile == null)
            {
                Directory.CreateDirectory(MachineNatureMaterials.Root);
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostPath);
            }

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(2.4f);
            bloom.intensity.Override(0.08f);
            bloom.scatter.Override(0.6f);
            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.5f);
            ColorAdjustments adjust = GetOrAdd<ColorAdjustments>(profile);
            adjust.postExposure.Override(0.1f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volumeObject = new GameObject("Nature Post");
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
