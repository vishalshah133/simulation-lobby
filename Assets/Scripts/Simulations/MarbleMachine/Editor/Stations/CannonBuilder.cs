using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the recycle cannon in local space: a 2.4 m square hopper (0.55 m catch walls) with its rim centre at the entry (origin),
    /// a breech column under it, and a cast-iron cannon on a wheeled carriage behind (+Z). Only the funnel, rim and
    /// breech have colliders; the carriage and barrel are decoration.
    /// </summary>
    public static class CannonBuilder
    {
        const float RimHalf = 1.2f;
        const float FunnelDepth = 1.2f;
        /// <summary>Half the breech width: 0.25 at r 0.065; 1.5 r (0.3) for bigger balls so a 0.4 m racer drops through. Set in <see cref="Build"/>.</summary>
        static float ThroatHalf = 0.25f;
        const float BreechBottom = -1.95f;
        const float DeckY = -2.2f;
        const float PivotHeight = 0.95f;
        /// <summary>~4 ball diameters at r 0.065 (0.55); at least 5 r for bigger balls (1.0 at r 0.2): a fast ball landing in the hopper can't hop out over the side.</summary>
        static float WallHeight = 0.55f;

        public static CannonStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            ThroatHalf = Mathf.Max(0.25f, 1.5f * config.ballRadius);
            WallHeight = Mathf.Max(0.55f, 5f * config.ballRadius);
            // The barrel is decoration, but a racer leaves its muzzle visibly larger than the bore unless the barrel grows a little.
            float bk = Mathf.Clamp(config.ballRadius / 0.1f, 1f, 1.4f);
            var root = new GameObject("Cannon");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            Transform t = root.transform;
            var station = root.AddComponent<CannonStation>();
            float floorLocalY = -placement.entry.y;

            // Funnel (terrazzo, double-sided collider) and wooden rim.
            MachineGeometry.MeshObject(t, "Hopper Funnel", BuildFunnel(), "Cannon_Funnel", kit.terrazzo, true, false);
            MachineGeometry.Box(t, "Rim Back", new Vector3(0f, -0.04f, -RimHalf), Quaternion.identity, new Vector3(RimHalf * 2f + 0.3f, 0.08f, 0.14f), kit.wood, false);
            MachineGeometry.Box(t, "Rim Front Beam", new Vector3(0f, -0.04f, RimHalf), Quaternion.identity, new Vector3(RimHalf * 2f + 0.3f, 0.08f, 0.14f), kit.wood, false);
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(t, "Rim Side Beam", new Vector3(s * RimHalf, -0.04f, 0f), Quaternion.identity, new Vector3(0.14f, 0.08f, RimHalf * 2f + 0.3f), kit.wood, false);
                // Catch walls on the sides and the front; the back (-Z) stays open for the connectors.
                MachineGeometry.Box(t, "Hopper Side Wall", new Vector3(s * (RimHalf + 0.03f), WallHeight * 0.5f, 0f), Quaternion.identity,
                    new Vector3(0.06f, WallHeight, RimHalf * 2f + 0.12f), kit.glass);
            }

            MachineGeometry.Box(t, "Hopper Front Wall", new Vector3(0f, WallHeight * 0.5f, RimHalf + 0.03f), Quaternion.identity,
                new Vector3(RimHalf * 2f + 0.12f, WallHeight, 0.06f), kit.glass);

            // Breech column and floor.
            float colTop = -FunnelDepth;
            float colH = colTop - BreechBottom;
            float wallC = ThroatHalf + 0.03f;
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(t, "Breech Wall X", new Vector3(s * wallC, colTop - colH * 0.5f, 0f), Quaternion.identity,
                    new Vector3(0.06f, colH, ThroatHalf * 2f + 0.12f), kit.glass);
                MachineGeometry.Box(t, "Breech Wall Z", new Vector3(0f, colTop - colH * 0.5f, s * wallC), Quaternion.identity,
                    new Vector3(ThroatHalf * 2f + 0.12f, colH, 0.06f), kit.glass);
            }

            MachineGeometry.Box(t, "Breech Floor", new Vector3(0f, BreechBottom - 0.03f, 0f), Quaternion.identity,
                new Vector3(ThroatHalf * 2f + 0.12f, 0.06f, ThroatHalf * 2f + 0.12f), kit.castIron);
            MachineGeometry.Cylinder(t, "Breech Band", new Vector3(0f, BreechBottom + 0.2f, 0f), Quaternion.identity, 0.36f, 0.05f, kit.brass, false);

            // Feed tube from the breech to the barrel's rear (decoration).
            const float turretZ = 2.4f;
            MachineGeometry.Cylinder(t, "Feed Tube", new Vector3(0f, BreechBottom + 0.25f, turretZ * 0.5f), Quaternion.Euler(90f, 0f, 0f),
                0.13f, turretZ, kit.copper, false);

            // Deck on four legs, and the hopper posts.
            MachineGeometry.Box(t, "Deck", new Vector3(0f, DeckY, 1.2f), Quaternion.identity, new Vector3(3f, 0.15f, 5.6f), kit.wood, false);
            float deckBottom = DeckY - 0.075f;
            float legLen = Mathf.Max(0.5f, deckBottom - floorLocalY);
            Vector2[] legs = { new Vector2(-1.35f, -1.45f), new Vector2(1.35f, -1.45f), new Vector2(-1.35f, 3.85f), new Vector2(1.35f, 3.85f) };
            for (int i = 0; i < legs.Length; i++)
            {
                MachineGeometry.Cylinder(t, "Leg " + (i + 1), new Vector3(legs[i].x, deckBottom - legLen * 0.5f, legs[i].y), Quaternion.identity,
                    0.07f, legLen, kit.chrome, false);
            }

            float postH = -0.08f - (DeckY + 0.075f);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    MachineGeometry.Box(t, "Hopper Post", new Vector3(sx * (RimHalf + 0.05f), DeckY + 0.075f + postH * 0.5f, sz * (RimHalf + 0.05f)),
                        Quaternion.identity, new Vector3(0.12f, postH, 0.12f), kit.woodDark, false);
                }
            }

            // Turret: yaws. Carriage cheeks, axle and spoked wheels.
            Transform turret = MachineGeometry.Anchor(t, "Turret", new Vector3(0f, DeckY + 0.075f, turretZ));
            MachineGeometry.Cylinder(turret, "Turntable", new Vector3(0f, 0.04f, 0f), Quaternion.identity, 0.8f, 0.08f, kit.castIron, false);
            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Box(turret, "Cheek", new Vector3(s * 0.42f, 0.5f, 0f), Quaternion.identity, new Vector3(0.12f, 0.8f, 1.5f), kit.wood, false);
            }

            MachineGeometry.Cylinder(turret, "Axle", new Vector3(0f, 0.5f, 0f), Quaternion.Euler(0f, 0f, 90f), 0.05f, 1.7f, kit.chrome, false);
            BuildWheel(turret, kit, -0.66f);
            BuildWheel(turret, kit, 0.66f);

            // Barrel: pivot pitches, visual recoils, no colliders.
            Transform pivot = MachineGeometry.Anchor(turret, "Barrel Pivot", new Vector3(0f, PivotHeight, 0f));
            Transform visual = MachineGeometry.Anchor(pivot, "Barrel Visual", Vector3.zero);
            Quaternion alongZ = Quaternion.Euler(90f, 0f, 0f);
            MachineGeometry.Cylinder(visual, "Barrel", new Vector3(0f, 0f, 0.9f), alongZ, 0.2f * bk, 2.4f, kit.castIron, false);
            MachineGeometry.Cylinder(visual, "Breech Cap", new Vector3(0f, 0f, -0.32f), alongZ, 0.16f * bk, 0.12f, kit.castIron, false);
            MachineGeometry.Cylinder(visual, "Muzzle Ring", new Vector3(0f, 0f, 2.0f), alongZ, 0.26f * bk, 0.2f, kit.brass, false);
            for (int i = 0; i < 3; i++)
            {
                MachineGeometry.Cylinder(visual, "Reinforce " + (i + 1), new Vector3(0f, 0f, 0.1f + i * 0.55f), alongZ, 0.235f * bk, 0.07f, kit.chrome, false);
            }

            for (int s = -1; s <= 1; s += 2)
            {
                MachineGeometry.Cylinder(pivot, "Trunnion", new Vector3(s * 0.3f, 0f, 0.4f), Quaternion.Euler(0f, 0f, 90f), 0.07f, 0.2f, kit.brass, false);
            }

            Transform muzzle = MachineGeometry.Anchor(pivot, "Muzzle", new Vector3(0f, 0f, 2.15f));
            pivot.localRotation = Quaternion.Euler(-config.cannonLaunchAngle, 0f, 0f);

            // Wire the station.
            station.displayName = "Cannon";
            station.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            station.exit = muzzle;
            station.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", new Vector3(0f, -0.5f, 1.4f));
            station.cameraOffset = placement.Rotation * new Vector3(4.5f, 1.2f, -2.5f);
            station.tourSeconds = 6f;
            station.activityBounds = SiloBuilder.WorldBounds(t, new Bounds(new Vector3(0f, -0.4f, 1.2f), new Vector3(3.2f, 4.2f, 5.6f)));
            station.breechLocal = new Bounds(new Vector3(0f, (BreechBottom - 0.8f) * 0.5f - 0.05f, 0f), new Vector3(1.1f, -0.8f - BreechBottom, 1.1f));
            station.hopperDropLocal = new Vector3(0f, 0.4f, 0f);
            station.scatterRadius = 0.9f;
            station.turret = turret;
            station.barrelPivot = pivot;
            station.barrelVisual = visual;
            station.muzzle = muzzle;
            return station;
        }

        static Mesh BuildFunnel()
        {
            var verts = new List<Vector3>
            {
                new Vector3(-RimHalf, 0f, -RimHalf), new Vector3(RimHalf, 0f, -RimHalf), new Vector3(RimHalf, 0f, RimHalf), new Vector3(-RimHalf, 0f, RimHalf),
                new Vector3(-ThroatHalf, -FunnelDepth, -ThroatHalf), new Vector3(ThroatHalf, -FunnelDepth, -ThroatHalf),
                new Vector3(ThroatHalf, -FunnelDepth, ThroatHalf), new Vector3(-ThroatHalf, -FunnelDepth, ThroatHalf)
            };
            var tris = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                tris.Add(i); tris.Add(j); tris.Add(4 + i);
                tris.Add(j); tris.Add(4 + j); tris.Add(4 + i);
            }

            return SiloBuilder.MakeDoubleSided(verts, tris);
        }

        static void BuildWheel(Transform parent, MachineKit kit, float x)
        {
            const float radius = 0.5f;
            var wheel = MachineGeometry.Anchor(parent, "Wheel", new Vector3(x, 0.5f, 0f));
            MachineGeometry.Cylinder(wheel, "Hub", Vector3.zero, Quaternion.Euler(0f, 0f, 90f), 0.1f, 0.16f, kit.brass, false);
            for (int i = 0; i < 4; i++)
            {
                MachineGeometry.Box(wheel, "Spoke", Vector3.zero, Quaternion.Euler(i * 45f, 0f, 0f), new Vector3(0.06f, radius * 2f - 0.04f, 0.05f), kit.castIron, false);
            }

            const int rimParts = 16;
            float arc = 2f * Mathf.PI * radius / rimParts * 1.06f;
            for (int i = 0; i < rimParts; i++)
            {
                float phi = i * 360f / rimParts;
                float rad = phi * Mathf.Deg2Rad;
                MachineGeometry.Box(wheel, "Rim", new Vector3(0f, Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius), Quaternion.Euler(phi, 0f, 0f),
                    new Vector3(0.1f, 0.06f, arc), kit.woodDark, false);
            }
        }
    }
}
