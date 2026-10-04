using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Capture
{
    /// <summary>
    /// What "good television" means for one video, expressed over the metrics its format already
    /// records. The scanner knows no format; a profile is where a format's drama gets named.
    /// </summary>
    /// <remarks>
    /// Metric keys match <see cref="RunMetric.key"/>. Two extras exist on every run:
    /// <c>duration</c> (simulated seconds) and <c>finished</c> (1 or 0). A key containing one
    /// <c>*</c> is the sum of every metric it matches, so <c>attempt_*_bounces</c> is a series total
    /// without the format having to record one.
    /// <para>
    /// Score lightly. Picking the single most dramatic seed every time makes the channel samey;
    /// scan, then choose from the top ten by eye.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Simulation Lobby/Seed Scan Profile", fileName = "SeedScan_")]
    public sealed class SeedScanProfile : ScriptableObject
    {
        public enum WeightMode
        {
            /// <summary>score += weight × value</summary>
            Linear,

            /// <summary>score −= weight × |value − target| — for "close contest" style metrics.</summary>
            NearTarget
        }

        [Serializable]
        public struct Requirement
        {
            public string metric;
            public float min;
            public float max;
        }

        [Serializable]
        public struct Weight
        {
            public string metric;
            public float weight;
            public WeightMode mode;
            [Tooltip("Only used by NearTarget.")]
            public float target;
        }

        [Header("Range")]
        public int firstSeed = 1;

        [Min(1)]
        public int seedCount = 500;

        [Header("Filter + score")]
        [Tooltip("Rows outside any of these ranges are kept in the table but marked rejected.")]
        public List<Requirement> requirements = new List<Requirement>();

        public List<Weight> weights = new List<Weight>();

        [Header("Checks")]
        [Tooltip("Re-run this many of the best seeds, in reverse order, and compare to their first run. " +
                 "A mismatch means state leaked between seeds and the table can't be trusted.")]
        [Min(0)]
        public int verifyTopCount = 10;

        [Tooltip("Rows printed to the console. The CSV always has every row.")]
        [Min(1)]
        public int printTopCount = 10;

        [TextArea(2, 4)]
        public string intent;

        /// <summary>Value of a metric key on a result, with the wildcard and built-in extras resolved.</summary>
        public static float Resolve(RunResult result, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return 0f;
            }

            if (key == "duration")
            {
                return result.simulatedSeconds;
            }

            if (key == "finished")
            {
                return result.Finished ? 1f : 0f;
            }

            int star = key.IndexOf('*');
            if (star < 0)
            {
                return result.GetMetric(key);
            }

            string prefix = key.Substring(0, star);
            string suffix = key.Substring(star + 1);
            float sum = 0f;
            foreach (RunMetric metric in result.metrics)
            {
                if (metric.key.Length >= prefix.Length + suffix.Length &&
                    metric.key.StartsWith(prefix, StringComparison.Ordinal) &&
                    metric.key.EndsWith(suffix, StringComparison.Ordinal))
                {
                    sum += metric.value;
                }
            }

            return sum;
        }

        /// <summary>Null if the run passes every requirement, else the first one it failed.</summary>
        public string Reject(RunResult result)
        {
            if (!result.Finished)
            {
                return result.completionReason.ToString();
            }

            foreach (Requirement requirement in requirements)
            {
                float value = Resolve(result, requirement.metric);
                if (value < requirement.min || value > requirement.max)
                {
                    return $"{requirement.metric}={value:0.##}";
                }
            }

            return null;
        }

        public float Score(RunResult result)
        {
            float score = 0f;
            foreach (Weight weight in weights)
            {
                float value = Resolve(result, weight.metric);
                score += weight.mode == WeightMode.NearTarget
                    ? -weight.weight * Mathf.Abs(value - weight.target)
                    : weight.weight * value;
            }

            return score;
        }
    }
}
