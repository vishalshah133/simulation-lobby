using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Graveyard: raised earth along the track, crooked tombstones and crosses, bare trees, lantern posts, rolling mist, and the three
    /// scheduled entities (GraveHand, FakeLantern, GraveyardGhost). Terrain height follows the rail (rail y minus <see cref="TerrainDrop"/>).
    /// </summary>
    public static class GraveyardSetBuilder
    {
        public const float TerrainDrop = 2.0f;

        /// <summary>Earth strips along a stretch of rail so props stand on something even when the rail is high above the ground plane.</summary>
        public static void BuildTerrain(Transform parent, ICoasterRail rail, float d0, float d1, float groundY, float halfWidth, Material mat, System.Func<float, Transform> parentAt = null)
        {
            for (float d = d0; d < d1; d += 6f)
            {
                CoasterKit.Frame(rail, Mathf.Min(rail.Length, d + 3f), out Vector3 p, out Vector3 f, out _, out _);
                Vector3 ff = CoasterSetKit.Flat(f);
                float top = Mathf.Max(groundY + 0.05f, p.y - TerrainDrop);
                float h = top - groundY;
                CoasterKit.Prim(PrimitiveType.Cube, parentAt != null ? parentAt(d) : parent, "Earth", new Vector3(p.x, groundY + h * 0.5f, p.z), Quaternion.LookRotation(ff, Vector3.up),
                                new Vector3(halfWidth * 2f, h, 6.3f), mat);
            }
        }

        public static float TerrainY(ICoasterRail rail, float d, float groundY)
        {
            rail.Sample(Mathf.Clamp(d, 0f, rail.Length), out Vector3 p, out _, out _);
            return Mathf.Max(groundY + 0.05f, p.y - TerrainDrop);
        }

        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            GraveyardSet set = CoasterSetKit.NewSet<GraveyardSet>(parent, "Graveyard", out Transform c);
            float groundY = CoasterKit.GroundY(rail);
            float d0 = Mathf.Min(cfg.ActStart(ActId.Graveyard), rail.Length);
            float d1 = Mathf.Min(cfg.ActEnd(ActId.Graveyard), rail.Length);
            Material earth = CoasterMaterials.Get("GraveEarth", new Color(0.05f, 0.08f, 0.07f), CoasterPalette.Glow(new Color(0.1f, 0.2f, 0.15f), 0.08f), 0.05f);
            var chunks = new CoasterSetKit.Chunker(c, "Dressing");
            BuildTerrain(c, rail, d0 - 10f < 0f ? 0f : d0 - 10f, Mathf.Min(rail.Length, d1 + 10f), groundY, 24f, earth, chunks.At);

            // Tombstones and crosses, crooked, clustered near the track.
            int n = 0;
            for (float d = d0; d < d1; d += 3.2f, n++)
            {
                CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out _, out _);
                Transform ck = chunks.At(d);
                Vector3 ff = CoasterSetKit.Flat(f);
                Vector3 rr = Vector3.Cross(Vector3.up, ff);
                float ty = TerrainY(rail, d, groundY);
                for (int side = -1; side <= 1; side += 2)
                {
                    int count = 1 + (int)(CoasterKit.Hash(n, side + 3) * 2.99f);
                    for (int k = 0; k < count; k++)
                    {
                        float lat = 3.4f + 11f * Mathf.Pow(CoasterKit.Hash(n * 5 + k, side + 7), 1.4f);
                        float along = (CoasterKit.Hash(n * 5 + k, side + 9) - 0.5f) * 4f;
                        Vector3 pos = new Vector3(p.x, ty, p.z) + rr * (side * lat) + ff * along;
                        float yaw = Quaternion.LookRotation(-rr * side, Vector3.up).eulerAngles.y + (CoasterKit.Hash(n, k + 11) - 0.5f) * 50f;
                        Quaternion rot = Quaternion.Euler((CoasterKit.Hash(n, k + 13) - 0.5f) * 16f, yaw, (CoasterKit.Hash(n, k + 15) - 0.5f) * 24f);
                        bool cross = CoasterKit.Hash(n * 3 + k, 17) > 0.86f;
                        CoasterFigures.Tombstone(ck, cross ? "Cross" : "Tombstone", pos, rot, 0.9f + 0.9f * CoasterKit.Hash(n * 3 + k, 19), cross);
                    }
                }
            }

            // Bare trees farther out, taller and leaning toward the track.
            int tn = 0;
            for (float d = d0 + 6f; d < d1; d += 15f, tn++)
            {
                Transform ck = chunks.At(d);
                CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out _, out _);
                Vector3 ff = CoasterSetKit.Flat(f);
                Vector3 rr = Vector3.Cross(Vector3.up, ff);
                float ty = TerrainY(rail, d, groundY);
                for (int side = -1; side <= 1; side += 2)
                {
                    float lat = 9f + 8f * CoasterKit.Hash(tn, side + 21);
                    Vector3 pos = new Vector3(p.x, ty, p.z) + rr * (side * lat) + ff * (CoasterKit.Hash(tn, side + 23) - 0.5f) * 10f;
                    CoasterFigures.Tree(ck, "Tree", pos, 7f + 4f * CoasterKit.Hash(tn, side + 25), -rr * side, 7f, tn * 2 + side + 400, 6);
                }
            }

            // Lantern posts along the track (a real light on every other one).
            int ln = 0;
            Material postMat = CoasterMaterials.Get("StationPost", new Color(0.12f, 0.09f, 0.14f), default, 0.2f);
            for (float d = d0 + 10f; d < d1; d += 13f, ln++)
            {
                Transform ck = chunks.At(d);
                CoasterKit.Frame(rail, d, out Vector3 p, out _, out Vector3 u, out Vector3 r);
                int side = ln % 2 == 0 ? 1 : -1;
                Vector3 pp = p + r * (side * 2.5f);
                CoasterFigures.Bar(ck, "LanternPost", new Vector3(pp.x, TerrainY(rail, d, groundY), pp.z), pp + u * 2.2f, 0.12f, postMat);
                CoasterFigures.Lantern(ck, "Lantern", pp + u * 2.15f - r * (side * 0.25f), 0.55f, ln % 4 == 0, ln + 500);
            }

            // Iron picket fence along both sides and gate arches over the rail: close, regular things that sweep past and show the speed.
            Material iron = CoasterMaterials.Get("GraveIron", new Color(0.05f, 0.05f, 0.08f), CoasterPalette.Glow(new Color(0.2f, 0.2f, 0.3f), 0.1f), 0.4f);
            int fn = 0;
            for (float d = d0; d < d1; d += 2.4f, fn++)
            {
                CoasterKit.Frame(rail, d, out Vector3 p, out _, out Vector3 u, out Vector3 r);
                Transform ck = chunks.At(d);
                float ty = TerrainY(rail, d, groundY);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 pp = p + r * (side * 4.3f);
                    pp.y = ty;
                    float hgt = 1.3f + 0.35f * CoasterKit.Hash(fn, side + 61);
                    CoasterKit.Prim(PrimitiveType.Cylinder, ck, "Picket", pp + Vector3.up * (hgt * 0.5f), Quaternion.identity, new Vector3(0.07f, hgt * 0.5f, 0.07f), iron);
                    CoasterFigures.Cone(ck, "PicketTip", pp + Vector3.up * (hgt + 0.1f), new Vector3(0.12f, 0.24f, 0.12f), iron, Quaternion.identity);
                }
            }

            int an = 0;
            for (float d = d0 + 18f; d < d1; d += 36f, an++)
            {
                CoasterKit.Frame(rail, d, out Vector3 p, out _, out Vector3 u, out Vector3 r);
                Transform ck = chunks.At(d);
                float ty = TerrainY(rail, d, groundY);
                Vector3 l = p - r * 3.4f;
                Vector3 rr = p + r * 3.4f;
                CoasterFigures.Bar(ck, "GatePostL", new Vector3(l.x, ty, l.z), l + u * 4.2f, 0.18f, iron);
                CoasterFigures.Bar(ck, "GatePostR", new Vector3(rr.x, ty, rr.z), rr + u * 4.2f, 0.18f, iron);
                CoasterFigures.Bar(ck, "GateBeam", l + u * 4.2f, rr + u * 4.2f, 0.16f, iron);
                CoasterFigures.Bar(ck, "GateCurveL", l + u * 4.2f, p - r * 1.1f + u * 5.0f, 0.1f, iron);
                CoasterFigures.Bar(ck, "GateCurveR", rr + u * 4.2f, p + r * 1.1f + u * 5.0f, 0.1f, iron);
                CoasterFigures.Lantern(ck, "GateLantern", p + u * 3.7f, 0.45f, an % 2 == 0, 900 + an);
            }

            // Mist banks: big soft low particles that drift.
            for (float d = d0; d < d1; d += 60f)
            {
                CoasterKit.Frame(rail, Mathf.Min(rail.Length, d + 21f), out Vector3 p, out Vector3 f, out _, out _);
                MakeMist(chunks.At(d), new Vector3(p.x, TerrainY(rail, d, groundY) + 1.1f, p.z), CoasterSetKit.Flat(f), (int)(d * 0.1f) + 11);
            }

            // ---- scheduled entities (under the set content, not the dressing chunks)

            // GraveHand: bone hand with a proper palm, fingers up its local +Y. The runtime puts it in the viewer's own cart.
            var handRoot = new GameObject("GraveHand");
            handRoot.transform.SetParent(c, false);
            var handPivot = new GameObject("HandPivot");
            handPivot.transform.SetParent(handRoot.transform, false);
            Transform hp = handPivot.transform;
            CoasterFigures.Bar(hp, "Forearm", Vector3.zero, new Vector3(0f, 0.9f, 0f), 0.2f, CoasterLook.BoneGlow);
            CoasterKit.Local(PrimitiveType.Cube, hp, "Palm", new Vector3(0f, 1.0f, 0f), new Vector3(0.32f, 0.22f, 0.1f), CoasterLook.BoneGlow);
            CoasterKit.Local(PrimitiveType.Sphere, hp, "Wrist", new Vector3(0f, 0.9f, 0f), Vector3.one * 0.26f, CoasterLook.BoneGlow);
            CoasterKit.Local(PrimitiveType.Sphere, hp, "Thumb", new Vector3(0.2f, 0.98f, 0f), new Vector3(0.1f, 0.2f, 0.09f), CoasterLook.BoneGlow, Quaternion.Euler(0f, 0f, -35f));
            var fingers = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                var fp = new GameObject("Finger" + i);
                fp.transform.SetParent(hp, false);
                float x = (i - 1.5f) * 0.075f;
                fp.transform.localPosition = new Vector3(x, 1.1f, 0f);
                fp.transform.localRotation = Quaternion.Euler(0f, 0f, -(i - 1.5f) * 9f);
                float flen = i == 1 || i == 2 ? 0.4f : 0.32f;
                CoasterFigures.Bar(fp.transform, "Bone", Vector3.zero, new Vector3(0f, flen, 0f), 0.055f, CoasterLook.BoneGlow);
                CoasterKit.Local(PrimitiveType.Sphere, fp.transform, "Tip", new Vector3(0f, flen, 0f), Vector3.one * 0.07f, CoasterLook.BoneGlow);
                fingers.Add(fp.transform);
            }

            var glow = CoasterFigures.PointLight(handRoot.transform, "HandGlow", new Vector3(0f, 0.9f, 0.5f), CoasterPalette.ToxicGreen, 4f, 5f).GetComponent<Light>();
            glow.enabled = false;
            handRoot.SetActive(false);
            set.graveRoot = handRoot.transform;
            set.handPivot = hp;
            set.fingers = fingers.ToArray();
            set.graveGlow = glow;

            // FakeLantern: one big, obvious lantern beside the track that stutters, then goes out.
            var lanternEvents = FindEvents(cfg, ScareKind.FakeLantern);
            float lanternD = lanternEvents.Count > 0 ? lanternEvents[0].distance + 9f : d0 + (d1 - d0) * 0.5f;
            CoasterKit.Frame(rail, Mathf.Min(rail.Length, lanternD), out Vector3 lp, out Vector3 lf, out Vector3 lu, out Vector3 lr);
            Vector3 lpost = lp + lr * 2.3f;
            CoasterFigures.Bar(c, "FakeLanternPost", new Vector3(lpost.x, TerrainY(rail, lanternD, groundY), lpost.z), lpost + lu * 2.0f, 0.16f, postMat);
            GameObject big = CoasterFigures.Lantern(c, "FakeLantern", lpost + lu * 1.95f - lr * 0.3f, 0.95f, true, 777);
            set.lanternBulb = big.transform.Find("Bulb").GetComponent<MeshRenderer>();
            set.lanternOn = CoasterLook.LanternOn;
            set.lanternOff = CoasterLook.LanternOff;
            set.lanternLight = big.GetComponentInChildren<Light>();
            set.lanternFlicker = big.GetComponent<FlickerLight>();
            if (set.lanternLight != null)
            {
                set.lanternLight.intensity = 3.5f;
                set.lanternLight.range = 10f;
                set.lanternFlicker.baseIntensity = 3.5f;
            }

            // GraveyardGhost: a glowing sheet, hidden until the sting; its own light is ramped on at the lunge.
            CoasterFigures.GhostParts g = CoasterFigures.Ghost(c, "GraveyardGhost", Vector3.zero, Quaternion.identity, 2.1f, CoasterLook.GhostSheet);
            Light ghostLight = CoasterFigures.PointLight(g.root.transform, "GhostGlow", new Vector3(0f, 1.5f, 1.2f), new Color(0.6f, 0.85f, 1f), 5f, 7f).GetComponent<Light>();
            ghostLight.enabled = false;
            g.root.SetActive(false);
            set.ghost = g.root.transform;
            set.ghostMouth = g.mouth;
            set.ghostLight = ghostLight;

            // The wisp: a small cold light bobbing among the stones. The quiet-phase cue for the ghost.
            var wisp = new GameObject("Wisp");
            wisp.transform.SetParent(c, false);
            CoasterKit.Local(PrimitiveType.Sphere, wisp.transform, "Core", Vector3.zero, Vector3.one * 0.22f, CoasterLook.GhostSheet);
            Light wl = CoasterFigures.PointLight(wisp.transform, "WispLight", Vector3.zero, new Color(0.55f, 0.8f, 1f), 1.6f, 6f).GetComponent<Light>();
            wl.enabled = false;
            wisp.SetActive(false);
            set.wisp = wisp.transform;
            set.wispLight = wl;

            chunks.Finish(set.gameObject);
            return set.gameObject;
        }

        static List<ScareEvent> FindEvents(HorrorCoasterConfig cfg, ScareKind kind)
        {
            var list = new List<ScareEvent>();
            if (cfg.events != null)
            {
                foreach (ScareEvent e in cfg.events)
                {
                    if (e.kind == kind)
                    {
                        list.Add(e);
                    }
                }
            }

            list.Sort((a, b) => a.distance.CompareTo(b.distance));
            return list;
        }

        internal static void MakeMist(Transform parent, Vector3 pos, Vector3 along, int seed)
        {
            var go = new GameObject("Mist");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(along, Vector3.up));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = (uint)(900 + seed);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 20f;
            main.prewarm = true;
            main.startLifetime = 18f;
            main.startSpeed = 0.35f;
            main.startSize = new ParticleSystem.MinMaxCurve(8f, 15f);
            main.startColor = new Color(0.55f, 0.65f, 0.9f, 0.1f);
            main.maxParticles = 22;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 22f / 18f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 1.2f, 46f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.08f;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = CoasterLook.Mist;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }
    }
}
