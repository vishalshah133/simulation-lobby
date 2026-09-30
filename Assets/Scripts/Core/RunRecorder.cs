using UnityEngine;

namespace SimulationLobby.Core
{
    /// <summary>
    /// Builds a <see cref="RunResult"/> as a run progresses. A format creates one in
    /// <see cref="ISimulation.Initialize"/>, ticks it, logs events, and returns
    /// <see cref="Result"/> from <see cref="ISimulation.Result"/>.
    /// </summary>
    /// <remarks>
    /// Owning the tick counter here is deliberate: events get their timestamp for free, and the
    /// count comes from simulation steps rather than frames, so it is identical whether the run was
    /// rendered at 1080p or scanned headless.
    /// </remarks>
    public sealed class RunRecorder
    {
        readonly RunResult _result;
        readonly SeededRandom _random;
        readonly int _eventLimit;

        /// <summary>Simulation steps taken so far.</summary>
        public int CurrentTick { get; private set; }

        /// <summary>Simulated seconds elapsed. Never wall-clock.</summary>
        public float SimulatedSeconds => CurrentTick * _result.fixedDeltaTime;

        /// <summary>The live result. Complete once <see cref="Finish"/> has been called.</summary>
        public RunResult Result
        {
            get
            {
                // Only track the live tick while running — once stopped, the final tick is frozen so
                // a stray extra step cannot change an already-stamped result.
                if (_result.completionReason == CompletionReason.Running)
                {
                    _result.tickCount = CurrentTick;
                    _result.simulatedSeconds = SimulatedSeconds;
                }

                _result.randomDrawCount = _random != null ? _random.DrawCount : 0;
                return _result;
            }
        }

        /// <param name="random">
        /// The run's generator. Its draw count lands on the result as a divergence tripwire.
        /// </param>
        /// <param name="eventLimit">
        /// Cap on logged events so a runaway format cannot allocate without bound during a long
        /// seed scan. Events past the cap are dropped, and a marker event records that.
        /// </param>
        public RunRecorder(string formatSlug, int seed, SimulationConfig config, SeededRandom random, int eventLimit = 4096)
        {
            _random = random;
            _eventLimit = Mathf.Max(1, eventLimit);
            _result = new RunResult
            {
                seed = seed,
                formatSlug = formatSlug,
                configName = config != null ? config.name : "(none)",
                unityVersion = Application.unityVersion,
                fixedDeltaTime = config != null ? config.fixedTimestep : Time.fixedDeltaTime,
                completionReason = CompletionReason.Running
            };
        }

        /// <summary>Call once at the top of <see cref="ISimulation.Tick"/>.</summary>
        public void AdvanceTick()
        {
            CurrentTick++;
        }

        /// <summary>
        /// Record something that happened, stamped with the current tick. Keep labels stable —
        /// they end up in the committed baseline a determinism check diffs against.
        /// </summary>
        public void LogEvent(string label, float value = 0f)
        {
            int count = _result.events.Count;
            if (count >= _eventLimit)
            {
                return;
            }

            if (count == _eventLimit - 1)
            {
                _result.events.Add(new RunEvent(CurrentTick, "event_limit_reached", _eventLimit));
                return;
            }

            _result.events.Add(new RunEvent(CurrentTick, label, value));
        }

        /// <summary>Set a named final number, replacing any earlier value for the same key.</summary>
        public void SetMetric(string key, float value)
        {
            for (int i = 0; i < _result.metrics.Count; i++)
            {
                if (_result.metrics[i].key == key)
                {
                    _result.metrics[i] = new RunMetric(key, value);
                    return;
                }
            }

            _result.metrics.Add(new RunMetric(key, value));
        }

        /// <summary>Append to the finish order — call as each contender resolves.</summary>
        public void AddStanding(string contender)
        {
            _result.standings.Add(contender);
        }

        /// <summary>Replace the standings wholesale, most significant first.</summary>
        public void SetStandings(System.Collections.Generic.IEnumerable<string> contenders)
        {
            _result.standings.Clear();
            if (contenders == null)
            {
                return;
            }

            foreach (string contender in contenders)
            {
                _result.standings.Add(contender);
            }
        }

        /// <summary>
        /// Stamp the outcome. A format calls this with <see cref="CompletionReason.Finished"/> the
        /// tick its win condition trips; the runner fills in timeouts and aborts itself.
        /// </summary>
        public void Finish(CompletionReason reason)
        {
            if (_result.completionReason != CompletionReason.Running)
            {
                return;
            }

            _result.MarkStopped(reason, CurrentTick);
        }
    }
}
