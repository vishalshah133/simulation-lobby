using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.Freefall.Kernel
{
    /// <summary>
    /// The validation pass: walks every voice's path at 1/240 s and measures how close the marble's surface
    /// comes to any pad other than the one it is hitting.
    /// </summary>
    /// <remarks>
    /// A pad is a thin disc of radius <c>padRadius</c> whose face sits one marble radius behind the contact
    /// point (so the marble touches it exactly at the hit). Clearance is the distance from the marble centre to
    /// that disc minus the marble radius, so zero means touching. A point-to-pad-centre test would fail every
    /// time the marble leaves its own pad, so the disc shape is what is measured.
    /// </remarks>
    internal static class FreefallClearance
    {
        public const float SampleStep = 1f / 240f;

        /// <summary>The marble's own hit is exempt this long either side of contact.</summary>
        public const float OwnHitWindow = 0.06f;

        /// <summary>
        /// Smallest marble-surface-to-pad gap over the whole world, or a negative number when a hit point
        /// sits outside the well. <paramref name="worstHit"/> is the index (into build.hits) of the pad involved.
        /// </summary>
        public static float Measure(FreefallWorldBuild build, float padRadius, float marbleRadius, float wellRadius, out int worstHit)
        {
            worstHit = -1;
            var pads = new List<int>(build.hits.Count);
            for (int i = 0; i < build.hits.Count; i++)
            {
                if (!build.hits[i].isRunFinal)
                {
                    pads.Add(i);
                }
            }

            int n = pads.Count;
            var cx = new float[n];
            var cy = new float[n];
            var cz = new float[n];
            var nx = new float[n];
            var ny = new float[n];
            var nz = new float[n];
            var time = new float[n];
            var voice = new int[n];
            var owner = new int[n];
            var order = new float[n];

            float minGap = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                FreefallHit h = build.hits[pads[i]];
                cx[i] = h.position.x - h.normal.x * marbleRadius;
                cy[i] = h.position.y - h.normal.y * marbleRadius;
                cz[i] = h.position.z - h.normal.z * marbleRadius;
                nx[i] = h.normal.x;
                ny[i] = h.normal.y;
                nz[i] = h.normal.z;
                time[i] = h.time;
                voice[i] = h.voice;
                owner[i] = pads[i];
                order[i] = cy[i];

                float inside = wellRadius - padRadius - Mathf.Sqrt(h.position.x * h.position.x + h.position.z * h.position.z);
                if (inside < minGap)
                {
                    minGap = inside;
                    worstHit = pads[i];
                }
            }

            // Sort the pads by height so a sample only looks at pads within reach in Y.
            var idx = new int[n];
            for (int i = 0; i < n; i++)
            {
                idx[i] = i;
            }

            Array.Sort(order, idx);

            float reach = padRadius + marbleRadius + 0.05f;

            for (int v = 0; v < build.segs.Length; v++)
            {
                List<FreefallSeg> segs = build.segs[v];
                for (int s = 0; s < segs.Count; s++)
                {
                    FreefallSeg seg = segs[s];
                    int steps = (int)Math.Ceiling((seg.t1 - seg.t0) / SampleStep);
                    for (int step = 0; step <= steps; step++)
                    {
                        double t = Math.Min(seg.t0 + step * (double)SampleStep, seg.t1);
                        Vector3 p = seg.PositionAt(t);

                        int lo = LowerBound(order, p.y - reach);
                        for (int q = lo; q < n && order[q] <= p.y + reach; q++)
                        {
                            int i = idx[q];
                            if (voice[i] == v && Math.Abs(t - time[i]) < OwnHitWindow)
                            {
                                continue;
                            }

                            float gap = DiscDistance(p.x - cx[i], p.y - cy[i], p.z - cz[i], nx[i], ny[i], nz[i], padRadius) - marbleRadius;
                            if (gap < minGap)
                            {
                                minGap = gap;
                                worstHit = owner[i];
                            }
                        }
                    }
                }
            }

            return minGap;
        }

        static int LowerBound(float[] sorted, float value)
        {
            int lo = 0;
            int hi = sorted.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (sorted[mid] < value)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return lo;
        }

        /// <summary>Distance from point d (relative to the disc centre) to a disc with unit normal n and radius r.</summary>
        internal static float DiscDistance(float dx, float dy, float dz, float nx, float ny, float nz, float r)
        {
            float dn = dx * nx + dy * ny + dz * nz;
            float lat2 = dx * dx + dy * dy + dz * dz - dn * dn;
            float lat = Mathf.Sqrt(Mathf.Max(0f, lat2));
            if (lat <= r)
            {
                return Mathf.Abs(dn);
            }

            float edge = lat - r;
            return Mathf.Sqrt(edge * edge + dn * dn);
        }
    }
}
