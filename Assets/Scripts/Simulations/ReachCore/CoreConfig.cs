using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.ReachCore
{
    /// <summary>
    /// Tunables for "Can the ball break in to the core?". A published video is this asset + a scene + a seed.
    /// Ring geometry is baked into the scene by the builder, so changing geometry means rebuilding the scene.
    /// Rings are indexed outermost first: ring 0 is the one the ball meets first.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CoreConfig",
        menuName = "Simulation Lobby/Configs/Reach the Core (core)",
        order = 12)]
    public sealed class CoreConfig : SimulationConfig
    {
        [Header("Rings (geometry — rebuild the scene after changing)")]
        [Range(1, 8)] public int ringCount = 4;

        [Tooltip("Inner radius of the outermost ring (the first one the ball meets).")]
        [Min(1f)] public float outerRingInnerRadius = 4.4f;

        [Tooltip("Distance between consecutive rings' inner radii, stepping inward.")]
        [Min(0.3f)] public float ringSpacing = 1f;

        [Min(0.05f)] public float ringThickness = 0.22f;

        [Tooltip("Segments in the outermost ring.")]
        [Range(4, 128)] public int outerSegments = 20;

        [Tooltip("Segments lost per ring inward, so inner segments stay a sensible size.")]
        [Range(0, 6)] public int segmentsLostPerRing = 2;

        [Header("Core")]
        [Min(0.1f)] public float coreRadius = 0.45f;

        [Header("Toughness")]
        [Tooltip("Touches to break a segment of the outermost ring. 1 = the first touch breaks, the instant hook.")]
        [Range(1, 10)] public int outerHitPoints = 1;

        [Tooltip("Touches to break a segment of the innermost ring. The core is armoured: it escalates.")]
        [Range(1, 10)] public int innerHitPoints = 6;

        [Tooltip("After a touch, the same segment ignores the ball for this long. Entering a ring through a hole " +
                 "sends the ball ping-ponging straight back and forth in the narrow gap, hitting one segment " +
                 "~10x a second; without this a 4-hit ring fell in under half a second.")]
        [Range(0f, 2f)] public float segmentCooldownSeconds = 0.8f;

        [Header("Path shaping (a ball in a thin gap either orbits it or ping-pongs along one radius)")]
        [Tooltip("Allowed angle between the ball's path and the radial line, in degrees. 0 = straight along the " +
                 "radius (ping-pong on one segment), 90 = skimming round the gap (orbit). Outside this band the " +
                 "ball is nudged back in on each wall bounce.")]
        public Vector2 incidenceBandDegrees = new Vector2(25f, 65f);

        [Tooltip("If no segment is touched for this long, the ball is re-aimed inside the band. 0 = off.")]
        [Min(0f)] public float stallSeconds = 2.5f;

        [Header("Spin")]
        [Tooltip("Ring rotation in degrees/second. Moving holes are what create near misses.")]
        public float ringAngularSpeed = 18f;

        public bool alternateDirections = true;

        [Tooltip("Draw the overall spin direction from the seed instead of always starting clockwise.")]
        public bool spinDirectionSeeded = true;

        [Header("Ball")]
        [Min(0.02f)] public float ballRadius = 0.16f;

        [Min(0.1f)] public float startSpeed = 3.6f;

        [Tooltip("Added each time a ring breaks: the escalation. The song speeds up with it.")]
        [Min(0f)] public float speedPerRing = 0.2f;

        [Header("Clock — how the ball can lose")]
        [Tooltip("Seconds to reach the core. Without a limit the ball always wins eventually, so the " +
                 "question has no stakes. 0 = no clock. Shorts run 60-120 s end to end: keep this at or " +
                 "under ~105 and tune toughness so most wins land past ~60 s.")]
        [Min(0f)] public float timeLimitSeconds = 100f;

        [Header("Pacing")]
        [Min(0f)] public float finishHoldSeconds = 1.2f;

        public bool HasClock => timeLimitSeconds > 0f;

        public override int MaxTicks => HasClock
            ? Mathf.CeilToInt((timeLimitSeconds + finishHoldSeconds + 1f) / fixedTimestep)
            : base.MaxTicks;

        public float RingInnerRadius(int ring) => outerRingInnerRadius - ring * ringSpacing;

        public float RingOuterRadius(int ring) => RingInnerRadius(ring) + ringThickness;

        public int SegmentsInRing(int ring) => Mathf.Max(6, outerSegments - ring * segmentsLostPerRing);

        /// <summary>
        /// Radius of the containment wall while the ball works on <paramref name="ring"/>. It sits just
        /// outside the previous ring's inner surface, so clearing a ring visibly closes the arena in.
        /// </summary>
        public float WallRadius(int ring) => ring == 0
            ? RingOuterRadius(0) + (ringSpacing - ringThickness)
            : RingInnerRadius(ring - 1) + 0.04f;

        /// <summary>Wall radius for the last chamber, once every ring is gone and only the core is left.</summary>
        public float FinalWallRadius => RingInnerRadius(ringCount - 1) + 0.04f;

        public int HitPointsForRing(int ring)
        {
            float t = ringCount > 1 ? ring / (float)(ringCount - 1) : 0f;
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(outerHitPoints, innerHitPoints, t)));
        }

        public float SpeedForRing(int ring) => startSpeed + Mathf.Max(0, ring) * speedPerRing;

        public float MaxSpeed => SpeedForRing(ringCount - 1);

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

            if (FinalWallRadius < coreRadius + ballDiameter * 2f)
            {
                error = $"the last chamber (radius {FinalWallRadius:0.00}) has no room around the core " +
                        $"({coreRadius:0.00}). Use fewer rings or a wider outer radius.";
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
