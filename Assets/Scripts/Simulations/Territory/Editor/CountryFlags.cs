using SimulationLobby.Presentation.EditorTools;
using UnityEngine;

namespace SimulationLobby.Simulations.Territory.EditorTools
{
    /// <summary>
    /// Paints the country flags terr-002 needs with <see cref="FlagPainter"/> and saves them as PNGs under
    /// <c>Assets/Settings/Flags</c>. Regenerated on every build (code is the source of truth). INDIA, SOUTH AFRICA and
    /// AUSTRALIA follow their real constructions as closely as flat primitives allow; WEST INDIES is a deliberately
    /// generic design (maroon field, gold sun, green island) that is NOT the Cricket West Indies crest. A team name
    /// this class does not know gets no flag.
    /// </summary>
    static class CountryFlags
    {
        public const string Folder = "Assets/Settings/Flags";
        const int PixelWidth = 600;

        static readonly Color White = Color.white;

        /// <summary>One flag per team, in team order; null where the name is unknown.</summary>
        public static Texture2D[] BuildAll(TerritoryTeamDef[] teams)
        {
            var flags = new Texture2D[teams.Length];
            for (int i = 0; i < teams.Length; i++)
            {
                flags[i] = Build(teams[i].name);
            }

            return flags;
        }

        /// <summary>Paints and saves the flag for <paramref name="teamName"/> (case-insensitive); null when there is none.</summary>
        public static Texture2D Build(string teamName)
        {
            string key = string.IsNullOrEmpty(teamName) ? string.Empty : teamName.Trim().ToUpperInvariant();
            FlagPainter painter;
            switch (key)
            {
                case "INDIA":
                    painter = India();
                    break;
                case "SOUTH AFRICA":
                    painter = SouthAfrica();
                    break;
                case "AUSTRALIA":
                    painter = Australia();
                    break;
                case "WEST INDIES":
                    painter = WestIndies();
                    break;
                default:
                    return null;
            }

            return painter.SavePng($"{Folder}/Flag_{key.Replace(' ', '_')}.png");
        }

        // ------------------------------------------------------------------ India (3:2)

        static FlagPainter India()
        {
            const float a = 1.5f;
            var painter = new FlagPainter(PixelWidth, PixelWidth * 2 / 3, White);
            painter.FillRect(N(0f, 2f / 3f, a), N(a, 1f, a), Hex(0xFF, 0x99, 0x33));   // saffron
            painter.FillRect(N(0f, 0f, a), N(a, 1f / 3f, a), Hex(0x13, 0x88, 0x08));   // green

            // Ashoka Chakra: diameter 3/4 of the white band (1/3 of the height), rim, 24 spokes, hub.
            Color navy = Hex(0x00, 0x00, 0x80);
            Vector2 centre = N(a * 0.5f, 0.5f, a);
            float outer = 0.75f * (1f / 3f) * 0.5f;
            float inner = outer * 0.86f;
            painter.FillRing(centre, inner, outer, navy);
            painter.FillSpokes(centre, inner, 24, 0.0125f, navy);
            painter.FillDisc(centre, outer * 0.17f, navy);
            return painter;
        }

        // ------------------------------------------------------------------ South Africa (3:2)

        static FlagPainter SouthAfrica()
        {
            const float a = 1.5f;
            Color red = Hex(0xE0, 0x3C, 0x31);
            Color blue = Hex(0x00, 0x14, 0x89);
            Color green = Hex(0x00, 0x77, 0x49);
            Color gold = Hex(0xFF, 0xB8, 0x1C);
            Color black = Color.black;

            var painter = new FlagPainter(PixelWidth, PixelWidth * 2 / 3, blue);
            painter.FillRect(N(0f, 0.5f, a), N(a, 1f, a), red);

            // The Y: white fimbriation (1/3 of the height in all), the green band (1/5) inside it.
            painter.FillPolygon(YPolygon(a, 1f / 6f), White);
            painter.FillPolygon(YPolygon(a, 0.1f), green);

            // The hoist triangle between the green arms, gold-bordered black. Green's inner edges meet the hoist at
            // y = 0.5 +- (0.5 - 0.1 sqrt 2) and each other at x = 0.5 - 0.1 sqrt 2.
            float inset = 0.1f * Mathf.Sqrt(2f);
            float yTop = 1f - inset;
            float yBottom = inset;
            float tip = 0.5f - inset;
            painter.FillPolygon(new[] { N(0f, yTop, a), N(0f, yBottom, a), N(tip, 0.5f, a) }, gold);

            // Black inset by 0.045 (perpendicular) from the two slanted edges.
            const float border = 0.045f;
            float shift = border * Mathf.Sqrt(2f);
            painter.FillPolygon(new[]
            {
                N(0f, yTop - shift, a), N(0f, yBottom + shift, a), N(tip - shift, 0.5f, a)
            }, black);
            return painter;
        }

        /// <summary>
        /// The horizontal Y of the South African flag as one polygon: two 45 degree arms from the hoist corners meeting at
        /// (0.5, 0.5), then a bar to the fly. <paramref name="half"/> is the half-thickness measured across each stroke.
        /// </summary>
        static Vector2[] YPolygon(float a, float half)
        {
            float d = half * Mathf.Sqrt(2f);
            float junction = half * (Mathf.Sqrt(2f) - 1f);
            const float overshoot = 0.1f;
            return new[]
            {
                N(-overshoot, 1f + d + overshoot, a),
                N(0.5f + junction, 0.5f + half, a),
                N(a + overshoot, 0.5f + half, a),
                N(a + overshoot, 0.5f - half, a),
                N(0.5f + junction, 0.5f - half, a),
                N(-overshoot, -d - overshoot, a),
                N(-overshoot, d - overshoot, a),
                N(0.5f - d, 0.5f, a),
                N(-overshoot, 1f - d + overshoot, a)
            };
        }

        // ------------------------------------------------------------------ Australia (2:1)

        static FlagPainter Australia()
        {
            const float a = 2f;
            Color blue = Hex(0x01, 0x21, 0x69);
            Color red = Hex(0xC8, 0x10, 0x2E);

            var painter = new FlagPainter(PixelWidth, PixelWidth / 2, blue);

            // Union Jack canton: the top-left quarter (x 0..1, y 0.5..1), so 1 wide and 0.5 tall (height h = 0.5).
            // White saltire h/5, red saltire h/15, white cross h/3, red cross h/5. The bands spill past the canton at
            // its edges; the blue repaint below trims them.
            painter.FillBand(N(0f, 1f, a), N(1f, 0.5f, a), 0.1f, White);
            painter.FillBand(N(0f, 0.5f, a), N(1f, 1f, a), 0.1f, White);
            painter.FillBand(N(0f, 1f, a), N(1f, 0.5f, a), 0.5f / 15f, red);
            painter.FillBand(N(0f, 0.5f, a), N(1f, 1f, a), 0.5f / 15f, red);
            painter.FillBand(N(0f, 0.75f, a), N(1f, 0.75f, a), 0.5f / 3f, White);
            painter.FillBand(N(0.5f, 0.5f, a), N(0.5f, 1f, a), 0.5f / 3f, White);
            painter.FillBand(N(0f, 0.75f, a), N(1f, 0.75f, a), 0.1f, red);
            painter.FillBand(N(0.5f, 0.5f, a), N(0.5f, 1f, a), 0.1f, red);
            painter.FillRect(N(1f, 0f, a), N(a, 1f, a), blue);
            painter.FillRect(N(0f, 0f, a), N(1f, 0.5f, a), blue);

            // Commonwealth Star (7 points) under the canton, centred on it.
            painter.FillStar(N(0.5f, 0.25f, a), 0.19f, 0.19f * 0.4f, 7, 0f, White);

            // Southern Cross in the fly half: four 7-point stars and one small 5-point.
            const float large = 0.07f;
            painter.FillStar(N(1.50f, 0.84f, a), large, large * 0.4f, 7, 0f, White);
            painter.FillStar(N(1.50f, 0.16f, a), large, large * 0.4f, 7, 0f, White);
            painter.FillStar(N(1.24f, 0.58f, a), large, large * 0.4f, 7, 0f, White);
            painter.FillStar(N(1.74f, 0.64f, a), large, large * 0.4f, 7, 0f, White);
            painter.FillStar(N(1.62f, 0.42f, a), 0.04f, 0.04f * 0.4f, 5, 0f, White);
            return painter;
        }

        // ------------------------------------------------------------------ West Indies (3:2, generic)

        static FlagPainter WestIndies()
        {
            const float a = 1.5f;
            Color maroon = Hex(0x7B, 0x10, 0x30);
            Color sun = Hex(0xF2, 0xB7, 0x05);
            Color island = Hex(0x1E, 0x8A, 0x4C);

            var painter = new FlagPainter(PixelWidth, PixelWidth * 2 / 3, maroon);
            Vector2 centre = N(a * 0.5f, 0.54f, a);
            painter.FillSpokes(centre, 0.37f, 16, 0.045f, sun);
            painter.FillDisc(centre, 0.25f, sun);

            // A low island hump across the bottom of the sun, flat below.
            const int steps = 16;
            var points = new Vector2[steps + 3];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float x = Mathf.Lerp(0.18f, a - 0.18f, t);
                float u = (t - 0.5f) * 2f;
                float y = 0.30f + 0.11f * (1f - u * u);
                points[i] = N(x, y, a);
            }

            points[steps + 1] = N(a - 0.18f, 0.18f, a);
            points[steps + 2] = N(0.18f, 0.18f, a);
            painter.FillPolygon(points, island);
            return painter;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Height-unit coordinates (x runs 0..aspect, y 0..1, y up) to the painter's normalised canvas.</summary>
        static Vector2 N(float x, float y, float aspect)
        {
            return new Vector2(x / aspect, y);
        }

        static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
