using System;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall.Kernel
{
    /// <summary>
    /// Places a world's pads one after another along a helix, choosing each next position so the contact at the
    /// pad before it is a real bounce and the marble's arc clears the pads it has just left.
    /// </summary>
    /// <remarks>
    /// Why a search and not a formula: a pad's normal is the velocity change (vOut - vIn), so a hit is only a
    /// bounce when the marble arrives at its front face (vIn.n &lt; 0) and leaves from it (vOut.n &gt; 0). That
    /// fails when the horizontal speed changes along the direction of travel (a fast leap followed by a slow step),
    /// and the marble then flies through the pad. For each hit the layout tries a fixed, ordered list of candidate
    /// positions (keep the current speed, steady winding speed, wider or narrower steps, mirrored turns, then a
    /// scan round the whole well) and takes the first that bounces cleanly. A hit with no acceptable candidate
    /// sends the search back one hit (a bounded depth-first search). Same inputs, same candidates, same pick:
    /// deterministic.
    /// </remarks>
    internal sealed class FreefallPadLayout
    {
        /// <summary>Consecutive pads are kept at least this many pad radii apart so they never overlap.</summary>
        const float MinSpacingInPadRadii = 1.7f;

        /// <summary>Marble radii of lateral clearance beyond a pad's edge when the marble falls back to that pad's height.</summary>
        const float HopClearanceInMarbles = 0.5f;

        /// <summary>Required |velocity| margin along the pad normal on both sides of a contact (m/s).</summary>
        const float MinBounceMargin = 0.15f;

        /// <summary>Gap the local pick demands between the marble and recent pads. Slightly stricter than the final validation.</summary>
        const float LocalClearance = 0.02f;

        /// <summary>A candidate is checked against every pad and arc within this many seconds before it...</summary>
        const double RecentSeconds = 1.5;

        /// <summary>...but never more than this many, and always at least this many.</summary>
        const int MaxRecent = 40;
        const int MinRecent = 6;

        /// <summary>Centre-to-centre distance a new pad keeps from recent pads, in pad radii (2 = discs just touching).</summary>
        const float PadSeparationInPadRadii = 2.2f;

        const float LocalSampleStep = 1f / 240f;

        /// <summary>Fallback scan: this many evenly spaced angles around the well.</summary>
        const int GridAngles = 72;

        /// <summary>Backtracking steps allowed per pad before the best available candidate is accepted.</summary>
        const int BacktrackBudgetPerPad = 20;

        /// <summary>Multipliers on the steady-speed chord, tried after the continue-the-current-speed candidates.</summary>
        static readonly double[] ChordFactors = { 1.0, 1.25, 1.6, 2.2, 0.8, 3.0 };

        /// <summary>Multipliers on the chord that keeps the marble's current horizontal speed. Tried first.</summary>
        static readonly double[] KeepSpeedFactors = { 1.0, 1.12, 0.89, 1.3, 0.75 };

        static readonly int CandidateCount = (KeepSpeedFactors.Length + ChordFactors.Length) * 2 + GridAngles;

        readonly FreefallWorldPlan _plan;
        readonly FreefallWorld _world;
        readonly int _count;
        readonly float _g;
        readonly float _mr;
        readonly float _pr;
        readonly double[] _times;
        readonly double _speed;
        readonly double _minSpacing;
        readonly float _y0;
        readonly Func<Vector3[], Vector3[], double[], Vector3, Vector3, float> _exitScorer;

        // Canon: rotated copies of the pads (voice k flies this path rotated k * 360 / voices), as cos/sin pairs.
        readonly float[] _rotCos;
        readonly float[] _rotSin;

        readonly Vector3[] _pos;
        readonly Vector3[] _normals;
        readonly FreefallSeg[] _arcs;     // _arcs[j] flies from pad j-1 to pad j
        readonly double[] _theta;
        readonly Vector3[] _vInAt;        // velocity arriving at pad j

        // Per-step values, set by Prepare(j).
        double _baseChord;
        double _keepChord;
        double _r1;
        double _r2;
        float _y;

        FreefallPadLayout(
            FreefallLayout layout, int w, float extraDegreesPerSecond,
            Func<Vector3[], Vector3[], double[], Vector3, Vector3, float> exitScorer)
        {
            FreefallConfig cfg = layout.config;
            _plan = layout.plans[w];
            _world = _plan.world;
            _count = _plan.count;
            _g = _world.gravity;
            _mr = cfg.marbleRadius;
            _pr = _world.padRadius;
            _y0 = layout.y0[w];
            _exitScorer = exitScorer;

            _times = new double[_count];
            for (int j = 0; j < _count; j++)
            {
                _times[j] = layout.firstHitTime[w] + _plan.tRel[j];
            }

            // degreesPerSecond sets the horizontal winding speed at the world's mean hit radius.
            double degPerSec = _world.degreesPerSecond + extraDegreesPerSecond;
            double meanRadius = 0.5 * (_world.innerRadiusFraction + _world.outerRadiusFraction) * _world.wellRadius;
            _speed = degPerSec * Math.PI / 180.0 * meanRadius;
            _minSpacing = MinSpacingInPadRadii * _pr;

            int voices = Math.Max(1, _plan.VoiceCount);
            int rotated = _world.canonVoices > 1 ? Math.Max(0, voices - 1) : 0;
            _rotCos = new float[rotated];
            _rotSin = new float[rotated];
            for (int m = 0; m < rotated; m++)
            {
                float phi = (m + 1) * 2f * Mathf.PI / _world.canonVoices;
                _rotCos[m] = (float)Math.Cos(phi);
                _rotSin[m] = (float)Math.Sin(phi);
            }

            _pos = new Vector3[_count];
            _normals = new Vector3[_count];
            _arcs = new FreefallSeg[_count];
            _theta = new double[_count];
            _vInAt = new Vector3[_count];

            _pos[0] = layout.firstHit[w];
            _theta[0] = _plan.theta0;
            _vInAt[0] = w == 0 ? new Vector3(0f, -_g * cfg.introDropSeconds, 0f) : default;
        }

        /// <summary>
        /// Voice 0's pad positions. <paramref name="normals"/> holds every pad's normal except the last (its
        /// departure is the exit arc, decided by the caller); <paramref name="lastVelocityIn"/> is the velocity
        /// the marble arrives at the last pad with.
        /// </summary>
        public static Vector3[] Lay(
            FreefallLayout layout, int w, float extraDegreesPerSecond, Vector3 incomingVelocity,
            Func<Vector3[], Vector3[], double[], Vector3, Vector3, float> exitScorer,
            out Vector3[] normals, out Vector3 lastVelocityIn)
        {
            var solver = new FreefallPadLayout(layout, w, extraDegreesPerSecond, exitScorer);
            if (w > 0)
            {
                solver._vInAt[0] = incomingVelocity;
            }

            solver.Run();
            normals = solver._normals;
            lastVelocityIn = solver._vInAt[solver._count - 1];
            return solver._pos;
        }

        void Run()
        {
            int budget = BacktrackBudgetPerPad * _count;
            var next = new int[_count];
            int j = 1;
            next[1] = 0;

            while (j < _count)
            {
                Prepare(j);

                bool accepted = false;
                while (next[j] < CandidateCount)
                {
                    int idx = next[j]++;
                    Vector3 cand = Candidate(j, idx, out double th);
                    if (Evaluate(j, cand, quick: true) >= 1f)
                    {
                        Accept(j, cand, th);
                        accepted = true;
                        break;
                    }
                }

                if (!accepted)
                {
                    if (j > 1 && budget-- > 0)
                    {
                        j--; // back up one pad and try its next candidate
                        continue;
                    }

                    // No acceptable candidate and no budget left: take the least bad one.
                    float best = float.NegativeInfinity;
                    Vector3 bestPos = default;
                    double bestTheta = 0.0;
                    for (int idx = 0; idx < CandidateCount; idx++)
                    {
                        Vector3 cand = Candidate(j, idx, out double th);
                        float score = Evaluate(j, cand, quick: false);
                        if (score > best)
                        {
                            best = score;
                            bestPos = cand;
                            bestTheta = th;
                        }
                    }

                    Accept(j, bestPos, bestTheta);
                }

                j++;
                if (j < _count)
                {
                    next[j] = 0;
                }
            }
        }

        /// <summary>Set the per-step values for choosing pad j from the state of pads 0..j-1.</summary>
        void Prepare(int j)
        {
            double dt = _times[j] - _times[j - 1];
            double dy = _plan.relY[j] - _plan.relY[j - 1];
            _r1 = _plan.radius[j - 1];
            _r2 = _plan.radius[j];
            _y = _y0 + _plan.relY[j];

            double minChord = Math.Sqrt(Math.Max(0.0, _minSpacing * _minSpacing - dy * dy));

            // The marble hops off pad j-1 and falls back to that pad's height after 2 vy / g. By then it must
            // be beyond the pad's edge, or it comes down onto the pad it just left. Quick notes hop for a
            // short time, so they need the widest steps.
            double hopUp = dy / dt + 0.5 * _g * dt;
            double hopTime = Math.Max(2.0 * hopUp / _g, 1e-3);
            minChord = Math.Max(minChord, (_pr + HopClearanceInMarbles * _mr) * dt / hopTime);
            _baseChord = Math.Max(_speed * dt, minChord);
            _baseChord = Math.Max(_baseChord, Math.Abs(_r2 - _r1));

            Vector3 vIn = _vInAt[j - 1];
            _keepChord = Math.Sqrt(vIn.x * vIn.x + vIn.z * vIn.z) * dt;
        }

        /// <summary>
        /// Candidate number idx for pad j: the keep-speed chords, then the steady-speed chords (each on the
        /// winding side, then mirrored), then a scan round the well starting nearest the previous pad.
        /// </summary>
        Vector3 Candidate(int j, int idx, out double theta)
        {
            int nKeep = KeepSpeedFactors.Length;
            int nChord = ChordFactors.Length;
            int dirSign = _plan.dir;
            double angleStep;

            if (idx < (nKeep + nChord) * 2)
            {
                int f = idx >> 1;
                double chord = f < nKeep ? _keepChord * KeepSpeedFactors[f] : _baseChord * ChordFactors[f - nKeep];
                chord = Math.Min(Math.Max(chord, Math.Abs(_r2 - _r1)), _r1 + _r2);

                angleStep = Math.PI;
                if (_r1 * _r2 > 1e-6)
                {
                    double cos = (_r1 * _r1 + _r2 * _r2 - chord * chord) / (2.0 * _r1 * _r2);
                    angleStep = Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos)));
                }

                if ((idx & 1) == 1)
                {
                    dirSign = -dirSign;
                }
            }
            else
            {
                int g = idx - (nKeep + nChord) * 2;     // 0..GridAngles-1
                int k = g / 2 + 1;                      // 1..GridAngles/2
                angleStep = 2.0 * Math.PI * k / GridAngles;
                if ((g & 1) == 1)
                {
                    dirSign = -dirSign;
                }
            }

            theta = _theta[j - 1] + dirSign * angleStep;
            return new Vector3((float)(_r2 * Math.Cos(theta)), _y, (float)(_r2 * Math.Sin(theta)));
        }

        void Accept(int j, Vector3 cand, double theta)
        {
            _pos[j] = cand;
            _theta[j] = theta;
            FreefallSeg arc = FreefallSeg.Between(_times[j - 1], _pos[j - 1], _times[j], cand, _g);
            _arcs[j] = arc;
            _normals[j - 1] = Normal(_vInAt[j - 1], arc.v0);
            _vInAt[j] = arc.EndVelocity;
        }

        internal static Vector3 Normal(Vector3 vIn, Vector3 vOut)
        {
            Vector3 dv = vOut - vIn;
            float m = dv.magnitude;
            return m > 1e-5f ? dv / m : Vector3.up;
        }

        /// <summary>
        /// &gt;= 1 means acceptable: pad j-1 bounces cleanly, the new pad keeps its distance from the pads around
        /// it, and the arc to it clears the recent pads (and, for the last pad, the exit hop does too). With
        /// <paramref name="quick"/>, returns as soon as a cheap test fails; the value is then only a lower bound.
        /// </summary>
        float Evaluate(int j, Vector3 cand, bool quick)
        {
            Vector3 vIn = _vInAt[j - 1];
            FreefallSeg arc = FreefallSeg.Between(_times[j - 1], _pos[j - 1], _times[j], cand, _g);
            Vector3 n = Normal(vIn, arc.v0);
            float bounce = Math.Min(-Dot(vIn, n), Dot(arc.v0, n)) / MinBounceMargin;
            if (quick && bounce < 1f)
            {
                return bounce;
            }

            int first = FirstRecent(_times, j);

            // The new pad must not land on a pad that is already there (a leap out and back lands on top).
            float nearest = float.MaxValue;
            for (int i = first; i < j; i++)
            {
                nearest = Math.Min(nearest, Distance(cand, _pos[i]));
                for (int m = 0; m < _rotCos.Length; m++)
                {
                    nearest = Math.Min(nearest, Distance(cand, Rotate(_pos[i], m)));
                }
            }

            float separation = nearest / (PadSeparationInPadRadii * _pr);
            float score = Math.Min(bounce, separation);
            if (quick && score < 1f)
            {
                return score;
            }

            // The arc to the candidate against the pads it has just left (pad j-1 uses this candidate's normal).
            float minGap = float.MaxValue;
            double span = _times[j] - _times[j - 1];
            int steps = Math.Max(1, (int)Math.Ceiling(span / LocalSampleStep));
            for (int step = 0; step <= steps; step++)
            {
                double t = Math.Min(_times[j - 1] + step * (double)LocalSampleStep, _times[j]);
                Vector3 p = arc.PositionAt(t);
                for (int i = first; i < j; i++)
                {
                    Vector3 ni = i == j - 1 ? n : _normals[i];
                    if (!(i == j - 1 && t - _times[i] < FreefallClearance.OwnHitWindow))
                    {
                        minGap = Math.Min(minGap, Gap(p, _pos[i], ni));
                    }

                    for (int m = 0; m < _rotCos.Length; m++)
                    {
                        minGap = Math.Min(minGap, Gap(p, Rotate(_pos[i], m), RotateDir(ni, m)));
                    }
                }
            }

            // The other direction: pad j-1 (its normal now fixed by this candidate) must not sit in the arcs that
            // flew before it. A tilted pad is a tall wall, and it can stand where the marble already was.
            for (int a = Math.Max(1, first); a < j; a++)
            {
                FreefallSeg prior = _arcs[a];
                int priorSteps = Math.Max(1, (int)Math.Ceiling((prior.t1 - prior.t0) / LocalSampleStep));
                for (int step = 0; step <= priorSteps; step++)
                {
                    double t = Math.Min(prior.t0 + step * (double)LocalSampleStep, prior.t1);
                    Vector3 p = prior.PositionAt(t);
                    if (!(a == j - 1 && _times[j - 1] - t < FreefallClearance.OwnHitWindow))
                    {
                        minGap = Math.Min(minGap, Gap(p, _pos[j - 1], n));
                    }

                    for (int m = 0; m < _rotCos.Length; m++)
                    {
                        minGap = Math.Min(minGap, Gap(p, Rotate(_pos[j - 1], m), RotateDir(n, m)));
                    }
                }
            }

            score = Math.Min(score, minGap / LocalClearance);

            if (j == _count - 1 && _exitScorer != null && (!quick || score >= 1f))
            {
                _normals[j - 1] = n;
                _pos[j] = cand;
                score = Math.Min(score, _exitScorer(_pos, _normals, _times, cand, arc.EndVelocity));
            }

            return score;
        }

        /// <summary>Marble surface to the disc pad whose contact point is <paramref name="contact"/>.</summary>
        float Gap(Vector3 p, Vector3 contact, Vector3 normal)
        {
            float cx = contact.x - normal.x * _mr;
            float cy = contact.y - normal.y * _mr;
            float cz = contact.z - normal.z * _mr;
            return FreefallClearance.DiscDistance(p.x - cx, p.y - cy, p.z - cz, normal.x, normal.y, normal.z, _pr) - _mr;
        }

        Vector3 Rotate(Vector3 v, int m)
        {
            return new Vector3(v.x * _rotCos[m] - v.z * _rotSin[m], v.y, v.x * _rotSin[m] + v.z * _rotCos[m]);
        }

        Vector3 RotateDir(Vector3 v, int m)
        {
            return Rotate(v, m);
        }

        static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Index of the oldest pad still in the recent window when pad j is being placed.</summary>
        internal static int FirstRecent(double[] times, int j)
        {
            int first = j - 1;
            while (first > 0 && j - 1 - first < MaxRecent && (times[j - 1] - times[first - 1] <= RecentSeconds || j - 1 - first < MinRecent))
            {
                first--;
            }

            return first;
        }

        /// <summary>
        /// Same acceptance test as <see cref="Evaluate"/> for the world's last pad: does the exit arc leave the
        /// pad as a bounce and keep clear of the pads the marble has just been bouncing among?
        /// </summary>
        internal static float ScoreExit(
            Vector3[] pos, Vector3[] normals, double[] times, int last, FreefallSeg exit, Vector3 vIn, float mr, float pr,
            float g, float[] rotCos, float[] rotSin)
        {
            Vector3 n = Normal(vIn, exit.v0);
            float bounce = Math.Min(-Dot(vIn, n), Dot(exit.v0, n));
            float minGap = float.MaxValue;

            int first = last;
            while (first > 0 && last - first < 400 && times[last] - times[first - 1] <= 12.0)
            {
                first--;
            }

            int steps = Math.Max(1, (int)Math.Ceiling((exit.t1 - exit.t0) / LocalSampleStep));
            for (int step = 0; step <= steps; step++)
            {
                double t = Math.Min(exit.t0 + step * (double)LocalSampleStep, exit.t1);
                Vector3 p = exit.PositionAt(t);
                for (int i = first; i <= last; i++)
                {
                    if (i == last && t - times[i] < FreefallClearance.OwnHitWindow)
                    {
                        continue;
                    }

                    Vector3 ni = i == last ? n : normals[i];
                    Vector3 c = pos[i] - ni * mr;
                    float gap = FreefallClearance.DiscDistance(p.x - c.x, p.y - c.y, p.z - c.z, ni.x, ni.y, ni.z, pr) - mr;
                    if (gap < minGap)
                    {
                        minGap = gap;
                    }

                    // The other canon voices' copies of the same pad.
                    for (int m = 0; rotCos != null && m < rotCos.Length; m++)
                    {
                        Vector3 rc = RotateBy(c, rotCos[m], rotSin[m]);
                        Vector3 rn = RotateBy(ni, rotCos[m], rotSin[m]);
                        float rgap = FreefallClearance.DiscDistance(p.x - rc.x, p.y - rc.y, p.z - rc.z, rn.x, rn.y, rn.z, pr) - mr;
                        if (rgap < minGap)
                        {
                            minGap = rgap;
                        }
                    }
                }
            }

            // The last pad itself (its normal now fixed by the exit) must not stand in the arcs that flew into it.
            Vector3 lastCentre = pos[last] - n * mr;
            for (int a = Math.Max(1, first); a <= last; a++)
            {
                FreefallSeg prior = FreefallSeg.Between(times[a - 1], pos[a - 1], times[a], pos[a], g);
                int priorSteps = Math.Max(1, (int)Math.Ceiling((prior.t1 - prior.t0) / LocalSampleStep));
                for (int step = 0; step <= priorSteps; step++)
                {
                    double t = Math.Min(prior.t0 + step * (double)LocalSampleStep, prior.t1);
                    if (a == last && times[last] - t < FreefallClearance.OwnHitWindow)
                    {
                        continue;
                    }

                    Vector3 p = prior.PositionAt(t);
                    float gap = FreefallClearance.DiscDistance(
                        p.x - lastCentre.x, p.y - lastCentre.y, p.z - lastCentre.z, n.x, n.y, n.z, pr) - mr;
                    if (gap < minGap)
                    {
                        minGap = gap;
                    }
                }
            }

            return Math.Min(bounce / MinBounceMargin, minGap / LocalClearance);
        }

        internal static Vector3 RotateBy(Vector3 v, float cos, float sin)
        {
            return new Vector3(v.x * cos - v.z * sin, v.y, v.x * sin + v.z * cos);
        }

        static float Dot(Vector3 a, Vector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }
    }
}
