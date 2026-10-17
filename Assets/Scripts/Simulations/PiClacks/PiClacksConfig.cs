using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.PiClacks
{
    /// <summary>
    /// One round of the <c>clack</c> format: the big block's mass, how it is drawn, where everything starts,
    /// and the words around it. Only the mass changes the clack count; the gaps and speed are framing and pacing.
    /// </summary>
    [System.Serializable]
    public struct PiClacksRound
    {
        [Tooltip("Big mass = 100^massExponent (small mass = 1). The clack count is the first massExponent+1 digits of pi.")]
        [Min(0)] public int massExponent;

        [Tooltip("Decal on the big block, e.g. 100 kg, 10K kg, 1T kg.")]
        public string massLabel;

        [Tooltip("Drawn side of the big block, world units. Grows on a log scale because 100x can't be drawn.")]
        [Min(0.1f)] public float bigSize;

        [Tooltip("Wall to the small block's left face at the start.")]
        [Min(0.05f)] public float smallGap;

        [Tooltip("Small block's right face to the big block's left face at the start.")]
        [Min(0.05f)] public float bigGap;

        [Tooltip("Big block's starting speed toward the wall, units per second. Gaps and speed set the pace: " +
                 "every event time scales with gap / speed.")]
        [Min(0.1f)] public float speed;

        [Tooltip("Measured launch-to-done seconds from the harness at the config's fixed timestep. Only feeds " +
                 "the SeriesSeconds estimate; the run itself ends when no clack is possible.")]
        [Min(0f)] public float expectedRunSeconds;

        [Tooltip("Safety cap: a run still going after this many seconds logs an error and ends.")]
        [Min(1f)] public float maxRunSeconds;

        [Tooltip("Bottom line during the transition and the run.")]
        public string caption;

        [Tooltip("Bottom line during the hold before launch. Empty = the config's holdCaption.")]
        public string holdCaption;

        [Tooltip("Result line once the last clack has happened. {n} = the count.")]
        public string result;
    }

    /// <summary>
    /// Config for the <c>clack</c> format (Pi Clacks): the same question asked once per round with the big block
    /// 100x heavier each time. A published video = this asset + a (cosmetic) seed.
    /// </summary>
    [CreateAssetMenu(fileName = "PiClacksConfig", menuName = "Simulation Lobby/Pi Clacks Config")]
    public sealed class PiClacksConfig : SimulationConfig
    {
        [Header("Rounds")]
        [Tooltip("One entry per round, in order. Defaults are the seven storyboard rounds: 3, 31, 314 ... 3141592.")]
        public PiClacksRound[] rounds = DefaultRounds();

        [Header("Blocks and frame")]
        [Tooltip("Drawn side of the small block, world units.")]
        [Min(0.1f)] public float smallSize = 1f;

        [Tooltip("Decal on the small block.")]
        public string smallMassLabel = "1 kg";

        [Tooltip("Visible world width. The wall face sits 0.6 in from the left edge, so every contact must stay " +
                 "within frameWidth - 1.2 of the wall. The harness checks it.")]
        [Min(4f)] public float frameWidth = 12f;

        [Header("Pacing")]
        [Tooltip("Static beat before round 1: the hook.")]
        [Min(0f)] public float firstHoldSeconds = 1.5f;

        [Tooltip("Static beat before each later round's launch, after the transition.")]
        [Min(0f)] public float preLaunchHoldSeconds = 1.2f;

        [Tooltip("Between rounds: blocks glide to the next start, the big block grows and its label flips.")]
        [Min(0f)] public float transitionSeconds = 1.5f;

        [Tooltip("Result line up, blocks still gliding, before the next round.")]
        [Min(0f)] public float resultHoldSeconds = 1.8f;

        [Tooltip("End card after the last round (blocks settle at the start layout, pi line under the ladder).")]
        [Min(0f)] public float endCardSeconds = 4f;

        [Header("Text")]
        public string title = "PI BLOCKS";

        public string question = "HOW MANY TIMES WILL THEY CLACK?";

        [Tooltip("Bottom line during a round's hold when the round sets none.")]
        public string holdCaption = "GUESS?";

        [Tooltip("Live counter prefix, e.g. CLACKS -> 'CLACKS 314'.")]
        public string counterLabel = "CLACKS";

        public string endCard = "EVERY 100× HEAVIER = ONE MORE DIGIT OF π";

        [Tooltip("Shown under the ladder on the end card.")]
        public string endLine = "π = 3.1415926…";

        public int RoundCount => rounds != null ? rounds.Length : 0;

        /// <summary>
        /// Estimated video length in simulated seconds: holds, transitions, results and the end card plus each
        /// round's <see cref="PiClacksRound.expectedRunSeconds"/> (harness-measured; falls back to the cap).
        /// </summary>
        public float SeriesSeconds
        {
            get
            {
                float total = endCardSeconds;
                for (int i = 0; i < RoundCount; i++)
                {
                    PiClacksRound r = rounds[i];
                    float run = r.expectedRunSeconds > 0f ? r.expectedRunSeconds : r.maxRunSeconds;
                    total += (i == 0 ? firstHoldSeconds : transitionSeconds + preLaunchHoldSeconds) + run + resultHoldSeconds;
                }

                return total;
            }
        }

        /// <summary>
        /// The length is structural, so the cap is the worst case (every run at its safety cap) plus a margin,
        /// not the estimate.
        /// </summary>
        public override int MaxTicks
        {
            get
            {
                float total = endCardSeconds + 5f;
                for (int i = 0; i < RoundCount; i++)
                {
                    total += (i == 0 ? firstHoldSeconds : transitionSeconds + preLaunchHoldSeconds) +
                             rounds[i].maxRunSeconds + resultHoldSeconds;
                }

                return Mathf.Max(1, Mathf.CeilToInt(total / fixedTimestep));
            }
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (RoundCount <= 0)
            {
                error = "A clack video needs at least one round.";
                return false;
            }

            for (int i = 0; i < RoundCount; i++)
            {
                PiClacksRound r = rounds[i];
                if (r.speed <= 0f || r.smallGap <= 0f || r.bigGap <= 0f || r.maxRunSeconds <= 0f)
                {
                    error = $"Round {i + 1}: speed, both gaps and maxRunSeconds must be greater than zero.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// The seven storyboard rounds. Gaps 2/2 and speed 2 give three clacks exactly 1 s apart in round 1 and
        /// keep every contact inside a 12-wide frame (rightmost x = 8.54, round 6); expectedRunSeconds are the
        /// harness's launch-to-done times at 1/60 s (round 3's last clack lands at 7.9 s: the "wait..." beat).
        /// </summary>
        public static PiClacksRound[] DefaultRounds()
        {
            return new[]
            {
                Round(0, "1 kg", 1.00f, 3.00f, "WATCH THE COUNTER", "", "{n} CLACKS."),
                Round(1, "100 kg", 1.35f, 3.93f, "NOW 100× HEAVIER...", "", "{n} CLACKS."),
                Round(2, "10K kg", 1.70f, 7.90f, "100× HEAVIER AGAIN...", "", "{n}. WAIT..."),
                Round(3, "1M kg", 2.00f, 3.70f, "1 MILLION kg...", "GUESS THE NEXT ONE?", "{n}. IT'S π!"),
                Round(4, "100M kg", 2.35f, 3.08f, "100 MILLION kg...", "", "{n}. STILL π."),
                Round(5, "10B kg", 2.70f, 5.78f, "10 BILLION kg...", "", "{n}!"),
                Round(6, "1T kg", 3.00f, 3.53f, "1 TRILLION kg...", "", "{n}. EXACTLY π.")
            };
        }

        static PiClacksRound Round(int exponent, string label, float bigSize, float runSeconds,
                                   string caption, string holdCaption, string result)
        {
            return new PiClacksRound
            {
                massExponent = exponent,
                massLabel = label,
                bigSize = bigSize,
                smallGap = 2f,
                bigGap = 2f,
                speed = 2f,
                expectedRunSeconds = runSeconds,
                maxRunSeconds = 10f,
                caption = caption,
                holdCaption = holdCaption,
                result = result
            };
        }
    }
}
