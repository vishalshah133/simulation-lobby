using SimulationLobby.Core;
using SimulationLobby.Simulations.Convergence.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.Convergence
{
    /// <summary>What a round is meant to show. Declared by the config, then checked against the run.</summary>
    public enum ConvergenceAnswer
    {
        Meet = 0,
        Miss = 1
    }

    /// <summary>The topic-independent part of a round: its caption, its answer and its timing.</summary>
    [System.Serializable]
    public struct ConvergenceRoundInfo
    {
        [Tooltip("Bottom-panel line during this round's transition, hold and run, e.g. WATCH BLUE.")]
        public string caption;

        [Tooltip("Result line once the verdict is in. {n} = the verdict count, {total} = ballCount.")]
        public string result;

        [Tooltip("What this round is meant to show. A run that disagrees logs an error, so a mis-tuned " +
                 "round never ships with the wrong caption.")]
        public ConvergenceAnswer expected;

        [Tooltip("Seconds of motion after launch.")]
        [Min(0.5f)] public float runSeconds;

        [Tooltip("Trail length for this round's bodies, seconds. Longer in a break round draws the caustic. 0 = trails off.")]
        [Range(0f, 4f)] public float trailSeconds;

        [Tooltip("Bodies launched this round. 0 = use the config's ballCount.")]
        [Range(0, 1000)] public int ballCount;
    }

    /// <summary>
    /// Base config for the <c>conv</c> genre: the round shell every topic shares. A topic (ellipse
    /// focus, cycloid, pendulum wave) derives from this, holds its own per-round geometry, and builds
    /// the kernel for each round. The simulation and presenter only ever see this base.
    /// </summary>
    public abstract class ConvergenceConfig : SimulationConfig
    {
        [Header("Bodies")]
        [Tooltip("Bodies launched each round.")]
        [Range(1, 1000)] public int ballCount = 120;

        [Tooltip("Drawn radius of a body, world units. Presentation only.")]
        [Range(0.02f, 0.3f)] public float ballRadius = 0.09f;

        [Tooltip("Bodies sit frozen at their launch positions through Hold, so the row is seen before it drops " +
                 "(an anticipation beat). Off = bodies first appear at launch.")]
        public bool showBodiesDuringHold = false;

        [Tooltip("Show the red start dot and its ring. Off for a topic with no start point.")]
        public bool showStartPoint = true;

        [Tooltip("Fraction of the hue wheel spread across the bodies. 1 = the full wheel (a fan, where the hue is " +
                 "the launch angle). Below 1 for a row, so the two ends are not both red.")]
        [Range(0.1f, 1f)] public float hueSpan = 1f;

        [Header("Meeting")]
        [Tooltip("A body counts as at the target point within this distance, world units.")]
        [Range(0.02f, 1f)] public float meetRadius = 0.25f;

        [Tooltip("Fraction of bodies at the target at once that counts as MEET.")]
        [Range(0.5f, 1f)] public float meetFraction = 0.95f;

        [Tooltip("The verdict is the most bodies at the target at any moment from launch until this many " +
                 "seconds after the expected meet time (the whole first pass).")]
        [Range(0.02f, 1f)] public float verdictWindowSeconds = 0.06f;

        [Header("Pacing")]
        [Tooltip("Static beat before launch with the start point pulsing. The first round's is longer: it's the hook.")]
        [Min(0f)] public float firstHoldSeconds = 1.5f;

        [Min(0f)] public float preLaunchHoldSeconds = 0.6f;

        [Tooltip("Result caption on screen, bodies frozen in place, before the next round.")]
        [Min(0f)] public float resultHoldSeconds = 1.5f;

        [Tooltip("Between rounds: the start point glides to its next spot (and the boundary morphs if the shape differs).")]
        [Min(0f)] public float transitionSeconds = 1.5f;

        [Tooltip("End card after the last round.")]
        [Min(0f)] public float endCardSeconds = 3f;

        [Header("Text")]
        [Tooltip("If not empty, the bottom line during every round's Hold (the beat before launch), e.g. GUESS?. " +
                 "Empty = the round's own caption stays up.")]
        public string holdCaption = string.Empty;

        [Tooltip("Live counter prefix while running, e.g. HIT BLUE -> 'HIT BLUE  37 / 120'.")]
        public string counterLabel = "HIT BLUE";

        public string title = "THE MAGIC POINT";

        [Tooltip("If not empty, replaces the title in the top panel during the end card, e.g. IT'S CALLED A PARABOLA. Empty = the title stays.")]
        public string endTitle = string.Empty;

        public string question = "120 BALLS FROM RED. HOW MANY HIT BLUE?";

        public string endCard = "ONLY THIS POINT WORKS. IT'S CALLED A FOCUS.";

        [Tooltip("Decal beside the ghost ring that marks the ideal start (the focus) while red is off it. " +
                 "2-3 words; empty hides it. Names the spot without spoiling the end card's explanation.")]
        public string idealSpotLabel = "MAGIC POINT";

        [Header("Cosmetic seed use")]
        [Tooltip("Largest seeded rotation of the launch fan, degrees. Cosmetic: the answer doesn't depend on it.")]
        [Range(0f, 5f)] public float maxAngleOffsetDegrees = 1f;

        /// <summary>Rounds in the video.</summary>
        public abstract int RoundCount { get; }

        /// <summary>Caption, answer and timing for round <paramref name="index"/> (0-based).</summary>
        public abstract ConvergenceRoundInfo GetRound(int index);

        /// <summary>Topic geometry for round <paramref name="index"/> as a short string, for the scene builder's log. Empty when a topic has none to add.</summary>
        public virtual string DescribeRound(int index) => string.Empty;

        /// <summary>A fresh kernel holding round <paramref name="index"/>'s geometry, not yet launched.</summary>
        public abstract IConvergenceKernel CreateKernel(int index);

        /// <summary>Bodies needed at the target at once for a MEET, at the config's default ball count.</summary>
        public int MeetThreshold => Mathf.CeilToInt(ballCount * meetFraction);

        /// <summary>Bodies launched in round <paramref name="index"/>: its own count, or the config's when it sets none.</summary>
        public int RoundBallCount(int index)
        {
            int own = GetRound(index).ballCount;
            return own > 0 ? own : ballCount;
        }

        /// <summary>Bodies needed at the target at once for round <paramref name="index"/> to be a MEET.</summary>
        public int RoundMeetThreshold(int index) => Mathf.CeilToInt(RoundBallCount(index) * meetFraction);

        /// <summary>Total length of the video in simulated seconds, derived from the rounds.</summary>
        public float SeriesSeconds
        {
            get
            {
                float total = endCardSeconds;
                for (int i = 0; i < RoundCount; i++)
                {
                    total += (i == 0 ? firstHoldSeconds : preLaunchHoldSeconds + transitionSeconds) +
                             GetRound(i).runSeconds + resultHoldSeconds;
                }

                return total;
            }
        }

        /// <summary>The run's length is structural, so the cap follows the rounds plus a margin.</summary>
        public override int MaxTicks => Mathf.CeilToInt((SeriesSeconds + 5f) / fixedTimestep);

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (RoundCount <= 0)
            {
                error = "A conv video needs at least one round.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
