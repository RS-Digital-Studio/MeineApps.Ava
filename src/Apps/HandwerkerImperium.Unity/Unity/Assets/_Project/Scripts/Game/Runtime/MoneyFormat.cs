using System.Globalization;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Idle-Kurzformat für Geld-/Zahlenanzeigen (deutsche Schreibweise): unter 10.000 voll mit
    /// Tausenderpunkt ("9.500"), darüber gestuft "12,5k" / "3,2M" / "1,5B" / "2,0T" (eine
    /// Nachkommastelle, Komma). Pure, NUnit-testbare Präsentations-Logik (kein Unity-API).
    /// </summary>
    public static class MoneyFormat
    {
        private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

        public static string Short(decimal value)
        {
            bool negative = value < 0m;
            if (negative) value = -value;

            string text;
            if (value < 10_000m)
                text = decimal.Floor(value).ToString("N0", De);
            else if (value < 1_000_000m)
                text = Stepped(value, 1_000m, "k");
            else if (value < 1_000_000_000m)
                text = Stepped(value, 1_000_000m, "M");
            else if (value < 1_000_000_000_000m)
                text = Stepped(value, 1_000_000_000m, "B");
            else
                text = Stepped(value, 1_000_000_000_000m, "T");

            return negative ? "-" + text : text;
        }

        private static string Stepped(decimal value, decimal unit, string suffix)
        {
            // Eine Nachkommastelle, ABGESCHNITTEN (nicht gerundet) — 999.999 darf nie "1000,0k" werden.
            decimal scaled = decimal.Floor(value / unit * 10m) / 10m;
            return scaled.ToString("0.0", De) + suffix;
        }
    }
}
