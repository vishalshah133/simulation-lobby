using UnityEngine;

namespace SimulationLobby.Simulations.Cricket
{
    /// <summary>
    /// One side in a match: a name and two colours. Deliberately nothing else -- no crest, no kit, no
    /// player names -- so a team is a config asset and a new matchup is a swap in the inspector.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CricketTeam",
        menuName = "Simulation Lobby/Configs/Cricket Team",
        order = 14)]
    public sealed class CricketTeamDef : ScriptableObject
    {
        [Tooltip("Shown in the result line, e.g. INDIA.")]
        public string displayName = "TEAM";

        [Tooltip("Three letters for the score line, e.g. IND.")]
        public string shortName = "TEA";

        [Tooltip("Main colour: the score line and the shot lines of this side's innings.")]
        public Color primary = new Color(0.2f, 0.5f, 1f, 1f);

        [Tooltip("Second colour: trims and highlights.")]
        public Color secondary = new Color(1f, 0.6f, 0.15f, 1f);
    }
}
