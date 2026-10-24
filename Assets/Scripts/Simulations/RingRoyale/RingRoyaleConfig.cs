using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.RingRoyale
{
    /// <summary>
    /// Tunables for "Color Rumble": N colours of marbles in one spinning ring with an EXIT gap and an x2
    /// wall arc opposite it. A published video is this asset plus a scene plus a seed.
    /// </summary>
    /// <remarks>
    /// Balance (modelled 2026-10-21, collision-free): an x2 arc wider than the exit makes populations grow;
    /// the scheduled sudden death then widens the exit so the game resolves. Ball-ball collisions push
    /// marbles off clean chords, so expect more wall hits and shorter games in Unity. The x2 width is the
    /// growth dial, <see cref="suddenDeathAtSeconds"/> the length dial.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "RingRoyaleConfig",
        menuName = "Simulation Lobby/Configs/Color Rumble (royale)",
        order = 16)]
    public sealed class RingRoyaleConfig : SimulationConfig
    {
        [Header("Contenders")]
        [Tooltip("Marble colours, one per team. The number of entries is the number of teams.")]
        public Color[] colours =
        {
            new Color(0.95f, 0.18f, 0.2f, 1f),
            new Color(0.2f, 0.45f, 1f, 1f),
            new Color(0.15f, 0.82f, 0.32f, 1f),
            new Color(1f, 0.93f, 0.2f, 1f)
        };

        [Tooltip("Display names, shown in captions. Same order as the colours.")]
        public string[] colourNames = { "RED", "BLUE", "GREEN", "YELLOW" };

        [Range(1, 30)] public int marblesPerColour = 5;

        [Header("Marbles")]
        [Range(0.05f, 0.4f)] public float ballRadius = 0.18f;

        [Tooltip("Constant speed of every marble inside the ring, units/second.")]
        [Min(0.5f)] public float ballSpeed = 5f;

        [Tooltip("Clone heading: the parent's heading turned by a seeded angle in this range (degrees, " +
                 "either side). Too small and clones ride their parent in a clump.")]
        public Vector2 cloneTurnDegrees = new Vector2(15f, 55f);

        [Tooltip("Start positions are scattered inside this fraction of the ring radius.")]
        [Range(0.3f, 0.95f)] public float spawnRadiusFraction = 0.75f;

        [Tooltip("Minimum distance between start positions, world units. Never starts from dead centre.")]
        [Min(0.2f)] public float spawnSpacing = 0.9f;

        [Header("The ring")]
        [Min(1f)] public float ringRadius = 4.6f;

        [Tooltip("EXIT width at launch, degrees. A marble must fit through the chord at the ring radius.")]
        [Range(2f, 90f)] public float exitStartDegrees = 16f;

        [Tooltip("Where the EXIT starts, degrees CCW from +X. 90 = top.")]
        [Range(0f, 360f)] public float exitCenterDegrees = 90f;

        [Tooltip("Width of the gold x2 arc, degrees. The growth dial: wider = bigger numbers, longer game.")]
        [Range(5f, 180f)] public float x2ArcDegrees = 40f;

        [Tooltip("Where the x2 arc starts, degrees CCW from +X. 270 = bottom, opposite the EXIT.")]
        [Range(0f, 360f)] public float x2CenterDegrees = 270f;

        [Tooltip("Ring spin, degrees/second. Direction is seeded.")]
        public float ringAngularSpeed = 30f;

        [Header("Sudden death")]
        [Tooltip("Seconds after launch when the EXIT starts widening. The length dial.")]
        [Min(0f)] public float suddenDeathAtSeconds = 40f;

        [Tooltip("EXIT growth after that, degrees/second.")]
        [Min(0f)] public float suddenDeathDegreesPerSecond = 3f;

        [Tooltip("The EXIT never widens beyond this, degrees.")]
        [Range(20f, 340f)] public float maxExitDegrees = 300f;

        [Header("Limits")]
        [Tooltip("Most marbles inside at once. At the cap an x2 hit still flashes but no clone is born.")]
        [Range(20, 1000)] public int bodyCap = 300;

        [Tooltip("Safety cap on the game, seconds after launch. If hit, the colour with most marbles wins " +
                 "and the run is logged as a timeout.")]
        [Min(10f)] public float maxGameSeconds = 150f;

        [Header("Beats")]
        [Tooltip("Still beat before launch, so the setup and the question read.")]
        [Range(0f, 5f)] public float preLaunchHoldSeconds = 2f;

        [Tooltip("After the win the EXIT closes over this long, sealing the winners in. 0 = no seal.")]
        [Range(0f, 3f)] public float sealSeconds = 0.9f;

        [Tooltip("Beat after the seal, so the winners read over a still picture.")]
        [Range(0f, 10f)] public float resultHoldSeconds = 4f;

        [Tooltip("An exited marble keeps flying and is recycled once this far from the centre.")]
        [Min(1f)] public float recycleDistance = 9f;

        public int ColourCount => colours != null ? colours.Length : 0;

        /// <summary>Bodies the pool needs: a full ring plus the exited marbles still in flight.</summary>
        public int BodyBudget => bodyCap + Mathf.Max(60, bodyCap / 4);

        public float TotalSeconds => preLaunchHoldSeconds + maxGameSeconds + sealSeconds + resultHoldSeconds + 1f;

        public override int MaxTicks => Mathf.Max(1, Mathf.CeilToInt(TotalSeconds / fixedTimestep));

        public string NameOf(int colour)
        {
            return colourNames != null && colour >= 0 && colour < colourNames.Length
                ? colourNames[colour]
                : $"TEAM {colour + 1}";
        }

        void OnValidate()
        {
            maxDurationSeconds = TotalSeconds;
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (ColourCount < 2)
            {
                error = "Need at least two colours.";
                return false;
            }

            if (cloneTurnDegrees.x > cloneTurnDegrees.y)
            {
                error = "cloneTurnDegrees.x must not exceed .y.";
                return false;
            }

            if (ColourCount * marblesPerColour > bodyCap)
            {
                error = "bodyCap is below the starting marble count.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
