using SimulationLobby.Shared.Coaster;
using UnityEngine;

namespace SimulationLobby.Simulations.TrippyCoaster.EditorTools
{
    /// <summary>Warns when a stretch of rail passes through another stretch's tunnel (a helix that crosses itself, a slab cut by a tunnel). Run at build time.</summary>
    static class TripClearance
    {
        static float Extent(TripWorldDef w)
        {
            switch (w.wall)
            {
                case TripWallShape.Slab: return 38f;
                case TripWallShape.None: return 12f;
                case TripWallShape.Funnel: return 16f;
                case TripWallShape.Star:
                case TripWallShape.Ripple: return w.wallRadius + w.wallRadius2 + 1f;
                default: return w.wallRadius + 1f;
            }
        }

        public static void Warn(ICoasterRail rail, TrippyCoasterConfig cfg)
        {
            const float step = 6f;
            int n = (int)(rail.Length / step);
            var pos = new Vector3[n];
            for (int i = 0; i < n; i++) rail.Sample(i * step, out pos[i], out _, out _);
            int bad = 0;
            for (int i = 0; i < n; i++)
            {
                TripWorldDef wi = cfg.worlds[cfg.WorldAt(i * step)];
                for (int j = i + 1; j < n; j++)
                {
                    if ((j - i) * step < 90f) continue;
                    TripWorldDef wj = cfg.worlds[cfg.WorldAt(j * step)];
                    if (wi.wall == TripWallShape.None && wj.wall == TripWallShape.None) continue;
                    float need = Extent(wi) + Extent(wj) + 3f;
                    if (Vector3.Distance(pos[i], pos[j]) < need && bad++ < 5)
                        Debug.LogWarning($"[trip] rail clearance: d={i * step:F0} ({wi.name}) is within {need:F0} m of d={j * step:F0} ({wj.name}); a tunnel will cut through the track.");
                }
            }
            if (bad == 0) Debug.Log("[trip] rail clearance OK: no stretch of track passes through another's tunnel.");
        }
    }
}
