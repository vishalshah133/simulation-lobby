using UnityEngine;

namespace SimulationLobby.Simulations.TrippyCoaster
{
    /// <summary>CPU twin of the shader's cosine palette, so lights, UI and audio can follow the colours the walls are painting.</summary>
    public static class TripPalette
    {
        public static Color Eval(in TripWorldDef w, float t)
        {
            return new Color(
                w.palA.r + w.palB.r * Mathf.Cos(6.28318f * (w.palC.r * t + w.palD.r)),
                w.palA.g + w.palB.g * Mathf.Cos(6.28318f * (w.palC.g * t + w.palD.g)),
                w.palA.b + w.palB.b * Mathf.Cos(6.28318f * (w.palC.b * t + w.palD.b)),
                1f);
        }

        /// <summary>The palette colour normalised to its brightest channel, for lights and text.</summary>
        public static Color Vivid(in TripWorldDef w, float t)
        {
            Color c = Eval(w, t);
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (m > 1e-3f) c /= m;
            c.a = 1f;
            return c;
        }
    }
}
