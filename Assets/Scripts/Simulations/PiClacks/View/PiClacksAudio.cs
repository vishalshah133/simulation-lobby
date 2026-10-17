using UnityEngine;

namespace SimulationLobby.Simulations.PiClacks
{
    /// <summary>
    /// Clip generation and the rate-to-sound mapping for the <c>clack</c> format. All procedural, nothing
    /// sampled. Pure functions, so the mapping is unit-checkable without a scene.
    /// </summary>
    public static class PiClacksAudio
    {
        const int SampleRate = 44100;

        /// <summary>
        /// A seamlessly looping buzz at <paramref name="baseHz"/> (one second, a whole number of cycles, so the
        /// loop point never clicks): a soft sawtooth from eight harmonics. The source's pitch property then
        /// sweeps it, so one clip covers the whole rising tone.
        /// </summary>
        public static AudioClip CreateBuzzLoop(int baseHz = 220, string name = "ClackBuzz")
        {
            var samples = new float[SampleRate];
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float phase = 2f * Mathf.PI * baseHz * (i / (float)SampleRate);
                float value = 0f;
                for (int k = 1; k <= 8; k++)
                {
                    value += Mathf.Sin(phase * k) / k;
                }

                samples[i] = value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }

            float gain = peak > 0f ? 0.7f / peak : 1f;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
            }

            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Rate (clacks per second) to 0..1 on a log scale: <paramref name="lowLog"/> (log10) maps to 0 and
        /// <paramref name="highLog"/> to 1.
        /// </summary>
        public static float RateToUnit(float clacksPerSecond, float lowLog, float highLog)
        {
            if (clacksPerSecond <= 1f)
            {
                return 0f;
            }

            float log = Mathf.Log10(clacksPerSecond);
            return Mathf.Clamp01((log - lowLog) / Mathf.Max(0.0001f, highLog - lowLog));
        }

        /// <summary>Buzz pitch for a rate: log10 1.5 to 6.5 onto the pitch range (default 0.7 to 2.4).</summary>
        public static float RateToPitch(float clacksPerSecond, float lowLog, float highLog, float pitchLow, float pitchHigh)
        {
            return Mathf.Lerp(pitchLow, pitchHigh, RateToUnit(clacksPerSecond, lowLog, highLog));
        }
    }
}
