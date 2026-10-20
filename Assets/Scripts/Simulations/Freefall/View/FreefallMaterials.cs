using System;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall
{
    /// <summary>
    /// The materials the <c>fall</c> view draws with, created by the scene builder and handed to the presenter.
    /// Every colour that changes at run time goes through a <see cref="MaterialPropertyBlock"/>, so these stay
    /// shared assets. The two glow materials are URP Particles/Unlit, additive and double sided, which multiply
    /// vertex colour, so one material serves rings, curtains, strips, trails and particles.
    /// </summary>
    [Serializable]
    public sealed class FreefallMaterials
    {
        [Tooltip("URP Lit, smooth, emission keyword on. Hex and crescent pads.")]
        public Material padOpaque;

        [Tooltip("URP Lit, alpha blended, emission keyword on. Disc, petal, lily and crystal pads.")]
        public Material padGlass;

        [Tooltip("URP Particles/Unlit, additive, double sided, no texture. Rings, curtains, strips, trails.")]
        public Material glowFlat;

        [Tooltip("URP Particles/Unlit, additive, double sided, soft round texture. Halos, mist, stars, sparks.")]
        public Material glowSoft;

        [Tooltip("The marble's glass shell: URP Lit, alpha blended, very smooth.")]
        public Material marbleGlass;

        [Tooltip("The marble's core: URP Unlit, HDR white (colour set by the presenter).")]
        public Material marbleCore;

        [Tooltip("Crystal shards on the walls: URP Lit, glossy, emission keyword on.")]
        public Material crystal;

        [Tooltip("The white core the last note lands on: URP Unlit, HDR.")]
        public Material coreSphere;

        public bool IsComplete =>
            padOpaque != null && padGlass != null && glowFlat != null && glowSoft != null &&
            marbleGlass != null && marbleCore != null && crystal != null && coreSphere != null;
    }

    /// <summary>Small colour and easing helpers shared by the view components.</summary>
    internal static class FreefallColors
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        /// <summary>Pitch to hue: low notes at hueStart, high notes at hueStart + hueSpan.</summary>
        public static Color Hue(FreefallWorld world, float pitch01, float saturation = 0.72f, float value = 1f)
        {
            return Color.HSVToRGB(Mathf.Repeat(world.hueStart + world.hueSpan * Mathf.Clamp01(pitch01), 1f), saturation, value);
        }

        public static Color Hdr(Color colour, float intensity, float alpha = 1f)
        {
            return new Color(colour.r * intensity, colour.g * intensity, colour.b * intensity, alpha);
        }

        /// <summary>Hermite 0..1 ramp between edges.</summary>
        public static float Smooth(float a, float b, float x)
        {
            if (b <= a)
            {
                return x >= b ? 1f : 0f;
            }

            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }
    }
}
