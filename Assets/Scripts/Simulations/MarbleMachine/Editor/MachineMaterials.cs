using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Creates the <see cref="MachineKit"/>: procedurally generated textures (saved as PNG, nothing imported by hand)
    /// and URP Lit materials for the neutral, textured machinery plus one glossy instanced material per ball colour
    /// (GPU instancing on: the frozen silo stack and the settled tube balls are drawn with Graphics.RenderMeshInstanced).
    /// </summary>
    /// <remarks>
    /// Re-running rewrites texture bytes only if they changed and updates materials in place, so GUIDs and every
    /// reference in saved scenes survive. Delete an asset to regenerate it from scratch.
    /// </remarks>
    public static class MachineMaterials
    {
        public const string Root = "Assets/Art/Generated/MarbleMachine";
        public const string TextureFolder = Root + "/Textures";
        public const string MaterialFolder = Root + "/Materials";

        const int TexSize = 512;

        public static MachineKit EnsureKit(MachineConfig config)
        {
            Directory.CreateDirectory(TextureFolder);
            Directory.CreateDirectory(MaterialFolder);

            Texture2D wood = EnsureTexture("Wood", TexSize, true, false, (u, v) => WoodPixel(u, v, new Color(0.80f, 0.60f, 0.38f), new Color(0.62f, 0.43f, 0.25f), 11));
            Texture2D woodDark = EnsureTexture("WoodDark", TexSize, true, false, (u, v) => WoodPixel(u, v, new Color(0.42f, 0.27f, 0.17f), new Color(0.30f, 0.18f, 0.11f), 29));
            Texture2D asphalt = EnsureTexture("Asphalt", TexSize, true, false, AsphaltPixel);
            Texture2D terrazzo = EnsureTexture("Terrazzo", TexSize, true, false, null, TerrazzoPixels);
            Texture2D brick = EnsureTexture("Brick", TexSize, true, false, BrickPixel);
            Texture2D mesh = EnsureTexture("Mesh", 256, true, true, MeshPixel);

            var kit = new MachineKit();
            kit.wood = EnsureLit("Machine_Wood", m => Opaque(m, Color.white, wood, 0f, 0.28f));
            kit.woodDark = EnsureLit("Machine_WoodDark", m => Opaque(m, Color.white, woodDark, 0f, 0.32f));
            kit.asphalt = EnsureLit("Machine_Asphalt", m => Opaque(m, Color.white, asphalt, 0f, 0.12f));
            kit.terrazzo = EnsureLit("Machine_Terrazzo", m => Opaque(m, Color.white, terrazzo, 0f, 0.55f));
            kit.brick = EnsureLit("Machine_Brick", m => Opaque(m, Color.white, brick, 0f, 0.1f));
            kit.chrome = EnsureLit("Machine_Chrome", m => Opaque(m, new Color(0.88f, 0.89f, 0.92f), null, 1f, 0.8f));
            kit.castIron = EnsureLit("Machine_CastIron", m => Opaque(m, new Color(0.17f, 0.17f, 0.19f), null, 0.6f, 0.35f));
            kit.copper = EnsureLit("Machine_Copper", m => Opaque(m, new Color(0.85f, 0.46f, 0.30f), null, 1f, 0.55f));
            kit.brass = EnsureLit("Machine_Brass", m => Opaque(m, new Color(0.86f, 0.68f, 0.30f), null, 1f, 0.5f));
            kit.rubber = EnsureLit("Machine_Rubber", m => Opaque(m, new Color(0.10f, 0.32f, 0.85f), null, 0f, 0.4f));
            kit.whiteLacquer = EnsureLit("Machine_WhiteLacquer", m => Opaque(m, new Color(0.93f, 0.93f, 0.91f), null, 0f, 0.5f));
            kit.glass = EnsureLit("Machine_Glass", Glass);
            kit.tubeGlass = EnsureLit("Machine_TubeGlass", m =>
            {
                Glass(m);
                m.SetColor("_BaseColor", new Color(0.86f, 0.92f, 0.97f, 0.14f));
                m.SetFloat("_Smoothness", 0.2f);
                m.SetFloat("_SpecularHighlights", 0f);
                m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            });
            kit.satinMetal = EnsureLit("Machine_SatinMetal", m =>
            {
                Opaque(m, new Color(0.62f, 0.63f, 0.66f), null, 0.35f, 0.25f);
                m.SetFloat("_SpecularHighlights", 0f);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            });
            kit.mesh = EnsureLit("Machine_Mesh", m =>
            {
                Opaque(m, Color.white, mesh, 0.7f, 0.5f);
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f);
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            });
            kit.emissive = EnsureLit("Machine_Emissive", m =>
            {
                Opaque(m, new Color(0.9f, 0.9f, 0.9f), null, 0f, 0.5f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white * 1.5f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.enableInstancing = true;
            });

            kit.boostArrow = EnsureLit("Machine_BoostArrow", m =>
            {
                Opaque(m, new Color(1f, 0.82f, 0.05f), null, 0f, 0.3f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1f, 0.7f, 0f) * 2.2f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });

            int count = config != null ? config.ColourCount : 0;
            kit.ballMaterials = new Material[count];
            for (int i = 0; i < count; i++)
            {
                Color colour = config.colours[i].color;
                string safe = Sanitize(config.colours[i].name);
                kit.ballMaterials[i] = EnsureLit($"Ball_{i}_{safe}", m =>
                {
                    Opaque(m, colour, null, 0f, 0.45f); // satin: a glossy ball puts a bright glint on every one of 16,800
                    m.enableInstancing = true;
                });
            }

            kit.ballMesh = BallMesh();
            AssetDatabase.SaveAssets();
            return kit;
        }

        /// <summary>
        /// mach-002: one glossy GPU-instanced URP Lit material per racer, saved as <c>Racer_NN.mat</c> (NN = racer number) from the
        /// config's colours. Updated in place on re-runs, so scene references survive. Separate from the kit's satin ball materials.
        /// </summary>
        public static Material[] EnsureRacerMaterials(MachineConfig config, float smoothness = 0.92f)
        {
            Directory.CreateDirectory(MaterialFolder);
            int count = config != null ? config.ColourCount : 0;
            var result = new Material[count];
            for (int i = 0; i < count; i++)
            {
                Color colour = config.colours[i].color;
                result[i] = EnsureLit($"Racer_{i + 1:00}", m =>
                {
                    Opaque(m, colour, null, 0f, smoothness);
                    m.enableInstancing = true;
                });
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        // ------------------------------------------------------------------ materials

        static Material EnsureLit(string name, Action<Material> setup)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError($"[mach] URP Lit shader not found for {name}.");
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            setup(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Opaque(Material m, Color colour, Texture2D map, float metallic, float smoothness)
        {
            m.SetFloat("_Surface", 0f);
            m.SetColor("_BaseColor", colour);
            m.SetTexture("_BaseMap", map);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
        }

        static void Glass(Material m)
        {
            // Transparent URP Lit with premultiplied alpha so the specular survives the low alpha: a visible edge and glints.
            m.SetColor("_BaseColor", new Color(0.82f, 0.90f, 0.96f, 0.2f));
            m.SetTexture("_BaseMap", null);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.55f); // a soft sheen, not a mirror: sharp glints off the key light were the glare
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_BlendModePreserveSpecular", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 2f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>
        /// Low-poly icosphere (subdivision 2, 320 triangles, smooth normals), diameter 1: the simulation scales it by
        /// <c>ballRadius</c> at runtime, so nothing here depends on the radius. Saved fresh every build (SaveMeshFresh rule).
        /// Active balls and settled tube balls use it; the frozen silo stack uses an 80-triangle copy the simulation builds itself.
        /// </summary>
        static Mesh BallMesh()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            var tris = new System.Collections.Generic.List<int>(f);
            for (int level = 0; level < 2; level++)
            {
                var cache = new System.Collections.Generic.Dictionary<long, int>();
                var next = new System.Collections.Generic.List<int>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(verts, cache, a, b), bc = Mid(verts, cache, b, c), ca = Mid(verts, cache, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }

                tris = next;
            }

            var normals = new Vector3[verts.Count];
            var positions = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                normals[i] = verts[i];
                positions[i] = verts[i] * 0.5f;
            }

            var mesh = new Mesh { name = "Machine_Ball", vertices = positions, normals = normals };
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return SimulationLobby.Shared.EditorTools.TrackMeshAssets.SaveMeshFresh(mesh, "Machine_Ball", MachineGeometry.MeshFolder);
        }

        static int Mid(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            index = verts.Count - 1;
            cache[key] = index;
            return index;
        }

        static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "colour";
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            return value.Replace(' ', '_');
        }

        // ------------------------------------------------------------------ texture assets

        /// <summary>
        /// Generates a texture (per-pixel <paramref name="pixel"/> function in 0..1 uv, or a whole-image
        /// <paramref name="whole"/> generator), writes it as PNG only if the bytes changed, and applies import settings.
        /// </summary>
        public static Texture2D EnsureTexture(string name, int size, bool repeat, bool alpha,
            Func<float, float, Color> pixel, Func<int, Color[]> whole = null, int height = 0, bool mipmaps = true)
        {
            int h = height > 0 ? height : size;
            Color[] pixels = whole != null ? whole(size) : null;
            if (pixels == null)
            {
                pixels = new Color[size * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / h);
                    }
                }
            }

            var tex = new Texture2D(size, h, TextureFormat.RGBA32, false, false);
            tex.SetPixels(pixels);
            tex.Apply(false);
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            Directory.CreateDirectory(TextureFolder);
            string path = $"{TextureFolder}/{name}.png";
            bool changed = !File.Exists(path) || !BytesEqual(File.ReadAllBytes(path), png);
            if (changed)
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                dirty |= Apply(importer.textureType, TextureImporterType.Default, v => importer.textureType = v);
                dirty |= Apply(importer.sRGBTexture, true, v => importer.sRGBTexture = v);
                dirty |= Apply(importer.wrapMode, repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, v => importer.wrapMode = v);
                dirty |= Apply(importer.mipmapEnabled, mipmaps, v => importer.mipmapEnabled = v);
                dirty |= Apply(importer.alphaIsTransparency, alpha, v => importer.alphaIsTransparency = v);
                dirty |= Apply(importer.alphaSource, alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None, v => importer.alphaSource = v);
                dirty |= Apply(importer.filterMode, FilterMode.Trilinear, v => importer.filterMode = v);
                dirty |= Apply(importer.anisoLevel, 8, v => importer.anisoLevel = v);
                dirty |= Apply(importer.maxTextureSize, 1024, v => importer.maxTextureSize = v);
                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static bool Apply<T>(T current, T wanted, Action<T> set) where T : struct
        {
            if (current.Equals(wanted))
            {
                return false;
            }

            set(wanted);
            return true;
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------ noise

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static int Wrap(int i, int period)
        {
            int m = i % period;
            return m < 0 ? m + period : m;
        }

        /// <summary>Tileable value noise: u and v in 0..1, lattice of <paramref name="px"/> x <paramref name="py"/> cells.</summary>
        static float Noise(float u, float v, int px, int py, int seed)
        {
            float x = u * px;
            float y = v * py;
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(Wrap(ix, px), Wrap(iy, py), seed);
            float b = Hash(Wrap(ix + 1, px), Wrap(iy, py), seed);
            float c = Hash(Wrap(ix, px), Wrap(iy + 1, py), seed);
            float d = Hash(Wrap(ix + 1, px), Wrap(iy + 1, py), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, int px, int py, int octaves, int seed)
        {
            float sum = 0f;
            float amp = 0.5f;
            float norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(u, v, px << o, py << o, seed + o * 17) * amp;
                norm += amp;
                amp *= 0.5f;
            }

            return sum / norm;
        }

        // ------------------------------------------------------------------ pixel functions

        static Color WoodPixel(float u, float v, Color light, Color dark, int seed)
        {
            const int planks = 4;
            float pv = v * planks;
            int plank = Mathf.FloorToInt(pv);
            float lv = pv - plank;
            Color baseColour = Color.Lerp(light, dark, Hash(plank, 3, seed) * 0.8f);

            // Grain: noise stretched along the plank, bent into rings.
            float warp = Fbm(u, v, 2, planks * 20, 4, seed);
            float rings = 0.5f + 0.5f * Mathf.Sin((warp * 9f + lv * 5f + Hash(plank, 9, seed) * 6f) * Mathf.PI);
            float fine = Noise(u, v, 4, planks * 90, seed + 5);
            float tone = 0.80f + 0.14f * rings + 0.10f * (fine - 0.5f);
            Color c = baseColour * tone;

            // Seams between planks and a staggered end joint per plank.
            float seam = Mathf.Min(lv, 1f - lv) * TexSize / planks;
            if (seam < 1.5f)
            {
                c *= 0.45f;
            }

            float joint = Mathf.Abs(Mathf.Repeat(u - Hash(plank, 7, seed), 1f));
            if (joint < 0.004f || joint > 0.996f)
            {
                c *= 0.55f;
            }

            c.a = 1f;
            return c;
        }

        static Color AsphaltPixel(float u, float v)
        {
            int px = Mathf.FloorToInt(u * TexSize);
            int py = Mathf.FloorToInt(v * TexSize);
            float low = Fbm(u, v, 6, 6, 4, 41);
            float grit = Hash(px, py, 5) - 0.5f;
            float c = 0.15f + 0.07f * (low - 0.5f) + 0.10f * grit;
            float speck = Hash(px, py, 77);
            if (speck > 0.985f)
            {
                c += 0.10f + 0.20f * Hash(px, py, 78);
            }

            c = Mathf.Clamp01(c);
            return new Color(c, c, c * 1.04f, 1f);
        }

        static Color BrickPixel(float u, float v)
        {
            const int rowH = 32;
            const int brickW = 128;
            const int mortar = 5;
            int x = Mathf.FloorToInt(u * TexSize);
            int y = Mathf.FloorToInt(v * TexSize);
            int row = y / rowH;
            int shifted = (x + (row % 2) * (brickW / 2)) % TexSize;
            int col = shifted / brickW;
            int xx = shifted % brickW;
            int yy = y % rowH;

            float speckle = Hash(x, y, 13) - 0.5f;
            float low = Fbm(u, v, 12, 12, 3, 61);
            if (xx < mortar || yy < mortar)
            {
                float m = 0.70f + 0.08f * speckle + 0.06f * (low - 0.5f);
                return new Color(m, m * 0.98f, m * 0.94f, 1f);
            }

            float variation = Hash(col, row, 3);
            Color brick = Color.Lerp(new Color(0.58f, 0.24f, 0.17f), new Color(0.46f, 0.18f, 0.14f), variation);
            float edge = Mathf.Min(Mathf.Min(xx - mortar, brickW - xx), Mathf.Min(yy - mortar, rowH - yy));
            float bevel = edge < 2f ? 0.82f : 1f;
            float tone = (0.88f + 0.22f * (low - 0.5f) + 0.20f * speckle) * bevel;
            return new Color(brick.r * tone, brick.g * tone, brick.b * tone, 1f);
        }

        static Color MeshPixel(float u, float v)
        {
            const int cells = 8;
            float cu = u * cells;
            float cv = v * cells;
            float du = cu - Mathf.Floor(cu) - 0.5f;
            float dv = cv - Mathf.Floor(cv) - 0.5f;
            float dist = Mathf.Sqrt(du * du + dv * dv);
            float n = Noise(u, v, 32, 32, 9);
            float metal = 0.55f + 0.12f * (n - 0.5f);
            if (dist < 0.30f)
            {
                return new Color(metal, metal, metal, 0f);
            }

            float rim = dist < 0.36f ? 0.8f : 1f;
            return new Color(metal * rim, metal * rim * 1.02f, metal * rim * 1.05f, 1f);
        }

        static Color[] TerrazzoPixels(int size)
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Fbm((x + 0.5f) / size, (y + 0.5f) / size, 8, 8, 3, 91) - 0.5f;
                    float g = Hash(x, y, 19) - 0.5f;
                    float b = 0.89f + 0.04f * n + 0.02f * g;
                    pixels[y * size + x] = new Color(b, b * 0.99f, b * 0.96f, 1f);
                }
            }

            Color[] palette =
            {
                new Color(0.34f, 0.34f, 0.37f), new Color(0.52f, 0.52f, 0.54f), new Color(0.68f, 0.68f, 0.70f),
                new Color(0.60f, 0.54f, 0.47f), new Color(0.44f, 0.49f, 0.54f), new Color(0.62f, 0.45f, 0.40f),
                new Color(0.42f, 0.52f, 0.45f), new Color(0.24f, 0.24f, 0.26f)
            };
            var rng = new System.Random(1234);
            int chips = 700;
            for (int i = 0; i < chips; i++)
            {
                float cx = (float)rng.NextDouble() * size;
                float cy = (float)rng.NextDouble() * size;
                float rx = 1.2f + (float)rng.NextDouble() * (float)rng.NextDouble() * 6f;
                float ry = rx * (0.5f + (float)rng.NextDouble() * 0.6f);
                float angle = (float)rng.NextDouble() * Mathf.PI;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                Color colour = palette[rng.Next(palette.Length)];
                float shade = 0.9f + (float)rng.NextDouble() * 0.2f;
                int reach = Mathf.CeilToInt(Mathf.Max(rx, ry)) + 1;
                for (int dy = -reach; dy <= reach; dy++)
                {
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        float lx = dx * cos + dy * sin;
                        float ly = -dx * sin + dy * cos;
                        float e = (lx * lx) / (rx * rx) + (ly * ly) / (ry * ry);
                        if (e > 1f)
                        {
                            continue;
                        }

                        int px = Wrap((int)cx + dx, size);
                        int py = Wrap((int)cy + dy, size);
                        pixels[py * size + px] = new Color(colour.r * shade, colour.g * shade, colour.b * shade, 1f);
                    }
                }
            }

            return pixels;
        }
    }
}
