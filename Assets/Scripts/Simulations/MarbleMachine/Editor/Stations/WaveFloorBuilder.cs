using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Wave Floor: a wooden tray over a 16 x 6 bed of kinematic pistons (mach-001 station 4).
    /// Sizes come from the ball radius r: piston pitch 4r (0.26 m), bed 3.12 m wide x 4.16 m long, so 12 balls fit across (2x the
    /// original 6). The chute reaches it through two flare walls that open from the track width to the bed width, and the exit
    /// stub funnels back to exactly the track width, then runs on as a standard port.
    /// Throughput: the bed is 12 deg downhill and the wave pushes balls on at lambda*f = 1.2 m/s, so a ball clears the bed in
    /// ~3-4 s; 15 balls/s means ~50 in transit over 6.5 m^2, far from crowded. The exit stub funnels to chuteWidth + 0.2.
    /// Launch check: wave surface acceleration is (A/2)(2 pi f)^2; at A = 0.24, f = 0.9 that is ~3.9 m/s^2 (under g = 9.81, so
    /// nothing leaves the surface). Largest step between neighbours is ~A (pi/lambda) 0.26, which pushes rather than pops.
    /// Pistons rise by up to A above rest, so the box is BoxHeight 0.42 (> A + 0.1): even at full rise its underside stays below
    /// the bed plane and no gap shows beneath it. The entry anchor sits A + 0.06 above the bed so balls step down onto the crests.
    /// </summary>
    public static class WaveFloorBuilder
    {
        const int Rows = 16, Cols = 12;
        const float TiltDegrees = 14f, StubTotalDegrees = 24f, BoxHeight = 0.42f, StemLength = 0.3f;
        const float FlareStart = 0.25f, FlareLength = 1.1f;

        public static WaveFloorStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            float r = config.ballRadius;
            // Piston pitch comes from the mach-001 ball (0.065), not the racer: at r 0.2 a 4r pitch would be a 9.6 m x 12.8 m bed. A 0.4 m
            // ball rides several 0.26 m pistons, which makes the wave smoother, not rougher.
            float pitch = 4f * Mathf.Min(r, 0.065f), top = pitch - 0.02f;
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

            // Flare walls take the stream from the chute's walls out to the bed's, then the side walls run the rest of the bed. There
            // is no catch floor: the chute runs straight onto the pistons, and the floor behind the first row sat above the chute's
            // end and stopped balls. Everything stands as high as the chute walls, so the joint has no step.
            float wallH = MachineConnectorBuilder.WallHeightFor(r), wallX = halfW + 0.01f + 0.04f;
            float flareEnd = FlareStart + FlareLength;
            float chuteMid = config.chuteWidth * 0.5f + MachineConnectorBuilder.WallThickness * 0.5f;
            float flareDx = wallX - chuteMid;
            float flareHyp = Mathf.Sqrt(flareDx * flareDx + FlareLength * FlareLength);
            float wallLen = length - flareEnd;
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(phys.transform, s < 0 ? "Flare L" : "Flare R", new Vector3(s * (wallX + chuteMid) * 0.5f, wallH * 0.5f - 0.05f, FlareStart + FlareLength * 0.5f),
                    Quaternion.Euler(0f, -s * Mathf.Atan2(flareDx, FlareLength) * Mathf.Rad2Deg, 0f),
                    new Vector3(MachineConnectorBuilder.WallThickness, wallH, flareHyp), kit.glass);
                MachineGeometry.Box(phys.transform, s < 0 ? "Wall L" : "Wall R", new Vector3(s * wallX, wallH * 0.5f - 0.05f, flareEnd + wallLen * 0.5f),
                    Quaternion.identity, new Vector3(MachineConnectorBuilder.WallThickness, wallH, wallLen), kit.glass);
            }

            // Exit stub sloping steeper than the bed. First 0.9 m funnels from the bed width to exactly the track width, then a
            // straight 0.4 m port in the connector's own profile, so the next chute meets an identical lip.
            var stub = MachineGeometry.Group(bed.transform, "Exit Stub");
            stub.transform.localPosition = new Vector3(0f, 0f, length);
            float stubRel = Mathf.Max(2f, StubTotalDegrees - TiltDegrees); // the stub is steeper than the bed, ~20 degrees overall
            stub.transform.localRotation = Quaternion.Euler(stubRel, 0f, 0f);
            float funnelLen = 0.9f, narrowLen = 0.4f;
            stubLen = funnelLen + narrowLen;
            float narrowMid = config.chuteWidth * 0.5f + MachineConnectorBuilder.WallThickness * 0.5f; // centre of the chute's wall
            MachineGeometry.Box(stub.transform, "Stub Floor", new Vector3(0f, -0.05f, funnelLen * 0.5f), Quaternion.identity,
                new Vector3(halfW * 2f + 0.02f, 0.1f, funnelLen), kit.wood).GetComponent<BoxCollider>().sharedMaterial = MachineGeometry.GlidePhysics;
            MachineConnectorBuilder.BuildPort(stub.transform, "Exit Port", new[] { new Vector3(0f, 0f, funnelLen - 0.02f), new Vector3(0f, 0f, funnelLen + narrowLen) },
                kit, config, wallH, null, MachineGeometry.GlidePhysics);
            float dxF = wallX - narrowMid;
            float funnelHyp = Mathf.Sqrt(dxF * dxF + funnelLen * funnelLen);
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(stub.transform, s < 0 ? "Stub Wall L" : "Stub Wall R",
                    new Vector3(s * (wallX + narrowMid) * 0.5f, wallH * 0.5f - 0.05f, funnelLen * 0.5f),
                    Quaternion.Euler(0f, -s * Mathf.Atan2(dxF, funnelLen) * Mathf.Rad2Deg, 0f),
                    new Vector3(MachineConnectorBuilder.WallThickness, wallH, funnelHyp), kit.glass);
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
                    MachineGeometry.Box(p.transform, "Top", Vector3.zero, Quaternion.identity, new Vector3(top, BoxHeight, top), kit.whiteLacquer)
                        .GetComponent<BoxCollider>().sharedMaterial = MachineGeometry.GlidePhysics; // no bounce: balls were losing speed to every piston hit
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
            // The chute ends just above the highest the pistons rise, so balls step down onto the wave instead of being hit by a crest.
            st.entry = MachineGeometry.Anchor(root.transform, "Entry", new Vector3(0f, config.waveAmplitude + 0.06f, 0f));
            st.exit = MachineGeometry.Anchor(root.transform, "Exit",
                bedRot * (new Vector3(0f, 0f, length) + Quaternion.Euler(stubRel, 0f, 0f) * new Vector3(0f, 0.05f, stubLen)));
            st.cameraFocus = MachineGeometry.Anchor(root.transform, "Camera Focus", mid);
            st.cameraOffset = placement.Rotation * new Vector3(-10f, 3.2f, -0.5f);
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
