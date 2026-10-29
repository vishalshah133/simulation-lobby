using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Rounds the corners of a chute's centreline with true circular arcs. Chaikin cutting (the mach-001 default) leaves a radius that
    /// depends on how short the neighbouring legs are, and a 90 degree corner with a 1.5 m leg ends up about 0.5 m in radius, which folds
    /// the inner wall of a 2.1 m wide chute and throws fast marbles off. An arc has a known radius and never folds when it is at least
    /// half the chute width.
    /// </summary>
    public static class ChutePath
    {
        /// <summary>
        /// Replaces every interior vertex with an arc of <paramref name="radius"/> metres, keeping both end points. A corner whose legs are
        /// too short for the full radius gets the largest arc that fits (each leg gives up at most <paramref name="legFraction"/> of its
        /// length, so neighbouring corners never overlap). Near-straight corners and hairpins are left as they are.
        /// </summary>
        public static List<Vector2> Fillet(List<Vector2> pts, float radius, float legFraction = 0.45f)
        {
            if (pts == null || pts.Count < 3 || radius <= 0f)
            {
                return pts;
            }

            var result = new List<Vector2> { pts[0] };
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector2 corner = pts[i];
                Vector2 toPrev = pts[i - 1] - corner;
                Vector2 toNext = pts[i + 1] - corner;
                float lengthPrev = toPrev.magnitude;
                float lengthNext = toNext.magnitude;
                if (lengthPrev < 1e-4f || lengthNext < 1e-4f)
                {
                    result.Add(corner);
                    continue;
                }

                toPrev /= lengthPrev;
                toNext /= lengthNext;
                float interior = Mathf.Acos(Mathf.Clamp(Vector2.Dot(toPrev, toNext), -1f, 1f)); // PI = a straight line
                float turn = Mathf.PI - interior;
                if (turn < 0.035f || turn > 3.0f)
                {
                    result.Add(corner);
                    continue;
                }

                float half = turn * 0.5f;
                float trim = Mathf.Min(radius * Mathf.Tan(half), legFraction * Mathf.Min(lengthPrev, lengthNext));
                float r = trim / Mathf.Tan(half);
                Vector2 start = corner + toPrev * trim;
                Vector2 end = corner + toNext * trim;
                Vector2 bisector = (toPrev + toNext).normalized;
                Vector2 centre = corner + bisector * (r / Mathf.Sin(interior * 0.5f));
                float a0 = Mathf.Atan2(start.y - centre.y, start.x - centre.x);
                float a1 = Mathf.Atan2(end.y - centre.y, end.x - centre.x);
                float sweep = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweep) / (8f * Mathf.Deg2Rad)));
                for (int s = 0; s <= steps; s++)
                {
                    float angle = a0 + sweep * s / steps;
                    result.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
                }
            }

            result.Add(pts[pts.Count - 1]);
            return result;
        }

        /// <summary>The tightest turn radius of a polyline, metres (a large number if it is straight). For tests.</summary>
        public static float MinRadius(IList<Vector2> pts)
        {
            float min = 1e6f;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i] - pts[i - 1];
                Vector2 b = pts[i + 1] - pts[i];
                float turn = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(a.y, a.x) * Mathf.Rad2Deg, Mathf.Atan2(b.y, b.x) * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (turn > 0.01f)
                {
                    min = Mathf.Min(min, 0.5f * (a.magnitude + b.magnitude) / turn);
                }
            }

            return min;
        }
    }
}
