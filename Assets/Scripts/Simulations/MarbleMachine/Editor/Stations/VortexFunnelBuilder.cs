using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Vortex Funnel. A straight ramp (inner edge tangent to the rim circle) feeds a spiral ledge that
    /// narrows to nothing over 130 degrees, so every ball is carried onto the funnel rim at orbital speed.
    /// </summary>
    public static class VortexFunnelBuilder
    {
        const float Rim = 1.6f;
        const float Depth = 0.6f; // shallower well (was 1.1): the height budget is tight
        const float LedgeWidth = 0.9f;
        const float SweepDeg = 130f;

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
            float half = w * 0.5f;

            float zT = 1.4f;                       // tangent point / ramp end
            float yR = -0.2f;                     // rim height
            Vector3 centre = new Vector3(half + Rim, yR - Depth, zT); // throat centre (x = ramp inner edge + rim radius)
            float rt = 2.0f * (2f * r);            // throat radius: 4 ball diameters across, ~90 balls/s (Beverloo), well above 15/s

            // Entry catch zone.
            MachineGeometry.Box(t, "Entry Floor", new Vector3(0f, -0.075f, -0.4f), Quaternion.identity,
                new Vector3(w + 0.4f, 0.05f, 0.8f), kit.asphalt);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                MachineGeometry.Box(t, "Entry Wall", new Vector3(sgn * ((w + 0.4f) * 0.5f + 0.025f), 0.1f, -0.4f), Quaternion.identity,
                    new Vector3(0.05f, 0.35f, 0.8f), kit.chrome);

            // Straight ramp: drop 0.2 m gives ~2 m/s against an orbital speed sqrt(g R tan(a)) ~ 1.5 m/s at the rim.
            var line = new List<Vector3> { new Vector3(0f, -0.05f, -0.05f), new Vector3(0f, yR, zT) };
            MachineGeometry.MeshObject(t, "Ramp", MachineGeometry.Channel(line, w, MachineConnectorBuilder.WallHeightFor(r)), "VortexRamp", kit.asphalt);

            // Spiral ledge.
            MachineGeometry.MeshObject(t, "Spiral Ledge", LedgeMesh(centre, yR), "VortexLedge", kit.asphalt);

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
            funnel.transform.localPosition = centre;

            // Lip ring where the ledge is not (chrome), from the ledge end round to just before the ramp.
            float step = 8f;
            for (float a = SweepDeg; a < 356f; a += step)
            {
                float am = a + step * 0.5f;
                Vector3 p = Pt(centre, am, Rim + 0.03f, yR + 0.15f);
                float chord = 2f * (Rim + 0.03f) * Mathf.Tan(step * 0.5f * Mathf.Deg2Rad);
                MachineGeometry.Box(t, "Lip", p, Quaternion.Euler(0f, am, 0f), new Vector3(0.05f, 0.36f, chord), kit.chrome);
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
            float yf0 = centre.y - 0.45f;
            Vector3 f0 = new Vector3(centre.x, yf0, centre.z - 0.5f);
            Vector3 f1 = new Vector3(centre.x, yf0 - 0.3f, centre.z + 2.2f);
            Quaternion q = Quaternion.LookRotation(f1 - f0, Vector3.up);
            float len = Vector3.Distance(f0, f1);
            MachineGeometry.Box(t, "Cup Floor", (f0 + f1) * 0.5f - q * Vector3.up * 0.025f, q, new Vector3(w, 0.05f, len), kit.asphalt);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                MachineGeometry.Box(t, "Cup Wall", (f0 + f1) * 0.5f + q * new Vector3(sgn * (half + 0.025f), 0.17f, 0f), q,
                    new Vector3(0.05f, 0.45f, len), kit.brick);
            MachineGeometry.Box(t, "Cup Back", f0 + q * new Vector3(0f, 0.15f, -0.025f), q, new Vector3(w + 0.1f, 0.45f, 0.05f), kit.brick);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Leg(t, kit.chrome, new Vector3(centre.x + sgn * (half + 0.1f), 0f, centre.z + 2.0f), f1.y + 0.1f, floorY, 0.05f);
                Leg(t, kit.chrome, new Vector3(centre.x + sgn * (half + 0.1f), 0f, centre.z - 0.4f), f0.y, floorY, 0.05f);
            }

            var station = root.AddComponent<VortexFunnelStation>();
            station.displayName = "Vortex Funnel";
            station.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            station.exit = MachineGeometry.Anchor(t, "Exit", f1 + new Vector3(0f, 0.05f, 0f));
            station.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", new Vector3(centre.x, -0.6f, centre.z));
            station.cameraOffset = placement.Rotation * new Vector3(0f, 4.2f, -3.6f);
            station.tourSeconds = 9f;
            station.activityBounds = WorldBounds(t, new Vector3(2.0f, -1.0f, 1.4f), new Vector3(5.6f, 2.2f, 4.8f));
            return station;
        }

        // Clockwise-from-above orbit: phi = 0 at the tangent point, heading +Z, centre on the +X side.
        static Vector3 Pt(Vector3 centre, float phiDeg, float rho, float y)
        {
            float p = phiDeg * Mathf.Deg2Rad;
            return new Vector3(centre.x - rho * Mathf.Cos(p), y, centre.z + rho * Mathf.Sin(p));
        }

        static Mesh LedgeMesh(Vector3 centre, float yR)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int segs = 26;
            for (int i = 0; i <= segs; i++)
            {
                float phi = SweepDeg * i / segs;
                float width = LedgeWidth * (1f - phi / SweepDeg);
                float ro = Rim + width;
                float ramp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phi / 20f));
                float rise = 0.18f * (width / LedgeWidth) * ramp;
                verts.Add(Pt(centre, phi, Rim, yR));
                verts.Add(Pt(centre, phi, ro, yR + rise));
                verts.Add(Pt(centre, phi, ro, yR + rise + 0.35f));
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
