using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Bucket Wheel (mach-001 station 5): a cantilevered ferris wheel of 16 open-fronted copper buckets.
    /// The rim, spokes and tower stand on the +X side; the buckets hang off it at |x| &lt; 0.35. They shovel from a pit
    /// (entry side, -Z), climb the +Z side and tip over the top onto an apron beside/below them; the apron drains toward -X
    /// into a collector that runs out along +Z. Nothing but the buckets ever enters the bucket x range at wheel height, so the
    /// hand-off cannot cross the buckets' swept volume.
    ///
    /// Maths (R = 4.0, bucket W x L x H = 0.70 x 0.55 x 0.42, 16 buckets, 6 rpm = 36 deg/s):
    ///  - Axle height cy = bowlBottom + gap + H/2 + R = -0.30 + 0.02 + 0.21 + 4.0 = 3.93 above the entry. Lowest bucket
    ///    plate passes 0.02 above the pit floor (gap &lt; 0.4 r, so no ball is pinched), bucket speed 2.5 m/s.
    ///  - The pit floor HUGS the lower envelope of the buckets' tilted bottoms (sampled from the station's tilt cam) from
    ///    the entry slope to wheel angle 40 deg on the rising side, so there is no dead layer of balls the buckets cannot
    ///    reach. Past that a shelf (16 deg up) and a tall casing board stand 0.4 m outside the bucket fronts: pushed or spilled
    ///    balls roll back down into the pit instead of being carried up a wedge.
    ///  - Capacity: ~20-25 balls per bucket (0.7 x 0.55 x 0.18 m of heap = 0.07 m^3 x 478 balls/m^3 x 0.6) x 1.6 buckets/s
    ///    = 35+ balls/s at 6 rpm (a 2D disc model of this exact profile, fed 40 balls/s over a 570-ball backlog, delivered
    ///    everything with no spill). The pit floor + entry slope hold the ~400-ball flood backlog.
    ///  - Tipping: cam rolls each bucket 25 deg back for the climb, then 110 deg toward -Z over wheel angle 160-190 deg,
    ///    so the load slides out over the back wall onto the apron (at axle + R - 1.0). The apron plate is kept 0.27 m
    ///    below the lowest tipped bucket and its +Z wall 0.2 m, so nothing is pinched.
    ///  - Rise: entry floor -> exit lip = apron (R - 1.0 + cy = 6.93) - plate drop to the collector (0.19) - collector fall
    ///    (8 deg over ~8.3 m = 1.2) = ~5.5 m; the apron is 3.2 m long (z 0.7..3.9) and the collector runs to z = 9.0.
    /// </summary>
    public static class BucketWheelBuilder
    {
        const int BucketCount = 16;
        const float Radius = 4.0f, CentreZ = 2.6f, BowlBottom = -0.30f, Gap = 0.02f, EntryY = -0.02f, FloorStartZ = -0.6f;
        const float BucketW = 1.40f, Wall = 0.03f; // 2x wide: ~2x the balls per bucket
        /// <summary>Bucket depth and height. 0.55 x 0.42 at r 0.065; grown in <see cref="Build"/> for big balls (a 0.4 m racer needs a 0.64 x 0.52 cup).</summary>
        static float BucketL = 0.55f, BucketH = 0.42f;
        const float HugDegrees = 40f, ShelfDz = 0.4f, ShelfDy = 0.45f, CasingAboveAxle = 2.5f;
        const float PitHalfNarrow = BucketW * 0.5f + 0.035f, PitWallH = 0.9f, PitWallT = 0.06f;
        const float RimX = 1.1f, TowerX = 1.75f;
        /// <summary>Set from the config at the start of <see cref="Build"/>: the pit opens to the track's inner width, the collector is as wide as the track.</summary>
        static float PitHalfWide = 0.9f, CollectorHalf = 0.9f;
        const float ApronFrontDz = 1.9f, ApronBackDz = 1.3f, ApronTilt = 12f;
        /// <summary>Apron depth under the top of the wheel; deepened when the buckets and apron walls grow for big balls.</summary>
        static float ApronDrop = 1.0f, ApronBackWallH = 0.40f, ApronFrontWallH = 0.34f;
        const float ApronMinX = -1.8f, ApronMaxX = 0.76f, CollectorPitch = 8f;
        const float ExitPortLength = 1.2f;

        public static BucketWheelStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            PitHalfWide = config.chuteWidth * 0.5f;
            CollectorHalf = config.chuteWidth * 0.5f;
            // Size the cups and the apron walls from the ball radius (all unchanged at 0.065): a bucket holds a 0.4 m racer with
            // room to spare, and the apron sinks to keep the same clearance under the taller tipped buckets and walls.
            float ballR = config.ballRadius;
            BucketL = Mathf.Max(0.55f, 3.2f * ballR);
            BucketH = Mathf.Max(0.42f, 2.6f * ballR);
            ApronBackWallH = Mathf.Max(0.40f, 2.5f * ballR);
            ApronFrontWallH = Mathf.Max(0.34f, 2.2f * ballR);
            ApronDrop = 1.0f + (BucketH - 0.42f) * 0.5f + (BucketL - 0.55f) * 0.3f + (ApronBackWallH - 0.40f);
            var root = new GameObject("Bucket Wheel");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            var st = root.AddComponent<BucketWheelStation>();
            float rootY = root.transform.position.y;
            float cy = BowlBottom + Gap + BucketH * 0.5f + Radius;
            var centre = new Vector3(0f, cy, CentreZ);
            st.wheelCentre = centre;
            st.radius = Radius;

            List<Vector2> floor = BuildFloorProfile(st);
            float casingTop = cy + CasingAboveAxle;
            BuildPit(root.transform, kit, floor, casingTop, rootY);
            BuildWheelVisual(st, root.transform, kit, centre);
            BuildBuckets(st, root.transform, kit);
            BuildTowers(root.transform, kit, centre, rootY);
            Vector3 exitLocal = BuildApronAndCollector(root.transform, kit, config, cy, rootY);

            st.displayName = "Bucket Wheel";
            st.entry = MachineGeometry.Anchor(root.transform, "Entry", Vector3.zero);
            st.exit = MachineGeometry.Anchor(root.transform, "Exit", exitLocal);
            st.cameraFocus = MachineGeometry.Anchor(root.transform, "Camera Focus", centre + new Vector3(-0.3f, 0.4f, 0.6f));
            st.cameraOffset = placement.Rotation * new Vector3(-19f, 0.5f, 0f);
            st.tourSeconds = 11f;
            Vector3 bc = new Vector3(-1.2f, 3.7f, 3.5f);
            Vector3 half = new Vector3(3.4f, 4.6f, 5.9f);
            var b = new Bounds(root.transform.TransformPoint(bc), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                b.Encapsulate(root.transform.TransformPoint(bc + Vector3.Scale(c, half)));
            }

            st.activityBounds = b;
            return st;
        }

        // ---- pit floor profile: entry slope merged with the lower envelope of the swept buckets ------------------

        static List<Vector2> BuildFloorProfile(BucketWheelStation st)
        {
            // env[bin of 5 cm] = lowest y reached by any bucket point at that z over wheel angles -90..+90 (tilt included).
            var env = new Dictionary<int, float>();
            for (float a = -90f; a <= 90.001f; a += 0.25f)
            {
                Vector3 p = st.PivotAt(a);
                float q = st.TiltAt(a) * Mathf.Deg2Rad, c = Mathf.Cos(q), s = Mathf.Sin(q);
                for (int k = 0; k < 12; k++)
                {
                    float lz = Mathf.Lerp(-BucketL * 0.5f, BucketL * 0.5f, k / 11f);
                    AddEnv(env, p, s, c, lz, -BucketH * 0.5f);              // bottom edge
                    float ly = Mathf.Lerp(-BucketH * 0.5f, BucketH * 0.5f, k / 11f);
                    AddEnv(env, p, s, c, -BucketL * 0.5f, ly);              // back wall outer face
                }
            }

            float zEnd = CentreZ + Radius * Mathf.Sin(HugDegrees * Mathf.Deg2Rad) + BucketL * 0.5f + 0.03f;
            var pts = new List<Vector2>();
            for (int i = 0; ; i++)
            {
                float z = FloorStartZ + i * 0.05f;
                if (z > zEnd + 1e-4f) break;
                float e = EnvAt(env, z) - Gap;
                float line = EntryY + (BowlBottom - EntryY) * Mathf.Clamp01((z - FloorStartZ) / (CentreZ - FloorStartZ));
                pts.Add(new Vector2(z, z < CentreZ ? Mathf.Min(line, e) : e));
            }

            var thin = new List<Vector2>();
            for (int i = 0; i < pts.Count; i += 6) thin.Add(pts[i]);
            if (thin[thin.Count - 1] != pts[pts.Count - 1]) thin.Add(pts[pts.Count - 1]);
            return thin;
        }

        static void AddEnv(Dictionary<int, float> env, Vector3 pivot, float s, float c, float lz, float ly)
        {
            float wz = pivot.z + ly * s + lz * c;
            float wy = pivot.y + ly * c - lz * s;
            int bin = Mathf.RoundToInt(wz / 0.05f);
            if (!env.TryGetValue(bin, out float cur) || wy < cur) env[bin] = wy;
        }

        static float EnvAt(Dictionary<int, float> env, float z)
        {
            int bin = Mathf.RoundToInt(z / 0.05f);
            for (int k = 0; k <= 8; k++)
            {
                if (env.TryGetValue(bin + k, out float v)) return v;
                if (env.TryGetValue(bin - k, out float w)) return w;
            }

            return 99f;
        }

        // ---- pit: hugging floor, side walls with the entry funnel, shelf and casing ------------------------------

        static float HalfWidthAt(float z)
        {
            float u = Mathf.Clamp01((z - 0.3f) / 1.2f);
            return Mathf.Lerp(PitHalfWide, PitHalfNarrow, u * u * (3f - 2f * u));
        }

        static void Segment(Transform pit, MachineKit kit, string tag, Vector2 a, Vector2 b, float floorW, float wallH, bool funnel, float yBelow)
        {
            Vector2 d = (b - a);
            float len2 = d.magnitude;
            Vector2 dn = d / len2;
            Vector2 n2 = new Vector2(-dn.y, dn.x);                          // (z,y) normal, toward the wheel side
            Vector3 dir = new Vector3(0f, dn.y, dn.x), up = new Vector3(0f, n2.y, n2.x);
            Vector3 mid = new Vector3(0f, (a.y + b.y) * 0.5f, (a.x + b.x) * 0.5f);
            MachineGeometry.Box(pit, tag + " Floor", mid - up * 0.05f, Quaternion.LookRotation(dir, up), new Vector3(floorW, 0.1f, len2 + 0.03f), kit.wood);
            for (int s = -1; s <= 1; s += 2)
            {
                float h0 = funnel ? HalfWidthAt(a.x) : PitHalfNarrow, h1 = funnel ? HalfWidthAt(b.x) : PitHalfNarrow;
                float xc = s * ((h0 + h1) * 0.5f + PitWallT * 0.5f);
                Vector3 wdir = new Vector3(s * (h1 - h0), dn.y * len2, dn.x * len2);
                MachineGeometry.Box(pit, tag + " Wall", mid + new Vector3(xc, 0f, 0f) + up * (wallH * 0.5f - yBelow * 0.5f),
                    Quaternion.LookRotation(wdir, up), new Vector3(PitWallT, wallH + yBelow, wdir.magnitude + 0.03f), kit.glass);
            }
        }

        static void BuildPit(Transform root, MachineKit kit, List<Vector2> floor, float casingTop, float rootY)
        {
            var pit = MachineGeometry.Group(root, "Pit").transform;
            float floorW = (PitHalfWide + PitWallT + 0.04f) * 2f;
            for (int i = 0; i < floor.Count - 1; i++)
                Segment(pit, kit, "Pit", floor[i], floor[i + 1], floorW, PitWallH, true, 0f);

            // Shelf: leaves the hugging floor and climbs 16 deg to a line 0.4 m outside the bucket fronts, then the casing board.
            Vector2 end = floor[floor.Count - 1];
            float zc = CentreZ + Radius + BucketL * 0.5f + ShelfDz;
            var shelfTop = new Vector2(zc, end.y + ShelfDy);
            Segment(pit, kit, "Shelf", end, shelfTop, floorW, 0.5f, false, 0f);
            float boardH = casingTop - shelfTop.y;
            MachineGeometry.Box(pit, "Casing Board", new Vector3(0f, shelfTop.y + boardH * 0.5f, zc + 0.05f), Quaternion.identity,
                new Vector3(PitHalfNarrow * 2f + PitWallT * 2f, boardH, 0.1f), kit.wood);
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(pit, "Casing Fin", new Vector3(s * (PitHalfNarrow + PitWallT * 0.5f), shelfTop.y + boardH * 0.5f, zc - 0.25f), Quaternion.identity,
                    new Vector3(PitWallT, boardH, 0.5f), kit.glass);
                // Visible posts behind the casing, down to the studio floor.
                float postTop = casingTop;
                float postH = postTop + rootY;
                if (postH > 0.2f)
                    MachineGeometry.Box(pit, "Casing Post", new Vector3(s * (PitHalfNarrow + 0.3f), postTop - postH * 0.5f, zc + 0.3f), Quaternion.identity,
                        new Vector3(0.14f, postH, 0.14f), kit.woodDark, false);
            }

            MachineGeometry.Box(pit, "Casing Cap", new Vector3(0f, casingTop + 0.04f, zc + 0.05f), Quaternion.identity,
                new Vector3(PitHalfNarrow * 2f + 0.3f, 0.08f, 0.2f), kit.chrome, false);

            // Legs under the pit so it reaches the studio floor.
            if (rootY > 0.3f)
            {
                for (int i = 1; i < floor.Count - 1; i += 4)
                {
                    float h = floor[i].y + rootY - 0.1f;
                    if (h < 0.15f) continue;
                    for (int s = -1; s <= 1; s += 2)
                        MachineGeometry.Box(pit, "Pit Leg", new Vector3(s * (PitHalfWide + 0.1f), floor[i].y - 0.1f - h * 0.5f, floor[i].x), Quaternion.identity,
                            new Vector3(0.16f, h, 0.16f), kit.woodDark, false);
                }
            }
        }

        // ---- rotating decor: rim, spokes, hub, gear, brackets --------------------------------------------------

        static void BuildWheelVisual(BucketWheelStation st, Transform root, MachineKit kit, Vector3 centre)
        {
            var wheel = MachineGeometry.Group(root, "Wheel");
            wheel.transform.localPosition = centre;
            st.wheelVisual = wheel.transform;
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);

            MachineGeometry.Cylinder(wheel.transform, "Hub", new Vector3(RimX, 0f, 0f), alongX, 0.42f, 0.24f, kit.castIron, false);
            MachineGeometry.Cylinder(wheel.transform, "Hub Cap", new Vector3(RimX - 0.15f, 0f, 0f), alongX, 0.22f, 0.08f, kit.brass, false);

            // Big gear on the tower side of the hub.
            float gx = RimX + 0.22f;
            MachineGeometry.Cylinder(wheel.transform, "Gear", new Vector3(gx, 0f, 0f), alongX, 0.95f, 0.07f, kit.castIron, false);
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                float rad = a * Mathf.Deg2Rad;
                MachineGeometry.Box(wheel.transform, "Tooth", new Vector3(gx, -Mathf.Cos(rad) * 1.0f, Mathf.Sin(rad) * 1.0f), Quaternion.Euler(-a, 0f, 0f),
                    new Vector3(0.07f, 0.13f, 0.11f), kit.castIron, false);
            }

            // Rim ring, 48 segments, at the pivot radius so the buckets visibly hang from it.
            int rimSegs = 48;
            float chord = 2f * Radius * Mathf.Sin(Mathf.PI / rimSegs) * 1.05f;
            for (int i = 0; i < rimSegs; i++)
            {
                float a = i * 360f / rimSegs;
                float rad = a * Mathf.Deg2Rad;
                MachineGeometry.Box(wheel.transform, "Rim", new Vector3(RimX, -Mathf.Cos(rad) * (Radius + 0.02f), Mathf.Sin(rad) * (Radius + 0.02f)),
                    Quaternion.Euler(-a, 0f, 0f), new Vector3(0.1f, 0.12f, chord), kit.castIron, false);
            }

            // Spokes, bucket arms and pivot pins, one per bucket.
            float armX = (BucketW * 0.5f + RimX) * 0.5f;
            for (int i = 0; i < BucketCount; i++)
            {
                float a = i * 360f / BucketCount;
                float rad = a * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(0f, -Mathf.Cos(rad), Mathf.Sin(rad));
                MachineGeometry.Box(wheel.transform, "Spoke", new Vector3(RimX, 0f, 0f) + dir * (Radius * 0.5f), Quaternion.Euler(-a, 0f, 0f),
                    new Vector3(0.1f, Radius - 0.4f, 0.1f), kit.wood, false);
                MachineGeometry.Box(wheel.transform, "Arm", new Vector3(armX, 0f, 0f) + dir * Radius, Quaternion.identity,
                    new Vector3(RimX - BucketW * 0.5f, 0.06f, 0.08f), kit.copper, false);
                MachineGeometry.Cylinder(wheel.transform, "Pivot", new Vector3(BucketW * 0.5f + 0.02f, 0f, 0f) + dir * Radius, alongX, 0.05f, 0.1f, kit.brass, false);
            }
        }

        // ---- buckets ------------------------------------------------------------------------------------------
        // Local frame: +Y mouth, back wall at -Z, OPEN FRONT at +Z (the scoop edge on the way up the pit), sides at +-X.

        static void BuildBuckets(BucketWheelStation st, Transform root, MachineKit kit)
        {
            var group = MachineGeometry.Group(root, "Buckets");
            st.buckets = new Rigidbody[BucketCount];
            for (int i = 0; i < BucketCount; i++)
            {
                float a = i * 360f / BucketCount;
                var go = new GameObject($"Bucket {i}");
                go.transform.SetParent(group.transform, false);
                go.transform.localPosition = st.PivotAt(a);
                go.transform.localRotation = Quaternion.Euler(st.TiltAt(a), 0f, 0f);
                MachineGeometry.Box(go.transform, "Floor", new Vector3(0f, -BucketH * 0.5f + Wall * 0.5f, 0f), Quaternion.identity, new Vector3(BucketW, Wall, BucketL), kit.copper);
                MachineGeometry.Box(go.transform, "Back", new Vector3(0f, 0f, -BucketL * 0.5f + Wall * 0.5f), Quaternion.identity, new Vector3(BucketW, BucketH, Wall), kit.copper);
                for (int s = -1; s <= 1; s += 2)
                    MachineGeometry.Box(go.transform, "Side", new Vector3(s * (BucketW * 0.5f - Wall * 0.5f), 0f, 0f), Quaternion.identity,
                        new Vector3(Wall, BucketH, BucketL), kit.copper);
                st.buckets[i] = MachineGeometry.MakeKinematic(go);
            }
        }

        // ---- A-frame tower and axle (+X side only) -----------------------------------------------------------

        static void BuildTowers(Transform root, MachineKit kit, Vector3 centre, float rootY)
        {
            var t = MachineGeometry.Group(root, "Tower");
            Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);
            MachineGeometry.Cylinder(t.transform, "Axle", centre + new Vector3(RimX * 0.5f + TowerX * 0.5f - 0.2f, 0f, 0f), alongX, 0.1f, TowerX - RimX + 0.6f, kit.chrome, false);
            float floorY = -rootY;
            foreach (float zOff in new[] { -2.4f, 2.4f })
            {
                Beam(t.transform, "Leg", new Vector3(TowerX, floorY, centre.z + zOff), new Vector3(TowerX, centre.y, centre.z), 0.2f, kit.wood);
                Beam(t.transform, "Leg Out", new Vector3(TowerX + 0.5f, floorY, centre.z + zOff * 0.95f), new Vector3(TowerX, centre.y, centre.z), 0.14f, kit.woodDark);
            }

            float my = Mathf.Lerp(floorY, centre.y, 0.3f);
            Beam(t.transform, "Brace", new Vector3(TowerX, my, centre.z - 2.4f * 0.7f), new Vector3(TowerX, my, centre.z + 2.4f * 0.7f), 0.14f, kit.woodDark);
            float my2 = Mathf.Lerp(floorY, centre.y, 0.62f);
            Beam(t.transform, "Brace", new Vector3(TowerX, my2, centre.z - 2.4f * 0.4f), new Vector3(TowerX, my2, centre.z + 2.4f * 0.4f), 0.14f, kit.woodDark);
            MachineGeometry.Box(t.transform, "Bearing", new Vector3(TowerX, centre.y, centre.z), Quaternion.identity, new Vector3(0.3f, 0.45f, 0.45f), kit.castIron, false);
        }

        // ---- apron under the top of the wheel, collector, exit lip --------------------------------------------

        static Vector3 BuildApronAndCollector(Transform root, MachineKit kit, MachineConfig config, float cy, float rootY)
        {
            var g = MachineGeometry.Group(root, "Apron and Collector").transform;
            float apronY = cy + Radius - ApronDrop;                          // surface height at x = 0
            float zFront = CentreZ - ApronFrontDz, zBack = CentreZ + ApronBackDz;
            float len = zBack - zFront, zMid = (zFront + zBack) * 0.5f;
            float cosT = Mathf.Cos(ApronTilt * Mathf.Deg2Rad), tanT = Mathf.Tan(ApronTilt * Mathf.Deg2Rad);

            // Apron: tilted 12 deg about Z so the -X edge is low; surface passes through (x = 0, y = apronY).
            var apron = MachineGeometry.Group(g, "Apron");
            apron.transform.localPosition = new Vector3(0f, apronY, zMid);
            apron.transform.localRotation = Quaternion.Euler(0f, 0f, ApronTilt);
            float xa = ApronMinX / cosT, xb = ApronMaxX / cosT;               // along the slope
            float wPlate = xb - xa, xcPlate = (xa + xb) * 0.5f;
            MachineGeometry.Box(apron.transform, "Apron Plate", new Vector3(xcPlate, -0.05f, 0f), Quaternion.identity, new Vector3(wPlate, 0.1f, len), kit.wood);
            MachineGeometry.Box(apron.transform, "Apron Wall +X", new Vector3(xb + 0.03f, 0.5f, 0f), Quaternion.identity, new Vector3(0.06f, 1.1f, len), kit.glass); // tall: tipped loads bounce off the side
            MachineGeometry.Box(apron.transform, "Apron Wall Back", new Vector3(xcPlate, ApronBackWallH * 0.5f, len * 0.5f - 0.03f), Quaternion.identity, new Vector3(wPlate, ApronBackWallH, 0.06f), kit.glass);
            MachineGeometry.Box(apron.transform, "Apron Wall Front", new Vector3(xcPlate, ApronFrontWallH * 0.5f, -len * 0.5f + 0.03f), Quaternion.identity, new Vector3(wPlate, ApronFrontWallH, 0.06f), kit.glass); // as tall as the swept buckets allow (front/back walls sit inside the bucket ring, so they cannot rise further)

            // Collector: runs along +Z at x = -1.45, 1.1 m wide (chuteWidth + 0.2), sloping 4 deg downhill. Its +X side is open
            // where the apron drains into it and walled where the apron isn't.
            float edgeY = apronY + ApronMinX * tanT;                          // apron surface at its -X edge
            float zExit = CentreZ + Radius + 2.4f;                // 1.5 m longer than before: the longer apron needs a longer collector
            float cLen = zExit - zFront;
            float pitchTan = Mathf.Tan(CollectorPitch * Mathf.Deg2Rad);
            float xC = ApronMinX - CollectorHalf;                             // centre line
            float y0 = edgeY - 0.04f;                                         // surface at the -Z end
            var col = MachineGeometry.Group(g, "Collector");
            col.transform.localPosition = new Vector3(xC, y0, zFront);
            col.transform.localRotation = Quaternion.Euler(CollectorPitch, 0f, 0f);
            float cHyp = cLen / Mathf.Cos(CollectorPitch * Mathf.Deg2Rad);
            float hw = CollectorHalf;
            // The body of the collector is the tall housing the tipped loads need; its last stretch is a standard port, so the chute
            // that takes it on meets exactly its own profile.
            float bodyLen = cHyp - ExitPortLength;
            MachineGeometry.Box(col.transform, "Collector Floor", new Vector3(0f, -0.05f, bodyLen * 0.5f), Quaternion.identity, new Vector3(hw * 2f + 0.16f, 0.1f, bodyLen), kit.wood);
            MachineGeometry.Box(col.transform, "Collector Wall -X", new Vector3(-hw - 0.06f, 0.4f, bodyLen * 0.5f), Quaternion.identity, new Vector3(0.12f, 0.9f, bodyLen), kit.glass);
            float plateEnd = (zBack - zFront) / Mathf.Cos(CollectorPitch * Mathf.Deg2Rad);
            MachineGeometry.Box(col.transform, "Collector Wall +X", new Vector3(hw + 0.06f, 0.4f, (plateEnd + bodyLen) * 0.5f), Quaternion.identity, new Vector3(0.12f, 0.9f, bodyLen - plateEnd), kit.glass);
            MachineConnectorBuilder.BuildPort(col.transform, "Exit Port", new[] { new Vector3(0f, 0f, bodyLen - 0.02f), new Vector3(0f, 0f, cHyp) }, kit, config);
            MachineGeometry.Box(col.transform, "Collector Back", new Vector3(0f, 0.25f, -0.04f), Quaternion.identity, new Vector3(hw * 2f + 0.16f, 0.6f, 0.08f), kit.glass);

            // Supports to the studio floor.
            foreach (float zf in new[] { 0.5f, 3.6f, 6.7f })
            {
                float zz = zFront + zf;
                float topY = y0 - zf * pitchTan - 0.1f;
                float h = topY + rootY;
                if (h < 0.1f) continue;
                for (int s = -1; s <= 1; s += 2)
                    MachineGeometry.Box(g, "Collector Leg", new Vector3(xC + s * (hw + 0.1f), topY - h * 0.5f, zz), Quaternion.identity, new Vector3(0.16f, h, 0.16f), kit.woodDark, false);
                MachineGeometry.Box(g, "Collector Cross", new Vector3(xC, topY, zz), Quaternion.identity, new Vector3(hw * 2f + 0.5f, 0.12f, 0.14f), kit.woodDark, false);
            }

            // Stringers under the apron plate, bolted to the collector (no posts on the wheel side, where the rim and spokes turn).
            for (int k = 0; k < 2; k++)
            {
                float zz = k == 0 ? -len * 0.5f + 0.4f : len * 0.5f - 0.4f;
                MachineGeometry.Box(apron.transform, "Apron Stringer", new Vector3((-2.0f / cosT + xb) * 0.5f, -0.2f, zz), Quaternion.identity,
                    new Vector3(xb + 2.0f / cosT, 0.12f, 0.14f), kit.woodDark, false);
            }

            float yExit = y0 - (zExit - zFront) * pitchTan + 0.04f;
            return new Vector3(xC, yExit, zExit);
        }

        static void Beam(Transform parent, string name, Vector3 a, Vector3 b, float thick, Material mat)
        {
            Vector3 d = b - a;
            MachineGeometry.Box(parent, name, (a + b) * 0.5f, Quaternion.LookRotation(d.normalized, Vector3.right), new Vector3(thick, thick, d.magnitude), mat, false);
        }
    }
}
