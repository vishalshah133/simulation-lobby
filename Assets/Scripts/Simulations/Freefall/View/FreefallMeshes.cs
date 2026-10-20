using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall
{
    /// <summary>
    /// Procedural meshes for the <c>fall</c> view: the six pad shapes, a sphere, a torus, a flat ring, a crystal
    /// shard and a unit quad. Nothing here is random, so every run draws identical geometry. Meshes are cached
    /// for the life of the domain and shared by every renderer that uses them.
    /// </summary>
    internal static class FreefallMeshes
    {
        /// <summary>Half the pad thickness in unit space (a pad of radius R is 2 * 0.11 * R thick).</summary>
        public const float PadHalfThickness = 0.11f;

        static readonly Dictionary<PadShape, Mesh> Pads = new Dictionary<PadShape, Mesh>();
        static Mesh _sphere;
        static Mesh _quad;
        static Mesh _shard;
        static readonly Dictionary<int, Mesh> Tori = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Mesh> Rings = new Dictionary<int, Mesh>();

        // ---------------------------------------------------------------- builder

        sealed class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();

            /// <summary>Flat-shaded triangle; the winding is flipped if needed so its face points along <paramref name="hint"/>.</summary>
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 hint)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(normal, hint) < 0f)
                {
                    Vector3 swap = b;
                    b = c;
                    c = swap;
                    normal = -normal;
                }

                if (normal.sqrMagnitude < 1e-12f)
                {
                    return;
                }

                normal.Normalize();
                int i = v.Count;
                v.Add(a);
                v.Add(b);
                v.Add(c);
                n.Add(normal);
                n.Add(normal);
                n.Add(normal);
                uv.Add(PlanarUv(a));
                uv.Add(PlanarUv(b));
                uv.Add(PlanarUv(c));
                t.Add(i);
                t.Add(i + 1);
                t.Add(i + 2);
            }

            static Vector2 PlanarUv(Vector3 p)
            {
                return new Vector2(p.x * 0.5f + 0.5f, p.z * 0.5f + 0.5f);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.hideFlags = HideFlags.HideAndDontSave;
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // ---------------------------------------------------------------- pads

        /// <summary>A pad of unit radius, lying in XZ with its face along +Y, centred on the origin.</summary>
        public static Mesh Pad(PadShape shape)
        {
            if (Pads.TryGetValue(shape, out Mesh mesh) && mesh != null)
            {
                return mesh;
            }

            mesh = BuildPad(shape);
            Pads[shape] = mesh;
            return mesh;
        }

        static Mesh BuildPad(PadShape shape)
        {
            var b = new Builder();
            float h = PadHalfThickness;
            switch (shape)
            {
                case PadShape.Disc:
                    FanPad(b, Circle(48, 1f, 0f, Mathf.PI * 2f, false), true, h, 0.08f);
                    break;
                case PadShape.Lily:
                    FanPad(b, Circle(46, 1f, 22f * Mathf.Deg2Rad, (360f - 22f) * Mathf.Deg2Rad, true), false, h, -0.07f);
                    break;
                case PadShape.Petal:
                    FanPad(b, Petal(36), true, h, 0.07f);
                    break;
                case PadShape.Crystal:
                    FanPad(b, Circle(6, 1f, Mathf.PI / 6f, Mathf.PI * 2f + Mathf.PI / 6f, false), true, h, 0.26f);
                    break;
                case PadShape.Hex:
                    FanPad(b, Circle(6, 1f, 0f, Mathf.PI * 2f, false), true, h, 0.0f);
                    break;
                case PadShape.Crescent:
                    CrescentPad(b, 44, h);
                    break;
            }

            return b.ToMesh("FallPad_" + shape);
        }

        static List<Vector2> Circle(int segments, float radius, float a0, float a1, bool inclusiveEnd)
        {
            var points = new List<Vector2>(segments + 1);
            int count = inclusiveEnd ? segments + 1 : segments;
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)segments);
                points.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }

            return points;
        }

        static List<Vector2> Petal(int segments)
        {
            // A leaf: pointed at both ends along z, plump in the middle. Counter-clockwise in (x, z).
            var points = new List<Vector2>(segments);
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float z = Mathf.Cos(a);
                float x = 0.58f * Mathf.Sin(a) * (1f - 0.28f * z);
                points.Add(new Vector2(x, z));
            }

            // Reversed so the loop runs counter-clockwise when viewed from +Y (x right, z up).
            points.Reverse();
            return points;
        }

        static float TopY(Vector2 p, float h, float lift)
        {
            return h + lift * Mathf.Clamp01(1f - p.magnitude);
        }

        /// <summary>
        /// A prism whose top is a fan from the centre over <paramref name="outline"/>. A closed outline wraps; an
        /// open one (the lily's notch) adds the two radial walls. Counter-clockwise outlines only.
        /// </summary>
        static void FanPad(Builder b, List<Vector2> outline, bool closed, float h, float lift)
        {
            int n = outline.Count;
            var centreTop = new Vector3(0f, h + lift, 0f);
            var centreBottom = new Vector3(0f, -h, 0f);
            int spans = closed ? n : n - 1;
            for (int i = 0; i < spans; i++)
            {
                Vector2 p = outline[i];
                Vector2 q = outline[(i + 1) % n];
                var pt = new Vector3(p.x, TopY(p, h, lift), p.y);
                var qt = new Vector3(q.x, TopY(q, h, lift), q.y);
                var pb = new Vector3(p.x, -h, p.y);
                var qb = new Vector3(q.x, -h, q.y);
                b.Tri(centreTop, pt, qt, Vector3.up);
                b.Tri(centreBottom, pb, qb, Vector3.down);
                Wall(b, p, q, h, lift);
            }

            if (!closed)
            {
                // Notch walls: centre -> first point and last point -> centre keep the loop counter-clockwise.
                Wall(b, Vector2.zero, outline[0], h, lift);
                Wall(b, outline[n - 1], Vector2.zero, h, lift);
            }
        }

        static void Wall(Builder b, Vector2 p, Vector2 q, float h, float lift)
        {
            var pt = new Vector3(p.x, TopY(p, h, lift), p.y);
            var qt = new Vector3(q.x, TopY(q, h, lift), q.y);
            var pb = new Vector3(p.x, -h, p.y);
            var qb = new Vector3(q.x, -h, q.y);
            Vector2 d = q - p;
            // Material on the left of the walking direction (counter-clockwise outer loops), so outward is to the right.
            var outward = new Vector3(d.y, 0f, -d.x);
            b.Tri(pt, qt, qb, outward);
            b.Tri(pt, qb, pb, outward);
        }

        static void CrescentPad(Builder b, int segments, float h)
        {
            // Outer: the unit circle. Inner (the bite): a circle offset toward +x, leaving a thin silver crescent
            // that is widest at -x and thinnest at +x.
            const float cx = 0.17f;
            const float rho = 0.8f;
            var outer = new Vector2[segments];
            var inner = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                outer[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                inner[i] = new Vector2(cx + rho * Mathf.Cos(a), rho * Mathf.Sin(a));
            }

            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                var ot = new Vector3(outer[i].x, h, outer[i].y);
                var ot2 = new Vector3(outer[j].x, h, outer[j].y);
                var it = new Vector3(inner[i].x, h, inner[i].y);
                var it2 = new Vector3(inner[j].x, h, inner[j].y);
                b.Tri(ot, ot2, it2, Vector3.up);
                b.Tri(ot, it2, it, Vector3.up);
                b.Tri(new Vector3(ot.x, -h, ot.z), new Vector3(ot2.x, -h, ot2.z), new Vector3(it2.x, -h, it2.z), Vector3.down);
                b.Tri(new Vector3(ot.x, -h, ot.z), new Vector3(it2.x, -h, it2.z), new Vector3(it.x, -h, it.z), Vector3.down);
                Wall(b, outer[i], outer[j], h, 0f);
                // Hole wall: walked clockwise so the material stays on the left.
                Wall(b, inner[j], inner[i], h, 0f);
            }
        }

        // ---------------------------------------------------------------- smooth shapes

        public static Mesh Sphere()
        {
            if (_sphere != null)
            {
                return _sphere;
            }

            const int lon = 28;
            const int lat = 16;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int y = 0; y <= lat; y++)
            {
                float phi = y / (float)lat * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float theta = x / (float)lon * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)) * 0.5f);
                    uvs.Add(new Vector2(x / (float)lon, 1f - y / (float)lat));
                }
            }

            var tris = new List<int>();
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int a = y * (lon + 1) + x;
                    int c = a + lon + 1;
                    tris.Add(a);
                    tris.Add(a + 1);
                    tris.Add(c);
                    tris.Add(a + 1);
                    tris.Add(c + 1);
                    tris.Add(c);
                }
            }

            var normals = new List<Vector3>(verts.Count);
            for (int i = 0; i < verts.Count; i++)
            {
                normals.Add(verts[i].normalized);
            }

            OrientOutward(verts, tris, normals);
            _sphere = MakeSmooth("FallSphere", verts, normals, uvs, tris);
            return _sphere;
        }

        /// <summary>Torus around Y with major radius 1 and the given minor radius.</summary>
        public static Mesh Torus(float minor)
        {
            int key = Mathf.RoundToInt(minor * 10000f);
            if (Tori.TryGetValue(key, out Mesh cached) && cached != null)
            {
                return cached;
            }

            const int segs = 72;
            const int sides = 6;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int i = 0; i <= segs; i++)
            {
                float theta = i / (float)segs * Mathf.PI * 2f;
                float ct = Mathf.Cos(theta);
                float st = Mathf.Sin(theta);
                for (int j = 0; j <= sides; j++)
                {
                    float phi = j / (float)sides * Mathf.PI * 2f;
                    float cp = Mathf.Cos(phi);
                    float sp = Mathf.Sin(phi);
                    verts.Add(new Vector3((1f + minor * cp) * ct, minor * sp, (1f + minor * cp) * st));
                    normals.Add(new Vector3(cp * ct, sp, cp * st));
                    uvs.Add(new Vector2(i / (float)segs, j / (float)sides));
                }
            }

            var tris = new List<int>();
            for (int i = 0; i < segs; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j;
                    int c = a + sides + 1;
                    tris.Add(a);
                    tris.Add(a + 1);
                    tris.Add(c);
                    tris.Add(a + 1);
                    tris.Add(c + 1);
                    tris.Add(c);
                }
            }

            OrientOutward(verts, tris, normals);
            Mesh mesh = MakeSmooth("FallTorus", verts, normals, uvs, tris);
            Tori[key] = mesh;
            return mesh;
        }

        /// <summary>A flat annulus in XZ, outer radius 1, inner radius <paramref name="inner"/>, face along +Y.</summary>
        public static Mesh FlatRing(float inner)
        {
            int key = Mathf.RoundToInt(inner * 1000f);
            if (Rings.TryGetValue(key, out Mesh cached) && cached != null)
            {
                return cached;
            }

            const int segs = 64;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int i = 0; i <= segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);
                verts.Add(new Vector3(c, 0f, s));
                verts.Add(new Vector3(c * inner, 0f, s * inner));
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(i / (float)segs, 1f));
                uvs.Add(new Vector2(i / (float)segs, 0f));
            }

            var tris = new List<int>();
            for (int i = 0; i < segs; i++)
            {
                int a = i * 2;
                tris.Add(a);
                tris.Add(a + 2);
                tris.Add(a + 1);
                tris.Add(a + 1);
                tris.Add(a + 2);
                tris.Add(a + 3);
            }

            Mesh mesh = MakeSmooth("FallFlatRing", verts, normals, uvs, tris);
            Rings[key] = mesh;
            return mesh;
        }

        /// <summary>A unit quad in XY (z = 0), for billboards. Used with double-sided materials.</summary>
        public static Mesh Quad()
        {
            if (_quad != null)
            {
                return _quad;
            }

            var verts = new List<Vector3>
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            var normals = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var uvs = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            var tris = new List<int> { 0, 2, 1, 0, 3, 2 };
            _quad = MakeSmooth("FallQuad", verts, normals, uvs, tris);
            return _quad;
        }

        /// <summary>A hexagonal bipyramid, 1 long along +Y from the base ring, with a short blunt tail. Flat shaded.</summary>
        public static Mesh Shard()
        {
            if (_shard != null)
            {
                return _shard;
            }

            var b = new Builder();
            var tip = new Vector3(0f, 1.7f, 0f);
            var tail = new Vector3(0f, -0.45f, 0f);
            var ring = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                ring[i] = new Vector3(Mathf.Cos(a) * 0.32f, 0f, Mathf.Sin(a) * 0.32f);
            }

            for (int i = 0; i < 6; i++)
            {
                Vector3 p = ring[i];
                Vector3 q = ring[(i + 1) % 6];
                Vector3 outward = (p + q) * 0.5f;
                b.Tri(tip, p, q, outward + Vector3.up * 0.4f);
                b.Tri(tail, p, q, outward - Vector3.up * 0.4f);
            }

            _shard = b.ToMesh("FallShard");
            return _shard;
        }

        // ---------------------------------------------------------------- helpers

        static void OrientOutward(List<Vector3> verts, List<int> tris, List<Vector3> normals)
        {
            // Unity front faces are clockwise from outside: flip every triangle if the first one faces against its normal.
            Vector3 a = verts[tris[0]];
            Vector3 b = verts[tris[1]];
            Vector3 c = verts[tris[2]];
            Vector3 face = Vector3.Cross(b - a, c - a);
            Vector3 hint = normals[tris[0]] + normals[tris[1]] + normals[tris[2]];
            if (Vector3.Dot(face, hint) >= 0f)
            {
                return;
            }

            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                int swap = tris[i + 1];
                tris[i + 1] = tris[i + 2];
                tris[i + 2] = swap;
            }
        }

        static Mesh MakeSmooth(string name, List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
