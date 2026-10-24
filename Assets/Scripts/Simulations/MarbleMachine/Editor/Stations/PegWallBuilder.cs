using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>Builds the Peg Wall: a 75 degree board, glass front, hex chrome pegs and two pinwheels.</summary>
    public static class PegWallBuilder
    {
        const float TiltDeg = 75f;
        const float BoardLen = 1.9f; // was 3.2: the drop after the silo has to be small (11 m height budget)

        public static PegWallStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            var root = new GameObject("Peg Wall");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            var t = root.transform;
            float r = config.ballRadius;
            float floorY = -placement.entry.y;

            float c = Mathf.Cos(TiltDeg * Mathf.Deg2Rad), s = Mathf.Sin(TiltDeg * Mathf.Deg2Rad);
            Vector3 d = new Vector3(0f, -s, c);   // down the board
            Vector3 n = new Vector3(0f, c, s);    // face normal (up and toward +Z)
            Vector3 p0 = new Vector3(0f, -0.25f, 0.5f);
            Quaternion bq = Quaternion.LookRotation(d, n); // local X lateral, Y normal, Z down-board
            float gap = 2.4f * r;
            float w = config.chuteWidth;

            Vector3 PB(float x, float u, float h) => p0 + Vector3.right * x + d * u + n * h;

            // Entry tray, ramp onto the board face.
            float trayW = w + 0.4f;
            MachineGeometry.Box(t, "Entry Floor", new Vector3(0f, -0.075f, -0.4f), Quaternion.identity,
                new Vector3(trayW, 0.05f, 0.8f), kit.asphalt);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                MachineGeometry.Box(t, "Entry Wall", new Vector3(sgn * (trayW * 0.5f + 0.025f), 0.1f, -0.4f), Quaternion.identity,
                    new Vector3(0.05f, 0.35f, 0.8f), kit.chrome);
            Vector3 ra = new Vector3(0f, -0.05f, 0f), rb = PB(0f, 0.3f, 0f);
            Slab(t, "Feed Ramp", ra, rb, trayW, 0.05f, kit.asphalt);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 off = Vector3.right * (sgn * (trayW * 0.5f + 0.025f));
                Slab(t, "Feed Rail", ra + off + Vector3.up * 0.2f, rb + off + Vector3.up * 0.2f, 0.05f, 0.35f, kit.chrome);
            }

            // Board, side walls, glass.
            MachineGeometry.Box(t, "Back Board", PB(0f, BoardLen * 0.5f, -0.03f), bq, new Vector3(2.1f, 0.06f, BoardLen), kit.wood);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                MachineGeometry.Box(t, "Side Wall", PB(sgn * 0.975f, BoardLen * 0.5f, 0.125f), bq,
                    new Vector3(0.1f, 0.25f, BoardLen), kit.woodDark);
            MachineGeometry.Box(t, "Glass Front", PB(0f, 1.1f, gap + 0.01f), bq, new Vector3(2.05f, 0.02f, 1.9f), kit.glass);
            MachineGeometry.Box(t, "Glass Rail Top", PB(0f, 0.5f, gap + 0.04f), bq, new Vector3(2.1f, 0.06f, 0.06f), kit.chrome, false);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                for (int k = 0; k < 3; k++)
                    MachineGeometry.Cylinder(t, "Bolt", PB(sgn * 0.975f, 0.5f + k * 0.7f, gap + 0.04f), Quaternion.FromToRotation(Vector3.up, n),
                        0.03f, 0.04f, kit.brass, false);

            // Distributor: diamond plus two fins spread the stream.
            MachineGeometry.Box(t, "Diamond", PB(0f, 0.6f, gap * 0.5f), bq * Quaternion.Euler(0f, 45f, 0f),
                new Vector3(0.2f, gap, 0.2f), kit.chrome);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                Fin(t, bq, PB(0f, 0f, 0f), d, n, gap, kit.chrome, sgn * 0.3f, 0.45f, sgn * 0.75f, 0.75f);

            // Pinwheels.
            float[] sx = { -0.4f, 0.4f };
            float[] su = { 1.0f, 1.3f };
            float spinR = 0.32f;
            var spinners = new KinematicSpinner[2];
            for (int i = 0; i < 2; i++)
            {
                var g = new GameObject("Pinwheel " + (i + 1));
                g.transform.SetParent(t, false);
                g.transform.localPosition = PB(sx[i], su[i], 0.1f);
                g.transform.localRotation = bq;
                MachineGeometry.Box(g.transform, "Arm A", Vector3.zero, Quaternion.identity, new Vector3(spinR * 2f, 0.14f, 0.05f), kit.castIron);
                MachineGeometry.Box(g.transform, "Arm B", Vector3.zero, Quaternion.identity, new Vector3(0.05f, 0.14f, spinR * 2f), kit.castIron);
                MachineGeometry.Cylinder(g.transform, "Hub", Vector3.zero, Quaternion.identity, 0.07f, 0.2f, kit.brass, false);
                MachineGeometry.MakeKinematic(g);
                spinners[i] = g.AddComponent<KinematicSpinner>();
            }

            // Hex peg grid.
            // 4.6 r pitch leaves ~1.9 ball diameters between pegs: at 3.2 r the gap was 0.2 r and balls wedged.
            float rowH = 4.6f * r * 0.866f, colW = 4.6f * r;
            int row = 0;
            for (float u = 0.9f; u < 1.5f; u += rowH, row++)
            {
                float x0 = (row % 2 == 0) ? 0f : colW * 0.5f;
                for (float x = x0 - 3f * colW; x <= 0.8f; x += colW)
                {
                    if (x < -0.8f) continue;
                    bool blocked = false;
                    for (int i = 0; i < 2; i++)
                        if (Mathf.Abs(x - sx[i]) < spinR + 0.2f && Mathf.Abs(u - su[i]) < spinR + 0.2f) blocked = true;
                    if (blocked) continue;
                    MachineGeometry.Cylinder(t, "Peg", PB(x, u, gap * 0.5f), Quaternion.FromToRotation(Vector3.up, n),
                        0.4f * r, gap, kit.chrome);
                }
            }

            // Bottom funnel to the centre.
            for (int sgn = -1; sgn <= 1; sgn += 2)
                Fin(t, bq, PB(0f, 0f, 0f), d, n, gap, kit.woodDark, sgn * 0.925f, 1.5f, sgn * 0.27f, 1.9f);

            // Outlet ramp and chute.
            Vector3 e0 = PB(0f, 1.9f, 0f);
            Vector3 e1 = e0 + new Vector3(0f, -0.12f, 0.3f);
            Vector3 e2 = e1 + new Vector3(0f, -0.05f, 0.6f);
            Slab(t, "Outlet Ramp", e0, e1, 0.7f, 0.05f, kit.asphalt);
            Slab(t, "Exit Floor", e1, e2, 0.7f, 0.05f, kit.asphalt);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 off = Vector3.right * (sgn * 0.375f) + Vector3.up * 0.15f;
                Slab(t, "Outlet Wall", e0 + off, e1 + off, 0.05f, 0.45f, kit.chrome);
                Slab(t, "Exit Wall", e1 + off, e2 + off, 0.05f, 0.45f, kit.chrome);
            }

            // Frame and legs.
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                MachineGeometry.Box(t, "Side Beam", PB(sgn * 1.08f, BoardLen * 0.5f, -0.1f), bq, new Vector3(0.12f, 0.14f, BoardLen), kit.wood);
                Leg(t, kit.chrome, sgn * 1.08f, -0.3f, -0.2f, floorY);
                Leg(t, kit.chrome, sgn * 1.08f, 0.9f, -1.8f, floorY);
                Leg(t, kit.chrome, sgn * 0.35f, e2.z - 0.15f, e2.y - 0.05f, floorY);
            }
            MachineGeometry.Box(t, "Top Crossbar", new Vector3(0f, -0.3f, 0.2f), Quaternion.identity, new Vector3(2.4f, 0.1f, 0.12f), kit.wood);
            MachineGeometry.Box(t, "Back Crossbar", new Vector3(0f, -0.2f, -0.3f), Quaternion.identity, new Vector3(2.2f, 0.1f, 0.1f), kit.wood);

            var station = root.AddComponent<PegWallStation>();
            station.displayName = "Peg Wall";
            station.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            station.exit = MachineGeometry.Anchor(t, "Exit", e2 + new Vector3(0f, 0.05f, 0f));
            station.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", new Vector3(0f, -1.3f, 1.0f));
            station.cameraOffset = placement.Rotation * new Vector3(0.6f, 0.6f, 5.5f);
            station.tourSeconds = 8f;
            station.spinners = spinners;
            station.activityBounds = WorldBounds(t, new Vector3(0f, -1.5f, 1.0f), new Vector3(2.4f, 3.4f, 3.4f));
            return station;
        }

        // origin is PB(0,0,0); x/u are added in board space.
        static void Fin(Transform t, Quaternion bq, Vector3 origin, Vector3 d, Vector3 n, float gap, Material m,
            float x1, float u1, float x2, float u2)
        {
            float dx = x2 - x1, du = u2 - u1;
            float len = Mathf.Sqrt(dx * dx + du * du);
            float yaw = Mathf.Atan2(dx, du) * Mathf.Rad2Deg;
            Vector3 centre = origin + Vector3.right * ((x1 + x2) * 0.5f) + d * ((u1 + u2) * 0.5f) + n * (gap * 0.5f);
            MachineGeometry.Box(t, "Fin", centre, bq * Quaternion.Euler(0f, yaw, 0f), new Vector3(0.05f, gap, len), m);
        }

        static void Slab(Transform t, string name, Vector3 a, Vector3 b, float width, float thick, Material m)
        {
            Quaternion q = Quaternion.LookRotation(b - a, Vector3.up);
            Vector3 centre = (a + b) * 0.5f - q * Vector3.up * (thick * 0.5f);
            MachineGeometry.Box(t, name, centre, q, new Vector3(width, thick, Vector3.Distance(a, b)), m);
        }

        static void Leg(Transform t, Material m, float x, float z, float topY, float floorY)
        {
            float h = Mathf.Max(0.1f, topY - floorY);
            MachineGeometry.Cylinder(t, "Leg", new Vector3(x, floorY + h * 0.5f, z), Quaternion.identity, 0.05f, h, m);
        }

        static Bounds WorldBounds(Transform t, Vector3 localCentre, Vector3 size)
        {
            float m = Mathf.Max(size.x, size.z);
            return new Bounds(t.TransformPoint(localCentre), new Vector3(m, size.y, m));
        }
    }
}
