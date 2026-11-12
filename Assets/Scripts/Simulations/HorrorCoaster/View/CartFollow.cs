using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>Keeps its transform at the cart (ambient dust / embers that travel with the ride). Plain MonoBehaviour, reads only.</summary>
    public sealed class CartFollow : MonoBehaviour
    {
        public Vector3 offset = new Vector3(0f, 1.5f, 8f);
        HorrorCoasterHost host;

        void LateUpdate()
        {
            if (host == null)
            {
                host = FindAnyObjectByType<HorrorCoasterHost>();
            }

            if (host == null || !host.Ready || host.State == null)
            {
                return;
            }

            ICoasterState s = host.State;
            transform.position = s.CartPosition + Quaternion.LookRotation(s.CartForward, Vector3.up) * offset;
        }
    }
}
