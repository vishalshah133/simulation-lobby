using UnityEngine;

namespace SimulationLobby.Simulations.TrippyCoaster
{
    /// <summary>A huge dark plasma sphere that follows the cart and swaps to the current world's look, so open worlds have a sky instead of a void. Reads state only.</summary>
    [DefaultExecutionOrder(105)]
    public sealed class TripBackdrop : MonoBehaviour
    {
        public TrippyCoasterHost host;
        public Material[] perWorld;
        public Renderer target;

        int _world = -1;

        void LateUpdate()
        {
            if (host == null || !host.Ready || target == null || perWorld == null || perWorld.Length == 0) return;
            ITripState s = host.State;
            transform.position = s.CartPosition;
            if (s.WorldIndex != _world)
            {
                _world = s.WorldIndex;
                target.sharedMaterial = perWorld[Mathf.Clamp(_world, 0, perWorld.Length - 1)];
            }
        }
    }
}
