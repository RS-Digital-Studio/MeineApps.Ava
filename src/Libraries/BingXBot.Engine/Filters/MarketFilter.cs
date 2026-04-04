using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Filters;

/// <summary>
/// Globale Markt-Filter die VOR der Strategie-Evaluation geprüft werden.
/// Statische Methoden (wie ScanHelper) - kein State, kein DI.
///
/// Filter:
/// - BTC Health Score (-4 bis +4): Long/Short-Erlaubnis basierend auf BTC-Zustand
/// - Funding-Rate: Contrarian-Signal, blockiert überhebelte Richtung
/// - Session: Positionsgröße nach Tageszeit (US > EU > Asia)
/// - Cooldown: Wartezeit nach Verlust-Trades
/// - Wochenende: Keine neuen Entries Sa/So
/// - Volatilitäts-Bremse: Halbe Position bei extremer ATR
/// - Max Trades/Tag: Overtrading-Schutz
/// </summary>
public static class MarketFilter
{
    /// <summary>
    /// Berechnet den BTC Health Score (-4 bis +4).
    /// Bestimmt ob Long/Short/Beide erlaubt sind und die Positionsgröße.
    ///
    /// +1: BTC D1 Preis > EMA50
    /// +1: BTC H4 Supertrend bullish
    /// +1: BTC H4 RSI > 50
    /// +1: BTC Funding-Rate < 0.03%
    /// (Analog negativ für bearish)
    /// </summary>
    public static BtcHealthResult CalculateBtcHealth(
        IReadOnlyList<Candle>? btcDailyCandles,
        IReadOnlyList<Candle>? btcH4Candles,
        decimal btcFundingRate = 0m)
    {
        var score = 0;
        var reasons = new List<string>();

        // D1: Preis vs EMA50
        if (btcDailyCandles != null && btcDailyCandles.Count >= 55)
        {
            var ema50 = IndicatorHelper.CalculateEma(btcDailyCandles, 50);
            if (ema50[^1].HasValue)
            {
                var btcPrice = btcDailyCandles[^1].Close;
                if (btcPrice > ema50[^1]!.Value)
                { score++; reasons.Add("D1>EMA50"); }
                else
                { score--; reasons.Add("D1<EMA50"); }
            }
        }

        // H4: Supertrend
        if (btcH4Candles != null && btcH4Candles.Count >= 15)
        {
            var (_, stBullish) = IndicatorHelper.CalculateSupertrend(btcH4Candles, 10, 3.0m);
            if (stBullish[^1].HasValue)
            {
                if (stBullish[^1]!.Value)
                { score++; reasons.Add("H4-ST↑"); }
                else
                { score--; reasons.Add("H4-ST↓"); }
            }
        }

        // H4: RSI
        if (btcH4Candles != null && btcH4Candles.Count >= 20)
        {
            var rsi = IndicatorHelper.CalculateRsi(btcH4Candles, 14);
            if (rsi[^1].HasValue)
            {
                if (rsi[^1]!.Value > 55m)
                { score++; reasons.Add($"RSI={rsi[^1]!.Value:F0}↑"); }
                else if (rsi[^1]!.Value < 45m)
                { score--; reasons.Add($"RSI={rsi[^1]!.Value:F0}↓"); }
            }
        }

        // Funding-Rate
        if (btcFundingRate < 0.03m && btcFundingRate > -0.03m)
        { score++; reasons.Add("Fund=neutral"); }
        else if (btcFundingRate >= 0.06m)
        { score--; reasons.Add($"Fund={btcFundingRate:P2}↑↑"); }
        else if (btcFundingRate <= -0.05m)
        { score--; reasons.Add($"Fund={btcFundingRate:P2}↓↓"); }

        var allowLong = score >= -1; // Nur bei stark bearish (-2 oder weniger) Longs blockieren
        var allowShort = score <= 1; // Nur bei stark bullish (+2 oder mehr) Shorts blockieren
        var positionScale = score switch
        {
            >= 3 => 1.0m,
            >= 1 => 0.85m,
            0 => 0.75m,
            >= -2 => 0.85m,
            _ => 1.0m
        };

        return new BtcHealthResult(score, allowLong, allowShort, positionScale,
            string.Join(", ", reasons));
    }

    /// <summary>
    /// Prüft Funding-Rate als Contrarian-Signal.
    /// Gibt zurück ob ein Trade in die gewünschte Richtung erlaubt ist.
    /// </summary>
    public static FundingFilterResult CheckFunding(decimal fundingRate, Side desiredSide)
    {
        // Positiv = Longs zahlen → Markt überhebelt Long
        if (fundingRate >= 0.08m && desiredSide == Side.Buy)
            return new FundingFilterResult(false, true, $"Funding {fundingRate:P3} zu hoch für Long (Markt überhebelt)");

        // Negativ = Shorts zahlen → Markt überhebelt Short
        if (fundingRate <= -0.05m && desiredSide == Side.Sell)
            return new FundingFilterResult(false, true, $"Funding {fundingRate:P3} zu negativ für Short (Markt überhebelt)");

        // Funding in unsere Richtung = Bonus
        var bonus = false;
        if (fundingRate >= 0.05m && desiredSide == Side.Sell) bonus = true; // Shorts werden bezahlt
        if (fundingRate <= -0.03m && desiredSide == Side.Buy) bonus = true; // Longs werden bezahlt

        return new FundingFilterResult(true, bonus, null);
    }

    /// <summary>
    /// Gibt den Session-basierten Positionsgrößen-Faktor zurück.
    /// US-Session (13-21 UTC) = 1.0, EU (7-15) = 0.9, Asia (0-8) = 0.75, Wochenende = 0.
    /// </summary>
    public static SessionFilterResult CheckSession(DateTime utcNow)
    {
        var day = utcNow.DayOfWeek;
        var hour = utcNow.Hour;

        // Wochenende: Keine neuen Entries (Fr 22:00 - Mo 06:00)
        if (day == DayOfWeek.Saturday ||
            day == DayOfWeek.Sunday ||
            (day == DayOfWeek.Friday && hour >= 22) ||
            (day == DayOfWeek.Monday && hour < 6))
        {
            return new SessionFilterResult(false, 0m, "Wochenende - keine neuen Entries");
        }

        // 15min vor/nach Funding-Settlement (00:00, 08:00, 16:00 UTC): Meiden
        var minuteOfDay = hour * 60 + utcNow.Minute;
        var fundingTimes = new[] { 0, 480, 960 }; // 00:00, 08:00, 16:00
        foreach (var ft in fundingTimes)
        {
            var diff = Math.Abs(minuteOfDay - ft);
            if (diff < 15 || diff > 1425) // 1440-15 für Mitternachts-Wrap
                return new SessionFilterResult(false, 0m, "Funding-Settlement in Kürze - warte 15min");
        }

        // Session-Scaling
        var scale = hour switch
        {
            >= 13 and < 16 => 1.0m,  // EU/US Overlap: Peak
            >= 13 and < 21 => 1.0m,  // US-Session: Volle Größe
            >= 7 and < 15  => 0.9m,  // EU-Session: 90%
            _              => 0.75m  // Asia/Off-Hours: 75%
        };

        var sessionName = hour switch
        {
            >= 13 and < 16 => "EU/US-Overlap (Peak)",
            >= 13 and < 21 => "US-Session",
            >= 7 and < 15  => "EU-Session",
            >= 0 and < 8   => "Asia-Session",
            _              => "Off-Hours"
        };

        return new SessionFilterResult(true, scale, sessionName);
    }

    /// <summary>
    /// Prüft ob der Cooldown nach einem Verlust-Trade eingehalten ist.
    /// Default: 8h (2 H4-Candles) nach dem letzten Verlust-Trade.
    /// </summary>
    public static bool IsCooldownActive(DateTime? lastLossTime, int cooldownHours = 8)
    {
        if (lastLossTime == null) return false;
        return (DateTime.UtcNow - lastLossTime.Value).TotalHours < cooldownHours;
    }

    /// <summary>
    /// Prüft ob die maximale Anzahl Trades pro Tag erreicht ist.
    /// Default: 3 Trades/Tag für CryptoTrendPro.
    /// </summary>
    public static bool IsMaxDailyTradesReached(int tradesToday, int maxTradesPerDay = 3)
    {
        return tradesToday >= maxTradesPerDay;
    }

    /// <summary>
    /// Prüft ob die ATR-Volatilität extrem ist (>90. Perzentil).
    /// Bei extremer Volatilität: Halbe Positionsgröße.
    /// </summary>
    public static decimal GetVolatilityScale(int atrPercentile)
    {
        if (atrPercentile >= 95) return 0.5m;
        if (atrPercentile >= 90) return 0.65m;
        if (atrPercentile >= 80) return 0.8m;
        return 1.0m;
    }
}

// ═══════════════════════════════════════════════════════════
// Result-Typen
// ═══════════════════════════════════════════════════════════

/// <summary>BTC-Health-Score Ergebnis.</summary>
public record BtcHealthResult(
    int Score,
    bool AllowLong,
    bool AllowShort,
    decimal PositionScale,
    string Reasons);

/// <summary>Funding-Rate Filter Ergebnis.</summary>
public record FundingFilterResult(
    bool IsAllowed,
    bool IsBonus,
    string? RejectionReason);

/// <summary>Session-Filter Ergebnis.</summary>
public record SessionFilterResult(
    bool IsAllowed,
    decimal PositionScale,
    string SessionInfo);
