using BingXBot.Core.Configuration;
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

        // User-Vorgabe 13.04.2026: Wochentags IMMER offen. Wochenende blocken — mit Ausnahme:
        // Forex öffnet Sonntag 22:00 UTC (Sydney-Open), nicht erst Montag 00:00 UTC.
        // Grund: BingX schaltet Kontrakte auch ausserhalb Original-Boersenzeiten liquide
        // (z.B. SPX500-Perp hat 24/5-Liquiditaet, unabhaengig von NYSE-Oeffnung).
        // Der frueher feingranulare Zeit-Check filterte Commodity/Index/Stock abends in EU
        // raus und lieferte fast nur Forex im Scanner-Ergebnis.
        var day = utcNow.DayOfWeek;
        var category = SymbolClassifier.Classify(symbol);

        if (day == DayOfWeek.Saturday)
            return false;

        if (day == DayOfWeek.Sunday)
        {
            // Forex öffnet Sonntag 22:00 UTC mit Sydney-Open (BingX folgt Standard-FX-Cycle).
            // Andere TradFi-Kategorien bleiben sonntags geschlossen (US-Indices/Stocks/Commodities ab Montag).
            if (category == MarketCategory.Forex && utcNow.Hour >= 22)
                return true;
            return false;
        }

        // Forex schliesst Freitag 22:00 UTC (BingX-Dokumentation, tatsaechlicher Cutover).
        if (category == MarketCategory.Forex)
        {
            var timeOfDay = utcNow.Hour * 60 + utcNow.Minute;
            if (day == DayOfWeek.Friday && timeOfDay > 1320) return false;
        }

        return true;
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

    /// <summary>
    /// Phase 18 / A7 — True wenn die aktuelle UTC-Zeit innerhalb der vom User erlaubten Crypto-Sessions
    /// liegt. Auf TradFi nicht angewendet (TradFi hat ohnehin <see cref="IsMarketOpen"/>-Pruefung).
    /// </summary>
    public static bool IsSessionAllowed(DateTime utcNow, TradingSessions allowed)
    {
        if (allowed == TradingSessions.All) return true;
        if (allowed == TradingSessions.None) return false;
        var current = ClassifySession(utcNow);
        return (allowed & current) != 0;
    }

    /// <summary>
    /// Phase 18 / A7 — Klassifiziert eine UTC-Stunde in eine Trading-Session.
    /// Asia: 00:00-08:00 UTC. EU: 08:00-13:00 UTC. EU/US-Overlap: 13:00-16:00 UTC. US: 16:00-22:00 UTC.
    /// 22:00-00:00 UTC = Asia-Vorlauf. Pro UTC-Stunde liegt der Tick in EXAKT einer Session.
    /// </summary>
    public static TradingSessions ClassifySession(DateTime utcNow)
    {
        var hour = utcNow.Hour;
        if (hour >= 8 && hour < 13) return TradingSessions.Eu;
        if (hour >= 13 && hour < 16) return TradingSessions.EuUsOverlap;
        if (hour >= 16 && hour < 22) return TradingSessions.Us;
        return TradingSessions.Asia;
    }
}

