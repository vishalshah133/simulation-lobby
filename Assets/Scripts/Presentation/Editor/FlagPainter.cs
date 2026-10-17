using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Presentation.EditorTools
{
    /// <summary>
    /// Paints flat vector-style artwork (flags, badges) into a pixel buffer with anti-aliased edges (4x4 supersampling
    /// per pixel), and saves it as a PNG texture asset. Coordinates are normalised: (0,0) is bottom-left and (1,1)
    /// top-right of the canvas. Lengths "in height units" are fractions of the canvas HEIGHT, so circles stay round on
    /// a 3:2 canvas. Format-agnostic: callers compose the designs. Later calls paint over earlier ones.
    /// </summary>
    public sealed class FlagPainter
    {
        const int Samples = 4;

        readonly Color[] _pixels;

        public int Width { get; }
        public int Height { get; }

        public FlagPainter(int width, int height, Color background)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            _pixels = new Color[Width * Height];
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = background;
        }

        /// <summary>Axis-aligned rectangle between normalised min and max corners.</summary>
        public void FillRect(Vector2 min, Vector2 max, Color color)
        {
            float x0 = Mathf.Min(min.x, max.x) * Width, x1 = Mathf.Max(min.x, max.x) * Width;
            float y0 = Mathf.Min(min.y, max.y) * Height, y1 = Mathf.Max(min.y, max.y) * Height;
            Paint(x0, y0, x1, y1, color, (x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1);
        }

        /// <summary>Simple polygon (convex or concave, not self-intersecting) through normalised points, even-odd fill.</summary>
        public void FillPolygon(Vector2[] points, Color color)
        {
            if (points == null || points.Length < 3)
            {
                return;
            }

            var px = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++) px[i] = new Vector2(points[i].x * Width, points[i].y * Height);
            FillPolygonPixels(px, color);
        }

        /// <summary>A straight band <paramref name="widthHeightUnits"/> wide between two normalised points (a thick line with square ends, which may run past the canvas).</summary>
        public void FillBand(Vector2 from, Vector2 to, float widthHeightUnits, Color color)
        {
            var a = new Vector2(from.x * Width, from.y * Height);
            var b = new Vector2(to.x * Width, to.y * Height);
            float half = Mathf.Abs(widthHeightUnits) * Height * 0.5f;
            Vector2 ab = b - a;
            float len = ab.magnitude;
            if (len < 1e-4f || half <= 0f)
            {
                return;
            }

            Vector2 dir = ab / len;
            float reach = half * 1.5f;   // bounding box padding covers the square corners
            float minX = Mathf.Min(a.x, b.x) - reach, maxX = Mathf.Max(a.x, b.x) + reach;
            float minY = Mathf.Min(a.y, b.y) - reach, maxY = Mathf.Max(a.y, b.y) + reach;
            Paint(minX, minY, maxX, maxY, color, (x, y) =>
            {
                float rx = x - a.x, ry = y - a.y;
                float along = rx * dir.x + ry * dir.y;
                float across = -rx * dir.y + ry * dir.x;
                return along >= 0f && along <= len && Mathf.Abs(across) <= half;
            });
        }

        /// <summary>Disc, radius in height units.</summary>
        public void FillDisc(Vector2 centre, float radiusHeightUnits, Color color)
        {
            FillRing(centre, 0f, radiusHeightUnits, color);
        }

        /// <summary>Ring between the inner and outer radius (height units).</summary>
        public void FillRing(Vector2 centre, float innerHeightUnits, float outerHeightUnits, Color color)
        {
            float cx = centre.x * Width, cy = centre.y * Height;
            float rIn = Mathf.Max(0f, Mathf.Min(innerHeightUnits, outerHeightUnits)) * Height;
            float rOut = Mathf.Max(innerHeightUnits, outerHeightUnits) * Height;
            if (rOut <= 0f)
            {
                return;
            }

            float in2 = rIn * rIn, out2 = rOut * rOut;
            Paint(cx - rOut, cy - rOut, cx + rOut, cy + rOut, color, (x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                float d2 = dx * dx + dy * dy;
                return d2 <= out2 && d2 >= in2;
            });
        }

        /// <summary><paramref name="count"/> evenly spaced spokes from the centre out to <paramref name="radiusHeightUnits"/>, each tapering from <paramref name="widthHeightUnits"/> at its middle to points at both ends.</summary>
        public void FillSpokes(Vector2 centre, float radiusHeightUnits, int count, float widthHeightUnits, Color color)
        {
            if (count <= 0)
            {
                return;
            }

            var c = new Vector2(centre.x * Width, centre.y * Height);
            float r = radiusHeightUnits * Height;
            float halfW = widthHeightUnits * Height * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float ang = i * Mathf.PI * 2f / count;
                var dir = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang));   // first spoke straight up
                var perp = new Vector2(-dir.y, dir.x);
                Vector2 mid = c + dir * (r * 0.5f);
                FillPolygonPixels(new[] { c, mid + perp * halfW, c + dir * r, mid - perp * halfW }, color);
            }
        }

        /// <summary>A star with <paramref name="points"/> tips; outer/inner radii in height units; rotation 0 puts a tip straight up.</summary>
        public void FillStar(Vector2 centre, float outerHeightUnits, float innerHeightUnits, int points, float rotationDegrees, Color color)
        {
            if (points < 2)
            {
                return;
            }

            var c = new Vector2(centre.x * Width, centre.y * Height);
            float rOut = outerHeightUnits * Height, rIn = innerHeightUnits * Height;
            var poly = new Vector2[points * 2];
            for (int i = 0; i < poly.Length; i++)
            {
                float r = (i & 1) == 0 ? rOut : rIn;
                float ang = (rotationDegrees + i * 180f / points) * Mathf.Deg2Rad;   // counter-clockwise from straight up
                poly[i] = c + new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang)) * r;
            }

            FillPolygonPixels(poly, color);
        }

        /// <summary>The painted pixels as a new Texture2D (RGBA32, bilinear, clamped).</summary>
        public Texture2D ToTexture(string name)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels(_pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>Writes a PNG at <paramref name="assetPath"/> (creating folders), imports it as a non-mipmapped sRGB Default texture (clamp, bilinear), and returns the imported asset.</summary>
        public Texture2D SavePng(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string full = Path.Combine(projectRoot, assetPath);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            Texture2D tex = ToTexture(Path.GetFileNameWithoutExtension(assetPath));
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllBytes(full, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(Width, Height)), 32, 16384);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        void FillPolygonPixels(Vector2[] poly, Color color)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < poly.Length; i++)
            {
                minX = Mathf.Min(minX, poly[i].x); maxX = Mathf.Max(maxX, poly[i].x);
                minY = Mathf.Min(minY, poly[i].y); maxY = Mathf.Max(maxY, poly[i].y);
            }

            int n = poly.Length;
            Paint(minX, minY, maxX, maxY, color, (x, y) =>
            {
                bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    Vector2 a = poly[i], b = poly[j];
                    if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    {
                        inside = !inside;
                    }
                }

                return inside;
            });
        }

        /// <summary>Visits only the pixels in the (pixel-space) bounding box; a pixel's coverage is the fraction of its 4x4 samples that are inside.</summary>
        void Paint(float minX, float minY, float maxX, float maxY, Color color, Func<float, float, bool> inside)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX));
            int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(maxX));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
            int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(maxY));
            const float inv = 1f / (Samples * Samples);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        float py = y + (sy + 0.5f) / Samples;
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            if (inside(x + (sx + 0.5f) / Samples, py)) hits++;
                        }
                    }

                    if (hits > 0)
                    {
                        Blend(y * Width + x, color, hits * inv * color.a);
                    }
                }
            }
        }

        void Blend(int index, Color src, float srcAlpha)
        {
            Color dst = _pixels[index];
            float outA = srcAlpha + dst.a * (1f - srcAlpha);
            if (outA <= 1e-6f)
            {
                return;
            }

            float k = dst.a * (1f - srcAlpha);
            _pixels[index] = new Color(
                (src.r * srcAlpha + dst.r * k) / outA,
                (src.g * srcAlpha + dst.g * k) / outA,
                (src.b * srcAlpha + dst.b * k) / outA,
                outA);
        }
    }
}
