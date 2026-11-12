using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.HauntedPumpkin
{
    /// <summary>
    /// Tunables for "Escape the Haunted Pumpkin": ghosts break nested rings, every break may split the
    /// ghost, and the swarm has until the candle burns out to get one ghost out through the outer ring.
    /// A published video is this asset plus a scene plus a seed.
    /// </summary>
    /// <remarks>
    /// Ring geometry is baked into the scene by the builder, so changing ring fields means rebuilding the scene.
    /// The pacing dials are <see cref="hitPointsPerRing"/> (mostly the outer two rings) and
    /// <see cref="timeLimitSeconds"/>. A bat target was tried first and was unreachable: ghosts are only born on
    /// breaks, so the swarm is capped near 1 + cloneChance x segments. Tune the candle from the logged ring-open
    /// times (`[hall] ring n open at ...`), aiming for the breakout near the candle's last 10 s.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "HauntedPumpkinConfig",
        menuName = "Simulation Lobby/Configs/Haunted Pumpkin (hall)",
        order = 16)]
    public sealed class HauntedPumpkinConfig : SimulationConfig
    {
        [Header("Rings (geometry: rebuild the scene after changing)")]
        [Range(1, 6)] public int ringCount = 5;

        [Tooltip("Inner radius of the innermost ring.")]
        [Min(0.5f)] public float innerRadius = 1.15f;

        [Tooltip("Distance between consecutive rings' inner radii. The corridor between two rings is this " +
                 "minus the thickness; it must hold a ghost and a freshly born clone.")]
        [Min(0.5f)] public float ringSpacing = 0.9f;

        [Min(0.1f)] public float ringThickness = 0.24f;

        [Tooltip("Segments per ring, innermost first. Missing entries repeat the last one.")]
        public int[] segmentsPerRing = { 10, 12, 14, 16, 18 };

        [Tooltip("Hits to break a segment, per ring, innermost first. The innermost ring should be 1 so the " +
                 "very first touch breaks something (the instant hook). Missing entries repeat the last one.")]
        public int[] hitPointsPerRing = { 1, 3, 6, 11, 16 };

        [Tooltip("Ring rotation in degrees/second. Moving holes make near misses. 0 = static rings.")]
        public float ringAngularSpeed = 10f;

        [Tooltip("Alternate rings spin opposite ways.")]
        public bool alternateDirections = true;

        [Header("Ghosts")]
        [Tooltip("Ghost radius in world units.")]
        [Range(0.05f, 0.4f)] public float ghostRadius = 0.16f;

        [Tooltip("Constant speed of every ghost, units/second.")]
        [Min(0.5f)] public float ghostSpeed = 2.7f;

        [Tooltip("Chance a segment break splits the ghost that broke it. More ghosts means more hits on the outer rings.")]
        [Range(0.05f, 1f)] public float cloneChance = 0.4f;

        [Tooltip("Clone heading: the parent's heading turned by a seeded angle in this range (degrees, either side).")]
        public Vector2 cloneTurnDegrees = new Vector2(15f, 55f);

        [Tooltip("Hard cap on live ghost bodies. Past it, a split adds weight to the parent instead of a body.")]
        [Range(20, 1500)] public int bodyCap = 400;

        [Tooltip("Where the first ghost starts, as a fraction of the innermost ring's room. Never zero: a ghost " +
                 "launched from dead centre bounces along one diameter forever (the brk lesson).")]
        [Range(0.2f, 0.9f)] public float startOffsetFraction = 0.5f;

        [Header("The race")]
        [Tooltip("Candle length: seconds from launch until the pumpkin wins, unless a ghost breaks out first.")]
        [Min(5f)] public float timeLimitSeconds = 56.5f;

        [Header("Pacing")]
        [Tooltip("Still beat before the first ghost moves, so the setup (and the question) reads.")]
        [Range(0f, 3f)] public float preLaunchHoldSeconds = 1.5f;

        [Tooltip("Beat after the result so the payoff and the one-line why can be read.")]
        [Range(0.5f, 8f)] public float finishHoldSeconds = 5f;

        public int SegmentsInRing(int ring)
        {
            return segmentsPerRing != null && segmentsPerRing.Length > 0
                ? Mathf.Max(4, segmentsPerRing[Mathf.Min(ring, segmentsPerRing.Length - 1)])
                : 10 + 2 * ring;
        }

        public int HitPointsForRing(int ring)
        {
            return hitPointsPerRing != null && hitPointsPerRing.Length > 0
                ? Mathf.Max(1, hitPointsPerRing[Mathf.Min(ring, hitPointsPerRing.Length - 1)])
                : 1;
        }

        public float RingInnerRadius(int ring) => innerRadius + ring * ringSpacing;

        public float RingOuterRadius(int ring) => RingInnerRadius(ring) + ringThickness;

        /// <summary>Width of the hole one broken segment leaves, at the inner surface.</summary>
        public float HoleChord(int ring) => 2f * RingInnerRadius(ring) * Mathf.Sin(Mathf.PI / SegmentsInRing(ring));

        public float RunSeconds => preLaunchHoldSeconds + timeLimitSeconds + finishHoldSeconds;

        public override int MaxTicks => Mathf.Max(1, Mathf.CeilToInt((RunSeconds + 2f) / fixedTimestep));

        void OnValidate()
        {
            maxDurationSeconds = RunSeconds + 2f;
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

            for (int ring = 0; ring < ringCount; ring++)
            {
                if (ghostRadius * 2f >= HoleChord(ring))
                {
                    error = $"A ghost ({ghostRadius * 2f:0.00}) cannot fit through one broken segment of ring " +
                            $"{ring + 1} ({HoleChord(ring):0.00}). Fewer ghosts-sized holes, nothing escapes.";
                    return false;
                }
            }

            if (ringSpacing - ringThickness < ghostRadius * 4f)
            {
                error = "The corridor between rings is narrower than two ghosts; clones would be born inside a wall.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
