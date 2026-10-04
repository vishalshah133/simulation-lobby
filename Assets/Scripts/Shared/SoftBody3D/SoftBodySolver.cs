using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>Everything that decides how a soft body behaves. Filled from a format's config.</summary>
    public struct SoftBodyParameters
    {
        public float totalMass;
        public Vector3 gravity;

        /// <summary>Solver substeps per <see cref="SoftBodySolver.Step"/>. Raise before anything else when unstable.</summary>
        public int substeps;

        /// <summary>XPBD compliance of every surface edge (inverse stiffness). 0 = inextensible skin.</summary>
        public float edgeCompliance;

        /// <summary>XPBD compliance of the global volume constraint. 0 = incompressible, like a water balloon.</summary>
        public float volumeCompliance;

        /// <summary>Target volume as a multiple of the rest volume. 1 = keep its size.</summary>
        public float pressure;

        /// <summary>
        /// When true the body is simulated as a true rigid body (position, rotation, linear and angular
        /// velocity) instead of as particles — see <see cref="SoftBodySolver"/>'s remarks for why this
        /// is a separate path rather than infinitely stiff shape matching. The particles still exist
        /// and follow it, so views and metrics work unchanged.
        /// </summary>
        public bool rigid;

        /// <summary>
        /// Bounciness, 0 = lands dead, 1 = perfectly elastic. The rigid path applies it per contact
        /// (glass on metal ≈ 0.3). The soft path applies it to the body as a whole: while touching
        /// anything, the centre of mass may leave the contact no faster than this × the speed it arrived
        /// at. Without that cap a stiff shell around an incompressible volume is a rubber ball — it
        /// rebounded ~70% of its drop height in testing, and no amount of deformation damping helped,
        /// because the energy comes back through the whole body's motion rather than as jiggle.
        /// </summary>
        public float restitution;

        /// <summary>
        /// How quickly the body pulls back toward its rest shape, per second (0 = never — pure
        /// membrane). Expressed per second rather than per substep so the feel does not change when the
        /// substep count does.
        /// </summary>
        public float shapeMatchingRate;

        /// <summary>Damping of the body's overall motion, per second. Keep tiny: this is air, not glue.</summary>
        public float airDamping;

        /// <summary>
        /// Damping of deformation, per second: how quickly motion that isn't rigid (jiggle, ringing,
        /// the rebound of a squashed body) dies out. Separate from <see cref="airDamping"/> so a jelly
        /// can wobble while still falling and tumbling at full speed. Unused by the rigid path, which
        /// has no deformation.
        /// </summary>
        public float wobbleDamping;

        /// <summary>Distance particles are kept from collider surfaces. About half the particle spacing.</summary>
        public float collisionThickness;
    }

    /// <summary>
    /// Extended position-based dynamics (XPBD) soft body over a closed triangle surface: stretch
    /// constraints on every edge, one global volume constraint, and shape matching toward the rest
    /// shape. One solver covers the whole range from rigid glass to jelly by changing
    /// <see cref="SoftBodyParameters"/> alone.
    /// </summary>
    /// <remarks>
    /// Plain single-threaded C# over fixed-order arrays: the same inputs produce the same floats on
    /// the same machine, which is what lets a take be re-rendered. Do not move this to Burst or jobs
    /// without <c>FloatMode.Deterministic</c> — fast-math reorders additions and breaks that.
    /// <para>
    /// Collisions are against analytic <see cref="SdfCollider"/>s, per particle. A sharp spike can
    /// slip <i>between</i> particles, so each <see cref="AddPointGuard"/> sphere is also tested
    /// against every triangle.
    /// </para>
    /// <para>
    /// A fully rigid body (<see cref="SoftBodyParameters.rigid"/>) takes a separate path: XPBD rigid
    /// body dynamics (Müller et al. 2020), where a contact correction is weighted by the body's mass
    /// and inertia at the contact point. Rigidity by shape matching alone does not work — a push on
    /// one particle of ~900 moves the best-fit pose by 1/900th, so the body barely feels the spike,
    /// sinks onto it, and is then ejected at tens of metres per second.
    /// </para>
    /// </remarks>
    public sealed class SoftBodySolver
    {
        readonly Vector3[] _x;
        readonly Vector3[] _previousPositions;
        readonly Vector3[] _prev;
        readonly Vector3[] _v;
        readonly Vector3[] _rest;
        readonly Vector3[] _grad;
        readonly bool[] _inContact;
        readonly bool[] _touching;
        readonly Vector3[] _contactNormal;
        readonly int[] _triangles;
        readonly int[] _edgeA;
        readonly int[] _edgeB;
        readonly float[] _edgeRest;
        readonly float _invMass;
        readonly float _restVolume;
        readonly List<SdfCollider> _colliders = new List<SdfCollider>();
        readonly List<SdfCollider> _nearby = new List<SdfCollider>();
        float _rigidRadius;
        readonly List<Vector4> _guards = new List<Vector4>();

        SoftBodyParameters _p;
        Quaternion _shapeRotation = Quaternion.identity;
        bool _bodyInContact;
        float _arrivalSpeed;
        Vector3 _freeVelocity;

        // Rigid path state: body pose maps rest offsets to world (x_i = _rbPosition + _rbRotation * _rest[i]).
        Vector3 _rbPosition;
        Quaternion _rbRotation = Quaternion.identity;
        Vector3 _rbVelocity;
        Vector3 _rbAngularVelocity;
        Matrix3 _rbInverseInertia;
        readonly Vector3[] _rigidPoints;
        readonly bool[] _rigidInContact;
        readonly List<RigidContact> _rbContacts = new List<RigidContact>();

        struct RigidContact
        {
            public Vector3 normal;
            public Vector3 offset; // contact point relative to the centre of mass, world space
            public float lambda; // normal correction (mass-weighted) — weights the average contact
            public float depth; // penetration resolved (m) — friction limits use this, never λ, which scales with mass
            public float approachSpeed; // normal speed into the surface before the substep
            public float friction;
        }

        public SoftBodySolver(SoftBodyShape shape, Vector3 position, Quaternion rotation, SoftBodyParameters parameters)
        {
            int n = shape.positions.Length;
            _x = new Vector3[n];
            _prev = new Vector3[n];
            _v = new Vector3[n];
            _rest = new Vector3[n];
            _grad = new Vector3[n];
            _inContact = new bool[n];
            _touching = new bool[n];
            _contactNormal = new Vector3[n];
            _triangles = (int[])shape.triangles.Clone();
            _p = parameters;
            _p.substeps = Mathf.Max(1, _p.substeps);
            _invMass = n / Mathf.Max(1e-4f, _p.totalMass);

            for (int i = 0; i < n; i++)
            {
                _x[i] = position + rotation * shape.positions[i];
            }

            _previousPositions = (Vector3[])_x.Clone();
            Vector3 centroid = Average(_x);
            for (int i = 0; i < n; i++)
            {
                _rest[i] = _x[i] - centroid;
            }

            // Unique edges, in first-seen order so the constraint order (and therefore the result)
            // never depends on hashing.
            var seen = new HashSet<long>();
            var a = new List<int>();
            var b = new List<int>();
            for (int t = 0; t < _triangles.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int i0 = _triangles[t + k];
                    int i1 = _triangles[t + (k + 1) % 3];
                    long key = i0 < i1 ? ((long)i0 << 32) | (uint)i1 : ((long)i1 << 32) | (uint)i0;
                    if (seen.Add(key))
                    {
                        a.Add(i0);
                        b.Add(i1);
                    }
                }
            }

            _edgeA = a.ToArray();
            _edgeB = b.ToArray();
            _edgeRest = new float[_edgeA.Length];
            for (int e = 0; e < _edgeA.Length; e++)
            {
                _edgeRest[e] = Vector3.Distance(_x[_edgeA[e]], _x[_edgeB[e]]);
            }

            _restVolume = Volume();
            VolumeRatio = 1f;
            Centroid = centroid;

            // Rigid path: centre of mass at the centroid, inertia from the particles as point masses.
            // Those sit on the surface, so this is a hollow shell's inertia; scaled toward a solid
            // body's (a solid sphere has 3/5 of a shell's), since the object reads as solid glass.
            _rbPosition = centroid;
            float particleMass = _p.totalMass / n;
            var inertia = new Matrix3();
            for (int i = 0; i < n; i++)
            {
                Vector3 r = _rest[i];
                float rr = Vector3.Dot(r, r);
                inertia.m00 += particleMass * (rr - r.x * r.x);
                inertia.m11 += particleMass * (rr - r.y * r.y);
                inertia.m22 += particleMass * (rr - r.z * r.z);
                inertia.m01 -= particleMass * r.x * r.y;
                inertia.m02 -= particleMass * r.x * r.z;
                inertia.m12 -= particleMass * r.y * r.z;
            }

            inertia.m10 = inertia.m01;
            inertia.m20 = inertia.m02;
            inertia.m21 = inertia.m12;
            _rbInverseInertia = inertia.Scaled(0.6f).Inverse();

            // Rigid contact samples: particles + edge midpoints + triangle centres (~6× the particles,
            // half the spacing). Only the rigid path uses them, where testing points is cheap.
            var samples = new List<Vector3>(_rest);
            for (int e = 0; e < _edgeA.Length; e++)
            {
                samples.Add((_rest[_edgeA[e]] + _rest[_edgeB[e]]) * 0.5f);
            }

            for (int t = 0; t < _triangles.Length; t += 3)
            {
                samples.Add((_rest[_triangles[t]] + _rest[_triangles[t + 1]] + _rest[_triangles[t + 2]]) / 3f);
            }

            _rigidPoints = samples.ToArray();
            _rigidInContact = new bool[_rigidPoints.Length];
            for (int i = 0; i < n; i++)
            {
                _rigidRadius = Mathf.Max(_rigidRadius, _rest[i].magnitude);
            }
        }

        public int ParticleCount => _x.Length;

        /// <summary>Live particle positions in world space. Read-only by contract — views copy, never write.</summary>
        public Vector3[] Positions => _x;

        /// <summary>Positions before the most recent <see cref="Step"/> — for render interpolation only.</summary>
        public Vector3[] PreviousPositions => _previousPositions;

        /// <summary>Steps taken. 0 = still at the initial pose (nothing to interpolate from).</summary>
        public int StepCount { get; private set; }

        public int[] Triangles => _triangles;

        public float RestVolume => _restVolume;

        public SoftBodyParameters Parameters => _p;

        // ---- Metrics, refreshed every Step. Presentation reads these and nothing else. ----

        public Vector3 Centroid { get; private set; }

        public Vector3 CentroidVelocity { get; private set; }

        /// <summary>Mean squared speed of particles relative to the centre — how much it is jiggling.</summary>
        public float WobbleEnergy { get; private set; }

        /// <summary>Mean absolute edge strain (|length / rest − 1|). 0 at rest shape.</summary>
        public float SurfaceStrain { get; private set; }

        /// <summary>Current volume over rest volume.</summary>
        public float VolumeRatio { get; private set; }

        /// <summary>Particles that touched a collider this step having not touched one the substep before.</summary>
        public int NewContacts { get; private set; }

        /// <summary>Fastest approach speed among this step's new contacts, m/s.</summary>
        public float PeakImpactSpeed { get; private set; }

        /// <summary>Particles currently touching any collider.</summary>
        public int ContactCount { get; private set; }

        public void AddCollider(SdfCollider collider)
        {
            _colliders.Add(collider);
        }

        /// <summary>
        /// A sphere tested against every triangle, not just every particle — for sharp features (a
        /// spike's tip) smaller than the spacing between particles.
        /// </summary>
        public void AddPointGuard(Vector3 center, float radius)
        {
            _guards.Add(new Vector4(center.x, center.y, center.z, radius));
        }

        /// <summary>Advance by <paramref name="dt"/> seconds in <see cref="SoftBodyParameters.substeps"/> substeps.</summary>
        public void Step(float dt)
        {
            int substeps = _p.substeps;
            float h = dt / substeps;
            float shapeStiffness = _p.rigid ? 1f : _p.shapeMatchingRate > 0f ? 1f - Mathf.Exp(-_p.shapeMatchingRate * h) : 0f;

            // Snapshot for render interpolation: a view blends this pose into the new one across the
            // frames between fixed steps. Copied, never read by the solver, so it can't affect a run.
            System.Array.Copy(_x, _previousPositions, _x.Length);
            StepCount++;

            NewContacts = 0;
            PeakImpactSpeed = 0f;

            if (_p.rigid)
            {
                for (int s = 0; s < substeps; s++)
                {
                    StepRigid(h);
                }

                UpdateMetrics();
                return;
            }

            // Hot loops below are written in plain floats rather than Vector3 operators. Identical
            // maths; in the Unity editor's Mono (Debug code optimisation by default) every Vector3
            // operator is a real method call, and the operator form ran ~10× slower than the budget
            // a 60Hz step allows — the "choppy" playtest note.
            float gx = _p.gravity.x * h;
            float gy = _p.gravity.y * h;
            float gz = _p.gravity.z * h;
            for (int s = 0; s < substeps; s++)
            {
                bool last = s == substeps - 1;
                for (int i = 0; i < _x.Length; i++)
                {
                    Vector3 x = _x[i];
                    Vector3 v = _v[i];
                    _prev[i] = x;
                    v.x += gx;
                    v.y += gy;
                    v.z += gz;
                    _v[i] = v;
                    _x[i] = new Vector3(x.x + v.x * h, x.y + v.y * h, x.z + v.z * h);
                }

                SolveEdges(h, last);
                SolveVolume(h);
                if (shapeStiffness > 0f)
                {
                    SolveShapeMatching(shapeStiffness);
                }

                SolveCollisions(h);
                SolveGuards();

                float inverseH = 1f / h;
                for (int i = 0; i < _x.Length; i++)
                {
                    Vector3 x = _x[i];
                    Vector3 p = _prev[i];
                    float vx = (x.x - p.x) * inverseH;
                    float vy = (x.y - p.y) * inverseH;
                    float vz = (x.z - p.z) * inverseH;

                    // Contact velocity pass. A particle pushed out of a surface this substep would
                    // otherwise leave with the push as velocity: energy injected at every contact, which
                    // is both the buzzing jitter (pushed out, pulled back in by its neighbours, pushed out
                    // again) and a super-ball rebound. Touching particles may slide but not separate.
                    if (_touching[i])
                    {
                        Vector3 n = _contactNormal[i];
                        float separating = vx * n.x + vy * n.y + vz * n.z;
                        if (separating > 0f)
                        {
                            vx -= n.x * separating;
                            vy -= n.y * separating;
                            vz -= n.z * separating;
                        }
                    }

                    _v[i] = new Vector3(vx, vy, vz);
                }

                LimitRebound();
                Damp(h);
            }

            UpdateMetrics();
        }

        void SolveEdges(float h, bool measure)
        {
            float alpha = _p.edgeCompliance / (h * h);
            float w = _invMass;
            float strainSum = 0f;
            float inverseDenominator = 1f / (w + w + alpha);
            for (int e = 0; e < _edgeA.Length; e++)
            {
                int ia = _edgeA[e];
                int ib = _edgeB[e];
                Vector3 a = _x[ia];
                Vector3 b = _x[ib];
                float dx = b.x - a.x;
                float dy = b.y - a.y;
                float dz = b.z - a.z;
                float length = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                if (length < 1e-9f)
                {
                    continue;
                }

                float c = length - _edgeRest[e];
                if (measure)
                {
                    strainSum += Mathf.Abs(c) / _edgeRest[e];
                }

                float scale = -c * inverseDenominator / length * w;
                dx *= scale;
                dy *= scale;
                dz *= scale;
                _x[ia] = new Vector3(a.x - dx, a.y - dy, a.z - dz);
                _x[ib] = new Vector3(b.x + dx, b.y + dy, b.z + dz);
            }

            if (measure)
            {
                SurfaceStrain = strainSum / Mathf.Max(1, _edgeA.Length);
            }
        }

        void SolveVolume(float h)
        {
            System.Array.Clear(_grad, 0, _grad.Length);

            // Relative to the centroid, accumulated in double. The per-triangle terms are triple
            // products of positions; measured from the world origin (the body sits metres up) they are
            // tens of times larger than the volume they sum to, and float32 cancellation leaves ~0.5%
            // noise in it. The solver then "corrects" that noise every substep, which is metres per
            // second of jitter — enough to fling a body off the plinth.
            Vector3 origin = Average(_x);
            float ox = origin.x;
            float oy = origin.y;
            float oz = origin.z;
            double volume = 0.0;
            for (int t = 0; t < _triangles.Length; t += 3)
            {
                int ia = _triangles[t];
                int ib = _triangles[t + 1];
                int ic = _triangles[t + 2];
                Vector3 pa = _x[ia];
                Vector3 pb = _x[ib];
                Vector3 pc = _x[ic];
                float ax = pa.x - ox, ay = pa.y - oy, az = pa.z - oz;
                float bx = pb.x - ox, by = pb.y - oy, bz = pb.z - oz;
                float cx = pc.x - ox, cy = pc.y - oy, cz = pc.z - oz;

                // b×c, c×a, a×b — the gradients of a·(b×c) with respect to a, b and c.
                float bcx = by * cz - bz * cy, bcy = bz * cx - bx * cz, bcz = bx * cy - by * cx;
                float cax = cy * az - cz * ay, cay = cz * ax - cx * az, caz = cx * ay - cy * ax;
                float abx = ay * bz - az * by, aby = az * bx - ax * bz, abz = ax * by - ay * bx;
                volume += ax * bcx + ay * bcy + az * bcz;

                Vector3 ga = _grad[ia];
                _grad[ia] = new Vector3(ga.x + bcx, ga.y + bcy, ga.z + bcz);
                Vector3 gb = _grad[ib];
                _grad[ib] = new Vector3(gb.x + cax, gb.y + cay, gb.z + caz);
                Vector3 gc = _grad[ic];
                _grad[ic] = new Vector3(gc.x + abx, gc.y + aby, gc.z + abz);
            }

            volume /= 6.0;
            VolumeRatio = (float)(volume / _restVolume);

            float constraint = (float)(volume - _restVolume * _p.pressure);
            float denominator = _p.volumeCompliance / (h * h);
            const float sixth = 1f / 6f;
            for (int i = 0; i < _grad.Length; i++)
            {
                Vector3 g = _grad[i];
                g = new Vector3(g.x * sixth, g.y * sixth, g.z * sixth);
                _grad[i] = g;
                denominator += _invMass * (g.x * g.x + g.y * g.y + g.z * g.z);
            }

            if (denominator < 1e-12f)
            {
                return;
            }

            float step = -constraint / denominator * _invMass;
            for (int i = 0; i < _x.Length; i++)
            {
                Vector3 x = _x[i];
                Vector3 g = _grad[i];
                _x[i] = new Vector3(x.x + g.x * step, x.y + g.y * step, x.z + g.z * step);
            }
        }

        void SolveShapeMatching(float stiffness)
        {
            Vector3 centroid = Average(_x);
            float cx = centroid.x;
            float cy = centroid.y;
            float cz = centroid.z;

            // A = Σ (x − c) qᵀ, kept as its three columns.
            float a00 = 0f, a10 = 0f, a20 = 0f, a01 = 0f, a11 = 0f, a21 = 0f, a02 = 0f, a12 = 0f, a22 = 0f;
            for (int i = 0; i < _x.Length; i++)
            {
                Vector3 x = _x[i];
                Vector3 q = _rest[i];
                float px = x.x - cx, py = x.y - cy, pz = x.z - cz;
                a00 += px * q.x; a10 += py * q.x; a20 += pz * q.x;
                a01 += px * q.y; a11 += py * q.y; a21 += pz * q.y;
                a02 += px * q.z; a12 += py * q.z; a22 += pz * q.z;
            }

            _shapeRotation = ExtractRotation(new Vector3(a00, a10, a20), new Vector3(a01, a11, a21),
                new Vector3(a02, a12, a22), _shapeRotation);

            // Rotation as a matrix once, so each goal is nine multiply-adds instead of a quaternion op.
            Vector3 r0 = _shapeRotation * Vector3.right;
            Vector3 r1 = _shapeRotation * Vector3.up;
            Vector3 r2 = _shapeRotation * Vector3.forward;
            for (int i = 0; i < _x.Length; i++)
            {
                Vector3 q = _rest[i];
                Vector3 x = _x[i];
                float goalX = cx + r0.x * q.x + r1.x * q.y + r2.x * q.z;
                float goalY = cy + r0.y * q.x + r1.y * q.y + r2.y * q.z;
                float goalZ = cz + r0.z * q.x + r1.z * q.y + r2.z * q.z;
                _x[i] = new Vector3(x.x + (goalX - x.x) * stiffness, x.y + (goalY - x.y) * stiffness,
                    x.z + (goalZ - x.z) * stiffness);
            }
        }

        /// <summary>
        /// Rotational part of a deformation matrix, warm-started from the last result. Müller et al.,
        /// "A Robust Method to Extract the Rotational Part of Deformations" (2016): stable through
        /// inversion, where a polar decomposition via SVD would flip.
        /// </summary>
        static Quaternion ExtractRotation(Vector3 a0, Vector3 a1, Vector3 a2, Quaternion q)
        {
            for (int iteration = 0; iteration < 12; iteration++)
            {
                Vector3 r0 = q * Vector3.right;
                Vector3 r1 = q * Vector3.up;
                Vector3 r2 = q * Vector3.forward;
                Vector3 omega = Vector3.Cross(r0, a0) + Vector3.Cross(r1, a1) + Vector3.Cross(r2, a2);
                float scale = 1f / (Mathf.Abs(Vector3.Dot(r0, a0) + Vector3.Dot(r1, a1) + Vector3.Dot(r2, a2)) + 1e-9f);
                omega *= scale;
                float angle = omega.magnitude;
                if (angle < 1e-9f)
                {
                    break;
                }

                q = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, omega / angle) * q;
                q = Quaternion.Normalize(q);
            }

            return q;
        }

        /// <summary>
        /// Broad phase: which colliders could touch the body this substep at all. A collider whose
        /// surface is farther from the body's centre than the body's bounding radius (plus margin)
        /// can't reach any particle, so it's skipped for all of them — the floor, for instance, is
        /// metres below a body sitting on a tall plinth. SDF distances never overestimate (the CSG
        /// subtraction included), so a skip is always safe.
        /// </summary>
        int GatherNearbyColliders(Vector3 centre, float radius)
        {
            _nearby.Clear();
            float reach = radius + _p.collisionThickness + 0.05f;
            for (int c = 0; c < _colliders.Count; c++)
            {
                if (_colliders[c].Distance(centre) < reach)
                {
                    _nearby.Add(_colliders[c]);
                }
            }

            return _nearby.Count;
        }

        float BoundingRadius(Vector3 centre)
        {
            float max = 0f;
            for (int i = 0; i < _x.Length; i++)
            {
                Vector3 x = _x[i];
                float dx = x.x - centre.x, dy = x.y - centre.y, dz = x.z - centre.z;
                float d = dx * dx + dy * dy + dz * dz;
                if (d > max)
                {
                    max = d;
                }
            }

            return Mathf.Sqrt(max);
        }

        void SolveCollisions(float h)
        {
            float thickness = _p.collisionThickness;
            int contacts = 0;
            Vector3 centre = Average(_x);
            if (GatherNearbyColliders(centre, BoundingRadius(centre)) == 0)
            {
                for (int i = 0; i < _x.Length; i++)
                {
                    _inContact[i] = false;
                    _touching[i] = false;
                }

                ContactCount = 0;
                return;
            }

            for (int i = 0; i < _x.Length; i++)
            {
                bool touching = false;
                for (int c = 0; c < _nearby.Count; c++)
                {
                    SdfCollider collider = _nearby[c];
                    float distance = collider.Distance(_x[i]);
                    if (distance >= thickness)
                    {
                        continue;
                    }

                    Vector3 normal = collider.Normal(_x[i]);
                    float penetration = thickness - distance;
                    Vector3 displacement = _x[i] - _prev[i];

                    if (!_inContact[i])
                    {
                        float approach = -Vector3.Dot(displacement, normal) / h;
                        if (approach > 0f)
                        {
                            NewContacts++;
                            PeakImpactSpeed = Mathf.Max(PeakImpactSpeed, approach);
                        }
                    }

                    if (collider.pierceable)
                    {
                        // Impaled: part the skin sideways and let the body slide along the collider,
                        // gripped by drag rather than held off by the surface.
                        normal = collider.PierceNormal(_x[i]);
                        _x[i] += normal * penetration;
                        float travelY = _x[i].y - _prev[i].y;
                        _x[i].y -= travelY * (1f - Mathf.Exp(-collider.pierceDrag * h));
                        touching = true;
                        _contactNormal[i] = normal;
                        continue;
                    }

                    _x[i] += normal * penetration;

                    // Coulomb friction on this substep's tangential travel: stick below the static
                    // limit, otherwise slide with the dynamic coefficient.
                    displacement = _x[i] - _prev[i];
                    Vector3 tangential = displacement - normal * Vector3.Dot(displacement, normal);
                    float slip = tangential.magnitude;
                    if (slip > 1e-9f)
                    {
                        if (slip < collider.staticFriction * penetration)
                        {
                            _x[i] -= tangential;
                        }
                        else
                        {
                            _x[i] -= tangential * Mathf.Min(collider.dynamicFriction * penetration / slip, 1f);
                        }
                    }

                    touching = true;
                    _contactNormal[i] = normal;
                }

                _inContact[i] = touching;
                _touching[i] = touching;
                if (touching)
                {
                    contacts++;
                }
            }

            ContactCount = contacts;
        }

        void SolveGuards()
        {
            float thickness = _p.collisionThickness;
            for (int g = 0; g < _guards.Count; g++)
            {
                Vector3 center = _guards[g];
                float radius = _guards[g].w;
                float reach = radius + thickness;

                for (int t = 0; t < _triangles.Length; t += 3)
                {
                    int ia = _triangles[t];
                    int ib = _triangles[t + 1];
                    int ic = _triangles[t + 2];
                    if (!GuardPush(center, reach, _x[ia], _x[ib], _x[ic], out Vector3 push, out _, out float u, out float v,
                            out float w))
                    {
                        continue;
                    }

                    // Split the push across the corners by barycentric weight (equal masses), scaled so
                    // the closest point itself moves by exactly the push.
                    float weightSquares = u * u + v * v + w * w;
                    if (weightSquares < 1e-9f)
                    {
                        continue;
                    }

                    _x[ia] += push * (u / weightSquares);
                    _x[ib] += push * (v / weightSquares);
                    _x[ic] += push * (w / weightSquares);

                    // These corners are touching the guard now: same no-separation rule as a contact.
                    Vector3 normal = push.normalized;
                    _touching[ia] = _touching[ib] = _touching[ic] = true;
                    _contactNormal[ia] = _contactNormal[ib] = _contactNormal[ic] = normal;
                }
            }
        }

        /// <summary>
        /// How far triangle abc must move to clear a guard sphere of radius <paramref name="reach"/>
        /// (radius + thickness). False when it doesn't touch.
        /// </summary>
        static bool GuardPush(Vector3 center, float reach, Vector3 a, Vector3 b, Vector3 c, out Vector3 push,
            out Vector3 closest, out float u, out float v, out float w)
        {
            push = Vector3.zero;
            closest = Vector3.zero;
            u = v = w = 0f;

            // Cheap reject before any real work: a triangle nowhere near the guard.
            Vector3 mid = (a + b + c) / 3f;
            float span = Mathf.Max((a - mid).sqrMagnitude, Mathf.Max((b - mid).sqrMagnitude, (c - mid).sqrMagnitude));
            float limit = reach + Mathf.Sqrt(span) + 0.1f;
            if ((center - mid).sqrMagnitude > limit * limit)
            {
                return false;
            }

            Vector3 normal = Vector3.Cross(b - a, c - a);
            float normalLength = normal.magnitude;
            if (normalLength < 1e-12f)
            {
                return false;
            }

            normal /= normalLength;
            closest = ClosestPointOnTriangle(center, a, b, c, out u, out v, out w);

            // The guard lives outside the body, i.e. on each triangle's outward side. A guard behind
            // a triangle (but not by more than a sliver) has poked through it.
            float side = Vector3.Dot(center - closest, normal);
            if (side < reach && side > -reach - 0.08f && IsInterior(u, v, w))
            {
                push = -normal * (reach - side);
                return true;
            }

            Vector3 offset = closest - center;
            float distance = offset.magnitude;
            if (side <= 0f || distance >= reach || distance < 1e-9f)
            {
                return false;
            }

            push = offset / distance * (reach - distance);
            return true;
        }

        static bool IsInterior(float u, float v, float w) => u > 1e-4f && v > 1e-4f && w > 1e-4f;

        // ------------------------------------------------------------------ rigid path

        /// <summary>
        /// One substep of XPBD rigid-body dynamics: integrate the pose, resolve contacts as positional
        /// corrections weighted by mass and inertia at the contact point, derive velocities from the
        /// pose change, then a velocity pass for restitution and sliding friction.
        /// </summary>
        void StepRigid(float h)
        {
            float inverseMass = 1f / Mathf.Max(1e-4f, _p.totalMass);
            Vector3 previousPosition = _rbPosition;
            Quaternion previousRotation = _rbRotation;
            Vector3 startVelocity = _rbVelocity;
            Vector3 startAngular = _rbAngularVelocity;

            _rbVelocity += _p.gravity * h;
            _rbPosition += _rbVelocity * h;
            _rbRotation = Rotate(_rbRotation, _rbAngularVelocity * h);
            _rbContacts.Clear();

            float thickness = _p.collisionThickness;
            int contacts = 0;
            GatherNearbyColliders(_rbPosition, _rigidRadius);
            bool anyThin = false;
            for (int c = 0; c < _nearby.Count; c++)
            {
                anyThin |= _nearby[c].thin;
            }

            // Particles are tested against every nearby collider; the dense extra samples (edge
            // midpoints, triangle centres) only against thin ones — a brass lip can sit between
            // particles and let the body slip through, a plinth top can't.
            int sampleCount = _nearby.Count == 0 ? 0 : anyThin ? _rigidPoints.Length : _x.Length;
            for (int i = 0; i < sampleCount; i++)
            {
                bool dense = i >= _x.Length;
                bool touching = false;
                for (int c = 0; c < _nearby.Count; c++)
                {
                    SdfCollider collider = _nearby[c];
                    if (dense && !collider.thin)
                    {
                        continue;
                    }

                    Vector3 point = _rbPosition + _rbRotation * _rigidPoints[i];
                    float distance = collider.Distance(point);
                    if (distance >= thickness)
                    {
                        continue;
                    }

                    Vector3 normal = collider.Normal(point);
                    float approach = -Vector3.Dot(normal, startVelocity + Vector3.Cross(startAngular, point - _rbPosition));
                    if (!_rigidInContact[i] && approach > 0f)
                    {
                        NewContacts++;
                        PeakImpactSpeed = Mathf.Max(PeakImpactSpeed, approach);
                    }

                    float lambda = RigidCorrection(point - _rbPosition, normal, thickness - distance, inverseMass);

                    // Static friction: undo this point's tangential travel if it is under the stick limit.
                    point = _rbPosition + _rbRotation * _rigidPoints[i];
                    Vector3 previousPoint = previousPosition + previousRotation * _rigidPoints[i];
                    Vector3 travel = point - previousPoint;
                    Vector3 tangential = travel - normal * Vector3.Dot(travel, normal);
                    float slip = tangential.magnitude;
                    if (slip > 1e-9f && slip < collider.staticFriction * (thickness - distance))
                    {
                        RigidCorrection(point - _rbPosition, -tangential / slip, slip, inverseMass);
                    }

                    _rbContacts.Add(new RigidContact
                    {
                        normal = normal,
                        offset = point - _rbPosition,
                        lambda = lambda,
                        depth = thickness - distance,
                        approachSpeed = approach,
                        friction = collider.dynamicFriction
                    });
                    touching = true;
                }

                _rigidInContact[i] = touching;
                if (touching)
                {
                    contacts++;
                }
            }

            for (int i = sampleCount; i < _rigidPoints.Length; i++)
            {
                _rigidInContact[i] = false;
            }

            ContactCount = contacts;
            RigidGuards(inverseMass, startVelocity, startAngular);

            // Velocities from the pose change.
            _rbVelocity = (_rbPosition - previousPosition) / h;
            Quaternion delta = _rbRotation * Conjugate(previousRotation);
            _rbAngularVelocity = new Vector3(delta.x, delta.y, delta.z) * (2f / h * (delta.w >= 0f ? 1f : -1f));

            // Velocity pass: sliding friction and restitution, applied ONCE at the λ-weighted average
            // contact. Applying them contact by contact looked right on a flat floor and broke on a
            // ring-shaped lip: each tilted contact added its own bounce and the processing order made
            // it lopsided — a centred drop at 4.4 m/s left at 2.6 m/s up and 1.1 m/s sideways.
            // Restitution only for real impacts: a body resting on a surface arrives at gravity × h
            // every substep, and bouncing that would make it buzz.
            float lambdaSum = 0f;
            Vector3 normalSum = Vector3.zero;
            Vector3 offsetSum = Vector3.zero;
            float approachSum = 0f;
            float frictionSum = 0f;
            float maxDepth = 0f;
            for (int k = 0; k < _rbContacts.Count; k++)
            {
                RigidContact contact = _rbContacts[k];
                float weight = Mathf.Max(contact.lambda, 1e-9f);
                maxDepth = Mathf.Max(maxDepth, contact.depth);
                lambdaSum += weight;
                normalSum += contact.normal * weight;
                offsetSum += contact.offset * weight;
                approachSum += contact.approachSpeed * weight;
                frictionSum += contact.friction * weight;
            }

            if (_rbContacts.Count > 0 && normalSum.sqrMagnitude > 1e-12f)
            {
                Vector3 normal = normalSum.normalized;
                Vector3 r = offsetSum / lambdaSum;
                float approach = approachSum / lambdaSum;
                float friction = frictionSum / lambdaSum;
                float restingSpeed = 2f * _p.gravity.magnitude * h;

                Vector3 velocity = _rbVelocity + Vector3.Cross(_rbAngularVelocity, r);
                float normalSpeed = Vector3.Dot(normal, velocity);
                Vector3 tangentVelocity = velocity - normal * normalSpeed;
                Vector3 change = Vector3.zero;

                float tangentSpeed = tangentVelocity.magnitude;
                if (tangentSpeed > 1e-6f)
                {
                    // Coulomb limit: friction can remove up to μ × the normal speed the contacts took
                    // out this substep (deepest depth / h). In distance units, so mass-independent.
                    change -= tangentVelocity / tangentSpeed * Mathf.Min(friction * maxDepth / h, tangentSpeed);
                }

                float bounce = approach > restingSpeed ? _p.restitution * approach : 0f;
                if (normalSpeed < bounce)
                {
                    change += normal * (bounce - normalSpeed);
                }

                RigidImpulse(r, change, inverseMass);
            }

            _rbVelocity *= Mathf.Exp(-_p.airDamping * h);
            _rbAngularVelocity *= Mathf.Exp(-_p.airDamping * h);

            for (int i = 0; i < _x.Length; i++)
            {
                Vector3 r = _rbRotation * _rest[i];
                _x[i] = _rbPosition + r;
                _v[i] = _rbVelocity + Vector3.Cross(_rbAngularVelocity, r);
            }
        }

        /// <summary>
        /// Guard spheres against the rigid body's triangles: a few passes, each clearing the deepest
        /// overlap with one rigid correction at the closest point.
        /// </summary>
        void RigidGuards(float inverseMass, Vector3 startVelocity, Vector3 startAngular)
        {
            if (_guards.Count == 0)
            {
                return;
            }

            float thickness = _p.collisionThickness;
            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = 0; i < _x.Length; i++)
                {
                    _x[i] = _rbPosition + _rbRotation * _rest[i];
                }

                bool found = false;
                Vector3 deepestPush = Vector3.zero;
                Vector3 deepestPoint = Vector3.zero;
                for (int g = 0; g < _guards.Count; g++)
                {
                    Vector3 center = _guards[g];
                    float reach = _guards[g].w + thickness;
                    for (int t = 0; t < _triangles.Length; t += 3)
                    {
                        if (GuardPush(center, reach, _x[_triangles[t]], _x[_triangles[t + 1]], _x[_triangles[t + 2]],
                                out Vector3 push, out Vector3 closest, out _, out _, out _) &&
                            push.sqrMagnitude > deepestPush.sqrMagnitude)
                        {
                            found = true;
                            deepestPush = push;
                            deepestPoint = closest;
                        }
                    }
                }

                if (!found)
                {
                    return;
                }

                float depth = deepestPush.magnitude;
                Vector3 normal = deepestPush / depth;
                Vector3 r = deepestPoint - _rbPosition;
                float approach = -Vector3.Dot(normal, startVelocity + Vector3.Cross(startAngular, r));
                if (pass == 0 && approach > 0f)
                {
                    NewContacts++;
                    PeakImpactSpeed = Mathf.Max(PeakImpactSpeed, approach);
                }

                float lambda = RigidCorrection(r, normal, depth, inverseMass);
                _rbContacts.Add(new RigidContact
                {
                    normal = normal,
                    offset = r,
                    lambda = lambda,
                    depth = depth,
                    approachSpeed = approach,
                    friction = 0.35f
                });
            }
        }

        /// <summary>
        /// Moves the rigid pose so the point at offset <paramref name="r"/> travels
        /// <paramref name="depth"/> along <paramref name="direction"/>, split between translation and
        /// rotation by the generalised inverse mass. Returns the correction magnitude (λ).
        /// </summary>
        float RigidCorrection(Vector3 r, Vector3 direction, float depth, float inverseMass)
        {
            Vector3 rn = Vector3.Cross(r, direction);
            float w = inverseMass + Vector3.Dot(rn, WorldInverseInertia(rn));
            if (w < 1e-9f)
            {
                return 0f;
            }

            float lambda = depth / w;
            Vector3 correction = direction * lambda;
            _rbPosition += correction * inverseMass;
            _rbRotation = Rotate(_rbRotation, WorldInverseInertia(Vector3.Cross(r, correction)));
            return lambda;
        }

        /// <summary>Changes the velocity of the point at offset <paramref name="r"/> by <paramref name="change"/>.</summary>
        void RigidImpulse(Vector3 r, Vector3 change, float inverseMass)
        {
            float size = change.magnitude;
            if (size < 1e-9f)
            {
                return;
            }

            Vector3 direction = change / size;
            Vector3 rn = Vector3.Cross(r, direction);
            float w = inverseMass + Vector3.Dot(rn, WorldInverseInertia(rn));
            Vector3 impulse = direction * (size / w);
            _rbVelocity += impulse * inverseMass;
            _rbAngularVelocity += WorldInverseInertia(Vector3.Cross(r, impulse));
        }

        Vector3 WorldInverseInertia(Vector3 v)
        {
            return _rbRotation * _rbInverseInertia.Multiply(Conjugate(_rbRotation) * v);
        }

        /// <summary>q advanced by a small rotation vector θ: q + ½[θ, 0]q, renormalised.</summary>
        static Quaternion Rotate(Quaternion q, Vector3 theta)
        {
            var spin = new Quaternion(theta.x, theta.y, theta.z, 0f) * q;
            return Quaternion.Normalize(new Quaternion(q.x + 0.5f * spin.x, q.y + 0.5f * spin.y, q.z + 0.5f * spin.z,
                q.w + 0.5f * spin.w));
        }

        static Quaternion Conjugate(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);

        /// <summary>Minimal 3×3 matrix for the inertia tensor.</summary>
        struct Matrix3
        {
            public float m00, m01, m02, m10, m11, m12, m20, m21, m22;

            public Vector3 Multiply(Vector3 v) => new Vector3(
                m00 * v.x + m01 * v.y + m02 * v.z,
                m10 * v.x + m11 * v.y + m12 * v.z,
                m20 * v.x + m21 * v.y + m22 * v.z);

            public Matrix3 Scaled(float s) => new Matrix3
            {
                m00 = m00 * s, m01 = m01 * s, m02 = m02 * s,
                m10 = m10 * s, m11 = m11 * s, m12 = m12 * s,
                m20 = m20 * s, m21 = m21 * s, m22 = m22 * s
            };

            public Matrix3 Inverse()
            {
                float c00 = m11 * m22 - m12 * m21;
                float c01 = m02 * m21 - m01 * m22;
                float c02 = m01 * m12 - m02 * m11;
                float c10 = m12 * m20 - m10 * m22;
                float c11 = m00 * m22 - m02 * m20;
                float c12 = m02 * m10 - m00 * m12;
                float c20 = m10 * m21 - m11 * m20;
                float c21 = m01 * m20 - m00 * m21;
                float c22 = m00 * m11 - m01 * m10;
                float det = m00 * c00 + m01 * c10 + m02 * c20;
                float inv = Mathf.Abs(det) > 1e-12f ? 1f / det : 0f;
                return new Matrix3
                {
                    m00 = c00 * inv, m01 = c01 * inv, m02 = c02 * inv,
                    m10 = c10 * inv, m11 = c11 * inv, m12 = c12 * inv,
                    m20 = c20 * inv, m21 = c21 * inv, m22 = c22 * inv
                };
            }
        }

        /// <summary>Closest point on triangle abc to p, with its barycentric weights (Ericson, RTCD 5.1.5).</summary>
        static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out float u, out float v,
            out float w)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = p - a;
            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)
            {
                u = 1f; v = 0f; w = 0f;
                return a;
            }

            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)
            {
                u = 0f; v = 1f; w = 0f;
                return b;
            }

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float t = d1 / (d1 - d3);
                u = 1f - t; v = t; w = 0f;
                return a + ab * t;
            }

            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)
            {
                u = 0f; v = 0f; w = 1f;
                return c;
            }

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float t = d2 / (d2 - d6);
                u = 1f - t; v = 0f; w = t;
                return a + ac * t;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
            {
                float t = (d4 - d3) / (d4 - d3 + (d5 - d6));
                u = 0f; v = 1f - t; w = t;
                return b + (c - b) * t;
            }

            float denominator = 1f / (va + vb + vc);
            v = vb * denominator;
            w = vc * denominator;
            u = 1f - v - w;
            return a + ab * v + ac * w;
        }

        /// <summary>
        /// Body-level restitution for the soft path (see <see cref="SoftBodyParameters.restitution"/>):
        /// while any particle touches a surface, the centre of mass may move away from the mean contact
        /// normal no faster than restitution × the speed it arrived at. Applied to every particle equally,
        /// so it removes rebound without touching the shape or the wobble.
        /// </summary>
        void LimitRebound()
        {
            Vector3 normalSum = Vector3.zero;
            for (int i = 0; i < _touching.Length; i++)
            {
                if (_touching[i])
                {
                    normalSum += _contactNormal[i];
                }
            }

            Vector3 mean = Average(_v);
            float normalLength = normalSum.magnitude;
            if (normalLength < 1e-6f)
            {
                _bodyInContact = false;
                _freeVelocity = mean;
                return;
            }

            Vector3 normal = normalSum / normalLength;
            if (!_bodyInContact)
            {
                // Arrival speed comes from the last substep the body was entirely free.
                _bodyInContact = true;
                _arrivalSpeed = Mathf.Max(0f, -Vector3.Dot(_freeVelocity, normal));
            }

            float allowed = _p.restitution * _arrivalSpeed;
            float leaving = Vector3.Dot(mean, normal);
            if (leaving <= allowed)
            {
                return;
            }

            Vector3 excess = normal * (leaving - allowed);
            for (int i = 0; i < _v.Length; i++)
            {
                _v[i] -= excess;
            }
        }

        /// <summary>
        /// Deformation damping (Müller et al. 2007, §3.5): each particle's velocity is pulled toward
        /// the body's best-fit rigid motion — centre-of-mass velocity plus a rigid spin — rather than
        /// toward the mean velocity alone. Damping only the <i>deviation</i> from rigid motion is what
        /// lets a jelly fall and tumble at full speed while its jiggle and ringing die out; damping
        /// relative to the mean would also brake every tumble.
        /// </summary>
        void Damp(float h)
        {
            Vector3 centre = Average(_x);
            Vector3 mean = Average(_v);

            // Angular momentum and inertia about the centre (unit particle masses — they cancel).
            float cx = centre.x, cy = centre.y, cz = centre.z;
            float mx = mean.x, my = mean.y, mz = mean.z;
            float lx = 0f, ly = 0f, lz = 0f;
            float i00 = 0f, i11 = 0f, i22 = 0f, i01 = 0f, i02 = 0f, i12 = 0f;
            for (int i = 0; i < _x.Length; i++)
            {
                Vector3 x = _x[i];
                Vector3 v = _v[i];
                float rx = x.x - cx, ry = x.y - cy, rz = x.z - cz;
                float ux = v.x - mx, uy = v.y - my, uz = v.z - mz;
                lx += ry * uz - rz * uy;
                ly += rz * ux - rx * uz;
                lz += rx * uy - ry * ux;
                float rr = rx * rx + ry * ry + rz * rz;
                i00 += rr - rx * rx;
                i11 += rr - ry * ry;
                i22 += rr - rz * rz;
                i01 -= rx * ry;
                i02 -= rx * rz;
                i12 -= ry * rz;
            }

            var inertia = new Matrix3
            {
                m00 = i00, m11 = i11, m22 = i22,
                m01 = i01, m10 = i01, m02 = i02, m20 = i02, m12 = i12, m21 = i12
            };
            Vector3 spin = inertia.Inverse().Multiply(new Vector3(lx, ly, lz));

            float air = Mathf.Exp(-_p.airDamping * h);
            float keep = Mathf.Exp(-_p.wobbleDamping * h);
            float sx = spin.x, sy = spin.y, sz = spin.z;
            for (int i = 0; i < _v.Length; i++)
            {
                Vector3 x = _x[i];
                Vector3 v = _v[i];
                float rx = x.x - cx, ry = x.y - cy, rz = x.z - cz;
                float gx = mx + sy * rz - sz * ry;
                float gy = my + sz * rx - sx * rz;
                float gz = mz + sx * ry - sy * rx;
                _v[i] = new Vector3(gx * air + (v.x - gx) * keep, gy * air + (v.y - gy) * keep,
                    gz * air + (v.z - gz) * keep);
            }
        }

        void UpdateMetrics()
        {
            Centroid = Average(_x);
            Vector3 mean = Average(_v);
            CentroidVelocity = mean;
            float energy = 0f;
            for (int i = 0; i < _v.Length; i++)
            {
                energy += (_v[i] - mean).sqrMagnitude;
            }

            WobbleEnergy = energy / _v.Length;
        }

        float Volume()
        {
            Vector3 origin = Average(_x);
            double volume = 0.0;
            for (int t = 0; t < _triangles.Length; t += 3)
            {
                volume += Vector3.Dot(_x[_triangles[t]] - origin,
                    Vector3.Cross(_x[_triangles[t + 1]] - origin, _x[_triangles[t + 2]] - origin));
            }

            return (float)(volume / 6.0);
        }

        static Vector3 Average(Vector3[] values)
        {
            float x = 0f, y = 0f, z = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                Vector3 v = values[i];
                x += v.x;
                y += v.y;
                z += v.z;
            }

            float inverse = 1f / values.Length;
            return new Vector3(x * inverse, y * inverse, z * inverse);
        }

        /// <summary>
        /// Order-sensitive fingerprint of every particle position. Two runs that agree on this after the
        /// same number of steps took the same path — the soft-body determinism check.
        /// </summary>
        public float StateHash()
        {
            double hash = 0.0;
            for (int i = 0; i < _x.Length; i++)
            {
                hash += (i + 1) * (_x[i].x * 0.7331 + _x[i].y * 0.1377 + _x[i].z * 0.5021);
            }

            return (float)(hash % 100000.0);
        }
    }
}
