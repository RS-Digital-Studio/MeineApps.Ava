using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// SK-System "Holy Trinity" — 3-Ebenen Multi-Timeframe Strategie.
/// 4H (Navigator) → 1H (Filter) → 15m (Trigger).
/// Strikte Top-Down Ampel-Logik: Alle 3 TFs müssen aligned sein.
/// SL unter 15m-Punkt-0 (winziges Risiko), TP bei 4H-Extension (riesiges CRV).
/// </summary>
public class SequenzKonzeptStrategy : IStrategy
{
    public string Name => "SK-System";
    public string Description => "Holy Trinity: 4H Navigator → 1H Filter → 15m Trigger | Fibonacci-Sequenzen";

    // === Deduplizierung + Whipsaw-Schutz ===
    private decimal _lastSignalPointA;
    private decimal _lastSignalPointB;
    private decimal _lastSignalPointC;
    private string _lastSignalSymbol = "";
    private bool _lastSignalIsLong;
    private int _signalCooldown;
    // === SK-Regel 4: Richtungs-Sperre + Gegensequenz nach Abarbeitung ===
    private bool? _completedDirection;
    private int _completedCooldown;
    private decimal _completedGkl559; // GKL 55.9% der abgearbeiteten Sequenz
    private decimal _completedGkl667; // GKL 66.7% der abgearbeiteten Sequenz

    // === Holy Trinity Parameter (4H → 1H → 15m) ===
    private int _h4SwingStrength = 5;         // 4H Navigator: Swing-Stärke
    private int _h1SwingStrength = 3;         // 1H Filter: feinere Swings
    private int _m15SwingStrength = 2;        // 15m Trigger: feinste Swings
    private decimal _minRangePercent = 0.5m;  // Min. A→B Range in %
    private bool _requireCloseBreak = true;   // 4H: Close über B für Aktivierung
    private int _minConfluence = 3;           // Min. Confluence (niedrig weil 3-TF-UND ist starker Filter)
    private decimal _slBufferPercent = 0.15m; // SL-Puffer unter 15m Punkt-0
    private TradingModePreset _activePreset = TradingModePreset.Swing;

    /// <summary>Aktueller Trading-Modus (wird bei Holy Trinity ignoriert).</summary>
    public TradingModePreset ActivePreset => _activePreset;

    /// <summary>SK-Regel: BE bei TP1 (50% Close, SL auf Entry).</summary>
    public bool DisableSmartBreakeven { get; private set; }

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("H4SwingStrength", "4H Navigator: Swing-Stärke", "int", _h4SwingStrength, 3, 10, 1),
        new("H1SwingStrength", "1H Filter: Swing-Stärke", "int", _h1SwingStrength, 2, 7, 1),
        new("M15SwingStrength", "15m Trigger: Swing-Stärke", "int", _m15SwingStrength, 1, 5, 1),
        new("MinRangePercent", "Min. A→B Range in %", "decimal", _minRangePercent, 0.1m, 3.0m, 0.1m),
        new("MinConfluence", "Min. Confluence-Score", "int", _minConfluence, 0, 10, 1),
        new("SLBufferPercent", "SL-Puffer unter 15m Punkt-0", "decimal", _slBufferPercent, 0.0m, 1.0m, 0.05m),
    };

    /// <summary>Holy Trinity: IMMER gleiche Parameter, kein Mode-Switch.</summary>
    public void ApplyPreset(TradingModePreset mode)
    {
        _activePreset = mode;
        // Alle Modi nutzen die gleiche Holy Trinity Konfiguration
        _h4SwingStrength = 5;
        _h1SwingStrength = 3;
        _m15SwingStrength = 2;
        _minRangePercent = 0.5m;
        _requireCloseBreak = true;
        _minConfluence = 3;
        _slBufferPercent = 0.15m;
        DisableSmartBreakeven = true; // SK-spezifischer gestufter BE (2× SL-Distanz, SL→TP1 bei 120%)
    }

    // ═══════════════════════════════════════════════════════════════
    // Hauptmethode: Holy Trinity Evaluate
    // ═══════════════════════════════════════════════════════════════

    public SignalResult Evaluate(MarketContext context)
    {
        // Holy Trinity: context.Candles = H4, HigherTimeframeCandles = H1, EntryTimeframeCandles = M15
        var h4Candles = context.Candles;
        var h1Candles = context.HigherTimeframeCandles;
        var m15Candles = context.EntryTimeframeCandles;

        if (h4Candles.Count < _h4SwingStrength * 2 + 20)
            return NoSignal("Zu wenig H4-Daten");

        var currentPrice = context.CurrentTicker.LastPrice;
        var currentClose = h4Candles[^1].Close;

        // Cooldowns runterzählen
        if (_signalCooldown > 0) _signalCooldown--;
        if (_completedCooldown > 0) _completedCooldown--;
        if (_completedCooldown > 0) _completedCooldown--;
        else _completedDirection = null;

        // Seitwärts-Filter: SK ist Trendfolge — ADX < 15 = kein Trend = pausieren
        var adx = IndicatorHelper.CalculateAdx(h4Candles, 14);
        if (adx.Count > 0 && adx[^1].HasValue && adx[^1]!.Value < 15m)
            return NoSignal($"Seitwärtsmarkt (4H ADX={adx[^1]!.Value:F0} < 15) — SK pausiert");

        // ═══════════════════════════════════════════════════════════
        // AMPEL 1 (GELB): 4H Navigator
        // Sucht aktivierte Sequenz mit Preis im GKL (50-66.7%)
        // ═══════════════════════════════════════════════════════════

        // State Machine statt Fraktal-Erkennung: Trailing Low findet B dynamisch
        var h4Machine = SequenceStateMachine.FromCandles(h4Candles, _minRangePercent, 0.3m);
        if (h4Machine == null || h4Machine.State < SmState.SucheB)
            return NoSignal($"4H: Keine Sequenz (State={h4Machine?.State})");

        var h4Seq = h4Machine.ToSequence();
        if (h4Seq == null)
            return NoSignal("4H: Sequenz nicht konstruierbar");

        // State Machine: SucheB = Korrektur läuft, Aktiviert = B gebrochen
        // (Suche0/SucheA bereits oben gefiltert)

        var inH4Gkl = h4Seq.IsInBuyZone(currentPrice) || h4Seq.IsInGklZone(currentPrice);

        // 4H: Preis MUSS im GKL/Korrekturlevel liegen ODER Sequenz muss Active sein (Re-Entry)
        // SK-Regel: Entry nur im 50-66.7% Retracement oder bei laufender Sequenz (BC-KL)
        if (!inH4Gkl && h4Machine.State != SmState.Aktiviert)
            return NoSignal("4H: Preis nicht im GKL und Sequenz nicht aktiviert");

        // FullyCompleted: Direkt mit HasFullyCompleted prüfen (ToSequence setzt diesen State nicht)
        if (h4Seq.HasFullyCompleted(currentPrice))
        {
            _completedDirection = h4Seq.IsLong;
            _completedCooldown = 20;
            _completedGkl559 = h4Seq.Retracement559;
            _completedGkl667 = h4Seq.Retracement667;
            return NoSignal("4H: Sequenz abgearbeitet (200%)");
        }

        // SK-Regel 4 + 13: Richtungs-Sperre + aktive Gegensequenz-Suche
        if (_completedDirection.HasValue && _completedDirection.Value == h4Seq.IsLong && _completedCooldown > 0)
        {
            // Während die alte Richtung gesperrt ist: Suche Gegensequenz ins GKL
            var counterDir = !_completedDirection.Value;
            var counterSeq = SequenceDetector.DetectSequence(h4Candles, _h4SwingStrength, _minRangePercent * 0.5m, false);
            if (counterSeq != null && counterSeq.IsLong == counterDir
                && counterSeq.State is SequenceState.WaitingBreak or SequenceState.Active
                && _completedGkl559 > 0 && _completedGkl667 > 0
                && SequenceDetector.IsInGKL(counterSeq.Extension1618, _completedGkl559, _completedGkl667))
            {
                // Gegensequenz gefunden: Ziel liegt im GKL der abgearbeiteten Sequenz
                // Override: Weiter mit Gegenrichtung (1H-Filter + 15m-Trigger prüfen)
                h4Seq = counterSeq;
                inH4Gkl = true; // Im GKL der alten Sequenz = gültiger Entry-Bereich
            }
            else
            {
                return NoSignal($"SK-Regel 4: {(h4Seq.IsLong ? "Long" : "Short")} gesperrt — {_completedCooldown} Kerzen (keine Gegensequenz ins GKL)");
            }
        }

        // Sequenztyp-Filter: Nur Typ 1 (Normal) handelbar
        if (!h4Seq.IsTradeableType)
            return NoSignal($"4H: Sequenz-Typ {h4Seq.Type} nicht handelbar");

        // Kill-Switch: Sandwich — Entry im Ziellevel einer AKTIVEN gegenlaufenden 4H-Sequenz
        // Nur prüfen: Die aktuellste Gegensequenz (nicht alle historischen!)
        // "Das größere Timeframe gewinnt immer"
        var counterMachine = new SequenceStateMachine(_minRangePercent, 0.3m);
        counterMachine.Reset();
        // Gegenrichtung: Eigene State Machine laufen lassen
        var counterSm = SequenceStateMachine.FromCandles(h4Candles, _minRangePercent, 0.3m);
        if (counterSm != null && counterSm.IsLong != h4Seq.IsLong && counterSm.State == SmState.Aktiviert)
        {
            // Gegensequenz ist AKTIVIERT — prüfe ob der aktuelle Preis im Ziellevel liegt
            var counterTarget = counterSm.Extension1618;
            var inCounterTarget = counterSm.IsLong
                ? currentPrice >= counterTarget * 0.95m && currentPrice <= counterTarget * 1.05m
                : currentPrice <= counterTarget * 1.05m && currentPrice >= counterTarget * 0.95m;
            if (inCounterTarget)
                return NoSignal("KILL: Sandwich — Entry im Ziellevel aktiver 4H-Gegensequenz");
        }

        var tradeIsLong = h4Seq.IsLong;
        // → GELB: 4H hat Sequenz, Preis im relevanten Bereich

        // ═══════════════════════════════════════════════════════════
        // AMPEL 2 (ORANGE): 1H Filter
        // Korrektur verliert Schwung? Erstes HH/HL erkannt?
        // ═══════════════════════════════════════════════════════════

        if (h1Candles is not { Count: > 20 })
            return NoSignal("1H: Keine Daten für Filter");

        var h1Swings = SequenceDetector.FindSwingPoints(h1Candles, _h1SwingStrength);
        var (correctionEnding, _) = SequenceDetector.DetectCorrectionEnd(h1Candles, h1Swings, tradeIsLong);

        if (!correctionEnding)
        {
            // Alternativ: Gibt es eine 1H-Sequenz in der gleichen Richtung?
            var h1Seq = SequenceDetector.DetectSequence(h1Candles, _h1SwingStrength, _minRangePercent * 0.5m, false);
            if (h1Seq == null || h1Seq.IsLong != tradeIsLong)
                return NoSignal("1H: Korrektur läuft noch — kein HH/HL erkannt");
            // 1H hat eine aligned Sequenz → das zählt als Filter-Bestätigung
        }

        // ChoCH auf 1H gegen die Trade-Richtung → blockieren
        var h1ChoCH = SequenceDetector.DetectChoCH(h1Swings);
        if (h1ChoCH != null && h1ChoCH.FromBullishToBearish == tradeIsLong)
            return NoSignal("1H: ChoCH gegen Trade-Richtung");

        // → ORANGE: 1H zeigt Korrektur-Ende

        // ═══════════════════════════════════════════════════════════
        // AMPEL 3 (GRÜN): 15m Trigger
        // Micro-Sequenz aktiviert? (Break über Punkt A/B)
        // ═══════════════════════════════════════════════════════════

        if (m15Candles is not { Count: > 20 })
            return NoSignal("15m: Keine Daten für Trigger");

        // 15m State Machine: Trailing Low findet den präzisen Micro-Entry
        var m15Machine = SequenceStateMachine.FromCandles(m15Candles, _minRangePercent * 0.3m, 0.15m);
        if (m15Machine == null)
            return NoSignal("15m: Keine Micro-Sequenz erkannt");

        if (m15Machine.IsLong != tradeIsLong)
            return NoSignal($"15m: Micro-Sequenz {(m15Machine.IsLong ? "Long" : "Short")} gegen 4H-Richtung");

        var microSeq = m15Machine.ToSequence();
        if (microSeq == null)
            return NoSignal("15m: Micro-Sequenz nicht konstruierbar");

        // 15m muss AKTIVIERT sein (State Machine: A wurde durchbrochen, B eingefroren)
        if (m15Machine.State != SmState.Aktiviert)
            return NoSignal($"15m: Micro-Sequenz nicht aktiviert (State={m15Machine.State})");

        // Kill-Switch: Over-Extension — 15m schon über 100% Extension
        var m15OverExt = tradeIsLong
            ? currentPrice > m15Machine.Extension100
            : currentPrice < m15Machine.Extension100; // Auch negative Extensions korrekt prüfen
        if (m15OverExt)
            return NoSignal("KILL: 15m über 100% Extension — Entry-Fenster verpasst");

        // → GRÜN: Alle 3 Ampeln aktiv!

        // ═══════════════════════════════════════════════════════════
        // DEDUPLIZIERUNG + WHIPSAW-SCHUTZ
        // ═══════════════════════════════════════════════════════════

        if (_lastSignalSymbol == context.Symbol &&
            _lastSignalPointA == microSeq.PointA.Price &&
            _lastSignalPointB == microSeq.PointB.Price &&
            _lastSignalPointC == (microSeq.PointC?.Price ?? 0))
            return NoSignal("Sequenz bereits signalisiert (Deduplizierung)");

        if (_signalCooldown > 0 && _lastSignalSymbol == context.Symbol && _lastSignalIsLong != tradeIsLong)
            return NoSignal($"Whipsaw-Schutz: {_signalCooldown} Kerzen Cooldown");

        // ═══════════════════════════════════════════════════════════
        // SL / TP / SIGNAL
        // ═══════════════════════════════════════════════════════════

        // SL unter 15m Punkt-0 (= m15Machine.Point0, winziges Risiko)
        var micro0 = m15Machine.Point0;
        var microSlBuf = micro0 * _slBufferPercent / 100m;
        var sl = tradeIsLong
            ? micro0 - microSlBuf
            : micro0 + microSlBuf;

        // SL-Distanz-Minimum: 0.3% (Spread + Fees)
        var slDistance = Math.Abs(currentPrice - sl);
        if (currentPrice > 0 && slDistance / currentPrice < 0.003m)
            return NoSignal($"SL-Distanz zu klein ({slDistance / currentPrice * 100:F2}%)");

        // TP1 = 15m Extension 161.8% ODER mindestens 2x SL-Distanz (was größer ist)
        // Verhindert dass TP1 fast gleich SL ist (RRR < 1.5 ist sinnlos)
        var m15Tp = m15Machine.Extension1618;
        var minTp1 = tradeIsLong
            ? currentPrice + slDistance * 2.0m   // Mindestens 2:1 RRR für TP1
            : currentPrice - slDistance * 2.0m;
        var tp1 = tradeIsLong
            ? Math.Max(m15Tp, minTp1)            // Größeres von beiden nehmen
            : Math.Min(m15Tp, minTp1);

        // TP2 = 4H Extension 161.8% (das große SK-Ziellevel — CRV 1:5 bis 1:10)
        var tp2 = h4Seq.Extension1618;

        // Wenn TP2 nicht weit genug vom Entry weg ist → 4H Extension 200% nehmen
        var tp2Dist = Math.Abs(tp2 - currentPrice);
        if (tp2Dist < slDistance * 3m)
            tp2 = h4Seq.Extension200;

        // RRR berechnen (auf TP2 — das ist das echte Ziel)
        var tpDist = Math.Abs(tp2 - currentPrice);
        var rrr = slDistance > 0 ? tpDist / slDistance : 0;

        // Min-RRR: Wenn selbst mit 4H-TP die RRR < 3:1 → kein Trade (nicht lohnenswert)
        if (rrr < 3m)
            return NoSignal($"RRR zu klein ({rrr:F1}:1 < 3:1) — 15m-SL zu weit für 4H-TP");

        // Confluence-Score (vereinfacht — die 3-TF-UND-Bedingung ist bereits ein starker Filter)
        var score = 3; // Basis: 3 TFs aligned
        var reasons = new List<string> { "4H+1H+15m" };

        if (inH4Gkl) { score += 2; reasons.Add("H4-GKL"); }
        if (h4Seq.HasGoodCharacter) { score += 1; reasons.Add($"H4-{h4Seq.CharacterPattern}"); }
        if (microSeq.HasGoodCharacter) { score += 1; reasons.Add($"M15-{microSeq.CharacterPattern}"); }
        if (rrr >= 5) { score += 1; reasons.Add($"RRR={rrr:F0}:1"); }

        // Volume-Bestätigung auf 15m: Dünnes Volumen = möglicher Fake-Out
        if (m15Candles.Count >= 20)
        {
            var avgVol = 0m;
            for (int i = m15Candles.Count - 21; i < m15Candles.Count - 1; i++)
                avgVol += m15Candles[i].Volume;
            avgVol /= 20m;
            if (avgVol > 0 && m15Candles[^1].Volume < avgVol * 0.5m)
            {
                score -= 1;
                reasons.Add("LowVol");
            }
        }

        if (score < _minConfluence)
            return NoSignal($"Confluence {score}/{_minConfluence} ({string.Join(", ", reasons)})");

        // Signal erstellen
        _lastSignalSymbol = context.Symbol;
        _lastSignalPointA = microSeq.PointA.Price;
        _lastSignalPointB = microSeq.PointB.Price;
        _lastSignalPointC = microSeq.PointC?.Price ?? 0;
        _lastSignalIsLong = tradeIsLong;
        _signalCooldown = 8;

        var side = tradeIsLong ? Signal.Long : Signal.Short;
        var confidence = Math.Clamp((decimal)score / 10m, 0m, 1m);

        var reasonText = $"SK Trinity {(tradeIsLong ? "Long" : "Short")} | " +
                         $"4H:A={h4Seq.PointA.Price:G6} B={h4Seq.PointB.Price:G6} | " +
                         $"15m:A={microSeq.PointA.Price:G6} B={microSeq.PointB.Price:G6} | " +
                         $"Score={score} RRR={rrr:F1}:1 | {string.Join(", ", reasons)}";

        return new SignalResult(side, confidence, currentPrice, sl, tp1, reasonText,
            TakeProfit2: tp2, ConfluenceScore: score, PreferLimitOrder: false,
            DisableSmartBreakeven: DisableSmartBreakeven, Tp1CloseRatioOverride: 0.5m);
    }

    // ═══════════════════════════════════════════════════════════════
    // IStrategy Methoden
    // ═══════════════════════════════════════════════════════════════

    public void WarmUp(IReadOnlyList<Candle> history) { }

    public void Reset()
    {
        _lastSignalPointA = 0;
        _lastSignalPointB = 0;
        _lastSignalPointC = 0;
        _lastSignalSymbol = "";
        _lastSignalIsLong = false;
        _signalCooldown = 0;
        _completedDirection = null;
        _completedCooldown = 0;
        _completedGkl559 = 0;
        _completedGkl667 = 0;
    }

    public IStrategy Clone() => new SequenzKonzeptStrategy
    {
        _h4SwingStrength = _h4SwingStrength,
        _h1SwingStrength = _h1SwingStrength,
        _m15SwingStrength = _m15SwingStrength,
        _minRangePercent = _minRangePercent,
        _requireCloseBreak = _requireCloseBreak,
        _minConfluence = _minConfluence,
        _slBufferPercent = _slBufferPercent,
        _activePreset = _activePreset,
        DisableSmartBreakeven = DisableSmartBreakeven
    };

    private static SignalResult NoSignal(string reason) =>
        new(Signal.None, 0m, null, null, null, reason);
}
