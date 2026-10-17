using UnityEngine;

namespace SimulationLobby.Simulations.Territory
{
    /// <summary>How the floor around the table is built.</summary>
    public enum TerritoryFloorPattern
    {
        /// <summary>terr-001: raised square slabs, three shades, seeded height variance.</summary>
        RaisedSlabs = 0,

        /// <summary>terr-002: raised hexagonal slabs (flat-top hex prisms), three shades, seeded height variance.</summary>
        HexTiles = 1
    }

    /// <summary>
    /// The look of one territory video: colours, floor, light, HUD text. Pure data, no rules. Read by the scene
    /// builder (materials, floor, HUD) and by the presenter (runtime tints, captions). A config with no theme uses
    /// <see cref="CreateClassic"/>, whose field defaults ARE the terr-001 look, so terr-001 rebuilds unchanged.
    /// </summary>
    [CreateAssetMenu(fileName = "TerritoryTheme", menuName = "Simulation Lobby/Configs/Territory Theme", order = 41)]
    public sealed class TerritoryTheme : ScriptableObject
    {
        [Header("Assets")]
        [Tooltip("Prefix of every generated material (Assets/Settings/Studio/<prefix>Charcoal.mat ...). A theme MUST have its own " +
                 "prefix, or rebuilding it repaints another video's scene. \"Territory\" is terr-001's.")]
        public string materialPrefix = "Territory";

        [Tooltip("Volume profile asset name under Assets/Settings/Configs/Territory.")]
        public string postProfileName = "TerritoryPost";

        [Header("Table and pieces")]
        public Color table = Hex(0x22, 0x23, 0x2B);
        public Color muzzle = Hex(0x10, 0x11, 0x16);
        [Range(0f, 1f)] public float tableSmoothness = 0.4f;
        public Color tileNeutral = new Color(0.07f, 0.07f, 0.09f, 1f);
        [Range(0f, 1f)] public float tileSmoothness = 0.45f;

        [Header("Slot chips")]
        public Color chipMultiply = Hex(0xFF, 0x4F, 0xA0);
        public Color chipRelease = Hex(0xF4, 0xF4, 0xF4);
        public Color chipSpin = Hex(0xFF, 0xC9, 0x3A);
        public Color chipNeutral = Hex(0x8A, 0x8D, 0x9A);
        public Color chipTextLight = Color.white;
        public Color chipTextDark = Hex(0x22, 0x23, 0x2B);
        public Color decalOutline = Hex(0x10, 0x10, 0x16);

        [Header("Drum")]
        public Color drumBody = Hex(0xFF, 0x4F, 0xA0);
        public Color drumArrow = Hex(0xFF, 0xD2, 0x3A);
        public Color drumIdleAccent = new Color(0.133f, 0.137f, 0.169f, 1f);
        public Color drumOutline = new Color(0.078f, 0.082f, 0.102f, 1f);

        [Header("Floor and light")]
        public TerritoryFloorPattern floorPattern = TerritoryFloorPattern.RaisedSlabs;
        public Color floorBase = Hex(0x3B, 0x35, 0x66);
        public Color floorAlt = Hex(0x46, 0x40, 0x7A);
        public Color floorLight = Hex(0x54, 0x4A, 0x90);
        public Color floorGrout = Hex(0x2E, 0x29, 0x55);
        public Color ambient = new Color(0.70f, 0.68f, 0.80f, 1f);
        public Color keyColor = new Color(1f, 0.97f, 0.92f, 1f);
        [Min(0f)] public float keyIntensity = 0.95f;
        [Range(0f, 1f)] public float keyShadowStrength = 0.6f;

        [Tooltip("Optional rim/fill directional from behind-right, no shadows. 0 = none (terr-001).")]
        [Min(0f)] public float rimIntensity = 0f;
        public Color rimColor = new Color(0.55f, 0.65f, 1f, 1f);

        [Tooltip("AmbientDriftField dust over the table (the 3D depth cue).")]
        public bool ambientDust = false;

        [Header("Post")]
        [Range(0f, 1f)] public float bloomIntensity = 0.2f;
        [Range(0f, 1f)] public float vignetteIntensity = 0.18f;
        [Range(-100f, 100f)] public float saturation = 10f;

        [Header("HUD")]
        public Color captionColor = Color.white;
        public Color sublineColor = new Color(0.78f, 0.82f, 0.9f, 1f);
        public Color footerColor = new Color(0.8f, 0.84f, 0.92f, 0.85f);
        public Color shareBarFrame = Hex(0x22, 0x23, 0x2B);

        [Tooltip("Share bar segments read \"IND 34%\" (team shortName) instead of \"34%\".")]
        public bool shareBarShowNames = false;

        public Color eliminatedCounter = new Color(0.55f, 0.55f, 0.6f, 1f);

        [Header("HUD text")]
        public string question = "WHICH COLOUR TAKES THE BOARD?";
        public string rules = "×2 doubles it  ·  R fires it all  ·  SPIN spins the drum  ·  lose your cannon, you're out";
        public string prompt = "Pick your colour before it starts.";

        [Header("Power-face colours")]
        public Color bombColor = new Color(0.102f, 0.102f, 0.125f, 1f);
        public Color selfDestructColor = new Color(0.541f, 0.063f, 0.125f, 1f);

        [Tooltip("Tint of a downed (BOOM) cannon's barrel and cap: scorched.")]
        public Color cannonDownTint = Hex(0x2A, 0x26, 0x22);

        [Tooltip("Hot core of the cannon explosion (BlastFx flash and debris).")]
        public Color cannonBlastColor = Hex(0xFF, 0x8A, 0x1F);

        [Header("Identity (terr-002)")]
        [Tooltip("Paint the team name on each board, in its counter zone, above the bank number.")]
        public bool boardNameDecals = false;

        [Tooltip("A waving flag on a pole at each quadrant's outer grid corner, next to the cannon.")]
        public bool showFlags = false;

        [Tooltip("Quadrant order (0 TL, 1 TR, 2 BL, 3 BR). Generated by the scene builder; empty entries get no flag.")]
        public Texture2D[] flags = new Texture2D[4];

        /// <summary>An in-memory theme with the terr-001 look. Never saved.</summary>
        public static TerritoryTheme CreateClassic()
        {
            var theme = CreateInstance<TerritoryTheme>();
            theme.name = "TerritoryTheme_Classic";
            theme.hideFlags = HideFlags.DontSave;
            return theme;
        }

        /// <summary>Writes the terr-002 Country Wars look ("night stadium": navy hex floor, violet ×2, gold) into this theme.</summary>
        public void ApplyCountryWars()
        {
            materialPrefix = "TerritoryCW";
            postProfileName = "TerritoryPost_CountryWars";

            table = Hex(0x1C, 0x22, 0x36);
            muzzle = Hex(0x0D, 0x10, 0x20);
            tableSmoothness = 0.45f;
            tileNeutral = new Color(0.06f, 0.07f, 0.11f, 1f);
            tileSmoothness = 0.45f;

            chipMultiply = Hex(0x7A, 0x4D, 0xFF);
            chipRelease = Hex(0xF4, 0xF4, 0xF4);
            chipSpin = Hex(0xFF, 0xC9, 0x3A);
            chipNeutral = Hex(0x8A, 0x8D, 0x9A);
            chipTextLight = Color.white;
            chipTextDark = Hex(0x1C, 0x22, 0x36);
            decalOutline = Hex(0x0B, 0x0E, 0x1A);

            drumBody = Hex(0x7A, 0x4D, 0xFF);
            drumArrow = Hex(0xFF, 0xC9, 0x3A);
            drumIdleAccent = Hex(0x1C, 0x22, 0x36);
            drumOutline = Hex(0x0B, 0x0E, 0x1A);

            floorPattern = TerritoryFloorPattern.HexTiles;
            floorBase = Hex(0x18, 0x21, 0x3F);
            floorAlt = Hex(0x1E, 0x2A, 0x50);
            floorLight = Hex(0x26, 0x34, 0x6A);
            floorGrout = Hex(0x0E, 0x14, 0x30);
            ambient = new Color(0.62f, 0.66f, 0.80f, 1f);
            keyColor = new Color(1f, 0.96f, 0.88f, 1f);
            keyIntensity = 1f;
            keyShadowStrength = 0.6f;
            rimIntensity = 0.35f;
            rimColor = new Color(0.55f, 0.65f, 1f, 1f);
            ambientDust = true;

            bloomIntensity = 0.22f;
            vignetteIntensity = 0.22f;
            saturation = 8f;

            captionColor = Color.white;
            sublineColor = Hex(0xFF, 0xD9, 0x78);
            footerColor = new Color(0.85f, 0.88f, 0.96f, 0.9f);
            shareBarFrame = Hex(0x1C, 0x22, 0x36);
            shareBarShowNames = true;
            eliminatedCounter = new Color(0.55f, 0.57f, 0.66f, 1f);

            question = "WHICH COUNTRY TAKES THE BOARD?";
            rules = "×2 doubles it  ·  R fires it all  ·  SPIN spins the drum  ·  lose your cannon, you're out";
            prompt = "Pick your country before it starts.";

            bombColor = new Color(0.08f, 0.09f, 0.13f, 1f);
            selfDestructColor = new Color(0.541f, 0.063f, 0.125f, 1f);
            cannonDownTint = Hex(0x2A, 0x26, 0x22);
            cannonBlastColor = Hex(0xFF, 0x8A, 0x1F);

            boardNameDecals = true;
            showFlags = true;
            if (flags == null || flags.Length != 4)
            {
                flags = new Texture2D[4];
            }
        }

        static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
