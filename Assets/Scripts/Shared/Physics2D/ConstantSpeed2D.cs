using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// Holds a 2D body at a fixed speed, preserving direction. Two jobs, both collision-safety:
    /// it stops the solver bleeding energy off a "perfectly elastic" bounce, and it is the hard
    /// ceiling that keeps escalation from outrunning the timestep.
    /// </summary>
    /// <remarks>
    /// Called from the simulation's fixed tick, never from its own <c>FixedUpdate</c> — the order of
    /// "physics stepped, then speed corrected" has to be explicit for a run to be reproducible.
    /// <para>
    /// Restitution 1 with zero damping still drifts a little in a discrete solver; over a 40-second
    /// run that drift is visible as a ball slowly losing pace, which reads as broken physics.
    /// </para>
    /// </remarks>
    public static class ConstantSpeed2D
    {
        /// <summary>
        /// Re-normalise velocity to <paramref name="speed"/>. A body at rest is left alone — there is
        /// no direction to preserve, and inventing one would be undeclared randomness.
        /// </summary>
        public static void Apply(Rigidbody2D body, float speed)
        {
            Vector2 velocity = body.linearVelocity;
            float current = velocity.magnitude;

            if (current < Mathf.Epsilon)
            {
                return;
            }

            body.linearVelocity = velocity * (speed / current);
        }

        /// <summary>
        /// Clamp speed to a ceiling without forcing it upward — for formats where speed varies but
        /// must stay inside what the timestep can resolve.
        /// </summary>
        public static void ClampTo(Rigidbody2D body, float maxSpeed)
        {
            Vector2 velocity = body.linearVelocity;
            float current = velocity.magnitude;

            if (current <= maxSpeed || current < Mathf.Epsilon)
            {
                return;
            }

            body.linearVelocity = velocity * (maxSpeed / current);
        }

        /// <summary>
        /// The largest speed a given timestep can resolve against a wall of a given thickness, with
        /// margin. Use it to sanity-check a config instead of discovering tunneling in a 1000-seed scan.
        /// </summary>
        public static float MaxSafeSpeed(float fixedTimestep, float wallThickness, float safetyFactor = 0.5f)
        {
            // A body must not travel more than a fraction of the wall's thickness in one step, or it
            // can start one side of the wall and end up the other with no contact generated.
            return wallThickness * safetyFactor / Mathf.Max(fixedTimestep, 1e-6f);
        }
    }
}
