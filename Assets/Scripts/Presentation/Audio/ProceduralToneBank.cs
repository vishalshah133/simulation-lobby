using UnityEngine;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Builds bell-like tones in code, so a video needs no audio files.
    /// </summary>
    /// <remarks>
    /// Generated rather than sampled for one blunt reason: a synthesised sine wave has no author and
    /// no licence, so there is nothing to attribute and nothing to get a video flagged for.
    /// <para>
    /// Notes come from a <b>pentatonic</b> scale, where no two degrees are a semitone apart — any
    /// sequence of them is consonant, so a bounce melody cannot land on a sour note however the
    /// physics plays out. That is what makes this "soothing" rather than the timbre alone.
    /// </para>
    /// </remarks>
    public static class ProceduralToneBank
    {
        const int SampleRate = 44100;

        /// <summary>Major pentatonic, in semitones from the root.</summary>
        static readonly int[] PentatonicSteps = { 0, 2, 4, 7, 9 };

        /// <summary>
        /// Frequency for the n-th degree of the pentatonic scale above a root, wrapping into higher
        /// octaves as the index climbs.
        /// </summary>
        public static float DegreeToFrequency(float rootHz, int degree)
        {
            int octave = Mathf.FloorToInt(degree / (float)PentatonicSteps.Length);
            int step = degree - octave * PentatonicSteps.Length;
            int semitones = PentatonicSteps[step] + octave * 12;
            return rootHz * Mathf.Pow(2f, semitones / 12f);
        }

        /// <summary>
        /// A single struck tone: fast soft attack, long exponential decay — the shape of a marimba or
        /// glass bell rather than a beep.
        /// </summary>
        /// <param name="frequency">Fundamental in Hz.</param>
        /// <param name="duration">Length in seconds. Longer decays overlap into a pad at high bounce rates.</param>
        public static AudioClip CreateTone(float frequency, float duration = 1.1f, string name = "Tone")
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[sampleCount];

            // 6ms attack. Starting a sine at full amplitude produces a click at the discontinuity,
            // which is the single most common way generated audio sounds cheap.
            int attackSamples = Mathf.RoundToInt(SampleRate * 0.006f);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;

                // Harmonics thin out quickly, which is what makes a struck object sound struck.
                float value =
                    Mathf.Sin(2f * Mathf.PI * frequency * t) +
                    0.32f * Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * Mathf.Exp(-6f * t) +
                    0.12f * Mathf.Sin(2f * Mathf.PI * frequency * 3.01f * t) * Mathf.Exp(-11f * t);

                float decay = Mathf.Exp(-3.4f * t);
                float attack = attackSamples > 0 ? Mathf.Min(1f, i / (float)attackSamples) : 1f;

                samples[i] = value * decay * attack * 0.38f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// A full scale of tones, ready to step through. <paramref name="rootHz"/> 261.63 is middle C;
        /// lower roots read warmer and less shrill on phone speakers.
        /// </summary>
        public static AudioClip[] CreateScale(int noteCount, float rootHz = 261.63f, float duration = 1.1f)
        {
            var clips = new AudioClip[Mathf.Max(1, noteCount)];
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = CreateTone(DegreeToFrequency(rootHz, i), duration, $"Tone_{i:D2}");
            }

            return clips;
        }

        /// <summary>
        /// A single percussive knock: a low sine "body" plus a short filtered-noise "crack", both
        /// gone within a fifth of a second. The ASMR texture for a solid-object collision, the way
        /// <see cref="CreateTone"/> is the texture for a bounce.
        /// </summary>
        /// <param name="seed">
        /// Drives the noise component only, deterministically — no two thuds in a bank sound
        /// identical, but the same seed always renders the same clip (re-renders must match).
        /// </param>
        public static AudioClip CreateImpactThud(float pitch = 1f, float duration = 0.18f, int seed = 1, string name = "Thud")
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[sampleCount];
            var noiseSource = new System.Random(seed);

            float bodyHz = 55f * pitch;

            // A knock made only of a 55 Hz body is inaudible on the speakers this content is watched
            // on — phone speakers roll off hard below ~200 Hz, so the hit lands as silence with a
            // faint hiss. The mid "click" partial is what makes it read as a hit at all; keep it.
            float clickHz = 340f * pitch;
            int attackSamples = Mathf.RoundToInt(SampleRate * 0.002f); // near-instant: a knock, not a swell

            // One-pole low-pass on white noise so the "crack" reads as a solid knock rather than static.
            float filteredNoise = 0f;
            const float noiseCutoff = 0.15f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;

                float body = Mathf.Sin(2f * Mathf.PI * bodyHz * t) * Mathf.Exp(-18f * t);
                float click = Mathf.Sin(2f * Mathf.PI * clickHz * t) * Mathf.Exp(-45f * t);

                float rawNoise = (float)(noiseSource.NextDouble() * 2.0 - 1.0);
                filteredNoise += (rawNoise - filteredNoise) * noiseCutoff;
                float crack = filteredNoise * Mathf.Exp(-35f * t);

                float attack = attackSamples > 0 ? Mathf.Min(1f, i / (float)attackSamples) : 1f;
                samples[i] = (body * 0.45f + click * 0.4f + crack * 0.45f) * attack;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// A bank of thuds sharing a pitch but each with independent noise, for round-robin playback
        /// that never repeats the exact same sample twice in a row.
        /// </summary>
        public static AudioClip[] CreateThudBank(int count, float pitch = 1f, float duration = 0.18f, int seedBase = 1000)
        {
            var clips = new AudioClip[Mathf.Max(1, count)];
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = CreateImpactThud(pitch, duration, seedBase + i, $"Thud_{i:D2}");
            }

            return clips;
        }

        /// <summary>
        /// A rising tension cue: a slow upward pitch sweep under a swelling noise wash. Played during
        /// a wave's anticipation hold so the quiet stretch before an impact is *building* rather than
        /// merely empty — the single cheapest way a multi-wave format gets drama instead of repetition.
        /// </summary>
        /// <param name="duration">Should match the hold beat it underscores, so it peaks on the launch.</param>
        /// <param name="startHz">Fundamental at the start of the sweep.</param>
        /// <param name="endHz">Fundamental at the moment of launch. Higher = more anxious.</param>
        public static AudioClip CreateRiser(float duration = 1.4f, float startHz = 70f, float endHz = 240f,
            int seed = 7, string name = "Riser")
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[sampleCount];
            var noiseSource = new System.Random(seed);

            float filteredNoise = 0f;
            const float noiseCutoff = 0.04f;

            // Phase is integrated rather than computed as sin(2*pi*f(t)*t): sweeping the frequency
            // inside the sine's argument that way makes the *phase* jump every sample and the sweep
            // reads as a warble instead of a glide.
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t01 = i / (float)sampleCount;

                // Exponential sweep — pitch perception is logarithmic, so a linear ramp sounds like
                // it stalls at the top.
                float frequency = Mathf.Lerp(startHz, endHz, t01 * t01);
                phase += 2f * Mathf.PI * frequency / SampleRate;

                float rawNoise = (float)(noiseSource.NextDouble() * 2.0 - 1.0);
                filteredNoise += (rawNoise - filteredNoise) * noiseCutoff;

                // Swells the whole way and stops dead at the end, so the launch lands in the gap.
                float envelope = Mathf.Pow(t01, 1.6f);

                samples[i] = (Mathf.Sin(phase) * 0.5f + filteredNoise * 0.5f) * envelope * 0.4f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// A seamless low ambient pad, meant to be looped under an entire run. Two detuned sines a
        /// fifth apart with a slow tremolo — the "room tone" that keeps a 90-second video from
        /// feeling like silence punctuated by noise.
        /// </summary>
        /// <remarks>
        /// Loops cleanly because every component's period divides <paramref name="duration"/> exactly:
        /// the frequencies are snapped to whole cycles per loop, so the last sample joins the first
        /// without the click an arbitrary tone would produce at the seam.
        /// </remarks>
        public static AudioClip CreateAmbientBed(float duration = 4f, float rootHz = 55f, string name = "AmbientBed")
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[sampleCount];

            float SnapToLoop(float hz)
            {
                return Mathf.Max(1f, Mathf.Round(hz * duration)) / duration;
            }

            float low = SnapToLoop(rootHz);
            float fifth = SnapToLoop(rootHz * 1.5f);
            float shimmer = SnapToLoop(rootHz * 4f);
            float tremolo = SnapToLoop(0.25f);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;

                float value =
                    Mathf.Sin(2f * Mathf.PI * low * t) * 0.5f +
                    Mathf.Sin(2f * Mathf.PI * fifth * t) * 0.28f +
                    Mathf.Sin(2f * Mathf.PI * shimmer * t) * 0.07f;

                float amplitude = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * tremolo * t);
                samples[i] = value * amplitude * 0.3f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// A seamless loop of low filtered noise — the sound of a field of debris still moving. Meant
        /// to be played continuously with its volume driven by how much is actually in motion, so a
        /// scatter is audible as it happens and fades out as the wall settles, rather than the run
        /// going silent the instant the last knock has decayed.
        /// </summary>
        /// <remarks>
        /// Loops without a seam because the noise is cross-faded with a copy of itself offset by half
        /// the clip: the join lands in the middle of the fade, where both halves are equal.
        /// </remarks>
        public static AudioClip CreateRumbleLoop(float duration = 2f, int seed = 11, string name = "Rumble")
        {
            int sampleCount = Mathf.Max(2, Mathf.RoundToInt(SampleRate * duration));
            var raw = new float[sampleCount];
            var noiseSource = new System.Random(seed);

            float filtered = 0f;
            const float cutoff = 0.08f;

            for (int i = 0; i < sampleCount; i++)
            {
                float rawNoise = (float)(noiseSource.NextDouble() * 2.0 - 1.0);
                filtered += (rawNoise - filtered) * cutoff;
                raw[i] = filtered;
            }

            var samples = new float[sampleCount];
            int half = sampleCount / 2;
            for (int i = 0; i < sampleCount; i++)
            {
                float fade = i / (float)sampleCount;
                samples[i] = Mathf.Lerp(raw[i], raw[(i + half) % sampleCount], fade) * 1.6f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// A soft rising-then-falling noise sweep — the "something is approaching" cue for a format
        /// with no melody of its own to lean on.
        /// </summary>
        public static AudioClip CreateWhoosh(float duration = 0.9f, int seed = 2, string name = "Whoosh")
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[sampleCount];
            var noiseSource = new System.Random(seed);

            float filteredNoise = 0f;
            const float noiseCutoff = 0.05f; // heavier filtering than a thud's crack: a wash, not a hiss

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float phase = t / duration; // 0..1 across the clip

                float rawNoise = (float)(noiseSource.NextDouble() * 2.0 - 1.0);
                filteredNoise += (rawNoise - filteredNoise) * noiseCutoff;

                // Triangular envelope: builds through the approach, peaks just before impact.
                float envelope = phase < 0.8f ? phase / 0.8f : (1f - phase) / 0.2f;
                samples[i] = filteredNoise * envelope * 0.45f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// A glass-on-metal "clink": a short bright tick, then a cluster of inharmonic partials ringing
        /// out like a struck wine glass, plus a low knock that gives it weight. The hard end of a
        /// material sound — what 0% soft sounds like.
        /// </summary>
        /// <remarks>
        /// The partial ratios are a free-free bar's (1 : 2.76 : 5.40 : 8.93), not integer harmonics —
        /// integer ratios read as a musical note, these read as an object. Each partial is doubled and
        /// detuned a few cents, and the slow beating between the pair is the shimmer real glass has.
        /// </remarks>
        /// <param name="pitch">Multiplier on a ~1.6 kHz fundamental.</param>
        public static AudioClip CreateGlassClink(float pitch = 1f, float duration = 1.6f, int seed = 3, string name = "GlassClink")
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[sampleCount];
            var noiseSource = new System.Random(seed);

            float fundamental = 1620f * pitch;
            float[] ratios = { 1f, 2.76f, 5.40f, 8.93f };
            float[] gains = { 1f, 0.55f, 0.28f, 0.14f };
            float[] decays = { 4.2f, 7f, 12f, 19f };
            const float detune = 0.0035f;
            float knockHz = 330f * pitch;
            int attackSamples = Mathf.RoundToInt(SampleRate * 0.0015f);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float ring = 0f;
                for (int p = 0; p < ratios.Length; p++)
                {
                    float f = fundamental * ratios[p];
                    float pair =
                        Mathf.Sin(2f * Mathf.PI * f * (1f - detune) * t) +
                        Mathf.Sin(2f * Mathf.PI * f * (1f + detune) * t);
                    ring += pair * 0.5f * gains[p] * Mathf.Exp(-decays[p] * t);
                }

                float knock = Mathf.Sin(2f * Mathf.PI * knockHz * t) * Mathf.Exp(-38f * t);
                float tick = (float)(noiseSource.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-900f * t);
                float attack = attackSamples > 0 ? Mathf.Min(1f, i / (float)attackSamples) : 1f;
                samples[i] = (ring * 0.34f + knock * 0.4f + tick * 0.5f) * attack;
            }

            Normalize(samples, 0.85f);
            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Glass breaking: a sharp high-passed noise crack, then a scatter of short inharmonic pings
        /// (fragments landing) thinning out over the tail. Sits above a melody rather than under it,
        /// so it reads on phone speakers without masking the notes.
        /// </summary>
        /// <param name="size">0..1. Bigger = longer tail and more fragments (a whole ring vs one segment).</param>
        /// <param name="seed">Noise and fragment placement. Same seed, same clip — re-renders must match.</param>
        public static AudioClip CreateShatter(float size = 1f, int seed = 9, string name = "Shatter")
        {
            size = Mathf.Clamp01(size);
            float duration = Mathf.Lerp(0.25f, 0.9f, size);
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[sampleCount];
            var random = new System.Random(seed);

            // The crack: white noise minus its own low-passed copy (a cheap high-pass), decaying fast.
            float low = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (noise - low) * 0.2f;
                samples[i] = (noise - low) * Mathf.Exp(-28f * t) * 0.8f;
            }

            // Fragments: short bright pings at random inharmonic pitches, denser near the start.
            int fragments = Mathf.RoundToInt(Mathf.Lerp(5f, 22f, size));
            for (int f = 0; f < fragments; f++)
            {
                double u = random.NextDouble();
                int start = Mathf.RoundToInt((float)(u * u) * (sampleCount - 1) * 0.85f);
                float hz = 2200f + (float)random.NextDouble() * 4200f;
                float gain = 0.12f + (float)random.NextDouble() * 0.18f;
                int length = Mathf.Min(sampleCount - start, Mathf.RoundToInt(SampleRate * 0.06f));
                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)SampleRate;
                    samples[start + i] += Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-70f * t) * gain;
                }
            }

            float peak = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }

            if (peak > 0.95f)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    samples[i] *= 0.95f / peak;
                }
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// A wet "squelch": a low body thump, a burst of noise through a resonant band-pass sweeping
        /// down (the slap of something soft deforming), and a scatter of tiny rising chirps — the
        /// classic procedural recipe for a bubble, and what makes it read as <i>wet</i> rather than
        /// merely soft. The soft end of a material sound — what 100% sounds like.
        /// </summary>
        /// <param name="softness">0..1. Softer = longer, lower, more bubbles.</param>
        /// <param name="seed">Noise and bubble placement. Same seed, same clip — re-renders must match.</param>
        public static AudioClip CreateSquelch(float softness = 1f, int seed = 5, string name = "Squelch")
        {
            softness = Mathf.Clamp01(softness);
            float duration = Mathf.Lerp(0.32f, 0.95f, softness);
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[sampleCount];
            var noiseSource = new System.Random(seed);

            // --- Body thump: a short downward pitch glide. The 2nd partial keeps it audible on a phone.
            float thumpPhase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float hz = Mathf.Lerp(150f, 62f, Mathf.Clamp01(t / 0.12f));
                thumpPhase += 2f * Mathf.PI * hz / SampleRate;
                float envelope = Mathf.Exp(-Mathf.Lerp(26f, 13f, softness) * t) * Mathf.Min(1f, t / 0.003f);
                samples[i] += (Mathf.Sin(thumpPhase) + 0.45f * Mathf.Sin(2f * thumpPhase)) * envelope * 0.55f;
            }

            // --- Wet slap: noise through a state-variable band-pass whose centre falls exponentially.
            float low = 0f;
            float band = 0f;
            float sweepSeconds = duration * 0.7f;
            const float q = 0.22f; // damping; lower = more resonant, more "vocal"
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float centre = 1500f * Mathf.Pow(230f / 1500f, Mathf.Clamp01(t / sweepSeconds));
                float f = 2f * Mathf.Sin(Mathf.PI * centre / SampleRate);
                float input = (float)(noiseSource.NextDouble() * 2.0 - 1.0);
                low += f * band;
                float high = input - low - q * band;
                band += f * high;
                float envelope = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-Mathf.Lerp(14f, 6f, softness) * t);
                samples[i] += band * envelope * 0.5f;
            }

            // --- Bubbles: short sine chirps rising in pitch, each with a smooth hump envelope.
            int bubbleCount = 2 + Mathf.RoundToInt(softness * 6f);
            for (int b = 0; b < bubbleCount; b++)
            {
                float start = Mathf.Lerp(0.02f, duration * 0.55f, (float)noiseSource.NextDouble());
                float length = Mathf.Lerp(0.018f, 0.045f, (float)noiseSource.NextDouble());
                float baseHz = Mathf.Lerp(260f, 720f, (float)noiseSource.NextDouble());
                float gain = Mathf.Lerp(0.12f, 0.3f, (float)noiseSource.NextDouble()) * Mathf.Exp(-start * 2.5f);
                int first = Mathf.RoundToInt(start * SampleRate);
                int count = Mathf.RoundToInt(length * SampleRate);
                float phase = 0f;
                for (int k = 0; k < count && first + k < sampleCount; k++)
                {
                    float u = k / (float)count;
                    float hz = baseHz * (1f + 0.9f * u * u);
                    phase += 2f * Mathf.PI * hz / SampleRate;
                    samples[first + k] += Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * u) * gain;
                }
            }

            // Fade the last 20ms so a long tail never ends on a click.
            int fade = Mathf.Min(sampleCount, Mathf.RoundToInt(SampleRate * 0.02f));
            for (int k = 0; k < fade; k++)
            {
                samples[sampleCount - 1 - k] *= k / (float)fade;
            }

            Normalize(samples, 0.85f);
            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Scales a buffer so its loudest sample sits at <paramref name="peak"/>.</summary>
        static void Normalize(float[] samples, float peak)
        {
            float max = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(samples[i]));
            }

            if (max < 1e-6f)
            {
                return;
            }

            float scale = peak / max;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= scale;
            }
        }
    }
}
