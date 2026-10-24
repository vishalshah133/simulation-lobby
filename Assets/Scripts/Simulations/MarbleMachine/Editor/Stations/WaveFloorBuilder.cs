using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Wave Floor: a wooden tray over a 16 x 6 bed of kinematic pistons (mach-001 station 4).
    /// Sizes come from the ball radius r: piston pitch 4r (0.26 m), bed 1.56 m wide x 4.16 m long, so 6 balls fit across.
    /// Throughput: the bed is 3 deg downhill and the wave pushes balls on at lambda*f = 1.2 m/s, so a ball clears the bed in
    /// ~3-4 s; 15 balls/s means ~50 in transit over 6.5 m^2, far from crowded. The exit stub funnels to chuteWidth + 0.2.
    /// Launch check: wave surface acceleration is (A/2)(2 pi f)^2 = 1.8 m/s^2 at A = 0.25 (under g, so nothing leaves the
    /// surface) and the largest step between neighbours is A (pi/lambda) 0.26 = 0.10 m (1.6 r), which pushes, not pops.
    /// </summary>
    public static class WaveFloorBuilder
    {
        const int Rows = 16, Cols = 6;
        const float TiltDegrees = 3f, BoxHeight = 0.22f, StemLength = 0.12f;

        public static WaveFloorStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            float r = config.ballRadius;
            float pitch = 4f * r, top = pitch - 0.02f;
            float halfW = Cols * pitch * 0.5f, length = Rows * pitch;

            var root = new GameObject("Wave Floor");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            var st = root.AddComponent<WaveFloorStation>();
            float stubLen;
            Quaternion bedRot = Quaternion.Euler(TiltDegrees, 0f, 0f);
            st.bedRotation = bedRot;

            var bed = MachineGeometry.Group(root.transform, "Bed");
            bed.transform.localRotation = bedRot;
            var phys = MachineGeometry.Group(bed.transform, "Tray");

            // Catch zone floor + side walls (open back for the chute).
            MachineGeometry.Box(phys.transform, "Catch Floor", new Vector3(0f, -0.05f, -0.45f), Quaternion.identity,
                new Vector3(halfW * 2f + 0.02f, 0.1f, 0.9f), kit.wood);
            float wallLen = 0.9f + length;
            float wallH = 0.7f, wallX = halfW + 0.01f + 0.04f;
            for (int s = -1; s <= 1; s += 2)
                MachineGeometry.Box(phys.transform, s < 0 ? "Wall L" : "Wall R", new Vector3(s * wallX, wallH * 0.5f - 0.05f, -0.9f + wallLen * 0.5f),
                    Quaternion.identity, new Vector3(0.08f, wallH, wallLen), kit.woodDark);

            // Exit stub sloping steeper than the bed. First 0.9 m funnels from the tray width to chuteWidth + 0.2, then a narrow
            // straight 0.4 m so the next connector's catch zone sees a lip it can take whole.
            var stub = MachineGeometry.Group(bed.transform, "Exit Stub");
            stub.transform.localPosition = new Vector3(0f, 0f, length);
            stub.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            float funnelLen = 0.9f, narrowLen = 0.4f;
            stubLen = funnelLen + narrowLen;
            float narrowHalf = Mathf.Max(0.55f, config.chuteWidth * 0.5f + 0.1f);
            MachineGeometry.Box(stub.transform, "Stub Floor", new Vector3(0f, -0.05f, funnelLen * 0.5f), Quaternion.identity,
                new Vector3(halfW * 2f + 0.02f, 0.1f, funnelLen), kit.wood);
            MachineGeometry.Box(stub.transform, "Stub Floor Narrow", new Vector3(0f, -0.05f, funnelLen + narrowLen * 0.5f), Quaternion.identity,
                new Vector3((narrowHalf + 0.08f) * 2f, 0.1f, narrowLen + 0.02f), kit.wood);
            float dxF = (halfW + 0.01f) - narrowHalf;
            float funnelHyp = Mathf.Sqrt(dxF * dxF + funnelLen * funnelLen);
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(stub.transform, s < 0 ? "Stub Wall L" : "Stub Wall R",
                    new Vector3(s * ((halfW + 0.01f + narrowHalf) * 0.5f + 0.04f), 0.2f, funnelLen * 0.5f),
                    Quaternion.Euler(0f, -s * Mathf.Atan2(dxF, funnelLen) * Mathf.Rad2Deg, 0f), new Vector3(0.08f, 0.5f, funnelHyp), kit.woodDark);
                MachineGeometry.Box(stub.transform, s < 0 ? "Stub Narrow L" : "Stub Narrow R",
                    new Vector3(s * (narrowHalf + 0.04f), 0.2f, funnelLen + narrowLen * 0.5f),
                    Quaternion.identity, new Vector3(0.08f, 0.5f, narrowLen), kit.woodDark);
            }

            // Pistons.
            int n = Rows * Cols;
            st.pistons = new Rigidbody[n];
            st.restLocal = new Vector3[n];
            st.pistonZ = new float[n];
            var pistonRoot = MachineGeometry.Group(root.transform, "Pistons");
            for (int zi = 0; zi < Rows; zi++)
            {
                for (int xi = 0; xi < Cols; xi++)
                {
                    int i = zi * Cols + xi;
                    float x = (xi - (Cols - 1) * 0.5f) * pitch;
                    float z = (zi + 0.5f) * pitch;
                    Vector3 rest = bedRot * new Vector3(x, -BoxHeight * 0.5f, z);
                    var p = new GameObject($"Piston {zi}_{xi}");
                    p.transform.SetParent(pistonRoot.transform, false);
                    p.transform.localPosition = rest;
                    p.transform.localRotation = bedRot;
                    MachineGeometry.Box(p.transform, "Top", Vector3.zero, Quaternion.identity, new Vector3(top, BoxHeight, top), kit.whiteLacquer);
                    MachineGeometry.Cylinder(p.transform, "Stem", new Vector3(0f, -(BoxHeight + StemLength) * 0.5f, 0f),
                        Quaternion.identity, 0.03f, StemLength, kit.chrome, false);
                    st.pistons[i] = MachineGeometry.MakeKinematic(p);
                    st.restLocal[i] = rest;
                    st.pistonZ[i] = z;
                }
            }

            // Decor: gear train on the left outside of the tray.
            var gearRot = Quaternion.Euler(0f, 0f, 90f);
            MachineGeometry.Cylinder(bed.transform, "Big Gear", new Vector3(-wallX - 0.12f, 0.1f, 0.6f), gearRot, 0.26f, 0.05f, kit.castIron, false);
            MachineGeometry.Cylinder(bed.transform, "Small Gear", new Vector3(-wallX - 0.12f, 0.1f, 1.05f), gearRot, 0.14f, 0.05f, kit.brass, false);
            MachineGeometry.Cylinder(bed.transform, "Gear Axle", new Vector3(-wallX - 0.06f, 0.1f, 0.6f), gearRot, 0.03f, 0.2f, kit.chrome, false);

            // Legs to the floor.
            float rootY = root.transform.position.y;
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 4; k++)
                {
                    float z = k * length / 3f;
                    LegTo(root.transform, new Vector3(s * (wallX + 0.1f), (bedRot * new Vector3(0f, -0.1f, z)).y, (bedRot * new Vector3(0f, -0.1f, z)).z), rootY, kit.woodDark);
                }

            Vector3 mid = bedRot * new Vector3(0f, 0.2f, length * 0.5f);
            st.displayName = "Wave Floor";
            st.entry = MachineGeometry.Anchor(root.transform, "Entry", Vector3.zero);
            st.exit = MachineGeometry.Anchor(root.transform, "Exit",
                bedRot * (new Vector3(0f, 0f, length) + Quaternion.Euler(14f, 0f, 0f) * new Vector3(0f, 0.05f, stubLen)));
            st.cameraFocus = MachineGeometry.Anchor(root.transform, "Camera Focus", mid);
            st.cameraOffset = placement.Rotation * new Vector3(-6.5f, 2.2f, -0.5f);
            st.tourSeconds = 9f;
            st.activityBounds = WorldBounds(root.transform, new Vector3(0f, 0.3f, length * 0.5f), new Vector3(halfW * 2f + 0.3f, 1.2f, length + 2f));
            return st;
        }

        static void LegTo(Transform root, Vector3 topLocal, float rootY, Material mat)
        {
            float h = topLocal.y + rootY;
            if (h < 0.05f) return;
            MachineGeometry.Cylinder(root, "Leg", new Vector3(topLocal.x, topLocal.y - h * 0.5f, topLocal.z), Quaternion.identity, 0.05f, h, mat, false);
        }

        static Bounds WorldBounds(Transform root, Vector3 centreLocal, Vector3 size)
        {
            var b = new Bounds(root.TransformPoint(centreLocal), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                b.Encapsulate(root.TransformPoint(centreLocal + Vector3.Scale(c, size * 0.5f)));
            }
            return b;
        }
    }
}
