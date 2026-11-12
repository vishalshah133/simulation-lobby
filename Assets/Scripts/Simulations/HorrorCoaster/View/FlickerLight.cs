using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>Candle-like flicker on a Light. Cosmetic only; driven by Time.time.</summary>
    public sealed class FlickerLight : MonoBehaviour
    {
        public Light target;
        public float baseIntensity = 1f;
        [Range(0f, 1f)] public float amount = 0.35f;
        public float speed = 9f;
        public int seed;

        void Update()
        {
            if (target == null)
            {
                return;
            }

            float n = Mathf.Clamp01(SetUtil.Flicker(Time.time, seed, speed));
            target.intensity = baseIntensity * Mathf.Lerp(1f - amount, 1f, n);
        }
    }
}
