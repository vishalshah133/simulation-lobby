using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Escape
{
    /// <summary>
    /// Tunables for "Will the Ball Escape?". A published video is this asset plus a scene plus a seed,
    /// so every knob that changes the footage lives here rather than in the scene or in code.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EscapeConfig",
        menuName = "Simulation Lobby/Configs/Escape (esc)",
        order = 10)]
    public sealed class EscapeConfig : SimulationConfig
    {
        [Header("Ball")]
        [Tooltip("Starting diameter as a fraction of the arena diameter. ~1/12 is the tuned start. " +
                 "Drawn from this range once per video, so every attempt in a series starts equal.")]
        [Range(0.02f, 0.35f)] public float initialSizeFractionMin = 1f / 13f;

        [Range(0.02f, 0.35f)] public float initialSizeFractionMax = 1f / 11f;

        [Tooltip("Re-draw the starting size for every attempt. Off by default: once a score is on " +
                 "screen, viewers read the attempts as a fair series, and a ball that started smaller " +
                 "having escaped is not the same achievement.")]
        public bool randomizeSizePerAttempt;

        [Tooltip("Constant speed in units/second, drawn per attempt. Speed does not escalate — " +
                 "do NOT also escalate speed on the first video.")]
        [Min(0.1f)] public float speedMin = 8f;

        [Min(0.1f)] public float speedMax = 10f;

        [Header("Series — how many attempts the video is")]
        [Tooltip("Rounds per video. More rounds means more resolution moments. Shorts allow 3 minutes, " +
                 "so 7 rounds of ~18s fits comfortably — no need to starve a round of bounces.")]
        [Range(1, 15)] public int attemptCount = 7;

        [Tooltip("Safety cap per round in simulated seconds, not a target — rounds normally end when " +
                 "the ball escapes or gets too big. Set well above the expected round length so a slow " +
                 "seed resolves properly instead of timing out, which reads as an anticlimax.")]
        [Min(2f)] public float maxAttemptSeconds = 24f;

        [Tooltip("Beat between rounds so the viewer registers the result before the reset.")]
        [Range(0f, 3f)] public float interAttemptSeconds = 0.8f;

        [Header("Resolution")]
        [Tooltip("End the attempt once the ball exceeds the gap by this fraction — escape has become " +
                 "impossible, and the margin still buys a near-miss or two at 'only just too big'. " +
                 "Without it, a hopeless ball bounces for 20s of dead footage.")]
        [Range(0f, 1f)] public float trappedMarginFraction = 0.1f;

        [Header("Escalation — the premise")]
        [Tooltip("Growth per bounce. Multiplicative 1.03 is already aggressive.")]
        public EscalationSettings growth = new EscalationSettings
        {
            mode = EscalationMode.Multiplicative,
            // ~29 bounces to the trapped threshold (~18s per round, ~130s for 7 rounds). Bounces are
            // the content here — each one is a note and a chance at the gap — so this is tuned for
            // bounce count first and total length second, now that Shorts allow 3 minutes.
            magnitude = 1.035f,
            startValue = 1f,
            minValue = 0.1f,
            maxValue = 6f
        };

        [Header("Arena")]
        [Tooltip("Gap width in degrees. The main tension dial — wider escapes sooner.")]
        [Range(2f, 90f)] public float gapDegrees = 24f;

        [Tooltip("Where the gap sits, degrees CCW from +X. 90 = top of the circle.")]
        [Range(0f, 360f)] public float gapCenterDegrees = 90f;

        [Tooltip("The gap's angular speed in degrees/second. 0 = static gap (esc-001 behaviour). " +
                 "Nonzero turns the boundary into the rotating-gap variant: escape becomes a timing " +
                 "problem as well as a size threshold.")]
        public float gapAngularSpeed;

        [Tooltip("Draw the rotation direction (CW/CCW) from the seed each attempt, magnitude fixed at " +
                 "|gapAngularSpeed|. Off uses gapAngularSpeed's sign literally for every attempt.")]
        public bool gapDirectionSeeded = true;

        [Header("Near misses — what makes the run watchable")]
        [Tooltip("A bounce this close to the gap edge (in gap widths) counts as a near miss.")]
        [Range(0.1f, 3f)] public float nearMissGapWidths = 1f;

        /// <summary>
        /// Worst-case length of the whole series in simulated seconds. The video's actual length is
        /// usually far shorter, since attempts end the moment the ball escapes or gets too big.
        /// </summary>
        public float SeriesSeconds => attemptCount * (maxAttemptSeconds + interAttemptSeconds);

        /// <summary>
        /// Derived from the series rather than from <c>maxDurationSeconds</c>. The run's cap and the
        /// series length are the same fact, and keeping them as two independently authored numbers
        /// meant any edit to attempt count or length could silently invalidate the config.
        /// </summary>
        public override int MaxTicks => Mathf.Max(1, Mathf.CeilToInt(SeriesSeconds / fixedTimestep));

        /// <summary>
        /// Keeps the inspector's <c>maxDurationSeconds</c> showing the truth. It no longer drives
        /// anything for this format, but a field that disagrees with actual behaviour is a trap.
        /// </summary>
        void OnValidate()
        {
            maxDurationSeconds = SeriesSeconds;
        }

        /// <summary>
        /// Catches configs that cannot produce a watchable run, before a scan burns time on them.
        /// </summary>
        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (growth.magnitude <= 1f && growth.mode == EscalationMode.Multiplicative)
            {
                error = "Multiplicative growth of <= 1 never grows the ball — the premise cannot resolve.";
                return false;
            }

            if (growth.maxValue <= growth.startValue)
            {
                error = "growth.maxValue must exceed startValue, or the ball is clamped from tick zero.";
                return false;
            }

            if (initialSizeFractionMin > initialSizeFractionMax)
            {
                error = "initialSizeFractionMin must not exceed initialSizeFractionMax.";
                return false;
            }

            if (speedMin > speedMax)
            {
                error = "speedMin must not exceed speedMax.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
