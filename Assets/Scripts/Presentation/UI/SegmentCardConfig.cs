using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>Plain data for one compilation segment: its card, hook and cold-viewer explainer.</summary>
    [CreateAssetMenu(fileName = "SegmentCard", menuName = "Simulation Lobby/Segment Card Config")]
    public sealed class SegmentCardConfig : ScriptableObject
    {
        public int index = 1;
        public int total = 10;

        [Tooltip("Short segment name, e.g. 'Digit Jump'.")]
        public string title = "Segment";

        [Tooltip("One-line question shown in the top bar.")]
        public string hook = "";

        [Tooltip("Plain-words explanation, max ~140 chars.")]
        [TextArea(2, 4)] public string explainer = "";

        [Min(0f)] public float cardSeconds = 1.5f;
        [Min(0f)] public float explainerSeconds = 6f;
        public Color accent = new Color(1f, 0.82f, 0.2f, 1f);
    }
}
