namespace SimulationLobby.Core
{
    /// <summary>
    /// The lifecycle contract every format implements. This is what lets
    /// <c>Capture</c> batch-run a format it has never heard of, with no per-format branch.
    /// </summary>
    /// <remarks>
    /// Implementations must be driven only from a fixed timestep (see <see cref="SimulationRunner"/>)
    /// and must not read <c>Time.deltaTime</c>, <c>Time.time</c>, or <c>UnityEngine.Random</c>.
    /// </remarks>
    public interface ISimulation
    {
        /// <summary>
        /// Short format identifier used in results and filenames: <c>esc</c>, <c>race</c>, <c>wars</c>,
        /// <c>rom</c>, <c>elim</c>, <c>surv</c>.
        /// </summary>
        string FormatSlug { get; }

        /// <summary>
        /// Build the run from a seed and a config. Called exactly once, before the first
        /// <see cref="Tick"/>. All setup randomness must come from a <see cref="SeededRandom"/>
        /// constructed here.
        /// </summary>
        /// <param name="config">
        /// The format's own config type, passed as the shared base. Implementations cast to their
        /// derived type.
        /// </param>
        void Initialize(int seed, SimulationConfig config);

        /// <summary>Advance one fixed step. Never called before <see cref="Initialize"/>.</summary>
        void Tick(float fixedDelta);

        /// <summary>
        /// True once the format's own win/finish condition is met. The runner also stops on the
        /// config's max duration, so an implementation never has to enforce a timeout itself.
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// Outcome of the run. Valid once <see cref="IsComplete"/> is true or the runner has
        /// stopped it; may be null before that.
        /// </summary>
        RunResult Result { get; }
    }
}
