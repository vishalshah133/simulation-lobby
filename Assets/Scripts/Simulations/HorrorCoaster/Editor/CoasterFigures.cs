using System;
using System.Collections.Generic;
using SimulationLobby.Presentation.EditorTools;
using UnityEngine;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>The shared night look: named materials used by the first-half sets (cached per editor session).</summary>
    public static class CoasterLook
    {
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        static Material M(string key, Func<Material> make)
        {
            if (Cache.TryGetValue(key, out Material m) && m != null)
            {
                return m;
            }

            m = make();
            Cache[key] = m;
            return m;
        }

        static Color G(Color c, float k)
        {
            return CoasterPalette.Glow(c, k);
        }

        public static Material Bone => M("Bone", () => CoasterMaterials.Get("Bone", CoasterPalette.Bone * 0.9f, G(CoasterPalette.Bone, 0.3f), 0.25f));
        public static Material BoneGlow => M("BoneGlow", () => CoasterMaterials.Get("BoneGlow", CoasterPalette.Bone, G(CoasterPalette.Bone, 1.1f), 0.2f));
        public static Material GhostSheet => M("GhostSheet", () => CoasterMaterials.Get("GhostSheet", new Color(0.8f, 0.9f, 1f, 0.62f),
            G(new Color(0.6f, 0.85f, 1f), 1.3f), 0.4f, 0f, true));
        public static Material Pumpkin => M("PumpkinGlow", () => CoasterMaterials.Get("PumpkinGlow", CoasterPalette.Pumpkin * 0.7f, G(CoasterPalette.Pumpkin, 1.5f), 0.3f));
        public static Material PumpkinBody => M("PumpkinBody", () => CoasterMaterials.Get("PumpkinBody", CoasterPalette.Pumpkin * 0.75f, G(CoasterPalette.Pumpkin, 0.35f), 0.3f));
        public static Material Toxic => M("ToxicGlow", () => CoasterMaterials.Get("ToxicGlow", CoasterPalette.ToxicGreen * 0.5f, G(CoasterPalette.ToxicGreen, 2.6f), 0.3f));
        public static Material Blood => M("BloodGlow", () => CoasterMaterials.Get("BloodGlow", CoasterPalette.BloodRed, G(CoasterPalette.BloodRed, 1.2f), 0.3f));
        public static Material Black => M("Black", () => CoasterMaterials.Get("Black", new Color(0.015f, 0.015f, 0.025f), default, 0.1f));
        public static Material DarkWood => M("DarkWood", () => CoasterMaterials.Get("DarkWood", new Color(0.2f, 0.12f, 0.09f), default, 0.15f));
        public static Material Roof => M("Roof", () => CoasterMaterials.Get("Roof", new Color(0.1f, 0.08f, 0.14f), G(new Color(0.2f, 0.16f, 0.3f), 0.12f), 0.2f));
        public static Material Stone => M("Stone", () => CoasterMaterials.Get("Stone", new Color(0.36f, 0.38f, 0.48f), G(new Color(0.3f, 0.34f, 0.5f), 0.12f), 0.15f));
        public static Material Bark => M("Bark", () => CoasterMaterials.Get("Bark", new Color(0.12f, 0.1f, 0.12f), G(new Color(0.25f, 0.22f, 0.32f), 0.2f), 0.1f));
        public static Material Dirt => M("Dirt", () => CoasterMaterials.Get("Dirt", new Color(0.1f, 0.07f, 0.06f), default, 0.05f));
        public static Material Window => M("WindowWarm", () => CoasterMaterials.Get("WindowWarm", CoasterPalette.Lantern * 0.4f, G(CoasterPalette.Lantern, 1.8f), 0.3f));
        public static Material WindowGreen => M("WindowGreen", () => CoasterMaterials.Get("WindowGreen", CoasterPalette.ToxicGreen * 0.3f, G(CoasterPalette.ToxicGreen, 1.2f), 0.3f));
        public static Material LanternOn => M("LanternOn", () => CoasterMaterials.Get("LanternOn", CoasterPalette.Lantern * 0.5f, G(CoasterPalette.Lantern, 2.6f), 0.3f));
        public static Material LanternOff => M("LanternOff", () => CoasterMaterials.Get("LanternOff", new Color(0.12f, 0.1f, 0.08f), default, 0.3f));
        public static Material Web => M("Web", () => CoasterMaterials.Get("Web", new Color(0.9f, 0.92f, 1f, 0.3f), G(new Color(0.8f, 0.85f, 1f), 0.3f), 0.2f, 0f, true));
        public static Material Crow => M("Crow", () => CoasterMaterials.Get("Crow", new Color(0.14f, 0.11f, 0.2f), G(new Color(0.3f, 0.24f, 0.45f), 0.5f), 0.2f));
        public static Material Bat => M("Bat", () => CoasterMaterials.Get("Bat", new Color(0.16f, 0.1f, 0.24f), G(new Color(0.35f, 0.2f, 0.55f), 0.55f), 0.2f));
        public static Material Mist => M("Mist", () => CoasterMaterials.GetParticle("Mist", new Color(0.55f, 0.65f, 0.9f, 0.5f), false,
            GeneratedArt.SoftDot()));
        public static Material Dust => M("Dust", () => CoasterMaterials.GetParticle("Dust", new Color(1f, 0.7f, 0.4f, 1f), true,
            GeneratedArt.SoftDot()));
        public static Material Candy => M("Candy", () => CoasterMaterials.GetParticle("Candy", Color.white, false));
        public static Material TunnelWall => M("TunnelWall", () => CoasterMaterials.Get("TunnelWall", new Color(0.09f, 0.08f, 0.13f), G(new Color(0.2f, 0.16f, 0.3f), 0.15f), 0.15f));
        public static Material Ground => M("Ground", () => CoasterMaterials.Get("Ground", new Color(0.035f, 0.05f, 0.07f), default, 0.08f));

        static readonly Color[] WallColours =
        {
            new Color(0.30f, 0.16f, 0.32f), new Color(0.12f, 0.27f, 0.31f), new Color(0.36f, 0.17f, 0.10f),
            new Color(0.20f, 0.27f, 0.12f), new Color(0.18f, 0.18f, 0.42f)
        };

        public static Material Wall(int i)
        {
            int k = ((i % WallColours.Length) + WallColours.Length) % WallColours.Length;
            return M("Wall" + k, () => CoasterMaterials.Get("Wall" + k, WallColours[k], G(WallColours[k], 0.25f), 0.15f));
        }

        static readonly Color[] FlagColours = { new Color(1f, 0.48f, 0.09f), new Color(0.6f, 0.25f, 0.9f), new Color(0.49f, 1f, 0.31f), new Color(0.93f, 0.9f, 0.82f) };

        public static Material Flag(int i)
        {
            int k = ((i % FlagColours.Length) + FlagColours.Length) % FlagColours.Length;
            return M("Flag" + k, () => CoasterMaterials.Get("Flag" + k, FlagColours[k] * 0.6f, G(FlagColours[k], 1.0f), 0.2f));
        }

        public static Material Solid(string key, Color c, float emissive = 0f)
        {
            return M("S_" + key, () => CoasterMaterials.Get("S_" + key, c, emissive > 0f ? G(c, emissive) : default, 0.2f));
        }
    }

    /// <summary>Primitive-built characters and props used by the first-half sets. Everything is collider-free and faces local +Z.</summary>
    public static class CoasterFigures
    {
        public static GameObject Cone(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = CoasterMeshes.Cone;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Prism(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = CoasterMeshes.Prism;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Thin cylinder from <paramref name="a"/> to <paramref name="b"/> (parent-local), diameter <paramref name="thick"/>.</summary>
        public static GameObject Bar(Transform parent, string name, Vector3 a, Vector3 b, float thick, Material mat)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            Quaternion rot = len < 1e-4f ? Quaternion.identity : Quaternion.FromToRotation(Vector3.up, d / len);
            return CoasterKit.Local(PrimitiveType.Cylinder, parent, name, (a + b) * 0.5f, new Vector3(thick, len * 0.5f, thick), mat, rot);
        }

        public static GameObject PointLight(Transform parent, string name, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return go;
        }

        /// <summary>Hanging lantern: glowing bulb, cap, optional point light, sway and flicker.</summary>
        public static GameObject Lantern(Transform parent, string name, Vector3 worldPos, float size, bool withLight, int seed)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = worldPos;
            CoasterKit.Local(PrimitiveType.Sphere, pivot.transform, "Bulb", new Vector3(0f, -size * 0.55f, 0f), new Vector3(size * 0.8f, size, size * 0.8f), CoasterLook.LanternOn);
            CoasterKit.Local(PrimitiveType.Cylinder, pivot.transform, "Cap", new Vector3(0f, -size * 0.02f, 0f), new Vector3(size * 0.5f, size * 0.07f, size * 0.5f), CoasterLook.Black);
            CoasterKit.Local(PrimitiveType.Cylinder, pivot.transform, "Chain", new Vector3(0f, size * 0.6f, 0f), new Vector3(size * 0.05f, size * 0.55f, size * 0.05f), CoasterLook.Black);
            var sway = pivot.AddComponent<Sway>();
            sway.amplitudeDegrees = new Vector3(3f, 0f, 5f);
            sway.speed = 1.1f + 0.4f * CoasterKit.Hash(seed, 1);
            sway.phase = CoasterKit.Hash(seed, 2) * 6.28f;
            if (withLight)
            {
                GameObject lg = PointLight(pivot.transform, "Light", new Vector3(0f, -size * 0.55f, 0f), CoasterPalette.Lantern, 2.2f, 7f);
                var fl = pivot.AddComponent<FlickerLight>();
                fl.target = lg.GetComponent<Light>();
                fl.baseIntensity = 2.2f;
                fl.seed = seed;
            }

            return pivot;
        }

        /// <summary>Jack-o'-lantern: lit sphere with a carved face on its +Z side.</summary>
        public static GameObject Pumpkin(Transform parent, string name, Vector3 worldPos, Quaternion worldRot, float size, bool lit = true)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(worldPos, worldRot);
            CoasterKit.Local(PrimitiveType.Sphere, root.transform, "Body", Vector3.zero, new Vector3(size, size * 0.85f, size), lit ? CoasterLook.Pumpkin : CoasterLook.PumpkinBody);
            CoasterKit.Local(PrimitiveType.Cylinder, root.transform, "Stem", new Vector3(0f, size * 0.46f, 0f), new Vector3(size * 0.12f, size * 0.09f, size * 0.12f), CoasterLook.DarkWood);
            float z = size * 0.47f;
            Material dark = CoasterLook.Black;
            CoasterKit.Local(PrimitiveType.Cube, root.transform, "EyeL", new Vector3(-size * 0.18f, size * 0.1f, z), new Vector3(size * 0.16f, size * 0.16f, size * 0.05f), dark, Quaternion.Euler(0f, 0f, 45f));
            CoasterKit.Local(PrimitiveType.Cube, root.transform, "EyeR", new Vector3(size * 0.18f, size * 0.1f, z), new Vector3(size * 0.16f, size * 0.16f, size * 0.05f), dark, Quaternion.Euler(0f, 0f, 45f));
            CoasterKit.Local(PrimitiveType.Cube, root.transform, "Mouth", new Vector3(0f, -size * 0.18f, z - size * 0.02f), new Vector3(size * 0.55f, size * 0.14f, size * 0.05f), dark);
            return root;
        }

        public struct GhostParts
        {
            public GameObject root;
            public Transform mouth;
        }

        /// <summary>Sheet ghost: cone body, round head, dark eye holes and an open mouth. Origin at the feet, face on +Z.</summary>
        public static GhostParts Ghost(Transform parent, string name, Vector3 worldPos, Quaternion worldRot, float height, Material sheet)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(worldPos, worldRot);
            Transform t = root.transform;
            CoasterFigures.Cone(t, "Body", new Vector3(0f, 0f, 0f), new Vector3(height * 0.62f, height * 0.8f, height * 0.62f), sheet, Quaternion.identity);
            CoasterKit.Local(PrimitiveType.Sphere, t, "Head", new Vector3(0f, height * 0.76f, 0f), Vector3.one * (height * 0.36f), sheet);
            Material hole = CoasterLook.Black;
            CoasterKit.Local(PrimitiveType.Sphere, t, "EyeL", new Vector3(-height * 0.075f, height * 0.8f, height * 0.155f), new Vector3(height * 0.075f, height * 0.1f, height * 0.04f), hole);
            CoasterKit.Local(PrimitiveType.Sphere, t, "EyeR", new Vector3(height * 0.075f, height * 0.8f, height * 0.155f), new Vector3(height * 0.075f, height * 0.1f, height * 0.04f), hole);
            GameObject mouth = CoasterKit.Local(PrimitiveType.Sphere, t, "Mouth", new Vector3(0f, height * 0.7f, height * 0.16f), new Vector3(height * 0.09f, height * 0.12f, height * 0.04f), hole);
            CoasterFigures.Cone(t, "ArmL", new Vector3(-height * 0.3f, height * 0.5f, height * 0.05f), new Vector3(height * 0.12f, height * 0.4f, height * 0.12f), sheet, Quaternion.Euler(0f, 0f, 70f));
            CoasterFigures.Cone(t, "ArmR", new Vector3(height * 0.3f, height * 0.5f, height * 0.05f), new Vector3(height * 0.12f, height * 0.4f, height * 0.12f), sheet, Quaternion.Euler(0f, 0f, -70f));
            return new GhostParts { root = root, mouth = mouth.transform };
        }

        public struct BatParts
        {
            public GameObject root;
        }

        /// <summary>Bat with a flapping pair of wings and toxic-green eyes. Nose on +Z. Wingspan about <paramref name="span"/> metres.</summary>
        public static BatParts Bat(Transform parent, string name, Vector3 worldPos, float span, float flapSpeed, float phase)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            Transform t = root.transform;
            float s = span;
            CoasterKit.Local(PrimitiveType.Sphere, t, "Body", Vector3.zero, new Vector3(s * 0.16f, s * 0.16f, s * 0.26f), CoasterLook.Bat);
            CoasterKit.Local(PrimitiveType.Sphere, t, "Head", new Vector3(0f, s * 0.02f, s * 0.16f), Vector3.one * (s * 0.14f), CoasterLook.Bat);
            CoasterFigures.Cone(t, "EarL", new Vector3(-s * 0.04f, s * 0.09f, s * 0.16f), new Vector3(s * 0.04f, s * 0.08f, s * 0.04f), CoasterLook.Bat, Quaternion.identity);
            CoasterFigures.Cone(t, "EarR", new Vector3(s * 0.04f, s * 0.09f, s * 0.16f), new Vector3(s * 0.04f, s * 0.08f, s * 0.04f), CoasterLook.Bat, Quaternion.identity);
            CoasterKit.Local(PrimitiveType.Sphere, t, "EyeL", new Vector3(-s * 0.035f, s * 0.03f, s * 0.225f), Vector3.one * (s * 0.032f), CoasterLook.Toxic);
            CoasterKit.Local(PrimitiveType.Sphere, t, "EyeR", new Vector3(s * 0.035f, s * 0.03f, s * 0.225f), Vector3.one * (s * 0.032f), CoasterLook.Toxic);

            Transform wl = Wing(t, "WingL", -1f, s);
            Transform wr = Wing(t, "WingR", 1f, s);
            var flap = root.AddComponent<BatFlap>();
            flap.wingLeft = wl;
            flap.wingRight = wr;
            flap.speed = flapSpeed;
            flap.phase = phase;
            return new BatParts { root = root };
        }

        static Transform Wing(Transform body, string name, float side, float s)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(body, false);
            pivot.transform.localPosition = new Vector3(side * s * 0.07f, s * 0.02f, 0f);
            CoasterKit.Local(PrimitiveType.Cube, pivot.transform, "Membrane", new Vector3(side * s * 0.21f, 0f, -s * 0.02f), new Vector3(s * 0.42f, s * 0.012f, s * 0.24f), CoasterLook.Bat);
            CoasterKit.Local(PrimitiveType.Cube, pivot.transform, "Edge", new Vector3(side * s * 0.4f, 0f, s * 0.04f), new Vector3(s * 0.1f, s * 0.016f, s * 0.05f), CoasterLook.Pumpkin);
            return pivot.transform;
        }

        public struct CrowParts
        {
            public GameObject root;
            public Transform wingLeft;
            public Transform wingRight;
        }

        public static CrowParts Crow(Transform parent, string name, Vector3 worldPos, float span)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            Transform t = root.transform;
            float s = span;
            CoasterKit.Local(PrimitiveType.Sphere, t, "Body", Vector3.zero, new Vector3(s * 0.17f, s * 0.15f, s * 0.34f), CoasterLook.Crow);
            CoasterKit.Local(PrimitiveType.Sphere, t, "Head", new Vector3(0f, s * 0.04f, s * 0.2f), Vector3.one * (s * 0.13f), CoasterLook.Crow);
            CoasterFigures.Cone(t, "Beak", new Vector3(0f, s * 0.035f, s * 0.285f), new Vector3(s * 0.05f, s * 0.12f, s * 0.05f), CoasterLook.Bone, Quaternion.Euler(90f, 0f, 0f));
            CoasterKit.Local(PrimitiveType.Sphere, t, "EyeL", new Vector3(-s * 0.04f, s * 0.07f, s * 0.255f), Vector3.one * (s * 0.03f), CoasterLook.BoneGlow);
            CoasterKit.Local(PrimitiveType.Sphere, t, "EyeR", new Vector3(s * 0.04f, s * 0.07f, s * 0.255f), Vector3.one * (s * 0.03f), CoasterLook.BoneGlow);
            Transform wl = Wing(t, "WingL", -1f, s * 1.2f);
            Transform wr = Wing(t, "WingR", 1f, s * 1.2f);
            return new CrowParts { root = root, wingLeft = wl, wingRight = wr };
        }

        public struct KidParts
        {
            public GameObject root;
            public Transform armPivot;
            public Transform bucketPivot;
        }

        /// <summary>
        /// Costumed kid with a pumpkin bucket. Types: 0 ghost sheet, 1 witch, 2 skeleton, 3 pumpkin. Origin at the feet, facing +Z.
        /// The arm pivot (right shoulder) waves by rotating about X; the bucket pivot tips toward +Z at +X rotation.
        /// </summary>
        public static KidParts Kid(Transform parent, string name, Vector3 worldPos, Quaternion worldRot, int type, float scale)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(worldPos, worldRot);
            root.transform.localScale = Vector3.one * scale;
            Transform t = root.transform;
            Material skin = CoasterLook.Solid("KidSkin", new Color(0.75f, 0.6f, 0.5f), 0.1f);

            switch (type)
            {
                case 0:
                {
                    GhostParts g = Ghost(t, "Costume", Vector3.zero, Quaternion.identity, 1.1f, CoasterLook.BoneGlow);
                    g.root.transform.SetParent(t, false);
                    g.root.transform.localPosition = Vector3.zero;
                    g.root.transform.localRotation = Quaternion.identity;
                    break;
                }
                case 1:
                    CoasterKit.Local(PrimitiveType.Capsule, t, "Dress", new Vector3(0f, 0.45f, 0f), new Vector3(0.4f, 0.42f, 0.4f), CoasterLook.Solid("WitchDress", new Color(0.35f, 0.12f, 0.5f), 0.4f));
                    CoasterKit.Local(PrimitiveType.Sphere, t, "Head", new Vector3(0f, 1.0f, 0f), Vector3.one * 0.3f, CoasterLook.Solid("WitchSkin", new Color(0.4f, 0.8f, 0.4f), 0.35f));
                    CoasterKit.Local(PrimitiveType.Cylinder, t, "Brim", new Vector3(0f, 1.14f, 0f), new Vector3(0.5f, 0.015f, 0.5f), CoasterLook.Black);
                    CoasterFigures.Cone(t, "Hat", new Vector3(0f, 1.14f, 0f), new Vector3(0.3f, 0.42f, 0.3f), CoasterLook.Black, Quaternion.Euler(-6f, 0f, 0f));
                    CoasterKit.Local(PrimitiveType.Cylinder, t, "Band", new Vector3(0f, 1.18f, 0f), new Vector3(0.31f, 0.025f, 0.31f), CoasterLook.Pumpkin);
                    break;
                case 2:
                    CoasterKit.Local(PrimitiveType.Capsule, t, "Suit", new Vector3(0f, 0.45f, 0f), new Vector3(0.36f, 0.42f, 0.36f), CoasterLook.Solid("SkeleSuit", new Color(0.04f, 0.04f, 0.07f)));
                    for (int i = 0; i < 3; i++)
                    {
                        CoasterKit.Local(PrimitiveType.Cube, t, "Rib" + i, new Vector3(0f, 0.55f - i * 0.1f, 0.17f), new Vector3(0.3f - i * 0.04f, 0.035f, 0.03f), CoasterLook.BoneGlow);
                    }

                    CoasterKit.Local(PrimitiveType.Sphere, t, "Skull", new Vector3(0f, 1.0f, 0f), Vector3.one * 0.32f, CoasterLook.BoneGlow);
                    CoasterKit.Local(PrimitiveType.Sphere, t, "EyeL", new Vector3(-0.07f, 1.03f, 0.13f), Vector3.one * 0.08f, CoasterLook.Black);
                    CoasterKit.Local(PrimitiveType.Sphere, t, "EyeR", new Vector3(0.07f, 1.03f, 0.13f), Vector3.one * 0.08f, CoasterLook.Black);
                    break;
                default:
                    CoasterKit.Local(PrimitiveType.Capsule, t, "Suit", new Vector3(0f, 0.45f, 0f), new Vector3(0.4f, 0.42f, 0.4f), CoasterLook.Solid("PumpkinSuit", new Color(0.9f, 0.4f, 0.05f), 0.6f));
                    Pumpkin(t, "PumpkinHead", Vector3.zero, Quaternion.identity, 0.46f, true).transform.localPosition = new Vector3(0f, 1.0f, 0f);
                    break;
            }

            // Right arm (waves), a hand-held pumpkin bucket on the left.
            var arm = new GameObject("ArmPivot");
            arm.transform.SetParent(t, false);
            arm.transform.localPosition = new Vector3(0.2f, 0.78f, 0f);
            CoasterKit.Local(PrimitiveType.Capsule, arm.transform, "Arm", new Vector3(0f, -0.2f, 0f), new Vector3(0.09f, 0.21f, 0.09f), skin);
            arm.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

            var bucket = new GameObject("BucketPivot");
            bucket.transform.SetParent(t, false);
            bucket.transform.localPosition = new Vector3(-0.2f, 0.68f, 0.1f);
            CoasterKit.Local(PrimitiveType.Cylinder, bucket.transform, "Bucket", new Vector3(0f, 0f, 0.16f), new Vector3(0.24f, 0.1f, 0.24f), CoasterLook.Pumpkin);
            CoasterKit.Local(PrimitiveType.Cylinder, bucket.transform, "Mouth", new Vector3(0f, 0.095f, 0.16f), new Vector3(0.2f, 0.01f, 0.2f), CoasterLook.Black);
            CoasterKit.Local(PrimitiveType.Capsule, bucket.transform, "Arm", new Vector3(0f, -0.12f, 0.02f), new Vector3(0.09f, 0.14f, 0.09f), skin, Quaternion.Euler(-40f, 0f, 0f));

            return new KidParts { root = root, armPivot = arm.transform, bucketPivot = bucket.transform };
        }

        /// <summary>Bare tree: leaning trunk and a spray of bare branches. Origin on the ground.</summary>
        public static GameObject Tree(Transform parent, string name, Vector3 groundPos, float height, Vector3 leanDirection, float leanDegrees, int seed, int branches)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = groundPos;
            Quaternion lean = leanDirection.sqrMagnitude < 1e-4f
                ? Quaternion.identity
                : Quaternion.AngleAxis(leanDegrees, Vector3.Cross(Vector3.up, leanDirection.normalized));
            root.transform.rotation = lean;
            Transform t = root.transform;
            float thick = 0.28f + 0.22f * CoasterKit.Hash(seed, 1);
            CoasterKit.Local(PrimitiveType.Cylinder, t, "Trunk", new Vector3(0f, height * 0.5f, 0f), new Vector3(thick, height * 0.5f, thick), CoasterLook.Bark);
            CoasterKit.Local(PrimitiveType.Cylinder, t, "Base", new Vector3(0f, 0.3f, 0f), new Vector3(thick * 1.6f, 0.3f, thick * 1.6f), CoasterLook.Bark);
            for (int i = 0; i < branches; i++)
            {
                float k = (i + 1f) / (branches + 1f);
                float y = height * Mathf.Lerp(0.35f, 0.97f, k);
                float yaw = (CoasterKit.Hash(seed, 10 + i) * 360f);
                float pitch = Mathf.Lerp(35f, 70f, CoasterKit.Hash(seed, 30 + i));
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * (Quaternion.Euler(0f, 0f, -pitch) * Vector3.up);
                // dir: up tilted by pitch from vertical, then spun about Y. Tilting uses -Z rotation which leans toward +X.
                float len = Mathf.Lerp(1.6f, 3.8f, CoasterKit.Hash(seed, 50 + i)) * Mathf.Lerp(1.1f, 0.6f, k);
                Vector3 a = new Vector3(0f, y, 0f);
                Vector3 b = a + dir * len;
                Bar(t, "Branch" + i, a, b, thick * Mathf.Lerp(0.38f, 0.18f, k), CoasterLook.Bark);
                Vector3 b2 = b + (Quaternion.Euler(0f, yaw + 40f, 0f) * (Quaternion.Euler(0f, 0f, -pitch - 20f) * Vector3.up)) * (len * 0.5f);
                Bar(t, "Twig" + i, b, b2, thick * 0.12f, CoasterLook.Bark);
            }

            return root;
        }

        /// <summary>Crooked tombstone (round top) or cross. Origin at the ground, face on +Z.</summary>
        public static GameObject Tombstone(Transform parent, string name, Vector3 groundPos, Quaternion rot, float height, bool cross)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(groundPos, rot);
            Transform t = root.transform;
            if (cross)
            {
                CoasterKit.Local(PrimitiveType.Cube, t, "Post", new Vector3(0f, height * 0.5f, 0f), new Vector3(0.22f, height, 0.2f), CoasterLook.Stone);
                CoasterKit.Local(PrimitiveType.Cube, t, "Arm", new Vector3(0f, height * 0.72f, 0f), new Vector3(height * 0.6f, 0.2f, 0.2f), CoasterLook.Stone);
            }
            else
            {
                float w = height * 0.62f;
                CoasterKit.Local(PrimitiveType.Cube, t, "Slab", new Vector3(0f, height * 0.4f, 0f), new Vector3(w, height * 0.8f, 0.22f), CoasterLook.Stone);
                CoasterKit.Local(PrimitiveType.Cylinder, t, "Top", new Vector3(0f, height * 0.8f, 0f), new Vector3(w, 0.11f, w), CoasterLook.Stone, Quaternion.Euler(90f, 0f, 0f));
                CoasterKit.Local(PrimitiveType.Cube, t, "EngraveA", new Vector3(0f, height * 0.55f, 0.115f), new Vector3(w * 0.5f, 0.05f, 0.02f), CoasterLook.Black);
                CoasterKit.Local(PrimitiveType.Cube, t, "EngraveB", new Vector3(0f, height * 0.42f, 0.115f), new Vector3(0.05f, height * 0.3f, 0.02f), CoasterLook.Black);
            }

            return root;
        }

        public static GameObject Skeleton(Transform parent, string name, Vector3 worldPos, Quaternion worldRot, float height, out Transform head, out Transform armPivot)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(worldPos, worldRot);
            Transform t = root.transform;
            float h = height;
            CoasterKit.Local(PrimitiveType.Capsule, t, "Coat", new Vector3(0f, h * 0.5f, 0f), new Vector3(h * 0.26f, h * 0.28f, h * 0.2f), CoasterLook.Solid("OperatorCoat", new Color(0.07f, 0.04f, 0.09f), 0.3f));
            for (int i = 0; i < 4; i++)
            {
                CoasterKit.Local(PrimitiveType.Cube, t, "Rib" + i, new Vector3(0f, h * (0.62f - i * 0.05f), h * 0.1f), new Vector3(h * (0.2f - i * 0.025f), h * 0.014f, h * 0.012f), CoasterLook.BoneGlow);
            }

            var headGo = new GameObject("Head");
            headGo.transform.SetParent(t, false);
            headGo.transform.localPosition = new Vector3(0f, h * 0.84f, 0f);
            head = headGo.transform;
            CoasterKit.Local(PrimitiveType.Sphere, head, "Skull", Vector3.zero, Vector3.one * (h * 0.16f), CoasterLook.BoneGlow);
            CoasterKit.Local(PrimitiveType.Cube, head, "Jaw", new Vector3(0f, -h * 0.07f, h * 0.03f), new Vector3(h * 0.09f, h * 0.04f, h * 0.08f), CoasterLook.BoneGlow);
            CoasterKit.Local(PrimitiveType.Sphere, head, "EyeL", new Vector3(-h * 0.035f, h * 0.015f, h * 0.07f), Vector3.one * (h * 0.04f), CoasterLook.Black);
            CoasterKit.Local(PrimitiveType.Sphere, head, "EyeR", new Vector3(h * 0.035f, h * 0.015f, h * 0.07f), Vector3.one * (h * 0.04f), CoasterLook.Black);
            CoasterKit.Local(PrimitiveType.Sphere, head, "GlowL", new Vector3(-h * 0.035f, h * 0.015f, h * 0.09f), Vector3.one * (h * 0.016f), CoasterLook.Pumpkin);
            CoasterKit.Local(PrimitiveType.Sphere, head, "GlowR", new Vector3(h * 0.035f, h * 0.015f, h * 0.09f), Vector3.one * (h * 0.016f), CoasterLook.Pumpkin);
            CoasterKit.Local(PrimitiveType.Cylinder, head, "Brim", new Vector3(0f, h * 0.075f, 0f), new Vector3(h * 0.21f, h * 0.006f, h * 0.21f), CoasterLook.Black);
            CoasterKit.Local(PrimitiveType.Cylinder, head, "Hat", new Vector3(0f, h * 0.15f, 0f), new Vector3(h * 0.13f, h * 0.075f, h * 0.13f), CoasterLook.Black);
            CoasterKit.Local(PrimitiveType.Cylinder, head, "Band", new Vector3(0f, h * 0.095f, 0f), new Vector3(h * 0.135f, h * 0.012f, h * 0.135f), CoasterLook.Pumpkin);

            var arm = new GameObject("ArmPivot");
            arm.transform.SetParent(t, false);
            arm.transform.localPosition = new Vector3(h * 0.17f, h * 0.7f, 0f);
            CoasterKit.Local(PrimitiveType.Capsule, arm.transform, "Arm", new Vector3(0f, -h * 0.16f, h * 0.0f), new Vector3(h * 0.05f, h * 0.17f, h * 0.05f), CoasterLook.BoneGlow);
            CoasterKit.Local(PrimitiveType.Sphere, arm.transform, "Hand", new Vector3(0f, -h * 0.33f, 0f), Vector3.one * (h * 0.07f), CoasterLook.BoneGlow);
            armPivot = arm.transform;

            CoasterKit.Local(PrimitiveType.Capsule, t, "ArmL", new Vector3(-h * 0.19f, h * 0.55f, 0f), new Vector3(h * 0.05f, h * 0.17f, h * 0.05f), CoasterLook.BoneGlow);
            return root;
        }
    }
}
