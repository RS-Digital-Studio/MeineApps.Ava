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

    // === Deduplizierung + Whipsaw-Schutz (SK-Nomenklatur: Point0/PointA/PointB) ===
    private decimal _lastSignalPoint0;
    private decimal _lastSignalPointA;
    private decimal _lastSignalPointB;
    private string _lastSignalSymbol = "";
    private bool _lastSignalIsLong;
    private int _signalCooldown;
    // === SK-Regel 4: Richtungs-Sperre + Gegensequenz nach Abarbeitung ===
    private bool? _completedDirection;
    private int _completedCooldown;
    private decimal _completedGkl500; // SK-VERIFY: [2.3] GKL 50% der Gesamtstrecke 0→TargetC (war: 55.9% der 0→A Strecke)
    private decimal _completedGkl667; // SK-VERIFY: [2.3] GKL 66.7% der Gesamtstrecke 0→TargetC
    // === Flash-Crash Cooldown (Symbol-Sperre nach massiver Invalidierung) ===
    private int _crashCooldown;        // Kerzen-Countdown nach Flash-Crash
    private decimal _lastH4Close;      // Letzter 4H-Close für Crash-Erkennung
    // === State-Memory: 4H-GKL-Zone bleibt aktiv auch wenn Preis kurz rausspringt ===
    // SK-VERIFY: [3.4] Zonen-Memory = 10 Kerzen des TF (nicht Evaluierungen)
    private DateTime _h4GklLastTouchTime; // Zeitpunkt des letzten GKL-Kontakts (4H-Kerze)
    private const int GklMemoryKerzen = 10; // 10 × 4H-Kerzen = 40 Stunden Toleranz
    // === 4H-Sequenz Deduplizierung: Gleiche Sequenz nicht endlos wiederholen ===
    private decimal _lastH4SeqPointA;
    private decimal _lastH4SeqLockedB;

    // SK-VERIFY: [3.5] Fahrplan (MarketBias) — Weekly/Daily Richtungsvorgabe
    // EMA-200 auf 4H-Candles ≈ D1-EMA-33. Als Soft-Filter (Confluence-Malus), NICHT Hard-Block.
    // SK-System: Fahrplan basiert auf Marktstruktur (BLASH), nicht nur auf EMA.
    // EMA-200 gegen Richtung = -1 Confluence, nicht Block (Mean Reversion an Wendebereichen ist erwünscht).
    private bool? _lastFahrplanBias; // true=Long, false=Short, null=Neutral
    private decimal _lastEma200;     // Letzter EMA-200 Wert für Logging
    private bool _tradeAgainstEma;   // True wenn Trade gegen EMA-200 läuft (für Confluence-Malus)

    // SK-VERIFY: [3.11] Bottom-Up Feedback — Trade-Ergebnisse für Richtungs-Anpassung
    private int _consecutiveFailsInDirection; // Aufeinanderfolgende Verluste in gleicher Richtung

    // === Holy Trinity Parameter (4H → 1H → 15m) ===
    private int _h4SwingStrength = 5;         // 4H Navigator: Swing-Stärke
    private int _h1SwingStrength = 3;         // 1H Filter: feinere Swings
    private int _m15SwingStrength = 2;        // 15m Trigger: feinste Swings
    private decimal _minRangePercent = 0.5m;  // Min. A→B Range in %
    // _requireCloseBreak entfernt: State Machine nutzt immer Close-Break (SM ProcessSucheB Zeile 339)
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
        if (_lastH4Close == 0)
        {
            // Erster Evaluate dieses Klons: nur initialisieren, kein Crash-Check möglich
            _lastH4Close = currentClose;
        }
        else if (currentClose > 0)
        {
            var movePercent = Math.Abs(currentClose - _lastH4Close) / _lastH4Close * 100m;
            var crashThreshold = 5m * categoryFactor; // Forex: 1.25%, Stock: 2.5%, Crypto: 5%
            if (movePercent > crashThreshold)
            {
                _crashCooldown = 4; // 4 Evaluierungen pausieren
            }
            _lastH4Close = currentClose;
        }

        if (_crashCooldown > 0)
            return Blocked($"Flash-Crash Cooldown: {_crashCooldown} Kerzen Pause nach massiver Bewegung");

        // SK-VERIFY: [5.1] ADX-Schwelle auf 20 angehoben (war 15/12). ADX < 25 = richtungsloser Markt
        var adxThreshold = categoryFactor < 1.0m ? 15m : 20m;
        var adx = IndicatorHelper.CalculateAdx(h4Candles, 14);
        if (adx.Count > 0 && adx[^1].HasValue && adx[^1]!.Value < adxThreshold)
            return Blocked($"Seitwärtsmarkt (4H ADX={adx[^1]!.Value:F0} < {adxThreshold}) — SK pausiert");

        // ═══════════════════════════════════════════════════════════
        // SK-VERIFY: [3.5] FAHRPLAN — Übergeordnete Marktrichtung (EMA-200 auf 4H)
        // Ohne Fahrplan = kein Trade. Das ist der #1 Anfängerfehler im SK-System.
        // EMA-200 auf 4H-Candles ≈ D1-EMA-33 (200 × 4h = 800h ≈ 33 Tage).
        // ═══════════════════════════════════════════════════════════

        var ema200 = IndicatorHelper.CalculateEma(h4Candles, 200);
        if (ema200.Count > 0 && ema200[^1].HasValue)
        {
            _lastEma200 = ema200[^1]!.Value;
            _lastFahrplanBias = currentPrice > _lastEma200; // true=Long, false=Short
        }
        else
        {
            _lastFahrplanBias = null; // Nicht genug Daten — neutral
        }

        // ═══════════════════════════════════════════════════════════
        // AMPEL 1 (GELB): 4H Navigator
        // Sucht aktivierte Sequenz mit Preis im GKL (50-66.7%)
        // ═══════════════════════════════════════════════════════════

        // State Machine statt Fraktal-Erkennung: Trailing Low findet B dynamisch
        // State Machine: B-Retracement wird berechnet (FibConfidence im Score), kein harter Filter
        // FromCandlesBoth: Beide Richtungen in einem Durchlauf (wird auch für Sandwich-Check verwendet)
        var (h4Machine, h4LongMachine, h4ShortMachine) = SequenceStateMachine.FromCandlesBoth(
            h4Candles, effectiveMinRange, 0.3m * categoryFactor);

        // SK-VERIFY: [Abweichung #5] Wenn primary gegen Fahrplan zeigt → Fahrplan-aligned Machine versuchen
        // FromCandlesBoth wählt die "am weitesten fortgeschrittene" Machine als primary.
        // Wenn die Short-Machine weiter ist als Long, aber Fahrplan Long sagt → primary ist Short → Block.
        // Fix: Bevorzuge die Machine die zum Fahrplan passt (wenn sie mindestens SucheB erreicht hat).
        if (h4Machine != null && _lastFahrplanBias.HasValue && h4Machine.IsLong != _lastFahrplanBias.Value)
        {
            var alignedMachine = _lastFahrplanBias.Value ? h4LongMachine : h4ShortMachine;
            if (alignedMachine.State >= SmState.SucheB)
                h4Machine = alignedMachine;
        }

        if (h4Machine == null || h4Machine.State < SmState.SucheB)
            return NoSignal($"4H: Keine Sequenz (State={h4Machine?.State})");

        var h4Seq = h4Machine.ToSequence(h4Candles);
        if (h4Seq == null)
            return Blocked("4H: Sequenz nicht konstruierbar");

        // SK-VERIFY: [3b.5] Mehrere Sequenzen pro Symbol: Zusätzlich zur SM-Sequenz auch
        // alle weiteren gültigen Sequenzen per DetectAllSequences evaluieren.
        // Die beste (höchste RRR + aktuellste) wird bevorzugt.
        var allH4Sequences = SequenceDetector.DetectAllSequences(h4Candles, _h4SwingStrength, effectiveMinRange * 0.5m);
        if (allH4Sequences.Count > 1)
        {
            // Filtere auf gleiche Richtung wie SM-Sequenz + nicht invalidiert
            var sameDir = allH4Sequences.Where(s =>
                s.IsLong == h4Seq.IsLong
                && s.State is SequenceState.WaitingBreak or SequenceState.Active or SequenceState.CorrectionZone
                && s.IsTradeableType
                && s.PointB != null)
                .ToList();

            if (sameDir.Count > 0)
            {
                // Beste Sequenz: Höchste RRR bei gleichem State, neueste bei Gleichstand
                var bestAlt = sameDir
                    .OrderByDescending(s => s.CalculateRRR(currentPrice))
                    .ThenByDescending(s => s.Point0.CandleIndex)
                    .First();

                // Nur ersetzen wenn alternative Sequenz deutlich bessere RRR hat (>20% besser)
                var smRrr = h4Seq.CalculateRRR(currentPrice);
                var altRrr = bestAlt.CalculateRRR(currentPrice);
                if (altRrr > smRrr * 1.2m && altRrr > 2m)
                    h4Seq = bestAlt;
            }
        }

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

        // SK-VERIFY: [1.4a] 4H-Sequenz Mindestgröße: 0→A Strecke muss >= 2× ATR_4H sein
        // Verhindert Trading von 4H-Marktrauschen (zu kleine Sequenzen die keine echte Impulsbewegung sind)
        var h4Atr = IndicatorHelper.CalculateAtr(h4Candles, 14);
        var h4AtrValue = h4Atr.Count > 0 && h4Atr[^1].HasValue ? h4Atr[^1]!.Value : 0m;
        if (h4AtrValue > 0 && h4Range < h4AtrValue * 2m)
            return Blocked($"4H: Sequenz zu klein ({h4Range:F8} < 2× ATR {h4AtrValue * 2m:F8})");

        // SK-VERIFY: [3.13b] 4H-Overtracing-Toleranz: Flash-Crash-Dochte unter Point0 nicht sofort invalidieren
        if (h4AtrValue > 0)
            h4Machine.InvalidationTolerance = h4AtrValue * 0.3m;

        var inH4GklNow = h4Seq.IsInBuyZone(currentPrice) || h4Seq.IsInGklZone(currentPrice);

        // SK-VERIFY: [3.4] Zonen-Memory: GKL-Flag bleibt 10 × 4H-Kerzen (= 40h) aktiv.
        // Verhindert MTFA-Deadlock: 4H fällt ins GKL → 15m braucht Stunden zum Drehen →
        // 4H ist kurz raus → ohne Memory wäre der Confluence-Bonus sofort weg.
        var lastH4CandleTime = h4Candles[^1].CloseTime;
        if (inH4GklNow)
            _h4GklLastTouchTime = lastH4CandleTime;

        // 10 Kerzen × 4h = 40 Stunden Toleranz
        var gklMemoryDuration = TimeSpan.FromHours(GklMemoryKerzen * 4);
        var gklMemoryActive = _h4GklLastTouchTime != default
            && (lastH4CandleTime - _h4GklLastTouchTime) < gklMemoryDuration;
        var inH4Gkl = inH4GklNow || gklMemoryActive;

        // Anti-MTFA-Deadlock: 4H muss NICHT zwingend im GKL sein.
        // 4H gibt die RICHTUNG vor. Solange die 4H-Sequenz ≥ SucheB ist (Korrektur/Aktiviert)
        // und nicht abgearbeitet → Entry über 15m-Trigger erlaubt.
        // GKL ist ein CONFLUENCE-BONUS (+2), kein harter Filter.

        // Abgearbeitet: State Machine erkennt 161.8%, HasFullyCompleted prüft 200%
        // Beides → Richtungs-Sperre + GKL merken für Gegensequenz
        if (h4Seq.State == SequenceState.TargetReached || h4Seq.HasFullyCompleted(currentPrice))
        {
            _completedDirection = h4Seq.IsLong;
            _completedCooldown = 8; // SK-VERIFY: KILLER #7 — 8 statt 20 Evaluierungen (≈2h statt 5h)
            // SK-VERIFY: [2.3] GKL = Fibonacci der GESAMTSTRECKE Point0 → TargetC (Extension1618)
            // War falsch: h4Seq.Retracement559/667 basiert auf 0→A Strecke (viel zu klein)
            // Korrekt: 50%-66.7% Retracement der Gesamtbewegung von Sequenz-Start bis Ziel
            var targetC = h4Seq.Extension1618;
            var point0 = h4Seq.Point0.Price; // SK-System Punkt 0
            var gklRange = Math.Abs(targetC - point0);
            if (h4Seq.IsLong)
            {
                _completedGkl500 = targetC - gklRange * 0.500m;
                _completedGkl667 = targetC - gklRange * 0.667m;
            }
            else
            {
                _completedGkl500 = targetC + gklRange * 0.500m;
                _completedGkl667 = targetC + gklRange * 0.667m;
            }
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
                && _completedGkl500 > 0 && _completedGkl667 > 0
                && SequenceDetector.IsInGKL(counterSeq.Extension1618, _completedGkl500, _completedGkl667))
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
        // Dedizierte Gegenrichtung aus FromCandlesBoth (keine doppelte Berechnung).
        // Alte Variante (DetectAllSequences) blockierte 25% aller Evaluierungen weil der Preis
        // fast immer nahe irgendeinem abgearbeiteten Ziellevel liegt.
        var counterMachine = h4Seq.IsLong ? h4ShortMachine : h4LongMachine;
        if (counterMachine.State == SmState.Aktiviert)
        {
            var counterTarget = counterMachine.Extension1618;
            var sandwichTol = 0.02m; // 2% Toleranz (enger als vorher 5%)
            if (counterTarget > 0 && Math.Abs(currentPrice - counterTarget) / counterTarget < sandwichTol)
                return Blocked("KILL: Sandwich — Entry im Ziellevel aktiver 4H-Gegensequenz");
        }

        var tradeIsLong = h4Seq.IsLong;
        var h4Dir = tradeIsLong ? "Long" : "Short";

        // SK-VERIFY: [Abweichung #3] Fahrplan als Soft-Filter statt Hard-Block
        // SK-System: Marktstruktur-basiert (BLASH — Buy Low, Sell High). EMA-200 ist ein
        // Trendfolge-Indikator der konträr zum SK-Ansatz arbeitet. Gegen EMA = -1 Confluence.
        // Beispiel: BTC fällt von 70k auf 30k → SK sagt "Long suchen!", EMA sagt "Short-Bias".
        // Hard-Block würde ALLE Longs blocken wenn SK die besten Long-Setups sieht.
        _tradeAgainstEma = _lastFahrplanBias.HasValue && _lastFahrplanBias.Value != tradeIsLong;

        // SK-VERIFY: KILLER #5 — Bottom-Up: Confluence-Erhöhung statt harter Block
        // 3+ Verluste → Min-Confluence steigt, aber Trades mit extrem hoher Confluence bleiben möglich
        // (Harter Block erzeugte Deadlock: Kein Trade möglich → kein Gewinn → Block bleibt ewig)

        // SK-VERIFY: KILLER #1 — 4H-Dedup nur mit Time-Lock (nicht permanent)
        // Eine 4H-Sequenz kann Tage bis Wochen aktiv sein. Mehrere 15m-Entries darin sind normal.
        // Deduplizierung läuft über die 15m-Punkte (weiter unten), nicht über 4H.
        // Time-Lock: Nur während _signalCooldown aktiv (8 Evaluierungen ≈ 2h bei 15m-Scan)
        if (h4Machine.State == SmState.Aktiviert
            && _lastH4SeqPointA == h4Machine.PointA && _lastH4SeqLockedB == h4Machine.LockedB
            && _signalCooldown > 0)
            return Blocked($"4H: Identische Sequenz, Cooldown aktiv ({_signalCooldown} Evaluierungen)");

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
            // SK-VERIFY: [5.1] ADX auch auf 1H prüfen (nicht nur 4H) — richtungsloser 1H blockiert Entry
            var h1Adx = IndicatorHelper.CalculateAdx(h1Candles!, 14);
            if (h1Adx.Count > 0 && h1Adx[^1].HasValue && h1Adx[^1]!.Value < adxThreshold)
                return Blocked($"1H: Seitwärtsmarkt (1H ADX={h1Adx[^1]!.Value:F0} < {adxThreshold})");

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

        // SK-VERIFY: KILLER #8 — Beide 15m-Richtungen prüfen, nicht nur die primäre.
        // FromCandles() gibt nur die "beste" Sequenz zurück — die kann gegen 4H zeigen.
        // FromCandlesBoth() gibt Long+Short, wir wählen die passende Richtung.
        var (_, m15LongMachine, m15ShortMachine) = SequenceStateMachine.FromCandlesBoth(
            m15Candles, effectiveMinRange * 0.3m, 0.15m * categoryFactor);
        var m15Machine = tradeIsLong ? m15LongMachine : m15ShortMachine;
        if (m15Machine == null)
            return Blocked($"15m: Keine {(tradeIsLong ? "Long" : "Short")}-Micro-Sequenz in 4H-Richtung");

        var microSeq = m15Machine.ToSequence(m15Candles);
        if (microSeq == null)
            return Blocked("15m: Micro-Sequenz nicht konstruierbar");

        // 15m muss AKTIVIERT sein (State Machine: A wurde durchbrochen, B eingefroren)
        if (m15Machine.State != SmState.Aktiviert)
            return Blocked($"15m: Micro-Sequenz nicht aktiviert (State={m15Machine.State})");

        // SK-VERIFY: [3b.1] 38.2%-Mindestaktivierung: Preis muss mindestens 38.2% der BC-Extension erreicht haben
        // Ohne Mindest-Fortschritt nach Aktivierung ist die Bewegung zu schwach für eine gültige Sequenz
        var m15BCRange = Math.Abs(m15Machine.PointA - m15Machine.LockedB);
        if (m15BCRange > 0)
        {
            var m15MinExt = tradeIsLong
                ? m15Machine.LockedB + m15BCRange * 0.382m
                : m15Machine.LockedB - m15BCRange * 0.382m;
            var hasMinExt = tradeIsLong ? currentPrice >= m15MinExt : currentPrice <= m15MinExt;
            if (!hasMinExt)
                return Blocked($"15m: 38.2% Extension nicht erreicht (aktuell {currentPrice:F8}, Minimum {m15MinExt:F8})");
        }

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

        // SK-VERIFY: [3.13] Overtracing-Toleranz für 15m-SM setzen (0.3× ATR)
        if (m15AtrValue > 0)
            m15Machine.InvalidationTolerance = m15AtrValue * 0.3m;

        // SK-VERIFY: KILLER #3 — Über 100% ist OK, solange unter 138.2% (BC-Zone noch valid)
        // Die 100er muss ERREICHT worden sein (Has100ExtensionReached weiter unten),
        // aber der Preis darf zwischen 100% und 138.2% sein — das BC kommt als Korrektur danach.
        // Altes Verhalten (Block bei >100%) ließ ein Entry-Fenster von effektiv 0 Sekunden.
        var impulseRange15m = Math.Abs(m15Machine.PointA - m15Machine.Point0);
        var m15Ext1382 = tradeIsLong
            ? m15Machine.LockedB + impulseRange15m * 1.382m
            : m15Machine.LockedB - impulseRange15m * 1.382m;
        var m15TooFar = tradeIsLong
            ? currentPrice > m15Ext1382
            : currentPrice < m15Ext1382;
        if (m15TooFar)
            return Blocked($"KILL: 15m über 138.2% Extension — zu weit für BC-Entry");

        // SK-VERIFY: [2.4] 100er Extension als Minimum-Gate
        // BC-Zone ist erst valid wenn die 100% Extension seit Aktivierung mindestens einmal erreicht wurde.
        // Ohne 100er: Die Sequenz ist noch nicht bestätigt (zu schwache Bewegung nach Aktivierung).
        if (!m15Machine.Has100ExtensionReached)
            return Blocked("15m: 100% Extension noch nicht erreicht — BC-Zone noch nicht valid");

        // SK-VERIFY: KILLER #2 — Impulsive Reaktion: Erweitertes Fenster (8 statt 5 Kerzen)
        // Nach Stabilisierungsphase startet der Ausbruch oft langsam — 2 Trend, 1 Gegen, dann hoch.
        // Methode 3 (Netto-Bewegung > 1× ATR) fängt solche langsamen Ausbrüche ab.
        if (m15Candles.Count > 8 && m15AtrValue > 0)
        {
            var activationImpulsive = false;
            var lastCandles = m15Candles.Skip(Math.Max(0, m15Candles.Count - 8)).ToList();
            // Methode 1: ≥3 Kerzen in Trendrichtung
            var trendCandles = lastCandles.Count(c =>
                tradeIsLong ? c.Close > c.Open : c.Close < c.Open);
            if (trendCandles >= 3)
                activationImpulsive = true;
            // Methode 2: Eine Kerze mit Body > 1.5× ATR
            if (!activationImpulsive)
            {
                var hasStrongBody = lastCandles.Any(c =>
                    Math.Abs(c.Close - c.Open) > m15AtrValue * 1.5m);
                if (hasStrongBody)
                    activationImpulsive = true;
            }
            // Methode 3: Netto-Bewegung > 1× ATR (fängt langsame Ausbrüche nach Stabilisierung ab)
            if (!activationImpulsive)
            {
                var nettoMove = Math.Abs(lastCandles[^1].Close - lastCandles[0].Open);
                if (nettoMove > m15AtrValue * 1.0m)
                    activationImpulsive = true;
            }
            if (!activationImpulsive)
                return Blocked("15m: Aktivierung nicht impulsiv (weder ≥3 Trend-Kerzen noch Body > 1.5×ATR noch Netto > ATR)");
        }

        _curM15 = $"GRÜN (Aktiviert, 0={m15Machine.Point0:G6} A={m15Machine.PointA:G6})";
        // → GRÜN: Alle 3 Ampeln aktiv!

        // ═══════════════════════════════════════════════════════════
        // DEDUPLIZIERUNG + WHIPSAW-SCHUTZ
        // ═══════════════════════════════════════════════════════════

        if (_lastSignalSymbol == context.Symbol &&
            _lastSignalPoint0 == microSeq.Point0.Price &&
            _lastSignalPointA == microSeq.PointA.Price &&
            _lastSignalPointB == (microSeq.PointB?.Price ?? 0))
            return Blocked("Sequenz bereits signalisiert (Deduplizierung)");

        if (_signalCooldown > 0 && _lastSignalSymbol == context.Symbol && _lastSignalIsLong != tradeIsLong)
            return Blocked($"Whipsaw-Schutz: {_signalCooldown} Kerzen Cooldown");

        // ═══════════════════════════════════════════════════════════
        // SL / TP / SIGNAL — SK ATR-basierter SL
        // ═══════════════════════════════════════════════════════════

        // SK-VERIFY: [4.2] Primärer SL = Point0 - (1.5 × ATR_15m) (bullisch)
        // ATR-basiert als primäre Formel (nicht Fib). Buffer-Faktor 1.5 ist konfigurierbar.
        // SL darf NIEMALS exakt auf Punkt 0 oder B liegen (Liquidity-Grab-Schutz).
        const decimal slAtrMultiplier = 1.5m; // SK-Regel: 1.5× ATR_15m Buffer
        decimal sl;
        var micro0 = m15Machine.Point0;

        if (m15AtrValue > 0)
        {
            // Primär: Point0 ± (1.5 × ATR_15m)
            var atrBuffer = m15AtrValue * slAtrMultiplier;
            // Min-Buffer: max(ATR-Buffer, 0.15% vom Preis) gegen Liquidity Grabs
            var minBuffer = currentPrice * _slBufferPercent / 100m;
            var effectiveBuffer = Math.Max(atrBuffer, minBuffer);
            sl = tradeIsLong ? micro0 - effectiveBuffer : micro0 + effectiveBuffer;
        }
        else
        {
            // Fallback ohne ATR: Point0 ± 0.15%
            var fallbackBuffer = micro0 * _slBufferPercent / 100m;
            sl = tradeIsLong ? micro0 - fallbackBuffer : micro0 + fallbackBuffer;
        }

        // Seitenprüfung: SL muss auf der richtigen Seite des Preises liegen
        var slOnWrongSide = (tradeIsLong && sl >= currentPrice) || (!tradeIsLong && sl <= currentPrice);
        if (slOnWrongSide)
        {
            // ATR-basierter Notfall-SL (3× ATR_15m vom aktuellen Preis)
            var atrFallback = m15AtrValue * 3m;
            if (atrFallback > 0)
                sl = tradeIsLong ? currentPrice - atrFallback : currentPrice + atrFallback;
            else
                return Blocked("SL auf falscher Seite und kein ATR-Fallback verfügbar");
        }

        // SL-Grenzen prüfen: Zu weit (>2.5%) oder zu eng (<0.1%)
        var slDistance = Math.Abs(currentPrice - sl);
        var maxSlPercent = 0.025m;
        var minSlPercent = 0.002m;
        var slPercent = currentPrice > 0 ? slDistance / currentPrice : 0m;
        if (slPercent > maxSlPercent || slPercent < minSlPercent * 0.5m)
        {
            // SL unrealistisch → 3× ATR_15m als Fallback
            var atrSl = m15AtrValue * 3m;
            if (atrSl > 0)
            {
                sl = tradeIsLong ? currentPrice - atrSl : currentPrice + atrSl;
            }
            else
            {
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

        // SK-VERIFY: KILLER #4 — RRR wird gestaffelt nach Confluence-Score geprüft (weiter unten)
        // Hoher Score = höhere Trefferquote → niedrigeres RRR akzeptabel

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

        // SK-VERIFY: [2.2] BC-Zone Confluence — Preis in dynamischer BC-Korrekturzone?
        // GetDynamicBcZone() = 50-66.7% Retracement von LockedB bis CurrentHigh/Low
        if (m15Machine.State == SmState.Aktiviert)
        {
            var (bcTop, bcBottom) = m15Machine.GetDynamicBcZone();
            if (bcTop > 0 && bcBottom > 0)
            {
                var bcMin = Math.Min(bcTop, bcBottom);
                var bcMax = Math.Max(bcTop, bcBottom);
                if (currentPrice >= bcMin && currentPrice <= bcMax)
                { score += 1; reasons.Add("BC-Zone"); }
            }
        }

        // SK-VERIFY: [3.12] Stabilisierung (Prestabilisation) — Qualitätsmerkmal
        // DetectEntryConfirmation prüft: StableInZone, HammerOrPin, Engulfing
        if (h4Candles.Count > 10 && inH4Gkl)
        {
            var confirmation = SequenceDetector.DetectEntryConfirmation(h4Candles, h4Seq, currentPrice);
            if (confirmation != CandleConfirmation.None)
            {
                score += 2;
                reasons.Add($"Stab:{confirmation}");
            }
        }

        // SK-VERIFY: [3.6] Sekundäre Sequenz-Bestätigung (1H innerhalb 4H-Zone)
        // Wenn 1H eine eigene Sequenz bildet die im 4H-Korrekturlevel liegt und 100er erreicht → starkes Signal
        if (h1Available && inH4Gkl)
        {
            var h1Seq = SequenceDetector.DetectSequence(h1Candles!, _h1SwingStrength, effectiveMinRange * 0.3m, false);
            if (h1Seq != null && h1Seq.IsLong == tradeIsLong
                && h1Seq.State is SequenceState.Active or SequenceState.TargetReached)
            {
                score += 2;
                reasons.Add("Sekundär-1H");
            }
        }

        // SK-VERIFY: [3.10] Wendebereiche-Validierung
        // Sequenzen an GKL oder in Überlappung mit SMC-Zonen = valider Wendebereich
        // Ohne validen Wendebereich: -1 Score (nicht blockieren, aber abwerten)
        var isAtValidWendebereich = inH4Gkl || hasSmcConfluence;
        if (!isAtValidWendebereich)
        {
            // Prüfe ob Preis nahe ATH/ATL (±3%) — auch ein valider Wendebereich
            if (h4Candles.Count > 50)
            {
                var recent50 = h4Candles.Skip(h4Candles.Count - 50);
                var ath = recent50.Max(c => c.High);
                var atl = recent50.Min(c => c.Low);
                var nearAth = ath > 0 && Math.Abs(currentPrice - ath) / ath < 0.03m;
                var nearAtl = atl > 0 && Math.Abs(currentPrice - atl) / atl < 0.03m;
                isAtValidWendebereich = nearAth || nearAtl;
            }
            if (!isAtValidWendebereich)
            {
                score -= 1;
                reasons.Add("KeinWende");
            }
        }

        // SK-VERIFY: [Abweichung #3] EMA-200 Fahrplan als Confluence-Malus (nicht Block)
        if (_tradeAgainstEma)
        {
            score -= 1;
            reasons.Add("gegenEMA");
        }

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

        // SK-VERIFY: KILLER #4 — Gestaffeltes Min-RRR basierend auf Confluence-Score
        // Hoher Score = höhere Trefferquote → niedrigeres RRR akzeptabel
        var minRrr = score switch
        {
            >= 8 => 1.5m,  // Sehr hohe Confluence → 1.5:1 reicht
            >= 6 => 2.0m,  // Gute Confluence → 2:1
            >= 4 => 2.5m,  // Mittlere Confluence → 2.5:1
            _    => 3.0m   // Niedrige Confluence → 3:1 bleibt
        };
        if (rrr < minRrr)
            return Blocked($"RRR zu klein ({rrr:F1}:1 < {minRrr:F1}:1 bei Score={score})");

        // SK-VERIFY: KILLER #5 — Bottom-Up: Confluence-Anforderung erhöhen statt harter Block
        var adjustedMinConfluence = _minConfluence;
        if (_consecutiveFailsInDirection >= 3)
            adjustedMinConfluence = _minConfluence + _consecutiveFailsInDirection - 1;

        if (score < adjustedMinConfluence)
            return Blocked($"Confluence {score}/{adjustedMinConfluence} ({string.Join(", ", reasons)})");

        // ═══════════════════════════════════════════════════════════
        // SIGNAL ERSTELLEN
        // ═══════════════════════════════════════════════════════════

        _lastSignalSymbol = context.Symbol;
        _lastSignalPoint0 = microSeq.Point0.Price;
        _lastSignalPointA = microSeq.PointA.Price;
        _lastSignalPointB = microSeq.PointB?.Price ?? 0;
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
        // SK-VERIFY: Abweichung #4 — Naming jetzt konsistent: Sequence.Point0=SM.Point0=SK:0, Sequence.PointA=SM.PointA=SK:A, Sequence.PointB=SK:B
        var reasonText = $"SK Trinity {(tradeIsLong ? "Long" : "Short")} | " +
                         $"4H:0={h4Machine.Point0:G6} A={h4Machine.PointA:G6} B={h4Machine.LockedB:G6} | " +
                         $"15m:0={m15Machine.Point0:G6} A={m15Machine.PointA:G6} B={m15Machine.LockedB:G6} | " +
                         $"Score={score} RRR={rrr:F1}:1 | {string.Join(", ", reasons)}";

        return new SignalResult(side, confidence, retestEntry, sl, tp1, reasonText,
            TakeProfit2: tp2, ConfluenceScore: score, PreferLimitOrder: useLimit,
            DisableSmartBreakeven: DisableSmartBreakeven, Tp1CloseRatioOverride: 0.5m);
    }

    // ═══════════════════════════════════════════════════════════════
    // IStrategy Methoden
    // ═══════════════════════════════════════════════════════════════

    public void WarmUp(IReadOnlyList<Candle> history) { }

    public void Reset()
    {
        _lastSignalPoint0 = 0;
        _lastSignalPointA = 0;
        _lastSignalPointB = 0;
        _lastSignalSymbol = "";
        _lastSignalIsLong = false;
        _signalCooldown = 0;
        _completedDirection = null;
        _completedCooldown = 0;
        _completedGkl500 = 0;
        _completedGkl667 = 0;
        _crashCooldown = 0;
        _lastH4Close = 0;
        _h4GklLastTouchTime = default;
        _lastH4SeqPointA = 0;
        _lastH4SeqLockedB = 0;
        _lastFahrplanBias = null;
        _lastEma200 = 0;
        _tradeAgainstEma = false;
        _consecutiveFailsInDirection = 0;
    }

    /// <summary>
    /// SK-VERIFY: [3.11] Bottom-Up Feedback — Trade-Ergebnis an die Strategie zurückmelden.
    /// Aufgerufen von TradingServiceBase nach Trade-Close.
    /// Bei Verlust: Richtungs-Counter erhöhen. Bei Gewinn: Counter zurücksetzen.
    /// </summary>
    public void RecordTradeOutcome(bool isWin, bool wasLong)
    {
        if (isWin)
        {
            _consecutiveFailsInDirection = 0; // Gewinn bestätigt die Richtung
        }
        else
        {
            // Verlust in der aktuellen Fahrplan-Richtung → Counter erhöhen
            if (_lastFahrplanBias.HasValue && _lastFahrplanBias.Value == wasLong)
                _consecutiveFailsInDirection++;
            else
                _consecutiveFailsInDirection = 0; // Verlust gegen Fahrplan zählt nicht
        }
    }

    /// <summary>Erstellt einen frischen Klon für ein neues Symbol.
    /// WICHTIG: ApplyPreset() muss VOR dem ersten Clone aufgerufen worden sein,
    /// da DisableSmartBreakeven dort gesetzt wird.</summary>
    public IStrategy Clone() => new SequenzKonzeptStrategy
    {
        _h4SwingStrength = _h4SwingStrength,
        _h1SwingStrength = _h1SwingStrength,
        _m15SwingStrength = _m15SwingStrength,
        _minRangePercent = _minRangePercent,
        _minConfluence = _minConfluence,
        _slBufferPercent = _slBufferPercent,
        _activePreset = _activePreset,
        DisableSmartBreakeven = DisableSmartBreakeven,
        // Fahrplan-State wird nicht geklont — jeder Symbol-Klon berechnet seinen eigenen Bias
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
