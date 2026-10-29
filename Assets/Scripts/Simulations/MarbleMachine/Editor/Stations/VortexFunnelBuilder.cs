using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Vortex Funnel. A straight ramp (inner edge tangent to the rim circle) feeds a spiral ledge that
    /// narrows to nothing over 130 degrees, so every ball (ledge as wide as the ramp, so none can miss it) is carried onto the funnel rim at orbital speed.
    /// </summary>
    public static class VortexFunnelBuilder
    {
        const float Rim = 3.2f;   // 2x: radius of the funnel and of the orbit
        const float Depth = 1.0f; // deeper well than the 0.6 it had at half the radius: the rim's slope (and so the orbit speed) rises with it
        const float SweepDeg = 130f;
        const float PortBack = 0.9f;

        public static VortexFunnelStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            var root = new GameObject("Vortex Funnel");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            var t = root.transform;
            float r = config.ballRadius;
            float floorY = -placement.entry.y;
            float w = config.chuteWidth;
            float rampW = w; // the ramp, the ledge and the cup are all exactly the track width
            float half = rampW * 0.5f;

            float zT = 2.4f;                       // tangent point / ramp end
            float yR = -0.75f;                     // rim height: a 0.7 m drop down the ramp gives ~3.7 m/s against ~1.4 m/s to hold the orbit
            Vector3 centre = new Vector3(half + Rim, yR - Depth, zT); // throat centre (x = ramp inner edge + rim radius)
            // Throat radius: 8 ball radii (= 4 diameters across at r 0.065, 0.52 m). Capped at 0.7 m so a racer's centre falling through the
            // hole still lands inside the 1.8 m cup below (inner half-width 0.9 minus r), which keeps r 0.2 at a 3.5-diameter hole.
            float rt = Mathf.Clamp(8f * r, 0.52f, 0.7f);
            float lipH = Mathf.Max(0.36f, 3.5f * r);   // lip ring and ledge wall: 0.36 at r 0.065, 0.7 at r 0.2 (1.75 diameters)

            // Entry port and straight ramp as one channel in the connector's own profile: the drop down the ramp gives ~3.7 m/s against
            // an orbital speed sqrt(g R tan(a)) ~ 1.4 m/s at the rim. The floor is slick, to keep the speed the drop gave the balls.
            var line = new List<Vector3> { new Vector3(0f, -0.05f, -PortBack), new Vector3(0f, -0.05f, -0.05f), new Vector3(0f, yR, zT) };
            MachineConnectorBuilder.BuildPort(t, "Entry Ramp", line, kit, config, 0f, null, MachineGeometry.SlickPhysics);

            // Spiral ledge.
            // Smooth, polished and slick: the grit texture and the 0.3 wall friction slowed balls on this level strip.
            var ledge = MachineGeometry.MeshObject(t, "Spiral Ledge", LedgeMesh(centre, yR, rampW, Mathf.Max(0.35f, 3.5f * r)), "VortexLedge", kit.terrazzo);
            ledge.GetComponent<MeshCollider>().sharedMaterial = MachineGeometry.SlickPhysics;

            // Funnel surface.
            var profile = new List<Vector2> { new Vector2(rt, -0.3f) };
            float k = 1f / (1f / rt - 1f / Rim);
            int n = 28;
            for (int i = 0; i <= n; i++)
            {
                float f = i / (float)n;
                float rr = rt + (Rim - rt) * Mathf.Pow(f, 1.3f);
                profile.Add(new Vector2(rr, Depth * k * (1f / rt - 1f / rr)));
            }

            var funnel = MachineGeometry.MeshObject(t, "Funnel Surface", MachineGeometry.Lathe(profile, 48, true), "VortexFunnel", kit.terrazzo);
            funnel.GetComponent<MeshCollider>().sharedMaterial = MachineGeometry.SlickPhysics; // friction was bleeding the orbit speed
            funnel.transform.localPosition = centre;

            // Lip ring where the ledge is not (chrome), from the ledge end round to just before the ramp.
            float step = 8f;
            for (float a = SweepDeg; a < 356f; a += step)
            {
                float am = a + step * 0.5f;
                Vector3 p = Pt(centre, am, Rim + 0.03f, yR + 0.15f);
                float chord = 2f * (Rim + 0.03f) * Mathf.Tan(step * 0.5f * Mathf.Deg2Rad);
                // Bottom stays where the 0.36 m lip's was (yR - 0.03), the top rises with lipH.
                MachineGeometry.Box(t, "Lip", p + Vector3.up * (lipH - 0.36f) * 0.5f, Quaternion.Euler(0f, am, 0f), new Vector3(0.05f, lipH, chord), kit.chrome);
            }

            // Wooden ring frame and legs.
            for (float a = 0f; a < 360f; a += 15f)
            {
                float chord = 2f * (Rim + 0.2f) * Mathf.Tan(7.5f * Mathf.Deg2Rad);
                MachineGeometry.Box(t, "Ring Frame", Pt(centre, a + 7.5f, Rim + 0.2f, yR - 0.15f), Quaternion.Euler(0f, a + 7.5f, 0f),
                    new Vector3(0.2f, 0.12f, chord + 0.01f), kit.woodDark, false);
            }

            float[] legAngles = { 60f, 150f, 240f, 300f };
            foreach (float a in legAngles)
                Leg(t, kit.chrome, Pt(centre, a, Rim + 0.2f, 0f), yR - 0.2f, floorY, 0.06f);

            // Throat support collar (decor).
            MachineGeometry.Cylinder(t, "Throat Collar", centre + new Vector3(0f, 0.02f, 0f), Quaternion.identity, rt + 0.06f, 0.1f, kit.brass, false);

            // Collector cup and chute heading +Z.
            // The throat sleeve reaches 0.3 m below the throat plane. The chute floor must sit far enough under it for a ball to drop through
            // and roll away: 0.15 m clear is fine for mach-001's 0.13 m balls, but a 0.4 m racer wedged in it. Racers get 3.5 radii of clearance.
            float cupDrop = r > 0.1f ? 0.3f + 3.5f * r : 0.45f;
            float yf0 = centre.y - cupDrop;
            Vector3 f0 = new Vector3(centre.x, yf0, centre.z - 1.0f);
            Vector3 f1 = new Vector3(centre.x, yf0 - 0.55f, centre.z + 3.0f);
            Quaternion q = Quaternion.LookRotation(f1 - f0, Vector3.up);
            MachineConnectorBuilder.BuildPort(t, "Cup Port", new[] { f0, f1 }, kit, config);
            float wallOuter = w + 2f * MachineConnectorBuilder.WallThickness;
            MachineGeometry.Box(t, "Cup Back", f0 + q * new Vector3(0f, 0.3f, -0.06f), q,
                new Vector3(wallOuter, MachineConnectorBuilder.WallHeightFor(r), 0.12f), kit.glass);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Leg(t, kit.chrome, new Vector3(centre.x + sgn * (w * 0.5f + 0.25f), 0f, f1.z - 0.2f), f1.y - 0.1f, floorY, 0.05f);
                Leg(t, kit.chrome, new Vector3(centre.x + sgn * (w * 0.5f + 0.25f), 0f, f0.z + 0.2f), f0.y - 0.1f, floorY, 0.05f);
            }

            var station = root.AddComponent<VortexFunnelStation>();
            station.displayName = "Vortex Funnel";
            station.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            station.exit = MachineGeometry.Anchor(t, "Exit", f1 + new Vector3(0f, 0.05f, 0f));
            station.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", new Vector3(centre.x, yR - 0.6f, centre.z));
            station.cameraOffset = placement.Rotation * new Vector3(0f, 7.5f, -7f);
            station.tourSeconds = 9f;
            {
                float x0 = -half - 0.4f, x1 = centre.x + Rim + 0.6f;
                float z0 = -PortBack - 0.2f, z1 = f1.z + 0.4f;
                float y1 = 0.6f, y0 = f1.y - 0.8f;
                station.activityBounds = WorldBounds(t, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                    new Vector3(x1 - x0, y1 - y0, z1 - z0));
            }
            return station;
        }

        // Clockwise-from-above orbit: phi = 0 at the tangent point, heading +Z, centre on the +X side.
        static Vector3 Pt(Vector3 centre, float phiDeg, float rho, float y)
        {
            float p = phiDeg * Mathf.Deg2Rad;
            return new Vector3(centre.x - rho * Mathf.Cos(p), y, centre.z + rho * Mathf.Sin(p));
        }

        static Mesh LedgeMesh(Vector3 centre, float yR, float ledgeWidth, float wallHeight)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int segs = 26;
            for (int i = 0; i <= segs; i++)
            {
                float phi = SweepDeg * i / segs;
                float width = ledgeWidth * (1f - phi / SweepDeg);
                float ro = Rim + width;
                float ramp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phi / 20f));
                float rise = 0.18f * (width / ledgeWidth) * ramp;
                verts.Add(Pt(centre, phi, Rim, yR));
                verts.Add(Pt(centre, phi, ro, yR + rise));
                verts.Add(Pt(centre, phi, ro, yR + rise + wallHeight));
            }

            for (int i = 0; i < segs; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    int a = i * 3 + j, b = a + 1, c = a + 3, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            int cnt = tris.Count;
            for (int i = 0; i < cnt; i += 3)
            {
                tris.Add(tris[i]); tris.Add(tris[i + 2]); tris.Add(tris[i + 1]);
            }

            var uvs = new List<Vector2>();
            for (int i = 0; i < verts.Count; i++) uvs.Add(new Vector2(verts[i].x, verts[i].z));
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            MachineGeometry.FixDegenerateNormals(mesh); // the ledge narrows to a point: its last vertices have no area
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Leg(Transform t, Material m, Vector3 p, float topY, float floorY, float radius)
        {
            float h = Mathf.Max(0.1f, topY - floorY);
            MachineGeometry.Cylinder(t, "Leg", new Vector3(p.x, floorY + h * 0.5f, p.z), Quaternion.identity, radius, h, m);
        }

        static Bounds WorldBounds(Transform t, Vector3 c, Vector3 size)
        {
            var b = new Bounds(t.TransformPoint(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -size.x : size.x, (i & 2) == 0 ? -size.y : size.y, (i & 4) == 0 ? -size.z : size.z) * 0.5f;
                b.Encapsulate(t.TransformPoint(corner));
            }

            return b;
        }
    }
}
