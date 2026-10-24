using SimulationLobby.Core;
using SimulationLobby.Simulations.HydraulicPress.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.HydraulicPress
{
    /// <summary>
    /// One round of the <c>press</c> format: how hard the press pushes, how it is labelled, and the words around it.
    /// Only <see cref="pressTons"/> changes the count; the rest is framing and pacing.
    /// </summary>
    [System.Serializable]
    public struct PressRound
    {
        [Tooltip("Press weight in tons. 2, 4, 8, 16, 32, 64.")]
        [Min(0.1f)] public float pressTons;

        [Tooltip("Decal on the plate face, e.g. 2 TONS.")]
        public string pressLabel;

        [Tooltip("Bottom line during the hold and the run. Empty = the config's holdCaption.")]
        public string caption;

        [Tooltip("Result line once the press has stopped. {n} = the ball count.")]
        public string result;

        [Tooltip("Safety cap: a run still going after this many seconds logs an error and ends.")]
        [Min(1f)] public float maxRunSeconds;

        [Tooltip("Median launch-to-stop seconds from the harness at the config's fixed timestep. Only feeds the " +
                 "SeriesSeconds estimate; the run itself ends when the press stops.")]
        [Min(0f)] public float expectedRunSeconds;
    }

    /// <summary>
    /// Config for the <c>press</c> format (Hydraulic Press): the same question asked once per round with the press 2x
    /// stronger each time. A published video = this asset + a seed (random per Play, logged at start).
    /// </summary>
    [CreateAssetMenu(fileName = "HydraulicPressConfig", menuName = "Simulation Lobby/Hydraulic Press Config")]
    public sealed class HydraulicPressConfig : SimulationConfig
    {
        [Header("Rounds")]
        [Tooltip("One entry per round, in order. Defaults are the six storyboard rounds: 2, 4, 8, 16, 32, 64 tons.")]
        public PressRound[] rounds = DefaultRounds();

        [Header("Chamber and ball")]
        [Tooltip("Inner width between the walls, world units.")]
        [Min(2f)] public float chamberWidth = 6f;

        [Tooltip("Floor to the plate's start height, world units.")]
        [Min(2f)] public float chamberHeight = 7f;

        [Tooltip("The one ball every round starts with. It sets the whole ladder: the count follows the radius " +
                 "(each pop is r/sqrt2), so retune the harness table after changing it.")]
        [Min(0.1f)] public float startRadius = 0.9f;

        [Tooltip("The first ball's x is drawn from [-startOffset, +startOffset] (seeded).")]
        [Min(0f)] public float startOffset = 0.6f;

        [Tooltip("Tons one ball can carry before it starts to squeeze. Size does not matter.")]
        [Min(0.01f)] public float ballStrengthTons = 1f;

        [Tooltip("A ball's own weight in tons. ~0: the press is the load.")]
        [Min(0f)] public float ballWeightTons;

        [Header("Press and crush")]
        [Tooltip("Plate speed while it is clear of the pile, units per second.")]
        [Min(0.1f)] public float pressSpeed = 2.5f;

        [Tooltip("A ball over strength for this long (while calm) is crushed. It flattens on screen over this time.")]
        [Min(0.01f)] public float squeezeSeconds = 0.12f;

        [Tooltip("How fast a squeeze unwinds when the load drops, in seconds of squeeze per second. 0 = it holds (damage " +
                 "accumulates), which keeps the tail of a big cascade short and the run length steady.")]
        [Min(0f)] public float squeezeDecay = 0f;

        [Tooltip("The two halves of a pop split along horizontal +- this many degrees (seeded).")]
        [Range(0f, 20f)] public float splitJitterDegrees = 3f;

        [Tooltip("A ball is over strength only above strength x (1 + this). Absorbs solver noise at a marginal load.")]
        [Range(0f, 0.5f)] public float loadTolerance = 0.15f;

        [Tooltip("Time constant (s) of the load average a ball is judged on, so a jittering contact can't pop it.")]
        [Min(0f)] public float loadSmoothing = 0.04f;

        [Tooltip("A ball's squeeze only builds while it moves slower than this (units per second).")]
        [Min(0f)] public float calmSpeed = 0.5f;

        [Tooltip("A ball at or below this radius cannot split: logs a warning (a tuning failure). Counted as a minRadius hit.")]
        [Min(0.001f)] public float minRadius = 0.008f;

        [Tooltip("Cap on pops per tick so a cascade reads as a ripple. Highest load first.")]
        [Min(1)] public int maxPopsPerTick = 64;

        [Header("Stop rule")]
        [Tooltip("The pile counts as still when its RMS speed is below this (units per second).")]
        [Min(0.001f)] public float settleSpeed = 0.1f;

        [Tooltip("Press resting + nobody over strength + still, for this long: the round is over.")]
        [Min(0f)] public float stopHoldSeconds = 0.5f;

        [Header("Solver")]
        [Tooltip("Physics substeps per fixed tick.")]
        [Range(1, 8)] public int substeps = 3;

        [Tooltip("Position-solver passes per substep.")]
        [Range(1, 16)] public int iterations = 4;

        [Tooltip("Extra push-out passes straight after a wave of pops.")]
        [Range(0, 64)] public int relaxIterations = 16;

        [Tooltip("Two balls (or a ball and the floor/plate) carry load when their gap is under this fraction of the smaller radius.")]
        [Range(0f, 0.5f)] public float contactTolerance = 0.1f;

        [Tooltip("Gravity in world units per second squared. Lower than 9.81 keeps a dense pile of grains calm.")]
        [Min(0.1f)] public float gravity = 4f;

        [Tooltip("Velocity damping per second.")]
        [Min(0f)] public float damping = 0.5f;

        [Tooltip("Sliding friction at contacts, per solver pass.")]
        [Range(0f, 1f)] public float friction = 0.05f;

        [Tooltip("Speed cap, units per second.")]
        [Min(1f)] public float maxSpeed = 6f;

        [Tooltip("Hard cap on balls per round. A round that reaches it logs an error and stops popping.")]
        [Min(10)] public int maxBalls = 5000;

        [Header("Pacing")]
        [Tooltip("Static beat before round 1: the hook.")]
        [Min(0f)] public float firstHoldSeconds = 1.5f;

        [Tooltip("Static beat before each later round's press drops, after the transition.")]
        [Min(0f)] public float holdSeconds = 1.0f;

        [Tooltip("Result line up, nothing moving, before the next round.")]
        [Min(0f)] public float resultHoldSeconds = 1.6f;

        [Tooltip("Between rounds: the press lifts, its label flips, the pile clears, one fresh ball drops in.")]
        [Min(0f)] public float transitionSeconds = 1.6f;

        [Tooltip("End card after the last round (final pile at rest under the press, ladder with the footnote).")]
        [Min(0f)] public float endCardSeconds = 4f;

        [Header("Text")]
        public string title = "CRUSHED BALLS SPLIT IN 2";

        public string question = "HOW MANY BALLS STOP THE PRESS?";

        [Tooltip("Sub-caption under the chamber.")]
        public string subCaption = "EACH BALL HOLDS 1 TON";

        [Tooltip("Bottom line during a round's hold when the round sets none.")]
        public string holdCaption = "GUESS?";

        [Tooltip("Live counter prefix, e.g. BALLS -> 'BALLS 128'.")]
        public string counterLabel = "BALLS";

        [Tooltip("End card line. The harness did not support a per-seed ~4x claim (plans/videos/press-001-split-press.md, " +
                 "As built), so this is the storyboard's fallback line. Never show a ratio the run doesn't produce.")]
        public string endLine = "EACH BALL HOLDS 1 TON. MORE FORCE, SMALLER BALLS.";

        [Tooltip("Shown under the ladder on the end card. Empty by default: the sub-caption already says it, and the end line sits in the bottom panel.")]
        public string ladderFootnote = "";

        public int RoundCount => rounds != null ? rounds.Length : 0;

        /// <summary>
        /// Estimated video length in simulated seconds: holds, transitions, results and the end card plus each
        /// round's <see cref="PressRound.expectedRunSeconds"/> (harness-measured; falls back to the cap).
        /// </summary>
        public float SeriesSeconds
        {
            get
            {
                float total = endCardSeconds;
                for (int i = 0; i < RoundCount; i++)
                {
                    PressRound r = rounds[i];
                    float run = r.expectedRunSeconds > 0f ? r.expectedRunSeconds : r.maxRunSeconds;
                    total += (i == 0 ? firstHoldSeconds : transitionSeconds + holdSeconds) + run + resultHoldSeconds;
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
                    total += (i == 0 ? firstHoldSeconds : transitionSeconds + holdSeconds) +
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
                error = "A press video needs at least one round.";
                return false;
            }

            for (int i = 0; i < RoundCount; i++)
            {
                PressRound r = rounds[i];
                if (r.pressTons <= 0f || r.maxRunSeconds <= 0f)
                {
                    error = $"Round {i + 1}: pressTons and maxRunSeconds must be greater than zero.";
                    return false;
                }
            }

            if (chamberWidth <= 0f || chamberHeight <= 0f || startRadius <= 0f)
            {
                error = "chamberWidth, chamberHeight and startRadius must be greater than zero.";
                return false;
            }

            if (startRadius * 2f > chamberWidth || startRadius * 2f >= chamberHeight)
            {
                error = "The start ball must fit inside the chamber (diameter below width and height).";
                return false;
            }

            if (minRadius >= startRadius)
            {
                error = "minRadius must be smaller than startRadius.";
                return false;
            }

            if (ballStrengthTons <= 0f)
            {
                error = "ballStrengthTons must be greater than zero.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>The kernel's plain-number copy of this config for one round.</summary>
        public PressKernelSettings MakeKernelSettings(PressRound round)
        {
            return new PressKernelSettings
            {
                HalfWidth = chamberWidth * 0.5,
                ChamberHeight = chamberHeight,
                StartRadius = startRadius,
                StartOffset = startOffset,
                PressTons = round.pressTons,
                BallStrengthTons = ballStrengthTons,
                BallWeightTons = ballWeightTons,
                LoadTolerance = loadTolerance,
                LoadSmoothing = loadSmoothing,
                MinNormalY = 0.05,
                NormalPower = 1.0,
                PressSpeed = pressSpeed,
                SqueezeSeconds = squeezeSeconds,
                SqueezeDecay = squeezeDecay,
                SplitJitterDegrees = splitJitterDegrees,
                SettleSpeed = settleSpeed,
                CalmSpeed = calmSpeed,
                LocalCalm = true,
                StopHoldSeconds = stopHoldSeconds,
                MinRadius = minRadius,
                MaxPopsPerTick = maxPopsPerTick,
                Substeps = substeps,
                Iterations = iterations,
                RelaxIterations = relaxIterations,
                ContactTolerance = contactTolerance,
                Gravity = gravity,
                Damping = damping,
                Friction = friction,
                MaxSpeed = maxSpeed,
                MaxBalls = maxBalls
            };
        }

        /// <summary>
        /// The six storyboard rounds. expectedRunSeconds are the harness's median launch-to-stop times at 1/60 s
        /// (see the *As built* table in plans/videos/press-001-split-press.md).
        /// </summary>
        public static PressRound[] DefaultRounds()
        {
            return new[]
            {
                Round(2f, "A 2-TON PRESS. GUESS?", "{n} BALLS.", 6f, 2.9f),
                Round(4f, "2× STRONGER: 4 TONS. GUESS?", "{n} BALLS.", 6f, 3.1f),
                Round(8f, "8 TONS. GUESS?", "{n} BALLS. NOT DOUBLE...", 10f, 3.6f),
                Round(16f, "16 TONS. GUESS?", "{n} BALLS.", 14f, 5.7f),
                Round(32f, "32 TONS. GUESS?", "{n} BALLS!", 20f, 7.9f),
                Round(64f, "64 TONS. GUESS?", "{n} BALLS!", 32f, 12.9f)
            };
        }

        static PressRound Round(float tons, string caption, string result, float maxRunSeconds, float expectedRunSeconds)
        {
            return new PressRound
            {
                pressTons = tons,
                pressLabel = $"{tons:0} TONS",
                caption = caption,
                result = result,
                maxRunSeconds = maxRunSeconds,
                expectedRunSeconds = expectedRunSeconds
            };
        }
    }
}
