using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Procedural, tileable surface textures (veined flesh, matted fur, reptile scales, rust, mossy stone, bone, wood) with height-derived
    /// normal maps, written as PNG assets and applied to the scare materials. Primitives alone read as plastic; a little surface detail is
    /// what makes a lunging face look like skin and a spike wall like old iron. Deterministic (fixed seeds), regenerated on each build.
    /// </summary>
    internal static class SigTextures
    {
        const string Dir = "Assets/Art/Generated/HorrorCoaster/Textures";
        const int Size = 256;

        // ---------------------------------------------------------------- noise

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static float Mod(int a, int n) { return ((a % n) + n) % n; }

        /// <summary>Tileable value noise with <paramref name="period"/> lattice cells across the texture.</summary>
        static float VNoise(float u, float v, int px, int py, int seed)
        {
            float x = u * px;
            float y = v * py;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash((int)Mod(x0, px), (int)Mod(y0, py), seed);
            float b = Hash((int)Mod(x0 + 1, px), (int)Mod(y0, py), seed);
            float c = Hash((int)Mod(x0, px), (int)Mod(y0 + 1, py), seed);
            float d = Hash((int)Mod(x0 + 1, px), (int)Mod(y0 + 1, py), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, int px, int py, int seed, int oct = 4)
        {
            float sum = 0f;
            float amp = 0.5f;
            float norm = 0f;
            for (int i = 0; i < oct; i++)
            {
                sum += amp * VNoise(u, v, px << i, py << i, seed + i * 17);
                norm += amp;
                amp *= 0.5f;
            }

            return sum / norm;
        }

        /// <summary>Tileable Voronoi: returns (distance to nearest point, distance to the second nearest minus the first, cell id 0..1).</summary>
        static Vector3 Voronoi(float u, float v, int cells, int seed)
        {
            float x = u * cells;
            float y = v * cells;
            int cx = Mathf.FloorToInt(x);
            int cy = Mathf.FloorToInt(y);
            float d1 = 9f, d2 = 9f;
            float id = 0f;
            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    int nx = cx + i;
                    int ny = cy + j;
                    int wx = (int)Mod(nx, cells);
                    int wy = (int)Mod(ny, cells);
                    float px = nx + Hash(wx, wy, seed);
                    float py = ny + Hash(wx, wy, seed + 5);
                    float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < d1) { d2 = d1; d1 = d; id = Hash(wx, wy, seed + 9); }
                    else if (d < d2) { d2 = d; }
                }
            }

            return new Vector3(d1, d2 - d1, id);
        }

        // ---------------------------------------------------------------- the textures

        struct Texel { public Color c; public float h; }

        static Texel Flesh(float u, float v)
        {
            float blot = Fbm(u, v, 3, 3, 11);
            float fine = Fbm(u, v, 24, 24, 21, 3);
            float ridged = 1f - Mathf.Abs(2f * Fbm(u, v, 5, 5, 31, 3) - 1f);
            float vein = Mathf.SmoothStep(0.86f, 0.97f, ridged);
            Color baseC = Color.Lerp(new Color(0.78f, 0.80f, 0.74f), new Color(0.55f, 0.52f, 0.58f), blot);
            baseC = Color.Lerp(baseC, new Color(0.36f, 0.24f, 0.36f), Mathf.SmoothStep(0.62f, 0.85f, blot) * 0.55f); // bruising
            baseC *= 0.9f + 0.18f * fine;
            baseC = Color.Lerp(baseC, new Color(0.22f, 0.25f, 0.42f), vein * 0.75f);
            return new Texel { c = baseC, h = 0.5f + 0.3f * fine - 0.25f * vein };
        }

        static Texel Fur(float u, float v)
        {
            float streak = Fbm(u, v, 40, 3, 41, 4);
            float clump = Fbm(u, v, 5, 5, 43, 3);
            float k = Mathf.Clamp01(streak * 0.8f + clump * 0.4f);
            Color c = Color.Lerp(new Color(0.07f, 0.06f, 0.07f), new Color(0.42f, 0.36f, 0.34f), Mathf.Pow(k, 1.6f));
            return new Texel { c = c, h = k };
        }

        static Texel Scales(float u, float v)
        {
            Vector3 vo = Voronoi(u, v, 14, 51);
            float edge = Mathf.SmoothStep(0f, 0.18f, vo.y);
            float cell = vo.z;
            Color top = Color.Lerp(new Color(0.15f, 0.26f, 0.1f), new Color(0.32f, 0.38f, 0.14f), cell);
            Color c = Color.Lerp(new Color(0.02f, 0.04f, 0.02f), top, edge);
            c *= 0.85f + 0.3f * (1f - vo.x * 0.8f);
            return new Texel { c = c, h = edge * (0.8f - 0.25f * vo.x) };
        }

        static Texel Rust(float u, float v)
        {
            float big = Fbm(u, v, 4, 4, 61);
            float fine = Fbm(u, v, 30, 30, 63, 3);
            float scratch = Mathf.SmoothStep(0.8f, 0.95f, Fbm(u, v, 3, 60, 65, 2));
            Color c = Color.Lerp(new Color(0.12f, 0.09f, 0.08f), new Color(0.46f, 0.2f, 0.07f), Mathf.SmoothStep(0.35f, 0.75f, big));
            c *= 0.75f + 0.5f * fine;
            c = Color.Lerp(c, new Color(0.5f, 0.5f, 0.52f), scratch * 0.4f);
            return new Texel { c = c, h = 0.4f + 0.4f * fine + 0.2f * big };
        }

        static Texel Stone(float u, float v)
        {
            float big = Fbm(u, v, 3, 3, 71);
            float fine = Fbm(u, v, 28, 28, 73, 4);
            Vector3 vo = Voronoi(u, v, 6, 75);
            float crack = 1f - Mathf.SmoothStep(0f, 0.05f, vo.y);
            Color c = Color.Lerp(new Color(0.22f, 0.23f, 0.26f), new Color(0.5f, 0.52f, 0.55f), big);
            c = Color.Lerp(c, new Color(0.1f, 0.2f, 0.1f), Mathf.SmoothStep(0.55f, 0.8f, Fbm(u, v, 5, 5, 77)) * 0.5f); // moss
            c *= 0.8f + 0.35f * fine;
            c = Color.Lerp(c, Color.black, crack * 0.85f);
            return new Texel { c = c, h = 0.5f + 0.3f * fine - 0.5f * crack };
        }

        static Texel Bone(float u, float v)
        {
            float big = Fbm(u, v, 4, 4, 81);
            float fine = Fbm(u, v, 36, 36, 83, 3);
            float crack = Mathf.SmoothStep(0.88f, 0.96f, 1f - Mathf.Abs(2f * Fbm(u, v, 6, 6, 85, 3) - 1f));
            Color c = Color.Lerp(new Color(0.82f, 0.77f, 0.62f), new Color(0.55f, 0.46f, 0.32f), big);
            c *= 0.9f + 0.15f * fine;
            c = Color.Lerp(c, new Color(0.2f, 0.14f, 0.1f), crack * 0.7f);
            return new Texel { c = c, h = 0.5f + 0.2f * fine - 0.3f * crack };
        }

        static Texel Wood(float u, float v)
        {
            float grain = Fbm(u, v, 3, 36, 91, 4);
            float knot = Fbm(u, v, 4, 6, 93, 2);
            float k = Mathf.Sin((grain * 9f + knot * 3f) * Mathf.PI);
            Color c = Color.Lerp(new Color(0.12f, 0.07f, 0.05f), new Color(0.32f, 0.2f, 0.12f), 0.5f + 0.5f * k);
            return new Texel { c = c, h = 0.5f + 0.25f * k };
        }

        // ---------------------------------------------------------------- assets

        static readonly System.Collections.Generic.Dictionary<string, Texture2D> Cache = new System.Collections.Generic.Dictionary<string, Texture2D>();

        public static Texture2D Albedo(string name)
        {
            return Make(name, false);
        }

        public static Texture2D NormalMap(string name)
        {
            return Make(name, true);
        }

        static Texture2D Make(string name, bool normal)
        {
            string key = name + (normal ? "_N" : "");
            if (Cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

            Func<float, float, Texel> fn;
            switch (name)
            {
                case "flesh": fn = Flesh; break;
                case "fur": fn = Fur; break;
                case "scales": fn = Scales; break;
                case "rust": fn = Rust; break;
                case "stone": fn = Stone; break;
                case "bone": fn = Bone; break;
                default: fn = Wood; break;
            }

            var colors = new Color[Size * Size];
            var heights = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Texel t = fn((x + 0.5f) / Size, (y + 0.5f) / Size);
                    colors[y * Size + x] = new Color(Mathf.Clamp01(t.c.r), Mathf.Clamp01(t.c.g), Mathf.Clamp01(t.c.b), 1f);
                    heights[y * Size + x] = t.h;
                }
            }

            if (normal)
            {
                float strength = 3.2f;
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float l = heights[y * Size + (int)Mod(x - 1, Size)];
                        float r = heights[y * Size + (int)Mod(x + 1, Size)];
                        float d = heights[(int)Mod(y - 1, Size) * Size + x];
                        float u = heights[(int)Mod(y + 1, Size) * Size + x];
                        var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                        colors[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                    }
                }
            }

            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/Sig_{name}{(normal ? "_N" : "")}.png";
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels(colors);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
            }

            Texture2D loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Cache[key] = loaded;
            return loaded;
        }

        /// <summary>Puts the albedo (and a normal map) on a material. The material colour stays as a tint.</summary>
        public static Material Apply(Material m, string texture, float tiling = 1f, float bump = 1f)
        {
            if (m == null) return m;
            m.SetTexture("_BaseMap", Albedo(texture));
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            if (bump > 0f)
            {
                m.SetTexture("_BumpMap", NormalMap(texture));
                m.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
                m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
