using UnityEngine;

namespace SimulationLobby.Simulations.Brachistochrone
{
    /// <summary>
    /// Looping clips for the <c>brach</c> format. All procedural, nothing sampled. Every loop is exactly one second
    /// with a whole number of cycles of every partial, so the loop point never clicks; the source's pitch
    /// property then sweeps the hum with the ball's speed.
    /// </summary>
    public static class BrachistochroneAudio
    {
        const int SampleRate = 44100;

        /// <summary>Soft rolling hum: a fundamental with two quiet overtones and a slow shimmer (whole cycles).</summary>
        public static AudioClip CreateHumLoop(int baseHz = 110, string name = "BrachHum")
        {
            var samples = new float[SampleRate];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)SampleRate;
                float phase = 2f * Mathf.PI * baseHz * t;
                float shimmer = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 3f * t);
                samples[i] = (Mathf.Sin(phase) + 0.35f * Mathf.Sin(2f * phase) + 0.12f * Mathf.Sin(3f * phase)) * shimmer * 0.55f;
            }

            return Make(name, samples);
        }

        /// <summary>Low pad for the photo-finish slow motion: a root, a fifth and an octave, slightly detuned by whole cycles.</summary>
        public static AudioClip CreateSwellLoop(int rootHz = 55, string name = "BrachSwell")
        {
            var samples = new float[SampleRate];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)SampleRate;
                float two = 2f * Mathf.PI * t;
                samples[i] = (Mathf.Sin(two * rootHz) + 0.7f * Mathf.Sin(two * rootHz * 1.5f) +
                              0.5f * Mathf.Sin(two * rootHz * 2f) + 0.25f * Mathf.Sin(two * (rootHz * 3 + 2))) * 0.3f;
            }

            return Make(name, samples);
        }

        static AudioClip Make(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
