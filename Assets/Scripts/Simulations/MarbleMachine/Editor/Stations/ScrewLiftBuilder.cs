using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Screw Lift: an Archimedes screw in an inclined glass tube (mach-001 station 9).
    /// Sizes from the ball radius r = 0.065: glass bore 5.5 r (0.36 m, 5.5 balls across), pitch 1.0 m.
    /// Capacity: annulus (bore minus shaft) = pi (0.3575^2 - 0.07^2) = 0.39 m^2, so one pitch of tube holds 0.39 m^3 =
    /// 0.39 x 478 balls/m^3 (bulk, 55% packed) = 187 balls at 100% fill. A 45 deg screw runs ~20% full: ~37 balls per turn.
    /// At 330 deg/s that is 0.917 turns/s, so ~34 balls/s (needs only 15/s sustained, 20/s with margin). Axial speed 0.92 m/s.
    /// Flight angle at the glass is atan(P / 2 pi r) = 24 deg, under the 45 deg limit where balls would slide back
    /// (tan(flight) must stay under tan(90 - incline)), so they ride ~27 deg up the wall in a stable pocket.
    /// </summary>
    public static class ScrewLiftBuilder
    {
        const float InclineDegrees = 45f, Pitch = 1.0f, ShaftRadius = 0.07f, RiseTarget = 6.1f;
        const float BoreInBalls = 5.5f, MouthDrop = 0.5f, BottomZ = 2.0f, TrayLen = 1.5f, TrayDegrees = 15f;
        const int SegmentsPerTurn = 24;

        public static ScrewLiftStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            float r = config.ballRadius;
            float rIn = BoreInBalls * r;                  // glass inner radius
            float rOut = rIn - 0.3f * r;                  // flight outer edge: gap < 0.4 r
            float sinI = Mathf.Sin(InclineDegrees * Mathf.Deg2Rad);
            // Entry -> exit lip rise = RiseTarget: exit.y = -MouthDrop + L sin(incline) + 0.04 cos(tray) - TrayLen sin(tray).
            float tubeLength = (RiseTarget + MouthDrop - 0.04f * Mathf.Cos(TrayDegrees * Mathf.Deg2Rad) + TrayLen * Mathf.Sin(TrayDegrees * Mathf.Deg2Rad)) / sinI;
            var root = new GameObject("Screw Lift");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            var st = root.AddComponent<ScrewLiftStation>();
            float rootY = root.transform.position.y;

            Quaternion frame = Quaternion.Euler(InclineDegrees, 0f, 0f);   // rotor and tube local +Y -> up the incline (+Z)
            Vector3 dir = frame * Vector3.up;
            // Perpendicular to the axis in the vertical plane, pointing down and forward: (0, -cos45, +sin45).
            Vector3 lowSide = new Vector3(0f, -Mathf.Cos(InclineDegrees * Mathf.Deg2Rad), Mathf.Sin(InclineDegrees * Mathf.Deg2Rad));
            float mouthDrop = MouthDrop;                                    // the hopper floor falls this far from the entry to the mouth (0.5 m = 14 deg)
            float bottomZ = BottomZ;                                        // where the glass bottom line starts
            Vector3 a0 = new Vector3(0f, -mouthDrop - lowSide.y * rIn, bottomZ - lowSide.z * rIn);
            Vector3 a1 = a0 + dir * tubeLength;
            st.frameRotation = frame;
            st.pitch = Pitch;

            // ---- glass tube (static, non-convex) ----
            Mesh tubeMesh = MachineGeometry.OpenCylinder(rIn, tubeLength, 40, true);
            var tube = MachineGeometry.MeshObject(root.transform, "Glass Tube", tubeMesh, "ScrewLift_Tube", kit.glass, true, false);
            tube.transform.localPosition = a0;
            tube.transform.localRotation = frame;

            // ---- rotor: shaft + flight boxes on one kinematic body, plus a visual helicoid ----
            var rotorGo = new GameObject("Rotor");
            rotorGo.transform.SetParent(root.transform, false);
            rotorGo.transform.localPosition = a0;
            rotorGo.transform.localRotation = frame;
            MachineGeometry.Cylinder(rotorGo.transform, "Shaft", new Vector3(0f, tubeLength * 0.5f, 0f), Quaternion.identity, ShaftRadius, tubeLength - 0.04f, kit.chrome, true);

            float yStart = 0.12f, yEnd = tubeLength - 0.02f;   // flight runs to the lip so the last turn pushes balls out
            float turns = (yEnd - yStart) / Pitch;
            int segs = Mathf.CeilToInt(turns * SegmentsPerTurn);
            float rInner = ShaftRadius * 0.9f;
            float rm = (rInner + rOut) * 0.5f, radialLen = rOut - rInner;
            float tangLen = 2f * Mathf.PI * rOut / SegmentsPerTurn * 1.15f;
            var colliders = MachineGeometry.Group(rotorGo.transform, "Flight Colliders").transform;
            for (int k = 0; k < segs; k++)
            {
                float phi = (k + 0.5f) / SegmentsPerTurn * 2f * Mathf.PI;
                float y = yStart + Pitch * phi / (2f * Mathf.PI);
                Vector3 radial = new Vector3(Mathf.Sin(phi), 0f, Mathf.Cos(phi));
                Vector3 tangent = new Vector3(rm * Mathf.Cos(phi), Pitch / (2f * Mathf.PI), -rm * Mathf.Sin(phi)).normalized;
                Vector3 normal = Vector3.Cross(radial, tangent).normalized;
                var go = new GameObject("F");
                go.transform.SetParent(colliders, false);
                go.transform.localPosition = radial * rm + new Vector3(0f, y, 0f);
                go.transform.localRotation = Quaternion.LookRotation(tangent, normal);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(radialLen, 0.04f, tangLen);
                box.sharedMaterial = MachineGeometry.WallPhysics;
            }

            var flight = MachineGeometry.MeshObject(rotorGo.transform, "Flight", HelicoidMesh(rInner, rOut, yStart, yEnd, Pitch), "ScrewLift_Flight", kit.brass, false);
            flight.transform.localPosition = Vector3.zero;
            st.rotor = MachineGeometry.MakeKinematic(rotorGo);

            // Chrome bands round the glass.
            GameObject firstBand = null;
            for (int i = 0; i < 9; i++)
            {
                float y = i == 0 ? 0.03f : (i == 8 ? tubeLength - 0.03f : tubeLength * i / 8f);
                GameObject band;
                if (firstBand == null)
                {
                    band = MachineGeometry.MeshObject(root.transform, "Band", MachineGeometry.OpenCylinder(rIn + 0.02f, 0.07f, 28, true), "ScrewLift_Band", kit.chrome, false);
                    firstBand = band;
                }
                else band = Object.Instantiate(firstBand, root.transform);
                band.name = "Band " + i;
                band.transform.localPosition = a0 + dir * y;
                band.transform.localRotation = frame;
            }

            // ---- hopper ----
            // Floor falls 0.5 m over 2 m (14 deg) into the mouth, so the pile settles against the screw. A flat pile level with the
            // entry holds 0.5 x 0.5 x 2.0 x 1.3 m = 0.65 m^3 (~340 balls) plus the funnel zone: the ~400 flood backlog fits.
            var hop = MachineGeometry.Group(root.transform, "Hopper").transform;
            float halfW = Mathf.Max(0.65f, config.chuteWidth * 0.5f + 0.2f), wt = 0.08f;
            float floorEnd = bottomZ + 0.05f;
            float slope = Mathf.Atan2(mouthDrop, bottomZ) * Mathf.Rad2Deg;
            float floorLen = (floorEnd + 0.3f) / Mathf.Cos(slope * Mathf.Deg2Rad);
            float floorMidZ = (floorEnd - 0.3f) * 0.5f;
            float floorMidTop = -mouthDrop * floorMidZ / bottomZ;
            MachineGeometry.Box(hop, "Hopper Floor", new Vector3(0f, floorMidTop - 0.05f, floorMidZ), Quaternion.Euler(slope, 0f, 0f),
                new Vector3((halfW + wt) * 2f, 0.1f, floorLen), kit.wood);
            float straightEnd = a0.z - 0.6f;
            float funnelEnd = a0.z;
            float wallH = 1.3f, wallY = 0.3f;                              // spans -0.35 .. 0.95: under the sloped floor, above the pile
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(hop, "Hopper Wall", new Vector3(s * (halfW + wt * 0.5f), wallY, (straightEnd - 0.3f) * 0.5f), Quaternion.identity,
                    new Vector3(wt, wallH, straightEnd + 0.3f), kit.woodDark);
                float dx = halfW - (rIn + 0.02f), dz = funnelEnd - straightEnd;
                float len = Mathf.Sqrt(dx * dx + dz * dz);
                MachineGeometry.Box(hop, "Funnel Wall", new Vector3(s * ((halfW + rIn + 0.02f) * 0.5f + wt * 0.5f), wallY, (straightEnd + funnelEnd) * 0.5f),
                    Quaternion.Euler(0f, -s * Mathf.Atan2(dx, dz) * Mathf.Rad2Deg, 0f), new Vector3(wt, wallH, len), kit.woodDark);
                MachineGeometry.Box(hop, "Mouth Wall", new Vector3(s * (rIn + 0.02f + wt * 0.5f), wallY, funnelEnd + 0.15f), Quaternion.identity,
                    new Vector3(wt, wallH, 0.3f), kit.woodDark);
            }

            // Gear motor housing beside the hopper (decor).
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);
            MachineGeometry.Box(root.transform, "Motor Housing", new Vector3(1.75f, 0.3f, 1.2f), Quaternion.identity, new Vector3(0.6f, 0.6f, 0.8f), kit.castIron, false);
            MachineGeometry.Cylinder(root.transform, "Motor Gear", new Vector3(1.4f, 0.35f, 1.2f), alongX, 0.28f, 0.08f, kit.brass, false);
            MachineGeometry.Cylinder(root.transform, "Drive Shaft", new Vector3(1.3f, 0.35f, 1.2f), alongX, 0.04f, 0.3f, kit.chrome, false);
            MachineGeometry.Cylinder(root.transform, "Motor Cap", new Vector3(2.15f, 0.3f, 1.2f), alongX, 0.18f, 0.2f, kit.castIron, false);

            // ---- top tray ----
            Vector3 topLip = a1 + lowSide * rIn;
            var tray = MachineGeometry.Group(root.transform, "Outlet Tray").transform;
            tray.localPosition = topLip;
            tray.localRotation = Quaternion.Euler(TrayDegrees, 0f, 0f);
            float trayHalf = Mathf.Max(0.55f, config.chuteWidth * 0.5f + 0.1f), trayLen = TrayLen;   // chuteWidth + 0.2 wide: the next connector's catch
            MachineGeometry.Box(tray, "Tray Floor", new Vector3(0f, -0.05f, trayLen * 0.5f), Quaternion.identity, new Vector3((trayHalf + wt) * 2f, 0.1f, trayLen), kit.wood);
            for (int s = -1; s <= 1; s += 2)
                MachineGeometry.Box(tray, "Tray Wall", new Vector3(s * (trayHalf + wt * 0.5f), 0.2f, trayLen * 0.5f - 0.25f), Quaternion.identity,
                    new Vector3(wt, 0.5f, trayLen + 0.5f), kit.woodDark);
            Vector3 exitLocal = topLip + Quaternion.Euler(TrayDegrees, 0f, 0f) * new Vector3(0f, 0.04f, trayLen);

            // ---- trestles ----
            for (int ti = 0; ti < 4; ti++)
            {
                float s = 1.2f + ti * (tubeLength - 2.4f) / 3f;
                Vector3 p = a0 + dir * s + lowSide * (rIn + 0.1f);
                MachineGeometry.Box(root.transform, "Trestle Beam", p, frame, new Vector3(1.1f, 0.1f, 0.16f), kit.woodDark, false);
                float h = p.y - 0.05f + rootY;
                if (h > 0.1f)
                    for (int sx = -1; sx <= 1; sx += 2)
                        MachineGeometry.Box(root.transform, "Trestle Leg", new Vector3(sx * 0.5f, p.y - 0.05f - h * 0.5f, p.z), Quaternion.identity, new Vector3(0.12f, h, 0.12f), kit.wood, false);
                MachineGeometry.Box(root.transform, "Trestle Cleat", p + new Vector3(0f, 0.06f, 0f), frame, new Vector3(0.5f, 0.05f, 0.12f), kit.chrome, false);
            }

            float ph = topLip.y + rootY - 0.5f;
            if (ph > 0.1f)
                for (int sx = -1; sx <= 1; sx += 2)
                    MachineGeometry.Cylinder(root.transform, "Tray Post", new Vector3(sx * 0.5f, topLip.y - 0.5f - ph * 0.5f, topLip.z + 1.2f), Quaternion.identity, 0.05f, ph, kit.chrome, false);

            st.displayName = "Screw Lift";
            st.entry = MachineGeometry.Anchor(root.transform, "Entry", Vector3.zero);
            st.exit = MachineGeometry.Anchor(root.transform, "Exit", exitLocal);
            st.cameraFocus = MachineGeometry.Anchor(root.transform, "Camera Focus", a0 + dir * (tubeLength * 0.5f));
            st.cameraOffset = placement.Rotation * new Vector3(-11f, 1.0f, 0f);
            st.tourSeconds = 9f;
            Vector3 centre = new Vector3(0f, exitLocal.y * 0.5f - 0.2f, (exitLocal.z - 0.3f) * 0.5f), half = new Vector3(1.2f, exitLocal.y * 0.5f + 0.9f, (exitLocal.z + 0.3f) * 0.5f);
            var b = new Bounds(root.transform.TransformPoint(centre), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                b.Encapsulate(root.transform.TransformPoint(centre + Vector3.Scale(c, half)));
            }

            st.activityBounds = b;
            return st;
        }

        static Mesh HelicoidMesh(float rInner, float rOuter, float yStart, float yEnd, float pitch)
        {
            float turns = (yEnd - yStart) / pitch;
            int steps = Mathf.CeilToInt(turns * 48f);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps;
                float phi = u * turns * 2f * Mathf.PI;
                float y = yStart + pitch * phi / (2f * Mathf.PI);
                float s = Mathf.Sin(phi), c = Mathf.Cos(phi);
                verts.Add(new Vector3(rInner * s, y, rInner * c));
                verts.Add(new Vector3(rOuter * s, y, rOuter * c));
                uvs.Add(new Vector2(u * turns, 0f));
                uvs.Add(new Vector2(u * turns, 1f));
            }

            for (int i = 0; i < steps; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

            var mesh = new Mesh();
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
