using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Trick or Treat Street: a row of decorated houses on both sides, glowing windows, jack-o'-lanterns, cobwebs, bunting and kids.
    /// The scheduled TreatDoor / DoorGhoul events each get a dedicated house whose porch door swings open as the cart nears.
    /// Distances come from <c>cfg.events</c>. House local axes: +Z faces the street (the rail), +X along the rail, +Y up.
    /// </summary>
    public static class StreetSetBuilder
    {
        const float Lateral = 11f;   // house origin to rail centreline

        sealed class HouseOut
        {
            public Transform root;
            public Transform doorPivot;
            public Light doorwayLight;
            public Transform kidArm;
            public Transform bucketPivot;
            public Vector3 bucketWorld;
            public Transform ghoulRoot;
            public Transform ghoulMouth;
            public Light ghoulLight;
            public Transform cueEyes;
        }

        public static GameObject Build(Transform parent, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            StreetSet set = CoasterSetKit.NewSet<StreetSet>(parent, "TrickOrTreatStreet", out Transform c);
            float groundY = CoasterKit.GroundY(rail);
            float d0 = Mathf.Min(cfg.ActStart(ActId.TrickOrTreatStreet), rail.Length);
            float d1 = Mathf.Min(cfg.ActEnd(ActId.TrickOrTreatStreet), rail.Length);
            var chunks = new CoasterSetKit.Chunker(c, "Houses");

            var doorEvents = new List<ScareEvent>();
            if (cfg.events != null)
            {
                foreach (ScareEvent e in cfg.events)
                {
                    if (e.kind == ScareKind.TreatDoor || e.kind == ScareKind.DoorGhoul)
                    {
                        doorEvents.Add(e);
                    }
                }
            }

            doorEvents.Sort((a, b) => a.distance.CompareTo(b.distance));

            var rigs = new List<StreetSet.DoorRig>();
            var wavers = new List<Transform>();
            var eventHouses = new List<(float d, int side)>();

            for (int i = 0; i < doorEvents.Count; i++)
            {
                ScareEvent ev = doorEvents[i];
                int side = i % 2 == 0 ? 1 : -1;
                float houseD = Mathf.Min(rail.Length, ev.distance + 8f);
                HouseOut h = BuildHouse(c, chunks.At(houseD), rail, houseD, side, 100 + i, groundY, true, ev.kind == ScareKind.DoorGhoul, ev.kind == ScareKind.TreatDoor, i);
                eventHouses.Add((houseD, side));

                var rig = new StreetSet.DoorRig
                {
                    kind = ev.kind,
                    eventDistance = ev.distance,
                    doorPivot = h.doorPivot,
                    openDegrees = -105f,
                    doorwayLight = h.doorwayLight,
                    kidArm = h.kidArm,
                    bucketPivot = h.bucketPivot,
                    ghoulRoot = h.ghoulRoot,
                    ghoulMouth = h.ghoulMouth,
                    ghoulLight = h.ghoulLight,
                    cueEyes = h.cueEyes
                };

                if (ev.kind == ScareKind.TreatDoor)
                {
                    // Candy flies from the bucket to a point just ahead of the cart's position at the event.
                    CoasterKit.Frame(rail, Mathf.Min(rail.Length, ev.distance + 2.5f), out Vector3 tp, out _, out Vector3 tu, out _);
                    rig.candy = MakeCandy(c, h.bucketWorld, tp + tu * 0.8f, i);
                }

                rigs.Add(rig);
            }

            // Everyday houses fill the rest of the street, skipping the ones next to an event house.
            float span = Mathf.Max(30f, d1 - d0);
            float spacing = 9f;
            int count = Mathf.Max(1, Mathf.FloorToInt((span - 8f) / spacing));
            BuildStreetFurniture(c, chunks, rail, d0, d1, groundY);
            int idx = 0;
            for (int k = 0; k < count; k++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float d = d0 + 6f + k * spacing + (side > 0 ? 0f : spacing * 0.5f);
                    if (d > d1 - 3f)
                    {
                        continue;
                    }

                    bool blocked = false;
                    foreach (var eh in eventHouses)
                    {
                        if (eh.side == side && Mathf.Abs(eh.d - d) < spacing * 0.8f)
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (blocked)
                    {
                        continue;
                    }

                    HouseOut h = BuildHouse(c, chunks.At(d), rail, d, side, idx, groundY, false, false, false, idx);
                    if (h.kidArm != null)
                    {
                        wavers.Add(h.kidArm);
                    }

                    idx++;
                }
            }

            chunks.Finish(set.gameObject);
            set.doors = rigs.ToArray();
            set.wavingArms = wavers.ToArray();
            return set.gameObject;
        }

        static HouseOut BuildHouse(Transform content, Transform c, ICoasterRail rail, float d, int side, int idx, float groundY, bool isEvent, bool ghoulDoor, bool treatDoor, int seed)
        {
            CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
            Vector3 ff = CoasterSetKit.Flat(f);
            Vector3 rr = Vector3.Cross(Vector3.up, ff);
            float deckY = p.y - 0.2f;
            Vector3 origin = new Vector3(p.x, deckY, p.z) + rr * (side * Lateral);
            Quaternion rot = Quaternion.LookRotation(-rr * side, Vector3.up);

            var houseGo = new GameObject(isEvent ? "EventHouse" : "House");
            houseGo.transform.SetParent(c, false);
            houseGo.transform.SetPositionAndRotation(origin, rot);
            Transform t = houseGo.transform;
            var o = new HouseOut { root = t };

            Material wall = CoasterLook.Wall(idx + (side > 0 ? 0 : 2));
            Material trimWood = CoasterLook.DarkWood;
            Material black = CoasterLook.Black;

            // Foundation down to the ground, body, roof, chimney.
            float found = Mathf.Max(0.2f, deckY - groundY);
            CoasterKit.Local(PrimitiveType.Cube, t, "Plinth", new Vector3(0f, -found * 0.5f - 0.02f, -0.5f), new Vector3(6.2f, found, 7.4f), CoasterLook.Roof);
            CoasterKit.Local(PrimitiveType.Cube, t, "Body", new Vector3(0f, 2.2f, -0.7f), new Vector3(5.4f, 4.4f, 4.4f), wall);
            CoasterFigures.Prism(t, "Roof", new Vector3(0f, 4.4f, -0.7f), new Vector3(5.9f, 2.4f, 4.9f), CoasterLook.Roof);
            CoasterKit.Local(PrimitiveType.Cube, t, "Chimney", new Vector3(1.7f, 5.3f, -1.6f), new Vector3(0.6f, 1.8f, 0.6f), CoasterLook.Roof);
            CoasterKit.Local(PrimitiveType.Cube, t, "Porch", new Vector3(0f, 0f, 2.35f), new Vector3(3.6f, 0.06f, 1.7f), trimWood);
            CoasterKit.Local(PrimitiveType.Cube, t, "Stoop", new Vector3(0f, -0.25f, 3.45f), new Vector3(2.2f, 0.5f, 0.5f), trimWood);
            CoasterKit.Local(PrimitiveType.Cube, t, "PorchRoof", new Vector3(0f, 2.75f, 2.45f), new Vector3(3.9f, 0.14f, 1.9f), CoasterLook.Roof);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                CoasterKit.Local(PrimitiveType.Cylinder, t, "PorchPost", new Vector3(sx * 1.75f, 1.37f, 3.2f), new Vector3(0.14f, 1.37f, 0.14f), trimWood);
            }

            // Door: glowing doorway behind a wooden leaf that swings out on a hinge.
            CoasterKit.Local(PrimitiveType.Cube, t, "Doorway", new Vector3(0f, 1.05f, 1.53f), new Vector3(1.0f, 2.1f, 0.04f), isEvent ? CoasterLook.Pumpkin : CoasterLook.Window);
            CoasterKit.Local(PrimitiveType.Cube, t, "FrameL", new Vector3(-0.58f, 1.1f, 1.55f), new Vector3(0.14f, 2.25f, 0.1f), trimWood);
            CoasterKit.Local(PrimitiveType.Cube, t, "FrameR", new Vector3(0.58f, 1.1f, 1.55f), new Vector3(0.14f, 2.25f, 0.1f), trimWood);
            CoasterKit.Local(PrimitiveType.Cube, t, "FrameTop", new Vector3(0f, 2.2f, 1.55f), new Vector3(1.3f, 0.14f, 0.1f), trimWood);
            var hinge = new GameObject("DoorHinge");
            hinge.transform.SetParent(t, false);
            hinge.transform.localPosition = new Vector3(-0.5f, 0f, 1.64f);
            CoasterKit.Local(PrimitiveType.Cube, hinge.transform, "Leaf", new Vector3(0.5f, 1.05f, 0f), new Vector3(1.0f, 2.1f, 0.07f), CoasterLook.Solid("DoorLeaf", new Color(0.3f, 0.17f, 0.1f), 0.1f));
            CoasterKit.Local(PrimitiveType.Sphere, hinge.transform, "Knob", new Vector3(0.88f, 1.0f, 0.07f), Vector3.one * 0.1f, CoasterLook.LanternOn);
            CoasterKit.Local(PrimitiveType.Sphere, hinge.transform, "DoorEmblem", new Vector3(0.5f, 1.55f, 0.06f), new Vector3(0.28f, 0.24f, 0.04f), CoasterLook.Pumpkin);
            o.doorPivot = hinge.transform;

            // Windows with muntins, an attic window, bunting, jack-o'-lanterns, cobwebs, a porch lantern.
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int row = 0; row < 2; row++)
                {
                    float wy = row == 0 ? 1.55f : 3.4f;
                    Material wm = CoasterKit.Hash(idx * 5 + row, sx + 4) > 0.88f ? CoasterLook.WindowGreen : CoasterLook.Window;
                    Vector3 wp = new Vector3(sx * 1.75f, wy, 1.53f);
                    CoasterKit.Local(PrimitiveType.Cube, t, "Window", wp, new Vector3(0.85f, 0.95f, 0.05f), wm);
                    CoasterKit.Local(PrimitiveType.Cube, t, "MuntinV", wp + new Vector3(0f, 0f, 0.03f), new Vector3(0.05f, 0.95f, 0.03f), black);
                    CoasterKit.Local(PrimitiveType.Cube, t, "MuntinH", wp + new Vector3(0f, 0f, 0.03f), new Vector3(0.85f, 0.05f, 0.03f), black);
                }
            }

            CoasterKit.Local(PrimitiveType.Cube, t, "Attic", new Vector3(0f, 5.0f, 1.76f), new Vector3(0.6f, 0.6f, 0.05f), CoasterLook.Window);

            for (int k = 0; k < 9; k++)
            {
                float s = k / 8f;
                float x = Mathf.Lerp(-2.6f, 2.6f, s);
                float y = 4.15f - 0.35f * Mathf.Sin(s * Mathf.PI);
                CoasterKit.Local(PrimitiveType.Cube, t, "Flag", new Vector3(x, y - 0.18f, 1.62f), new Vector3(0.26f, 0.3f, 0.02f), CoasterLook.Flag(k + idx), Quaternion.Euler(0f, 0f, 45f));
            }

            CoasterFigures.Pumpkin(t, "Pumpkin", t.TransformPoint(new Vector3(-1.45f, 0.24f, 2.7f)), rot, 0.52f);
            CoasterFigures.Pumpkin(t, "Pumpkin", t.TransformPoint(new Vector3(1.5f, 0.2f, 2.8f)), rot, 0.42f);
            CoasterFigures.Pumpkin(t, "Pumpkin", t.TransformPoint(new Vector3(0.75f, 0.2f, 3.45f)), rot, 0.4f);

            for (int sx = -1; sx <= 1; sx += 2)
            {
                var web = GameObject.CreatePrimitive(PrimitiveType.Quad);
                web.name = "Cobweb";
                Object.DestroyImmediate(web.GetComponent<Collider>());
                web.transform.SetParent(t, false);
                web.transform.localPosition = new Vector3(sx * 1.55f, 2.4f, 3.25f);
                web.transform.localRotation = Quaternion.Euler(0f, 180f, 45f);
                web.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                web.GetComponent<MeshRenderer>().sharedMaterial = CoasterLook.Web;
                web.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            bool realLight = isEvent || idx % 3 == 0;
            CoasterFigures.Lantern(c, "PorchLantern", t.TransformPoint(new Vector3(-1.2f, 2.68f, 3.0f)), 0.5f, realLight, seed + 300);

            if (isEvent)
            {
                var lg = CoasterFigures.PointLight(t, "DoorwayLight", new Vector3(0f, 1.6f, 2.5f), CoasterPalette.Pumpkin, 4f, 8f).GetComponent<Light>();
                lg.enabled = false;
                o.doorwayLight = lg;
            }

            if (treatDoor)
            {
                CoasterFigures.KidParts kid = CoasterFigures.Kid(t, "TreatKid", t.TransformPoint(new Vector3(0.45f, 0f, 2.3f)), rot, seed % 4, 1.15f);
                o.kidArm = kid.armPivot;
                o.bucketPivot = kid.bucketPivot;
                o.bucketWorld = kid.bucketPivot.TransformPoint(new Vector3(0f, 0.12f, 0.2f));
            }
            else if (ghoulDoor)
            {
                BuildGhoul(content, t, o);
            }
            else if (CoasterKit.Hash(idx, 21) > 0.4f)
            {
                CoasterFigures.KidParts kid = CoasterFigures.Kid(t, "Kid", t.TransformPoint(new Vector3(1.0f + 0.4f * CoasterKit.Hash(idx, 22), 0f, 2.55f)), rot, idx % 4, 1.1f);
                kid.armPivot.localRotation = Quaternion.Euler(-150f, 0f, 0f);
                o.kidArm = kid.armPivot;
            }

            return o;
        }

        static void BuildGhoul(Transform content, Transform house, HouseOut o)
        {
            // The ghoul lives under the set content (not the house chunk) so it can fly out to the cart; placed at the doorway.
            var root = new GameObject("Ghoul");
            root.transform.SetParent(content, false);
            root.transform.SetPositionAndRotation(house.TransformPoint(new Vector3(0f, 0f, 1.95f)), house.rotation);
            Transform t = root.transform;
            CoasterFigures.Cone(t, "Robe", Vector3.zero, new Vector3(1.0f, 1.75f, 1.0f), CoasterLook.BoneGlow, Quaternion.identity);
            CoasterKit.Local(PrimitiveType.Sphere, t, "Head", new Vector3(0f, 1.78f, 0.05f), new Vector3(0.52f, 0.62f, 0.52f), CoasterLook.BoneGlow);
            CoasterKit.Local(PrimitiveType.Sphere, t, "EyeL", new Vector3(-0.12f, 1.9f, 0.26f), new Vector3(0.13f, 0.1f, 0.06f), CoasterLook.Toxic);
            CoasterKit.Local(PrimitiveType.Sphere, t, "EyeR", new Vector3(0.12f, 1.9f, 0.26f), new Vector3(0.13f, 0.1f, 0.06f), CoasterLook.Toxic);
            GameObject mouth = CoasterKit.Local(PrimitiveType.Sphere, t, "Mouth", new Vector3(0f, 1.65f, 0.26f), new Vector3(0.24f, 0.3f, 0.08f), CoasterLook.Black);
            CoasterKit.Local(PrimitiveType.Cube, t, "TeethTop", new Vector3(0f, 1.74f, 0.27f), new Vector3(0.2f, 0.05f, 0.04f), CoasterLook.Bone);
            CoasterFigures.Bar(t, "ArmL", new Vector3(-0.3f, 1.4f, 0.1f), new Vector3(-0.6f, 1.55f, 0.95f), 0.1f, CoasterLook.BoneGlow);
            CoasterFigures.Bar(t, "ArmR", new Vector3(0.3f, 1.4f, 0.1f), new Vector3(0.6f, 1.55f, 0.95f), 0.1f, CoasterLook.BoneGlow);
            CoasterKit.Local(PrimitiveType.Sphere, t, "HandL", new Vector3(-0.6f, 1.55f, 0.98f), Vector3.one * 0.18f, CoasterLook.BoneGlow);
            CoasterKit.Local(PrimitiveType.Sphere, t, "HandR", new Vector3(0.6f, 1.55f, 0.98f), Vector3.one * 0.18f, CoasterLook.BoneGlow);
            // Its own light, in front of the face; ramped on at the lunge so the face reads in the dark.
            GameObject glow = CoasterFigures.PointLight(t, "GhoulGlow", new Vector3(0f, 1.85f, 1.0f), CoasterPalette.ToxicGreen, 5f, 6f);
            glow.GetComponent<Light>().enabled = false;
            root.SetActive(false);
            o.ghoulRoot = t;
            o.ghoulMouth = mouth.transform;
            o.ghoulLight = glow.GetComponent<Light>();

            // Cue: two small eyes deep in the doorway.
            var eyes = new GameObject("DoorwayEyes");
            eyes.transform.SetParent(house, false);
            eyes.transform.localPosition = new Vector3(0f, 1.75f, 1.58f);
            CoasterKit.Local(PrimitiveType.Sphere, eyes.transform, "EyeL", new Vector3(-0.11f, 0f, 0f), new Vector3(0.1f, 0.06f, 0.04f), CoasterLook.Toxic);
            CoasterKit.Local(PrimitiveType.Sphere, eyes.transform, "EyeR", new Vector3(0.11f, 0f, 0f), new Vector3(0.1f, 0.06f, 0.04f), CoasterLook.Toxic);
            eyes.SetActive(false);
            o.cueEyes = eyes.transform;
        }

        /// <summary>Lamp posts and cobwebs along the rail and bunting strung across the street: close things that sweep past and show the speed.</summary>
        static void BuildStreetFurniture(Transform content, CoasterSetKit.Chunker chunks, ICoasterRail rail, float d0, float d1, float groundY)
        {
            Material post = CoasterMaterials.Get("StationPost", new Color(0.12f, 0.09f, 0.14f), default, 0.2f);
            int n = 0;
            for (float d = d0 + 3f; d < d1; d += 5.5f, n++)
            {
                CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
                Transform ck = chunks.At(d);
                int side = n % 2 == 0 ? 1 : -1;
                Vector3 pp = p + r * (side * 2.6f);
                CoasterFigures.Bar(ck, "LampPost", pp - u * 0.5f, pp + u * 2.4f, 0.1f, post);
                CoasterFigures.Lantern(ck, "Lantern", pp + u * 2.3f - r * (side * 0.2f), 0.4f, n % 5 == 0, n + 800);
                if (n % 3 == 0)
                {
                    var web = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    web.name = "HangingWeb";
                    Object.DestroyImmediate(web.GetComponent<Collider>());
                    web.transform.SetParent(ck, false);
                    web.transform.position = p + u * 3.2f + r * (-side * 1.2f);
                    web.transform.rotation = Quaternion.LookRotation(f, u) * Quaternion.Euler(0f, 0f, 30f * side);
                    web.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
                    web.GetComponent<MeshRenderer>().sharedMaterial = CoasterLook.Web;
                    web.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            // Bunting across the street every ~16 m, high enough to pass overhead.
            n = 0;
            for (float d = d0 + 8f; d < d1; d += 16f, n++)
            {
                CoasterKit.Frame(rail, d, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
                Transform ck = chunks.At(d);
                Vector3 a = p - r * 7f + u * 4.6f;
                Vector3 b = p + r * 7f + u * 4.6f;
                for (int k = 1; k < 12; k++)
                {
                    float sp = k / 12f;
                    Vector3 pt = Vector3.Lerp(a, b, sp) - u * (0.9f * Mathf.Sin(sp * Mathf.PI));
                    CoasterKit.Prim(PrimitiveType.Cube, ck, "Flag", pt, Quaternion.LookRotation(f, u) * Quaternion.Euler(0f, 0f, 45f), new Vector3(0.3f, 0.34f, 0.02f), CoasterLook.Flag(k + n));
                }
            }
        }

        static ParticleSystem MakeCandy(Transform parent, Vector3 from, Vector3 to, int seed)
        {
            const float flight = 1.4f;
            Vector3 delta = to - from;
            Vector3 v = (delta - 0.5f * Physics.gravity * flight * flight) / flight;
            var go = new GameObject("CandyShower");
            go.transform.SetParent(parent, false);
            go.transform.position = from;
            go.transform.rotation = SetUtil.Look(v, Vector3.up);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = (uint)(4242 + seed);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 2.4f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 2.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(v.magnitude * 0.9f, v.magnitude * 1.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 260;
            var colours = new Gradient();
            colours.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.25f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.25f),
                    new GradientColorKey(new Color(1f, 0.92f, 0.3f), 0.5f), new GradientColorKey(new Color(0.5f, 1f, 0.35f), 0.75f),
                    new GradientColorKey(new Color(0.75f, 0.4f, 1f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            var mmg = new ParticleSystem.MinMaxGradient(colours);
            mmg.mode = ParticleSystemGradientMode.RandomColor;
            main.startColor = mmg;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 85f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 11f;
            shape.radius = 0.14f;

            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = CoasterKit.BuiltinMesh(PrimitiveType.Sphere);
            pr.sharedMaterial = CoasterLook.Candy;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }
    }
}
