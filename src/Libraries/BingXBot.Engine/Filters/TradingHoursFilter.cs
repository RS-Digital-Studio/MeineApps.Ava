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
            // Forex: Mo 00:00 - Fr 22:00 UTC (24/5, schließt Fr 22:00 UTC / 17:00 ET)
            MarketCategory.Forex => !(day == DayOfWeek.Friday && timeOfDay > 1320),

            // Commodities: Fast 24/5, 1h Pause 22:00-23:00 UTC (CME Globex Maintenance)
            // 23:00 UTC (18:00 ET) bis nächsten Tag 22:00 UTC (17:00 ET)
            MarketCategory.Commodity => !(timeOfDay >= 1320 && timeOfDay < 1380),

            // Stocks: Mo-Fr 08:00-24:00 UTC (BingX Extended: Pre-Market 04:00 ET = 08:00 UTC,
            // After-Hours bis 20:00 ET = 00:00 UTC/Mitternacht)
            MarketCategory.Stock => timeOfDay >= 480,

            // Indices: Fast 24/5, 1h Pause 22:00-23:00 UTC (CME E-mini Maintenance)
            MarketCategory.Index => !(timeOfDay >= 1320 && timeOfDay < 1380),

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
