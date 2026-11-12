using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Dead forest: gnarled trees crowding the banked curves (some branches arch over the rail), glowing eyes watching from the trunks,
    /// a few orange lantern pools, mist, and the CrowBurst flock (about 60 crows, hidden until the quiet).
    /// </summary>
    public static class DeadForestSetBuilder
    {
        public const int CrowCount = 72;

        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            DeadForestSet set = CoasterSetKit.NewSet<DeadForestSet>(parent, "DeadForest", out Transform c);
            float groundY = CoasterKit.GroundY(rail);
            float d0 = Mathf.Min(cfg.ActStart(ActId.DeadForest), rail.Length);
            float d1 = Mathf.Min(cfg.ActEnd(ActId.DeadForest), rail.Length);
            Material earth = CoasterMaterials.Get("ForestEarth", new Color(0.04f, 0.05f, 0.05f), CoasterPalette.Glow(new Color(0.15f, 0.12f, 0.2f), 0.06f), 0.05f);
            var chunks = new CoasterSetKit.Chunker(c, "Dressing");
            GraveyardSetBuilder.BuildTerrain(c, rail, Mathf.Max(0f, d0 - 10f), Mathf.Min(rail.Length, d1 + 10f), groundY, 22f, earth, chunks.At);

            var eyes = new List<Transform>();
            Material postMat = CoasterMaterials.Get("StationPost", new Color(0.12f, 0.09f, 0.14f), default, 0.2f);
            int n = 0;
            for (float d = d0; d < d1; d += 3.8f, n++)
            {
                Transform ck = chunks.At(d);
                CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
                Vector3 ff = CoasterSetKit.Flat(f);
                Vector3 rr = Vector3.Cross(Vector3.up, ff);
                float ty = GraveyardSetBuilder.TerrainY(rail, d, groundY);
                for (int side = -1; side <= 1; side += 2)
                {
                    if (CoasterKit.Hash(n, side + 31) < 0.12f)
                    {
                        continue;
                    }

                    float lat = 3.6f + 7f * CoasterKit.Hash(n, side + 33);
                    Vector3 pos = new Vector3(p.x, ty, p.z) + rr * (side * lat) + ff * ((CoasterKit.Hash(n, side + 35) - 0.5f) * 4f);
                    float height = 9f + 6f * CoasterKit.Hash(n, side + 37);
                    Vector3 toTrack = -rr * side;
                    int seed = n * 2 + side + 1000;
                    CoasterFigures.Tree(ck, "Tree", pos, height, toTrack, 8f + 8f * CoasterKit.Hash(n, side + 39), seed, 8);

                    // Eyes on the trunk, facing the track.
                    if (CoasterKit.Hash(n, side + 41) < 0.55f)
                    {
                        float ey = 1.3f + 2.2f * CoasterKit.Hash(n, side + 43);
                        Vector3 ep = pos + toTrack * 0.38f + Vector3.up * ey;
                        var pair = new GameObject("Eyes");
                        pair.transform.SetParent(ck, false);
                        pair.transform.SetPositionAndRotation(ep, Quaternion.LookRotation(toTrack, Vector3.up));
                        Material em = CoasterKit.Hash(n, side + 45) < 0.65f ? CoasterLook.Toxic : CoasterLook.Pumpkin;
                        float gap = 0.16f + 0.06f * CoasterKit.Hash(n, side + 47);
                        CoasterKit.Local(PrimitiveType.Sphere, pair.transform, "EyeL", new Vector3(-gap, 0f, 0f), new Vector3(0.2f, 0.12f, 0.1f), em, Quaternion.Euler(0f, 0f, 14f));
                        CoasterKit.Local(PrimitiveType.Sphere, pair.transform, "EyeR", new Vector3(gap, 0f, 0f), new Vector3(0.2f, 0.12f, 0.1f), em, Quaternion.Euler(0f, 0f, -14f));
                        eyes.Add(pair.transform);
                    }

                    // Some trunks throw a long branch over the rail: the forest closing in.
                    if (n % 2 == 0 && CoasterKit.Hash(n, side + 49) < 0.7f)
                    {
                        Vector3 from = pos + Vector3.up * (height * 0.72f) + toTrack * (height * 0.1f);
                        Vector3 to = new Vector3(p.x, p.y + 6.2f + 2.2f * CoasterKit.Hash(n, side + 51), p.z) + rr * (-side * (0.5f + 2.5f * CoasterKit.Hash(n, side + 53)));
                        CoasterFigures.Bar(ck, "ArchBranch", from, to, 0.2f, CoasterLook.Bark);
                        Vector3 mid = Vector3.Lerp(from, to, 0.6f);
                        CoasterFigures.Bar(ck, "ArchTwig", mid, mid + (Vector3.up * 1.2f + ff * ((CoasterKit.Hash(n, 5) - 0.5f) * 3f)), 0.07f, CoasterLook.Bark);
                    }
                }
            }

            // A few hanging lanterns for orange pools in the gloom.
            int ln = 0;
            for (float d = d0 + 12f; d < d1; d += 20f, ln++)
            {
                Transform ck = chunks.At(d);
                CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
                int side = ln % 2 == 0 ? 1 : -1;
                Vector3 pp = p + r * (side * 2.4f);
                CoasterFigures.Bar(ck, "LanternPost", new Vector3(pp.x, GraveyardSetBuilder.TerrainY(rail, d, groundY), pp.z), pp + u * 2.4f, 0.12f, postMat);
                CoasterFigures.Lantern(ck, "Lantern", pp + u * 2.35f - r * (side * 0.25f), 0.55f, ln % 2 == 0, ln + 600);
                // A web hanging over the rail between the lanterns: it sweeps past at head height and shows the speed.
                var web = GameObject.CreatePrimitive(PrimitiveType.Quad);
                web.name = "HangingWeb";
                Object.DestroyImmediate(web.GetComponent<Collider>());
                web.transform.SetParent(ck, false);
                web.transform.position = p + u * 3.0f - r * (side * 0.8f);
                web.transform.rotation = Quaternion.LookRotation(f, u) * Quaternion.Euler(0f, 0f, 20f * side);
                web.transform.localScale = new Vector3(2.6f, 2.6f, 1f);
                web.GetComponent<MeshRenderer>().sharedMaterial = CoasterLook.Web;
                web.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            for (float d = d0; d < d1; d += 60f)
            {
                CoasterKit.Frame(rail, Mathf.Min(rail.Length, d + 30f), out Vector3 p, out Vector3 f, out _, out _);
                GraveyardSetBuilder.MakeMist(chunks.At(d), new Vector3(p.x, GraveyardSetBuilder.TerrainY(rail, d, groundY) + 1f, p.z), CoasterSetKit.Flat(f), (int)(d * 0.1f) + 57);
            }

            // The flock, hidden until the quiet. The runtime places and animates each crow.
            var flock = new GameObject("Crows");
            flock.transform.SetParent(c, false);
            var crows = new List<DeadForestSet.Crow>();
            for (int i = 0; i < CrowCount; i++)
            {
                CoasterFigures.CrowParts cp = CoasterFigures.Crow(flock.transform, "Crow" + i, Vector3.zero, 1.25f);
                cp.root.SetActive(false);
                crows.Add(new DeadForestSet.Crow { root = cp.root.transform, wingLeft = cp.wingLeft, wingRight = cp.wingRight });
            }

            var flockLight = CoasterFigures.PointLight(c, "FlockLight", Vector3.zero, new Color(0.7f, 0.6f, 1f), 4f, 9f).GetComponent<Light>();
            flockLight.enabled = false;
            set.flockLight = flockLight;
            chunks.Finish(set.gameObject);
            set.eyePairs = eyes.ToArray();
            set.crows = crows.ToArray();
            return set.gameObject;
        }
    }
}
