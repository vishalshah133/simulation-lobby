using UnityEngine;

namespace SimulationLobby.Simulations.ReleaseOrMultiply
{
    /// <summary>
    /// Presentation-only tunables for the 16:9 "Gate Funnel" variant (comp-001 segment 3). Nothing here
    /// feeds back into the run: the run is <see cref="RomConfig"/> + seed. These knobs change only when
    /// the run starts, how long captions hold, and how the gold pile and final number look.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RomWideSettings",
        menuName = "Simulation Lobby/Configs/Release or Multiply Wide (rom)",
        order = 13)]
    public sealed class RomWideSettings : ScriptableObject
    {
        [Header("Start")]
        [Tooltip("Wall-clock seconds the board sits still before the run begins. Keep at least the segment card " +
                 "length so the first drop is seen.")]
        [Min(0f)] public float startDelaySeconds = 1.8f;

        [Header("Captions")]
        [Tooltip("Minimum seconds a result caption stays before the next replaces it (plan rule: 1.2s or more).")]
        [Min(1.2f)] public float captionHoldSeconds = 1.5f;

        public string openingCaption = "ONE MARBLE. FIVE ROWS OF GATES.";

        [Header("Gold pile (visual only, shows marbles that reached the bottom)")]
        [Range(20, 400)] public int pileMaxDots = 170;
        [Range(0.15f, 0.8f)] public float pileDotDiameter = 0.5f;
        [Range(1, 30)] public int pileDotsRevealedPerFrame = 6;

        [Header("Final number")]
        [Tooltip("Reference-pixel font size of the huge final count (canvas is 1920x1080).")]
        [Range(120f, 520f)] public float finalNumberSize = 330f;
        [Range(0.05f, 2f)] public float finalFadeSeconds = 0.5f;
    }
}
