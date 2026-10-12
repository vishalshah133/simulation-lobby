using System.Globalization;

namespace SimulationLobby.Presentation
{
    /// <summary>
    /// Number formatting for on-screen counters. The count is the hook in any "how many" format, so it
    /// has to stay legible at 480px wide: full digits while they fit, then a short suffix.
    /// </summary>
    public static class CountFormat
    {
        static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa" };

        /// <summary>
        /// <c>999</c> -> "999", <c>12,345</c> -> "12,345", <c>1,234,567</c> -> "1.23M". Plain digits up
        /// to <paramref name="fullDigits"/> long, a suffix beyond that.
        /// </summary>
        public static string Compact(long value, int fullDigits = 6)
        {
            if (value < 0)
            {
                return "-" + Compact(-value, fullDigits);
            }

            int digits = value == 0 ? 1 : (int)System.Math.Floor(System.Math.Log10(value)) + 1;
            if (digits <= fullDigits)
            {
                return value.ToString("N0", CultureInfo.InvariantCulture);
            }

            double scaled = value;
            int tier = 0;
            while (scaled >= 1000d && tier < Suffixes.Length - 1)
            {
                scaled /= 1000d;
                tier++;
            }

            // Three significant figures: 1.23M, 12.3M, 123M.
            string number = scaled >= 100d ? scaled.ToString("0", CultureInfo.InvariantCulture)
                : scaled >= 10d ? scaled.ToString("0.0", CultureInfo.InvariantCulture)
                : scaled.ToString("0.00", CultureInfo.InvariantCulture);
            return number + Suffixes[tier];
        }
    }
}
