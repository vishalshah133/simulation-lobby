using UnityEngine;

namespace SimulationLobby.Simulations.PerfectFit.Kernel
{
    /// <summary>The silhouettes a round can use. Every one is star-shaped about the origin, which the meshes rely on.</summary>
    public enum ProfileKind
    {
        Triangle = 0,
        Cross = 1,
        Star5 = 2,
        Gear = 3,
        Flower = 4,
        Shuriken = 5,
        Star12 = 6
    }

    /// <summary>
    /// Pure 2D outline maths for <c>fit</c>: the shape's silhouette, the same silhouette grown by a gap (the hole),
    /// and a measurement of the smallest air between the two. No Unity components, no randomness.
    /// </summary>
    /// <remarks>
    /// Outlines are counter-clockwise, sorted by angle about the origin, and star-shaped about it, so a triangle fan
    /// from the origin fills them and a ring strip to a circle with matching angles leaves a hole of exactly that shape.
    /// </remarks>
    public static class FitProfile
    {
        /// <summary>Kinds whose outline is a dense sampling of a smooth curve, so their sides want smooth normals.</summary>
        public static bool IsSmooth(ProfileKind kind)
        {
            return kind == ProfileKind.Flower || kind == ProfileKind.Shuriken;
        }

        /// <summary>The outline of <paramref name="kind"/> fitting inside a circle of <paramref name="radius"/>.</summary>
        public static Vector2[] Create(ProfileKind kind, float radius)
        {
            switch (kind)
            {
                case ProfileKind.Triangle:
                    return Star(3, 1f, radius); // inner ratio 1 collapses to a regular triangle (see Star)

                case ProfileKind.Cross:
                {
                    float a = radius * 0.92f;
                    float w = radius * 0.30f;
                    return new[]
                    {
                        new Vector2(a, -w), new Vector2(a, w), new Vector2(w, w), new Vector2(w, a),
                        new Vector2(-w, a), new Vector2(-w, w), new Vector2(-a, w), new Vector2(-a, -w),
                        new Vector2(-w, -w), new Vector2(-w, -a), new Vector2(w, -a), new Vector2(w, -w)
                    };
                }

                case ProfileKind.Star5:
                    return Star(5, 0.42f, radius);

                case ProfileKind.Gear:
                    return Gear(12, radius * 0.78f, radius);

                case ProfileKind.Flower:
                    return Polar(360, radius, theta => 0.68f + 0.32f * Mathf.Cos(6f * theta));

                case ProfileKind.Shuriken:
                    return Polar(480, radius, theta =>
                    {
                        float lobe = 0.5f + 0.5f * Mathf.Cos(4f * theta);
                        return 0.26f + 0.74f * Mathf.Pow(lobe, 1.7f);
                    });

                case ProfileKind.Star12:
                    return Star(12, 0.66f, radius);

                default:
                    return Star(5, 0.42f, radius);
            }
        }

        /// <summary>An n-pointed star, one tip straight up. <paramref name="innerRatio"/> of 1 gives a regular n-gon.</summary>
        static Vector2[] Star(int points, float innerRatio, float radius)
        {
            bool polygon = innerRatio >= 0.999f;
            int count = polygon ? points : points * 2;
            var result = new Vector2[count];
            float step = 2f * Mathf.PI / count;
            float start = Mathf.PI * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float r = polygon || i % 2 == 0 ? radius : radius * innerRatio;
                float angle = start + i * step;
                result[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }

            return SortByAngle(result);
        }

        /// <summary>Trapezoid teeth on a root circle: root, tip, tip, root per tooth, with a flat valley between teeth.</summary>
        static Vector2[] Gear(int teeth, float rootRadius, float tipRadius)
        {
            var result = new Vector2[teeth * 4];
            float period = 360f / teeth;
            // Tooth profile as fractions of one period: root, tip, tip, root, then the valley runs to the next tooth.
            float[] fractions = { 0f, 0.17f, 0.47f, 0.64f };
            bool[] tip = { false, true, true, false };
            int index = 0;
            for (int k = 0; k < teeth; k++)
            {
                for (int j = 0; j < 4; j++)
                {
                    float angle = (k + fractions[j]) * period * Mathf.Deg2Rad;
                    float r = tip[j] ? tipRadius : rootRadius;
                    result[index++] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                }
            }

            return result;
        }

        static Vector2[] Polar(int samples, float radius, System.Func<float, float> shape)
        {
            var result = new Vector2[samples];
            for (int i = 0; i < samples; i++)
            {
                float theta = i * 2f * Mathf.PI / samples;
                float r = radius * shape(theta);
                result[i] = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * r;
            }

            return result;
        }

        /// <summary>Rotates the list so it is sorted by angle in [0, 2pi) from the +X axis (a no-op for already sorted polar samples).</summary>
        static Vector2[] SortByAngle(Vector2[] points)
        {
            var angles = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                float a = Mathf.Atan2(points[i].y, points[i].x);
                angles[i] = a < 0f ? a + 2f * Mathf.PI : a;
            }

            System.Array.Sort(angles, points);
            return points;
        }

        /// <summary>
        /// The outline grown outward by <paramref name="distance"/> everywhere: each vertex moves along the miter of its
        /// two edge normals so both neighbouring edges end up exactly <paramref name="distance"/> further out. The miter is
        /// capped at 4x so a needle tip can't spike; the cap only loosens a gap, never closes one.
        /// </summary>
        public static Vector2[] Offset(Vector2[] outline, float distance)
        {
            int n = outline.Length;
            var result = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = outline[(i + n - 1) % n];
                Vector2 here = outline[i];
                Vector2 next = outline[(i + 1) % n];
                Vector2 n0 = EdgeNormal(prev, here);
                Vector2 n1 = EdgeNormal(here, next);
                Vector2 miter = n0 + n1;
                if (miter.sqrMagnitude < 1e-8f)
                {
                    miter = n0;
                }

                miter.Normalize();
                float scale = distance / Mathf.Max(0.25f, Vector2.Dot(miter, n0));
                result[i] = here + miter * scale;
            }

            return result;
        }

        /// <summary>Outward unit normal of the edge a to b of a counter-clockwise outline.</summary>
        public static Vector2 EdgeNormal(Vector2 a, Vector2 b)
        {
            Vector2 e = b - a;
            float length = e.magnitude;
            if (length < 1e-9f)
            {
                return Vector2.up;
            }

            return new Vector2(e.y, -e.x) / length;
        }

        /// <summary>
        /// The smallest distance between the two outlines, measured both ways (every vertex of one against every edge
        /// of the other). With the hole built by <see cref="Offset"/> this is the gap the viewer is told about.
        /// </summary>
        public static float MinClearance(Vector2[] inner, Vector2[] outer)
        {
            return Mathf.Min(MinVertexToEdges(inner, outer), MinVertexToEdges(outer, inner));
        }

        static float MinVertexToEdges(Vector2[] points, Vector2[] outline)
        {
            float best = float.MaxValue;
            int n = outline.Length;
            for (int i = 0; i < points.Length; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    best = Mathf.Min(best, DistanceToSegment(points[i], outline[j], outline[(j + 1) % n]));
                }
            }

            return best;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            float t = lengthSquared > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
