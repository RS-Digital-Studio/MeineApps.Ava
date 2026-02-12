using System.Globalization;

namespace FinanzRechner.Helpers;

/// <summary>
/// Zentraler Helper für Währungsformatierung (EUR).
/// Alle Betragsanzeigen sollen diese Methoden verwenden.
/// </summary>
public static class CurrencyHelper
{
    private const string CurrencySymbol = "€";

    /// <summary>Betrag formatieren: "1.234,56 €"</summary>
    public static string Format(double amount) => $"{amount:N2} {CurrencySymbol}";

    /// <summary>Betrag mit Vorzeichen: "+1.234,56 €" oder "-1.234,56 €"</summary>
    public static string FormatSigned(double amount) =>
        amount >= 0 ? $"+{amount:N2} {CurrencySymbol}" : $"{amount:N2} {CurrencySymbol}";

    /// <summary>Betrag für CSV-Export (InvariantCulture, Punkt als Dezimaltrenner)</summary>
    public static string FormatInvariant(double amount) =>
        amount.ToString("F2", CultureInfo.InvariantCulture);
}
