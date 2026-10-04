using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Breaker
{
    /// <summary>
    /// Tunables for "Can it break all the rings?". A published video is this asset + a scene + a seed.
    /// Ring geometry is baked into the scene by the builder from these values, so changing geometry
    /// here means rebuilding the scene.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BreakerConfig",
        menuName = "Simulation Lobby/Configs/Ring Breaker (brk)",
        order = 11)]
    public sealed class BreakerConfig : SimulationConfig
    {
        [Header("Rings (geometry — rebuild the scene after changing)")]
        [Range(1, 12)] public int ringCount = 5;

        [Tooltip("Inner radius of the innermost ring.")]
        [Min(0.3f)] public float innerRadius = 1.6f;

        [Tooltip("Distance between consecutive rings' inner radii.")]
        [Min(0.2f)] public float ringSpacing = 0.75f;

        [Min(0.05f)] public float ringThickness = 0.24f;

        [Tooltip("Segments in the innermost ring.")]
        [Range(4, 128)] public int innerSegments = 14;

        [Tooltip("Extra segments per ring outward, so outer segments don't become huge.")]
        [Range(0, 16)] public int extraSegmentsPerRing = 1;

        [Header("Toughness")]
        [Tooltip("Touches to break a segment of the innermost ring.")]
        [Range(1, 10)] public int innerHitPoints = 2;

        [Tooltip("Touches to break a segment of the outermost ring. Rings between are interpolated.")]
        [Range(1, 10)] public int outerHitPoints = 2;

        [Header("Spin")]
        [Tooltip("Ring rotation in degrees/second. Moving holes are what create near misses.")]
        public float ringAngularSpeed = 18f;

        [Tooltip("Alternate rings spin opposite ways.")]
        public bool alternateDirections = true;

        [Tooltip("Draw the overall spin direction from the seed instead of always starting clockwise.")]
        public bool spinDirectionSeeded = true;

        [Header("Ball")]
        [Min(0.02f)] public float ballRadius = 0.16f;

        [Tooltip("Speed inside the innermost ring, units/second.")]
        [Min(0.1f)] public float startSpeed = 6.8f;

        [Tooltip("Added each time a ring breaks: the escalation. The song speeds up with it.")]
        [Min(0f)] public float speedPerRing = 0.4f;

        [Tooltip("How far from the centre the ball may start, as a fraction of the room it has. " +
                 "Never zero: a ball launched from dead centre bounces along one diameter forever.")]
        [Range(0.2f, 0.95f)] public float startOffsetMax = 0.6f;

        [Header("Clock — how the ball can lose")]
        [Tooltip("Seconds to break every ring. Without a limit the ball always wins eventually, so the " +
                 "question has no stakes. 0 = no clock.")]
        [Min(0f)] public float timeLimitSeconds = 60f;

        [Header("Pacing")]
        [Tooltip("Seconds the run keeps going after the last ring breaks, so the ball clears the frame " +
                 "and the final shatter plays before the reveal.")]
        [Min(0f)] public float finishHoldSeconds = 1.2f;

        public bool HasClock => timeLimitSeconds > 0f;

        /// <summary>With a clock, the run's length is structural: the limit plus the end beat.</summary>
        public override int MaxTicks => HasClock
            ? Mathf.CeilToInt((timeLimitSeconds + finishHoldSeconds + 1f) / fixedTimestep)
            : base.MaxTicks;

        public float RingInnerRadius(int ring) => innerRadius + ring * ringSpacing;

        public float RingOuterRadius(int ring) => RingInnerRadius(ring) + ringThickness;

        public int SegmentsInRing(int ring) => innerSegments + ring * extraSegmentsPerRing;

        public int HitPointsForRing(int ring)
        {
            float t = ringCount > 1 ? ring / (float)(ringCount - 1) : 0f;
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(innerHitPoints, outerHitPoints, t)));
        }

        public float SpeedForRing(int ring) => startSpeed + Mathf.Max(0, ring) * speedPerRing;

        public float MaxSpeed => SpeedForRing(ringCount - 1);

        /// <summary>Hole a single broken segment leaves in a ring, at its inner surface.</summary>
        public float HoleChord(int ring) => 2f * RingInnerRadius(ring) * Mathf.Sin(Mathf.PI / SegmentsInRing(ring));

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            float ballDiameter = ballRadius * 2f;

            for (int ring = 0; ring < ringCount; ring++)
            {
                // A ball that can't fit through a one-segment hole would need two neighbouring breaks
                // to escape — a different, much slower game than the one this config describes.
                if (HoleChord(ring) < ballDiameter * 1.15f)
                {
                    error = $"ring {ring + 1}: a one-segment hole ({HoleChord(ring):0.00}) is too narrow for the ball " +
                            $"({ballDiameter:0.00}). Use fewer segments or a smaller ball.";
                    return false;
                }
            }

            if (ringSpacing - ringThickness < ballDiameter * 1.2f)
            {
                error = $"the gap between rings ({ringSpacing - ringThickness:0.00}) is too tight for the ball " +
                        $"({ballDiameter:0.00}) — it would wedge between them.";
                return false;
            }

            float safe = ConstantSpeed2D.MaxSafeSpeed(fixedTimestep, ringThickness);
            if (MaxSpeed > safe)
            {
                error = $"top speed {MaxSpeed:0.0} exceeds the safe ceiling {safe:0.0} for ring thickness " +
                        $"{ringThickness} at timestep {fixedTimestep} — the ball could tunnel through a ring.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
