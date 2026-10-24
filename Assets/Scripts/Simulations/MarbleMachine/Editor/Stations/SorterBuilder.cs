using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the colour sorter: a deep entry hopper whose wedges split the stream over <c>sorterLanes</c> single-file
    /// lanes, a paddle-wheel singulator across the lanes, a scanner gantry (one ring per lane) and one hinged flap per
    /// lane per colour above the tubes. Entry anchor at local origin, flow along local +Z.
    /// The scene builder then calls <see cref="TubeRackBuilder.Build"/> with the finished sorter.
    /// </summary>
    /// <remarks>
    /// Throughput maths (r 0.065, d 0.13): 8 paddles at 270 deg/s = 6 pockets/s per lane (18/s for 3 lanes, target 15).
    /// Lane slope 4 deg gives a rolling acceleration of g sin4 * 5/7 = 0.49 m/s^2; with ~0.9 m/s leaving the wheel, a ball
    /// is doing ~1.9 m/s at the first flap (2.7 m on), so 5 balls/s are 0.38 m apart there and further apart downstream.
    /// A flap is 0.26 m long (2 d), so two balls never share a flap window.
    /// </remarks>
    public static class SorterBuilder
    {
        const float HopperTiltDeg = 15f;
        const float SideAngleDeg = 36f;
        const float LaneSlopeDeg = 4f;
        const float TubeSpacing = 1.5f;
        const float FlapLength = 0.26f;
        const float FloorThickness = 0.05f;
        const float RailHeight = 0.22f;
        const float CageHeight = 0.8f;
        const float HopperWall = 1.0f;
        const float StraightEnd = 1.3f;
        const float WheelU = 0.45f;
        const float WheelTipRadius = 0.28f;
        const float WheelHubRadius = 0.05f;
        const int PaddleCount = 8;
        const float FunnelHeight = 0.26f;
        const float FunnelBottomRadius = 0.42f;
        const float FunnelTopRadius = 0.56f;
        const float FunnelBelowFloor = 0.09f;
        const float FirstTubeU = 3.2f;
        const float LanePitch = 0.30f;
        const float OuterRail = 0.04f;

        /// <summary>Lowest lane-floor height (above the studio floor) that clears the tube rims with the guide funnel under it.</summary>
        public static float MinFloorAtLastFlap(MachineConfig config)
        {
            return TubeRackBuilder.TubeHeight(config) + FunnelBelowFloor + FunnelHeight + 0.08f;
        }

        public static SorterStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            float r = config.ballRadius;
            float d = 2f * r;
            int n = config.ColourCount;
            int lanes = Mathf.Max(1, config.sorterLanes);
            float laneW = 1.3f * d;
            float pitch = Mathf.Max(LanePitch, laneW + d);
            float gap = pitch - laneW;
            float totalHalf = (lanes - 1) * 0.5f * pitch + laneW * 0.5f + OuterRail;
            float hw = Mathf.Max(config.chuteWidth * 0.5f + 0.2f, totalHalf + 0.15f);
            float convLen = (hw - totalHalf) / Mathf.Tan(SideAngleDeg * Mathf.Deg2Rad);
            float hopperEnd = StraightEnd + convLen;
            float lastCentreU = FirstTubeU + (n - 1) * TubeSpacing;
            float laneLength = lastCentreU + 1.0f;
            float scanU = 1.5f;
            var laneX = new float[lanes];
            for (int l = 0; l < lanes; l++) laneX[l] = (l - (lanes - 1) * 0.5f) * pitch;

            var root = new GameObject("Sorter");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(placement.entry, placement.Rotation);
            var st = root.AddComponent<SorterStation>();
            var t = root.transform;
            float rootY = t.position.y;

            Quaternion hopperRot = Quaternion.Euler(HopperTiltDeg, 0f, 0f);
            Quaternion laneRot = Quaternion.Euler(LaneSlopeDeg, 0f, 0f);
            Vector3 p0 = hopperRot * new Vector3(0f, 0f, hopperEnd);
            System.Func<float, float, float, Vector3> lanePos = (x, y, u) => p0 + laneRot * new Vector3(x, y, u);

            // ---- Entry hopper: tilted frame, walls 1.0 m high so ~300 queued balls (0.58 m^3) never spill. ----
            var hopper = MachineGeometry.Group(t, "Hopper");
            hopper.transform.localRotation = hopperRot;
            var ht = hopper.transform;
            float hopperLen = hopperEnd + 0.1f;
            MachineGeometry.Box(ht, "Hopper Floor", new Vector3(0f, -0.03f, -0.1f + hopperLen * 0.5f), Quaternion.identity,
                new Vector3(2f * hw + 0.12f, 0.06f, hopperLen), kit.woodDark);
            for (int s = -1; s <= 1; s += 2)
            {
                Wall(ht, "Mouth Wall", new Vector2(s * hw, -0.1f), new Vector2(s * hw, StraightEnd), HopperWall, 0.04f, kit.wood);
                Wall(ht, "V Wall", new Vector2(s * hw, StraightEnd), new Vector2(s * totalHalf, hopperEnd), HopperWall, 0.04f, kit.wood);
            }

            // Splitter wedges between lane mouths: a solid ridge, so balls fall to either side and none can rest on top.
            Mesh wedgeMesh = null;
            for (int l = 0; l < lanes - 1; l++)
            {
                float xm = (laneX[l] + laneX[l + 1]) * 0.5f;
                GameObject wedge;
                if (wedgeMesh == null)
                {
                    wedge = MachineGeometry.MeshObject(ht, "Splitter Wedge", WedgeMesh(gap, StraightEnd, hopperEnd, RailHeight),
                        "Sorter_Wedge", kit.chrome, true, true);
                    wedgeMesh = wedge.GetComponent<MeshFilter>().sharedMesh;
                }
                else
                {
                    wedge = SharedMeshObject(ht, "Splitter Wedge", wedgeMesh, kit.chrome);
                    var mc = wedge.AddComponent<MeshCollider>();
                    mc.sharedMesh = wedgeMesh;
                    mc.convex = true;
                    mc.sharedMaterial = MachineGeometry.WallPhysics;
                }

                wedge.transform.localPosition = new Vector3(xm, 0f, 0f);
            }

            // ---- Lanes. ----
            var lane = MachineGeometry.Group(t, "Lanes");
            lane.transform.localPosition = p0;
            lane.transform.localRotation = laneRot;
            var lt = lane.transform;

            var hinges = new float[n];
            for (int i = 0; i < n; i++) hinges[i] = FirstTubeU + i * TubeSpacing - FlapLength * 0.5f;

            // Floor per lane in segments between the flap windows.
            for (int l = 0; l < lanes; l++)
            {
                float segStart = 0f;
                for (int i = 0; i <= n; i++)
                {
                    float segEnd = i < n ? hinges[i] : laneLength;
                    if (segEnd - segStart > 0.001f)
                        MachineGeometry.Box(lt, "Floor " + l + "." + i, new Vector3(laneX[l], -FloorThickness * 0.5f, (segStart + segEnd) * 0.5f),
                            Quaternion.identity, new Vector3(laneW, FloorThickness, segEnd - segStart), kit.asphalt);
                    if (i < n) segStart = hinges[i] + FlapLength;
                }
            }

            // Dividers and outer rails: solid blocks down to the floor underside, taller around the singulator wheel.
            var railX = new List<float>();
            var railW = new List<float>();
            railX.Add(laneX[0] - laneW * 0.5f - OuterRail * 0.5f);
            railW.Add(OuterRail);
            for (int l = 0; l < lanes - 1; l++)
            {
                railX.Add((laneX[l] + laneX[l + 1]) * 0.5f);
                railW.Add(gap);
            }

            railX.Add(laneX[lanes - 1] + laneW * 0.5f + OuterRail * 0.5f);
            railW.Add(OuterRail);
            for (int k = 0; k < railX.Count; k++)
            {
                MachineGeometry.Box(lt, "Rail", new Vector3(railX[k], (RailHeight - FloorThickness) * 0.5f, laneLength * 0.5f), Quaternion.identity,
                    new Vector3(railW[k], RailHeight + FloorThickness, laneLength), k == 0 || k == railX.Count - 1 ? kit.chrome : kit.woodDark);
                MachineGeometry.Box(lt, "Wheel Cage", new Vector3(railX[k], CageHeight * 0.5f, WheelU + 0.1f), Quaternion.identity,
                    new Vector3(Mathf.Min(railW[k], 0.05f), CageHeight, 0.95f), kit.chrome);
            }

            // ---- Singulator: one kinematic paddle wheel, a pocket set per lane. ----
            float wheelCentreY = WheelTipRadius + 0.025f;
            var wheel = MachineGeometry.Group(t, "Singulator");
            wheel.transform.position = lane.transform.TransformPoint(new Vector3(0f, wheelCentreY, WheelU));
            wheel.transform.rotation = t.rotation;
            var wt = wheel.transform;
            var wheelBody = MachineGeometry.MakeKinematic(wheel);
            float padW = laneW - 0.02f;
            float padLen = WheelTipRadius - WheelHubRadius;
            for (int l = 0; l < lanes; l++)
            {
                for (int k = 0; k < PaddleCount; k++)
                {
                    Quaternion q = Quaternion.Euler(k * 360f / PaddleCount, 0f, 0f);
                    Vector3 pp = q * new Vector3(0f, 0f, WheelHubRadius + padLen * 0.5f);
                    MachineGeometry.Box(wt, "Paddle " + l + "." + k, new Vector3(laneX[l], pp.y, pp.z), q, new Vector3(padW, 0.03f, padLen), kit.copper);
                }

                MachineGeometry.Cylinder(wt, "Hub", new Vector3(laneX[l], 0f, 0f), Quaternion.Euler(0f, 0f, 90f), WheelHubRadius, padW, kit.brass);
            }

            float axleLen = 2f * totalHalf + 0.5f;
            MachineGeometry.Cylinder(wt, "Axle", Vector3.zero, Quaternion.Euler(0f, 0f, 90f), 0.02f, axleLen, kit.chrome, false);
            for (int s = -1; s <= 1; s += 2)
            {
                float gx = s * (totalHalf + 0.1f);
                MachineGeometry.Cylinder(wt, "Gear", new Vector3(gx, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.17f, 0.03f, kit.castIron, false);
                MachineGeometry.Cylinder(wt, "Gear Cap", new Vector3(gx + s * 0.03f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.06f, 0.03f, kit.brass, false);
                for (int k = 0; k < 10; k++)
                {
                    Quaternion q = Quaternion.Euler(k * 36f, 0f, 0f);
                    MachineGeometry.Box(wt, "Tooth", q * new Vector3(gx, 0f, 0.175f), q, new Vector3(0.03f, 0.04f, 0.04f), kit.castIron, false);
                }
            }

            // ---- Scanner gantry: posts and a cross bar over all lanes, a flashing ring around each lane. ----
            var scan = MachineGeometry.Group(lt, "Scanner Gantry");
            scan.transform.localPosition = new Vector3(0f, 0f, scanU);
            for (int s = -1; s <= 1; s += 2)
                MachineGeometry.Box(scan.transform, "Post", new Vector3(s * (totalHalf + 0.04f), 0.3f, 0f), Quaternion.identity,
                    new Vector3(0.05f, 0.6f, 0.08f), kit.chrome, false);
            MachineGeometry.Box(scan.transform, "Cross Bar", new Vector3(0f, 0.6f, 0f), Quaternion.identity,
                new Vector3(2f * totalHalf + 0.13f, 0.06f, 0.1f), kit.chrome, false);
            var rings = new Renderer[lanes];
            Mesh ringMesh = null;
            for (int l = 0; l < lanes; l++)
            {
                var armGo = MachineGeometry.Box(scan.transform, "Arm", new Vector3(laneX[l], 0.45f, 0f), Quaternion.identity,
                    new Vector3(0.02f, 0.3f, 0.02f), kit.chrome, false);
                armGo.name = "Arm " + l;
                GameObject ring;
                if (ringMesh == null)
                {
                    ring = MachineGeometry.MeshObject(scan.transform, "Scan Ring", MachineGeometry.OpenCylinder(0.13f, 0.05f, 32, true),
                        "Sorter_ScanRing", kit.emissive, false);
                    ringMesh = ring.GetComponent<MeshFilter>().sharedMesh;
                }
                else
                {
                    ring = SharedMeshObject(scan.transform, "Scan Ring", ringMesh, kit.emissive);
                }

                ring.transform.localPosition = new Vector3(laneX[l], 0.2f, -0.025f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                rings[l] = ring.GetComponent<Renderer>();
            }

            // ---- Flap gates (one per lane per colour) and one guide funnel per tube. ----
            var flapBodies = new Rigidbody[lanes * n];
            var restPos = new Vector3[lanes * n];
            var restRot = new Quaternion[lanes * n];
            var hingeLocal = new Vector3[lanes * n];
            var drops = new Transform[n];
            Mesh funnelMesh = null;
            var dropRoot = MachineGeometry.Group(t, "Drop Points");
            for (int i = 0; i < n; i++)
            {
                float uC = hinges[i] + FlapLength * 0.5f;
                for (int l = 0; l < lanes; l++)
                {
                    int f = l * n + i;
                    var flap = MachineGeometry.Box(lt, "Flap " + config.colours[i].name + " " + l, new Vector3(laneX[l], -FloorThickness * 0.5f, uC),
                        Quaternion.identity, new Vector3(laneW - 0.01f, FloorThickness, FlapLength), kit.chrome);
                    flapBodies[f] = MachineGeometry.MakeKinematic(flap);
                    restPos[f] = t.InverseTransformPoint(flap.transform.position);
                    restRot[f] = Quaternion.Inverse(t.rotation) * flap.transform.rotation;
                    var hinge = MachineGeometry.Cylinder(lt, "Hinge", new Vector3(laneX[l], -FloorThickness * 0.5f, hinges[i]),
                        Quaternion.Euler(0f, 0f, 90f), 0.015f, laneW + 0.02f, kit.brass, false);
                    hingeLocal[f] = t.InverseTransformPoint(hinge.transform.position);
                }

                // Colour marks on every divider and rail, level with the window.
                for (int k = 0; k < railX.Count; k++)
                    MachineGeometry.Box(lt, "Gate Mark", new Vector3(railX[k], RailHeight + 0.01f, uC), Quaternion.identity,
                        new Vector3(Mathf.Max(railW[k] * 0.8f, 0.03f), 0.02f, FlapLength), kit.ballMaterials[i], false);

                Vector3 c = lanePos(0f, 0f, uC);
                float coneTop = lanePos(0f, -FloorThickness, uC).y - FunnelBelowFloor;
                GameObject funnel;
                if (funnelMesh == null)
                {
                    funnel = MachineGeometry.MeshObject(t, "Guide Funnel",
                        MachineGeometry.Lathe(new[] { new Vector2(FunnelBottomRadius, 0f), new Vector2(FunnelTopRadius, FunnelHeight) }, 40, true),
                        "Sorter_Funnel", kit.chrome, true, false);
                    funnelMesh = funnel.GetComponent<MeshFilter>().sharedMesh;
                }
                else
                {
                    funnel = SharedMeshObject(t, "Guide Funnel", funnelMesh, kit.chrome);
                    var mc = funnel.AddComponent<MeshCollider>();
                    mc.sharedMesh = funnelMesh;
                    mc.sharedMaterial = MachineGeometry.WallPhysics;
                }

                funnel.transform.localPosition = new Vector3(0f, coneTop - FunnelHeight, c.z);
                drops[i] = MachineGeometry.Anchor(dropRoot.transform, "Drop " + config.colours[i].name, new Vector3(0f, coneTop - FunnelHeight - 0.02f, c.z));
            }

            // ---- Supports: legs to the floor (between the tubes), cross beams under the lanes and hopper. ----
            float floorLocalY = -rootY;
            float legX = totalHalf + 0.35f;
            var legPoints = new List<Vector3>();
            foreach (float z in new[] { 0.2f, 1.0f })
            {
                Vector3 hp = hopperRot * new Vector3(0f, -0.06f, z);
                legPoints.Add(new Vector3(0f, hp.y - 0.08f, hp.z));
            }

            legPoints.Add(lanePos(0f, -FloorThickness - 0.08f, 1.6f));
            for (int k = 0; k < n - 1; k += 2)
                legPoints.Add(lanePos(0f, -FloorThickness - 0.08f, FirstTubeU + k * TubeSpacing + TubeSpacing * 0.5f));
            legPoints.Add(lanePos(0f, -FloorThickness - 0.08f, laneLength - 0.4f));

            int li = 0;
            foreach (Vector3 lp in legPoints)
            {
                float h = lp.y - floorLocalY;
                if (h < 0.2f) continue;
                MachineGeometry.Box(t, "Cross Beam " + li, lp, laneRot, new Vector3(2f * legX + 0.1f, 0.12f, 0.14f), kit.woodDark);
                for (int s = -1; s <= 1; s += 2)
                {
                    MachineGeometry.Cylinder(t, "Leg", new Vector3(s * legX, floorLocalY + h * 0.5f, lp.z), Quaternion.identity, 0.05f, h, kit.chrome);
                    MachineGeometry.Cylinder(t, "Foot", new Vector3(s * legX, floorLocalY + 0.02f, lp.z), Quaternion.identity, 0.15f, 0.04f, kit.castIron);
                }

                li++;
            }

            // ---- Anchors and station fields. ----
            var exit = MachineGeometry.Anchor(t, "Exit", lanePos(0f, -0.1f, laneLength + 0.12f));
            Vector3 mid = lanePos(0f, 0f, laneLength * 0.5f);
            float focusY = 0.55f * (rootY + mid.y) - rootY;

            st.displayName = "Colour Sorter";
            st.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            st.exit = exit;
            st.recycleExit = exit;
            st.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", new Vector3(0f, focusY, mid.z));
            st.cameraOffset = placement.Rotation * new Vector3(8f, 1.2f, 0f);
            st.tourSeconds = 10f;
            st.laneFrame = lt;
            st.laneWidth = laneW;
            st.laneLength = laneLength;
            st.scanU = scanU;
            st.laneCentresX = laneX;
            st.singulator = wheelBody;
            st.singulatorRestPos = t.InverseTransformPoint(wheel.transform.position);
            st.flapBodies = flapBodies;
            st.flapRestPos = restPos;
            st.flapRestRot = restRot;
            st.flapHinge = hingeLocal;
            st.gateHingeU = hinges;
            st.flapLength = FlapLength;
            st.dropPoints = drops;
            st.scannerRings = rings;

            var bounds = new Bounds(t.position, Vector3.zero);
            foreach (Vector3 lp in new[] { Vector3.zero, p0, lanePos(0f, 0f, laneLength), mid })
            {
                bounds.Encapsulate(t.TransformPoint(lp) + new Vector3(1.4f, 1.4f, 1.4f));
                bounds.Encapsulate(t.TransformPoint(lp) - new Vector3(1.4f, 1.4f, 1.4f));
            }

            st.activityBounds = bounds;

            // Tubes stand on the floor under the drop points: the funnel bottom must clear the rim.
            float lowestFloor = rootY + lanePos(0f, 0f, hinges[n - 1] + FlapLength * 0.5f).y;
            float needed = MinFloorAtLastFlap(config);
            if (lowestFloor < needed)
                Debug.LogWarning("Sorter: lane floor at the last flap is " + lowestFloor.ToString("F2") + " m above the floor, needs at least "
                    + needed.ToString("F2") + " m (tube rim + funnel). Raise the sorter root to y >= " + (rootY + needed - lowestFloor).ToString("F2") + ".");
            return st;
        }

        /// <summary>Wall from <paramref name="a"/> to <paramref name="b"/> (x, z in the parent frame), standing on y=0.</summary>
        static GameObject Wall(Transform parent, string name, Vector2 a, Vector2 b, float height, float thickness, Material material)
        {
            Vector2 dir = b - a;
            Vector2 mid = (a + b) * 0.5f;
            return MachineGeometry.Box(parent, name, new Vector3(mid.x, height * 0.5f, mid.y),
                Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y)), new Vector3(thickness, height, dir.magnitude + thickness * 0.5f), material);
        }

        /// <summary>Triangular ridge between two lane mouths: apex at z0 (x 0), base <paramref name="baseWidth"/> wide at z1, ridge rising to <paramref name="height"/>.</summary>
        static Mesh WedgeMesh(float baseWidth, float z0, float z1, float height)
        {
            var a0 = new Vector3(0f, 0f, z0);
            var b0 = new Vector3(-baseWidth * 0.5f, 0f, z1);
            var c0 = new Vector3(baseWidth * 0.5f, 0f, z1);
            var r1 = new Vector3(0f, height * 0.25f, z0);
            var r2 = new Vector3(0f, height, z1);
            var verts = new List<Vector3> { a0, b0, c0, r1, r2 };
            var tris = new List<int> { 0, 1, 4, 0, 4, 3, 0, 3, 4, 0, 4, 2, 1, 2, 4 };
            int count = tris.Count;
            for (int i = 0; i < count; i += 3)
            {
                tris.Add(tris[i]);
                tris.Add(tris[i + 2]);
                tris.Add(tris[i + 1]);
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static GameObject SharedMeshObject(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
