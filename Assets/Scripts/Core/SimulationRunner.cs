using System;
using UnityEngine;

namespace SimulationLobby.Core
{
    /// <summary>
    /// Drives one <see cref="ISimulation"/> from <c>FixedUpdate</c>. The single place a simulation is
    /// stepped, so "simulation logic runs on a fixed timestep" is structural rather than a habit.
    /// </summary>
    /// <remarks>
    /// Scene usage: put this on a GameObject whose simulation component implements
    /// <see cref="ISimulation"/> (or assign one), point it at a config asset, set a seed.
    /// Headless usage: the batch harness calls <see cref="Begin"/> and <see cref="StepOnce"/>
    /// directly, bypassing Unity's loop entirely — which is why every stop condition is counted in
    /// ticks and nothing here reads <c>Time.deltaTime</c>.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SimulationRunner : MonoBehaviour
    {
        [Header("Run identity")]
        [Tooltip("The seed that defines this run. A published video is scene + config + this number.")]
        public int seed;

        [Tooltip("Config asset for the format. Its fixedTimestep drives Time.fixedDeltaTime.")]
        public SimulationConfig config;

        [Header("Behaviour")]
        [Tooltip("Start on Awake. Turn off when a harness or an intro sequence drives the start.")]
        public bool autoStart = true;

        [Tooltip("Draw a fresh, unsaved seed on every Awake instead of using the seed field above. " +
                 "The run this produces cannot be re-rendered later -- there is nothing to write down. " +
                 "Off by default: the rest of this project (seed scans, retros, re-renders) assumes a " +
                 "published video's seed is known and reproducible. Console still logs the seed that " +
                 "was actually drawn, in case you want to hand-copy it into the seed field afterward.")]
        public bool randomizeSeedOnStart;

        [Tooltip("Log the one-line result summary to the console when the run stops.")]
        public bool logResult = true;

        /// <summary>Fires once when the run stops, whatever the reason. Presentation may listen; it must not mutate.</summary>
        public event Action<RunResult> RunCompleted;

        ISimulation _simulation;
        float _restoreFixedDeltaTime;
        bool _fixedDeltaOverridden;

        /// <summary>True between a successful <see cref="Begin"/> and the run stopping.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Steps taken this run.</summary>
        public int Tick { get; private set; }

        /// <summary>Simulated seconds elapsed — ticks × fixed delta, never wall-clock.</summary>
        public float SimulatedSeconds => Tick * (config != null ? config.fixedTimestep : Time.fixedDeltaTime);

        /// <summary>Result of the run, once it has stopped. Null before the first <see cref="Begin"/>.</summary>
        public RunResult Result { get; private set; }

        /// <summary>
        /// Assign the simulation explicitly. Needed when the implementation is a plain class rather
        /// than a component on this GameObject, and by the headless harness.
        /// </summary>
        public void SetSimulation(ISimulation simulation)
        {
            if (IsRunning)
            {
                Debug.LogError("[SimulationRunner] Cannot swap the simulation mid-run.", this);
                return;
            }

            _simulation = simulation;
        }

        void Awake()
        {
            if (_simulation == null)
            {
                _simulation = GetComponent<ISimulation>();
            }

            if (autoStart)
            {
                int startSeed = randomizeSeedOnStart ? GenerateUnsavedSeed() : seed;
                Begin(startSeed);
            }
        }

        /// <summary>
        /// A seed with no provenance -- not derived from anything this project treats as reproducible
        /// (not <see cref="UnityEngine.Random"/>, not tied to a config or scan). Only for
        /// <see cref="randomizeSeedOnStart"/>, which exists precisely to be unreproducible.
        /// </summary>
        static int GenerateUnsavedSeed()
        {
            return Guid.NewGuid().GetHashCode();
        }

        void OnDestroy()
        {
            RestoreFixedDeltaTime();
        }

        /// <summary>Start a run. Returns false and logs if the setup is unusable.</summary>
        public bool Begin(int runSeed)
        {
            if (IsRunning)
            {
                Debug.LogError("[SimulationRunner] A run is already in progress.", this);
                return false;
            }

            if (_simulation == null)
            {
                Debug.LogError("[SimulationRunner] No ISimulation assigned or found on this GameObject.", this);
                return false;
            }

            if (config == null)
            {
                Debug.LogError("[SimulationRunner] No config assigned — a run has no tunables without one.", this);
                return false;
            }

            if (!config.Validate(out string error))
            {
                Debug.LogError($"[SimulationRunner] Config '{config.name}' is invalid: {error}", config);
                return false;
            }

            seed = runSeed;
            Tick = 0;
            Result = null;

            ApplyFixedDeltaTime(config.fixedTimestep);

            _simulation.Initialize(runSeed, config);
            IsRunning = true;

            // A format whose start state already satisfies its finish condition stops on tick zero
            // rather than running a step it was never meant to.
            if (_simulation.IsComplete)
            {
                Stop(CompletionReason.Finished);
            }

            return true;
        }

        void FixedUpdate()
        {
            if (!IsRunning)
            {
                return;
            }

            StepOnce();
        }

        /// <summary>
        /// Advance exactly one step and evaluate the stop conditions. Public so the headless harness
        /// can run a seed as fast as the CPU allows, through the same code path the editor uses.
        /// </summary>
        public void StepOnce()
        {
            if (!IsRunning)
            {
                return;
            }

            float fixedDelta = config.fixedTimestep;

            try
            {
                _simulation.Tick(fixedDelta);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Stop(CompletionReason.Failed);
                return;
            }

            Tick++;

            if (_simulation.IsComplete)
            {
                Stop(CompletionReason.Finished);
                return;
            }

            if (Tick >= config.MaxTicks)
            {
                Stop(CompletionReason.TimedOut);
            }
        }

        /// <summary>Stop early — used by the harness and by an editor abort.</summary>
        public void Abort()
        {
            if (IsRunning)
            {
                Stop(CompletionReason.Aborted);
            }
        }

        void Stop(CompletionReason reason)
        {
            IsRunning = false;
            RestoreFixedDeltaTime();

            RunResult result = _simulation.Result;
            if (result == null)
            {
                // A format that never built a recorder still owes the harness a row, so synthesise
                // the minimum rather than handing back null.
                result = new RunResult
                {
                    seed = seed,
                    formatSlug = _simulation.FormatSlug,
                    configName = config != null ? config.name : "(none)",
                    unityVersion = Application.unityVersion,
                    fixedDeltaTime = config != null ? config.fixedTimestep : Time.fixedDeltaTime
                };

                Debug.LogWarning(
                    $"[SimulationRunner] '{_simulation.FormatSlug}' returned no RunResult — " +
                    "it should expose a RunRecorder's result. Synthesised a minimal one.", this);
            }

            if (result.completionReason == CompletionReason.Running)
            {
                result.MarkStopped(reason, Tick);
            }

            Result = result;

            if (logResult)
            {
                Debug.Log($"[SimulationRunner] {result.ToSummaryLine()}", this);
            }

            RunCompleted?.Invoke(result);
        }

        void ApplyFixedDeltaTime(float fixedTimestep)
        {
            if (!_fixedDeltaOverridden)
            {
                _restoreFixedDeltaTime = Time.fixedDeltaTime;
                _fixedDeltaOverridden = true;
            }

            Time.fixedDeltaTime = fixedTimestep;
        }

        void RestoreFixedDeltaTime()
        {
            if (!_fixedDeltaOverridden)
            {
                return;
            }

            Time.fixedDeltaTime = _restoreFixedDeltaTime;
            _fixedDeltaOverridden = false;
        }
    }
}
