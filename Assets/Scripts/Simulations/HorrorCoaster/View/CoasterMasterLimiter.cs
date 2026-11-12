using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Last stage of the mix: a soft-knee limiter on the final output so the ride can never clip. Lives on the same GameObject as
    /// the <see cref="AudioListener"/> (Unity runs <c>OnAudioFilterRead</c> there on the whole mix). Peak ceiling -3 dBFS.
    /// Knee starts at 0.5, rolls off with tanh toward the ceiling, and a hard clamp sits at the ceiling.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoasterMasterLimiter : MonoBehaviour
    {
        /// <summary>-3 dBFS in linear amplitude.</summary>
        public const float Ceiling = 0.70795f;
        const float Knee = 0.5f;

        void OnAudioFilterRead(float[] data, int channels)
        {
            float room = Ceiling - Knee;
            for (int i = 0; i < data.Length; i++)
            {
                float x = data[i];
                float a = x < 0f ? -x : x;
                if (a > Knee)
                {
                    float y = Knee + room * (float)System.Math.Tanh((a - Knee) / room);
                    if (y > Ceiling)
                    {
                        y = Ceiling;
                    }

                    x = x < 0f ? -y : y;
                }

                data[i] = x;
            }
        }
    }
}
