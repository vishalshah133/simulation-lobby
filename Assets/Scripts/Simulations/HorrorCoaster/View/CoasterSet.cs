using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Base for the acts built by the scene builder. Culls its <see cref="content"/> child unless the cart is in this act or a
    /// neighbouring one (cheap, and fog hides the pop), calls <see cref="Tick"/> while visible, and resets in <see cref="ResetSet"/>.
    /// Read-only on the simulation.
    /// </summary>
    public abstract class CoasterSet : ActSetBase
    {
        [Tooltip("Child holding every renderer and light of the set; toggled by distance in acts.")]
        public Transform content;
        [Tooltip("How many acts either side keep the content active.")]
        public int cullActs = 1;

        protected bool Near { get; private set; } = true;

        protected sealed override void OnBound()
        {
            Near = true;
            if (content != null)
            {
                content.gameObject.SetActive(true);
            }

            ResetSet();
        }

        /// <summary>Hide scare entities and zero animation so a re-Play starts clean.</summary>
        protected abstract void ResetSet();

        protected abstract void Tick();

        void Update()
        {
            if (State == null)
            {
                return;
            }

            Near = Mathf.Abs((int)State.Act - (int)Act) <= cullActs;
            if (content != null && content.gameObject.activeSelf != Near)
            {
                content.gameObject.SetActive(Near);
            }

            if (Near)
            {
                Tick();
            }
        }

        protected static void Show(Component c, bool on)
        {
            SetUtil.SetActive(c, on);
        }

        protected static void Show(GameObject g, bool on)
        {
            SetUtil.SetActive(g, on);
        }
    }
}
