using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.MultiplyEscape
{
    /// <summary>
    /// Tunables for "Multiply vs Escape": every wall bounce adds a ball, every escape widens the exit.
    /// A published video is this asset plus a scene plus a seed.
    /// </summary>
    /// <remarks>
    /// Two feedback loops race each other, so the outcome is decided by <see cref="startGapDegrees"/>
    /// against <see cref="gapDegreesPerEscape"/>. Modelled without ball-ball collisions (2026-10-13):
    /// gap 16° / +1.1° per escape at speed 5 and fill 500 gives about 50/50, with a third of rounds
    /// emptying after the swarm passed 400. In Unity, crowding cuts wall bounces, which favours EMPTY;
    /// if FULL never happens, lower <see cref="gapDegreesPerEscape"/> first.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "MultiplyEscapeConfig",
        menuName = "Simulation Lobby/Configs/Multiply vs Escape (mve)",
        order = 15)]
    public sealed class MultiplyEscapeConfig : SimulationConfig
    {
        [Header("Balls")]
        [Tooltip("Ball radius in world units. Sets how full the circle looks at the fill target.")]
        [Range(0.05f, 0.4f)] public float ballRadius = 0.12f;

        [Tooltip("Constant speed of every ball inside the circle, units/second.")]
        [Min(0.5f)] public float ballSpeed = 5f;

        [Tooltip("Clone direction: the parent's new heading turned by a seeded angle in this range " +
                 "(degrees, either side). Too small and clones ride their parent in a clump.")]
        public Vector2 cloneTurnDegrees = new Vector2(15f, 55f);

        [Header("The race")]
        [Tooltip("The circle is FULL when this many balls are inside at once. Also the body budget.")]
        [Range(50, 1500)] public int fillTarget = 500;

        [Tooltip("Exit width at the start of each round, degrees.")]
        [Range(2f, 90f)] public float startGapDegrees = 16f;

        [Tooltip("The exit widens by this much for every ball that escapes. The balance dial: " +
                 "higher favours EMPTY, lower favours FULL.")]
        [Range(0f, 10f)] public float gapDegreesPerEscape = 1.1f;

        [Tooltip("Exit spin, degrees/second. Direction is seeded per round.")]
        public float gapAngularSpeed = 25f;

        [Tooltip("Where the exit starts, degrees CCW from +X. 90 = top.")]
        [Range(0f, 360f)] public float gapCenterDegrees = 90f;

        [Header("Series")]
        [Range(1, 9)] public int roundCount = 5;

        [Tooltip("Safety cap per round. A round normally ends FULL or EMPTY well before this.")]
        [Min(5f)] public float maxRoundSeconds = 40f;

        [Tooltip("Still beat before the first ball moves, so the setup reads.")]
        [Range(0f, 3f)] public float preLaunchHoldSeconds = 1f;

        [Tooltip("Beat after a result, so the viewer reads it before the reset.")]
        [Range(0f, 4f)] public float interRoundSeconds = 1.6f;

        [Tooltip("An escaped ball keeps flying and is recycled once this far from the centre.")]
        [Min(1f)] public float recycleDistance = 8f;

        /// <summary>Bodies the pool needs: a full circle plus the escaped balls still in flight.</summary>
        public int BodyBudget => fillTarget + Mathf.Max(100, fillTarget / 3);

        public float SeriesSeconds =>
            roundCount * (preLaunchHoldSeconds + maxRoundSeconds + interRoundSeconds);

        public override int MaxTicks => Mathf.Max(1, Mathf.CeilToInt(SeriesSeconds / fixedTimestep));

        void OnValidate()
        {
            maxDurationSeconds = SeriesSeconds;
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (cloneTurnDegrees.x > cloneTurnDegrees.y)
            {
                error = "cloneTurnDegrees.x must not exceed .y.";
                return false;
            }

            if (gapDegreesPerEscape <= 0f)
            {
                error = "gapDegreesPerEscape must be above 0, or the exit never opens and FULL is guaranteed.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
