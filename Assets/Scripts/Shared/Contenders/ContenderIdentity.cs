using UnityEngine;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// Who a contender is: a name, a colour and a slot. The one small component every format that has
    /// named competitors (<c>race</c>, <c>wars</c>, <c>elim</c>) puts on its bodies, so leaderboards,
    /// labels and trails can be built without knowing which format spawned them.
    /// </summary>
    public sealed class ContenderIdentity : MonoBehaviour
    {
        [SerializeField] string _displayName = "Contender";
        [SerializeField] Color _color = Color.white;
        [SerializeField] int _index;

        public string DisplayName => _displayName;
        public Color Color => _color;

        /// <summary>Stable position in the roster. Not a rank.</summary>
        public int Index => _index;

        public void Set(int index, string displayName, Color color)
        {
            _index = index;
            _displayName = displayName;
            _color = color;
        }
    }
}
