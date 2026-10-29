using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Tipping Tower (wave 2): three copper trough tippers, alternating sides, pivoting on their pour lips.
    /// Troughs are 1.5 long x 1.5 wide x 0.4 deep (2x the original in both plan dimensions, so the 1.8 m stream lands inside): the
    /// catch box holds ~360 balls at r 0.065, so tipperThreshold 200 tips at ~55% full. Tier pitch 1.6 m clears a tipped (upright)
    /// 1.5 m trough. The entry is a standard track port that ends just short of the first trough; the exit is the same port again.
    /// </summary>
    public static class TippingTowerBuilder
    {
        const float L = 1.5f, W = 1.5f;
        /// <summary>Trough depth: 0.4 at r 0.065; 3 ball radii (0.6) for bigger balls so a racer sits well inside the sides. Set in <see cref="Build"/>.</summary>
        static float D = 0.4f;
        const int Tiers = 3;
        const float Rest = 8f;
        const float Tip = -85f;
        const float PortBack = 0.9f;

        public static TippingTowerStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            D = Mathf.Max(0.4f, 3f * config.ballRadius);
            var root = new GameObject("Tipping Tower");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            var t = root.transform;
            float floorY = -placement.entry.y;
            float w = config.chuteWidth;
            float wallH = MachineConnectorBuilder.WallHeightFor(config.ballRadius);

            float[] px = { 0.8f, 0.6f, 0.8f };
            float[] py = { -0.9f, -2.5f, -4.1f };
            float[] yaw = { 0f, 180f, 0f };
            float zBack = 0.45f, zFront = 2.3f;
            float zc = 1.35f;                // tipper centre line: troughs span z 0.6 .. 2.1
            float trayTop = -4.6f;
            float xL = -1.4f, xR = 2.5f;

            // Entry port: the track's own profile, ending just short of the first trough so the stream drops into it.
            MachineConnectorBuilder.BuildPort(t, "Entry Port", new[] { new Vector3(0f, -0.05f, -PortBack), new Vector3(0f, -0.05f, 0.5f) }, kit, config);

            // Tower shell: back panel, side panels, glass front.
            float shellTop = -0.25f, shellBot = trayTop - 0.05f;
            float shellH = shellTop - shellBot;
            float cx = (xL + xR) * 0.5f, sx = xR - xL;
            MachineGeometry.Box(t, "Back Panel", new Vector3(cx, (shellTop + shellBot) * 0.5f, zBack), Quaternion.identity,
                new Vector3(sx + 0.1f, shellH, 0.04f), kit.glass);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                MachineGeometry.Box(t, "Side Panel", new Vector3(sgn < 0 ? xL : xR, (shellTop + shellBot) * 0.5f, (zBack + zFront) * 0.5f),
                    Quaternion.identity, new Vector3(0.05f, shellH, zFront - zBack), kit.glass);
            float glassBot = trayTop + 0.3f;
            MachineGeometry.Box(t, "Glass Front", new Vector3(cx, (glassBot + shellTop) * 0.5f, zFront), Quaternion.identity,
                new Vector3(sx, shellTop - glassBot, 0.03f), kit.glass);

            // Glass cage above the shell: a tipped trough stands upright (pivot -0.9 + 1.5 = 0.6 at its highest), so the roof sits at
            // 0.82 and the side/front panels rise to it, which keeps flung balls inside. The entry stays open: the port walls top out
            // at ~0.65 and the stream runs in at y -0.05..0.65, so the upper back panel is split around x +-1.0 with a lintel
            // above the port (0.76..0.82). The roof starts at the back panel, so nothing sits over the stream before the first trough.
            // The roof also clears the entry port's walls (5.5 r tall), which stand taller for bigger balls: 0.82 at r 0.065.
            float roofY = Mathf.Max(0.82f, wallH + 0.12f), upBot = shellTop, upH = roofY - upBot, upMid = (roofY + upBot) * 0.5f;
            float portHalf = w * 0.5f + MachineConnectorBuilder.WallThickness + 0.05f;
            MachineGeometry.Box(t, "Roof", new Vector3(cx, roofY + 0.015f, (zBack + zFront) * 0.5f), Quaternion.identity,
                new Vector3(sx + 0.1f, 0.03f, zFront - zBack + 0.04f), kit.glass);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                MachineGeometry.Box(t, "Upper Side Panel", new Vector3(sgn < 0 ? xL : xR, upMid, (zBack + zFront) * 0.5f),
                    Quaternion.identity, new Vector3(0.05f, upH, zFront - zBack), kit.glass);
                float x0 = sgn < 0 ? xL - 0.05f : portHalf, x1 = sgn < 0 ? -portHalf : xR + 0.05f;
                MachineGeometry.Box(t, "Upper Back Panel", new Vector3((x0 + x1) * 0.5f, upMid, zBack), Quaternion.identity,
                    new Vector3(x1 - x0, upH, 0.04f), kit.glass);
            }

            MachineGeometry.Box(t, "Back Lintel", new Vector3(0f, roofY - 0.03f, zBack), Quaternion.identity,
                new Vector3(portHalf * 2f, 0.06f, 0.04f), kit.glass);
            MachineGeometry.Box(t, "Upper Glass Front", new Vector3(cx, upMid, zFront), Quaternion.identity, new Vector3(sx, upH, 0.03f), kit.glass);

            // Tippers.
            var bodies = new Rigidbody[Tiers];
            var pivots = new Vector3[Tiers];
            for (int i = 0; i < Tiers; i++)
            {
                pivots[i] = new Vector3(px[i], py[i], zc);
                bodies[i] = BuildTipper(t, kit, i, pivots[i], yaw[i]);
                // Deflector plate behind the pour lip so a hard pour can't bounce back up the tower.
                float side = yaw[i] < 90f ? 1f : -1f;
                MachineGeometry.Box(t, "Splash Plate", new Vector3(px[i] + side * 0.24f, py[i] - 0.4f, zc), Quaternion.Euler(0f, 0f, -side * 55f),
                    new Vector3(0.5f, 0.03f, W), kit.copper);
            }

            // Bottom tray under the whole tower, then a converging chute and an exit port.
            float tilt = 9f;
            float tan = Mathf.Tan(tilt * Mathf.Deg2Rad);
            float zEnd = 4.9f, zB = 3.0f, zC = 4.0f;
            float TrayY(float z) => trayTop - (z - zBack) * tan;
            Vector3 t0 = new Vector3(cx, TrayY(zBack), zBack), t1 = new Vector3(cx, TrayY(zEnd), zEnd);
            Quaternion tq = Quaternion.LookRotation(t1 - t0, Vector3.up);
            float tlen = Vector3.Distance(t0, t1);
            float trayW = sx - 0.1f;
            MachineGeometry.Box(t, "Tray Floor", (t0 + t1) * 0.5f - tq * Vector3.up * 0.025f, tq, new Vector3(trayW, 0.05f, tlen), kit.asphalt);
            float exitHalf = w * 0.5f + MachineConnectorBuilder.WallThickness * 0.5f; // the connector's wall centre, so the glass lands on its inner face
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                // Straight wall for the tower footprint, then converge to the track width.
                float xw = cx + sgn * trayW * 0.5f;
                Vector3 a = new Vector3(xw, TrayY(zBack) + 0.3f, zBack), b = new Vector3(xw, TrayY(zB) + 0.3f, zB);
                Vector3 c = new Vector3(cx + sgn * exitHalf, TrayY(zC) + 0.3f, zC);
                Wall(t, kit.glass, a, b, wallH); Wall(t, kit.glass, b, c, wallH);
            }

            MachineConnectorBuilder.BuildPort(t, "Exit Port", new[] { new Vector3(cx, TrayY(zC - 0.02f), zC - 0.02f), new Vector3(cx, TrayY(zEnd), zEnd) },
                kit, config);

            // Frame: chrome posts to the floor, wood cross beams at each tier.
            foreach (float zz in new[] { zBack - 0.05f, zFront + 0.05f })
                foreach (float xx in new[] { xL - 0.07f, xR + 0.07f })
                    Leg(t, kit.chrome, xx, zz, shellTop, floorY, 0.05f);
            for (int i = 0; i < Tiers; i++)
                foreach (float zz in new[] { zBack - 0.05f, zFront + 0.05f })
                    MachineGeometry.Box(t, "Tier Beam", new Vector3(cx, py[i] - 0.3f, zz), Quaternion.identity, new Vector3(sx + 0.2f, 0.1f, 0.08f), kit.wood, false);
            Leg(t, kit.chrome, cx - 1.0f, zEnd - 0.1f, t1.y, floorY, 0.05f);
            Leg(t, kit.chrome, cx + 1.0f, zEnd - 0.1f, t1.y, floorY, 0.05f);

            var station = root.AddComponent<TippingTowerStation>();
            station.displayName = "Tipping Tower";
            station.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            station.exit = MachineGeometry.Anchor(t, "Exit", new Vector3(cx, t1.y + 0.05f, zEnd));
            station.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", new Vector3(cx, -2.4f, zc));
            station.cameraOffset = placement.Rotation * new Vector3(1.0f, 0.8f, 13f);
            station.tourSeconds = 9f;
            station.bodies = bodies;
            station.pivotLocal = pivots;
            station.yawDegrees = yaw;
            station.restDegrees = Rest;
            station.tipDegrees = Tip;
            station.catchCentre = new Vector3(-L * 0.5f, D * 0.5f, 0f);
            station.catchHalfExtents = new Vector3(L * 0.5f - 0.04f, D * 0.5f, W * 0.5f - 0.04f);
            Vector3 lo = new Vector3(xL - 0.2f, t1.y - 0.2f, -PortBack - 0.1f), hi = new Vector3(xR + 0.2f, 1.0f, zEnd + 0.1f);
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
                MachineGeometry.Cylinder(g, "Pivot Disc", new Vector3(-0.02f, 0f, sgn * (W * 0.5f + 0.03f)), Quaternion.Euler(90f, 0f, 0f), 0.14f, 0.04f, kit.castIron, false);
                MachineGeometry.Cylinder(g, "Pivot Bolt", new Vector3(-0.02f, 0f, sgn * (W * 0.5f + 0.06f)), Quaternion.Euler(90f, 0f, 0f), 0.06f, 0.03f, kit.brass, false);
            }

            return MachineGeometry.MakeKinematic(go);
        }

        static void Wall(Transform t, Material m, Vector3 a, Vector3 b, float height)
        {
            MachineGeometry.Box(t, "Tray Wall", (a + b) * 0.5f, Quaternion.LookRotation(b - a, Vector3.up),
                new Vector3(MachineConnectorBuilder.WallThickness, height, (b - a).magnitude + 0.02f), m);
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
