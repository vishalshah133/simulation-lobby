using System.Collections.Generic;
using UnityEngine;
using SimulationLobby.Shared.Coaster;

namespace SimulationLobby.Simulations.HorrorCoaster.EditorTools
{
    /// <summary>
    /// Builds the train ahead of the viewer (one car per other rider, each with a head-and-shoulders silhouette, an orange lap bar and a
    /// rim light) and the viewer's own lap bar and gloved hands parented to the camera. Binds everything into a <see cref="RiderRoster"/>.
    /// </summary>
    public static class RiderRosterBuilder
    {
        public static RiderRoster Build(Transform parent, Camera cam, ICoasterRail rail, HorrorCoasterConfig cfg)
        {
            var go = new GameObject("Riders");
            go.transform.SetParent(parent, false);
            var roster = go.AddComponent<RiderRoster>();

            int seats = Mathf.Max(1, cfg.ridersAtStart - 1);
            var list = new List<RiderRoster.Seat>();
            float startD = Mathf.Max(0f, cfg.ActStart(ActId.Station));
            for (int i = 0; i < seats; i++)
            {
                float offset = RiderRoster.SeatSpacing * (i + 1);
                CoasterKit.Frame(rail, Mathf.Min(rail.Length, startD + offset), out Vector3 p, out Vector3 f, out Vector3 u, out _);
                list.Add(BuildSeat(go.transform, i, offset, p, Quaternion.LookRotation(f, u)));
            }

            roster.seats = list.ToArray();
            roster.ownCar = BuildOwnCar(go.transform, rail, startD);
            BuildViewerRig(roster, cam);
            return roster;
        }

        static RiderRoster.Seat BuildSeat(Transform parent, int index, float offset, Vector3 pos, Quaternion rot)
        {
            var car = new GameObject("Car" + (index + 1));
            car.transform.SetParent(parent, false);
            car.transform.SetPositionAndRotation(pos, rot);
            Transform t = car.transform;

            Material body = CoasterMaterials.Get("CarBody", new Color(0.42f, 0.12f, 0.1f), CoasterPalette.Glow(new Color(0.9f, 0.3f, 0.1f), 0.12f), 0.35f);
            Material seatMat = CoasterMaterials.Get("CarSeat", new Color(0.2f, 0.09f, 0.18f), CoasterPalette.Glow(new Color(0.5f, 0.2f, 0.45f), 0.1f), 0.2f);
            const float sx0 = RiderRoster.SeatX;

            // A lean one-seat tub on the LEFT of the rail: 0.7 m wide, top at 0.28 m. The right half of the car is open air, so the
            // inner edge stays more than 10 degrees left of the viewer's line of sight for the first two cars.
            CoasterKit.Local(PrimitiveType.Cube, t, "Tub", new Vector3(sx0, 0.17f, 0f), new Vector3(0.7f, 0.22f, 2.3f), body);
            CoasterKit.Local(PrimitiveType.Cube, t, "Nose", new Vector3(sx0, 0.2f, 1.2f), new Vector3(0.6f, 0.2f, 0.35f), body);
            CoasterKit.Local(PrimitiveType.Cube, t, "Back", new Vector3(sx0, 0.6f, -0.7f), new Vector3(0.62f, 0.7f, 0.14f), seatMat);
            CoasterKit.Local(PrimitiveType.Cube, t, "Chassis", new Vector3(0f, 0.04f, 0f), new Vector3(0.12f, 0.06f, 2.3f), body);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                CoasterKit.Local(PrimitiveType.Cube, t, "Rim", new Vector3(sx0 + sx * 0.33f, 0.3f, 0f), new Vector3(0.04f, 0.04f, 2.3f), CoasterLook.Pumpkin);
            }

            // Lap bar over the left seat only, orange glow, readable from behind. Its inner end stays left of the corridor.
            CoasterKit.Local(PrimitiveType.Cylinder, t, "LapBar", new Vector3(sx0, 0.78f, 0.5f), new Vector3(0.08f, 0.26f, 0.08f), CoasterLook.Pumpkin, Quaternion.Euler(0f, 0f, 90f));
            CoasterFigures.Bar(t, "LapBarArm", new Vector3(sx0 - 0.26f, 0.78f, 0.5f), new Vector3(sx0 - 0.26f, 0.45f, 0.72f), 0.07f, CoasterLook.Pumpkin);

            // Rider: capsule body + sphere head + hat/hair variant.
            var rider = new GameObject("Rider");
            rider.transform.SetParent(t, false);
            rider.transform.localPosition = new Vector3(sx0, 0f, -0.15f);
            Transform rt = rider.transform;
            Color jacket;
            switch (index % 5)
            {
                case 0: jacket = new Color(0.55f, 0.2f, 0.85f); break;
                case 1: jacket = new Color(0.95f, 0.42f, 0.06f); break;
                case 2: jacket = new Color(0.1f, 0.65f, 0.75f); break;
                case 3: jacket = new Color(0.95f, 0.22f, 0.45f); break;
                default: jacket = new Color(0.35f, 0.75f, 0.3f); break;
            }

            Material jacketMat = CoasterLook.Solid("Rider" + (index % 5), jacket * 0.85f, 0.4f);
            Material skin = CoasterLook.Solid("RiderSkin", new Color(0.8f, 0.62f, 0.5f), 0.35f);
            CoasterKit.Local(PrimitiveType.Capsule, rt, "Torso", new Vector3(0f, 0.92f, 0f), new Vector3(0.5f, 0.36f, 0.36f), jacketMat, Quaternion.Euler(0f, 0f, 90f));
            CoasterKit.Local(PrimitiveType.Capsule, rt, "Chest", new Vector3(0f, 0.8f, 0f), new Vector3(0.42f, 0.28f, 0.32f), jacketMat);
            var headGo = new GameObject("Head");
            headGo.transform.SetParent(rt, false);
            headGo.transform.localPosition = new Vector3(0f, 1.38f, 0f);
            Transform head = headGo.transform;
            BuildHead(head, index % 5, skin, jacketMat);

            // Orange rim light from above and behind so the silhouette reads at 480 px.
            GameObject rim = CoasterFigures.PointLight(t, "RimLight", new Vector3(sx0 - 0.1f, 1.9f, -1.25f), CoasterPalette.Pumpkin, 2.6f, 4.4f);
            rim.GetComponent<Light>().shadows = LightShadows.None;

            // Poof puffs and the scream silhouette (hidden until the rider is taken).
            var puffs = new List<Transform>();
            for (int i = 0; i < 6; i++)
            {
                GameObject puff = CoasterKit.Local(PrimitiveType.Sphere, t, "Puff" + i, new Vector3(0f, 1f, 0f), Vector3.one * 0.3f, CoasterLook.BoneGlow);
                puff.SetActive(false);
                puffs.Add(puff.transform);
            }

            CoasterFigures.GhostParts ghost = CoasterFigures.Ghost(t, "Scream", Vector3.zero, Quaternion.identity, 1.3f, CoasterLook.GhostSheet);
            ghost.root.transform.localPosition = new Vector3(sx0, 0.9f, -0.15f);
            ghost.root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            ghost.root.SetActive(false);

            GameObject streak = CoasterKit.Local(PrimitiveType.Capsule, t, "Streak", new Vector3(sx0, 1.5f, -0.15f), new Vector3(0.28f, 1f, 0.28f), CoasterLook.BoneGlow);
            streak.SetActive(false);

            return new RiderRoster.Seat { car = t, rider = rt, head = head, puffs = puffs.ToArray(), ghost = ghost.root.transform, streak = streak.transform, offset = offset };
        }

        /// <summary>
        /// The viewer's own car: a low floor with low side walls and a nose. The viewer sits at +0.4 m (the right seat), the left seat
        /// stays empty (the GraveHand reaches for it). Floor top 0.12 m, walls 0.62 m, nose 0.3 m: every part is more than 20 degrees
        /// below the line of sight or more than 20 degrees off to the side of it.
        /// </summary>
        static Transform BuildOwnCar(Transform parent, ICoasterRail rail, float startD)
        {
            var car = new GameObject("OwnCar");
            car.transform.SetParent(parent, false);
            CoasterKit.Frame(rail, Mathf.Min(rail.Length, startD), out Vector3 p, out Vector3 f, out Vector3 u, out _);
            car.transform.SetPositionAndRotation(p, Quaternion.LookRotation(f, u));
            Transform t = car.transform;
            Material body = CoasterMaterials.Get("CarBody", new Color(0.42f, 0.12f, 0.1f), CoasterPalette.Glow(new Color(0.9f, 0.3f, 0.1f), 0.12f), 0.35f);
            Material seatMat = CoasterMaterials.Get("CarSeat", new Color(0.2f, 0.09f, 0.18f), CoasterPalette.Glow(new Color(0.5f, 0.2f, 0.45f), 0.1f), 0.2f);
            CoasterKit.Local(PrimitiveType.Cube, t, "Floor", new Vector3(0f, 0.08f, -0.05f), new Vector3(1.9f, 0.08f, 2.3f), body);
            CoasterKit.Local(PrimitiveType.Cube, t, "Nose", new Vector3(0f, 0.17f, 1.25f), new Vector3(1.3f, 0.26f, 0.35f), body);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                CoasterKit.Local(PrimitiveType.Cube, t, "Wall", new Vector3(sx * 0.95f, 0.36f, -0.05f), new Vector3(0.08f, 0.5f, 2.3f), body);
                CoasterKit.Local(PrimitiveType.Cube, t, "Rim", new Vector3(sx * 0.95f, 0.63f, -0.05f), new Vector3(0.1f, 0.04f, 2.3f), CoasterLook.Pumpkin);
            }

            CoasterKit.Local(PrimitiveType.Cube, t, "Back", new Vector3(0.4f, 0.6f, -1.0f), new Vector3(0.8f, 0.7f, 0.14f), seatMat);
            return t;
        }

        static void BuildHead(Transform head, int variant, Material skin, Material jacket)
        {
            Material hair = CoasterLook.Solid("RiderHair", new Color(0.18f, 0.1f, 0.08f), 0.2f);
            Material dark = CoasterLook.Solid("RiderHat", new Color(0.08f, 0.05f, 0.12f), 0.3f);
            switch (variant)
            {
                case 0: // witch hat
                    CoasterKit.Local(PrimitiveType.Sphere, head, "Skull", Vector3.zero, Vector3.one * 0.34f, skin);
                    CoasterKit.Local(PrimitiveType.Cylinder, head, "Brim", new Vector3(0f, 0.14f, 0f), new Vector3(0.56f, 0.012f, 0.56f), dark);
                    CoasterFigures.Cone(head, "Hat", new Vector3(0f, 0.14f, 0f), new Vector3(0.38f, 0.62f, 0.38f), dark, Quaternion.Euler(-8f, 0f, 0f));
                    CoasterKit.Local(PrimitiveType.Cylinder, head, "Band", new Vector3(0f, 0.2f, 0f), new Vector3(0.39f, 0.03f, 0.39f), CoasterLook.Pumpkin);
                    break;
                case 1: // pumpkin head
                    CoasterFigures.Pumpkin(head, "PumpkinHead", head.position, head.rotation, 0.55f, true).transform.localPosition = Vector3.zero;
                    break;
                case 2: // cap
                    CoasterKit.Local(PrimitiveType.Sphere, head, "Skull", Vector3.zero, Vector3.one * 0.34f, skin);
                    CoasterKit.Local(PrimitiveType.Sphere, head, "Cap", new Vector3(0f, 0.05f, -0.01f), new Vector3(0.38f, 0.3f, 0.38f), jacket);
                    CoasterKit.Local(PrimitiveType.Cube, head, "Peak", new Vector3(0f, 0.04f, 0.22f), new Vector3(0.3f, 0.025f, 0.2f), jacket);
                    break;
                case 3: // ponytail
                    CoasterKit.Local(PrimitiveType.Sphere, head, "Skull", Vector3.zero, Vector3.one * 0.34f, skin);
                    CoasterKit.Local(PrimitiveType.Sphere, head, "Hair", new Vector3(0f, 0.04f, -0.03f), new Vector3(0.37f, 0.33f, 0.37f), hair);
                    CoasterKit.Local(PrimitiveType.Capsule, head, "Tail", new Vector3(0f, -0.08f, -0.26f), new Vector3(0.1f, 0.2f, 0.1f), hair, Quaternion.Euler(-25f, 0f, 0f));
                    CoasterKit.Local(PrimitiveType.Sphere, head, "Tie", new Vector3(0f, 0.06f, -0.2f), Vector3.one * 0.07f, CoasterLook.Pumpkin);
                    break;
                default: // hood
                    CoasterKit.Local(PrimitiveType.Sphere, head, "Skull", Vector3.zero, Vector3.one * 0.3f, skin);
                    CoasterKit.Local(PrimitiveType.Sphere, head, "Hood", new Vector3(0f, 0.0f, -0.04f), new Vector3(0.44f, 0.42f, 0.44f), jacket);
                    CoasterFigures.Cone(head, "Point", new Vector3(0f, 0.17f, -0.1f), new Vector3(0.2f, 0.26f, 0.2f), jacket, Quaternion.Euler(-30f, 0f, 0f));
                    break;
            }
        }

        static void BuildViewerRig(RiderRoster roster, Camera cam)
        {
            var rig = new GameObject("Viewer Lap Bar And Hands");
            rig.transform.SetParent(cam.transform, false);
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;
            Transform t = rig.transform;

            Material glove = CoasterMaterials.Get("ViewerGlove", new Color(0.1f, 0.07f, 0.09f), CoasterPalette.Glow(new Color(0.4f, 0.25f, 0.3f), 0.12f), 0.35f);
            Material bone = CoasterMaterials.Get("ViewerBone", CoasterPalette.Bone, CoasterPalette.Glow(CoasterPalette.Bone, 0.6f), 0.3f);
            Material sleeve = CoasterLook.Solid("ViewerSleeve", new Color(0.55f, 0.25f, 0.06f), 0.35f);
            Material pad = CoasterMaterials.Get("ViewerPad", new Color(0.05f, 0.04f, 0.06f), default, 0.4f);

            // The bar and its housing sit low: everything the viewer owns stays below -25 degrees of elevation (z is forward, y up,
            // camera space), so the centre of the frame is never covered. Checked by ViewClearanceTest.
            CoasterKit.Local(PrimitiveType.Cylinder, t, "LapBar", new Vector3(0f, -0.8f, 0.9f), new Vector3(0.09f, 0.7f, 0.09f), CoasterLook.Pumpkin, Quaternion.Euler(0f, 0f, 90f));
            CoasterKit.Local(PrimitiveType.Cylinder, t, "Pad", new Vector3(0f, -0.88f, 0.86f), new Vector3(0.16f, 0.55f, 0.16f), pad, Quaternion.Euler(0f, 0f, 90f));
            for (int sx = -1; sx <= 1; sx += 2)
            {
                CoasterFigures.Bar(t, "BarArm", new Vector3(sx * 0.7f, -0.8f, 0.9f), new Vector3(sx * 0.7f, -1.4f, 0.55f), 0.08f, CoasterLook.Pumpkin);
            }

            var hand = new List<Renderer>();
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3 h = new Vector3(sx * 0.28f, -0.7f, 0.92f);
                hand.Add(CoasterKit.Local(PrimitiveType.Sphere, t, "Palm", h, new Vector3(0.15f, 0.1f, 0.17f), glove).GetComponent<Renderer>());
                for (int k = 0; k < 4; k++)
                {
                    Vector3 fp = h + new Vector3((k - 1.5f) * 0.036f, 0.03f, 0.07f);
                    hand.Add(CoasterKit.Local(PrimitiveType.Capsule, t, "Finger", fp, new Vector3(0.04f, 0.045f, 0.04f), glove, Quaternion.Euler(0f, 0f, 0f)).GetComponent<Renderer>());
                }

                hand.Add(CoasterKit.Local(PrimitiveType.Sphere, t, "Thumb", h + new Vector3(-sx * 0.075f, 0.03f, 0.0f), Vector3.one * 0.05f, glove).GetComponent<Renderer>());
                Vector3 elbow = new Vector3(sx * 0.45f, -1.05f, 0.25f);
                hand.Add(CoasterFigures.Bar(t, "Forearm", elbow, h + new Vector3(0f, -0.02f, -0.06f), 0.085f, glove).GetComponent<Renderer>());
                CoasterFigures.Bar(t, "Sleeve", elbow, Vector3.Lerp(elbow, h, 0.38f), 0.13f, sleeve);
            }

            CoasterFigures.PointLight(t, "HandsLight", new Vector3(0f, -0.35f, 0.35f), CoasterPalette.Pumpkin, 1.0f, 2.4f);

            roster.handRenderers = hand.ToArray();
            roster.gloveMaterial = glove;
            roster.boneMaterial = bone;
        }
    }
}
