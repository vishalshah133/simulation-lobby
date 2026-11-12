using System;
using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.ReleaseOrMultiply
{
    /// <summary>
    /// Starts the run after a short still beat, so the segment card fades and the viewer sees the empty
    /// funnel before the single marble drops. The runner's <c>autoStart</c> is off for this scene.
    /// </summary>
    /// <remarks>
    /// Only the moment of <c>Begin</c> depends on wall-clock time; the run itself is untouched, so the seed
    /// still reproduces it exactly. Seed handling matches the runner's: random per Play and logged, or fixed.
    /// </remarks>
    public sealed class RomWideStarter : MonoBehaviour
    {
        public SimulationRunner runner;
        public RomWideSettings settings;
        public bool randomizeSeed = true;
        public int seed = 1;

        bool _started;

        /// <summary>Seconds since the scene began, for the card/explainer timeline (presentation clock).</summary>
        public float Elapsed { get; private set; }

        void Update()
        {
            Elapsed += Time.deltaTime;
            if (_started || runner == null)
            {
                return;
            }

            float delay = settings != null ? settings.startDelaySeconds : 0f;
            if (Elapsed < delay)
            {
                return;
            }

            _started = true;
            int runSeed = randomizeSeed ? Guid.NewGuid().GetHashCode() : seed;
            if (randomizeSeed)
            {
                Debug.Log($"[rom-wide] Random seed {runSeed}. To re-render this exact take: set Seed on " +
                          "RomWideStarter to this value and untick Randomize Seed.", this);
            }

            runner.Begin(runSeed);
        }
    }
}
