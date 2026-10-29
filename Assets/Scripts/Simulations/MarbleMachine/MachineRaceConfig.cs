using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine
{
    /// <summary>
    /// Config for the marble machine as a lap race (<c>mach-002</c>): 16 numbered balls, N laps, first across the line wins.
    /// Derives from <see cref="MachineConfig"/> so every existing station can be initialised with it unchanged. Contract file for
    /// mach-002: work packages read it, they don't add fields to it.
    /// </summary>
    /// <remarks>
    /// Reused base fields in race mode: <c>ballCount</c> = racerCount, <c>ballRadius</c> = racer radius (about 0.2),
    /// <c>colours</c> = one entry per racer (name is the number "1".."16"), <c>tubeCapacity</c> = 1 (unused; keeps the base
    /// <c>Validate</c> happy), <c>holdSeconds</c> = the grid hold before the countdown, <c>outroSeconds</c> = time after the win.
    /// <c>stages</c>, <c>sorterLanes</c>, <c>singulator*</c>, <c>sorterFlap*</c> and <c>finaleFraction</c> are unused.
    /// </remarks>
    [CreateAssetMenu(menuName = "Simulation Lobby/Marble Machine Race Config", fileName = "MachineRaceConfig")]
    public sealed class MachineRaceConfig : MachineConfig
    {
        [Header("Race")]
        [Min(2)] public int racerCount = 16;
        [Min(1)] public int laps = 3;
        [Tooltip("Seconds of 3-2-1 after the grid hold, before the start gate drops.")]
        [Min(0f)] public float countdownSeconds = 3f;
        [Tooltip("Racers on the grid per row (the grid is racerCount / gridColumns rows). Must fit the start gate width.")]
        [Min(1)] public int gridColumns = 4;
        [Tooltip("Leader enters the last lap: the Finale phase starts.")]
        public bool finaleOnLastLap = true;

        [Header("Layout")]
        [Tooltip("Shallowest connector slope in degrees. mach-001 uses 8; the race wants a faster course.")]
        [Range(6f, 30f)] public float targetSlopeDegrees = 12f;

        [Tooltip("Height the screw lift raises racers, metres. mach-001 uses a fixed 6.1; the race loop needs more to close in height.")]
        [Min(2f)] public float screwLiftRise = 10f;

        [Header("Presentation")]
        [Tooltip("Rows the leaderboard shows (top N plus the followed racer if outside).")]
        [Min(3)] public int boardRows = 5;
        [Min(0.5f)] public float lapBannerSeconds = 3f;

        void Reset()
        {
            fixedTimestep = 1f / 100f;
            maxDurationSeconds = 900f;
            ballCount = 16;
            ballRadius = 0.2f;
            ballMass = 0.2f;
            tubeCapacity = 1;
            chuteWidth = 1.8f;
            tipperThreshold = 2;
            holdSeconds = 6f;
            killPlaneY = -2f;
            maxBallSpeed = 14f;
            colours = DefaultRacerColours();
        }

        /// <summary>16 high-contrast hues, named by number. Neighbouring numbers differ strongly in hue or lightness.</summary>
        public static MachineColour[] DefaultRacerColours()
        {
            Color[] c =
            {
                new Color(0.92f, 0.12f, 0.14f), new Color(0.12f, 0.42f, 0.95f), new Color(1.00f, 0.86f, 0.10f), new Color(0.15f, 0.78f, 0.25f),
                new Color(1.00f, 0.50f, 0.05f), new Color(0.58f, 0.22f, 0.88f), new Color(0.10f, 0.85f, 0.85f), new Color(1.00f, 0.38f, 0.70f),
                new Color(0.55f, 0.35f, 0.15f), new Color(0.95f, 0.95f, 0.95f), new Color(0.05f, 0.05f, 0.10f), new Color(0.60f, 0.90f, 0.20f),
                new Color(0.10f, 0.20f, 0.55f), new Color(0.75f, 0.10f, 0.45f), new Color(0.98f, 0.70f, 0.55f), new Color(0.45f, 0.55f, 0.60f),
            };
            var result = new MachineColour[c.Length];
            for (int i = 0; i < c.Length; i++) result[i] = new MachineColour((i + 1).ToString(), c[i]);
            return result;
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error)) return false;
            if (colours == null || colours.Length != racerCount)
            {
                error = $"colours has {(colours == null ? 0 : colours.Length)} entries but racerCount is {racerCount}.";
                return false;
            }

            if (racerCount > ballCount)
            {
                error = "ballCount must be at least racerCount.";
                return false;
            }

            if (gridColumns * Mathf.CeilToInt(racerCount / (float)gridColumns) < racerCount)
            {
                error = "gridColumns cannot seat every racer.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
