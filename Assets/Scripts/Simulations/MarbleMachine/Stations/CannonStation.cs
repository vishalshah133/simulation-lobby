using System.Collections.Generic;
using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// The recycle cannon. A hopper funnel catches rejects and lost balls; every interval one ball from the breech is
    /// teleported to the muzzle and fired in a ballistic arc at the silo top. The barrel only animates (visual).
    /// </summary>
    public sealed class CannonStation : MachineStation, ICannon
    {
        [Header("Hopper / breech (set by CannonBuilder)")]
        [Tooltip("Local-space box of the breech zone: balls here are eligible to be fired.")]
        public Bounds breechLocal = new Bounds(new Vector3(0f, -1.4f, 0f), new Vector3(1.1f, 1.1f, 1.1f));
        public Vector3 hopperDropLocal = new Vector3(0f, 0.4f, 0f);

        [Header("Barrel (visual)")]
        [Tooltip("Yaws about world up.")]
        public Transform turret;
        [Tooltip("Pitches the barrel. Local +Z is the bore.")]
        public Transform barrelPivot;
        [Tooltip("Barrel meshes; slides back on recoil.")]
        public Transform barrelVisual;
        public Transform muzzle;
        [Tooltip("Optional: the scene builder sets this to the silo top so the barrel faces it from frame one.")]
        public Transform aimTarget;
        [Min(0.01f)] public float recoilSeconds = 0.25f;
        [Min(0f)] public float recoilDistance = 0.22f;

        [Header("Fire path")]
        [Tooltip("Balls land anywhere in a disc this wide around the target, so recycled balls cover the stack instead of heaping.")]
        [Min(0f)] public float scatterRadius = 0.9f;
        [Tooltip("Fallback lob: the arc peaks this far above the target when the configured launch angle can't reach it.")]
        [Min(0.5f)] public float lobClearance = 1.5f;

        struct Boost
        {
            public MachineBall ball;
            public float restoreAt;
        }

        readonly Boost[] _boosts = new Boost[48];
        int _boostCount;
        MachineConfig _config;
        Bounds _breechWorld;
        Vector3 _barrelRestLocal;
        bool _aimed;
        float _nextFireTime;
        float _lastFireTime = -100f;
        float _nextScanTime;

        public int FiredCount { get; private set; }
        public Vector3 HopperDrop => transform.TransformPoint(hopperDropLocal);
        public Bounds Bounds => _breechWorld.size == Vector3.zero ? ComputeBreech() : _breechWorld;

        Bounds ComputeBreech()
        {
            Vector3 c = breechLocal.center, e = breechLocal.extents;
            var b = new Bounds(transform.TransformPoint(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                b.Encapsulate(transform.TransformPoint(c + corner));
            }

            _breechWorld = b;
            return b;
        }

        public override void Initialize(MachineConfig config, SeededRandom rng)
        {
            _config = config;
            ComputeBreech();
            FiredCount = 0;
            _boostCount = 0;
            _nextFireTime = 0f;
            _lastFireTime = -100f;
            _nextScanTime = 0f;
            _barrelRestLocal = barrelVisual != null ? barrelVisual.localPosition : Vector3.zero;
            _aimed = false;
            if (aimTarget != null) AimAt(aimTarget.position);
        }

        /// <summary>Yaw the turret and pitch the barrel so the bore points along the launch arc toward <paramref name="worldTarget"/>.</summary>
        public void AimAt(Vector3 worldTarget)
        {
            if (turret == null || barrelPivot == null) return;
            float angle = _config != null ? _config.cannonLaunchAngle : 55f;
            Vector3 flat = worldTarget - turret.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f)
            {
                turret.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }

            barrelPivot.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            _aimed = true;
        }

        public override void Step(float simTime, float dt)
        {
            if (barrelVisual == null) return;
            float k = Mathf.Clamp01(1f - (simTime - _lastFireTime) / recoilSeconds);
            barrelVisual.localPosition = _barrelRestLocal - Vector3.forward * (recoilDistance * k * k);
        }

        public void StepFire(float simTime, IReadOnlyList<MachineBall> balls, Vector3 target, SeededRandom rng)
        {
            for (int i = _boostCount - 1; i >= 0; i--)
            {
                if (simTime < _boosts[i].restoreAt) continue;
                _boosts[i].ball.body.maxLinearVelocity = _config.maxBallSpeed;
                _boosts[i] = _boosts[--_boostCount];
            }

            if (simTime < _nextFireTime || simTime < _nextScanTime) return;
            if (!_aimed) AimAt(target);

            Bounds breech = Bounds;
            MachineBall pick = null;
            for (int i = 0; i < balls.Count; i++)
            {
                MachineBall b = balls[i];
                if (b.counted) continue;
                if ((pick == null || b.id < pick.id) && breech.Contains(b.body.position))
                {
                    pick = b;
                }
            }

            if (pick == null)
            {
                _nextScanTime = simTime + 0.05f;
                return;
            }

            float angle = _config.cannonLaunchAngle;
            Vector3 aim = target;
            float jitter = _config.cannonJitter;
            aim.x += rng.NextFloat(-1f, 1f) * jitter;
            aim.z += rng.NextFloat(-1f, 1f) * jitter;
            if (scatterRadius > 0.001f)
            {
                float a = rng.NextFloat(0f, Mathf.PI * 2f);
                float d = scatterRadius * Mathf.Sqrt(rng.NextFloat(0f, 1f));
                aim.x += Mathf.Cos(a) * d;
                aim.z += Mathf.Sin(a) * d;
            }

            // Solve from the muzzle, point the barrel along the answer, then solve again from the muzzle's new position.
            Transform mz = muzzle != null ? muzzle : transform;
            Vector3 from = mz.position;
            Vector3 velocity = Vector3.zero;
            float flightTime = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                if (!SolveShot(from, aim, angle, lobClearance, out velocity, out flightTime))
                {
                    _nextScanTime = simTime + 0.25f;
                    return;
                }

                PointBarrel(velocity);
                from = mz.position;
            }

            Vector3 start = from + velocity.normalized * 0.12f;
            Rigidbody rb = pick.body;
            rb.position = start;
            pick.transform.position = start;
            rb.maxLinearVelocity = Mathf.Max(_config.maxBallSpeed, velocity.magnitude * 1.1f);
            rb.linearVelocity = velocity;
            rb.angularVelocity = Vector3.zero;
            pick.recycles++;
            pick.lastMovingTime = simTime;

            if (_boostCount < _boosts.Length)
            {
                _boosts[_boostCount++] = new Boost { ball = pick, restoreAt = simTime + flightTime + 0.6f };
            }

            FiredCount++;
            _lastFireTime = simTime;
            _nextFireTime = simTime + _config.cannonIntervalSeconds;
            RaisePulse(from, 1f);
        }

        /// <summary>Pitches the barrel to the shot's elevation and yaws the turret to its heading (visual only).</summary>
        void PointBarrel(Vector3 velocity)
        {
            if (turret == null || barrelPivot == null) return;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.sqrMagnitude > 0.0001f) turret.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            float elevation = Mathf.Atan2(velocity.y, flat.magnitude) * Mathf.Rad2Deg;
            barrelPivot.localRotation = Quaternion.Euler(-elevation, 0f, 0f);
        }

        /// <summary>
        /// Ballistic velocity from <paramref name="from"/> to <paramref name="target"/>. First the configured launch angle
        /// (raised in steps until reachable): v^2 = g d^2 / (2 cos^2(t) (d tan(t) - h)). If that fails or is absurdly fast
        /// (target too close or too high for any angle), lob: peak <paramref name="clearance"/> above the target and
        /// come down on it, which always has a solution wherever the cannon stands.
        /// </summary>
        static bool SolveShot(Vector3 from, Vector3 target, float angleDegrees, float clearance, out Vector3 velocity, out float flightTime)
        {
            velocity = Vector3.zero;
            flightTime = 0f;
            float g = Mathf.Abs(Physics.gravity.y);
            Vector3 flat = target - from;
            float h = flat.y;
            flat.y = 0f;
            float d = flat.magnitude;
            Vector3 dir = d > 0.001f ? flat / d : Vector3.forward;

            if (d > 0.5f)
            {
                for (float a = angleDegrees; a <= 80.01f; a += 2f)
                {
                    float t = a * Mathf.Deg2Rad;
                    float denom = 2f * Mathf.Cos(t) * Mathf.Cos(t) * (d * Mathf.Tan(t) - h);
                    if (denom <= 0.001f) continue;
                    float v = Mathf.Sqrt(g * d * d / denom);
                    if (v > 40f) continue;
                    velocity = dir * (v * Mathf.Cos(t)) + Vector3.up * (v * Mathf.Sin(t));
                    flightTime = d / (v * Mathf.Cos(t));
                    return true;
                }
            }

            float apex = Mathf.Max(h, 0f) + clearance;
            float vy = Mathf.Sqrt(2f * g * apex);
            float rise = vy / g;
            float fall = Mathf.Sqrt(2f * Mathf.Max(0.01f, apex - h) / g);
            flightTime = rise + fall;
            velocity = dir * (d / flightTime) + Vector3.up * vy;
            return true;
        }
    }
}
