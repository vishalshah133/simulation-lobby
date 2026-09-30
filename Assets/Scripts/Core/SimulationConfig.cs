using UnityEngine;

namespace SimulationLobby.Core
{
    /// <summary>
    /// Base config type. Holds only what is universal to every format — a published video is
    /// <c>scene + config asset + seed</c>, so anything tunable belongs in a derived config asset
    /// rather than an inline constant.
    /// </summary>
    /// <remarks>
    /// Not creatable on its own: each format defines its own <c>[CreateAssetMenu]</c> subclass.
    /// Changing <see cref="fixedTimestep"/> on an existing asset invalidates every seed previously
    /// scanned with it — treat it as a new config instead.
    /// </remarks>
    public abstract class SimulationConfig : ScriptableObject
    {
        [Header("Determinism")]
        [Tooltip("Simulation step length in seconds. 1/60 is the default; smaller is more stable " +
                 "for fast collisions and more expensive. Changing this invalidates existing seeds.")]
        [Range(1f / 240f, 1f / 30f)]
        public float fixedTimestep = 1f / 60f;

        [Tooltip("Hard stop in simulated seconds. A run that hits this timed out and is not " +
                 "publishable — it means the config needs tuning, not that the seed was unlucky.")]
        [Min(1f)]
        public float maxDurationSeconds = 120f;

        [Header("Identity")]
        [Tooltip("Optional note for the retro: what this config was trying to achieve.")]
        [TextArea(2, 4)]
        public string intent;

        /// <summary>
        /// Max duration expressed in ticks — what the runner actually counts against. Virtual because
        /// a format whose length is structural (a series of N attempts) must derive its own cap rather
        /// than depend on an author keeping a second field in sync with it.
        /// </summary>
        public virtual int MaxTicks => Mathf.Max(1, Mathf.CeilToInt(maxDurationSeconds / fixedTimestep));

        /// <summary>
        /// Called by the runner before a run starts. Override to catch a config that cannot produce
        /// a watchable run (zero contenders, impossible thresholds) and return false with a reason —
        /// cheaper to fail here than to discover it in a 200-seed scan.
        /// </summary>
        public virtual bool Validate(out string error)
        {
            if (fixedTimestep <= 0f)
            {
                error = "fixedTimestep must be greater than zero.";
                return false;
            }

            if (maxDurationSeconds <= 0f)
            {
                error = "maxDurationSeconds must be greater than zero.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
