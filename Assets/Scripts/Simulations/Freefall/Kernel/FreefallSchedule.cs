using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Simulations.Freefall.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall
{
    /// <summary>One bounce: a pad, a note, a moment. The schedule is a sorted list of these.</summary>
    public readonly struct FreefallHit
    {
        /// <summary>Seconds from run start.</summary>
        public readonly float time;

        /// <summary>0 = the hero marble; 1.. = canon voices.</summary>
        public readonly int voice;

        public readonly int world;

        /// <summary>Index into the world's melody.</summary>
        public readonly int noteIndex;

        /// <summary><c>melody.FrequencyAt(noteIndex)</c>.</summary>
        public readonly float frequencyHz;

        /// <summary>How long the note should ring: beats times seconds-per-beat at that moment, clamped 0.25..3.</summary>
        public readonly float ringSeconds;

        /// <summary>0 = lowest note in that world's melody, 1 = highest. Drives radius and hue.</summary>
        public readonly float pitch01;

        /// <summary>Contact point (marble centre at contact).</summary>
        public readonly Vector3 position;

        /// <summary>Pad normal = normalize(vOut - vIn).</summary>
        public readonly Vector3 normal;

        /// <summary>|vOut - vIn|, for volume.</summary>
        public readonly float impulse;

        /// <summary>Last hit of the world's melody (voice 0's final note).</summary>
        public readonly bool isWorldFinal;

        /// <summary>The core hit that ends the run.</summary>
        public readonly bool isRunFinal;

        public FreefallHit(
            float time, int voice, int world, int noteIndex, float frequencyHz, float ringSeconds, float pitch01,
            Vector3 position, Vector3 normal, float impulse, bool isWorldFinal, bool isRunFinal)
        {
            this.time = time;
            this.voice = voice;
            this.world = world;
            this.noteIndex = noteIndex;
            this.frequencyHz = frequencyHz;
            this.ringSeconds = ringSeconds;
            this.pitch01 = pitch01;
            this.position = position;
            this.normal = normal;
            this.impulse = impulse;
            this.isWorldFinal = isWorldFinal;
            this.isRunFinal = isRunFinal;
        }
    }

    /// <summary>Where a world sits in the run, in time and height.</summary>
    public readonly struct FreefallWorldSpan
    {
        public readonly int index;

        /// <summary>The previous world's final hit (0 for the first): when the fall into this world begins.</summary>
        public readonly float startTime;

        public readonly float firstHitTime;
        public readonly float lastHitTime;

        /// <summary>Start of the next world's first fall (the run's end for the last world).</summary>
        public readonly float endTime;

        /// <summary>Highest point any marble reaches in the world, canon entry points included.</summary>
        public readonly float yTop;

        /// <summary>Height of voice 0's last pad.</summary>
        public readonly float yBottom;

        /// <summary>Index into <see cref="FreefallSchedule.Hits"/> of the world's first hit.</summary>
        public readonly int firstHitIndex;

        /// <summary>Hits in the world, canon voices included. The last world's count includes the core hit.</summary>
        public readonly int hitCount;

        /// <summary>Marbles falling in this world (canonVoices, minus any that never get a note).</summary>
        public readonly int voiceCount;

        public FreefallWorldSpan(
            int index, float startTime, float firstHitTime, float lastHitTime, float endTime,
            float yTop, float yBottom, int firstHitIndex, int hitCount, int voiceCount)
        {
            this.index = index;
            this.startTime = startTime;
            this.firstHitTime = firstHitTime;
            this.lastHitTime = lastHitTime;
            this.endTime = endTime;
            this.yTop = yTop;
            this.yBottom = yBottom;
            this.firstHitIndex = firstHitIndex;
            this.hitCount = hitCount;
            this.voiceCount = voiceCount;
        }
    }

    /// <summary>
    /// The whole run as data: every bounce of every marble, computed once and sampled by time. Pure maths, no
    /// MonoBehaviour. There is no physics: the marble flies analytic ballistic arcs between pads placed exactly
    /// where it will be when the next note is due, so the rhythm is the composer's, not the engine's.
    /// </summary>
    /// <remarks>
    /// Determinism: the only random draws are cosmetic (winding start and direction, radius jitter) and are made
    /// in a fixed order from the run's <see cref="SeededRandom"/>. Note times, heights and tempo never depend on
    /// the seed, so two seeds produce the same hit times and differ only in where the pads sit.
    /// </remarks>
    public sealed class FreefallSchedule
    {
        /// <summary>Retries per world when a pad clips the marble's path.</summary>
        const int MaxRetries = 8;

        /// <summary>Winding rate added per retry (degrees per second).</summary>
        const float RetryStepDegreesPerSecond = 7f;

        /// <summary>Required gap between the marble's surface and any pad that is not the one it is hitting.</summary>
        const float RequiredClearance = 0.005f;

        readonly List<FreefallHit> _hits = new List<FreefallHit>();
        readonly List<FreefallWorldSpan> _worlds = new List<FreefallWorldSpan>();
        readonly List<float> _clearance = new List<float>();
        readonly List<int> _retries = new List<int>();
        readonly List<string> _warnings = new List<string>();
        List<FreefallSeg>[] _voiceSegs;

        FreefallSchedule()
        {
        }

        public IReadOnlyList<FreefallHit> Hits => _hits;

        public IReadOnlyList<FreefallWorldSpan> Worlds => _worlds;

        public int MaxVoices { get; private set; }

        /// <summary>Last hit (the core) plus <c>outroSeconds</c>.</summary>
        public float Duration { get; private set; }

        /// <summary>The final hit's target, below the last world.</summary>
        public Vector3 CoreCentre { get; private set; }

        // --- Additions beyond the contract (read-only diagnostics) ---

        /// <summary>When the marble lands on the core.</summary>
        public float RunFinalHitTime { get; private set; }

        /// <summary>Per world: smallest gap between the marble's surface and any other pad over the whole world.</summary>
        public IReadOnlyList<float> WorldMinClearance => _clearance;

        /// <summary>Per world: how many retries the validation needed (0 = first layout was clean).</summary>
        public IReadOnlyList<int> WorldRetries => _retries;

        /// <summary>One line per world that still clipped after the last retry (also logged).</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        public static FreefallSchedule Build(FreefallConfig config, SeededRandom rng)
        {
            var schedule = new FreefallSchedule();
            schedule.BuildAll(config, rng);
            return schedule;
        }

        /// <summary>Index of the world the run is in at <paramref name="time"/> (the fall into a world counts as that world).</summary>
        public int WorldAt(float time)
        {
            int lo = 0;
            int hi = _worlds.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (_worlds[mid].startTime <= time)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return lo;
        }

        /// <summary>
        /// Marble position and velocity at <paramref name="time"/>. False when that voice does not exist then:
        /// before it enters, after it merges into voice 0, or (voice 0) after the core hit.
        /// </summary>
        public bool TrySample(int voice, float time, out Vector3 position, out Vector3 velocity)
        {
            position = Vector3.zero;
            velocity = Vector3.zero;
            if (voice < 0 || _voiceSegs == null || voice >= _voiceSegs.Length)
            {
                return false;
            }

            List<FreefallSeg> segs = _voiceSegs[voice];
            if (segs.Count == 0 || time < segs[0].t0)
            {
                return false;
            }

            int lo = 0;
            int hi = segs.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (segs[mid].t0 <= time)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            FreefallSeg seg = segs[lo];
            if (time > seg.t1)
            {
                return false;
            }

            position = seg.PositionAt(time);
            velocity = seg.VelocityAt(time);
            return true;
        }

        void BuildAll(FreefallConfig config, SeededRandom rng)
        {
            int worldCount = config.worlds.Count;

            var layout = new FreefallLayout { config = config, plans = new FreefallWorldPlan[worldCount] };
            for (int w = 0; w < worldCount; w++)
            {
                layout.plans[w] = new FreefallWorldPlan(config.worlds[w], rng);
            }

            FreefallWorldBuilder.PlanLayout(layout);
            MaxVoices = layout.maxVoices;
            CoreCentre = layout.core;
            RunFinalHitTime = (float)layout.coreTime;
            Duration = (float)layout.coreTime + config.outroSeconds;

            _voiceSegs = new List<FreefallSeg>[MaxVoices];
            for (int k = 0; k < MaxVoices; k++)
            {
                _voiceSegs[k] = new List<FreefallSeg>();
            }

            var topBottom = new Vector2[worldCount];
            Vector3 incoming = new Vector3(0f, -config.worlds[0].gravity * config.introDropSeconds, 0f);

            for (int w = 0; w < worldCount; w++)
            {
                FreefallWorldBuild best = null;
                int bestAttempt = 0;
                int attempts = 0;
                for (int attempt = 0; attempt <= MaxRetries; attempt++)
                {
                    FreefallWorldBuild build = FreefallWorldBuilder.Build(
                        layout, w, attempt * RetryStepDegreesPerSecond, incoming);
                    attempts = attempt;
                    if (best == null || build.clearance > best.clearance)
                    {
                        best = build;
                        bestAttempt = attempt;
                    }

                    if (build.clearance >= RequiredClearance)
                    {
                        break;
                    }
                }

                if (best.clearance < RequiredClearance)
                {
                    FreefallHit bad = best.worstHit >= 0 ? best.hits[best.worstHit] : default;
                    string warning =
                        $"[fall] world {w + 1} ({config.worlds[w].worldName}): pad still within {best.clearance:0.0000} m of a " +
                        $"marble path after {attempts} retries (worst hit: voice {bad.voice} note {bad.noteIndex} at {bad.time:0.00}s). " +
                        "Raise degreesPerSecond, dropFraction or padRadius spacing for this world.";
                    _warnings.Add(warning);
                    Debug.LogWarning(warning);
                }

                _clearance.Add(best.clearance);
                _retries.Add(bestAttempt);
                _hits.AddRange(best.hits);
                for (int k = 0; k < best.segs.Length; k++)
                {
                    _voiceSegs[k].AddRange(best.segs[k]);
                }

                if (w + 1 < worldCount)
                {
                    layout.firstHit[w + 1] = best.nextFirstHit;
                }

                topBottom[w] = new Vector2(best.yTop, best.yBottom);
                incoming = best.arrivalVelocity;
            }

            _hits.Sort(CompareHits);

            // Spans: derived from the sorted hit list so firstHitIndex/hitCount are exact.
            for (int w = 0; w < worldCount; w++)
            {
                int first = -1;
                int hitCount = 0;
                float lastTime = 0f;
                for (int i = 0; i < _hits.Count; i++)
                {
                    FreefallHit h = _hits[i];
                    if (h.world != w)
                    {
                        continue;
                    }

                    if (first < 0)
                    {
                        first = i;
                    }

                    hitCount++;
                    if (h.isWorldFinal)
                    {
                        lastTime = h.time;
                    }
                }

                float start = w == 0 ? 0f : _worlds[w - 1].lastHitTime;
                float firstTime = _hits[first].time;
                _worlds.Add(new FreefallWorldSpan(
                    w, start, firstTime, lastTime, 0f, topBottom[w].x, topBottom[w].y, first, hitCount,
                    layout.plans[w].VoiceCount));
            }

            // endTime needs the next world's startTime, so patch it in a second pass.
            for (int w = 0; w < worldCount; w++)
            {
                FreefallWorldSpan s = _worlds[w];
                float end = w + 1 < worldCount ? _worlds[w + 1].startTime : Duration;
                _worlds[w] = new FreefallWorldSpan(
                    s.index, s.startTime, s.firstHitTime, s.lastHitTime, end, s.yTop, s.yBottom,
                    s.firstHitIndex, s.hitCount, s.voiceCount);
            }
        }

        static int CompareHits(FreefallHit a, FreefallHit b)
        {
            int c = a.time.CompareTo(b.time);
            return c != 0 ? c : a.voice.CompareTo(b.voice);
        }
    }
}
