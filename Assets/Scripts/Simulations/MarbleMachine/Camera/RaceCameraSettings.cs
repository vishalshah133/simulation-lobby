using System;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>One set-piece shot: where the camera goes while the focus is inside a station's arc-length range.</summary>
    [Serializable]
    public struct ZoneShot
    {
        [Tooltip("Case-insensitive start of the station's displayName this shot applies to.")]
        public string match;
        [Tooltip("Camera azimuth off the course heading in degrees: 0 directly behind, 90 the outside flank, 180 in front.")]
        public float azimuth;
        [Tooltip("Camera elevation angle above the look point, degrees.")]
        public float elevation;
        [Tooltip("The camera never sits closer than this to the look point while the shot is on.")]
        public float minDistance;
        [Tooltip("How far the look point is pulled from the followed racers toward the station focus (0 none, 1 on the focus).")]
        [Range(0f, 1f)] public float focusPull;
        [Tooltip("Weight of the real racers' offset from the course rail in the look point (1 normal, low = stay on the rail).")]
        public float offsetWeight;
        [Tooltip("Metres of course before the station where the shot starts to blend in.")]
        public float preRoll;
        [Tooltip("Metres of course after the station where the shot is held before blending out.")]
        public float postRoll;

        public ZoneShot(string match, float azimuth, float elevation, float minDistance, float focusPull, float offsetWeight, float preRoll, float postRoll)
        {
            this.match = match;
            this.azimuth = azimuth;
            this.elevation = elevation;
            this.minDistance = minDistance;
            this.focusPull = focusPull;
            this.offsetWeight = offsetWeight;
            this.preRoll = preRoll;
            this.postRoll = postRoll;
        }
    }

    /// <summary>
    /// Every tunable of the mach-002 race camera (see plans/videos/mach-002-camera-rework.md). Plain data so the director, the
    /// pure <see cref="RaceCameraCore"/> and the offline smoothness harness share one set of numbers.
    /// </summary>
    [Serializable]
    public sealed class RaceCameraSettings
    {
        [Header("Time")]
        [Tooltip("Longest frame time the camera integrates (a hitch is treated as this long).")]
        public float maxDt = 0.25f;
        [Tooltip("Substeps per second: every filter steps ceil(dt * this) times per frame.")]
        public float substepHz = 120f;

        [Header("Opening")]
        [Tooltip("Opening shot margin over the tight fit of the grid.")]
        public float openingMargin = 1.2f;
        [Tooltip("Countdown push-in length in seconds (the director measures it from when the countdown is first seen).")]
        public float countdownSeconds = 3f;
        [Tooltip("Countdown push-in ends at this fraction of the opening distance.")]
        public float countdownPushIn = 0.62f;
        [Tooltip("Never pushes in closer than this to the grid.")]
        public float minPushDistance = 5f;

        [Header("Phase crossfades (seconds)")]
        public float openToFollowFade = 3f;
        public float followToWinnerFade = 1.5f;
        public float winnerToOutroFade = 4f;
        [Tooltip("Seconds the winner shot is held before the outro pull-back starts.")]
        public float winnerDwell = 6f;
        [Tooltip("Pose spring omegas are scaled by this during the outro.")]
        public float outroOmegaScale = 0.6f;
        public float outroDistanceMaxSpeed = 5f;
        public float outroDistanceFactor = 1.1f;

        [Header("Eligibility")]
        [Tooltip("A recycled racer is ignored for this long after the recycle is seen, then ramps in over the next seconds.")]
        public float recycleBlockSeconds = 8f;
        public float recycleRampEnd = 10f;
        [Tooltip("A racer farther than this from its place on the rail starts to fade out of the pack (a ball in the cannon).")]
        public float offCourseStart = 5f;
        public float offCourseEnd = 8f;

        [Header("Leader hysteresis")]
        [Tooltip("A challenger this far ahead takes the camera at once.")]
        public float leaderSwitchNowGap = 8f;
        [Tooltip("A challenger this far ahead for leaderHoldSeconds takes the camera.")]
        public float leaderSwitchHoldGap = 2f;
        public float leaderHoldSeconds = 1.5f;

        [Header("Pack")]
        public int packCount = 6;
        public float packLength = 24f;
        public int finalPackCount = 4;
        public float finalPackLength = 18f;
        [Tooltip("Rank weight falloff for the followers behind the leader.")]
        public float packWeightDecay = 0.85f;
        [Tooltip("Fraction of the pack length where a straggler starts to fade out.")]
        public float packFadeStart = 0.65f;
        [Tooltip("Time constant of the leader speed estimate used as feed-forward.")]
        public float speedFilterSeconds = 0.6f;
        public float speedFilterMax = 14f;
        [Tooltip("The focus sits this fraction of the pack extent behind the leader (leader ahead of centre, pack behind).")]
        public float focusBackFraction = 0.4f;

        [Header("Focus springs: omega / max speed / max accel")]
        public float focusOmega = 1.4f;
        public float focusSpeedMin = -2f;
        public float focusSpeedMax = 16f;
        public float focusAccelMax = 2.5f;
        public float extentOmega = 0.7f;
        public float extentSpeedMax = 4f;
        public float extentAccelMax = 3f;
        public float offsetOmega = 0.9f;
        public float offsetSpeedMax = 3f;
        public float offsetAccelMax = 3f;
        public float verticalOmega = 0.7f;
        public float verticalSpeedMax = 3f;
        public float verticalAccelMax = 3f;

        [Header("Pose springs: omega / max speed / max accel")]
        public float lookOmega = 2.5f;
        public float lookSpeedMax = 18f;
        public float lookAccelMax = 2.8f;
        public float yawOmega = 0.9f;
        public float yawSpeedMax = 11f;
        [Tooltip("Faster yaw allowed for the first seconds after GO, for the swing from the grid shot to the chase.")]
        public float yawSpeedMaxAfterGo = 25f;
        public float yawAfterGoSeconds = 6f;
        public float yawAccelMax = 12f;
        public float pitchOmega = 1f;
        public float pitchSpeedMax = 8f;
        public float pitchAccelMax = 15f;
        public float distanceOmegaOut = 0.8f;
        public float distanceOmegaIn = 0.5f;
        public float distanceSpeedMax = 3f;
        public float distanceAccelMax = 2f;
        public float openingDistanceOmega = 1.5f;
        public float openingDistanceSpeedMax = 4f;
        public float lookFeedForwardMax = 18f;

        [Header("Follow shot")]
        public float azimuthPortrait = 30f;
        public float azimuthLandscape = 62f;
        public float pitch = 24f;
        [Tooltip("Metres above the rail the look point sits.")]
        public float lookHeight = 0.3f;
        [Tooltip("Look-ahead metres per m/s of focus speed, capped by lookAheadMax.")]
        public float lookAheadGain = 0.5f;
        public float lookAheadMax = 4f;
        [Tooltip("The look point leans toward the real racers by at most this far horizontally (the rail uses chords).")]
        public float racerOffsetMax = 6f;
        public float racerOffsetMaxY = 4f;
        [Tooltip("Fraction of the frame the pack may fill (0.58 leaves a margin for a leader that outruns the focus).")]
        public float fitFill = 0.58f;
        public float fitMarginWidth = 2.5f;
        public float fitMarginHeight = 1f;
        public float minDistance = 11f;
        public float minDistanceFinalLap = 10f;
        public float maxDistance = 32f;
        [Tooltip("Landscape: aim this fraction of the half-width to the right, so the action sits left of centre, away from the board.")]
        public float aimOffsetLandscape = 0.15f;

        [Header("Winner shot")]
        public float winnerAzimuth = 60f;
        public float winnerPitch = 22f;
        public float winnerMinDistance = 9f;

        [Header("Ground and obstruction")]
        public float groundClearance = 1f;
        public float minPitch = 8f;
        public float maxPitch = 65f;
        [Tooltip("Pitch added at full obstruction (the camera rises over the obstacle).")]
        public float obstructionPitch = 18f;
        [Tooltip("Distance shrink at full obstruction.")]
        public float obstructionShrink = 0.2f;
        public float obstructionRiseSeconds = 0.6f;
        public float obstructionFallSeconds = 2f;

        [Header("Zones")]
        public ZoneShot[] zones = DefaultZones();
        public float zoneBlendInSeconds = 2f;
        public float zoneBlendOutSeconds = 2.5f;
        public float zoneMinActiveSeconds = 3f;

        public static ZoneShot[] DefaultZones()
        {
            return new[]
            {
                new ZoneShot("Helix", 95f, 24f, 15f, 0.5f, 0.3f, 6f, 3f),
                new ZoneShot("Screw Lift", 90f, 18f, 16f, 0.1f, 1.0f, 6f, 2f),
                new ZoneShot("Peg Wall", 130f, 22f, 12f, 0.6f, 0.6f, 6f, 3f),
                new ZoneShot("Bucket Wheel", 90f, 16f, 14f, 0.5f, 0.6f, 5f, 3f),
                new ZoneShot("Ski Jump", 80f, 16f, 13f, 0.5f, 0.8f, 15f, 4f),
                new ZoneShot("Vortex", 55f, 50f, 13f, 0.7f, 0.2f, 6f, 4f),
            };
        }
    }
}
