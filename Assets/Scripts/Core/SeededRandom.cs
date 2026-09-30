using System;
using UnityEngine;

// Aliased deliberately: with UnityEngine in scope, bare `Random` is ambiguous, and the ambiguity is
// exactly the mistake this class exists to prevent. Spelling it out keeps the intent unmissable.
using SystemRandom = System.Random;

namespace SimulationLobby.Core
{
    /// <summary>
    /// The only legal source of randomness in simulation code. Wraps <see cref="System.Random"/>
    /// so a run is reproducible from its seed.
    /// </summary>
    /// <remarks>
    /// Thread this instance explicitly — never hold it in static state, and never call
    /// <see cref="UnityEngine.Random"/> from simulation code. Both break reproducibility.
    /// <para>
    /// <see cref="DrawCount"/> is the determinism tripwire: two runs of the same seed that consumed
    /// a different number of draws have diverged, even if their visible outcome happens to match.
    /// </para>
    /// </remarks>
    public sealed class SeededRandom
    {
        readonly SystemRandom _random;

        /// <summary>Seed this generator was constructed with. Part of a run's identity.</summary>
        public int Seed { get; }

        /// <summary>How many values have been drawn. Compared across runs to detect divergence.</summary>
        public int DrawCount { get; private set; }

        public SeededRandom(int seed)
        {
            Seed = seed;
            _random = new SystemRandom(seed);
        }

        /// <summary>Uniform float in [0,1).</summary>
        public float NextFloat()
        {
            DrawCount++;
            return (float)_random.NextDouble();
        }

        /// <summary>Uniform float in [min,max).</summary>
        public float NextFloat(float min, float max)
        {
            return min + NextFloat() * (max - min);
        }

        /// <summary>Uniform int in [min,max) — max exclusive, matching System.Random.</summary>
        public int NextInt(int min, int max)
        {
            DrawCount++;
            return _random.Next(min, max);
        }

        /// <summary>Uniform int in [0,max).</summary>
        public int NextInt(int max)
        {
            DrawCount++;
            return _random.Next(max);
        }

        public bool NextBool()
        {
            return NextFloat() < 0.5f;
        }

        /// <summary>True with the given probability (0 = never, 1 = always).</summary>
        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        /// <summary>Unit vector, uniformly distributed around the circle.</summary>
        public Vector2 NextDirection2D()
        {
            float angle = NextFloat(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>Unit vector, uniformly distributed on the sphere.</summary>
        public Vector3 NextDirection3D()
        {
            // z uniform in [-1,1] then a uniform azimuth gives an even spherical distribution;
            // picking three uniform components and normalising would clump toward the corners.
            float z = NextFloat(-1f, 1f);
            float azimuth = NextFloat(0f, Mathf.PI * 2f);
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(ring * Mathf.Cos(azimuth), ring * Mathf.Sin(azimuth), z);
        }

        /// <summary>Random element of a list. Throws on an empty list rather than returning default.</summary>
        public T Pick<T>(System.Collections.Generic.IList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                throw new ArgumentException("Cannot pick from an empty collection.", nameof(items));
            }

            return items[NextInt(items.Count)];
        }

        /// <summary>
        /// Fisher-Yates shuffle in place. Deterministic for a given seed and draw position —
        /// use this rather than sorting by a random key, which depends on the sort implementation.
        /// </summary>
        public void Shuffle<T>(System.Collections.Generic.IList<T> items)
        {
            if (items == null)
            {
                return;
            }

            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        /// <summary>
        /// A derived generator for an independent stream (per-contender jitter, per-hazard timing).
        /// Keeps one subsystem's draw count from shifting another's sequence.
        /// </summary>
        public SeededRandom Branch(int salt)
        {
            unchecked
            {
                return new SeededRandom(Seed * 397 + salt);
            }
        }
    }
}
