using System.Collections.Generic;
using SimulationLobby.Shared.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.Timer.EditorTools
{
    /// <summary>
    /// Visual-only rail dressing swept along the <see cref="TrackMeshBuilder"/> samples: a slightly oversize wall slab that hides the
    /// body mesh's rails, a round tube along the top edge (a toy rail), and a small rounded lip where floor meets rail.
    /// The body <c>MeshCollider</c> is untouched, so none of this is a collider.
    /// </summary>
    static class TimerRailBuilder
    {
        public const float CapRadius = 0.095f;
        public const int CapSides = 12;
        const float LipRadius = 0.04f;
        const float Skin = 0.012f;

        /// <summary>Rails mesh: submesh 0 is the wall slab and lip, submesh 1 the top tube.</summary>
        public static Mesh BuildRailMesh(List<TrackMeshBuilder.Sample> samples)
        {
            var vertices = new List<Vector3>(samples.Count * 40);
            var normals = new List<Vector3>(samples.Count * 40);
            var wall = new List<int>(samples.Count * 40);
            var cap = new List<int>(samples.Count * 40);

            for (int side = -1; side <= 1; side += 2)
            {
                AddSlab(vertices, normals, wall, samples, side);
                float capLateral = TrackMeshBuilder.RailThickness * 0.5f;
                SweepTube(vertices, normals, cap, samples, s => side * (s.width * 0.5f + capLateral), s => s.railHeight, CapRadius, CapSides);
                SweepTube(vertices, normals, wall, samples, s => side * (s.width * 0.5f - LipRadius * 0.5f), s => LipRadius * 0.7f, LipRadius, 8);
            }

            var mesh = new Mesh { name = "TimerRails", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(wall, 0);
            mesh.SetTriangles(cap, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A closed ring of tube around a vertical axis, for rims and bases. Smooth normals.</summary>
        public static Mesh BuildRing(float ringRadius, float tubeRadius, int segments, int sides, string name)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int k = 0; k < sides; k++)
                {
                    float b = k / (float)sides * Mathf.PI * 2f;
                    Vector3 direction = outward * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    vertices.Add(outward * ringRadius + direction * tubeRadius);
                    normals.Add(direction);
                }
            }

            for (int i = 0; i < segments; i++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a = i * sides + k;
                    int b = i * sides + (k + 1) % sides;
                    int c = (i + 1) * sides + (k + 1) % sides;
                    int d = (i + 1) * sides + k;
                    AddQuad(triangles, vertices, normals[a], a, b, c, d);
                }
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>An open cylinder wall (no caps) about the vertical axis, for the collector glass. Outward normals.</summary>
        public static Mesh BuildCylinderWall(float radius, float height, float bottom, int segments, string name)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(outward * radius + Vector3.up * bottom);
                vertices.Add(outward * radius + Vector3.up * (bottom + height));
                normals.Add(outward);
                normals.Add(outward);
                uvs.Add(new Vector2(i / (float)segments, 0f));
                uvs.Add(new Vector2(i / (float)segments, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                AddQuad(triangles, vertices, normals[a], a, a + 2, a + 3, a + 1);
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The hopper body: a vertical square spout (<paramref name="spoutBottom"/> to <paramref name="spoutTop"/>) under an inverted pyramid
        /// frustum widening to <paramref name="topHalf"/> at <paramref name="top"/>. Local space, centred on the vertical axis, flat-shaded,
        /// with an inner and an outer skin so it reads from above and from the side.
        /// </summary>
        public static Mesh BuildHopperMesh(float spoutHalf, float topHalf, float spoutBottom, float spoutTop, float top)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            AddFrustum(vertices, normals, triangles, spoutHalf, spoutHalf, spoutBottom, spoutTop);
            AddFrustum(vertices, normals, triangles, spoutHalf, topHalf, spoutTop, top);

            var mesh = new Mesh { name = "TimerHopper", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddFrustum(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, float lowHalf, float highHalf, float lowY, float highY)
        {
            for (int side = 0; side < 4; side++)
            {
                Vector3 direction = Quaternion.Euler(0f, side * 90f, 0f) * Vector3.forward;
                Vector3 tangent = Quaternion.Euler(0f, side * 90f, 0f) * Vector3.right;
                Vector3 low = direction * lowHalf + Vector3.up * lowY;
                Vector3 high = direction * highHalf + Vector3.up * highY;
                Vector3 slope = high - low;
                Vector3 normal = Vector3.Cross(tangent, slope).normalized;
                if (Vector3.Dot(normal, direction) < 0f)
                {
                    normal = -normal;
                }

                for (int skin = 0; skin < 2; skin++)
                {
                    Vector3 n = skin == 0 ? normal : -normal;
                    int first = vertices.Count;
                    vertices.Add(low - tangent * lowHalf);
                    vertices.Add(low + tangent * lowHalf);
                    vertices.Add(high + tangent * highHalf);
                    vertices.Add(high - tangent * highHalf);
                    for (int k = 0; k < 4; k++)
                    {
                        normals.Add(n);
                    }

                    AddQuad(triangles, vertices, n, first, first + 1, first + 2, first + 3);
                }
            }
        }

        // ------------------------------------------------------------------ sweeps

        /// <summary>The rail wall: an inner and an outer face, each a flat-shaded strip, a hair outside the body mesh so it never z-fights.</summary>
        static void AddSlab(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, List<TrackMeshBuilder.Sample> samples, int side)
        {
            for (int face = 0; face < 2; face++)
            {
                bool outer = face == 1;
                int first = vertices.Count;
                for (int i = 0; i < samples.Count; i++)
                {
                    TrackMeshBuilder.Sample s = samples[i];
                    float half = s.width * 0.5f;
                    float lateral = outer ? half + TrackMeshBuilder.RailThickness + Skin : half - Skin;
                    float bottom = outer ? -TrackMeshBuilder.FloorThickness - Skin : 0f;
                    float top = s.railHeight - CapRadius * 0.5f;
                    Vector3 normal = s.right * (outer ? side : -side);
                    vertices.Add(s.position + s.right * (side * lateral) + s.up * bottom);
                    vertices.Add(s.position + s.right * (side * lateral) + s.up * top);
                    normals.Add(normal);
                    normals.Add(normal);
                }

                for (int i = 0; i < samples.Count - 1; i++)
                {
                    int a = first + i * 2;
                    AddQuad(triangles, vertices, normals[a], a, a + 1, a + 3, a + 2);
                }
            }
        }

        /// <summary>A round tube along the samples. <paramref name="lateral"/> and <paramref name="height"/> place its centre in each sample's banked frame.</summary>
        static void SweepTube(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, List<TrackMeshBuilder.Sample> samples,
            System.Func<TrackMeshBuilder.Sample, float> lateral, System.Func<TrackMeshBuilder.Sample, float> height, float radius, int sides)
        {
            int first = vertices.Count;
            for (int i = 0; i < samples.Count; i++)
            {
                TrackMeshBuilder.Sample s = samples[i];
                Vector3 centre = s.position + s.right * lateral(s) + s.up * height(s);
                for (int k = 0; k < sides; k++)
                {
                    float b = k / (float)sides * Mathf.PI * 2f;
                    Vector3 direction = s.right * Mathf.Cos(b) + s.up * Mathf.Sin(b);
                    vertices.Add(centre + direction * radius);
                    normals.Add(direction);
                }
            }

            for (int i = 0; i < samples.Count - 1; i++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a = first + i * sides + k;
                    int b = first + i * sides + (k + 1) % sides;
                    int c = first + (i + 1) * sides + (k + 1) % sides;
                    int d = first + (i + 1) * sides + k;
                    AddQuad(triangles, vertices, normals[a], a, b, c, d);
                }
            }
        }

        /// <summary>Two triangles of a quad, wound so the front face looks along <paramref name="outward"/>.</summary>
        static void AddQuad(List<int> triangles, List<Vector3> vertices, Vector3 outward, int a, int b, int c, int d)
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
    }
}
