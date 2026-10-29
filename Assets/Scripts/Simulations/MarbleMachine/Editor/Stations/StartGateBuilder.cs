using UnityEngine;

namespace SimulationLobby.Simulations.MarbleMachine.EditorTools
{
    /// <summary>
    /// Builds the Start Gate of the lap race (mach-002): entry anchor at the local origin, flow along +Z.
    ///
    /// Layout (local, metres, r = ball radius 0.2, chuteWidth 1.8, 4 columns, 4 rows):
    ///  - z -0.9..0     entry port (the closing connector's profile) opening through the back wall.
    ///  - z 0..~2.1     starting platform, 4.5 degrees downhill, inner width ~2.24 (4 lanes at 0.5 m pitch + margin), side walls and a
    ///                  back wall 1.3 m tall with inward-leaning catch lips so a cannon-fired racer cannot bounce out.
    ///  - z ~2.1        gate: a plate + 3 lane dividers on ONE kinematic body that sinks into the floor when released.
    ///                  Dividers keep the 4 columns straight while the racers wait; rows are 0.42 m apart (2.1 r, almost touching).
    ///  - z ~2.65       lap line: a chequered stripe on the floor and the start/finish arch over it.
    ///  - then          a funnel (1.4 m) from the platform width down to chuteWidth and a 0.6 m exit port.
    /// The floor is one continuous slab with the port floors flush at both ends, so there is no gap a 0.2 m ball can fall through.
    /// Grid: slot = row * columns + column, row 0 against the gate, column 0 on the -X side; a slot is a ball CENTRE resting on the
    /// floor (floor + r along the floor normal).
    /// </summary>
    public static class StartGateBuilder
    {
        const float SlopeDegrees = 4.5f;
        const float PortBack = 0.9f;
        const float WallT = 0.16f;          // the connector's wall thickness, so walls meet the ports without a step
        const float FloorY0 = -0.05f;       // platform floor top at z = 0 (5 cm under the entry anchor, like the other stations' ports)
        const float GateHeight = 0.8f;
        const float DividerHeight = 0.32f;
        const float GateSink = 0.8f;
        const float FunnelLength = 1.4f;
        const float ExitPortLength = 0.6f;
        const float ArchHeight = 3.4f;

        public static StartGateStation Build(Transform parent, StationPlacement placement, MachineKit kit, MachineConfig config)
        {
            float r = config.ballRadius;
            float chute = config.chuteWidth;
            var race = config as MachineRaceConfig;
            int columns = race != null ? Mathf.Max(1, race.gridColumns) : 4;
            int racers = race != null ? race.racerCount : 16;
            int rows = Mathf.CeilToInt(Mathf.Max(16, racers) / (float)columns);
            float pitchX = 2.5f * r, pitchZ = 2.1f * r;

            float S = SlopeDegrees * Mathf.Deg2Rad, tanS = Mathf.Tan(S), cosS = Mathf.Cos(S), sinS = Mathf.Sin(S);
            Quaternion slopeRot = Quaternion.Euler(SlopeDegrees, 0f, 0f);       // local +Z pitched down along the floor
            Vector3 upN = new Vector3(0f, cosS, sinS);                           // floor normal
            Vector3 fwdF = new Vector3(0f, -sinS, cosS);                         // along the floor, forward
            float YTop(float z) => FloorY0 - z * tanS;
            Vector3 P(float z) => new Vector3(0f, YTop(z), z);

            // Platform half-width (inner): fits the grid with margin and is never narrower than the track.
            float hw = Mathf.Max(chute * 0.5f + 0.2f, columns * pitchX * 0.5f + 0.12f);
            float wallH = Mathf.Max(1.3f, MachineConnectorBuilder.WallHeightFor(r) + 0.2f);
            float chuteHalf = chute * 0.5f;

            // z stations.
            float gateHalfT = 0.03f;
            float gridSpanZ = (rows - 1) * pitchZ * cosS;
            float zG = 0.45f + 2f * r + gridSpanZ + 0.1f;          // gate plane
            float zL = zG + 0.55f;                                 // lap line
            float zFs = zL + 0.6f;                                 // funnel start
            float zFe = zFs + FunnelLength;                        // funnel end / exit port start
            float zEnd = zFe + ExitPortLength;

            var root = new GameObject("Start Gate");
            root.transform.SetParent(parent, false);
            root.transform.position = placement.entry;
            root.transform.rotation = placement.Rotation;
            Transform t = root.transform;
            float floorY = -placement.entry.y;
            var station = root.AddComponent<StartGateStation>();

            // Entry port: flush with the platform's floor top at z = 0.
            MachineConnectorBuilder.BuildPort(t, "Entry Port", new[] { new Vector3(0f, FloorY0, -PortBack), new Vector3(0f, FloorY0, 0.02f) }, kit, config);

            // Floor slab: one continuous box under the whole platform and funnel, overlapping both ports.
            float zFloor0 = -0.1f, zFloor1 = zFe + 0.04f;
            float floorLen = (zFloor1 - zFloor0) / cosS;
            float zFloorMid = (zFloor0 + zFloor1) * 0.5f;
            float floorW = (hw + WallT) * 2f;
            Vector3 floorCentre = P(zFloorMid) - upN * 0.08f;
            MachineGeometry.Box(t, "Floor", floorCentre, slopeRot, new Vector3(floorW, 0.16f, floorLen), kit.asphalt);
            MachineGeometry.Box(t, "Deck", P(zFloorMid) - upN * (0.16f + 0.15f), slopeRot, new Vector3(floorW + 0.1f, 0.3f, floorLen), kit.woodDark, false);

            // Walls: side walls to the funnel start, funnel walls down to the track width, back wall either side of the entry opening.
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                float xw = sgn * (hw + WallT * 0.5f);
                Wall(t, kit.glass, new Vector3(xw, YTop(0f), 0f), new Vector3(xw, YTop(zFs), zFs), wallH);
                Wall(t, kit.glass, new Vector3(xw, YTop(zFs), zFs), new Vector3(sgn * (chuteHalf + WallT * 0.5f), YTop(zFe), zFe), wallH);

                // Back wall segment from the port's outer wall face to the platform's outer wall face.
                float bx0 = chuteHalf + WallT, bx1 = hw + WallT;
                MachineGeometry.Box(t, sgn < 0 ? "Back Wall L" : "Back Wall R", new Vector3(sgn * (bx0 + bx1) * 0.5f, YTop(0f) - 0.1f + wallH * 0.5f, 0f),
                    Quaternion.identity, new Vector3(bx1 - bx0, wallH, WallT), kit.glass);

                // Catch lip along the top of the side wall: leans inward and down, so a ball that bounces up meets its underside and falls
                // back in, while a ball that lands on top of it rolls inward off it.
                const float lipLen = 0.5f, lipAngle = 20f * Mathf.Deg2Rad;
                Vector3 inDir = new Vector3(-sgn * Mathf.Cos(lipAngle), -Mathf.Sin(lipAngle), 0f);
                float zMidSide = zFs * 0.5f;
                Vector3 top = new Vector3(sgn * (hw + WallT * 0.25f), YTop(zMidSide) - 0.1f + wallH, zMidSide);
                Vector3 centre = top + slopeRot * (inDir * (lipLen * 0.5f));
                MachineGeometry.Box(t, sgn < 0 ? "Catch Lip L" : "Catch Lip R", centre, slopeRot * Quaternion.FromToRotation(Vector3.right, inDir),
                    new Vector3(lipLen, 0.05f, zFs / cosS), kit.glass);
                // And the same overhang across the top of each back wall segment (leans toward +Z).
                float bzTop = YTop(0f) - 0.1f + wallH;
                Vector3 bIn = new Vector3(0f, -Mathf.Sin(lipAngle), Mathf.Cos(lipAngle));
                MachineGeometry.Box(t, sgn < 0 ? "Back Lip L" : "Back Lip R",
                    new Vector3(sgn * (chuteHalf + WallT + hw + WallT) * 0.5f, bzTop, 0.02f) + bIn * (lipLen * 0.5f),
                    Quaternion.FromToRotation(Vector3.forward, bIn), new Vector3(hw - chuteHalf, 0.05f, lipLen), kit.glass);
            }

            // Exit port: flush with the funnel end, following the platform's slope.
            MachineConnectorBuilder.BuildPort(t, "Exit Port", new[] { P(zFe - 0.02f), P(zEnd) }, kit, config);

            // ---- gate: plate + lane dividers on one kinematic body ----
            var gateGo = new GameObject("Gate");
            gateGo.transform.SetParent(t, false);
            Vector3 gateClosed = new Vector3(0f, YTop(zG), zG);
            gateGo.transform.localPosition = gateClosed;
            gateGo.transform.localRotation = Quaternion.identity;
            // Plate: 0.12 below the floor top (inside the slab, so no seam) to GateHeight - 0.12 above it, spanning the whole inner width.
            MachineGeometry.Box(gateGo.transform, "Gate Plate", new Vector3(0f, (GateHeight - 0.24f) * 0.5f, 0f), Quaternion.identity,
                new Vector3((hw + 0.02f) * 2f, GateHeight, gateHalfT * 2f), kit.castIron);
            MachineGeometry.Box(gateGo.transform, "Gate Stripe", new Vector3(0f, GateHeight - 0.24f - 0.1f, -gateHalfT - 0.004f), Quaternion.identity,
                new Vector3((hw + 0.02f) * 2f, 0.14f, 0.012f), kit.brass, false);
            float rowBackZ = zG - 0.03f - 0.02f - r - gridSpanZ - r - 0.05f;       // rear end of the dividers (horizontal z)
            float divLenH = zG - gateHalfT - rowBackZ;                              // horizontal length
            for (int i = 1; i < columns; i++)
            {
                float x = (i - columns * 0.5f) * pitchX;
                MachineGeometry.Box(gateGo.transform, "Lane Divider " + i,
                    new Vector3(x, divLenH * 0.5f * tanS + (DividerHeight - 0.12f) * 0.5f * cosS, -gateHalfT - divLenH * 0.5f + (DividerHeight - 0.12f) * 0.5f * sinS),
                    slopeRot, new Vector3(0.04f, DividerHeight + 0.12f, divLenH / cosS), kit.chrome);
            }

            station.gate = MachineGeometry.MakeKinematic(gateGo);
            station.gateClosedLocal = gateClosed;
            station.gateSinkDistance = GateSink;
            station.releaseSeconds = 0.25f;

            // ---- grid slots: ball centres on the floor, row 0 against the gate ----
            float frontCentreZ = zG - gateHalfT - 0.02f - r;
            float contactZ = frontCentreZ - r * sinS;
            Vector3 slot0 = P(contactZ) + upN * r;
            slot0.x = -(columns - 1) * 0.5f * pitchX;
            station.columns = columns;
            station.rows = rows;
            station.pitchX = pitchX;
            station.pitchZ = pitchZ;
            station.slot0Local = slot0;
            station.floorForwardLocal = fwdF;
            float cannonZ = zG - 0.5f * (gridSpanZ + 2f * r) - 0.1f;
            station.cannonTargetLocal = P(cannonZ) + upN * (r + 0.1f);
            station.lapLineLocal = P(zL);
            station.pads = new BoostPad[0];

            // ---- chequered lap line on the floor, and the arch over it ----
            Material white = kit.whiteLacquer, black = kit.castIron;
            int cells = Mathf.Max(2, Mathf.RoundToInt(hw * 2f / 0.2f));
            float cellW = hw * 2f / cells;
            for (int row = 0; row < 2; row++)
            {
                float zr = zL + (row - 0.5f) * 0.2f;
                for (int c = 0; c < cells; c++)
                {
                    float x = (c + 0.5f) * cellW - hw;
                    Vector3 p = P(zr) + upN * 0.006f;
                    p.x = x;
                    MachineGeometry.Box(t, "Line Cell", p, slopeRot, new Vector3(cellW, 0.012f, 0.2f / cosS), ((c + row) & 1) == 0 ? white : black, false);
                }
            }

            float archHalf = hw + WallT + 0.3f;
            float archTop = YTop(zL) + ArchHeight;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                float h = archTop - floorY;
                MachineGeometry.Cylinder(t, "Arch Post", new Vector3(sgn * archHalf, floorY + h * 0.5f, zL), Quaternion.identity, 0.09f, h, kit.chrome, false);
                MachineGeometry.Cylinder(t, "Arch Foot", new Vector3(sgn * archHalf, floorY + 0.06f, zL), Quaternion.identity, 0.2f, 0.12f, kit.castIron, false);
            }

            MachineGeometry.Box(t, "Arch Beam", new Vector3(0f, archTop + 0.05f, zL), Quaternion.identity, new Vector3(archHalf * 2f + 0.2f, 0.1f, 0.2f), kit.chrome, false);
            int archCells = Mathf.Max(4, Mathf.RoundToInt(archHalf * 2f / 0.3f));
            float archCellW = archHalf * 2f / archCells;
            for (int row = 0; row < 2; row++)
            {
                for (int c = 0; c < archCells; c++)
                {
                    float x = (c + 0.5f) * archCellW - archHalf;
                    MachineGeometry.Box(t, "Arch Cell", new Vector3(x, archTop - 0.15f - row * 0.3f, zL), Quaternion.identity,
                        new Vector3(archCellW, 0.3f, 0.06f), ((c + row) & 1) == 0 ? white : black, false);
                }
            }

            // Legs to the studio floor under the deck.
            foreach (float zz in new[] { 0.4f, zG, zFe - 0.2f })
            {
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    float top = YTop(zz) - 0.5f;
                    float h = top - floorY;
                    if (h > 0.1f)
                        MachineGeometry.Cylinder(t, "Leg", new Vector3(sgn * (hw + WallT + 0.12f), floorY + h * 0.5f, zz), Quaternion.identity, 0.06f, h, kit.chrome, false);
                }
            }

            // ---- wiring ----
            station.displayName = "Start Gate";
            station.entry = MachineGeometry.Anchor(t, "Entry", Vector3.zero);
            station.exit = MachineGeometry.Anchor(t, "Exit", P(zEnd) + new Vector3(0f, 0.05f, 0f));
            float zFocus = zG - 0.5f * (gridSpanZ + 2f * r);
            station.cameraFocus = MachineGeometry.Anchor(t, "Camera Focus", P(zFocus) + new Vector3(0f, 0.5f, 0f));
            station.cameraOffset = placement.Rotation * new Vector3(-3.5f, 3.2f, 6.5f);
            station.tourSeconds = 8f;
            Vector3 lo = new Vector3(-hw - 0.6f, YTop(zEnd) - 0.4f, -PortBack - 0.1f), hi = new Vector3(hw + 0.6f, wallH + 0.6f, zEnd + 0.2f);
            station.activityBounds = WorldBounds(t, (lo + hi) * 0.5f, hi - lo);
            return station;
        }

        static void Wall(Transform t, Material m, Vector3 a, Vector3 b, float height)
        {
            Vector3 mid = (a + b) * 0.5f + Vector3.up * (height * 0.5f - 0.1f);
            MachineGeometry.Box(t, "Platform Wall", mid, Quaternion.LookRotation(b - a, Vector3.up), new Vector3(WallT, height, (b - a).magnitude + 0.02f), m);
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
