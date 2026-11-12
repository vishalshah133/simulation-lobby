using System;
using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Tunnel of Faces: a dark tube swept along the rail (saved as a fresh mesh), glowing wall strips, a toothed mouth, and carved
    /// jack-o'-lantern faces that light in sequence ahead of the cart. The last face (the TunnelFace event, behind the cart) is a bigger,
    /// dark carving that flares green, swells and turns to glare in the sting. Exposed as <see cref="TunnelRadius"/> for the supports skip.
    /// </summary>
    public static class TunnelSetBuilder
    {
        public const float TunnelRadius = 3.3f;
        const float CenterUp = 1.3f;

        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            TunnelSet set = CoasterSetKit.NewSet<TunnelSet>(parent, "TunnelOfFaces", out Transform c);
            float d0 = Mathf.Min(cfg.ActStart(ActId.TunnelOfFaces), rail.Length);
            float d1 = Mathf.Min(cfg.ActEnd(ActId.TunnelOfFaces), rail.Length);

            BuildShell(c, rail, d0, d1);
            BuildMouth(c, rail, d0 + 1.0f);

            // The scheduled final face sits in the LEFT wall just ahead of the sting point: dark until the sting, then it smashes at the lens.
            float finalD = d1 - 12f;
            if (cfg.events != null)
            {
                foreach (ScareEvent e in cfg.events)
                {
                    if (e.kind == ScareKind.TunnelFace)
                    {
                        finalD = e.distance + 6f;
                    }
                }
            }

            Material faceLit = CoasterMaterials.Get("TunnelFaceLit", CoasterPalette.Pumpkin * 0.25f, CoasterPalette.Glow(CoasterPalette.Pumpkin, 0.05f), 0.3f);
            Material skin = CoasterMaterials.Get("TunnelFaceSkin", CoasterPalette.Pumpkin * 0.45f, CoasterPalette.Glow(CoasterPalette.Pumpkin, 0.18f), 0.3f);
            Material skinDark = CoasterMaterials.Get("TunnelFaceSkinDark", CoasterPalette.Pumpkin * 0.22f, CoasterPalette.Glow(CoasterPalette.Pumpkin, 0.05f), 0.3f);

            var faces = new List<TunnelSet.Face>();
            int n = 0;
            for (float d = d0 + 14f; d < d1 - 6f; d += 7f, n++)
            {
                if (Mathf.Abs(d - finalD) < 6f)
                {
                    continue;
                }

                int side = n % 2 == 0 ? 1 : -1;
                var lit = new List<Renderer>();
                Transform root = BuildFace(c, rail, d, side, 1.0f, skin, faceLit, lit, n);
                Light light = null;
                if (n % 3 == 0)
                {
                    light = CoasterFigures.PointLight(root, "FaceLight", new Vector3(0f, 0f, 1.1f), CoasterPalette.Pumpkin, 0f, 10f).GetComponent<Light>();
                }

                faces.Add(new TunnelSet.Face { distance = d, lit = lit.ToArray(), light = light, lightBase = 4f });
            }

            var finalLit = new List<Renderer>();
            int fside = -1;
            Transform finalRoot = BuildFace(c, rail, finalD, fside, 1.4f, skinDark, faceLit, finalLit, 99);
            Light finalLight = CoasterFigures.PointLight(finalRoot, "FinalLight", new Vector3(0f, 0f, 1.4f), CoasterPalette.ToxicGreen, 0f, 12f).GetComponent<Light>();
            finalLight.enabled = false;
            // The tiny cue: two faint green pinpoints in the dark carving that flicker in the quiet.
            var cue = new GameObject("FinalCue");
            cue.transform.SetParent(finalRoot, false);
            cue.transform.localPosition = new Vector3(0f, 0.32f * 1.4f, 0.1f);
            CoasterKit.Local(PrimitiveType.Sphere, cue.transform, "PinL", new Vector3(-0.42f * 1.4f, 0f, 0f), new Vector3(0.12f, 0.08f, 0.05f), CoasterLook.Toxic);
            CoasterKit.Local(PrimitiveType.Sphere, cue.transform, "PinR", new Vector3(0.42f * 1.4f, 0f, 0f), new Vector3(0.12f, 0.08f, 0.05f), CoasterLook.Toxic);
            cue.SetActive(false);
            set.finalCue = cue.transform;

            set.faces = faces.ToArray();
            set.finalFace = finalRoot;
            set.finalLit = finalLit.ToArray();
            set.finalLight = finalLight;
            return set.gameObject;
        }

        static Transform BuildFace(Transform c, ICoasterRail rail, float d, int side, float radius, Material skin, Material litMat, List<Renderer> lit, int seed)
        {
            CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
            float v = 0.25f;
            float wall = Mathf.Sqrt(TunnelRadius * TunnelRadius - v * v) - 0.08f;
            Vector3 centre = p + u * CenterUp;
            Vector3 pos = centre + r * (side * wall) + u * v;
            var root = new GameObject("Face");
            root.transform.SetParent(c, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-r * side, u));
            Transform t = root.transform;
            float s = radius;

            CoasterKit.Local(PrimitiveType.Cylinder, t, "Disc", new Vector3(0f, 0f, 0.0f), new Vector3(2.1f * s, 0.07f * s, 2.1f * s), skin, Quaternion.Euler(90f, 0f, 0f));
            Material lm = litMat;
            Vector3 ez = new Vector3(0f, 0f, 0.07f * s);
            lit.Add(CoasterKit.Local(PrimitiveType.Cube, t, "EyeL", new Vector3(-0.42f * s, 0.32f * s, 0f) + ez, new Vector3(0.42f * s, 0.42f * s, 0.06f * s), lm, Quaternion.Euler(0f, 0f, 45f)).GetComponent<Renderer>());
            lit.Add(CoasterKit.Local(PrimitiveType.Cube, t, "EyeR", new Vector3(0.42f * s, 0.32f * s, 0f) + ez, new Vector3(0.42f * s, 0.42f * s, 0.06f * s), lm, Quaternion.Euler(0f, 0f, 45f)).GetComponent<Renderer>());
            lit.Add(CoasterKit.Local(PrimitiveType.Cube, t, "Nose", new Vector3(0f, 0.0f, 0f) + ez, new Vector3(0.2f * s, 0.2f * s, 0.06f * s), lm, Quaternion.Euler(0f, 0f, 45f)).GetComponent<Renderer>());
            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3) * 0.22f * s;
                float y = -0.5f * s - (i % 2 == 0 ? 0.06f : -0.06f) * s;
                lit.Add(CoasterKit.Local(PrimitiveType.Cube, t, "Tooth" + i, new Vector3(x, y, 0f) + ez, new Vector3(0.2f * s, 0.2f * s, 0.06f * s), lm,
                                         Quaternion.Euler(0f, 0f, 45f)).GetComponent<Renderer>());
            }

            return t;
        }

        static void BuildShell(Transform c, ICoasterRail rail, float d0, float d1)
        {
            var shell = new CoasterTrackBuilder.MeshData();
            var strips = new CoasterTrackBuilder.MeshData();
            var ribs = new CoasterTrackBuilder.MeshData();
            const int seg = 18;
            const float step = 2f;
            var samplePos = new List<Vector3>();
            var sampleUp = new List<Vector3>();
            var sampleRight = new List<Vector3>();

            var prev = new Vector3[seg + 1];
            bool havePrev = false;
            int rib = 0;
            for (float d = d0; d <= d1 + step * 0.5f; d += step)
            {
                CoasterKit.Frame(rail, Mathf.Min(d, rail.Length), out Vector3 p, out Vector3 fw, out Vector3 u, out Vector3 r);
                Vector3 centre = p + u * CenterUp;
                var ring = new Vector3[seg + 1];
                for (int k = 0; k <= seg; k++)
                {
                    float a = k * Mathf.PI * 2f / seg;
                    ring[k] = centre + (r * Mathf.Cos(a) + u * Mathf.Sin(a)) * TunnelRadius;
                }

                if (havePrev)
                {
                    for (int k = 0; k < seg; k++)
                    {
                        float am = (k + 0.5f) * Mathf.PI * 2f / seg;
                        Vector3 inward = -(r * Mathf.Cos(am) + u * Mathf.Sin(am));
                        shell.Quad(prev[k], prev[k + 1], ring[k + 1], ring[k], inward);
                    }
                }

                // A rib ring every 6 m: a raised band the cart sweeps through, so the speed of the tunnel is obvious.
                if (rib % 3 == 0)
                {
                    const float inner = TunnelRadius - 0.22f;
                    for (int k = 0; k < seg; k++)
                    {
                        float a0 = k * Mathf.PI * 2f / seg;
                        float a1 = (k + 1) * Mathf.PI * 2f / seg;
                        Vector3 o0 = centre + (r * Mathf.Cos(a0) + u * Mathf.Sin(a0)) * TunnelRadius;
                        Vector3 o1 = centre + (r * Mathf.Cos(a1) + u * Mathf.Sin(a1)) * TunnelRadius;
                        Vector3 i0 = centre + (r * Mathf.Cos(a0) + u * Mathf.Sin(a0)) * inner;
                        Vector3 i1 = centre + (r * Mathf.Cos(a1) + u * Mathf.Sin(a1)) * inner;
                        ribs.Quad(o0 - fw * 0.14f, o1 - fw * 0.14f, i1 - fw * 0.14f, i0 - fw * 0.14f, -fw);
                        ribs.Quad(i0 - fw * 0.14f, i1 - fw * 0.14f, i1 + fw * 0.14f, i0 + fw * 0.14f, -(r * Mathf.Cos(a0) + u * Mathf.Sin(a0)));
                    }
                }

                rib++;
                Array.Copy(ring, prev, ring.Length);
                havePrev = true;
                samplePos.Add(p);
                sampleUp.Add(u);
                sampleRight.Add(r);
            }

            // Two glowing strips low on the side walls, modest so the faces stay the stars.
            var pa = samplePos.ToArray();
            var ua = sampleUp.ToArray();
            var ra = sampleRight.ToArray();
            if (pa.Length > 1)
            {
                CoasterTrackBuilder.Sweep(strips, pa, ua, ra, -3.0f, 0.55f, 0.05f, 0.05f);
                CoasterTrackBuilder.Sweep(strips, pa, ua, ra, 3.0f, 0.55f, 0.05f, 0.05f);
            }

            Material wallMat = CoasterLook.TunnelWall;
            Material stripMat = CoasterMaterials.Get("TunnelStrip", CoasterPalette.Pumpkin * 0.3f, CoasterPalette.Glow(CoasterPalette.Pumpkin, 0.9f), 0.2f);
            AddMesh(c, "Shell", shell, "Coaster_TunnelShell", wallMat);
            AddMesh(c, "Wall Strips", strips, "Coaster_TunnelStrips", stripMat);
            Material ribMat = CoasterMaterials.Get("TunnelRib", CoasterPalette.Pumpkin * 0.18f, CoasterPalette.Glow(CoasterPalette.Pumpkin, 0.25f), 0.2f);
            AddMesh(c, "Ribs", ribs, "Coaster_TunnelRibs", ribMat);
        }

        static void AddMesh(Transform parent, string name, CoasterTrackBuilder.MeshData data, string assetName, Material mat)
        {
            Mesh mesh = CoasterTrackBuilder.SaveMeshFresh(data.ToMesh(), assetName);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void BuildMouth(Transform c, ICoasterRail rail, float d)
        {
            CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
            Vector3 centre = p + u * CenterUp;
            const int teeth = 16;
            for (int i = 0; i < teeth; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / teeth;
                Vector3 radial = r * Mathf.Cos(a) + u * Mathf.Sin(a);
                Vector3 pos = centre + radial * (TunnelRadius - 0.1f);
                var tooth = CoasterFigures.Cone(c, "Tooth", Vector3.zero, new Vector3(0.7f, 1.9f, 0.7f), CoasterLook.BoneGlow, Quaternion.FromToRotation(Vector3.up, -radial));
                tooth.transform.position = pos;
            }
        }
    }
}
