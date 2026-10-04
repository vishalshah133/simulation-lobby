using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Presentation.EditorTools
{
    /// <summary>
    /// Procedural textures for scene builders: a ball, debris shards, a soft dot, a shockwave ring.
    /// Generated rather than imported so a scene has no external art dependency, the same reasoning as
    /// procedural audio. Each is written once to <c>Assets/Art/Generated/</c> and reused after that.
    /// </summary>
    /// <remarks>
    /// <c>esc</c>'s builder still has its own private circle/square generator; it writes the same
    /// <c>Circle_256.png</c> path, so the two agree. Move it onto this class the next time that builder
    /// is touched.
    /// </remarks>
    public static class GeneratedArt
    {
        const string Folder = "Assets/Art/Generated";

        /// <summary>White circle, one world unit across, feathered by a pixel.</summary>
        public static Sprite Circle()
        {
            const int size = 256;
            float radius = size * 0.5f;
            return EnsureSprite("Circle_256.png", size, (x, y) =>
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                return Mathf.Clamp01(Mathf.InverseLerp(radius, radius - 1.5f, distance));
            });
        }

        /// <summary>An irregular triangle — one glass shard. Rotated and stretched per particle, so one is enough.</summary>
        public static Texture2D Shard()
        {
            const int size = 64;
            Vector2 a = new Vector2(0.10f, 0.08f) * size;
            Vector2 b = new Vector2(0.92f, 0.30f) * size;
            Vector2 c = new Vector2(0.38f, 0.95f) * size;
            return EnsureTexture("Shard_64.png", size, (x, y) =>
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                // Signed distance to the nearest edge, positive inside, for a one-pixel antialiased edge.
                float d = Mathf.Min(EdgeDistance(p, a, b), Mathf.Min(EdgeDistance(p, b, c), EdgeDistance(p, c, a)));
                return Mathf.Clamp01(d + 0.5f);
            });
        }

        /// <summary>A long thin needle of glass, slightly bent off-axis.</summary>
        public static Texture2D Sliver()
        {
            const int size = 64;
            return Convex("Sliver_64.png", size, new[]
            {
                new Vector2(0.46f, 0.02f), new Vector2(0.58f, 0.30f), new Vector2(0.56f, 0.98f), new Vector2(0.44f, 0.60f)
            });
        }

        /// <summary>A diamond chip.</summary>
        public static Texture2D Diamond()
        {
            const int size = 64;
            return Convex("Diamond_64.png", size, new[]
            {
                new Vector2(0.5f, 0.04f), new Vector2(0.86f, 0.5f), new Vector2(0.5f, 0.96f), new Vector2(0.14f, 0.5f)
            });
        }

        /// <summary>A curved piece of ring — debris that looks like what it broke off from.</summary>
        public static Texture2D ArcChunk()
        {
            const int size = 64;
            Vector2 centre = new Vector2(0.5f, -0.35f) * size;
            float inner = 0.72f * size;
            float outer = 1.05f * size;
            return EnsureTexture("ArcChunk_64.png", size, (x, y) =>
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - centre;
                float r = p.magnitude;
                float angle = Mathf.Abs(Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg);
                float radial = Mathf.Min(r - inner, outer - r);
                float angular = (24f - angle) * Mathf.Deg2Rad * r;
                return Mathf.Clamp01(Mathf.Min(radial, angular) + 0.5f);
            });
        }

        /// <summary>A four-point sparkle star — the finale's debris.</summary>
        public static Texture2D Star()
        {
            const int size = 64;
            float half = size * 0.5f;
            return EnsureTexture("Star_64.png", size, (x, y) =>
            {
                Vector2 p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float r = p.magnitude;
                float theta = Mathf.Atan2(p.y, p.x);
                float spike = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * theta)), 6f);
                float edge = Mathf.Lerp(0.18f, 0.98f, spike) * half;
                return Mathf.Clamp01(edge - r + 0.5f);
            });
        }

        static Texture2D Convex(string fileName, int size, Vector2[] corners)
        {
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] *= size;
            }

            return EnsureTexture(fileName, size, (x, y) =>
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = float.MaxValue;
                for (int i = 0; i < corners.Length; i++)
                {
                    d = Mathf.Min(d, EdgeDistance(p, corners[i], corners[(i + 1) % corners.Length]));
                }

                return Mathf.Clamp01(d + 0.5f);
            });
        }

        /// <summary>Soft round falloff — sparks and glows.</summary>
        public static Texture2D SoftDot()
        {
            const int size = 64;
            float radius = size * 0.5f;
            return EnsureTexture("SoftDot_64.png", size, (x, y) =>
            {
                float t = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                return Mathf.Clamp01(1f - t) * Mathf.Clamp01(1f - t);
            });
        }

        /// <summary>A thin soft-edged annulus filling the texture — the shockwave.</summary>
        public static Texture2D Ring()
        {
            const int size = 256;
            float radius = size * 0.5f;
            return EnsureTexture("Ring_256.png", size, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                float band = 1f - Mathf.Abs(d - 0.9f) / 0.08f;
                return Mathf.Clamp01(band);
            });
        }

        static float EdgeDistance(Vector2 p, Vector2 from, Vector2 to)
        {
            Vector2 edge = to - from;
            Vector2 normal = new Vector2(-edge.y, edge.x).normalized;
            return Vector2.Dot(p - from, normal);
        }

        static Sprite EnsureSprite(string fileName, int size, Func<int, int, float> alpha)
        {
            string path = $"{Folder}/{fileName}";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            Write(path, size, alpha);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size; // one sprite == one world unit
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture2D EnsureTexture(string fileName, int size, Func<int, int, float> alpha)
        {
            string path = $"{Folder}/{fileName}";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            Write(path, size, alpha);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void Write(string path, int size, Func<int, int, float> alpha)
        {
            Directory.CreateDirectory(Folder);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha(x, y) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
