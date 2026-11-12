using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// The station at the end of the ride, changed: dusty, dead flowers, cobwebs, the skeleton operator under a "Welcome back" sign,
    /// and the "SEAT 6: YOU" board ahead of the stop. Platform sits on the left of the rail.
    /// </summary>
    public static class StationReturnSetBuilder
    {
        /// <summary>Set false if the original station's platform and roof already exist at this spot (a closed-loop rail), to avoid doubled geometry.</summary>
        public static bool BuildPlatform = true;

        const float PlatformTop = -0.8f;
        const float Seg = 8f;

        static readonly Color Orange = new Color(1f, 0.48f, 0.09f);
        static readonly Color Bone = new Color(0.93f, 0.9f, 0.82f);
        static readonly Color Yellow = new Color(1f, 0.78f, 0.25f);

        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            ActSetBuildKit.Span(cfg, rail, ActId.StationReturn, out float start, out float end);
            end = Mathf.Min(end, rail.Length);

            Material ground = ActSetBuildKit.M("StationR_Ground", new Color(0.1f, 0.1f, 0.12f), default, 0.1f);
            Material plat = ActSetBuildKit.M("StationR_Platform", new Color(0.24f, 0.22f, 0.21f), default, 0.1f);
            Material wall = ActSetBuildKit.M("StationR_Wall", new Color(0.16f, 0.15f, 0.2f), default, 0.1f);
            Material roof = ActSetBuildKit.M("StationR_Roof", new Color(0.13f, 0.11f, 0.15f), default, 0.1f);
            Material postM = ActSetBuildKit.M("StationR_Post", new Color(0.3f, 0.28f, 0.28f), default, 0.2f);
            Material bulb = ActSetBuildKit.M("StationR_Bulb", Yellow * 0.5f, ActSetBuildKit.Hdr(Yellow, 1.6f), 0.2f);
            Material planter = ActSetBuildKit.M("StationR_Planter", new Color(0.25f, 0.12f, 0.07f), default, 0.1f);
            Material stem = ActSetBuildKit.M("StationR_Stem", new Color(0.24f, 0.2f, 0.09f), default, 0.1f);
            Material head = ActSetBuildKit.M("StationR_DeadBloom", new Color(0.32f, 0.14f, 0.22f), ActSetBuildKit.Hdr(new Color(0.6f, 0.24f, 1f), 0.12f), 0.1f);
            Material web = ActSetBuildKit.M("StationR_Web", Bone * 0.7f, ActSetBuildKit.Hdr(Bone, 0.3f), 0.1f);
            Material bone = ActSetBuildKit.M("StationR_Bone", Bone * 0.9f, ActSetBuildKit.Hdr(Bone, 0.3f), 0.3f);
            Material black = ActSetBuildKit.M("StationR_Black", new Color(0.01f, 0.01f, 0.02f), default, 0.2f);
            Material ember = ActSetBuildKit.M("StationR_Ember", Orange, ActSetBuildKit.Hdr(Orange, 2.0f), 0.2f);
            Material cap = ActSetBuildKit.M("StationR_Cap", new Color(0.1f, 0.07f, 0.22f), ActSetBuildKit.Hdr(Orange, 0.05f), 0.2f);
            Material board = ActSetBuildKit.M("StationR_Board", new Color(0.07f, 0.06f, 0.12f), default, 0.2f);
            Material border = ActSetBuildKit.M("StationR_Border", Bone * 0.7f, ActSetBuildKit.Hdr(Bone, 0.3f), 0.2f);
            Material seatBulb = ActSetBuildKit.M("StationR_SeatBulb", Yellow * 0.5f, ActSetBuildKit.Hdr(Yellow, 1.8f), 0.2f);
            Material dustM = ActSetBuildKit.M("StationR_Dust", Bone * 0.5f, ActSetBuildKit.Hdr(Bone, 0.5f), 0.1f);

            Mesh webMesh = ActSetBuildKit.SaveMeshFresh(WebMesh(), "CoastStation_Web");

            GameObject root = ActSetBuildKit.Empty("Station Return Set", parent);
            var set = root.AddComponent<StationReturnSet>();
            set.startDist = start;
            set.endDist = end;
            GameObject content = ActSetBuildKit.Empty("Content", root.transform);
            set.content = content;

            var lamps = new List<Light>();
            var lampBase = new List<float>();
            var lampDist = new List<float>();

            // ---- platform, roof, posts, bulbs, planters with dead flowers, cobwebs
            int si = 0;
            for (float d = start; d < end; d += Seg, si++)
            {
                float dc = Mathf.Min(d + Seg * 0.5f, end);
                ActSetBuildKit.Pose(rail, dc, out Vector3 p, out Vector3 f, out Vector3 u);
                GameObject s = ActSetBuildKit.EmptyWorld("Station " + si, content.transform, p, ActSetBuildKit.Level(f));
                if (BuildPlatform)
                {
                    ActSetBuildKit.Prim(PrimitiveType.Cube, "Ground", s.transform, new Vector3(-1f, PlatformTop - 1.4f, 0f), new Vector3(24f, 0.4f, Seg + 0.3f), ground);
                    ActSetBuildKit.Prim(PrimitiveType.Cube, "Platform", s.transform, new Vector3(-5.9f, PlatformTop - 0.35f, 0f), new Vector3(7.4f, 0.7f, Seg + 0.3f), plat);
                    ActSetBuildKit.Prim(PrimitiveType.Cube, "Back Wall", s.transform, new Vector3(-9.6f, PlatformTop + 2.7f, 0f), new Vector3(0.5f, 5.4f, Seg + 0.3f), wall);
                    ActSetBuildKit.Prim(PrimitiveType.Cube, "Roof", s.transform, new Vector3(-6.1f, 3.95f, 0f), new Vector3(7.6f, 0.25f, Seg + 0.3f), roof);
                    ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Post", s.transform, new Vector3(-2.8f, PlatformTop + 2.35f, 0f), new Vector3(0.18f, 2.35f, 0.18f), postM);
                }
                GameObject b = ActSetBuildKit.Prim(PrimitiveType.Sphere, "Bulb", s.transform, new Vector3(-3.6f, 3.2f, 0f), Vector3.one * 0.3f, bulb);
                if (si % 2 == 0)
                {
                    lamps.Add(ActSetBuildKit.PointLight("Station Lamp", s.transform, new Vector3(-3.6f, 3.0f, 0f), new Color(1f, 0.65f, 0.3f), 2.4f, 11f));
                    lampBase.Add(2.4f);
                    lampDist.Add(dc);
                }
                // dead flowers in a planter against the back wall
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Planter", s.transform, new Vector3(-8.7f, PlatformTop + 0.25f, 0f), new Vector3(0.7f, 0.5f, 1.8f), planter);
                for (int k = 0; k < 4; k++)
                {
                    float h = ActSetBuildKit.HashF(si * 8 + k, 51), h2 = ActSetBuildKit.HashF(si * 8 + k, 52);
                    Vector3 baseP = new Vector3(-8.7f, PlatformTop + 0.5f, -0.7f + 0.47f * k);
                    Quaternion lean = Quaternion.Euler((h - 0.5f) * 70f, 0f, (h2 - 0.5f) * 70f);
                    Vector3 tip = baseP + lean * new Vector3(0f, 0.55f, 0f);
                    ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Stem", s.transform, baseP + lean * new Vector3(0f, 0.275f, 0f), new Vector3(0.035f, 0.275f, 0.035f), stem, lean);
                    ActSetBuildKit.Prim(PrimitiveType.Sphere, "Dead Bloom", s.transform, tip + Vector3.down * 0.06f, new Vector3(0.2f, 0.1f, 0.2f), head);
                }
                // cobwebs: hung in the roof corners, one across the track entrance every other segment
                GameObject w1 = ActSetBuildKit.MeshObj("Web", s.transform, webMesh, new[] { web }, new Vector3(-2.9f, 3.3f, 0.5f), Quaternion.identity, Vector3.one * 1.3f);
                if (si % 2 == 1)
                    ActSetBuildKit.MeshObj("Web Across", s.transform, webMesh, new[] { web }, new Vector3(0f, 3.0f, 0f), Quaternion.identity, Vector3.one * 2.3f);
            }

            // ---- skeleton operator, facing the incoming cart, with the speech sign above
            float dOp = Mathf.Max(start + 5f, end - 3f);
            ActSetBuildKit.Pose(rail, dOp, out Vector3 po, out Vector3 fo, out Vector3 uo);
            Quaternion lvo = ActSetBuildKit.Level(fo);
            set.operatorDist = dOp;
            GameObject op = ActSetBuildKit.EmptyWorld("Skeleton Operator", content.transform, po + lvo * new Vector3(-3.4f, PlatformTop, 0f), lvo * Quaternion.Euler(0f, 180f, 0f));
            op.transform.localScale = Vector3.one * 1.15f;
            BuildOperator(op.transform, bone, black, ember, cap, out Transform opHead, out Transform opJaw, out Transform opArm);
            set.opRoot = op.transform;
            set.opHead = opHead;
            set.opJaw = opJaw;
            set.opArm = opArm;
            ActSetBuildKit.PointLight("Operator Light", content.transform, Vector3.zero, new Color(1f, 0.6f, 0.25f), 3f, 8f).transform.position =
                po + lvo * new Vector3(-3.0f, 2.6f, -0.8f);

            // lever he "pulled"
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Lever Post", content.transform, po + lvo * new Vector3(-2.7f, PlatformTop + 0.55f, 0.6f), new Vector3(0.18f, 1.1f, 0.18f), postM, lvo);
            ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Lever", content.transform, po + lvo * new Vector3(-2.7f, PlatformTop + 1.2f, 0.9f), new Vector3(0.07f, 0.45f, 0.07f), ember, lvo * Quaternion.Euler(60f, 0f, 0f));

            GameObject speech = ActSetBuildKit.EmptyWorld("Speech Sign", content.transform, po + lvo * new Vector3(-3.4f, 2.9f, 0f), lvo);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Border", speech.transform, new Vector3(0f, 0f, 0.05f), new Vector3(3.8f, 1.1f, 0.08f), border);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Board", speech.transform, Vector3.zero, new Vector3(3.6f, 0.9f, 0.1f), board);
            ActSetBuildKit.Text("Speech Text", speech.transform, new Vector3(0f, 0f, -0.07f), new Vector2(3.3f, 0.75f), "Welcome back", Bone, 6f);
            set.speechRoot = speech.transform;

            // ---- SEAT 6: YOU board ahead of the stop
            ActSetBuildKit.Pose(rail, end, out Vector3 pe, out Vector3 fe, out Vector3 ue);
            Quaternion lve = ActSetBuildKit.Level(fe);
            GameObject seat = ActSetBuildKit.EmptyWorld("Seat Sign", content.transform, pe + lve * new Vector3(0f, 2.5f, 6f), lve);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Border", seat.transform, new Vector3(0f, 0f, 0.06f), new Vector3(6.6f, 1.9f, 0.1f), border);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Board", seat.transform, Vector3.zero, new Vector3(6.2f, 1.6f, 0.12f), board);
            for (int sgn = -1; sgn <= 1; sgn += 2)
                ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Sign Post", seat.transform, new Vector3(sgn * 3.4f, -1.6f, 0.3f), new Vector3(0.15f, 2.7f, 0.15f), postM);
            TMPro.TextMeshPro seatText = ActSetBuildKit.Text("Seat Text", seat.transform, new Vector3(0f, 0f, -0.08f), new Vector2(5.8f, 1.3f), "SEAT 6: YOU", new Color(0.28f, 0.26f, 0.22f), 12f);
            var bulbs = new List<Renderer>();
            for (int i = 0; i < 6; i++)
            {
                float x = -2.75f + 5.5f * i / 5f;
                bulbs.Add(ActSetBuildKit.Prim(PrimitiveType.Sphere, "Bulb Top", seat.transform, new Vector3(x, 1.0f, -0.1f), Vector3.one * 0.2f, seatBulb).GetComponent<Renderer>());
                bulbs.Add(ActSetBuildKit.Prim(PrimitiveType.Sphere, "Bulb Bottom", seat.transform, new Vector3(x, -1.0f, -0.1f), Vector3.one * 0.2f, seatBulb).GetComponent<Renderer>());
            }
            Light seatLight = ActSetBuildKit.PointLight("Seat Light", seat.transform, new Vector3(0f, 0f, -2.5f), new Color(1f, 0.8f, 0.4f), 3f, 12f);
            set.seatText = seatText;
            set.seatBulbs = bulbs.ToArray();
            set.seatBulbEmission = ActSetBuildKit.Hdr(Yellow, 1.8f);
            set.seatLight = seatLight;

            // ---- dust motes
            int nDust = 40;
            var dustT = new Transform[nDust];
            var dustB = new Vector3[nDust];
            GameObject dustRoot = ActSetBuildKit.Empty("Dust", content.transform);
            for (int i = 0; i < nDust; i++)
            {
                float dd = Mathf.Lerp(start, end, ActSetBuildKit.HashF(i, 61));
                ActSetBuildKit.Pose(rail, dd, out Vector3 p, out Vector3 f, out Vector3 u);
                Vector3 pos = p + ActSetBuildKit.Level(f) * new Vector3(Mathf.Lerp(-7f, 3f, ActSetBuildKit.HashF(i, 62)),
                                                                        Mathf.Lerp(-0.4f, 3.2f, ActSetBuildKit.HashF(i, 63)), 0f);
                GameObject m = ActSetBuildKit.Prim(PrimitiveType.Sphere, "Mote", dustRoot.transform, Vector3.zero, Vector3.one * 0.05f, dustM);
                m.transform.position = pos;
                dustT[i] = m.transform;
                dustB[i] = pos;
            }
            set.dust = dustT;
            set.dustBase = dustB;

            set.lamps = lamps.ToArray();
            set.lampBase = lampBase.ToArray();
            set.lampDist = lampDist.ToArray();
            return root;
        }

        /// <summary>Skeleton, feet at origin, facing +Z, about 1.9 m tall.</summary>
        static void BuildOperator(Transform t, Material bone, Material black, Material ember, Material cap,
                                  out Transform head, out Transform jaw, out Transform arm)
        {
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Pelvis", t, new Vector3(0f, 0.95f, 0f), new Vector3(0.32f, 0.14f, 0.2f), bone);
            ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Spine", t, new Vector3(0f, 1.25f, 0f), new Vector3(0.05f, 0.25f, 0.05f), bone);
            for (int i = 0; i < 4; i++)
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Rib", t, new Vector3(0f, 1.3f + 0.08f * i, 0.02f), new Vector3(0.34f - 0.02f * i, 0.03f, 0.2f), bone);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Shoulders", t, new Vector3(0f, 1.62f, 0f), new Vector3(0.52f, 0.05f, 0.08f), bone);
            for (int s = -1; s <= 1; s += 2)
            {
                ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Thigh", t, new Vector3(s * 0.1f, 0.5f, 0f), new Vector3(0.055f, 0.45f, 0.055f), bone);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Foot", t, new Vector3(s * 0.1f, 0.03f, 0.07f), new Vector3(0.1f, 0.06f, 0.24f), bone);
            }
            // left arm hangs, right arm is a pivot that waves
            ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Arm Hang", t, new Vector3(-0.27f, 1.38f, 0f), new Vector3(0.045f, 0.25f, 0.045f), bone);
            GameObject armPiv = ActSetBuildKit.Empty("Arm Pivot", t, new Vector3(0.27f, 1.6f, 0f), Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Arm", armPiv.transform, new Vector3(0f, -0.22f, 0f), new Vector3(0.045f, 0.22f, 0.045f), bone);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Hand", armPiv.transform, new Vector3(0f, -0.46f, 0f), Vector3.one * 0.09f, bone);
            arm = armPiv.transform;

            GameObject h = ActSetBuildKit.Empty("Head", t, new Vector3(0f, 1.82f, 0f), Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Skull", h.transform, Vector3.zero, new Vector3(0.26f, 0.3f, 0.28f), bone);
            for (int e = -1; e <= 1; e += 2)
            {
                ActSetBuildKit.Prim(PrimitiveType.Sphere, "Socket", h.transform, new Vector3(e * 0.065f, 0.03f, 0.115f), new Vector3(0.08f, 0.09f, 0.06f), black);
                ActSetBuildKit.Prim(PrimitiveType.Sphere, "Ember", h.transform, new Vector3(e * 0.065f, 0.03f, 0.14f), Vector3.one * 0.03f, ember);
            }
            ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Cap Crown", h.transform, new Vector3(0f, 0.17f, 0f), new Vector3(0.24f, 0.05f, 0.24f), cap);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Cap Brim", h.transform, new Vector3(0f, 0.12f, 0.14f), new Vector3(0.26f, 0.02f, 0.14f), cap);
            GameObject jawPiv = ActSetBuildKit.Empty("Jaw Pivot", h.transform, new Vector3(0f, -0.1f, -0.02f), Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Jaw", jawPiv.transform, new Vector3(0f, -0.05f, 0.09f), new Vector3(0.14f, 0.05f, 0.17f), bone);
            head = h.transform;
            jaw = jawPiv.transform;
        }

        /// <summary>Flat cobweb in the XY plane, unit radius: 8 spokes and 3 sagging rings, thin double-sided quads.</summary>
        static Mesh WebMesh()
        {
            var mb = new MeshAcc();
            const int spokes = 8;
            const float w = 0.03f;
            for (int i = 0; i < spokes; i++)
            {
                float a = 2f * Mathf.PI * i / spokes;
                Quad(mb, Vector2.zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), w);
            }
            float[] rings = { 0.35f, 0.62f, 0.88f };
            foreach (float r in rings)
                for (int i = 0; i < spokes; i++)
                {
                    float a0 = 2f * Mathf.PI * i / spokes, a1 = 2f * Mathf.PI * (i + 1) / spokes;
                    Vector2 p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
                    Vector2 p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                    Vector2 mid = (p0 + p1) * 0.5f * 0.9f; // sag toward the centre
                    Quad(mb, p0, mid, w * 0.8f);
                    Quad(mb, mid, p1, w * 0.8f);
                }
            return mb.ToMesh();
        }

        static void Quad(MeshAcc mb, Vector2 a, Vector2 b, float width)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            mb.FlatFan(0, (a + b) * 0.5f, new[] { a - perp, b - perp, b + perp, a + perp }, 0f);
        }
    }
}
