using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// The sampled half of the coaster's sound (Creative Commons / licensed recordings in <c>Assets/Resources/CoasterSfx</c>). Every jump-scare
    /// sound comes from here; nothing for a scare is synthesised. The recordings are quiet and uneven (peaks from 0 to -24 dB, loud parts far
    /// lower), so the scare hits are loudness-matched once when first used: gain is chosen so the loudest 50 ms sits at a target level, then
    /// a soft saturator rounds the peaks. The result is a sudden, hard, full-scale hit; the master limiter still holds the final mix at -3 dBFS.
    /// Lead-ins and ambience (whisper, wolf, bells, omen bed) are left as recorded. A missing clip returns null.
    /// </summary>
    internal static class CoasterSfx
    {
        /// <summary>Target level (dB RMS of the loudest 50 ms) for each clip that is shaped. Clips not listed play as recorded.</summary>
        static readonly Dictionary<string, float> TargetDb = new Dictionary<string, float>
        {
            { "impact", -5f },
            { "stab", -6f },
            { "roar_a", -5f },
            { "roar_b", -6f },
            { "growl_deep", -6f },
            { "screech_a", -6f },
            { "screech_b", -6f },
            { "bat_screech", -6f },
            { "bat_wings", -9f },
            { "scream_female", -5f },
            { "scream_agony", -7f },
            { "snake_hiss_short", -8f },
            { "snake_hiss_long", -16f }, // a lead-in: audible, not a hit
        };

        const float MaxGainDb = 24f;

        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, AudioClip> Shaped = new Dictionary<string, AudioClip>();

        public static IEnumerable<string> ShapedNames => TargetDb.Keys;

        public static AudioClip Get(string name)
        {
            if (Shaped.TryGetValue(name, out AudioClip s) && s != null)
            {
                return s;
            }

            if (!Cache.TryGetValue(name, out AudioClip c) || c == null)
            {
                c = Resources.Load<AudioClip>("CoasterSfx/" + name);
                Cache[name] = c;
            }

            if (c == null || !TargetDb.TryGetValue(name, out float target))
            {
                return c;
            }

            if (c.loadState != AudioDataLoadState.Loaded)
            {
                c.LoadAudioData(); // decode now; the shaped copy is made on a later call, and until then the raw clip plays
                return c;
            }

            AudioClip shaped = Shape(c, target);
            if (shaped != null)
            {
                Shaped[name] = shaped;
                return shaped;
            }

            return c;
        }

        /// <summary>Starts decoding every shaped clip and, once decoded, builds its loud copy. Returns true when all are ready. Safe to call repeatedly.</summary>
        public static bool Warm()
        {
            bool all = true;
            foreach (string n in TargetDb.Keys)
            {
                Get(n);
                if (!Shaped.TryGetValue(n, out AudioClip c) || c == null)
                {
                    all = false;
                }
            }

            return all;
        }

        static AudioClip Shape(AudioClip src, float targetDb)
        {
            int ch = src.channels;
            var d = new float[src.samples * ch];
            if (d.Length == 0 || !src.GetData(d, 0))
            {
                return null;
            }

            // loudest 50 ms RMS
            int w = Mathf.Max(ch, (int)(0.05f * src.frequency) * ch);
            double best = 0.0;
            for (int s = 0; s + w <= d.Length; s += w / 2)
            {
                double e = 0.0;
                for (int i = 0; i < w; i++)
                {
                    e += d[s + i] * d[s + i];
                }

                e = System.Math.Sqrt(e / w);
                if (e > best)
                {
                    best = e;
                }
            }

            if (best < 1e-6)
            {
                return null;
            }

            float gainDb = Mathf.Min(MaxGainDb, targetDb - 20f * Mathf.Log10((float)best));
            float gain = Mathf.Pow(10f, gainDb / 20f);
            float norm = 1f / (float)System.Math.Tanh(1.0);
            for (int i = 0; i < d.Length; i++)
            {
                // soft saturation: transparent when quiet, rounds and hardens the peaks so the hit reads as loud, not just large
                d[i] = Mathf.Clamp((float)System.Math.Tanh(d[i] * gain) * norm * 0.98f, -1f, 1f);
            }

            AudioClip outClip = AudioClip.Create(src.name + " (loud)", src.samples, ch, src.frequency, false);
            outClip.SetData(d, 0);
            return outClip;
        }
    }
}
