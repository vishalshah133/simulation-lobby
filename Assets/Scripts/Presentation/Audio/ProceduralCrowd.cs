using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// The two hard sounds a cricket ground needs: a bat crack and wood on wood. Short, pitched
    /// knocks that synthesise convincingly. The sibling of <see cref="ProceduralToneBank"/>:
    /// nothing sampled, nothing to license.
    /// </summary>
    /// <remarks>
    /// A synthesized <b>crowd</b> (noise, then a formant choir) was tried twice and sounded wrong both
    /// times, so crowd sounds are recorded clips in a <c>CricketAudioKit</c> instead. Each call takes
    /// a seed and uses its own generator: the same arguments make the same clip, and nothing here
    /// touches the simulation's random stream.
    /// </remarks>
    public static class ProceduralCrowd
    {
        const int SampleRate = 44100;

        /// <summary>Leather on willow: a dry wooden "tok", with only a hair of click at the front.</summary>
        public static AudioClip CreateBatCrack(float pitch = 1f, int seed = 25, string name = "BatCrack")
        {
            const float duration = 0.22f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            var rng = new System.Random(seed);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float knock = Mathf.Sin(2f * Mathf.PI * 175f * pitch * t) * Mathf.Exp(-42f * t) * 0.75f;
                float body = Mathf.Sin(2f * Mathf.PI * 520f * pitch * t) * Mathf.Exp(-70f * t) * 0.45f;
                float wood = Mathf.Sin(2f * Mathf.PI * 1180f * pitch * t) * Mathf.Exp(-95f * t) * 0.4f;
                float click = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-900f * t) * 0.45f;

                float attack = Mathf.Min(1f, i / (SampleRate * 0.0006f));
                samples[i] = (knock + body + wood + click) * attack;
            }

            return Finish(name, samples, 0.85f);
        }

        /// <summary>Stumps going over: a short run of dry wooden knocks, pitched, no noise.</summary>
        public static AudioClip CreateStumpsClatter(int seed = 26, string name = "Stumps")
        {
            const float duration = 0.75f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            var rng = new System.Random(seed);

            const int hits = 7;
            var when = new float[hits];
            var freq = new float[hits];
            var amp = new float[hits];
            float cursor = 0f;
            for (int h = 0; h < hits; h++)
            {
                when[h] = cursor;
                freq[h] = 420f + (float)rng.NextDouble() * 900f;
                amp[h] = 0.9f - h * 0.1f;
                cursor += 0.02f + (float)rng.NextDouble() * 0.06f;
            }

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float value = 0f;
                for (int h = 0; h < hits; h++)
                {
                    float local = t - when[h];
                    if (local < 0f)
                    {
                        continue;
                    }

                    // A fundamental and an inharmonic partial: wood, not a bell.
                    float knock = Mathf.Sin(2f * Mathf.PI * freq[h] * local) + 0.5f * Mathf.Sin(2f * Mathf.PI * freq[h] * 2.4f * local);
                    float attack = Mathf.Min(1f, local / 0.0008f);
                    value += knock * Mathf.Exp(-38f * local) * attack * amp[h];
                }

                samples[i] = value * 0.4f;
            }

            return Finish(name, samples, 0.8f);
        }

        /// <summary>Scales to a target peak and wraps in a clip, so every clip arrives at a known level.</summary>
        static AudioClip Finish(string name, float[] samples, float targetPeak)
        {
            // A short fade at both ends, so no clip can start or stop with a click.
            int fade = Mathf.Min(samples.Length / 2, Mathf.RoundToInt(SampleRate * 0.01f));
            for (int i = 0; i < fade; i++)
            {
                float g = i / (float)fade;
                samples[i] *= g;
                samples[samples.Length - 1 - i] *= g;
            }

            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }

            if (peak > 0.0001f)
            {
                float gain = targetPeak / peak;
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] *= gain;
                }
            }

            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
