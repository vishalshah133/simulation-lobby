using System.Collections.Generic;
using SimulationLobby.Simulations.PerfectFit.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.PerfectFit.View
{
    /// <summary>
    /// Builds the two meshes of a round at runtime from its outline: the shape (a prism) and the wall (a disc with
    /// that outline, grown by the gap, cut out of it). Generated each Play, never saved as an asset, so the picture
    /// can't drift from the numbers (the stale-mesh trap in CLAUDE.md).
    /// </summary>
    /// <remarks>
    /// Both are authored around the origin with the outline in XY and the thickness along Z, viewed from -Z (the
    /// camera side). Winding is decided from the wanted normal, never by hand, so a mirrored axis can't flip a face.
    /// </remarks>
    public static class ProfileMeshBuilder
    {
        sealed class MeshData
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();

            /// <summary>Adds one triangle with its own vertices, wound so its geometric normal faces <paramref name="wanted"/>.</summary>
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 wanted, Vector3? na = null, Vector3? nb = null, Vector3? nc = null)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), wanted) < 0f)
                {
                    (b, c) = (c, b);
                    (nb, nc) = (nc, nb);
                }

                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                normals.Add(na ?? wanted);
                normals.Add(nb ?? wanted);
                normals.Add(nc ?? wanted);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }

            /// <summary>A quad a-b-c-d (in order around its edge), with optional per-vertex normals for smooth sides.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 wanted,
                             Vector3? na = null, Vector3? nb = null, Vector3? nc = null, Vector3? nd = null)
            {
                Triangle(a, b, c, wanted, na, nb, nc);
                Triangle(a, c, d, wanted, na, nc, nd);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65000)
                {
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>The shape: <paramref name="outline"/> extruded <paramref name="depth"/> along Z, centred on the origin.</summary>
        public static Mesh BuildPrism(Vector2[] outline, float depth, bool smooth, string name)
        {
            var data = new MeshData();
            float h = depth * 0.5f;
            int n = outline.Length;

            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i];
                Vector2 q = outline[(i + 1) % n];
                data.Triangle(new Vector3(0f, 0f, -h), new Vector3(p.x, p.y, -h), new Vector3(q.x, q.y, -h), Vector3.back);
                data.Triangle(new Vector3(0f, 0f, h), new Vector3(p.x, p.y, h), new Vector3(q.x, q.y, h), Vector3.forward);

                Vector3 edgeNormal = Normal(outline, i, n, false, out Vector3 nA, out Vector3 nB, smooth);
                data.Quad(new Vector3(p.x, p.y, -h), new Vector3(q.x, q.y, -h), new Vector3(q.x, q.y, h), new Vector3(p.x, p.y, h),
                          edgeNormal, nA, nB, nB, nA);
            }

            return data.ToMesh(name);
        }

        /// <summary>
        /// The wall: a disc of <paramref name="outerRadius"/> and <paramref name="thickness"/> with <paramref name="hole"/>
        /// cut out (the hole must already include the gap). Outer vertices share the hole vertices' angles, so the ring
        /// between them is a plain quad strip.
        /// </summary>
        public static Mesh BuildWall(Vector2[] hole, float outerRadius, float thickness, bool smooth, string name)
        {
            var data = new MeshData();
            float h = thickness * 0.5f;
            int n = hole.Length;
            var outer = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 direction = hole[i].normalized;
                outer[i] = direction * outerRadius;
            }

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 hi = hole[i];
                Vector2 hj = hole[j];
                Vector2 oi = outer[i];
                Vector2 oj = outer[j];

                // Front (facing the camera, -Z) and back faces of the ring.
                data.Quad(V(hi, -h), V(hj, -h), V(oj, -h), V(oi, -h), Vector3.back);
                data.Quad(V(hi, h), V(hj, h), V(oj, h), V(oi, h), Vector3.forward);

                // The hole's inside surface faces the axis: the opposite of the outline's outward normal.
                Vector3 edgeNormal = Normal(hole, i, n, true, out Vector3 nA, out Vector3 nB, smooth);
                data.Quad(V(hi, -h), V(hj, -h), V(hj, h), V(hi, h), edgeNormal, nA, nB, nB, nA);

                // The rim faces outward, away from the axis.
                Vector3 rimA = new Vector3(oi.x, oi.y, 0f).normalized;
                Vector3 rimB = new Vector3(oj.x, oj.y, 0f).normalized;
                Vector3 rim = (rimA + rimB).normalized;
                data.Quad(V(oi, -h), V(oj, -h), V(oj, h), V(oi, h), rim, rimA, rimB, rimB, rimA);
            }

            return data.ToMesh(name);
        }

        static Vector3 V(Vector2 p, float z)
        {
            return new Vector3(p.x, p.y, z);
        }

        /// <summary>
        /// The side normal of edge <paramref name="i"/>, flipped for a hole, plus its two vertex normals (averaged with
        /// the neighbouring edges when <paramref name="smooth"/>, else equal to the edge normal so the side stays flat).
        /// </summary>
        static Vector3 Normal(Vector2[] outline, int i, int n, bool inward, out Vector3 vertexA, out Vector3 vertexB, bool smooth)
        {
            float sign = inward ? -1f : 1f;
            Vector2 prev = outline[(i + n - 1) % n];
            Vector2 a = outline[i];
            Vector2 b = outline[(i + 1) % n];
            Vector2 next = outline[(i + 2) % n];

            Vector2 edge = FitProfile.EdgeNormal(a, b);
            Vector3 edgeNormal = sign * new Vector3(edge.x, edge.y, 0f);
            if (!smooth)
            {
                vertexA = edgeNormal;
                vertexB = edgeNormal;
                return edgeNormal;
            }

            Vector2 before = FitProfile.EdgeNormal(prev, a);
            Vector2 after = FitProfile.EdgeNormal(b, next);
            vertexA = sign * new Vector3(before.x + edge.x, before.y + edge.y, 0f).normalized;
            vertexB = sign * new Vector3(edge.x + after.x, edge.y + after.y, 0f).normalized;
            return edgeNormal;
        }
    }
}
