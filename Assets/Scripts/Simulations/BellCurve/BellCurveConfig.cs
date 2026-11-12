using SimulationLobby.Core;
using SimulationLobby.Simulations.BellCurve.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.BellCurve
{
    /// <summary>
    /// One round of the <c>bell</c> format: how many marbles drop, how the pegs are rigged, and the words
    /// around it. The same question ("what shape?") is asked every round; only the marble count and the rigging change.
    /// </summary>
    [System.Serializable]
    public struct BellRound
    {
        [Tooltip("Marbles dropped this round.")]
        [Min(1)] public int marbles;

        [Tooltip("Marbles leaving the spout per second.")]
        [Min(1f)] public float spawnPerSecond;

        [Tooltip("Chance a marble goes right at each peg. 0.5 = a fair board.")]
        [Range(0.05f, 0.95f)] public float rightProbability;

        [Tooltip("How far the pegs are drawn leaning right (negative = left). Cosmetic: the odds come from rightProbability.")]
        [Range(-1f, 1f)] public float pegLean;

        [Tooltip("Draw the fitted bell-curve outline over the pile once the round ends.")]
        public bool showCurve;

        [Tooltip("Row added to the ladder when the round ends, e.g. '1,000: BELL'.")]
        public string ladderRow;

        [Tooltip("Bottom line during the transition and the drop.")]
        public string caption;

        [Tooltip("Bottom line during the hold before the drop. Empty = the config's holdCaption.")]
        public string holdCaption;

        [Tooltip("Result line once every marble has landed.")]
        public string result;
    }

    /// <summary>
    /// Config for the <c>bell</c> format (Bell Curve Drop). A published video = this asset + a seed.
    /// </summary>
    [CreateAssetMenu(fileName = "BellCurveConfig", menuName = "Simulation Lobby/Bell Curve Config")]
    public sealed class BellCurveConfig : SimulationConfig
    {
        [Header("Rounds")]
        public BellRound[] rounds = DefaultRounds();

        [Header("Board (world units; board space has y = 0 at the first peg)")]
        [Tooltip("Peg rows. The board has rows+1 bins. At most 20 (a path is kept as bits).")]
        [Range(2, 20)] public int rows = 10;

        [Tooltip("Gap between neighbouring pegs in a row = bin width.")]
        [Min(0.2f)] public float binSpacing = 0.92f;

        [Tooltip("Vertical gap between peg rows.")]
        [Min(0.1f)] public float rowSpacing = 0.52f;

        [Tooltip("Seconds for one peg-to-peg hop.")]
        [Range(0.08f, 0.6f)] public float hopSeconds = 0.2f;

        [Tooltip("Height of each hop's arc above the straight line between pegs.")]
        [Range(0f, 0.4f)] public float hopBump = 0.12f;

        [Tooltip("Spout height above the first peg.")]
        [Min(0.3f)] public float dropHeight = 1.4f;

        [Tooltip("Gravity for the free falls (spout to peg, last peg to pile), units/s^2.")]
        [Min(2f)] public float gravity = 18f;

        [Tooltip("Marble radius.")]
        [Min(0.03f)] public float marbleRadius = 0.14f;

        [Tooltip("Peg radius.")]
        [Min(0.02f)] public float pegRadius = 0.09f;

        [Header("Tray")]
        [Tooltip("First peg to the tray floor.")]
        [Min(1f)] public float trayDepth = 7.6f;

        [Tooltip("Height of the tallest bar the tray is drawn for.")]
        [Min(0.5f)] public float trayHeight = 2f;

        [Tooltip("The tallest bin is expected to hold at most this share of a round's marbles. Sets the bar scale " +
                 "(height per marble = trayHeight / (marbles * peakFraction)); a bar past the tray top is clamped.")]
        [Range(0.1f, 0.6f)] public float peakFraction = 0.34f;

        [Header("Frame")]
        [Min(4f)] public float frameWidth = 11f;

        [Header("Pacing")]
        [Min(0f)] public float firstHoldSeconds = 1.8f;
        [Min(0f)] public float preDropHoldSeconds = 1.2f;
        [Tooltip("After the last marble lands, before the result line.")]
        [Min(0f)] public float settleSeconds = 0.4f;
        [Min(0f)] public float resultHoldSeconds = 2.4f;
        [Tooltip("Between rounds: the pile drains away and the pegs re-rig.")]
        [Min(0f)] public float transitionSeconds = 1.4f;
        [Min(0f)] public float endCardSeconds = 5f;

        [Header("Text")]
        public string title = "EVERY PEG IS A COIN FLIP";
        public string question = "WHAT SHAPE WILL THEY MAKE?";
        [Tooltip("Replaces the question in the top panel on the end card.")]
        public string endQuestion = "BELL CURVE";
        public string holdCaption = "GUESS THE SHAPE?";
        public string counterLabel = "MARBLES";
        public string endCard = "RANDOM LEFT/RIGHT STEPS ADD UP TO A BELL CURVE";
        [Tooltip("Shown under the ladder on the end card.")]
        public string endLine = "THE CENTRAL LIMIT THEOREM";

        public int RoundCount => rounds != null ? rounds.Length : 0;

        public BoardGeometry Geometry =>
            new BoardGeometry(rows, binSpacing, rowSpacing, hopSeconds, hopBump, dropHeight, gravity);

        /// <summary>Tray floor in board space.</summary>
        public float TrayFloorY => -trayDepth;

        /// <summary>Bar height per landed marble for a round.</summary>
        public float HeightPerMarble(BellRound round)
        {
            return trayHeight / (Mathf.Max(1, round.marbles) * peakFraction);
        }

        /// <summary>Seconds from the first marble leaving the spout to the last one landing (upper bound).</summary>
        public float RunSeconds(BellRound round)
        {
            BoardGeometry g = Geometry;
            float last = (round.marbles - 1) / Mathf.Max(1f, round.spawnPerSecond);
            return last + g.LandAge(TrayFloorY);
        }

        /// <summary>Estimated video length in simulated seconds.</summary>
        public float SeriesSeconds
        {
            get
            {
                float total = endCardSeconds;
                for (int i = 0; i < RoundCount; i++)
                {
                    total += (i == 0 ? firstHoldSeconds : transitionSeconds + preDropHoldSeconds) +
                             RunSeconds(rounds[i]) + settleSeconds + resultHoldSeconds;
                }

                return total;
            }
        }

        /// <summary>The length is structural, so the cap is the series estimate plus a margin.</summary>
        public override int MaxTicks => Mathf.Max(1, Mathf.CeilToInt((SeriesSeconds + 5f) / fixedTimestep));

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (RoundCount <= 0)
            {
                error = "A bell video needs at least one round.";
                return false;
            }

            if (rows < 2 || rows > 20)
            {
                error = "rows must be 2..20.";
                return false;
            }

            for (int i = 0; i < RoundCount; i++)
            {
                BellRound r = rounds[i];
                if (r.marbles <= 0 || r.spawnPerSecond <= 0f || r.rightProbability <= 0f || r.rightProbability >= 1f)
                {
                    error = $"Round {i + 1}: marbles and spawnPerSecond must be positive and rightProbability inside (0,1).";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>100 fair marbles (a rough mound), 1,000 fair marbles (a bell), 1,000 on a rigged board (still a bell, shifted).</summary>
        public static BellRound[] DefaultRounds()
        {
            return new[]
            {
                new BellRound
                {
                    marbles = 100, spawnPerSecond = 14f, rightProbability = 0.5f, pegLean = 0f, showCurve = false,
                    ladderRow = "100: ROUGH",
                    caption = "100 MARBLES. EVERY PEG A COIN FLIP...",
                    holdCaption = "",
                    result = "A ROUGH MOUND..."
                },
                new BellRound
                {
                    marbles = 1000, spawnPerSecond = 55f, rightProbability = 0.5f, pegLean = 0f, showCurve = true,
                    ladderRow = "1,000: BELL",
                    caption = "NOW 1,000 MARBLES...",
                    holdCaption = "GUESS AGAIN?",
                    result = "A SMOOTH BELL CURVE!"
                },
                new BellRound
                {
                    marbles = 1000, spawnPerSecond = 60f, rightProbability = 0.65f, pegLean = 0.8f, showCurve = true,
                    ladderRow = "RIGGED: BELL",
                    caption = "RIGGED: EVERY PEG LEANS RIGHT...",
                    holdCaption = "STILL A BELL?",
                    result = "STILL A BELL. JUST SHIFTED."
                }
            };
        }
    }
}
