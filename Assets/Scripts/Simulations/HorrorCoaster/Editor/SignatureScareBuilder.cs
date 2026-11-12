using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Builds the five pooled signature-scare entities (beast, ghoul + perch, cobra, spike walls, bat flood) hidden under one
    /// <see cref="SignatureScareSet"/>. Primitives plus procedural cones (claws, fangs, spikes), procedural textures with normal maps
    /// (<see cref="SigTextures"/>) and wet, glossy materials so they read as flesh, fur, scales and old iron rather than plastic.
    /// </summary>
    public static class SignatureScareBuilder
    {
        // Surfaces are textured; the colour is a tint over the texture. Smoothness is high on purpose: wet skin catches the lights.
        static Material Fur => Tex(Mat("SigFur", new Color(1.1f, 1.0f, 1.0f), CoasterPalette.Glow(new Color(0.5f, 0.3f, 0.3f), 0.12f), 0.3f), "fur", 2f, 1f);
        static Material Bone => Tex(Mat("SigBone", Color.white, CoasterPalette.Glow(CoasterPalette.Bone, 0.25f), 0.45f), "bone", 1f, 0.8f);
        static Material Black => Mat("SigBlack", new Color(0.01f, 0.01f, 0.02f), default, 0.7f);
        static Material EyeRed => Mat("SigEyeRed", CoasterPalette.BloodRed, CoasterPalette.Glow(new Color(1f, 0.1f, 0.06f), 7f), 0.9f);
        static Material EyeGreen => Mat("SigEyeGreen", CoasterPalette.ToxicGreen * 0.5f, CoasterPalette.Glow(CoasterPalette.ToxicGreen, 4.5f), 0.9f);
        static Material EyeYellow => Mat("SigEyeYellow", new Color(0.8f, 0.7f, 0.1f), CoasterPalette.Glow(new Color(1f, 0.85f, 0.15f), 4f), 0.9f);
        static Material Pale => Tex(Mat("SigPale", new Color(1.05f, 1.05f, 1.0f), CoasterPalette.Glow(new Color(0.6f, 0.75f, 0.65f), 0.35f), 0.55f), "flesh", 1f, 1.2f);
        static Material Rag => Tex(Mat("SigRag", new Color(0.55f, 0.5f, 0.55f), CoasterPalette.Glow(new Color(0.4f, 0.3f, 0.6f), 0.08f), 0.1f), "stone", 2f, 0.5f);
        static Material Stone => Tex(Mat("SigStone", new Color(0.8f, 0.82f, 0.9f), CoasterPalette.Glow(new Color(0.3f, 0.35f, 0.6f), 0.08f), 0.2f), "stone", 1.5f, 1.2f);
        static Material Scale => Tex(Mat("SigScale", new Color(1.1f, 1.2f, 0.9f), CoasterPalette.Glow(CoasterPalette.ToxicGreen, 0.22f), 0.65f), "scales", 3f, 1.4f);
        static Material Belly => Tex(Mat("SigBelly", new Color(1.2f, 1.1f, 0.6f), CoasterPalette.Glow(CoasterPalette.Lantern, 0.3f), 0.6f), "scales", 2f, 1f);
        static Material Hood => Tex(Mat("SigHood", new Color(0.9f, 1.1f, 0.7f), CoasterPalette.Glow(CoasterPalette.ToxicGreen, 0.3f), 0.55f), "scales", 3f, 1f);
        static Material HoodMark => Mat("SigHoodMark", new Color(0.9f, 0.85f, 0.45f), CoasterPalette.Glow(new Color(1f, 0.9f, 0.4f), 0.8f), 0.4f);
        static Material Tongue => Mat("SigTongue", CoasterPalette.BloodRed, CoasterPalette.Glow(CoasterPalette.BloodRed, 1.0f), 0.8f);
        static Material Mouth => Mat("SigMouth", new Color(0.35f, 0.02f, 0.05f), CoasterPalette.Glow(CoasterPalette.BloodRed, 0.7f), 0.85f);
        static Material Drool => Mat("SigDrool", new Color(0.8f, 0.9f, 0.8f, 0.5f), CoasterPalette.Glow(new Color(0.7f, 1f, 0.8f), 0.5f), 1f, 0f, true);
        static Material Venom => Mat("SigVenom", new Color(0.4f, 1f, 0.3f, 0.7f), CoasterPalette.Glow(CoasterPalette.ToxicGreen, 2.5f), 1f, 0f, true);
        static Material Blood => Mat("SigBlood", new Color(0.3f, 0.01f, 0.02f), CoasterPalette.Glow(CoasterPalette.BloodRed, 0.35f), 0.95f);
        static Material RiderSuit => Mat("SigRider", new Color(0.25f, 0.12f, 0.05f), CoasterPalette.Glow(CoasterPalette.Pumpkin, 0.8f), 0.2f);
        static Material Rust => Tex(Mat("SigRust", new Color(1.1f, 0.9f, 0.8f), CoasterPalette.Glow(new Color(0.8f, 0.35f, 0.1f), 0.18f), 0.35f, 0.5f), "rust", 1.5f, 1.3f);
        static Material Steel => Tex(Mat("SigSteel", new Color(0.8f, 0.82f, 0.9f), CoasterPalette.Glow(new Color(0.7f, 0.75f, 0.9f), 0.2f), 0.85f, 0.95f), "rust", 0.6f, 0.6f);
        static Material Warn => Mat("SigWarn", CoasterPalette.Pumpkin * 0.5f, CoasterPalette.Glow(CoasterPalette.Pumpkin, 3.2f), 0.5f);
        static Material BatMat => Mat("SigBat", new Color(0.2f, 0.1f, 0.3f), CoasterPalette.Glow(new Color(0.55f, 0.28f, 0.9f), 1.0f), 0.2f);
        static Material Mouthglow => Mat("SigMouthGlow", CoasterPalette.Pumpkin * 0.6f, CoasterPalette.Glow(CoasterPalette.Pumpkin, 2.6f), 0.1f);

        // realism pass: raw wet flesh, black ichor, fresh blood, a pale cataract
        static Material Sinew => Mat("SigSinew", new Color(0.45f, 0.07f, 0.09f), CoasterPalette.Glow(new Color(0.8f, 0.1f, 0.12f), 0.3f), 0.92f);
        static Material Ichor => Mat("SigIchor", new Color(0.015f, 0.012f, 0.02f), CoasterPalette.Glow(new Color(0.2f, 0.5f, 0.3f), 0.15f), 1f);
        static Material BloodFresh => Mat("SigBloodFresh", new Color(0.55f, 0.015f, 0.03f), CoasterPalette.Glow(CoasterPalette.BloodRed, 0.8f), 1f);
        static Material Cataract => Mat("SigCataract", new Color(0.85f, 0.9f, 0.85f), CoasterPalette.Glow(new Color(0.85f, 0.95f, 0.9f), 1.8f), 0.9f);

        static Material Mat(string key, Color c, Color emission, float smooth, float metal = 0f, bool transparent = false)
        {
            return CoasterMaterials.Get(key, c, emission, smooth, metal, transparent);
        }

        static Material Tex(Material m, string texture, float tiling, float bump)
        {
            return SigTextures.Apply(m, texture, tiling, bump);
        }

        static Mesh _cone;

        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            var go = new GameObject("SignatureScares");
            go.transform.SetParent(parent, false);
            var set = go.AddComponent<SignatureScareSet>();
            set.stingSeconds = cfg != null ? cfg.stingSeconds : 0.6f;

            _cone = ActSetBuildKit.SaveMeshFresh(ConeMesh(), "CoastSig_Spike");
            Mesh bat = MicroScareMeshes.Bat();

            set.entities = new[]
            {
                Sprinter(go.transform),
                Leaper(go.transform),
                Snake(go.transform),
                Spikes(go.transform),
                BatFlood(go.transform, bat)
            };
            return go;
        }

        // ---------------------------------------------------------------- shared

        static SigEntity NewEntity(Transform parent, ScareKind kind, Color lightColor, float lightMax, float range, float duration, out Transform root)
        {
            GameObject r = ActSetBuildKit.Empty("Sig_" + kind, parent);
            Light l = ActSetBuildKit.PointLight("Light", r.transform, Vector3.zero, lightColor, 0f, range);
            l.enabled = false;
            r.SetActive(false);
            root = r.transform;
            return new SigEntity { kind = kind, root = r, light = l, duration = duration, lightMax = lightMax };
        }

        static GameObject P(PrimitiveType t, string n, Transform parent, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
        {
            return ActSetBuildKit.Prim(t, n, parent, pos, scale, m, rot);
        }

        /// <summary>A thin wet streak hanging from pos (blood, ichor, drool): a stretched cylinder with a bead at the end.</summary>
        static void Drip(Transform parent, string n, Vector3 pos, float length, float width, Material m)
        {
            P(PrimitiveType.Cylinder, n, parent, pos + Vector3.down * (length * 0.5f), new Vector3(width, length * 0.5f, width), m);
            P(PrimitiveType.Sphere, n + "Bead", parent, pos + Vector3.down * (length + width * 0.5f), new Vector3(width * 1.8f, width * 2.4f, width * 1.8f), m);
        }

        /// <summary>A small coloured light so the creature lights its own surroundings (its glare falls on the riders hands and the lap bar).</summary>
        static void EyeLight(Transform parent, Vector3 pos, Color c, float intensity, float range)
        {
            ActSetBuildKit.PointLight("EyeLight", parent, pos, c, intensity, range);
        }

        static Transform Pivot(string n, Transform parent, Vector3 pos)
        {
            return ActSetBuildKit.Empty(n, parent, pos, Quaternion.identity).transform;
        }

        /// <summary>A pointed cone from <paramref name="pos"/> along <paramref name="dir"/>: claws, fangs, teeth, thorns.</summary>
        static GameObject Cone(Transform parent, string n, Vector3 pos, Vector3 dir, float length, float diameter, Material m)
        {
            return ActSetBuildKit.MeshObj(n, parent, _cone, new[] { m }, pos, Quaternion.LookRotation(dir.normalized, Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up),
                new Vector3(diameter, diameter, length));
        }

        /// <summary>A cone along +z: base radius 0.5 at z = 0, tip at z = 1.</summary>
        static Mesh ConeMesh()
        {
            var mb = new MeshAcc();
            const int sides = 10;
            Vector3 tip = new Vector3(0f, 0f, 1f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides;
                float a1 = 2f * Mathf.PI * (i + 1) / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, Mathf.Sin(a0) * 0.5f, 0f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, Mathf.Sin(a1) * 0.5f, 0f);
                Vector3 n = Vector3.Cross(p1 - p0, tip - p0).normalized;
                int a = mb.Add(p0, n);
                int b = mb.Add(p1, n);
                int c = mb.Add(tip, n);
                mb.Tri(0, a, c, b);
                mb.Tri(0, a, b, c);
            }

            return mb.ToMesh();
        }

        // ---------------------------------------------------------------- SprinterLeap

        static SigEntity Sprinter(Transform parent)
        {
            SigEntity e = NewEntity(parent, ScareKind.SprinterLeap, new Color(1f, 0.35f, 0.25f), 6f, 12f, 1.5f, out Transform root);
            Transform body = Pivot("Beast", root, Vector3.zero);

            // trunk: deep chest, tucked waist, heavy haunches, a visible ribcage and spine
            P(PrimitiveType.Sphere, "Chest", body, new Vector3(0f, 0.66f, 0.42f), new Vector3(0.62f, 0.66f, 0.7f), Fur);
            P(PrimitiveType.Sphere, "Waist", body, new Vector3(0f, 0.56f, -0.05f), new Vector3(0.42f, 0.42f, 0.7f), Fur);
            P(PrimitiveType.Sphere, "Haunch", body, new Vector3(0f, 0.6f, -0.5f), new Vector3(0.56f, 0.58f, 0.62f), Fur);
            P(PrimitiveType.Sphere, "ShoulderL", body, new Vector3(-0.27f, 0.76f, 0.5f), new Vector3(0.3f, 0.34f, 0.4f), Fur);
            P(PrimitiveType.Sphere, "ShoulderR", body, new Vector3(0.27f, 0.76f, 0.5f), new Vector3(0.3f, 0.34f, 0.4f), Fur);
            for (int i = 0; i < 6; i++)
            {
                float z = 0.55f - i * 0.15f;
                float w = 0.36f - i * 0.025f;
                P(PrimitiveType.Capsule, "RibL" + i, body, new Vector3(-w * 0.78f, 0.55f, z), new Vector3(0.035f, 0.17f, 0.035f), Bone, Quaternion.Euler(0f, 0f, 24f));
                P(PrimitiveType.Capsule, "RibR" + i, body, new Vector3(w * 0.78f, 0.55f, z), new Vector3(0.035f, 0.17f, 0.035f), Bone, Quaternion.Euler(0f, 0f, -24f));
            }

            for (int i = 0; i < 9; i++)
            {
                float z = 0.7f - i * 0.17f;
                Cone(body, "Thorn" + i, new Vector3(0f, 0.93f - 0.02f * i, z), new Vector3(0f, 1f, -0.5f), 0.16f - 0.008f * i, 0.07f, Bone);
            }

            // head: long skull, heavy brow, snout, exposed teeth rows, drool
            Transform head = Pivot("Head", body, new Vector3(0f, 0.68f, 0.88f));
            P(PrimitiveType.Sphere, "Skull", head, new Vector3(0f, 0f, 0.06f), new Vector3(0.4f, 0.34f, 0.5f), Fur);
            P(PrimitiveType.Sphere, "BrowL", head, new Vector3(-0.11f, 0.1f, 0.2f), new Vector3(0.15f, 0.08f, 0.16f), Fur);
            P(PrimitiveType.Sphere, "BrowR", head, new Vector3(0.11f, 0.1f, 0.2f), new Vector3(0.15f, 0.08f, 0.16f), Fur);
            P(PrimitiveType.Sphere, "Snout", head, new Vector3(0f, -0.07f, 0.4f), new Vector3(0.22f, 0.18f, 0.36f), Fur);
            P(PrimitiveType.Sphere, "Nose", head, new Vector3(0f, -0.02f, 0.58f), new Vector3(0.09f, 0.07f, 0.07f), Black);
            P(PrimitiveType.Sphere, "EyeL", head, new Vector3(-0.12f, 0.05f, 0.27f), new Vector3(0.1f, 0.055f, 0.05f), EyeRed, Quaternion.Euler(0f, 0f, 14f));
            P(PrimitiveType.Sphere, "EyeR", head, new Vector3(0.12f, 0.05f, 0.27f), new Vector3(0.1f, 0.055f, 0.05f), EyeRed, Quaternion.Euler(0f, 0f, -14f));
            P(PrimitiveType.Cube, "EarL", head, new Vector3(-0.14f, 0.2f, -0.03f), new Vector3(0.07f, 0.22f, 0.07f), Fur, Quaternion.Euler(0f, 0f, 18f));
            P(PrimitiveType.Cube, "EarR", head, new Vector3(0.14f, 0.2f, -0.03f), new Vector3(0.07f, 0.22f, 0.07f), Fur, Quaternion.Euler(0f, 0f, -18f));
            P(PrimitiveType.Sphere, "Gums", head, new Vector3(0f, -0.16f, 0.4f), new Vector3(0.2f, 0.04f, 0.32f), Mouth);
            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3f) * 0.028f;
                Cone(head, "TopTooth" + i, new Vector3(x, -0.15f, 0.3f + (3f - Mathf.Abs(i - 3f)) * 0.03f + 0.06f), Vector3.down + Vector3.back * 0.1f, i == 3 ? 0.12f : 0.085f, 0.028f, Bone);
            }

            Cone(head, "FangL", new Vector3(-0.07f, -0.15f, 0.5f), Vector3.down, 0.15f, 0.04f, Bone);
            Cone(head, "FangR", new Vector3(0.07f, -0.15f, 0.5f), Vector3.down, 0.15f, 0.04f, Bone);
            for (int i = 0; i < 3; i++)
            {
                P(PrimitiveType.Cylinder, "Drool" + i, head, new Vector3((i - 1) * 0.07f, -0.3f - 0.05f * i, 0.4f), new Vector3(0.012f, 0.1f + 0.05f * i, 0.012f), Drool);
            }

            Transform jaw = Pivot("Jaw", head, new Vector3(0f, -0.12f, 0.12f));
            P(PrimitiveType.Sphere, "JawBone", jaw, new Vector3(0f, -0.03f, 0.22f), new Vector3(0.17f, 0.06f, 0.4f), Fur);
            P(PrimitiveType.Sphere, "Tongue", jaw, new Vector3(0f, 0f, 0.22f), new Vector3(0.1f, 0.03f, 0.3f), Tongue);
            for (int i = 0; i < 6; i++)
            {
                Cone(jaw, "LowTooth" + i, new Vector3((i - 2.5f) * 0.03f, -0.005f, 0.28f + 0.05f * (i == 0 || i == 5 ? 0 : 1)), Vector3.up + Vector3.forward * 0.1f, i == 0 || i == 5 ? 0.12f : 0.07f, 0.026f, Bone);
            }

            P(PrimitiveType.Capsule, "Tail", body, new Vector3(0f, 0.55f, -0.95f), new Vector3(0.1f, 0.5f, 0.1f), Fur, Quaternion.Euler(80f, 0f, 0f));

            // --- realism pass: mange and open wounds, a blood-soaked muzzle, tufted spines, wet eye glints and a red glare on its own face
            P(PrimitiveType.Sphere, "MangeL", body, new Vector3(-0.3f, 0.62f, 0.1f), new Vector3(0.05f, 0.26f, 0.36f), Sinew);
            P(PrimitiveType.Sphere, "MangeR", body, new Vector3(0.28f, 0.7f, -0.3f), new Vector3(0.05f, 0.22f, 0.3f), Sinew);
            P(PrimitiveType.Sphere, "ShoulderWound", body, new Vector3(0.36f, 0.82f, 0.5f), new Vector3(0.08f, 0.2f, 0.26f), BloodFresh);
            P(PrimitiveType.Sphere, "SkullWoundL", head, new Vector3(-0.17f, 0.04f, 0.1f), new Vector3(0.04f, 0.14f, 0.2f), Sinew);
            P(PrimitiveType.Sphere, "MuzzleBlood", head, new Vector3(0f, -0.1f, 0.5f), new Vector3(0.2f, 0.07f, 0.2f), BloodFresh);
            P(PrimitiveType.Sphere, "ChinBlood", jaw, new Vector3(0f, -0.06f, 0.3f), new Vector3(0.14f, 0.05f, 0.24f), BloodFresh);
            P(PrimitiveType.Sphere, "GlintL", head, new Vector3(-0.115f, 0.075f, 0.3f), Vector3.one * 0.018f, Cataract);
            P(PrimitiveType.Sphere, "GlintR", head, new Vector3(0.125f, 0.075f, 0.3f), Vector3.one * 0.018f, Cataract);
            for (int i = 0; i < 3; i++)
            {
                Drip(jaw, "BloodDrip" + i, new Vector3((i - 1) * 0.06f, -0.04f, 0.3f + 0.04f * i), 0.12f + 0.06f * i, 0.012f, BloodFresh);
            }

            for (int i = 0; i < 12; i++)
            {
                Cone(body, "Tuft" + i, new Vector3((i % 2 == 0 ? -0.22f : 0.22f), 0.86f - 0.01f * i, 0.55f - i * 0.1f), new Vector3(i % 2 == 0 ? -0.5f : 0.5f, 1f, -0.6f), 0.2f, 0.06f, Fur);
            }

            EyeLight(head, new Vector3(0f, 0.05f, 0.34f), new Color(1f, 0.1f, 0.06f), 1.4f, 3.2f);

            var parts = new List<Transform> { body };
            Vector3[] hips = { new Vector3(-0.26f, 0.7f, 0.5f), new Vector3(0.26f, 0.7f, 0.5f), new Vector3(-0.25f, 0.68f, -0.5f), new Vector3(0.25f, 0.68f, -0.5f) };
            for (int i = 0; i < 4; i++)
            {
                Transform leg = Pivot("Leg" + i, body, hips[i]);
                P(PrimitiveType.Capsule, "Thigh", leg, new Vector3(0f, -0.22f, 0.02f), new Vector3(0.17f, 0.26f, 0.17f), Fur);
                P(PrimitiveType.Capsule, "Shin", leg, new Vector3(0f, -0.52f, -0.04f), new Vector3(0.09f, 0.22f, 0.09f), Fur, Quaternion.Euler(-14f, 0f, 0f));
                P(PrimitiveType.Sphere, "Paw", leg, new Vector3(0f, -0.72f, 0.02f), new Vector3(0.15f, 0.08f, 0.22f), Fur);
                for (int c = 0; c < 4; c++)
                {
                    Cone(leg, "Claw" + c, new Vector3((c - 1.5f) * 0.04f, -0.72f, 0.12f), new Vector3(0f, -0.4f, 1f), 0.15f, 0.028f, Bone);
                }

                parts.Add(leg);
            }

            parts.Add(jaw);
            e.p = parts.ToArray();
            return e;
        }

        // ---------------------------------------------------------------- SideLeap

        static SigEntity Leaper(Transform parent)
        {
            SigEntity e = NewEntity(parent, ScareKind.SideLeap, new Color(0.6f, 1f, 0.7f), 5f, 9f, 1.2f, out Transform root);
            Transform g = Pivot("Ghoul", root, Vector3.zero);

            // emaciated body: sunken torso with visible ribs and spine, a torn shroud
            P(PrimitiveType.Capsule, "Torso", g, new Vector3(0f, 1.05f, 0f), new Vector3(0.36f, 0.46f, 0.24f), Pale);
            for (int i = 0; i < 6; i++)
            {
                P(PrimitiveType.Capsule, "RibL" + i, g, new Vector3(-0.13f, 1.22f - i * 0.07f, 0.1f), new Vector3(0.025f, 0.13f, 0.025f), Pale, Quaternion.Euler(0f, 0f, 76f));
                P(PrimitiveType.Capsule, "RibR" + i, g, new Vector3(0.13f, 1.22f - i * 0.07f, 0.1f), new Vector3(0.025f, 0.13f, 0.025f), Pale, Quaternion.Euler(0f, 0f, -76f));
                P(PrimitiveType.Sphere, "Spine" + i, g, new Vector3(0f, 1.25f - i * 0.08f, -0.12f), Vector3.one * 0.05f, Pale);
            }

            P(PrimitiveType.Sphere, "Pelvis", g, new Vector3(0f, 0.7f, 0f), new Vector3(0.34f, 0.2f, 0.2f), Pale);
            P(PrimitiveType.Capsule, "LegL", g, new Vector3(-0.11f, 0.4f, 0f), new Vector3(0.12f, 0.4f, 0.12f), Pale);
            P(PrimitiveType.Capsule, "LegR", g, new Vector3(0.11f, 0.4f, 0f), new Vector3(0.12f, 0.4f, 0.12f), Pale);
            for (int i = 0; i < 7; i++)
            {
                float x = -0.28f + i * 0.095f;
                P(PrimitiveType.Cube, "Shroud" + i, g, new Vector3(x, 0.62f - (i % 2) * 0.08f, 0.02f), new Vector3(0.1f, 0.5f + (i % 3) * 0.12f, 0.04f), Rag, Quaternion.Euler(0f, 0f, (i - 3) * 4f));
            }

            // head: hollow face, black sockets, hanging hair, a stretched-open mouth
            Transform head = Pivot("Head", g, new Vector3(0f, 1.62f, 0.1f));
            P(PrimitiveType.Sphere, "Skull", head, Vector3.zero, new Vector3(0.3f, 0.4f, 0.32f), Pale);
            P(PrimitiveType.Sphere, "CheekL", head, new Vector3(-0.1f, -0.07f, 0.1f), new Vector3(0.09f, 0.12f, 0.08f), Pale);
            P(PrimitiveType.Sphere, "CheekR", head, new Vector3(0.1f, -0.07f, 0.1f), new Vector3(0.09f, 0.12f, 0.08f), Pale);
            P(PrimitiveType.Sphere, "SocketL", head, new Vector3(-0.075f, 0.05f, 0.13f), new Vector3(0.11f, 0.14f, 0.07f), Black);
            P(PrimitiveType.Sphere, "SocketR", head, new Vector3(0.075f, 0.05f, 0.13f), new Vector3(0.11f, 0.14f, 0.07f), Black);
            P(PrimitiveType.Sphere, "GlowL", head, new Vector3(-0.075f, 0.045f, 0.165f), Vector3.one * 0.05f, EyeGreen);
            P(PrimitiveType.Sphere, "GlowR", head, new Vector3(0.075f, 0.045f, 0.165f), Vector3.one * 0.05f, EyeGreen);
            P(PrimitiveType.Sphere, "NoseHole", head, new Vector3(0f, -0.04f, 0.16f), new Vector3(0.05f, 0.07f, 0.04f), Black);
            P(PrimitiveType.Sphere, "MouthCavity", head, new Vector3(0f, -0.15f, 0.13f), new Vector3(0.17f, 0.2f, 0.1f), Mouth);
            for (int i = 0; i < 14; i++)
            {
                float a = (i / 13f - 0.5f) * 2.4f;
                P(PrimitiveType.Cylinder, "Hair" + i, head, new Vector3(Mathf.Sin(a) * 0.17f, 0.12f - Mathf.Abs(a) * 0.04f, -0.06f - Mathf.Cos(a) * 0.04f), new Vector3(0.012f, 0.28f + 0.1f * ((i * 7) % 4) / 3f, 0.012f), Black, Quaternion.Euler(4f * (i % 3 - 1), 0f, a * 14f));
            }

            Transform jaw = Pivot("Jaw", g, new Vector3(0f, 1.52f, 0.14f));
            P(PrimitiveType.Sphere, "JawBone", jaw, new Vector3(0f, -0.06f, 0.1f), new Vector3(0.17f, 0.06f, 0.2f), Pale);
            P(PrimitiveType.Sphere, "Tongue", jaw, new Vector3(0f, -0.03f, 0.1f), new Vector3(0.08f, 0.03f, 0.14f), Tongue);
            for (int i = 0; i < 6; i++)
            {
                Cone(jaw, "LowTooth" + i, new Vector3((i - 2.5f) * 0.026f, -0.03f, 0.19f), Vector3.up, 0.065f, 0.02f, Bone);
                Cone(head, "TopTooth" + i, new Vector3((i - 2.5f) * 0.026f, -0.1f, 0.15f), Vector3.down, 0.065f, 0.02f, Bone);
            }

            // --- realism pass: ichor weeping from the sockets and mouth, a torn cheek, veins, a dead pale eye, and a sickly glare on its own face
            for (int i = 0; i < 2; i++)
            {
                Drip(head, "Tear" + i, new Vector3(i == 0 ? -0.075f : 0.075f, -0.02f, 0.16f), 0.2f + 0.05f * i, 0.012f, Ichor);
            }

            for (int i = 0; i < 4; i++)
            {
                Drip(jaw, "MouthIchor" + i, new Vector3((i - 1.5f) * 0.04f, -0.05f, 0.2f), 0.16f + 0.07f * ((i * 3) % 4), 0.011f, Ichor);
            }

            P(PrimitiveType.Sphere, "CheekWound", head, new Vector3(0.115f, -0.07f, 0.12f), new Vector3(0.05f, 0.1f, 0.06f), Sinew);
            for (int i = 0; i < 3; i++)
            {
                Cone(head, "ExposedTooth" + i, new Vector3(0.125f, -0.1f + i * 0.025f, 0.16f), Vector3.right, 0.04f, 0.018f, Bone);
            }

            for (int i = 0; i < 5; i++)
            {
                P(PrimitiveType.Cylinder, "Vein" + i, head, new Vector3(-0.12f + i * 0.06f, 0.16f, 0.1f), new Vector3(0.006f, 0.07f, 0.006f), Ichor, Quaternion.Euler(20f, 0f, (i - 2) * 28f));
            }

            P(PrimitiveType.Sphere, "ChestWound", g, new Vector3(0.08f, 1.12f, 0.12f), new Vector3(0.12f, 0.16f, 0.04f), BloodFresh);
            P(PrimitiveType.Sphere, "Cataract", head, new Vector3(0.075f, 0.045f, 0.2f), Vector3.one * 0.022f, Cataract);
            EyeLight(head, new Vector3(0f, 0.04f, 0.26f), new Color(0.5f, 1f, 0.6f), 1.3f, 2.8f);

            // long arms, bony elbows, elongated clawed fingers
            Transform armL = Pivot("ArmL", g, new Vector3(-0.3f, 1.38f, 0f));
            Transform armR = Pivot("ArmR", g, new Vector3(0.3f, 1.38f, 0f));
            foreach (Transform a in new[] { armL, armR })
            {
                P(PrimitiveType.Capsule, "Upper", a, new Vector3(0f, -0.3f, 0f), new Vector3(0.085f, 0.3f, 0.085f), Pale);
                P(PrimitiveType.Sphere, "Elbow", a, new Vector3(0f, -0.62f, 0f), Vector3.one * 0.11f, Pale);
                P(PrimitiveType.Capsule, "Fore", a, new Vector3(0f, -0.92f, 0f), new Vector3(0.07f, 0.3f, 0.07f), Pale);
                P(PrimitiveType.Sphere, "Hand", a, new Vector3(0f, -1.25f, 0f), new Vector3(0.1f, 0.14f, 0.05f), Pale);
                for (int i = 0; i < 5; i++)
                {
                    float x = (i - 2f) * 0.034f;
                    P(PrimitiveType.Capsule, "Finger" + i, a, new Vector3(x, -1.42f, 0f), new Vector3(0.016f, 0.1f, 0.016f), Pale, Quaternion.Euler(0f, 0f, -x * 260f));
                    Cone(a, "Nail" + i, new Vector3(x * 1.5f, -1.52f, 0f), new Vector3(x * 3f, -1f, 0f), 0.09f, 0.02f, Bone);
                }
            }

            Transform pillar = Pivot("Perch", root, Vector3.zero);
            P(PrimitiveType.Cube, "Shaft", pillar, Vector3.zero, new Vector3(1.5f, 3.0f, 1.5f), Stone);
            P(PrimitiveType.Cube, "Cap", pillar, new Vector3(0f, 1.55f, 0f), new Vector3(1.9f, 0.18f, 1.9f), Stone);
            for (int i = 0; i < 4; i++)
            {
                P(PrimitiveType.Cube, "Chip" + i, pillar, new Vector3((i - 1.5f) * 0.4f, 1.62f, 0.9f - (i % 2) * 1.8f), new Vector3(0.22f, 0.1f, 0.22f), Stone, Quaternion.Euler(0f, i * 31f, 8f));
            }

            e.p = new[] { g, armL, armR, jaw, pillar };
            return e;
        }

        // ---------------------------------------------------------------- SnakeStrike

        static SigEntity Snake(Transform parent)
        {
            SigEntity e = NewEntity(parent, ScareKind.SnakeStrike, new Color(0.65f, 1f, 0.5f), 6f, 10f, 2.4f, out Transform root);
            var parts = new List<Transform>();

            Transform head = Pivot("SnakeHead", root, Vector3.zero);
            P(PrimitiveType.Sphere, "Skull", head, new Vector3(0f, 0f, 0.02f), new Vector3(0.44f, 0.3f, 0.6f), Scale);
            P(PrimitiveType.Sphere, "Snout", head, new Vector3(0f, -0.03f, 0.3f), new Vector3(0.26f, 0.18f, 0.3f), Scale);
            P(PrimitiveType.Sphere, "BrowL", head, new Vector3(-0.13f, 0.1f, 0.15f), new Vector3(0.14f, 0.08f, 0.26f), Scale, Quaternion.Euler(0f, 12f, 0f));
            P(PrimitiveType.Sphere, "BrowR", head, new Vector3(0.13f, 0.1f, 0.15f), new Vector3(0.14f, 0.08f, 0.26f), Scale, Quaternion.Euler(0f, -12f, 0f));
            P(PrimitiveType.Sphere, "EyeL", head, new Vector3(-0.17f, 0.06f, 0.18f), new Vector3(0.1f, 0.1f, 0.09f), EyeYellow);
            P(PrimitiveType.Sphere, "EyeR", head, new Vector3(0.17f, 0.06f, 0.18f), new Vector3(0.1f, 0.1f, 0.09f), EyeYellow);
            P(PrimitiveType.Capsule, "PupilL", head, new Vector3(-0.2f, 0.06f, 0.2f), new Vector3(0.016f, 0.07f, 0.016f), Black);
            P(PrimitiveType.Capsule, "PupilR", head, new Vector3(0.2f, 0.06f, 0.2f), new Vector3(0.016f, 0.07f, 0.016f), Black);
            P(PrimitiveType.Sphere, "NostrilL", head, new Vector3(-0.05f, 0.02f, 0.44f), Vector3.one * 0.03f, Black);
            P(PrimitiveType.Sphere, "NostrilR", head, new Vector3(0.05f, 0.02f, 0.44f), Vector3.one * 0.03f, Black);
            P(PrimitiveType.Sphere, "Mouth", head, new Vector3(0f, -0.09f, 0.26f), new Vector3(0.28f, 0.08f, 0.36f), Mouth);
            Cone(head, "FangL", new Vector3(-0.075f, -0.09f, 0.34f), new Vector3(0f, -1f, -0.15f), 0.2f, 0.045f, Bone);
            Cone(head, "FangR", new Vector3(0.075f, -0.09f, 0.34f), new Vector3(0f, -1f, -0.15f), 0.2f, 0.045f, Bone);
            P(PrimitiveType.Sphere, "VenomL", head, new Vector3(-0.075f, -0.31f, 0.3f), new Vector3(0.025f, 0.05f, 0.025f), Venom);
            P(PrimitiveType.Sphere, "VenomR", head, new Vector3(0.075f, -0.29f, 0.3f), new Vector3(0.025f, 0.045f, 0.025f), Venom);
            for (int i = 0; i < 5; i++)
            {
                Cone(head, "TopTeeth" + i, new Vector3((i - 2f) * 0.045f, -0.09f, 0.2f + (i % 2) * 0.05f), Vector3.down, 0.05f, 0.016f, Bone);
            }

            Transform jaw = Pivot("Jaw", head, new Vector3(0f, -0.1f, 0.0f));
            P(PrimitiveType.Sphere, "JawBone", jaw, new Vector3(0f, -0.04f, 0.2f), new Vector3(0.32f, 0.08f, 0.5f), Belly);
            for (int i = 0; i < 5; i++)
            {
                Cone(jaw, "LowTeeth" + i, new Vector3((i - 2f) * 0.05f, -0.01f, 0.28f), Vector3.up, 0.055f, 0.016f, Bone);
            }

            Transform tongue = Pivot("Tongue", jaw, new Vector3(0f, 0.0f, 0.4f));
            P(PrimitiveType.Cube, "Tongue", tongue, new Vector3(0f, 0f, 0.15f), new Vector3(0.035f, 0.012f, 0.3f), Tongue);
            P(PrimitiveType.Cube, "ForkL", tongue, new Vector3(-0.028f, 0f, 0.33f), new Vector3(0.012f, 0.01f, 0.14f), Tongue, Quaternion.Euler(0f, 18f, 0f));
            P(PrimitiveType.Cube, "ForkR", tongue, new Vector3(0.028f, 0f, 0.33f), new Vector3(0.012f, 0.01f, 0.14f), Tongue, Quaternion.Euler(0f, -18f, 0f));

            // --- realism pass: crest spines, a scarred and a clouded eye, blood on the fangs, long strings of venom and a toxic glare
            for (int i = 0; i < 6; i++)
            {
                Cone(head, "Crest" + i, new Vector3((i % 2 == 0 ? -1f : 1f) * 0.09f, 0.14f, -0.05f + i * 0.06f), new Vector3(0f, 1f, -0.4f), 0.14f - 0.012f * i, 0.04f, Scale);
            }

            P(PrimitiveType.Sphere, "EyeScar", head, new Vector3(-0.18f, 0.1f, 0.2f), new Vector3(0.012f, 0.1f, 0.05f), Sinew, Quaternion.Euler(0f, 0f, 30f));
            P(PrimitiveType.Sphere, "EyeCloud", head, new Vector3(0.17f, 0.06f, 0.2f), Vector3.one * 0.05f, Cataract);
            P(PrimitiveType.Sphere, "FangBloodL", head, new Vector3(-0.075f, -0.22f, 0.32f), new Vector3(0.04f, 0.09f, 0.04f), BloodFresh);
            P(PrimitiveType.Sphere, "FangBloodR", head, new Vector3(0.075f, -0.22f, 0.32f), new Vector3(0.04f, 0.09f, 0.04f), BloodFresh);
            Drip(head, "VenomStringL", new Vector3(-0.075f, -0.3f, 0.3f), 0.22f, 0.014f, Venom);
            Drip(head, "VenomStringR", new Vector3(0.075f, -0.28f, 0.3f), 0.3f, 0.014f, Venom);
            EyeLight(head, new Vector3(0f, 0.08f, 0.3f), new Color(0.7f, 1f, 0.3f), 1.4f, 3f);

            // hood: scaled disc with a pale spectacle mark on the back of it (reads even at 480 px)
            Transform hood = Pivot("Hood", root, Vector3.zero);
            P(PrimitiveType.Sphere, "HoodDisc", hood, Vector3.zero, Vector3.one, Hood);
            P(PrimitiveType.Sphere, "SpotL", hood, new Vector3(-0.2f, 0.12f, 0.5f), new Vector3(0.2f, 0.24f, 0.3f), HoodMark);
            P(PrimitiveType.Sphere, "SpotR", hood, new Vector3(0.2f, 0.12f, 0.5f), new Vector3(0.2f, 0.24f, 0.3f), HoodMark);
            P(PrimitiveType.Sphere, "InkL", hood, new Vector3(-0.2f, 0.12f, 0.58f), new Vector3(0.1f, 0.14f, 0.2f), Black);
            P(PrimitiveType.Sphere, "InkR", hood, new Vector3(0.2f, 0.12f, 0.58f), new Vector3(0.1f, 0.14f, 0.2f), Black);
            P(PrimitiveType.Capsule, "Bridge", hood, new Vector3(0f, -0.1f, 0.55f), new Vector3(0.06f, 0.2f, 0.1f), Black, Quaternion.Euler(0f, 0f, 90f));

            Transform bulge = P(PrimitiveType.Sphere, "Bulge", root, Vector3.zero, Vector3.one * 0.46f, Belly).transform;

            Transform rider = Pivot("MouthRider", root, Vector3.zero);
            P(PrimitiveType.Capsule, "Body", rider, Vector3.zero, new Vector3(0.3f, 0.45f, 0.22f), RiderSuit);
            P(PrimitiveType.Sphere, "Head", rider, new Vector3(0f, 0.55f, 0f), Vector3.one * 0.26f, Pale);
            P(PrimitiveType.Capsule, "ArmL", rider, new Vector3(-0.22f, 0.2f, 0f), new Vector3(0.09f, 0.28f, 0.09f), RiderSuit, Quaternion.Euler(0f, 0f, 40f));
            P(PrimitiveType.Capsule, "ArmR", rider, new Vector3(0.22f, 0.2f, 0f), new Vector3(0.09f, 0.28f, 0.09f), RiderSuit, Quaternion.Euler(0f, 0f, -40f));

            parts.Add(head);
            parts.Add(hood);
            parts.Add(jaw);
            parts.Add(tongue);
            parts.Add(bulge);
            parts.Add(rider);
            for (int i = 0; i < SigPart.SnakeSegs; i++)
            {
                Material m = i % 8 < 2 ? Belly : Scale;
                parts.Add(P(PrimitiveType.Sphere, "Seg" + i, root, Vector3.zero, Vector3.one * 0.4f, m).transform);
            }

            e.p = parts.ToArray();
            return e;
        }

        // ---------------------------------------------------------------- SpikeWall

        static Transform SpikePanel(Transform root, string name, float[] rows, Transform[] spikes, int slot0, bool hanging)
        {
            Transform panel = Pivot(name, root, Vector3.zero);
            P(PrimitiveType.Cube, "Plate", panel, new Vector3(0f, 0.95f, -0.2f), new Vector3(3.8f, 2.6f, 0.35f), Rust);
            P(PrimitiveType.Cube, "FrameTop", panel, new Vector3(0f, 2.3f, -0.03f), new Vector3(3.95f, 0.14f, 0.3f), Steel);
            P(PrimitiveType.Cube, "FrameBottom", panel, new Vector3(0f, -0.35f, -0.03f), new Vector3(3.95f, 0.14f, 0.3f), Steel);
            P(PrimitiveType.Cube, "FrameL", panel, new Vector3(-1.95f, 0.95f, -0.03f), new Vector3(0.14f, 2.7f, 0.3f), Steel);
            P(PrimitiveType.Cube, "FrameR", panel, new Vector3(1.95f, 0.95f, -0.03f), new Vector3(0.14f, 2.7f, 0.3f), Steel);
            for (int i = 0; i < 4; i++)
            {
                float x = -1.5f + i * 1.0f;
                P(PrimitiveType.Sphere, "Lamp" + i, panel, new Vector3(x, 2.05f, 0.04f), Vector3.one * 0.18f, Warn);
                P(PrimitiveType.Cylinder, "Cage" + i, panel, new Vector3(x, 2.05f, 0.0f), new Vector3(0.26f, 0.05f, 0.26f), Steel, Quaternion.Euler(90f, 0f, 0f));
            }

            // bolts along the frame, a blood-stained plate, a skull ornament and hanging chains
            for (int i = 0; i < 8; i++)
            {
                P(PrimitiveType.Sphere, "BoltT" + i, panel, new Vector3(-1.75f + i * 0.5f, 2.3f, 0.14f), Vector3.one * 0.07f, Steel);
                P(PrimitiveType.Sphere, "BoltB" + i, panel, new Vector3(-1.75f + i * 0.5f, -0.35f, 0.14f), Vector3.one * 0.07f, Steel);
            }

            for (int i = 0; i < 6; i++)
            {
                float x = -1.6f + i * 0.62f + (i % 2) * 0.15f;
                float len = 0.5f + 0.35f * ((i * 5) % 4);
                P(PrimitiveType.Cube, "BloodStreak" + i, panel, new Vector3(x, 1.9f - len * 0.5f, 0.02f), new Vector3(0.05f + 0.03f * (i % 3), len, 0.02f), Blood);
            }

            P(PrimitiveType.Sphere, "Skull", panel, new Vector3(0f, 2.62f, 0.05f), new Vector3(0.34f, 0.4f, 0.32f), Bone);
            P(PrimitiveType.Sphere, "SkullSocketL", panel, new Vector3(-0.08f, 2.66f, 0.2f), new Vector3(0.1f, 0.12f, 0.06f), Black);
            P(PrimitiveType.Sphere, "SkullSocketR", panel, new Vector3(0.08f, 2.66f, 0.2f), new Vector3(0.1f, 0.12f, 0.06f), Black);
            if (hanging)
            {
                for (int i = 0; i < 2; i++)
                {
                    float x = i == 0 ? -1.7f : 1.7f;
                    for (int k = 0; k < 6; k++)
                    {
                        P(PrimitiveType.Sphere, "Link", panel, new Vector3(x, 2.1f - k * 0.18f, 0.2f), new Vector3(0.1f, 0.14f, 0.05f), Steel, Quaternion.Euler(0f, k % 2 * 90f, 0f));
                    }
                }
            }

            // --- realism pass: pooled and running blood, rust weeping from the bolts, scraps of torn clothing and bone snagged on the plate
            for (int i = 0; i < 8; i++)
            {
                float x = -1.7f + i * 0.5f;
                P(PrimitiveType.Cube, "RustRun" + i, panel, new Vector3(x, 2.0f - 0.25f * ((i * 3) % 3), 0.15f), new Vector3(0.025f, 0.5f + 0.2f * (i % 3), 0.012f), Rust);
            }

            P(PrimitiveType.Cylinder, "BloodPool", panel, new Vector3(0.3f, -0.34f, 0.5f), new Vector3(1.7f, 0.01f, 1.0f), BloodFresh);
            for (int i = 0; i < 5; i++)
            {
                float x = -1.2f + i * 0.6f;
                Drip(panel, "BloodRun" + i, new Vector3(x, 1.2f - 0.15f * (i % 3), 0.18f), 0.45f + 0.25f * ((i * 5) % 3), 0.03f, BloodFresh);
                P(PrimitiveType.Cube, "ClothScrap" + i, panel, new Vector3(x + 0.18f, 1.0f - 0.1f * (i % 2), 0.2f), new Vector3(0.14f, 0.22f, 0.012f), Rag, Quaternion.Euler(0f, 0f, 14f * (i - 2)));
            }

            P(PrimitiveType.Capsule, "BoneA", panel, new Vector3(-1.4f, 0.4f, 0.22f), new Vector3(0.05f, 0.22f, 0.05f), Bone, Quaternion.Euler(0f, 0f, 60f));
            P(PrimitiveType.Capsule, "BoneB", panel, new Vector3(1.3f, 0.8f, 0.22f), new Vector3(0.05f, 0.2f, 0.05f), Bone, Quaternion.Euler(0f, 0f, -35f));
            ActSetBuildKit.PointLight("WallGlare", panel, new Vector3(0f, 1.0f, 0.9f), new Color(1f, 0.15f, 0.08f), 1.8f, 5f);

            int n = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    Transform s = ActSetBuildKit.MeshObj("Spike", panel, _cone, new[] { Steel }, new Vector3(-1.0f + c * 0.5f, rows[r], 0f),
                        Quaternion.identity, new Vector3(0.16f, 0.16f, 0.03f)).transform;
                    spikes[slot0 + n] = s;
                    P(PrimitiveType.Cylinder, "Collar", panel, new Vector3(-1.0f + c * 0.5f, rows[r], 0.02f), new Vector3(0.26f, 0.03f, 0.26f), Steel, Quaternion.Euler(90f, 0f, 0f));
                    n++;
                }
            }

            return panel;
        }

        static SigEntity Spikes(Transform parent)
        {
            SigEntity e = NewEntity(parent, ScareKind.SpikeWall, new Color(1f, 0.25f, 0.12f), 7f, 9f, 3.4f, out Transform root);
            var all = new Transform[2 + 2 * SigPart.SpikeCount];
            all[SigPart.SpikeLeftPanel] = SpikePanel(root, "WallLeft", new[] { 0.55f, 0.9f, 1.25f }, all, SigPart.SpikeLeft0, true);
            all[SigPart.SpikeRightPanel] = SpikePanel(root, "WallRight", new[] { 1.0f, 1.3f, 1.6f }, all, SigPart.SpikeRight0, false);
            e.p = all;
            return e;
        }

        // ---------------------------------------------------------------- BatFlood

        static SigEntity BatFlood(Transform parent, Mesh bat)
        {
            SigEntity e = NewEntity(parent, ScareKind.BatFlood, new Color(1f, 0.6f, 0.25f), 6f, 22f, 2.8f, out Transform root);
            var parts = new Transform[SigPart.BatCount + 1];
            for (int i = 0; i < SigPart.BatCount; i++)
            {
                parts[i] = ActSetBuildKit.MeshObj("Bat" + i, root, bat, new[] { BatMat }, Vector3.zero, Quaternion.identity, Vector3.one * 0.4f).transform;
                parts[i].gameObject.SetActive(false);
            }

            parts[SigPart.BatMouth] = P(PrimitiveType.Sphere, "MouthGlow", root, Vector3.zero, new Vector3(7f, 5.5f, 0.1f), Mouthglow).transform;
            e.p = parts;
            return e;
        }
    }
}
