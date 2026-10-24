using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Tipping Tower (wave 2): three copper trough tippers, alternating sides, pivoting on their pour lips.
    /// Troughs are 0.75 long x 0.75 wide x 0.4 deep: the catch box holds ~90 balls at r 0.065 (0.18 m3 x 0.6 pack /
    /// 1.15e-3 m3), so tipperThreshold 60 tips at ~65% full. Tier pitch 0.8 m clears a tipped (upright) 0.75 m trough.
    /// </summary>
    public static class TippingTowerBuilder
    {
        const float L = 0.75f, W = 0.75f, D = 0.4f;
        const int Tiers = 3;
        const float Rest = 8f;
        const float Tip = -100f;

        public static TippingTowerStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            var root = new GameObject("Tipping Tower");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            var t = root.transform;
            float floorY = -placement.entry.y;
            float w = config.chuteWidth;

            float[] px = { 0.4f, 0.3f, 0.4f };
            float[] py = { -0.5f, -1.3f, -2.1f };
            float[] yaw = { 0f, 180f, 0f };
            float zc = 0.45f;                // tipper centre line
            float trayTop = -2.55f;
            float zBack = -0.15f, zFront = 0.95f;
            float xL = -0.7f, xR = 1.25f;

            // Entry tray, narrowing to 0.6 over the first trough.
            MachineGeometry.Box(t, "Entry Floor", new Vector3(0f, -0.075f, -0.2f), Quaternion.identity,
                new Vector3(w + 0.4f, 0.05f, 1.1f), kit.asphalt);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                MachineGeometry.Box(t, "Entry Wall", new Vector3(sgn * ((w + 0.4f) * 0.5f + 0.025f), 0.125f, -0.4f), Quaternion.identity,
                    new Vector3(0.05f, 0.4f, 0.8f), kit.chrome);
                Vector3 a = new Vector3(sgn * 0.675f, 0.1f, 0.0f), b = new Vector3(sgn * 0.33f, 0.1f, 0.35f);
                MachineGeometry.Box(t, "Funnel Wall", (a + b) * 0.5f, Quaternion.LookRotation(b - a, Vector3.up),
                    new Vector3(0.05f, 0.35f, (b - a).magnitude), kit.chrome);
            }

            // Tower shell: back panel, side panels, glass front.
            float shellTop = -0.15f, shellBot = trayTop - 0.05f;
            float shellH = shellTop - shellBot;
            float cx = (xL + xR) * 0.5f, sx = xR - xL;
            MachineGeometry.Box(t, "Back Panel", new Vector3(cx, (shellTop + shellBot) * 0.5f, zBack), Quaternion.identity,
                new Vector3(sx + 0.1f, shellH, 0.04f), kit.woodDark);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                MachineGeometry.Box(t, "Side Panel", new Vector3(sgn < 0 ? xL : xR, (shellTop + shellBot) * 0.5f, (zBack + zFront) * 0.5f),
                    Quaternion.identity, new Vector3(0.05f, shellH, zFront - zBack), kit.wood);
            float glassBot = trayTop + 0.3f;
            MachineGeometry.Box(t, "Glass Front", new Vector3(cx, (glassBot + 0.05f) * 0.5f, zFront), Quaternion.identity,
                new Vector3(sx, 0.05f - glassBot, 0.03f), kit.glass);

            // Tippers.
            var bodies = new Rigidbody[Tiers];
            var pivots = new Vector3[Tiers];
            for (int i = 0; i < Tiers; i++)
            {
                pivots[i] = new Vector3(px[i], py[i], zc);
                bodies[i] = BuildTipper(t, kit, i, pivots[i], yaw[i]);
                MachineGeometry.Cylinder(t, "Axle", new Vector3(px[i], py[i], (zBack + zFront) * 0.5f), Quaternion.Euler(90f, 0f, 0f),
                    0.035f, zFront - zBack, kit.chrome, false);
                // Deflector plate behind the pour lip so a hard pour can't bounce back up the tower.
                float side = yaw[i] < 90f ? 1f : -1f;
                MachineGeometry.Box(t, "Splash Plate", new Vector3(px[i] + side * 0.12f, py[i] - 0.28f, zc), Quaternion.Euler(0f, 0f, -side * 55f),
                    new Vector3(0.3f, 0.03f, W), kit.copper);
            }

            // Bottom tray, then chute to the exit.
            float tilt = 6f;
            float tan = Mathf.Tan(tilt * Mathf.Deg2Rad);
            float zEnd = 2.2f;
            Vector3 t0 = new Vector3(cx, trayTop, 0.0f), t1 = new Vector3(cx, trayTop - (zEnd - 0.0f) * tan, zEnd);
            Quaternion tq = Quaternion.LookRotation(t1 - t0, Vector3.up);
            float tlen = Vector3.Distance(t0, t1);
            float trayW = sx - 0.1f;
            MachineGeometry.Box(t, "Tray Floor", (t0 + t1) * 0.5f - tq * Vector3.up * 0.025f, tq, new Vector3(trayW, 0.05f, tlen), kit.asphalt);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                // Straight wall for the tower footprint, then converge to the chute width.
                float xw = cx + sgn * trayW * 0.5f;
                Vector3 a = new Vector3(xw, trayTop + 0.22f, 0.0f), b = new Vector3(xw, trayTop - 1.2f * tan + 0.22f, 1.2f);
                Vector3 c = new Vector3(cx + sgn * (w * 0.5f + 0.025f), trayTop - 1.8f * tan + 0.22f, 1.8f);
                Vector3 e = new Vector3(c.x, trayTop - zEnd * tan + 0.22f, zEnd);
                Wall(t, kit.brick, a, b); Wall(t, kit.brick, b, c); Wall(t, kit.brick, c, e);
            }

            // Frame: chrome posts to the floor, wood cross beams at each tier.
            foreach (float zz in new[] { zBack - 0.05f, zFront + 0.05f })
                foreach (float xx in new[] { xL - 0.07f, xR + 0.07f })
                    Leg(t, kit.chrome, xx, zz, 0.45f, floorY, 0.05f);
            for (int i = 0; i < Tiers; i++)
                foreach (float zz in new[] { zBack - 0.05f, zFront + 0.05f })
                    MachineGeometry.Box(t, "Tier Beam", new Vector3(cx, py[i] - 0.15f, zz), Quaternion.identity, new Vector3(sx + 0.2f, 0.1f, 0.08f), kit.wood, false);
            foreach (float zz in new[] { zBack - 0.05f, zFront + 0.05f })
                MachineGeometry.Box(t, "Top Beam", new Vector3(cx, 0.45f, zz), Quaternion.identity, new Vector3(sx + 0.3f, 0.1f, 0.1f), kit.wood, false);
            Leg(t, kit.chrome, cx - 0.5f, zEnd - 0.1f, t1.y, floorY, 0.05f);
            Leg(t, kit.chrome, cx + 0.5f, zEnd - 0.1f, t1.y, floorY, 0.05f);

            var station = root.AddComponent<TippingTowerStation>();
            station.displayName = "Tipping Tower";
            station.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            station.exit = MachineGeometry.Anchor(t, "Exit", new Vector3(cx, t1.y + 0.05f, zEnd));
            station.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", new Vector3(cx, -1.4f, 0.4f));
            station.cameraOffset = placement.Rotation * new Vector3(0.6f, 0.4f, 7.5f);
            station.tourSeconds = 9f;
            station.bodies = bodies;
            station.pivotLocal = pivots;
            station.yawDegrees = yaw;
            station.restDegrees = Rest;
            station.tipDegrees = Tip;
            station.catchCentre = new Vector3(-L * 0.5f, D * 0.5f, 0f);
            station.catchHalfExtents = new Vector3(L * 0.5f - 0.04f, D * 0.5f, W * 0.5f - 0.04f);
            Vector3 lo = new Vector3(xL - 0.2f, t1.y - 0.2f, zBack - 0.2f), hi = new Vector3(xR + 0.2f, 0.3f, zEnd + 0.1f);
            station.activityBounds = WorldBounds(t, (lo + hi) * 0.5f, hi - lo);
            return station;
        }

        static Rigidbody BuildTipper(Transform parent, MachineKit kit, int index, Vector3 pivot, float yawDeg)
        {
            var go = new GameObject("Tipper " + (index + 1));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pivot;
            go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f) * Quaternion.Euler(0f, 0f, Rest);
            var g = go.transform;
            MachineGeometry.Box(g, "Floor", new Vector3(-L * 0.5f, -0.02f, 0f), Quaternion.identity, new Vector3(L, 0.04f, W), kit.copper);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                MachineGeometry.Box(g, "Side", new Vector3(-L * 0.5f, (D - 0.04f) * 0.5f, sgn * (W * 0.5f - 0.02f)), Quaternion.identity,
                    new Vector3(L, D + 0.04f, 0.04f), kit.copper);
            MachineGeometry.Box(g, "Back", new Vector3(-L + 0.02f, (D - 0.04f) * 0.5f, 0f), Quaternion.identity, new Vector3(0.04f, D + 0.04f, W), kit.copper);
            MachineGeometry.Box(g, "Lip", new Vector3(-0.02f, 0.05f, 0f), Quaternion.identity, new Vector3(0.04f, 0.1f, W), kit.copper);
            // Decor: counterweight, pivot disc, bolts.
            MachineGeometry.Box(g, "Counterweight", new Vector3(-L + 0.08f, -0.14f, 0f), Quaternion.identity, new Vector3(0.14f, 0.16f, W * 0.8f), kit.castIron, false);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                MachineGeometry.Cylinder(g, "Pivot Disc", new Vector3(-0.02f, 0f, sgn * (W * 0.5f + 0.03f)), Quaternion.Euler(90f, 0f, 0f), 0.1f, 0.04f, kit.castIron, false);
                MachineGeometry.Cylinder(g, "Pivot Bolt", new Vector3(-0.02f, 0f, sgn * (W * 0.5f + 0.06f)), Quaternion.Euler(90f, 0f, 0f), 0.04f, 0.03f, kit.brass, false);
            }

            return MachineGeometry.MakeKinematic(go);
        }

        static void Wall(Transform t, Material m, Vector3 a, Vector3 b)
        {
            MachineGeometry.Box(t, "Tray Wall", (a + b) * 0.5f, Quaternion.LookRotation(b - a, Vector3.up),
                new Vector3(0.05f, 0.45f, (b - a).magnitude + 0.02f), m);
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
