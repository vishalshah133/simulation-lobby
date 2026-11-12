using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.Timer
{
    /// <summary>Palette family the scene builder dresses the course in. Data only: the mechanism never changes.</summary>
    public enum TimerTheme { Cartoon, Neon, Christmas, Halloween, Winter }

    /// <summary>
    /// Config for <c>timer</c> (marble-run timer): a hopper releases a steady stream of marbles down the course
    /// into a collector, timed so the last marble lands around 0:00.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TimerConfig",
        menuName = "Simulation Lobby/Configs/Timer (timer)",
        order = 19)]
    public sealed class TimerConfig : SimulationConfig
    {
        [Tooltip("Set by the scene builder. When it is behind the builder's version, the build rewrites this asset to the storyboard defaults.")]
        public int defaultsVersion;

        [Header("Clock")]
        [Tooltip("Simulated seconds from release to 0:00. Exact by construction.")]
        [Min(10f)] public float durationSeconds = 300f;

        [Tooltip("Scene sits static and lit this long before the release.")]
        [Min(0f)] public float setupHoldSeconds = 5f;

        [Tooltip("The sim stays alive this long after 0:00 so the finale and result can show.")]
        [Min(0f)] public float finishTailSeconds = 8f;

        [Header("Marbles")]
        [Min(1)] public int marbleCount = 300;
        [Min(0.05f)] public float marbleRadius = 0.2f;
        [Min(0.01f)] public float marbleMass = 1f;
        [Min(0f)] public float marbleLinearDamping = 0.1f;
        [Min(0f)] public float marbleAngularDamping = 0.05f;
        [Min(0.1f)] public float maxDepenetrationVelocity = 3f;

        [Header("Release")]
        [Tooltip("Typical release-to-collector travel time. The release window ends this long before 0:00.")]
        [Min(0f)] public float expectedTravelSeconds = 120f;

        [Tooltip("Seeded sideways offset (metres) applied to each release.")]
        [Min(0f)] public float releaseJitterLateral = 0.25f;

        [Tooltip("Marbles per release event.")]
        [Min(1)] public int releaseBurst = 1;

        [Header("On-screen wording (read by the HUD)")]
        [Tooltip("The question card. {0} = marble count, {1} = duration as '5 MINUTES' or '90 SECONDS'.")]
        public string questionFormat = "HOW MANY OF {0} MARBLES MAKE IT IN {1}?";

        [Tooltip("Celebration card when every marble reaches the bucket. {0} = marble count.")]
        public string allInFormat = "ALL {0} MADE IT!";

        [Tooltip("Result card at 0:00 when not all made it. {0} = collected, {1} = marble count.")]
        public string resultFormat = "{0} OF {1} MADE IT";

        [Header("Colours (round-robin by marble index)")]
        public Color[] colors =
        {
            new Color(0.95f, 0.15f, 0.15f, 1f),
            new Color(1f, 0.5f, 0.05f, 1f),
            new Color(1f, 0.9f, 0.1f, 1f),
            new Color(0.5f, 0.95f, 0.15f, 1f),
            new Color(0.1f, 0.85f, 0.9f, 1f),
            new Color(0.15f, 0.35f, 0.95f, 1f),
            new Color(0.6f, 0.2f, 0.9f, 1f),
            new Color(1f, 0.35f, 0.7f, 1f)
        };

        [Header("Safety")]
        [Tooltip("A marble further than this from the centreline is put back a little upstream.")]
        [Min(0.5f)] public float offTrackDistance = 3f;

        [Header("Theme (read by the scene builder)")]
        public TimerTheme theme = TimerTheme.Cartoon;

        public float WorstCaseSeconds => setupHoldSeconds + durationSeconds + finishTailSeconds;

        public override int MaxTicks => Mathf.CeilToInt((WorstCaseSeconds + 5f) / fixedTimestep);

        void OnValidate()
        {
            maxDurationSeconds = Mathf.Max(maxDurationSeconds, WorstCaseSeconds + 5f);
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (marbleCount <= 0)
            {
                error = "A timer video needs at least one marble.";
                return false;
            }

            if (colors == null || colors.Length == 0)
            {
                error = "colors must hold at least one colour.";
                return false;
            }

            if (releaseBurst < 1 || releaseBurst > marbleCount)
            {
                error = "releaseBurst must be between 1 and marbleCount.";
                return false;
            }

            if (expectedTravelSeconds < 0f || expectedTravelSeconds >= durationSeconds)
            {
                error = "expectedTravelSeconds must be 0 or more and less than durationSeconds.";
                return false;
            }

            if (marbleRadius <= 0f || marbleMass <= 0f || offTrackDistance <= 0f)
            {
                error = "marbleRadius, marbleMass and offTrackDistance must be positive.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
