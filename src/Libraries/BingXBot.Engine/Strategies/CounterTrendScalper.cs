using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Task 4.10 — Counter-Trend-Scalper nach SK-Buch Masterclass.
///
/// Buch-Zitat: "Gegen den Trend (Antizyklisch): Erreicht der Markt das Ziellevel C, prallt er
/// dort fast immer zumindest kurzfristig ab. Viele Trader nutzen den Zielbereich für kleine,
/// schnelle Trades gegen den Haupttrend."
///
/// Warnung: "Counter-Trend-Trading ist hochriskant. Eine Bewegung kann sich auch in eine
/// Überschießung (Extension bis 261.8% oder mehr) verwandeln. Solche Trades sollten niemals
/// blind platziert werden, sondern erfordern Bestätigung auf kleineren Zeiteinheiten."
///
/// Trigger (alle Bedingungen MÜSSEN erfüllt sein):
/// 1. Hauptsequenz ist Aktiviert und Preis hat Extension1618 oder Extension200 erreicht (±1%)
/// 2. LTF-Gegensequenz ist Aktiviert mit Point0 in der TP-Zone der Hauptsequenz
/// 3. Positionsgröße wird auf 50% der normalen SK-Position reduziert
/// 4. TP liegt auf halbem Weg zum nächsten Haupt-Fibonacci-Level
/// 5. SL strikt unter dem LTF-Punkt 0 (TF-spezifischer Pip-Buffer)
///
/// Default-Aktivierung: Aus. Nur auf explizite User-Anweisung aktivieren.
/// </summary>
public sealed record CounterTrendHit(
    decimal EntryPrice,
    decimal StopLoss,
    decimal TakeProfit,
    decimal LtfPoint0,
    string Reason,
    decimal PositionScaleOverride);

public static class CounterTrendScalper
{
    /// <summary>
    /// Prüft ob ein Counter-Trend-Trade gegen die Hauptsequenz platziert werden kann.
    /// Liefert Null wenn einer der Buch-Bedingungen nicht erfüllt ist.
    /// </summary>
    /// <param name="mainSeq">Aktivierte Haupt-Sequenz (Long = Short-Scalp möglich, Short = Long-Scalp).</param>
    /// <param name="currentPrice">Aktueller Preis.</param>
    /// <param name="filterCandles">LTF-Kerzen für Gegensequenz-Detection.</param>
    /// <param name="ltfPointBuffer">Pip-Buffer unter LTF-Point0 für SL.</param>
    public static CounterTrendHit? TryDetect(
        Sequence mainSeq,
        decimal currentPrice,
        IReadOnlyList<Candle>? filterCandles,
        decimal ltfPointBuffer)
    {
        if (mainSeq.State != SequenceState.Active) return null;
        if (filterCandles == null || filterCandles.Count < 25) return null;

        // 1. Preis muss in TP-Zone (±1% von Extension1618 oder Extension200) sein
        var tpZoneHit = IsNearLevel(currentPrice, mainSeq.Extension1618, 0.01m)
                     || IsNearLevel(currentPrice, mainSeq.Extension200, 0.01m);
        if (!tpZoneHit) return null;

        // 2. LTF-Gegensequenz: entgegengesetzte Richtung zur Hauptsequenz
        // Strukturpunkte §3: BOS-Anker aktivieren — Counter-Trend-Scalp nur bei bestätigtem LTF-Strukturbruch.
        // Hardcoded `3` analog zu LtfReversalDetector (Counter-Trend ist Micro-Setup, keine Settings-Kopplung).
        var (_, ltfLong, ltfShort) = SequenceStateMachine.FromCandlesBoth(
            filterCandles, minImpulsePercent: 0.3m, correctionThreshold: 0.2m, minPoint0Candles: 2,
            bosAnchorSwingStrength: 3);
        var counter = mainSeq.IsLong ? ltfShort : ltfLong;
        if (counter.State < SmState.Aktiviert) return null;

        // 3. LTF-Point0 muss in TP-Zone der Hauptsequenz liegen (= "frische Gegen-Sequenz im Zielbereich")
        var inTpZone = IsNearLevel(counter.Point0, mainSeq.Extension1618, 0.01m)
                    || IsNearLevel(counter.Point0, mainSeq.Extension200, 0.01m);
        if (!inTpZone) return null;

        // 4. TP = halber Weg zum nächsten Haupt-Fib (eleventh level statt 161.8% → 200% → 261.8%)
        var nextMainLevel = mainSeq.IsLong ? mainSeq.Extension1618 : mainSeq.Extension1618;
        var counterExt = counter.Extension1618;
        var tp = (nextMainLevel + counterExt) / 2m;

        // 5. SL = LTF-Punkt 0 + Pip-Buffer (auf Gegen-Richtung projiziert)
        var counterIsLong = counter.IsLong;
        var sl = counterIsLong ? counter.Point0 - ltfPointBuffer : counter.Point0 + ltfPointBuffer;

        var reason = $"Counter-Trend (gegen {(mainSeq.IsLong ? "Long" : "Short")}-Haupt, LTF-Gegensequenz @ {counter.Point0:F6}, halbe Position)";

        return new CounterTrendHit(
            EntryPrice: currentPrice,
            StopLoss: sl,
            TakeProfit: tp,
            LtfPoint0: counter.Point0,
            Reason: reason,
            PositionScaleOverride: 0.5m);
    }

    private static bool IsNearLevel(decimal price, decimal level, decimal percent)
        => level > 0 && Math.Abs(price - level) / level <= percent;
}
