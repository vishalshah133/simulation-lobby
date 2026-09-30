using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>A closed, welded triangle surface — the input a <see cref="SoftBodySolver"/> is built from.</summary>
    public sealed class SoftBodyShape
    {
        public Vector3[] positions;
        public int[] triangles;
    }

    /// <summary>Generators for soft-body source shapes. Welded (no seam duplicates), outward-facing.</summary>
    public static class SoftBodyShapes
    {
        /// <summary>
        /// A capsule along Y, centred on the origin: two hemispheres of <paramref name="radius"/> joined
        /// by a cylinder of length <c>2 × halfLength</c>, meshed with near-equilateral triangles of edge
        /// ≈ the equator spacing (<c>2πr / segments</c>).
        /// </summary>
        /// <remarks>
        /// Each latitude ring gets a vertex count proportional to its circumference — about 6 at the
        /// poles, <paramref name="segments"/> at the equator — and neighbouring rings are stitched with
        /// a zipper. The obvious UV-sphere layout (the same count on every ring) was tried first and is
        /// unusable for a solver: dozens of needle triangles meet at each pole, the edge network there is
        /// hopelessly stiff, and it rang at over 100 m/s on the first impact.
        /// </remarks>
        public static SoftBodyShape Capsule(float radius, float halfLength, int segments = 40)
        {
            segments = Mathf.Max(8, segments);
            float spacing = 2f * Mathf.PI * radius / segments;
            int hemisphereRings = Mathf.Max(2, Mathf.RoundToInt(Mathf.PI * 0.5f * radius / spacing));
            int cylinderRings = Mathf.Max(1, Mathf.RoundToInt(2f * halfLength / spacing));

            // (ringY, ringRadius) from top to bottom, excluding the poles.
            var rings = new List<Vector2>();
            for (int k = 1; k <= hemisphereRings; k++)
            {
                float phi = Mathf.PI * 0.5f * k / hemisphereRings;
                rings.Add(new Vector2(halfLength + radius * Mathf.Cos(phi), radius * Mathf.Sin(phi)));
            }

            for (int k = 1; k < cylinderRings; k++)
            {
                rings.Add(new Vector2(halfLength - 2f * halfLength * k / cylinderRings, radius));
            }

            if (halfLength > 0f)
            {
                rings.Add(new Vector2(-halfLength, radius));
            }

            for (int k = hemisphereRings - 1; k >= 1; k--)
            {
                float phi = Mathf.PI * 0.5f * k / hemisphereRings;
                rings.Add(new Vector2(-halfLength - radius * Mathf.Cos(phi), radius * Mathf.Sin(phi)));
            }

            var positions = new List<Vector3> { new Vector3(0f, halfLength + radius, 0f) };
            var ringStart = new int[rings.Count];
            var ringCount = new int[rings.Count];
            for (int r = 0; r < rings.Count; r++)
            {
                int count = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * rings[r].y / spacing));
                ringStart[r] = positions.Count;
                ringCount[r] = count;
                // Half-step offset per ring (in each ring's own units) keeps vertices of neighbouring
                // rings staggered rather than stacked, which is what makes the zipper's triangles fat.
                for (int s = 0; s < count; s++)
                {
                    float angle = (s + 0.5f * (r % 2)) * Mathf.PI * 2f / count;
                    positions.Add(new Vector3(Mathf.Cos(angle) * rings[r].y, rings[r].x, Mathf.Sin(angle) * rings[r].y));
                }
            }

            int bottomPole = positions.Count;
            positions.Add(new Vector3(0f, -halfLength - radius, 0f));

            var triangles = new List<int>();
            int first = rings.Count > 0 ? 0 : -1;
            for (int s = 0; s < ringCount[first]; s++)
            {
                triangles.Add(0);
                triangles.Add(ringStart[first] + (s + 1) % ringCount[first]);
                triangles.Add(ringStart[first] + s);
            }

            for (int r = 0; r < rings.Count - 1; r++)
            {
                Zipper(triangles, ringStart[r], ringCount[r], 0.5f * (r % 2),
                    ringStart[r + 1], ringCount[r + 1], 0.5f * ((r + 1) % 2));
            }

            int last = rings.Count - 1;
            for (int s = 0; s < ringCount[last]; s++)
            {
                triangles.Add(bottomPole);
                triangles.Add(ringStart[last] + s);
                triangles.Add(ringStart[last] + (s + 1) % ringCount[last]);
            }

            var shape = new SoftBodyShape { positions = positions.ToArray(), triangles = triangles.ToArray() };
            if (MeshWinding.SignedOutwardness(shape.positions, shape.triangles, Vector3.zero) < 0f)
            {
                for (int i = 0; i < shape.triangles.Length; i += 3)
                {
                    (shape.triangles[i + 1], shape.triangles[i + 2]) = (shape.triangles[i + 2], shape.triangles[i + 1]);
                }
            }

            return shape;
        }

        /// <summary>
        /// Stitches ring A (above) to ring B (below) when they have different vertex counts: walk both
        /// rings in angle order, always advancing whichever ring's next vertex comes first.
        /// </summary>
        static void Zipper(List<int> triangles, int startA, int countA, float offsetA, int startB, int countB, float offsetB)
        {
            int i = 0;
            int j = 0;
            while (i < countA || j < countB)
            {
                float nextA = (i + 1 + offsetA) / countA;
                float nextB = (j + 1 + offsetB) / countB;
                int a = startA + i % countA;
                int b = startB + j % countB;
                if (j >= countB || (i < countA && nextA <= nextB))
                {
                    triangles.Add(a);
                    triangles.Add(startA + (i + 1) % countA);
                    triangles.Add(b);
                    i++;
                }
                else
                {
                    triangles.Add(a);
                    triangles.Add(startB + (j + 1) % countB);
                    triangles.Add(b);
                    j++;
                }
            }
        }
    }
}
