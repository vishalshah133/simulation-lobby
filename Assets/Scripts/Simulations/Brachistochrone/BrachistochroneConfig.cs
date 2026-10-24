using SimulationLobby.Core;
using SimulationLobby.Simulations.Brachistochrone.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.Brachistochrone
{
    /// <summary>One lane in a round: which shape it is, its one-word decal, and its shape parameter.</summary>
    [System.Serializable]
    public struct BrachTrack
    {
        public CurveKind kind;

        [Tooltip("One-word decal near the track: STRAIGHT, CURVE, STEEP, CIRCLE, PARABOLA, DEEP.")]
        public string label;

        [Tooltip("SteepDrop: bend radius as a fraction of the finish x. DeepDip: depth factor (about 0.8-1.2). Ignored by the rest.")]
        public float shape;
    }

    /// <summary>
    /// One round of the <c>brach</c> format: which tracks race, where the finish is and the words around it.
    /// tracks[i] keeps its colour across rounds when its kind repeats at the same index.
    /// </summary>
    [System.Serializable]
    public struct BrachRound
    {
        public BrachTrack[] tracks;

        [Tooltip("B in world units (A is the origin, y is the drop, down).")]
        public float finishX, finishY;

        [Tooltip("Bottom line during the transition and the run.")]
        public string caption;

        [Tooltip("Bottom line during the hold before launch. Empty = the config's holdCaption.")]
        public string holdCaption;

        [Tooltip("Bottom line once every ball has finished.")]
        public string result;

        [Tooltip("Round 1: path lengths fade in on the result.")]
        public bool showLengths;

        [Tooltip("Safety cap in real seconds: a run that hits it logs an error and ends.")]
        [Min(1f)] public float maxRunSeconds;

        [Header("Extras beyond the contract")]
        [Tooltip("Replaces caption in the second half of the transition and during the run. Empty = keep caption.")]
        public string caption2;

        [Tooltip("Replaces result from 40% of the result phase on. Empty = keep result.")]
        public string result2;

        [Tooltip("Result phase length for this round. 0 = the config's resultHoldSeconds.")]
        [Min(0f)] public float resultSeconds;

        [Tooltip("Harness-measured launch-to-last-finish real seconds (slow motion included). Feeds SeriesSeconds only.")]
        [Min(0f)] public float expectedRunSeconds;
    }

    /// <summary>
    /// Config for the <c>brach</c> format (Fastest Path): balls on differently shaped tracks from one start to one
    /// finish; the cycloid wins every round. A published video = this asset + a (cosmetic) seed.
    /// </summary>
    [CreateAssetMenu(fileName = "BrachistochroneConfig", menuName = "Simulation Lobby/Brachistochrone Config")]
    public sealed class BrachistochroneConfig : SimulationConfig
    {
        [Header("Rounds")]
        [Tooltip("One entry per round, in order. Defaults are the five storyboard rounds.")]
        public BrachRound[] rounds = DefaultRounds();

        [Header("Physics and frame")]
        [Tooltip("Gravity for the bead (world units per s^2). Tuned so round 1's winner takes ~3.5 s.")]
        [Min(0.1f)] public float gravity = DefaultGravity;

        [Tooltip("Visible world width.")]
        [Min(4f)] public float frameWidth = 10f;

        [Tooltip("Polyline samples per track.")]
        [Range(64, 8000)] public int pathSamples = 2000;

        [Header("Pacing")]
        [Tooltip("Static beat before round 1: the hook.")]
        [Min(0f)] public float firstHoldSeconds = 3.5f;

        [Tooltip("Static beat before each later round's launch, after the transition.")]
        [Min(0f)] public float preLaunchHoldSeconds = 2.4f;

        [Tooltip("Between rounds: balls glide back to A, new tracks grow in, tracks morph to the new finish.")]
        [Min(0f)] public float transitionSeconds = 3f;

        [Tooltip("Result line up, balls resting at B (a round may override this).")]
        [Min(0f)] public float resultHoldSeconds = 3.2f;

        [Tooltip("End card after the last round.")]
        [Min(0f)] public float endCardSeconds = 9f;

        [Header("Photo-finish slow motion")]
        [Tooltip("World units from B: when both leaders are inside it and their finish times differ by < slowMoGap, the race clock slows.")]
        [Min(0.01f)] public float slowMoRadius = 0.8f;

        [Min(0f)] public float slowMoGap = 0.1f;

        [Range(0.05f, 1f)] public float slowMoRate = 0.3f;

        [Header("Text")]
        public string title = "FASTEST PATH";

        public string question = "WHICH BALL WINS?";

        [Tooltip("Bottom line during a round's hold when the round sets none.")]
        public string holdCaption = "GUESS?";

        [Tooltip("End card: replaces the question in the top panel. The reveal.")]
        public string endCard = "BRACHISTOCHRONE CURVE";

        [Tooltip("End card caption size as a % of the question's, so one long word (BRACHISTOCHRONE) fits on a line.")]
        [Range(50f, 100f)] public float endCardSizePercent = 80f;

        [Tooltip("End card: replaces the title line under it. What the name means.")]
        public string endTitle = "GREEK FOR \"SHORTEST TIME\"";

        [Tooltip("End card: the winning track's on-track label switches to this.")]
        public string endLabel = "BRACHISTOCHRONE";

        [Tooltip("End card, first half: bottom line, the first Greek root.")]
        public string endLine = "BRACHISTOS = SHORTEST";

        [Tooltip("End card, second half: bottom line, the second Greek root.")]
        public string endLine2 = "CHRONOS = TIME";

        public const float DefaultGravity = 1.5f;

        public int RoundCount => rounds != null ? rounds.Length : 0;

        /// <summary>
        /// Estimated video length in simulated seconds: holds, transitions, results and the end card plus each
        /// round's <see cref="BrachRound.expectedRunSeconds"/> (harness-measured; falls back to the cap).
        /// </summary>
        public float SeriesSeconds
        {
            get
            {
                float total = endCardSeconds;
                for (int i = 0; i < RoundCount; i++)
                {
                    BrachRound r = rounds[i];
                    float run = r.expectedRunSeconds > 0f ? r.expectedRunSeconds : r.maxRunSeconds;
                    total += (i == 0 ? firstHoldSeconds : transitionSeconds + preLaunchHoldSeconds) + run + ResultSecondsOf(r);
                }

                return total;
            }
        }

        /// <summary>Structural length: the worst case (every run at its cap) plus a margin, not the estimate.</summary>
        public override int MaxTicks
        {
            get
            {
                float total = endCardSeconds + 5f;
                for (int i = 0; i < RoundCount; i++)
                {
                    total += (i == 0 ? firstHoldSeconds : transitionSeconds + preLaunchHoldSeconds) +
                             rounds[i].maxRunSeconds + ResultSecondsOf(rounds[i]);
                }

                return Mathf.Max(1, Mathf.CeilToInt(total / fixedTimestep));
            }
        }

        /// <summary>Result phase length of a round (its own override, else the config's).</summary>
        public float ResultSecondsOf(BrachRound r)
        {
            return r.resultSeconds > 0f ? r.resultSeconds : resultHoldSeconds;
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (RoundCount <= 0)
            {
                error = "A brach video needs at least one round.";
                return false;
            }

            if (gravity <= 0f || pathSamples < 64)
            {
                error = "gravity must be greater than zero and pathSamples at least 64.";
                return false;
            }

            for (int i = 0; i < RoundCount; i++)
            {
                BrachRound r = rounds[i];
                if (r.tracks == null || r.tracks.Length == 0)
                {
                    error = $"Round {i + 1}: needs at least one track.";
                    return false;
                }

                if (r.finishX <= 0f || r.finishY <= 0f || r.maxRunSeconds <= 0f)
                {
                    error = $"Round {i + 1}: finishX, finishY and maxRunSeconds must be greater than zero.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// The five storyboard rounds. Track order is stable (Straight, Cycloid, Steep, Circle, Parabola, Deep) so
        /// tracks[i] keeps its colour. expectedRunSeconds are the harness's measured launch-to-last-finish times.
        /// </summary>
        public static BrachRound[] DefaultRounds()
        {
            BrachTrack straight = Track(CurveKind.Straight, "STRAIGHT", 0f);
            BrachTrack curve = Track(CurveKind.Cycloid, "CURVE", 0f);
            BrachTrack steep = Track(CurveKind.SteepDrop, "STEEP", 0.12f);
            BrachTrack circle = Track(CurveKind.CircleArc, "CIRCLE", 0f);
            BrachTrack parabola = Track(CurveKind.Parabola, "PARABOLA", 0f);
            BrachTrack deep = Track(CurveKind.DeepDip, "DEEP", 1f);

            return new[]
            {
                Round(new[] { straight, curve }, 6f, 4f, 4.2f,
                      "SHORTEST vs CURVE", "", "", "CURVE WINS!", "BUT IT'S LONGER!", true, 5.5f),
                Round(new[] { straight, curve, steep }, 6f, 4f, 4.2f,
                      "NOW A STEEP DROP", "", "", "CURVE AGAIN!", "", false, 0f),
                Round(new[] { straight, curve, steep, circle }, 6f, 4f, 4.6f,
                      "NOW A CIRCLE", "", "", "CURVE. JUST!", "", false, 0f),
                Round(new[] { straight, curve, steep, circle }, 8f, 2.6f, 6.1f,
                      "FINISH MOVES UP", "CURVE GOES UNDER IT!", "", "STILL THE CURVE!", "", false, 0f),
                Round(new[] { straight, curve, steep, circle, parabola, deep }, 7f, 3.2f, 5.0f,
                      "6 SHAPES", "", "", "CURVE. ALWAYS!", "", false, 0f)
            };
        }

        static BrachTrack Track(CurveKind kind, string label, float shape)
        {
            return new BrachTrack { kind = kind, label = label, shape = shape };
        }

        static BrachRound Round(BrachTrack[] tracks, float bx, float by, float expectedRun, string caption,
                                string caption2, string holdCaption, string result, string result2,
                                bool showLengths, float resultSeconds)
        {
            return new BrachRound
            {
                tracks = tracks,
                finishX = bx,
                finishY = by,
                caption = caption,
                caption2 = caption2,
                holdCaption = holdCaption,
                result = result,
                result2 = result2,
                showLengths = showLengths,
                maxRunSeconds = 15f,
                expectedRunSeconds = expectedRun,
                resultSeconds = resultSeconds
            };
        }
    }
}
