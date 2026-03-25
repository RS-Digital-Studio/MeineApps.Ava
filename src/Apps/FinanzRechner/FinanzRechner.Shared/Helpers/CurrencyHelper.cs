using System.Globalization;
using FinanzRechner.Models;

namespace FinanzRechner.Helpers;

/// <summary>
/// Zentraler Helper für Währungsformatierung.
/// Konfigurierbar über CurrencySettings (Standard: EUR/de-DE).
/// Thread-safe durch volatile Snapshot-Pattern (atomarer Config-Swap).
/// </summary>
public static class CurrencyHelper
{
    /// <summary>Immutable Konfigurations-Snapshot für Thread-Safety.</summary>
    private sealed record CurrencyConfig(
        string Symbol, CultureInfo Culture, bool SymbolAfter, string Code)
    {
        public static readonly CurrencyConfig Default = new("€", CultureInfo.GetCultureInfo("de-DE"), true, "EUR");
    }

    private static volatile CurrencyConfig _config = CurrencyConfig.Default;

    /// <summary>Aktueller Währungscode (z.B. "EUR", "USD").</summary>
    public static string CurrencyCode => _config.Code;

    /// <summary>Aktuelles Währungssymbol.</summary>
    public static string CurrencySymbol => _config.Symbol;

    /// <summary>
    /// Konfiguriert die Währungsformatierung.
    /// Thread-safe: Atomarer Swap eines immutable Config-Objekts.
    /// </summary>
    public static void Configure(CurrencySettings settings)
    {
        CultureInfo culture;
        try { culture = CultureInfo.GetCultureInfo(settings.CultureName); }
        catch { culture = CultureInfo.GetCultureInfo("de-DE"); }

        _config = new CurrencyConfig(settings.CurrencySymbol, culture, settings.SymbolAfterAmount, settings.CurrencyCode);
    }

    /// <summary>Betrag formatieren: "1.234,56 €" oder "$1,234.56"</summary>
    public static string Format(double amount)
    {
        var c = _config; // Lokaler Snapshot für konsistente Formatierung
        var number = amount.ToString("N2", c.Culture);
        return c.SymbolAfter ? $"{number} {c.Symbol}" : $"{c.Symbol}{number}";
    }

    /// <summary>Betrag mit Vorzeichen: "+1.234,56 €" oder "-1.234,56 €"</summary>
    public static string FormatSigned(double amount)
    {
        var c = _config;
        var number = amount.ToString("N2", c.Culture);
        var prefix = amount >= 0 ? "+" : "";
        return c.SymbolAfter ? $"{prefix}{number} {c.Symbol}" : $"{prefix}{c.Symbol}{number}";
    }

    /// <summary>Kompakt mit Vorzeichen ohne Leerzeichen: "+1.234,56€" (für FloatingText)</summary>
    public static string FormatCompactSigned(double amount)
    {
        var c = _config;
        var number = amount.ToString("N2", c.Culture);
        var prefix = amount >= 0 ? "+" : "";
        return $"{prefix}{number}{c.Symbol}";
    }

    /// <summary>Gerundeter Betrag für Chart-Achsen: "1.235 €" (N0)</summary>
    public static string FormatAxis(double amount)
    {
        var c = _config;
        var number = amount.ToString("N0", c.Culture);
        return c.SymbolAfter ? $"{number} {c.Symbol}" : $"{c.Symbol}{number}";
    }

    /// <summary>Betrag für CSV-Export (InvariantCulture, Punkt als Dezimaltrenner)</summary>
    public static string FormatInvariant(double amount) =>
        amount.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>Nur das Suffix für CountUpBehavior: " €" oder "" (bei Symbol davor).</summary>
    public static string GetSuffix()
    {
        var c = _config;
        return c.SymbolAfter ? $" {c.Symbol}" : "";
    }

    /// <summary>Nur das Präfix für CountUpBehavior: "$" oder "" (bei Symbol danach).</summary>
    public static string GetPrefix()
    {
        var c = _config;
        return c.SymbolAfter ? "" : c.Symbol;
    }
}
