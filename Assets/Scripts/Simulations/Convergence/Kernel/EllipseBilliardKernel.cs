using System.Collections.Generic;

namespace SimulationLobby.Simulations.Convergence.Kernel
{
    /// <summary>
    /// Bodies fired from one focus of an axis-aligned ellipse at constant speed, reflecting off the
    /// wall. Every path focus, wall, other focus is 2a long, so they all arrive together. A circle
    /// (equal half-axes) has both foci at the centre. The target is the other focus plus <c>targetOffset</c>
    /// (zero = the focus), so a round can leave the target dot where it was while the ellipse changes.
    /// </summary>
    /// <remarks>Doubles and System.Math only; no UnityEngine, so a console harness can compile it.</remarks>
    public sealed class EllipseBilliardKernel : IConvergenceKernel
    {
        const int MaxBouncesPerStep = 8;
        const double Nudge = 1e-9;

        readonly double _hw;
        readonly double _hh;
        readonly double _speed;
        readonly Double2 _start;
        readonly Double2 _idealStart;
        readonly Double2 _target;
        readonly double _refAngle;
        readonly bool _isCircle;

        double[] _x = System.Array.Empty<double>();
        double[] _y = System.Array.Empty<double>();
        double[] _dx = System.Array.Empty<double>();
        double[] _dy = System.Array.Empty<double>();
        double _launchAngle0;

        public EllipseBilliardKernel(double halfWidth, double halfHeight, Double2 startOffset, Double2 targetOffset, double speed)
        {
            _hw = halfWidth;
            _hh = halfHeight;
            _speed = speed;
            double c = System.Math.Sqrt(System.Math.Abs(_hh * _hh - _hw * _hw));
            _isCircle = c < 1e-12;

            Double2 focus;
            Double2 other;
            if (_isCircle)
            {
                focus = new Double2(0, 0);
                other = focus;
                _refAngle = 0;
            }
            else if (_hh > _hw)
            {
                focus = new Double2(0, c);
                other = new Double2(0, -c);
                _refAngle = System.Math.PI * 0.5;
            }
            else
            {
                focus = new Double2(c, 0);
                other = new Double2(-c, 0);
                _refAngle = 0;
            }

            _idealStart = focus;
            _start = focus + startOffset;
            _target = other + targetOffset;
        }

        public int Count { get; private set; }

        public Double2 Start => _start;

        public Double2 IdealStart => _idealStart;

        public Double2 Target => _target;

        public double ExpectedMeetSeconds => 2.0 * System.Math.Max(_hw, _hh) / _speed;

        public bool OutlineClosed => true;

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

            _launchAngle0 = angleOffsetRadians;
            for (int i = 0; i < count; i++)
            {
                double a = 2.0 * System.Math.PI * i / count + angleOffsetRadians;
                _x[i] = _start.X;
                _y[i] = _start.Y;
                _dx[i] = System.Math.Cos(a);
                _dy[i] = System.Math.Sin(a);
            }
        }

        public void Step(double dt, List<WallHit> hits)
        {
            double hw2 = _hw * _hw;
            double hh2 = _hh * _hh;
            for (int i = 0; i < Count; i++)
            {
                double remaining = _speed * dt;
                double x = _x[i];
                double y = _y[i];
                double dx = _dx[i];
                double dy = _dy[i];

                for (int bounce = 0; bounce < MaxBouncesPerStep; bounce++)
                {
                    double a = dx * dx / hw2 + dy * dy / hh2;
                    double b = x * dx / hw2 + y * dy / hh2;
                    double c = x * x / hw2 + y * y / hh2 - 1.0;
                    double disc = b * b - a * c;
                    double t = disc > 0 ? (-b + System.Math.Sqrt(disc)) / a : 0.0;
                    if (t < 0)
                    {
                        t = 0;
                    }

                    if (t > remaining)
                    {
                        x += dx * remaining;
                        y += dy * remaining;
                        break;
                    }

                    x += dx * t;
                    y += dy * t;
                    remaining -= t;

                    double nx = x / hw2;
                    double ny = y / hh2;
                    double nl = System.Math.Sqrt(nx * nx + ny * ny);
                    nx /= nl;
                    ny /= nl;
                    double dot = dx * nx + dy * ny;
                    dx -= 2.0 * dot * nx;
                    dy -= 2.0 * dot * ny;

                    if (hits != null)
                    {
                        hits.Add(new WallHit(i, new Double2(x, y), Pitch(x, y)));
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

        /// <summary>Angle from the start's side vertex (or body 0's launch direction for a circle), folded to 0..1.</summary>
        double Pitch(double x, double y)
        {
            double theta = System.Math.Atan2(y, x);
            double reference = _isCircle ? _launchAngle0 : _refAngle;
            double d = System.Math.Abs(theta - reference) % (2.0 * System.Math.PI);
            if (d > System.Math.PI)
            {
                d = 2.0 * System.Math.PI - d;
            }

            return d / System.Math.PI;
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

        /// <summary>Clockwise from the top (0, hh), the same for every ellipse so morphs never twist.</summary>
        public void SampleOutline(int samples, List<Double2> points)
        {
            points.Clear();
            for (int i = 0; i < samples; i++)
            {
                double phi = 2.0 * System.Math.PI * i / samples;
                points.Add(new Double2(_hw * System.Math.Sin(phi), _hh * System.Math.Cos(phi)));
            }
        }
    }
}
