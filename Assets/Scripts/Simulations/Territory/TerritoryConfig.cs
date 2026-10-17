using System;
using SimulationLobby.Core;
using SimulationLobby.Shared;
using UnityEngine;

namespace SimulationLobby.Simulations.Territory
{
    /// <summary>One colour in the war: a name and a colour.</summary>
    [Serializable]
    public struct TerritoryTeamDef
    {
        public string name;
        public Color color;

        [Tooltip("Optional short name for tight spots (share bar label): IND, AUS. Empty uses name.")]
        public string shortName;

        public TerritoryTeamDef(string name, Color color, string shortName = null)
        {
            this.name = name;
            this.color = color;
            this.shortName = shortName;
        }

        /// <summary>shortName, or name when it is empty.</summary>
        public string Short => string.IsNullOrEmpty(shortName) ? name : shortName;
    }

    /// <summary>One face on the spin reel.</summary>
    [Serializable]
    public struct SpinFace
    {
        public SpinFaceKind kind;

        [Tooltip("Used when kind is Economy.")]
        public SlotEffect effect;

        [Tooltip("Optional override for Bomb (shot count), PlusLine (thickness) and SelfDestruct (radius); for Boost it is the factor. 0 uses the config field.")]
        [Min(0)] public int value;

        [Min(0)] public int weight;

        public SpinFace(SlotEffect effect, int weight)
        {
            kind = SpinFaceKind.Economy;
            this.effect = effect;
            value = 0;
            this.weight = weight;
        }

        public SpinFace(SpinFaceKind kind, int weight, int value = 0)
        {
            this.kind = kind;
            effect = default;
            this.value = value;
            this.weight = weight;
        }

        /// <summary>Short text for a drum face: FIRE, BOMB, +LINE, BOOM, or the economy label (x2, +25...).</summary>
        public string Label
        {
            get
            {
                switch (kind)
                {
                    case SpinFaceKind.Bomb: return "BOMB";
                    case SpinFaceKind.PlusLine: return "+LINE";
                    case SpinFaceKind.SelfDestruct: return "BOOM";
                    case SpinFaceKind.Boost: return "×" + value;
                    case SpinFaceKind.CannonStrike: return "BOOM";
                    case SpinFaceKind.Rain: return "RAIN";
                    default: return effect.kind == SlotEffectKind.Release ? "FIRE" : effect.Label;
                }
            }
        }
    }

    /// <summary>
    /// Tunables for the four-colour territory war. A published video is this asset plus a scene plus a seed.
    /// </summary>
    [CreateAssetMenu(fileName = "TerritoryConfig", menuName = "Simulation Lobby/Configs/Territory", order = 40)]
    public sealed class TerritoryConfig : SimulationConfig
    {
        /// <summary>Bump when the layout defaults change; the scene builder migrates older configs.</summary>
        public const int CurrentLayoutVersion = 7;

        // v3-v7 layout defaults: the ONE place they live (field initialisers and ResetLayoutToDefaults both read these).
        const int DefaultGridWidth = 36;
        const int DefaultGridHeight = 48;
        const float DefaultCellSize = 1f / 3f;   // 36 x 48 cells still spans 12 m x 16 m
        const int DefaultBaseInsetCells = 3;
        const float DefaultBoardWidth = 4.5f;
        const float DefaultBoardLength = 7.4f;
        const float DefaultBoardTiltDegrees = 25f;
        const int DefaultPegRows = 6;
        const int DefaultPegColumns = 4;
        const float DefaultPegRadius = 0.17f;
        const float DefaultBallRadius = 0.17f;
        const int DefaultMaxQueuedSpins = 1;
        const int DefaultMaxLiveShotsPerTeam = 60;
        const float DefaultBoostSeconds = 10f;
        const int DefaultBombShots = 3;
        const int DefaultBombRadius = 4;
        const int DefaultLineThickness = 2;
        const int DefaultSelfDestructRadius = 4;
        const float DefaultTileHeight = 0.03f;
        const int DefaultDropSpreadSamples = 3;
        const float DefaultDropSpreadFraction = 0.7f;
        const float DefaultSpinLaneFraction = 0.5f;

        // Deliberately no initialiser: Unity keeps a field's C# default when the YAML lacks it, so initialising
        // to CurrentLayoutVersion would make a pre-v3 asset look current and skip the builder's migration.
        [Tooltip("Layout generation this asset was last reset to. 0 means never reset; the scene builder migrates it.")]
        public int layoutVersion;

        [Header("Teams (quadrant order: 0 top-left, 1 top-right, 2 bottom-left, 3 bottom-right)")]
        public TerritoryTeamDef[] teams = DefaultTeams();

        [Header("Grid")]
        [Min(8)] public int gridWidth = DefaultGridWidth;
        [Min(8)] public int gridHeight = DefaultGridHeight;
        [Min(0.01f)] public float cellSize = DefaultCellSize;

        [Tooltip("How far in from its outer corner a cannon sits, in cells.")]
        [Min(0)] public int baseInsetCells = DefaultBaseInsetCells;

        [Header("Cannons")]
        [Min(1f)] public float sweepDegreesPerSecond = 30f;
        [Range(0f, 30f)] public float sweepMarginDegrees = 5f;
        [Min(0.5f)] public float shotSpeedCellsPerSecond = 12f;

        [Tooltip("Ammo one normal shot carries and consumes. 1 = one count, one ball (the FIRING counter drops by one per ball).")]
        [Min(1)] public int shotHealth = 1;
        [Min(0.01f)] public float fireIntervalSeconds = 0.08f;

        [Min(1)] public int maxLiveShotsPerTeam = DefaultMaxLiveShotsPerTeam;
        [Min(1f)] public float shotMaxLifetimeSeconds = 25f;

        [Header("Plinko boards")]
        [Tooltip("Index 0 is the board's OUTER edge (right-hand boards are mirrored by the builder). A Bonus slot (SPIN) triggers the reel.")]
        public SlotEffect[] boardSlots = DefaultBoardSlots();

        [Min(1)] public int ballsPerBoard = 6;
        [Min(0.05f)] public float dropIntervalSeconds = 0.85f;

        [Tooltip("Drop x is the mean of this many uniform draws (an Irwin-Hall bell, so drops favour the centre). 1 = uniform.")]
        [Range(1, 6)] public int dropSpreadSamples = DefaultDropSpreadSamples;

        [Tooltip("Fraction of the rig's dropHalfWidth the bell spans.")]
        [Range(0.1f, 1f)] public float dropSpreadFraction = DefaultDropSpreadFraction;
        [Tooltip("Height of the partition wall between the SPIN slot and its neighbour, as a fraction of the board's inner length. 0 = no partition.")]
        [Range(0f, 0.8f)] public float spinLaneFraction = DefaultSpinLaneFraction;

        [Tooltip("Rule: a diagonal plank roofs over the SPIN lane and slides balls inward. Off = no gate is built and the lane is always open.")]
        public bool laneGateEnabled = true;

        [Tooltip("Seconds the lane gate is solid per cycle. Rule field: the simulation owns the collider state.")]
        [Min(0.1f)] public float laneGateClosedSeconds = 5f;

        [Tooltip("Seconds the lane gate is gone per cycle, the window in which balls can reach SPIN.")]
        [Min(0.1f)] public float laneGateOpenSeconds = 1f;

        [Tooltip("Progression: seconds added to every lane gate's open window per team eliminated so far. 0 = off (terr-001).")]
        [Min(0f)] public float laneGateOpenPerElimination;

        [Tooltip("Slope of the lane gate plank in degrees from horizontal. Its upper end is at the outer wall.")]
        [Range(20f, 70f)] public float laneGateAngle = 45f;
        [Min(1)] public int bankStart = 1;
        [Min(1)] public long bankCap = 4096;

        [Tooltip("When a Multiply or Add brings a bank to bankCap, release it immediately (Released event with other = 1) instead of letting it pin at the cap.")]
        public bool autoReleaseAtCap = true;

        [Tooltip("A board ball alive this long is put back in the pool.")]
        [Min(1f)] public float ballStuckSeconds = 12f;

        [Tooltip("A board ball this far below the board's lowest point, or further from the drop point than the board is long plus wide, is recovered.")]
        [Min(0.5f)] public float ballRecoverMargin = 2f;

        [Header("Spinner")]
        public SpinFace[] spinFaces = DefaultSpinFaces();
        [Min(0.5f)] public float spinSeconds = 3.5f;
        [Min(1)] public int maxQueuedSpins = DefaultMaxQueuedSpins;

        [Tooltip("BOMB face: bomb shots fired at once (they do not consume ammo).")]
        [Min(1)] public int bombShots = DefaultBombShots;

        [Tooltip("Bomb blast radius in cells.")]
        [Min(1)] public int bombRadius = DefaultBombRadius;

        [Tooltip("+LINE face: thickness in cells of the painted row and column.")]
        [Min(1)] public int lineThickness = DefaultLineThickness;

        [Tooltip("Boost faces: seconds the team's Multiply slots use the face's factor instead of their own.")]
        [Min(0.5f)] public float boostSeconds = DefaultBoostSeconds;

        [Tooltip("SELF DESTRUCT face: radius in cells of the hole blasted in the team's own territory.")]
        [Min(1)] public int selfDestructRadius = DefaultSelfDestructRadius;

        // terr-002 faces. Not layout fields: older assets load them at these defaults, and layoutVersion is unchanged.
        [Tooltip("BOOM (CannonStrike) face: whose cannon goes down.")]
        public CannonStrikeTarget cannonStrikeTarget = CannonStrikeTarget.Leader;

        [Tooltip("BOOM (CannonStrike) face: seconds the cannon is down (no firing, aim frozen). value > 0 on the face overrides it.")]
        [Min(0.5f)] public float cannonDownSeconds = 10f;

        [Tooltip("RAIN face: drops scheduled per rain (value > 0 on the face overrides it).")]
        [Min(1)] public int rainDrops = 36;

        [Tooltip("RAIN face: seconds over which the drops land.")]
        [Min(0.2f)] public float rainSeconds = 4f;

        [Tooltip("RAIN face: seconds between the face resolving and the first drop landing (fall time for the animation).")]
        [Min(0f)] public float rainLeadSeconds = 0.8f;

        [Tooltip("RAIN face: splash radius in cells (GridPainter.PaintDisc).")]
        [Min(0)] public int rainSplashRadius = 1;

        [Tooltip("RAIN face: no drop lands, and no splash paints, within this many cells (Chebyshev) of ANY base cell. Rain never touches a cannon.")]
        [Min(1)] public int rainCannonClearance = 3;

        [Tooltip("Base plate radius in cells, centred on the base cell. An enemy ball painting any cell that overlaps it knocks the team out, and BOMB / LINE / RAIN can never paint those cells. 0 = the base cell alone (terr-001). The builder sizes the cannon's base plate to match.")]
        [Min(0f)] public float basePlateRadiusCells;

        [Header("Progression (terr-002; every field off by default)")]
        [Tooltip("A knock-out hands the eliminated team's bank to the team that reached its plate, plus a free spin.")]
        public bool knockoutReward;

        [Tooltip("Per team eliminated so far, cannons sweep and fire this fraction faster (0.25: x1.25, x1.5, x1.75). 0 = off.")]
        [Min(0f)] public float cannonSpeedupPerElimination;

        [Tooltip("Final showdown: when two teams are left, their base plates grow to this radius in cells (claiming the cells under them) and SPIN lanes stay open. 0 = no showdown.")]
        [Min(0f)] public float showdownPlateRadiusCells;

        [Tooltip("Heavy shots: from this many battle seconds, every cell a normal ball paints splashes a disc of heavyShotRadius (BOMB / LINE protection still applies). 0 = off.")]
        [Min(0f)] public float heavyShotsAfterSeconds;

        [Min(1)] public int heavyShotRadius = 1;

        [Header("Look (read by the scene builder and the presenter)")]
        [Tooltip("Colours, floor pattern and HUD text. Empty = the terr-001 look (TerritoryTheme defaults).")]
        public TerritoryTheme theme;

        [NonSerialized] TerritoryTheme _classicTheme;

        /// <summary>The theme to use: <see cref="theme"/>, or an in-memory terr-001 classic when it is empty. Never null.</summary>
        public TerritoryTheme Theme
        {
            get
            {
                if (theme != null)
                {
                    return theme;
                }

                if (_classicTheme == null)
                {
                    _classicTheme = TerritoryTheme.CreateClassic();
                }

                return _classicTheme;
            }
        }

        [Header("Escalation (battle seconds)")]
        [Min(0f)] public float escalateAfterSeconds = 360f;
        [Min(1f)] public float escalateEverySeconds = 60f;
        [Range(0.2f, 1f)] public float escalateDropFactor = 0.8f;
        [Min(0.05f)] public float minDropIntervalSeconds = 0.35f;

        [Tooltip("Every this many battle seconds, every multiplier (board ×n slots, spinner ×n faces, boosts) gains " +
                 "multiplierRampStep: ×2 becomes ×3, then ×4. 0 = off (terr-001). terr-002 uses 300.")]
        [Min(0f)] public float multiplierRampSeconds = 0f;

        [Min(1)] public int multiplierRampStep = 1;

        [Header("Timing")]
        [Min(0f)] public float preStartHoldSeconds = 5f;
        [Min(0f)] public float endHoldSeconds = 10f;

        [Tooltip("Battle seconds at which a stalled run is decided on tile count.")]
        [Min(10f)] public float decisionAtSeconds = 1200f;

        [Header("Camera (read by the scene builder)")]
        [Tooltip("Use the hand-framed pose below instead of the builder's automatic fit. The default is the framing the " +
                 "user picked in the editor on 2026-10-12. Turn it off after changing the grid or board geometry.")]
        public bool useCameraOverride = true;
        public Vector3 cameraPosition = new Vector3(-0.06f, 14.48f, -5.9f);
        public Quaternion cameraRotation = new Quaternion(0.3907301f, 0.0020404f, -0.0008661f, 0.9205027f);

        [Header("Board geometry (read by the scene builder; changing it means rebuilding the scene)")]
        [Min(1f)] public float boardWidth = DefaultBoardWidth;
        [Min(1f)] public float boardLength = DefaultBoardLength;
        [Tooltip("Effective slope in degrees. The boards are built flat; the simulation tilts gravity by this angle so balls roll toward the camera.")]
        [Range(5f, 60f)] public float boardTiltDegrees = DefaultBoardTiltDegrees;
        [Min(1)] public int pegRows = DefaultPegRows;
        [Min(1)] public int pegColumns = DefaultPegColumns;
        [Min(0.01f)] public float pegRadius = DefaultPegRadius;
        [Min(0.03f)] public float ballRadius = DefaultBallRadius;

        [Tooltip("Thickness of a grid tile. Read by the scene builder and the presenter so shots ride on the tile tops.")]
        [Min(0.005f)] public float tileHeight = DefaultTileHeight;

        public TerritoryConfig()
        {
            // ScriptableObject.Reset() only runs for assets made in the Create menu; code-created assets
            // (the scene builder) would otherwise keep the base class's 120s cap.
            maxDurationSeconds = 1500f;
            fixedTimestep = 1f / 60f;
        }

        /// <summary>Resets only the layout and rules fields (grid, board, pegs, slots, tile height, spin faces, queue, shot caps, bomb/line/self-destruct, boost seconds) to the v7 defaults.</summary>
        public void ResetLayoutToDefaults()
        {
            gridWidth = DefaultGridWidth;
            gridHeight = DefaultGridHeight;
            cellSize = DefaultCellSize;
            baseInsetCells = DefaultBaseInsetCells;
            boardWidth = DefaultBoardWidth;
            boardLength = DefaultBoardLength;
            boardTiltDegrees = DefaultBoardTiltDegrees;
            pegRows = DefaultPegRows;
            pegColumns = DefaultPegColumns;
            pegRadius = DefaultPegRadius;
            ballRadius = DefaultBallRadius;
            boardSlots = DefaultBoardSlots();
            tileHeight = DefaultTileHeight;
            spinFaces = DefaultSpinFaces();
            maxQueuedSpins = DefaultMaxQueuedSpins;
            maxLiveShotsPerTeam = DefaultMaxLiveShotsPerTeam;
            boostSeconds = DefaultBoostSeconds;
            bombShots = DefaultBombShots;
            bombRadius = DefaultBombRadius;
            lineThickness = DefaultLineThickness;
            selfDestructRadius = DefaultSelfDestructRadius;
            dropSpreadSamples = DefaultDropSpreadSamples;
            dropSpreadFraction = DefaultDropSpreadFraction;
            spinLaneFraction = DefaultSpinLaneFraction;
            layoutVersion = CurrentLayoutVersion;
        }

        public static TerritoryTeamDef[] DefaultTeams()
        {
            return new[]
            {
                new TerritoryTeamDef("GREEN",  Hex(0x3D, 0xDC, 0x4A)),
                new TerritoryTeamDef("YELLOW", Hex(0xFF, 0xD2, 0x3A)),
                new TerritoryTeamDef("BLUE",   Hex(0x2E, 0x6B, 0xFF)),
                new TerritoryTeamDef("RED",    Hex(0xFF, 0x3B, 0x3B))
            };
        }

        public static SlotEffect[] DefaultBoardSlots()
        {
            return new[]
            {
                SlotEffect.Bonus(), SlotEffect.Release(), SlotEffect.Multiply(2),
                SlotEffect.Release(), SlotEffect.Multiply(2)
            };
        }

        public static SpinFace[] DefaultSpinFaces()
        {
            return new[]
            {
                new SpinFace(SlotEffect.Multiply(2), 4),
                new SpinFace(SpinFaceKind.Boost, 3, 3),
                new SpinFace(SpinFaceKind.Boost, 1, 5),
                new SpinFace(SlotEffect.Add(25), 3),
                new SpinFace(SlotEffect.Add(100), 2),
                new SpinFace(SlotEffect.Release(), 2),
                new SpinFace(SpinFaceKind.Bomb, 3),
                new SpinFace(SpinFaceKind.PlusLine, 3),
                new SpinFace(SpinFaceKind.SelfDestruct, 2)
            };
        }

        /// <summary>terr-002 Country Wars, in quadrant order (0 TL, 1 TR, 2 BL, 3 BR). INDIA sits front-left, nearest the camera.</summary>
        public static TerritoryTeamDef[] CountryWarsTeams()
        {
            return new[]
            {
                new TerritoryTeamDef("SOUTH AFRICA", Hex(0x1F, 0xB8, 0x5A), "SA"),
                new TerritoryTeamDef("AUSTRALIA",    Hex(0xFF, 0xCC, 0x1A), "AUS"),
                new TerritoryTeamDef("INDIA",        Hex(0x2F, 0x7B, 0xFF), "IND"),
                new TerritoryTeamDef("WEST INDIES",  Hex(0xD3, 0x23, 0x3F), "WI")
            };
        }

        /// <summary>terr-002 reel: terr-001's nine faces with + LINE swapped for RAIN and SELF DESTRUCT for BOOM (cannon strike).</summary>
        public static SpinFace[] CountryWarsSpinFaces()
        {
            return new[]
            {
                new SpinFace(SlotEffect.Multiply(2), 4),
                new SpinFace(SpinFaceKind.Boost, 3, 3),
                new SpinFace(SpinFaceKind.Boost, 1, 5),
                new SpinFace(SlotEffect.Add(25), 3),
                new SpinFace(SlotEffect.Add(100), 2),
                new SpinFace(SlotEffect.Release(), 2),
                new SpinFace(SpinFaceKind.Bomb, 3),
                new SpinFace(SpinFaceKind.Rain, 3),
                new SpinFace(SpinFaceKind.CannonStrike, 2)
            };
        }

        static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        /// <summary>
        /// True when cell (x, y) is part of the team's base plate: the base cell itself, or (with
        /// <see cref="basePlateRadiusCells"/> &gt; 0) any cell whose square overlaps the plate disc centred on the base cell.
        /// </summary>
        public bool OnBasePlate(int team, int x, int y)
        {
            return OnBasePlate(team, x, y, basePlateRadiusCells);
        }

        /// <summary>As <see cref="OnBasePlate(int,int,int)"/> for a plate of <paramref name="radiusCells"/> (the showdown plate).</summary>
        public bool OnBasePlate(int team, int x, int y, float radiusCells)
        {
            Vector2Int bc = BaseCell(team);
            if (radiusCells <= 0f)
            {
                return x == bc.x && y == bc.y;
            }

            // Nearest point of the cell's square to the plate centre (the base cell's centre).
            float dx = Mathf.Max(0f, Mathf.Abs(x - bc.x) - 0.5f);
            float dy = Mathf.Max(0f, Mathf.Abs(y - bc.y) - 0.5f);
            return dx * dx + dy * dy < radiusCells * radiusCells;
        }

        /// <summary>The cell a team's cannon stands on: inset from the team's outer corner.</summary>
        public Vector2Int BaseCell(int team)
        {
            int left = baseInsetCells;
            int right = gridWidth - 1 - baseInsetCells;
            int bottom = baseInsetCells;
            int top = gridHeight - 1 - baseInsetCells;
            switch (team)
            {
                case 0: return new Vector2Int(left, top);
                case 1: return new Vector2Int(right, top);
                case 2: return new Vector2Int(left, bottom);
                default: return new Vector2Int(right, bottom);
            }
        }

        /// <summary>
        /// The 90 degree wedge a cannon sweeps, aimed inward from its outer corner and widened by the margin.
        /// Grid-space degrees: 0 is +x, 90 is +y.
        /// </summary>
        public Vector2 SweepRange(int team)
        {
            float min;
            switch (team)
            {
                case 0: min = 270f; break;   // top-left looks toward +x, -y
                case 1: min = 180f; break;   // top-right
                case 2: min = 0f; break;     // bottom-left
                default: min = 90f; break;   // bottom-right
            }

            return new Vector2(min - sweepMarginDegrees, min + 90f + sweepMarginDegrees);
        }

        /// <summary>Total weight of the spinner's faces.</summary>
        public int SpinWeightTotal()
        {
            int total = 0;
            if (spinFaces != null)
            {
                for (int i = 0; i < spinFaces.Length; i++)
                {
                    total += Mathf.Max(0, spinFaces[i].weight);
                }
            }

            return total;
        }

        public override bool Validate(out string error)
        {
            if (!base.Validate(out error))
            {
                return false;
            }

            if (teams == null || teams.Length != 4)
            {
                error = "teams must have exactly 4 entries (one per quadrant).";
                return false;
            }

            for (int i = 0; i < teams.Length; i++)
            {
                if (string.IsNullOrEmpty(teams[i].name))
                {
                    error = $"teams[{i}] has no name.";
                    return false;
                }
            }

            if (gridWidth < 8 || gridHeight < 8 || cellSize <= 0f || tileHeight <= 0f)
            {
                error = "The grid must be at least 8x8 with a positive cellSize.";
                return false;
            }

            if (baseInsetCells * 2 >= Mathf.Min(gridWidth, gridHeight))
            {
                error = "baseInsetCells is too large: a cannon would sit past the middle of its quadrant.";
                return false;
            }

            if (shotSpeedCellsPerSecond <= 0f || fireIntervalSeconds <= 0f || sweepDegreesPerSecond <= 0f)
            {
                error = "shotSpeedCellsPerSecond, fireIntervalSeconds and sweepDegreesPerSecond must be positive.";
                return false;
            }

            if (shotHealth < 1 || maxLiveShotsPerTeam < 1 || boostSeconds <= 0f)
            {
                error = "shotHealth and maxLiveShotsPerTeam must be at least 1, and boostSeconds positive.";
                return false;
            }

            if (boardSlots == null || boardSlots.Length < 1)
            {
                error = "boardSlots needs at least one slot.";
                return false;
            }

            bool hasRelease = false;
            bool hasBonus = false;
            for (int i = 0; i < boardSlots.Length; i++)
            {
                hasRelease |= boardSlots[i].kind == SlotEffectKind.Release;
                hasBonus |= boardSlots[i].kind == SlotEffectKind.Bonus;
            }

            int spinWeight = SpinWeightTotal();
            if (hasBonus && spinWeight <= 0)
            {
                error = "A SPIN can happen (Bonus slot) but spinFaces has no positive weight.";
                return false;
            }

            if (bombShots < 1 || bombRadius < 1 || lineThickness < 1 || selfDestructRadius < 1)
            {
                error = "bombShots, bombRadius, lineThickness and selfDestructRadius must be at least 1.";
                return false;
            }

            if (cannonDownSeconds <= 0f || rainDrops < 1 || rainSeconds <= 0f || rainLeadSeconds < 0f ||
                rainSplashRadius < 0 || rainCannonClearance < 1 || basePlateRadiusCells < 0f ||
                basePlateRadiusCells > baseInsetCells + 0.5f || showdownPlateRadiusCells > baseInsetCells + 0.5f ||
                cannonSpeedupPerElimination < 0f || heavyShotsAfterSeconds < 0f || heavyShotRadius < 1)
            {
                error = "cannonDownSeconds and rainSeconds must be positive, rainDrops and rainCannonClearance at least 1, rainLeadSeconds and rainSplashRadius at least 0, basePlateRadiusCells and showdownPlateRadiusCells between 0 and baseInsetCells + 0.5, cannonSpeedupPerElimination and heavyShotsAfterSeconds at least 0, heavyShotRadius at least 1.";
                return false;
            }

            if (spinFaces != null)
            {
                for (int i = 0; i < spinFaces.Length; i++)
                {
                    if (spinFaces[i].weight > 0 && spinFaces[i].kind == SpinFaceKind.Economy &&
                        spinFaces[i].effect.kind == SlotEffectKind.Bonus)
                    {
                        error = $"spinFaces[{i}] is a Bonus; a spin cannot trigger another spin.";
                        return false;
                    }

                    hasRelease |= spinFaces[i].weight > 0 && spinFaces[i].kind == SpinFaceKind.Economy &&
                                  spinFaces[i].effect.kind == SlotEffectKind.Release;
                }
            }

            if (!hasRelease)
            {
                error = "Nothing can release: no Release slot on the board and no Release face on the reel. Nobody could ever fire.";
                return false;
            }

            if (ballsPerBoard < 1 || dropIntervalSeconds <= 0f || ballStuckSeconds <= 0f)
            {
                error = "ballsPerBoard, dropIntervalSeconds and ballStuckSeconds must be positive.";
                return false;
            }

            if (bankStart < 1 || bankCap < bankStart)
            {
                error = "bankStart must be at least 1 and bankCap at least bankStart.";
                return false;
            }

            if (escalateDropFactor <= 0f || escalateDropFactor > 1f || minDropIntervalSeconds <= 0f || escalateEverySeconds <= 0f)
            {
                error = "escalateDropFactor must be in (0,1], and minDropIntervalSeconds and escalateEverySeconds positive.";
                return false;
            }

            if (spinSeconds <= 0f || maxQueuedSpins < 1)
            {
                error = "spinSeconds must be positive and maxQueuedSpins at least 1.";
                return false;
            }

            if (decisionAtSeconds + preStartHoldSeconds + endHoldSeconds >= maxDurationSeconds)
            {
                error = "decisionAtSeconds + holds must be less than maxDurationSeconds, or the run times out before it is decided.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
