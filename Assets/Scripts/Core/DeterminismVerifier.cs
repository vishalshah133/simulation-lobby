using System;
using UnityEngine;

namespace SimulationLobby.Core
{
    /// <summary>
    /// Runs the same seed twice and compares the serialized results. The regression check to run
    /// after any change to <c>Core</c> or <c>Shared</c>, before trusting an old seed again.
    /// </summary>
    /// <remarks>
    /// Deliberately drives runs through <see cref="SimulationRunner"/> rather than its own loop —
    /// a verifier with its own stepping logic would prove the verifier deterministic, not the runner.
    /// </remarks>
    public static class DeterminismVerifier
    {
        public struct Comparison
        {
            public bool identical;
            public string reason;
            public RunResult first;
            public RunResult second;

            public string ToSummaryLine()
            {
                return identical
                    ? $"DETERMINISTIC · {first.ToSummaryLine()}"
                    : $"DIVERGED · {reason}";
            }
        }

        /// <summary>
        /// Run <paramref name="factory"/> twice with the same seed and config, and compare.
        /// </summary>
        /// <param name="factory">
        /// Must produce a fresh simulation each call — a reused instance carries state and would
        /// pass or fail for the wrong reason.
        /// </param>
        public static Comparison Compare(Func<ISimulation> factory, int seed, SimulationConfig config)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            RunResult first = RunHeadless(factory(), seed, config);
            RunResult second = RunHeadless(factory(), seed, config);

            return Compare(first, second);
        }

        /// <summary>
        /// Compare two results directly — used against a committed baseline JSON to answer
        /// "did I break an old video" as a diff rather than from memory.
        /// </summary>
        public static Comparison Compare(RunResult first, RunResult second)
        {
            var comparison = new Comparison { first = first, second = second };

            if (first == null || second == null)
            {
                comparison.reason = "one of the runs produced no result";
                return comparison;
            }

            // Checked before the JSON compare so a failure names the cause instead of dumping two
            // near-identical blobs at the reader.
            if (first.randomDrawCount != second.randomDrawCount)
            {
                comparison.reason =
                    $"random draw count differs: {first.randomDrawCount} vs {second.randomDrawCount} " +
                    "— something consumed the RNG conditionally on non-simulation state";
                return comparison;
            }

            if (first.tickCount != second.tickCount)
            {
                comparison.reason = $"tick count differs: {first.tickCount} vs {second.tickCount}";
                return comparison;
            }

            if (first.completionReason != second.completionReason)
            {
                comparison.reason = $"completion reason differs: {first.completionReason} vs {second.completionReason}";
                return comparison;
            }

            string firstJson = first.ToJson(false);
            string secondJson = second.ToJson(false);
            if (firstJson != secondJson)
            {
                comparison.reason = "serialized results differ:\n" + DescribeFirstDifference(firstJson, secondJson);
                return comparison;
            }

            comparison.identical = true;
            return comparison;
        }

        /// <summary>
        /// Step one simulation to completion as fast as the CPU allows, through the real runner but
        /// outside Unity's loop. Safe to call from the editor with no scene set up.
        /// </summary>
        public static RunResult RunHeadless(ISimulation simulation, int seed, SimulationConfig config)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            var host = new GameObject($"DeterminismVerifier_{config.name}_{seed}");
            host.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                SimulationRunner runner = host.AddComponent<SimulationRunner>();
                runner.autoStart = false;
                runner.logResult = false;
                runner.config = config;
                runner.SetSimulation(simulation);

                if (!runner.Begin(seed))
                {
                    return null;
                }

                // MaxTicks is the runner's own timeout, so this loop cannot outlive it; the +1 lets
                // the runner be the thing that stops the run rather than the loop bound.
                int safetyLimit = config.MaxTicks + 1;
                int steps = 0;
                while (runner.IsRunning && steps < safetyLimit)
                {
                    runner.StepOnce();
                    steps++;
                }

                if (runner.IsRunning)
                {
                    runner.Abort();
                }

                return runner.Result;
            }
            finally
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(host);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }
        }

        static string DescribeFirstDifference(string first, string second)
        {
            int limit = Mathf.Min(first.Length, second.Length);
            for (int i = 0; i < limit; i++)
            {
                if (first[i] != second[i])
                {
                    int from = Mathf.Max(0, i - 60);
                    int length = Mathf.Min(140, limit - from);
                    return $"  at char {i}\n  A: …{first.Substring(from, length)}\n  B: …{second.Substring(from, length)}";
                }
            }

            return $"  identical for {limit} chars, then lengths differ ({first.Length} vs {second.Length})";
        }
    }
}
