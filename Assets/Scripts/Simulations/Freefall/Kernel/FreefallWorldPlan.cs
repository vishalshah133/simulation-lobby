using System;
using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall.Kernel
{
    /// <summary>
    /// Everything about a world that does not depend on where the pads end up: the note timeline (tempo ramp in
    /// beats), pitch-to-radius mapping, the seeded cosmetic draws, and the height each note drops. The retry loop
    /// in the builder only re-lays the winding angles, so timing and heights are identical on every attempt.
    /// </summary>
    internal sealed class FreefallWorldPlan
    {
        public const float JitterFraction = 0.04f;

        public readonly FreefallWorld world;

        /// <summary>Notes in the run: melody length times passes.</summary>
        public readonly int count;

        /// <summary>Index into the melody for hit j.</summary>
        public readonly int[] noteIndex;

        public readonly float[] pitch01;
        public readonly float[] ringSeconds;

        /// <summary>Seconds from the world's first hit to hit j (voice 0).</summary>
        public readonly double[] tRel;

        /// <summary>Height of hit j relative to the first hit (zero then falling).</summary>
        public readonly float[] relY;

        /// <summary>Distance of hit j from the axis, jitter included.</summary>
        public readonly float[] radius;

        public readonly double theta0;
        public readonly int dir;

        /// <summary>Per canon voice: time shift in seconds, and how many hits it gets (0 = voice never appears).</summary>
        public readonly double[] voiceShift;
        public readonly int[] voiceHitCount;

        public int VoiceCount { get; }

        public double LastTRel => tRel[count - 1];

        readonly double _bpm0;
        readonly double _slope;
        readonly double _totalBeats;

        public FreefallWorldPlan(FreefallWorld world, SeededRandom rng)
        {
            this.world = world;
            int n = world.melody.Count;
            count = n * Math.Max(1, world.passes);

            noteIndex = new int[count];
            pitch01 = new float[count];
            ringSeconds = new float[count];
            tRel = new double[count];
            relY = new float[count];
            radius = new float[count];

            var beatStart = new double[count];
            var beats = new double[count];
            double sum = 0.0;
            int minSemi = int.MaxValue;
            int maxSemi = int.MinValue;
            for (int i = 0; i < n; i++)
            {
                minSemi = Math.Min(minSemi, world.melody.notes[i].semitone);
                maxSemi = Math.Max(maxSemi, world.melody.notes[i].semitone);
            }

            for (int j = 0; j < count; j++)
            {
                noteIndex[j] = j % n;
                beats[j] = Math.Max(0.01, world.melody.notes[noteIndex[j]].beats);
                beatStart[j] = sum;
                sum += beats[j];
            }

            _totalBeats = sum;
            _bpm0 = Math.Max(10.0, world.bpm);
            _slope = (Math.Max(10.0, world.bpmEnd) - _bpm0) / _totalBeats;

            // Seeded draws: start angle, winding direction, then one jitter per hit. Fixed order, so a given seed
            // always draws the same numbers no matter how many retries the builder needs.
            theta0 = rng.NextFloat(0f, Mathf.PI * 2f);
            dir = rng.NextBool() ? 1 : -1;

            float semiRange = maxSemi - minSemi;
            float g = world.gravity;
            for (int j = 0; j < count; j++)
            {
                int semi = world.melody.notes[noteIndex[j]].semitone;
                pitch01[j] = semiRange > 0f ? (semi - minSemi) / semiRange : 0.5f;
                tRel[j] = TimeAtBeat(beatStart[j]);
                double spb = 60.0 / BpmAtBeat(beatStart[j]);
                ringSeconds[j] = Mathf.Clamp((float)(beats[j] * spb), 0.25f, 3f);

                float jitter = rng.NextFloat(-JitterFraction, JitterFraction);
                float frac = Mathf.Lerp(world.innerRadiusFraction, world.outerRadiusFraction, pitch01[j]);
                radius[j] = frac * world.wellRadius * (1f + jitter);
            }

            relY[0] = 0f;
            for (int j = 1; j < count; j++)
            {
                float dt = (float)(tRel[j] - tRel[j - 1]);
                float drop = Mathf.Min(world.dropFraction * 0.5f * g * dt * dt, world.maxDropPerNote);
                relY[j] = relY[j - 1] - drop;
            }

            // Canon voices: voice k replays voice 0's path k * canonDelayBeats later and stops at voice 0's last note.
            int voices = Math.Max(1, world.canonVoices);
            voiceShift = new double[voices];
            voiceHitCount = new int[voices];
            int present = 0;
            for (int k = 0; k < voices; k++)
            {
                voiceShift[k] = k == 0 ? 0.0 : TimeAtBeat(k * (double)world.canonDelayBeats);
                int hits = 0;
                for (int j = 0; j < count; j++)
                {
                    if (tRel[j] + voiceShift[k] <= LastTRel + 1e-6)
                    {
                        hits++;
                    }
                }

                voiceHitCount[k] = hits;
                if (hits > 0)
                {
                    present = k + 1;
                }
            }

            VoiceCount = present;
        }

        /// <summary>Quarter-note bpm after <paramref name="beat"/> beats; linear in beats.</summary>
        public double BpmAtBeat(double beat)
        {
            return Math.Max(10.0, _bpm0 + _slope * beat);
        }

        /// <summary>Seconds elapsed after <paramref name="beat"/> beats: the integral of 60 / bpm(b) db.</summary>
        public double TimeAtBeat(double beat)
        {
            if (Math.Abs(_slope) < 1e-9)
            {
                return 60.0 * beat / _bpm0;
            }

            double arg = Math.Max(1e-6, 1.0 + _slope * beat / _bpm0);
            return 60.0 / _slope * Math.Log(arg);
        }
    }
}
