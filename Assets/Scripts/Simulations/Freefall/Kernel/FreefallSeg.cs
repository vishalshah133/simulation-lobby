using UnityEngine;

namespace SimulationLobby.Simulations.Freefall.Kernel
{
    /// <summary>
    /// One ballistic arc of a marble: position and velocity at <see cref="t0"/>, constant gravity after.
    /// A voice's whole path is a chain of these, so sampling any time is one binary search plus a quadratic.
    /// </summary>
    internal struct FreefallSeg
    {
        public double t0;
        public double t1;
        public Vector3 p0;
        public Vector3 v0;
        public float g;


        /// <summary>
        /// The arc from <paramref name="p0"/> at <paramref name="t0"/> to <paramref name="p1"/> at <paramref name="t1"/>
        /// under gravity <paramref name="g"/>: vOut = (p1 - p0) / dt - 1/2 gVec dt, with gVec = (0, -g, 0).
        /// </summary>
        public static FreefallSeg Between(double t0, Vector3 p0, double t1, Vector3 p1, float g)
        {
            float dt = (float)System.Math.Max(t1 - t0, 1e-4);
            return new FreefallSeg
            {
                t0 = t0,
                t1 = t1,
                p0 = p0,
                v0 = new Vector3(
                    (p1.x - p0.x) / dt,
                    (p1.y - p0.y) / dt + 0.5f * g * dt,
                    (p1.z - p0.z) / dt),
                g = g
            };
        }

        public Vector3 PositionAt(double t)
        {
            float tau = (float)(t - t0);
            return new Vector3(
                p0.x + v0.x * tau,
                p0.y + v0.y * tau - 0.5f * g * tau * tau,
                p0.z + v0.z * tau);
        }

        public Vector3 VelocityAt(double t)
        {
            float tau = (float)(t - t0);
            return new Vector3(v0.x, v0.y - g * tau, v0.z);
        }

        public Vector3 EndVelocity => VelocityAt(t1);
    }

    /// <summary>
    /// Makes the <c>...Freefall.Kernel</c> namespace exist for callers that import it. The public kernel types
    /// (<c>FreefallSchedule</c>, <c>FreefallHit</c>, ...) live in the parent namespace; helpers here are internal.
    /// </summary>
    internal static class FreefallKernelNamespace
    {
    }
}
