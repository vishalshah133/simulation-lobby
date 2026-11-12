using UnityEngine;

namespace SimulationLobby.Simulations.TrippyCoaster
{
    /// <summary>Coloured point lights that orbit the cart and take the current world's palette. They light the cart body and the track.</summary>
    [DefaultExecutionOrder(110)]
    public sealed class TripLightRig : MonoBehaviour
    {
        public TrippyCoasterHost host;
        public Light[] lights;
        public float orbitRadius = 3.2f;

        void LateUpdate()
        {
            if (host == null || !host.Ready || lights == null) return;
            ITripState s = host.State;
            TripWorldDef w = s.Config.worlds[s.WorldIndex];
            float t = s.SimTime;
            Vector3 right = Vector3.Cross(s.CartUp, s.CartForward);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                float a = t * 0.9f + i * 6.28318f / lights.Length;
                Vector3 off = right * (Mathf.Cos(a) * orbitRadius) + s.CartUp * (1.4f + Mathf.Sin(a) * orbitRadius * 0.6f)
                              + s.CartForward * (Mathf.Sin(a * 0.5f) * 2f);
                lights[i].transform.position = s.CartPosition + off;
                lights[i].color = TripPalette.Vivid(w, t * w.hueSpeed + i * 0.33f);
                lights[i].intensity = 6f + 3f * Mathf.Sin(t * 1.3f + i);
            }
        }
    }
}
