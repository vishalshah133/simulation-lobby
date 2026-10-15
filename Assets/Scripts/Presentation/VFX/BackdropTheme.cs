using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// One look for a 2D scene's backdrop: background colour plus the drifting motes. Several of these
    /// on a <see cref="BackdropStyler"/> let consecutive videos of the same format look different without
    /// touching the arena, which carries the readability rules.
    /// </summary>
    /// <remarks>
    /// Keep every theme dark. The ball, the wall and the HUD are tuned against a near-black frame, and a
    /// light background would undo their contrast.
    /// </remarks>
    [CreateAssetMenu(fileName = "BackdropTheme", menuName = "Simulation Lobby/Presentation/Backdrop Theme", order = 50)]
    public sealed class BackdropTheme : ScriptableObject
    {
        public string displayName = "Midnight";

        [Tooltip("Camera clear colour. Keep it dark.")]
        public Color background = new Color(0.04f, 0.05f, 0.09f, 1f);

        public Color moteTint = new Color(0.55f, 0.65f, 1f, 1f);

        [Range(0.01f, 0.5f)] public float moteAlpha = 0.13f;

        public Vector2 moteSize = new Vector2(0.05f, 0.2f);

        [Range(4, 400)] public int moteCount = 90;

        [Range(0.01f, 1f)] public float driftSpeed = 0.14f;

        [Tooltip("Steady current: up = embers, down = snow, zero = wander.")]
        public Vector2 flow;

        [Range(0f, 180f)] public float spinSpeed;

        [Tooltip("Mote shape (soft dot, star, diamond...). Empty keeps the field's existing texture.")]
        public Texture2D moteTexture;
    }
}
