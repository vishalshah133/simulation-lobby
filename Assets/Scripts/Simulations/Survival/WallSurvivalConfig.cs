using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.Survival
{
    /// <summary>
    /// Tunables for "Wall vs Ball" survival: a wall of blocks takes hit after hit from an escalating
    /// ball until nothing is left standing. The hook is the guess — how many hits will it take? — so
    /// the hit count is the answer the whole video builds toward. A published video is this asset
    /// plus a scene plus a seed, same as every other format.
    /// </summary>
    /// <remarks>
    /// The wall is never rebuilt between hits. Damage accumulates where it lands: holes stay holes,
    /// nudged blocks stay nudged, and the viewer can read the wall's whole history off one frame.
    /// That is what makes the guess fair — the evidence is all on screen.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "WallSurvivalConfig",
        menuName = "Simulation Lobby/Configs/Wall Survival",
        order = 30)]
    public sealed class WallSurvivalConfig : SimulationConfig
    {
        [Header("Block field")]
        [Tooltip("Blocks across (X) in the wall.")]
        [Range(1, 60)] public int columns = 25;

        [Tooltip("Blocks tall (Y) in the wall.")]
        [Range(1, 60)] public int rows = 12;

        [Tooltip("Block edge length. The wall is laid out at this size plus the air gap, before jitter.")]
        [Min(0.05f)] public float blockSize = 0.4f;

        [Tooltip("Per-block mass.")]
        [Min(0.01f)] public float blockMass = 1f;

        [Tooltip("Air gap between neighbouring blocks, as a fraction of blockSize. This must stay " +
                 "above zero. Blocks packed at exactly blockSize have touching — and, once jitter is " +
                 "added, overlapping — colliders, and Unity resolves an overlap by shoving the pair " +
                 "apart hard the instant they stop being kinematic: the wall detonates before the " +
                 "ball ever reaches it. The gap is what makes the wall a wall.")]
        [Range(0.01f, 0.5f)] public float blockGapFraction = 0.1f;

        [Tooltip("Random position offset per block, as a fraction of blockSize. Breaks the perfectly " +
                 "uniform grid look. Clamped at runtime to half the gap — jitter that exceeds the gap " +
                 "re-creates the overlap the gap exists to prevent.")]
        [Range(0f, 0.3f)] public float positionJitterFraction = 0.05f;

        [Tooltip("Ceiling on how fast Unity may push two overlapping blocks apart, in units/second. " +
                 "The second half of the same safety: even with a gap, the ball drives blocks into " +
                 "each other hard enough to overlap mid-collision, and the default (10) turns that " +
                 "into a spray of blocks launched at speeds nothing in the scene imparted.")]
        [Range(0.1f, 20f)] public float maxDepenetrationVelocity = 1.5f;

        [Tooltip("Linear damping applied to every block. Blocks spawn with gravity off, so this is the " +
                 "only thing bleeding off an impact's energy — and it decides how fast debris settles " +
                 "before the next hit. Too low and every hit runs to its flight cap with blocks still " +
                 "drifting through frame.")]
        [Min(0f)] public float blockLinearDamping = 1.5f;

        [Header("Run shape — the beats that make this a video, not a clip")]
        [Tooltip("Safety cap on hits. The run is meant to end when the wall is gone; this only stops a " +
                 "run that never gets there from rolling past the 3-minute Shorts limit. A take that " +
                 "hits the cap has no answer to its own question — reject it in the seed scan.")]
        [Range(1, 60)] public int maxHits = 25;

        [Tooltip("Seconds the intact wall sits on screen before the first hit's hold begins. This is " +
                 "the guessing window — the question is on screen and the viewer needs time to look " +
                 "at the wall and commit to a number.")]
        [Range(0f, 8f)] public float openingHoldSeconds = 3f;

        [Tooltip("Seconds each hit sits static before its ball launches. The anticipation beat: the " +
                 "riser cue is generated to this length, so it peaks exactly on launch. Kept short — " +
                 "there are many more hits than the old nine waves, and a long hold multiplies.")]
        [Range(0f, 5f)] public float preLaunchHoldSeconds = 1f;

        [Tooltip("Hard cap on a single hit's flight. A hit whose debris never quite settles would " +
                 "otherwise stall the whole run.")]
        [Range(1f, 30f)] public float maxHitFlightSeconds = 5f;

        [Tooltip("Seconds after a hit settles and its cascade finishes, before the next hit's hold. " +
                 "The beat where the damage and the running hit count are legible.")]
        [Range(0f, 6f)] public float hitResultHoldSeconds = 0.9f;

        [Tooltip("Seconds held on the demolished (or surviving) wall at the end — the reveal of the " +
                 "answer, and what the end card sits on.")]
        [Range(0f, 10f)] public float finalHoldSeconds = 4f;

        [Tooltip("Blocks per tick removed during the elimination cascade. Low values stretch the " +
                 "cascade into a rising run of chimes — the format's most ASMR moment. 0 removes " +
                 "them all at once, which wastes it.")]
        [Range(0f, 20f)] public float eliminationsPerTick = 1.5f;

        [Tooltip("Once this few blocks are left standing, the run is in its last stand: every hit gets " +
                 "the extra slow-motion and the HUD counts down what is left. A survival run's final " +
                 "stretch needs room — the survival brief's 'slow the end down' rule.")]
        [Range(0, 60)] public int lastStandBlocks = 12;

        [Header("Hit escalation — why hit 15 is scarier than hit 1")]
        [Tooltip("Ball radius multiplier per hit. 1 = every hit identical, which drags the ending out: " +
                 "a sparse, scattered wall needs a bigger ball to keep losing several blocks a hit.")]
        [Range(1f, 1.5f)] public float radiusGrowthPerHit = 1.07f;

        [Tooltip("Ball speed multiplier per hit.")]
        [Range(1f, 1.5f)] public float speedGrowthPerHit = 1.03f;

        [Tooltip("Ball mass multiplier per hit.")]
        [Range(1f, 3f)] public float massGrowthPerHit = 1.25f;

        [Header("Ball")]
        [Tooltip("First hit's ball radius. Every later hit scales from this.")]
        [Min(0.05f)] public float ballRadius = 0.405f;

        [Tooltip("First hit's ball mass. Hit 1 should punch a hole, not level the wall — a first-hit " +
                 "wipeout answers the question before anyone has had time to guess.")]
        [Min(0.1f)] public float ballMass = 8f;

        [Tooltip("First hit's launch speed, drawn per hit from this range.")]
        [Min(0.1f)] public float ballSpeedMin = 11f;

        [Min(0.1f)] public float ballSpeedMax = 14f;

        [Tooltip("Distance in front of the wall the ball spawns at, along its launch direction.")]
        [Min(0.5f)] public float ballSpawnDistance = 12f;

        [Tooltip("Aim jitter around the chosen target block, as a fraction of the ball's radius. Every " +
                 "hit aims at the densest cluster still standing; this only nudges it so consecutive " +
                 "hits are not pixel-identical. Keep it well under 1 or late hits start missing the " +
                 "few blocks that remain.")]
        [Range(0f, 2f)] public float aimJitterBallRadii = 0.5f;

        [Tooltip("Draw the aim jitter from UnityEngine.Random instead of the run's seeded RNG, so " +
                 "every play of the same seed aims somewhere new. " +
                 "This makes the run UNREPRODUCIBLE: the seed no longer describes the footage, so a " +
                 "good take cannot be re-rendered at higher quality or re-cut later, and the hit " +
                 "count it reveals cannot be verified. Turn it off before a seed scan — every seed " +
                 "would produce a different run each time it is evaluated and the scan measures nothing.")]
        public bool unseededAim = false;

        [Header("Settle detection — when a hit is over")]
        [Tooltip("A block counts as settled once its velocity drops below this, for settleTicksRequired " +
                 "consecutive ticks. Mirrors Unity's own rigidbody sleep heuristic.")]
        [Min(0.001f)] public float settleVelocityThreshold = 0.05f;

        [Tooltip("Consecutive ticks every block must stay below threshold before the hit is over.")]
        [Min(1)] public int settleTicksRequired = 15;

        [Header("Scoring — what the HUD and the result report")]
        [Tooltip("A block is knocked out of the wall — destroyed — once it has moved this many block " +
                 "spacings from its ORIGINAL slot in the wall. Because the wall is never rebuilt, this " +
                 "is cumulative: two small nudges add up to a knockout, exactly as it reads on screen. " +
                 "Measured in spacings so it scales with blockSize. Strict on purpose — any block " +
                 "visibly out of line should count: 1.25 let clearly shoved blocks keep standing, and " +
                 "0.5 still felt loose on playback. The placement jitter is baked into each block's " +
                 "slot, so it never counts; the floor is blocks merely brushed by a neighbour.")]
        [Range(0.05f, 6f)] public float eliminationDistanceBlocks = 0.2f;

        [Tooltip("A block also counts as destroyed once it is tilted this many degrees from the wall's " +
                 "orientation, wherever it sits. A block knocked crooked but still near its slot reads " +
                 "as broken, and the distance test alone never catches it. 0 disables the tilt test.")]
        [Range(0f, 90f)] public float eliminationTiltDegrees = 25f;

        [Tooltip("A block with fewer than this many standing neighbours (the 8 around it, diagonals " +
                 "included) is destroyed too — it has nothing left holding it up. A lone block left " +
                 "floating in a hole reads as a leftover, not as wall. 1 = only fully orphaned blocks " +
                 "go; 2 also clears loose pairs and dangling strands; 0 disables the rule.")]
        [Range(0, 8)] public int minNeighboursToStand = 1;

        [Header("Drama — presentation cues the format decides, not the components")]
        [Tooltip("Slow-motion on the first contact of every hit. Off = slow-motion only during the " +
                 "last stand, which is punchier but gives up the per-hit payoff.")]
        public bool slowMotionOnEveryHit = true;

        [Tooltip("Unscaled seconds a slow-motion hold lasts once first contact triggers it.")]
        [Range(0f, 5f)] public float slowMotionSeconds = 0.9f;

        [Tooltip("Extra unscaled seconds of slow-motion on each hit once the last stand has begun.")]
        [Range(0f, 6f)] public float lastStandSlowMotionBonus = 0.8f;

        [Tooltip("Unscaled seconds of slow-motion the moment the final blocks are knocked out — the " +
                 "answer being revealed. Triggered by the outcome, not predicted: nobody knows which " +
                 "hit is the last until every remaining block is on its way out.")]
        [Range(0f, 6f)] public float wipeoutSlowMotionSeconds = 2.2f;

        /// <summary>Elimination threshold in world units.</summary>
        public float EliminationDistance => BlockSpacing * eliminationDistanceBlocks;

        /// <summary>
        /// Centre-to-centre spacing of the wall — block plus its air gap. Everything that lays out or
        /// measures the wall goes through this, so the gap can never be forgotten in one of them.
        /// </summary>
        public float BlockSpacing => blockSize * (1f + blockGapFraction);

        /// <summary>
        /// Jitter amplitude in world units, clamped so a jittered block cannot reach its neighbour.
        /// </summary>
        public float JitterAmplitude =>
            blockSize * Mathf.Min(positionJitterFraction, blockGapFraction * 0.5f);

        /// <summary>Total blocks in the wall — the number the title and thumbnail advertise.</summary>
        public int BlockCount => Mathf.Max(0, columns * rows);

        /// <summary>Ball radius for a zero-based hit index.</summary>
        public float RadiusForHit(int hitIndex) => ballRadius * Mathf.Pow(radiusGrowthPerHit, hitIndex);

        /// <summary>Ball mass for a zero-based hit index.</summary>
        public float MassForHit(int hitIndex) => ballMass * Mathf.Pow(massGrowthPerHit, hitIndex);

        /// <summary>Speed multiplier for a zero-based hit index.</summary>
        public float SpeedScaleForHit(int hitIndex) => Mathf.Pow(speedGrowthPerHit, hitIndex);

        /// <summary>
        /// Worst-case run length in simulated seconds: every hit up to the cap running to its flight
        /// cap, plus every hold. The runner's timeout must sit above this or a healthy run would be
        /// cut off mid-hit, so <see cref="MaxTicks"/> derives from it.
        /// </summary>
        public float WorstCaseDurationSeconds =>
            openingHoldSeconds +
            maxHits * (preLaunchHoldSeconds + maxHitFlightSeconds + hitResultHoldSeconds) +
            finalHoldSeconds;

        public override int MaxTicks =>
            Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(maxDurationSeconds, WorstCaseDurationSeconds * 1.1f) / fixedTimestep));

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (ballSpeedMin > ballSpeedMax)
            {
                error = "ballSpeedMin must not exceed ballSpeedMax.";
                return false;
            }

            if (BlockCount <= 0)
            {
                error = "columns * rows must be at least 1 — an empty wall has nothing to hit.";
                return false;
            }

            if (maxHits <= 0)
            {
                error = "maxHits must be at least 1.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
