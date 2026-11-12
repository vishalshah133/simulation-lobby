using UnityEngine;
using UnityEngine.Rendering;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// The ride-wide mood layer: candle clusters on the ground, floating candles beside the rail, jack-o-lanterns on posts and in piles, and
    /// a rolling ground mist that travels with the cart. Props sit in ~100 m chunks (the shared ChunkCuller switches far ones off);
    /// only a few clusters carry a real light. Skips the trippy and tightly walled acts (Trip, Tunnel, Asylum, Final) where props would clip.
    /// Everything is seeded by distance, so a rebuild is identical.
    /// </summary>
    public static class AtmosphereBuilder
    {
        static readonly ActId[] Outdoor = { ActId.ChainLift, ActId.TrickOrTreatStreet, ActId.Graveyard, ActId.DeadForest, ActId.Mansion };

        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            var root = new GameObject("Atmosphere");
            root.transform.SetParent(parent, false);
            var chunks = new CoasterSetKit.Chunker(root.transform, "Mood");
            float groundY = CoasterKit.GroundY(rail);

            int cluster = 0;
            foreach (ActId act in Outdoor)
            {
                float s = cfg.ActStart(act);
                float e = Mathf.Min(cfg.ActEnd(act), rail.Length);
                if (e <= s + 10f)
                {
                    continue;
                }

                for (float d = s + 6f; d < e - 4f; d += 11f + 9f * CoasterKit.Hash(cluster, 71))
                {
                    Transform c = chunks.At(d);
                    PlaceCluster(c, rail, d, groundY, cluster, act == ActId.Mansion);
                    cluster++;
                }
            }

            chunks.Finish(root);
            AddMist(root.transform);
            return root;
        }

        static void PlaceCluster(Transform chunk, ICoasterRail rail, float d, float groundY, int n, bool mansion)
        {
            CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 fwd, out Vector3 up, out Vector3 right);
            float side = CoasterKit.Hash(n, 72) > 0.5f ? 1f : -1f;
            float lat = side * (4.2f + 5.5f * CoasterKit.Hash(n, 73));
            Vector3 flatRight = new Vector3(right.x, 0f, right.z).normalized;
            Vector3 flat = new Vector3(p.x, groundY, p.z) + flatRight * lat;
            float railAbove = p.y - groundY;

            // 1) a ground cluster: pillar candles of mixed height, a wax pool, and a jack-o-lantern pile.
            var group = new GameObject("Candles and Pumpkins " + n);
            group.transform.SetParent(chunk, false);
            int candles = 3 + (int)(3f * CoasterKit.Hash(n, 74));
            for (int i = 0; i < candles; i++)
            {
                float a = CoasterKit.Hash(n * 13 + i, 75) * 6.28f;
                float r = 0.15f + 0.9f * CoasterKit.Hash(n * 13 + i, 76);
                Vector3 pos = flat + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float h = 0.18f + 0.55f * CoasterKit.Hash(n * 13 + i, 77);
                Candle(group.transform, pos, h, 0.07f + 0.05f * CoasterKit.Hash(n * 13 + i, 78), n * 13 + i, i == 0 && n % 3 == 0);
            }

            CoasterKit.Prim(PrimitiveType.Cylinder, group.transform, "Wax Pool", flat + Vector3.up * 0.01f, Quaternion.identity,
                new Vector3(1.4f, 0.01f, 1.4f), CoasterLook.Bone);

            if (n % 2 == 0)
            {
                float size = 0.5f + 0.55f * CoasterKit.Hash(n, 79);
                Quaternion face = Quaternion.LookRotation(-flatRight * side + new Vector3(fwd.x, 0f, fwd.z) * 0.4f, Vector3.up);
                CoasterFigures.Pumpkin(group.transform, "Pumpkin", flat + new Vector3(0.9f, size * 0.42f, 0.4f), face, size, true);
                if (n % 4 == 0)
                {
                    CoasterFigures.Pumpkin(group.transform, "Pumpkin Small", flat + new Vector3(-0.6f, size * 0.3f, 1.0f), face * Quaternion.Euler(0f, 25f, 0f), size * 0.6f, true);
                }
            }

            // 2) a jack-o-lantern on a post at roughly rail height (when the rail is low enough for it to read), facing the cart.
            if (n % 3 == 1 && railAbove < 14f && railAbove > 1.5f)
            {
                float postH = railAbove - 0.6f;
                CoasterKit.Prim(PrimitiveType.Cylinder, group.transform, "Post", flat + Vector3.up * (postH * 0.5f), Quaternion.identity,
                    new Vector3(0.18f, postH * 0.5f, 0.18f), CoasterLook.DarkWood);
                Quaternion face = Quaternion.LookRotation(-flatRight * side - new Vector3(fwd.x, 0f, fwd.z) * 0.5f, Vector3.up);
                GameObject pk = CoasterFigures.Pumpkin(group.transform, "Post Pumpkin", flat + Vector3.up * (postH + 0.38f), face, 0.8f, true);
                AddPumpkinLight(pk.transform, n);
            }

            // 3) floating candles hovering at eye height beside the rail, bobbing slowly.
            int floaters = mansion ? 5 : 3;
            for (int i = 0; i < floaters; i++)
            {
                float fl = side * (3.6f + 3.2f * CoasterKit.Hash(n * 7 + i, 80)) * (CoasterKit.Hash(n * 7 + i, 81) > 0.7f ? -1f : 1f);
                float fd = d + (CoasterKit.Hash(n * 7 + i, 82) - 0.5f) * 9f;
                CoasterKit.Frame(rail, Mathf.Clamp(fd, 0f, rail.Length), out Vector3 fp, out _, out Vector3 fu, out Vector3 fr);
                Vector3 fpos = fp + fr * fl + fu * (0.7f + 3.2f * CoasterKit.Hash(n * 7 + i, 83));
                var holder = new GameObject("Floating Candle");
                holder.transform.SetParent(group.transform, false);
                holder.transform.position = fpos;
                var sway = holder.AddComponent<Sway>();
                sway.amplitudeDegrees = new Vector3(5f, 0f, 5f);
                sway.speed = 0.7f + 0.5f * CoasterKit.Hash(n * 7 + i, 84);
                sway.phase = CoasterKit.Hash(n * 7 + i, 85) * 6.28f;
                Candle(holder.transform, fpos, 0.4f + 0.3f * CoasterKit.Hash(n * 7 + i, 86), 0.07f, n * 7 + i, i == 0 && n % 4 == 2);
            }
        }

        /// <summary>A pillar candle: wax body, a wax drip, a dark wick, a flickering flame; optionally a real flickering light.</summary>
        static void Candle(Transform parent, Vector3 basePos, float height, float radius, int seed, bool withLight)
        {
            var c = new GameObject("Candle");
            c.transform.SetParent(parent, false);
            c.transform.position = basePos;
            CoasterKit.Local(PrimitiveType.Cylinder, c.transform, "Wax", new Vector3(0f, height * 0.5f, 0f), new Vector3(radius * 2f, height * 0.5f, radius * 2f), CoasterLook.Bone);
            CoasterKit.Local(PrimitiveType.Capsule, c.transform, "Drip", new Vector3(radius * 0.95f, height * 0.78f, 0f), new Vector3(radius * 0.45f, height * 0.2f, radius * 0.45f), CoasterLook.Bone);
            CoasterKit.Local(PrimitiveType.Cylinder, c.transform, "Wick", new Vector3(0f, height + 0.015f, 0f), new Vector3(0.012f, 0.02f, 0.012f), CoasterLook.Black);
            GameObject flame = CoasterKit.Local(PrimitiveType.Sphere, c.transform, "Flame", new Vector3(0f, height + 0.075f, 0f), new Vector3(radius * 0.9f, radius * 2.4f, radius * 0.9f), CoasterLook.LanternOn);
            flame.AddComponent<FlameFlutter>().seed = seed;
            if (withLight)
            {
                GameObject lg = CoasterFigures.PointLight(c.transform, "Candle Light", new Vector3(0f, height + 0.15f, 0f), CoasterPalette.Lantern, 1.6f, 6.5f);
                var fl = c.AddComponent<FlickerLight>();
                fl.target = lg.GetComponent<Light>();
                fl.baseIntensity = 1.6f;
                fl.amount = 0.5f;
                fl.speed = 12f;
                fl.seed = seed;
            }
        }

        static void AddPumpkinLight(Transform pumpkin, int seed)
        {
            GameObject lg = CoasterFigures.PointLight(pumpkin, "Pumpkin Light", new Vector3(0f, 0f, 0.5f), CoasterPalette.Pumpkin, 2.4f, 8f);
            var fl = pumpkin.gameObject.AddComponent<FlickerLight>();
            fl.target = lg.GetComponent<Light>();
            fl.baseIntensity = 2.4f;
            fl.amount = 0.4f;
            fl.seed = seed + 500;
        }

        /// <summary>Slow rolling mist near the ground that travels with the cart, plus a thinner high haze: the depth cue that sells the dark.</summary>
        static void AddMist(Transform root)
        {
            Mist(root, "Ground Mist", new Vector3(0f, -1.6f, 26f), new Vector3(46f, 2.6f, 80f), new Vector2(7f, 13f), new Color(0.62f, 0.72f, 0.95f, 0.17f), 26f, 220);
            Mist(root, "Rail Haze", new Vector3(0f, 0.8f, 30f), new Vector3(30f, 4f, 90f), new Vector2(9f, 16f), new Color(0.5f, 0.55f, 0.85f, 0.07f), 10f, 90);
        }

        static void Mist(Transform root, string name, Vector3 offset, Vector3 box, Vector2 size, Color color, float rate, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<CartFollow>().offset = offset;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = 4411;
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
            main.startSpeed = 0.35f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = color;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = rate;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = box;
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 0.08f;
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = CoasterLook.Mist;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();
        }
    }
}
