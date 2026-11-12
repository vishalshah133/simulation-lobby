using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>Which frame a scene is rendered for. Portrait = 9:16 Shorts, Wide = 16:9 long form.</summary>
    public enum AspectMode
    {
        Portrait,
        Wide
    }

    /// <summary>
    /// Pure frame maths shared by every format's builder (see plans/blocks/wide-frame-hud.md).
    /// No scene state, so a builder and the presenter agree on the same numbers.
    /// </summary>
    public static class WideFrameLayout
    {
        /// <summary>Fraction of frame height the arena may fill (the middle band between top bar and bottom strip).</summary>
        public const float ArenaHeightFraction = 0.75f;

        /// <summary>Fraction of frame width kept clear on the right for the counter in Wide mode.</summary>
        public const float WideRightMarginFraction = 0.22f;

        public static Vector2 ReferenceResolution(AspectMode mode)
        {
            return mode == AspectMode.Wide ? new Vector2(1920f, 1080f) : new Vector2(1080f, 1920f);
        }

        public static float MatchWidthOrHeight(AspectMode mode)
        {
            return mode == AspectMode.Wide ? 0.5f : 1f;
        }

        public static float Aspect(AspectMode mode)
        {
            return mode == AspectMode.Wide ? 16f / 9f : 9f / 16f;
        }

        /// <summary>
        /// Orthographic size that fits an arena of the given world size into the middle
        /// <see cref="ArenaHeightFraction"/> of the frame height and, in Wide mode, left of the right margin.
        /// <paramref name="padding"/> is a multiplier (1 = tight fit).
        /// </summary>
        public static float OrthoSizeForArena(float arenaWorldWidth, float arenaWorldHeight, AspectMode mode, float padding = 1f)
        {
            float aspect = Aspect(mode);
            float usableWidthFraction = mode == AspectMode.Wide ? 1f - WideRightMarginFraction : 0.92f;
            float byHeight = arenaWorldHeight / (2f * ArenaHeightFraction);
            float byWidth = arenaWorldWidth / (2f * aspect * usableWidthFraction);
            return Mathf.Max(byHeight, byWidth) * Mathf.Max(0.01f, padding);
        }

        /// <summary>
        /// World-space x offset to add to the camera so the arena centres in the area left of the right
        /// margin (0 in Portrait). Positive means move the camera right of the arena centre.
        /// </summary>
        public static float ArenaCameraOffsetX(float orthoSize, AspectMode mode)
        {
            if (mode != AspectMode.Wide)
            {
                return 0f;
            }

            return orthoSize * Aspect(mode) * WideRightMarginFraction;
        }
    }
}
