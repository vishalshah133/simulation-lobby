using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>Builds the Trip (midpoint) act dressing along the rail between ActStart(Trip) and ActEnd(Trip).</summary>
    public static class TripSetBuilder
    {
        static readonly Color Orange = new Color(1f, 0.48f, 0.09f);
        static readonly Color Purple = new Color(0.6f, 0.24f, 1f);
        static readonly Color Green = new Color(0.49f, 1f, 0.31f);

        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            ActSetBuildKit.Span(cfg, rail, ActId.Trip, out float start, out float end);
            float L = end - start;

            GameObject root = ActSetBuildKit.Empty("Trip Set", parent);
            var set = root.AddComponent<TripSet>();
            set.startDist = start;
            set.endDist = end;
            GameObject content = ActSetBuildKit.Empty("Content", root.transform);
            set.content = content;

            // ---- meshes (fresh assets, never overwritten in place)
            Mesh torus = ActSetBuildKit.SaveMeshFresh(MeshAcc.Torus(1f, 0.06f, 0.3f, 12, 96, 10), "CoastTrip_Torus");
            Mesh face = ActSetBuildKit.SaveMeshFresh(MeshAcc.PumpkinFace(), "CoastTrip_Face");
            Mesh bat = ActSetBuildKit.SaveMeshFresh(MeshAcc.Bat(), "CoastTrip_Bat");
            Mesh ghost = ActSetBuildKit.SaveMeshFresh(MeshAcc.Ghost(), "CoastTrip_Ghost");
            Mesh corn = ActSetBuildKit.SaveMeshFresh(MeshAcc.CandyCorn(), "CoastTrip_CandyCorn");
            Mesh sweet = ActSetBuildKit.SaveMeshFresh(MeshAcc.WrappedSweet(), "CoastTrip_Sweet");

            // ---- materials (cloned and hue-cycled at runtime; these are the starting looks)
            Material[] ringM =
            {
                ActSetBuildKit.M("Trip_Ring0", ActSetBuildKit.Hdr(Orange, 0.3f), ActSetBuildKit.Hdr(Orange, 1.1f), 0.4f),
                ActSetBuildKit.M("Trip_Ring1", ActSetBuildKit.Hdr(Purple, 0.3f), ActSetBuildKit.Hdr(Purple, 1.1f), 0.4f),
                ActSetBuildKit.M("Trip_Ring2", ActSetBuildKit.Hdr(Green, 0.3f), ActSetBuildKit.Hdr(Green, 1.1f), 0.4f)
            };
            Material faceDisc = ActSetBuildKit.M("Trip_FaceDisc", Orange * 0.2f, ActSetBuildKit.Hdr(Orange, 0.45f), 0.2f);
            Material faceGlow = ActSetBuildKit.M("Trip_FaceGlow", ActSetBuildKit.Hdr(Green, 0.3f), ActSetBuildKit.Hdr(Green, 1.6f), 0.2f);
            Material[] cornM =
            {
                ActSetBuildKit.M("Trip_CornTip", ActSetBuildKit.Hdr(Color.white, 0.4f), ActSetBuildKit.Hdr(Color.white, 0.9f), 0.5f),
                ActSetBuildKit.M("Trip_CornMid", ActSetBuildKit.Hdr(Orange, 0.3f), ActSetBuildKit.Hdr(Orange, 1.1f), 0.5f),
                ActSetBuildKit.M("Trip_CornBase", ActSetBuildKit.Hdr(Purple, 0.3f), ActSetBuildKit.Hdr(Purple, 1.1f), 0.5f)
            };
            Material sweetM = ActSetBuildKit.M("Trip_Sweet", ActSetBuildKit.Hdr(Green, 0.3f), ActSetBuildKit.Hdr(Green, 1.1f), 0.6f);
            Material batM = ActSetBuildKit.M("Trip_Bat", ActSetBuildKit.Hdr(Purple, 0.3f), ActSetBuildKit.Hdr(Purple, 1.2f), 0.2f);
            Material ghostM = ActSetBuildKit.M("Trip_Ghost", ActSetBuildKit.Hdr(Green, 0.3f), ActSetBuildKit.Hdr(Green, 0.8f), 0.4f, 0f, true);
            Material ghostEye = ActSetBuildKit.M("Trip_GhostEye", new Color(0.02f, 0.02f, 0.05f), default, 0.1f);
            Material starM = ActSetBuildKit.M("Trip_Star", ActSetBuildKit.Hdr(Color.white, 0.4f), ActSetBuildKit.Hdr(Color.white, 1.2f), 0.2f);
            Material skyDisc = ActSetBuildKit.M("Trip_SkyDisc", new Color(0.07f, 0.03f, 0.16f), ActSetBuildKit.Hdr(new Color(0.25f, 0.1f, 0.5f), 0.25f), 0.1f);
            Material skyRingM = ActSetBuildKit.M("Trip_SkyRing", ActSetBuildKit.Hdr(Purple, 0.3f), ActSetBuildKit.Hdr(Purple, 1.0f), 0.1f);
            Material moonM = ActSetBuildKit.M("Trip_Moon", new Color(0.93f, 0.9f, 0.82f), ActSetBuildKit.Hdr(new Color(0.93f, 0.9f, 0.82f), 0.55f), 0.1f);

            var ringR = new List<Renderer>[] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
            // neon fluorescent tubes: near-white hot core tinted by the hue cycle (whiten 0.45 keeps the tube reading as a lit tube)
            Material[] tubeM =
            {
                ActSetBuildKit.M("Trip_Tube0", ActSetBuildKit.Hdr(Green, 0.5f), ActSetBuildKit.Hdr(Green, 1.8f), 0.8f),
                ActSetBuildKit.M("Trip_Tube1", ActSetBuildKit.Hdr(Purple, 0.5f), ActSetBuildKit.Hdr(Purple, 1.8f), 0.8f),
                ActSetBuildKit.M("Trip_Tube2", ActSetBuildKit.Hdr(Orange, 0.5f), ActSetBuildKit.Hdr(Orange, 1.8f), 0.8f)
            };
            var tubeR = new List<Renderer>[] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
            var faceR = new List<Renderer>();
            var cornR = new List<Renderer>();
            var sweetR = new List<Renderer>();
            var batR = new List<Renderer>();
            var ghostR = new List<Renderer>();
            var starR = new List<Renderer>();
            var skyR = new List<Renderer>();
            var stations = new List<TripStation>();

            // ---- giant concentric glowing rings the cart flies through
            // one ring per ~20 m: at the 14+ m/s drive speed that is a ring about every second (under 1.5 Hz at 30 m/s: flash safe)
            int nRing = Mathf.Clamp(Mathf.RoundToInt(L / 20f), 14, 80);
            float[] radii = { 5.5f, 8.5f, 12f };
            for (int i = 0; i < nRing; i++)
            {
                float d = start + L * (i + 0.5f) / nRing;
                ActSetBuildKit.Pose(rail, d, out Vector3 p, out Vector3 f, out Vector3 u);
                GameObject st = ActSetBuildKit.EmptyWorld("Ring " + i, content.transform, p, Quaternion.LookRotation(f, u));
                var kids = new Transform[3];
                for (int k = 0; k < 3; k++)
                {
                    GameObject t = ActSetBuildKit.MeshObj("Torus " + k, st.transform, torus, new[] { ringM[k] },
                        Vector3.zero, Quaternion.identity, Vector3.one * radii[k]);
                    kids[k] = t.transform;
                    ringR[k].Add(t.GetComponent<Renderer>());
                }
                stations.Add(Station(st, TripStationKind.Ring, d, i, 1f, kids, 0));
            }

            // ---- fluorescent tube cages: six long neon tubes ringed around the rail, one cage about every 9 m, offset from the rings
            int nTube = Mathf.Clamp(Mathf.RoundToInt(L / 9f), 16, 120);
            for (int i = 0; i < nTube; i++)
            {
                float d = start + L * (i + 0.25f) / nTube;
                ActSetBuildKit.Pose(rail, d, out Vector3 p, out Vector3 f, out Vector3 u);
                GameObject st = ActSetBuildKit.EmptyWorld("Tubes " + i, content.transform, p, Quaternion.LookRotation(f, u));
                var kids = new Transform[6];
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3f + (i % 2) * 0.26f;
                    GameObject g = ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Tube " + k, st.transform,
                        new Vector3(Mathf.Cos(a) * 7f, Mathf.Sin(a) * 7f, 0f), new Vector3(0.22f, 4.2f, 0.22f), tubeM[k % 3],
                        Quaternion.Euler(90f, 0f, 0f));
                    kids[k] = g.transform;
                    tubeR[k % 3].Add(g.GetComponent<Renderer>());
                }
                stations.Add(Station(st, TripStationKind.Tube, d, i, 1f, kids, 0));
            }

            // ---- kaleidoscope tunnel of jack-o'-lantern faces (middle of the act)
            int nFace = Mathf.Clamp(Mathf.RoundToInt(0.3f * L / 12f), 8, 32);
            for (int i = 0; i < nFace; i++)
            {
                float d = start + L * (0.35f + 0.3f * (i + 0.5f) / nFace);
                ActSetBuildKit.Pose(rail, d, out Vector3 p, out Vector3 f, out Vector3 u);
                GameObject st = ActSetBuildKit.EmptyWorld("Faces " + i, content.transform, p, Quaternion.LookRotation(f, u));
                var kids = new Transform[6];
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3f;
                    GameObject g = ActSetBuildKit.MeshObj("Face " + k, st.transform, face, new[] { faceDisc, faceGlow },
                        new Vector3(Mathf.Cos(a) * 11f, Mathf.Sin(a) * 11f, 0f),
                        Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f), Vector3.one * 3.4f);
                    kids[k] = g.transform;
                    faceR.Add(g.GetComponent<Renderer>());
                }
                stations.Add(Station(st, TripStationKind.FaceRing, d, i, 1f, kids, 0));
            }

            // ---- candy corn and wrapped sweets swirling around the rails
            int nCandy = Mathf.Clamp(Mathf.RoundToInt(L / 13f), 20, 100);
            for (int i = 0; i < nCandy; i++)
            {
                float d = start + L * (i + 0.3f) / nCandy;
                ActSetBuildKit.Pose(rail, d, out Vector3 p, out Vector3 f, out Vector3 u);
                GameObject st = ActSetBuildKit.EmptyWorld("Candy " + i, content.transform, p, Quaternion.LookRotation(f, u));
                var kids = new Transform[3];
                for (int k = 0; k < 3; k++)
                {
                    float h = ActSetBuildKit.HashF(i * 8 + k, 1), h2 = ActSetBuildKit.HashF(i * 8 + k, 2), h3 = ActSetBuildKit.HashF(i * 8 + k, 3);
                    float r = 4f + 4f * h, a = h2 * Mathf.PI * 2f;
                    Vector3 lp = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, (h3 - 0.5f) * 8f);
                    bool isSweet = k == 2;
                    GameObject c = ActSetBuildKit.MeshObj(isSweet ? "Sweet" : "Corn", st.transform, isSweet ? sweet : corn,
                        isSweet ? new[] { sweetM } : cornM, lp, Quaternion.identity, Vector3.one * (isSweet ? 0.9f : 1.0f));
                    kids[k] = c.transform;
                    (isSweet ? sweetR : cornR).Add(c.GetComponent<Renderer>());
                }
                stations.Add(Station(st, TripStationKind.Candy, d, i, 1f, kids, 0));
            }

            // ---- fractal bat and ghost clusters drifting beside the rails
            int nCl = Mathf.Clamp(Mathf.RoundToInt(L / 80f), 6, 20);
            for (int i = 0; i < nCl; i++)
            {
                float d = Mathf.Min(end - 5f, start + L * (i + 0.5f) / nCl + 20f);
                ActSetBuildKit.Pose(rail, d, out Vector3 p, out Vector3 f, out Vector3 u);
                float side = (i % 2 == 0) ? 1f : -1f;
                Vector3 right = ActSetBuildKit.RightOf(u, f);
                Vector3 pos = p + right * (side * (17f + ActSetBuildKit.HashF(i, 4) * 5f)) + u * (ActSetBuildKit.HashF(i, 5) * 10f - 3f);
                Quaternion rot = Quaternion.LookRotation((p - pos).normalized, u);
                GameObject st = ActSetBuildKit.EmptyWorld("Cluster " + i, content.transform, pos, rot);
                bool isGhost = i % 3 == 2;
                var layout = new List<(Vector2, float)>();
                if (isGhost) Frac(layout, Vector2.zero, 2.6f, 1, 5, 0.3f);
                else Frac(layout, Vector2.zero, 2.6f, 2, 4, 0.3f);
                var kids = new Transform[layout.Count];
                for (int k = 0; k < layout.Count; k++)
                {
                    Vector3 lp = new Vector3(layout[k].Item1.x, layout[k].Item1.y, 0f);
                    float sc = layout[k].Item2;
                    if (isGhost)
                    {
                        GameObject g = ActSetBuildKit.MeshObj("Ghost " + k, st.transform, ghost, new[] { ghostM }, lp, Quaternion.identity, Vector3.one * sc);
                        ghostR.Add(g.GetComponent<Renderer>());
                        for (int e = -1; e <= 1; e += 2)
                            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Eye", g.transform, new Vector3(0.24f * e, 0.5f, 0.55f),
                                new Vector3(0.18f, 0.28f, 0.12f), ghostEye);
                        kids[k] = g.transform;
                    }
                    else
                    {
                        GameObject b = ActSetBuildKit.MeshObj("Bat " + k, st.transform, bat, new[] { batM }, lp, Quaternion.identity, Vector3.one * sc);
                        batR.Add(b.GetComponent<Renderer>());
                        kids[k] = b.transform;
                    }
                }
                stations.Add(Station(st, TripStationKind.Cluster, d, i, side, kids, isGhost ? 0 : kids.Length));
            }

            // ---- pulsing starfield (positions are driven at runtime, wrapped around the cart)
            const int nStars = 220;
            var starT = new Transform[nStars];
            GameObject starRoot = ActSetBuildKit.Empty("Stars", content.transform);
            for (int i = 0; i < nStars; i++)
            {
                GameObject s = ActSetBuildKit.Prim(PrimitiveType.Cube, "Star", starRoot.transform, Vector3.zero, Vector3.one * 0.3f, starM,
                    Quaternion.Euler(45f, 45f, 0f));
                starT[i] = s.transform;
                starR.Add(s.GetComponent<Renderer>());
            }

            // ---- inverted world: the ground floats overhead as concentric glowing rings, a moon sits where the ground should be
            float minY = float.MaxValue, maxY = float.MinValue;
            Vector3 mid = Vector3.zero;
            for (int i = 0; i <= 64; i++)
            {
                ActSetBuildKit.Pose(rail, start + L * i / 64f, out Vector3 p, out Vector3 f, out Vector3 u);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
                if (i == 32) mid = p;
            }
            GameObject sky = ActSetBuildKit.Empty("Inverted Sky", content.transform, new Vector3(mid.x, maxY + 80f, mid.z), Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Overhead Ground", sky.transform, Vector3.zero, new Vector3(1200f, 0.5f, 1200f), skyDisc);
            for (int i = 1; i <= 7; i++)
            {
                float r = 60f * i;
                GameObject g = ActSetBuildKit.MeshObj("Sky Ring " + i, sky.transform, torus, new[] { skyRingM },
                    new Vector3(0f, -1f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(r, r, r * 0.25f));
                skyR.Add(g.GetComponent<Renderer>());
            }
            GameObject moon = ActSetBuildKit.Prim(PrimitiveType.Sphere, "Moon Below", content.transform,
                new Vector3(mid.x + 150f, minY - 170f, mid.z - 100f), Vector3.one * 110f, moonM);

            // soft travelling light so rails and candy have something to catch
            Light fill = ActSetBuildKit.PointLight("Trip Fill", content.transform, Vector3.zero, Orange, 2.2f, 20f);

            set.stations = stations.ToArray();
            set.stars = starT;
            set.skyRoot = sky.transform;
            set.moonRoot = moon.transform;
            set.fill = fill;
            set.groups = new[]
            {
                Hue(ringR[0], 0, 0f, 1.1f, 0.3f), Hue(ringR[1], 0, 1f, 1.1f, 0.3f), Hue(ringR[2], 0, 2f, 1.1f, 0.3f),
                Hue(tubeR[0], 0, 0f, 1.8f, 0.5f, 0.45f), Hue(tubeR[1], 0, 1f, 1.8f, 0.5f, 0.45f), Hue(tubeR[2], 0, 2f, 1.8f, 0.5f, 0.45f),
                Hue(faceR, 0, 0f, 0.45f, 0.2f), Hue(faceR, 1, 1.5f, 1.6f, 0.3f, 0.25f),
                Hue(cornR, 0, 0.5f, 0.9f, 0.4f, 0.55f), Hue(cornR, 1, 0f, 1.1f, 0.3f), Hue(cornR, 2, 1f, 1.1f, 0.3f),
                Hue(sweetR, 0, 2f, 1.1f, 0.3f), Hue(batR, 0, 1f, 1.2f, 0.3f), Hue(ghostR, 0, 2f, 0.8f, 0.3f, 0f, 0.55f),
                Hue(starR, 0, 0f, 1.2f, 0.4f, 0.6f), Hue(skyR, 0, 1f, 1.0f, 0.3f)
            };
            return root;
        }

        static TripStation Station(GameObject go, TripStationKind kind, float dist, int index, float side, Transform[] kids, int flap)
        {
            var s = go.AddComponent<TripStation>();
            s.kind = kind;
            s.dist = dist;
            s.index = index;
            s.side = side;
            s.kids = kids;
            s.flapCount = flap;
            return s;
        }

        static HueGroup Hue(List<Renderer> r, int slot, float phase, float emission, float baseMul, float whiten = 0f, float alpha = 1f)
        {
            return new HueGroup
            {
                renderers = r.ToArray(), slot = slot, phase = phase, emission = emission, baseMul = baseMul, whiten = whiten, alpha = alpha
            };
        }

        /// <summary>Recursive ring-of-rings layout: one at the centre, n around it, each repeating at 0.42 scale.</summary>
        static void Frac(List<(Vector2, float)> o, Vector2 c, float sc, int depth, int n, float rot)
        {
            o.Add((c, sc));
            if (depth == 0) return;
            for (int k = 0; k < n; k++)
            {
                float a = rot + k * Mathf.PI * 2f / n;
                Frac(o, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (sc * 1.9f), sc * 0.42f, depth - 1, n, a + 0.5f);
            }
        }
    }
}
