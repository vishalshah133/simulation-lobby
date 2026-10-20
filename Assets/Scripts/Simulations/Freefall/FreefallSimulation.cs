using SimulationLobby.Core;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall
{
    /// <summary>
    /// The clock shell for <c>fall</c>: builds the <see cref="FreefallSchedule"/> once in <c>Initialize</c> and
    /// then only counts ticks. There is no physics; every position the view needs comes from sampling the schedule.
    /// </summary>
    /// <remarks>
    /// Determinism: the schedule is pure maths over the seeded generator, and time is ticks times the fixed delta,
    /// so a run is identical whether it renders at 60 fps or runs headless. Presentation reads these members
    /// and never writes them.
    /// </remarks>
    [RequireComponent(typeof(SimulationRunner))]
    public sealed class FreefallSimulation : MonoBehaviour, ISimulation
    {
        RunRecorder _recorder;
        SeededRandom _random;
        float _dt;
        int _hitCursor;
        int _lastWorld = -1;
        bool _finished;

        public string FormatSlug => "fall";

        public bool IsComplete => Schedule != null && Time >= Schedule.Duration;

        public RunResult Result => _recorder?.Result;

        // --- Read by presentation. Observes; never writes. ---

        public FreefallConfig Config { get; private set; }

        /// <summary>Built in <see cref="Initialize"/>.</summary>
        public FreefallSchedule Schedule { get; private set; }

        /// <summary>The run's seed. Cosmetic only: it moves pads around but never changes note timing.</summary>
        public int Seed { get; private set; }

        /// <summary>Simulated seconds: ticks times the fixed delta.</summary>
        public float Time { get; private set; }

        /// <summary>Hits with time at or before <see cref="Time"/>.</summary>
        public int HitsPlayed { get; private set; }

        public int CurrentWorld { get; private set; }

        public void Initialize(int seed, SimulationConfig config)
        {
            Config = (FreefallConfig)config;
            Seed = seed;
            _dt = Config.fixedTimestep;
            _random = new SeededRandom(seed);
            _recorder = new RunRecorder(FormatSlug, seed, config, _random, 512);

            Schedule = FreefallSchedule.Build(Config, _random);

            Time = 0f;
            HitsPlayed = 0;
            CurrentWorld = 0;
            _hitCursor = 0;
            _lastWorld = -1;
            _finished = false;

            if (Schedule.Duration > Config.maxDurationSeconds)
            {
                Debug.LogError($"[fall] schedule lasts {Schedule.Duration:0.0}s but maxDurationSeconds is " +
                               $"{Config.maxDurationSeconds:0.0}s; the run would time out. Raise it or shorten the worlds.");
            }

            _recorder.SetMetric("worlds", Config.worlds.Count);
            _recorder.SetMetric("hits", Schedule.Hits.Count);
            _recorder.SetMetric("duration_seconds", Schedule.Duration);
            _recorder.SetMetric("max_voices", Schedule.MaxVoices);
            for (int w = 0; w < Schedule.Worlds.Count; w++)
            {
                _recorder.SetMetric($"world_{w + 1}_start_seconds", Schedule.Worlds[w].startTime);
            }

            Debug.Log(Summary());
            Advance();
        }

        public void Tick(float fixedDelta)
        {
            if (_finished || _recorder == null)
            {
                return;
            }

            _recorder.AdvanceTick();
            Time = _recorder.CurrentTick * _dt;
            Advance();

            if (IsComplete)
            {
                _finished = true;
                _recorder.AddStanding(Summary());
                _recorder.Finish(CompletionReason.Finished);
            }
        }

        string Summary()
        {
            return $"fall seed={Seed} worlds={Config.worlds.Count} hits={Schedule.Hits.Count} " +
                   $"duration={Schedule.Duration:0.0}s";
        }

        /// <summary>Move the hit cursor and world index up to the current time, logging world boundaries.</summary>
        void Advance()
        {
            var hits = Schedule.Hits;
            while (_hitCursor < hits.Count && hits[_hitCursor].time <= Time)
            {
                if (hits[_hitCursor].isWorldFinal)
                {
                    _recorder.LogEvent($"world_{hits[_hitCursor].world + 1}_final_hit", hits[_hitCursor].time);
                }

                _hitCursor++;
            }

            HitsPlayed = _hitCursor;

            CurrentWorld = Schedule.WorldAt(Time);
            if (CurrentWorld != _lastWorld)
            {
                _lastWorld = CurrentWorld;
                _recorder.LogEvent($"world_{CurrentWorld + 1}_start", Time);
            }
        }
    }
}
