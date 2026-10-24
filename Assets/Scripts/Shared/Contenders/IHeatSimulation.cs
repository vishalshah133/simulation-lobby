using System.Collections.Generic;
using SimulationLobby.Core;

namespace SimulationLobby.Shared
{
    /// <summary>
    /// A format that can run as one heat inside an orchestrator (<c>elim</c>). The orchestrator calls
    /// <see cref="ConfigureHeat"/>, then <c>Initialize</c>, then <c>Tick</c> until <c>IsComplete</c>. It never needs to
    /// know which format is underneath.
    /// </summary>
    public interface IHeatSimulation : ISimulation
    {
        /// <summary>
        /// Call before <c>Initialize</c>; the setting holds for every later <c>Initialize</c> until changed.
        /// <paramref name="entrants"/> null: the format's own roster. <paramref name="safeCount"/> &lt;= 0: the format's
        /// normal finish rule. <paramref name="safeCount"/> &gt; 0: the heat completes once that many entrants have
        /// finished (plus the format's cut grace), and <c>Result.standings</c> is finish order for the finishers
        /// followed by progress order for the rest.
        /// </summary>
        void ConfigureHeat(IReadOnlyList<HeatEntrant> entrants, int safeCount);

        /// <summary>The configured safe count; 0 when the normal finish rule applies.</summary>
        int SafeCount { get; }

        /// <summary>How many entrants this heat has (the configured ones, else the format's own roster).</summary>
        int EntrantCount { get; }

        /// <summary>How many entrants have crossed the line so far this heat.</summary>
        int FinishedCount { get; }
    }
}
