using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Simulations.Timer.EditorTools
{
    /// <summary>
    /// Offline check of the marble-run timer acceptance criteria, no Play mode: steps the built scene's
    /// <see cref="TimerSimulation"/> and physics by hand for a few seeds and logs the clock, fill, travel time and speed numbers.
    /// </summary>
    /// <remarks>
    /// Physics runs in <see cref="SimulationMode.Script"/> and is restored afterwards, and the pool and hopper gate are put back as found.
    /// </remarks>
    public static class TimerDurationTest
    {
        const int SeedCount = 3;
        const float SpeedSampleSeconds = 0.5f;
        const float StallSpeed = 0.2f;
        const float StallSeconds = 3f;
        const float MeanSpeedMin = 6f;
        const float MeanSpeedMax = 9f;
        const float FillMin = 0.8f;

        sealed class Row
        {
            public int seed;
            public int clockZeroTickError;
            public bool completed;
            public int released;
            public int collected;
            public float fillAtZero;
            public int maxOnTrack;
            public float meanTravel;
            public float p95Travel;
            public float meanSpeed;
            public int respawns;
            public int stalled;
        }

        struct BodyState
        {
            public Rigidbody body;
            public Vector3 position;
            public Quaternion rotation;
            public bool kinematic;
            public bool active;
        }

        [MenuItem("Simulation Lobby/3D Formats/Marble Run Timer (timer)/Stream Test", false, 372)]
        [MenuItem("Simulation Lobby/Tools/Timer Stream Test", false, 961)]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[timer-test] Exit play mode first: the test steps physics itself.");
                return;
            }

            var sim = Object.FindFirstObjectByType<TimerSimulation>();
            if (sim == null || sim.Config == null || sim.Path == null || sim.CourseLayout == null || sim.TotalMarbles == 0)
            {
                Debug.LogError("[timer-test] Open the built Marble Run Timer scene (Simulation Lobby > 3D Formats > Marble Run Timer) first.");
                return;
            }

            if (!sim.Config.Validate(out string error))
            {
                Debug.LogError($"[timer-test] Config invalid: {error}");
                return;
            }

            var snapshot = new List<BodyState>();
            for (int i = 0; i < sim.TotalMarbles; i++)
            {
                Rigidbody body = sim.MarbleBody(i);
                if (body == null)
                {
                    Debug.LogError("[timer-test] The pool has a missing body. Rebuild the scene.");
                    return;
                }

                snapshot.Add(new BodyState
                {
                    body = body,
                    position = body.position,
                    rotation = body.rotation,
                    kinematic = body.isKinematic,
                    active = body.gameObject.activeSelf
                });
            }

            bool gateActive = sim.HopperGate != null && sim.HopperGate.gameObject.activeSelf;
            SimulationMode previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            var rows = new List<Row>();
            try
            {
                for (int s = 0; s < SeedCount; s++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Timer Stream Test", $"Seed {s + 1} of {SeedCount}", s / (float)SeedCount))
                    {
                        Debug.LogWarning($"[timer-test] Cancelled after {rows.Count} seeds.");
                        break;
                    }

                    rows.Add(RunSeed(sim, s + 1));
                    Restore(snapshot, sim, gateActive);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Physics.simulationMode = previousMode;
                Restore(snapshot, sim, gateActive);
            }

            Report(sim, rows);
        }

        static Row RunSeed(TimerSimulation sim, int seed)
        {
            float dt = sim.Config.fixedTimestep;
            int maxTicks = sim.Config.MaxTicks;
            sim.Initialize(seed, sim.Config);
            Physics.SyncTransforms();

            int n = sim.TotalMarbles;
            var lowSpeedTime = new float[n];
            var stalled = new bool[n];
            int sampleEvery = Mathf.Max(1, Mathf.RoundToInt(SpeedSampleSeconds / dt));
            float sampleDt = sampleEvery * dt;
            double speedSum = 0.0;
            long speedCount = 0;
            int zeroTick = -1;

            int ticks = 0;
            while (!sim.IsComplete && ticks < maxTicks)
            {
                sim.Tick(dt);
                Physics.Simulate(dt);
                ticks++;

                if (zeroTick < 0 && sim.Phase >= TimerPhase.Finale)
                {
                    zeroTick = ticks;
                }

                if (ticks % sampleEvery != 0)
                {
                    continue;
                }

                for (int i = 0; i < n; i++)
                {
                    if (!sim.MarbleOnTrack(i))
                    {
                        lowSpeedTime[i] = 0f;
                        continue;
                    }

                    float speed = sim.MarbleBody(i).linearVelocity.magnitude;
                    speedSum += speed;
                    speedCount++;
                    if (speed < StallSpeed)
                    {
                        lowSpeedTime[i] += sampleDt;
                        if (lowSpeedTime[i] > StallSeconds)
                        {
                            stalled[i] = true;
                        }
                    }
                    else
                    {
                        lowSpeedTime[i] = 0f;
                    }
                }
            }

            var row = new Row { seed = seed, completed = sim.IsComplete };
            row.clockZeroTickError = zeroTick < 0 ? int.MaxValue : zeroTick - (sim.HoldTicks + sim.DurationTicks);
            RunResultView(sim, row);
            row.meanSpeed = speedCount > 0 ? (float)(speedSum / speedCount) : 0f;
            foreach (bool s in stalled)
            {
                row.stalled += s ? 1 : 0;
            }

            return row;
        }

        static void RunResultView(TimerSimulation sim, Row row)
        {
            var result = sim.Result;
            row.released = Mathf.RoundToInt(result.GetMetric("released"));
            row.collected = Mathf.RoundToInt(result.GetMetric("collected"));
            row.fillAtZero = result.GetMetric("fill_at_zero");
            row.maxOnTrack = Mathf.RoundToInt(result.GetMetric("max_on_track"));
            row.meanTravel = result.GetMetric("mean_travel_s");
            row.p95Travel = result.GetMetric("p95_travel_s");
            row.respawns = Mathf.RoundToInt(result.GetMetric("respawns"));
        }

        static void Restore(List<BodyState> snapshot, TimerSimulation sim, bool gateActive)
        {
            foreach (BodyState state in snapshot)
            {
                Rigidbody body = state.body;
                body.gameObject.SetActive(true);
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = state.position;
                body.rotation = state.rotation;
                body.transform.SetPositionAndRotation(state.position, state.rotation);
                body.isKinematic = state.kinematic;
                var trail = body.GetComponent<TrailRenderer>();
                if (trail != null)
                {
                    trail.Clear();
                }

                body.gameObject.SetActive(state.active);
            }

            if (sim.HopperGate != null)
            {
                sim.HopperGate.gameObject.SetActive(gateActive);
            }

            Physics.SyncTransforms();
        }

        static void Report(TimerSimulation sim, List<Row> rows)
        {
            if (rows.Count == 0)
            {
                return;
            }

            var text = new StringBuilder();
            text.AppendLine($"[timer-test] {rows.Count} seeds, {sim.TotalMarbles} marbles, clock {sim.Duration:0}s, course {sim.CourseLength:0.0} m, dt {sim.Config.fixedTimestep:0.0000}s");
            text.AppendLine("seed | zero err ticks | released | collected | fill@0 | max on track | mean travel s | p95 travel s | mean m/s | respawns | stalled");

            bool zeroOk = true;
            bool speedOk = true;
            bool fillOk = true;
            bool stallOk = true;
            bool respawnOk = true;
            bool complete = true;
            foreach (Row row in rows)
            {
                string zero = row.clockZeroTickError == int.MaxValue ? "NEVER" : row.clockZeroTickError.ToString("+0;-0;0");
                text.AppendLine($"{row.seed,4} | {zero,14} | {row.released,8} | {row.collected,9} | {row.fillAtZero,6:0.00} | {row.maxOnTrack,12} | " +
                                $"{row.meanTravel,13:0.0} | {row.p95Travel,12:0.0} | {row.meanSpeed,8:0.0} | {row.respawns,8} | {row.stalled,7}");

                zeroOk &= row.clockZeroTickError == 0;
                speedOk &= row.meanSpeed >= MeanSpeedMin && row.meanSpeed <= MeanSpeedMax;
                fillOk &= row.fillAtZero >= FillMin && row.fillAtZero <= 1f;
                stallOk &= row.stalled == 0;
                respawnOk &= row.respawns == 0;
                complete &= row.completed;
            }

            Debug.Log(text.ToString());

            Verdict(zeroOk, "clock reaches 0:00 on the exact tick on every seed", "clock-zero tick error is not 0", true);
            Verdict(complete, "every seed completed", "a seed hit the tick cap before completing", true);
            Verdict(speedOk, $"mean marble speed within {MeanSpeedMin:0}-{MeanSpeedMax:0} m/s", $"mean marble speed outside {MeanSpeedMin:0}-{MeanSpeedMax:0} m/s: tune slope or damping", false);
            Verdict(fillOk, $"fill at 0:00 within {FillMin:0.0}-1.0", $"fill at 0:00 outside {FillMin:0.0}-1.0: check expectedTravelSeconds against the mean travel time", false);
            Verdict(stallOk, "no stalled marbles", "marbles stalled on the track (speed < 0.2 m/s for over 3 s)", false);
            Verdict(respawnOk, "no respawns", "marbles left the track and were respawned", false);
        }

        static void Verdict(bool ok, string pass, string problem, bool hard)
        {
            if (ok)
            {
                Debug.Log($"[timer-test] PASS {pass}.");
            }
            else if (hard)
            {
                Debug.LogError($"[timer-test] FAIL {problem}.");
            }
            else
            {
                Debug.LogWarning($"[timer-test] WARN {problem}.");
            }
        }
    }
}
