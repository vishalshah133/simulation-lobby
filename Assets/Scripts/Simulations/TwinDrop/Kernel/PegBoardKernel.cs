using System;
using System.Collections.Generic;

namespace SimulationLobby.Simulations.TwinDrop.Kernel
{
    /// <summary>
    /// Two balls falling through a board of round pegs under gravity, in double precision. Each peg hit
    /// multiplies the difference between the two paths, so two balls released a hair apart end up in
    /// different slots, while two released from exactly the same spot always end up in the same one.
    /// </summary>
    /// <remarks>
    /// Pure maths: no <c>UnityEngine</c>, no randomness. The release position is passed in. Stepped in
    /// fixed sub-steps so the result depends only on the inputs.
    /// </remarks>
    public sealed class PegBoardKernel
    {
        const double SubStep = 1.0 / 960.0;
        const int BallCount = 2;

        readonly double _halfWidth;
        readonly double _floorY;
        readonly double _pegRadius;
        readonly double _ballRadius;
        readonly double _gravity;
        readonly double _restitution;
        readonly int _bins;
        readonly List<double> _px = new List<double>();
        readonly List<double> _py = new List<double>();

        readonly double[] _x = new double[BallCount];
        readonly double[] _y = new double[BallCount];
        readonly double[] _vx = new double[BallCount];
        readonly double[] _vy = new double[BallCount];
        readonly bool[] _landed = new bool[BallCount];
        readonly int[] _slot = new int[BallCount];
        double _carry;

        public PegBoardKernel(int rows, double spacing, double rowSpacing, double halfWidth, double topRowY,
                              double floorY, double pegRadius, double ballRadius, double gravity,
                              double restitution, int bins)
        {
            _halfWidth = halfWidth;
            _floorY = floorY;
            _pegRadius = pegRadius;
            _ballRadius = ballRadius;
            _gravity = gravity;
            _restitution = restitution;
            _bins = bins;

            // Staggered rows. The outermost pegs stay at least one spacing from each wall, so a ball can
            // never wedge between a peg and the wall.
            int reach = (int)Math.Floor(halfWidth / spacing - 1.0);
            for (int r = 0; r < rows; r++)
            {
                double y = topRowY - r * rowSpacing;
                if (r % 2 == 0)
                {
                    for (int k = -reach; k <= reach; k++)
                    {
                        _px.Add(k * spacing);
                        _py.Add(y);
                    }
                }
                else
                {
                    for (int k = -reach; k < reach; k++)
                    {
                        _px.Add((k + 0.5) * spacing);
                        _py.Add(y);
                    }
                }
            }
        }

        public int PegCount => _px.Count;

        public double PegX(int i) => _px[i];

        public double PegY(int i) => _py[i];

        public double BallX(int i) => _x[i];

        public double BallY(int i) => _y[i];

        public bool Landed(int i) => _landed[i];

        public int Slot(int i) => _slot[i];

        public bool AllLanded => _landed[0] && _landed[1];

        /// <summary>Peg and wall contacts made by ball 0 so far. Drives the note per bounce.</summary>
        public long LeaderHits { get; private set; }

        /// <summary>Ball 0 starts at <paramref name="x"/>, ball 1 at <paramref name="x"/> + <paramref name="gap"/>.</summary>
        public void Release(double x, double gap, double y)
        {
            for (int i = 0; i < BallCount; i++)
            {
                _x[i] = x + (i == 1 ? gap : 0.0);
                _y[i] = y;
                _vx[i] = 0.0;
                _vy[i] = 0.0;
                _landed[i] = false;
                _slot[i] = -1;
            }

            _carry = 0.0;
            LeaderHits = 0;
        }

        public void Step(double dt)
        {
            _carry += dt;
            while (_carry >= SubStep)
            {
                _carry -= SubStep;
                for (int i = 0; i < BallCount; i++)
                {
                    if (!_landed[i])
                    {
                        Advance(i, SubStep);
                    }
                }
            }
        }

        void Advance(int i, double dt)
        {
            double vx = _vx[i];
            double vy = _vy[i] - _gravity * dt;
            double x = _x[i] + vx * dt;
            double y = _y[i] + vy * dt;
            bool hit = false;
            double limit = _halfWidth - _ballRadius;

            if (x > limit) { x = 2.0 * limit - x; vx = -vx * _restitution; hit = true; }
            else if (x < -limit) { x = -2.0 * limit - x; vx = -vx * _restitution; hit = true; }

            double reach = _pegRadius + _ballRadius;
            for (int p = 0; p < _px.Count; p++)
            {
                double dy = y - _py[p];
                if (dy > reach || dy < -reach)
                {
                    continue;
                }

                double dx = x - _px[p];
                if (dx > reach || dx < -reach)
                {
                    continue;
                }

                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < reach && d > 1e-12)
                {
                    double nx = dx / d;
                    double ny = dy / d;
                    double dot = vx * nx + vy * ny;
                    if (dot < 0.0)
                    {
                        vx -= (1.0 + _restitution) * dot * nx;
                        vy -= (1.0 + _restitution) * dot * ny;
                        hit = true;
                    }

                    x = _px[p] + nx * reach;
                    y = _py[p] + ny * reach;
                }
            }

            _vx[i] = vx;
            _vy[i] = vy;
            _x[i] = x;
            _y[i] = y;

            if (i == 0 && hit)
            {
                LeaderHits++;
            }

            if (y <= _floorY + _ballRadius)
            {
                _y[i] = _floorY + _ballRadius;
                _landed[i] = true;
                int slot = (int)Math.Floor((x + _halfWidth) / (2.0 * _halfWidth / _bins));
                _slot[i] = Math.Max(0, Math.Min(_bins - 1, slot));
            }
        }
    }
}
