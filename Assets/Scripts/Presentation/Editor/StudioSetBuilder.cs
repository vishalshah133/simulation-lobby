using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SimulationLobby.Presentation.EditorTools
{
    /// <summary>
    /// The house look for 3D videos (<c>plans/blocks/studio-look-3d.md</c>): a dark product-photo
    /// studio with elegant materials — tinted glass, chrome, piano-black lacquer, brushed brass — and
    /// the lighting that makes them read: a key, two rim lights, a halo on the backdrop, and softbox
    /// cards the camera never sees but every reflective surface does.
    /// </summary>
    /// <remarks>
    /// Scene-building code, not a runtime component: a format's scene builder calls
    /// <see cref="Build"/> once, after placing its camera. Materials are created once and never
    /// overwritten on rebuild, so hand-tuning in the Inspector survives; delete an asset to regenerate it.
    /// <para>
    /// The softbox cards sit <i>behind</i> the camera, which is how they stay invisible to it without
    /// layers: chrome and glass facing the camera reflect exactly what is behind the camera.
    /// </para>
    /// </remarks>
    public static class StudioSetBuilder
    {
        public const string Folder = "Assets/Settings/Studio";

        public struct Materials
        {
            public Material glass;

            /// <summary>
            /// Dark tinted glass for set pieces the subject goes <i>inside</i> (a plinth with a hole).
            /// Plain alpha-blended URP Lit drawn after the hero glass, not the refraction shader:
            /// refraction samples the opaque image, which never contains transparent objects, so two
            /// refraction-glass objects can't see each other.
            /// </summary>
            public Material smokedGlass;
            public Material chrome;
            public Material lacquer;
            public Material brass;
            public Material cyclorama;
        }

        public struct Options
        {
            /// <summary>The video's one hue. Tints the glass and warms the backdrop halo.</summary>
            public Color hue;

            /// <summary>Asset name for this video's glass material, e.g. "SoftSweepGlass".</summary>
            public string glassName;

            /// <summary>Centre of the action — lights and the reflection probe aim here.</summary>
            public Vector3 subjectCenter;

            /// <summary>Rough height of everything that moves, for scaling the light rig.</summary>
            public float subjectHeight;
        }

        public static Materials Build(Camera camera, Options options)
        {
            Directory.CreateDirectory(Folder);
            Materials materials = EnsureMaterials(options);

            // The backdrop gradient is part of the light rig, not a hand-tuned material: re-apply it every
            // build so a change to the house backdrop reaches scenes whose material predates it.
            materials.cyclorama.SetTexture("_BaseMap", EnsureGradientTexture());
            EditorUtility.SetDirty(materials.cyclorama);

            BuildCyclorama(materials.cyclorama, camera.transform.position);
            BuildLights(options);
            BuildSoftboxCards(camera, options);
            BuildReflectionProbe(options);
            BuildPostVolume();
            ConfigureCamera(camera);
            ConfigureEnvironment(camera, options);
            return materials;
        }

        // ------------------------------------------------------------------ materials

        public static Materials EnsureMaterials(Options options)
        {
            return new Materials
            {
                glass = EnsureGlass(options.glassName, options.hue),
                smokedGlass = EnsureMaterial("StudioSmokedGlass", "Universal Render Pipeline/Lit", m =>
                {
                    // Smoky, not clear: dark enough to read as a solid block, clear enough to see the
                    // amber subject squeezing inside it. Mirror-smooth so the softbox cards show on it.
                    m.SetColor("_BaseColor", new Color(0.07f, 0.07f, 0.08f, 0.45f));
                    m.SetFloat("_Metallic", 0f);
                    m.SetFloat("_Smoothness", 0.96f);
                    MakeTransparent(m, 10);
                }),
                chrome = EnsureMaterial("StudioChrome", "Universal Render Pipeline/Lit", m =>
                {
                    m.SetColor("_BaseColor", new Color(0.93f, 0.93f, 0.95f, 1f));
                    m.SetFloat("_Metallic", 1f);
                    m.SetFloat("_Smoothness", 0.94f);
                }),
                lacquer = EnsureMaterial("StudioLacquer", "Universal Render Pipeline/Complex Lit", m =>
                {
                    // Piano black: a near-black base under a mirror-smooth clear coat. The coat is what
                    // reads as "expensive" — sharp reflections floating over a matte-deep colour.
                    m.SetColor("_BaseColor", new Color(0.014f, 0.014f, 0.016f, 1f));
                    m.SetFloat("_Metallic", 0f);
                    m.SetFloat("_Smoothness", 0.55f);
                    if (m.HasProperty("_ClearCoat"))
                    {
                        m.SetFloat("_ClearCoat", 1f);
                        m.SetFloat("_ClearCoatMask", 1f);
                        m.SetFloat("_ClearCoatSmoothness", 0.97f);
                        m.EnableKeyword("_CLEARCOAT");
                    }
                    else
                    {
                        m.SetFloat("_Smoothness", 0.9f);
                    }
                }),
                brass = EnsureMaterial("StudioBrass", "Universal Render Pipeline/Lit", m =>
                {
                    m.SetColor("_BaseColor", new Color(0.93f, 0.71f, 0.38f, 1f));
                    m.SetFloat("_Metallic", 1f);
                    m.SetFloat("_Smoothness", 0.78f);
                }),
                cyclorama = EnsureMaterial("StudioCyclorama", "Universal Render Pipeline/Lit", m =>
                {
                    m.SetTexture("_BaseMap", EnsureGradientTexture());
                    m.SetColor("_BaseColor", Color.white);
                    m.SetFloat("_Metallic", 0f);
                    m.SetFloat("_Smoothness", 0.62f);
                    m.SetFloat("_Cull", 0f); // two-sided: the sweep is seen from both sides of its curve
                    m.doubleSidedGI = true;
                })
            };
        }

        static Material EnsureGlass(string name, Color hue)
        {
            string path = $"{Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader glassShader = Shader.Find("Simulation Lobby/Studio Glass");
            Material material;
            if (glassShader != null && !ShaderUtil.ShaderHasError(glassShader))
            {
                material = new Material(glassShader);
                material.SetColor("_Tint", hue);
                material.SetColor("_RimColor", Color.Lerp(hue, Color.white, 0.55f));
            }
            else
            {
                // Fallback so a shader compile problem degrades the look instead of breaking the scene:
                // URP Lit, transparent, specular preserved — glossy tinted glass without refraction.
                Debug.LogWarning("[Studio] 'Simulation Lobby/Studio Glass' is missing or failed to compile — " +
                                 "using transparent URP Lit for the glass. Check the shader in the Console.");
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(hue.r, hue.g, hue.b, 0.38f));
                material.SetFloat("_Smoothness", 0.96f);
                MakeTransparent(material, 0);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Turns a URP Lit material transparent (alpha blend, specular preserved) the way the material
        /// Inspector would, via URP's own <see cref="BaseShaderGUI.SetupMaterialBlendMode"/>, so every
        /// keyword and blend state matches what URP expects. <paramref name="queueOffset"/> orders it
        /// after other transparents (e.g. +10 draws a glass plinth over the glass subject inside it).
        /// </summary>
        static void MakeTransparent(Material material, int queueOffset)
        {
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f); // Alpha
            material.SetFloat("_BlendModePreserveSpecular", 1f);
            material.SetFloat("_QueueOffset", queueOffset);
            BaseShaderGUI.SetupMaterialBlendMode(material);
        }

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

        public static Material EnsureCardMaterial()
        {
            return EnsureMaterial("StudioSoftbox", "Universal Render Pipeline/Unlit", m =>
            {
                // HDR white: brighter than 1 so reflections of it bloom into a clean highlight.
                m.SetColor("_BaseColor", new Color(3.2f, 3.1f, 3f, 1f));
                m.SetFloat("_Cull", 0f);
            });
        }

        /// <summary>
        /// Vertical gradient for the cyclorama: a near-white floor rising into warm beige.
        /// </summary>
        /// <remarks>
        /// v1 was charcoal-to-black, and a smoked-glass plinth and a dark-tinted capsule disappeared
        /// into it. v2 tried mid-tone slate. v3 (playtest 5) goes white-to-beige: the dark glass
        /// objects read as clean silhouettes on a bright field — the high-key product-shot look.
        /// </remarks>
        static Texture2D EnsureGradientTexture()
        {
            string path = "Assets/Art/Generated/StudioGradient_v3.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/Art/Generated");
            const int height = 256;
            var texture = new Texture2D(4, height, TextureFormat.RGBA32, false);
            var stops = new[]
            {
                new KeyValuePair<float, Color>(0f, new Color(0.97f, 0.96f, 0.94f)),
                new KeyValuePair<float, Color>(0.35f, new Color(0.95f, 0.93f, 0.89f)),
                new KeyValuePair<float, Color>(0.55f, new Color(0.90f, 0.86f, 0.79f)),
                new KeyValuePair<float, Color>(1f, new Color(0.80f, 0.74f, 0.64f))
            };
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                Color color = stops[stops.Length - 1].Value;
                for (int s = 0; s < stops.Length - 1; s++)
                {
                    if (v <= stops[s + 1].Key)
                    {
                        float t = Mathf.InverseLerp(stops[s].Key, stops[s + 1].Key, v);
                        color = Color.Lerp(stops[s].Value, stops[s + 1].Value, Mathf.SmoothStep(0f, 1f, t));
                        break;
                    }
                }

                for (int x = 0; x < 4; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ set pieces

        /// <summary>
        /// Seamless floor-to-wall sweep: no horizon line, so the subject floats in a gradient. The
        /// floor runs under the camera, the wall rises 6m behind the subject.
        /// </summary>
        static void BuildCyclorama(Material material, Vector3 cameraPosition)
        {
            const float width = 40f;
            const float back = 6f;
            const float bend = 3f;
            const float wallHeight = 16f;
            float front = Mathf.Max(8f, cameraPosition.z + 4f);

            // Profile in (z, y), front edge to top of wall.
            var profile = new List<Vector2> { new Vector2(front, 0f) };
            const int floorSteps = 16;
            for (int i = 1; i <= floorSteps; i++)
            {
                profile.Add(new Vector2(Mathf.Lerp(front, -back + bend, i / (float)floorSteps), 0f));
            }

            const int bendSteps = 16;
            for (int i = 1; i <= bendSteps; i++)
            {
                float angle = Mathf.PI * 0.5f * i / bendSteps;
                profile.Add(new Vector2(-back + bend - Mathf.Sin(angle) * bend, bend - Mathf.Cos(angle) * bend));
            }

            const int wallSteps = 12;
            for (int i = 1; i <= wallSteps; i++)
            {
                profile.Add(new Vector2(-back, Mathf.Lerp(bend, wallHeight, i / (float)wallSteps)));
            }

            float totalLength = 0f;
            var along = new float[profile.Count];
            for (int i = 1; i < profile.Count; i++)
            {
                totalLength += Vector2.Distance(profile[i - 1], profile[i]);
                along[i] = totalLength;
            }

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i < profile.Count; i++)
            {
                float v = along[i] / totalLength;
                vertices.Add(new Vector3(-width * 0.5f, profile[i].y, profile[i].x));
                vertices.Add(new Vector3(width * 0.5f, profile[i].y, profile[i].x));
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));
                if (i == 0)
                {
                    continue;
                }

                int a = (i - 1) * 2;
                triangles.Add(a);
                triangles.Add(a + 2);
                triangles.Add(a + 1);
                triangles.Add(a + 1);
                triangles.Add(a + 2);
                triangles.Add(a + 3);
            }

            var mesh = new Mesh { name = "Cyclorama" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();

            // Faces must point up off the floor (toward the subject); flip if the winding came out
            // the other way.
            if (mesh.normals[2].y < 0f)
            {
                int[] t = mesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
                }

                mesh.triangles = t;
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            var cyclorama = new GameObject("Cyclorama");
            cyclorama.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, "Cyclorama");
            var renderer = cyclorama.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildLights(Options options)
        {
            Vector3 subject = options.subjectCenter;
            float size = Mathf.Max(1f, options.subjectHeight);
            var root = new GameObject("Studio Lights").transform;

            // Diffuse by design (playtest 4: "light is harsh"). URP has no realtime area lights, so
            // softness comes from: lower intensities spread across more lights, wide spot cones with
            // a near-zero inner angle (a long, gradual falloff instead of a hard-edged pool), weaker
            // shadows, a brighter ambient fill, and large dim softbox cards for broad reflections
            // rather than small hot ones.

            // Key: high front-left, warm. The one light that casts, with soft, half-strength shadows.
            Light key = CreateLight(root, "Key", LightType.Directional, new Color(1f, 0.96f, 0.9f), 0.85f);
            key.transform.rotation = Quaternion.Euler(40f, 148f, 0f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.5f;
            key.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;

            // Rims: two spots behind the subject, either side. They draw the bright edge down both
            // sides of the glass — the strongest "product shot" cue — now as a wide soft glow.
            for (int side = -1; side <= 1; side += 2)
            {
                Light rim = CreateLight(root, side < 0 ? "Rim Left" : "Rim Right", LightType.Spot,
                    new Color(0.9f, 0.95f, 1f), 8f);
                rim.transform.position = subject + new Vector3(side * 1.9f * size * 0.5f + side * 1.2f, size * 0.55f, -1.8f);
                rim.transform.LookAt(subject);
                rim.range = 10f;
                rim.spotAngle = 75f;
                rim.innerSpotAngle = 2f;
            }

            // Halo: a gentle neutral glow on the backdrop behind the subject, lifting it a touch
            // brighter right where the dark subject sits — maximum contrast at the focal point.
            Light halo = CreateLight(root, "Backdrop Halo", LightType.Spot, new Color(1f, 0.98f, 0.95f), 6f);
            halo.transform.position = subject + new Vector3(0f, -size * 0.2f, -1.2f);
            halo.transform.LookAt(new Vector3(subject.x, subject.y + size * 0.3f, -6f));
            halo.range = 16f;
            halo.spotAngle = 100f;
            halo.innerSpotAngle = 2f;

            // Front fill: broad and low from the camera side, so the front of whatever the subject
            // stands on isn't left in the key light's shadow side.
            Light fill = CreateLight(root, "Front Fill", LightType.Spot, new Color(1f, 0.97f, 0.93f), 9f);
            fill.transform.position = new Vector3(subject.x, subject.y * 0.55f, subject.z + 6f);
            fill.transform.LookAt(new Vector3(subject.x, subject.y * 0.3f, subject.z));
            fill.range = 16f;
            fill.spotAngle = 85f;
            fill.innerSpotAngle = 2f;

            // Top: a broad soft wash from above, so the top surfaces read without a hot spot.
            Light top = CreateLight(root, "Top", LightType.Spot, new Color(1f, 0.97f, 0.93f), 5f);
            top.transform.position = subject + Vector3.up * (size * 1.4f + 1.5f);
            top.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            top.range = 14f;
            top.spotAngle = 90f;
            top.innerSpotAngle = 2f;
        }

        static Light CreateLight(Transform parent, string name, LightType type, Color color, float intensity)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        static void BuildSoftboxCards(Camera camera, Options options)
        {
            Material cardMaterial = EnsureCardMaterial();
            // Rig-owned like the lights: re-applied each build. Dimmer than the first build's 3.2 —
            // large, moderately bright cards give broad soft reflections; small hot ones read harsh.
            cardMaterial.SetColor("_BaseColor", new Color(1.8f, 1.8f, 1.8f, 1f));
            EditorUtility.SetDirty(cardMaterial);
            Transform cam = camera.transform;
            Vector3 subject = options.subjectCenter;
            var root = new GameObject("Softbox Cards (reflection only)").transform;

            void Card(string name, Vector3 position, Vector2 size)
            {
                var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = name;
                card.transform.SetParent(root, false);
                card.transform.position = position;
                // A quad shows its face along -forward, so point forward away from the subject.
                card.transform.rotation = Quaternion.LookRotation(position - subject);
                card.transform.localScale = new Vector3(size.x, size.y, 1f);
                Object.DestroyImmediate(card.GetComponent<Collider>());
                var renderer = card.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = cardMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            // All behind the camera plane: invisible to it at any aspect, visible to every reflection.
            Vector3 behind = -cam.forward;
            Card("Softbox Overhead", cam.position + behind * 2.5f + Vector3.up * 3.2f, new Vector2(11f, 5f));

            // The one the front faces see. A vertical face, viewed from a camera slightly above it,
            // mirrors a band just above camera height behind the camera. Without a card there the
            // lacquer front reflects darkness and reads as a black hole in the frame (first playtest).
            Card("Softbox Front (low)", cam.position + behind * 2.5f + Vector3.up * 0.6f, new Vector2(11f, 2.8f));
            Card("Strip Left", cam.position + behind * 1.5f - cam.right * 4.5f + Vector3.up * 0.5f, new Vector2(1.8f, 7f));
            Card("Strip Right", cam.position + behind * 1.5f + cam.right * 4.5f + Vector3.up * 0.5f, new Vector2(1.8f, 7f));
        }

        static void BuildReflectionProbe(Options options)
        {
            var probeObject = new GameObject("Studio Reflection Probe");
            probeObject.transform.position = options.subjectCenter;
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 512;
            probe.hdr = true;
            probe.boxProjection = true;
            probe.size = new Vector3(40f, 20f, 40f);
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.01f, 0.01f, 0.012f, 1f);
            probe.nearClipPlane = 0.05f;
            probe.farClipPlane = 60f;
            probe.intensity = 1f;
            probe.importance = 10;
        }

        static void BuildPostVolume()
        {
            string path = $"{Folder}/StudioPost.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);

                Tonemapping tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.Neutral);

                // High threshold: only specular glints and the softbox reflections bloom.
                Bloom bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(1.05f);
                bloom.intensity.Override(0.6f);
                bloom.scatter.Override(0.62f);

                Vignette vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.26f);
                vignette.smoothness.Override(0.45f);

                ColorAdjustments color = profile.Add<ColorAdjustments>(true);
                color.contrast.Override(10f);
                color.saturation.Override(6f);

                foreach (VolumeComponent component in profile.components)
                {
                    component.name = component.GetType().Name;
                    AssetDatabase.AddObjectToAsset(component, profile);
                }

                EditorUtility.SetDirty(profile);
            }

            // Rig-owned: re-applied every build. On a bright white set, lit surfaces exceed the old 1.05
            // threshold and bloom turns into a haze over the whole frame, flattening the contrast the
            // backdrop exists for. Only true glints (above ~1.5) should bloom.
            if (profile.TryGet(out Bloom bloomSettings))
            {
                bloomSettings.threshold.Override(1.5f);
                bloomSettings.intensity.Override(0.35f);
                EditorUtility.SetDirty(profile);
            }

            var volumeObject = new GameObject("Studio Post");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        static void ConfigureCamera(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Matches the top of the backdrop gradient, so anything past the cyclorama (and the fog)
            // blends into it rather than into black.
            camera.backgroundColor = new Color(0.80f, 0.74f, 0.64f, 1f);
            camera.allowHDR = true;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            // SMAA: thin specular edges on glass and chrome crawl without it, and MSAA is off in PC_RPAsset.
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            // The glass refracts the opaque texture; force it on for this camera whatever the asset says.
            data.requiresColorOption = CameraOverrideOption.On;
            data.requiresDepthOption = CameraOverrideOption.On;
        }

        static void ConfigureEnvironment(Camera camera, Options options)
        {
            float distance = Vector3.Distance(camera.transform.position, options.subjectCenter);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Bright, warm ambient (it bounces off a white studio): the shadow side is filled rather than black, which is most of
            // what makes light read as diffuse instead of harsh.
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.41f, 0.39f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.32f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.19f, 0.17f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = distance * 1.5f;
            RenderSettings.fogEndDistance = distance * 4f;
        }

        /// <summary>
        /// Saves a generated mesh as an asset (replacing any earlier one) so the scene references a file
        /// rather than an object that only exists in memory.
        /// </summary>
        public static Mesh SaveMesh(Mesh mesh, string name)
        {
            string folder = $"{Folder}/Meshes";
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Copy into the existing asset: keeps its GUID, so nothing else pointing at it breaks.
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
