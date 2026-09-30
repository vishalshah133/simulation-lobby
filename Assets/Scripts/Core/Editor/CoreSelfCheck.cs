using System.Text;
using SimulationLobby.Core;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Core.EditorTools
{
    /// <summary>
    /// Proves the deterministic core in isolation, with no scene, no physics and no format built yet.
    /// Menu: <c>Simulation Lobby ▸ Verify Deterministic Core</c>.
    /// </summary>
    /// <remarks>
    /// This covers the part of determinism that is ours: seeded RNG, tick counting, result
    /// serialization, and the runner's stop conditions. It cannot cover Unity physics reproducibility
    /// — that needs a real scene and is verified for the first time with <c>esc-001</c>.
    /// </remarks>
    static class CoreSelfCheck
    {
        [MenuItem("Simulation Lobby/Verify Deterministic Core")]
        static void Run()
        {
            var log = new StringBuilder("Deterministic core self-check\n");
            bool passed = true;

            SmokeConfig config = ScriptableObject.CreateInstance<SmokeConfig>();
            config.name = "SmokeConfig";
            config.fixedTimestep = 1f / 60f;
            config.maxDurationSeconds = 30f;

            try
            {
                // 1. Same seed twice → byte-identical result.
                DeterminismVerifier.Comparison repeat =
                    DeterminismVerifier.Compare(() => new SmokeSimulation(), 12345, config);
                passed &= Report(log, "same seed twice is byte-identical", repeat.identical, repeat.reason);

                // 2. Different seeds → different runs. Guards against a result so empty that any two
                //    runs match, which would make check 1 pass for the wrong reason.
                RunResult a = DeterminismVerifier.RunHeadless(new SmokeSimulation(), 12345, config);
                RunResult b = DeterminismVerifier.RunHeadless(new SmokeSimulation(), 999, config);
                bool seedsDiffer = a != null && b != null && a.ToJson(false) != b.ToJson(false);
                passed &= Report(log, "different seeds produce different runs", seedsDiffer,
                    "two seeds gave identical output — the result is not capturing the run");

                // 3. JSON round-trips, since seed scans and baselines go through it.
                bool roundTrips = a != null && RunResult.FromJson(a.ToJson(false)).ToJson(false) == a.ToJson(false);
                passed &= Report(log, "RunResult survives a JSON round-trip", roundTrips,
                    "serialization is lossy — a committed baseline would not compare");

                // 4. Timeout is enforced in ticks, not seconds of wall-clock.
                SmokeConfig shortConfig = ScriptableObject.CreateInstance<SmokeConfig>();
                shortConfig.name = "SmokeConfig_Short";
                shortConfig.fixedTimestep = 1f / 60f;
                shortConfig.maxDurationSeconds = 1f;
                shortConfig.threshold = float.MaxValue; // never finishes on its own
                RunResult timedOut = DeterminismVerifier.RunHeadless(new SmokeSimulation(), 7, shortConfig);
                bool timeoutOk = timedOut != null
                                 && timedOut.completionReason == CompletionReason.TimedOut
                                 && timedOut.tickCount == shortConfig.MaxTicks;
                passed &= Report(log, "runner times out at exactly MaxTicks", timeoutOk,
                    timedOut == null ? "no result" : $"{timedOut.completionReason} @ {timedOut.tickCount}, expected TimedOut @ {shortConfig.MaxTicks}");
                Object.DestroyImmediate(shortConfig);

                if (a != null)
                {
                    log.AppendLine().AppendLine("Sample result:").AppendLine(a.ToJson());
                }
            }
            finally
            {
                Object.DestroyImmediate(config);
            }

            if (passed)
            {
                Debug.Log("PASS — " + log);
            }
            else
            {
                Debug.LogError("FAIL — " + log);
            }
        }

        static bool Report(StringBuilder log, string label, bool ok, string reason)
        {
            log.AppendLine(ok ? $"  ok   {label}" : $"  FAIL {label}: {reason}");
            return ok;
        }

        /// <summary>Config for the self-check only. Not a format, deliberately not asset-creatable.</summary>
        class SmokeConfig : SimulationConfig
        {
            public float threshold = 8f;
            public float stepSize = 0.35f;
        }

        /// <summary>
        /// A random walk that stops when it drifts past a threshold. No physics and no GameObjects,
        /// so it isolates our own determinism from the engine's.
        /// </summary>
        class SmokeSimulation : ISimulation
        {
            SeededRandom _random;
            RunRecorder _recorder;
            SmokeConfig _config;
            Vector2 _position;

            public string FormatSlug => "smoke";
            public bool IsComplete { get; private set; }
            public RunResult Result => _recorder?.Result;

            public void Initialize(int seed, SimulationConfig config)
            {
                _config = (SmokeConfig)config;
                _random = new SeededRandom(seed);
                _recorder = new RunRecorder(FormatSlug, seed, config, _random);
                _position = _random.NextDirection2D() * 0.1f;
                IsComplete = false;
            }

            public void Tick(float fixedDelta)
            {
                _recorder.AdvanceTick();
                _position += _random.NextDirection2D() * _config.stepSize;

                float distance = _position.magnitude;
                if (_recorder.CurrentTick % 30 == 0)
                {
                    _recorder.LogEvent("distance", distance);
                }

                if (distance >= _config.threshold)
                {
                    _recorder.SetMetric("final_distance", distance);
                    _recorder.SetStandings(new[] { "walker" });
                    _recorder.Finish(CompletionReason.Finished);
                    IsComplete = true;
                }
            }
        }
    }
}
