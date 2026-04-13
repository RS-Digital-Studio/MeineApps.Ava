using BingXBot.Core.Enums;

namespace BingXBot.Engine.Risk;

/// <summary>
/// SK-Buch Stop-Loss-Berechnung mit FIXEN Pip-Werten pro Asset-Klasse.
/// Quelle: Tradebook SK-System (Sascha Wenzel, Stefan Kassing) S.13.
/// </summary>
/// <remarks>
/// Buch-Tabelle:
/// - Hauptwährungen (FX) + Metalle (Gold/Silber): -20 Pips
/// - Indices (DAX, Dow, etc.) + Öl: -40 Pips
/// - Krypto: -100 Pips
/// - GBP-Paare: höherer Pip-Wert (+50%)
/// - Exoten (AUD/CAD/NZD-Paare): höherer Pip-Wert (+50%)
///
/// Pip-Definition je Instrument:
/// - FX Standard: 1/10000 (4. Nachkommastelle)
/// - FX JPY: 1/100 (2. Nachkommastelle)
/// - Metalle (Gold, Silber): 1/10 (Hochzahl)
/// - Öl: 1/100
/// - Indices: 1 Punkt
/// - Krypto: 1/10000 des Preises (skaliert mit Preis)
/// </remarks>
public static class PipStopLossCalculator
{
    /// <summary>
    /// Berechnet die SL-Distanz in Preis-Einheiten nach SK-Buch-Regel.
    /// </summary>
    public static decimal CalculateSlDistance(string symbol, MarketCategory category, decimal entryPrice, bool isSingleTrade = false)
    {
        var pipCount = GetPipCount(symbol, category, isSingleTrade);
        var pipValue = GetPipValue(symbol, category, entryPrice);
        return pipCount * pipValue;
    }

    /// <summary>
    /// Berechnet den absoluten SL-Preis nach SK-Buch-Regel (nur Pip-Cap, ohne 78.6er und Point0).
    /// Wird als Fallback verwendet wenn keine Sequenz verfügbar ist.
    /// </summary>
    public static decimal CalculateStopLoss(string symbol, MarketCategory category, decimal entryPrice, bool isLong, bool isSingleTrade = false)
    {
        var slDistance = CalculateSlDistance(symbol, category, entryPrice, isSingleTrade);
        return isLong ? entryPrice - slDistance : entryPrice + slDistance;
    }

    /// <summary>
    /// Berechnet den SL strikt nach Buch (Cheat 36, S.13, Workflow 6.9):
    /// 1. Basis = 78.6% Retracement der 0→A-Range
    /// 2. GECAPPT bei Markt-Pips (Hauptwährungen/Metalle 20, Indices/Öl 40, Krypto 100)
    /// 3. NIEMALS über Punkt 0 hinaus
    /// </summary>
    /// <param name="fib786">78.6% Retracement-Level der 0→A-Range (SL-Basis).</param>
    /// <param name="point0">Punkt 0 der Sequenz (absolute Grenze).</param>
    public static decimal CalculateBookStopLoss(
        string symbol, MarketCategory category,
        decimal entryPrice, bool isLong,
        decimal fib786, decimal point0,
        bool isSingleTrade = false)
    {
        // 1. Basis: 78.6% Retracement
        var sl = fib786;

        // 2. Pip-Cap: SL darf vom Entry nicht weiter weg sein als Markt-Pips
        // SK-Buch: 1 Trade (Cheat 37) = 10-15 Pips, Multiple Trade (Cheat 49) = 20 Pips
        var pipDistance = CalculateSlDistance(symbol, category, entryPrice, isSingleTrade);
        var pipCapSl = isLong ? entryPrice - pipDistance : entryPrice + pipDistance;
        sl = isLong ? Math.Max(sl, pipCapSl) : Math.Min(sl, pipCapSl);

        // 3. Point0-Grenze: nie jenseits Punkt 0 (Workflow 6.9)
        sl = isLong ? Math.Max(sl, point0) : Math.Min(sl, point0);

        // 4. Sanity: SL auf richtiger Seite (Fallback auf reinen Pip-Cap)
        if (isLong && sl >= entryPrice) sl = entryPrice - pipDistance;
        if (!isLong && sl <= entryPrice) sl = entryPrice + pipDistance;

        return sl;
    }

    /// <summary>
    /// 20-Pips-Buffer über dem 200er Extensionslevel (Buch Workflow 4.5).
    /// Skaliert mit Pip-Einheit des Instruments (nicht mit GBP/Exoten-Upscale).
    /// </summary>
    public static decimal Get20PipsBuffer(string symbol, MarketCategory category, decimal price)
        => 20m * GetPipValue(symbol, category, price);

    /// <summary>
    /// Anzahl Pips nach SK-Buch-Tabelle.
    /// SK-Buch Cheat Node 37: 1 Trade Strategie = Standard Stop 10-15 Pips
    /// SK-Buch Cheat Node 49: Multiple Trade Strategie = 20 Pips Stop
    /// GBP/Exoten-Paare: höherer Pip-Wert (+50%) (Cheat Node 46/48)
    /// </summary>
    private static decimal GetPipCount(string symbol, MarketCategory category, bool isSingleTrade = false)
    {
        return category switch
        {
            MarketCategory.Forex when IsGbpPair(symbol) => isSingleTrade ? 22m : 30m,
            MarketCategory.Forex when IsExoticPair(symbol) => isSingleTrade ? 22m : 30m,
            MarketCategory.Forex => isSingleTrade ? 15m : 20m,                // Cheat 37: 15, Cheat 49: 20
            MarketCategory.Commodity when IsMetal(symbol) => isSingleTrade ? 15m : 20m,
            MarketCategory.Commodity => 40m,                          // Öl -40 Pips
            MarketCategory.Index => 40m,                              // Indices -40 Pips
            MarketCategory.Stock => 40m,                              // Aktien wie Indices
            MarketCategory.Crypto => 100m,                            // Krypto -100 Pips
            _ => 20m
        };
    }

    /// <summary>
    /// Pip-Wert in Preis-Einheiten je nach Instrument.
    /// </summary>
    private static decimal GetPipValue(string symbol, MarketCategory category, decimal entryPrice)
    {
        return category switch
        {
            MarketCategory.Forex when symbol.Contains("JPY", StringComparison.OrdinalIgnoreCase) => 0.01m,
            MarketCategory.Forex => 0.0001m,
            MarketCategory.Commodity when IsMetal(symbol) => 0.1m,    // Gold/Silber
            MarketCategory.Commodity => 0.01m,                         // Öl
            MarketCategory.Index => 1m,                                 // Indices: 1 Punkt
            MarketCategory.Stock => 0.01m,
            MarketCategory.Crypto => entryPrice * 0.0001m,             // 1/10000 des Preises
            _ => 0.0001m
        };
    }

    private static bool IsMetal(string symbol) =>
        symbol.Contains("GOLD", StringComparison.OrdinalIgnoreCase)
        || symbol.Contains("XAU", StringComparison.OrdinalIgnoreCase)
        || symbol.Contains("XAG", StringComparison.OrdinalIgnoreCase)
        || symbol.Contains("SILV", StringComparison.OrdinalIgnoreCase);

    private static bool IsGbpPair(string symbol) =>
        symbol.Contains("GBP", StringComparison.OrdinalIgnoreCase);

    private static bool IsExoticPair(string symbol) =>
        symbol.Contains("AUD", StringComparison.OrdinalIgnoreCase)
        || symbol.Contains("CAD", StringComparison.OrdinalIgnoreCase)
        || symbol.Contains("NZD", StringComparison.OrdinalIgnoreCase);
}
