using System.Globalization;

namespace FinanzRechner.Models;

/// <summary>
/// Währungs-Einstellungen. Bestimmt Formatierung aller Geldbeträge.
/// </summary>
public class CurrencySettings
{
    /// <summary>ISO 4217 Währungscode (z.B. "EUR", "USD", "CHF").</summary>
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>Währungssymbol (z.B. "€", "$", "CHF").</summary>
    public string CurrencySymbol { get; set; } = "€";

    /// <summary>Culture für Zahlenformatierung (z.B. "de-DE", "en-US").</summary>
    public string CultureName { get; set; } = "de-DE";

    /// <summary>Ob das Symbol nach dem Betrag steht (true: "1.234,56 €", false: "$1,234.56").</summary>
    public bool SymbolAfterAmount { get; set; } = true;

    /// <summary>CultureInfo-Instanz (gecacht).</summary>
    public CultureInfo GetCulture() => CultureInfo.GetCultureInfo(CultureName);

    /// <summary>Standard-Einstellung (EUR, de-DE).</summary>
    public static CurrencySettings Default => new();

    /// <summary>Vordefinierte Währungs-Presets.</summary>
    public static readonly IReadOnlyList<CurrencySettings> Presets =
    [
        new() { CurrencyCode = "EUR", CurrencySymbol = "€", CultureName = "de-DE", SymbolAfterAmount = true },
        new() { CurrencyCode = "USD", CurrencySymbol = "$", CultureName = "en-US", SymbolAfterAmount = false },
        new() { CurrencyCode = "GBP", CurrencySymbol = "£", CultureName = "en-GB", SymbolAfterAmount = false },
        new() { CurrencyCode = "CHF", CurrencySymbol = "CHF", CultureName = "de-CH", SymbolAfterAmount = true },
        new() { CurrencyCode = "JPY", CurrencySymbol = "¥", CultureName = "ja-JP", SymbolAfterAmount = false },
        new() { CurrencyCode = "CAD", CurrencySymbol = "CA$", CultureName = "en-CA", SymbolAfterAmount = false },
        new() { CurrencyCode = "AUD", CurrencySymbol = "A$", CultureName = "en-AU", SymbolAfterAmount = false },
        new() { CurrencyCode = "SEK", CurrencySymbol = "kr", CultureName = "sv-SE", SymbolAfterAmount = true },
        new() { CurrencyCode = "NOK", CurrencySymbol = "kr", CultureName = "nb-NO", SymbolAfterAmount = true },
        new() { CurrencyCode = "DKK", CurrencySymbol = "kr", CultureName = "da-DK", SymbolAfterAmount = true },
        new() { CurrencyCode = "PLN", CurrencySymbol = "zł", CultureName = "pl-PL", SymbolAfterAmount = true },
        new() { CurrencyCode = "CZK", CurrencySymbol = "Kč", CultureName = "cs-CZ", SymbolAfterAmount = true },
        new() { CurrencyCode = "BRL", CurrencySymbol = "R$", CultureName = "pt-BR", SymbolAfterAmount = false },
        new() { CurrencyCode = "MXN", CurrencySymbol = "MX$", CultureName = "es-MX", SymbolAfterAmount = false },
        new() { CurrencyCode = "INR", CurrencySymbol = "₹", CultureName = "hi-IN", SymbolAfterAmount = false },
        new() { CurrencyCode = "TRY", CurrencySymbol = "₺", CultureName = "tr-TR", SymbolAfterAmount = true },
    ];
}
