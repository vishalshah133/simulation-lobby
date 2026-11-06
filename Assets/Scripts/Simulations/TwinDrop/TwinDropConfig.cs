using System;
using SimulationLobby.Core;
using SimulationLobby.Simulations.TwinDrop.Kernel;
using UnityEngine;

namespace SimulationLobby.Simulations.TwinDrop
{
    /// <summary>
    /// Config for <c>twin</c> (two marbles, same drop): pairs of marbles are released through a peg board,
    /// each round with a smaller gap between the two, and the viewer guesses whether they land in the same slot.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TwinDropConfig",
        menuName = "Simulation Lobby/Configs/Twin Drop (twin)",
        order = 18)]
    public sealed class TwinDropConfig : SimulationConfig
    {
        [Serializable]
        public struct TwinRound
        {
            [Tooltip("Real-world gap between the two release points, metres. 0 = exactly identical.")]
            public double gapMeters;

            [Tooltip("Bottom line during the round, e.g. 1 CENTIMETRE APART.")]
            public string gapLabel;

            [Range(1, 6)] public int pairs;
        }

        [Header("Board (world units; the board is metersPerUnit * 10 wide)")]
        [Range(6, 16)] public int rows = 10;
        [Min(0.5f)] public float spacing = 1f;
        [Min(0.3f)] public float rowSpacing = 0.7f;
        [Min(3f)] public float halfWidth = 5f;
        [Min(1f)] public float topRowY = 7.6f;
        [Min(0.02f)] public float pegRadius = 0.1f;
        [Min(0.05f)] public float ballRadius = 0.22f;
        [Min(1f)] public float gravity = 36f;
        [Range(0.3f, 1f)] public float restitution = 0.8f;
        [Range(4, 14)] public int bins = 10;

        [Tooltip("World units are this many real metres, so a gap in metres converts to a gap on the board.")]
        [Min(0.001f)] public float metersPerUnit = 0.1f;

        [Header("Release")]
        [Tooltip("Each pair's release x is drawn from the seed in -this..+this.")]
        [Range(0f, 2f)] public float releaseRange = 1f;

        [Min(0.5f)] public float releaseHeight = 1.2f;

        [Tooltip("A pair that has not landed by now is cut and logs an error.")]
        [Min(5f)] public float maxFallSeconds = 20f;

        [Header("Pacing")]
        [Min(0f)] public float firstHoldSeconds = 3f;
        [Min(0f)] public float roundHoldSeconds = 1.8f;
        [Min(0f)] public float pairHoldSeconds = 0.8f;
        [Min(0f)] public float resultHoldSeconds = 1.1f;
        [Min(0f)] public float endCardSeconds = 6f;

        [Header("Text")]
        public string title = "SAME SLOT OR DIFFERENT?";
        [Tooltip("Second line under the title. Empty hides it, which is the default: one question is enough.")]
        public string question = string.Empty;
        public string sameName = "SAME";
        public string apartName = "DIFFERENT";
        public string sameResult = "SAME SLOT!";
        public string apartResult = "DIFFERENT SLOTS!";
        [Tooltip("{apart} and {n}: measured over every pair that started with a gap.")]
        public string endTitle = "{apart} OF {n} LANDED APART";
        public string endWhy = "THE BUTTERFLY EFFECT";

        public TwinRound[] rounds =
        {
            new TwinRound { gapMeters = 0.0, gapLabel = "NO GAP", pairs = 2 },
            new TwinRound { gapMeters = 0.01, gapLabel = "GAP: 1 CENTIMETRE", pairs = 2 },
            new TwinRound { gapMeters = 0.0001, gapLabel = "GAP: A HAIR'S WIDTH", pairs = 2 },
            new TwinRound { gapMeters = 0.000001, gapLabel = "GAP: A BACTERIUM", pairs = 2 },
            new TwinRound { gapMeters = 0.000000001, gapLabel = "GAP: ONE MOLECULE", pairs = 2 },
            new TwinRound { gapMeters = 0.0000000001, gapLabel = "GAP: ONE ATOM", pairs = 2 }
        };

        public int RoundCount => rounds != null ? rounds.Length : 0;

        public int TotalPairs
        {
            get
            {
                int n = 0;
                for (int i = 0; i < RoundCount; i++)
                {
                    n += rounds[i].pairs;
                }

                return n;
            }
        }

        public PegBoardKernel CreateKernel()
        {
            return new PegBoardKernel(rows, spacing, rowSpacing, halfWidth, topRowY, 0.0, pegRadius, ballRadius,
                                      gravity, restitution, bins);
        }

        public double GapUnits(int round) => rounds[round].gapMeters / metersPerUnit;

        public float WorstCaseSeconds =>
            firstHoldSeconds + endCardSeconds +
            TotalPairs * (pairHoldSeconds + maxFallSeconds + resultHoldSeconds) + RoundCount * roundHoldSeconds;

        public override int MaxTicks => Mathf.CeilToInt((WorstCaseSeconds + 5f) / fixedTimestep);

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (RoundCount <= 0 || TotalPairs <= 0)
            {
                error = "A twin video needs at least one round with at least one pair.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
