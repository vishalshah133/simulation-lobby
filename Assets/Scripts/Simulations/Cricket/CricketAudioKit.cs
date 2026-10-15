using UnityEngine;

namespace SimulationLobby.Simulations.Cricket
{
    /// <summary>
    /// The sounds a cricket video plays, one slot per moment. A real recorded crowd, because a
    /// synthesized one sounded wrong; every slot is optional and a missing one is simply silent
    /// (except bat and stumps, which fall back to procedural knocks).
    /// </summary>
    /// <remarks>
    /// Drop royalty-free clips into <c>Assets/Audio/Cricket/</c> named as below and run
    /// <b>Simulation Lobby ▸ 2D Formats ▸ Cricket (cric) ▸ Fill Audio Kit</b> (the scene builder also does it), or drag clips
    /// into the slots by hand. Check each clip's licence allows monetised YouTube use before publishing.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "CricketAudioKit",
        menuName = "Simulation Lobby/Configs/Cricket Audio Kit",
        order = 15)]
    public sealed class CricketAudioKit : ScriptableObject
    {
        [Header("Crowd (file name contains ...)")]
        [Tooltip("cheer_small: a ripple of approval. Used for threes. Falls back to applause.")]
        public AudioClip cheerSmall;

        [Tooltip("cheer_big: a proper cheer. Used for fours and when a wicket falls.")]
        public AudioClip cheerBig;

        [Tooltip("cheer_roar: the full stand, for a six. Falls back to cheer_big.")]
        public AudioClip cheerRoar;

        [Tooltip("cheer_final: the roar when the match is won. Falls back to cheer_roar.")]
        public AudioClip cheerFinal;

        [Tooltip("groan: a collective 'ooooh'. Used for a dot ball and, with a cheer, for a wicket.")]
        public AudioClip groan;

        [Tooltip("applause: for twos, the end of an innings and the result.")]
        public AudioClip applause;

        [Tooltip("applause_soft: a polite clap for a single. Falls back to applause.")]
        public AudioClip applauseSoft;

        [Header("Hard sounds (optional; procedural knocks are used if empty)")]
        [Tooltip("bat: leather on willow.")]
        public AudioClip bat;

        [Tooltip("stumps: wickets going over.")]
        public AudioClip stumps;
    }
}
