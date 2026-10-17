using System;
using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.Survival.LavaRun
{
    /// <summary>One colour in the run: a name and a colour.</summary>
    [Serializable]
    public struct LavaRunTeamDef
    {
        public string name;
        public Color color;

        [Tooltip("Optional short name for tight spots. Empty uses name.")]
        public string shortName;

        public LavaRunTeamDef(string name, Color color, string shortName = null)
        {
            this.name = name;
            this.color = color;
            this.shortName = shortName;
        }

        public string Short => string.IsNullOrEmpty(shortName) ? name : shortName;
    }

    /// <summary>
    /// Tunables for "Lava Run" (surv-002): four colours multiply down three stages of track ahead of a
    /// rising lava front, the smallest colour is out at each stage's door, and the survivors fight on a
    /// sinking island. A published video is this asset plus a scene plus a seed.
    /// </summary>
    /// <remarks>
    /// Per-stage arrays are indexed by stage 0..2 (the island is not a stage in these arrays; its knobs
    /// are the island group). Distances are metres along each stage's TrackPath.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "LavaRunConfig",
        menuName = "Simulation Lobby/Configs/Lava Run",
        order = 31)]
    public sealed class LavaRunConfig : SimulationConfig
    {
        public const int StageCount = 3;
        public const int TeamCount = 4;

        [Header("Teams")]
        [Tooltip("Exactly 4. Default RED / BLUE / GREEN / YELLOW.")]
        public LavaRunTeamDef[] teams =
        {
            new LavaRunTeamDef("RED", new Color(0.93f, 0.16f, 0.16f)),
            new LavaRunTeamDef("BLUE", new Color(0.12f, 0.45f, 0.98f)),
            new LavaRunTeamDef("GREEN", new Color(0.15f, 0.85f, 0.30f)),
            new LavaRunTeamDef("YELLOW", new Color(1.00f, 0.88f, 0.10f))
        };

        [Header("Population")]
        [Min(1)] public int startMarblesPerTeam = 5;

        [Tooltip("Hard cap on live marbles per colour; clones beyond it are not spawned (GateCapped). 0 = no cap: the pools grow on demand.")]
        [Min(0)] public int maxLivePerTeam = 0;

        [Tooltip("Marbles cloned per colour up front, so early gates don't instantiate mid-run. The pools grow past this when uncapped.")]
        [Min(1)] public int poolPrewarmPerTeam = 150;

        [Tooltip("Spread of marbles around a pen spawn point (metres, horizontal).")]
        [Min(0f)] public float penJitter = 0.15f;

        [Tooltip("Marbles beyond the number of pen points stack upward by this much per layer.")]
        [Min(0f)] public float penStackHeight = 0.25f;

        [Header("Timing")]
        [Tooltip("Stage 1, before the countdown: the camera flies the whole course (stages 1-3, then the island) while nothing moves. 0 = no flyover.")]
        [Min(0f)] public float trackPreviewSeconds = 20f;

        [Tooltip("Stage 1 anticipation beat: the scene sits static and lit before the pen gate opens.")]
        [Min(0f)] public float preLaunchHoldSeconds = 5f;

        [Tooltip("Stages 2-3: seconds in the pens (after the drop) before the pen gate opens.")]
        [Min(0f)] public float penSettleSeconds = 2.5f;

        [Tooltip("Seconds the pen gate takes to open.")]
        [Min(0.01f)] public float penGateOpenSeconds = 0.5f;

        [Tooltip("Per stage: seconds after the pen gate opens until the crater erupts and the lava front starts.")]
        public float[] eruptionDelaySeconds = { 8f, 5f, 4f };

        [Tooltip("The eruption delay only starts counting once the foremost marble is this far (m) along the stage path, i.e. the pack has left the pens.")]
        [Min(0f)] public float eruptionArmDistance = 4f;

        [Tooltip("Safety: arm the eruption anyway this long after release (and log a warning) if nothing has moved eruptionArmDistance.")]
        [Min(1f)] public float eruptionArmTimeoutSeconds = 20f;

        [Tooltip("Hold on the result (door shut, counts frozen, \"X IS OUT\") before the trapdoor opens.")]
        [Min(0f)] public float stageResultHoldSeconds = 3f;

        [Tooltip("Seconds the trapdoor takes to open.")]
        [Min(0.01f)] public float trapdoorOpenSeconds = 0.6f;

        [Tooltip("Seconds survivors fall before they are re-seated into the next stage's pens.")]
        [Min(0.05f)] public float dropSeconds = 1.4f;

        [Tooltip("Seconds after the winner is decided before the run completes (push-in, confetti, caption).")]
        [Min(0f)] public float payoffHoldSeconds = 20f;

        [Tooltip("Forced decision (most marbles wins) if no colour has won by this many simulated seconds. Should never fire.")]
        [Min(10f)] public float maxRunSeconds = 780f;

        [Header("Lava")]
        [Tooltip("Where the front starts, metres along the stage path. Negative = behind the path start (the pens).")]
        public float lavaStartDistance = -3f;

        [Tooltip("Per stage: front speed (m/s) at eruption.")]
        public float[] lavaSpeedStart = { 1.6f, 2.2f, 2.6f };

        [Tooltip("Per stage: front speed (m/s) after lavaRampSeconds.")]
        public float[] lavaSpeedEnd = { 2.2f, 3.0f, 3.4f };

        [Tooltip("Per stage: seconds over which the front speed ramps from start to end.")]
        public float[] lavaRampSeconds = { 100f, 100f, 60f };

        [Tooltip("Per stage: how far (m) the front may trail the rearmost live marble at eruption. The front chases the pack to stay this close.")]
        public float[] lavaMaxGapStart = { 25f, 20f, 15f };

        [Tooltip("Per stage: the allowed gap after lavaRampSeconds. Smaller = lava breathes down the tail harder.")]
        public float[] lavaMaxGapEnd = { 6f, 4f, 3f };

        [Tooltip("Catch-up gain (1/s): front speed toward (rearmost marble - allowed gap) = distance * gain.")]
        [Min(0f)] public float lavaCatchUpGain = 0.6f;

        [Tooltip("Hard ceiling on the front speed (m/s), catch-up included.")]
        [Min(0.1f)] public float lavaMaxSpeed = 12f;

        [Tooltip("A marble whose path progress is below front + this margin is swallowed.")]
        [Min(0f)] public float lavaKillMargin = 0.15f;

        [Tooltip("TrackPath.Nearest search window behind the marble's last index (path points).")]
        [Min(1)] public int progressWindowBack = 10;

        [Tooltip("TrackPath.Nearest search window ahead of the marble's last index (path points).")]
        [Min(1)] public int progressWindowForward = 60;

        [Header("Stage doors")]
        [Tooltip("Per stage: does the smallest colour get eliminated at the door? (Stage 3, the bridge, does not.)")]
        public bool[] eliminates = { true, true, false };

        [Tooltip("Seconds the basin door takes to close once the front reaches the basin.")]
        [Min(0.05f)] public float doorCloseSeconds = 0.8f;

        [Tooltip("If every live marble is already safe in the basin, close early after this many seconds (negative = never close early).")]
        public float earlyCloseDelaySeconds = 1.5f;

        [Header("Gates / clones")]
        [Tooltip("How far behind its parent the first clone appears (metres). The greed trade-off: clones start nearer the lava.")]
        [Min(0f)] public float cloneBackOffset = 0.6f;

        [Tooltip("Extra distance between successive clones of a x3/x5 gate.")]
        [Min(0f)] public float cloneSpacing = 0.3f;

        [Tooltip("Seeded sideways offset of a clone (metres, +/-).")]
        [Min(0f)] public float cloneLateralJitter = 0.05f;

        [Tooltip("Seeded sideways speed added to a clone (m/s, +/-).")]
        [Min(0f)] public float cloneLateralSpeed = 0.15f;

        [Header("Island (finale)")]
        [Tooltip("Seconds between ring sinks at the start of the island.")]
        [Min(0.5f)] public float ringSinkIntervalStart = 18f;

        [Tooltip("Seconds between ring sinks for the last sinkable ring.")]
        [Min(0.5f)] public float ringSinkIntervalEnd = 10f;

        [Tooltip("The ring shakes this long before it lowers.")]
        [Min(0f)] public float ringTrembleSeconds = 1.5f;

        [Min(0f)] public float ringTrembleAmplitude = 0.03f;

        [Tooltip("Seconds a ring takes to lower into the lava.")]
        [Min(0.1f)] public float ringSinkSeconds = 2.5f;

        [Tooltip("How far a ring lowers (metres).")]
        [Min(0.1f)] public float ringSinkDepth = 4f;

        [Tooltip("Sweeper arm speed (degrees/s) at the start of the island.")]
        public float sweeperDegreesStart = 20f;

        [Tooltip("Sweeper arm speed (degrees/s) after sweeperRampSeconds.")]
        public float sweeperDegreesEnd = 70f;

        [Min(1f)] public float sweeperRampSeconds = 120f;

        [Tooltip("When fewer than this many marbles remain on the island, the finale slows so the last duel gets room.")]
        [Min(0)] public int slowdownBelow = 6;

        [Tooltip("Rate multiplier for ring timers and sweeper speed during the slowdown (0.5 = half speed).")]
        [Range(0.05f, 1f)] public float finaleSlowdown = 0.5f;

        void Reset()
        {
            // The base default (120 s) would stop an 11-minute run on the runner's MaxTicks.
            maxDurationSeconds = 900f;
        }

        public float EruptionDelay(int stage) => eruptionDelaySeconds[Mathf.Clamp(stage, 0, StageCount - 1)];

        public bool Eliminates(int stage) => eliminates[Mathf.Clamp(stage, 0, StageCount - 1)];

        /// <summary>Allowed front-to-pack gap (m) <paramref name="secondsSinceEruption"/> into the stage.</summary>
        public float LavaMaxGap(int stage, float secondsSinceEruption)
        {
            stage = Mathf.Clamp(stage, 0, StageCount - 1);
            float t = lavaRampSeconds[stage] > 0f ? Mathf.Clamp01(secondsSinceEruption / lavaRampSeconds[stage]) : 1f;
            return Mathf.Lerp(lavaMaxGapStart[stage], lavaMaxGapEnd[stage], t);
        }

        /// <summary>Front speed (m/s) <paramref name="secondsSinceEruption"/> into the stage.</summary>
        public float LavaSpeed(int stage, float secondsSinceEruption)
        {
            stage = Mathf.Clamp(stage, 0, StageCount - 1);
            float t = lavaRampSeconds[stage] > 0f ? Mathf.Clamp01(secondsSinceEruption / lavaRampSeconds[stage]) : 1f;
            return Mathf.Lerp(lavaSpeedStart[stage], lavaSpeedEnd[stage], t);
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (teams == null || teams.Length != TeamCount)
            {
                error = "teams must have exactly 4 entries.";
                return false;
            }

            for (int i = 0; i < teams.Length; i++)
            {
                if (string.IsNullOrEmpty(teams[i].name))
                {
                    error = $"teams[{i}] has no name.";
                    return false;
                }
            }

            if (eruptionDelaySeconds == null || eruptionDelaySeconds.Length != StageCount ||
                lavaSpeedStart == null || lavaSpeedStart.Length != StageCount ||
                lavaSpeedEnd == null || lavaSpeedEnd.Length != StageCount ||
                lavaRampSeconds == null || lavaRampSeconds.Length != StageCount ||
                lavaMaxGapStart == null || lavaMaxGapStart.Length != StageCount ||
                lavaMaxGapEnd == null || lavaMaxGapEnd.Length != StageCount ||
                eliminates == null || eliminates.Length != StageCount)
            {
                error = "eruptionDelaySeconds, lavaSpeedStart, lavaSpeedEnd, lavaRampSeconds, lavaMaxGapStart, lavaMaxGapEnd and eliminates must each have exactly 3 entries (stages 1-3).";
                return false;
            }

            for (int s = 0; s < StageCount; s++)
            {
                if (eruptionDelaySeconds[s] < 0f)
                {
                    error = $"eruptionDelaySeconds[{s}] must not be negative.";
                    return false;
                }

                if (lavaSpeedStart[s] <= 0f || lavaSpeedEnd[s] <= 0f)
                {
                    error = $"Lava speeds for stage {s + 1} must be greater than zero.";
                    return false;
                }
            }

            if (startMarblesPerTeam < 1 || (maxLivePerTeam > 0 && maxLivePerTeam < startMarblesPerTeam))
            {
                error = "startMarblesPerTeam must be at least 1 and no more than maxLivePerTeam (0 = no cap).";
                return false;
            }

            if (ringSinkIntervalEnd > ringSinkIntervalStart)
            {
                error = "ringSinkIntervalEnd should not be longer than ringSinkIntervalStart (the island escalates).";
                return false;
            }

            if (sweeperDegreesStart < 0f || sweeperDegreesEnd < sweeperDegreesStart)
            {
                error = "Sweeper speed must escalate: 0 <= sweeperDegreesStart <= sweeperDegreesEnd.";
                return false;
            }

            if (maxDurationSeconds < maxRunSeconds + payoffHoldSeconds + trackPreviewSeconds + 5f)
            {
                error = $"maxDurationSeconds ({maxDurationSeconds}) must exceed maxRunSeconds + payoffHoldSeconds + trackPreviewSeconds + 5 " +
                        $"({maxRunSeconds + payoffHoldSeconds + trackPreviewSeconds + 5f}), or the runner stops the run before the forced decision.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
