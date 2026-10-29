using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// mach-002 "nature" material kit: warm wood, dark forest-meadow grass, mossy stone, cool-tinted glass. Everything is saved
    /// under <see cref="Root"/> with a <c>Nature_</c> prefix, so mach-001's assets in MarbleMachine/ are never overwritten.
    /// Textures are procedural, 512 px, tileable and deliberately low-contrast: the primitive cubes map UV 0-1 per face, so a long
    /// beam stretches them, and the balls must stay the only strong colour (16 saturated racers on dark muted green and brown).
    /// </summary>
    public static class MachineNatureMaterials
    {
        public const string Root = "Assets/Art/Generated/MarbleMachineNature";
        public const string TextureFolder = Root + "/Textures";
        public const string MaterialFolder = Root + "/Materials";

        const int TexSize = 512;

        /// <summary>
        /// The standard kit (so ballMesh and the shared accent materials exist, exactly as before) with every surface role
        /// remapped to a nature material. Roles: wood/woodDark = warm planks / dark stained wood, asphalt = meadow grass (chute floors),
        /// terrazzo = lighter meadow grass (funnels, cones), brick = mossy stone, brass/copper = dark bronze, castIron = dark slate,
        /// glass/tubeGlass = cool-tinted glass. Chrome, rubber, mesh and the emissive materials are left as they were.
        /// </summary>
        public static MachineKit EnsureKit(MachineConfig config)
        {
            MachineKit kit = MachineMaterials.EnsureKit(config);

            Directory.CreateDirectory(TextureFolder);
            Directory.CreateDirectory(MaterialFolder);

            Texture2D wood = EnsureTexture("Nature_Wood", false, (u, v) => WoodPixel(u, v, new Color(0.60f, 0.42f, 0.25f), new Color(0.52f, 0.36f, 0.21f), 11));
            Texture2D woodDark = EnsureTexture("Nature_WoodDark", false, (u, v) => WoodPixel(u, v, new Color(0.34f, 0.23f, 0.15f), new Color(0.28f, 0.19f, 0.12f), 29));
            Texture2D grass = EnsureTexture("Nature_Grass", false, (u, v) => GrassPixel(u, v, new Color(0.17f, 0.27f, 0.12f), new Color(0.26f, 0.38f, 0.17f), 5));
            Texture2D meadow = EnsureTexture("Nature_Meadow", false, (u, v) => GrassPixel(u, v, new Color(0.21f, 0.31f, 0.14f), new Color(0.31f, 0.42f, 0.20f), 23));
            Texture2D stone = EnsureTexture("Nature_MossStone", false, StonePixel);

            kit.wood = EnsureLit("Nature_Wood", m => Opaque(m, Color.white, wood, 0f, 0.22f));
            kit.woodDark = EnsureLit("Nature_WoodDark", m => Opaque(m, Color.white, woodDark, 0f, 0.26f));
            kit.asphalt = EnsureLit("Nature_Grass", m => Opaque(m, Color.white, grass, 0f, 0.06f));
            kit.terrazzo = EnsureLit("Nature_Meadow", m => Opaque(m, Color.white, meadow, 0f, 0.08f));
            kit.brick = EnsureLit("Nature_MossStone", m => Opaque(m, Color.white, stone, 0f, 0.12f));
            kit.castIron = EnsureLit("Nature_Slate", m => Opaque(m, new Color(0.15f, 0.17f, 0.17f), null, 0.4f, 0.3f));
            kit.brass = EnsureLit("Nature_Bronze", m => Opaque(m, new Color(0.55f, 0.40f, 0.20f), null, 1f, 0.45f));
            kit.copper = EnsureLit("Nature_Copper", m => Opaque(m, new Color(0.58f, 0.34f, 0.22f), null, 1f, 0.5f));
            kit.whiteLacquer = EnsureLit("Nature_Birch", m => Opaque(m, new Color(0.80f, 0.78f, 0.70f), null, 0f, 0.35f));
            // Names keep "Glass": MachineGeometry thickens colliders for materials whose name contains it.
            kit.glass = EnsureLit("Nature_Glass", m => Glass(m, new Color(0.78f, 0.92f, 0.97f, 0.2f), 0.55f));
            kit.tubeGlass = EnsureLit("Nature_TubeGlass", m =>
            {
                Glass(m, new Color(0.80f, 0.93f, 0.97f, 0.14f), 0.2f);
                m.SetFloat("_SpecularHighlights", 0f);
                m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            });

            AssetDatabase.SaveAssets();
            return kit;
        }

        // ------------------------------------------------------------------ public helpers (environment)

        /// <summary>Saves a texture with explicit size, sRGB / normal-map flag and wrap; returns the imported asset.</summary>
        public static Texture2D EnsureTexture(string name, bool normalMap, Func<float, float, Color> pixel, int width = TexSize, int height = TexSize,
            bool mipmaps = true, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = pixel((x + 0.5f) / width, (y + 0.5f) / height);
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            tex.SetPixels(pixels);
            tex.Apply(false);
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            Directory.CreateDirectory(TextureFolder);
            string path = $"{TextureFolder}/{name}.png";
            if (!File.Exists(path) || !BytesEqual(File.ReadAllBytes(path), png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                var type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                dirty |= Apply(importer.textureType, type, v => importer.textureType = v);
                dirty |= Apply(importer.sRGBTexture, !normalMap, v => importer.sRGBTexture = v);
                dirty |= Apply(importer.wrapMode, wrap, v => importer.wrapMode = v);
                dirty |= Apply(importer.mipmapEnabled, mipmaps, v => importer.mipmapEnabled = v);
                dirty |= Apply(importer.filterMode, FilterMode.Trilinear, v => importer.filterMode = v);
                dirty |= Apply(importer.anisoLevel, 8, v => importer.anisoLevel = v);
                dirty |= Apply(importer.maxTextureSize, 2048, v => importer.maxTextureSize = v);
                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material EnsureLit(string name, Action<Material> setup) => EnsureMaterial(name, "Universal Render Pipeline/Lit", setup);

        public static Material EnsureMaterial(string name, string shaderName, Action<Material> setup)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogError($"[mach] Shader '{shaderName}' not found for {name}.");
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            setup(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void Opaque(Material m, Color colour, Texture2D map, float metallic, float smoothness)
        {
            m.SetFloat("_Surface", 0f);
            m.SetColor("_BaseColor", colour);
            m.SetTexture("_BaseMap", map);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
        }

        static void Glass(Material m, Color tint, float smoothness)
        {
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BaseMap", null);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smoothness);
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

        static bool Apply<T>(T current, T wanted, Action<T> set) where T : struct
        {
            if (current.Equals(wanted)) return false;
            set(wanted);
            return true;
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }

        // ------------------------------------------------------------------ noise (tileable)

        public static float Hash(int x, int y, int seed)
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

        public static float Noise(float u, float v, int px, int py, int seed)
        {
            float x = u * px, y = v * py;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(Wrap(ix, px), Wrap(iy, py), seed);
            float b = Hash(Wrap(ix + 1, px), Wrap(iy, py), seed);
            float c = Hash(Wrap(ix, px), Wrap(iy + 1, py), seed);
            float d = Hash(Wrap(ix + 1, px), Wrap(iy + 1, py), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Fbm(float u, float v, int px, int py, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(u, v, px << o, py << o, seed + o * 17) * amp;
                norm += amp;
                amp *= 0.5f;
            }

            return sum / norm;
        }

        // ------------------------------------------------------------------ pixel functions

        // Warm planks: grain rings bent by noise, soft seams. Contrast kept low because cubes stretch it.
        static Color WoodPixel(float u, float v, Color light, Color dark, int seed)
        {
            const int planks = 4;
            float pv = v * planks;
            int plank = Mathf.FloorToInt(pv);
            float lv = pv - plank;
            Color baseColour = Color.Lerp(light, dark, Hash(plank, 3, seed));
            float warp = Fbm(u, v, 2, planks * 20, 4, seed);
            float rings = 0.5f + 0.5f * Mathf.Sin((warp * 9f + lv * 5f + Hash(plank, 9, seed) * 6f) * Mathf.PI);
            float fine = Noise(u, v, 4, planks * 90, seed + 5);
            float tone = 0.88f + 0.09f * rings + 0.07f * (fine - 0.5f);
            Color c = baseColour * tone;
            float seam = Mathf.Min(lv, 1f - lv) * TexSize / planks;
            if (seam < 1.5f) c *= 0.62f;
            float joint = Mathf.Repeat(u - Hash(plank, 7, seed), 1f);
            if (joint < 0.004f || joint > 0.996f) c *= 0.7f;
            c.a = 1f;
            return c;
        }

        // Mottled dark meadow: big soft patches, fine grain, and short vertical blade streaks.
        static Color GrassPixel(float u, float v, Color deep, Color light, int seed)
        {
            float patch = Fbm(u, v, 5, 5, 4, seed);
            float blades = Noise(u, v, 120, 14, seed + 31);
            float blades2 = Noise(u, v, 70, 9, seed + 57);
            float grit = Noise(u, v, 256, 256, seed + 71) - 0.5f;
            float t = Mathf.Clamp01(patch * 0.8f + (blades - 0.5f) * 0.45f + (blades2 - 0.5f) * 0.3f + grit * 0.1f);
            Color c = Color.Lerp(deep, light, t);
            // The odd dry, yellower tuft, kept subtle so it never reads as a racer colour.
            float tuft = Noise(u, v, 24, 24, seed + 101);
            if (tuft > 0.78f) c = Color.Lerp(c, new Color(0.36f, 0.38f, 0.18f), (tuft - 0.78f) * 2.2f);
            c.a = 1f;
            return c;
        }

        // Grey-green speckled stone with a hint of moss.
        static Color StonePixel(float u, float v)
        {
            int x = Mathf.FloorToInt(u * TexSize), y = Mathf.FloorToInt(v * TexSize);
            float low = Fbm(u, v, 7, 7, 4, 61);
            float speck = Hash(x, y, 13) - 0.5f;
            float g = 0.36f + 0.10f * (low - 0.5f) + 0.07f * speck;
            Color stone = new Color(g * 0.95f, g, g * 0.93f, 1f);
            float moss = Mathf.Clamp01((Fbm(u, v, 9, 9, 3, 88) - 0.5f) * 3f);
            Color mossColour = new Color(0.20f, 0.31f, 0.15f, 1f);
            Color c = Color.Lerp(stone, mossColour, moss * 0.55f);
            if (Hash(x, y, 77) > 0.985f) c *= 1.3f;
            c.a = 1f;
            return c;
        }
    }
}
