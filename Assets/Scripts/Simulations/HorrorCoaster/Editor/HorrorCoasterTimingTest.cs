using System.Collections.Generic;
using System.Text;
using SimulationLobby.Core;
using UnityEditor;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Headless timing check for <c>coast</c>: runs the storyboard-default ride at the fixed step for several seeds and logs
    /// total time, running speeds, act starts, every event (time, distance, kind, riders) and the scare-spacing rules:
    /// stings at least 6 s apart, at least 10 s of real quiet before every big scare's pre-quiet, at least 4 s after a big
    /// sting before the next one, micro scares only in eligible acts and not in the last 25 s. Does not enter Play mode.
    /// </summary>
    public static class HorrorCoasterTimingTest
    {
        const float MinStingGap = 6f;
        const float QuietBeforeBig = 10f;
        const float QuietAfterBig = 4f;
        const float MinBigGap = 30f;
        const float LastSecondsNoMicro = 25f;
        const float MinRunningSpeed = 5.4f;
        static readonly int[] Seeds = { 1, 2, 3, 12345, 987654 };

        [MenuItem("Simulation Lobby/3D Formats/Coasters/Haunted: Ride Timing Test", false, 233)]
        public static void Run()
        {
            var cfg = ScriptableObject.CreateInstance<HorrorCoasterConfig>();
            try
            {
                HorrorCoasterDefaults.Apply(cfg);
                if (!cfg.Validate(out string error))
                {
                    Debug.LogError($"[coast] timing test: default config is invalid: {error}");
                    return;
                }

                var report = new StringBuilder();
                bool allOk = true;
                float firstTotal = -1f;
                for (int s = 0; s < Seeds.Length; s++)
                {
                    allOk &= RunSeed(cfg, Seeds[s], report, s == 0, ref firstTotal);
                }

                report.Insert(0, $"[coast] Ride Timing Test: {Seeds.Length} seeds, target {cfg.totalTargetSeconds:0} s " +
                                 $"(+/- 15), {(allOk ? "ALL OK" : "PROBLEMS FOUND")}\n");
                if (allOk) Debug.Log(report.ToString());
                else Debug.LogWarning(report.ToString());
            }
            finally
            {
                Object.DestroyImmediate(cfg);
            }
        }

        static bool IsEligibleAct(ActId a)
        {
            switch (a)
            {
                case ActId.TrickOrTreatStreet:
                case ActId.Graveyard:
                case ActId.DeadForest:
                case ActId.TunnelOfFaces:
                case ActId.Mansion:
                case ActId.Asylum:
                case ActId.FinalDrop:
                    return true;
                default:
                    return false;
            }
        }

        struct Sting
        {
            public int index;
            public float time, preStart;
            public ActId act;
            public int riders;
            public float speed;
        }

        static bool RunSeed(HorrorCoasterConfig cfg, int seed, StringBuilder report, bool verbose, ref float firstTotal)
        {
            bool ok = true;
            var sim = new HorrorCoasterSimulation();
            sim.Initialize(seed, cfg);
            float dt = cfg.fixedTimestep;
            int maxTicks = cfg.MaxTicks;
            IReadOnlyList<ScareEvent> events = sim.Events;

            var actStarts = new List<KeyValuePair<ActId, float>>();
            var stings = new List<Sting>();
            var preStart = new float[events.Count];
            for (int i = 0; i < preStart.Length; i++) preStart[i] = -1f;
            ActId lastAct = (ActId)(-1);
            int lastSting = -1;
            float maxSpeed = 0f, minSpeed = float.MaxValue, runTime = 0f, runDist = 0f, liftMin = 99f, liftMax = 0f;
            float motionStart = -1f;
            float minDrop = 0f, maxDrop = 0f;
            float railLen = sim.Rail.Length;
            var speedLog = new List<float>(30000);
            var distLog = new List<float>(30000);
            int tick = 0;
            while (!sim.IsComplete && tick < maxTicks)
            {
                sim.Tick(dt);
                tick++;
                float t = sim.SimTime;
                if (sim.Act != lastAct)
                {
                    lastAct = sim.Act;
                    actStarts.Add(new KeyValuePair<ActId, float>(lastAct, t));
                }
                if (sim.Phase == ScarePhase.PreQuiet && preStart[sim.CurrentEventIndex] < 0f) preStart[sim.CurrentEventIndex] = t;
                if (sim.Phase == ScarePhase.Sting && sim.CurrentEventIndex != lastSting)
                {
                    lastSting = sim.CurrentEventIndex;
                    stings.Add(new Sting
                    {
                        index = lastSting, time = t, preStart = preStart[lastSting] >= 0f ? preStart[lastSting] : t,
                        act = sim.Act, riders = sim.RidersLeft, speed = sim.Speed
                    });
                }
                if (sim.Speed > 0.01f && sim.Distance < railLen - 0.01f)
                {
                    if (motionStart < 0f) motionStart = t;
                    runTime += dt;
                    runDist += sim.Speed * dt;
                    maxSpeed = Mathf.Max(maxSpeed, sim.Speed);
                }
                speedLog.Add(sim.Speed);
                distLog.Add(sim.Distance);
                if (sim.OnLift && t - motionStart > 3f)
                {
                    liftMin = Mathf.Min(liftMin, sim.Speed);
                    liftMax = Mathf.Max(liftMax, sim.Speed);
                }
            }

            float total = sim.SimTime;
            if (!sim.IsComplete)
            {
                ok = false;
                report.AppendLine($"seed {seed}: DID NOT COMPLETE in {total:0.0} s (maxDurationSeconds {cfg.maxDurationSeconds:0})");
            }
            if (Mathf.Abs(total - cfg.totalTargetSeconds) > 15f)
            {
                ok = false;
                report.AppendLine($"seed {seed}: VIOLATION ride time {total:0.0} s is outside {cfg.totalTargetSeconds:0} +/- 15 s");
            }
            if (firstTotal < 0f) firstTotal = total;
            else if (Mathf.Abs(firstTotal - total) > 0.01f)
            {
                ok = false;
                report.AppendLine($"seed {seed}: VIOLATION ride time {total:0.00} differs from seed {Seeds[0]} ({firstTotal:0.00}); the seed must not change timing");
            }
            float avg = runTime > 0f ? runDist / runTime : 0f;
            if (avg < 9f)
            {
                ok = false;
                report.AppendLine($"seed {seed}: VIOLATION average running speed {avg:0.0} m/s is below 9");
            }

            // Slowest moment outside the allowed slow-downs: pull-out, the ease into a big sting (+ recovery), the last 20 m.
            float sampleDt = dt;
            for (int i = 0; i < speedLog.Count; i++)
            {
                float t = (i + 1) * sampleDt;
                float v = speedLog[i];
                if (v <= 0.01f) continue;
                if (t < motionStart + 3f) continue;
                bool allowed = false;
                for (int k = 0; k < stings.Count && !allowed; k++)
                {
                    if (!HorrorCoasterSimulation.IsBigScare(events[stings[k].index].kind)) continue;
                    if (t > stings[k].time - 1.7f && t < stings[k].time + 3f) allowed = true;
                }
                if (allowed) continue;
                float dist = distLog[i];
                if (dist > railLen - 25f) continue;
                minSpeed = Mathf.Min(minSpeed, v);
            }
            if (minSpeed < MinRunningSpeed)
            {
                ok = false;
                report.AppendLine($"seed {seed}: VIOLATION running speed fell to {minSpeed:0.0} m/s outside an allowed slow-down");
            }

            if (stings.Count != events.Count)
            {
                ok = false;
                report.AppendLine($"seed {seed}: VIOLATION {stings.Count} of {events.Count} events fired");
            }

            // Spacing rules.
            int micros = 0;
            var kindCount = new int[8];
            float eligibleSeconds = 0f;
            for (int a = 0; a < actStarts.Count; a++)
            {
                float end = a + 1 < actStarts.Count ? actStarts[a + 1].Value : total;
                if (IsEligibleAct(actStarts[a].Key)) eligibleSeconds += Mathf.Min(end, total - LastSecondsNoMicro) - actStarts[a].Value;
            }
            for (int i = 0; i < stings.Count; i++)
            {
                Sting a = stings[i];
                ScareEvent ea = events[a.index];
                bool aMicro = ea.kind.IsMicro();
                bool aBig = HorrorCoasterSimulation.IsBigScare(ea.kind);
                if (aMicro)
                {
                    micros++;
                    kindCount[(int)ea.kind - (int)ScareKind.SpiderDrop]++;
                    if (!IsEligibleAct(a.act) || a.time > total - LastSecondsNoMicro)
                    {
                        ok = false;
                        report.AppendLine($"seed {seed}: VIOLATION micro {ea.kind} at {a.time:0.0} s is in {a.act} / the last {LastSecondsNoMicro:0} s");
                    }
                    if (ea.takesRider || ea.preQuietSeconds < 0.4f || ea.preQuietSeconds > 0.9f || !string.IsNullOrEmpty(ea.cueCaption))
                    {
                        ok = false;
                        report.AppendLine($"seed {seed}: VIOLATION micro {ea.kind} at {a.time:0.0} s has takesRider/preQuiet/caption out of spec");
                    }
                }
                if (i + 1 < stings.Count)
                {
                    Sting b = stings[i + 1];
                    ScareEvent eb = events[b.index];
                    float gap = b.time - a.time;
                    if (gap < MinStingGap)
                    {
                        ok = false;
                        report.AppendLine($"seed {seed}: VIOLATION {eb.kind}@{b.time:0.0} is {gap:0.0} s after {ea.kind}@{a.time:0.0} (< {MinStingGap:0} s)");
                    }
                    if (aBig && gap < QuietAfterBig + 0f && eb.kind.IsMicro())
                    {
                        ok = false;
                        report.AppendLine($"seed {seed}: VIOLATION micro {eb.kind}@{b.time:0.0} only {gap:0.0} s after big {ea.kind}");
                    }
                    if (aBig && eb.kind.IsMicro() && gap < QuietAfterBig)
                    {
                        // already reported above
                    }
                    if (aBig && HorrorCoasterSimulation.IsBigScare(eb.kind) && gap < MinBigGap)
                    {
                        ok = false;
                        report.AppendLine($"seed {seed}: VIOLATION big {eb.kind}@{b.time:0.0} only {gap:0.0} s after big {ea.kind}");
                    }
                    if (HorrorCoasterSimulation.IsBigScare(eb.kind))
                    {
                        float quiet = b.preStart - a.time;
                        if (quiet < QuietBeforeBig)
                        {
                            ok = false;
                            report.AppendLine($"seed {seed}: VIOLATION only {quiet:0.0} s of quiet between {ea.kind}@{a.time:0.0} and the pre-quiet of {eb.kind}@{b.time:0.0} (< {QuietBeforeBig:0} s)");
                        }
                    }
                }
            }
            if (micros < 6 || micros > 12)
            {
                ok = false;
                report.AppendLine($"seed {seed}: VIOLATION {micros} micro scares (want 6-12)");
            }
            for (int k = 0; k < kindCount.Length; k++)
            {
                if (kindCount[k] < 1)
                {
                    ok = false;
                    report.AppendLine($"seed {seed}: VIOLATION micro kind {(ScareKind)((int)ScareKind.SpiderDrop + k)} appears {kindCount[k]} time(s), want >= 1");
                }
            }

            float perMicro = micros > 0 ? eligibleSeconds / micros : 0f;
            report.AppendLine($"seed {seed}: total {total:0.0} s, running {runTime:0.0} s, rail {railLen:0} m, avg {avg:0.0} / max {maxSpeed:0.0} / min {minSpeed:0.0} m/s " +
                              $"(outside allowed slow-downs), lift {liftMin:0.0}-{liftMax:0.0} m/s, riders left {sim.RidersLeft}, " +
                              $"{micros} micros in {eligibleSeconds:0} s eligible = 1 per {perMicro:0.0} s");
            if (verbose)
            {
                var acts = new StringBuilder("  acts:");
                for (int a = 0; a < actStarts.Count; a++)
                {
                    float end = a + 1 < actStarts.Count ? actStarts[a + 1].Value : total;
                    acts.Append($" {actStarts[a].Key}@{actStarts[a].Value:0.0}({end - actStarts[a].Value:0.0}s)");
                }
                report.AppendLine(acts.ToString());
                for (int i = 0; i < stings.Count; i++)
                {
                    Sting s = stings[i];
                    ScareEvent ev = events[s.index];
                    report.AppendLine($"  event {s.index,2} {ev.kind,-15} t={s.time,6:0.0} s  pre={s.preStart,6:0.0}  d={ev.distance,7:0.0} m  v={s.speed,4:0.0}  " +
                                      $"{(ev.kind.IsMicro() ? "micro" : (HorrorCoasterSimulation.IsBigScare(ev.kind) ? "BIG  " : "fake "))} takes={ev.takesRider}  riders after={s.riders}  {s.act}");
                }
            }
            else
            {
                var order = new StringBuilder("  riders by big event:");
                for (int i = 0; i < stings.Count; i++)
                {
                    if (events[stings[i].index].takesRider) order.Append(' ').Append(stings[i].riders);
                }
                report.AppendLine(order.ToString());
            }
            if (sim.RidersLeft != 1)
            {
                ok = false;
                report.AppendLine($"seed {seed}: VIOLATION ended with {sim.RidersLeft} riders, expected 1");
            }
            return ok;
        }
    }
}
