namespace BingXBot.Core.Models;

/// <summary>
/// Handelsinformationen pro Symbol (Precision, Minimum-Werte).
/// Wird beim Start von der BingX API geladen und gecacht.
/// </summary>
public record SymbolInfo(
    string Symbol,
    /// <summary>Dezimalstellen für Quantity (z.B. 3 für BTC = 0.001).</summary>
    int QuantityPrecision,
    /// <summary>Dezimalstellen für Preis (z.B. 1 für BTC = 0.1).</summary>
    int PricePrecision,
    /// <summary>Minimale Order-Quantity (z.B. 0.001 BTC).</summary>
    decimal MinQuantity,
    /// <summary>Minimaler Notional-Wert in USDT (z.B. 5 USDT).</summary>
    decimal MinNotional);
