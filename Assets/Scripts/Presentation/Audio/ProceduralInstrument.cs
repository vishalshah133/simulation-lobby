using System;
using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Pitched instrument timbres built in code, so a melody can be played in a glass bell, a flute,
    /// a soft piano, a celesta, a harp or a mallet without a single audio file.
    /// </summary>
    /// <remarks>
    /// Same reasoning as <see cref="ProceduralToneBank"/>: synthesised, so there is nothing to license.
    /// Everything is deterministic: the only randomness is a seeded <see cref="System.Random"/> (breath
    /// noise, hammer thump, the harp's pluck), so a re-render matches the original take.
    /// <para>
    /// Every note is normalised to the same peak so timbres are balanced against each other, starts with a
    /// soft attack (no click), and fades to exactly 0 at the end. Low notes get extra 2nd and 3rd partials
    /// because phone speakers roll off hard below ~250 Hz.
    /// </para>
    /// </remarks>
    public static class ProceduralInstrument
    {
        public enum Timbre { GlassBell, Flute, SoftPiano, Celesta, Harp, Mallet }

        const int SampleRate = 44100;
        const float TargetPeak = 0.8f;
        const double TailSeconds = 0.4;
        const double FadeOutSeconds = 0.06;
        const double TwoPi = 2.0 * Math.PI;

        /// <summary>
        /// One pitched note. <paramref name="ringSeconds"/> is how long it should sing; the clip is a little
        /// longer (a tail that fades to silence), so an overlapping next note never meets a hard cut.
        /// </summary>
        public static AudioClip CreateNote(Timbre timbre, float frequencyHz, float ringSeconds, int seed = 1)
        {
            float[] samples = Render(timbre, frequencyHz, ringSeconds, seed);
            var clip = AudioClip.Create($"{timbre}_{frequencyHz:F1}Hz", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>The sample maths, separated from AudioClip so it can be ported/tested offline.</summary>
        public static float[] Render(Timbre timbre, float frequencyHz, float ringSeconds, int seed = 1)
        {
            double f = Math.Max(30.0, Math.Min(frequencyHz, 4000.0));
            double ring = Math.Max(0.1, Math.Min(ringSeconds, 6.0));
            double duration = ring + TailSeconds;
            int n = Math.Max(2, (int)Math.Round(SampleRate * duration));
            var buf = new double[n];
            var rng = new System.Random(seed);

            // 0 at 300 Hz and above, 1 at 200 Hz and below: how much to reinforce the upper partials.
            double lowBoost = Math.Max(0.0, Math.Min(1.0, (300.0 - f) / 100.0));

            switch (timbre)
            {
                case Timbre.GlassBell: RenderGlassBell(buf, f, ring, lowBoost); break;
                case Timbre.Flute: RenderFlute(buf, f, ring, lowBoost, rng); break;
                case Timbre.SoftPiano: RenderSoftPiano(buf, f, ring, lowBoost, rng); break;
                case Timbre.Celesta: RenderCelesta(buf, f, ring, lowBoost); break;
                case Timbre.Harp: RenderHarp(buf, f, ring, rng); break;
                default: RenderMallet(buf, f, ring, lowBoost); break;
            }

            return Finish(buf);
        }

        // ---------------------------------------------------------------- timbres

        static void RenderGlassBell(double[] buf, double f, double ring, double lowBoost)
        {
            // Inharmonic partials are what make glass sound like glass and not a sine.
            double[] ratio = { 1.0, 2.0, 2.76, 5.4, 8.93 };
            double[] amp = { 1.0, 0.5, 0.35, 0.15, 0.08 };
            double[] decayMul = { 1.0, 1.4, 2.0, 3.0, 4.0 };
            amp[1] += 1.0 * lowBoost;
            amp[2] += 0.7 * lowBoost;

            double baseRate = 5.5 / ring;
            for (int p = 0; p < ratio.Length; p++)
            {
                double pf = f * ratio[p];
                if (pf > 16000.0) continue;
                double w = TwoPi * pf / SampleRate;
                double rate = baseRate * decayMul[p];
                for (int i = 0; i < buf.Length; i++)
                {
                    double t = i / (double)SampleRate;
                    buf[i] += amp[p] * Math.Exp(-rate * t) * Math.Sin(w * i);
                }
            }

            SoftAttack(buf, 0.003);
        }

        static void RenderFlute(double[] buf, double f, double ring, double lowBoost, System.Random rng)
        {
            double a2 = 0.12 + 1.0 * lowBoost;
            double a3 = 0.05 + 0.6 * lowBoost;
            double phase = 0.0;
            double lp = 0.0;
            double releaseStart = Math.Max(0.1, ring * 0.9);

            for (int i = 0; i < buf.Length; i++)
            {
                double t = i / (double)SampleRate;
                // Vibrato fades in over 0.4 s: a held flute note starts straight, then wavers.
                double vibDepth = 0.006 * Math.Min(1.0, t / 0.4);
                double vib = 1.0 + vibDepth * Math.Sin(TwoPi * 5.0 * t);
                phase += TwoPi * f * vib / SampleRate;

                double tone = Math.Sin(phase) + a2 * Math.Sin(2.0 * phase) + a3 * Math.Sin(3.0 * phase);

                double attack = Math.Min(1.0, t / 0.06);
                attack = attack * attack * (3.0 - 2.0 * attack);
                double body = Math.Exp(-1.2 * t / ring);
                double release = t < releaseStart ? 1.0 : Math.Exp(-9.0 * (t - releaseStart));

                // Breath puff at onset: low-passed noise that dies in ~80 ms.
                double raw = rng.NextDouble() * 2.0 - 1.0;
                lp += (raw - lp) * 0.35;
                double breath = lp * 0.22 * Math.Exp(-t / 0.05) * Math.Min(1.0, t / 0.01);

                buf[i] = (tone + breath * 1.0) * attack * body * release;
            }
        }

        static void RenderSoftPiano(double[] buf, double f, double ring, double lowBoost, System.Random rng)
        {
            double w = TwoPi * f / SampleRate;
            double rate = 3.6 / ring;
            double a2 = 0.35 * lowBoost;
            double a3 = 0.25 * lowBoost;
            double lp = 0.0;

            for (int i = 0; i < buf.Length; i++)
            {
                double t = i / (double)SampleRate;
                // Two-operator FM, ratio 1:1. Index decays 2.5 -> 0.3, so the note is bright then mellow.
                double index = 0.3 + 2.2 * Math.Exp(-t / 0.22);
                double ph = w * i;
                double v = Math.Sin(ph + index * Math.Sin(ph));
                if (lowBoost > 0.0)
                    v += a2 * Math.Sin(2.0 * ph) * Math.Exp(-1.5 * rate * t) + a3 * Math.Sin(3.0 * ph) * Math.Exp(-2.0 * rate * t);

                // Fast fall then a slower tail, like a struck string.
                double env = 0.65 * Math.Exp(-rate * 1.8 * t) + 0.35 * Math.Exp(-rate * 0.5 * t);

                // Hammer thump: 12 ms of low-passed noise.
                double raw = rng.NextDouble() * 2.0 - 1.0;
                lp += (raw - lp) * 0.2;
                double hammer = lp * 0.12 * Math.Exp(-t / 0.006);

                buf[i] = v * env + hammer;
            }

            SoftAttack(buf, 0.003);
        }

        static void RenderCelesta(double[] buf, double f, double ring, double lowBoost)
        {
            double w = TwoPi * f / SampleRate;
            double rate = 4.5 / ring;
            double a2 = 0.15 + 1.0 * lowBoost;
            double a3 = 0.7 * lowBoost;
            for (int i = 0; i < buf.Length; i++)
            {
                double t = i / (double)SampleRate;
                double ph = w * i;
                double v = Math.Sin(ph) * Math.Exp(-rate * t)
                         + a2 * Math.Sin(2.0 * ph) * Math.Exp(-rate * 1.6 * t)
                         + a3 * Math.Sin(3.0 * ph) * Math.Exp(-rate * 1.8 * t)
                         + 0.55 * Math.Sin(4.0 * ph) * Math.Exp(-rate * 2.4 * t);
                buf[i] = v;
            }

            SoftAttack(buf, 0.002);
        }

        static void RenderHarp(double[] buf, double f, double ring, System.Random rng)
        {
            // Karplus-Strong: a noise burst circulating in a delay line with a lowpass in the loop.
            // A first-order allpass tunes the loop to the exact pitch.
            double d = SampleRate / f - 0.5; // the 2-point averager adds half a sample of delay
            int nInt = (int)Math.Floor(d);
            double frac = d - nInt;
            if (frac < 0.1) { nInt -= 1; frac += 1.0; }
            nInt = Math.Max(2, nInt);
            double a = (1.0 - frac) / (1.0 + frac);

            var line = new double[nInt];
            double lp = 0.0;
            for (int i = 0; i < nInt; i++)
            {
                double raw = rng.NextDouble() * 2.0 - 1.0;
                lp += (raw - lp) * 0.35; // dulled pluck
                line[i] = lp;
            }

            // Loop gain sized so the string falls ~60 dB over ring seconds.
            double gain = Math.Pow(10.0, -3.0 / (ring * f));
            gain = Math.Min(gain, 0.9995);

            int idx = 0;
            double prev = 0.0, apX = 0.0, apY = 0.0;
            for (int i = 0; i < buf.Length; i++)
            {
                double x = line[idx];
                double avg = 0.5 * (x + prev) * gain;
                prev = x;
                double ap = a * avg + apX - a * apY;
                apX = avg;
                apY = ap;
                line[idx] = ap;
                idx++;
                if (idx >= nInt) idx = 0;
                buf[i] = x;
            }

            SoftAttack(buf, 0.003);
        }

        static void RenderMallet(double[] buf, double f, double ring, double lowBoost)
        {
            double w = TwoPi * f / SampleRate;
            double rate = Math.Max(6.0, 4.0 / ring);
            double a2 = 0.8 * lowBoost;
            double a3 = 0.6 * lowBoost;
            for (int i = 0; i < buf.Length; i++)
            {
                double t = i / (double)SampleRate;
                double ph = w * i;
                double v = Math.Sin(ph) * Math.Exp(-rate * t)
                         + a2 * Math.Sin(2.0 * ph) * Math.Exp(-rate * t)
                         + a3 * Math.Sin(3.0 * ph) * Math.Exp(-rate * 1.2 * t)
                         + 0.5 * Math.Sin(4.0 * ph) * Math.Exp(-rate * 2.5 * t);
                if (f * 10.0 < 16000.0)
                    v += 0.18 * Math.Sin(10.0 * ph) * Math.Exp(-rate * 5.0 * t);
                buf[i] = v;
            }

            SoftAttack(buf, 0.003);
        }

        // ---------------------------------------------------------------- shared finishing

        /// <summary>Raised-cosine ramp over the first few ms, so the note never starts mid-wave.</summary>
        static void SoftAttack(double[] buf, double seconds)
        {
            int ramp = Math.Max(1, (int)Math.Round(SampleRate * seconds));
            for (int i = 0; i < ramp && i < buf.Length; i++)
            {
                double x = i / (double)ramp;
                buf[i] *= 0.5 - 0.5 * Math.Cos(Math.PI * x);
            }
        }

        /// <summary>Remove DC, fade the tail to exactly 0, normalise to <see cref="TargetPeak"/>.</summary>
        static float[] Finish(double[] buf)
        {
            int n = buf.Length;

            double mean = 0.0;
            for (int i = 0; i < n; i++) mean += buf[i];
            mean /= n;

            int fade = Math.Min(n / 2, (int)Math.Round(SampleRate * FadeOutSeconds));
            double peak = 1e-9;
            for (int i = 0; i < n; i++)
            {
                double v = buf[i] - mean;
                int fromEnd = n - 1 - i;
                if (fromEnd < fade)
                {
                    double x = fromEnd / (double)fade;
                    v *= 0.5 - 0.5 * Math.Cos(Math.PI * x);
                }

                buf[i] = v;
                double av = Math.Abs(v);
                if (av > peak) peak = av;
            }

            // The first sample is 0 by the attack ramp; the mean removal could nudge it, so pin both ends.
            buf[0] = 0.0;
            buf[n - 1] = 0.0;

            var samples = new float[n];
            double scale = TargetPeak / peak;
            for (int i = 0; i < n; i++) samples[i] = (float)(buf[i] * scale);
            return samples;
        }
    }
}
