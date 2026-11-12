using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>Builds the haunted mansion: facade and double doors, grand hall, portraits, chandeliers, stairs, candles, PaintingLunge.</summary>
    public static class MansionSetBuilder
    {
        const float HalfW = 7.5f;      // hall half width
        const float FloorY = -1.4f;    // floor below the rail
        const float CeilY = 8f;        // ceiling above the rail
        const float Seg = 10f;

        static readonly Color Orange = new Color(1f, 0.48f, 0.09f);
        static readonly Color Bone = new Color(0.93f, 0.9f, 0.82f);
        static readonly Color Yellow = new Color(1f, 0.78f, 0.25f);
        static readonly Color Blood = new Color(0.8f, 0.04f, 0.06f);

        static Material _floorA, _floorB, _carpet, _wall, _ceil, _stone, _grass, _wood, _gold, _canvas, _faceA, _faceB, _eyeW,
                        _pupil, _flame, _candle, _chain, _window, _stairs;

        /// <summary>
        /// Shell segment length at distance d: the normal 10 m on the level, 4 m wherever the rail is pitched. The act opens with a 40 m drop
        /// through the front doors; 10 m flat boxes stepped down that slope like a staircase, so the shell must follow the rail's pitch.
        /// </summary>
        static float SegLength(ICoasterRail rail, float d)
        {
            ActSetBuildKit.Pose(rail, d, out _, out Vector3 f0, out _);
            ActSetBuildKit.Pose(rail, d + Seg, out _, out Vector3 f1, out _);
            return Mathf.Abs(f0.y) > 0.03f || Mathf.Abs(f1.y) > 0.03f ? 4f : Seg;
        }

        /// <summary>Rotation for a shell segment: pitched with the rail where it slopes (no roll), yaw-only on the level.</summary>
        static Quaternion SegRot(Vector3 f)
        {
            return Mathf.Abs(f.y) > 0.03f ? Quaternion.LookRotation(f, Vector3.up) : ActSetBuildKit.Level(f);
        }

        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            ActSetBuildKit.Span(cfg, rail, ActId.Mansion, out float start, out float end);
            float L = end - start;
            float d0 = start + Mathf.Min(25f, L * 0.25f);   // the front doors
            MakeMaterials();

            GameObject root = ActSetBuildKit.Empty("Mansion Set", parent);
            var set = root.AddComponent<MansionSet>();
            set.startDist = start;
            set.endDist = end;
            set.doorDist = d0;
            GameObject content = ActSetBuildKit.Empty("Content", root.transform);
            set.content = content;

            var flames = new List<Transform>();
            var lights = new List<Light>();
            var lightDist = new List<float>();
            var eyePiv = new List<Transform>();
            var eyeRoot = new List<Transform>();
            var cullObjs = new List<GameObject>();
            var cullDists = new List<float>();

            // ---- exterior ground and lantern posts up to the doors
            float outLen;
            int outIndex = 0;
            for (float d = start; d < d0; d += outLen, outIndex++)
            {
                outLen = SegLength(rail, d);
                float outCover = outLen < Seg ? outLen * 1.3f + 0.4f : outLen + 0.4f; // sloped joints overlap so no crack opens between tilted boxes
                ActSetBuildKit.Pose(rail, Mathf.Min(d + outLen * 0.5f, d0), out Vector3 p, out Vector3 f, out Vector3 u);
                GameObject s = ActSetBuildKit.EmptyWorld("Outside " + (int)d, content.transform, p, SegRot(f));
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Ground", s.transform, new Vector3(0f, FloorY - 0.2f, 0f), new Vector3(44f, 0.4f, outCover), _grass);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Path", s.transform, new Vector3(0f, FloorY + 0.01f, 0f), new Vector3(7f, 0.05f, outCover), _stone);
                if (outIndex % (outLen < Seg ? 3 : 2) == 0)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Post", s.transform, new Vector3(side * 6f, FloorY + 1.2f, 0f), new Vector3(0.25f, 1.2f, 0.25f), _chain);
                        GameObject fl = ActSetBuildKit.Prim(PrimitiveType.Sphere, "Lantern", s.transform, new Vector3(side * 6f, FloorY + 2.6f, 0f), new Vector3(0.5f, 0.6f, 0.5f), _flame);
                        flames.Add(fl.transform);
                    }
                cullObjs.Add(s); cullDists.Add(d);
            }

            // ---- facade with the double doors
            ActSetBuildKit.Pose(rail, d0, out Vector3 p0, out Vector3 f0, out Vector3 u0);
            Quaternion lv0 = ActSetBuildKit.Level(f0);
            GameObject entrance = ActSetBuildKit.EmptyWorld("Entrance", content.transform, p0, lv0);
            const float doorH = 6.4f, wallTop = 26f;
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Facade L", entrance.transform, new Vector3(-(3.6f + 20f), FloorY + wallTop * 0.5f, 0f), new Vector3(40f, wallTop, 1.4f), _stone, null, true);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Facade R", entrance.transform, new Vector3(3.6f + 20f, FloorY + wallTop * 0.5f, 0f), new Vector3(40f, wallTop, 1.4f), _stone, null, true);
            float lintelH = wallTop - doorH;
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Lintel", entrance.transform, new Vector3(0f, FloorY + doorH + lintelH * 0.5f, 0f), new Vector3(7.4f, lintelH, 1.4f), _stone, null, true);
            for (int i = 0; i < 12; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float x = side * (7f + (i / 2) * 5.5f);
                float y = FloorY + 7f + ((i / 2) % 2) * 6f + ActSetBuildKit.HashF(i, 21) * 3f;
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Window", entrance.transform, new Vector3(x, y, -0.78f), new Vector3(2f, 3.2f, 0.12f), _window);
            }
            ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Porch Column L", entrance.transform, new Vector3(-4.6f, FloorY + 4f, -2.2f), new Vector3(0.8f, 4f, 0.8f), _stone, null, true);
            ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Porch Column R", entrance.transform, new Vector3(4.6f, FloorY + 4f, -2.2f), new Vector3(0.8f, 4f, 0.8f), _stone, null, true);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Porch Roof", entrance.transform, new Vector3(0f, FloorY + 8.2f, -1.6f), new Vector3(11f, 0.5f, 3.4f), _stone, null, true);

            set.doorL = MakeDoor(entrance.transform, -1f, doorH);
            set.doorR = MakeDoor(entrance.transform, 1f, doorH);

            // ---- hall shell
            int segIndex = 0;
            float hallLen;
            for (float d = d0; d < end; d += hallLen, segIndex++)
            {
                hallLen = SegLength(rail, d);
                float cover = hallLen < Seg ? hallLen * 1.3f + 0.3f : hallLen + 0.3f;
                ActSetBuildKit.Pose(rail, Mathf.Min(d + hallLen * 0.5f, end), out Vector3 p, out Vector3 f, out Vector3 u);
                GameObject s = ActSetBuildKit.EmptyWorld("Hall " + segIndex, content.transform, p, SegRot(f));
                float wallH = CeilY - FloorY;
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Floor", s.transform, new Vector3(0f, FloorY - 0.2f, 0f), new Vector3(HalfW * 2f + 1f, 0.4f, cover), segIndex % 2 == 0 ? _floorA : _floorB);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Carpet", s.transform, new Vector3(0f, FloorY + 0.02f, 0f), new Vector3(3.6f, 0.04f, cover), _carpet);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Wall L", s.transform, new Vector3(-(HalfW + 0.25f), FloorY + wallH * 0.5f, 0f), new Vector3(0.5f, wallH, cover), _wall);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Wall R", s.transform, new Vector3(HalfW + 0.25f, FloorY + wallH * 0.5f, 0f), new Vector3(0.5f, wallH, cover), _wall);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Ceiling", s.transform, new Vector3(0f, CeilY + 0.25f, 0f), new Vector3(HalfW * 2f + 1f, 0.5f, cover), _ceil);
                if (segIndex % (hallLen < Seg ? 3 : 2) == 0)
                    for (int side = -1; side <= 1; side += 2)
                        ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Column", s.transform, new Vector3(side * (HalfW - 0.7f), FloorY + wallH * 0.5f, 0f), new Vector3(0.9f, wallH * 0.5f, 0.9f), _candle);
                cullObjs.Add(s); cullDists.Add(d + hallLen * 0.5f);
            }

            // doorway closing the hall: the cart leaves through it into the next act
            {
                ActSetBuildKit.Pose(rail, end, out Vector3 p, out Vector3 f, out Vector3 u);
                GameObject w = ActSetBuildKit.EmptyWorld("Hall End", content.transform, p, ActSetBuildKit.Level(f));
                float wallH = CeilY - FloorY;
                ActSetBuildKit.Prim(PrimitiveType.Cube, "End L", w.transform, new Vector3(-(3.5f + 5f), FloorY + wallH * 0.5f, 0.3f), new Vector3(10f, wallH, 0.6f), _wall);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "End R", w.transform, new Vector3(3.5f + 5f, FloorY + wallH * 0.5f, 0.3f), new Vector3(10f, wallH, 0.6f), _wall);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "End Top", w.transform, new Vector3(0f, FloorY + 6.5f + (wallH - 6.5f) * 0.5f, 0.3f), new Vector3(7f, wallH - 6.5f, 0.6f), _wall);
            }

            // ---- candelabras (flickering flames)
            int candleIndex = 0;
            for (float d = d0 + 4.5f; d < end - 3f; d += 9f, candleIndex++)
            {
                ActSetBuildKit.Pose(rail, d, out Vector3 p, out Vector3 f, out Vector3 u);
                GameObject s = ActSetBuildKit.EmptyWorld("Candles " + candleIndex, content.transform, p, ActSetBuildKit.Level(f));
                float side = (candleIndex % 2 == 0) ? -1f : 1f;
                float x = side * (HalfW - 1.6f);
                ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Candle", s.transform, new Vector3(x, FloorY + 0.7f, 0f), new Vector3(0.16f, 0.7f, 0.16f), _candle);
                GameObject fl = ActSetBuildKit.Prim(PrimitiveType.Sphere, "Flame", s.transform, new Vector3(x, FloorY + 1.6f, 0f), new Vector3(0.22f, 0.34f, 0.22f), _flame);
                flames.Add(fl.transform);
                cullObjs.Add(s); cullDists.Add(d);
            }

            // ---- portraits whose eyes follow the viewer
            float pSpacing = Mathf.Max(14f, (end - d0) / 18f);
            int pIndex = 0;
            for (float d = d0 + 8f; d < end - 6f; d += pSpacing, pIndex++)
            {
                ActSetBuildKit.Pose(rail, d, out Vector3 p, out Vector3 f, out Vector3 u);
                Quaternion lv = ActSetBuildKit.Level(f);
                for (int side = -1; side <= 1; side += 2)
                {
                    int id = pIndex * 2 + (side + 1) / 2;
                    Vector3 wp = p + lv * new Vector3(side * (HalfW - 0.02f), FloorY + 4f, 0f);
                    Quaternion wr = lv * Quaternion.Euler(0f, -side * 90f, 0f); // local +Z points into the hall
                    GameObject pr = ActSetBuildKit.EmptyWorld("Portrait " + id, content.transform, wp, wr);
                    BuildPortrait(pr.transform, id, eyePiv, eyeRoot);
                    cullObjs.Add(pr); cullDists.Add(d);
                }
            }

            // ---- chandeliers (decor) with a light each
            for (float d = d0 + 15f; d < end - 10f; d += 30f)
            {
                ActSetBuildKit.Pose(rail, d, out Vector3 p, out Vector3 f, out Vector3 u);
                Quaternion lv = ActSetBuildKit.Level(f);
                GameObject c = BuildChandelier("Chandelier", content.transform, p + lv * new Vector3(0f, CeilY - 2.4f, 0f), lv, flames, out _);
                Light l = ActSetBuildKit.PointLight("Chandelier Light", c.transform, new Vector3(0f, -0.3f, 0f), new Color(1f, 0.62f, 0.28f), 2.2f, 16f);
                lights.Add(l);
                lightDist.Add(d);
                cullObjs.Add(c); cullDists.Add(d);
            }

            // ---- grand staircases (both sides, rising forward)
            {
                float dS = d0 + (end - d0) * 0.45f;
                ActSetBuildKit.Pose(rail, dS, out Vector3 p, out Vector3 f, out Vector3 u);
                GameObject s = ActSetBuildKit.EmptyWorld("Staircases", content.transform, p, ActSetBuildKit.Level(f));
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * (HalfW - 1.9f);
                    for (int i = 0; i < 18; i++)
                    {
                        float h = (i + 1) * 0.3f;
                        ActSetBuildKit.Prim(PrimitiveType.Cube, "Step", s.transform, new Vector3(x, FloorY + h * 0.5f, i * 0.6f), new Vector3(3.2f, h, 0.62f), _stairs);
                    }
                    ActSetBuildKit.Prim(PrimitiveType.Cube, "Landing", s.transform, new Vector3(x, FloorY + 2.7f, 10.8f + 2f), new Vector3(3.2f, 5.4f, 4f), _stairs);
                    float xi = side * (HalfW - 3.7f);
                    Vector3 a = new Vector3(xi, FloorY + 1.1f, 0f), b = new Vector3(xi, FloorY + 6.5f, 10.8f);
                    Vector3 dir = b - a;
                    ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Banister", s.transform, (a + b) * 0.5f, new Vector3(0.1f, dir.magnitude * 0.5f, 0.1f), _gold,
                        Quaternion.FromToRotation(Vector3.up, dir.normalized));
                }
            }

            // ---- PaintingLunge: the big portrait, plus the chandelier that drops beside the cart
            var evs = ActSetBuildKit.EventsOf(cfg, ScareKind.PaintingLunge);
            if (evs.Count > 0)
            {
                float dE = evs[0].ev.distance;
                set.lungeHint = evs[0].index;

                ActSetBuildKit.Pose(rail, Mathf.Min(dE + 2f, rail.Length), out Vector3 p, out Vector3 f, out Vector3 u);
                Quaternion lv = ActSetBuildKit.Level(f);
                GameObject lunge = ActSetBuildKit.EmptyWorld("Lunge Painting", content.transform,
                    p + lv * new Vector3(-(HalfW - 0.1f), FloorY + 3.4f, 0f), lv * Quaternion.Euler(0f, 90f, 0f));
                BuildLungePainting(lunge.transform, set);
                set.lungeObj = lunge;

                ActSetBuildKit.Pose(rail, Mathf.Min(dE + 7f, rail.Length), out Vector3 pc, out Vector3 fc, out Vector3 uc);
                Quaternion lvc = ActSetBuildKit.Level(fc);
                GameObject chand = BuildChandelier("Dropping Chandelier", content.transform, pc + lvc * new Vector3(2.7f, CeilY - 2.4f, 0f), lvc, null, out GameObject fl);
                Light cl = ActSetBuildKit.PointLight("Drop Light", chand.transform, new Vector3(0f, -0.3f, 0f), new Color(1f, 0.62f, 0.28f), 2.5f, 14f);
                set.chandelier = chand.transform;
                set.chandLight = cl;
                set.chandFlames = fl;
                set.chandFloorY = pc.y + FloorY + 0.6f;
                if (dE < d0 + 12f) Debug.LogWarning("MansionSetBuilder: PaintingLunge is within 12 m of the front doors; check the schedule.");
            }

            set.cullObjs = cullObjs.ToArray();
            set.cullDists = cullDists.ToArray();
            set.eyePivots = eyePiv.ToArray();
            set.eyeRoots = eyeRoot.ToArray();
            set.flames = flames.ToArray();
            set.lights = lights.ToArray();
            set.lightDist = lightDist.ToArray();
            return root;
        }

        static void MakeMaterials()
        {
            _floorA = ActSetBuildKit.M("Mansion_FloorA", new Color(0.12f, 0.05f, 0.05f), default, 0.6f);
            _floorB = ActSetBuildKit.M("Mansion_FloorB", new Color(0.22f, 0.1f, 0.08f), default, 0.6f);
            _carpet = ActSetBuildKit.M("Mansion_Carpet", new Color(0.35f, 0.14f, 0.05f), ActSetBuildKit.Hdr(Orange, 0.06f), 0.2f);
            _wall = ActSetBuildKit.M("Mansion_Wall", new Color(0.14f, 0.1f, 0.2f), ActSetBuildKit.Hdr(new Color(0.3f, 0.2f, 0.5f), 0.08f), 0.2f);
            _ceil = ActSetBuildKit.M("Mansion_Ceiling", new Color(0.08f, 0.06f, 0.12f), default, 0.1f);
            _stone = ActSetBuildKit.M("Mansion_Stone", new Color(0.2f, 0.18f, 0.26f), ActSetBuildKit.Hdr(new Color(0.3f, 0.25f, 0.5f), 0.06f), 0.2f);
            _grass = ActSetBuildKit.M("Mansion_Grass", new Color(0.05f, 0.1f, 0.07f), default, 0.1f);
            _wood = ActSetBuildKit.M("Mansion_Door", new Color(0.2f, 0.1f, 0.06f), default, 0.4f);
            _gold = ActSetBuildKit.M("Mansion_Gold", new Color(0.65f, 0.45f, 0.14f), ActSetBuildKit.Hdr(Yellow, 0.25f), 0.8f, 0.8f);
            _canvas = ActSetBuildKit.M("Mansion_Canvas", new Color(0.07f, 0.05f, 0.08f), default, 0.1f);
            _faceA = ActSetBuildKit.M("Mansion_FaceBone", Bone * 0.8f, ActSetBuildKit.Hdr(Bone, 0.25f), 0.2f);
            _faceB = ActSetBuildKit.M("Mansion_FaceSick", new Color(0.55f, 0.75f, 0.45f), ActSetBuildKit.Hdr(new Color(0.49f, 1f, 0.31f), 0.22f), 0.2f);
            _eyeW = ActSetBuildKit.M("Mansion_EyeWhite", Bone, ActSetBuildKit.Hdr(Bone, 0.55f), 0.5f);
            _pupil = ActSetBuildKit.M("Mansion_Pupil", new Color(0.02f, 0.02f, 0.03f), default, 0.7f);
            _flame = ActSetBuildKit.M("Mansion_Flame", Yellow, ActSetBuildKit.Hdr(Yellow, 2.2f), 0.1f);
            _candle = ActSetBuildKit.M("Mansion_Candle", Bone * 0.8f, ActSetBuildKit.Hdr(Bone, 0.12f), 0.3f);
            _chain = ActSetBuildKit.M("Mansion_Iron", new Color(0.09f, 0.09f, 0.11f), default, 0.6f, 0.7f);
            _window = ActSetBuildKit.M("Mansion_Window", Yellow * 0.5f, ActSetBuildKit.Hdr(Orange, 1.4f), 0.2f);
            _stairs = ActSetBuildKit.M("Mansion_Stairs", new Color(0.18f, 0.08f, 0.07f), default, 0.4f);
        }

        static Transform MakeDoor(Transform entrance, float side, float h)
        {
            // hinge at the outer edge; the slab extends toward the centre
            GameObject pivot = ActSetBuildKit.Empty(side < 0 ? "Door L Hinge" : "Door R Hinge", entrance, new Vector3(side * 3.45f, FloorY, 0.1f), Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Door", pivot.transform, new Vector3(-side * 1.75f, h * 0.5f, 0f), new Vector3(3.5f, h, 0.28f), _wood, null, true);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Door Glow", pivot.transform, new Vector3(-side * 1.75f, h * 0.62f, -0.16f), new Vector3(0.5f, 2.2f, 0.06f), _window);
            return pivot.transform;
        }

        static void BuildPortrait(Transform pr, int id, List<Transform> pivots, List<Transform> roots)
        {
            Material face = ActSetBuildKit.HashF(id, 31) > 0.7f ? _faceB : _faceA;
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Frame", pr, Vector3.zero, new Vector3(2.7f, 3.7f, 0.14f), _gold);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Canvas", pr, new Vector3(0f, 0f, 0.05f), new Vector3(2.3f, 3.3f, 0.12f), _canvas);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Face", pr, new Vector3(0f, 0.05f, 0.12f), new Vector3(1.3f, 1.7f, 0.25f), face);
            for (int e = -1; e <= 1; e += 2)
            {
                GameObject piv = ActSetBuildKit.Empty("Eye Pivot", pr, new Vector3(e * 0.32f, 0.3f, 0.22f), Quaternion.identity);
                ActSetBuildKit.Prim(PrimitiveType.Sphere, "Eyeball", piv.transform, Vector3.zero, Vector3.one * 0.3f, _eyeW);
                ActSetBuildKit.Prim(PrimitiveType.Sphere, "Pupil", piv.transform, new Vector3(0f, 0f, 0.12f), Vector3.one * 0.13f, _pupil);
                pivots.Add(piv.transform);
                roots.Add(pr);
            }
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Mouth", pr, new Vector3(0f, -0.5f, 0.22f), new Vector3(0.55f, 0.07f, 0.05f), _pupil);
        }

        static void BuildLungePainting(Transform lr, MansionSet set)
        {
            Material frame = ActSetBuildKit.M("Mansion_LungeFrame", new Color(0.65f, 0.45f, 0.14f), ActSetBuildKit.Hdr(Orange, 0.7f), 0.5f);
            Material face = ActSetBuildKit.M("Mansion_LungeFace", Bone * 0.85f, ActSetBuildKit.Hdr(Bone, 1.1f), 0.2f);
            Material socket = ActSetBuildKit.M("Mansion_LungeSocket", new Color(0.01f, 0.01f, 0.02f), default, 0.2f);
            Material red = ActSetBuildKit.M("Mansion_LungeRed", Blood, ActSetBuildKit.Hdr(Blood, 2.4f), 0.2f);
            Material tooth = ActSetBuildKit.M("Mansion_LungeTooth", Bone, ActSetBuildKit.Hdr(Bone, 0.9f), 0.3f);
            Material arm = ActSetBuildKit.M("Mansion_LungeArm", new Color(0.55f, 0.57f, 0.5f), ActSetBuildKit.Hdr(Bone, 0.7f), 0.2f);

            ActSetBuildKit.Prim(PrimitiveType.Cube, "Frame", lr, Vector3.zero, new Vector3(4.6f, 6.0f, 0.2f), frame);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Canvas", lr, new Vector3(0f, 0f, 0.06f), new Vector3(4.0f, 5.4f, 0.2f), _canvas);

            // the face is its own group so it can leave the frame: origin at the face centre, local +Z = out of the canvas
            GameObject fg = ActSetBuildKit.Empty("Lunge Face", lr, new Vector3(0f, 0.2f, 0.2f), Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Face", fg.transform, Vector3.zero, new Vector3(2.7f, 3.5f, 0.5f), face);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Brow", fg.transform, new Vector3(0f, 1.0f, 0.2f), new Vector3(2.3f, 0.5f, 0.4f), socket);
            set.lungeEyeL = BuildLungeEye(fg.transform, new Vector3(-0.6f, 0.55f, 0.22f), socket, red).transform;
            set.lungeEyeR = BuildLungeEye(fg.transform, new Vector3(0.6f, 0.55f, 0.22f), socket, red).transform;

            GameObject mouth = ActSetBuildKit.Empty("Mouth", fg.transform, new Vector3(0f, -0.95f, 0.22f), Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Maw", mouth.transform, Vector3.zero, new Vector3(1.5f, 0.7f, 0.1f), red);
            for (int i = 0; i < 6; i++)
            {
                float x = -0.6f + 0.24f * i;
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Tooth", mouth.transform, new Vector3(x, 0.32f, 0.07f), new Vector3(0.14f, 0.2f, 0.08f), tooth, Quaternion.Euler(0f, 0f, 45f));
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Tooth", mouth.transform, new Vector3(x, -0.32f, 0.07f), new Vector3(0.14f, 0.2f, 0.08f), tooth, Quaternion.Euler(0f, 0f, 45f));
            }
            set.lungeFace = fg.transform;
            set.lungeMouth = mouth.transform;

            // the grabbing arm (unit cylinder, stretched at runtime) and clawed hand
            set.lungeArm = ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Grab Arm", lr, Vector3.zero, Vector3.one, arm).transform;
            GameObject claw = ActSetBuildKit.Empty("Grab Claw", lr);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Palm", claw.transform, Vector3.zero, new Vector3(0.45f, 0.18f, 0.5f), arm);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.18f + 0.12f * i;
                ActSetBuildKit.Prim(PrimitiveType.Capsule, "Finger", claw.transform, new Vector3(x, 0f, 0.4f), new Vector3(0.06f, 0.3f, 0.06f), arm, Quaternion.Euler(80f, 0f, (i - 1.5f) * 8f));
            }
            set.lungeClaw = claw.transform;
            set.lungeArm.gameObject.SetActive(false);
            claw.SetActive(false);

            set.lungeLight = ActSetBuildKit.PointLight("Lunge Light", lr, new Vector3(0f, 0f, 1.5f), new Color(1f, 0.55f, 0.2f), 3f, 6f);
        }

        static GameObject BuildLungeEye(Transform lr, Vector3 pos, Material socket, Material red)
        {
            GameObject eye = ActSetBuildKit.Empty("Eye", lr, pos, Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Socket", eye.transform, Vector3.zero, new Vector3(0.8f, 0.6f, 0.2f), socket);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Pupil", eye.transform, new Vector3(0f, 0f, 0.1f), new Vector3(0.22f, 0.22f, 0.1f), red);
            return eye;
        }

        /// <summary>Ring chandelier, origin at the ring centre. Flames are returned in a child group so they can be hidden.</summary>
        static GameObject BuildChandelier(string name, Transform parent, Vector3 worldPos, Quaternion rot, List<Transform> flameList, out GameObject flameGroup)
        {
            GameObject c = ActSetBuildKit.EmptyWorld(name, parent, worldPos, rot);
            ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Chain", c.transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.07f, 1.7f, 0.07f), _chain);
            ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Ring", c.transform, Vector3.zero, new Vector3(2.8f, 0.08f, 2.8f), _gold);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Boss", c.transform, new Vector3(0f, -0.3f, 0f), new Vector3(0.6f, 0.8f, 0.6f), _gold);
            flameGroup = ActSetBuildKit.Empty("Flames", c.transform);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 1.35f, 0f, Mathf.Sin(a) * 1.35f);
                ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Candle", c.transform, p + Vector3.up * 0.25f, new Vector3(0.12f, 0.25f, 0.12f), _candle);
                GameObject fl = ActSetBuildKit.Prim(PrimitiveType.Sphere, "Flame", flameGroup.transform, p + Vector3.up * 0.65f, new Vector3(0.2f, 0.3f, 0.2f), _flame);
                if (flameList != null) flameList.Add(fl.transform);
            }
            return c;
        }
    }
}
