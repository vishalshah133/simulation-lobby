using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall.Kernel
{
    /// <summary>Output of laying out one world on one retry attempt.</summary>
    internal sealed class FreefallWorldBuild
    {
        public readonly List<FreefallHit> hits = new List<FreefallHit>();

        /// <summary>Per voice, the ordered arcs this world contributes (intro, hit arcs, exit).</summary>
        public List<FreefallSeg>[] segs;

        public float yTop;
        public float yBottom;

        /// <summary>Voice 0's velocity on arrival at the exit target (next world's first pad, or the core).</summary>
        public Vector3 arrivalVelocity;

        public float clearance;
        public int worstHit;

        /// <summary>Where the next world's first pad ended up (this attempt's exit target).</summary>
        public Vector3 nextFirstHit;
    }

    /// <summary>
    /// Cross-world layout fixed before any world is built: absolute start times and heights, where each world's
    /// first pad is (independent of retries), and where the run ends.
    /// </summary>
    internal sealed class FreefallLayout
    {
        public FreefallConfig config;
        public FreefallWorldPlan[] plans;
        public double[] firstHitTime;
        public float[] y0;
        public Vector3[] firstHit;
        public double coreTime;
        public Vector3 core;
        public int maxVoices;
    }

    /// <summary>Turns a world plan plus a winding angle step into pad positions, arcs and hits.</summary>
    internal static class FreefallWorldBuilder
    {

        /// <summary>Canon voices hop to a waypoint this many seconds after their last hit, this far sideways.</summary>
        static readonly float[] ExitWaypointDistances = { 1.2f, 2.2f, 3.4f };
        static readonly float[] ExitHopFractions = { 1f, 0.6f, 0.35f };
        static readonly double[] ExitWaypointAngles = { 0, 40, -40, 80, -80, 120, -120, 160, 180 };

        /// <summary>Degrees added to the exit angle, in the order tried (the first that clears the earlier pads wins).</summary>
        static readonly double[] ExitAngleSteps = { 0, 25, -25, 50, -50, 80, -80, 110, -110, 140, -140 };


        public static void PlanLayout(FreefallLayout layout)
        {
            FreefallConfig cfg = layout.config;
            int count = cfg.worlds.Count;
            layout.firstHitTime = new double[count];
            layout.y0 = new float[count];
            layout.firstHit = new Vector3[count];

            double t = cfg.introDropSeconds;
            float y = 0f;
            for (int w = 0; w < count; w++)
            {
                FreefallWorldPlan plan = layout.plans[w];
                float g = plan.world.gravity;
                float tr = cfg.transitionSeconds;
                if (w > 0)
                {
                    FreefallWorldPlan prev = layout.plans[w - 1];
                    t = layout.firstHitTime[w - 1] + prev.LastTRel + tr;
                    y = layout.y0[w - 1] + prev.relY[prev.count - 1] - FallDrop(cfg);
                }

                layout.firstHitTime[w] = t;
                layout.y0[w] = y;
                float r0 = plan.radius[0];
                layout.firstHit[w] = new Vector3(
                    r0 * (float)Math.Cos(plan.theta0), y, r0 * (float)Math.Sin(plan.theta0));
                layout.maxVoices = Math.Max(layout.maxVoices, plan.VoiceCount);
            }

            int last = count - 1;
            FreefallWorldPlan lastPlan = layout.plans[last];
            layout.coreTime = layout.firstHitTime[last] + lastPlan.LastTRel + cfg.transitionSeconds;
            float coreY = layout.y0[last] + lastPlan.relY[lastPlan.count - 1] - FallDrop(cfg);
            layout.core = new Vector3(0f, coreY, 0f);
        }

        /// <summary>
        /// Gravity of the fall between worlds. The well between worlds is always standard gravity, whatever the
        /// worlds either side use: a low-gravity world could not fall a useful distance in transitionSeconds.
        /// </summary>
        public const float TransitionGravity = 9.81f;

        /// <summary>The marble leaves a world's last pad at least this long before it is back at the pad's height.</summary>
        const float MinExitHopSeconds = 1.4f;

        /// <summary>
        /// Upward speed the marble leaves each world's last pad with. It must leave the pad (vOut.y &gt; 0) or it
        /// would pass through it, and the hop must last long enough to carry it clear of the pad's edge before it
        /// falls back to the pad's height: 1.4 s of hop at standard gravity is 6.9 m/s and a 2.4 m apex. The world
        /// gap can raise it, never lower it.
        /// </summary>
        public static float ExitLaunchSpeed(FreefallConfig cfg)
        {
            return Math.Max(cfg.worldGap / cfg.transitionSeconds, 0.5f * TransitionGravity * MinExitHopSeconds);
        }

        /// <summary>Height lost across a transition: a free fall of transitionSeconds minus what the exit hop buys.</summary>
        static float FallDrop(FreefallConfig cfg)
        {
            float tr = cfg.transitionSeconds;
            return 0.5f * TransitionGravity * tr * tr - ExitLaunchSpeed(cfg) * tr;
        }

        /// <summary>
        /// Lay out world <paramref name="w"/> with the winding rate nudged by <paramref name="extraDegreesPerSecond"/>.
        /// </summary>
        public static FreefallWorldBuild Build(FreefallLayout layout, int w, float extraDegreesPerSecond, Vector3 incomingVelocity)
        {
            FreefallConfig cfg = layout.config;
            FreefallWorldPlan plan = layout.plans[w];
            FreefallWorld world = plan.world;
            int count = plan.count;
            int worldCount = cfg.worlds.Count;
            bool isLast = w == worldCount - 1;

            float g = world.gravity;
            float introT = cfg.introDropSeconds;

            // Exit target: the next world's first pad, or the core. The fall there uses standard gravity.
            double targetTime = isLast ? layout.coreTime : layout.firstHitTime[w + 1];
            float gDest = TransitionGravity;

            // --- Voice 0 pad positions (see FreefallPadLayout). ---
            // The next world's first pad sits across the well from this world's last pad, so the exit hop has
            // sideways travel to clear the pad. Chosen here (not in the layout plan) because it depends on where
            // this attempt put the last pad; the seeded start angle of the next world picks the base angle and a
            // short ordered list of alternatives is tried if the hop would climb through the pads just played.
            // Rotated copies of the pads (canon voices k = 1.. fly the same path turned k * 360 / voices degrees).
            int copies = world.canonVoices > 1 ? Math.Max(0, plan.VoiceCount - 1) : 0;
            var rotCos = new float[copies];
            var rotSin = new float[copies];
            for (int m = 0; m < copies; m++)
            {
                float rphi = (m + 1) * 2f * Mathf.PI / world.canonVoices;
                rotCos[m] = (float)Math.Cos(rphi);
                rotSin[m] = (float)Math.Sin(rphi);
            }

            double lastTime = layout.firstHitTime[w] + plan.tRel[count - 1];
            var absTimes = new double[count];
            for (int j = 0; j < count; j++)
            {
                absTimes[j] = layout.firstHitTime[w] + plan.tRel[j];
            }

            Vector3 target = layout.core;
            float SearchExit(Vector3[] padPos, Vector3[] padNormals, double[] t, Vector3 lastPad, Vector3 vInLast, out Vector3 bestTarget)
            {
                bestTarget = layout.core;
                if (isLast)
                {
                    FreefallSeg exitCore = FreefallSeg.Between(lastTime, lastPad, targetTime, layout.core, gDest);
                    return FreefallPadLayout.ScoreExit(padPos, padNormals, t, count - 1, exitCore, vInLast, cfg.marbleRadius, world.padRadius, world.gravity, rotCos, rotSin);
                }

                FreefallWorldPlan next = layout.plans[w + 1];
                // Across the well from the last pad: 120-240 degrees round, or for a canon exactly opposite (halfway
                // between the voices' pads, so no voice falls back onto its own).
                double offset = world.canonVoices > 1
                    ? Math.PI
                    : 2.0 * Math.PI / 3.0 * (1.0 + next.theta0 / (2.0 * Math.PI));
                double baseAngle = Math.Atan2(lastPad.z, lastPad.x) + plan.dir * offset;
                float r0 = next.radius[0];
                float best = float.NegativeInfinity;
                for (int k = 0; k < ExitAngleSteps.Length; k++)
                {
                    double angle = baseAngle + plan.dir * ExitAngleSteps[k] * Math.PI / 180.0;
                    var cand = new Vector3(r0 * (float)Math.Cos(angle), layout.y0[w + 1], r0 * (float)Math.Sin(angle));
                    FreefallSeg exitCand = FreefallSeg.Between(lastTime, lastPad, targetTime, cand, gDest);
                    float score = FreefallPadLayout.ScoreExit(padPos, padNormals, t, count - 1, exitCand, vInLast, cfg.marbleRadius, world.padRadius, world.gravity, rotCos, rotSin);
                    if (score > best)
                    {
                        best = score;
                        bestTarget = cand;
                    }

                    if (score >= 1f)
                    {
                        break;
                    }
                }

                return best;
            }

            Vector3[] pos = FreefallPadLayout.Lay(
                layout, w, extraDegreesPerSecond, incomingVelocity,
                (padPos, padNormals, t, lastPad, vInLast) => SearchExit(padPos, padNormals, t, lastPad, vInLast, out _),
                out Vector3[] finalNormals, out Vector3 lastVelocityIn);
            SearchExit(pos, finalNormals, absTimes, pos[count - 1], lastVelocityIn, out target);

            var build = new FreefallWorldBuild();
            build.segs = new List<FreefallSeg>[plan.VoiceCount];

            float yTop = layout.y0[w];
            Vector3 voice0LastPos = pos[count - 1];
            FreefallSeg exit0 = default;
            double lastTime0 = layout.firstHitTime[w] + plan.LastTRel;

            for (int k = 0; k < plan.VoiceCount; k++)
            {
                int n = plan.voiceHitCount[k];
                double shift = plan.voiceShift[k];
                float phi = world.canonVoices > 1 ? k * 2f * Mathf.PI / world.canonVoices : 0f;
                float cs = (float)Math.Cos(phi);
                float sn = (float)Math.Sin(phi);

                var times = new double[n];
                var ps = new Vector3[n];
                for (int m = 0; m < n; m++)
                {
                    times[m] = layout.firstHitTime[w] + plan.tRel[m] + shift;
                    Vector3 p = pos[m];
                    ps[m] = new Vector3(p.x * cs - p.z * sn, p.y, p.x * sn + p.z * cs);
                }

                var chain = new List<FreefallSeg>(n + 3);
                bool intro = k > 0 || w == 0;
                if (intro)
                {
                    Vector3 start = ps[0] + new Vector3(0f, 0.5f * g * introT * introT, 0f);
                    chain.Add(FreefallSeg.Between(times[0] - introT, start, times[0], ps[0], g));
                    yTop = Math.Max(yTop, start.y);
                }

                for (int m = 0; m + 1 < n; m++)
                {
                    FreefallSeg arc = FreefallSeg.Between(times[m], ps[m], times[m + 1], ps[m + 1], g);
                    chain.Add(arc);
                    if (k == 0 && arc.v0.y > 0f)
                    {
                        yTop = Math.Max(yTop, arc.p0.y + arc.v0.y * arc.v0.y / (2f * g));
                    }
                }

                FreefallSeg exit;
                if (k == 0)
                {
                    exit = FreefallSeg.Between(times[n - 1], ps[n - 1], targetTime, target, gDest);
                    exit0 = exit;
                }
                else
                {
                    // Every voice ends the transition at the same target, arriving with voice 0, so the canon
                    // marbles draw together and the presenter can fade them into one on contact. A canon voice
                    // first hops up and sideways to a waypoint (so it clears its own pad and the pads around it
                    // whatever its drop below voice 0), then falls to the target. The sideways direction is the
                    // first of a short list that clears every voice's pads.
                    Vector3 pk = ps[n - 1];
                    double tk = times[n - 1];
                    float hopMax = ExitLaunchSpeed(cfg);
                    Vector3 vInK = chain[n - 1].EndVelocity; // k > 0 always has an intro arc, so this arc ends at the last hit
                    var toward = new Vector3(target.x - pk.x, 0f, target.z - pk.z);
                    double baseAngle = toward.magnitude >= 0.8f ? Math.Atan2(toward.z, toward.x) : Math.Atan2(pk.z, pk.x);

                    var norm0 = new Vector3[count];
                    Array.Copy(finalNormals, norm0, count);
                    norm0[count - 1] = FreefallPadLayout.Normal(lastVelocityIn, exit0.v0);

                    float bestScore = float.NegativeInfinity;
                    FreefallSeg bestFirst = default;
                    FreefallSeg bestSecond = default;
                    for (int h = 0; h < ExitHopFractions.Length && bestScore < 1f; h++)
                    {
                        // The waypoint is the hop's apex: the marble goes up and sideways, then drops to the target.
                        float hop = hopMax * ExitHopFractions[h];
                        float tw = Math.Min(hop / gDest, (float)(0.5 * (targetTime - tk)));
                        Vector3 lift = new Vector3(0f, hop * tw - 0.5f * gDest * tw * tw, 0f);
                        for (int d = 0; d < ExitWaypointDistances.Length && bestScore < 1f; d++)
                        {
                            for (int ai = 0; ai < ExitWaypointAngles.Length && bestScore < 1f; ai++)
                            {
                                double angle = baseAngle + ExitWaypointAngles[ai] * Math.PI / 180.0;
                                Vector3 waypoint = pk + new Vector3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle)) * ExitWaypointDistances[d] + lift;
                                FreefallSeg first = FreefallSeg.Between(tk, pk, tk + tw, waypoint, gDest);
                                FreefallSeg second = FreefallSeg.Between(tk + tw, waypoint, targetTime, target, gDest);
                                float score = CanonExitScore(pos, norm0, k, n - 1, phi, copies, rotCos, rotSin, first, second, vInK, cfg.marbleRadius, world.padRadius);
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestFirst = first;
                                    bestSecond = second;
                                }
                            }
                        }
                    }

                    chain.Add(bestFirst);
                    exit = bestSecond;
                }

                chain.Add(exit);

                build.segs[k] = chain;

                // Hits: the velocity in is the end of the arc before, the velocity out is the start of the arc after.
                int offset = intro ? 1 : 0;
                for (int m = 0; m < n; m++)
                {
                    Vector3 vIn = m == 0
                        ? (intro ? chain[0].EndVelocity : incomingVelocity)
                        : chain[m - 1 + offset].EndVelocity;
                    Vector3 vOut = chain[m + offset].v0;
                    build.hits.Add(MakeHit(plan, cfg, w, k, m, times[m], ps[m], vIn, vOut,
                        isWorldFinal: k == 0 && m == count - 1, isRunFinal: false));
                }
            }

            build.yTop = yTop;
            build.yBottom = voice0LastPos.y;
            build.arrivalVelocity = exit0.EndVelocity;
            build.nextFirstHit = target;

            if (isLast)
            {
                // The core hit: the marble lands flat on the white core. Reported against the last note's pitch.
                int j = count - 1;
                build.hits.Add(new FreefallHit(
                    (float)layout.coreTime, 0, w, plan.noteIndex[j],
                    world.melody.FrequencyAt(plan.noteIndex[j]), 3f, plan.pitch01[j],
                    layout.core, Vector3.up, build.arrivalVelocity.magnitude,
                    isWorldFinal: false, isRunFinal: true));
            }

            build.clearance = FreefallClearance.Measure(build, world.padRadius, cfg.marbleRadius, world.wellRadius, out build.worstHit);
            return build;
        }

        /// <summary>
        /// Acceptance score for a canon voice's exit (its last pad's bounce, then the clearance of both arcs from
        /// every voice's pads). 1 or more passes. The voice's own last pad uses the exit's normal, not voice 0's.
        /// </summary>
        static float CanonExitScore(
            Vector3[] pos, Vector3[] norm0, int voice, int lastPadIndex, float phi, int copies,
            float[] rotCos, float[] rotSin, FreefallSeg first, FreefallSeg second, Vector3 vIn, float mr, float pr)
        {
            float cosK = (float)Math.Cos(phi);
            float sinK = (float)Math.Sin(phi);
            Vector3 contact = FreefallPadLayout.RotateBy(pos[lastPadIndex], cosK, sinK);
            Vector3 n = FreefallPadLayout.Normal(vIn, first.v0);
            float bounce = Math.Min(
                -(vIn.x * n.x + vIn.y * n.y + vIn.z * n.z), first.v0.x * n.x + first.v0.y * n.y + first.v0.z * n.z) / 0.15f;
            if (bounce < 1f)
            {
                return bounce;
            }

            float minGap = float.MaxValue;
            float reach = pr + mr + 0.1f;
            const float step = 1f / 120f;
            for (int s = 0; s < 2; s++)
            {
                FreefallSeg seg = s == 0 ? first : second;
                int steps = Math.Max(1, (int)Math.Ceiling((seg.t1 - seg.t0) / step));
                for (int i = 0; i <= steps; i++)
                {
                    double t = Math.Min(seg.t0 + i * (double)step, seg.t1);
                    Vector3 p = seg.PositionAt(t);

                    if (!(s == 0 && t - seg.t0 < FreefallClearance.OwnHitWindow))
                    {
                        minGap = Math.Min(minGap, DiscGap(p, contact, n, mr, pr));
                    }

                    for (int idx = 0; idx < pos.Length; idx++)
                    {
                        if (Math.Abs(pos[idx].y - p.y) > reach)
                        {
                            continue;
                        }

                        for (int m = -1; m < copies; m++)
                        {
                            // m == -1 is voice 0's own copy; m >= 0 are the voices with rotation index m + 1.
                            int rotVoice = m + 1;
                            if (rotVoice == voice && idx == lastPadIndex)
                            {
                                continue;
                            }

                            Vector3 c = pos[idx];
                            Vector3 cn = norm0[idx];
                            if (m >= 0)
                            {
                                c = FreefallPadLayout.RotateBy(c, rotCos[m], rotSin[m]);
                                cn = FreefallPadLayout.RotateBy(cn, rotCos[m], rotSin[m]);
                            }

                            minGap = Math.Min(minGap, DiscGap(p, c, cn, mr, pr));
                        }
                    }
                }
            }

            return Math.Min(bounce, minGap / 0.02f);
        }

        static float DiscGap(Vector3 p, Vector3 contact, Vector3 normal, float mr, float pr)
        {
            float cx = contact.x - normal.x * mr;
            float cy = contact.y - normal.y * mr;
            float cz = contact.z - normal.z * mr;
            return FreefallClearance.DiscDistance(p.x - cx, p.y - cy, p.z - cz, normal.x, normal.y, normal.z, pr) - mr;
        }

        static FreefallHit MakeHit(
            FreefallWorldPlan plan, FreefallConfig cfg, int w, int voice, int m, double time, Vector3 position,
            Vector3 vIn, Vector3 vOut, bool isWorldFinal, bool isRunFinal)
        {
            Vector3 dv = vOut - vIn;
            float impulse = dv.magnitude;
            Vector3 normal = impulse > 1e-5f ? dv / impulse : Vector3.up;
            return new FreefallHit(
                (float)time, voice, w, plan.noteIndex[m],
                plan.world.melody.FrequencyAt(plan.noteIndex[m]), plan.ringSeconds[m], plan.pitch01[m],
                position, normal, impulse, isWorldFinal, isRunFinal);
        }
    }
}
