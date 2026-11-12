using System;
using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.TrippyCoaster
{
    /// <summary>
    /// Scene entry point for <c>trip</c>. Creates the simulation, hands it to the project's <see cref="SimulationRunner"/> and starts
    /// the run in Awake (execution order -200, so every view sees <see cref="Ready"/> by its own Start). Presentation reads
    /// <see cref="State"/>; nothing writes to it.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class TrippyCoasterHost : MonoBehaviour
    {
        [SerializeField] TrippyCoasterConfig config;

        [Tooltip("Used when the config's randomizeSeedOnStart is off. A published video = scene + config + this seed.")]
        [SerializeField] int seed = 1;

        [Tooltip("Pick a fresh seed every Play (house default). The drawn seed is logged; paste it above to re-render a take.")]
        [SerializeField] bool randomizeSeedOnStart = true;

        [SerializeField] bool autoStart = true;

        SimulationRunner _runner;
        TrippyCoasterSimulation _simulation;

        public TrippyCoasterConfig Config => config;
        public ITripState State => _simulation;
        public SimulationRunner Runner => _runner;
        public int Seed => seed;
        public bool Ready { get; private set; }

        public void SetConfig(TrippyCoasterConfig value)
        {
            config = value;
        }

        void Awake()
        {
            _runner = GetComponent<SimulationRunner>();
            _runner.autoStart = false;
            if (autoStart) Begin();
        }

        public bool Begin()
        {
            if (Ready) return true;
            if (config == null)
            {
                Debug.LogError("[trip] TrippyCoasterHost has no config assigned.", this);
                return false;
            }
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[trip] config '{config.name}' is invalid: {error}", config);
                return false;
            }

            int runSeed = randomizeSeedOnStart ? Guid.NewGuid().GetHashCode() : seed;
            seed = runSeed;
            Debug.Log($"[trip] seed = {runSeed}", this);

            _runner.config = config;
            _simulation = new TrippyCoasterSimulation();
            _runner.SetSimulation(_simulation);
            if (!_runner.Begin(runSeed))
            {
                _simulation = null;
                return false;
            }

            Ready = true;
            return true;
        }
    }
}
