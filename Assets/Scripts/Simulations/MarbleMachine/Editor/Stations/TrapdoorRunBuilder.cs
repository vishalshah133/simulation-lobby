using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Trapdoor Run: a tray with ten hinged doors over a brick chute (mach-001 station 8).
    /// Tray 1.1 m wide (8.5 ball diameters), 8.7 m long. Per 5 s cycle ~75 balls pile up at 15/s (~210 in the flood), and
    /// the tray holds thousands (1.1 x 8.7 x 0.8 m = 7.7 m^3 = ~4,000 balls), so the pile never reaches the walls.
    /// The doors hang open 80 deg: the free edge swings to y = -0.56, so the chute floor sits 0.95 m under the tray (0.4 m of
    /// clearance, well over 2.4 r). The chute is 1.7 m wide with 0.8 m walls whose tops stay under the tray beams, and it
    /// funnels to chuteWidth + 0.2 at the lip so the next connector's catch takes the whole dump.
    /// </summary>
    public static class TrapdoorRunBuilder
    {
        const int DoorCount = 10;
        const float DoorLength = 0.8f, FixedLength = 0.6f, TiltDegrees = 2f, DropDepth = 0.95f, ChuteTilt = 4f;
        const float HalfW = 0.55f, WallT = 0.08f;

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
            float wallH = 0.8f;
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(bed.transform, "Tray Wall", new Vector3(s * (HalfW + WallT * 0.5f), wallH * 0.5f - 0.05f, (trayStart + trayEnd) * 0.5f),
                    Quaternion.identity, new Vector3(WallT, wallH, trayLen), kit.wood);
                MachineGeometry.Box(bed.transform, "Tray Beam", new Vector3(s * (HalfW + WallT * 0.5f), -0.14f, (trayStart + trayEnd) * 0.5f),
                    Quaternion.identity, new Vector3(0.14f, 0.12f, trayLen), kit.woodDark, false);
            }

            MachineGeometry.Box(bed.transform, "End Wall", new Vector3(0f, wallH * 0.5f - 0.05f, trayEnd - 0.04f), Quaternion.identity,
                new Vector3((HalfW + WallT) * 2f, wallH, 0.08f), kit.woodDark);

            // ---- doors ----
            st.doors = new Rigidbody[DoorCount];
            st.hingeBed = new Vector3[DoorCount];
            st.panelOffset = new Vector3(-0.54f, 0f, 0f);
            var doorRoot = MachineGeometry.Group(root.transform, "Doors");
            float hingeX = 0.54f;
            for (int i = 0; i < DoorCount; i++)
            {
                float zc = FixedLength + (i + 0.5f) * DoorLength;
                var hinge = new Vector3(hingeX, -0.025f, zc);
                st.hingeBed[i] = hinge;
                var door = MachineGeometry.Box(doorRoot.transform, $"Door {i}", bedRot * (hinge + st.panelOffset), bedRot,
                    new Vector3(1.08f, 0.05f, DoorLength - 0.02f), kit.woodDark);
                st.doors[i] = MachineGeometry.MakeKinematic(door);
                // Decor outside the wall: hinge gear.
                MachineGeometry.Cylinder(bed.transform, "Hinge Gear", new Vector3(HalfW + WallT + 0.04f, -0.03f, zc), Quaternion.Euler(0f, 0f, 90f),
                    0.11f, 0.04f, kit.castIron, false);
            }

            MachineGeometry.Cylinder(bed.transform, "Hinge Rod", new Vector3(HalfW + WallT + 0.1f, -0.03f, (FixedLength + doorsEnd) * 0.5f),
                Quaternion.Euler(90f, 0f, 0f), 0.025f, doorsEnd - FixedLength, kit.chrome, false);

            // ---- lower brick chute ----
            var chute = MachineGeometry.Group(root.transform, "Chute");
            chute.transform.localPosition = new Vector3(0f, -DropDepth, -0.3f);
            chute.transform.localRotation = Quaternion.Euler(ChuteTilt, 0f, 0f);
            float chuteInner = HalfW + 0.3f;
            float narrowHalf = Mathf.Max(0.55f, config.chuteWidth * 0.5f + 0.1f);
            float chuteMain = trayEnd + 0.3f + 0.5f, funnelLen = 1.2f, narrowLen = 0.5f;
            float chuteLen = chuteMain + funnelLen + narrowLen;
            float chuteWallH = 0.8f;
            MachineGeometry.Box(chute.transform, "Chute Floor", new Vector3(0f, -0.05f, (chuteMain + funnelLen) * 0.5f), Quaternion.identity,
                new Vector3((chuteInner + WallT) * 2f, 0.1f, chuteMain + funnelLen), kit.asphalt);
            MachineGeometry.Box(chute.transform, "Chute Floor Narrow", new Vector3(0f, -0.05f, chuteMain + funnelLen + narrowLen * 0.5f), Quaternion.identity,
                new Vector3((narrowHalf + WallT) * 2f, 0.1f, narrowLen + 0.02f), kit.asphalt);
            float dxF = chuteInner - narrowHalf;
            float funnelHyp = Mathf.Sqrt(dxF * dxF + funnelLen * funnelLen);
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(chute.transform, "Chute Wall", new Vector3(s * (chuteInner + WallT * 0.5f), chuteWallH * 0.5f - 0.05f, chuteMain * 0.5f),
                    Quaternion.identity, new Vector3(WallT, chuteWallH, chuteMain), kit.brick);
                MachineGeometry.Box(chute.transform, "Chute Funnel Wall", new Vector3(s * ((chuteInner + narrowHalf) * 0.5f + WallT * 0.5f), chuteWallH * 0.5f - 0.05f, chuteMain + funnelLen * 0.5f),
                    Quaternion.Euler(0f, -s * Mathf.Atan2(dxF, funnelLen) * Mathf.Rad2Deg, 0f), new Vector3(WallT, chuteWallH, funnelHyp), kit.brick);
                MachineGeometry.Box(chute.transform, "Chute Lip Wall", new Vector3(s * (narrowHalf + WallT * 0.5f), chuteWallH * 0.5f - 0.05f, chuteMain + funnelLen + narrowLen * 0.5f),
                    Quaternion.identity, new Vector3(WallT, chuteWallH, narrowLen), kit.brick);
            }

            MachineGeometry.Box(chute.transform, "Chute Back", new Vector3(0f, chuteWallH * 0.5f - 0.05f, -0.04f), Quaternion.identity,
                new Vector3((chuteInner + WallT) * 2f, chuteWallH, 0.08f), kit.brick);

            // ---- supports ----
            for (int k = 0; k < 4; k++)
            {
                float z = Mathf.Lerp(0.2f, doorsEnd - 0.3f, k / 3f);
                float topY = (bedRot * new Vector3(0f, -0.15f, z)).y;
                float zz = (bedRot * new Vector3(0f, -0.15f, z)).z;
                for (int s = -1; s <= 1; s += 2)
                {
                    float x = s * 1.12f;
                    MachineGeometry.Box(root.transform, "Outrigger", new Vector3(s * 0.85f, topY, zz), Quaternion.identity, new Vector3(0.6f, 0.08f, 0.1f), kit.castIron, false);
                    float h = topY + rootY;
                    if (h > 0.1f)
                        MachineGeometry.Cylinder(root.transform, "Chrome Post", new Vector3(x, topY - h * 0.5f, zz), Quaternion.identity, 0.06f, h, kit.chrome, false);
                }

                // chute legs
                Vector3 under = chute.transform.localPosition + Quaternion.Euler(ChuteTilt, 0f, 0f) * new Vector3(0f, -0.1f, z + 0.3f);
                float lh = under.y + rootY;
                if (lh > 0.1f)
                    for (int s = -1; s <= 1; s += 2)
                        MachineGeometry.Box(root.transform, "Chute Leg", new Vector3(s * 0.7f, under.y - lh * 0.5f, under.z), Quaternion.identity,
                            new Vector3(0.14f, lh, 0.14f), kit.woodDark, false);
            }

            // Tail legs under the part of the chute that runs past the tray.
            foreach (float zt in new[] { doorsEnd + 1.3f, chuteLen - 0.6f })
            {
                Vector3 under = chute.transform.localPosition + Quaternion.Euler(ChuteTilt, 0f, 0f) * new Vector3(0f, -0.1f, zt);
                float lh = under.y + rootY;
                if (lh > 0.1f)
                    for (int s = -1; s <= 1; s += 2)
                        MachineGeometry.Box(root.transform, "Chute Leg", new Vector3(s * 0.7f, under.y - lh * 0.5f, under.z), Quaternion.identity,
                            new Vector3(0.14f, lh, 0.14f), kit.woodDark, false);
            }

            st.displayName = "Trapdoor Run";
            st.entry = MachineGeometry.Anchor(root.transform, "Entry", Vector3.zero);
            st.exit = MachineGeometry.Anchor(root.transform, "Exit", chute.transform.localPosition + Quaternion.Euler(ChuteTilt, 0f, 0f) * new Vector3(0f, 0.08f, chuteLen));
            st.cameraFocus = MachineGeometry.Anchor(root.transform, "Camera Focus", new Vector3(0f, -0.6f, doorsEnd * 0.5f));
            st.cameraOffset = placement.Rotation * new Vector3(-7f, 3f, 0f);
            st.tourSeconds = 8f;
            Vector3 centre = new Vector3(0f, -0.8f, chuteLen * 0.5f - 0.3f);
            Vector3 half = new Vector3(1.2f, 1.2f, chuteLen * 0.5f + 0.2f);
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
