using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.Survival.EditorTools
{
    /// <summary>
    /// Generated meshes and textures for Lava Run: the faceted volcano heightfield, the island's lipped rings, and the
    /// tileable basalt / lava images. Everything is made from code so there is nothing to import or license.
    /// </summary>
    static class LavaRunGeometry
    {
        public const string ArtFolder = "Assets/Art/Generated";

        // ------------------------------------------------------------------ meshes

        /// <summary>
        /// A flat-shaded square heightfield centred on the origin. Cells whose four corners are all below
        /// <paramref name="skipBelow"/> are left out (they would sit under the lava lake anyway).
        /// </summary>
        public static Mesh BuildHeightfield(string name, float halfExtent, float cell, Func<float, float, float> height,
            float skipBelow, float uvScale)
        {
            int n = Mathf.CeilToInt(halfExtent * 2f / cell);
            var heights = new float[n + 1, n + 1];
            float origin = -n * cell * 0.5f;
            for (int i = 0; i <= n; i++)
            {
                for (int j = 0; j <= n; j++)
                {
                    heights[i, j] = height(origin + i * cell, origin + j * cell);
                }
            }

            var vertices = new List<Vector3>(n * n * 6);
            var normals = new List<Vector3>(n * n * 6);
            var uvs = new List<Vector2>(n * n * 6);
            var triangles = new List<int>(n * n * 6);

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float h00 = heights[i, j];
                    float h10 = heights[i + 1, j];
                    float h01 = heights[i, j + 1];
                    float h11 = heights[i + 1, j + 1];
                    if (Mathf.Max(Mathf.Max(h00, h10), Mathf.Max(h01, h11)) < skipBelow)
                    {
                        continue;
                    }

                    var p00 = new Vector3(origin + i * cell, h00, origin + j * cell);
                    var p10 = new Vector3(origin + (i + 1) * cell, h10, origin + j * cell);
                    var p01 = new Vector3(origin + i * cell, h01, origin + (j + 1) * cell);
                    var p11 = new Vector3(origin + (i + 1) * cell, h11, origin + (j + 1) * cell);

                    // Alternate the diagonal so the facets do not all lean one way.
                    if ((i + j) % 2 == 0)
                    {
                        AddTriangle(vertices, normals, uvs, triangles, p00, p01, p11, uvScale);
                        AddTriangle(vertices, normals, uvs, triangles, p00, p11, p10, uvScale);
                    }
                    else
                    {
                        AddTriangle(vertices, normals, uvs, triangles, p00, p01, p10, uvScale);
                        AddTriangle(vertices, normals, uvs, triangles, p10, p01, p11, uvScale);
                    }
                }
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddTriangle(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, float uvScale)
        {
            // Unity front faces are clockwise from the viewer; this winding faces up for the (x, z) grid order used above.
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.y < 0f)
            {
                Vector3 swap = b;
                b = c;
                c = swap;
                normal = -normal;
            }

            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            uvs.Add(new Vector2(a.x, a.z) * uvScale);
            uvs.Add(new Vector2(b.x, b.z) * uvScale);
            uvs.Add(new Vector2(c.x, c.z) * uvScale);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        /// <summary>
        /// A solid ring (or a disc when <paramref name="innerRadius"/> is 0) revolved from a lipped profile, top face at
        /// local y = 0 and the underside <paramref name="thickness"/> below. Submesh 0 is the body, submesh 1 the lip.
        /// </summary>
        public static Mesh BuildRing(string name, float innerRadius, float outerRadius, float thickness, float lipHeight,
            float lipWidth, int segments)
        {
            var profile = new List<Vector2>
            {
                new Vector2(innerRadius, -thickness),
                new Vector2(outerRadius, -thickness)
            };
            // Edge index -> submesh: edges are profile[i] -> profile[i + 1].
            var edgeSubmesh = new List<int> { 0 };
            if (lipHeight > 0.001f)
            {
                profile.Add(new Vector2(outerRadius, lipHeight));
                profile.Add(new Vector2(outerRadius - lipWidth, lipHeight));
                profile.Add(new Vector2(outerRadius - lipWidth, 0f));
                edgeSubmesh.Add(0);
                edgeSubmesh.Add(1);
                edgeSubmesh.Add(1);
                edgeSubmesh.Add(0);
            }
            else
            {
                profile.Add(new Vector2(outerRadius, 0f));
                edgeSubmesh.Add(0);
            }

            profile.Add(new Vector2(innerRadius, 0f));
            edgeSubmesh.Add(0);
            edgeSubmesh.Add(0); // closing edge: inner wall

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var body = new List<int>();
            var lip = new List<int>();

            for (int e = 0; e < profile.Count; e++)
            {
                Vector2 a = profile[e];
                Vector2 b = profile[(e + 1) % profile.Count];
                if (a.x < 1e-4f && b.x < 1e-4f)
                {
                    continue; // the inner wall of a disc collapses to the axis
                }

                Vector2 edge = b - a;
                if (edge.sqrMagnitude < 1e-8f)
                {
                    continue;
                }

                Vector2 normal2 = new Vector2(edge.y, -edge.x).normalized;
                List<int> target = edgeSubmesh[e] == 1 ? lip : body;

                for (int s = 0; s < segments; s++)
                {
                    float t0 = s / (float)segments * Mathf.PI * 2f;
                    float t1 = (s + 1) / (float)segments * Mathf.PI * 2f;
                    Vector3 d0 = new Vector3(Mathf.Cos(t0), 0f, Mathf.Sin(t0));
                    Vector3 d1 = new Vector3(Mathf.Cos(t1), 0f, Mathf.Sin(t1));

                    int start = vertices.Count;
                    vertices.Add(d0 * a.x + Vector3.up * a.y);
                    vertices.Add(d0 * b.x + Vector3.up * b.y);
                    vertices.Add(d1 * b.x + Vector3.up * b.y);
                    vertices.Add(d1 * a.x + Vector3.up * a.y);
                    normals.Add(d0 * normal2.x + Vector3.up * normal2.y);
                    normals.Add(d0 * normal2.x + Vector3.up * normal2.y);
                    normals.Add(d1 * normal2.x + Vector3.up * normal2.y);
                    normals.Add(d1 * normal2.x + Vector3.up * normal2.y);
                    uvs.Add(new Vector2(a.x, t0));
                    uvs.Add(new Vector2(b.x, t0));
                    uvs.Add(new Vector2(b.x, t1));
                    uvs.Add(new Vector2(a.x, t1));
                    AddOutwardQuad(target, vertices, normals[start], start, start + 1, start + 2, start + 3);
                }
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(lip, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddOutwardQuad(List<int> triangles, List<Vector3> vertices, Vector3 outward, int a, int b, int c, int d)
        {
            Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (face.sqrMagnitude < 1e-12f)
            {
                face = Vector3.Cross(vertices[c] - vertices[a], vertices[d] - vertices[a]);
            }

            if (Vector3.Dot(face, outward) >= 0f)
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }
            else
            {
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(a); triangles.Add(d); triangles.Add(c);
            }
        }

        /// <summary>Saves a mesh as a brand-new asset (delete first), so a changed layout never shows a stale mesh.</summary>
        public static Mesh SaveMeshFresh(Mesh mesh, string name)
        {
            return SimulationLobby.Shared.EditorTools.TrackMeshAssets.SaveMeshFresh(mesh, name, LavaRunMaterials.MeshFolder);
        }

        // ------------------------------------------------------------------ textures

        public static Texture2D EnsureBasaltAlbedo()
        {
            return EnsureTexture("LavaRun_BasaltAlbedo", 256, true, (u, v) =>
            {
                float n = FractalNoise(u, v, 6, 3);
                float speckle = FractalNoise(u + 0.37f, v + 0.11f, 24, 2);
                float edge = CellEdge(u, v, 7, 11);
                float crack = 1f - Mathf.SmoothStep(0f, 0.12f, edge);
                float shade = 0.03f + 0.035f * n + 0.02f * speckle;
                shade *= 1f - 0.7f * crack;
                return new Color(shade * 1.05f, shade, shade * 1.1f, 1f);
            });
        }

        public static Texture2D EnsureBasaltEmission()
        {
            return EnsureTexture("LavaRun_BasaltEmission", 256, true, (u, v) =>
            {
                float edge = CellEdge(u, v, 7, 11);
                float crack = 1f - Mathf.SmoothStep(0f, 0.09f, edge);
                // Only some cracks glow, in broken veins, so the cone is not a uniform net.
                float mask = Mathf.SmoothStep(0.38f, 0.7f, FractalNoise(u + 0.5f, v + 0.25f, 4, 3));
                float glow = crack * mask;
                float hot = Mathf.SmoothStep(0.55f, 1f, glow);
                return new Color(glow * 1f, glow * (0.28f + 0.5f * hot), glow * (0.03f + 0.12f * hot), 1f);
            });
        }

        public static Texture2D EnsureLavaNoise()
        {
            return EnsureTexture("LavaRun_LavaNoise", 512, true, (u, v) =>
            {
                // Domain-warped fractal noise for the flow, molten veins along the cell edges, dark crust in the cells.
                float wu = u + 0.06f * FractalNoise(u + 0.3f, v + 0.7f, 5, 3);
                float wv = v + 0.06f * FractalNoise(u + 0.9f, v + 0.2f, 5, 3);
                float flow = FractalNoise(wu, wv, 6, 4);
                float edge = CellEdge(wu, wv, 6, 5);
                float vein = 1f - Mathf.SmoothStep(0f, 0.22f, edge);

                Color crust = Color.Lerp(new Color(0.20f, 0.012f, 0f), new Color(0.78f, 0.10f, 0.01f), Mathf.Clamp01(flow * 1.4f));
                Color molten = Color.Lerp(new Color(1f, 0.38f, 0.03f), new Color(1f, 0.88f, 0.34f), Mathf.Clamp01(flow * 1.2f + vein * 0.4f));
                return Color.Lerp(crust, molten, Mathf.Clamp01(vein * 1.15f));
            });
        }

        public static Texture2D EnsureEmberDot()
        {
            string path = $"{ArtFolder}/LavaRun_EmberDot_64.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(ArtFolder);
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Writes a tileable PNG once; an existing file is left alone (and so is any hand-painted replacement).</summary>
        static Texture2D EnsureTexture(string name, int size, bool srgb, Func<float, float, Color> pixel)
        {
            string path = $"{ArtFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(ArtFolder);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = srgb;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ tileable noise

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static int Wrap(int i, int period)
        {
            int m = i % period;
            return m < 0 ? m + period : m;
        }

        static float ValueNoise(float u, float v, int period, int seed)
        {
            float x = u * period;
            float y = v * period;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(Wrap(x0, period), Wrap(y0, period), seed);
            float b = Hash(Wrap(x0 + 1, period), Wrap(y0, period), seed);
            float c = Hash(Wrap(x0, period), Wrap(y0 + 1, period), seed);
            float d = Hash(Wrap(x0 + 1, period), Wrap(y0 + 1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float FractalNoise(float u, float v, int basePeriod, int octaves)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += ValueNoise(u, v, period, 17 + o * 31) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }

            return sum / total;
        }

        /// <summary>F2 - F1 of a tileable Worley field with <paramref name="cells"/> cells across: near zero on the cell edges.</summary>
        static float CellEdge(float u, float v, int cells, int seed)
        {
            float x = u * cells;
            float y = v * cells;
            int cx = Mathf.FloorToInt(x);
            int cy = Mathf.FloorToInt(y);
            float f1 = float.MaxValue;
            float f2 = float.MaxValue;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cx + dx;
                    int gy = cy + dy;
                    int wx = Wrap(gx, cells);
                    int wy = Wrap(gy, cells);
                    float px = gx + Hash(wx, wy, seed);
                    float py = gy + Hash(wx, wy, seed + 101);
                    float distance = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (distance < f1)
                    {
                        f2 = f1;
                        f1 = distance;
                    }
                    else if (distance < f2)
                    {
                        f2 = distance;
                    }
                }
            }

            return f2 - f1;
        }
    }
}
