using UnityEngine;

namespace SimulationLobby.Simulations.TrippyCoaster
{
    /// <summary>Pushes simulation time into the trip shaders. Every pattern is a pure function of this value, so a replay is identical.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class TripShaderGlobals : MonoBehaviour
    {
        static readonly int TripTime = Shader.PropertyToID("_TripTime");

        public TrippyCoasterHost host;

        void OnEnable()
        {
            Shader.SetGlobalFloat(TripTime, 0f);
        }

        void LateUpdate()
        {
            if (host == null || !host.Ready) return;
            Shader.SetGlobalFloat(TripTime, host.State.SimTime);
        }
    }
}
