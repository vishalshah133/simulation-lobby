using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Builds the pooled micro jump scare entities (8 kinds, one each, hidden, with one point light each) under a single
    /// <see cref="MicroScareSet"/>. Primitives plus a few procedural meshes; no model imports. Each entity is well under 150 renderers.
    /// </summary>
    public static class MicroScareBuilder
    {
        // ---------------------------------------------------------------- materials (shared look: dark bodies, bright faces)

        static Material Skin => Mat("MicroSkin", new Color(0.80f, 0.82f, 0.76f), CoasterPalette.Glow(CoasterPalette.Bone, 0.85f), 0.25f);
        static Material Bone => Mat("MicroBone", CoasterPalette.Bone * 0.9f, CoasterPalette.Glow(CoasterPalette.Bone, 0.9f), 0.25f);
        static Material Black => Mat("MicroBlack", new Color(0.01f, 0.01f, 0.02f), default, 0.15f);
        static Material Rot => Mat("MicroRot", new Color(0.36f, 0.46f, 0.28f), CoasterPalette.Glow(CoasterPalette.ToxicGreen, 0.22f), 0.2f);
        static Material Claw => Mat("MicroClaw", new Color(0.08f, 0.06f, 0.05f), CoasterPalette.Glow(CoasterPalette.BloodRed, 0.3f), 0.3f);
        static Material EyeRed => Mat("MicroEyeRed", CoasterPalette.BloodRed, CoasterPalette.Glow(new Color(1f, 0.15f, 0.1f), 4.5f), 0.3f);
        static Material EyeGreen => Mat("MicroEyeGreen", CoasterPalette.ToxicGreen * 0.5f, CoasterPalette.Glow(CoasterPalette.ToxicGreen, 3.5f), 0.3f);
        static Material EyeWhite => Mat("MicroEyeWhite", new Color(0.8f, 1f, 0.9f), CoasterPalette.Glow(new Color(0.8f, 1f, 0.9f), 5f), 0.3f);
        static Material SpiderBody => Mat("MicroSpiderBody", new Color(0.09f, 0.05f, 0.12f), CoasterPalette.Glow(new Color(0.55f, 0.25f, 0.8f), 0.5f), 0.35f);
        static Material SpiderLeg => Mat("MicroSpiderLeg", new Color(0.11f, 0.07f, 0.08f), CoasterPalette.Glow(CoasterPalette.Pumpkin, 0.28f), 0.3f);
        static Material SpiderMark => Mat("MicroSpiderMark", CoasterPalette.BloodRed, CoasterPalette.Glow(CoasterPalette.BloodRed, 1.6f), 0.3f);
        static Material Thread => Mat("MicroThread", new Color(0.85f, 0.9f, 1f), CoasterPalette.Glow(new Color(0.8f, 0.88f, 1f), 0.7f), 0.2f);
        static Material Suit => Mat("MicroSuit", new Color(0.07f, 0.07f, 0.12f), CoasterPalette.Glow(new Color(0.3f, 0.3f, 0.6f), 0.4f), 0.2f);
        static Material Tie => Mat("MicroTie", CoasterPalette.BloodRed, CoasterPalette.Glow(CoasterPalette.BloodRed, 0.9f), 0.3f);
        static Material Mouth => Mat("MicroMouth", new Color(0.25f, 0.02f, 0.04f), CoasterPalette.Glow(CoasterPalette.BloodRed, 0.35f), 0.4f);
        static Material Wood => Mat("MicroWood", new Color(0.24f, 0.13f, 0.09f), CoasterPalette.Glow(new Color(0.8f, 0.4f, 0.3f), 0.35f), 0.25f);
        static Material Glow => Mat("MicroCoffinGlow", CoasterPalette.ToxicGreen * 0.4f, CoasterPalette.Glow(CoasterPalette.ToxicGreen, 2.2f), 0.2f);
        static Material Shadow => Mat("MicroShadow", new Color(0.012f, 0.012f, 0.025f), CoasterPalette.Glow(new Color(0.25f, 0.3f, 0.6f), 0.07f), 0.1f);
        static Material Glass => Mat("MicroGlass", new Color(0.06f, 0.1f, 0.18f, 0.88f), CoasterPalette.Glow(new Color(0.2f, 0.35f, 0.7f), 0.06f), 1f, 0.6f, true);
        static Material MirrorFrame => Mat("MicroMirrorFrame", new Color(0.5f, 0.38f, 0.16f), CoasterPalette.Glow(CoasterPalette.Pumpkin, 0.25f), 0.5f, 0.6f);
        static Material Crack => Mat("MicroCrack", CoasterPalette.Bone, CoasterPalette.Glow(CoasterPalette.Bone, 1.6f), 0.2f);
        static Material Shard => Mat("MicroShard", new Color(0.8f, 0.9f, 1f), CoasterPalette.Glow(new Color(0.8f, 0.9f, 1f), 1.3f), 0.9f);
        static Material BatMat => Mat("MicroBat", new Color(0.2f, 0.1f, 0.3f), CoasterPalette.Glow(new Color(0.55f, 0.28f, 0.9f), 1.0f), 0.2f);
        static Material HoleDark => Mat("MicroHoleDark", new Color(0.005f, 0.005f, 0.01f), default, 0.1f);
        static Material HoleRing => Mat("MicroHoleRing", CoasterPalette.ToxicGreen * 0.4f, CoasterPalette.Glow(CoasterPalette.ToxicGreen, 2.4f), 0.2f);

        // realism pass
        static Material Ichor => Mat("MicroIchor", new Color(0.015f, 0.012f, 0.02f), CoasterPalette.Glow(new Color(0.2f, 0.5f, 0.3f), 0.15f), 1f);
        static Material BloodFresh => Mat("MicroBloodFresh", new Color(0.55f, 0.015f, 0.03f), CoasterPalette.Glow(CoasterPalette.BloodRed, 0.8f), 1f);
        static Material Sinew => Mat("MicroSinew", new Color(0.45f, 0.07f, 0.09f), CoasterPalette.Glow(new Color(0.8f, 0.1f, 0.12f), 0.3f), 0.92f);
        static Material Hair => Mat("MicroHair", new Color(0.02f, 0.02f, 0.03f), default, 0.3f);

        static Material Mat(string key, Color c, Color emission, float smooth, float metal = 0f, bool transparent = false)
        {
            return CoasterMaterials.Get(key, c, emission, smooth, metal, transparent);
        }

        // ---------------------------------------------------------------- entry

        /// <summary>Creates the "MicroScares" object under <paramref name="parent"/> with all eight pooled entities (hidden).</summary>
        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            var go = new GameObject("MicroScares");
            go.transform.SetParent(parent, false);
            var set = go.AddComponent<MicroScareSet>();
            set.stingSeconds = cfg != null ? cfg.stingSeconds : 0.6f;

            Mesh finger = MicroScareMeshes.Finger();
            Mesh coffinBody = MicroScareMeshes.CoffinBody();
            Mesh coffinLid = MicroScareMeshes.CoffinLid();
            Mesh shard = MicroScareMeshes.Shard();
            Mesh bat = MicroScareMeshes.Bat();

            set.entities = new[]
            {
                Spider(go.transform),
                Window(go.transform),
                Skeleton(go.transform),
                ClawHand(go.transform, finger),
                Bats(go.transform, bat),
                Coffin(go.transform, coffinBody, coffinLid),
                ShadowRush(go.transform),
                Mirror(go.transform, shard)
            };
            return go;
        }

        // ---------------------------------------------------------------- shared pieces

        static MicroEntity NewEntity(Transform parent, ScareKind kind, Color lightColor, float lightMax, float range, float duration, out Transform root)
        {
            GameObject r = ActSetBuildKit.Empty("Micro_" + kind, parent);
            Light l = ActSetBuildKit.PointLight("Light", r.transform, Vector3.zero, lightColor, 0f, range);
            l.enabled = false;
            r.SetActive(false);
            root = r.transform;
            return new MicroEntity { kind = kind, root = r, light = l, duration = duration, lightMax = lightMax };
        }

        static GameObject P(PrimitiveType t, string n, Transform parent, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
        {
            return ActSetBuildKit.Prim(t, n, parent, pos, scale, m, rot);
        }

        static Transform Pivot(string n, Transform parent, Vector3 pos)
        {
            return ActSetBuildKit.Empty(n, parent, pos, Quaternion.identity).transform;
        }

        /// <summary>A thin wet streak hanging from pos with a bead at the end (blood, ichor, venom).</summary>
        static void Drip(Transform parent, string n, Vector3 pos, float length, float width, Material m)
        {
            P(PrimitiveType.Cylinder, n, parent, pos + Vector3.down * (length * 0.5f), new Vector3(width, length * 0.5f, width), m);
            P(PrimitiveType.Sphere, n + "Bead", parent, pos + Vector3.down * (length + width * 0.5f), new Vector3(width * 1.8f, width * 2.4f, width * 1.8f), m);
        }

        /// <summary>A cylinder along local +x of the given length starting at the origin.</summary>
        static void Seg(Transform parent, string n, float length, float radius, Material m)
        {
            P(PrimitiveType.Cylinder, n, parent, new Vector3(length * 0.5f, 0f, 0f), new Vector3(radius * 2f, length * 0.5f, radius * 2f), m,
                Quaternion.Euler(0f, 0f, 90f));
        }

        /// <summary>A cylinder hanging down local -y from the origin.</summary>
        static void Limb(Transform parent, string n, float y0, float y1, float radius, Material m)
        {
            float len = Mathf.Abs(y1 - y0);
            P(PrimitiveType.Cylinder, n, parent, new Vector3(0f, (y0 + y1) * 0.5f, 0f), new Vector3(radius * 2f, len * 0.5f, radius * 2f), m);
        }

        /// <summary>A face looking down +z: skull, black sockets, an open black mouth and teeth. Roughly 0.3 x 0.4 m.</summary>
        static Transform Face(Transform parent, string name, Material skin, Material eyePin)
        {
            Transform f = Pivot(name, parent, Vector3.zero);
            P(PrimitiveType.Sphere, "Skull", f, Vector3.zero, new Vector3(0.30f, 0.40f, 0.28f), skin);
            P(PrimitiveType.Sphere, "SocketL", f, new Vector3(-0.07f, 0.06f, 0.115f), new Vector3(0.10f, 0.12f, 0.06f), Black);
            P(PrimitiveType.Sphere, "SocketR", f, new Vector3(0.07f, 0.06f, 0.115f), new Vector3(0.10f, 0.12f, 0.06f), Black);
            if (eyePin != null)
            {
                P(PrimitiveType.Sphere, "PinL", f, new Vector3(-0.07f, 0.055f, 0.145f), Vector3.one * 0.028f, eyePin);
                P(PrimitiveType.Sphere, "PinR", f, new Vector3(0.07f, 0.055f, 0.145f), Vector3.one * 0.028f, eyePin);
            }

            P(PrimitiveType.Sphere, "Nose", f, new Vector3(0f, -0.03f, 0.135f), new Vector3(0.035f, 0.05f, 0.03f), Black);
            P(PrimitiveType.Sphere, "Mouth", f, new Vector3(0f, -0.13f, 0.12f), new Vector3(0.12f, 0.17f, 0.07f), Black);
            P(PrimitiveType.Cube, "TeethTop", f, new Vector3(0f, -0.07f, 0.14f), new Vector3(0.10f, 0.022f, 0.02f), Bone);
            P(PrimitiveType.Cube, "TeethBottom", f, new Vector3(0f, -0.19f, 0.13f), new Vector3(0.09f, 0.02f, 0.02f), Bone);

            // realism pass: hollow cheeks and brow, ichor tears, a bleeding torn mouth, stringy hair, wounds and veins, and a cold glare
            P(PrimitiveType.Sphere, "SunkenL", f, new Vector3(-0.1f, -0.08f, 0.1f), new Vector3(0.07f, 0.1f, 0.05f), Black);
            P(PrimitiveType.Sphere, "SunkenR", f, new Vector3(0.1f, -0.08f, 0.1f), new Vector3(0.07f, 0.1f, 0.05f), Black);
            P(PrimitiveType.Sphere, "Brow", f, new Vector3(0f, 0.13f, 0.11f), new Vector3(0.24f, 0.05f, 0.08f), skin);
            Drip(f, "TearL", new Vector3(-0.07f, 0.02f, 0.14f), 0.16f, 0.012f, Ichor);
            Drip(f, "TearR", new Vector3(0.07f, 0.02f, 0.14f), 0.22f, 0.012f, Ichor);
            Drip(f, "MouthDrip", new Vector3(-0.03f, -0.2f, 0.13f), 0.14f, 0.014f, BloodFresh);
            Drip(f, "MouthDripB", new Vector3(0.04f, -0.2f, 0.13f), 0.2f, 0.012f, BloodFresh);
            P(PrimitiveType.Sphere, "TornCorner", f, new Vector3(0.075f, -0.14f, 0.12f), new Vector3(0.04f, 0.06f, 0.03f), Sinew);
            P(PrimitiveType.Sphere, "ForeheadWound", f, new Vector3(-0.05f, 0.17f, 0.1f), new Vector3(0.07f, 0.03f, 0.04f), Sinew);
            for (int i = 0; i < 12; i++)
            {
                float a = (i / 11f - 0.5f) * 2.5f;
                P(PrimitiveType.Cylinder, "Hair" + i, f, new Vector3(Mathf.Sin(a) * 0.15f, 0.1f - Mathf.Abs(a) * 0.05f, -0.03f - Mathf.Cos(a) * 0.03f),
                    new Vector3(0.012f, 0.22f + 0.08f * ((i * 5) % 3), 0.012f), Hair, Quaternion.Euler(0f, 0f, a * 12f));
            }

            for (int i = 0; i < 4; i++)
            {
                P(PrimitiveType.Cylinder, "Vein" + i, f, new Vector3(-0.09f + i * 0.06f, 0.14f, 0.11f), new Vector3(0.006f, 0.06f, 0.006f), Ichor, Quaternion.Euler(15f, 0f, (i - 1.5f) * 30f));
            }

            ActSetBuildKit.PointLight("Glare", f, new Vector3(0f, 0.04f, 0.25f), new Color(0.75f, 0.95f, 0.85f), 1.2f, 2.6f);
            return f;
        }

        /// <summary>A flat hand: palm toward +z, fingers up +y. About 0.13 x 0.28 m (the Set scales it up).</summary>
        static Transform FlatHand(Transform parent, string name)
        {
            Transform h = Pivot(name, parent, Vector3.zero);
            P(PrimitiveType.Cube, "Palm", h, Vector3.zero, new Vector3(0.12f, 0.12f, 0.03f), Skin);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 0.032f;
                P(PrimitiveType.Cube, "Finger" + i, h, new Vector3(x, 0.115f + (i == 1 || i == 2 ? 0.01f : 0f), 0f),
                    new Vector3(0.022f, 0.12f, 0.02f), Skin, Quaternion.Euler(0f, 0f, -x * 220f));
            }

            P(PrimitiveType.Cube, "Thumb", h, new Vector3(-0.075f, 0.02f, 0f), new Vector3(0.022f, 0.09f, 0.02f), Skin, Quaternion.Euler(0f, 0f, 55f));
            return h;
        }

        // ---------------------------------------------------------------- SpiderDrop

        static MicroEntity Spider(Transform parent)
        {
            MicroEntity e = NewEntity(parent, ScareKind.SpiderDrop, new Color(1f, 0.62f, 0.4f), 5f, 5f, 0.9f, out Transform root);
            var parts = new List<Transform>();
            Transform body = Pivot("SpiderBody", root, Vector3.zero);
            parts.Add(body);
            P(PrimitiveType.Sphere, "Abdomen", body, new Vector3(0f, 0.05f, -0.34f), new Vector3(0.50f, 0.46f, 0.62f), SpiderBody);
            P(PrimitiveType.Sphere, "Mark", body, new Vector3(0f, 0.2f, -0.28f), new Vector3(0.12f, 0.03f, 0.20f), SpiderMark);
            P(PrimitiveType.Sphere, "Thorax", body, new Vector3(0f, 0f, 0.1f), new Vector3(0.34f, 0.30f, 0.34f), SpiderBody);
            P(PrimitiveType.Sphere, "EyeL", body, new Vector3(-0.08f, 0.08f, 0.255f), Vector3.one * 0.10f, EyeRed);
            P(PrimitiveType.Sphere, "EyeR", body, new Vector3(0.08f, 0.08f, 0.255f), Vector3.one * 0.10f, EyeRed);
            for (int i = 0; i < 4; i++)
            {
                float x = (i < 2 ? -1f : 1f) * (i % 2 == 0 ? 0.15f : 0.045f);
                float y = i % 2 == 0 ? 0.0f : 0.15f;
                P(PrimitiveType.Sphere, "Eye" + i, body, new Vector3(x, y, 0.24f), Vector3.one * 0.05f, EyeRed);
            }

            P(PrimitiveType.Capsule, "FangL", body, new Vector3(-0.06f, -0.12f, 0.25f), new Vector3(0.04f, 0.09f, 0.04f), Bone, Quaternion.Euler(-15f, 0f, 8f));
            P(PrimitiveType.Capsule, "FangR", body, new Vector3(0.06f, -0.12f, 0.25f), new Vector3(0.04f, 0.09f, 0.04f), Bone, Quaternion.Euler(-15f, 0f, -8f));

            // realism pass: bristling hairs on the abdomen, venom hanging from the fangs, the glare of eight eyes
            for (int i = 0; i < 24; i++)
            {
                float a = i * 2.4f;
                float y = 0.05f + 0.22f * Mathf.Sin(a * 0.7f);
                P(PrimitiveType.Cylinder, "Bristle" + i, body, new Vector3(Mathf.Cos(a) * 0.22f, y, -0.34f + Mathf.Sin(a) * 0.28f), new Vector3(0.008f, 0.05f, 0.008f), SpiderLeg,
                    Quaternion.Euler(Mathf.Sin(a) * 50f, 0f, -Mathf.Cos(a) * 50f));
            }

            Drip(body, "VenomL", new Vector3(-0.06f, -0.2f, 0.26f), 0.16f, 0.012f, Ichor);
            Drip(body, "VenomR", new Vector3(0.06f, -0.2f, 0.26f), 0.24f, 0.012f, Ichor);
            ActSetBuildKit.PointLight("EyeGlare", body, new Vector3(0f, 0.08f, 0.35f), new Color(1f, 0.12f, 0.08f), 1.3f, 3f);

            Transform thread = P(PrimitiveType.Cylinder, "Thread", root, Vector3.zero, new Vector3(0.014f, 3f, 0.014f), Thread).transform;
            parts.Add(thread);

            for (int i = 0; i < MicroPart.SpiderLegs; i++)
            {
                bool right = i < 4;
                int k = i & 3;
                Transform hip = Pivot("Hip" + i, body, new Vector3(right ? 0.1f : -0.1f, 0f, 0.2f - k * 0.1f));
                Seg(hip, "Femur", 0.55f, 0.03f, SpiderLeg);
                Transform knee = Pivot("Knee" + i, hip, new Vector3(0.55f, 0f, 0f));
                P(PrimitiveType.Sphere, "Joint", knee, Vector3.zero, Vector3.one * 0.07f, SpiderLeg);
                Seg(knee, "Tibia", 0.75f, 0.02f, SpiderLeg);
                parts.Add(hip);
                parts.Add(knee);
            }

            e.p = parts.ToArray();
            return e;
        }

        // ---------------------------------------------------------------- WindowFace

        static MicroEntity Window(Transform parent)
        {
            MicroEntity e = NewEntity(parent, ScareKind.WindowFace, new Color(0.8f, 0.9f, 1f), 4.5f, 4f, 0.9f, out Transform root);
            Transform face = Face(root, "WindowFace", Skin, null);
            Transform l = FlatHand(root, "HandL");
            Transform r = FlatHand(root, "HandR");
            e.p = new[] { face, l, r };
            return e;
        }

        // ---------------------------------------------------------------- SkeletonPop

        static void BoneArm(Transform shoulder)
        {
            Limb(shoulder, "Upper", 0f, -0.4f, 0.025f, Bone);
            P(PrimitiveType.Sphere, "Elbow", shoulder, new Vector3(0f, -0.4f, 0f), Vector3.one * 0.06f, Bone);
            Limb(shoulder, "Fore", -0.4f, -0.8f, 0.02f, Bone);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 0.022f;
                P(PrimitiveType.Cube, "Finger" + i, shoulder, new Vector3(x, -0.88f, 0f), new Vector3(0.012f, 0.13f, 0.012f), Bone,
                    Quaternion.Euler(0f, 0f, x * 200f));
            }
        }

        static MicroEntity Skeleton(Transform parent)
        {
            MicroEntity e = NewEntity(parent, ScareKind.SkeletonPop, new Color(0.5f, 1f, 0.4f), 5f, 5f, 0.9f, out Transform root);
            Transform sk = Pivot("Skeleton", root, Vector3.zero);
            P(PrimitiveType.Cube, "Pelvis", sk, new Vector3(0f, 0.15f, 0f), new Vector3(0.30f, 0.12f, 0.15f), Bone);
            Limb(sk, "Spine", 0.15f, 1.25f, 0.03f, Bone);
            for (int i = 0; i < 5; i++)
            {
                float y = 0.78f + i * 0.09f;
                float w = 0.34f - i * 0.025f;
                P(PrimitiveType.Cube, "Rib" + i, sk, new Vector3(0f, y, 0.05f), new Vector3(w, 0.025f, 0.14f), Bone);
            }

            P(PrimitiveType.Cube, "Sternum", sk, new Vector3(0f, 0.95f, 0.12f), new Vector3(0.04f, 0.3f, 0.02f), Bone);
            P(PrimitiveType.Cube, "Shoulders", sk, new Vector3(0f, 1.22f, 0f), new Vector3(0.54f, 0.04f, 0.05f), Bone);

            Transform head = Pivot("Head", sk, new Vector3(0f, 1.5f, 0.03f));
            P(PrimitiveType.Sphere, "Skull", head, Vector3.zero, new Vector3(0.34f, 0.38f, 0.34f), Bone);
            P(PrimitiveType.Sphere, "SocketL", head, new Vector3(-0.08f, 0.04f, 0.14f), new Vector3(0.10f, 0.11f, 0.06f), Black);
            P(PrimitiveType.Sphere, "SocketR", head, new Vector3(0.08f, 0.04f, 0.14f), new Vector3(0.10f, 0.11f, 0.06f), Black);
            P(PrimitiveType.Sphere, "GlowL", head, new Vector3(-0.08f, 0.04f, 0.17f), Vector3.one * 0.045f, EyeGreen);
            P(PrimitiveType.Sphere, "GlowR", head, new Vector3(0.08f, 0.04f, 0.17f), Vector3.one * 0.045f, EyeGreen);
            P(PrimitiveType.Cube, "Nose", head, new Vector3(0f, -0.04f, 0.165f), new Vector3(0.03f, 0.06f, 0.02f), Black);
            P(PrimitiveType.Cube, "TeethTop", head, new Vector3(0f, -0.10f, 0.15f), new Vector3(0.20f, 0.03f, 0.04f), Bone);
            Transform jaw = Pivot("Jaw", head, new Vector3(0f, -0.10f, 0.0f));
            P(PrimitiveType.Cube, "JawBone", jaw, new Vector3(0f, -0.04f, 0.10f), new Vector3(0.19f, 0.05f, 0.18f), Bone);
            P(PrimitiveType.Cube, "TeethBottom", jaw, new Vector3(0f, -0.01f, 0.17f), new Vector3(0.17f, 0.03f, 0.03f), Bone);

            // realism pass: a cracked skull, tatters of flesh and cloth, bile-green marrow glare, blood on the teeth
            P(PrimitiveType.Cube, "Crack", head, new Vector3(0.05f, 0.17f, 0.12f), new Vector3(0.012f, 0.14f, 0.012f), Black, Quaternion.Euler(0f, 0f, -25f));
            P(PrimitiveType.Cube, "CrackB", head, new Vector3(0.08f, 0.1f, 0.14f), new Vector3(0.01f, 0.08f, 0.012f), Black, Quaternion.Euler(0f, 0f, 30f));
            P(PrimitiveType.Sphere, "FleshScrap", head, new Vector3(-0.13f, -0.02f, 0.12f), new Vector3(0.05f, 0.1f, 0.04f), Sinew);
            P(PrimitiveType.Cube, "ToothBlood", jaw, new Vector3(0f, -0.018f, 0.185f), new Vector3(0.15f, 0.014f, 0.012f), BloodFresh);
            Drip(jaw, "JawDrip", new Vector3(0.04f, -0.03f, 0.19f), 0.16f, 0.012f, BloodFresh);
            for (int i = 0; i < 6; i++)
            {
                P(PrimitiveType.Cube, "Rag" + i, sk, new Vector3(-0.2f + i * 0.08f, 0.62f - (i % 2) * 0.08f, 0.1f), new Vector3(0.07f, 0.28f + 0.06f * (i % 3), 0.012f), Hair, Quaternion.Euler(0f, 0f, (i - 2.5f) * 5f));
            }

            ActSetBuildKit.PointLight("SkullGlare", head, new Vector3(0f, 0.05f, 0.25f), new Color(0.5f, 1f, 0.4f), 1.3f, 2.8f);

            Transform armL = Pivot("ArmL", sk, new Vector3(-0.27f, 1.2f, 0f));
            armL.localRotation = Quaternion.Euler(-125f, 0f, 14f);
            BoneArm(armL);
            Transform armR = Pivot("ArmSwipe", sk, new Vector3(0.27f, 1.2f, 0f));
            BoneArm(armR);

            Transform hole = Pivot("Hole", root, Vector3.zero);
            P(PrimitiveType.Cylinder, "Ring", hole, new Vector3(0f, -0.02f, 0f), new Vector3(1.3f, 0.015f, 1.3f), HoleRing);
            P(PrimitiveType.Cylinder, "Pit", hole, Vector3.zero, new Vector3(1.0f, 0.02f, 1.0f), HoleDark);

            e.p = new[] { sk, head, jaw, armR, hole };
            return e;
        }

        // ---------------------------------------------------------------- ClawHand

        static Transform RottingHand(Transform parent, string name, Mesh finger, Transform[] fingerPivots, int slot0)
        {
            Transform h = Pivot(name, parent, Vector3.zero);
            P(PrimitiveType.Cube, "Palm", h, new Vector3(0f, 0.10f, 0f), new Vector3(0.17f, 0.20f, 0.045f), Rot);
            P(PrimitiveType.Sphere, "Knuckles", h, new Vector3(0f, 0.2f, 0f), new Vector3(0.18f, 0.04f, 0.05f), Rot);
            Limb(h, "Forearm", 0f, -1.2f, 0.045f, Rot);
            P(PrimitiveType.Cube, "Rag", h, new Vector3(0f, -0.25f, 0f), new Vector3(0.14f, 0.12f, 0.12f), Suit);
            Material[] fm = { Rot, Claw };
            // thumb: a short fixed finger
            ActSetBuildKit.MeshObj("Thumb", h, finger, fm, new Vector3(-0.085f, 0.06f, 0f), Quaternion.Euler(0f, 0f, 55f), new Vector3(1f, 0.7f, 1f));
            float[] xs = { -0.064f, -0.032f, 0f, 0.032f, 0.064f };
            float[] len = { 0.85f, 1.1f, 1.25f, 1.1f, 0.85f };
            for (int i = 0; i < 5; i++)
            {
                Transform pv = Pivot("FingerPivot" + i, h, new Vector3(xs[i], 0.2f, 0f));
                ActSetBuildKit.MeshObj("Finger", pv, finger, fm, Vector3.zero, Quaternion.Euler(0f, 0f, -xs[i] * 260f), new Vector3(1f, len[i], 1f));
                fingerPivots[slot0 + i] = pv;
            }

            return h;
        }

        static MicroEntity ClawHand(Transform parent, Mesh finger)
        {
            MicroEntity e = NewEntity(parent, ScareKind.ClawHand, new Color(0.75f, 1f, 0.45f), 4f, 3.5f, 0.9f, out Transform root);
            var all = new Transform[12];
            all[0] = RottingHand(root, "HandA", finger, all, 2);
            all[1] = RottingHand(root, "HandB", finger, all, 7);
            e.p = all;
            return e;
        }

        // ---------------------------------------------------------------- BatSwarm

        static MicroEntity Bats(Transform parent, Mesh bat)
        {
            MicroEntity e = NewEntity(parent, ScareKind.BatSwarm, new Color(0.75f, 0.55f, 1f), 4f, 7f, 0.85f, out Transform root);
            var parts = new Transform[MicroPart.BatCount];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = ActSetBuildKit.MeshObj("Bat" + i, root, bat, new[] { BatMat }, Vector3.zero, Quaternion.identity, Vector3.one * 0.4f).transform;
            }

            e.p = parts;
            return e;
        }

        // ---------------------------------------------------------------- CoffinLid

        static MicroEntity Coffin(Transform parent, Mesh body, Mesh lid)
        {
            MicroEntity e = NewEntity(parent, ScareKind.CoffinLid, new Color(0.5f, 1f, 0.4f), 5f, 5f, 0.9f, out Transform root);
            Transform coffin = Pivot("Coffin", root, Vector3.zero);
            ActSetBuildKit.MeshObj("Body", coffin, body, new[] { Wood }, Vector3.zero, Quaternion.identity, Vector3.one);
            Transform glow = P(PrimitiveType.Cube, "InnerGlow", coffin, new Vector3(0f, 1.0f, -0.42f), new Vector3(0.55f, 1.8f, 0.02f), Glow).transform;

            Transform lidPivot = Pivot("LidPivot", coffin, new Vector3(0.42f, 1.0f, 0f));
            ActSetBuildKit.MeshObj("Lid", lidPivot, lid, new[] { Wood }, Vector3.zero, Quaternion.identity, Vector3.one);
            P(PrimitiveType.Cube, "CrossV", lidPivot, new Vector3(-0.42f, 0.45f, 0.012f), new Vector3(0.07f, 0.75f, 0.015f), Bone);
            P(PrimitiveType.Cube, "CrossH", lidPivot, new Vector3(-0.42f, 0.65f, 0.012f), new Vector3(0.36f, 0.07f, 0.015f), Bone);

            // the corpse: a man in a suit, feet at the origin, facing +z, head at y = 1.5
            Transform corpse = Pivot("Corpse", root, Vector3.zero);
            P(PrimitiveType.Cube, "Torso", corpse, new Vector3(0f, 1.12f, 0f), new Vector3(0.46f, 0.66f, 0.26f), Suit);
            P(PrimitiveType.Cube, "Shoulders", corpse, new Vector3(0f, 1.42f, 0f), new Vector3(0.62f, 0.12f, 0.25f), Suit);
            P(PrimitiveType.Cube, "Shirt", corpse, new Vector3(0f, 1.18f, 0.135f), new Vector3(0.14f, 0.5f, 0.01f), Bone);
            P(PrimitiveType.Cube, "Tie", corpse, new Vector3(0f, 1.10f, 0.145f), new Vector3(0.05f, 0.4f, 0.01f), Tie);
            P(PrimitiveType.Cube, "LegL", corpse, new Vector3(-0.1f, 0.42f, 0f), new Vector3(0.15f, 0.8f, 0.15f), Suit);
            P(PrimitiveType.Cube, "LegR", corpse, new Vector3(0.1f, 0.42f, 0f), new Vector3(0.15f, 0.8f, 0.15f), Suit);
            Transform head = Pivot("Head", corpse, new Vector3(0f, 1.5f, 0.02f));
            P(PrimitiveType.Sphere, "Skull", head, Vector3.zero, new Vector3(0.25f, 0.31f, 0.27f), Rot);
            P(PrimitiveType.Sphere, "SocketL", head, new Vector3(-0.06f, 0.05f, 0.105f), new Vector3(0.08f, 0.09f, 0.05f), Black);
            P(PrimitiveType.Sphere, "SocketR", head, new Vector3(0.06f, 0.05f, 0.105f), new Vector3(0.08f, 0.09f, 0.05f), Black);
            P(PrimitiveType.Sphere, "PinL", head, new Vector3(-0.06f, 0.05f, 0.13f), Vector3.one * 0.026f, EyeGreen);
            P(PrimitiveType.Sphere, "PinR", head, new Vector3(0.06f, 0.05f, 0.13f), Vector3.one * 0.026f, EyeGreen);
            P(PrimitiveType.Sphere, "Mouth", head, new Vector3(0f, -0.1f, 0.105f), new Vector3(0.11f, 0.13f, 0.06f), Mouth);
            Transform jaw = Pivot("Jaw", head, new Vector3(0f, -0.11f, 0.02f));
            P(PrimitiveType.Cube, "JawBone", jaw, new Vector3(0f, -0.03f, 0.08f), new Vector3(0.12f, 0.05f, 0.12f), Rot);
            Transform armL = Pivot("ArmL", corpse, new Vector3(-0.32f, 1.4f, 0f));
            Transform armR = Pivot("ArmR", corpse, new Vector3(0.32f, 1.4f, 0f));
            foreach (Transform a in new[] { armL, armR })
            {
                Limb(a, "Sleeve", 0f, -0.5f, 0.06f, Suit);
                Limb(a, "Forearm", -0.5f, -0.9f, 0.05f, Suit);
                P(PrimitiveType.Sphere, "Hand", a, new Vector3(0f, -0.96f, 0f), new Vector3(0.1f, 0.14f, 0.05f), Rot);
                for (int i = 0; i < 3; i++)
                {
                    P(PrimitiveType.Cube, "Finger" + i, a, new Vector3((i - 1) * 0.03f, -1.07f, 0f), new Vector3(0.015f, 0.09f, 0.015f), Rot);
                }
            }

            e.p = new[] { coffin, lidPivot, corpse, head, jaw, armL, armR, glow };
            return e;
        }

        // ---------------------------------------------------------------- ShadowRush

        static MicroEntity ShadowRush(Transform parent)
        {
            MicroEntity e = NewEntity(parent, ScareKind.ShadowRush, new Color(0.7f, 1f, 0.85f), 3f, 4f, 0.55f, out Transform root);
            Transform sh = Pivot("Shadow", root, Vector3.zero);
            sh.localScale = Vector3.one * 1.3f;
            P(PrimitiveType.Capsule, "Torso", sh, new Vector3(0f, 1.1f, 0f), new Vector3(0.42f, 0.45f, 0.28f), Shadow);
            P(PrimitiveType.Sphere, "Head", sh, new Vector3(0f, 1.62f, 0.02f), new Vector3(0.26f, 0.30f, 0.26f), Shadow);
            P(PrimitiveType.Sphere, "EyeL", sh, new Vector3(-0.075f, 1.65f, 0.125f), new Vector3(0.12f, 0.07f, 0.04f), EyeWhite);
            P(PrimitiveType.Sphere, "EyeR", sh, new Vector3(0.075f, 1.65f, 0.125f), new Vector3(0.12f, 0.07f, 0.04f), EyeWhite);
            Transform armL = Pivot("ArmL", sh, new Vector3(-0.26f, 1.4f, 0f));
            Transform armR = Pivot("ArmR", sh, new Vector3(0.26f, 1.4f, 0f));
            Transform legL = Pivot("LegL", sh, new Vector3(-0.1f, 0.75f, 0f));
            Transform legR = Pivot("LegR", sh, new Vector3(0.1f, 0.75f, 0f));
            foreach (Transform a in new[] { armL, armR })
            {
                P(PrimitiveType.Capsule, "Arm", a, new Vector3(0f, -0.32f, 0f), new Vector3(0.1f, 0.34f, 0.1f), Shadow);
                P(PrimitiveType.Capsule, "Claw", a, new Vector3(0f, -0.72f, 0f), new Vector3(0.05f, 0.12f, 0.05f), Shadow);
            }

            foreach (Transform l in new[] { legL, legR })
            {
                P(PrimitiveType.Capsule, "Leg", l, new Vector3(0f, -0.38f, 0f), new Vector3(0.13f, 0.4f, 0.13f), Shadow);
            }

            e.p = new[] { sh, armL, armR, legL, legR };
            return e;
        }

        // ---------------------------------------------------------------- MirrorFace

        static MicroEntity Mirror(Transform parent, Mesh shard)
        {
            MicroEntity e = NewEntity(parent, ScareKind.MirrorFace, new Color(0.8f, 0.9f, 1f), 5.5f, 5f, 1.0f, out Transform root);
            var parts = new List<Transform>();
            Transform mirror = Pivot("Mirror", root, Vector3.zero);
            parts.Add(mirror);
            P(PrimitiveType.Cube, "FrameL", mirror, new Vector3(-0.56f, 0f, 0f), new Vector3(0.10f, 1.9f, 0.10f), MirrorFrame);
            P(PrimitiveType.Cube, "FrameR", mirror, new Vector3(0.56f, 0f, 0f), new Vector3(0.10f, 1.9f, 0.10f), MirrorFrame);
            P(PrimitiveType.Cube, "FrameT", mirror, new Vector3(0f, 0.9f, 0f), new Vector3(1.22f, 0.10f, 0.10f), MirrorFrame);
            P(PrimitiveType.Cube, "FrameB", mirror, new Vector3(0f, -0.9f, 0f), new Vector3(1.22f, 0.10f, 0.10f), MirrorFrame);
            P(PrimitiveType.Sphere, "Crest", mirror, new Vector3(0f, 1.0f, 0f), new Vector3(0.22f, 0.18f, 0.12f), MirrorFrame);
            Transform pane = P(PrimitiveType.Cube, "Pane", mirror, Vector3.zero, new Vector3(1.0f, 1.7f, 0.02f), Glass).transform;

            Transform cracks = Pivot("Cracks", mirror, new Vector3(0f, 0f, 0.016f));
            float[] ang = { 20f, -35f, 70f, 5f, -60f };
            float[] len = { 1.1f, 0.9f, 0.7f, 0.6f, 0.55f };
            float[] ox = { 0f, 0.1f, -0.15f, 0.2f, -0.1f };
            float[] oy = { 0f, 0.2f, -0.25f, 0.45f, -0.5f };
            for (int i = 0; i < ang.Length; i++)
            {
                P(PrimitiveType.Cube, "Crack" + i, cracks, new Vector3(ox[i], oy[i], 0f), new Vector3(0.012f, len[i], 0.004f), Crack, Quaternion.Euler(0f, 0f, ang[i]));
            }

            Transform face = Face(root, "MirrorFace", Skin, EyeGreen);
            parts.Add(pane);
            parts.Add(face);
            parts.Add(cracks);
            for (int i = 0; i < MicroPart.MirShards; i++)
            {
                parts.Add(ActSetBuildKit.MeshObj("Shard" + i, root, shard, new[] { Shard }, Vector3.zero, Quaternion.identity, Vector3.one).transform);
            }

            e.p = parts.ToArray();
            return e;
        }
    }
}
