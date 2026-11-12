using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>Builds the asylum: long corridor, cell doors, flickering ceiling lights, two fake wheelchairs, the pale figure.</summary>
    public static class AsylumSetBuilder
    {
        const float HalfW = 3.0f;
        const float Floor = -1.2f;
        const float Ceil = 2.8f;
        const float Seg = 10f;

        static readonly Color Bone = new Color(0.93f, 0.9f, 0.82f);
        static readonly Color Pale = new Color(0.85f, 1f, 0.88f);
        static readonly Color Green = new Color(0.49f, 1f, 0.31f);
        static readonly Color Blood = new Color(0.8f, 0.04f, 0.06f);

        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            ActSetBuildKit.Span(cfg, rail, ActId.Asylum, out float start, out float end);

            Material floorA = ActSetBuildKit.M("Asylum_FloorA", new Color(0.2f, 0.23f, 0.21f), default, 0.7f);
            Material floorB = ActSetBuildKit.M("Asylum_FloorB", new Color(0.13f, 0.15f, 0.14f), default, 0.7f);
            Material wall = ActSetBuildKit.M("Asylum_Wall", new Color(0.4f, 0.44f, 0.38f), ActSetBuildKit.Hdr(Pale, 0.05f), 0.15f);
            Material tile = ActSetBuildKit.M("Asylum_Tile", new Color(0.24f, 0.32f, 0.29f), ActSetBuildKit.Hdr(Green, 0.04f), 0.5f);
            Material ceil = ActSetBuildKit.M("Asylum_Ceiling", new Color(0.22f, 0.23f, 0.22f), default, 0.1f);
            Material pipe = ActSetBuildKit.M("Asylum_Pipe", new Color(0.12f, 0.12f, 0.13f), default, 0.6f, 0.6f);
            Material doorM = ActSetBuildKit.M("Asylum_Door", new Color(0.2f, 0.24f, 0.24f), default, 0.4f, 0.4f);
            Material winDark = ActSetBuildKit.M("Asylum_DoorWin", new Color(0.01f, 0.01f, 0.01f), default, 0.1f);
            Material winGlow = ActSetBuildKit.M("Asylum_DoorGlow", Green * 0.3f, ActSetBuildKit.Hdr(Green, 0.7f), 0.1f);
            Color fixEmission = ActSetBuildKit.Hdr(Pale, 1.7f);
            Material fixM = ActSetBuildKit.M("Asylum_Fixture", Pale * 0.6f, fixEmission, 0.1f);
            Material robe = ActSetBuildKit.M("Asylum_Robe", new Color(0.82f, 0.84f, 0.8f), ActSetBuildKit.Hdr(Bone, 0.3f), 0.2f);
            Material hair = ActSetBuildKit.M("Asylum_Hair", new Color(0.02f, 0.02f, 0.03f), default, 0.2f);
            Material skin = ActSetBuildKit.M("Asylum_Skin", new Color(0.9f, 0.9f, 0.86f), ActSetBuildKit.Hdr(Bone, 0.75f), 0.2f);
            Material redEye = ActSetBuildKit.M("Asylum_RedEye", Blood, ActSetBuildKit.Hdr(Blood, 2.2f), 0.2f);
            Material chairM = ActSetBuildKit.M("Asylum_Chair", new Color(0.55f, 0.57f, 0.55f), ActSetBuildKit.Hdr(Pale, 0.18f), 0.5f, 0.5f);
            Material dark = ActSetBuildKit.M("Asylum_Dark", new Color(0.03f, 0.03f, 0.04f), default, 0.3f);

            GameObject root = ActSetBuildKit.Empty("Asylum Set", parent);
            var set = root.AddComponent<AsylumSet>();
            set.startDist = start;
            set.endDist = end;
            GameObject content = ActSetBuildKit.Empty("Content", root.transform);
            set.content = content;
            set.floorDrop = -Floor;

            var cullObjs = new List<GameObject>();
            var cullDists = new List<float>();

            var figEvents = ActSetBuildKit.EventsOf(cfg, ScareKind.AsylumFigure);
            var chairEvents = ActSetBuildKit.EventsOf(cfg, ScareKind.FakeWheelchair);
            float lastLight = figEvents.Count > 0 ? figEvents[0].ev.distance + 14f : end - 8f;
            lastLight = Mathf.Min(lastLight, end - 4f);

            // ---- corridor shell
            int si = 0;
            for (float d = start; d < end; d += Seg, si++)
            {
                float dc = Mathf.Min(d + Seg * 0.5f, end);
                ActSetBuildKit.Pose(rail, dc, out Vector3 p, out Vector3 f, out Vector3 u);
                GameObject s = ActSetBuildKit.EmptyWorld("Corridor " + si, content.transform, p, ActSetBuildKit.Level(f));
                float h = Ceil - Floor;
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Floor", s.transform, new Vector3(0f, Floor - 0.2f, 0f), new Vector3(HalfW * 2f + 1f, 0.4f, Seg + 0.3f), si % 2 == 0 ? floorA : floorB);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Ceiling", s.transform, new Vector3(0f, Ceil + 0.2f, 0f), new Vector3(HalfW * 2f + 1f, 0.4f, Seg + 0.3f), ceil);
                for (int side = -1; side <= 1; side += 2)
                {
                    ActSetBuildKit.Prim(PrimitiveType.Cube, "Wall", s.transform, new Vector3(side * (HalfW + 0.25f), Floor + h * 0.5f, 0f), new Vector3(0.5f, h, Seg + 0.3f), wall);
                    ActSetBuildKit.Prim(PrimitiveType.Cube, "Tiles", s.transform, new Vector3(side * (HalfW - 0.03f), Floor + 1.0f, 0f), new Vector3(0.08f, 2.0f, Seg + 0.3f), tile);
                    ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Pipe", s.transform, new Vector3(side * (HalfW - 0.4f), Ceil - 0.25f, 0f), new Vector3(0.14f, (Seg + 0.3f) * 0.5f, 0.14f), pipe, Quaternion.Euler(90f, 0f, 0f));
                }
                cullObjs.Add(s);
                cullDists.Add(dc);
            }

            // ---- wheelchairs (rolled out of a left-hand cell door), built first so static doors avoid their spot
            var slots = new List<WheelchairSlot>();
            var chairSpots = new List<float>();
            foreach (var (index, ev) in chairEvents)
            {
                float dC = Mathf.Min(ev.distance + 6f, end - 3f);
                chairSpots.Add(dC);
                ActSetBuildKit.Pose(rail, dC, out Vector3 p, out Vector3 f, out Vector3 u);
                Quaternion lv = ActSetBuildKit.Level(f);
                var slot = new WheelchairSlot { hint = index, doorAngle = 85f };
                slot.from = p + lv * new Vector3(-(HalfW - 0.5f), Floor, 0f);
                slot.to = p + lv * new Vector3(1.75f, Floor, 0.5f);
                slot.rot = lv * Quaternion.Euler(0f, 90f, 0f);

                GameObject chair = ActSetBuildKit.EmptyWorld("Wheelchair " + index, content.transform, slot.from, slot.rot);
                BuildWheelchair(chair.transform, chairM, dark, out slot.wheelL, out slot.wheelR);
                slot.chair = chair;

                GameObject holder = ActSetBuildKit.EmptyWorld("Chair Door Hinge", content.transform,
                    p + lv * new Vector3(-(HalfW - 0.1f), Floor, -0.65f), lv);
                GameObject pivot = ActSetBuildKit.Empty("Pivot", holder.transform);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Door", pivot.transform, new Vector3(0f, 1.15f, 0.65f), new Vector3(0.1f, 2.3f, 1.3f), doorM);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Window", pivot.transform, new Vector3(0.06f, 1.7f, 0.65f), new Vector3(0.14f, 0.32f, 0.34f), winDark);
                slot.door = pivot.transform;
                slots.Add(slot);
            }

            // ---- cell doors on both walls, some ajar
            int di = 0;
            for (float d = start + 4f; d < end - 2f; d += 7f, di++)
            {
                ActSetBuildKit.Pose(rail, d, out Vector3 p, out Vector3 f, out Vector3 u);
                Quaternion lv = ActSetBuildKit.Level(f);
                GameObject row = ActSetBuildKit.EmptyWorld("Cells " + di, content.transform, p, lv);
                for (int side = -1; side <= 1; side += 2)
                {
                    bool skip = false;
                    if (side < 0) foreach (float cs in chairSpots) if (Mathf.Abs(cs - d) < 3f) skip = true;
                    if (skip) continue;
                    float h1 = ActSetBuildKit.HashF(di * 2 + (side + 1) / 2, 41), h2 = ActSetBuildKit.HashF(di * 2 + (side + 1) / 2, 42);
                    GameObject hinge = ActSetBuildKit.Empty("Cell Hinge", row.transform, new Vector3(side * (HalfW - 0.06f), Floor, -0.65f), Quaternion.identity);
                    float ajar = h1 > 0.55f ? (6f + h2 * 20f) : 0f;
                    hinge.transform.localRotation = Quaternion.Euler(0f, -side * ajar, 0f);
                    ActSetBuildKit.Prim(PrimitiveType.Cube, "Door", hinge.transform, new Vector3(0f, 1.15f, 0.65f), new Vector3(0.1f, 2.3f, 1.3f), doorM);
                    ActSetBuildKit.Prim(PrimitiveType.Cube, "Window", hinge.transform, new Vector3(-side * 0.06f, 1.7f, 0.65f), new Vector3(0.14f, 0.32f, 0.34f), h2 > 0.65f ? winGlow : winDark);
                }
                cullObjs.Add(row);
                cullDists.Add(d);
            }

            // ---- ceiling fixtures (flicker at runtime); the last light is where the figure stands
            var fixR = new List<Renderer>();
            var fixD = new List<float>();
            var fixP = new List<Vector3>();
            for (float d = start + 6f; d < lastLight - 5f; d += 10f)
                AddFixture(rail, content.transform, d, fixM, fixR, fixD, fixP);
            AddFixture(rail, content.transform, lastLight, fixM, fixR, fixD, fixP);

            var pool = new Light[4];
            for (int i = 0; i < pool.Length; i++)
                pool[i] = ActSetBuildKit.PointLight("Corridor Light " + i, content.transform, Vector3.zero, Pale, 2.2f, 13f);

            // ---- the figure, standing in the last light, visible from the start of the quiet
            if (figEvents.Count > 0)
            {
                ActSetBuildKit.Pose(rail, lastLight, out Vector3 p, out Vector3 f, out Vector3 u);
                Quaternion lv = ActSetBuildKit.Level(f);
                GameObject fig = ActSetBuildKit.EmptyWorld("Asylum Figure", content.transform,
                    p + lv * new Vector3(1.5f, Floor, 0f), lv * Quaternion.Euler(0f, 180f, 0f));
                set.figureHead = BuildFigure(fig.transform, robe, hair, skin, redEye, dark, out set.figureArmL, out set.figureArmR);
                set.figure = fig;
                set.figureHint = figEvents[0].index;
                GameObject lampGo = ActSetBuildKit.EmptyWorld("Figure Lamp", content.transform, fig.transform.TransformPoint(new Vector3(0f, 2.3f, 1.3f)), Quaternion.identity);
                Light lamp = lampGo.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.color = new Color(0.9f, 0.95f, 0.9f);
                lamp.intensity = 3.5f;
                lamp.range = 6f;
                lamp.shadows = LightShadows.None;
                set.figureLamp = lamp;
            }

            set.cullObjs = cullObjs.ToArray();
            set.cullDists = cullDists.ToArray();
            set.fixtures = fixR.ToArray();
            set.fixtureDist = fixD.ToArray();
            set.fixturePos = fixP.ToArray();
            set.fixtureEmission = fixEmission;
            set.pool = pool;
            set.chairs = slots.ToArray();
            set.lastLightDist = lastLight;
            return root;
        }

        static void AddFixture(ICoasterRail rail, Transform parent, float d, Material m, List<Renderer> rs, List<float> ds, List<Vector3> ps)
        {
            ActSetBuildKit.Pose(rail, d, out Vector3 p, out Vector3 f, out Vector3 u);
            Quaternion lv = ActSetBuildKit.Level(f);
            Vector3 pos = p + lv * new Vector3(0f, Ceil - 0.06f, 0f);
            GameObject g = ActSetBuildKit.Prim(PrimitiveType.Cube, "Ceiling Light", parent, Vector3.zero, new Vector3(0.45f, 0.1f, 1.6f), m);
            g.transform.position = pos;
            g.transform.rotation = lv;
            rs.Add(g.GetComponent<Renderer>());
            ds.Add(d);
            ps.Add(pos);
        }

        static void BuildWheelchair(Transform c, Material m, Material dark, out Transform wheelL, out Transform wheelR)
        {
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Seat", c, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 0.07f, 0.5f), m);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Back", c, new Vector3(0f, 0.85f, -0.25f), new Vector3(0.5f, 0.7f, 0.05f), m);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Arm L", c, new Vector3(-0.27f, 0.7f, 0f), new Vector3(0.04f, 0.04f, 0.5f), dark);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Arm R", c, new Vector3(0.27f, 0.7f, 0f), new Vector3(0.04f, 0.04f, 0.5f), dark);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Handle L", c, new Vector3(-0.22f, 1.2f, -0.32f), new Vector3(0.04f, 0.04f, 0.14f), dark);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Handle R", c, new Vector3(0.22f, 1.2f, -0.32f), new Vector3(0.04f, 0.04f, 0.14f), dark);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Caster L", c, new Vector3(-0.25f, 0.1f, 0.3f), Vector3.one * 0.2f, dark);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Caster R", c, new Vector3(0.25f, 0.1f, 0.3f), Vector3.one * 0.2f, dark);
            wheelL = MakeWheel(c, -0.32f, m);
            wheelR = MakeWheel(c, 0.32f, m);
        }

        static Transform MakeWheel(Transform c, float x, Material m)
        {
            GameObject piv = ActSetBuildKit.Empty("Wheel Pivot", c, new Vector3(x, 0.33f, -0.05f), Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Tyre", piv.transform, Vector3.zero, new Vector3(0.66f, 0.025f, 0.66f), m, Quaternion.Euler(0f, 0f, 90f));
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Spoke A", piv.transform, Vector3.zero, new Vector3(0.04f, 0.62f, 0.03f), m);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Spoke B", piv.transform, Vector3.zero, new Vector3(0.04f, 0.03f, 0.62f), m);
            return piv.transform;
        }

        /// <summary>Pale figure, feet at the origin, facing +Z. Returns the head pivot.</summary>
        static Transform BuildFigure(Transform f, Material robe, Material hair, Material skin, Material redEye, Material dark,
                                     out Transform armL, out Transform armR)
        {
            ActSetBuildKit.Prim(PrimitiveType.Capsule, "Gown", f, new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 0.9f, 0.42f), robe);
            armL = BuildArm(f, -1f, skin);
            armR = BuildArm(f, 1f, skin);
            GameObject head = ActSetBuildKit.Empty("Head", f, new Vector3(0f, 1.95f, 0.02f), Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Skull", head.transform, Vector3.zero, new Vector3(0.36f, 0.44f, 0.38f), skin);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Hair Top", head.transform, new Vector3(0f, 0.06f, -0.05f), new Vector3(0.4f, 0.42f, 0.42f), hair);
            ActSetBuildKit.Prim(PrimitiveType.Capsule, "Hair Long", head.transform, new Vector3(0f, -0.35f, -0.12f), new Vector3(0.34f, 0.5f, 0.12f), hair);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Eye L", head.transform, new Vector3(-0.09f, 0.03f, 0.17f), new Vector3(0.09f, 0.06f, 0.05f), redEye);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Eye R", head.transform, new Vector3(0.09f, 0.03f, 0.17f), new Vector3(0.09f, 0.06f, 0.05f), redEye);
            // a wide black screaming mouth (reads at 0.7 m)
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Mouth", head.transform, new Vector3(0f, -0.1f, 0.16f), new Vector3(0.14f, 0.12f, 0.05f), dark);
            return head.transform;
        }

        /// <summary>Shoulder pivot with the arm hanging down along -Y and a splayed hand at the end. Rotating about X by -90 swings it forward.</summary>
        static Transform BuildArm(Transform f, float side, Material skin)
        {
            GameObject sh = ActSetBuildKit.Empty(side < 0 ? "Shoulder L" : "Shoulder R", f, new Vector3(side * 0.44f, 1.55f, 0f), Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Capsule, "Arm", sh.transform, new Vector3(0f, -0.35f, 0f), new Vector3(0.13f, 0.45f, 0.13f), skin);
            GameObject hand = ActSetBuildKit.Empty("Hand", sh.transform, new Vector3(0f, -0.82f, 0f), Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Palm", hand.transform, Vector3.zero, new Vector3(0.12f, 0.14f, 0.05f), skin);
            for (int i = 0; i < 4; i++)
                ActSetBuildKit.Prim(PrimitiveType.Capsule, "Finger", hand.transform, new Vector3(-0.045f + 0.03f * i, -0.11f, 0f), new Vector3(0.025f, 0.09f, 0.025f), skin,
                    Quaternion.Euler(0f, 0f, (i - 1.5f) * 10f));
            return sh.transform;
        }
    }
}
