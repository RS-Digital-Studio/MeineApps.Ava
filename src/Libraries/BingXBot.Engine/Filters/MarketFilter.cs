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
/// - Session: Liquiditäts-Gewichtung (US > EU > Asia), kein Wochenend-Block (Krypto 24/7)
/// - Cooldown: Wartezeit nach Verlust-Trades (4h Basis, eskalierbar)
/// - Funding-Settlement: 5min Pause um Funding-Spikes zu meiden
/// - Volatilit��ts-Bremse: Halbe Position bei extremer ATR
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
    /// +1: BTC H4 RSI > 55 (Rauschfilter: 45-55 = neutral)
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

        // Funding-Rate (symmetrische Schwellen: neutral ±0.03%, Malus ab ±0.05%)
        // BingX API liefert Dezimalwerte: 0.0001 = 0.01%, daher Schwellen als Dezimal
        if (btcFundingRate < 0.0003m && btcFundingRate > -0.0003m)
        { score++; reasons.Add("Fund=neutral"); }
        else if (btcFundingRate >= 0.0005m)
        { score--; reasons.Add($"Fund={btcFundingRate:P3}↑↑"); }
        else if (btcFundingRate <= -0.0005m)
        { score--; reasons.Add($"Fund={btcFundingRate:P3}↓↓"); }

        // SK-VERIFY: KILLER #6 — BTC-Health als Confluence-Malus statt harter Block
        // Nur bei extremem BTC-Crash (Score -4) hart blocken. Score -2/-3 wird über PositionScale abgedeckt.
        // Alt: score >= -1 (blockierte bei -2, zu aggressiv für Altcoins mit eigener Struktur)
        var allowLong = score >= -3;
        var allowShort = score <= 3;
        var positionScale = score switch
        {
            >= 3 => 1.0m,     // Stark bullish: volle Positionsgröße
            >= 1 => 0.85m,    // Leicht bullish: minimal reduziert
            0 => 0.75m,       // Neutral: vorsichtiger
            >= -2 => 0.85m,   // Leicht bearish: minimal reduziert
            _ => 0.65m        // Stark bearish: deutlich reduziert
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
        // BingX API liefert Funding-Rate als Dezimalwert: 0.0001 = 0.01%
        // Asymmetrische Schwellen (bewusst): Short-Squeezes sind heftiger als Long-Liquidations,
        // daher Shorts konservativer gefiltert (+0.08% Long-Block vs. -0.05% Short-Block).
        // Positiv = Longs zahlen → Markt überhebelt Long
        if (fundingRate >= 0.0008m && desiredSide == Side.Buy)
            return new FundingFilterResult(false, true, $"Funding {fundingRate:P3} zu hoch für Long (Markt überhebelt)");

        // Negativ = Shorts zahlen → Markt überhebelt Short
        if (fundingRate <= -0.0005m && desiredSide == Side.Sell)
            return new FundingFilterResult(false, true, $"Funding {fundingRate:P3} zu negativ für Short (Markt überhebelt)");

        // Funding in unsere Richtung = Bonus
        var bonus = false;
        if (fundingRate >= 0.0005m && desiredSide == Side.Sell) bonus = true; // Shorts werden bezahlt
        if (fundingRate <= -0.0003m && desiredSide == Side.Buy) bonus = true; // Longs werden bezahlt

        return new FundingFilterResult(true, bonus, null);
    }

    /// <summary>
    /// Gibt den Session-basierten Positionsgrößen-Faktor zurück.
    /// Krypto-Märkte handeln 24/7 - KEIN Wochenend-Block.
    /// Funding-Settlement (00:00, 08:00, 16:00 UTC): 5min Pause für ALLE Perpetuals
    /// (Krypto UND TradFi auf BingX haben Funding-Settlements).
    /// </summary>
    public static SessionFilterResult CheckSession(DateTime utcNow)
    {
        var hour = utcNow.Hour;

        // 5min vor/nach Funding-Settlement (00:00, 08:00, 16:00 UTC): Kurze Pause
        // Gilt für ALLE BingX-Perpetuals (Krypto + TradFi haben Funding auf BingX).
        if (IsFundingSettlement(utcNow))
            return new SessionFilterResult(false, 0m, "Funding-Settlement - warte 5min");

        var sessionName = hour switch
        {
            >= 13 and < 16 => "EU/US-Overlap (Peak)",
            >= 16 and < 21 => "US-Session",
            >= 7 and < 13  => "EU-Session",
            >= 0 and < 7   => "Asia-Session",
            _              => "Off-Hours"
        };

        return new SessionFilterResult(true, 1.0m, sessionName);
    }

    /// <summary>
    /// Prüft ob gerade Funding-Settlement ist (±5min um 00:00, 08:00, 16:00 UTC).
    /// Gilt für ALLE BingX-Perpetuals (Krypto + TradFi haben Funding auf BingX).
    /// </summary>
    public static bool IsFundingSettlement(DateTime utcNow)
    {
        var minuteOfDay = utcNow.Hour * 60 + utcNow.Minute;
        var fundingTimes = new[] { 0, 480, 960 }; // 00:00, 08:00, 16:00
        foreach (var ft in fundingTimes)
        {
            var rawDiff = Math.Abs(minuteOfDay - ft);
            var diff = Math.Min(rawDiff, 1440 - rawDiff);
            if (diff < 5) return true;
        }
        return false;
    }

    // 04.05.2026: IsMaxDailyTradesReached + GetVolatilityScale entfernt.
    // MaxTradesPerDay wurde im Buch-Only Strip Phase 2 (21.04.2026) aus den RiskSettings entfernt
    // (kein Buch-Konzept). GetVolatilityScale wurde nirgends mehr aufgerufen — Position-Sizing
    // läuft über RiskManager.Check (Risk%-basiert) ohne Volatility-Scaling.
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
