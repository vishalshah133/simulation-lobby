using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Helix Slide (wave 2, compact): 2 clockwise turns (radius 1.3 m, 1.9 m drop = 7 degree slope, 15 degree bank) around a chrome column,
    /// a counter-clockwise half turn that turns the flow back to +Z, then a short straight run to the exit.
    /// Ignoring friction a ball reaches sqrt(2 g H / 1.4) ~ 6 m/s; in practice wall friction (normal force v^2/R)
    /// caps it near 3 m/s (g sin(a) = mu v^2 / R), so the 0.55 m walls are ample.
    /// </summary>
    public static class HelixSlideBuilder
    {
        const float Rho = 1.3f;
        const float Rho2 = 0.9f;
        const float Drop = 1.9f;
        const float UDrop = 0.15f;
        const float TotalDeg = 720f;
        const float Bank = 15f;
        const float WallH = 0.5f;

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

            // Entry catch zone.
            MachineGeometry.Box(t, "Entry Floor", new Vector3(0f, -0.075f, -0.075f), Quaternion.identity,
                new Vector3(w + 0.4f, 0.05f, 1.45f), kit.asphalt);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                MachineGeometry.Box(t, "Entry Wall", new Vector3(sgn * ((w + 0.4f) * 0.5f + 0.025f), 0.125f, -0.075f), Quaternion.identity,
                    new Vector3(0.05f, 0.4f, 1.45f), kit.chrome);

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

            Vector3 e1 = pts[pts.Count - 1];
            Vector3 c2 = e1 + new Vector3(Rho2, 0f, 0f);
            for (int a = step; a <= 180; a += step)
            {
                float rad = a * Mathf.Deg2Rad;
                pts.Add(new Vector3(c2.x - Rho2 * Mathf.Cos(rad), e1.y - UDrop * a / 180f, c2.z - Rho2 * Mathf.Sin(rad)));
                bank.Add(Mathf.Lerp(-Bank, Bank, Mathf.SmoothStep(0f, 1f, a / 50f)));
            }

            Vector3 u1 = pts[pts.Count - 1];
            int runSteps = 8;
            float runLen = 1.6f;
            for (int i = 1; i <= runSteps; i++)
            {
                float f = i / (float)runSteps;
                pts.Add(u1 + new Vector3(0f, -0.05f * f, runLen * f));
                bank.Add(Bank * (1f - Mathf.SmoothStep(0f, 1f, f * 2.5f)));
            }

            Vector3 exitPt = pts[pts.Count - 1];
            MachineGeometry.MeshObject(t, "Helix Channel", MachineGeometry.Channel(pts, w, WallH, bank), "HelixChannel", kit.whiteLacquer);

            // Column, brass collars and radial arms.
            float colTop = y0 + 0.6f;
            float colH = colTop - floorY;
            MachineGeometry.Cylinder(t, "Column", new Vector3(c.x, floorY + colH * 0.5f, c.z), Quaternion.identity, 0.2f, colH, kit.chrome);
            MachineGeometry.Cylinder(t, "Column Cap", new Vector3(c.x, colTop + 0.05f, c.z), Quaternion.identity, 0.28f, 0.1f, kit.brass, false);
            MachineGeometry.Cylinder(t, "Base Collar", new Vector3(c.x, floorY + 0.15f, c.z), Quaternion.identity, 0.32f, 0.3f, kit.castIron, false);
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
            Leg(t, kit.chrome, uMid.x + 0.6f, uMid.z, uMid.y - 0.1f, floorY, 0.05f);
            Leg(t, kit.chrome, c2.x - 0.5f, c2.z + 0.2f, e1.y - UDrop - 0.15f, floorY, 0.05f);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Leg(t, kit.chrome, exitPt.x + sgn * (w * 0.5f + 0.1f), exitPt.z - 0.2f, exitPt.y - 0.1f, floorY, 0.05f);
                Leg(t, kit.chrome, u1.x + sgn * (w * 0.5f + 0.1f), u1.z + 0.3f, u1.y - 0.1f, floorY, 0.05f);
            }

            var station = root.AddComponent<HelixSlideStation>();
            station.displayName = "Helix Slide";
            station.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            station.exit = MachineGeometry.Anchor(t, "Exit", exitPt + new Vector3(0f, 0.05f, 0f));
            station.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", new Vector3(c.x + 0.6f, -1.1f, c.z));
            station.cameraOffset = placement.Rotation * new Vector3(-8f, 2.5f, 1.5f);
            station.tourSeconds = 10f;
            Vector3 lo = new Vector3(-0.9f, exitPt.y - 0.3f, -0.9f), hi = new Vector3(c2.x + Rho2 + 0.6f, 0.7f, exitPt.z + 0.3f);
            station.activityBounds = WorldBounds(t, (lo + hi) * 0.5f, hi - lo);
            return station;
        }

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
