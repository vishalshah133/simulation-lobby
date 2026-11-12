using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Builds the final climb (lantern-lined lift into the giant jack-o'-lantern's open mouth) and the final drop, with the
    /// FakeEmptySeat draught props and the SkullFinale figure. Covers ActId.FinalClimb and ActId.FinalDrop.
    /// </summary>
    public static class FinalSetBuilder
    {
        /// <summary>Pumpkin radius in metres. The mouth is centred on the rail where the climb ends.</summary>
        public static float PumpkinRadius = 24f;

        const float Squash = 0.85f;
        const float MouthEl = -16f;   // elevation (deg) of the mouth centre

        static readonly Color Orange = new Color(1f, 0.48f, 0.09f);
        static readonly Color Bone = new Color(0.93f, 0.9f, 0.82f);
        static readonly Color Yellow = new Color(1f, 0.78f, 0.25f);
        static readonly Color Blood = new Color(0.8f, 0.04f, 0.06f);

        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            float climbStart = cfg.ActStart(ActId.FinalClimb);
            float top = cfg.ActEnd(ActId.FinalClimb);
            float end = cfg.ActEnd(ActId.FinalDrop);
            if (top <= climbStart + 1f) top = Mathf.Min(rail.Length, climbStart + 60f);
            if (end <= top + 1f) end = Mathf.Min(rail.Length, top + 60f);

            Material shell = ActSetBuildKit.M("Final_PumpkinShell", new Color(0.7f, 0.28f, 0.04f), ActSetBuildKit.Hdr(Orange, 0.8f), 0.4f);
            Material stem = ActSetBuildKit.M("Final_Stem", new Color(0.18f, 0.22f, 0.08f), ActSetBuildKit.Hdr(new Color(0.49f, 1f, 0.31f), 0.15f), 0.2f);
            Material tooth = ActSetBuildKit.M("Final_Tooth", Bone, ActSetBuildKit.Hdr(Bone, 0.8f), 0.3f);
            Material post = ActSetBuildKit.M("Final_Post", new Color(0.1f, 0.09f, 0.13f), default, 0.4f, 0.5f);
            Material lantern = ActSetBuildKit.M("Final_Lantern", Orange, ActSetBuildKit.Hdr(Orange, 2.0f), 0.2f);
            Material lampGlobe = ActSetBuildKit.M("Final_LampGlobe", Yellow, ActSetBuildKit.Hdr(Yellow, 2.0f), 0.2f);
            Material cord = ActSetBuildKit.M("Final_Cord", new Color(0.08f, 0.08f, 0.1f), default, 0.2f);

            GameObject root = ActSetBuildKit.Empty("Final Set", parent);
            var set = root.AddComponent<FinalSet>();
            set.startDist = climbStart;
            set.endDist = end;
            set.topDist = top;
            GameObject content = ActSetBuildKit.Empty("Content", root.transform);
            set.content = content;

            // ---- lantern posts along the lift
            var lanterns = new List<Transform>();
            int n = 0;
            for (float d = climbStart + 8f; d < top - 10f; d += 14f, n++)
            {
                ActSetBuildKit.Pose(rail, d, out Vector3 p, out Vector3 f, out Vector3 u);
                Vector3 right = ActSetBuildKit.RightOf(u, f);
                float side = (n % 2 == 0) ? -1f : 1f;
                Vector3 baseP = p + right * (side * 3.4f) + Vector3.down * 1.5f;
                ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Post", content.transform, baseP + Vector3.up * 2.0f, new Vector3(0.12f, 2.0f, 0.12f), post);
                Vector3 tip = baseP + Vector3.up * 4.0f - right * (side * 0.9f);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Arm", content.transform, baseP + Vector3.up * 4.0f - right * (side * 0.45f),
                    new Vector3(0.9f, 0.08f, 0.08f), post, Quaternion.LookRotation(f, Vector3.up));
                GameObject pivot = ActSetBuildKit.EmptyWorld("Lantern Pivot", content.transform, tip, Quaternion.identity);
                ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Lantern", pivot.transform, new Vector3(0f, -0.5f, 0f), new Vector3(0.32f, 0.25f, 0.32f), lantern);
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Cap", pivot.transform, new Vector3(0f, -0.2f, 0f), new Vector3(0.4f, 0.06f, 0.4f), post);
                lanterns.Add(pivot.transform);
            }
            set.lanterns = lanterns.ToArray();

            // ---- hanging lamp over the track at the FakeEmptySeat cue
            var seatEvs = ActSetBuildKit.EventsOf(cfg, ScareKind.FakeEmptySeat);
            float dLamp = seatEvs.Count > 0 ? seatEvs[0].ev.distance : top - 25f;
            if (seatEvs.Count > 0) set.draughtHint = seatEvs[0].index;
            {
                ActSetBuildKit.Pose(rail, dLamp, out Vector3 p, out Vector3 f, out Vector3 u);
                GameObject pivot = ActSetBuildKit.EmptyWorld("Hanging Lamp Pivot", content.transform, p + Vector3.up * 6f, Quaternion.identity);
                ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Cord", pivot.transform, new Vector3(0f, -1.5f, 0f), new Vector3(0.04f, 1.5f, 0.04f), cord);
                ActSetBuildKit.Prim(PrimitiveType.Sphere, "Globe", pivot.transform, new Vector3(0f, -3.3f, 0f), new Vector3(0.9f, 1.1f, 0.9f), lampGlobe);
                set.lampLight = ActSetBuildKit.PointLight("Lamp Light", pivot.transform, new Vector3(0f, -3.3f, 0f), new Color(1f, 0.7f, 0.35f), 2.5f, 12f);
                set.lampPivot = pivot.transform;
            }

            // ---- the giant jack-o'-lantern: the mouth is centred on the rail at the top of the climb
            float R = PumpkinRadius;
            ActSetBuildKit.Pose(rail, Mathf.Max(0f, top - 4f), out Vector3 pm, out Vector3 fm, out Vector3 um);
            Quaternion lv = ActSetBuildKit.Level(fm);
            float elM = MouthEl * Mathf.Deg2Rad;
            Vector3 centre = pm + Vector3.up * 1.0f + lv * new Vector3(0f, -R * Squash * Mathf.Sin(elM), R * Mathf.Cos(elM));
            Mesh pumpMesh = ActSetBuildKit.SaveMeshFresh(PumpkinShell(), "CoastFinal_Pumpkin");
            GameObject pump = ActSetBuildKit.MeshObj("Giant Pumpkin", content.transform, pumpMesh, new[] { shell }, centre, lv, Vector3.one * R);
            ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Stem", content.transform, centre + Vector3.up * (R * Squash + 1.2f), new Vector3(R * 0.14f, R * 0.12f, R * 0.14f), stem, Quaternion.Euler(0f, 0f, 8f));

            // teeth along the mouth edges
            for (int k = 0; k < 7; k++)
            {
                float az = (-21f + 7f * k) * Mathf.Deg2Rad;
                foreach (int topRow in new[] { 1, 0 })
                {
                    float el = (topRow == 1 ? -6f : -30f) * Mathf.Deg2Rad;
                    float shift = (topRow == 1 ? 1f : -1f) * ((k % 2 == 0) ? 0.5f : -0.5f) * 0.0f;
                    Vector3 local = new Vector3(R * Mathf.Sin(az) * Mathf.Cos(el), R * Squash * Mathf.Sin(el), -R * Mathf.Cos(az) * Mathf.Cos(el));
                    Vector3 nrm = lv * new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el) / Squash, -Mathf.Cos(az) * Mathf.Cos(el));
                    Vector3 pos = centre + lv * local + Vector3.up * (topRow == 1 ? -1.1f : 1.1f) + lv * new Vector3(0f, 0f, 0.8f + shift);
                    ActSetBuildKit.Prim(PrimitiveType.Cube, "Tooth", content.transform, pos, new Vector3(2.4f, 2.4f, 1.0f), tooth,
                        Quaternion.LookRotation(nrm.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, 45f));
                }
            }
            set.pumpkinLight = ActSetBuildKit.PointLight("Pumpkin Light", content.transform, Vector3.zero, new Color(1f, 0.55f, 0.15f), 4f, 80f);
            set.pumpkinLight.transform.position = centre + lv * new Vector3(0f, -3f, -6f);
            set.pumpkinLightBase = 4f;

            // ---- SkullFinale: the figure in the pumpkin, the red halo, and the chrome strip that mirrors it
            var skullEvs = ActSetBuildKit.EventsOf(cfg, ScareKind.SkullFinale);
            if (skullEvs.Count > 0)
            {
                float dS = skullEvs[0].ev.distance;
                set.skullHint = skullEvs[0].index;
                ActSetBuildKit.Pose(rail, Mathf.Min(rail.Length, dS + 11f), out Vector3 ps, out Vector3 fs, out Vector3 us);

                Material bone = ActSetBuildKit.M("Final_SkullBone", Bone * 0.9f, ActSetBuildKit.Hdr(Bone, 0.4f), 0.3f);
                Material black = ActSetBuildKit.M("Final_SkullBlack", new Color(0.01f, 0.01f, 0.02f), default, 0.2f);
                Material red = ActSetBuildKit.M("Final_SkullRed", Blood, ActSetBuildKit.Hdr(Blood, 2.2f), 0.2f);
                Material cloak = ActSetBuildKit.M("Final_Cloak", new Color(0.2f, 0.02f, 0.04f), ActSetBuildKit.Hdr(Blood, 0.3f), 0.2f);
                Material haloM = ActSetBuildKit.M("Final_Halo", new Color(0.6f, 0.03f, 0.05f), ActSetBuildKit.Hdr(Blood, 1.1f), 0.1f);
                Material chrome = ActSetBuildKit.M("Final_Chrome", new Color(0.85f, 0.85f, 0.88f), ActSetBuildKit.Hdr(Bone, 0.08f), 1f, 1f);

                GameObject skull = ActSetBuildKit.EmptyWorld("Skull Finale", content.transform, ps + Vector3.up * 2.6f, Quaternion.LookRotation(fs, Vector3.up));
                skull.transform.localScale = Vector3.one * 1.6f;
                set.jaw = BuildSkull(skull.transform, bone, black, red);
                ActSetBuildKit.Prim(PrimitiveType.Capsule, "Cloak", skull.transform, new Vector3(0f, -1.9f, -0.1f), new Vector3(1.5f, 1.5f, 0.9f), cloak);
                GameObject halo = ActSetBuildKit.Prim(PrimitiveType.Sphere, "Halo", skull.transform, new Vector3(0f, 0.1f, -0.9f), new Vector3(3.6f, 3.6f, 0.12f), haloM);
                set.skull = skull;
                set.halo = halo.transform;
                set.skullLight = ActSetBuildKit.PointLight("Skull Light", content.transform, skull.transform.position, new Color(1f, 0.15f, 0.1f), 3f, 9f);

                GameObject bar = ActSetBuildKit.Empty("Lap Bar Mirror", content.transform);
                ActSetBuildKit.Prim(PrimitiveType.Cylinder, "Bar", bar.transform, Vector3.zero, new Vector3(0.07f, 0.5f, 0.07f), chrome, Quaternion.Euler(0f, 0f, 90f));
                GameObject mini = ActSetBuildKit.Empty("Reflection", bar.transform, new Vector3(0f, 0.01f, -0.07f), Quaternion.Euler(0f, 180f, 0f));
                mini.transform.localScale = new Vector3(0.09f, -0.09f, 0.09f);
                BuildSkull(mini.transform, bone, black, red);
                set.lapBar = bar;
                set.miniSkull = mini.transform;
            }

            return root;
        }

        /// <summary>Skull centred on the origin, facing +Z, about 1 m wide. Returns the jaw hinge (rotate about X by +40 to open the mouth).</summary>
        static Transform BuildSkull(Transform t, Material bone, Material black, Material red)
        {
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Cranium", t, new Vector3(0f, 0.1f, 0f), new Vector3(1.0f, 1.1f, 1.0f), bone);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Mouth Cavity", t, new Vector3(0f, -0.38f, 0.22f), new Vector3(0.56f, 0.3f, 0.4f), black);
            for (int i = 0; i < 8; i++)
            {
                float x = -0.28f + 0.08f * i;
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Upper Tooth", t, new Vector3(x, -0.43f, 0.385f), new Vector3(0.065f, 0.12f, 0.06f), bone);
            }
            GameObject hinge = ActSetBuildKit.Empty("Jaw Hinge", t, new Vector3(0f, -0.15f, -0.1f), Quaternion.identity);
            ActSetBuildKit.Prim(PrimitiveType.Sphere, "Jaw", hinge.transform, new Vector3(0f, -0.30f, 0.18f), new Vector3(0.72f, 0.45f, 0.7f), bone);
            for (int i = 0; i < 8; i++)
            {
                float x = -0.28f + 0.08f * i;
                ActSetBuildKit.Prim(PrimitiveType.Cube, "Lower Tooth", hinge.transform, new Vector3(x, -0.30f, 0.485f), new Vector3(0.065f, 0.14f, 0.06f), bone);
            }
            for (int e = -1; e <= 1; e += 2)
            {
                ActSetBuildKit.Prim(PrimitiveType.Sphere, "Socket", t, new Vector3(e * 0.24f, 0.12f, 0.4f), new Vector3(0.3f, 0.34f, 0.2f), black);
                ActSetBuildKit.Prim(PrimitiveType.Sphere, "Ember", t, new Vector3(e * 0.24f, 0.1f, 0.5f), new Vector3(0.14f, 0.14f, 0.07f), red);
            }
            ActSetBuildKit.Prim(PrimitiveType.Cube, "Nose", t, new Vector3(0f, -0.12f, 0.48f), new Vector3(0.12f, 0.12f, 0.06f), black, Quaternion.Euler(0f, 0f, 45f));
            return hinge.transform;
        }

        // ---- procedural jack-o'-lantern shell: ribbed squashed sphere, eyes / nose / mouth cut out, bottom open, double-sided
        static Mesh PumpkinShell()
        {
            var mb = new MeshAcc();
            const int lon = 144, lat = 72;
            var outer = new int[lon + 1, lat + 1];
            var inner = new int[lon + 1, lat + 1];
            for (int i = 0; i <= lon; i++)
            {
                float phi = 2f * Mathf.PI * i / lon;
                for (int j = 0; j <= lat; j++)
                {
                    float el = -Mathf.PI * 0.5f + Mathf.PI * j / lat;
                    float rib = 1f + 0.05f * Mathf.Cos(10f * phi) * Mathf.Cos(el);
                    Vector3 dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(el), Mathf.Sin(el), -Mathf.Cos(phi) * Mathf.Cos(el));
                    Vector3 pos = dir * rib;
                    pos.y *= Squash;
                    outer[i, j] = mb.Add(pos, dir);
                    inner[i, j] = mb.Add(pos, -dir);
                }
            }
            for (int i = 0; i < lon; i++)
                for (int j = 0; j < lat; j++)
                {
                    float az = 360f * (i + 0.5f) / lon;
                    if (az > 180f) az -= 360f;
                    float el = -90f + 180f * (j + 0.5f) / lat;
                    if (el < -80f || InWindow(az, el)) continue;
                    int a = outer[i, j], b = outer[i + 1, j], c = outer[i, j + 1], d = outer[i + 1, j + 1];
                    mb.Tri(0, a, c, b);
                    mb.Tri(0, b, c, d);
                    a = inner[i, j]; b = inner[i + 1, j]; c = inner[i, j + 1]; d = inner[i + 1, j + 1];
                    mb.Tri(0, a, b, c);
                    mb.Tri(0, b, d, c);
                }
            return mb.ToMesh();
        }

        static bool InWindow(float az, float el)
        {
            if (Mathf.Abs(az) <= 24f && el >= -30f && el <= -6f) return true;            // mouth
            float dy = el - 20f;                                                          // eyes, apex up
            if (dy >= -8f && dy <= 8f)
            {
                float hw = 10f * (8f - dy) / 16f;
                if (Mathf.Abs(az - 27f) <= hw || Mathf.Abs(az + 27f) <= hw) return true;
            }
            float ny = el - 4f;                                                           // nose
            if (ny >= -3f && ny <= 4f && Mathf.Abs(az) <= 4f * (4f - ny) / 7f) return true;
            return false;
        }
    }
}
