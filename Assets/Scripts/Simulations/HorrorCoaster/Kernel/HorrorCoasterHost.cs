using System;
using System.Collections.Generic;
using SimulationLobby.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SimulationLobby.Simulations.HorrorCoaster
{
    /// <summary>
    /// Scene entry point for <c>coast</c>. Creates the <see cref="HorrorCoasterSimulation"/>, hands it to the project's
    /// <see cref="SimulationRunner"/> and starts the run in Awake (execution order -200, so every other component sees
    /// <see cref="Ready"/> by its own Start). Presentation reads <see cref="State"/>; nothing writes to it.
    /// </summary>
    /// <remarks>
    /// Review keys (Play mode, new Input System, <see cref="debugKeys"/>): N = jump to 5 s before the next unplayed event,
    /// P = jump to 5 s before the previous event, B = back to the start. A jump rebuilds the simulation from the same seed
    /// and ticks it headlessly at the fixed step until the target time, so rider counts, phases and the cart are exactly what
    /// a normal run has there. The runner keeps ticking through a thin proxy, so a jump swaps the simulation object under it:
    /// <see cref="State"/> then returns the new object and <see cref="StateVersion"/> increases (views poll either).
    /// </remarks>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class HorrorCoasterHost : MonoBehaviour
    {
        /// <summary>Seconds of lead-in before an event when jumping to it.</summary>
        const float JumpLeadSeconds = 5f;

        [SerializeField] HorrorCoasterConfig config;

        [Tooltip("Used when the config's randomizeSeedOnStart is off. A published video = scene + config + this seed.")]
        [SerializeField] int seed = 1;

        [Tooltip("Start on Awake. Turn off when a harness drives the run through Begin().")]
        [SerializeField] bool autoStart = true;

        [Tooltip("Play-mode review keys: N next event, P previous event, B back to the start (new Input System).")]
        [SerializeField] bool debugKeys = true;

        /// <summary>Forwards the runner's calls to whichever simulation is current, so a jump can swap it mid-run.</summary>
        sealed class SwitchableSimulation : ISimulation
        {
            public HorrorCoasterSimulation Inner;
            public string FormatSlug => Inner.FormatSlug;
            public bool IsComplete => Inner.IsComplete;
            public RunResult Result => Inner.Result;
            public void Initialize(int s, SimulationConfig c) => Inner.Initialize(s, c);
            public void Tick(float fixedDelta) => Inner.Tick(fixedDelta);
        }

        HorrorCoasterSimulation _simulation;
        SwitchableSimulation _proxy;
        SimulationRunner _runner;
        float[] _stingTimes;
        ScareKind[] _stingKinds;

        public HorrorCoasterConfig Config => config;
        public ICoasterState State => _simulation;
        public HorrorCoasterSimulation Simulation => _simulation;
        public SimulationRunner Runner => _runner;
        public int Seed => seed;

        /// <summary>Increases every time <see cref="State"/> is replaced (a review jump). Starts at 1 on Begin.</summary>
        public int StateVersion { get; private set; }

        /// <summary>True once the run has begun and <see cref="State"/> is valid.</summary>
        public bool Ready { get; private set; }

        /// <summary>Assign the config from code (scene builders); call before Awake or before Begin().</summary>
        public void SetConfig(HorrorCoasterConfig value)
        {
            config = value;
        }

        void Awake()
        {
            _runner = GetComponent<SimulationRunner>();
            // This host owns the start, so the runner must not auto-start on its own Awake.
            _runner.autoStart = false;
            if (autoStart) Begin();
        }

        /// <summary>Build the simulation and start the run. Returns false (and logs) if the config is unusable.</summary>
        public bool Begin()
        {
            if (Ready) return true;
            if (config == null)
            {
                Debug.LogError("[coast] HorrorCoasterHost has no config assigned.", this);
                return false;
            }
            if (!config.Validate(out string error))
            {
                Debug.LogError($"[coast] config '{config.name}' is invalid: {error}", config);
                return false;
            }

            int runSeed = config.randomizeSeedOnStart ? Guid.NewGuid().GetHashCode() : seed;
            seed = runSeed;
            Debug.Log($"[coast] seed = {runSeed}", this);

            _runner.config = config;
            _simulation = new HorrorCoasterSimulation();
            _proxy = new SwitchableSimulation { Inner = _simulation };
            _runner.SetSimulation(_proxy);
            if (!_runner.Begin(runSeed))
            {
                _simulation = null;
                _proxy = null;
                return false;
            }

            _stingTimes = null;
            StateVersion = 1;
            Ready = true;
            return true;
        }

        void Update()
        {
            if (!debugKeys || !Ready) return;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.nKey.wasPressedThisFrame) JumpToNextEvent();
            else if (kb.pKey.wasPressedThisFrame) JumpToPreviousEvent();
            else if (kb.bKey.wasPressedThisFrame) JumpToStart();
#endif
        }

        // ---- review jumps ----

        /// <summary>Jump to <see cref="JumpLeadSeconds"/> before the next unplayed event (the one after, while one is in its pre-quiet).</summary>
        public void JumpToNextEvent()
        {
            if (!Ready) return;
            EnsureSchedule();
            float now = _simulation.SimTime;
            for (int i = 0; i < _stingTimes.Length; i++)
            {
                float t = _stingTimes[i] - JumpLeadSeconds;
                if (t > now + 0.5f)
                {
                    JumpToTime(t, i);
                    return;
                }
            }
            Debug.Log("[coast] no further events to jump to.", this);
        }

        /// <summary>Jump to <see cref="JumpLeadSeconds"/> before the event we are in or just passed (press again to step back further).</summary>
        public void JumpToPreviousEvent()
        {
            if (!Ready) return;
            EnsureSchedule();
            float now = _simulation.SimTime;
            for (int i = _stingTimes.Length - 1; i >= 0; i--)
            {
                float t = _stingTimes[i] - JumpLeadSeconds;
                if (t < now - 1f)
                {
                    JumpToTime(Mathf.Max(0f, t), i);
                    return;
                }
            }
            JumpToStart();
        }

        public void JumpToStart()
        {
            if (!Ready) return;
            JumpToTime(0f, -1);
        }

        /// <summary>One headless pass over the whole ride to learn when each event stings. Timing never depends on the seed.</summary>
        void EnsureSchedule()
        {
            if (_stingTimes != null) return;
            var sim = new HorrorCoasterSimulation();
            sim.Initialize(seed, config);
            int n = sim.Events.Count;
            _stingTimes = new float[n];
            _stingKinds = new ScareKind[n];
            for (int i = 0; i < n; i++)
            {
                _stingTimes[i] = float.MaxValue;
                _stingKinds[i] = sim.Events[i].kind;
            }
            float dt = config.fixedTimestep;
            int guard = config.MaxTicks;
            int seen = 0;
            while (!sim.IsComplete && guard-- > 0)
            {
                sim.Tick(dt);
                while (seen < sim.NextEventIndex && seen < n)
                {
                    _stingTimes[seen] = sim.SimTime;
                    seen++;
                }
            }
        }

        void JumpToTime(float targetTime, int eventIndex)
        {
            float dt = config.fixedTimestep;
            HorrorCoasterSimulation sim;
            if (_runner.IsRunning)
            {
                sim = new HorrorCoasterSimulation();
                sim.Initialize(seed, config);
            }
            else
            {
                // The ride had finished and the runner stopped: restart it on a fresh simulation (Begin initialises it).
                sim = new HorrorCoasterSimulation();
                _proxy.Inner = sim;
                if (!_runner.Begin(seed)) return;
            }

            int guard = config.MaxTicks;
            while (!sim.IsComplete && sim.SimTime + dt * 0.5f < targetTime && guard-- > 0)
            {
                sim.Tick(dt);
            }

            _proxy.Inner = sim;
            _simulation = sim;
            StateVersion++;

            if (eventIndex >= 0 && eventIndex < _stingKinds.Length)
            {
                Debug.Log($"[coast] jumped to event {eventIndex} ({_stingKinds[eventIndex]}) at d={sim.Distance:0.0} (t={sim.SimTime:0.0} s)", this);
            }
            else
            {
                Debug.Log($"[coast] jumped to the start at d={sim.Distance:0.0} (t={sim.SimTime:0.0} s)", this);
            }
        }
    }
}
