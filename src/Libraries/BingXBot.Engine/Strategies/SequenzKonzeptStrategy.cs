using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// SK-System (Sequenz-Konzept) — strukturbasierte Trading-Strategie.
/// Folgt den "Spuren der Market Maker" durch A-B-C Sequenzen.
/// Multi-Timeframe: Übergeordnete Struktur (GKL) → Sequenz → Entry.
/// </summary>
public class SequenzKonzeptStrategy : IStrategy
{
    public string Name => "SK-System";
    public string Description => "Sequenz-Konzept: A-B-C Muster + Fibonacci-Zielzonen + GKL + Liquidität";

    // === Parameter ===
    private int _swingStrength = 5;           // Fractal-Stärke (3=Scalping, 5=DayTrading, 7=Swing)
    private int _htfSwingStrength = 7;        // Swing-Stärke für HTF (GKL-Berechnung)
    private decimal _minRangePercent = 0.5m;  // Min. A→B Range in % (filtert Noise)
    private bool _requireCloseBreak = true;   // True=Close über B, False=Wick reicht
    private bool _useGkl = true;              // GKL aus HTF verwenden
    private bool _useLiquidity = true;        // Liquiditätszonen-Validierung
    private bool _useIKI = true;              // Interne Korrektur-Sequenzen
    private bool _useBOS = true;              // Break of Structure als Confluence
    private bool _useChoCH = true;            // Change of Character als Warnung
    private int _minConfluence = 4;           // Min. Confluence-Score (0-10) für Entry
    private decimal _slBufferPercent = 0.2m;  // SL-Puffer unter Punkt A (in %)
    private TradingModePreset _activePreset = TradingModePreset.Swing;

    /// <summary>Aktueller Trading-Modus.</summary>
    public TradingModePreset ActivePreset => _activePreset;

    // === IStrategy ===

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("SwingStrength", "Fractal-Stärke für Swing-Erkennung", "int", _swingStrength, 2, 10, 1),
        new("HtfSwingStrength", "Swing-Stärke für übergeordnete Struktur (HTF)", "int", _htfSwingStrength, 3, 15, 1),
        new("MinRangePercent", "Min. A→B Range in % des Preises", "decimal", _minRangePercent, 0.1m, 3.0m, 0.1m),
        new("RequireCloseBreak", "Close über B für Aktivierung (1=ja, 0=Wick reicht)", "int", _requireCloseBreak ? 1 : 0, 0, 1, 1),
        new("UseGKL", "GKL aus HTF als Confluence (1=ja, 0=nein)", "int", _useGkl ? 1 : 0, 0, 1, 1),
        new("UseLiquidity", "Liquiditätszonen-Validierung (1=ja, 0=nein)", "int", _useLiquidity ? 1 : 0, 0, 1, 1),
        new("MinConfluence", "Min. Confluence-Score für Entry (0-10)", "int", _minConfluence, 0, 10, 1),
        new("SLBufferPercent", "SL-Puffer unter Punkt A in %", "decimal", _slBufferPercent, 0.0m, 1.0m, 0.1m),
    };

    /// <summary>Preset für einen Trading-Modus anwenden.</summary>
    public void ApplyPreset(TradingModePreset mode)
    {
        _activePreset = mode;
        if (mode == TradingModePreset.Custom) return;

        switch (mode)
        {
            case TradingModePreset.Scalping:
                _swingStrength = 3;
                _htfSwingStrength = 5;
                _minRangePercent = 0.3m;
                _requireCloseBreak = false;  // Wick reicht bei Scalping (schnellerer Entry)
                _useGkl = true;
                _useLiquidity = true;
                _useIKI = false;             // Zu schnell für IKI
                _useBOS = true;
                _useChoCH = true;
                _minConfluence = 3;          // Niedrigerer Threshold für schnelle Entries
                _slBufferPercent = 0.15m;
                break;

            case TradingModePreset.DayTrading:
                _swingStrength = 5;
                _htfSwingStrength = 7;
                _minRangePercent = 0.5m;
                _requireCloseBreak = true;
                _useGkl = true;
                _useLiquidity = true;
                _useIKI = true;
                _useBOS = true;
                _useChoCH = true;
                _minConfluence = 4;
                _slBufferPercent = 0.2m;
                break;

            default: // Swing
                _swingStrength = 7;
                _htfSwingStrength = 10;
                _minRangePercent = 1.0m;
                _requireCloseBreak = true;
                _useGkl = true;
                _useLiquidity = true;
                _useIKI = true;
                _useBOS = true;
                _useChoCH = true;
                _minConfluence = 5;          // Höherer Threshold für Swing
                _slBufferPercent = 0.3m;
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Hauptmethode: Evaluate
    // ═══════════════════════════════════════════════════════════════

    public SignalResult Evaluate(MarketContext context)
    {
        var candles = context.Candles;
        if (candles.Count < _swingStrength * 2 + 20)
            return NoSignal("Zu wenig Daten");

        var currentPrice = context.CurrentTicker.LastPrice;
        var currentClose = candles[^1].Close;

        // ═══════════════════════════════════════════════════════════
        // EBENE 1: Übergeordnete Struktur (HTF)
        // Bestimmt Trend-Richtung + GKL der großen Bewegung
        // ═══════════════════════════════════════════════════════════

        bool? htfTrendDirection = null; // null = unbekannt, true = up, false = down
        bool priceInHtfGkl = false;
        Sequence? htfSequence = null;

        if (_useGkl && context.HigherTimeframeCandles is { Count: > 30 })
        {
            // Vollständige HTF-Sequenz erkennen (nicht nur GKL-Level)
            htfSequence = SequenceDetector.DetectSequence(
                context.HigherTimeframeCandles, _htfSwingStrength, _minRangePercent * 2, true);

            if (htfSequence != null)
            {
                htfTrendDirection = htfSequence.IsLong;

                // Preis im GKL der HTF-Sequenz? (55.9-66.7%)
                priceInHtfGkl = htfSequence.IsInGklZone(currentPrice);
            }
            else
            {
                // Fallback: Einfaches GKL aus letztem Swing-Paar
                var gkl = SequenceDetector.CalculateGKL(context.HigherTimeframeCandles, _htfSwingStrength);
                if (gkl.HasValue)
                {
                    htfTrendDirection = gkl.Value.IsUptrend;
                    priceInHtfGkl = SequenceDetector.IsInGKL(currentPrice, gkl.Value.Gkl559, gkl.Value.Gkl667);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // EBENE 2: Sequenz-Erkennung (aktuelle TF)
        // ═══════════════════════════════════════════════════════════

        var sequence = SequenceDetector.DetectSequence(candles, _swingStrength, _minRangePercent, _requireCloseBreak);
        if (sequence == null)
            return NoSignal("Keine gültige Sequenz erkannt");

        // State aktualisieren
        sequence.State = SequenceDetector.UpdateState(sequence, currentPrice, currentClose, _requireCloseBreak);

        // HTF-Trend-Filter: Nur in Richtung des übergeordneten Trends traden
        // Long-Sequenz nur bei HTF-Uptrend, Short nur bei HTF-Downtrend
        if (htfTrendDirection.HasValue && sequence.IsLong != htfTrendDirection.Value)
            return NoSignal($"Sequenz {(sequence.IsLong ? "Long" : "Short")} gegen HTF-Trend ({(htfTrendDirection.Value ? "Up" : "Down")})");

        // ═══════════════════════════════════════════════════════════
        // CLOSE-SIGNALE: Bestehende Positionen bei Zielzone schließen
        // ═══════════════════════════════════════════════════════════

        // Aktive Sequenz hat Extension erreicht → Close-Signal
        if (sequence.State == SequenceState.TargetReached)
        {
            var closeSide = sequence.IsLong ? Signal.CloseLong : Signal.CloseShort;
            return new SignalResult(closeSide, 0.9m, null, null, null,
                $"SK-Zielzone erreicht (161.8% Extension) — Position schließen");
        }

        // Sequenz invalidiert + offene Position → Close-Signal (SL sollte greifen, aber Sicherheit)
        if (sequence.State == SequenceState.Invalidated)
        {
            // Prüfe ob eine Position in dieser Richtung offen ist
            var hasOpenPos = context.OpenPositions.Any(p =>
                p.Symbol == context.Symbol &&
                ((sequence.IsLong && p.Side == Side.Buy) || (!sequence.IsLong && p.Side == Side.Sell)));
            if (hasOpenPos)
            {
                var closeSide = sequence.IsLong ? Signal.CloseLong : Signal.CloseShort;
                return new SignalResult(closeSide, 1.0m, null, null, null,
                    "SK-Sequenz invalidiert (unter Punkt A) — Position schließen");
            }
            return NoSignal("Sequenz invalidiert (unter Punkt A)");
        }

        // ═══════════════════════════════════════════════════════════
        // RE-ENTRY: Nach abgeschlossener Sequenz im GKL der größeren Struktur
        // ═══════════════════════════════════════════════════════════

        // Wenn die aktuelle Sequenz Active ist (B durchbrochen, Ziel noch nicht erreicht):
        // Kein neuer Entry möglich, aber Re-Entry prüfen
        if (sequence.State == SequenceState.Active)
        {
            // Re-Entry: Gibt es eine NEUE Sequenz innerhalb der aktiven?
            // (Verschachtelte Sequenz im Retracement der aktiven Bewegung)
            if (_useIKI && sequence.PointC != null)
            {
                var innerSeqs = SequenceDetector.DetectIKI(candles, sequence, Math.Max(2, _swingStrength - 2));
                var reentrySeq = innerSeqs.FirstOrDefault(s =>
                    s.IsLong == sequence.IsLong &&
                    s.State is SequenceState.CorrectionZone or SequenceState.WaitingBreak &&
                    s.IsInBuyZone(currentPrice));

                if (reentrySeq != null)
                {
                    // IKI-basierter Re-Entry
                    var ikiSlBuf = reentrySeq.PointA.Price * _slBufferPercent / 100m;
                    var ikiSl = reentrySeq.IsLong
                        ? reentrySeq.PointA.Price - ikiSlBuf
                        : reentrySeq.PointA.Price + ikiSlBuf;

                    return new SignalResult(
                        reentrySeq.IsLong ? Signal.Long : Signal.Short,
                        0.6m, currentPrice, ikiSl, sequence.Extension1618,
                        $"SK Re-Entry via IKI | A={reentrySeq.PointA.Price:G8} B={reentrySeq.PointB.Price:G8}",
                        TakeProfit2: sequence.Extension2618, ConfluenceScore: 5);
                }
            }

            // Preis im GKL der HTF-Sequenz? → Re-Entry möglich in der größeren Struktur
            if (priceInHtfGkl && htfSequence != null)
            {
                // Suche neue Sequenz die mit HTF-GKL aligned
                var gklSeq = SequenceDetector.DetectSequence(candles, Math.Max(2, _swingStrength - 1), _minRangePercent * 0.5m, _requireCloseBreak);
                if (gklSeq != null && gklSeq.IsLong == htfSequence.IsLong &&
                    gklSeq.State is SequenceState.CorrectionZone or SequenceState.WaitingBreak &&
                    gklSeq.IsInBuyZone(currentPrice))
                {
                    var gklSlBuf = gklSeq.PointA.Price * _slBufferPercent / 100m;
                    var gklSl = gklSeq.IsLong
                        ? gklSeq.PointA.Price - gklSlBuf
                        : gklSeq.PointA.Price + gklSlBuf;

                    return new SignalResult(
                        gklSeq.IsLong ? Signal.Long : Signal.Short,
                        0.7m, currentPrice, gklSl, gklSeq.Extension1618,
                        $"SK GKL Re-Entry | HTF-GKL + neue Sequenz A={gklSeq.PointA.Price:G8}",
                        TakeProfit2: gklSeq.Extension2618, ConfluenceScore: 6);
                }
            }

            return NoSignal("Sequenz aktiv — kein Entry-Zeitpunkt (warte auf Re-Entry oder Zielzone)");
        }

        if (sequence.State == SequenceState.Forming)
            return NoSignal("Sequenz bildet sich noch (wartet auf B-Bestätigung)");

        // ═══════════════════════════════════════════════════════════
        // ENTRY-LOGIK: CorrectionZone oder WaitingBreak
        // ═══════════════════════════════════════════════════════════

        if (sequence.State is not (SequenceState.CorrectionZone or SequenceState.WaitingBreak))
            return NoSignal($"Sequenz-State {sequence.State} nicht geeignet für Entry");

        // Prüfe ob Preis in der idealen Kaufzone liegt
        var inBuyZone = sequence.IsInBuyZone(currentPrice);       // 50-61.8%
        var inGklZone = sequence.IsInGklZone(currentPrice);       // 55.9-66.7% (SK-spezifisch)

        if (!inBuyZone && !inGklZone)
        {
            // Erweiterte Zone: 38.2-78.6% — nur mit starker Confluence akzeptabel
            var retLevel = sequence.Range > 0
                ? Math.Abs(sequence.PointB.Price - currentPrice) / sequence.Range
                : 0m;
            if (retLevel < 0.382m || retLevel > 0.786m)
                return NoSignal("Preis außerhalb der Retracement-Zone (38.2-78.6%)");
        }

        // SK-System 3. Ebene: Entry-TF-Candles für präzises Timing nutzen (M5/M15)
        // Wenn EntryTimeframeCandles vorhanden → feinere Bestätigung auf kleinerem TF
        // Wenn nicht → Bestätigung auf aktuellem TF (Fallback)
        var entryCandles = context.EntryTimeframeCandles is { Count: > 20 }
            ? context.EntryTimeframeCandles
            : candles;

        // Auf Entry-TF: Eigene Mini-Sequenz suchen die mit der Hauptsequenz aligned
        if (context.EntryTimeframeCandles is { Count: > 30 })
        {
            var entrySwingStrength = Math.Max(2, _swingStrength - 2);
            var entrySeq = SequenceDetector.DetectSequence(
                context.EntryTimeframeCandles, entrySwingStrength, _minRangePercent * 0.5m, false);

            // Entry-TF Sequenz muss in gleiche Richtung zeigen wie Hauptsequenz
            if (entrySeq != null && entrySeq.IsLong == sequence.IsLong &&
                entrySeq.State is SequenceState.CorrectionZone or SequenceState.WaitingBreak or SequenceState.Active)
            {
                // 3. Ebene bestätigt die Hauptsequenz → stärkeres Signal
                // (wird unten als Confluence-Bonus gezählt)
            }
            else if (entrySeq != null && entrySeq.IsLong != sequence.IsLong)
            {
                // Entry-TF zeigt in ANDERE Richtung → Signal schwächer
                return NoSignal("Entry-TF Sequenz gegen Hauptsequenz — kein Entry");
            }
        }

        // SK-System: Entry-Bestätigung bei Punkt C (Stabilisierung/Candlestick-Muster)
        // Nutzt Entry-TF-Candles für feinere Bestätigung wenn verfügbar
        var entryConfirmation = SequenceDetector.DetectEntryConfirmation(entryCandles, sequence, currentPrice);
        if (entryConfirmation == CandleConfirmation.None)
            return NoSignal("Keine Entry-Bestätigung (Preis noch nicht in der Zone)");
        // Nur PriceTouches (schwächste Bestätigung) → höherer Confluence-Threshold
        var confirmationBonus = entryConfirmation switch
        {
            CandleConfirmation.StableInZone => 2,  // Stabilisierung = stärkstes Signal
            CandleConfirmation.Engulfing => 2,      // Engulfing = starkes Umkehr-Signal
            CandleConfirmation.HammerOrPin => 1,    // Hammer = gutes Signal
            CandleConfirmation.HighVolume => 1,     // Hohes Volumen = institutionell
            _ => 0                                   // PriceTouches = kein Bonus
        };

        // ═══════════════════════════════════════════════════════════
        // CONFLUENCE-SCORE (0-10)
        // ═══════════════════════════════════════════════════════════

        var swings = SequenceDetector.FindSwingPoints(candles, _swingStrength);
        int confluenceScore = 0;
        var reasons = new List<string>();

        // +2: Sequenz im GKL der übergeordneten HTF-Struktur (stärkstes Signal)
        if (_useGkl && priceInHtfGkl)
        {
            confluenceScore += 2;
            reasons.Add("HTF-GKL");
        }

        // +2: BOS in Sequenz-Richtung (Struktur-Bestätigung)
        if (_useBOS)
        {
            var bos = SequenceDetector.DetectBOS(candles, swings);
            if (bos != null && bos.IsBullish == sequence.IsLong)
            {
                confluenceScore += 2;
                reasons.Add("BOS");
            }
        }

        // +1: HTF-Trend stimmt mit Sequenz überein
        if (htfTrendDirection.HasValue && htfTrendDirection.Value == sequence.IsLong)
        {
            confluenceScore += 1;
            reasons.Add("HTF-Trend");
        }

        // +1: Preis im idealen 50-61.8% Bereich
        if (inBuyZone)
        {
            confluenceScore += 1;
            reasons.Add("50-61.8%");
        }

        // +1: Preis im GKL-Bereich (55.9-66.7%)
        if (inGklZone)
        {
            confluenceScore += 1;
            reasons.Add("GKL");
        }

        // +1: Liquiditätszone bestätigt Zielzone (Market Maker treiben Preis dorthin)
        if (_useLiquidity)
        {
            var liquidityZones = LiquidityAnalyzer.FindLiquidityZones(candles, swings);
            if (LiquidityAnalyzer.IsInLiquidityZone(sequence.Extension1618, liquidityZones))
            {
                confluenceScore += 1;
                reasons.Add("Liq-Target");
            }
            // Bonus: Entry-Zone ist auch eine Liquiditätszone (Akkumulation)
            if (LiquidityAnalyzer.IsInLiquidityZone(currentPrice, liquidityZones))
            {
                confluenceScore += 1;
                reasons.Add("Liq-Entry");
            }
        }

        // +1: Volume-Bestätigung (institutionelles Interesse bei Punkt C)
        var volSma = IndicatorHelper.CalculateVolumeSma(candles, 20);
        if (volSma.Count > 0 && volSma[^1].HasValue && candles[^1].Volume > volSma[^1]!.Value * 1.2m)
        {
            confluenceScore += 1;
            reasons.Add("Vol");
        }

        // +1: IKI bestätigt Richtung (interne Sequenz in der Korrektur)
        if (_useIKI && sequence.PointC != null)
        {
            var ikiSequences = SequenceDetector.DetectIKI(candles, sequence, Math.Max(2, _swingStrength - 2));
            if (ikiSequences.Any(iki => iki.IsLong == sequence.IsLong &&
                iki.State is SequenceState.Active or SequenceState.WaitingBreak))
            {
                confluenceScore += 1;
                reasons.Add("IKI");
            }
        }

        // +1: Kein ChoCH gegen die Sequenz (Trendwechsel-Warnung)
        if (_useChoCH)
        {
            var choch = SequenceDetector.DetectChoCH(swings);
            if (choch != null && choch.FromBullishToBearish == sequence.IsLong)
            {
                // ChoCH GEGEN die Sequenz → Abzug statt Bonus
                confluenceScore -= 2;
                reasons.Add("ChoCH-WARNUNG!");
            }
            else
            {
                confluenceScore += 1;
                reasons.Add("Kein ChoCH");
            }
        }

        // Entry-Bestätigung als Confluence-Bonus (SK: Stabilisierung bei C stärkt das Signal)
        confluenceScore += confirmationBonus;
        if (confirmationBonus > 0)
            reasons.Add(entryConfirmation.ToString());

        // Confluence zu niedrig → kein Entry
        // Bei schwacher Entry-Bestätigung (PriceTouches) brauchen wir MEHR Confluence
        var effectiveMinConfluence = entryConfirmation == CandleConfirmation.PriceTouches
            ? _minConfluence + 2  // +2 höherer Threshold bei schwacher Bestätigung
            : _minConfluence;
        if (confluenceScore < effectiveMinConfluence)
            return NoSignal($"Confluence {confluenceScore}/{effectiveMinConfluence} ({string.Join(", ", reasons)})");

        // ═══════════════════════════════════════════════════════════
        // SIGNAL ERSTELLEN
        // ═══════════════════════════════════════════════════════════

        var side = sequence.IsLong ? Signal.Long : Signal.Short;
        var confidence = Math.Min(1m, (decimal)confluenceScore / 10m);

        // SL: Strukturell unter/über Punkt A mit Puffer
        var slBuffer = sequence.PointA.Price * _slBufferPercent / 100m;
        var sl = sequence.IsLong
            ? sequence.PointA.Price - slBuffer
            : sequence.PointA.Price + slBuffer;

        // TP: Fibonacci-Extensions (strukturell, nicht ATR-basiert)
        var tp1 = sequence.Extension1618;
        var tp2 = sequence.Extension2618;

        // RRR berechnen
        var rrr = sequence.CalculateRRR(currentPrice);

        var reasonText = $"SK {(sequence.IsLong ? "Long" : "Short")} | " +
                        $"A={sequence.PointA.Price:G8} B={sequence.PointB.Price:G8}" +
                        (sequence.PointC != null ? $" C={sequence.PointC.Price:G8}" : "") +
                        $" | Score={confluenceScore}/10 RRR={rrr:F1}:1 | {string.Join(", ", reasons)}";

        // Limit-Order empfehlen bei WaitingBreak (Preis hat B noch nicht durchbrochen)
        var preferLimit = sequence.State == SequenceState.WaitingBreak;
        var entryPrice = inBuyZone ? currentPrice : (decimal?)null;

        return new SignalResult(side, confidence, entryPrice, sl, tp1, reasonText,
            TakeProfit2: tp2, ConfluenceScore: confluenceScore, PreferLimitOrder: preferLimit);
    }

    // ═══════════════════════════════════════════════════════════════
    // IStrategy Methoden
    // ═══════════════════════════════════════════════════════════════

    public void WarmUp(IReadOnlyList<Candle> history)
    {
        // SK-System ist stateless — Sequenz-Erkennung berechnet alles pro Evaluate()-Aufruf
        // WarmUp nicht nötig (Swing-Punkte werden nicht gecached)
    }

    public void Reset() { }

    public IStrategy Clone() => new SequenzKonzeptStrategy
    {
        _swingStrength = _swingStrength,
        _htfSwingStrength = _htfSwingStrength,
        _minRangePercent = _minRangePercent,
        _requireCloseBreak = _requireCloseBreak,
        _useGkl = _useGkl,
        _useLiquidity = _useLiquidity,
        _useIKI = _useIKI,
        _useBOS = _useBOS,
        _useChoCH = _useChoCH,
        _minConfluence = _minConfluence,
        _slBufferPercent = _slBufferPercent,
        _activePreset = _activePreset
    };

    private static SignalResult NoSignal(string reason) =>
        new(Signal.None, 0m, null, null, null, reason);
}
