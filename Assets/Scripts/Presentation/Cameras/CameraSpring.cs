using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Critically damped spring steps with velocity feed-forward plus rate and acceleration limits. Pure maths, no state: callers
    /// own the position and velocity and may substep (the unlimited step is exact, so substep size does not change the result).
    /// Format-agnostic; the camera directors build their filters from it.
    /// </summary>
    public static class CameraSpring
    {
        /// <summary>
        /// One step of a scalar spring toward a target that moves at <paramref name="targetVel"/> (the feed-forward velocity), with
        /// <paramref name="target"/> its position at the start of the step. While no limit binds the step is the exact solution of
        /// the critically damped spring, so the result does not depend on how a frame is divided into steps; when a limit binds it
        /// falls back to a clamped semi-implicit Euler step.
        /// </summary>
        public static void Step(ref float x, ref float v, float target, float targetVel, float omega, float vMin, float vMax, float aMax, float h)
        {
            float e = x - target;
            float ev = v - targetVel;
            float k = ev + omega * e;
            float damp = Mathf.Exp(-omega * h);
            float eN = (e + k * h) * damp;
            float vN = targetVel + (ev - omega * k * h) * damp;
            float a = (vN - v) / h;
            if (Mathf.Abs(a) <= aMax && vN >= vMin && vN <= vMax)
            {
                x = target + targetVel * h + eN;
                v = vN;
                return;
            }

            a = omega * omega * (target - x) + 2f * omega * (targetVel - v);
            a = Mathf.Clamp(a, -aMax, aMax);
            v = Mathf.Clamp(v + a * h, vMin, vMax);
            x += v * h;
        }

        /// <summary>Vector version of <see cref="Step(ref float, ref float, float, float, float, float, float, float, float)"/>; limits are by magnitude.</summary>
        public static void Step(ref Vector3 x, ref Vector3 v, Vector3 target, Vector3 targetVel, float omega, float vMax, float aMax, float h)
        {
            Vector3 e = x - target;
            Vector3 ev = v - targetVel;
            Vector3 k = ev + omega * e;
            float damp = Mathf.Exp(-omega * h);
            Vector3 eN = (e + k * h) * damp;
            Vector3 vN = targetVel + (ev - omega * k * h) * damp;
            Vector3 a = (vN - v) / h;
            if (a.sqrMagnitude <= aMax * aMax && vN.sqrMagnitude <= vMax * vMax)
            {
                x = target + targetVel * h + eN;
                v = vN;
                return;
            }

            a = omega * omega * (target - x) + 2f * omega * (targetVel - v);
            a = Vector3.ClampMagnitude(a, aMax);
            v = Vector3.ClampMagnitude(v + a * h, vMax);
            x += v * h;
        }

        /// <summary>0..1 smoothstep of an already normalised parameter (clamped).</summary>
        public static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Smoothstep between edges <paramref name="a"/> and <paramref name="b"/>.</summary>
        public static float SmoothStep(float a, float b, float x)
        {
            if (b <= a)
            {
                return x >= b ? 1f : 0f;
            }

            return SmoothStep01((x - a) / (b - a));
        }
    }
}
