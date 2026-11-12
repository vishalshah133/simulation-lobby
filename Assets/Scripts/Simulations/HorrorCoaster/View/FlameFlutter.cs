using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>Candle flame: a living stretch and lean on an emissive teardrop. Cosmetic; driven by Time.time, never touches the simulation.</summary>
    public sealed class FlameFlutter : MonoBehaviour
    {
        public int seed;
        Vector3 _scale;

        void Awake()
        {
            _scale = transform.localScale;
        }

        void Update()
        {
            float n = SetUtil.Flicker(Time.time, seed, 11f);
            float k = Mathf.Lerp(0.8f, 1.2f, Mathf.Clamp01(n));
            transform.localScale = new Vector3(_scale.x * (2f - k) * 0.9f, _scale.y * k, _scale.z * (2f - k) * 0.9f);
        }
    }
}
