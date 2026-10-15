using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimulationLobby.Simulations.Race.EditorTools
{
    /// <summary>
    /// Sweeps a <see cref="RaceTrackLayout"/> into geometry: samples the centreline section by section
    /// (position, travel direction, banked frame, width), then extrudes a U-shaped channel along it.
    /// Editor-time only: the scene stores the finished meshes, so nothing here runs in a build.
    /// </summary>
    static class TrackMeshBuilder
    {
        public const float RailHeight = 0.34f;
        public const float RailThickness = 0.09f;
        public const float FloorThickness = 0.12f;

        /// <summary>Distance between rings, in metres. Small enough that a 3.4m helix reads as a curve, not a polygon.</summary>
        const float RingSpacing = 0.25f;

        public struct Sample
        {
            public Vector3 position;
            public Vector3 forward;

            /// <summary>Banked across-track direction.</summary>
            public Vector3 right;

            /// <summary>Banked surface normal.</summary>
            public Vector3 up;

            public float width;

            /// <summary>Metres along the centreline.</summary>
            public float distance;

            public int section;
        }

        // ------------------------------------------------------------------ sampling

        public static List<Sample> SampleCentreline(RaceTrackLayout layout, Vector3 start)
        {
            var samples = new List<Sample>(2048);
            Vector3 position = start;
            float yaw = 0f;
            float width = layout.startWidth;
            float distance = 0f;

            for (int k = 0; k < layout.sections.Length; k++)
            {
                TrackSection section = layout.sections[k];
                float slope = section.slopeDegrees * Mathf.Deg2Rad;
                float widthStart = width;
                float widthTarget = section.widthEnd > 0f ? section.widthEnd : width;

                bool curve = section.kind == TrackSectionKind.Curve && Mathf.Abs(section.arcDegrees) > 0.01f
                             && section.radius > 0.1f;
                float arcRadians = curve ? Mathf.Abs(section.arcDegrees) * Mathf.Deg2Rad : 0f;
                float turnSign = section.arcDegrees >= 0f ? 1f : -1f;

                float horizontalLength = curve ? section.radius * arcRadians : Mathf.Max(0.1f, section.length) * Mathf.Cos(slope);
                float pathLength = horizontalLength / Mathf.Max(0.05f, Mathf.Cos(slope));
                int steps = Mathf.Max(1, Mathf.CeilToInt(pathLength / RingSpacing));
                if (curve)
                {
                    steps = Mathf.Max(steps, Mathf.CeilToInt(Mathf.Abs(section.arcDegrees) / 3f));
                }

                float bankRadians = curve ? turnSign * section.bankDegrees * Mathf.Deg2Rad : 0f;
                float ramp = Mathf.Min(3f, pathLength * 0.3f);

                if (k == 0)
                {
                    samples.Add(MakeSample(position, Heading(yaw, slope), 0f, width, distance, k));
                }

                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float stepHorizontal = horizontalLength / steps;
                    float stepDrop = stepHorizontal * Mathf.Tan(slope);

                    if (curve)
                    {
                        float dTheta = arcRadians / steps;
                        float midYaw = yaw - turnSign * dTheta * 0.5f;
                        float chord = 2f * section.radius * Mathf.Sin(dTheta * 0.5f);
                        Vector3 direction = new Vector3(Mathf.Sin(midYaw), 0f, Mathf.Cos(midYaw));
                        stepDrop = chord * Mathf.Tan(slope);
                        position += direction * chord + Vector3.down * stepDrop;
                        yaw -= turnSign * dTheta;
                        stepHorizontal = chord;
                    }
                    else
                    {
                        Vector3 direction = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                        position += direction * stepHorizontal + Vector3.down * stepDrop;
                    }

                    distance += stepHorizontal / Mathf.Max(0.05f, Mathf.Cos(slope));

                    // Banking eases in and out so every section joins its neighbours flat.
                    float along = t * pathLength;
                    float edge = Mathf.Min(along, pathLength - along);
                    float bankWeight = ramp > 0.01f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / ramp)) : 1f;
                    float currentWidth = Mathf.Lerp(widthStart, widthTarget, Mathf.SmoothStep(0f, 1f, t));

                    samples.Add(MakeSample(position, Heading(yaw, slope), bankRadians * bankWeight, currentWidth,
                        distance, k));
                }

                width = widthTarget;
            }

            return samples;
        }

        static Vector3 Heading(float yaw, float slope)
        {
            return (new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * Mathf.Cos(slope) + Vector3.down * Mathf.Sin(slope))
                .normalized;
        }

        static Sample MakeSample(Vector3 position, Vector3 forward, float bank, float width, float distance, int section)
        {
            Vector3 flatRight = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 flatUp = Vector3.Cross(forward, flatRight);

            // A positive bank lifts the right-hand side (a left turn: the inside is the low side).
            float cos = Mathf.Cos(bank);
            float sin = Mathf.Sin(bank);
            return new Sample
            {
                position = position,
                forward = forward,
                right = flatRight * cos + flatUp * sin,
                up = flatUp * cos - flatRight * sin,
                width = width,
                distance = distance,
                section = section
            };
        }

        /// <summary>The sample nearest <paramref name="distance"/> metres along the path.</summary>
        public static Sample At(List<Sample> samples, float distance)
        {
            int lo = 0;
            int hi = samples.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (samples[mid].distance < distance)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return samples[lo];
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>
        /// The channel: floor and rails in submesh 0 (lacquer), the brass rail caps in submesh 1.
        /// The cross-section is wound so each edge's outward normal is <c>(dy, -dx)</c>.
        /// </summary>
        public static Mesh BuildBody(List<Sample> samples)
        {
            var vertices = new List<Vector3>(samples.Count * 16);
            var normals = new List<Vector3>(samples.Count * 16);
            var uvs = new List<Vector2>(samples.Count * 16);
            var body = new List<int>(samples.Count * 36);
            var trim = new List<int>(samples.Count * 12);

            const int edgeCount = 8;

            for (int i = 0; i < samples.Count; i++)
            {
                Sample s = samples[i];
                Vector2[] profile = Profile(s.width);
                float perimeter = 0f;
                for (int e = 0; e < edgeCount; e++)
                {
                    Vector2 a = profile[e];
                    Vector2 b = profile[(e + 1) % edgeCount];
                    Vector2 edge = b - a;
                    Vector2 normal2 = new Vector2(edge.y, -edge.x).normalized;
                    Vector3 normal = s.right * normal2.x + s.up * normal2.y;

                    vertices.Add(s.position + s.right * a.x + s.up * a.y);
                    vertices.Add(s.position + s.right * b.x + s.up * b.y);
                    normals.Add(normal);
                    normals.Add(normal);
                    uvs.Add(new Vector2(perimeter, s.distance));
                    perimeter += edge.magnitude;
                    uvs.Add(new Vector2(perimeter, s.distance));
                }
            }

            for (int i = 0; i < samples.Count - 1; i++)
            {
                int ringA = i * edgeCount * 2;
                int ringB = (i + 1) * edgeCount * 2;
                for (int e = 0; e < edgeCount; e++)
                {
                    int a = ringA + e * 2;
                    int b = ringA + e * 2 + 1;
                    int c = ringB + e * 2 + 1;
                    int d = ringB + e * 2;
                    bool isTrim = e == 2 || e == 6;
                    AddQuad(isTrim ? trim : body, vertices, normals[a], a, b, c, d);
                }
            }

            var mesh = new Mesh { name = "RaceTrackBody", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(trim, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Two thin brass pinstripes down the floor, visual only (no collider uses this mesh).</summary>
        public static Mesh BuildInlay(List<Sample> samples)
        {
            const float inset = 0.07f;
            const float stripe = 0.03f;
            const float lift = 0.004f;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            for (int i = 0; i < samples.Count; i++)
            {
                Sample s = samples[i];
                float half = s.width * 0.5f;
                foreach (float side in new[] { -1f, 1f })
                {
                    float outer = side * (half - inset);
                    float inner = side * (half - inset - stripe);
                    vertices.Add(s.position + s.right * outer + s.up * lift);
                    vertices.Add(s.position + s.right * inner + s.up * lift);
                    normals.Add(s.up);
                    normals.Add(s.up);
                }
            }

            for (int i = 0; i < samples.Count - 1; i++)
            {
                for (int side = 0; side < 2; side++)
                {
                    int a = i * 4 + side * 2;
                    int b = a + 1;
                    int c = (i + 1) * 4 + side * 2 + 1;
                    int d = (i + 1) * 4 + side * 2;
                    AddQuad(triangles, vertices, normals[a], a, b, c, d);
                }
            }

            var mesh = new Mesh { name = "RaceTrackInlay", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>U-channel, counter-clockwise: outer bottom, outer right, right rail top, inner right, floor, inner left, left rail top, outer left.</summary>
        static Vector2[] Profile(float width)
        {
            float h = width * 0.5f;
            const float t = RailThickness;
            const float rail = RailHeight;
            const float d = FloorThickness;
            return new[]
            {
                new Vector2(-h - t, -d),
                new Vector2(h + t, -d),
                new Vector2(h + t, rail),
                new Vector2(h, rail),
                new Vector2(h, 0f),
                new Vector2(-h, 0f),
                new Vector2(-h, rail),
                new Vector2(-h - t, rail)
            };
        }

        /// <summary>Adds the two triangles of a quad, wound so the front face looks along <paramref name="outward"/>.</summary>
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

        // ------------------------------------------------------------------ checks

        /// <summary>
        /// Rough race time from rolling-sphere physics (5/7 g sin slope, some friction loss). Good enough to
        /// tell a 30 second track from a 90 second one before opening Unity.
        /// </summary>
        public static float EstimateSeconds(List<Sample> samples, float untilDistance)
        {
            float speed = 0f;
            float time = 0f;
            for (int i = 1; i < samples.Count && samples[i].distance <= untilDistance; i++)
            {
                float step = samples[i].distance - samples[i - 1].distance;
                float sinSlope = Mathf.Clamp(-samples[i].forward.y, 0f, 1f);
                float acceleration = 0.714f * 9.81f * sinSlope * 0.85f;
                speed = Mathf.Min(14f, Mathf.Sqrt(speed * speed + 2f * acceleration * step));
                time += step / Mathf.Max(0.6f, speed);
            }

            return time;
        }

        /// <summary>
        /// Warns about places where one stretch of track passes through another: far apart along the
        /// path, close together in space.
        /// </summary>
        public static int CountCrossings(List<Sample> samples, out string firstDescription)
        {
            firstDescription = null;
            int crossings = 0;
            for (int i = 0; i < samples.Count; i += 2)
            {
                for (int j = i + 1; j < samples.Count; j += 2)
                {
                    if (samples[j].distance - samples[i].distance < 8f)
                    {
                        continue;
                    }

                    Vector3 delta = samples[j].position - samples[i].position;
                    float reach = (samples[i].width + samples[j].width) * 0.5f + 0.4f;
                    if (new Vector2(delta.x, delta.z).magnitude < reach && Mathf.Abs(delta.y) < 1.1f)
                    {
                        crossings++;
                        if (firstDescription == null)
                        {
                            firstDescription = $"at {samples[i].distance:0}m and {samples[j].distance:0}m (vertical gap {Mathf.Abs(delta.y):0.00}m)";
                        }
                    }
                }
            }

            return crossings;
        }

        /// <summary>True when something sits within the footprint below this point, so a support column would pass through it.</summary>
        public static bool IsObstructedBelow(List<Sample> samples, int index)
        {
            Sample s = samples[index];
            for (int j = 0; j < samples.Count; j += 2)
            {
                if (Mathf.Abs(samples[j].distance - s.distance) < 4f)
                {
                    continue;
                }

                Vector3 delta = samples[j].position - s.position;
                if (delta.y < -0.3f && new Vector2(delta.x, delta.z).magnitude < s.width * 0.5f + 0.5f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
