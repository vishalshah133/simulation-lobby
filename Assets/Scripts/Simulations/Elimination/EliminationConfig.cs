using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using SimulationLobby.Shared.Tracks;
using UnityEngine;

namespace SimulationLobby.Simulations.Elimination
{
    /// <summary>How a heat changes who is still in.</summary>
    public enum HeatRule
    {
        /// <summary>The first <c>safeCount</c> across the line stay in; everyone else is out.</summary>
        CutLast = 0,

        /// <summary>The entrants are every marble currently out. The winner comes back; the rest stay out.</summary>
        LastChance = 1,

        /// <summary>The winner is the champion; everyone else is out.</summary>
        Final = 2
    }

    /// <summary>One race of the show: its name, its rule, how many survive, and the track it runs on.</summary>
    [Serializable]
    public struct EliminationHeat
    {
        [Tooltip("Shown as a caption in the heat intro and on the leaderboard (at most 4 words).")]
        public string title;

        public HeatRule rule;

        [Tooltip("CutLast only: how many finishers are safe. LastChance and Final always use 1.")]
        public int safeCount;

        [Tooltip("The course. Baked into the heat's rig by the scene builder; the runtime never reads it.")]
        public TrackLayout layout;

        public EliminationHeat(string title, HeatRule rule, int safeCount, TrackLayout layout)
        {
            this.title = title;
            this.rule = rule;
            this.safeCount = safeCount;
            this.layout = layout;
        }

        /// <summary>How many finishers end the heat: <see cref="safeCount"/> for a cut, else 1.</summary>
        public int EffectiveSafeCount => rule == HeatRule.CutLast ? Mathf.Max(1, safeCount) : 1;
    }

    /// <summary>
    /// Tunables for the elimination show: the sixteen marbles, the six heats, and the phase timings. A published
    /// video is this asset plus a scene plus a seed. The heat itself (the race) is configured by
    /// <see cref="heatSimulationConfig"/>, typed as the base so this assembly never needs to know about Race.
    /// </summary>
    [CreateAssetMenu(fileName = "EliminationConfig", menuName = "Simulation Lobby/Configs/Elimination", order = 33)]
    public sealed class EliminationConfig : SimulationConfig
    {
        [Header("Field")]
        [Tooltip("Sixteen unique names and jewel colours. The roster index is the marble's identity for the whole show.")]
        public HeatEntrant[] roster = DefaultRoster();

        [Header("Heats")]
        public EliminationHeat[] heats = DefaultHeats();

        [Tooltip("Passed to every heat's Initialize (a RaceConfig asset for race heats, assigned by the scene builder).")]
        public SimulationConfig heatSimulationConfig;

        [Header("Timing (seconds)")]
        [Min(1f)] public float introSeconds = 25f;
        [Min(1f)] public float heatIntroSeconds = 12f;
        [Tooltip("After the heat is called, the rig stays on screen this long so the last-spot moment lands.")]
        [Min(1.5f)] public float heatOutroSeconds = 2.5f;
        [Min(1f)] public float resultSeconds = 15f;
        [Min(1f)] public float twistSeconds = 15f;
        [Min(1f)] public float payoffSeconds = 20f;

        [Tooltip("A heat that has not finished after this long is a failed run (the race has its own stall guards, so this is a backstop).")]
        [Min(30f)] public float heatMaxSeconds = 300f;

        [Header("Text")]
        public string question = "WHICH MARBLE SURVIVES?";

        void Reset()
        {
            fixedTimestep = 1f / 120f;
            maxDurationSeconds = 1800f;
        }

        /// <summary>Phases plus one worst-case heat per heat, so the runner's cap is structural and not a number to keep in sync.</summary>
        public override int MaxTicks
        {
            get
            {
                int heatCount = heats != null ? heats.Length : 0;
                float seconds = introSeconds + twistSeconds + payoffSeconds + 30f;
                seconds += heatCount * (heatIntroSeconds + heatMaxSeconds + heatOutroSeconds + resultSeconds);
                return Mathf.Max(1, Mathf.CeilToInt(seconds / fixedTimestep));
            }
        }

        /// <summary>Checks that the heat table is playable for this many marbles: every cut leaves fewer in, a last chance has marbles to bring back.</summary>
        public static bool TryTrace(int rosterCount, IReadOnlyList<EliminationHeat> table, out string error)
        {
            int alive = rosterCount;
            int outCount = 0;
            for (int i = 0; i < table.Count; i++)
            {
                EliminationHeat heat = table[i];
                switch (heat.rule)
                {
                    case HeatRule.CutLast:
                        if (heat.safeCount < 1 || heat.safeCount >= alive)
                        {
                            error = $"Heat {i + 1} '{heat.title}': safeCount {heat.safeCount} must be between 1 and {alive - 1} ({alive} are in).";
                            return false;
                        }

                        outCount += alive - heat.safeCount;
                        alive = heat.safeCount;
                        break;

                    case HeatRule.LastChance:
                        if (outCount < 2)
                        {
                            error = $"Heat {i + 1} '{heat.title}': a last chance needs at least two marbles out ({outCount} are).";
                            return false;
                        }

                        alive++;
                        outCount--;
                        break;

                    default:
                        if (alive < 2)
                        {
                            error = $"Heat {i + 1} '{heat.title}': a final needs at least two marbles in.";
                            return false;
                        }

                        outCount += alive - 1;
                        alive = 1;
                        break;
                }
            }

            error = null;
            return true;
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (roster == null || roster.Length < 2 || roster.Length > 16)
            {
                error = "The roster needs between 2 and 16 marbles.";
                return false;
            }

            var seen = new HashSet<string>();
            foreach (HeatEntrant entrant in roster)
            {
                if (string.IsNullOrEmpty(entrant.name) || !seen.Add(entrant.name))
                {
                    error = $"Roster names must be unique and non-empty ('{entrant.name}').";
                    return false;
                }
            }

            if (heats == null || heats.Length == 0)
            {
                error = "The show needs at least one heat.";
                return false;
            }

            if (heatSimulationConfig == null)
            {
                error = "heatSimulationConfig is not assigned (the scene builder assigns the race config).";
                return false;
            }

            if (!TryTrace(roster.Length, heats, out error))
            {
                return false;
            }

            if (heats[heats.Length - 1].rule != HeatRule.Final)
            {
                error = "The last heat must be a Final.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Sixteen jewel tones, picked to stay apart on piano-black at 480px. The first twelve are
        /// <c>RaceConfig.DefaultRoster</c> (copied, because this assembly cannot see Race); BRONZE, SILVER,
        /// MAGENTA and MINT fill the gaps in hue.
        /// </summary>
        public static HeatEntrant[] DefaultRoster()
        {
            return new[]
            {
                new HeatEntrant("RUBY",     new Color(0.86f, 0.07f, 0.16f), 0),
                new HeatEntrant("EMERALD",  new Color(0.05f, 0.62f, 0.30f), 1),
                new HeatEntrant("SAPPHIRE", new Color(0.08f, 0.28f, 0.90f), 2),
                new HeatEntrant("TOPAZ",    new Color(0.98f, 0.76f, 0.08f), 3),
                new HeatEntrant("AMETHYST", new Color(0.58f, 0.22f, 0.86f), 4),
                new HeatEntrant("PEARL",    new Color(0.96f, 0.93f, 0.86f), 5),
                new HeatEntrant("CORAL",    new Color(1.00f, 0.38f, 0.30f), 6),
                new HeatEntrant("AQUA",     new Color(0.10f, 0.80f, 0.82f), 7),
                new HeatEntrant("CITRINE",  new Color(0.62f, 0.86f, 0.12f), 8),
                new HeatEntrant("ROSE",     new Color(1.00f, 0.42f, 0.70f), 9),
                new HeatEntrant("COBALT",   new Color(0.30f, 0.55f, 1.00f), 10),
                new HeatEntrant("AMBER",    new Color(1.00f, 0.50f, 0.06f), 11),
                new HeatEntrant("BRONZE",   new Color(0.66f, 0.40f, 0.18f), 12),
                new HeatEntrant("SILVER",   new Color(0.70f, 0.73f, 0.78f), 13),
                new HeatEntrant("MAGENTA",  new Color(0.88f, 0.10f, 0.72f), 14),
                new HeatEntrant("MINT",     new Color(0.52f, 0.95f, 0.70f), 15)
            };
        }

        /// <summary>The fixed show: 16 in, 12, 8, one comes back, 6, 3, a champion. Layouts are assigned by the scene builder.</summary>
        public static EliminationHeat[] DefaultHeats()
        {
            return new[]
            {
                new EliminationHeat("THE GRAND PRIX", HeatRule.CutLast, 12, null),
                new EliminationHeat("PEG FALL", HeatRule.CutLast, 8, null),
                new EliminationHeat("LAST CHANCE", HeatRule.LastChance, 1, null),
                new EliminationHeat("PINWHEELS", HeatRule.CutLast, 6, null),
                new EliminationHeat("THE HELIX", HeatRule.CutLast, 3, null),
                new EliminationHeat("THE GAUNTLET", HeatRule.Final, 1, null)
            };
        }
    }
}
