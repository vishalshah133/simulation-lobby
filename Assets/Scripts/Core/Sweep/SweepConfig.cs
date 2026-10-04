using System;
using UnityEngine;

namespace SimulationLobby.Core
{
    /// <summary>
    /// A "0% vs 100%" video: the same simulation run once per take, each take with its own config,
    /// captioned with the value being swept. Drives a <see cref="SweepRunner"/>.
    /// </summary>
    /// <remarks>
    /// Each take points at a complete config asset rather than patching one value at runtime, so any
    /// single take can be re-rendered on its own and the difference between takes is visible in a
    /// config diff. Format-agnostic: it only ever sees <see cref="SimulationConfig"/>.
    /// </remarks>
    [CreateAssetMenu(menuName = "Simulation Lobby/Sweep Config", fileName = "SweepConfig")]
    public sealed class SweepConfig : ScriptableObject
    {
        [Serializable]
        public struct Take
        {
            [Tooltip("Full config for this take. Every take must share one fixedTimestep.")]
            public SimulationConfig config;

            [Tooltip("The swept value as shown on screen, e.g. \"25%\".")]
            public string label;
        }

        [Tooltip("Caption per take. {0} is replaced by the take's label, e.g. \"SOFT {0}\".")]
        public string captionFormat = "{0}";

        public Take[] takes = Array.Empty<Take>();

        [Header("Rhythm")]
        [Tooltip("A 3-2-1 countdown before the first take, with the first take already on screen. " +
                 "Gives the editor time to finish its first-frame work (shader compiles, reflection " +
                 "probe, audio generation) before anything moves, so the first drop isn't stuttery. " +
                 "Whole seconds: one count per second. 0 = no countdown. Trim it in the edit if the " +
                 "video shouldn't show it.")]
        [Min(0)] public int countdownSeconds = 3;

        [Tooltip("Seconds the first take sits still with its caption up before anything moves. " +
                 "Longer than the rest: it has to state the premise.")]
        [Min(0f)] public float firstLeadInSeconds = 1.2f;

        [Tooltip("Seconds every later take sits still with its caption up before it runs.")]
        [Min(0f)] public float leadInSeconds = 0.35f;

        [Tooltip("Simulated seconds each take runs. Fixed on purpose: a steady beat is what lets the " +
                 "viewer anticipate the next take.")]
        [Min(0.1f)] public float takeSeconds = 3.4f;

        [Tooltip("Seconds to hold on the last take's final frame after it ends.")]
        [Min(0f)] public float endHoldSeconds = 0.4f;

        [TextArea(2, 4)] public string intent;

        public int TakeCount => takes != null ? takes.Length : 0;

        public float FixedTimestep => TakeCount > 0 && takes[0].config != null ? takes[0].config.fixedTimestep : 1f / 60f;

        public string Caption(int take) =>
            take >= 0 && take < TakeCount ? string.Format(captionFormat, takes[take].label) : string.Empty;

        public int LeadInTicks(int take) =>
            Mathf.RoundToInt((take == 0 ? firstLeadInSeconds : leadInSeconds) / FixedTimestep);

        public int TakeTicks => Mathf.Max(1, Mathf.RoundToInt(takeSeconds / FixedTimestep));

        public int EndHoldTicks => Mathf.RoundToInt(endHoldSeconds / FixedTimestep);

        public int CountdownTicks => Mathf.RoundToInt(countdownSeconds / FixedTimestep);

        /// <summary>Total length on screen, in seconds, before any slow-motion. Includes the countdown.</summary>
        public float TotalSeconds
        {
            get
            {
                int ticks = EndHoldTicks + CountdownTicks;
                for (int i = 0; i < TakeCount; i++)
                {
                    ticks += LeadInTicks(i) + TakeTicks;
                }
                return ticks * FixedTimestep;
            }
        }

        public bool Validate(out string error)
        {
            if (TakeCount == 0)
            {
                error = "A sweep needs at least one take.";
                return false;
            }

            float step = FixedTimestep;
            for (int i = 0; i < TakeCount; i++)
            {
                SimulationConfig config = takes[i].config;
                if (config == null)
                {
                    error = $"Take {i} has no config.";
                    return false;
                }

                if (!config.Validate(out string takeError))
                {
                    error = $"Take {i} ('{config.name}'): {takeError}";
                    return false;
                }

                // One timestep for the whole sweep: Time.fixedDeltaTime is global, and a take that
                // stepped at a different rate would not be comparable with its neighbours anyway.
                if (!Mathf.Approximately(config.fixedTimestep, step))
                {
                    error = $"Take {i} ('{config.name}') uses fixedTimestep {config.fixedTimestep}, " +
                            $"the sweep uses {step}. Every take must match.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
