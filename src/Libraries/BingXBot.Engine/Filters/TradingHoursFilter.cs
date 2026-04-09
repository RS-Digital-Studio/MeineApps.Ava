using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;

namespace BingXBot.Engine.Filters;

/// <summary>
/// Prüft ob ein Markt gerade geöffnet ist. Krypto = 24/7, TradFi = marktabhängig.
/// Verhindert Orders auf geschlossene Märkte (BingX lehnt diese ab oder queued sie).
/// </summary>
public static class TradingHoursFilter
{
    /// <summary>
    /// True wenn der Markt für dieses Symbol gerade geöffnet ist.
    /// Krypto-Symbole geben immer true zurück (24/7-Handel).
    /// TradFi-Symbole werden anhand der Marktöffnungszeiten geprüft.
    /// </summary>
    public static bool IsMarketOpen(string symbol, DateTime utcNow)
    {
        if (!SymbolClassifier.IsTradFi(symbol)) return true;
        if (SymbolClassifier.Is24x7(symbol)) return true;

        var category = SymbolClassifier.Classify(symbol);
        var day = utcNow.DayOfWeek;
        var timeOfDay = utcNow.Hour * 60 + utcNow.Minute; // Minuten seit Mitternacht UTC

        // Wochenende: Alle TradFi-Märkte geschlossen
        if (day is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;

        return category switch
        {
            // Forex: Mo 00:00 - Fr 22:00 UTC (24/5)
            MarketCategory.Forex => !(day == DayOfWeek.Friday && timeOfDay > 1320),

            // Commodities: Mo 01:00 - Fr 21:00 UTC (Haupt-Sessions, Produkt-Pausen ignoriert)
            MarketCategory.Commodity => timeOfDay >= 60 && timeOfDay <= 1260,

            // Stocks: Mo-Fr 10:00-21:00 UTC (Pre-Market 10:00, Regular 14:30, Close 21:00)
            MarketCategory.Stock => timeOfDay >= 600 && timeOfDay <= 1260,

            // Indices: Mo 01:00 - Fr 21:00 UTC (extended hours, fast wie Commodities)
            MarketCategory.Index => timeOfDay >= 60 && timeOfDay <= 1260,

            _ => true
        };
    }

    /// <summary>Gibt die nächste Marktöffnungszeit zurück (für Logging).</summary>
    public static string GetMarketStatusText(string symbol, DateTime utcNow)
    {
        if (!SymbolClassifier.IsTradFi(symbol)) return "24/7";
        if (IsMarketOpen(symbol, utcNow)) return "Geöffnet";

        var category = SymbolClassifier.Classify(symbol);
        var categoryName = SymbolClassifier.GetCategoryDisplayName(category);
        return $"{categoryName}-Markt geschlossen";
    }
}
