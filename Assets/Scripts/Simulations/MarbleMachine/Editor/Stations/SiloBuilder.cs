using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the silo in local space: entry at the open top centre (origin), everything else hangs below (negative y),
    /// flow leaves along +Z from a tray lip about 5.4 m below the entry.
    /// </summary>
    /// <remarks>
    /// Volume maths (ball radius 0.065, lattice pitch 2.05 r = 0.133 m, hex cell 0.133 x 0.1154 x 0.1088 m = 1.67e-3 m^3):
    /// 16,800 balls need ~28 m^3 of lattice. Cylinder r 2.0 (inner lattice radius 1.93, ~760 balls per layer) x 2.45 m is
    /// ~11.5 m^3 of ball space, the cone (r 2.0 to 0.4 over 1.25 m at 38 degrees, ~3,900 balls) adds ~6.3 m^3; the
    /// simulation's lattice packs 16,800 into the cone (~3,200 balls) plus ~1.9 m of cylinder, leaving ~0.5 m (~20%) of free
    /// height under the rim for the stack lid and the recycled balls that rest on it. Height budget (top to exit lip): cylinder 2.45 + cone 1.25 + throat 0.17 + rotor 0.23 + 0.46 + drop to tray 0.40 +
    /// tray fall 0.51 = ~5.4 m.
    /// Feeder: throat 0.8 m (6.2 ball diameters) feeds an inlet 2 x 0.398 m wide (housing radius 0.46, slot half angle 60);
    /// 6 vanes at 60 degrees can never join inlet and outlet (the arcs between them span 60 degrees); vane tip gap 0.02 m
    /// (0.3 r) and axial gap 0.02 m, so nothing can wedge; pockets are 0.26 m (2 diameters) deep.
    /// </remarks>
    public static class SiloBuilder
    {
        // Layout constants (geometry, not tunables).
        const float Radius = 2.0f;
        const float Height = 2.45f;
        const float ConeSlope = 0.7813f; // tan 38 degrees: steeper than 35 so the cone self-empties
        const float ThroatRadius = 0.40f;
        const float HousingRadius = 0.46f;
        const float HousingHalfLength = 0.42f;
        const float HubRadius = 0.18f;
        const float VaneTipGap = 0.02f;
        const int Pockets = 6;
        const float SlotHalfAngle = 60f;
        const float FloodHalfAngle = 20f;
        const float FloodInnerRadius = 0.5f;
        const int ConeSegments = 36;
        static readonly float[] ConeRings = { 2.0f, 1.6f, 1.25f, 0.95f, 0.7f, 0.5f, 0.4f };

        // Catch tray under the feeder and the flood opening.
        const float TrayTiltDegrees = 10f;
        const float TrayHalf = 0.8f;
        const float TrayZStart = -0.7f;
        const float TrayZTaper = 2.1f;
        const float TrayZEnd = 2.9f;
        const float TrayWallHeight = 0.7f;

        public static SiloStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            var root = new GameObject("Silo");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            Transform t = root.transform;
            var station = root.AddComponent<SiloStation>();

            float floorLocalY = -placement.entry.y;
            float coneY(float r) => -Height - (Radius - r) * ConeSlope;
            float coneBottomY = coneY(ThroatRadius);
            float throatLen = 0.17f;
            float shellEdgeY = coneBottomY - throatLen;
            float rotorY = shellEdgeY - HousingRadius * Mathf.Cos(SlotHalfAngle * Mathf.Deg2Rad);
            float plateTopY = coneY(FloodInnerRadius) - 0.017f;

            // Glass cylinder (double-sided, non-convex collider) and the terrazzo cone with the flood opening.
            var glass = MachineGeometry.MeshObject(t, "Glass Cylinder", MachineGeometry.OpenCylinder(Radius, Height, 64, true),
                "Silo_Glass", kit.glass, true, false);
            glass.transform.localPosition = new Vector3(0f, -Height, 0f);

            MachineGeometry.MeshObject(t, "Terrazzo Cone", BuildCone(plateTopY, coneY), "Silo_Cone", kit.terrazzo, true, false);

            // Throat from the cone bottom to the feeder housing.
            var throat = MachineGeometry.MeshObject(t, "Outlet Throat", MachineGeometry.OpenCylinder(ThroatRadius, throatLen, 32, true),
                "Silo_Throat", kit.terrazzo, true, false);
            throat.transform.localPosition = new Vector3(0f, shellEdgeY, 0f);

            // Iron ring frame at the cylinder foot.
            var ring = MachineGeometry.MeshObject(t, "Ring Frame", MachineGeometry.Lathe(new[]
            {
                new Vector2(Radius + 0.01f, -Height - 0.12f), new Vector2(Radius + 0.22f, -Height - 0.12f),
                new Vector2(Radius + 0.22f, -Height + 0.06f), new Vector2(Radius + 0.01f, -Height + 0.06f),
                new Vector2(Radius + 0.01f, -Height - 0.12f)
            }, 64, true), "Silo_Ring", kit.castIron, false);
            ring.name = "Ring Frame";

            // Rim: chrome band round the open top.
            MachineGeometry.MeshObject(t, "Rim Band", MachineGeometry.Lathe(new[]
            {
                new Vector2(Radius - 0.01f, -0.05f), new Vector2(Radius + 0.07f, -0.05f), new Vector2(Radius + 0.07f, 0.03f),
                new Vector2(Radius - 0.01f, 0.03f), new Vector2(Radius - 0.01f, -0.05f)
            }, 64, true), "Silo_Rim", kit.chrome, false);

            // Four chrome legs to the floor, clear of the flood plate's slide (+X, z 0.45..2.1) and of the tray.
            Vector2[] legs = { new Vector2(2.14f, 0f), new Vector2(-1.5f, -1.5f), new Vector2(1.5f, -1.5f), new Vector2(-1.5f, 1.5f) };
            float legTop = -Height - 0.1f;
            float legLen = Mathf.Max(0.5f, legTop - floorLocalY);
            for (int i = 0; i < legs.Length; i++)
            {
                MachineGeometry.Cylinder(t, "Leg " + (i + 1), new Vector3(legs[i].x, legTop - legLen * 0.5f, legs[i].y),
                    Quaternion.identity, 0.09f, legLen, kit.chrome, false);
            }

            // Star feeder: housing shell (two arcs leave a top inlet and a bottom outlet), side plates, rotor.
            Vector3 rotorPos = new Vector3(0f, rotorY, 0f);
            var housing = MachineGeometry.Group(t, "Feeder Housing");
            housing.transform.localPosition = rotorPos;
            float a0 = SlotHalfAngle, a1 = 180f - SlotHalfAngle;
            MachineGeometry.MeshObject(housing.transform, "Shell Front", ArcShell(HousingRadius, HousingHalfLength, a0, a1, 16),
                "Silo_ShellFront", kit.castIron, true, false);
            MachineGeometry.MeshObject(housing.transform, "Shell Back", ArcShell(HousingRadius, HousingHalfLength, 180f + SlotHalfAngle, 360f - SlotHalfAngle, 16),
                "Silo_ShellBack", kit.castIron, true, false);
            float plateSize = HousingRadius * 2f; // 0.92: top at rotorY + 0.46, clear of the flood plate (z from 0.47)
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(housing.transform, "Side Plate", new Vector3(s * (HousingHalfLength + 0.016f), 0f, 0f),
                    Quaternion.identity, new Vector3(0.032f, plateSize, plateSize), kit.castIron);
                MachineGeometry.Cylinder(housing.transform, "Bearing", new Vector3(s * (HousingHalfLength + 0.06f), 0f, 0f),
                    Quaternion.Euler(0f, 0f, 90f), 0.1f, 0.06f, kit.brass, false);
            }

            // Drive motor on the +X side.
            MachineGeometry.Cylinder(housing.transform, "Motor", new Vector3(0.80f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f),
                0.14f, 0.32f, kit.castIron, false);
            MachineGeometry.Cylinder(housing.transform, "Motor Cap", new Vector3(0.98f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f),
                0.09f, 0.04f, kit.brass, false);

            // Rotor: kinematic hub with 6 vanes (pocket maths in the class remarks).
            var rotor = MachineGeometry.Group(t, "Star Rotor");
            rotor.transform.localPosition = rotorPos;
            float vaneAxial = HousingHalfLength * 2f - 0.04f;
            MachineGeometry.Cylinder(rotor.transform, "Hub", Vector3.zero, Quaternion.Euler(0f, 0f, 90f), HubRadius, vaneAxial, kit.chrome);
            MachineGeometry.Cylinder(rotor.transform, "Axle", Vector3.zero, Quaternion.Euler(0f, 0f, 90f), 0.05f, 1.3f, kit.chrome, false);
            MachineGeometry.Cylinder(rotor.transform, "Gear", new Vector3(0.55f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.2f, 0.05f,
                kit.brass, false);
            float vaneInner = HubRadius - 0.02f;
            float vaneOuter = HousingRadius - VaneTipGap;
            float vaneLen = vaneOuter - vaneInner;
            float vaneMid = (vaneOuter + vaneInner) * 0.5f;
            for (int i = 0; i < Pockets; i++)
            {
                float phi = i * 360f / Pockets;
                float rad = phi * Mathf.Deg2Rad;
                MachineGeometry.Box(rotor.transform, "Vane " + (i + 1), new Vector3(0f, Mathf.Cos(rad) * vaneMid, Mathf.Sin(rad) * vaneMid),
                    Quaternion.Euler(phi, 0f, 0f), new Vector3(vaneAxial, vaneLen, 0.03f), kit.copper);
            }

            Rigidbody rotorBody = MachineGeometry.MakeKinematic(rotor);

            // Anti-bridging agitator: a slow kinematic bar sweeping the lower cone. Tip 0.7 m from the axis against a cone
            // radius of 1.10 m at its height: normal gap 0.25 m (3.9 r), so it can't pinch a ball against the wall.
            float agitatorY = coneBottomY + 0.55f;
            var agitator = MachineGeometry.Group(t, "Agitator");
            agitator.transform.localPosition = new Vector3(0f, agitatorY, 0f);
            MachineGeometry.Box(agitator.transform, "Agitator Bar", Vector3.zero, Quaternion.identity, new Vector3(1.4f, 0.05f, 0.05f), kit.chrome);
            MachineGeometry.Cylinder(agitator.transform, "Agitator Hub", Vector3.zero, Quaternion.identity, 0.07f, 0.12f, kit.chrome);
            Rigidbody agitatorBody = MachineGeometry.MakeKinematic(agitator);

            // Flood gate: sliding plate under the wedge opening (wedge half width 0.73 m at the rim; plate half width 0.8 m).
            float plateZ0 = 0.47f, plateZ1 = 2.1f;
            var plate = MachineGeometry.Box(t, "Flood Plate", new Vector3(0f, plateTopY - 0.02f, (plateZ0 + plateZ1) * 0.5f),
                Quaternion.identity, new Vector3(TrayHalf * 2f, 0.04f, plateZ1 - plateZ0), kit.brass);
            Rigidbody plateBody = MachineGeometry.MakeKinematic(plate);
            // Guide rails the plate slides in (decoration).
            MachineGeometry.Box(t, "Gate Rail", new Vector3(1.3f, plateTopY - 0.07f, (plateZ0 + plateZ1) * 0.5f), Quaternion.identity,
                new Vector3(3.2f, 0.04f, 0.06f), kit.chrome, false);

            // Catch tray under the feeder and the flood opening, sloping down toward +Z, narrowing to the chute width at the lip.
            float tan = Mathf.Tan(TrayTiltDegrees * Mathf.Deg2Rad);
            float cos = Mathf.Cos(TrayTiltDegrees * Mathf.Deg2Rad);
            float trayY0 = rotorY - HousingRadius - 0.40f; // floor top height at z = 0 (0.40 m = 6 r under the outlet)
            float len = (TrayZEnd - TrayZStart) / cos;
            float zMid = (TrayZStart + TrayZEnd) * 0.5f;
            Quaternion trayRot = Quaternion.Euler(TrayTiltDegrees, 0f, 0f);
            float trayY(float z) => trayY0 - z * tan;
            Vector3 trayPoint(float x, float z, float up) => new Vector3(x, trayY(z), z) + trayRot * new Vector3(0f, up, 0f);

            MachineGeometry.Box(t, "Tray Floor", trayPoint(0f, zMid, -0.03f), trayRot,
                new Vector3(TrayHalf * 2f, 0.06f, len), kit.asphalt);
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(t, "Tray Wall", trayPoint(s * (TrayHalf + 0.03f), zMid, TrayWallHeight * 0.5f), trayRot,
                    new Vector3(0.06f, TrayWallHeight, len), kit.brick);
            }

            MachineGeometry.Box(t, "Tray Back", trayPoint(0f, TrayZStart - 0.03f, TrayWallHeight * 0.5f), trayRot,
                new Vector3(TrayHalf * 2f + 0.12f, TrayWallHeight, 0.06f), kit.brick);

            // Guide walls converge from the full tray width to the chute width, so the stream lands in the connector.
            float exitHalf = config.chuteWidth * 0.5f + 0.02f;
            float dx = TrayHalf - exitHalf;
            float dz = (TrayZEnd - TrayZTaper) / cos;
            float guideLen = Mathf.Sqrt(dx * dx + dz * dz);
            float guideYaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(t, "Tray Guide", trayPoint(s * (TrayHalf + exitHalf) * 0.5f, (TrayZTaper + TrayZEnd) * 0.5f, TrayWallHeight * 0.5f),
                    trayRot * Quaternion.Euler(0f, -s * guideYaw, 0f), new Vector3(0.06f, TrayWallHeight, guideLen), kit.brick);
            }

            float exitY = trayY(TrayZEnd) + 0.05f;
            var exit = MachineGeometry.Anchor(t, "Exit", new Vector3(0f, exitY, TrayZEnd));

            // Tray support columns, each topping out just under the floor at its own z.
            Vector2[] cols = { new Vector2(-0.78f, -0.6f), new Vector2(0.78f, -0.6f), new Vector2(-0.78f, TrayZEnd - 0.1f), new Vector2(0.78f, TrayZEnd - 0.1f) };
            for (int i = 0; i < cols.Length; i++)
            {
                float top = trayY(cols[i].y) - 0.08f;
                float colLen = Mathf.Max(0.4f, top - floorLocalY);
                MachineGeometry.Cylinder(t, "Tray Column " + (i + 1), new Vector3(cols[i].x, top - colLen * 0.5f, cols[i].y),
                    Quaternion.identity, 0.05f, colLen, kit.chrome, false);
            }

            // Wire the station.
            station.displayName = "Silo";
            station.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            station.exit = exit;
            station.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", new Vector3(0f, rotorY + 0.6f, 0.3f));
            station.cameraOffset = placement.Rotation * new Vector3(-5.0f, 1.4f, -5.6f);
            station.tourSeconds = 8f;
            float boundsTop = 0.4f, boundsBottom = exitY - 0.6f;
            station.activityBounds = WorldBounds(t, new Bounds(new Vector3(0f, (boundsTop + boundsBottom) * 0.5f, 0.3f),
                new Vector3(4.8f, boundsTop - boundsBottom, 5.4f)));
            station.innerRadius = Radius;
            station.storageBottomLocalY = -Height;
            station.coneBottomLocalY = coneBottomY;
            station.throatRadius = ThroatRadius;
            station.coneSlope = ConeSlope;
            station.storageTopLocalY = -0.1f;
            station.pockets = Pockets;
            station.ballsPerPocket = 20f;
            station.outletLocalY = rotorY - HousingRadius - 0.1f;
            station.rotorBody = rotorBody;
            station.rotorRestPosition = rotor.transform.localPosition;
            station.rotorRestRotation = rotor.transform.localRotation;
            station.agitatorBody = agitatorBody;
            station.agitatorRestPosition = agitator.transform.localPosition;
            station.agitatorRestRotation = agitator.transform.localRotation;
            station.plateBody = plateBody;
            station.plateRestPosition = plate.transform.localPosition;
            station.plateRestRotation = plate.transform.localRotation;
            station.plateOpenOffset = new Vector3(1.7f, 0f, 0f);

            // Tray volume for the flood back-pressure count: starts just past the housing (z 0.5) so pockets don't count.
            float trayCountZ0 = 0.5f;
            float trayCountY0 = trayY(TrayZEnd) - 0.1f;
            float trayCountY1 = trayY(trayCountZ0) + TrayWallHeight;
            station.trayLocal = new Bounds(
                new Vector3(0f, (trayCountY0 + trayCountY1) * 0.5f, (trayCountZ0 + TrayZEnd) * 0.5f),
                new Vector3(TrayHalf * 2f, trayCountY1 - trayCountY0, TrayZEnd - trayCountZ0));
            station.trayCapacity = 300;

            Debug.Log($"[mach] Silo: cylinder r {Radius:0.00} x {Height:0.00} m, cone {Radius - ThroatRadius:0.00} m of radius down to a " +
                      $"{ThroatRadius * 2f:0.00} m throat ({Mathf.Atan(ConeSlope) * Mathf.Rad2Deg:0} deg), rotor y {rotorY:0.00}, " +
                      $"exit lip {-exitY:0.00} m below the top (budget 5.5), flood plate top y {plateTopY:0.00}.");
            return station;
        }

        // ---------- meshes ----------

        /// <summary>The cone with a wedge-shaped flood opening, its side walls and the outer arc wall, double-sided.</summary>
        static Mesh BuildCone(float plateTopY, System.Func<float, float> coneY)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int rings = ConeRings.Length;
            float seg = 360f / ConeSegments;
            for (int i = 0; i < rings; i++)
            {
                for (int s = 0; s < ConeSegments; s++)
                {
                    float a = s * seg * Mathf.Deg2Rad;
                    verts.Add(new Vector3(Mathf.Sin(a) * ConeRings[i], coneY(ConeRings[i]), Mathf.Cos(a) * ConeRings[i]));
                }
            }

            for (int i = 0; i < rings - 1; i++)
            {
                bool holeRing = ConeRings[i + 1] >= FloodInnerRadius - 0.001f;
                for (int s = 0; s < ConeSegments; s++)
                {
                    if (holeRing && InHole(s, seg)) continue;
                    int s1 = (s + 1) % ConeSegments;
                    int a = i * ConeSegments + s, b = i * ConeSegments + s1;
                    int c = (i + 1) * ConeSegments + s, d = (i + 1) * ConeSegments + s1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            // Side walls down to the plate along both hole edges.
            for (int side = -1; side <= 1; side += 2)
            {
                float az = side * FloodHalfAngle * Mathf.Deg2Rad;
                for (int i = 0; i < rings - 1; i++)
                {
                    if (ConeRings[i + 1] < FloodInnerRadius - 0.001f) break;
                    AddQuad(verts, tris, Polar(az, ConeRings[i], coneY(ConeRings[i])), Polar(az, ConeRings[i + 1], coneY(ConeRings[i + 1])),
                        Polar(az, ConeRings[i], plateTopY), Polar(az, ConeRings[i + 1], plateTopY));
                }
            }

            // Outer arc wall closing the gap between the cylinder foot and the plate.
            int steps = Mathf.RoundToInt(FloodHalfAngle * 2f / seg);
            for (int k = 0; k < steps; k++)
            {
                float a0 = (-FloodHalfAngle + k * seg) * Mathf.Deg2Rad;
                float a1 = (-FloodHalfAngle + (k + 1) * seg) * Mathf.Deg2Rad;
                AddQuad(verts, tris, Polar(a0, Radius, -Height), Polar(a1, Radius, -Height), Polar(a0, Radius, plateTopY), Polar(a1, Radius, plateTopY));
            }

            return MakeDoubleSided(verts, tris);
        }

        static bool InHole(int segment, float segDegrees)
        {
            // Segment s spans [s, s+1] * seg; the hole spans -FloodHalfAngle..+FloodHalfAngle around azimuth 0 (+Z).
            float mid = (segment + 0.5f) * segDegrees;
            if (mid > 180f) mid -= 360f;
            return Mathf.Abs(mid) < FloodHalfAngle;
        }

        static Vector3 Polar(float azimuthRadians, float radius, float y)
        {
            return new Vector3(Mathf.Sin(azimuthRadians) * radius, y, Mathf.Cos(azimuthRadians) * radius);
        }

        static void AddQuad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>Partial surface of revolution about local X: angle 0 is +Y, increasing toward +Z. Double-sided.</summary>
        static Mesh ArcShell(float radius, float halfLength, float degrees0, float degrees1, int steps)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(degrees0, degrees1, i / (float)steps) * Mathf.Deg2Rad;
                float y = Mathf.Cos(a) * radius, z = Mathf.Sin(a) * radius;
                verts.Add(new Vector3(-halfLength, y, z));
                verts.Add(new Vector3(halfLength, y, z));
            }

            for (int i = 0; i < steps; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

            return MakeDoubleSided(verts, tris);
        }

        /// <summary>Duplicates the geometry with flipped winding and adds planar UVs. Shared with <c>CannonBuilder</c>.</summary>
        internal static Mesh MakeDoubleSided(List<Vector3> verts, List<int> tris)
        {
            int n = verts.Count, m = tris.Count;
            for (int i = 0; i < n; i++) verts.Add(verts[i]);
            for (int i = 0; i < m; i += 3)
            {
                tris.Add(tris[i] + n);
                tris.Add(tris[i + 2] + n);
                tris.Add(tris[i + 1] + n);
            }

            var uvs = new List<Vector2>(verts.Count);
            for (int i = 0; i < verts.Count; i++) uvs.Add(new Vector2((verts[i].x + verts[i].z) * 0.5f, verts[i].y * 0.5f));
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Bounds WorldBounds(Transform t, Bounds local)
        {
            Vector3 c = local.center, e = local.extents;
            var b = new Bounds(t.TransformPoint(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                b.Encapsulate(t.TransformPoint(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
            }

            return b;
        }
    }
}
