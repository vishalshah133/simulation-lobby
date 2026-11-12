using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Ride speed along the rail. Free-running stretches follow the energy model v = sqrt(v0^2 + 2 g (h0 - h)) with
    /// quadratic drag and rolling friction, written as the equivalent along-the-track acceleration
    /// a = -g sin(slope) - mu g cos(slope) - k v^2 (so a crest never needs a sqrt of a negative number).
    /// Lift zones hold a constant liftSpeed; brake/drive zones drive the speed toward a target. Speed is capped at
    /// maxSpeed and never falls below a crawl so the cart cannot stall.
    /// Pure class: no MonoBehaviour, no Time, no Physics; the same dt always gives the same ride.
    /// </summary>
    public sealed class CoasterDynamics
    {
        const float MaxSubstep = 1f / 120f;

        readonly ICoasterRail _rail;
        readonly float _gravity, _drag, _mu, _liftSpeed, _maxSpeed, _crawl, _brakeDecel, _driveAccel, _liftAccel, _dwell;
        readonly float[] _liftStart, _liftEnd, _brakeStart, _brakeEnd, _brakeSpeed;

        // Scare ease: the one deliberate slow-down. Sting distances of BIG scares (rider-taking or named), ascending.
        readonly float[] _big;
        readonly float _easeSeconds, _easeMin, _easeFraction;
        int _bigIdx;
        bool _easing;
        float _easeTarget, _easeDecel;

        float _elapsed;
        bool _moving;

        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public bool OnLift { get; private set; }
        /// <summary>True once the cart has reached the end of the rail (speed is then forced to 0).</summary>
        public bool AtEnd { get; private set; }

        public CoasterDynamics(ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            _rail = rail;
            _gravity = cfg.gravity;
            _drag = cfg.drag;
            _mu = cfg.rollingFriction;
            _liftSpeed = cfg.liftSpeed;
            _maxSpeed = cfg.maxSpeed;
            _crawl = cfg.minCrawlSpeed;
            _brakeDecel = cfg.brakeDecel;
            _driveAccel = cfg.driveAccel;
            _liftAccel = cfg.liftAccel;
            _dwell = cfg.stationDwellSeconds;
            _liftStart = cfg.liftStarts ?? new float[0];
            _liftEnd = cfg.liftEnds ?? new float[0];
            _brakeStart = cfg.brakeStarts ?? new float[0];
            _brakeEnd = cfg.brakeEnds ?? new float[0];
            _brakeSpeed = cfg.brakeSpeeds ?? new float[0];
            _easeSeconds = Mathf.Max(0.1f, cfg.easeSeconds);
            _easeMin = cfg.easeMinSpeed;
            _easeFraction = cfg.easeFraction;
            var big = new System.Collections.Generic.List<float>();
            if (cfg.events != null)
            {
                for (int i = 0; i < cfg.events.Length; i++)
                {
                    if (HorrorCoasterSimulation.IsBigScare(cfg.events[i].kind)) big.Add(cfg.events[i].distance);
                }
            }
            big.Sort();
            _big = big.ToArray();
            Distance = 0f;
            Speed = 0f;
        }

        public void Step(float dt)
        {
            if (AtEnd) return;

            if (_elapsed < _dwell)
            {
                // Still in the station.
                _elapsed += dt;
                Speed = 0f;
                OnLift = false;
                if (_elapsed < _dwell) return;
                dt = _elapsed - _dwell; // the remainder of this tick is real motion
                if (dt <= 0f) return;
            }
            else
            {
                _elapsed += dt;
            }

            int n = Mathf.Max(1, Mathf.CeilToInt(dt / MaxSubstep));
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                SubStep(h);
                if (AtEnd) return;
            }
        }

        void SubStep(float h)
        {
            float v = Speed;
            float floor = _crawl;

            int lift = Find(_liftStart, _liftEnd, Distance);
            int brake = lift < 0 ? Find(_brakeStart, _brakeEnd, Distance) : -1;
            OnLift = lift >= 0;

            while (_bigIdx < _big.Length && Distance >= _big[_bigIdx] - 1e-4f)
            {
                _bigIdx++;
                _easing = false;
            }

            if (lift >= 0)
            {
                v = Approach(v, _liftSpeed, _liftAccel * h, _liftAccel * h);
            }
            else if (EaseBeforeSting(ref v, h))
            {
                floor = Mathf.Min(floor, _easeTarget);
            }
            else if (brake >= 0)
            {
                float target = _brakeSpeed[brake];
                v = Approach(v, target, _driveAccel * h, _brakeDecel * h);
                floor = Mathf.Min(_crawl, target);
            }
            else
            {
                _rail.Sample(Distance, out _, out Vector3 fwd, out _);
                float fy = fwd.y;
                float cos = Mathf.Sqrt(Mathf.Max(0f, 1f - fy * fy));
                float a = -_gravity * fy - _mu * _gravity * cos - _drag * v * v;
                v += a * h;
            }

            if (!_moving)
            {
                floor = 0f; // pulling out of the station: no instant jump to the crawl speed
                if (v >= _crawl) _moving = true;
            }
            v = Mathf.Clamp(v, floor, _maxSpeed);
            Speed = v;
            Distance += v * h;

            if (Distance >= _rail.Length)
            {
                Distance = _rail.Length;
                Speed = 0f;
                AtEnd = true;
                OnLift = false;
            }
        }

        /// <summary>
        /// Slows the cart linearly over easeSeconds so it reaches max(easeMin, easeFraction * v) exactly at the next big
        /// sting. Only slows; never speeds up. Returns true while the ease owns the speed.
        /// </summary>
        bool EaseBeforeSting(ref float v, float h)
        {
            if (_bigIdx >= _big.Length) return false;
            if (!_easing)
            {
                float tgt = Mathf.Max(_easeMin, _easeFraction * v);
                if (v <= tgt + 0.05f) return false;
                float rem = _big[_bigIdx] - Distance;
                if (rem > _easeSeconds * (v + tgt) * 0.5f) return false;
                _easing = true;
                _easeTarget = tgt;
                _easeDecel = (v - tgt) / _easeSeconds;
            }
            v = Mathf.Max(_easeTarget, v - _easeDecel * h);
            return true;
        }

        static float Approach(float v, float target, float maxUp, float maxDown)
        {
            if (v < target) return Mathf.Min(target, v + maxUp);
            return Mathf.Max(target, v - maxDown);
        }

        static int Find(float[] starts, float[] ends, float d)
        {
            int c = Mathf.Min(starts.Length, ends.Length);
            for (int i = 0; i < c; i++)
            {
                if (d >= starts[i] && d < ends[i]) return i;
            }
            return -1;
        }
    }
}
