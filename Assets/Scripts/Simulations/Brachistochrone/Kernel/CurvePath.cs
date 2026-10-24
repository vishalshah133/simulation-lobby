using System;

namespace SimulationLobby.Simulations.Brachistochrone.Kernel
{
    /// <summary>
    /// A track as a polyline from A = (0,0) to B = (bx, by). x runs right, y is the DROP (positive downward).
    /// Pure double maths, no UnityEngine, so the harness can compile it on its own. Every sample except A has
    /// y &gt; 0, so a bead released at A never stalls.
    /// </summary>
    public sealed class CurvePath
    {
        readonly double[] _x;
        readonly double[] _y;
        readonly double[] _s;

        /// <summary>The curve kind this was built as.</summary>
        public CurveKind Kind { get; }

        /// <summary>Cycloid only: generating-circle radius (0 for other kinds).</summary>
        public double CycloidRadius { get; private set; }

        /// <summary>Cycloid only: the end angle thetaB (&gt; pi means the curve dips below B and climbs back).</summary>
        public double CycloidThetaEnd { get; private set; }

        /// <summary>Largest y on the path (the lowest point of the track).</summary>
        public double MaxY { get; }

        public int Count => _x.Length;

        public double Length => _s[_s.Length - 1];

        public double X(int i) => _x[i];

        public double Y(int i) => _y[i];

        /// <summary>Cumulative arc length at sample i.</summary>
        public double S(int i) => _s[i];

        CurvePath(CurveKind kind, double[] x, double[] y)
        {
            Kind = kind;
            _x = x;
            _y = y;
            _s = new double[x.Length];
            double max = 0.0;
            for (int i = 1; i < x.Length; i++)
            {
                double dx = x[i] - x[i - 1];
                double dy = y[i] - y[i - 1];
                _s[i] = _s[i - 1] + Math.Sqrt(dx * dx + dy * dy);
                if (y[i] > max)
                {
                    max = y[i];
                }
            }

            MaxY = max;
        }

        /// <summary>
        /// Build a track. <paramref name="shape"/> is per-kind: SteepDrop bend radius as a fraction of bx,
        /// DeepDip depth factor; ignored by the others. <paramref name="samples"/> is the point count (min 8).
        /// </summary>
        public static CurvePath Build(CurveKind kind, double bx, double by, double shape, int samples)
        {
            if (!(bx > 0.0) || !(by > 0.0))
            {
                throw new ArgumentException("B must have bx > 0 and by > 0.");
            }

            int n = Math.Max(8, samples);
            var x = new double[n];
            var y = new double[n];
            double radius = 0.0;
            double thetaB = 0.0;

            switch (kind)
            {
                case CurveKind.Straight:
                    for (int i = 0; i < n; i++)
                    {
                        double u = (double)i / (n - 1);
                        x[i] = bx * u;
                        y[i] = by * u;
                    }

                    break;

                case CurveKind.Cycloid:
                    thetaB = SolveCycloidTheta(bx / by);
                    radius = by / (1.0 - Math.Cos(thetaB));
                    for (int i = 0; i < n; i++)
                    {
                        double th = thetaB * i / (n - 1);
                        x[i] = radius * (th - Math.Sin(th));
                        y[i] = radius * (1.0 - Math.Cos(th));
                    }

                    break;

                case CurveKind.CircleArc:
                {
                    double c = (bx * bx + by * by) / (2.0 * bx);
                    double phiB = Math.Atan2(by, c - bx);
                    for (int i = 0; i < n; i++)
                    {
                        double phi = phiB * i / (n - 1);
                        x[i] = c * (1.0 - Math.Cos(phi));
                        y[i] = c * Math.Sin(phi);
                    }

                    break;
                }

                case CurveKind.Parabola:
                    for (int i = 0; i < n; i++)
                    {
                        double u = (double)i / (n - 1);
                        x[i] = bx * u;
                        y[i] = by * (1.0 - (1.0 - u) * (1.0 - u));
                    }

                    break;

                case CurveKind.DeepDip:
                    for (int i = 0; i < n; i++)
                    {
                        double u = (double)i / (n - 1);
                        x[i] = bx * u;
                        y[i] = by * u + shape * by * Math.Sin(Math.PI * u);
                    }

                    break;

                case CurveKind.SteepDrop:
                    BuildSteepDrop(x, y, bx, by, shape);
                    break;
            }

            // The end sample is B exactly, whatever rounding the parametrisation left behind.
            x[n - 1] = bx;
            y[n - 1] = by;
            x[0] = 0.0;
            y[0] = 0.0;

            return new CurvePath(kind, x, y)
            {
                CycloidRadius = radius,
                CycloidThetaEnd = thetaB
            };
        }

        /// <summary>Per-sample blend of two paths with the same sample count (the Transition morph).</summary>
        public static CurvePath Lerp(CurvePath a, CurvePath b, double t)
        {
            if (a.Count != b.Count)
            {
                return b;
            }

            int n = a.Count;
            var x = new double[n];
            var y = new double[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = a._x[i] + (b._x[i] - a._x[i]) * t;
                y[i] = a._y[i] + (b._y[i] - a._y[i]) * t;
            }

            return new CurvePath(b.Kind, x, y)
            {
                CycloidRadius = a.CycloidRadius + (b.CycloidRadius - a.CycloidRadius) * t,
                CycloidThetaEnd = a.CycloidThetaEnd + (b.CycloidThetaEnd - a.CycloidThetaEnd) * t
            };
        }

        /// <summary>Position and heading (radians, atan2(dy, dx) in the y-down frame) at arc length s; s clamps to [0, Length].</summary>
        public void PointAt(double s, out double x, out double y, out double angle)
        {
            int last = _s.Length - 1;
            if (s <= 0.0)
            {
                s = 0.0;
            }
            else if (s >= _s[last])
            {
                s = _s[last];
            }

            int lo = 0;
            int hi = last;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_s[mid] <= s)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            double seg = _s[hi] - _s[lo];
            double f = seg > 0.0 ? (s - _s[lo]) / seg : 0.0;
            x = _x[lo] + (_x[hi] - _x[lo]) * f;
            y = _y[lo] + (_y[hi] - _y[lo]) * f;
            angle = Math.Atan2(_y[hi] - _y[lo], _x[hi] - _x[lo]);
        }

        /// <summary>Solve (th - sin th) / (1 - cos th) = k on (0, 2pi). The ratio is strictly increasing, so bisect then polish.</summary>
        static double SolveCycloidTheta(double k)
        {
            double lo = 1e-6;
            double hi = 2.0 * Math.PI - 1e-6;
            for (int i = 0; i < 200; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (Ratio(mid) < k)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            double th = 0.5 * (lo + hi);
            for (int i = 0; i < 6; i++) // Newton polish
            {
                double f = (th - Math.Sin(th)) - k * (1.0 - Math.Cos(th));
                double d = (1.0 - Math.Cos(th)) - k * Math.Sin(th);
                if (Math.Abs(d) < 1e-14)
                {
                    break;
                }

                double next = th - f / d;
                if (next <= lo - 1e-3 || next >= hi + 1e-3)
                {
                    break;
                }

                th = next;
            }

            return th;
        }

        static double Ratio(double th)
        {
            return (th - Math.Sin(th)) / (1.0 - Math.Cos(th));
        }

        /// <summary>
        /// Straight down to 0.9 by, a tight circular bend of radius shape * bx (counter-clockwise, toward +x),
        /// then the tangent line to B. Samples are spread by arc length over the three pieces.
        /// </summary>
        static void BuildSteepDrop(double[] x, double[] y, double bx, double by, double shape)
        {
            int n = x.Length;
            double y1 = 0.9 * by;
            double rad = Math.Max(0.02 * bx, shape * bx);
            double cx = rad;
            double cy = y1;

            // Tangent point: first alpha where (B - P) is parallel to the path direction (sin a, cos a).
            double alpha = Math.PI * 0.5;
            const int scan = 4000;
            double prev = TangentError(0.0, cx, cy, rad, bx, by);
            for (int i = 1; i <= scan; i++)
            {
                double a = Math.PI * i / scan;
                double cur = TangentError(a, cx, cy, rad, bx, by);
                if (prev > 0.0 && cur <= 0.0)
                {
                    double lo = Math.PI * (i - 1) / scan;
                    double hi = a;
                    for (int k = 0; k < 80; k++)
                    {
                        double mid = 0.5 * (lo + hi);
                        if (TangentError(mid, cx, cy, rad, bx, by) > 0.0)
                        {
                            lo = mid;
                        }
                        else
                        {
                            hi = mid;
                        }
                    }

                    alpha = 0.5 * (lo + hi);
                    break;
                }

                prev = cur;
            }

            double tx = cx - rad * Math.Cos(alpha);
            double ty = cy + rad * Math.Sin(alpha);
            double arcLen = rad * alpha;
            double tailLen = Math.Sqrt((bx - tx) * (bx - tx) + (by - ty) * (by - ty));
            double total = y1 + arcLen + tailLen;

            for (int i = 0; i < n; i++)
            {
                double s = total * i / (n - 1);
                if (s <= y1)
                {
                    x[i] = 0.0;
                    y[i] = s;
                }
                else if (s <= y1 + arcLen)
                {
                    double a = (s - y1) / rad;
                    x[i] = cx - rad * Math.Cos(a);
                    y[i] = cy + rad * Math.Sin(a);
                }
                else
                {
                    double f = tailLen > 0.0 ? (s - y1 - arcLen) / tailLen : 1.0;
                    x[i] = tx + (bx - tx) * f;
                    y[i] = ty + (by - ty) * f;
                }
            }
        }

        static double TangentError(double a, double cx, double cy, double rad, double bx, double by)
        {
            double px = cx - rad * Math.Cos(a);
            double py = cy + rad * Math.Sin(a);
            return (bx - px) * Math.Cos(a) - (by - py) * Math.Sin(a);
        }
    }
}
