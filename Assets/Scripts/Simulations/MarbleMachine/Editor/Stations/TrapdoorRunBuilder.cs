using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Trapdoor Run: a tray with ten hinged doors over a brick chute (mach-001 station 8).
    /// Tray 2.2 m wide (17 ball diameters, 2x the original), 8.7 m long. Each of the ten rows is a pair of leaves hinged on the two
    /// side walls, so an open leaf hangs the same 1.1 m as before and the chute below stays as shallow as it was. Per 5 s cycle ~75 balls pile up at 15/s (~210 in the flood), and
    /// the tray holds thousands (1.1 x 8.7 x 0.8 m = 7.7 m^3 = ~4,000 balls), so the pile never reaches the walls.
    /// The doors hang open 80 deg: the free edge swings to y = -0.56, so the chute floor sits 0.95 m under the tray (0.4 m of
    /// clearance, well over 2.4 r). The chute is 1.7 m wide with 0.8 m walls whose tops stay under the tray beams, and it
    /// funnels to chuteWidth + 0.2 at the lip so the next connector's catch takes the whole dump.
    /// </summary>
    public static class TrapdoorRunBuilder
    {
        const int DoorCount = 10;
        const float DoorLength = 0.8f, FixedLength = 0.6f, TiltDegrees = 3f, DropDepth = 0.95f, ChuteTilt = 8f;
        const float HalfW = 1.1f, WallT = 0.08f;
        const float ExitPortLength = 0.8f;

        public static TrapdoorRunStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            var root = new GameObject("Trapdoor Run");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            var st = root.AddComponent<TrapdoorRunStation>();
            float rootY = root.transform.position.y;
            Quaternion bedRot = Quaternion.Euler(TiltDegrees, 0f, 0f);
            st.bedRotation = bedRot;

            float doorsEnd = FixedLength + DoorCount * DoorLength;     // 8.6
            float trayStart = -0.3f, trayEnd = doorsEnd + 0.08f;
            float trayLen = trayEnd - trayStart;

            // ---- upper tray (bed frame) ----
            var bed = MachineGeometry.Group(root.transform, "Tray");
            bed.transform.localRotation = bedRot;
            MachineGeometry.Box(bed.transform, "Catch Floor", new Vector3(0f, -0.05f, (trayStart + FixedLength) * 0.5f), Quaternion.identity,
                new Vector3((HalfW + WallT) * 2f, 0.1f, FixedLength - trayStart), kit.wood);
            float wallH = 0.9f;
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(bed.transform, "Tray Wall", new Vector3(s * (HalfW + WallT * 0.5f), wallH * 0.5f - 0.05f, (trayStart + trayEnd) * 0.5f),
                    Quaternion.identity, new Vector3(WallT, wallH, trayLen), kit.glass);
                MachineGeometry.Box(bed.transform, "Tray Beam", new Vector3(s * (HalfW + WallT * 0.5f), -0.14f, (trayStart + trayEnd) * 0.5f),
                    Quaternion.identity, new Vector3(0.14f, 0.12f, trayLen), kit.woodDark, false);
            }

            MachineGeometry.Box(bed.transform, "End Wall", new Vector3(0f, wallH * 0.5f - 0.05f, trayEnd - 0.04f), Quaternion.identity,
                new Vector3((HalfW + WallT) * 2f, wallH, 0.08f), kit.glass);

            // ---- glass cover ----
            // Doors snapping shut throw balls up, so a glass roof spans the tray from just past the catch floor to the end wall. Its
            // underside is 1.1 m above the tray floor (walls top out at 0.85), sealed to the walls by short skirts that overlap them,
            // so there is no gap a ball could squeeze through. The first ~0.9 m (z < FixedLength + 0.3) stays open for the chute's
            // entry; a short front skirt there stops balls bouncing back out. Nothing reaches below the wall tops except that front
            // skirt, so the doors (which hang below the floor when open) never touch it.
            float roofStart = FixedLength + 0.3f, roofBottom = 1.1f, roofT = 0.04f;
            float roofLen = trayEnd - roofStart, roofMidZ = (roofStart + trayEnd) * 0.5f;
            float roofW = (HalfW + WallT) * 2f + 0.2f;
            MachineGeometry.Box(bed.transform, "Glass Roof", new Vector3(0f, roofBottom + roofT * 0.5f, roofMidZ), Quaternion.identity,
                new Vector3(roofW, roofT, roofLen), kit.glass);
            float skirtBottom = wallH - 0.05f - 0.03f;                      // overlaps the wall tops by 3 cm
            float skirtH = roofBottom - skirtBottom;
            for (int s = -1; s <= 1; s += 2)
                MachineGeometry.Box(bed.transform, "Roof Skirt", new Vector3(s * (HalfW + WallT * 0.5f), skirtBottom + skirtH * 0.5f, roofMidZ),
                    Quaternion.identity, new Vector3(WallT, skirtH, roofLen), kit.glass);
            MachineGeometry.Box(bed.transform, "Roof End Skirt", new Vector3(0f, skirtBottom + skirtH * 0.5f, trayEnd - 0.04f),
                Quaternion.identity, new Vector3((HalfW + WallT) * 2f, skirtH, 0.08f), kit.glass);
            MachineGeometry.Box(bed.transform, "Roof Front Skirt", new Vector3(0f, 0.85f, roofStart + 0.02f), Quaternion.identity,
                new Vector3((HalfW + WallT) * 2f, 0.5f, 0.04f), kit.glass);   // y 0.6 .. 1.1: clear of the entry stream

            // ---- doors ----
            // Two leaves per row, hinged on the side walls and meeting in the middle; they open together.
            float leaf = HalfW - 0.01f;
            st.doors = new Rigidbody[DoorCount * 2];
            st.hingeBed = new Vector3[DoorCount * 2];
            st.panelOffset = new Vector3(-leaf * 0.5f, 0f, 0f);
            st.leavesPerRow = 2;
            var doorRoot = MachineGeometry.Group(root.transform, "Doors");
            float hingeX = HalfW - 0.005f;
            for (int i = 0; i < DoorCount; i++)
            {
                float zc = FixedLength + (i + 0.5f) * DoorLength;
                for (int side = 0; side < 2; side++)
                {
                    float sign = side == 0 ? 1f : -1f;
                    int k = i * 2 + side;
                    var hinge = new Vector3(sign * hingeX, -0.025f, zc);
                    st.hingeBed[k] = hinge;
                    var door = MachineGeometry.Box(doorRoot.transform, $"Door {i}{(side == 0 ? "R" : "L")}",
                        bedRot * (hinge + new Vector3(st.panelOffset.x * sign, 0f, 0f)), bedRot,
                        new Vector3(leaf, 0.05f, DoorLength - 0.02f), kit.woodDark);
                    st.doors[k] = MachineGeometry.MakeKinematic(door);
                    // Decor outside the wall: hinge gear.
                    MachineGeometry.Cylinder(bed.transform, "Hinge Gear", new Vector3(sign * (HalfW + WallT + 0.04f), -0.03f, zc), Quaternion.Euler(0f, 0f, 90f),
                        0.11f, 0.04f, kit.castIron, false);
                }
            }

            for (int side = -1; side <= 1; side += 2)
                MachineGeometry.Cylinder(bed.transform, "Hinge Rod", new Vector3(side * (HalfW + WallT + 0.1f), -0.03f, (FixedLength + doorsEnd) * 0.5f),
                    Quaternion.Euler(90f, 0f, 0f), 0.025f, doorsEnd - FixedLength, kit.chrome, false);

            // ---- lower brick chute ----
            var chute = MachineGeometry.Group(root.transform, "Chute");
            chute.transform.localPosition = new Vector3(0f, -DropDepth, -0.3f);
            chute.transform.localRotation = Quaternion.Euler(ChuteTilt, 0f, 0f);
            float chuteInner = HalfW + 0.3f;
            float narrowMid = config.chuteWidth * 0.5f + MachineConnectorBuilder.WallThickness * 0.5f; // centre of the chute's wall
            float chuteMain = trayEnd + 0.3f + 0.5f, funnelLen = 1.2f, narrowLen = ExitPortLength;
            float chuteLen = chuteMain + funnelLen + narrowLen;
            float chuteWallH = 0.8f;
            MachineGeometry.Box(chute.transform, "Chute Floor", new Vector3(0f, -0.05f, (chuteMain + funnelLen) * 0.5f), Quaternion.identity,
                new Vector3((chuteInner + WallT) * 2f, 0.1f, chuteMain + funnelLen), kit.asphalt);
            MachineConnectorBuilder.BuildPort(chute.transform, "Exit Port",
                new[] { new Vector3(0f, 0f, chuteMain + funnelLen - 0.02f), new Vector3(0f, 0f, chuteLen) }, kit, config);
            // Speed-boost pad on the exit port: arrows on the floor push the dump up to boosterSurfaceSpeed as it leaves.
            {
                Quaternion cq = Quaternion.Euler(ChuteTilt, 0f, 0f);
                Vector3 padCentre = chute.transform.localPosition + cq * new Vector3(0f, 0f, chuteMain + funnelLen + 0.4f);
                Vector3 midCentre = chute.transform.localPosition + cq * new Vector3(0f, 0f, chuteMain * 0.5f);
                st.pads = new[]
                {
                    BoostPadBuilder.Build(root.transform, "Boost Pad", padCentre, cq, 0.7f, kit, config),
                    BoostPadBuilder.Build(root.transform, "Boost Pad Mid", midCentre, cq, 0.8f, kit, config)
                };
            }

            float dxF = chuteInner + WallT * 0.5f - narrowMid;
            float funnelHyp = Mathf.Sqrt(dxF * dxF + funnelLen * funnelLen);
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(chute.transform, "Chute Wall", new Vector3(s * (chuteInner + WallT * 0.5f), chuteWallH * 0.5f - 0.05f, chuteMain * 0.5f),
                    Quaternion.identity, new Vector3(WallT, chuteWallH, chuteMain), kit.glass);
                MachineGeometry.Box(chute.transform, "Chute Funnel Wall", new Vector3(s * ((chuteInner + WallT * 0.5f + narrowMid) * 0.5f), chuteWallH * 0.5f - 0.05f, chuteMain + funnelLen * 0.5f),
                    Quaternion.Euler(0f, -s * Mathf.Atan2(dxF, funnelLen) * Mathf.Rad2Deg, 0f), new Vector3(MachineConnectorBuilder.WallThickness, chuteWallH, funnelHyp), kit.glass);
            }

            MachineGeometry.Box(chute.transform, "Chute Back", new Vector3(0f, chuteWallH * 0.5f - 0.05f, -0.04f), Quaternion.identity,
                new Vector3((chuteInner + WallT) * 2f, chuteWallH, 0.08f), kit.glass);

            // ---- supports ----
            for (int k = 0; k < 4; k++)
            {
                float z = Mathf.Lerp(0.2f, doorsEnd - 0.3f, k / 3f);
                float topY = (bedRot * new Vector3(0f, -0.15f, z)).y;
                float zz = (bedRot * new Vector3(0f, -0.15f, z)).z;
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * (HalfW + 0.62f);
                    MachineGeometry.Box(root.transform, "Outrigger", new Vector3(s * (HalfW + 0.3f), topY, zz), Quaternion.identity, new Vector3(0.8f, 0.08f, 0.1f), kit.castIron, false);
                    float h = topY + rootY;
                    if (h > 0.1f)
                        MachineGeometry.Cylinder(root.transform, "Chrome Post", new Vector3(x, topY - h * 0.5f, zz), Quaternion.identity, 0.06f, h, kit.chrome, false);
                }

                // chute legs
                Vector3 under = chute.transform.localPosition + Quaternion.Euler(ChuteTilt, 0f, 0f) * new Vector3(0f, -0.1f, z + 0.3f);
                float lh = under.y + rootY;
                if (lh > 0.1f)
                    for (int s = -1; s <= 1; s += 2)
                        MachineGeometry.Box(root.transform, "Chute Leg", new Vector3(s * 1.2f, under.y - lh * 0.5f, under.z), Quaternion.identity,
                            new Vector3(0.14f, lh, 0.14f), kit.woodDark, false);
            }

            // Tail legs under the part of the chute that runs past the tray.
            foreach (float zt in new[] { doorsEnd + 1.3f, chuteLen - 0.6f })
            {
                Vector3 under = chute.transform.localPosition + Quaternion.Euler(ChuteTilt, 0f, 0f) * new Vector3(0f, -0.1f, zt);
                float lh = under.y + rootY;
                if (lh > 0.1f)
                    for (int s = -1; s <= 1; s += 2)
                        MachineGeometry.Box(root.transform, "Chute Leg", new Vector3(s * 1.2f, under.y - lh * 0.5f, under.z), Quaternion.identity,
                            new Vector3(0.14f, lh, 0.14f), kit.woodDark, false);
            }

            st.displayName = "Trapdoor Run";
            st.entry = MachineGeometry.Anchor(root.transform, "Entry", Vector3.zero);
            st.exit = MachineGeometry.Anchor(root.transform, "Exit", chute.transform.localPosition + Quaternion.Euler(ChuteTilt, 0f, 0f) * new Vector3(0f, 0.08f, chuteLen));
            st.cameraFocus = MachineGeometry.Anchor(root.transform, "Camera Focus", new Vector3(0f, -0.6f, doorsEnd * 0.5f));
            st.cameraOffset = placement.Rotation * new Vector3(-11f, 4f, 0f);
            st.tourSeconds = 8f;
            Vector3 centre = new Vector3(0f, -0.8f, chuteLen * 0.5f - 0.3f);
            Vector3 half = new Vector3(2.0f, 1.2f, chuteLen * 0.5f + 0.2f);
            var b = new Bounds(root.transform.TransformPoint(centre), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                b.Encapsulate(root.transform.TransformPoint(centre + Vector3.Scale(c, half)));
            }

            st.activityBounds = b;
            return st;
        }
    }
}
