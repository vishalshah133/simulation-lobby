using System;

namespace SimulationLobby.Shared.Timing
{
    /// <summary>
    /// A monotone progress profile p(t): p(0) = 0, p(Duration) = 1, with a continuous speed. Cruise speed is
    /// flat; an optional ease fraction ramps the speed linearly up and down at each end. The cruise speed is
    /// solved so the integral lands on exactly 1 at <see cref="Duration"/>, and the closed form is used so no
    /// stepping error accumulates.
    /// </summary>
    public sealed class CountdownProfile
    {
        readonly float _ramp;
        readonly float _cruiseSpeed;

        public float Duration { get; }

        public float EaseFraction { get; }

        public CountdownProfile(float duration, float easeFraction = 0f)
        {
            if (duration <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive.");
            }

            Duration = duration;
            EaseFraction = Math.Min(Math.Max(easeFraction, 0f), 0.45f);
            _ramp = EaseFraction * duration;
            _cruiseSpeed = 1f / (duration * (1f - EaseFraction));
        }

        /// <summary>Progress 0..1 at time <paramref name="t"/> seconds. Clamped outside 0..Duration.</summary>
        public float Progress(float t)
        {
            if (t <= 0f)
            {
                return 0f;
            }

            if (t >= Duration)
            {
                return 1f;
            }

            if (_ramp > 0f && t < _ramp)
            {
                return _cruiseSpeed * t * t / (2f * _ramp);
            }

            float remaining = Duration - t;
            if (_ramp > 0f && remaining < _ramp)
            {
                return 1f - _cruiseSpeed * remaining * remaining / (2f * _ramp);
            }

            return _cruiseSpeed * (_ramp * 0.5f + (t - _ramp));
        }

        /// <summary>Speed in normalised units per second at time <paramref name="t"/>.</summary>
        public float Speed(float t)
        {
            if (t <= 0f || t >= Duration)
            {
                return 0f;
            }

            if (_ramp > 0f && t < _ramp)
            {
                return _cruiseSpeed * t / _ramp;
            }

            float remaining = Duration - t;
            if (_ramp > 0f && remaining < _ramp)
            {
                return _cruiseSpeed * remaining / _ramp;
            }

            return _cruiseSpeed;
        }
    }
}
