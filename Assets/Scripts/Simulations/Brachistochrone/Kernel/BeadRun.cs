using System;

namespace SimulationLobby.Simulations.Brachistochrone.Kernel
{
    /// <summary>
    /// A frictionless bead released from rest at A with gravity g, riding a <see cref="CurvePath"/>.
    /// Speed at a vertex is sqrt(2 g y) (energy), acceleration along a straight segment is constant
    /// (a = g * dy / L), so each segment's time is exactly 2L / (v0 + v1) and position inside it is closed-form.
    /// </summary>
    public sealed class BeadRun
    {
        readonly CurvePath _path;
        readonly double _g;
        readonly double[] _t;  // time at each vertex
        readonly double[] _v;  // speed at each vertex

        public BeadRun(CurvePath path, double g)
        {
            _path = path;
            _g = g;
            int n = path.Count;
            _t = new double[n];
            _v = new double[n];
            for (int i = 0; i < n; i++)
            {
                _v[i] = Math.Sqrt(Math.Max(0.0, 2.0 * g * path.Y(i)));
            }

            for (int i = 1; i < n; i++)
            {
                double len = path.S(i) - path.S(i - 1);
                double vs = _v[i - 1] + _v[i];
                _t[i] = _t[i - 1] + (vs > 0.0 ? 2.0 * len / vs : 0.0);
            }

            FinishTime = _t[n - 1];
        }

        /// <summary>Sum over segments of 2L / (v0 + v1).</summary>
        public double FinishTime { get; }

        /// <summary>Arc length travelled at time t (clamps to [0, Length]). Exact inside a segment.</summary>
        public double DistanceAt(double t)
        {
            int seg;
            double tau;
            if (!Locate(t, out seg, out tau))
            {
                return t <= 0.0 ? 0.0 : _path.Length;
            }

            double len = _path.S(seg + 1) - _path.S(seg);
            double a = len > 0.0 ? _g * (_path.Y(seg + 1) - _path.Y(seg)) / len : 0.0;
            double s = _path.S(seg) + _v[seg] * tau + 0.5 * a * tau * tau;
            return Math.Min(_path.S(seg + 1), Math.Max(_path.S(seg), s));
        }

        /// <summary>Speed along the track at time t (0 before release; 0 after the finish, where the bead rests).</summary>
        public double SpeedAt(double t)
        {
            int seg;
            double tau;
            if (!Locate(t, out seg, out tau))
            {
                return 0.0;
            }

            double len = _path.S(seg + 1) - _path.S(seg);
            double a = len > 0.0 ? _g * (_path.Y(seg + 1) - _path.Y(seg)) / len : 0.0;
            return Math.Max(0.0, _v[seg] + a * tau);
        }

        /// <summary>Time along a cycloid with generating radius r from the cusp to angle thetaEnd: theta * sqrt(r / g).</summary>
        public static double CycloidTime(double r, double thetaEnd, double g)
        {
            return thetaEnd * Math.Sqrt(r / g);
        }

        bool Locate(double t, out int seg, out double tau)
        {
            seg = 0;
            tau = 0.0;
            if (t <= 0.0 || t >= FinishTime)
            {
                return false;
            }

            int lo = 0;
            int hi = _t.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_t[mid] <= t)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            seg = lo;
            tau = t - _t[lo];
            return true;
        }
    }
}
