using System;
using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// A turret angle that ping-pongs between two limits at a constant rate. A pure function of the
    /// time the simulation feeds <see cref="Step"/>, so a shot's heading is reproducible from the seed.
    /// Angles are grid-space degrees: 0 points along +x, 90 along +y.
    /// </summary>
    [Serializable]
    public struct SweepAim
    {
        public float minDegrees;
        public float maxDegrees;
        public float degreesPerSecond;
        public float angle;

        [Tooltip("+1 sweeps toward max, -1 toward min.")]
        public int direction;

        /// <summary>Starts at min + startPhase01 * (max - min), sweeping toward max.</summary>
        public static SweepAim Create(float minDegrees, float maxDegrees, float degreesPerSecond, float startPhase01)
        {
            return new SweepAim
            {
                minDegrees = minDegrees,
                maxDegrees = maxDegrees,
                degreesPerSecond = degreesPerSecond,
                angle = Mathf.Lerp(minDegrees, maxDegrees, Mathf.Clamp01(startPhase01)),
                direction = 1,
            };
        }

        public void Step(float dt)
        {
            float span = maxDegrees - minDegrees;
            if (span <= 0f)
            {
                angle = minDegrees;
                return;
            }

            if (direction == 0)
            {
                direction = 1;
            }

            float next = angle + (direction > 0 ? 1 : -1) * Mathf.Abs(degreesPerSecond) * dt;

            // Fold any overshoot back inside; loops only if a single step spans several sweeps.
            for (int guard = 0; guard < 64; guard++)
            {
                if (next > maxDegrees)
                {
                    next = maxDegrees - (next - maxDegrees);
                    direction = -1;
                }
                else if (next < minDegrees)
                {
                    next = minDegrees + (minDegrees - next);
                    direction = 1;
                }
                else
                {
                    break;
                }
            }

            angle = Mathf.Clamp(next, minDegrees, maxDegrees);
        }

        /// <summary>Unit vector in grid space for the current angle.</summary>
        public Vector2 Direction
        {
            get
            {
                float r = angle * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            }
        }
    }
}
