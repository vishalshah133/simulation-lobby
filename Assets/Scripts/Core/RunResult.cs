using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Core
{
    /// <summary>Why a run stopped.</summary>
    public enum CompletionReason
    {
        /// <summary>Still ticking — a snapshot taken mid-run.</summary>
        Running = 0,

        /// <summary>The format's own finish condition was met. The only outcome worth publishing.</summary>
        Finished = 1,

        /// <summary>Hit the config's max duration first. Usually means the config needs tuning.</summary>
        TimedOut = 2,

        /// <summary>Stopped by the harness or the editor before finishing.</summary>
        Aborted = 3,

        /// <summary>The simulation reported an unrecoverable state. Seed is unusable.</summary>
        Failed = 4
    }

    /// <summary>A named number recorded on the result — final counts, scores, survivor totals.</summary>
    [Serializable]
    public struct RunMetric
    {
        public string key;
        public float value;

        public RunMetric(string key, float value)
        {
            this.key = key;
            this.value = value;
        }
    }

    /// <summary>
    /// One timestamped thing that happened, in ticks rather than wall-clock so the log is
    /// byte-identical across runs of the same seed.
    /// </summary>
    [Serializable]
    public struct RunEvent
    {
        public int tick;
        public string label;
        public float value;

        public RunEvent(int tick, string label, float value)
        {
            this.tick = tick;
            this.label = label;
            this.value = value;
        }
    }

    /// <summary>
    /// The outcome of a run, serializable to JSON so a seed scan produces a readable table and a
    /// published video's result can be committed as a determinism baseline.
    /// </summary>
    /// <remarks>
    /// Deliberately contains no wall-clock time and no frame count — both would differ between two
    /// runs of the same seed and defeat byte-identical comparison. <see cref="unityVersion"/> is
    /// recorded because Unity physics is deterministic for a fixed binary, not across versions.
    /// </remarks>
    [Serializable]
    public sealed class RunResult
    {
        public int seed;
        public string formatSlug;
        public string configName;
        public string unityVersion;

        public CompletionReason completionReason;
        public int tickCount;
        public float fixedDeltaTime;

        /// <summary>Simulated seconds elapsed — ticks × fixed delta, not wall-clock.</summary>
        public float simulatedSeconds;

        /// <summary>Draws consumed from the run's <see cref="SeededRandom"/>. A divergence tripwire.</summary>
        public int randomDrawCount;

        /// <summary>Finish order or surviving contenders, most significant first.</summary>
        public List<string> standings = new List<string>();

        public List<RunMetric> metrics = new List<RunMetric>();
        public List<RunEvent> events = new List<RunEvent>();

        public bool Finished => completionReason == CompletionReason.Finished;

        /// <summary>
        /// Stamp the stop reason and final tick. Called by <see cref="SimulationRunner"/> when it
        /// stops a run the simulation did not end itself (timeout, abort).
        /// </summary>
        public void MarkStopped(CompletionReason reason, int finalTick)
        {
            completionReason = reason;
            tickCount = finalTick;
            simulatedSeconds = finalTick * fixedDeltaTime;
        }

        public float GetMetric(string key, float fallback = 0f)
        {
            for (int i = 0; i < metrics.Count; i++)
            {
                if (metrics[i].key == key)
                {
                    return metrics[i].value;
                }
            }

            return fallback;
        }

        /// <summary>
        /// Canonical serialization. Two runs of the same seed must produce identical strings —
        /// this is the determinism check, so nothing non-reproducible may be added to this type.
        /// </summary>
        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public static RunResult FromJson(string json)
        {
            return JsonUtility.FromJson<RunResult>(json);
        }

        /// <summary>One-line summary for console logs and seed-scan tables.</summary>
        public string ToSummaryLine()
        {
            string winner = standings.Count > 0 ? standings[0] : "-";
            return $"{formatSlug} seed {seed}: {completionReason} @ tick {tickCount} " +
                   $"({simulatedSeconds:0.00}s) winner={winner} draws={randomDrawCount}";
        }
    }
}
