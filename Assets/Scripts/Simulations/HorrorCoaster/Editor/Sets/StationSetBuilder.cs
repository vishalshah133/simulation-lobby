using System.Collections.Generic;
using TMPro;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>Small shared plumbing for the first-half set builders.</summary>
    internal static class CoasterSetKit
    {
        public static T NewSet<T>(Transform parent, string name, out Transform content) where T : CoasterSet
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            T set = root.AddComponent<T>();
            var c = new GameObject("Content");
            c.transform.SetParent(root.transform, false);
            set.content = c.transform;
            content = c.transform;
            return set;
        }

        /// <summary>World-space TextMeshPro decal (arena decal exception in CLAUDE.md), readable from behind <paramref name="forward"/>.</summary>
        public static TextMeshPro Text(Transform parent, string name, string text, Vector3 worldPos, Vector3 forward, float fontSize, Color color, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, CoasterKit.Look(forward, Vector3.up));
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.fontStyle = FontStyles.Bold;
            tmp.rectTransform.sizeDelta = size;
            return tmp;
        }

        /// <summary>
        /// Groups props into chunks of ~100 m of rail so the runtime ChunkCuller can switch far ones off (the rail is ~3.8 km). Chunks sit at the
        /// origin with unit scale, so world-space placement under them works. Call <see cref="Finish"/> once the set is built.
        /// </summary>
        public sealed class Chunker
        {
            readonly Transform parent;
            readonly float size;
            readonly string label;
            readonly SortedDictionary<int, Transform> map = new SortedDictionary<int, Transform>();

            public Chunker(Transform parent, string label, float size = 100f)
            {
                this.parent = parent;
                this.label = label;
                this.size = size;
            }

            public Transform At(float d)
            {
                int k = Mathf.Max(0, Mathf.FloorToInt(d / size));
                if (!map.TryGetValue(k, out Transform t))
                {
                    var go = new GameObject($"{label} {k * size:F0}-{(k + 1) * size:F0} m");
                    go.transform.SetParent(parent, false);
                    t = go.transform;
                    map[k] = t;
                }

                return t;
            }

            public void Finish(GameObject setRoot)
            {
                var list = new List<ChunkCuller.Entry>();
                foreach (KeyValuePair<int, Transform> kv in map)
                {
                    list.Add(new ChunkCuller.Entry { chunk = kv.Value, d0 = kv.Key * size, d1 = (kv.Key + 1) * size });
                }

                ChunkCuller culler = setRoot.GetComponent<ChunkCuller>();
                if (culler == null)
                {
                    culler = setRoot.AddComponent<ChunkCuller>();
                    culler.entries = list.ToArray();
                }
                else
                {
                    var merged = new List<ChunkCuller.Entry>(culler.entries);
                    merged.AddRange(list);
                    culler.entries = merged.ToArray();
                }
            }
        }

        /// <summary>Ground-level y for a rail position's x/z is the global ground.</summary>
        public static Vector3 OnGround(Vector3 p, float groundY)
        {
            return new Vector3(p.x, groundY, p.z);
        }

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }
    }

    /// <summary>Departure platform: canopy, operator and lever, hanging lanterns, a gate with the Halloween sign.</summary>
    public static class StationSetBuilder
    {
        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            StationSet set = CoasterSetKit.NewSet<StationSet>(parent, "Station", out Transform c);
            float groundY = CoasterKit.GroundY(rail);
            float d0 = Mathf.Max(0f, Mathf.Min(cfg.ActStart(ActId.Station), rail.Length));
            float d1 = Mathf.Min(d0 + 46f, Mathf.Min(cfg.ActEnd(ActId.Station), rail.Length));

            Material deck = CoasterMaterials.Get("StationDeck", new Color(0.16f, 0.13f, 0.17f), default, 0.2f);
            Material trim = CoasterLook.Pumpkin;
            Material post = CoasterMaterials.Get("StationPost", new Color(0.12f, 0.09f, 0.14f), default, 0.2f);

            // Platform decks on both sides, built in short flat segments that follow the rail.
            for (float d = d0; d < d1; d += 4f)
            {
                CoasterKit.Frame(rail, d + 2f, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
                Vector3 ff = CoasterSetKit.Flat(f);
                Vector3 rr = Vector3.Cross(Vector3.up, ff);
                Quaternion rot = Quaternion.LookRotation(ff, Vector3.up);
                float deckTop = p.y - 0.45f;
                float h = deckTop - groundY;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 center = p + rr * (side * 2.95f);
                    center.y = groundY + h * 0.5f;
                    CoasterKit.Prim(PrimitiveType.Cube, c, "Deck", center, rot, new Vector3(3.1f, h, 4.05f), deck);
                    Vector3 edge = p + rr * (side * 1.42f);
                    edge.y = deckTop + 0.02f;
                    CoasterKit.Prim(PrimitiveType.Cube, c, "EdgeGlow", edge, rot, new Vector3(0.1f, 0.04f, 4.05f), trim);
                }
            }

            // Canopy roof with posts every 8 m, a glowing fascia and lanterns underneath.
            int n = 0;
            for (float d = d0 + 4f; d < d1; d += 8f, n++)
            {
                CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
                Vector3 ff = CoasterSetKit.Flat(f);
                Vector3 rr = Vector3.Cross(Vector3.up, ff);
                Quaternion rot = Quaternion.LookRotation(ff, Vector3.up);
                float roofY = p.y + 4.3f;
                CoasterKit.Prim(PrimitiveType.Cube, c, "Roof", new Vector3(p.x, roofY, p.z), rot, new Vector3(9.4f, 0.22f, 8.1f), CoasterLook.Roof);
                CoasterKit.Prim(PrimitiveType.Cube, c, "Fascia", new Vector3(p.x, roofY - 0.1f, p.z) + ff * 4.06f, rot, new Vector3(9.4f, 0.12f, 0.06f), trim);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 pp = p + rr * (side * 4.2f);
                    float top = roofY;
                    float bottom = p.y - 0.45f;
                    CoasterKit.Prim(PrimitiveType.Cylinder, c, "Post", new Vector3(pp.x, (top + bottom) * 0.5f, pp.z), Quaternion.identity,
                                    new Vector3(0.28f, (top - bottom) * 0.5f, 0.28f), post);
                    CoasterFigures.Lantern(c, "Lantern", new Vector3(pp.x, roofY - 0.35f, pp.z) - rr * (side * 0.8f), 0.55f, n % 2 == 0 && side > 0, n * 2 + side);
                }

                CoasterFigures.Lantern(c, "CentreLantern", new Vector3(p.x, roofY - 0.3f, p.z), 0.6f, n % 3 == 0, 70 + n);
            }

            // Pumpkins along the platform edge.
            for (float d = d0 + 3f; d < d1 - 10f; d += 7.5f)
            {
                CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
                Vector3 ff = CoasterSetKit.Flat(f);
                Vector3 rr = Vector3.Cross(Vector3.up, ff);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 pos = p + rr * (side * 2.0f) + ff * (side * 1.2f);
                    pos.y = p.y - 0.45f + 0.24f;
                    CoasterFigures.Pumpkin(c, "Pumpkin", pos, Quaternion.LookRotation(-rr * side, Vector3.up), 0.5f);
                }
            }

            // Operator on the right platform beside the lever stand, facing the train.
            float opD = Mathf.Min(d0 + 12f, d1);
            CoasterKit.Frame(rail, opD, out Vector3 op, out Vector3 of, out _, out _);
            Vector3 oFlat = CoasterSetKit.Flat(of);
            Vector3 oRight = Vector3.Cross(Vector3.up, oFlat);
            Vector3 opPos = op + oRight * 2.5f;
            opPos.y = op.y - 0.45f;
            Quaternion face = Quaternion.LookRotation(-oRight, Vector3.up);
            CoasterFigures.Skeleton(c, "Operator", opPos, face, 1.85f, out Transform head, out Transform arm);
            arm.localRotation = Quaternion.Euler(-25f, 0f, 0f);

            // Lever stand in front of the operator (toward the track).
            var stand = CoasterKit.Empty(c, "Lever Stand", opPos + face * new Vector3(-0.38f, 0f, 0.62f));
            stand.transform.rotation = face;
            CoasterKit.Local(PrimitiveType.Cube, stand.transform, "Base", new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.9f, 0.36f), post);
            CoasterKit.Local(PrimitiveType.Cube, stand.transform, "Panel", new Vector3(0f, 0.92f, 0f), new Vector3(0.5f, 0.06f, 0.42f), trim);
            var leverPivot = new GameObject("Lever");
            leverPivot.transform.SetParent(stand.transform, false);
            leverPivot.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            CoasterKit.Local(PrimitiveType.Cylinder, leverPivot.transform, "Bar", new Vector3(0f, 0.32f, 0f), new Vector3(0.06f, 0.32f, 0.06f), CoasterLook.Black);
            CoasterKit.Local(PrimitiveType.Sphere, leverPivot.transform, "Knob", new Vector3(0f, 0.68f, 0f), Vector3.one * 0.16f, CoasterLook.Toxic);
            CoasterFigures.PointLight(stand.transform, "PanelGlow", new Vector3(0f, 1.3f, 0.2f), CoasterPalette.Pumpkin, 1.6f, 4.5f);

            // The gate with the sign, ahead of the departing cart.
            float gateD = Mathf.Min(d0 + 34f, d1);
            CoasterKit.Frame(rail, gateD, out Vector3 gp, out Vector3 gf, out _, out _);
            Vector3 gFlat = CoasterSetKit.Flat(gf);
            Vector3 gRight = Vector3.Cross(Vector3.up, gFlat);
            Quaternion gRot = Quaternion.LookRotation(gFlat, Vector3.up);
            float beamY = gp.y + 4.9f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 pp = gp + gRight * (side * 3.7f);
                CoasterKit.Prim(PrimitiveType.Cylinder, c, "GatePost", new Vector3(pp.x, (beamY + gp.y - 0.45f) * 0.5f, pp.z), Quaternion.identity,
                                new Vector3(0.45f, (beamY - gp.y + 0.45f) * 0.5f, 0.45f), post);
                CoasterFigures.Pumpkin(c, "GatePumpkin", pp + Vector3.up * (beamY - gp.y + 0.55f) - gFlat * 0.0f, Quaternion.LookRotation(-gFlat, Vector3.up), 0.9f);
            }

            Vector3 boardPos = new Vector3(gp.x, beamY + 0.1f, gp.z);
            CoasterKit.Prim(PrimitiveType.Cube, c, "SignBoard", boardPos, gRot, new Vector3(7.8f, 1.7f, 0.22f), CoasterLook.Black);
            CoasterKit.Prim(PrimitiveType.Cube, c, "SignTrimTop", boardPos + Vector3.up * 0.86f - gFlat * 0.02f, gRot, new Vector3(7.8f, 0.07f, 0.26f), trim);
            CoasterKit.Prim(PrimitiveType.Cube, c, "SignTrimBottom", boardPos - Vector3.up * 0.86f - gFlat * 0.02f, gRot, new Vector3(7.8f, 0.07f, 0.26f), trim);
            // The reader approaches from behind, so the text front faces back along the ride direction.
            CoasterSetKit.Text(c, "SignText", "HAUNTED COASTER", boardPos - gFlat * 0.14f, gFlat, 7.2f, CoasterPalette.Pumpkin, new Vector2(7.4f, 1.4f));
            CoasterSetKit.Text(c, "SignSub", "RIDE AT MIDNIGHT", boardPos - gFlat * 0.14f - Vector3.up * 0.66f, gFlat, 2.6f, CoasterPalette.Bone, new Vector2(7.4f, 0.6f));

            set.lever = leverPivot.transform;
            set.armPivot = arm;
            set.head = head;
            set.pullStartSeconds = Mathf.Max(1f, cfg.stationDwellSeconds - 2.6f);
            set.pullSeconds = 1.0f;

            CoasterKit.Frame(rail, Mathf.Min(d0 + 20f, d1), out Vector3 lp, out _, out _, out _);
            CoasterFigures.PointLight(c, "Platform Fill", lp + Vector3.up * 3.4f, CoasterPalette.Pumpkin, 2.4f, 16f);
            return set.gameObject;
        }
    }
}
