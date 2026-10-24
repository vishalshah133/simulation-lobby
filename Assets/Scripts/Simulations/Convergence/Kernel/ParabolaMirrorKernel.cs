using System.Collections.Generic;

namespace SimulationLobby.Simulations.Convergence.Kernel
{
    /// <summary>
    /// A row of bodies dropped straight down into an upward-opening parabolic mirror
    /// <c>y = vertexY + x^2 / 4f</c>, at constant speed. Every vertical ray reflects through the focus, so
    /// they all cross the focus at the same instant (every path drop row, wall, focus is the same length),
    /// then fan out, bounce a second time and leave the bowl as a flat vertical line back at the drop height.
    /// The wall is open at the top: beyond the rim (|x| &gt; rimHalfWidth) there is nothing to hit.
    /// </summary>
    /// <remarks>
    /// Doubles and System.Math only; no UnityEngine, so a console harness can compile it.
    /// <para>
    /// No centre slit: the drop row is one evenly spaced, exactly symmetric line. Every body still crosses the focus
    /// at the same instant, but a body dropped with |x1| &lt; 4f^2 / rimHalfWidth lands its second bounce beyond the
    /// rim (x2 = -4f^2 / x1), so it leaves over the rim at an angle rather than rising vertically; the centre body
    /// skips the second bounce entirely. With f 0.8 and W 4 that is the middle ~17% of the row (up to ~43 degrees off vertical).
    /// </para>
    /// </remarks>
    public sealed class ParabolaMirrorKernel : IConvergenceKernel
    {
        const int MaxBouncesPerStep = 8;
        const double Nudge = 1e-9;
        const double MinT = 1e-9;

        readonly double _f;
        readonly double _w;
        readonly double _vy;
        readonly double _dropY;
        readonly double _speed;
        readonly double _margin;
        readonly Double2 _start;
        readonly Double2 _target;

        double[] _x = System.Array.Empty<double>();
        double[] _y = System.Array.Empty<double>();
        double[] _dx = System.Array.Empty<double>();
        double[] _dy = System.Array.Empty<double>();

        public ParabolaMirrorKernel(double focalLength, double rimHalfWidth, double vertexY, double dropY, double speed, double dropMargin)
        {
            _f = focalLength;
            _w = rimHalfWidth;
            _vy = vertexY;
            _dropY = dropY;
            _speed = speed;
            _margin = dropMargin;
            _start = new Double2(0, dropY);
            _target = new Double2(0, vertexY + focalLength);
        }

        public int Count { get; private set; }

        public Double2 Start => _start;

        public Double2 IdealStart => _start;

        public Double2 Target => _target;

        public double ExpectedMeetSeconds => (_dropY - _vy + _f) / _speed;

        public bool OutlineClosed => false;

        /// <summary>
        /// Drop <paramref name="count"/> bodies from the row at y = dropY, moving straight down. The angle offset is
        /// ignored: any tilt would break the focus. Index order is left to right.
        /// </summary>
        public void Launch(int count, double angleOffsetRadians)
        {
            Count = count;
            if (_x.Length != count)
            {
                _x = new double[count];
                _y = new double[count];
                _dx = new double[count];
                _dy = new double[count];
            }

            double outer = _w * (1.0 - _margin);
            for (int i = 0; i < count; i++)
            {
                _x[i] = -outer + 2.0 * outer * (i + 0.5) / count;
            }

            for (int i = 0; i < count; i++)
            {
                _y[i] = _dropY;
                _dx[i] = 0.0;
                _dy[i] = -1.0;
            }
        }

        public void Step(double dt, List<WallHit> hits)
        {
            double k = 1.0 / (4.0 * _f);
            for (int i = 0; i < Count; i++)
            {
                double remaining = _speed * dt;
                double x = _x[i];
                double y = _y[i];
                double dx = _dx[i];
                double dy = _dy[i];

                for (int bounce = 0; bounce < MaxBouncesPerStep; bounce++)
                {
                    double a = dx * dx * k;
                    double b = 2.0 * x * dx * k - dy;
                    double c = x * x * k + _vy - y;

                    double t = double.PositiveInfinity;
                    if (System.Math.Abs(a) < 1e-12)
                    {
                        if (System.Math.Abs(b) > 1e-12)
                        {
                            double t0 = -c / b;
                            if (t0 > MinT && System.Math.Abs(x + dx * t0) <= _w)
                            {
                                t = t0;
                            }
                        }
                    }
                    else
                    {
                        double disc = b * b - 4.0 * a * c;
                        if (disc >= 0)
                        {
                            double sq = System.Math.Sqrt(disc);
                            double t1 = (-b - sq) / (2.0 * a);
                            double t2 = (-b + sq) / (2.0 * a);
                            if (t1 > t2)
                            {
                                double tmp = t1;
                                t1 = t2;
                                t2 = tmp;
                            }

                            if (t1 > MinT && System.Math.Abs(x + dx * t1) <= _w)
                            {
                                t = t1;
                            }
                            else if (t2 > MinT && System.Math.Abs(x + dx * t2) <= _w)
                            {
                                t = t2;
                            }
                        }
                    }

                    if (double.IsInfinity(t) || t > remaining)
                    {
                        x += dx * remaining;
                        y += dy * remaining;
                        break;
                    }

                    x += dx * t;
                    y += dy * t;
                    remaining -= t;

                    double nx = x * 0.5 / _f;
                    double ny = -1.0;
                    double nl = System.Math.Sqrt(nx * nx + ny * ny);
                    nx /= nl;
                    ny /= nl;
                    double dot = dx * nx + dy * ny;
                    dx -= 2.0 * dot * nx;
                    dy -= 2.0 * dot * ny;

                    if (hits != null)
                    {
                        double pitch = 1.0 - System.Math.Abs(x) / _w;
                        pitch = pitch < 0 ? 0 : (pitch > 1 ? 1 : pitch);
                        hits.Add(new WallHit(i, new Double2(x, y), pitch));
                    }

                    x -= nx * Nudge;
                    y -= ny * Nudge;
                }

                _x[i] = x;
                _y[i] = y;
                _dx[i] = dx;
                _dy[i] = dy;
            }
        }

        public Double2 GetPosition(int index) => new Double2(_x[index], _y[index]);

        public int CountWithin(Double2 point, double radius)
        {
            double r2 = radius * radius;
            int n = 0;
            for (int i = 0; i < Count; i++)
            {
                double ex = _x[i] - point.X;
                double ey = _y[i] - point.Y;
                if (ex * ex + ey * ey <= r2)
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>Left rim to right rim along the parabola, inclusive of both ends.</summary>
        public void SampleOutline(int samples, List<Double2> points)
        {
            points.Clear();
            for (int i = 0; i < samples; i++)
            {
                double x = samples > 1 ? -_w + 2.0 * _w * i / (samples - 1) : 0.0;
                points.Add(new Double2(x, _vy + x * x / (4.0 * _f)));
            }
        }
    }
}
