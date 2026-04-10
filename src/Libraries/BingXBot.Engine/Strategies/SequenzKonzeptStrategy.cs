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

    /// <summary>Letzter SK-Status (wird bei jedem Evaluate aktualisiert). Für UI-Anzeige und Debugging.</summary>
    public string LastStatus { get; private set; } = "";
    /// <summary>Letzte Ampel-Farbe pro TF: 4H/1H/15m. Für kompakte Status-Anzeige.</summary>
    public (string H4, string H1, string M15) AmpelStatus { get; private set; } = ("—", "—", "—");

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
    // === Flash-Crash Cooldown (Symbol-Sperre nach massiver Invalidierung) ===
    private int _crashCooldown;        // Kerzen-Countdown nach Flash-Crash
    private decimal _lastH4Close;      // Letzter 4H-Close für Crash-Erkennung
    // === State-Memory: 4H-GKL-Zone bleibt aktiv auch wenn Preis kurz rausspringt ===
    private int _h4GklActiveCountdown; // Evaluierungen seit letztem GKL-Kontakt
    private const int GklMemoryKerzen = 10; // Wie lange das GKL-Flag aktiv bleibt
    // === 4H-Sequenz Deduplizierung: Gleiche Sequenz nicht endlos wiederholen ===
    private decimal _lastH4SeqPointA;
    private decimal _lastH4SeqLockedB;

    // === Holy Trinity Parameter (4H → 1H → 15m) ===
    private int _h4SwingStrength = 5;         // 4H Navigator: Swing-Stärke
    private int _h1SwingStrength = 3;         // 1H Filter: feinere Swings
    private int _m15SwingStrength = 2;        // 15m Trigger: feinste Swings
    private decimal _minRangePercent = 0.5m;  // Min. A→B Range in %
    private bool _requireCloseBreak = true;   // 4H: Close über B für Aktivierung
    private int _minConfluence = 3;           // Min. Confluence (niedrig weil 3-TF-UND ist starker Filter)
    private decimal _slBufferPercent = 0.15m; // SL-Puffer unter Fib-Level (Liquidity-Grab-Schutz)
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
        // Ampel-Status Reset
        _curH4 = "—"; _curH1 = "—"; _curM15 = "—";

        var h4Candles = context.Candles;
        var h1Candles = context.HigherTimeframeCandles;
        var m15Candles = context.EntryTimeframeCandles;

        if (h4Candles.Count < _h4SwingStrength * 2 + 20)
            return Blocked("Zu wenig H4-Daten");

        var currentPrice = context.CurrentTicker.LastPrice;
        var currentClose = h4Candles[^1].Close;

        // Category-abhängige MinRange: TradFi hat engere Ranges als Krypto
        var categoryFactor = context.Category switch
        {
            MarketCategory.Forex     => 0.25m,   // Forex: 1/4 der Krypto-Range
            MarketCategory.Stock     => 0.5m,    // Aktien: halbe Range
            MarketCategory.Index     => 0.4m,    // Indices
            MarketCategory.Commodity => 0.6m,    // Rohstoffe
            _ => 1.0m                            // Krypto: Basis
        };
        var effectiveMinRange = _minRangePercent * categoryFactor;

        // Cooldowns runterzählen
        if (_signalCooldown > 0) _signalCooldown--;
        if (_completedCooldown > 0) _completedCooldown--;
        else _completedDirection = null;
        if (_crashCooldown > 0) _crashCooldown--;

        // Flash-Crash-Erkennung: 4H-Kerze > 5% Bewegung = massive Volatilität
        // → Symbol für 4 Kerzen (= 1h bei 15m-Scan) pausieren
        if (_lastH4Close > 0 && currentClose > 0)
        {
            var movePercent = Math.Abs(currentClose - _lastH4Close) / _lastH4Close * 100m;
            var crashThreshold = 5m * categoryFactor; // Forex: 1.25%, Stock: 2.5%, Crypto: 5%
            if (movePercent > crashThreshold)
            {
                _crashCooldown = 4; // 4 Evaluierungen pausieren
            }
        }
        _lastH4Close = currentClose;

        if (_crashCooldown > 0)
            return Blocked($"Flash-Crash Cooldown: {_crashCooldown} Kerzen Pause nach massiver Bewegung");

        // Seitwärts-Filter: ADX-Schwelle category-abhängig (TradFi oft niedrigerer ADX bei Trends)
        var adxThreshold = categoryFactor < 1.0m ? 12m : 15m;
        var adx = IndicatorHelper.CalculateAdx(h4Candles, 14);
        if (adx.Count > 0 && adx[^1].HasValue && adx[^1]!.Value < adxThreshold)
            return Blocked($"Seitwärtsmarkt (4H ADX={adx[^1]!.Value:F0} < {adxThreshold}) — SK pausiert");

        // ═══════════════════════════════════════════════════════════
        // AMPEL 1 (GELB): 4H Navigator
        // Sucht aktivierte Sequenz mit Preis im GKL (50-66.7%)
        // ═══════════════════════════════════════════════════════════

        // State Machine statt Fraktal-Erkennung: Trailing Low findet B dynamisch
        // State Machine: B-Retracement wird berechnet (FibConfidence im Score), kein harter Filter
        var h4Machine = SequenceStateMachine.FromCandles(h4Candles, effectiveMinRange, 0.3m * categoryFactor);
        if (h4Machine == null || h4Machine.State < SmState.SucheB)
            return NoSignal($"4H: Keine Sequenz (State={h4Machine?.State})");

        var h4Seq = h4Machine.ToSequence();
        if (h4Seq == null)
            return Blocked("4H: Sequenz nicht konstruierbar");

        // 4H gibt die RICHTUNG und das GKL vor. Aktiviert ist ideal (bestätigter B),
        // SucheB ist erlaubt (Korrektur läuft, 15m-Trigger entscheidet).
        // Elliott B-Retracement-Validierung (23.6-88.6%) filtert ungültige Wellen.
        // FibConfidence im Confluence-Score belohnt saubere B-Punkte.

        // Range-Markt-Filter: Zu große Sequenz-Range = Seitwärtskanal (category-skaliert)
        var maxRangePercent = categoryFactor switch
        {
            <= 0.25m => 0.03m,  // Forex: 3%
            <= 0.5m  => 0.06m,  // Aktien: 6%
            <= 0.6m  => 0.08m,  // Rohstoffe/Indices: 8%
            _        => 0.12m   // Krypto: 12%
        };
        var h4Range = Math.Abs(h4Machine.PointA - h4Machine.Point0);
        if (h4Machine.PointA > 0 && h4Range / h4Machine.PointA > maxRangePercent)
            return Blocked($"4H: Range zu groß ({h4Range / h4Machine.PointA * 100:F1}% > {maxRangePercent * 100:F0}%) — Range-Markt");

        var inH4GklNow = h4Seq.IsInBuyZone(currentPrice) || h4Seq.IsInGklZone(currentPrice);

        // State-Memory: Wenn der Preis das GKL berührt hat, bleibt das Flag für N Evaluierungen aktiv.
        // Verhindert MTFA-Deadlock: 4H fällt ins GKL → 15m braucht Stunden zum Drehen →
        // 4H ist kurz raus → ohne Memory wäre der Confluence-Bonus sofort weg.
        if (inH4GklNow)
            _h4GklActiveCountdown = GklMemoryKerzen;
        else if (_h4GklActiveCountdown > 0)
            _h4GklActiveCountdown--;

        var inH4Gkl = inH4GklNow || _h4GklActiveCountdown > 0;

        // Anti-MTFA-Deadlock: 4H muss NICHT zwingend im GKL sein.
        // 4H gibt die RICHTUNG vor. Solange die 4H-Sequenz ≥ SucheB ist (Korrektur/Aktiviert)
        // und nicht abgearbeitet → Entry über 15m-Trigger erlaubt.
        // GKL ist ein CONFLUENCE-BONUS (+2), kein harter Filter.

        // Abgearbeitet: State Machine erkennt 161.8%, HasFullyCompleted prüft 200%
        // Beides → Richtungs-Sperre + GKL merken für Gegensequenz
        if (h4Seq.State == SequenceState.TargetReached || h4Seq.HasFullyCompleted(currentPrice))
        {
            _completedDirection = h4Seq.IsLong;
            _completedCooldown = 20;
            _completedGkl559 = h4Seq.Retracement559;
            _completedGkl667 = h4Seq.Retracement667;
            return Blocked($"4H: Sequenz abgearbeitet ({(h4Seq.HasFullyCompleted(currentPrice) ? "200%" : "161.8%")})");
        }

        // SK-Regel 4 + 13: Richtungs-Sperre + aktive Gegensequenz-Suche
        if (_completedDirection.HasValue && _completedDirection.Value == h4Seq.IsLong && _completedCooldown > 0)
        {
            // Während die alte Richtung gesperrt ist: Suche Gegensequenz ins GKL
            var counterDir = !_completedDirection.Value;
            var counterSeq = SequenceDetector.DetectSequence(h4Candles, _h4SwingStrength, effectiveMinRange * 0.5m, false);
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
                return Blocked($"SK-Regel 4: {(h4Seq.IsLong ? "Long" : "Short")} gesperrt — {_completedCooldown} Kerzen (keine Gegensequenz ins GKL)");
            }
        }

        // Sequenztyp-Filter: Nur Typ 1 (Normal) handelbar
        if (!h4Seq.IsTradeableType)
            return Blocked($"4H: Sequenz-Typ {h4Seq.Type} nicht handelbar");

        // Kill-Switch: Sandwich — Entry im Ziellevel einer AKTIVEN 4H-Gegensequenz
        // Nur prüfen gegen die State Machine (aktuellste Gegensequenz), nicht alle historischen.
        // Alte Variante (DetectAllSequences) blockierte 25% aller Evaluierungen weil der Preis
        // fast immer nahe irgendeinem abgearbeiteten Ziellevel liegt.
        var counterSm = SequenceStateMachine.FromCandles(h4Candles, effectiveMinRange, 0.3m);
        if (counterSm != null && counterSm.IsLong != h4Seq.IsLong && counterSm.State == SmState.Aktiviert)
        {
            var counterTarget = counterSm.Extension1618;
            var sandwichTol = 0.02m; // 2% Toleranz (enger als vorher 5%)
            if (counterTarget > 0 && Math.Abs(currentPrice - counterTarget) / counterTarget < sandwichTol)
                return Blocked("KILL: Sandwich — Entry im Ziellevel aktiver 4H-Gegensequenz");
        }

        var tradeIsLong = h4Seq.IsLong;
        var h4Dir = tradeIsLong ? "Long" : "Short";

        // 4H-Sequenz Deduplizierung: Identische A/B-Punkte nicht endlos wiederholen
        // (SOL-Problem: 10 von 10 Signalen handeln denselben 4H-Kanal)
        if (h4Machine.State == SmState.Aktiviert
            && _lastH4SeqPointA == h4Machine.PointA && _lastH4SeqLockedB == h4Machine.LockedB)
            return Blocked($"4H: Identische Sequenz bereits gehandelt (A={h4Machine.PointA:G6})");

        _curH4 = $"GELB {h4Dir} ({h4Machine.State})";
        // → GELB: 4H hat Sequenz, Preis im relevanten Bereich

        // ═══════════════════════════════════════════════════════════
        // AMPEL 2 (ORANGE): 1H Filter
        // Korrektur verliert Schwung? Erstes HH/HL erkannt?
        // ═══════════════════════════════════════════════════════════

        // 1H-Filter: Wenn keine Daten verfügbar → 15m-Trigger entscheidet allein
        // (Im Live-Bot fehlt 1H nie, im Backtest kann es am Anfang fehlen)
        var h1Available = h1Candles is { Count: > 20 };
        var correctionEnding = false;

        if (h1Available)
        {
            var h1Swings = SequenceDetector.FindSwingPoints(h1Candles, _h1SwingStrength);
            (correctionEnding, _) = SequenceDetector.DetectCorrectionEnd(h1Candles, h1Swings, tradeIsLong);

            if (!correctionEnding)
            {
                var h1Seq = SequenceDetector.DetectSequence(h1Candles, _h1SwingStrength, effectiveMinRange * 0.5m, false);
                if (h1Seq != null && h1Seq.IsLong != tradeIsLong
                    && h1Seq.State == SequenceState.Active)
                    return Blocked("1H: Aktive Gegensequenz — Korrektur hat noch Schwung");

                var h1ChoCH = SequenceDetector.DetectChoCH(h1Swings);
                if (h1ChoCH != null && h1ChoCH.FromBullishToBearish == tradeIsLong)
                    return Blocked("1H: ChoCH gegen Trade-Richtung");
            }
        }

        _curH1 = !h1Available ? "ORANGE (keine 1H-Daten)" :
                 correctionEnding ? "ORANGE (Korrektur-Ende)" : "ORANGE (1H durchgelassen)";
        // → ORANGE: 1H blockiert nicht (Korrektur endet ODER kein aktiver Gegentrend)

        // ═══════════════════════════════════════════════════════════
        // AMPEL 3 (GRÜN): 15m Trigger
        // Micro-Sequenz aktiviert? (Break über Punkt A/B)
        // ═══════════════════════════════════════════════════════════

        if (m15Candles is not { Count: > 20 })
            return Blocked("15m: Keine Daten für Trigger");

        // 15m State Machine: Trailing Low findet den präzisen Micro-Entry
        // 15m State Machine: Trailing Low findet den präzisen Micro-Entry
        var m15Machine = SequenceStateMachine.FromCandles(m15Candles, effectiveMinRange * 0.3m, 0.15m * categoryFactor);
        if (m15Machine == null)
            return Blocked("15m: Keine Micro-Sequenz erkannt");

        if (m15Machine.IsLong != tradeIsLong)
            return Blocked($"15m: Micro-Sequenz {(m15Machine.IsLong ? "Long" : "Short")} gegen 4H-Richtung");

        var microSeq = m15Machine.ToSequence();
        if (microSeq == null)
            return Blocked("15m: Micro-Sequenz nicht konstruierbar");

        // 15m muss AKTIVIERT sein (State Machine: A wurde durchbrochen, B eingefroren)
        if (m15Machine.State != SmState.Aktiviert)
            return Blocked($"15m: Micro-Sequenz nicht aktiviert (State={m15Machine.State})");

        // Elliott-Proportions und FibConfidence: Weiche Bewertung via Confluence-Score,
        // kein harter Filter (killt sonst profitable Krypto-Setups bei flachen Korrekturen).

        // ChoCh-Bestätigung auf 15m: Strukturbruch innerhalb der Kaufzone
        // Die 15m-Aktivierung IST ein ChoCh (Close über letztes Swing-High nach tieferen Highs).
        // Zusätzlich: Prüfe ob auf 15m ein ChoCh GEGEN unsere Richtung lauert (Warnung)
        var m15Swings = SequenceDetector.FindSwingPoints(m15Candles, _m15SwingStrength);
        var m15ChoCH = SequenceDetector.DetectChoCH(m15Swings);
        if (m15ChoCH != null && m15ChoCH.FromBullishToBearish == tradeIsLong)
            return Blocked("15m: ChoCH gegen Trade-Richtung — Strukturbruch blockiert Entry");

        // ATR auf 15m — wird für Mindestgröße UND SL-Buffer verwendet
        var m15Atr = IndicatorHelper.CalculateAtr(m15Candles, 14);
        var m15AtrValue = m15Atr.Count > 0 && m15Atr[^1].HasValue ? m15Atr[^1]!.Value : 0m;

        // Mikro-Sequenz Mindestgröße: 0→A Strecke muss >= 2× ATR_15m sein
        // Verhindert Trading von Rauschen (0.15% Sequenzen die nur Fees kosten)
        if (m15AtrValue > 0)
        {
            var seqRange = Math.Abs(m15Machine.PointA - m15Machine.Point0);
            var minRange = m15AtrValue * 2m;
            if (seqRange < minRange)
                return Blocked($"15m: Sequenz zu klein ({seqRange:F8} < 2×ATR {minRange:F8}) — nur Rauschen");
        }

        // Kill-Switch: Over-Extension — 15m schon über 100% Extension
        var m15OverExt = tradeIsLong
            ? currentPrice > m15Machine.Extension100
            : currentPrice < m15Machine.Extension100; // Auch negative Extensions korrekt prüfen
        if (m15OverExt)
            return Blocked("KILL: 15m über 100% Extension — Entry-Fenster verpasst");

        _curM15 = $"GRÜN (Aktiviert, 0={m15Machine.Point0:G6} A={m15Machine.PointA:G6})";
        // → GRÜN: Alle 3 Ampeln aktiv!

        // ═══════════════════════════════════════════════════════════
        // DEDUPLIZIERUNG + WHIPSAW-SCHUTZ
        // ═══════════════════════════════════════════════════════════

        if (_lastSignalSymbol == context.Symbol &&
            _lastSignalPointA == microSeq.PointA.Price &&
            _lastSignalPointB == microSeq.PointB.Price &&
            _lastSignalPointC == (microSeq.PointC?.Price ?? 0))
            return Blocked("Sequenz bereits signalisiert (Deduplizierung)");

        if (_signalCooldown > 0 && _lastSignalSymbol == context.Symbol && _lastSignalIsLong != tradeIsLong)
            return Blocked($"Whipsaw-Schutz: {_signalCooldown} Kerzen Cooldown");

        // ═══════════════════════════════════════════════════════════
        // SL / TP / SIGNAL — SK Fibonacci-Level SL
        // ═══════════════════════════════════════════════════════════

        // SK-System: SL knapp vor dem NÄCHSTEN tieferen 4H-Fibonacci-Level.
        // Nicht unter dem 15m-Punkt-0 (das war 2-5% weg → riesige Verluste).
        //
        // Fibonacci-Staffelung: Preis im 50%-Level → SL vor 55.9%
        //                       Preis im 55.9%-Level → SL vor 61.8%
        //                       Preis im 61.8%-Level → SL vor 66.7%
        //                       Preis im 66.7%-Level → SL vor 78.6% (letztes Level vor Invalidierung)
        //
        // Der kleine Puffer (_slBufferPercent) verhindert Liquidity-Grab-Stops.
        var fibLevels = new[] { h4Seq.Retracement500, h4Seq.Retracement559, h4Seq.Retracement618, h4Seq.Retracement667, h4Seq.Retracement786 };

        // Finde das nächste tiefere Fib-Level als SL
        // Skip(1): Nicht das direkt nächste Level (dort sammeln sich Stops → Liquidity-Grab),
        // sondern das übernächste. Der fibBuffer gibt zusätzlichen Puffer.
        decimal sl;
        var fibBuffer = currentPrice * _slBufferPercent / 100m;
        if (tradeIsLong)
        {
            // Long: SL unter dem übernächsten tieferen Fib-Level
            var nextFib = fibLevels.Where(f => f < currentPrice).OrderByDescending(f => f).Skip(1).FirstOrDefault();
            if (nextFib == 0) nextFib = h4Seq.Retracement786; // Fallback: letztes Level
            sl = nextFib - fibBuffer;
        }
        else
        {
            // Short: SL über dem übernächsten höheren Fib-Level
            var nextFib = fibLevels.Where(f => f > currentPrice).OrderBy(f => f).Skip(1).FirstOrDefault();
            if (nextFib == 0) nextFib = h4Seq.Retracement786; // Fallback: letztes Level
            sl = nextFib + fibBuffer;
        }

        // Fallback: Wenn Fib-SL unrealistisch (>2% oder <0.2%) → 15m-Punkt-0 als Alternative
        var slDistance = Math.Abs(currentPrice - sl);
        // SL-Grenzen: Feste Prozent-Grenzen (einfach, robust, category-skaliert)
        var maxSlPercent = 0.025m; // 2.5% max für Krypto
        var minSlPercent = 0.002m; // 0.2% min
        var slPercent = currentPrice > 0 ? slDistance / currentPrice : 0m;
        if (slPercent > maxSlPercent || slPercent < minSlPercent * 0.5m)
        {
            // Fib-SL unrealistisch → ATR-basierter SL als Fallback (statt 15m-Punkt-0)
            // 3× ATR_15m = struktureller Stop der Volatilität entspricht
            var atrSl = m15AtrValue * 3m;
            if (atrSl > 0)
            {
                sl = tradeIsLong ? currentPrice - atrSl : currentPrice + atrSl;
            }
            else
            {
                // Kein ATR → 15m-Punkt-0 mit kleinem Buffer
                var micro0 = m15Machine.Point0;
                var microBuffer = Math.Max(m15AtrValue * 0.5m, micro0 * 0.001m);
                sl = tradeIsLong ? micro0 - microBuffer : micro0 + microBuffer;
            }
            slDistance = Math.Abs(currentPrice - sl);
            slPercent = currentPrice > 0 ? slDistance / currentPrice : 0m;
        }

        if (slPercent < minSlPercent)
            return Blocked($"SL-Distanz zu klein ({slPercent * 100:F2}% < {minSlPercent * 100:F2}%)");
        if (slPercent > maxSlPercent)
            return Blocked($"SL-Distanz zu groß ({slPercent * 100:F2}% > {maxSlPercent * 100:F1}%)");

        // TP1 = 15m Extension 161.8% ODER mindestens 1.5x SL-Distanz (was größer ist)
        // 1.5x statt 2.0x: Der eigentliche Gewinn kommt aus TP2 (4H Extension).
        // TP1 dient nur zum Risiko-Rausnehmen (Partial Close + BE).
        var m15Tp = m15Machine.Extension1618;
        var minTp1 = tradeIsLong
            ? currentPrice + slDistance * 1.5m   // Mindestens 1.5:1 RRR für TP1
            : currentPrice - slDistance * 1.5m;
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
            return Blocked($"RRR zu klein ({rrr:F1}:1 < 3:1) — 15m-SL zu weit für 4H-TP");

        // ═══════════════════════════════════════════════════════════
        // SMC-KONFLUENZ: Order Blocks + Fair Value Gaps (Endboss-Filter)
        // SK-Zone + SMC Orderblock = High-Probability-Setup
        // ═══════════════════════════════════════════════════════════

        var h4Swings = SequenceDetector.FindSwingPoints(h4Candles, _h4SwingStrength);
        var orderBlocks = SmcAnalyzer.FindOrderBlocks(h4Candles, h4Swings);
        var fvgs = SmcAnalyzer.FindFairValueGaps(h4Candles);

        var smcType = tradeIsLong ? SmcZoneType.Bullish : SmcZoneType.Bearish;
        var hasSmcConfluence = false;

        // Prüfe ob der aktuelle Preis in einem aligned Order Block liegt
        foreach (var ob in orderBlocks)
        {
            if (ob.Type == smcType && currentPrice >= ob.ZoneLow && currentPrice <= ob.ZoneHigh)
            {
                hasSmcConfluence = true;
                break;
            }
        }
        // Oder in einem aligned FVG
        if (!hasSmcConfluence)
        {
            var activeFvg = SmcAnalyzer.GetActiveFvg(currentPrice, fvgs, smcType);
            if (activeFvg != null)
                hasSmcConfluence = true;
        }

        // ═══════════════════════════════════════════════════════════
        // CONFLUENCE-SCORE
        // ═══════════════════════════════════════════════════════════

        var score = 3; // Basis: 3 TFs aligned
        var reasons = new List<string> { "4H+1H+15m" };

        if (inH4Gkl) { score += 2; reasons.Add("H4-GKL"); }
        if (h4Seq.HasGoodCharacter) { score += 1; reasons.Add($"H4-{h4Seq.CharacterPattern}"); }
        if (microSeq.HasGoodCharacter) { score += 1; reasons.Add($"M15-{microSeq.CharacterPattern}"); }
        if (rrr >= 5) { score += 1; reasons.Add($"RRR={rrr:F0}:1"); }

        // Elliott Fib-Confidence: B-Punkt nahe idealem Fibonacci-Level → Bonus
        if (m15Machine.FibConfidence >= 0.7m) { score += 1; reasons.Add($"FibB={m15Machine.BRetracementRatio:P0}"); }
        if (h4Machine.FibConfidence >= 0.7m) { score += 1; reasons.Add($"H4-FibB={h4Machine.BRetracementRatio:P0}"); }

        // Elliott Proportions-Bonus: 15m-Range >= 10% der 4H-Range → stärkere Sub-Welle
        var m15Range = m15Machine.ImpulseRange;
        if (h4Range > 0 && m15Range > 0 && m15Range / h4Range >= 0.10m)
        { score += 1; reasons.Add($"M15-Prop={m15Range / h4Range * 100:F0}%"); }

        // SMC-Konfluenz: Order Block oder FVG am Entry → +2 (High-Probability-Setup)
        if (hasSmcConfluence) { score += 2; reasons.Add("SMC-OB/FVG"); }

        // Volume POC: Preis nahe dem höchsten Volumen-Level der 4H-Strecke (0→A)?
        // Wenn Fibonacci + POC am gleichen Preis → institutionelle Konfluenz → +1
        if (h4Candles.Count > 20)
        {
            var pocPrice = CalculateVPOC(h4Candles, Math.Max(0, h4Candles.Count - 50), h4Candles.Count);
            if (pocPrice > 0)
            {
                var pocDist = Math.Abs(currentPrice - pocPrice) / currentPrice;
                if (pocDist < 0.01m) // Preis innerhalb 1% vom POC
                {
                    score += 1;
                    reasons.Add("VPOC");
                }
            }
        }

        // Volume-Bestätigung auf 15m: Dünnes Volumen = möglicher Fake-Out
        if (m15Candles.Count > 20)
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
            return Blocked($"Confluence {score}/{_minConfluence} ({string.Join(", ", reasons)})");

        // ═══════════════════════════════════════════════════════════
        // SIGNAL ERSTELLEN
        // ═══════════════════════════════════════════════════════════

        _lastSignalSymbol = context.Symbol;
        _lastSignalPointA = microSeq.PointA.Price;
        _lastSignalPointB = microSeq.PointB.Price;
        _lastSignalPointC = microSeq.PointC?.Price ?? 0;
        _lastSignalIsLong = tradeIsLong;
        _signalCooldown = 8;
        // 4H-Sequenz merken für Deduplizierung (gleiche Sequenz nicht endlos wiederholen)
        _lastH4SeqPointA = h4Machine.PointA;
        _lastH4SeqLockedB = h4Machine.LockedB;

        var side = tradeIsLong ? Signal.Long : Signal.Short;
        var confidence = Math.Clamp((decimal)score / 10m, 0m, 1m);

        // Retest-Entry: Statt MARKET bei Aktivierung → Limit-Order am Punkt-A Level
        // Spart Slippage + Taker-Fees. Der Preis testet das Level oft nach dem Break.
        var retestEntry = m15Machine.PointA; // Punkt-A = das durchbrochene Level
        var useLimit = true; // Immer Limit-Entry (Retest-Strategie)

        AmpelStatus = (_curH4, _curH1, _curM15);
        LastStatus = $"SIGNAL! {(tradeIsLong ? "Long" : "Short")} Score={score} RRR={rrr:F1}:1";

        // Log mit SK-Original-Nomenklatur: 0=Ursprung, A=Impulsgipfel, B=Korrekturpunkt
        // (Sequence.PointA=SM.Point0=SK:0, Sequence.PointB=SM.PointA=SK:A, LockedB=SK:B)
        var reasonText = $"SK Trinity {(tradeIsLong ? "Long" : "Short")} | " +
                         $"4H:0={h4Machine.Point0:G6} A={h4Machine.PointA:G6} B={h4Machine.LockedB:G6} | " +
                         $"15m:0={m15Machine.Point0:G6} A={m15Machine.PointA:G6} B={m15Machine.LockedB:G6} | " +
                         $"Score={score} RRR={rrr:F1}:1 | {string.Join(", ", reasons)}";

        return new SignalResult(side, confidence, retestEntry, sl, tp1, reasonText,
            TakeProfit2: tp2, ConfluenceScore: score, PreferLimitOrder: useLimit,
            DisableSmartBreakeven: DisableSmartBreakeven, Tp1CloseRatioOverride: 0.3m);
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
        _crashCooldown = 0;
        _lastH4Close = 0;
        _h4GklActiveCountdown = 0;
        _lastH4SeqPointA = 0;
        _lastH4SeqLockedB = 0;
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

    /// <summary>
    /// Berechnet den Volume Point of Control (VPOC) — das Preis-Level mit dem höchsten Volumen.
    /// Einfache Bucket-Implementierung: Preis-Range in 50 Buckets aufteilen, Volumen pro Bucket summieren.
    /// </summary>
    private static decimal CalculateVPOC(IReadOnlyList<Candle> candles, int startIdx, int endIdx)
    {
        if (endIdx <= startIdx || endIdx > candles.Count) return 0;

        var high = decimal.MinValue;
        var low = decimal.MaxValue;
        for (int i = startIdx; i < endIdx; i++)
        {
            if (candles[i].High > high) high = candles[i].High;
            if (candles[i].Low < low) low = candles[i].Low;
        }
        if (high <= low || low <= 0) return 0;

        const int buckets = 50;
        var bucketSize = (high - low) / buckets;
        var volumes = new decimal[buckets];

        for (int i = startIdx; i < endIdx; i++)
        {
            // Volumen auf die Buckets verteilen die die Kerze abdeckt
            var cLow = Math.Max(candles[i].Low, low);
            var cHigh = Math.Min(candles[i].High, high);
            var startBucket = Math.Max(0, (int)((cLow - low) / bucketSize));
            var endBucket = Math.Min(buckets - 1, (int)((cHigh - low) / bucketSize));
            var bucketCount = endBucket - startBucket + 1;
            if (bucketCount <= 0) continue;
            var volPerBucket = candles[i].Volume / bucketCount;
            for (int b = startBucket; b <= endBucket; b++)
                volumes[b] += volPerBucket;
        }

        // Bucket mit höchstem Volumen = POC
        var maxVol = 0m;
        var pocBucket = 0;
        for (int b = 0; b < buckets; b++)
        {
            if (volumes[b] > maxVol)
            {
                maxVol = volumes[b];
                pocBucket = b;
            }
        }

        return low + (pocBucket + 0.5m) * bucketSize; // Mitte des Buckets
    }

    // Ampel-Tracking: Wird progressiv aktualisiert während Evaluate() läuft
    private string _curH4 = "—", _curH1 = "—", _curM15 = "—";

    private SignalResult Blocked(string reason)
    {
        AmpelStatus = (_curH4, _curH1, _curM15);
        LastStatus = $"[4H:{_curH4}|1H:{_curH1}|15m:{_curM15}] {reason}";
        return new SignalResult(Signal.None, 0m, null, null, null, LastStatus);
    }

    private static SignalResult NoSignal(string reason) =>
        new(Signal.None, 0m, null, null, null, reason);
}
