using SimulationLobby.Presentation;
using UnityEngine;

namespace SimulationLobby.Simulations.Elimination
{
    /// <summary>
    /// The show's few one-shots, made in code like the rest of the house sound (nothing sampled, nothing to
    /// license): a gong for the last safe spot, a thud per OUT stamp, a rising two-note for a return, a chord for
    /// the champion, a whoosh for each fly-in and a riser for the twist.
    /// </summary>
    public sealed class EliminationAudioKit
    {
        const int SampleRate = 44100;

        public AudioClip gong;
        public AudioClip[] thuds;
        public AudioClip whoosh;
        public AudioClip riser;
        public AudioClip[] returnNotes;
        public AudioClip[] chord;

        public static EliminationAudioKit Create()
        {
            var kit = new EliminationAudioKit
            {
                gong = CreateGong(),
                thuds = new[]
                {
                    ProceduralToneBank.CreateImpactThud(0.55f, 0.34f, 21, "ElimThud1"),
                    ProceduralToneBank.CreateImpactThud(0.5f, 0.34f, 22, "ElimThud2"),
                    ProceduralToneBank.CreateImpactThud(0.6f, 0.34f, 23, "ElimThud3")
                },
                whoosh = ProceduralToneBank.CreateWhoosh(1.1f, 31, "ElimWhoosh"),
                riser = ProceduralToneBank.CreateRiser(3.2f, 80f, 300f, 33, "ElimRiser"),
                returnNotes = new[]
                {
                    ProceduralToneBank.CreateTone(392f, 1.3f, "ElimBackG"),
                    ProceduralToneBank.CreateTone(587.33f, 1.6f, "ElimBackD")
                },
                chord = new[]
                {
                    ProceduralToneBank.CreateTone(261.63f, 2.6f, "ElimChordC"),
                    ProceduralToneBank.CreateTone(329.63f, 2.6f, "ElimChordE"),
                    ProceduralToneBank.CreateTone(392f, 2.6f, "ElimChordG"),
                    ProceduralToneBank.CreateTone(523.25f, 2.6f, "ElimChordC2")
                }
            };
            return kit;
        }

        /// <summary>A struck gong: inharmonic partials with different decays, a soft attack, a slow beat between two close partials.</summary>
        static AudioClip CreateGong()
        {
            const float duration = 3.2f;
            int count = Mathf.RoundToInt(SampleRate * duration);
            var samples = new float[count];
            float[] ratios = { 1f, 1.51f, 2.04f, 2.77f, 3.4f, 4.12f };
            float[] weights = { 1f, 0.7f, 0.5f, 0.32f, 0.2f, 0.12f };
            float[] decays = { 1.1f, 1.5f, 2.0f, 2.6f, 3.4f, 4.2f };
            const float root = 98f;
            int attack = Mathf.RoundToInt(SampleRate * 0.012f);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float value = 0f;
                for (int p = 0; p < ratios.Length; p++)
                {
                    float f = root * ratios[p];
                    value += weights[p] * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-decays[p] * t);
                }

                // A second, slightly detuned fundamental gives the slow shimmer of a real gong.
                value += 0.6f * Mathf.Sin(2f * Mathf.PI * (root * 1.012f) * t) * Mathf.Exp(-1.3f * t);
                float env = Mathf.Min(1f, i / (float)attack);
                samples[i] = value * env * 0.22f;
            }

            var clip = AudioClip.Create("ElimGong", count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
