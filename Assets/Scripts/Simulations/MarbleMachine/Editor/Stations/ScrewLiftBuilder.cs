using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Screw Lift: an Archimedes screw in an inclined glass tube (mach-001 station 9).
    /// Sizes from the ball radius r = 0.065: glass bore 11 r (0.72 m radius, 11 balls across; 2x), pitch 1.0 m. Helical grooves (ribs on
    /// the glass between the flight turns) and a high-friction tube stop balls sliding back down.
    /// Capacity: annulus (bore minus shaft) = pi (0.3575^2 - 0.07^2) = 0.39 m^2, so one pitch of tube holds 0.39 m^3 =
    /// 0.39 x 478 balls/m^3 (bulk, 55% packed) = 187 balls at 100% fill. A 45 deg screw runs ~20% full: ~37 balls per turn.
    /// At 330 deg/s that is 0.917 turns/s, so ~34 balls/s (needs only 15/s sustained, 20/s with margin). Axial speed 0.92 m/s.
    /// Flight angle at the glass is atan(P / 2 pi r) = 24 deg, under the 45 deg limit where balls would slide back
    /// (tan(flight) must stay under tan(90 - incline)), so they ride ~27 deg up the wall in a stable pocket.
    /// </summary>
    public static class ScrewLiftBuilder
    {
        const float RaceInclineDegrees = 40f; // mach-002: shallower than mach-001, so a resting racer is far more likely to stay put
        const float InclineDegrees = 45f, ShaftRadius = 0.07f, RiseTarget = 6.1f; // RiseTarget: mach-001; the lap race takes MachineRaceConfig.screwLiftRise
        const float BoreInBalls = 11f, MouthDrop = 0.8f, BottomZ = 3.0f, TrayLen = 1.5f, TrayDegrees = 15f; // bore 2x (was 5.5)
        const float BaseRibHeight = 0.045f; // 0.7 r at r 0.065: tall enough to stop a ball slipping back, short enough that it still rolls over
        const bool RaceCleats = false; // off: no measured benefit at pitch 1.2, and they narrow the pocket
        const int SegmentsPerTurn = 24;
        const float IntakeReach = 0.5f;   // how far the screw extends below the glass mouth into the hopper

        public static ScrewLiftStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            float r = config.ballRadius;
            // Glass inner radius: 11 r (0.715 m) at the mach-001 ball; capped near 0.72 m for bigger balls (11 r at r 0.2 would be a 4.4 m
            // tube), which is still 3.6 racer-diameters across, with the pitch and ribs scaled so a racer fits between flight and rib.
            float rIn = Mathf.Min(BoreInBalls * r, Mathf.Max(BoreInBalls * 0.065f, 3.6f * r));
            // Racers: pitch 1.2 m, tested in an isolated physics scene (5 racers, 40 deg, 18 s): 1.2 m carries the pack at up to ~230 deg/s;
            // 1.6 m carries some of it and 2.0 m+ carries none (the flight is too steep for a ball to ride). Plain ribs/cleats made no difference.
            float Pitch = Mathf.Max(1.0f, (config is MachineRaceConfig ? 6f : 8f) * r);        // axial distance per turn: a 0.4 m ball needs a pocket over 0.4 m between flight and rib
            bool raceBall = config is MachineRaceConfig;
            float RibHeight = Mathf.Max(BaseRibHeight, 0.7f * r);
            float RibThickness = raceBall ? 0.06f : 0.05f;
            float rOut = rIn - 0.3f * r;                  // flight outer edge: gap < 0.4 r

            float incline = raceBall ? RaceInclineDegrees : InclineDegrees;
            float sinI = Mathf.Sin(incline * Mathf.Deg2Rad);
            // Entry -> exit lip rise = RiseTarget: exit.y = -MouthDrop + L sin(incline) + 0.04 cos(tray) - TrayLen sin(tray).
            // Two lifts in one scene (mach-002) are built under groups named "Lift N"; their mesh assets get that suffix so they don't overwrite each other.
            string sfx = parent != null && parent.name.StartsWith("Lift ") ? "_" + parent.name.Replace(' ', '_') : string.Empty;
            float rise = config is MachineRaceConfig raceConfig ? raceConfig.screwLiftRise : RiseTarget;
            float tubeLength = (rise + MouthDrop - 0.04f * Mathf.Cos(TrayDegrees * Mathf.Deg2Rad) + TrayLen * Mathf.Sin(TrayDegrees * Mathf.Deg2Rad)) / sinI;
            var root = new GameObject("Screw Lift");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            var st = root.AddComponent<ScrewLiftStation>();
            float rootY = root.transform.position.y;

            // Local +Y must point up the incline at `incline` degrees ABOVE HORIZONTAL: tilt it from vertical by (90 - incline). Euler(incline) only
            // equals that at 45 degrees (mach-001); at any other angle it builds the complementary slope and the offsets below stop matching.
            Quaternion frame = Quaternion.Euler(90f - incline, 0f, 0f);
            Vector3 dir = frame * Vector3.up;
            // Perpendicular to the axis in the vertical plane, pointing down and forward: (0, -cos45, +sin45).
            Vector3 lowSide = new Vector3(0f, -Mathf.Cos(incline * Mathf.Deg2Rad), Mathf.Sin(incline * Mathf.Deg2Rad));
            float mouthDrop = MouthDrop;                                    // the hopper floor falls this far from the entry to the mouth (0.5 m = 14 deg)
            float bottomZ = BottomZ;                                        // where the glass bottom line starts
            Vector3 a0 = new Vector3(0f, -mouthDrop - lowSide.y * rIn, bottomZ - lowSide.z * rIn);
            Vector3 a1 = a0 + dir * tubeLength;
            st.frameRotation = frame;
            st.pitch = Pitch;
            if (raceBall)
            {
                // Racers: capped at ~220 deg/s (2.5 m/s at the flight's edge): the isolated test carried 5 of 5 racers at 200 and 4 of 5 at 260.
                st.tipRadius = rOut;
                st.maxTipSpeed = 2.5f;
            }

            // ---- glass tube (static, non-convex) ----
            Mesh tubeMesh = MachineGeometry.OpenCylinder(rIn, tubeLength, 40, true);
            var tube = MachineGeometry.MeshObject(root.transform, "Glass Tube", tubeMesh, "ScrewLift_Tube" + sfx, kit.glass, true, false);
            tube.GetComponent<MeshCollider>().sharedMaterial = MachineGeometry.GripPhysics; // grip, so balls ride up instead of slipping back
            tube.transform.localPosition = a0;
            tube.transform.localRotation = frame;

            // ---- rotor: shaft + flight boxes on one kinematic body, plus a visual helicoid ----
            var rotorGo = new GameObject("Rotor");
            rotorGo.transform.SetParent(root.transform, false);
            rotorGo.transform.localPosition = a0;
            rotorGo.transform.localRotation = frame;
            // The flight starts INTO the hopper, below the glass mouth: the floor ends at the tube's low corner, and a flight that
            // began 0.12 m up the tube left a gap where balls reached the corner with nothing to catch them. The buried lower half
            // is hidden in the floor; the upper half scoops the pile and feeds the tube.
            float yStart = -IntakeReach, yEnd = tubeLength - 0.02f;   // flight runs to the lip so the last turn pushes balls out
            MachineGeometry.Cylinder(rotorGo.transform, "Shaft", new Vector3(0f, (yStart + tubeLength - 0.04f) * 0.5f, 0f), Quaternion.identity, ShaftRadius, tubeLength - 0.04f - yStart, kit.chrome, true);
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
                box.size = new Vector3(radialLen, raceBall ? 0.06f : 0.04f, tangLen);
                box.sharedMaterial = MachineGeometry.WallPhysics;
            }

            // Cleats: radial bars across the flight, standing proud of both faces, about 1.3 ball diameters apart along the turn. They
            // make a row of notches on the flight, so a racer rests in a groove against the face that is lifting it instead of
            // rolling round and down it. Racers only; mach-001's thousands of small balls are unchanged.
            if (raceBall && RaceCleats)
            {
                float cleatHeight = Mathf.Max(0.04f, 0.3f * r);
                float cleatThickness = Mathf.Max(0.04f, 0.25f * r);
                float cleatPitch = 1.3f * 2f * r;                                   // tangential spacing, metres at the mid radius
                int cleatEvery = Mathf.Max(1, Mathf.RoundToInt(cleatPitch / (2f * Mathf.PI * rm / SegmentsPerTurn)));
                for (int k = 1; k < segs; k += cleatEvery)
                {
                    float phi = (k + 0.5f) / SegmentsPerTurn * 2f * Mathf.PI;
                    float y = yStart + Pitch * phi / (2f * Mathf.PI);
                    Vector3 radial = new Vector3(Mathf.Sin(phi), 0f, Mathf.Cos(phi));
                    Vector3 tangent = new Vector3(rm * Mathf.Cos(phi), Pitch / (2f * Mathf.PI), -rm * Mathf.Sin(phi)).normalized;
                    Vector3 normal = Vector3.Cross(radial, tangent).normalized;
                    var cleat = MachineGeometry.Box(colliders, "Cleat", radial * rm + new Vector3(0f, y, 0f), Quaternion.LookRotation(tangent, normal),
                        new Vector3(radialLen * 0.96f, 0.06f + 2f * cleatHeight, cleatThickness), kit.brass);
                }
            }

            var flight = MachineGeometry.MeshObject(rotorGo.transform, "Flight", HelicoidMesh(rInner, rOut, yStart, yEnd, Pitch), "ScrewLift_Flight" + sfx, kit.brass, false);
            flight.transform.localPosition = Vector3.zero;
            st.rotor = MachineGeometry.MakeKinematic(rotorGo);

            // Grooves: two static helical ribs per pitch on the glass wall, wound like the flight but a third of a pitch either side
            // of it, so the rotor never touches them. A ball resting in the pocket between flights meets a rib before it can slide
            // back to the bottom.
            var grooves = MachineGeometry.Group(root.transform, "Grooves");
            grooves.transform.localPosition = a0;
            grooves.transform.localRotation = frame;
            float rRib = rIn - RibHeight * 0.5f;
            float ribTang = 2f * Mathf.PI * rIn / SegmentsPerTurn * 1.15f;
            foreach (float phase in raceBall ? new float[0] : new[] { 1f / 3f, 2f / 3f })
            {
                int ribSegs = Mathf.CeilToInt((tubeLength - 0.3f) / Pitch * SegmentsPerTurn);
                for (int k = 0; k < ribSegs; k++)
                {
                    float phi = (k + 0.5f) / SegmentsPerTurn * 2f * Mathf.PI;
                    float y = 0.1f + Pitch * (phi / (2f * Mathf.PI) + phase);
                    if (y > tubeLength - 0.15f) break;
                    Vector3 radial = new Vector3(Mathf.Sin(phi), 0f, Mathf.Cos(phi));
                    Vector3 tangent = new Vector3(rRib * Mathf.Cos(phi), Pitch / (2f * Mathf.PI), -rRib * Mathf.Sin(phi)).normalized;
                    Vector3 normal = Vector3.Cross(radial, tangent).normalized;
                    var rib = MachineGeometry.Box(grooves.transform, "Rib", radial * rRib + new Vector3(0f, y, 0f), Quaternion.LookRotation(tangent, normal),
                        new Vector3(RibHeight, RibThickness, ribTang), kit.chrome);
                    rib.GetComponent<BoxCollider>().sharedMaterial = MachineGeometry.GripPhysics;
                }
            }

            // Chrome bands round the glass.
            GameObject firstBand = null;
            for (int i = 0; i < 9; i++)
            {
                float y = i == 0 ? 0.03f : (i == 8 ? tubeLength - 0.03f : tubeLength * i / 8f);
                GameObject band;
                if (firstBand == null)
                {
                    band = MachineGeometry.MeshObject(root.transform, "Band", MachineGeometry.OpenCylinder(rIn + 0.02f, 0.07f, 28, true), "ScrewLift_Band" + sfx, kit.chrome, false);
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
            float halfW = config.chuteWidth * 0.5f, wt = 0.12f; // the hopper is exactly the track's inner width
            // Alignment: the chute arrives with its floor top at the Entry anchor (y 0, z 0) and overlaps 0.25 m past it. A standard
            // entry port (same profile as a connector) starts 0.3 m before the anchor, flat to z 0, then follows the hopper floor's
            // slope line y = -mouthDrop z / bottomZ to z = portEnd. The hopper floor box and the tall hopper walls begin exactly at
            // portEnd, with their top surface on that same line, so the floor top is continuous from the chute to the mouth (no lip,
            // no step), and nothing taller than the port's own walls sits in front of the chute's end.
            const float portStart = -0.3f, portEnd = 1.0f;
            MachineConnectorBuilder.BuildPort(hop, "Entry Port", new[]
            {
                new Vector3(0f, 0f, portStart),
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, -mouthDrop * portEnd / bottomZ, portEnd)
            }, kit, config);
            float floorEnd = bottomZ + 0.05f;
            float slope = Mathf.Atan2(mouthDrop, bottomZ) * Mathf.Rad2Deg;
            float floorLen = (floorEnd - portEnd) / Mathf.Cos(slope * Mathf.Deg2Rad);
            float floorMidZ = (floorEnd + portEnd) * 0.5f;
            float floorMidTop = -mouthDrop * floorMidZ / bottomZ;
            MachineGeometry.Box(hop, "Hopper Floor", new Vector3(0f, floorMidTop - 0.05f, floorMidZ), Quaternion.Euler(slope, 0f, 0f),
                new Vector3((halfW + wt) * 2f, 0.1f, floorLen), kit.wood);
            // Fins: the tube mouth is a tilted ellipse, and off the centre line its underside sits well above the floor (0.06 m at 0.4 m
            // out, 0.2 m at 0.5), so balls on the outer floor ran off the end of the floor past the tube. Two glass fins therefore
            // converge from the port's walls to +-0.4 m at the tube's low line, forcing every ball into the narrow strip where the
            // tube's underside is within a ball diameter of the floor. They reach below the lowest floor point and above the pile.
            float wallBottom = -mouthDrop - 0.4f, wallTop = 0.95f;
            float wallH = wallTop - wallBottom, wallY = (wallTop + wallBottom) * 0.5f;
            float finEndX = Mathf.Max(0.4f, 2.5f * r), finEndZ = bottomZ - 0.1f;   // 2.5 racer-diameters wide at the throat of the fins
            for (int s = -1; s <= 1; s += 2)
            {
                float x0 = halfW + wt * 0.5f, x1 = finEndX + wt * 0.5f;
                Vector3 from = new Vector3(s * x0, wallY, portEnd), to = new Vector3(s * x1, wallY, finEndZ);
                Vector3 d = to - from;
                MachineGeometry.Box(hop, "Hopper Fin", (from + to) * 0.5f, Quaternion.LookRotation(d, Vector3.up),
                    new Vector3(wt, wallH, d.magnitude + 0.05f), kit.glass);
            }

            // Gear motor housing beside the hopper (decor).
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);
            MachineGeometry.Box(root.transform, "Motor Housing", new Vector3(2.7f, 0.3f, 1.2f), Quaternion.identity, new Vector3(0.8f, 0.8f, 1.0f), kit.castIron, false);
            MachineGeometry.Cylinder(root.transform, "Motor Gear", new Vector3(2.2f, 0.35f, 1.2f), alongX, 0.36f, 0.08f, kit.brass, false);
            MachineGeometry.Cylinder(root.transform, "Drive Shaft", new Vector3(2.1f, 0.35f, 1.2f), alongX, 0.05f, 0.3f, kit.chrome, false);
            MachineGeometry.Cylinder(root.transform, "Motor Cap", new Vector3(3.2f, 0.3f, 1.2f), alongX, 0.22f, 0.2f, kit.castIron, false);

            // ---- top tray ----
            Vector3 topLip = a1 + lowSide * rIn;
            var tray = MachineGeometry.Group(root.transform, "Outlet Tray").transform;
            tray.localPosition = topLip;
            tray.localRotation = Quaternion.Euler(TrayDegrees, 0f, 0f);
            float trayLen = TrayLen;
            // The outlet tray is a standard track port, so the chute that takes it on meets an identical profile.
            MachineConnectorBuilder.BuildPort(tray, "Outlet Port", new[] { new Vector3(0f, 0f, -0.5f), new Vector3(0f, 0f, trayLen) }, kit, config);
            Vector3 exitLocal = topLip + Quaternion.Euler(TrayDegrees, 0f, 0f) * new Vector3(0f, 0.04f, trayLen);

            // ---- trestles ----
            for (int ti = 0; ti < 4; ti++)
            {
                float s = 1.2f + ti * (tubeLength - 2.4f) / 3f;
                Vector3 p = a0 + dir * s + lowSide * (rIn + 0.1f);
                MachineGeometry.Box(root.transform, "Trestle Beam", p, frame, new Vector3(2.2f, 0.1f, 0.16f), kit.woodDark, false);
                float h = p.y - 0.05f + rootY;
                if (h > 0.1f)
                    for (int sx = -1; sx <= 1; sx += 2)
                        MachineGeometry.Box(root.transform, "Trestle Leg", new Vector3(sx * 1.0f, p.y - 0.05f - h * 0.5f, p.z), Quaternion.identity, new Vector3(0.12f, h, 0.12f), kit.wood, false);
                MachineGeometry.Box(root.transform, "Trestle Cleat", p + new Vector3(0f, 0.06f, 0f), frame, new Vector3(1.0f, 0.05f, 0.12f), kit.chrome, false);
            }

            float ph = topLip.y + rootY - 0.5f;
            if (ph > 0.1f)
                for (int sx = -1; sx <= 1; sx += 2)
                    MachineGeometry.Cylinder(root.transform, "Tray Post", new Vector3(sx * 1.25f, topLip.y - 0.5f - ph * 0.5f, topLip.z + 1.2f), Quaternion.identity, 0.05f, ph, kit.chrome, false);

            st.displayName = "Screw Lift";
            st.entry = MachineGeometry.Anchor(root.transform, "Entry", Vector3.zero);
            st.exit = MachineGeometry.Anchor(root.transform, "Exit", exitLocal);
            st.cameraFocus = MachineGeometry.Anchor(root.transform, "Camera Focus", a0 + dir * (tubeLength * 0.5f));
            st.cameraOffset = placement.Rotation * new Vector3(-15f, 1.5f, 0f);
            st.tourSeconds = 9f;
            Vector3 centre = new Vector3(0f, exitLocal.y * 0.5f - 0.2f, (exitLocal.z - 0.3f) * 0.5f), half = new Vector3(2.4f, exitLocal.y * 0.5f + 0.9f, (exitLocal.z + 0.3f) * 0.5f);
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

            // Two-sided: the back faces get their own vertices. (Sharing vertices between a face and its reverse made the vertex
            // normals cancel to zero, and the flight lit as white blobs.)
            int front = verts.Count;
            for (int i = 0; i < front; i++)
            {
                verts.Add(verts[i]);
                uvs.Add(uvs[i]);
            }

            for (int i = 0; i < steps; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
                tris.Add(a + front); tris.Add(c + front); tris.Add(b + front);
                tris.Add(b + front); tris.Add(c + front); tris.Add(d + front);
            }

            var mesh = new Mesh();
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            MachineGeometry.FixDegenerateNormals(mesh);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
