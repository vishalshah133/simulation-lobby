using System;
using UnityEngine;

namespace SimulationLobby.Shared.Coaster
{
    /// <summary>Tunables for <see cref="RideDynamics"/>. Plain data so any coaster format can fill it from its own config.</summary>
    [Serializable]
    public struct RideParams
    {
        public float gravity;
        public float drag;
        public float rollingFriction;
        public float liftSpeed;
        public float liftAccel;
        public float maxSpeed;
        public float minSpeed;
        public float brakeDecel;
        public float driveAccel;
        public float startHoldSeconds;
        public float[] liftStarts, liftEnds;
        public float[] brakeStarts, brakeEnds, brakeSpeeds;

        public static RideParams Default()
        {
            return new RideParams
            {
                gravity = 9.81f, drag = 0.0008f, rollingFriction = 0.006f, liftSpeed = 7f, liftAccel = 3f,
                maxSpeed = 30f, minSpeed = 10f, brakeDecel = 5f, driveAccel = 4f, startHoldSeconds = 2f
            };
        }
    }

    /// <summary>
    /// Ride speed along a rail. Free stretches follow an energy model written as an along-track acceleration
    /// a = -g sin(slope) - mu g cos(slope) - k v^2; lift zones hold a constant speed; brake/drive zones steer speed toward a target.
    /// Free stretches are nudged back up to minSpeed so the cart never stalls on a hill, and speed is capped at maxSpeed. Pure class: no Time, no Physics.
    /// </summary>
    public sealed class RideDynamics
    {
        const float MaxSubstep = 1f / 120f;

        readonly ICoasterRail _rail;
        readonly RideParams _p;
        float _elapsed;

        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public bool OnLift { get; private set; }
        public bool AtEnd { get; private set; }

        public RideDynamics(ICoasterRail rail, RideParams p)
        {
            _rail = rail;
            _p = p;
        }

        public void Step(float dt)
        {
            if (AtEnd) return;
            if (_elapsed < _p.startHoldSeconds)
            {
                _elapsed += dt;
                Speed = 0f;
                if (_elapsed < _p.startHoldSeconds) return;
                dt = _elapsed - _p.startHoldSeconds;
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
            int lift = Find(_p.liftStarts, _p.liftEnds, Distance);
            int brake = lift < 0 ? Find(_p.brakeStarts, _p.brakeEnds, Distance) : -1;
            OnLift = lift >= 0;

            if (lift >= 0)
            {
                v = Approach(v, _p.liftSpeed, _p.liftAccel * h, _p.liftAccel * h);
            }
            else if (brake >= 0)
            {
                v = Approach(v, _p.brakeSpeeds[brake], _p.driveAccel * h, _p.brakeDecel * h);
            }
            else
            {
                _rail.Sample(Distance, out _, out Vector3 fwd, out _);
                float fy = fwd.y;
                float cos = Mathf.Sqrt(Mathf.Max(0f, 1f - fy * fy));
                v += (-_p.gravity * fy - _p.rollingFriction * _p.gravity * cos - _p.drag * v * v) * h;
                // a gentle kick keeps the cart above minSpeed on a crest instead of letting it stall
                if (v < _p.minSpeed) v = Mathf.Min(_p.minSpeed, v + _p.driveAccel * h);
            }

            v = Mathf.Clamp(v, 0f, _p.maxSpeed);
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

        static float Approach(float v, float target, float maxUp, float maxDown)
        {
            return v < target ? Mathf.Min(target, v + maxUp) : Mathf.Max(target, v - maxDown);
        }

        static int Find(float[] starts, float[] ends, float d)
        {
            if (starts == null || ends == null) return -1;
            int c = Mathf.Min(starts.Length, ends.Length);
            for (int i = 0; i < c; i++)
            {
                if (d >= starts[i] && d < ends[i]) return i;
            }
            return -1;
        }
    }
}
