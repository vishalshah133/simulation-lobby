using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Helix Slide: 2 clockwise turns (radius 2.6 m, 3.5 m drop = 6 degree slope, 15 degree bank) around a chrome column,
    /// an S-curve (right arc, left arc) that shifts the flow 3.6 m sideways while still heading +Z, then a run to the exit. The radius
    /// and the shift are 2x the original, and the drop is 2x so the slope (and the speed) holds. The whole thing, entry to exit, is one
    /// channel in the connector's own profile.
    /// Ignoring friction a ball reaches sqrt(2 g H / 1.4) ~ 6 m/s; in practice wall friction (normal force v^2/R)
    /// caps it near 3 m/s (g sin(a) = mu v^2 / R), so the 0.55 m walls are ample.
    /// </summary>
    public static class HelixSlideBuilder
    {
        const float Rho = 2.6f;
        // Total fall is unchanged (1.75 + 0.30 + 0.05 = 2.1 m) so the exit height and everything downstream stay put; the S-curve gets
        // 0.30 m over ~3.3 m (5 degrees) so balls keep rolling through its banked bends.
        const float Drop = 5.0f;   // 8.7 degree slope (was 6): the helix has to hand the sorter a fast stream
        const float SDrop = 0.90f;
        const float RunDrop = 0.25f;
        const float SShift = 3.6f;      // sideways shift of the exit, metres (+X)
        const float SAngle = 70f;       // heading change of each arc, degrees
        const float TotalDeg = 720f;
        const float Bank = 15f;
        const float WallH = 0.7f;
        const float PortBack = 0.9f;

        public static HelixSlideStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            var root = new GameObject("Helix Slide");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            var t = root.transform;
            float floorY = -placement.entry.y;
            float w = config.chuteWidth;

            float zs = 0.6f, y0 = -0.1f;
            Vector3 c = new Vector3(Rho, 0f, zs);

            // Centreline: helix, U-turn, exit run.
            var pts = new List<Vector3>();
            var bank = new List<float>();
            int step = 5;
            for (int a = 0; a <= (int)TotalDeg; a += step)
            {
                float rad = a * Mathf.Deg2Rad;
                float y = y0 - Drop * a / TotalDeg;
                pts.Add(new Vector3(c.x - Rho * Mathf.Cos(rad), y, c.z + Rho * Mathf.Sin(rad)));
                bank.Add(-Bank * Mathf.SmoothStep(0f, 1f, a / 60f));
            }

            // Exit S-curve. The helix ends back at x = 0 heading +Z, and its last quarter-turn sweeps x 0..1.3, z -0.7..0.6. The old
            // exit was a half-turn U that started heading -Z: a 180 degree cusp whose footprint sat on that last quarter-turn at
            // nearly the same height, so balls piled up at the bottom of the helix and nothing reached the exit run. Instead the
            // flow keeps heading +Z (always on the free side, z > 0.6) and shifts +X by SShift with a right arc then a left arc.
            Vector3 e1 = pts[pts.Count - 1];
            float sTheta = SAngle * Mathf.Deg2Rad;
            float sR = SShift / (2f * (1f - Mathf.Cos(sTheta)));
            int sSteps = 36;
            for (int i = 1; i <= sSteps; i++)
            {
                float u = i / (float)sSteps;
                float phi = u <= 0.5f ? u * 2f * sTheta : (1f - u) * 2f * sTheta;   // heading angle off +Z: 0 -> theta -> 0
                float dx, dz;
                if (u <= 0.5f)
                {
                    dx = sR * (1f - Mathf.Cos(phi));
                    dz = sR * Mathf.Sin(phi);
                }
                else
                {
                    dx = SShift - sR * (1f - Mathf.Cos(phi));
                    dz = 2f * sR * Mathf.Sin(sTheta) - sR * Mathf.Sin(phi);
                }

                pts.Add(new Vector3(e1.x + dx, e1.y - SDrop * u, e1.z + dz));
                // Right arc banks like the helix, left arc the opposite way, flat at the inflection and at the end.
                bank.Add(-Bank * (1f - SS(0.3f, 0.5f, u)) + Bank * SS(0.5f, 0.7f, u) * (1f - SS(0.8f, 1f, u)));
            }

            Vector3 u1 = pts[pts.Count - 1];
            int runSteps = 4;
            float runLen = 1.2f;
            for (int i = 1; i <= runSteps; i++)
            {
                float f = i / (float)runSteps;
                pts.Add(u1 + new Vector3(0f, -RunDrop * f, runLen * f));
                bank.Add(0f);
            }

            Vector3 exitPt = pts[pts.Count - 1];
            // One channel, entry port to exit run, in the connector's own profile (the straight in front of the first bend is the entry port).
            var channelPts = new List<Vector3>();
            var channelBank = new List<float>();
            for (float z = -PortBack; z < pts[0].z - 0.01f; z += 0.3f)
            {
                channelPts.Add(new Vector3(0f, y0, z));
                channelBank.Add(0f);
            }

            channelPts.AddRange(pts);
            channelBank.AddRange(bank);
            // Walls stand at least 5.5 r (race balls are 0.2 m: a 0.7 m wall on a banked bend is not enough); unchanged at r 0.065.
            MachineConnectorBuilder.BuildPort(t, "Helix Channel", channelPts, kit, config, Mathf.Max(WallH, MachineConnectorBuilder.WallHeightFor(config.ballRadius)), channelBank);

            // Column, brass collars and radial arms.
            float colTop = y0 + 0.6f;
            float colH = colTop - floorY;
            MachineGeometry.Cylinder(t, "Column", new Vector3(c.x, floorY + colH * 0.5f, c.z), Quaternion.identity, 0.35f, colH, kit.chrome);
            MachineGeometry.Cylinder(t, "Column Cap", new Vector3(c.x, colTop + 0.05f, c.z), Quaternion.identity, 0.5f, 0.1f, kit.brass, false);
            MachineGeometry.Cylinder(t, "Base Collar", new Vector3(c.x, floorY + 0.15f, c.z), Quaternion.identity, 0.6f, 0.3f, kit.castIron, false);
            for (int a = 0; a < (int)TotalDeg; a += 45)
            {
                float rad = a * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(-Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                float y = y0 - Drop * a / TotalDeg - 0.2f;
                float len = Rho + 0.5f;
                MachineGeometry.Box(t, "Arm", new Vector3(c.x, y, c.z) + dir * (len * 0.5f), Quaternion.LookRotation(dir, Vector3.up),
                    new Vector3(0.1f, 0.08f, len), kit.castIron, false);
                MachineGeometry.Cylinder(t, "Arm Bolt", new Vector3(c.x, y + 0.05f, c.z) + dir * Rho, Quaternion.identity, 0.04f, 0.04f, kit.brass, false);
            }

            // Legs under the bend and run.
            Vector3 uMid = pts[(int)(TotalDeg / step) + 18];
            Leg(t, kit.chrome, uMid.x + 1.2f, uMid.z, uMid.y - 0.1f, floorY, 0.05f);
            Vector3 uLate = pts[(int)(TotalDeg / step) + 28];
            Leg(t, kit.chrome, uLate.x - 1.2f, uLate.z, uLate.y - 0.1f, floorY, 0.05f);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Leg(t, kit.chrome, exitPt.x + sgn * (w * 0.5f + 0.25f), exitPt.z - 0.2f, exitPt.y - 0.1f, floorY, 0.05f);
                Leg(t, kit.chrome, u1.x + sgn * (w * 0.5f + 0.25f), u1.z + 0.3f, u1.y - 0.1f, floorY, 0.05f);
            }

            var station = root.AddComponent<HelixSlideStation>();
            // Speed-boost pad at the START of the helix, on the flat entry stretch before the first bend: arrows on the floor that push
            // balls up to boosterSurfaceSpeed.
            var helixPads = new List<BoostPad> { BoostPadBuilder.Build(t, "Boost Pad", new Vector3(0f, y0, -0.15f), Quaternion.identity, 1.0f, kit, config) };
            // Three more along the turns, each lying on the banked floor along the direction of travel.
            foreach (int angle in new[] { 200, 420, 640 })
            {
                int i = angle / step;
                Vector3 fwd = (pts[i + 1] - pts[i - 1]).normalized;
                Vector3 side = Quaternion.AngleAxis(bank[i], fwd) * Vector3.Cross(Vector3.up, fwd).normalized;
                Vector3 up = Vector3.Cross(fwd, side).normalized;
                helixPads.Add(BoostPadBuilder.Build(t, "Boost Pad " + angle, pts[i], Quaternion.LookRotation(fwd, up), 0.7f, kit, config));
            }

            // And two near the end, where the stream was running out of speed: one late in the S-curve, one on the exit run.
            int[] endIndices = { pts.Count - 1 - runSteps - 10, pts.Count - 3 };
            for (int k = 0; k < endIndices.Length; k++)
            {
                int i = endIndices[k];
                Vector3 fwd = (pts[i + 1] - pts[i - 1]).normalized;
                Vector3 side = Quaternion.AngleAxis(bank[i], fwd) * Vector3.Cross(Vector3.up, fwd).normalized;
                Vector3 up = Vector3.Cross(fwd, side).normalized;
                helixPads.Add(BoostPadBuilder.Build(t, "Boost Pad End " + (k + 1), pts[i], Quaternion.LookRotation(fwd, up), 0.7f, kit, config));
            }

            station.pads = helixPads.ToArray();

            station.displayName = "Helix Slide";
            station.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            station.exit = MachineGeometry.Anchor(t, "Exit", exitPt + new Vector3(0f, 0.05f, 0f));
            station.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", new Vector3(c.x + 1.0f, -Drop * 0.6f, c.z));
            station.cameraOffset = placement.Rotation * new Vector3(-15f, 4f, 2f);
            station.tourSeconds = 10f;
            Vector3 lo = new Vector3(-1.5f, exitPt.y - 0.3f, -PortBack - 0.1f), hi = new Vector3(Mathf.Max(c.x + Rho, exitPt.x) + 1.4f, 0.7f, exitPt.z + 0.3f);
            station.activityBounds = WorldBounds(t, (lo + hi) * 0.5f, hi - lo);
            return station;
        }

        static float SS(float a, float b, float u) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, u));

        static void Leg(Transform t, Material m, float x, float z, float topY, float floorY, float radius)
        {
            float h = Mathf.Max(0.1f, topY - floorY);
            MachineGeometry.Cylinder(t, "Leg", new Vector3(x, floorY + h * 0.5f, z), Quaternion.identity, radius, h, m);
        }

        static Bounds WorldBounds(Transform t, Vector3 c, Vector3 size)
        {
            var b = new Bounds(t.TransformPoint(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -size.x : size.x, (i & 2) == 0 ? -size.y : size.y, (i & 4) == 0 ? -size.z : size.z) * 0.5f;
                b.Encapsulate(t.TransformPoint(corner));
            }

            return b;
        }
    }
}
