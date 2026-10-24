using System;

namespace SimulationLobby.Simulations.HydraulicPress.Kernel
{
    /// <summary>One crushed ball. Ids are indexes into the kernel's ball arrays.</summary>
    public readonly struct PopEvent
    {
        public readonly int ParentId;
        public readonly int ChildA;
        public readonly int ChildB;
        public readonly double X;
        public readonly double Y;
        public readonly double Radius;
        public readonly int Generation;

        public PopEvent(int parentId, int childA, int childB, double x, double y, double radius, int generation)
        {
            ParentId = parentId;
            ChildA = childA;
            ChildB = childB;
            X = x;
            Y = y;
            Radius = radius;
            Generation = generation;
        }
    }

    /// <summary>Plain copy of the config's numbers for one round. No engine types, so the harness can fill it.</summary>
    public struct PressKernelSettings
    {
        public double HalfWidth;
        public double ChamberHeight;
        public double StartRadius;
        /// <summary>The first ball's x is drawn from [-StartOffset, StartOffset].</summary>
        public double StartOffset;
        public double PressTons;
        public double BallStrengthTons;
        public double BallWeightTons;
        /// <summary>A ball is over strength only above strength x (1 + this). Absorbs solver noise at a marginal 1.00 tons.</summary>
        public double LoadTolerance;
        /// <summary>Contacts flatter than this (vertical normal component) carry no load.</summary>
        public double MinNormalY;
        /// <summary>Load splits among a ball's supports in proportion to (vertical normal component) ^ this. 1 = proportional.</summary>
        public double NormalPower;
        /// <summary>Time constant (s) of the average a ball's load is judged on, so a jittering contact can't pop it.</summary>
        public double LoadSmoothing;
        public double PressSpeed;
        public double SqueezeSeconds;
        /// <summary>How fast a squeeze unwinds when the load drops, in seconds of squeeze per second (0 = it holds).</summary>
        public double SqueezeDecay;
        public double SplitJitterDegrees;
        public double SettleSpeed;
        /// <summary>A ball's squeeze only advances while the pile's RMS speed is below this.</summary>
        public double CalmSpeed;
        /// <summary>True: calm is judged per ball (its own displacement); false: by the whole pile's RMS speed.</summary>
        public bool LocalCalm;
        public double StopHoldSeconds;
        public double MinRadius;
        public int MaxPopsPerTick;
        public int Substeps;
        public int Iterations;
        /// <summary>Extra push-out passes run right after a wave of pops.</summary>
        public int RelaxIterations;
        /// <summary>Two balls (or a ball and the plate/floor) carry load when their gap is under this fraction of the smaller radius.</summary>
        public double ContactTolerance;
        public double Gravity;
        /// <summary>Velocity damping per second.</summary>
        public double Damping;
        public double MaxSpeed;
        public int MaxBalls;
        /// <summary>Sliding friction at contacts, 0..1 per pass.</summary>
        public double Friction;

        /// <summary>The tuned values of HydraulicPressConfig's defaults (the harness runs these).</summary>
        public static PressKernelSettings Default()
        {
            return new PressKernelSettings
            {
                HalfWidth = 3.0, ChamberHeight = 7.0, StartRadius = 0.9, StartOffset = 0.6,
                PressTons = 2.0, BallStrengthTons = 1.0, BallWeightTons = 0.0, LoadTolerance = 0.15, LoadSmoothing = 0.04,
                MinNormalY = 0.05, NormalPower = 1.0, PressSpeed = 2.5,
                SqueezeSeconds = 0.12, SqueezeDecay = 0.0, SplitJitterDegrees = 3.0, SettleSpeed = 0.1, CalmSpeed = 0.5, LocalCalm = true,
                StopHoldSeconds = 0.5, MinRadius = 0.008, MaxPopsPerTick = 64, Substeps = 3, Iterations = 4,
                RelaxIterations = 16, ContactTolerance = 0.1, Gravity = 4.0, Damping = 0.5, Friction = 0.05,
                MaxSpeed = 6.0, MaxBalls = 5000
            };
        }
    }

    /// <summary>
    /// Deterministic pile of circles under a press. Position-based solver (gravity, floor, walls, plate, circle
    /// contacts, uniform-grid broadphase), plus a static q-model load pass over the current contact graph: the
    /// press weight enters through plate contacts and flows top to bottom, split by each contact's vertical
    /// normal component; the floor absorbs. A ball overloaded for SqueezeSeconds pops into two of r/sqrt2.
    /// Pure double maths, no engine types. Floor top at y = 0, inner walls at x = +-HalfWidth.
    /// </summary>
    public sealed class PressKernel
    {
        const int Columns = 2048;
        const double Sqrt2 = 1.4142135623730951;

        readonly PressKernelSettings _s;
        readonly Random _rng;

        double[] _colTop;
        int[] _colId;
        double[] _x, _y, _r, _vx, _vy, _px, _py, _load, _loadS, _squeeze, _timer, _sx, _sy, _inbound, _sumW, _floorW, _sortKey;
        int[] _gen, _next, _order, _ordY, _rank;
        double[] _keyY;
        bool[] _blocked;
        int _count;

        // grid
        int[] _head = new int[1];
        int _cols, _rows;
        double _cell;

        // contact edges (higher ball -> lower ball) and their CSR index by source
        int[] _eFrom = new int[64];
        int[] _eTo = new int[64];
        double[] _eW = new double[64];
        int _edges;
        int[] _csrStart = new int[1];
        int[] _csrEdge = new int[64];

        int[] _cand = new int[16];
        double[] _candKey = new double[16];

        double _pressY;
        bool _resting;
        double _maxLoad;
        double _maxSpeed;
        double _agitation;
        double _stopTimer;
        double _elapsed;
        int _maxGen;
        int _minRadiusHits;
        int _plateContacts;
        bool _overflow;

        public PressKernel(PressKernelSettings s, int seed)
        {
            _s = s;
            _rng = new Random(seed);
            Allocate(64);
            double off = (_rng.NextDouble() * 2.0 - 1.0) * s.StartOffset;
            Add(off, s.StartRadius, s.StartRadius, 0);
            _pressY = s.ChamberHeight;
        }

        public int Count => _count;
        public double[] X => _x;
        public double[] Y => _y;
        public double[] R => _r;
        public double[] Load => _load;
        public double[] Squeeze => _squeeze;
        public int[] Generation => _gen;
        public double PressY => _pressY;
        public bool PressResting => _resting;
        public bool IsStopped { get; private set; }
        public double MaxLoad => _maxLoad;

        // --- Additions beyond the plan's contract (read-only extras) ---
        public double MaxSpeed => _maxSpeed;
        /// <summary>RMS displacement speed of the whole pile (what calm and settled are judged on).</summary>
        public double Agitation => _agitation;
        public double Elapsed => _elapsed;
        public int MaxGeneration => _maxGen;
        public int MinRadiusHits => _minRadiusHits;
        public int PlateContacts => _plateContacts;
        /// <summary>True if MaxBalls blocked a pop (a tuning failure).</summary>
        public bool Overflow => _overflow;

        void Allocate(int cap)
        {
            Array.Resize(ref _x, cap); Array.Resize(ref _y, cap); Array.Resize(ref _r, cap);
            Array.Resize(ref _vx, cap); Array.Resize(ref _vy, cap); Array.Resize(ref _px, cap); Array.Resize(ref _py, cap);
            Array.Resize(ref _sx, cap); Array.Resize(ref _sy, cap); Array.Resize(ref _load, cap); Array.Resize(ref _loadS, cap); Array.Resize(ref _squeeze, cap); Array.Resize(ref _timer, cap);
            Array.Resize(ref _inbound, cap); Array.Resize(ref _sumW, cap); Array.Resize(ref _floorW, cap);
            Array.Resize(ref _sortKey, cap);
            Array.Resize(ref _gen, cap); Array.Resize(ref _next, cap); Array.Resize(ref _order, cap);
            Array.Resize(ref _blocked, cap);
            Array.Resize(ref _ordY, cap); Array.Resize(ref _rank, cap); Array.Resize(ref _keyY, cap);
        }

        int Add(double x, double y, double r, int gen)
        {
            if (_count >= _x.Length)
            {
                Allocate(_x.Length * 2);
            }

            int i = _count++;
            _x[i] = x; _y[i] = y; _sx[i] = x; _sy[i] = y; _r[i] = r; _px[i] = x; _py[i] = y; _vx[i] = 0; _vy[i] = 0;
            _load[i] = 0; _loadS[i] = 0; _squeeze[i] = 0; _timer[i] = 0; _gen[i] = gen; _blocked[i] = false;
            if (gen > _maxGen)
            {
                _maxGen = gen;
            }

            return i;
        }

        /// <summary>One fixed tick. Returns the number of pops.</summary>
        public int Step(double dt, Action<PopEvent> onPop)
        {
            if (IsStopped)
            {
                return 0;
            }

            _elapsed += dt;
            for (int i = 0; i < _count; i++)
            {
                _sx[i] = _x[i];
                _sy[i] = _y[i];
            }

            int subs = Math.Max(1, _s.Substeps);
            double h = dt / subs;
            for (int k = 0; k < subs; k++)
            {
                Substep(h);
            }

            double moved = 0, sum2 = 0;
            for (int i = 0; i < _count; i++)
            {
                double mx = _x[i] - _sx[i], my = _y[i] - _sy[i];
                double m2 = mx * mx + my * my;
                sum2 += m2;
                if (m2 > moved) moved = m2;
            }

            // Real displacement per second (not the solver's internal velocity). Calm/settled use the RMS over the
            // whole pile so one jittering grain in a corner can't hold up the round.
            _maxSpeed = Math.Sqrt(moved) / dt;
            _agitation = _count > 0 ? Math.Sqrt(sum2 / _count) / dt : 0.0;

            ComputeLoads();
            int pops = ResolveSqueeze(dt, onPop);
            EvaluateStop(dt, pops);
            return pops;
        }

        // ------------------------------------------------------------------ physics

        void Substep(double h)
        {
            int n = _count;

            // The plate lowers at PressSpeed, never past the highest ball top.
            {
                double target = _pressY - _s.PressSpeed * h;
                double top = MaxTop();
                _pressY = target < top ? top : target;
            }

            double g = _s.Gravity;
            for (int i = 0; i < n; i++)
            {
                _vy[i] -= g * h;
                _px[i] = _x[i];
                _py[i] = _y[i];
                _x[i] += _vx[i] * h;
                _y[i] += _vy[i] * h;
            }

            BuildGrid();
            SortByHeight();
            for (int it = 0; it < _s.Iterations; it++)
            {
                SolvePairs();
                SolveBounds();
            }

            for (int i = 0; i < n; i++)
            {
                _vx[i] = (_x[i] - _px[i]) / h;
                _vy[i] = (_y[i] - _py[i]) / h;
            }

            BuildGrid();
            for (int it = 0; it < Math.Min(_s.Iterations, 3); it++)
            {
                VelocityPass();
            }

            double damp = Math.Max(0.0, 1.0 - _s.Damping * h);
            double maxS = _s.MaxSpeed;
            double maxSeen = 0;
            for (int i = 0; i < n; i++)
            {
                double vx = _vx[i] * damp, vy = _vy[i] * damp;
                double sp = Math.Sqrt(vx * vx + vy * vy);
                if (sp > maxS)
                {
                    double k = maxS / sp;
                    vx *= k; vy *= k; sp = maxS;
                }

                _vx[i] = vx; _vy[i] = vy;
                if (sp > maxSeen)
                {
                    maxSeen = sp;
                }
            }


            // The plate is rigid and kinematic: balls are never pushed through it, it rides up on the pile.
            double topNow = MaxTop();
            if (topNow > _pressY)
            {
                _pressY = topNow;
            }

        }

        double MaxTop()
        {
            double top = 0;
            for (int i = 0; i < _count; i++)
            {
                double t = _y[i] + _r[i];
                if (t > top)
                {
                    top = t;
                }
            }

            return top;
        }

        void BuildGrid()
        {
            double rmax = 0;
            for (int i = 0; i < _count; i++)
            {
                if (_r[i] > rmax)
                {
                    rmax = _r[i];
                }
            }

            _cell = Math.Max(2.0 * rmax * 1.05, 1e-3);
            _cols = (int)(2.0 * _s.HalfWidth / _cell) + 3;
            _rows = (int)((_s.ChamberHeight + 1.0) / _cell) + 3;
            int cells = _cols * _rows;
            if (_head.Length < cells)
            {
                _head = new int[cells];
            }

            Array.Fill(_head, -1, 0, cells);
            for (int i = 0; i < _count; i++)
            {
                int c = CellOf(_x[i], _y[i]);
                _next[i] = _head[c];
                _head[c] = i;
            }
        }

        int CellOf(double x, double y)
        {
            int cx = (int)((x + _s.HalfWidth) / _cell) + 1;
            int cy = (int)(y / _cell) + 1;
            if (cx < 0) cx = 0; else if (cx >= _cols) cx = _cols - 1;
            if (cy < 0) cy = 0; else if (cy >= _rows) cy = _rows - 1;
            return cy * _cols + cx;
        }

        /// <summary>
        /// One Gauss-Seidel sweep, lowest ball first: each ball is held inside the chamber, then pushes the balls
        /// above it that overlap it. Bottom-up is what lets a stack settle in a sweep or two instead of sagging.
        /// </summary>
        void SolvePairs()
        {
            int n = _count;
            double hw = _s.HalfWidth;
            for (int k = 0; k < n; k++)
            {
                int i = _ordY[k];
                double ri = _r[i];
                double d0;
                if (_x[i] < -hw + ri) { d0 = -hw + ri - _x[i]; _x[i] += d0; _px[i] += d0; }
                else if (_x[i] > hw - ri) { d0 = _x[i] - (hw - ri); _x[i] -= d0; _px[i] -= d0; }
                if (_y[i] < ri) { d0 = ri - _y[i]; _y[i] += d0; _py[i] += d0; }

                int c = CellOf(_x[i], _y[i]);
                int cx = c % _cols, cy = c / _cols;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = cy + dy;
                    if (yy < 0 || yy >= _rows) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = cx + dx;
                        if (xx < 0 || xx >= _cols) continue;
                        for (int j = _head[yy * _cols + xx]; j >= 0; j = _next[j])
                        {
                            if (_rank[j] <= k) continue;
                            double ddx = _x[j] - _x[i];
                            double ddy = _y[j] - _y[i];
                            double minD = ri + _r[j];
                            double d2 = ddx * ddx + ddy * ddy;
                            if (d2 >= minD * minD) continue;
                            double d = Math.Sqrt(d2);
                            double nx, ny;
                            if (d < 1e-12) { nx = 0; ny = 1; d = 0; }
                            else { nx = ddx / d; ny = ddy / d; }
                            double overlap = minD - d;
                            double wi = 1.0 / (ri * ri), wj = 1.0 / (_r[j] * _r[j]);
                            double si = overlap * wi / (wi + wj);
                            double sj = overlap * wj / (wi + wj);
                            _x[i] -= nx * si; _y[i] -= ny * si;
                            _x[j] += nx * sj; _y[j] += ny * sj;
                            // Push-out is a teleport, not motion: shift the previous position too, so overlap
                            // never becomes velocity. Contact velocities are handled by VelocityPass.
                            _px[i] -= nx * si; _py[i] -= ny * si;
                            _px[j] += nx * sj; _py[j] += ny * sj;
                        }
                    }
                }
            }
        }

        void SortByHeight()
        {
            int n = _count;
            for (int i = 0; i < n; i++)
            {
                _ordY[i] = i;
                _keyY[i] = _y[i];
            }

            Array.Sort(_keyY, _ordY, 0, n);
            for (int k = 0; k < n; k++)
            {
                _rank[_ordY[k]] = k;
            }
        }

        /// <summary>Inelastic contacts: remove approaching normal velocity, damp sliding. Floor, walls and circle pairs.</summary>
        void VelocityPass()
        {
            int n = _count;
            double hw = _s.HalfWidth;
            double mu = _s.Friction;
            for (int i = 0; i < n; i++)
            {
                double ri = _r[i];
                double eps = 0.02 * ri;
                if (_y[i] - ri <= eps && _vy[i] < 0) { _vy[i] = 0; _vx[i] *= 1.0 - mu; }
                if (_x[i] - ri <= -hw + eps && _vx[i] < 0) { _vx[i] = 0; _vy[i] *= 1.0 - mu; }
                if (_x[i] + ri >= hw - eps && _vx[i] > 0) { _vx[i] = 0; _vy[i] *= 1.0 - mu; }

                int c = CellOf(_x[i], _y[i]);
                int cx = c % _cols, cy = c / _cols;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = cy + dy;
                    if (yy < 0 || yy >= _rows) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = cx + dx;
                        if (xx < 0 || xx >= _cols) continue;
                        for (int j = _head[yy * _cols + xx]; j >= 0; j = _next[j])
                        {
                            if (j <= i) continue;
                            double ddx = _x[j] - _x[i];
                            double ddy = _y[j] - _y[i];
                            double reach = ri + _r[j] + 0.02 * Math.Min(ri, _r[j]);
                            double d2 = ddx * ddx + ddy * ddy;
                            if (d2 > reach * reach || d2 < 1e-24) continue;
                            double d = Math.Sqrt(d2);
                            double nx = ddx / d, ny = ddy / d;
                            double rvx = _vx[j] - _vx[i], rvy = _vy[j] - _vy[i];
                            double vn = rvx * nx + rvy * ny;
                            double wi = 1.0 / (ri * ri), wj = 1.0 / (_r[j] * _r[j]);
                            double ws = wi + wj;
                            if (vn < 0)
                            {
                                double jn = vn / ws;      // impulse that zeroes the approach
                                _vx[i] += nx * jn * wi; _vy[i] += ny * jn * wi;
                                _vx[j] -= nx * jn * wj; _vy[j] -= ny * jn * wj;
                            }

                            double tx = rvx - vn * nx, ty = rvy - vn * ny;   // sliding
                            double jt = mu / ws;
                            _vx[i] += tx * jt * wi; _vy[i] += ty * jt * wi;
                            _vx[j] -= tx * jt * wj; _vy[j] -= ty * jt * wj;
                        }
                    }
                }
            }
        }

        void SolveBounds()
        {
            double hw = _s.HalfWidth;
            for (int i = 0; i < _count; i++)
            {
                double r = _r[i];
                double d;
                if (_x[i] < -hw + r) { d = -hw + r - _x[i]; _x[i] += d; _px[i] += d; }
                else if (_x[i] > hw - r) { d = _x[i] - (hw - r); _x[i] -= d; _px[i] -= d; }
                if (_y[i] < r) { d = r - _y[i]; _y[i] += d; _py[i] += d; }
            }
        }

        // ------------------------------------------------------------------ load model

        void GrowEdges()
        {
            int cap = _eFrom.Length * 2;
            Array.Resize(ref _eFrom, cap);
            Array.Resize(ref _eTo, cap);
            Array.Resize(ref _eW, cap);
        }

        void ComputeLoads()
        {
            int n = _count;
            double tol = _s.ContactTolerance;
            BuildGrid();
            _edges = 0;
            for (int i = 0; i < n; i++)
            {
                _inbound[i] = 0; _sumW[i] = 0; _floorW[i] = 0;
                _order[i] = i;
                _sortKey[i] = -_y[i];
            }

            for (int i = 0; i < n; i++)
            {
                double xi = _x[i], yi = _y[i], ri = _r[i];
                if (yi - ri <= tol * ri)
                {
                    _floorW[i] = 1.0;
                }

                int c = CellOf(xi, yi);
                int cx = c % _cols, cy = c / _cols;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = cy + dy;
                    if (yy < 0 || yy >= _rows) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = cx + dx;
                        if (xx < 0 || xx >= _cols) continue;
                        for (int j = _head[yy * _cols + xx]; j >= 0; j = _next[j])
                        {
                            if (j <= i) continue;
                            double ddx = _x[j] - xi;
                            double ddy = _y[j] - yi;
                            double rj = _r[j];
                            double reach = ri + rj + tol * Math.Min(ri, rj);
                            double d2 = ddx * ddx + ddy * ddy;
                            if (d2 > reach * reach) continue;
                            double d = Math.Sqrt(d2);
                            if (d < 1e-12) continue;
                            double ny = Math.Abs(ddy) / d;
                            if (ny <= _s.MinNormalY) continue;
                            int from = ddy < 0 ? i : j;   // the higher ball carries onto the lower one
                            int to = ddy < 0 ? j : i;
                            if (_edges >= _eFrom.Length) GrowEdges();
                            double wgt = _s.NormalPower == 1.0 ? ny : Math.Pow(ny, _s.NormalPower);
                            _eFrom[_edges] = from; _eTo[_edges] = to; _eW[_edges] = wgt;
                            _sumW[from] += wgt;
                            _edges++;
                        }
                    }
                }
            }

            // Plate: once it rests on the pile it presses like a soft pad. Its weight is shared across the pile's
            // exposed top surface, in proportion to the horizontal width each ball shows to the plate (the ball on
            // top of a column, not the ones tucked beneath it). So the answer depends on how wide each exposed
            // ball is next to the pile, not on which ball happens to be the tallest.
            int topBall = -1;
            double topY = double.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                double ty = _y[i] + _r[i];
                if (ty > topY)
                {
                    topY = ty;
                    topBall = i;
                }
            }

            _plateContacts = 0;
            _resting = topBall >= 0 && _pressY - topY <= tol * _r[topBall] + 1e-9;
            if (_resting)
            {
                AssignPlateLoad(n);
            }

            // CSR of edges by source ball (cursor borrowed from _next, which the grid no longer needs)
            if (_csrStart.Length < n + 1) _csrStart = new int[Math.Max(n + 1, _csrStart.Length * 2)];
            if (_csrEdge.Length < _edges) _csrEdge = new int[Math.Max(_edges, _csrEdge.Length * 2)];
            for (int i = 0; i <= n; i++) _csrStart[i] = 0;
            for (int e = 0; e < _edges; e++) _csrStart[_eFrom[e] + 1]++;
            for (int i = 0; i < n; i++) _csrStart[i + 1] += _csrStart[i];
            for (int i = 0; i < n; i++) _next[i] = _csrStart[i];
            for (int e = 0; e < _edges; e++) _csrEdge[_next[_eFrom[e]]++] = e;

            Array.Sort(_sortKey, _order, 0, n);   // top first; deterministic for identical input

            double maxLoad = 0;
            for (int k = 0; k < n; k++)
            {
                int i = _order[k];
                double load = _inbound[i] + _s.BallWeightTons;
                _load[i] = load;
                if (load > maxLoad) maxLoad = load;
                double tot = _sumW[i] + _floorW[i];
                if (load <= 0 || tot <= 0) continue;
                double per = load / tot;
                for (int p = _csrStart[i]; p < _csrStart[i + 1]; p++)
                {
                    int e = _csrEdge[p];
                    _inbound[_eTo[e]] += per * _eW[e];
                }
            }

            _maxLoad = maxLoad;
        }

        void AssignPlateLoad(int n)
        {
            if (_colId == null)
            {
                _colTop = new double[Columns];
                _colId = new int[Columns];
            }

            double width = 2.0 * _s.HalfWidth;
            double dx = width / Columns;
            double left = -_s.HalfWidth;
            for (int c = 0; c < Columns; c++)
            {
                _colTop[c] = double.NegativeInfinity;
                _colId[c] = -1;
            }

            for (int i = 0; i < n; i++)
            {
                double r = _r[i];
                int c0 = (int)Math.Ceiling((_x[i] - r - left) / dx - 0.5);
                int c1 = (int)Math.Floor((_x[i] + r - left) / dx - 0.5);
                if (c0 < 0) c0 = 0;
                if (c1 >= Columns) c1 = Columns - 1;
                for (int c = c0; c <= c1; c++)
                {
                    double ddx = left + (c + 0.5) * dx - _x[i];
                    double q = r * r - ddx * ddx;
                    if (q <= 0) continue;
                    double h = _y[i] + Math.Sqrt(q);
                    if (h > _colTop[c])
                    {
                        _colTop[c] = h;
                        _colId[c] = i;
                    }
                }
            }

            int covered = 0;
            for (int c = 0; c < Columns; c++)
            {
                int id = _colId[c];
                if (id >= 0)
                {
                    _inbound[id] += 1.0;
                    covered++;
                }
            }

            if (covered == 0)
            {
                return;
            }

            double scale = _s.PressTons / covered;
            int touched = 0;
            for (int i = 0; i < n; i++)
            {
                if (_inbound[i] > 0)
                {
                    _inbound[i] *= scale;
                    touched++;
                }
            }

            _plateContacts = touched;
        }

        // ------------------------------------------------------------------ squeeze and pop

        bool Over(int i)
        {
            return !_blocked[i] && _loadS[i] > _s.BallStrengthTons * (1.0 + _s.LoadTolerance);
        }

        int ResolveSqueeze(double dt, Action<PopEvent> onPop)
        {
            int n = _count;
            int nc = 0;
            double sq = Math.Max(_s.SqueezeSeconds, 1e-6);
            bool calm = _agitation < _s.CalmSpeed;
            double calmD2 = _s.CalmSpeed * dt * (_s.CalmSpeed * dt);
            double ema = _s.LoadSmoothing > 1e-9 ? Math.Min(1.0, dt / _s.LoadSmoothing) : 1.0;
            for (int i = 0; i < n; i++)
            {
                _loadS[i] += (_load[i] - _loadS[i]) * ema;
            }

            for (int i = 0; i < n; i++)
            {
                if (Over(i))
                {
                    double mx = _x[i] - _sx[i], my = _y[i] - _sy[i];
                    if ((!_s.LocalCalm && calm) || (_s.LocalCalm && mx * mx + my * my < calmD2))
                    {
                        _timer[i] += dt;   // the squeeze only builds while the pile is quiet, so loads are the settled ones
                    }
                }
                else
                {
                    _timer[i] = Math.Max(0.0, _timer[i] - _s.SqueezeDecay * dt);
                }

                double s = _timer[i] / sq;
                _squeeze[i] = s > 1.0 ? 1.0 : s;
                if (_timer[i] >= sq - 1e-9 && Over(i))
                {
                    if (nc >= _cand.Length)
                    {
                        Array.Resize(ref _cand, _cand.Length * 2);
                        Array.Resize(ref _candKey, _candKey.Length * 2);
                    }

                    _cand[nc] = i;
                    _candKey[nc] = -_load[i];
                    nc++;
                }
            }

            if (nc == 0)
            {
                return 0;
            }

            // Highest load first; equal loads fall back to id so the order is total.
            Array.Sort(_candKey, _cand, 0, nc);
            for (int a = 0; a < nc;)
            {
                int b = a;
                while (b + 1 < nc && _candKey[b + 1] == _candKey[a]) b++;
                if (b > a) Array.Sort(_cand, a, b - a + 1);
                a = b + 1;
            }

            int pops = 0;
            double jitter = _s.SplitJitterDegrees * Math.PI / 180.0;
            for (int c = 0; c < nc && pops < _s.MaxPopsPerTick; c++)
            {
                int i = _cand[c];
                double rc = _r[i] / Sqrt2;
                if (rc < _s.MinRadius)
                {
                    _blocked[i] = true;
                    _minRadiusHits++;
                    continue;
                }

                if (_count + 1 > _s.MaxBalls)
                {
                    _overflow = true;
                    _blocked[i] = true;
                    continue;
                }

                double ang = (_rng.NextDouble() * 2.0 - 1.0) * jitter + (_rng.Next(2) == 0 ? 0.0 : Math.PI);
                double ax = Math.Cos(ang) * rc, ay = Math.Sin(ang) * rc;
                double px = _x[i], py = _y[i], pr = _r[i];
                int gen = _gen[i] + 1;
                double pvx = _vx[i], pvy = _vy[i];

                _r[i] = rc; _gen[i] = gen; _timer[i] = 0; _squeeze[i] = 0; _load[i] = 0; _loadS[i] = 0;
                // Children keep the parent's bottom edge (so they rest where it rested) and only the tilt lifts one.
                double yBase = py - pr + rc;
                _x[i] = px + ax; _y[i] = yBase + Math.Max(ay, 0.0); _px[i] = _x[i]; _py[i] = _y[i];
                _vx[i] = pvx; _vy[i] = pvy;
                int childB = Add(px - ax, yBase - Math.Min(ay, 0.0), rc, gen);
                _vx[childB] = pvx; _vy[childB] = pvy;
                onPop?.Invoke(new PopEvent(i, i, childB, px, py, pr, gen));
                pops++;
            }

            if (pops > 0)
            {
                Relax();
            }

            return pops;
        }

        /// <summary>
        /// After a wave of pops: many extra push-out passes (all teleports) so the new fragments find room before the
        /// next physics step, instead of leaving the pile overlapped and letting that overlap leak out as motion.
        /// </summary>
        void Relax()
        {
            for (int it = 0; it < _s.RelaxIterations; it++)
            {
                if ((it & 3) == 0)
                {
                    BuildGrid();
                    SortByHeight();
                }

                SolvePairs();
                SolveBounds();
            }
        }

        void EvaluateStop(double dt, int pops)
        {
            bool quiet = pops == 0 && _resting && _agitation < _s.SettleSpeed;
            for (int i = 0; quiet && i < _count; i++)
            {
                if (Over(i))
                {
                    quiet = false;
                }
            }

            if (quiet)
            {
                _stopTimer += dt;
                if (_stopTimer >= _s.StopHoldSeconds - 1e-9)
                {
                    IsStopped = true;
                }
            }
            else
            {
                _stopTimer = 0;
            }
        }
    }
}
