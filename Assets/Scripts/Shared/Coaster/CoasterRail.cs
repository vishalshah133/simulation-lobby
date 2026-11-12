using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Shared.Coaster
{
    /// <summary>Tiny vector helpers shared by the pure kernel classes (no Quaternion, so the maths runs anywhere).</summary>
    public static class CoasterMath
    {
        /// <summary>Rodrigues rotation of v about a unit axis by angleRad (right-hand rule in the formula's own handedness).</summary>
        public static Vector3 Rotate(Vector3 v, Vector3 axis, float angleRad)
        {
            float c = Mathf.Cos(angleRad);
            float s = Mathf.Sin(angleRad);
            return v * c + Vector3.Cross(axis, v) * s + axis * (Vector3.Dot(axis, v) * (1f - c));
        }

        /// <summary>Remove the part of v along unit n and normalise; falls back to <paramref name="fallback"/> if degenerate.</summary>
        public static Vector3 OrthoNormalize(Vector3 v, Vector3 n, Vector3 fallback)
        {
            Vector3 r = v - n * Vector3.Dot(v, n);
            float m = r.magnitude;
            return m > 1e-6f ? r / m : fallback;
        }

        public static float SmoothStep01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }

    /// <summary>The rail a cart follows. Implemented by <see cref="CoasterRail"/>.</summary>
    public interface ICoasterRail
    {
        float Length { get; }
        /// <summary>Pose on the rail at arc length d (clamped). forward is unit tangent, up is the banked normal (unit, perpendicular to forward).</summary>
        void Sample(float d, out Vector3 position, out Vector3 forward, out Vector3 up);
    }

    /// <summary>
    /// The coaster rail: a centripetal Catmull-Rom spline through the control points, resampled into an arc-length table
    /// (about 0.25 m) with a parallel-transported up vector, so a vertical loop or corkscrew never flips. Banking is
    /// layered on top as a roll about the tangent, and an optional per-point level weight unwinds the transport twist
    /// back toward the horizon where the track is meant to be upright again (see railLevelWeight).
    /// Pure class: no MonoBehaviour, no Time, no Physics.
    /// </summary>
    public sealed class CoasterRail : ICoasterRail
    {
        const float TableStep = 0.25f;
        const float FineStep = 0.1f;

        readonly Vector3[] _pos;
        readonly Vector3[] _fwd;
        readonly Vector3[] _up;
        readonly float _step;
        readonly float[] _controlDist;

        public float Length { get; }

        /// <summary>Number of control points the rail was built from.</summary>
        public int ControlCount => _controlDist.Length;

        /// <summary>Largest angle (degrees) between the up vectors of two adjacent table samples. A flip would show here.</summary>
        public float MaxUpStepDegrees { get; private set; }

        /// <summary>Largest angle (degrees) between the tangents of two adjacent table samples.</summary>
        public float MaxTurnStepDegrees { get; private set; }

        /// <summary>Arc length (m) at which control point i sits.</summary>
        public float ControlDistance(int i)
        {
            return _controlDist[Mathf.Clamp(i, 0, _controlDist.Length - 1)];
        }

        public CoasterRail(Vector3[] points, float[] bankDeg, float[] levelWeight)
        {
            if (points == null || points.Length < 2) throw new ArgumentException("A rail needs at least 2 control points.");
            int n = points.Length;
            float[] bank = Pad(bankDeg, n);
            float[] level = Pad(levelWeight, n);

            // 1. Fine polyline along the spline, remembering the fractional control index of every sample.
            var fp = new List<Vector3>(n * 40);
            var fci = new List<float>(n * 40);
            var fd = new List<float>(n * 40);
            _controlDist = new float[n];
            float run = 0f;
            for (int s = 0; s < n - 1; s++)
            {
                Vector3 p0 = s > 0 ? points[s - 1] : points[0] * 2f - points[1];
                Vector3 p1 = points[s];
                Vector3 p2 = points[s + 1];
                Vector3 p3 = s + 2 < n ? points[s + 2] : points[n - 1] * 2f - points[n - 2];
                int m = Mathf.Max(4, Mathf.CeilToInt(Vector3.Distance(p1, p2) / FineStep));
                for (int k = 0; k < m; k++)
                {
                    float u = (float)k / m;
                    Vector3 p = CatmullRom(p0, p1, p2, p3, u);
                    if (fp.Count > 0) run += Vector3.Distance(fp[fp.Count - 1], p);
                    if (k == 0) _controlDist[s] = run;
                    fp.Add(p);
                    fci.Add(s + u);
                    fd.Add(run);
                }
            }
            Vector3 last = points[n - 1];
            run += Vector3.Distance(fp[fp.Count - 1], last);
            fp.Add(last);
            fci.Add(n - 1);
            fd.Add(run);
            _controlDist[n - 1] = run;
            Length = run;

            // 2. Uniform arc-length table.
            int count = Mathf.Max(2, Mathf.CeilToInt(Length / TableStep));
            _step = Length / count;
            _pos = new Vector3[count + 1];
            _fwd = new Vector3[count + 1];
            _up = new Vector3[count + 1];
            var ci = new float[count + 1];
            int j = 0;
            for (int i = 0; i <= count; i++)
            {
                float d = i * _step;
                while (j < fd.Count - 2 && fd[j + 1] < d) j++;
                float span = Mathf.Max(1e-6f, fd[j + 1] - fd[j]);
                float t = Mathf.Clamp01((d - fd[j]) / span);
                _pos[i] = Vector3.Lerp(fp[j], fp[j + 1], t);
                ci[i] = Mathf.Lerp(fci[j], fci[j + 1], t);
            }

            for (int i = 0; i <= count; i++)
            {
                Vector3 a = _pos[Mathf.Max(0, i - 1)];
                Vector3 b = _pos[Mathf.Min(count, i + 1)];
                Vector3 f = b - a;
                float mag = f.magnitude;
                _fwd[i] = mag > 1e-8f ? f / mag : (i > 0 ? _fwd[i - 1] : Vector3.forward);
            }

            // 3. Parallel transport of the up vector, then level unwinding and banking.
            var pt = new Vector3[count + 1];
            pt[0] = LevelUp(_fwd[0]);
            for (int i = 1; i <= count; i++)
            {
                Vector3 f0 = _fwd[i - 1];
                Vector3 f1 = _fwd[i];
                Vector3 axis = Vector3.Cross(f0, f1);
                float sin = axis.magnitude;
                Vector3 u = pt[i - 1];
                if (sin > 1e-7f)
                {
                    float ang = Mathf.Atan2(sin, Vector3.Dot(f0, f1));
                    u = CoasterMath.Rotate(u, axis / sin, ang);
                }
                pt[i] = CoasterMath.OrthoNormalize(u, f1, pt[i - 1]);
            }

            for (int i = 0; i <= count; i++)
            {
                int seg = Mathf.Clamp(Mathf.FloorToInt(ci[i]), 0, n - 2);
                float fr = Mathf.Clamp01(ci[i] - seg);
                float sm = CoasterMath.SmoothStep01(fr);
                float b = Mathf.Lerp(bank[seg], bank[seg + 1], sm);
                float w = Mathf.Lerp(level[seg], level[seg + 1], sm);

                Vector3 f = _fwd[i];
                float roll = 0f;
                if (w > 1e-4f && Mathf.Abs(f.y) < 0.98f)
                {
                    Vector3 lv = LevelUp(f);
                    float e = Mathf.Atan2(Vector3.Dot(Vector3.Cross(pt[i], lv), f), Vector3.Dot(pt[i], lv));
                    roll += w * e;
                }
                roll -= b * Mathf.Deg2Rad; // positive bank leans right = rotate toward -right
                _up[i] = CoasterMath.OrthoNormalize(CoasterMath.Rotate(pt[i], f, roll), f, pt[i]);
            }

            float maxUp = 0f, maxTurn = 0f;
            for (int i = 1; i <= count; i++)
            {
                maxUp = Mathf.Max(maxUp, AngleDeg(_up[i - 1], _up[i]));
                maxTurn = Mathf.Max(maxTurn, AngleDeg(_fwd[i - 1], _fwd[i]));
            }
            MaxUpStepDegrees = maxUp;
            MaxTurnStepDegrees = maxTurn;
        }

        public void Sample(float d, out Vector3 position, out Vector3 forward, out Vector3 up)
        {
            d = Mathf.Clamp(d, 0f, Length);
            float x = d / _step;
            int i = Mathf.Min((int)x, _pos.Length - 2);
            float t = x - i;
            position = Vector3.Lerp(_pos[i], _pos[i + 1], t);
            Vector3 f = Vector3.Lerp(_fwd[i], _fwd[i + 1], t);
            forward = CoasterMath.OrthoNormalize(f, Vector3.zero, _fwd[i]);
            Vector3 u = Vector3.Lerp(_up[i], _up[i + 1], t);
            up = CoasterMath.OrthoNormalize(u, forward, _up[i]);
        }

        static float[] Pad(float[] src, int n)
        {
            var r = new float[n];
            if (src != null)
            {
                for (int i = 0; i < n && i < src.Length; i++) r[i] = src[i];
            }
            return r;
        }

        static Vector3 LevelUp(Vector3 f)
        {
            Vector3 u = Vector3.up - f * f.y;
            float m = u.magnitude;
            if (m > 1e-4f) return u / m;
            // Vertical tangent: any perpendicular will do, pick one that is stable.
            Vector3 alt = Vector3.forward - f * f.z;
            return alt.normalized;
        }

        static float AngleDeg(Vector3 a, Vector3 b)
        {
            float c = Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f);
            return Mathf.Acos(c) * Mathf.Rad2Deg;
        }

        static float Knot(Vector3 a, Vector3 b)
        {
            return Mathf.Max(1e-3f, Mathf.Sqrt(Vector3.Distance(a, b)));
        }

        static Vector3 Blend(Vector3 pa, Vector3 pb, float ta, float tb, float t)
        {
            float span = Mathf.Max(1e-6f, tb - ta);
            return pa * ((tb - t) / span) + pb * ((t - ta) / span);
        }

        /// <summary>Barry-Goldman centripetal Catmull-Rom (alpha 0.5) between p1 and p2.</summary>
        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float t0 = 0f;
            float t1 = t0 + Knot(p0, p1);
            float t2 = t1 + Knot(p1, p2);
            float t3 = t2 + Knot(p2, p3);
            float t = Mathf.Lerp(t1, t2, u);
            Vector3 a1 = Blend(p0, p1, t0, t1, t);
            Vector3 a2 = Blend(p1, p2, t1, t2, t);
            Vector3 a3 = Blend(p2, p3, t2, t3, t);
            Vector3 b1 = Blend(a1, a2, t0, t2, t);
            Vector3 b2 = Blend(a2, a3, t1, t3, t);
            return Blend(b1, b2, t1, t2, t);
        }
    }
}
