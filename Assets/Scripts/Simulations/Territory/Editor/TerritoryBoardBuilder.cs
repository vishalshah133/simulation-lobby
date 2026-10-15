using SimulationLobby.Shared;
using TMPro;
using UnityEngine;

namespace SimulationLobby.Simulations.Territory.EditorTools
{
    /// <summary>
    /// Builds one team's Plinko board and cannon out of primitives. The board is FLAT on the table: +X across
    /// (left to right as the camera sees it), +Z toward the far end, +Y off the surface. Its "slope" is faked
    /// by the simulation tilting <c>Physics.gravity</c> toward -Z, so balls roll toward the camera and the slot
    /// chips sit on the near (-Z) edge.
    /// </summary>
    static class TerritoryBoardBuilder
    {
        public struct Kit
        {
            public Material charcoal;
            public Material chipPink;
            public Material chipWhite;
            public Material chipGold;
            public Material chipNeutral;
            public PhysicsMaterial boardPhysics;
            public PhysicsMaterial pegPhysics;
            public PhysicsMaterial ballPhysics;
        }

        public struct TeamLook
        {
            public string name;
            public Color color;
            public Material surface;
            public Material ball;
            public Material muzzle;
        }

        public const float WallThickness = 0.12f;
        public const float StopThickness = 0.12f;
        public const float SlotZone = 0.7f;
        public const float DropInset = 0.25f;
        /// <summary>Fraction of the inner length, at the far (drop) end, kept peg-free for the counter decal.</summary>
        public const float CounterZoneFraction = 0.22f;
        public const float FirstRowClearance = 0.1f;
        public const float LastRowGap = 0.3f;

        /// <summary>Surface height above the table top: a hair proud so the board reads as inlaid.</summary>
        public const float SurfaceLift = 0.05f;

        /// <summary>Walls stand about this far above the surface (raised to ball diameter plus margin when the ball is big).</summary>
        public const float WallHeight = 0.45f;

        /// <summary>Decals float this far above the surface they lie on, so they never z-fight it.</summary>
        const float DecalLift = 0.006f;

        static readonly Color DecalOutline = new Color32(0x10, 0x10, 0x16, 255);
        static readonly Color ChipCharcoal = new Color32(0x22, 0x23, 0x2B, 255);

        /// <summary>Smallest centre-to-edge gap between pegs, as a multiple of the ball's diameter, before we warn.</summary>
        const float MinGapRatio = 1.1f;

        /// <param name="boardCentre">World X/Z of the board centre; Y is ignored (the table top sets it).</param>
        public static TerritoryTeamRig BuildRig(int team, TerritoryConfig config, Kit kit, TeamLook look,
            Vector3 boardCentre, Vector3 cannonBase, Transform sceneRoot, bool mirrored = false)
        {
            float length = config.boardLength;
            float width = config.boardWidth;
            float innerWidth = width - 2f * WallThickness;
            float ball = config.ballRadius;
            float obstacleHeight = 2f * ball + 0.03f;
            float pegHeight = 2f * ball;   // pegs stand about one ball diameter tall
            int slotCount = config.boardSlots.Length;

            var rigObject = new GameObject($"Team Rig {look.name}");
            rigObject.transform.SetParent(sceneRoot, false);
            TerritoryTeamRig rig = rigObject.AddComponent<TerritoryTeamRig>();

            // --- Board root: flat, surface top is local y = 0. ---
            var board = new GameObject("Board").transform;
            board.SetParent(rigObject.transform, false);
            board.position = new Vector3(boardCentre.x, TerritoryArenaBuilder.TableTop + SurfaceLift, boardCentre.z);

            // Surface: the team colour darkened. Its collider is what balls roll on.
            Prim(PrimitiveType.Cube, "Surface", board, new Vector3(0f, -0.06f, 0f),
                new Vector3(innerWidth, 0.12f, length), look.surface, true, kit.boardPhysics);

            // Charcoal frame: side walls, far wall, bottom stop, and an outer near bar. The frame is the walls.
            float wallHeight = Mathf.Max(WallHeight, obstacleHeight + 0.08f);
            float wallY = wallHeight * 0.5f - 0.04f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (innerWidth * 0.5f + WallThickness * 0.5f);
                Prim(PrimitiveType.Cube, side < 0 ? "Wall Left" : "Wall Right", board,
                    new Vector3(x, wallY, 0f),
                    new Vector3(WallThickness, wallHeight, length + 2f * WallThickness), kit.charcoal, true, kit.boardPhysics);
            }

            Prim(PrimitiveType.Cube, "Far Wall", board, new Vector3(0f, wallY, length * 0.5f + WallThickness * 0.5f),
                new Vector3(innerWidth, wallHeight, WallThickness), kit.charcoal, true, kit.boardPhysics);
            Prim(PrimitiveType.Cube, "Near Bar", board, new Vector3(0f, wallY, -length * 0.5f - WallThickness * 0.5f),
                new Vector3(innerWidth, wallHeight, WallThickness), kit.charcoal, false, null);

            float stopCentreZ = -length * 0.5f + StopThickness * 0.5f;
            Prim(PrimitiveType.Cube, "Bottom Stop", board, new Vector3(0f, wallY, stopCentreZ),
                new Vector3(innerWidth, wallHeight, StopThickness), kit.charcoal, true, kit.boardPhysics);

            // --- Pegs: staggered rows. Even rows hold pegColumns, odd rows one fewer, offset half a pitch. ---
            float diameter = config.ballRadius * 2f;
            float margin = 1.3f * diameter;
            int fullCount = Mathf.Max(2, config.pegColumns);
            float pitch = PegPitch(fullCount, innerWidth, config.pegRadius, margin);
            while (fullCount > 2 && pitch - 2f * config.pegRadius < 1.2f * diameter)
            {
                fullCount--;
                pitch = PegPitch(fullCount, innerWidth, config.pegRadius, margin);
            }

            if (fullCount != config.pegColumns)
            {
                Debug.LogWarning($"[terr] pegColumns {config.pegColumns} leaves inter-peg gaps under 1.2 ball diameters; " +
                                 $"built {fullCount} columns instead (pitch {pitch:0.000}m). Lower pegColumns in the config.");
            }
            float innerLength = length - StopThickness;
            float zoneLength = CounterZoneFraction * innerLength;
            float zoneTopZ = length * 0.5f;
            float zoneBottomZ = zoneTopZ - zoneLength;
            float zoneCentreZ = zoneTopZ - zoneLength * 0.5f;
            float topRowZ = zoneBottomZ - config.pegRadius - FirstRowClearance;
            float bottomRowZ = -length * 0.5f + StopThickness + SlotZone + LastRowGap;
            int rows = Mathf.Max(1, config.pegRows);
            float rowSpacing = rows > 1 ? (topRowZ - bottomRowZ) / (rows - 1) : 0f;
            var pegs = new GameObject("Pegs").transform;
            pegs.SetParent(board, false);
            // The last row is a funnel: slotCount - 1 pegs sitting on the slot DIVIDER x positions, so the gaps
            // between them line up with the slot centres and every slot gets a fair share of the balls.
            float funnelSlotPitch = innerWidth / slotCount;
            float funnelGap = funnelSlotPitch - 2f * config.pegRadius;

            // --- SPIN lane partition: a tall wall on the slot-0 / slot-1 divider, so SPIN is a narrow lane along the outer wall. ---
            const float DividerThickness = 0.06f;
            float slotStartZ = -length * 0.5f + StopThickness;
            float partitionTopZ = slotStartZ + Mathf.Clamp(config.spinLaneFraction, 0f, 0.8f) * innerLength;
            bool hasPartition = slotCount >= 2 && config.spinLaneFraction > 0f && partitionTopZ > slotStartZ + SlotZone;
            float outward = mirrored ? 1f : -1f;   // +X on mirrored boards, -X otherwise
            // The slot-0/slot-1 divider sits one slot pitch in from the OUTER wall, i.e. on the outward side of centre.
            float partitionX = outward * (innerWidth * 0.5f - funnelSlotPitch);
            // --- Lane gate: a diagonal plank from the partition top to the OUTER wall's inner face, sloping so balls slide inward. ---
            bool hasGate = hasPartition && config.laneGateEnabled;
            float outerX = outward * innerWidth * 0.5f;                 // outer wall inner face: -X left boards, +X mirrored
            float gateLaneWidth = Mathf.Abs(outerX - partitionX);      // laneClearWidth measured partition axis to wall face
            float gateTan = Mathf.Tan(config.laneGateAngle * Mathf.Deg2Rad);
            Vector2 gateLow = new Vector2(partitionX, partitionTopZ);                                    // (x, z) at the partition top
            Vector2 gateHigh = new Vector2(outerX, partitionTopZ + gateLaneWidth * gateTan);            // (x, z) at the outer wall
            int gatePegsRemoved = 0;
            int pegsRemoved = 0;
            var removedList = new System.Text.StringBuilder();
            for (int row = 0; row < rows; row++)
            {
                bool funnel = row == rows - 1 && slotCount >= 2;
                bool full = row % 2 == 0;
                int count = funnel ? slotCount - 1 : (full ? fullCount : Mathf.Max(1, fullCount - 1));
                float z = topRowZ - row * rowSpacing;
                for (int i = 0; i < count; i++)
                {
                    float x = funnel
                        ? -innerWidth * 0.5f + (i + 1) * funnelSlotPitch
                        : (i - (count - 1) * 0.5f) * pitch;
                    if (hasPartition && z - config.pegRadius <= partitionTopZ)
                    {
                        float dx = x - partitionX;
                        bool inLane = dx * outward > 0f;
                        float gapToPartition = Mathf.Abs(dx) - config.pegRadius - DividerThickness * 0.5f;
                        if (inLane || gapToPartition < margin)
                        {
                            pegsRemoved++;
                            removedList.Append($" (row {row} x {x:0.000} z {z:0.000})");
                            continue;
                        }
                    }

                    if (hasGate)
                    {
                        float outDist = (x - partitionX) * outward;   // distance from the partition toward the outer wall
                        bool inTriangle = outDist > 0f && outDist < gateLaneWidth && z > partitionTopZ && z < partitionTopZ + outDist * gateTan;
                        float gateGap = PointSegmentDistance(new Vector2(x, z), gateLow, gateHigh) - config.pegRadius - DividerThickness * 0.5f;
                        if (inTriangle || gateGap < margin)
                        {
                            pegsRemoved++;
                            gatePegsRemoved++;
                            removedList.Append($" [gate: row {row} x {x:0.000} z {z:0.000} gap {gateGap:0.000}]");
                            continue;
                        }
                    }

                    GameObject peg = Prim(PrimitiveType.Cylinder, "Peg", pegs, new Vector3(x, pegHeight * 0.5f, z),
                        new Vector3(config.pegRadius * 2f, pegHeight * 0.5f, config.pegRadius * 2f), kit.charcoal, true, kit.pegPhysics);
                    peg.GetComponent<Collider>().sharedMaterial = kit.pegPhysics;
                }
            }

            if (hasPartition)
            {
                float laneClear = funnelSlotPitch - DividerThickness;
                Debug.Log($"[terr] Board {look.name}: SPIN partition at x {partitionX:0.000}, z {slotStartZ:0.00}..{partitionTopZ:0.00} " +
                          $"({config.spinLaneFraction:0.00} of inner length {innerLength:0.00}m), lane clear width {laneClear:0.000}m " +
                          $"({laneClear / diameter:0.00} ball diameters), pegs removed {pegsRemoved}:{removedList}.");
                if (laneClear < 1.2f * diameter)
                {
                    Debug.LogWarning($"[terr] SPIN lane too narrow: {laneClear:0.000}m, needs >= {1.2f * diameter:0.000}m (1.2 x ball diameter). " +
                                     "Fewer boardSlots, a smaller ballRadius, or a wider board.");
                }
            }

            float gateMinGap = float.MaxValue;
            if (hasGate)
            {
                // Re-measure the survivors: the smallest clear gap from any remaining peg to the plank.
                foreach (Transform peg in pegs)
                {
                    float g = PointSegmentDistance(new Vector2(peg.localPosition.x, peg.localPosition.z), gateLow, gateHigh)
                              - config.pegRadius - DividerThickness * 0.5f;
                    gateMinGap = Mathf.Min(gateMinGap, g);
                }

                Debug.Log($"[terr] Board {look.name}: LANE GATE plank (x, z) ({gateLow.x:0.000}, {gateLow.y:0.000}) -> ({gateHigh.x:0.000}, {gateHigh.y:0.000}) " +
                          $"at {config.laneGateAngle:0}deg, outer wall face x {outerX:0.000}, partition x {partitionX:0.000}, " +
                          $"pegs removed for gate {gatePegsRemoved}, min peg clearance to plank {(gateMinGap == float.MaxValue ? 0f : gateMinGap):0.000}m " +
                          $"(margin {margin:0.000}m).");
            }

            WarnIfTight(config, pitch, rowSpacing, innerWidth, slotCount);
            if (slotCount >= 2)
            {
                float funnelZ = topRowZ - (rows - 1) * rowSpacing;
                float slotTopZ = -length * 0.5f + StopThickness + SlotZone;
                float firstX = -innerWidth * 0.5f + funnelSlotPitch;
                float lastX = -innerWidth * 0.5f + (slotCount - 1) * funnelSlotPitch;
                Debug.Log($"[terr] Board {look.name}: funnel row z {funnelZ:0.00}, {slotCount - 1} pegs at divider x " +
                          $"{firstX:0.000}..{lastX:0.000} (pitch {funnelSlotPitch:0.000}m), gap {funnelGap:0.000}m " +
                          $"({funnelGap / diameter:0.00} ball diameters), clearance above slot chips {funnelZ - config.pegRadius - slotTopZ:0.000}m " +
                          $"(peg edge to divider tops; centre {funnelZ - slotTopZ:0.000}m).");
                if (funnelGap < 1.2f * diameter)
                {
                    Debug.LogWarning($"[terr] Funnel row gaps too tight: slot pitch {funnelSlotPitch:0.000}m - 2 x pegRadius " +
                                     $"{config.pegRadius:0.000}m = {funnelGap:0.000}m, needs >= {1.2f * diameter:0.000}m (1.2 x ball diameter " +
                                     $"{diameter:0.000}m). Fewer boardSlots, smaller pegRadius/ballRadius, or a wider board.");
                }
            }
            Debug.Log($"[terr] Board {look.name}: counter zone {zoneLength:0.00}m (z {zoneBottomZ:0.00}..{zoneTopZ:0.00}), " +
                      $"first peg row z {topRowZ:0.00}, row spacing {rowSpacing:0.000}m.");

            // --- Slots: coloured chips on the near edge, charcoal dividers, trigger boxes. ---
            float slotWidth = innerWidth / slotCount;
            const float chipHeight = 0.014f;
            var slotsRoot = new GameObject("Slots").transform;
            slotsRoot.SetParent(board, false);
            var zones = new TriggerZone3D[slotCount];
            var chipTexts = new TMP_Text[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                // Slot 0 is the board's OUTER edge: -X on left boards, +X on mirrored (right) boards.
                float x = mirrored
                    ? innerWidth * 0.5f - (i + 0.5f) * slotWidth
                    : -innerWidth * 0.5f + (i + 0.5f) * slotWidth;
                float chipZ = slotStartZ + SlotZone * 0.5f;

                Prim(PrimitiveType.Cube, $"Slot Chip {i + 1}", slotsRoot,
                    new Vector3(x, chipHeight * 0.5f, chipZ),
                    new Vector3(slotWidth - 0.08f, chipHeight, SlotZone - 0.04f), ChipMaterial(kit, config.boardSlots[i].kind), false, null);

                var trigger = new GameObject($"Slot {i + 1}");
                trigger.transform.SetParent(slotsRoot, false);
                trigger.transform.localPosition = new Vector3(x, obstacleHeight * 0.5f, chipZ);
                var box = trigger.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(slotWidth * 0.8f, obstacleHeight, SlotZone * 0.7f);
                zones[i] = trigger.AddComponent<TriggerZone3D>();

                // Chip label: a flat decal sized to the chip, auto-sized so it can never overflow it.
                SlotEffect effect = config.boardSlots[i];
                bool whiteText = effect.kind == SlotEffectKind.Multiply;
                chipTexts[i] = Decal($"Chip Label {i + 1}", slotsRoot, new Vector3(x, chipHeight + DecalLift, chipZ),
                    new Vector2(slotWidth - 0.14f, SlotZone - 0.12f), effect.Label, 6f,
                    whiteText ? Color.white : ChipCharcoal, FontStyles.Bold, whiteText ? 0.18f : 0f);
            }

            for (int i = 1; i < slotCount; i++)
            {
                float x = -innerWidth * 0.5f + i * slotWidth;
                if (hasPartition && Mathf.Abs(x - partitionX) < 1e-3f)
                {
                    continue;   // the partition replaces this divider
                }

                Prim(PrimitiveType.Cube, $"Divider {i}", slotsRoot,
                    new Vector3(x, obstacleHeight * 0.5f, slotStartZ + SlotZone * 0.5f),
                    new Vector3(DividerThickness, obstacleHeight, SlotZone), kit.charcoal, true, kit.boardPhysics);
            }

            if (hasPartition)
            {
                // Straight wall up to a rounded cap (a vertical cylinder of the same thickness) so balls glance off the top end.
                float capR = DividerThickness * 0.5f;
                float wallTopZ = partitionTopZ - capR;
                float wallLen = wallTopZ - slotStartZ;
                Prim(PrimitiveType.Cube, "SPIN Partition", slotsRoot,
                    new Vector3(partitionX, obstacleHeight * 0.5f, slotStartZ + wallLen * 0.5f),
                    new Vector3(DividerThickness, obstacleHeight, wallLen), kit.charcoal, true, kit.boardPhysics);
                Prim(PrimitiveType.Cylinder, "SPIN Partition Cap", slotsRoot,
                    new Vector3(partitionX, obstacleHeight * 0.5f, wallTopZ),
                    new Vector3(DividerThickness, obstacleHeight * 0.5f, DividerThickness), kit.charcoal, true, kit.boardPhysics);
            }

            if (hasGate)
            {
                // Overlap the partition top and the outer wall so no ball-sized hole is left at either end.
                Vector2 dir2 = (gateHigh - gateLow).normalized;
                float overlap = DividerThickness + 0.02f;
                Vector2 a = gateLow - dir2 * overlap;
                Vector2 b = gateHigh + dir2 * overlap;
                Vector2 mid = (a + b) * 0.5f;
                GameObject gate = Prim(PrimitiveType.Cube, "SPIN Lane Gate", slotsRoot,
                    new Vector3(mid.x, obstacleHeight * 0.5f, mid.y),
                    new Vector3(DividerThickness, obstacleHeight, (b - a).magnitude), kit.charcoal, true, kit.boardPhysics);
                gate.transform.localRotation = Quaternion.LookRotation(new Vector3(dir2.x, 0f, dir2.y), Vector3.up);
                rig.laneGate = gate.GetComponent<Collider>();
                rig.laneGateRenderer = gate.GetComponent<Renderer>();
            }

            // --- Cover: an invisible collider just above ball height, so nothing can hop out. No renderer. ---
            var cover = new GameObject("Cover Collider");
            cover.transform.SetParent(board, false);
            cover.transform.localPosition = new Vector3(0f, obstacleHeight + 0.02f, 0f);
            var coverBox = cover.AddComponent<BoxCollider>();
            coverBox.size = new Vector3(innerWidth, 0.04f, length);
            coverBox.sharedMaterial = kit.boardPhysics;

            // --- Ball template: inactive, cloned into the pool by the simulation. ---
            var templates = new GameObject("Ball Template Holder").transform;
            templates.SetParent(rigObject.transform, false);
            GameObject template = BuildBallTemplate(templates, config, kit, look);

            var ballParent = new GameObject("Balls").transform;
            ballParent.SetParent(rigObject.transform, false);

            // --- Anchors. ---
            var dropPoint = new GameObject("Drop Point").transform;
            dropPoint.SetParent(board, false);
            dropPoint.localPosition = new Vector3(0f, ball + 0.01f, length * 0.5f - DropInset);
            dropPoint.localRotation = Quaternion.identity;

            // Painted in the peg-free zone at the far end: counter centred in it, FIRING just below the counter
            // (still inside the zone), both within the zone's bounds.
            float counterHeight = zoneLength * 0.55f;
            float firingHeight = zoneLength * 0.2f;
            float firingZ = -counterHeight * 0.5f - 0.04f - firingHeight * 0.5f;
            var counterAnchor = new GameObject("Counter Anchor").transform;
            counterAnchor.SetParent(board, false);
            counterAnchor.localPosition = new Vector3(0f, DecalLift, zoneCentreZ);
            TMP_Text counterText = Decal("Counter", counterAnchor, Vector3.zero, new Vector2(innerWidth * 0.6f, counterHeight),
                config.bankStart.ToString(), 12f, Color.white, FontStyles.Bold | FontStyles.Italic, 0.3f);
            TMP_Text firingText = Decal("Firing", counterAnchor, new Vector3(0f, 0f, firingZ), new Vector2(innerWidth * 0.7f, firingHeight),
                string.Empty, 3.6f, Color.white, FontStyles.Bold | FontStyles.Italic, 0.25f);
            Debug.Log($"[terr] Board {look.name} decals (local z): counter {counterHeight:0.00}m tall at {zoneCentreZ:0.00} " +
                      $"({zoneCentreZ - counterHeight * 0.5f:0.00}..{zoneCentreZ + counterHeight * 0.5f:0.00}), firing {firingHeight:0.00}m tall at " +
                      $"{zoneCentreZ + firingZ:0.00} ({zoneCentreZ + firingZ - firingHeight * 0.5f:0.00}..{zoneCentreZ + firingZ + firingHeight * 0.5f:0.00}).");

            Transform pivot = BuildCannon(rigObject.transform, cannonBase, kit, look, out Transform muzzle);

            rig.ballTemplate = template;
            rig.ballParent = ballParent;
            rig.dropPoint = dropPoint;
            rig.dropHalfWidth = Mathf.Max(0.2f, innerWidth * 0.5f - ball * 2f - 0.1f);
            rig.slots = zones;
            rig.counterAnchor = counterAnchor;
            rig.counterText = counterText;
            rig.firingText = firingText;
            rig.slotTexts = chipTexts;
            rig.cannonPivot = pivot;
            rig.cannonMuzzle = muzzle;
            return rig;
        }

        // ------------------------------------------------------------------ pieces

        static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static Material ChipMaterial(Kit kit, SlotEffectKind kind)
        {
            switch (kind)
            {
                case SlotEffectKind.Multiply:
                    return kit.chipPink;
                case SlotEffectKind.Release:
                    return kit.chipWhite;
                case SlotEffectKind.Bonus:
                    return kit.chipGold;
                default:
                    return kit.chipNeutral;
            }
        }

        static GameObject BuildBallTemplate(Transform parent, TerritoryConfig config, Kit kit, TeamLook look)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = $"Ball Template {look.name}";
            ball.transform.SetParent(parent, false);
            ball.transform.localScale = Vector3.one * (config.ballRadius * 2f);
            ball.GetComponent<MeshRenderer>().sharedMaterial = look.ball;

            var collider = ball.GetComponent<SphereCollider>();
            collider.sharedMaterial = kit.ballPhysics;

            var body = ball.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            ball.AddComponent<CollisionDetector3D>();
            ball.SetActive(false);
            return ball;
        }

        /// <summary>
        /// Chunky toy cannon (1.6x the v2 size): a 1.4 m charcoal base disc, a team-coloured 1.3 m x 0.5 m barrel
        /// and cap on a yaw pivot, a dark muzzle ring. Visual only: no colliders. The pivot +Z runs along the
        /// barrel; the barrel and muzzle are its children, so the presenter yaws the pivot about world Y.
        /// </summary>
        static Transform BuildCannon(Transform parent, Vector3 baseWorld, Kit kit, TeamLook look, out Transform muzzle)
        {
            var root = new GameObject("Cannon").transform;
            root.SetParent(parent, false);
            root.position = baseWorld;

            Prim(PrimitiveType.Cylinder, "Cannon Base", root, new Vector3(0f, 0.11f, 0f),
                new Vector3(1.4f, 0.11f, 1.4f), kit.charcoal, false, null);

            var pivot = new GameObject("Cannon Pivot").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(0f, 0.48f, 0f);

            Prim(PrimitiveType.Sphere, "Cannon Cap", pivot, Vector3.zero, Vector3.one * 0.7f, look.ball, false, null);

            // Cylinders run along local Y, so lay them down onto +Z. Height = 2 * scale.y: 1.3 m long, 0.5 m thick.
            GameObject barrel = Prim(PrimitiveType.Cylinder, "Barrel", pivot, new Vector3(0f, 0f, 0.65f),
                new Vector3(0.5f, 0.65f, 0.5f), look.ball, false, null);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            GameObject ring = Prim(PrimitiveType.Cylinder, "Muzzle Ring", pivot, new Vector3(0f, 0f, 1.25f),
                new Vector3(0.6f, 0.065f, 0.6f), look.muzzle, false, null);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            muzzle = new GameObject("Cannon Muzzle").transform;
            muzzle.SetParent(pivot, false);
            muzzle.localPosition = new Vector3(0f, 0f, 1.36f);
            return pivot;
        }

        /// <summary>
        /// A TextMeshPro 3D decal lying flat on a surface: its up axis points to the far end and its face to the
        /// sky, so it reads upright from the camera. Auto-sizing inside <paramref name="size"/> keeps the text
        /// within its rect at any length.
        /// </summary>
        static TMP_Text Decal(string name, Transform parent, Vector3 localPosition, Vector2 size, string text, float maxFontSize,
            Color color, FontStyles style, float outline)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = size;
            tmp.text = text;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = maxFontSize;
            tmp.fontSizeMin = maxFontSize * 0.1f;
            tmp.fontSize = maxFontSize;
            if (outline > 0f)
            {
                tmp.outlineWidth = outline;
                tmp.outlineColor = DecalOutline;
            }

            return tmp;
        }

        /// <summary>Pitch that leaves exactly <paramref name="margin"/> between the outer peg's edge and the wall.</summary>
        static float PegPitch(int fullCount, float innerWidth, float pegRadius, float margin)
        {
            return (innerWidth - 2f * (pegRadius + margin)) / Mathf.Max(1, fullCount - 1);
        }

        static void WarnIfTight(TerritoryConfig config, float pitch, float rowSpacing, float innerWidth, int slotCount)
        {
            float diameter = config.ballRadius * 2f;
            float rowGap = pitch - config.pegRadius * 2f;
            float diagonal = Mathf.Sqrt(pitch * pitch * 0.25f + rowSpacing * rowSpacing) - config.pegRadius * 2f;
            float slotGap = innerWidth / slotCount - 0.05f;
            float worst = Mathf.Min(rowGap, Mathf.Min(rowSpacing > 0f ? diagonal : rowGap, slotGap));
            if (worst < diameter * MinGapRatio)
            {
                Debug.LogWarning($"[terr] Plinko gaps are tight: smallest clear gap {worst:0.000}m vs ball diameter {diameter:0.000}m " +
                                 $"(row {rowGap:0.000}, diagonal {diagonal:0.000}, slot {slotGap:0.000}). Balls may wedge: " +
                                 "fewer pegColumns/pegRows, a smaller ballRadius, or a longer board.");
            }
        }

        /// <summary>Creates a primitive under <paramref name="parent"/>. Colliders are removed unless asked for.</summary>
        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, bool collider, PhysicsMaterial physics)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (material != null)
            {
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            Collider existing = go.GetComponent<Collider>();
            if (!collider)
            {
                Object.DestroyImmediate(existing);
            }
            else if (physics != null)
            {
                existing.sharedMaterial = physics;
            }

            return go;
        }
    }
}
