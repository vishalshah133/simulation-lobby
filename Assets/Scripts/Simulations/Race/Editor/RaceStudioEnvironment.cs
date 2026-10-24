using SimulationLobby.Presentation.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.Race.EditorTools
{
    /// <summary>
    /// The scene-level half of the race look: studio post and ambient, the pale polished floor, the key + fill + rim
    /// lights, and the box-projected reflection probe with its softbox cards. Built once per scene, whatever the
    /// number of <see cref="RaceRig"/>s in it.
    /// </summary>
    public static class RaceStudioEnvironment
    {
        /// <summary>
        /// Builds the whole environment around <paramref name="bounds"/> (the area the camera(s) will see; for several
        /// rigs, encapsulate all of them). <paramref name="camera"/> is the camera the studio build configures
        /// (post volume, ambient, fog distances); rig cameras are configured by the rig builder.
        /// </summary>
        /// <param name="floorCenter">Centre of the floor disc (y is ignored; the floor sits just under y = 0).</param>
        /// <param name="floorScale">Floor disc scale in x and z (500 = a 1 km disc).</param>
        public static void Build(Camera camera, Bounds bounds, Vector3 floorCenter, float floorScale = 500f,
            float fogStart = 45f, float fogEnd = 190f)
        {
            StudioSetBuilder.Build(camera, new StudioSetBuilder.Options
            {
                hue = RaceRigBuilder.Hue,
                glassName = RaceRigBuilder.GlassName,
                subjectCenter = bounds.center,
                subjectHeight = bounds.size.y
            });
            DestroyByName("Cyclorama");
            DestroyByName("Studio Lights");
            DestroyByName("Softbox Cards (reflection only)");
            DestroyByName("Studio Reflection Probe");

            camera.cullingMask &= ~(1 << RaceRigBuilder.ReflectionOnlyLayer);
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            BuildFloor(floorCenter, floorScale);
            BuildLights();
            BuildReflections(bounds);
        }

        public static void BuildFloor(Vector3 center, float scale = 500f)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Studio Floor";
            floor.transform.position = new Vector3(center.x, -0.1f, center.z);
            floor.transform.localScale = new Vector3(scale, 0.1f, scale);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.AddComponent<BoxCollider>();
            floor.GetComponent<MeshRenderer>().sharedMaterial = RaceAssets.EnsureMaterial("RaceFloor", "Universal Render Pipeline/Lit", m =>
            {
                // Pale champagne, polished: the dark course reads against it, and its sheen picks up the probe.
                m.SetColor("_BaseColor", new Color(0.80f, 0.76f, 0.69f, 1f));
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0.8f);
            });
        }

        public static void BuildLights()
        {
            var root = new GameObject("Race Lights").transform;

            // Key: warm, high and to the front-left; the only light that casts. Shadow distance is 50m
            // from the camera in PC_RPAsset, which is plenty for a chase shot.
            var key = NewDirectional(root, "Key", new Color(1f, 0.95f, 0.88f), 1.05f, Quaternion.Euler(48f, 148f, 0f));
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.55f;
            key.shadowResolution = LightShadowResolution.VeryHigh;

            // Fill and rim: cooler, no shadows, from the opposite side and behind, to lift the dark lacquer's edges.
            NewDirectional(root, "Fill", new Color(0.85f, 0.9f, 1f), 0.35f, Quaternion.Euler(30f, -35f, 0f));
            NewDirectional(root, "Rim", new Color(1f, 0.97f, 0.92f), 0.4f, Quaternion.Euler(18f, 330f, 0f));
        }

        static Light NewDirectional(Transform parent, string name, Color color, float intensity, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = rotation;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>
        /// One box-projected probe over the whole course, plus large emissive cards around it on a layer the
        /// camera does not draw. The cards are what give the glass and lacquer their long soft highlights.
        /// </summary>
        public static void BuildReflections(Bounds bounds)
        {
            var root = new GameObject("Reflections").transform;

            var probeObject = new GameObject("Course Reflection Probe");
            probeObject.transform.SetParent(root, false);
            probeObject.transform.position = bounds.center + Vector3.up * 2f;
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 512;
            probe.hdr = true;
            probe.boxProjection = true;
            probe.size = bounds.size + new Vector3(60f, 40f, 60f);
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.80f, 0.74f, 0.64f, 1f);
            probe.nearClipPlane = 0.1f;
            probe.farClipPlane = 250f;
            probe.importance = 10;

            Material card = StudioSetBuilder.EnsureCardMaterial();
            card.SetColor("_BaseColor", new Color(1.8f, 1.8f, 1.8f, 1f));
            EditorUtility.SetDirty(card);

            float ring = Mathf.Max(bounds.extents.x, bounds.extents.z) + 30f;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"Softbox {i + 1}";
                quad.transform.SetParent(root, false);
                quad.transform.position = bounds.center + direction * ring + Vector3.up * (bounds.extents.y * 0.8f + 6f);
                quad.transform.rotation = Quaternion.LookRotation(quad.transform.position - bounds.center);
                quad.transform.localScale = new Vector3(40f, 14f, 1f);
                quad.layer = RaceRigBuilder.ReflectionOnlyLayer;
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = card;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        static void DestroyByName(string name)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
            {
                Object.DestroyImmediate(found);
            }
        }
    }
}
