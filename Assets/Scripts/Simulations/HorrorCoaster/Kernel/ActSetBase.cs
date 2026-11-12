using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Base for every per-act set / scare-entity component in the scene. Views are read-only: they read
    /// <see cref="State"/> in Update/LateUpdate and never write simulation state. The presenter calls
    /// <see cref="Bind"/> once at Play start on every ActSetBase under the scene root.
    /// </summary>
    public abstract class ActSetBase : MonoBehaviour
    {
        public abstract ActId Act { get; }
        protected ICoasterState State { get; private set; }

        public void Bind(ICoasterState state)
        {
            State = state;
            OnBound();
        }

        /// <summary>Called after State is set. Reset any animation here so a re-Play starts clean.</summary>
        protected virtual void OnBound() { }
    }
}
