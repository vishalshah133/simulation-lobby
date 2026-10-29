using System.Collections.Generic;
using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Ski Jump (wave 2, compact). The lip sits 1.45 m below the entry; rolling-ball speed at the lip is
    /// sqrt(2 g H / 1.4) = 4.5 m/s ideal, ~3.96 m/s after losses. The crate floor is 0.5 m below the lip, so a 25 degree
    /// launch at 3.96 m/s is airborne 0.53 s and lands ~1.9 m out: 0.3 m of margin on the default 1.6 m gap. Balls
    /// slowed or knocked by neighbours fall short into the net (target 10-20% misses; tune skiJumpGap).
    /// </summary>
    public static class SkiJumpBuilder
    {
        const float SlopeDeg = 32f;
        const float KickDeg = 25f;
        const float ArcR = 1.8f;
        const float KickLen = 0.5f;
        const float LipDrop = 1.45f;
        const float CrateDrop = 0.5f;
        const float CrateLen = 2.2f;
        const float CrateHalf = 2.6f; // 2x
        const float PortBack = 0.9f;
        const float FloorTilt = 6f;

        public static SkiJumpStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            var root = new GameObject("Ski Jump");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            var t = root.transform;
            float floorY = -placement.entry.y;
            float w = config.chuteWidth;
            float half = w * 0.5f;
            float gap = config.skiJumpGap;
            float wallH = MachineConnectorBuilder.WallHeightFor(config.ballRadius);
            float guideH = Mathf.Max(0.5f, 3.5f * config.ballRadius); // crate guide walls stand at least 1.75 ball diameters

            // Inrun centreline.
            float s1 = SlopeDeg * Mathf.Deg2Rad, k1 = KickDeg * Mathf.Deg2Rad;
            float arcDrop = ArcR * (Mathf.Cos(s1) - Mathf.Cos(k1)); // net fall over the arc (positive = down)
            float netRise = KickLen * Mathf.Sin(k1) - arcDrop;
            float yStart = -0.05f;
            float yLip = yStart - LipDrop;
            float yArc0 = yLip - netRise;
            float la = (yStart - yArc0) / Mathf.Sin(s1);
            var pts = new List<Vector3>();
            Vector3 p = new Vector3(0f, yStart, -0.05f);
            pts.Add(p);
            p += new Vector3(0f, -Mathf.Sin(s1), Mathf.Cos(s1)) * la;
            pts.Add(p);
            int arcSteps = 10;
            float dth = (k1 + s1) / arcSteps;
            for (int i = 0; i < arcSteps; i++)
            {
                float th = -s1 + (i + 0.5f) * dth;
                p += new Vector3(0f, Mathf.Sin(th), Mathf.Cos(th)) * (ArcR * dth);
                pts.Add(p);
            }

            p += new Vector3(0f, Mathf.Sin(k1), Mathf.Cos(k1)) * KickLen;
            pts.Add(p);
            Vector3 lip = p;
            // Entry port and inrun are one channel in the connector's own profile, so the chute meets an identical section. The floor is
            // slick: launch speed decides who clears the gap.
            var inrunPts = new List<Vector3>(pts);
            inrunPts.Insert(0, new Vector3(0f, yStart, -PortBack));
            MachineConnectorBuilder.BuildPort(t, "Inrun", inrunPts, kit, config, wallH, null, MachineGeometry.SlickPhysics);

            // Speed-boost pad on the flat entry stretch, just before the ramp starts.
            // Not in the lap race: the pad clamps every ball up to boosterSurfaceSpeed, which equalises the speed at the kicker, and the
            // ski jump is only a gamble if arrival speed (who got bumped, who came straight through) decides the jump.
            if (!(config is MachineRaceConfig))
                BoostPadBuilder.Build(t, "Boost Pad", new Vector3(0f, yStart, -0.45f), Quaternion.identity, 0.8f, kit, config);

            // Chrome rails on the channel walls (decor) and the brass launch strip.
            for (int sgn = -1; sgn <= 1; sgn += 2)
                for (int i = 0; i < pts.Count - 1; i++)
                    Rod(t, kit.chrome, pts[i] + new Vector3(sgn * (half + 0.08f), wallH, 0f), pts[i + 1] + new Vector3(sgn * (half + 0.08f), wallH, 0f), 0.025f);
            Quaternion kq = Quaternion.Euler(-KickDeg, 0f, 0f);
            MachineGeometry.Box(t, "Launch Strip", lip + kq * new Vector3(0f, 0.005f, -0.06f), kq, new Vector3(w, 0.02f, 0.12f), kit.brass, false);

            // Catch crate.
            float zc0 = lip.z + gap;
            float yF0 = lip.y - CrateDrop;
            float tilt = Mathf.Tan(FloorTilt * Mathf.Deg2Rad);
            float yF1 = yF0 - CrateLen * tilt;
            Quaternion fq = Quaternion.Euler(FloorTilt, 0f, 0f);
            float flen = CrateLen / Mathf.Cos(FloorTilt * Mathf.Deg2Rad);
            Vector3 fmid = new Vector3(0f, (yF0 + yF1) * 0.5f, zc0 + CrateLen * 0.5f);
            MachineGeometry.Box(t, "Crate Floor", fmid - fq * Vector3.up * 0.05f, fq, new Vector3(CrateHalf * 2f, 0.1f, flen), kit.wood);
            float zc1 = zc0 + CrateLen;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                MachineGeometry.Box(t, "Crate Side", fmid + fq * new Vector3(sgn * (CrateHalf + 0.05f), 0.5f, 0f), fq,
                    new Vector3(0.1f, 1.0f, flen), kit.glass);
                // Plan-view guide walls funnel the crate floor into the exit opening.
                Vector3 g0 = new Vector3(sgn * CrateHalf, yF1 + 0.2f, zc1 - 2.0f);
                Vector3 g1 = new Vector3(sgn * (half + 0.03f), yF1 + 0.2f, zc1 - 0.05f);
                Vector3 gd = g1 - g0;
                Quaternion gq = Quaternion.LookRotation(gd, Vector3.up);
                MachineGeometry.Box(t, "Crate Guide", (g0 + g1) * 0.5f + Vector3.up * (guideH - 0.5f) * 0.5f, gq, new Vector3(0.06f, guideH, gd.magnitude), kit.glass);
            }

            float backH = 1.8f;
            // The exit opening under the lintel clears a ball diameter plus margin (0.6 at r 0.065, 0.8 at r 0.2).
            float openH = Mathf.Max(0.6f, 4f * config.ballRadius);
            float sideW = CrateHalf - half;
            for (int sgn = -1; sgn <= 1; sgn += 2)
                MachineGeometry.Box(t, "Crate Back", new Vector3(sgn * (half + sideW * 0.5f + 0.05f), yF1 + backH * 0.5f - 0.1f, zc1 + 0.05f),
                    Quaternion.identity, new Vector3(sideW + 0.1f, backH, 0.1f), kit.wood);
            MachineGeometry.Box(t, "Crate Lintel", new Vector3(0f, yF1 + (backH + openH) * 0.5f - 0.1f, zc1 + 0.05f),
                Quaternion.identity, new Vector3(w, backH - openH, 0.1f), kit.wood);
            MachineGeometry.Box(t, "Crate Front", new Vector3(0f, (yF0 + yF1 - 0.2f) * 0.5f, zc0 - 0.05f),
                Quaternion.identity, new Vector3(CrateHalf * 2f + 0.2f, (yF0 - yF1) + 0.2f, 0.1f), kit.woodDark);

            // Exit chute out of the crate.
            Vector3 e0 = new Vector3(0f, yF1 - 0.02f, zc1);
            Vector3 e1 = e0 + new Vector3(0f, -0.2f, 1.3f);
            MachineConnectorBuilder.BuildPort(t, "Exit Port", new[] { e0 + new Vector3(0f, tilt * 0.3f, -0.3f), e0, e1 }, kit, config, wallH);

            // The miss net and gutter are built mirrored (parent scale x = -1) so the gutter lands on local +X: with the
            // station facing -X in the layout that is the back of the machine, away from the main exit, where the cannon sits.
            var rig = MachineGeometry.Group(t, "Net Rig");
            rig.transform.localScale = new Vector3(-1f, 1f, 1f);
            Transform rt = rig.transform;

            // Miss net under the gap, sloping to -X into a gutter that runs +Z.
            float nz0 = lip.z - 0.4f, nz1 = zc0 + 0.6f;
            float netHi = lip.y - CrateDrop - 0.55f, netTilt = 10f; // 0.55 m under the crate floor
            float netW = 5.4f; // 2x, so it spans the whole 2x crate
            float netDrop = netW * Mathf.Tan(netTilt * Mathf.Deg2Rad);
            Quaternion nq = Quaternion.Euler(0f, 0f, netTilt);
            Vector3 nmid = new Vector3(0f, netHi - netDrop * 0.5f, (nz0 + nz1) * 0.5f);
            MachineGeometry.Box(rt, "Miss Net", nmid - nq * Vector3.up * 0.02f, nq, new Vector3(netW / Mathf.Cos(netTilt * Mathf.Deg2Rad), 0.04f, nz1 - nz0), kit.mesh);
            float xHi = netW * 0.5f;
            MachineGeometry.Box(rt, "Net High Wall", new Vector3(xHi + 0.03f, netHi + 0.3f, (nz0 + nz1) * 0.5f), Quaternion.identity,
                new Vector3(0.06f, 1.2f, nz1 - nz0 + 0.1f), kit.glass);
            MachineGeometry.Box(rt, "Net Front Wall", new Vector3(0f, netHi - netDrop * 0.5f + 0.35f, nz0 - 0.03f), Quaternion.identity,
                new Vector3(netW + 0.1f, 1.2f, 0.06f), kit.glass);
            float underY = yF1 - 0.12f;
            float netRearBot = netHi - netDrop - 0.1f;
            MachineGeometry.Box(rt, "Net Rear Wall", new Vector3(0f, (underY + netRearBot) * 0.5f, nz1 + 0.03f), Quaternion.identity,
                new Vector3(netW + 0.1f, underY - netRearBot, 0.06f), kit.glass);

            // Gutter: x from -netW/2 down to -netW/2 - 0.9, floor falls 4 degrees toward +Z.
            float gx0 = -netW * 0.5f, gw = config.chuteWidth; // the gutter is the track width, so the recycle chute it feeds is an exact match
            float gz1 = zc1 + 1.1f;
            float gTilt = 4f * Mathf.Deg2Rad;
            float gylow = netHi - netDrop - 0.05f;
            float gLen = gz1 - nz0;
            Quaternion gfq = Quaternion.Euler(4f, 0f, 0f);
            Vector3 gmid = new Vector3(gx0 - gw * 0.5f, gylow - gLen * 0.5f * Mathf.Tan(gTilt), (nz0 + gz1) * 0.5f);
            MachineGeometry.Box(rt, "Gutter Floor", gmid - gfq * Vector3.up * 0.025f, gfq, new Vector3(gw + 0.1f, 0.05f, gLen / Mathf.Cos(gTilt)), kit.asphalt);
            MachineGeometry.Box(rt, "Gutter Outer Wall", gmid + gfq * new Vector3(-(gw * 0.5f + 0.05f), 0.3f, 0f), gfq,
                new Vector3(0.06f, wallH, gLen / Mathf.Cos(gTilt)), kit.glass);
            float innerStart = nz1 + 0.06f;
            float innerLen = gz1 - innerStart;
            Vector3 imid = new Vector3(gx0 + 0.03f, gylow - ((innerStart + gz1) * 0.5f - nz0) * Mathf.Tan(gTilt) + 0.3f, (innerStart + gz1) * 0.5f);
            MachineGeometry.Box(rt, "Gutter Inner Wall", imid, gfq, new Vector3(0.06f, wallH, innerLen / Mathf.Cos(gTilt)), kit.glass);
            MachineGeometry.Box(rt, "Gutter Back", new Vector3(gx0 - gw * 0.5f, gylow + 0.2f, nz0 - 0.03f), Quaternion.identity,
                new Vector3(gw + 0.1f, 0.5f, 0.06f), kit.glass);
            float gEndY = gylow - (gz1 - nz0) * Mathf.Tan(gTilt);

            // Supports: trestles under the inrun, legs under crate, net and gutter.
            for (int i = 1; i < pts.Count - 1; i += 3)
                Trestle(rt, kit, pts[i], half + 0.12f, floorY);
            Trestle(rt, kit, lip, half + 0.12f, floorY);
            foreach (float zz in new[] { zc0 + 0.15f, zc1 - 0.15f })
                for (int sgn = -1; sgn <= 1; sgn += 2)
                    Leg(rt, kit.chrome, sgn * (CrateHalf + 0.1f), zz, yF1 - 0.05f, floorY, 0.06f);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Leg(rt, kit.chrome, sgn * (xHi + 0.1f), nz0, netHi - 0.05f, floorY, 0.05f);
                Leg(rt, kit.chrome, -netW * 0.5f - gw - 0.1f, sgn > 0 ? nz0 : gz1 - 0.1f, gylow - 0.05f, floorY, 0.05f);
            }

            Leg(rt, kit.chrome, xHi + 0.1f, nz1, netHi - 0.05f, floorY, 0.05f);
            Leg(rt, kit.chrome, -netW * 0.5f - 0.1f, gz1 - 0.1f, gEndY, floorY, 0.05f);

            var station = root.AddComponent<SkiJumpStation>();
            station.displayName = "Ski Jump";
            station.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            station.exit = MachineGeometry.Anchor(t, "Exit", e1 + new Vector3(0f, 0.05f, 0f));
            station.recycleExit = MachineGeometry.Anchor(t, "Recycle Exit", new Vector3(-(gx0 - gw * 0.5f), gEndY + 0.05f, gz1));
            station.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", new Vector3(0f, lip.y - 0.4f, lip.z + gap * 0.5f));
            station.cameraOffset = placement.Rotation * new Vector3(12f, 1.5f, 0f);
            station.tourSeconds = 9f;
            Vector3 lo = new Vector3(-CrateHalf - 0.3f, gEndY - 0.3f, -PortBack - 0.1f);
            Vector3 hi = new Vector3(netW * 0.5f + gw + 0.3f, 0.5f, gz1 + 0.2f);
            station.activityBounds = WorldBounds(t, (lo + hi) * 0.5f, hi - lo);
            return station;
        }

        static void Rod(Transform t, Material m, Vector3 a, Vector3 b, float radius)
        {
            Vector3 d = b - a;
            MachineGeometry.Cylinder(t, "Rail", (a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d.normalized), radius, d.magnitude, m, false);
        }

        static void Trestle(Transform t, MachineKit kit, Vector3 at, float x, float floorY)
        {
            for (int sgn = -1; sgn <= 1; sgn += 2)
                Leg(t, kit.chrome, sgn * x, at.z, at.y - 0.05f, floorY, 0.045f);
            MachineGeometry.Box(t, "Trestle Beam", new Vector3(0f, at.y - 0.1f, at.z), Quaternion.identity, new Vector3(x * 2f + 0.1f, 0.08f, 0.1f), kit.wood, false);
        }

        static void Leg(Transform t, Material m, float x, float z, float topY, float floorY, float radius)
        {
            float h = Mathf.Max(0.1f, topY - floorY);
            MachineGeometry.Cylinder(t, "Leg", new Vector3(x, floorY + h * 0.5f, z), Quaternion.identity, radius, h, m);
        }

        static Bounds WorldBounds(Transform t, Vector3 c, Vector3 size)
        {
            var b = new Bounds(t.TransformPoint(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -size.x : size.x, (i & 2) == 0 ? -size.y : size.y, (i & 4) == 0 ? -size.z : size.z) * 0.5f;
                b.Encapsulate(t.TransformPoint(corner));
            }

            return b;
        }
    }
}
