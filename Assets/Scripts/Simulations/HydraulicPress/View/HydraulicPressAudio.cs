using UnityEngine;

namespace SimulationLobby.Simulations.HydraulicPress
{
    /// <summary>
    /// Clip generation and mappings for the <c>press</c> format. Built on <c>ProceduralToneBank</c>: nothing
    /// sampled. Pure functions so the pitch mapping can be checked without a scene.
    /// </summary>
    public static class HydraulicPressAudio
    {
        const int SampleRate = 44100;

        /// <summary>
        /// A pop: a dry click with a short glass clink laid under it, so a single pop is unmistakable and a
        /// pile of them sounds like ice cracking rather than noise. Pitch it by setting the source pitch.
        /// </summary>
        public static AudioClip CreatePop(int seed = 7, string name = "PressPop")
        {
            AudioClip click = Presentation.ProceduralToneBank.CreateClick(1.4f, seed, name + "_Click");
            AudioClip clink = Presentation.ProceduralToneBank.CreateGlassClink(2.2f, 0.18f, seed + 1, name + "_Clink");
            return Blend(click, clink, 0.45f, name);
        }

        /// <summary>
        /// Sum two clips (the longer sets the length), the second at <paramref name="gainB"/>, normalised to a
        /// peak of 0.9. Falls back to the first clip if the sample data cannot be read.
        /// </summary>
        public static AudioClip Blend(AudioClip a, AudioClip b, float gainB, string name)
        {
            try
            {
                int channels = a.channels;
                var da = new float[a.samples * a.channels];
                var db = new float[b.samples * b.channels];
                a.GetData(da, 0);
                b.GetData(db, 0);
                if (b.channels != channels)
                {
                    return a;
                }

                int length = Mathf.Max(da.Length, db.Length);
                var mix = new float[length];
                float peak = 0.0001f;
                for (int i = 0; i < length; i++)
                {
                    float s = (i < da.Length ? da[i] : 0f) + (i < db.Length ? db[i] * gainB : 0f);
                    mix[i] = s;
                    peak = Mathf.Max(peak, Mathf.Abs(s));
                }

                float gain = 0.9f / peak;
                for (int i = 0; i < length; i++)
                {
                    mix[i] *= gain;
                }

                var clip = AudioClip.Create(name, length / channels, channels, a.frequency, false);
                clip.SetData(mix, 0);
                return clip;
            }
            catch (System.Exception)
            {
                return a;
            }
        }

        /// <summary>
        /// A seamless crackle bed: sparse, randomly spaced, very short noise ticks (about 140 per second),
        /// one second long. Seeded, so the same seed gives the same bed.
        /// </summary>
        public static AudioClip CreateCrackleLoop(int seed = 21, string name = "PressCrackle")
        {
            var samples = new float[SampleRate];
            var random = new System.Random(seed);
            int ticks = 140;
            for (int t = 0; t < ticks; t++)
            {
                int start = random.Next(SampleRate);
                float amp = 0.25f + 0.75f * (float)random.NextDouble();
                int len = 40 + random.Next(120);
                for (int i = 0; i < len; i++)
                {
                    float env = Mathf.Exp(-i / (len * 0.25f));
                    float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                    samples[(start + i) % SampleRate] += noise * env * amp;
                }
            }

            float peak = 0.0001f;
            for (int i = 0; i < samples.Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= 0.8f / peak;
            }

            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Pop pitch for a generation: smaller balls ring higher, about a whole tone per 1.2 generations, capped.</summary>
        public static float PopPitch(int generation, float octavesPerGeneration = 0.1f, float maxPitch = 2.6f)
        {
            return Mathf.Min(maxPitch, Mathf.Pow(2f, Mathf.Max(0, generation) * octavesPerGeneration));
        }
    }
}
