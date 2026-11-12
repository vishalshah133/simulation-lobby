using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>The climb: lamp posts along the lift, a pumpkin-lit miniature town below with lantern strings, ambient bats, the fake-out bat.</summary>
    public static class ChainLiftSetBuilder
    {
        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            ChainLiftSet set = CoasterSetKit.NewSet<ChainLiftSet>(parent, "ChainLift", out Transform c);
            float groundY = CoasterKit.GroundY(rail);
            float d0 = Mathf.Min(cfg.ActStart(ActId.ChainLift), rail.Length);
            float d1 = Mathf.Min(cfg.ActEnd(ActId.ChainLift), rail.Length);
            float len = Mathf.Max(20f, d1 - d0);
            var chunks = new CoasterSetKit.Chunker(c, "Dressing");

            // Lamp posts with lanterns on both sides of the lift, every 4.5 m so the climb reads as moving even at a crawl.
            Material post = CoasterMaterials.Get("StationPost", new Color(0.12f, 0.09f, 0.14f), default, 0.2f);
            int post_i = 0;
            for (float d = d0 + 4f; d < d1; d += 4.5f, post_i++)
            {
                CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
                Transform ck = chunks.At(d);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 pp = p + r * (side * 2.1f);
                    CoasterFigures.Bar(ck, "LampPost", pp - u * 0.5f, pp + u * 2.6f, 0.12f, post);
                    CoasterFigures.Lantern(ck, "Lantern", pp + u * 2.5f - r * (side * 0.25f), 0.5f, post_i % 6 == 0 && side > 0, post_i * 2 + side + 200);
                }
            }

            // Truss arches over the lift every ~27 m with hanging bulbs: something big to pass under.
            Material beam = CoasterMaterials.Get("StationPost", new Color(0.12f, 0.09f, 0.14f), default, 0.2f);
            for (float d = d0 + 13f; d < d1; d += 27f)
            {
                CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
                Transform ck = chunks.At(d);
                CoasterFigures.Bar(ck, "ArchPostL", p - r * 2.6f - u * 0.5f, p - r * 2.6f + u * 4.4f, 0.16f, beam);
                CoasterFigures.Bar(ck, "ArchPostR", p + r * 2.6f - u * 0.5f, p + r * 2.6f + u * 4.4f, 0.16f, beam);
                CoasterFigures.Bar(ck, "ArchBeam", p - r * 2.7f + u * 4.4f, p + r * 2.7f + u * 4.4f, 0.2f, beam);
                for (int k = -1; k <= 1; k++)
                {
                    CoasterKit.Prim(PrimitiveType.Sphere, ck, "ArchBulb", p + r * (k * 1.7f) + u * 4.15f, Vector3.one * 0.3f, k == 0 ? CoasterLook.Pumpkin : CoasterLook.LanternOn);
                }
            }

            // The town below: houses either side, set back from the lift, with lit windows and the odd rooftop jack-o'-lantern.
            var roofPoints = new System.Collections.Generic.List<Vector3>();
            var roofChunk = new System.Collections.Generic.List<Transform>();
            int idx = 0;
            for (float d = d0 - 10f; d < d1 + 20f; d += 6.5f, idx++)
            {
                float dd = Mathf.Clamp(d, 0f, rail.Length);
                CoasterKit.Frame(rail, dd, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
                Vector3 ff = CoasterSetKit.Flat(f);
                Vector3 rr = Vector3.Cross(Vector3.up, ff);
                for (int side = -1; side <= 1; side += 2)
                {
                    float jitter = CoasterKit.Hash(idx, side + 5);
                    float lateral = 16f + 28f * jitter;
                    Vector3 basePos = new Vector3(p.x, groundY, p.z) + rr * (side * lateral) + ff * ((CoasterKit.Hash(idx, side + 9) - 0.5f) * 3f);
                    float w = 3.2f + 3f * CoasterKit.Hash(idx, side + 11);
                    float h = 3.5f + 4.5f * CoasterKit.Hash(idx, side + 13);
                    float dep = 3.2f + 2.4f * CoasterKit.Hash(idx, side + 15);
                    Quaternion rot = Quaternion.LookRotation(-rr * side, Vector3.up);
                    var house = new GameObject("House");
                    house.transform.SetParent(chunks.At(dd), false);
                    house.transform.SetPositionAndRotation(basePos, rot);
                    Transform t = house.transform;
                    CoasterKit.Local(PrimitiveType.Cube, t, "Body", new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, dep), CoasterLook.Wall(idx + side));
                    CoasterFigures.Prism(t, "Roof", new Vector3(0f, h, 0f), new Vector3(w * 1.12f, 1.6f + w * 0.2f, dep * 1.1f), CoasterLook.Roof);
                    int windows = 2 + (int)(CoasterKit.Hash(idx, side + 17) * 3f);
                    for (int k = 0; k < windows; k++)
                    {
                        float wx = (k - (windows - 1) * 0.5f) * (w / (windows + 0.5f));
                        float wy = h * (0.3f + 0.35f * CoasterKit.Hash(idx * 7 + k, side));
                        Material wm = CoasterKit.Hash(idx * 3 + k, 4) > 0.9f ? CoasterLook.WindowGreen : CoasterLook.Window;
                        CoasterKit.Local(PrimitiveType.Cube, t, "Window", new Vector3(wx, wy, dep * 0.5f + 0.02f), new Vector3(0.6f, 0.75f, 0.05f), wm);
                    }

                    if (idx % 5 == 0)
                    {
                        CoasterFigures.Pumpkin(t, "RoofPumpkin", t.TransformPoint(new Vector3(0f, h + 0.9f + w * 0.1f, dep * 0.15f)), rot, 1.2f);
                    }

                    roofPoints.Add(t.TransformPoint(new Vector3(0f, h + 0.4f, dep * 0.5f)));
                    roofChunk.Add(chunks.At(dd));
                }
            }

            // Lantern strings strung between neighbouring rooftops.
            for (int i = 0; i + 2 < roofPoints.Count; i += 2)
            {
                Vector3 a = roofPoints[i];
                Vector3 b = roofPoints[i + 2];
                if ((a - b).magnitude > 24f)
                {
                    continue;
                }

                const int bulbs = 7;
                for (int k = 1; k < bulbs; k++)
                {
                    float s = k / (float)bulbs;
                    Vector3 pt = Vector3.Lerp(a, b, s) - Vector3.up * (1.4f * Mathf.Sin(s * Mathf.PI));
                    CoasterKit.Prim(PrimitiveType.Sphere, roofChunk[i], "Bulb", pt, Vector3.one * 0.32f, (k + i) % 3 == 0 ? CoasterLook.Pumpkin : CoasterLook.LanternOn);
                }
            }

            // A church tower with a lit clock face for a landmark, on the left of the mid-lift.
            CoasterKit.Frame(rail, d0 + len * 0.5f, out Vector3 mp, out Vector3 mf, out _, out Vector3 mr);
            Vector3 towerBase = new Vector3(mp.x, groundY, mp.z) - mr * 34f;
            Quaternion towerRot = Quaternion.LookRotation(mr, Vector3.up);
            var tower = new GameObject("Tower");
            tower.transform.SetParent(c, false);
            tower.transform.SetPositionAndRotation(towerBase, towerRot);
            CoasterKit.Local(PrimitiveType.Cube, tower.transform, "Shaft", new Vector3(0f, 9f, 0f), new Vector3(5f, 18f, 5f), CoasterLook.Wall(1));
            CoasterFigures.Cone(tower.transform, "Spire", new Vector3(0f, 18f, 0f), new Vector3(6.4f, 9f, 6.4f), CoasterLook.Roof, Quaternion.Euler(0f, 45f, 0f));
            CoasterKit.Local(PrimitiveType.Cylinder, tower.transform, "ClockFace", new Vector3(0f, 14.5f, 2.55f), new Vector3(3f, 0.06f, 3f), CoasterLook.LanternOn, Quaternion.Euler(90f, 0f, 0f));
            CoasterKit.Local(PrimitiveType.Cube, tower.transform, "HandA", new Vector3(0f, 14.9f, 2.62f), new Vector3(0.12f, 1.1f, 0.05f), CoasterLook.Black);
            CoasterKit.Local(PrimitiveType.Cube, tower.transform, "HandB", new Vector3(0.45f, 14.5f, 2.62f), new Vector3(0.9f, 0.12f, 0.05f), CoasterLook.Black);
            CoasterFigures.PointLight(tower.transform, "ClockGlow", new Vector3(0f, 14.5f, 5f), CoasterPalette.Lantern, 5f, 14f);

            chunks.Finish(set.gameObject);

            // Ambient bats circling over the climb.
            var bats = new GameObject("Bats");
            bats.transform.SetParent(c, false);
            bats.transform.position = mp + Vector3.up * 6f;
            for (int i = 0; i < 9; i++)
            {
                var bp = CoasterFigures.Bat(bats.transform, "Bat" + i, bats.transform.position, 1.3f, 11f + i, i);
                var orbit = bp.root.AddComponent<Orbit>();
                orbit.center = new Vector3(CoasterKit.Hash(i, 1) * 8f - 4f, CoasterKit.Hash(i, 2) * 5f, CoasterKit.Hash(i, 3) * 20f - 10f);
                orbit.radius = 7f + 7f * CoasterKit.Hash(i, 4);
                orbit.angularSpeed = (i % 2 == 0 ? 1f : -1f) * (0.4f + 0.4f * CoasterKit.Hash(i, 5));
                orbit.bob = 0.6f + CoasterKit.Hash(i, 6);
                orbit.phase = i * 0.9f;
            }

            // The fake-out bat: big, eyes lit, hidden until the quiet.
            var big = CoasterFigures.Bat(c, "FakeBat", mp + Vector3.up * 8f, 2.8f, 15f, 0f);
            big.root.SetActive(false);
            set.bat = big.root;
            return set.gameObject;
        }
    }
}
