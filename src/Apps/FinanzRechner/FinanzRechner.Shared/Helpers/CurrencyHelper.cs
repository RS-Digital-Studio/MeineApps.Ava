using System.Globalization;

namespace FinanzRechner.Helpers;

/// <summary>
/// Zentraler Helper für Währungsformatierung (EUR).
/// Alle Betragsanzeigen sollen diese Methoden verwenden.
/// Verwendet feste de-DE CultureInfo für konsistente EUR-Formatierung
/// unabhängig vom Betriebssystem-Locale (1.234,56 €).
/// </summary>
public static class CurrencyHelper
{
    private const string CurrencySymbol = "€";
    private static readonly CultureInfo EurCulture = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Betrag formatieren: "1.234,56 €"</summary>
    public static string Format(double amount) => $"{amount.ToString("N2", EurCulture)} {CurrencySymbol}";

    /// <summary>Betrag mit Vorzeichen: "+1.234,56 €" oder "-1.234,56 €"</summary>
    public static string FormatSigned(double amount) =>
        amount >= 0
            ? $"+{amount.ToString("N2", EurCulture)} {CurrencySymbol}"
            : $"{amount.ToString("N2", EurCulture)} {CurrencySymbol}";

    /// <summary>Kompakt mit Vorzeichen ohne Leerzeichen: "+1.234,56€" (für FloatingText)</summary>
    public static string FormatCompactSigned(double amount) =>
        amount >= 0
            ? $"+{amount.ToString("N2", EurCulture)}{CurrencySymbol}"
            : $"{amount.ToString("N2", EurCulture)}{CurrencySymbol}";

    /// <summary>Gerundeter Betrag für Chart-Achsen: "1.235 €" (N0)</summary>
    public static string FormatAxis(double amount) => $"{amount.ToString("N0", EurCulture)} {CurrencySymbol}";

    /// <summary>Betrag für CSV-Export (InvariantCulture, Punkt als Dezimaltrenner)</summary>
    public static string FormatInvariant(double amount) =>
        amount.ToString("F2", CultureInfo.InvariantCulture);
}
