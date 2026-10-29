using System.Collections.Generic;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Peg Wall: a 75 degree board, glass front, hex chrome pegs and four pinwheels. The board is twice as wide as it was
    /// (5.6 m) and the flow reaches it through a diffuser ramp that opens gently from the track width; the funnel at the bottom
    /// narrows it back to exactly the track width, where an exit port with the connector's own profile takes over.
    /// </summary>
    public static class PegWallBuilder
    {
        const float TiltDeg = 75f;
        const float BoardLen = 3.2f;
        const float BoardHalf = 2.8f;
        const float FunnelStartU = 1.5f;
        const float BoardTopU = 0.3f;
        /// <summary>Plan-view length and angle of the diffuser ramp that opens the stream from the track width to the board width.</summary>
        const float FeedLen = 2.0f;
        const float FeedDegrees = 30f;
        const float PortBack = 0.9f;

        public static PegWallStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            var root = new GameObject("Peg Wall");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            var t = root.transform;
            float r = config.ballRadius;
            float floorY = -placement.entry.y;

            float c = Mathf.Cos(TiltDeg * Mathf.Deg2Rad), s = Mathf.Sin(TiltDeg * Mathf.Deg2Rad);
            Vector3 d = new Vector3(0f, -s, c);   // down the board
            Vector3 n = new Vector3(0f, c, s);    // face normal (up and toward +Z)
            // The board starts where the diffuser ramp ends: BoardTopU along the board from p0.
            Vector3 ra = new Vector3(0f, -0.05f, 0f);
            Vector3 rb = ra + new Vector3(0f, -FeedLen * Mathf.Tan(FeedDegrees * Mathf.Deg2Rad), FeedLen);
            Vector3 p0 = rb - d * BoardTopU;
            Quaternion bq = Quaternion.LookRotation(d, n); // local X lateral, Y normal, Z down-board
            float gap = 3.5f * r; // a ball-and-a-half slot wedged; this is wide enough to pass but too thin to stack two side by side
            float w = config.chuteWidth;
            float half = w * 0.5f;
            float funnelExitX = half - 0.05f;

            Vector3 PB(float x, float u, float h) => p0 + Vector3.right * x + d * u + n * h;

            // Entry port: the track's own profile, straight in to the diffuser.
            MachineConnectorBuilder.BuildPort(t, "Entry Port", new[] { ra + new Vector3(0f, 0f, -PortBack), ra }, kit, config);

            // Diffuser ramp: a plan-view trapezoid from the port's outer width to the full board, with glass rails that continue the port walls.
            float wallOuter = half + MachineConnectorBuilder.WallThickness;
            float boardOuter = BoardHalf - 0.05f;
            Prism(t, "Feed Ramp", "PegWall_FeedRamp", ra, rb, wallOuter, boardOuter, 0.08f, kit.asphalt);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 railA = ra + Vector3.right * (sgn * (half + MachineConnectorBuilder.WallThickness * 0.5f)) + Vector3.up * 0.9f;
                Vector3 railB = rb + Vector3.right * (sgn * (BoardHalf - 0.075f)) + Vector3.up * 0.9f;
                Slab(t, "Feed Rail", railA, railB, MachineConnectorBuilder.WallThickness, 1.0f, kit.glass);
            }

            // Board, side walls, glass.
            // The board starts where the feed ramp meets its face (u 0.3): above that the board stood up in front of the ramp and stopped balls.
            MachineGeometry.Box(t, "Back Board", PB(0f, (BoardTopU + BoardLen) * 0.5f, -0.03f), bq, new Vector3(BoardHalf * 2f, 0.06f, BoardLen - BoardTopU), kit.wood);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                MachineGeometry.Box(t, "Side Wall", PB(sgn * (BoardHalf - 0.075f), BoardLen * 0.5f, gap * 0.5f + 0.02f), bq,
                    new Vector3(0.1f, gap + 0.08f, BoardLen), kit.glass);
            // The front glass reaches 0.5 m above the board's top edge, so a ball thrown by the landing can't hop over it.
            float glassU0 = -0.5f, glassU1 = BoardLen - 0.1f;
            MachineGeometry.Box(t, "Glass Front", PB(0f, (glassU0 + glassU1) * 0.5f, gap + 0.01f + MachineGeometry.GlassMinThickness * 0.5f), bq,
                new Vector3(BoardHalf * 2f - 0.05f, MachineGeometry.GlassMinThickness, glassU1 - glassU0), kit.glass); // inner face stays at the slot width
            MachineGeometry.Box(t, "Glass Rail Top", PB(0f, 0.5f, gap + 0.04f), bq, new Vector3(BoardHalf * 2f, 0.06f, 0.06f), kit.chrome, false);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                for (int k = 0; k < 4; k++)
                    MachineGeometry.Cylinder(t, "Bolt", PB(sgn * (BoardHalf - 0.075f), 0.5f + k * 0.7f, gap + 0.04f), Quaternion.FromToRotation(Vector3.up, n),
                        0.03f, 0.04f, kit.brass, false);

            // Distributor: the diamond plus four pegs spread the stream across the wide board.
            MachineGeometry.Box(t, "Diamond", PB(0f, 0.7f, gap * 0.5f), bq * Quaternion.Euler(0f, 45f, 0f),
                new Vector3(0.4f, gap, 0.4f), kit.chrome);
            foreach (float px in new[] { 1.0f, 1.9f })
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    MachineGeometry.Cylinder(t, "Distributor Peg", PB(sgn * px, 0.7f, gap * 0.5f), Quaternion.FromToRotation(Vector3.up, n),
                        0.4f * r, gap, kit.chrome);

            // Pinwheels.
            float[] sx = { -1.9f, -0.65f, 0.65f, 1.9f };
            float[] su = { 1.3f, 1.0f, 1.0f, 1.3f };
            // Pinwheel reach shrinks for big balls so the gap between neighbouring tips (1.25 - 2 spinR) stays well over a ball diameter.
            float spinR = Mathf.Min(0.5f, 0.625f - 1.6f * r);
            var spinners = new KinematicSpinner[sx.Length];
            for (int i = 0; i < sx.Length; i++)
            {
                var g = new GameObject("Pinwheel " + (i + 1));
                g.transform.SetParent(t, false);
                g.transform.localPosition = PB(sx[i], su[i], 0.1f);
                g.transform.localRotation = bq;
                MachineGeometry.Box(g.transform, "Arm A", Vector3.zero, Quaternion.identity, new Vector3(spinR * 2f, 0.14f, 0.05f), kit.castIron);
                MachineGeometry.Box(g.transform, "Arm B", Vector3.zero, Quaternion.identity, new Vector3(0.05f, 0.14f, spinR * 2f), kit.castIron);
                MachineGeometry.Cylinder(g.transform, "Hub", Vector3.zero, Quaternion.identity, 0.1f, 0.2f, kit.brass, false);
                MachineGeometry.MakeKinematic(g);
                spinners[i] = g.AddComponent<KinematicSpinner>();
            }

            // Hex peg grid.
            // 4.6 r pitch leaves ~1.9 ball diameters between pegs: at 3.2 r the gap was 0.2 r and balls wedged.
            float rowH = 4.6f * r * 0.866f, colW = 4.6f * r;
            int row = 0;
            float pegLimit = BoardHalf - 0.3f;
            float funnelEdge = BoardHalf - 0.125f;
            for (float u = 1.0f; u < FunnelStartU + 1.0f; u += rowH, row++)
            {
                float x0 = (row % 2 == 0) ? 0f : colW * 0.5f;
                // Past the funnel's start the usable width narrows along the funnel fins.
                float usable = u <= FunnelStartU
                    ? pegLimit
                    : Mathf.Min(pegLimit, Mathf.Lerp(funnelEdge, funnelExitX, (u - FunnelStartU) / (BoardLen - FunnelStartU)) - 0.2f);
                for (float x = x0 - 12f * colW; x <= usable; x += colW)
                {
                    if (x < -usable) continue;
                    // The outermost peg on each side, sitting right above the funnel fins, is left out: it caught balls against the fin.
                    if (u > FunnelStartU - 0.1f && Mathf.Abs(x) > usable - 0.1f) continue;
                    bool blocked = false;
                    for (int i = 0; i < sx.Length; i++)
                        if (Mathf.Abs(x - sx[i]) < spinR + 0.2f && Mathf.Abs(u - su[i]) < spinR + 0.2f) blocked = true;
                    if (blocked) continue;
                    MachineGeometry.Cylinder(t, "Peg", PB(x, u, gap * 0.5f), Quaternion.FromToRotation(Vector3.up, n),
                        0.4f * r, gap, kit.chrome);
                }
            }

            // Bottom funnel to the centre: narrows the board to exactly the track width.
            for (int sgn = -1; sgn <= 1; sgn += 2)
                Fin(t, bq, PB(0f, 0f, 0f), d, n, gap, kit.woodDark, sgn * funnelEdge, FunnelStartU, sgn * funnelExitX, BoardLen);

            // Exit port: off the board's foot and on down, in the connector's profile.
            Vector3 e0 = PB(0f, BoardLen, 0f);
            Vector3 e1 = e0 + new Vector3(0f, -0.2f, 0.3f);
            Vector3 e2 = e1 + new Vector3(0f, -0.1f, 0.9f);
            MachineConnectorBuilder.BuildPort(t, "Exit Port", new[] { e0, e1, e2 }, kit, config);

            // Frame and legs.
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                MachineGeometry.Box(t, "Side Beam", PB(sgn * (BoardHalf + 0.03f), BoardLen * 0.5f, -0.1f), bq, new Vector3(0.12f, 0.14f, BoardLen), kit.wood);
                Leg(t, kit.chrome, sgn * (half + 0.25f), -0.6f, ra.y - 0.1f, floorY);
                Leg(t, kit.chrome, sgn * (boardOuter - 0.1f), rb.z - 0.2f, rb.y - 0.12f, floorY);
                Leg(t, kit.chrome, sgn * (BoardHalf + 0.03f), PB(0f, BoardLen * 0.6f, 0f).z, PB(0f, BoardLen * 0.6f, 0f).y - 0.3f, floorY);
                Leg(t, kit.chrome, sgn * (half + 0.25f), e2.z - 0.15f, e2.y - 0.05f, floorY);
            }

            MachineGeometry.Box(t, "Top Crossbar", rb + new Vector3(0f, -0.2f, -0.1f), Quaternion.identity, new Vector3(BoardHalf * 2f + 0.3f, 0.1f, 0.12f), kit.wood);
            MachineGeometry.Box(t, "Back Crossbar", PB(0f, BoardLen - 0.2f, -0.15f), Quaternion.identity, new Vector3(BoardHalf * 2f + 0.1f, 0.1f, 0.1f), kit.wood);

            var station = root.AddComponent<PegWallStation>();
            station.displayName = "Peg Wall";
            station.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            station.exit = MachineGeometry.Anchor(t, "Exit", e2 + new Vector3(0f, 0.05f, 0f));
            Vector3 mid = PB(0f, BoardLen * 0.5f, 0f);
            station.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", mid);
            station.cameraOffset = placement.Rotation * new Vector3(1.0f, 1.0f, 9.0f);
            station.tourSeconds = 8f;
            station.spinners = spinners;
            float top = 0.6f, bottom = e2.y - 0.8f;
            station.activityBounds = WorldBounds(t, new Vector3(0f, (top + bottom) * 0.5f, (e2.z - PortBack) * 0.5f + 0.2f),
                new Vector3(BoardHalf * 2f + 1f, top - bottom, e2.z + PortBack + 1f));
            return station;
        }

        // origin is PB(0,0,0); x/u are added in board space.
        static void Fin(Transform t, Quaternion bq, Vector3 origin, Vector3 d, Vector3 n, float gap, Material m,
            float x1, float u1, float x2, float u2)
        {
            float dx = x2 - x1, du = u2 - u1;
            float len = Mathf.Sqrt(dx * dx + du * du);
            float yaw = Mathf.Atan2(dx, du) * Mathf.Rad2Deg;
            Vector3 centre = origin + Vector3.right * ((x1 + x2) * 0.5f) + d * ((u1 + u2) * 0.5f) + n * (gap * 0.5f);
            MachineGeometry.Box(t, "Fin", centre, bq * Quaternion.Euler(0f, yaw, 0f), new Vector3(0.05f, gap, len), m);
        }

        static void Slab(Transform t, string name, Vector3 a, Vector3 b, float width, float thick, Material m)
        {
            Quaternion q = Quaternion.LookRotation(b - a, Vector3.up);
            Vector3 centre = (a + b) * 0.5f - q * Vector3.up * (thick * 0.5f);
            MachineGeometry.Box(t, name, centre, q, new Vector3(width, thick, Vector3.Distance(a, b)), m);
        }

        /// <summary>
        /// A convex slab whose top is the trapezoid from half-width <paramref name="halfA"/> at <paramref name="a"/> to
        /// <paramref name="halfB"/> at <paramref name="b"/> (centre line a to b), <paramref name="thick"/> thick below it.
        /// </summary>
        static void Prism(Transform t, string name, string asset, Vector3 a, Vector3 b, float halfA, float halfB, float thick, Material m)
        {
            var top = new[]
            {
                a + Vector3.left * halfA, a + Vector3.right * halfA, b + Vector3.right * halfB, b + Vector3.left * halfB
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector3 centre = Vector3.zero;
            foreach (Vector3 p in top) centre += p + (p + Vector3.down * thick);
            centre /= 8f;
            void Face(Vector3 p, Vector3 q, Vector3 rr)
            {
                if (Vector3.Dot(Vector3.Cross(q - p, rr - p), (p + q + rr) / 3f - centre) < 0f)
                {
                    Vector3 tmp = q;
                    q = rr;
                    rr = tmp;
                }

                int i = verts.Count;
                verts.Add(p); verts.Add(q); verts.Add(rr);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            Vector3 dn = Vector3.down * thick;
            Face(top[0], top[1], top[2]); Face(top[0], top[2], top[3]);
            Face(top[0] + dn, top[1] + dn, top[2] + dn); Face(top[0] + dn, top[2] + dn, top[3] + dn);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Face(top[i], top[j], top[j] + dn);
                Face(top[i], top[j] + dn, top[i] + dn);
            }

            var uvs = new List<Vector2>(verts.Count);
            foreach (Vector3 v in verts) uvs.Add(new Vector2(v.x, v.z));
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            MachineGeometry.FixDegenerateNormals(mesh);
            mesh.RecalculateBounds();
            // Sloped, so a ball on it keeps rolling; the floor is asphalt like the chutes either side.
            MachineGeometry.MeshObject(t, name, mesh, asset, m, true, true);
        }

        static void Leg(Transform t, Material m, float x, float z, float topY, float floorY)
        {
            float h = Mathf.Max(0.1f, topY - floorY);
            MachineGeometry.Cylinder(t, "Leg", new Vector3(x, floorY + h * 0.5f, z), Quaternion.identity, 0.05f, h, m);
        }

        static Bounds WorldBounds(Transform t, Vector3 localCentre, Vector3 size)
        {
            float m = Mathf.Max(size.x, size.z);
            return new Bounds(t.TransformPoint(localCentre), new Vector3(m, size.y, m));
        }
    }
}
