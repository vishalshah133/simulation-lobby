using System.Collections.Generic;
using SimulationLobby.Shared.Coaster;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.TrippyCoaster.EditorTools
{
    /// <summary>
    /// Builds the geometry of every world along the rail: a wall tunnel (when the world has one), the props that float around the cart,
    /// the neon track and a portal ring where each world begins. Everything is merged into ~100 m chunks (so the renderer culls them by
    /// distance) and every pattern, glow and breathing motion is done in the TripPlasma shader: nothing here moves at runtime.
    /// </summary>
    public static class TripWorldBuilder
    {
        const float ChunkLength = 100f;

        sealed class Batch
        {
            public readonly Dictionary<int, List<CombineInstance>> Chunks = new Dictionary<int, List<CombineInstance>>();

            public void Add(int chunk, Mesh mesh, Matrix4x4 m)
            {
                if (!Chunks.TryGetValue(chunk, out List<CombineInstance> list))
                {
                    list = new List<CombineInstance>();
                    Chunks[chunk] = list;
                }
                list.Add(new CombineInstance { mesh = mesh, transform = m });
            }
        }

        struct Meshes
        {
            public Mesh Torus, Octa, Sphere, Cylinder, Cube;
        }

        public static Material[] Build(Transform root, ICoasterRail rail, TrippyCoasterConfig cfg)
        {
            var meshes = new Meshes
            {
                Torus = TripAssets.Torus(1f, 0.03f, 64, 8),
                Octa = TripAssets.Octahedron(),
                Sphere = TripAssets.Primitive(PrimitiveType.Sphere),
                Cylinder = TripAssets.Primitive(PrimitiveType.Cylinder),
                Cube = TripAssets.Primitive(PrimitiveType.Cube)
            };

            var worlds = new GameObject("Worlds");
            worlds.transform.SetParent(root, false);
            var backdrops = new Material[cfg.worlds.Length];

            for (int i = 0; i < cfg.worlds.Length; i++)
            {
                TripWorldDef w = cfg.worlds[i];
                float a = w.startDistance;
                float b = cfg.WorldEnd(i, rail.Length);
                string tag = "W" + (i + 1);

                var go = new GameObject($"{tag} {w.name}");
                go.transform.SetParent(worlds.transform, false);

                Material wallMat = TripAssets.Trip($"Trip_{tag}_Wall", w, w.pattern, 1f, false, 0f);
                Material propMat = TripAssets.Trip($"Trip_{tag}_Prop", w, TripPattern.Glow, 1.25f, false, w.pulse);
                Material trackMat = TripAssets.Trip($"Trip_{tag}_Track", w, TripPattern.Glow, 1.5f, false, 0f);
                backdrops[i] = TripAssets.Trip($"Trip_{tag}_Backdrop", w, TripPattern.Plasma, 0.35f, true, 0f, 0f);
                backdrops[i].SetFloat("_Scale", 0.05f);

                if (w.wall != TripWallShape.None) BuildWalls(go.transform, rail, w, a, b, wallMat, tag, i == cfg.worlds.Length - 1);
                BuildProps(go.transform, rail, w, a, b, propMat, meshes, tag, i);
                BuildTrack(go.transform, rail, a, b, trackMat, meshes, tag);
            }

            return backdrops;
        }

        // ---------------------------------------------------------------- walls

        static void BuildWalls(Transform parent, ICoasterRail rail, TripWorldDef w, float a, float b, Material mat, string tag, bool last)
        {
            const float step = 2f;
            const int sides = 48;
            int chunks = Mathf.Max(1, Mathf.CeilToInt((b - a) / 150f));
            for (int c = 0; c < chunks; c++)
            {
                float c0 = a + (b - a) * c / chunks;
                float c1 = a + (b - a) * (c + 1) / chunks;
                int rings = Mathf.Max(2, Mathf.CeilToInt((c1 - c0) / step)) + 1;
                var verts = new List<Vector3>(rings * (sides + 1));
                var uvs = new List<Vector2>(rings * (sides + 1));
                var idx = new List<int>();
                for (int r = 0; r < rings; r++)
                {
                    float d = Mathf.Lerp(c0, c1, r / (float)(rings - 1));
                    rail.Sample(d, out Vector3 p, out Vector3 f, out Vector3 u);
                    Vector3 right = Vector3.Cross(u, f).normalized;
                    float s = Mathf.InverseLerp(a, b, d);
                    float flareIn = 1f - Smooth(Mathf.Clamp01((d - a) / 30f));
                    float flareOut = last ? 0f : 1f - Smooth(Mathf.Clamp01((b - d) / 30f));
                    float flare = 1f + 1.4f * Mathf.Max(flareIn, flareOut);
                    for (int j = 0; j <= sides; j++)
                    {
                        float th = j / (float)sides * Mathf.PI * 2f;
                        Vector2 q = WallPoint(w, th, s, d) * flare;
                        verts.Add(p + right * q.x + u * q.y);
                        uvs.Add(new Vector2(j / (float)sides, d));
                    }
                }
                int row = sides + 1;
                for (int r = 0; r < rings - 1; r++)
                {
                    for (int j = 0; j < sides; j++)
                    {
                        int i0 = r * row + j, i1 = i0 + row;
                        idx.AddRange(new[] { i0, i1, i0 + 1, i0 + 1, i1, i1 + 1 });
                    }
                }
                var m = new Mesh { indexFormat = IndexFormat.UInt32 };
                m.SetVertices(verts);
                m.SetUVs(0, uvs);
                m.SetTriangles(idx, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                Emit(parent, TripAssets.SaveMeshFresh(m, $"Trip_{tag}_Wall{c}"), mat, $"Wall {c}");
            }
        }

        static Vector2 WallPoint(TripWorldDef w, float th, float s, float dist)
        {
            float cs = Mathf.Cos(th), sn = Mathf.Sin(th);
            float r;
            switch (w.wall)
            {
                case TripWallShape.Star:
                    r = w.wallRadius + w.wallRadius2 * Mathf.Cos(w.lobes * th);
                    break;
                case TripWallShape.Ripple:
                    r = w.wallRadius + w.wallRadius2 * 0.5f * Mathf.Sin(w.lobes * th + dist * 0.16f);
                    break;
                case TripWallShape.Funnel:
                    r = Mathf.Lerp(w.wallRadius, w.wallRadius2, Smooth(s));
                    break;
                case TripWallShape.Slab:
                    return new Vector2(cs * w.wallRadius * 0.5f, sn * w.wallRadius2);
                default:
                    r = w.wallRadius;
                    break;
            }
            return new Vector2(cs * r, sn * r);
        }

        public static float FunnelRadius(TripWorldDef w, float s)
        {
            return Mathf.Lerp(w.wallRadius, w.wallRadius2, Smooth(s));
        }

        // ---------------------------------------------------------------- props

        static void BuildProps(Transform parent, ICoasterRail rail, TripWorldDef w, float a, float b, Material mat, Meshes m, string tag, int worldIndex)
        {
            var batch = new Batch();
            int n = 0;

            // portal ring where the world begins (skipped for the very first world: it opens the ride)
            if (worldIndex > 0)
            {
                Frame(rail, a + 1f, out Vector3 p, out Quaternion q, out _, out _);
                batch.Add(Chunk(a, a + 1f), m.Torus, Matrix4x4.TRS(p, q, Vector3.one * 15f));
                batch.Add(Chunk(a, a + 1f), m.Torus, Matrix4x4.TRS(p, q, Vector3.one * 12f));
            }

            switch (w.props)
            {
                case TripPropKind.GateRings:
                    for (float d = a + 12f; d < b - 6f; d += 90f, n++)
                    {
                        Frame(rail, d, out Vector3 p, out Quaternion q, out Vector3 up, out Vector3 right);
                        batch.Add(Chunk(a, d), m.Torus, Matrix4x4.TRS(p, q, Vector3.one * 11f));
                        batch.Add(Chunk(a, d), m.Torus, Matrix4x4.TRS(p, q * Quaternion.Euler(0f, 0f, 22f), Vector3.one * 7.5f));
                        float side = n % 2 == 0 ? 1f : -1f;
                        batch.Add(Chunk(a, d), m.Cube, Matrix4x4.TRS(p + right * side * 17f + up * (H(n, 1) * 24f - 8f),
                            q, new Vector3(0.4f, 26f, 0.4f)));
                        batch.Add(Chunk(a, d), m.Sphere, Matrix4x4.TRS(p + right * (H(n, 2) * 50f - 25f) + up * (H(n, 3) * 30f - 6f),
                            q, Vector3.one * (1f + H(n, 4) * 2.5f)));
                    }
                    break;

                case TripPropKind.UvTubes:
                    for (float d = a + 8f; d < b - 4f; d += 50f, n++)
                    {
                        Frame(rail, d, out Vector3 p, out Quaternion q, out Vector3 up, out Vector3 right);
                        for (int k = 0; k < 8; k++)
                        {
                            float th = (k + (n % 2) * 0.5f) / 8f * Mathf.PI * 2f;
                            Vector3 pos = p + (right * Mathf.Cos(th) + up * Mathf.Sin(th)) * (w.wallRadius - 1.3f);
                            batch.Add(Chunk(a, d), m.Cylinder, Matrix4x4.TRS(pos, q * Quaternion.Euler(90f, 0f, 0f), new Vector3(0.3f, 20f, 0.3f)));
                        }
                    }
                    break;

                case TripPropKind.Crystals:
                    for (float d = a + 20f; d < b - 20f; d += 72f, n++)
                    {
                        Frame(rail, d, out Vector3 p, out Quaternion q, out Vector3 up, out Vector3 right);
                        float sProg = Mathf.InverseLerp(a, b, d);
                        for (int k = 0; k < w.lobes; k++)
                        {
                            // alternate stations between the lobe tips and the valleys; sit each crystal on the real wall radius
                            float th = (k + (n % 2) * 0.5f) / w.lobes * Mathf.PI * 2f;
                            Vector3 radial = right * Mathf.Cos(th) + up * Mathf.Sin(th);
                            float wallR = WallPoint(w, th, sProg, d).magnitude;
                            Quaternion rq = Quaternion.LookRotation(q * Vector3.forward, radial);
                            batch.Add(Chunk(a, d), m.Octa, Matrix4x4.TRS(p + radial * (wallR - 2.4f), rq, new Vector3(0.9f, 2.4f, 0.9f)));
                        }
                    }
                    break;

                case TripPropKind.Spheres:
                    for (float d = a + 6f; d < b - 4f; d += 54f, n++)
                    {
                        Frame(rail, d, out Vector3 p, out Quaternion q, out Vector3 up, out Vector3 right);
                        float side = n % 2 == 0 ? 1f : -1f;
                        float size = 1.5f + H(n, 5) * 3.5f;
                        batch.Add(Chunk(a, d), m.Sphere, Matrix4x4.TRS(p + right * side * (8f + H(n, 6) * 22f) + up * (H(n, 7) * 14f - 7f), q, Vector3.one * size));
                        if (n % 3 == 0)
                            batch.Add(Chunk(a, d), m.Torus, Matrix4x4.TRS(p, q * Quaternion.Euler(0f, 0f, H(n, 8) * 90f), new Vector3(13f, 13f, 13f)));
                    }
                    break;

                case TripPropKind.Orbs:
                    for (float d = a + 4f; d < b - 4f; d += 24f, n++)
                    {
                        Frame(rail, d, out Vector3 p, out Quaternion q, out Vector3 up, out Vector3 right);
                        float th = H(n, 9) * Mathf.PI * 2f;
                        float rr = 2.6f + H(n, 10) * 3.2f;
                        float size = 0.35f + H(n, 11) * 0.7f;
                        batch.Add(Chunk(a, d), m.Sphere, Matrix4x4.TRS(p + (right * Mathf.Cos(th) + up * Mathf.Sin(th)) * rr, q, Vector3.one * size));
                    }
                    break;

                case TripPropKind.Fractals:
                    for (float d = a + 10f; d < b - 8f; d += 100f, n++)
                    {
                        Frame(rail, d, out Vector3 p, out Quaternion q, out Vector3 up, out Vector3 right);
                        float side = n % 2 == 0 ? 1f : -1f;
                        Vector3 centre = p + right * side * (12f + H(n, 12) * 12f) + up * (H(n, 13) * 20f - 8f);
                        Fractal(batch, Chunk(a, d), m.Octa, centre, Quaternion.Euler(H(n, 14) * 360f, H(n, 15) * 360f, 0f), 2.4f + H(n, 16) * 1.6f, 2, -1);
                        if (n % 2 == 0)
                            batch.Add(Chunk(a, d), m.Torus, Matrix4x4.TRS(p, q * Quaternion.Euler(0f, 0f, H(n, 17) * 90f), Vector3.one * 17f));
                    }
                    break;

                case TripPropKind.Vortex:
                    for (float d = a + 6f; d < b - 2f; d += 36f, n++)
                    {
                        Frame(rail, d, out Vector3 p, out Quaternion q, out _, out _);
                        float s = Mathf.InverseLerp(a, b, d);
                        float rr = FunnelRadius(w, s) * 0.9f;
                        batch.Add(Chunk(a, d), m.Torus, Matrix4x4.TRS(p, q * Quaternion.Euler(0f, 0f, n * 11f), Vector3.one * rr));
                    }
                    break;
            }

            foreach (KeyValuePair<int, List<CombineInstance>> kv in batch.Chunks)
            {
                Emit(parent, TripAssets.SaveMeshFresh(TripAssets.Combine(kv.Value), $"Trip_{tag}_Props{kv.Key}"), mat, $"Props {kv.Key}");
            }

            int Chunk(float start, float d) { return Mathf.FloorToInt((d - start) / ChunkLength); }
        }

        static void Fractal(Batch batch, int chunk, Mesh octa, Vector3 centre, Quaternion rot, float size, int depth, int skipAxis)
        {
            batch.Add(chunk, octa, Matrix4x4.TRS(centre, rot, Vector3.one * size));
            if (depth == 0) return;
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int k = 0; k < axes.Length; k++)
            {
                if (k == skipAxis) continue;
                int opposite = k ^ 1;
                Fractal(batch, chunk, octa, centre + rot * axes[k] * size * 1.9f, rot, size * 0.42f, depth - 1, opposite);
            }
        }

        // ---------------------------------------------------------------- track

        static void BuildTrack(Transform parent, ICoasterRail rail, float a, float b, Material mat, Meshes m, string tag)
        {
            int chunks = Mathf.Max(1, Mathf.CeilToInt((b - a) / 200f));
            for (int c = 0; c < chunks; c++)
            {
                float c0 = a + (b - a) * c / chunks;
                float c1 = a + (b - a) * (c + 1) / chunks;
                var parts = new List<CombineInstance>();
                parts.Add(new CombineInstance { mesh = Sweep(rail, c0, c1, new[] { new Vector2(-0.67f, -0.35f), new Vector2(-0.57f, -0.35f), new Vector2(-0.57f, -0.45f), new Vector2(-0.67f, -0.45f) }, true, "r"), transform = Matrix4x4.identity });
                parts.Add(new CombineInstance { mesh = Sweep(rail, c0, c1, new[] { new Vector2(0.57f, -0.35f), new Vector2(0.67f, -0.35f), new Vector2(0.67f, -0.45f), new Vector2(0.57f, -0.45f) }, true, "l"), transform = Matrix4x4.identity });
                parts.Add(new CombineInstance { mesh = Sweep(rail, c0, c1, new[] { new Vector2(-0.2f, -0.5f), new Vector2(0.2f, -0.5f) }, false, "s"), transform = Matrix4x4.identity });
                for (float d = c0 + 1f; d < c1; d += 6f)
                {
                    Frame(rail, d, out Vector3 p, out Quaternion q, out Vector3 up, out _);
                    parts.Add(new CombineInstance { mesh = m.Cube, transform = Matrix4x4.TRS(p - up * 0.55f, q, new Vector3(1.55f, 0.08f, 0.22f)) });
                }
                Mesh merged = TripAssets.Combine(parts);
                for (int i = 0; i < 3; i++) Object.DestroyImmediate(parts[i].mesh);
                Emit(parent, TripAssets.SaveMeshFresh(merged, $"Trip_{tag}_Track{c}"), mat, $"Track {c}");
            }
        }

        /// <summary>Extrudes a 2D cross-section (right, up offsets from the rail) along the rail at 1 m spacing.</summary>
        static Mesh Sweep(ICoasterRail rail, float a, float b, Vector2[] section, bool closed, string name)
        {
            int stations = Mathf.Max(2, Mathf.CeilToInt(b - a)) + 1;
            int k = section.Length;
            var verts = new List<Vector3>(stations * k);
            var uvs = new List<Vector2>(stations * k);
            var idx = new List<int>();
            for (int s = 0; s < stations; s++)
            {
                float d = Mathf.Lerp(a, b, s / (float)(stations - 1));
                rail.Sample(d, out Vector3 p, out Vector3 f, out Vector3 u);
                Vector3 right = Vector3.Cross(u, f).normalized;
                for (int j = 0; j < k; j++)
                {
                    verts.Add(p + right * section[j].x + u * section[j].y);
                    uvs.Add(new Vector2(j / (float)k, d));
                }
            }
            int segs = closed ? k : k - 1;
            for (int s = 0; s < stations - 1; s++)
            {
                for (int j = 0; j < segs; j++)
                {
                    int j2 = (j + 1) % k;
                    int i0 = s * k + j, i1 = s * k + j2, i2 = (s + 1) * k + j, i3 = (s + 1) * k + j2;
                    idx.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
                }
            }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "sweep_" + name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        // ---------------------------------------------------------------- helpers

        static void Frame(ICoasterRail rail, float d, out Vector3 p, out Quaternion q, out Vector3 up, out Vector3 right)
        {
            rail.Sample(d, out p, out Vector3 f, out up);
            q = Quaternion.LookRotation(f, up);
            right = Vector3.Cross(up, f).normalized;
        }

        static void Emit(Transform parent, Mesh mesh, Material mat, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>Stable hash in [0,1): the same scene is built every time.</summary>
        static float H(int a, int b)
        {
            unchecked
            {
                uint x = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                x ^= x >> 13;
                x *= 0x5bd1e995u;
                x ^= x >> 15;
                return (x & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
