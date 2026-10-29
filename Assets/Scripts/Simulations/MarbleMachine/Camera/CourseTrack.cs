using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// The course as a smooth closed rail for the race camera. Built from the same vertices (and the same cumulative lengths) as
    /// <see cref="RaceProgress"/>, so an arc length here is the same arc length the simulation ranks by. The raw polyline is
    /// resampled, then Gaussian-smoothed into a rail (where the camera looks) and a heading table (which way the course runs,
    /// noise-free). Pure geometry, no scene access.
    /// </summary>
    public sealed class CourseTrack
    {
        readonly Vector3[] _vertices;
        readonly float[] _vertexDist;
        readonly float _length;
        readonly int _n;
        readonly float _ds;
        readonly Vector3[] _raw;
        readonly Vector3[] _rail;
        readonly Vector3[] _heading;

        /// <summary>Total lap length in metres; equals <c>RaceProgress.TotalLength</c> for the same vertices.</summary>
        public float Length => _length;

        /// <summary>True when the lap runs clockwise seen from above, so the inside of the loop is on the racers' right.</summary>
        public bool InteriorOnRight { get; }

        public int VertexCount => _vertices.Length;

        public CourseTrack(IReadOnlyList<Vector3> vertices, float ds = 0.5f, float railSigma = 4f, float headingSigma = 8f)
        {
            int count = vertices.Count;
            _vertices = new Vector3[count];
            _vertexDist = new float[count];
            float run = 0f;
            for (int i = 0; i < count; i++)
            {
                _vertices[i] = vertices[i];
                _vertexDist[i] = run;
                run += Vector3.Distance(vertices[i], vertices[(i + 1) % count]);
            }

            _length = Mathf.Max(0.01f, run);

            float area = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = _vertices[i];
                Vector3 q = _vertices[(i + 1) % count];
                area += p.x * q.z - q.x * p.z;
            }

            InteriorOnRight = 0.5f * area < 0f;

            _n = Mathf.Max(8, Mathf.CeilToInt(_length / Mathf.Max(0.05f, ds)));
            _ds = _length / _n;
            _raw = new Vector3[_n];
            int seg = 0;
            for (int j = 0; j < _n; j++)
            {
                float s = j * _ds;
                while (seg < count - 1 && _vertexDist[seg + 1] <= s)
                {
                    seg++;
                }

                Vector3 a = _vertices[seg];
                Vector3 b = _vertices[(seg + 1) % count];
                float segLen = Vector3.Distance(a, b);
                float t = segLen > 1e-6f ? Mathf.Clamp01((s - _vertexDist[seg]) / segLen) : 0f;
                _raw[j] = Vector3.Lerp(a, b, t);
            }

            _rail = GaussianSmooth(_raw, railSigma);

            // Heading: smoothed horizontal tangents of the rail.
            var tangent = new Vector3[_n];
            for (int k = 0; k < _n; k++)
            {
                Vector3 d = _rail[(k + 1) % _n] - _rail[(k - 1 + _n) % _n];
                d.y = 0f;
                tangent[k] = d;
            }

            _heading = new Vector3[_n];
            int reach = Mathf.CeilToInt(3f * headingSigma / _ds);
            var weights = new float[reach + 1];
            for (int k = 0; k <= reach; k++)
            {
                float u = k * _ds / headingSigma;
                weights[k] = Mathf.Exp(-0.5f * u * u);
            }

            var valid = new bool[_n];
            for (int j = 0; j < _n; j++)
            {
                Vector3 sum = Vector3.zero;
                for (int k = -reach; k <= reach; k++)
                {
                    sum += tangent[Wrap(j + k)] * weights[Mathf.Abs(k)];
                }

                if (sum.magnitude >= 1e-4f)
                {
                    _heading[j] = sum.normalized;
                    valid[j] = true;
                }
            }

            // A degenerate sample (a perfectly closed-in stretch) copies its predecessor; a second pass covers index 0.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int j = 0; j < _n; j++)
                {
                    if (!valid[j])
                    {
                        int prev = Wrap(j - 1);
                        if (valid[prev] || pass == 1)
                        {
                            _heading[j] = _heading[prev];
                            valid[j] = true;
                        }
                    }
                }
            }

            for (int j = 0; j < _n; j++)
            {
                if (_heading[j].sqrMagnitude < 0.5f)
                {
                    _heading[j] = Vector3.forward;
                }
            }
        }

        int Wrap(int j) => ((j % _n) + _n) % _n;

        Vector3[] GaussianSmooth(Vector3[] src, float sigma)
        {
            int reach = Mathf.CeilToInt(3f * sigma / _ds);
            var w = new float[reach + 1];
            for (int k = 0; k <= reach; k++)
            {
                float u = k * _ds / sigma;
                w[k] = Mathf.Exp(-0.5f * u * u);
            }

            var dst = new Vector3[_n];
            for (int j = 0; j < _n; j++)
            {
                Vector3 sum = Vector3.zero;
                float wsum = 0f;
                for (int k = -reach; k <= reach; k++)
                {
                    float wk = w[Mathf.Abs(k)];
                    sum += src[Wrap(j + k)] * wk;
                    wsum += wk;
                }

                dst[j] = sum / wsum;
            }

            return dst;
        }

        /// <summary>Wraps any arc length into 0..Length.</summary>
        public float Wrap01(float s) => Mod(s, _length);

        public static float Mod(float s, float length)
        {
            float m = s % length;
            return m < 0f ? m + length : m;
        }

        /// <summary>Cumulative arc length at vertex <paramref name="v"/> (vertex 2i is station i's entry, 2i+1 its exit).</summary>
        public float VertexDistance(int v) => _vertexDist[Mathf.Clamp(v, 0, _vertexDist.Length - 1)];

        Vector3 Lookup(Vector3[] table, float s)
        {
            float f = Mod(s, _length) / _ds;
            int i = Mathf.FloorToInt(f);
            float t = f - i;
            i = Wrap(i);
            return Vector3.Lerp(table[i], table[Wrap(i + 1)], t);
        }

        /// <summary>Smoothed course centre line at arc length <paramref name="s"/>.</summary>
        public Vector3 Rail(float s) => Lookup(_rail, s);

        /// <summary>The exact course polyline at arc length <paramref name="s"/>.</summary>
        public Vector3 Raw(float s) => Lookup(_raw, s);

        /// <summary>Smoothed horizontal flow direction (unit, y = 0) at arc length <paramref name="s"/>.</summary>
        public Vector3 Heading(float s)
        {
            Vector3 h = Lookup(_heading, s);
            return h.sqrMagnitude > 1e-8f ? h.normalized : Vector3.forward;
        }

        /// <summary>Horizontal unit vector to the outside of the loop for a horizontal forward <paramref name="f"/>.</summary>
        public Vector3 Outside(Vector3 f)
        {
            Vector3 right = Vector3.Cross(Vector3.up, f);
            return InteriorOnRight ? -right : right;
        }

        /// <summary>
        /// Arc length of the point on the course nearest <paramref name="p"/>, searched from <paramref name="back"/> behind to
        /// <paramref name="ahead"/> ahead of <paramref name="sGuess"/>. The result is not wrapped: it lies in the same unrolled
        /// coordinates as <paramref name="sGuess"/> (so a lap-line crossing is continuous).
        /// </summary>
        public float ProjectNear(Vector3 p, float sGuess, float back, float ahead)
        {
            int lo = Mathf.FloorToInt((sGuess - back) / _ds);
            int hi = Mathf.CeilToInt((sGuess + ahead) / _ds);
            int best = lo;
            float bestD = float.MaxValue;
            for (int k = lo; k <= hi; k++)
            {
                float d = (p - _raw[Wrap(k)]).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = k;
                }
            }

            // Refine on the two segments touching the best sample.
            float bestS = best * _ds;
            float bestSeg = bestD;
            for (int side = -1; side <= 0; side++)
            {
                int k0 = best + side;
                Vector3 a = _raw[Wrap(k0)];
                Vector3 b = _raw[Wrap(k0 + 1)];
                Vector3 ab = b - a;
                float l2 = ab.sqrMagnitude;
                float t = l2 > 1e-9f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0f;
                float d = (p - (a + ab * t)).sqrMagnitude;
                if (d < bestSeg)
                {
                    bestSeg = d;
                    bestS = (k0 + t) * _ds;
                }
            }

            return bestS;
        }
    }
}
