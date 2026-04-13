using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.Risk;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Stefan-Kassing (SK) Trading-System — STRIKT BUCH-KONFORM.
///
/// Quelle: Tradebook SK-System (Sascha Wenzel, Stefan Kassing).
///
/// Chart-Hierarchie (Buch S.15):
///   Übergeordnet: Weekly → Daily → H4 → H1 (Marktanalyse)
///   Untergeordnet: M30 (Primär-Entry-Chart)
///
/// Sequenz (Buch S.15-17):
///   Punkt 0 → Impuls → Punkt A → Korrektur (50-66.7% Retracement) → Punkt B → Aktivierung (Break über A) → Punkt C (161.8-200% Extension)
///
/// Stop-Loss (Buch S.13): Feste Pip-Werte pro Asset-Klasse
///   Hauptwährungen+Metalle: -20 Pips, Indices+Öl: -40 Pips, Krypto: -100 Pips
///
/// Take-Profit-Prioritäten (Buch S.35, Workflow-Anmerkungen 3):
///   1) CRV min 1:1 (besser 2:1)
///   2) 100% Bullen-/Bärenkorrekturlevel
///   3) 100% Extension
///   4) 161.8-200% Extension (Hauptzielbereich)
///   5) Dailyrange
/// </summary>
public class SequenzKonzeptStrategy : IStrategy
{
    public string Name => "SK-System";
    public string Description => "Buch-konformes Stefan-Kassing System (W1/D1/H4/H1/M30, Pip-SL, 3-4 Bestätigungen)";

    // ═══════════════════════════════════════════════════════════════
    // Parameter (Buch-konform, EINHEITLICH für alle Assets)
    // ═══════════════════════════════════════════════════════════════

    // Swing-Stärken (Fraktal-Erkennung)
    private int _h4SwingStrength = 5;
    private int _h1SwingStrength = 3;
    private int _m30SwingStrength = 3;

    // Confluence-Minimum (Buch: 3-4 Bestätigungen)
    private int _minConfluence = 3;

    /// <summary>Letzter SK-Status für UI.</summary>
    public string LastStatus { get; private set; } = "";
    /// <summary>Ampel-Status W1/D1/H4/H1/M30 für UI.</summary>
    public (string W1, string D1, string H4, string H1, string M30) AmpelStatus { get; private set; }
        = ("—", "—", "—", "—", "—");

    // ═══════════════════════════════════════════════════════════════
    // Laufzeit-State (pro Symbol-Klon)
    // ═══════════════════════════════════════════════════════════════

    // Deduplizierung + Whipsaw-Schutz
    private decimal _lastSignalPoint0;
    private decimal _lastSignalPointA;
    private decimal _lastSignalPointB;
    private string _lastSignalSymbol = "";
    private bool _lastSignalIsLong;
    private int _signalCooldown;

    // Richtungs-Sperre nach Abarbeitung (Buch: Sequenz abgearbeitet → Gegensequenz suchen)
    private bool? _completedDirection;
    private int _completedCooldown;
    private decimal _completedGkl500;
    private decimal _completedGkl667;

    // 4H-Sequenz Deduplizierung (Time-Lock)
    private decimal _lastH4SeqPointA;
    private decimal _lastH4SeqLockedB;

    // Multi-Entry Staffelung (Buch: 50er voll + 66.7er halb)
    private decimal _firstEntrySeqA;     // PointA der Sequenz des ersten Entries (Markierung)
    private decimal _firstEntrySeqB;     // PointB der Sequenz des ersten Entries
    private bool _firstEntryIsLong;
    private bool _firstEntryTriggered;

    // Ampel-Tracking (wird progressiv aktualisiert während Evaluate)
    private string _curW1 = "—", _curD1 = "—", _curH4 = "—", _curH1 = "—", _curM30 = "—";

    // ═══════════════════════════════════════════════════════════════
    // IStrategy-Interface
    // ═══════════════════════════════════════════════════════════════

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("H4SwingStrength", "H4 Navigator Swing-Stärke", "int", _h4SwingStrength, 3, 10, 1),
        new("H1SwingStrength", "H1 Filter Swing-Stärke", "int", _h1SwingStrength, 2, 7, 1),
        new("M30SwingStrength", "M30 Trigger Swing-Stärke", "int", _m30SwingStrength, 2, 7, 1),
        new("MinConfluence", "Min. Confluence-Bestätigungen", "int", _minConfluence, 2, 6, 1),
    };

    public bool DisableSmartBreakeven { get; private set; }

    public void ApplyPreset(TradingModePreset mode)
    {
        // Buch-konform: EINHEITLICHE Parameter für alle Assets und Modi
        _h4SwingStrength = 5;
        _h1SwingStrength = 3;
        _m30SwingStrength = 3;
        _minConfluence = 3;
        DisableSmartBreakeven = true; // SK: Kein Smart-BE (B-C Korrekturen stoppen sonst aus)
    }

    public void WarmUp(IReadOnlyList<Candle> history) { }

    /// <summary>Wird nach jedem Trade-Outcome aufgerufen. SK nutzt keinen Bottom-Up-Feedback-Mechanismus (nicht im Buch).</summary>
    public void RecordTradeOutcome(bool isWin, bool wasLong) { }

    public void Reset()
    {
        _lastSignalPoint0 = _lastSignalPointA = _lastSignalPointB = 0;
        _lastSignalSymbol = "";
        _lastSignalIsLong = false;
        _signalCooldown = 0;
        _completedDirection = null;
        _completedCooldown = 0;
        _completedGkl500 = _completedGkl667 = 0;
        _lastH4SeqPointA = _lastH4SeqLockedB = 0;
        _firstEntrySeqA = _firstEntrySeqB = 0;
        _firstEntryIsLong = false;
        _firstEntryTriggered = false;
    }

    public IStrategy Clone() => new SequenzKonzeptStrategy
    {
        _h4SwingStrength = _h4SwingStrength,
        _h1SwingStrength = _h1SwingStrength,
        _m30SwingStrength = _m30SwingStrength,
        _minConfluence = _minConfluence,
        DisableSmartBreakeven = DisableSmartBreakeven,
    };

    // ═══════════════════════════════════════════════════════════════
    // Evaluate — Haupt-Trading-Logik (Buch-Workflow)
    // ═══════════════════════════════════════════════════════════════

    public SignalResult Evaluate(MarketContext context)
    {
        _curW1 = _curD1 = _curH4 = _curH1 = _curM30 = "—";

        var h4Candles = context.Candles;
        var h1Candles = context.HigherTimeframeCandles;
        var m30Candles = context.EntryTimeframeCandles;
        var dailyCandles = context.DailyCandles;
        var weeklyCandles = context.WeeklyCandles;

        if (h4Candles.Count < _h4SwingStrength * 2 + 20)
            return Blocked("Zu wenig H4-Daten");

        var currentPrice = context.CurrentTicker.LastPrice;

        // Cooldowns dekrementieren
        if (_signalCooldown > 0) _signalCooldown--;
        if (_completedCooldown > 0) _completedCooldown--;
        else _completedDirection = null;

        // ───────────────────────────────────────────────────────────
        // AMPEL W1/D1 — Fahrplan (Buch S.15: Übergeordnete Marktanalyse)
        // ───────────────────────────────────────────────────────────
        var fahrplanBias = DetermineFahrplanBias(weeklyCandles, dailyCandles, currentPrice);

        // ───────────────────────────────────────────────────────────
        // AMPEL H4 — Navigator-Sequenz finden (Buch S.15)
        // ───────────────────────────────────────────────────────────

        // SK-FIX: ATR-basierte Schwellenwerte statt feste Prozente.
        // Feste % funktionieren nur für Krypto. Forex hat ~0.3% ATR, Krypto ~2-4%, Indices ~0.7%.
        // ATR passt sich automatisch an JEDES Asset und jede Marktphase an.
        //
        // Multiplikatoren (empirisch, skaliert mit Timeframe):
        //   minImpulse  = 1.0× ATR → Impuls muss mindestens eine volle ATR-Einheit betragen
        //   correction  = 1.5× ATR → A wird erst nach 1.5× ATR Rücksetzer gelockt (>1 Kerze typisch)
        //
        // Beispiele mit diesen Multiplikatoren:
        //   BTC H4 (ATR~2000$, Preis 80k): impulse=2.5%, correction=3.75% → sichtbare Moves
        //   EUR/USD H4 (ATR~0.003, Preis 1.08): impulse=0.28%, correction=0.42% → passt zu Forex
        //   Gold H4 (ATR~18$, Preis 2300$): impulse=0.78%, correction=1.17% → passt zu Rohstoffen
        //   DAX H4 (ATR~120, Preis 18500): impulse=0.65%, correction=0.97% → passt zu Indices
        var h4Atr = IndicatorHelper.CalculateAtr(h4Candles, 14);
        var h4AtrValue = h4Atr.Count > 0 && h4Atr[^1].HasValue ? h4Atr[^1]!.Value : 0m;
        var h4AtrPercent = currentPrice > 0 && h4AtrValue > 0
            ? h4AtrValue / currentPrice * 100m
            : 1.5m; // Fallback: 1.5% wenn ATR nicht verfügbar

        var h4MinImpulse = h4AtrPercent * 1.0m;     // 1.0× ATR für Impuls-Trigger
        var h4CorrThreshold = h4AtrPercent * 1.5m;   // 1.5× ATR für Korrektur-Bestätigung

        // SK-FIX: B-Retracement auf 38.2-78.6% verschärft (Default war 23.6-88.6%)
        // Tradebook S.16: Korrekturlevel bei 0.50/0.559/0.618/0.667 — das ist die IDEAL-Zone.
        // 38.2% = absolute Untergrenze (flachere Korrektur = kaum ein echtes B)
        // 78.6% = absolute Obergrenze (tiefer = fast Invalidierung, kein sauberes Setup)
        var (navMachine, navLongMachine, navShortMachine) =
            SequenceStateMachine.FromCandlesBoth(h4Candles, h4MinImpulse, h4CorrThreshold, 0.382m, 0.786m, minPoint0Candles: 5);

        // Fahrplan-Alignment: Wenn primary gegen Fahrplan → aligned Machine versuchen
        if (navMachine != null && fahrplanBias.HasValue && navMachine.IsLong != fahrplanBias.Value)
        {
            var aligned = fahrplanBias.Value ? navLongMachine : navShortMachine;
            if (aligned.State >= SmState.SucheB)
                navMachine = aligned;
        }

        if (navMachine == null || navMachine.State < SmState.SucheB)
            return Blocked($"Keine H4-Sequenz (State={navMachine?.State})");

        var navSeq = navMachine.ToSequence(h4Candles);
        if (navSeq == null)
            return Blocked("H4-Sequenz nicht konstruierbar");

        // Overtracing-Toleranz (bleibt ATR-basiert wie vorher)
        if (h4AtrValue > 0)
            navMachine.InvalidationTolerance = h4AtrValue * 0.3m;

        _curH4 = $"GELB {(navSeq.IsLong ? "Long" : "Short")} ({navMachine.State})";

        // Abgearbeitet (Buch: nach 200% keine Entries mehr, nur Gegensequenz)
        if (navSeq.State == SequenceState.TargetReached || navSeq.HasFullyCompleted(currentPrice))
        {
            _completedDirection = navSeq.IsLong;
            _completedCooldown = 8;
            var targetC = navSeq.Extension1618;
            var point0 = navSeq.Point0.Price;
            var gklRange = Math.Abs(targetC - point0);
            if (navSeq.IsLong)
            {
                _completedGkl500 = targetC - gklRange * 0.500m;
                _completedGkl667 = targetC - gklRange * 0.667m;
            }
            else
            {
                _completedGkl500 = targetC + gklRange * 0.500m;
                _completedGkl667 = targetC + gklRange * 0.667m;
            }
            return Blocked("H4-Sequenz abgearbeitet (200%)");
        }

        // Richtungs-Sperre nach Abarbeitung — Gegensequenz ins GKL suchen (Buch-Regel)
        if (_completedDirection.HasValue && _completedDirection.Value == navSeq.IsLong && _completedCooldown > 0)
        {
            // SK-FIX: Gegensequenz per StateMachine statt SequenceDetector (einheitliches System)
            var counterDir = !_completedDirection.Value;
            var counterMachineForGkl = counterDir ? navLongMachine : navShortMachine;
            var counterSeq = counterMachineForGkl.ToSequence(h4Candles);
            if (counterSeq != null && counterSeq.IsLong == counterDir
                && counterSeq.State is SequenceState.WaitingBreak or SequenceState.Active
                && _completedGkl500 > 0 && _completedGkl667 > 0
                && SequenceDetector.IsInGKL(counterSeq.Extension1618, _completedGkl500, _completedGkl667))
            {
                navSeq = counterSeq;
            }
            else
            {
                return Blocked($"{(navSeq.IsLong ? "Long" : "Short")}-Richtung gesperrt ({_completedCooldown} Kerzen)");
            }
        }

        // Sequenztyp-Filter: Nur Typ 1 (Normal) handelbar (Buch: Überextendiert/Langgezogen = nur Analyse)
        if (!navSeq.IsTradeableType)
            return Blocked($"Sequenz-Typ {navSeq.Type} nicht handelbar");

        // Sandwich-Kill: Entry im Ziellevel aktiver Gegensequenz
        var counterMachine = navSeq.IsLong ? navShortMachine : navLongMachine;
        if (counterMachine.State == SmState.Aktiviert)
        {
            var counterTarget = counterMachine.Extension1618;
            if (counterTarget > 0 && Math.Abs(currentPrice - counterTarget) / counterTarget < 0.02m)
            {
                var closeNear = Math.Abs(h4Candles[^1].Close - counterTarget) / counterTarget < 0.02m;
                if (closeNear)
                    return Blocked("KILL: Sandwich — Close am Ziellevel aktiver Gegensequenz");
            }
        }

        var tradeIsLong = navSeq.IsLong;

        // H4-Dedup (Time-Lock: nur während Cooldown blockieren)
        if (navMachine.State == SmState.Aktiviert
            && _lastH4SeqPointA == navMachine.PointA
            && _lastH4SeqLockedB == navMachine.LockedB
            && _signalCooldown > 0)
            return Blocked($"Identische H4-Sequenz, Cooldown ({_signalCooldown})");

        // ───────────────────────────────────────────────────────────
        // AMPEL H1 — Filter (Buch: Korrektur verliert Schwung?)
        // ───────────────────────────────────────────────────────────
        var h1Available = h1Candles is { Count: > 20 };
        var correctionEnding = false;

        if (h1Available && h1Candles is not null)
        {
            var h1Swings = SequenceDetector.FindSwingPoints(h1Candles, _h1SwingStrength);
            (correctionEnding, _) = SequenceDetector.DetectCorrectionEnd(h1Candles, h1Swings, tradeIsLong);

            if (!correctionEnding)
            {
                // SK-FIX: Einheitliches System — H1-Gegensequenz-Check per StateMachine statt SequenceDetector
                // SequenceDetector (fraktalbasiert) und StateMachine finden unterschiedliche Sequenzen
                // auf den gleichen Daten → inkonsistente Analyse. Jetzt alles StateMachine.
                // SK-FIX: ATR-basierte Schwellenwerte für H1
                var h1Atr = IndicatorHelper.CalculateAtr(h1Candles, 14);
                var h1AtrValue = h1Atr.Count > 0 && h1Atr[^1].HasValue ? h1Atr[^1]!.Value : 0m;
                var h1AtrPercent = currentPrice > 0 && h1AtrValue > 0
                    ? h1AtrValue / currentPrice * 100m
                    : 0.8m; // Fallback
                var (_, h1LongMachine, h1ShortMachine) =
                    SequenceStateMachine.FromCandlesBoth(h1Candles, h1AtrPercent * 1.0m, h1AtrPercent * 1.5m, 0.382m, 0.786m, minPoint0Candles: 3);
                var h1CounterMachine = tradeIsLong ? h1ShortMachine : h1LongMachine;
                if (h1CounterMachine.State == SmState.Aktiviert)
                    return Blocked("H1: Aktive Gegensequenz — Korrektur läuft noch");

                // ChoCH gegen Richtung (bleibt Swing-basiert — das ist ein reines Muster, kein Sequenz-Check)
                var h1ChoCH = SequenceDetector.DetectChoCH(h1Swings);
                if (h1ChoCH != null && h1ChoCH.FromBullishToBearish == tradeIsLong)
                    return Blocked("H1: ChoCH gegen Trade-Richtung");
            }
        }

        _curH1 = !h1Available ? "ORANGE (keine H1-Daten)"
               : correctionEnding ? "ORANGE (Korrektur-Ende)"
               : "ORANGE (durchgelassen)";

        // ───────────────────────────────────────────────────────────
        // AMPEL M30 — Trigger (Buch S.15: Primär-Entry-Chart)
        // ───────────────────────────────────────────────────────────
        if (m30Candles is not { Count: > 20 })
            return Blocked("Keine M30-Daten");

        // SK-FIX: ATR-basierte Schwellenwerte für M30 (gleiche Multiplikatoren wie H4)
        var m30Atr = IndicatorHelper.CalculateAtr(m30Candles, 14);
        var m30AtrValue = m30Atr.Count > 0 && m30Atr[^1].HasValue ? m30Atr[^1]!.Value : 0m;
        var m30AtrPercent = currentPrice > 0 && m30AtrValue > 0
            ? m30AtrValue / currentPrice * 100m
            : 0.5m; // Fallback: 0.5% wenn ATR nicht verfügbar

        var m30MinImpulse = m30AtrPercent * 1.0m;     // 1.0× ATR
        var m30CorrThreshold = m30AtrPercent * 1.5m;   // 1.5× ATR

        // SK-FIX: M30 auch mit SK-konformen B-Retracement-Grenzen
        var (_, m30LongMachine, m30ShortMachine) =
            SequenceStateMachine.FromCandlesBoth(m30Candles, m30MinImpulse, m30CorrThreshold, 0.382m, 0.786m, minPoint0Candles: 2);
        var m30Machine = tradeIsLong ? m30LongMachine : m30ShortMachine;

        if (m30Machine == null)
            return Blocked($"Keine M30-{(tradeIsLong ? "Long" : "Short")}-Sequenz");

        var m30Seq = m30Machine.ToSequence(m30Candles);
        if (m30Seq == null)
            return Blocked("M30-Sequenz nicht konstruierbar");

        if (m30Machine.State != SmState.Aktiviert)
            return Blocked($"M30 nicht aktiviert (State={m30Machine.State})");

        // Buch S.16: 38.2% Mindest-Aktivierung
        var m30BCRange = Math.Abs(m30Machine.PointA - m30Machine.LockedB);
        if (m30BCRange > 0)
        {
            var minExt = tradeIsLong
                ? m30Machine.LockedB + m30BCRange * 0.382m
                : m30Machine.LockedB - m30BCRange * 0.382m;
            var hasMinExt = tradeIsLong ? currentPrice >= minExt : currentPrice <= minExt;
            if (!hasMinExt)
                return Blocked($"38.2% Extension nicht erreicht");
        }

        // Overtracing-Toleranz (m30AtrValue bereits oben berechnet)
        if (m30AtrValue > 0)
            m30Machine.InvalidationTolerance = m30AtrValue * 0.3m;

        // 138.2% Over-Extension = zu spät (Buch: 100er ist Richtungsweiser)
        var m30ImpulseRange = Math.Abs(m30Machine.PointA - m30Machine.Point0);
        var m30Ext1382 = tradeIsLong
            ? m30Machine.LockedB + m30ImpulseRange * 1.382m
            : m30Machine.LockedB - m30ImpulseRange * 1.382m;
        var tooFar = tradeIsLong ? currentPrice > m30Ext1382 : currentPrice < m30Ext1382;
        if (tooFar)
            return Blocked("KILL: Über 138.2% Extension");

        // ChoCH auf M30 gegen Richtung
        var m30Swings = SequenceDetector.FindSwingPoints(m30Candles, _m30SwingStrength);
        var m30ChoCH = SequenceDetector.DetectChoCH(m30Swings);
        if (m30ChoCH != null && m30ChoCH.FromBullishToBearish == tradeIsLong)
            return Blocked("M30: ChoCH gegen Trade-Richtung");

        _curM30 = $"GRÜN (0={m30Machine.Point0:G6} A={m30Machine.PointA:G6})";

        // ───────────────────────────────────────────────────────────
        // Deduplizierung + Whipsaw
        // ───────────────────────────────────────────────────────────
        if (_lastSignalSymbol == context.Symbol
            && _lastSignalPoint0 == m30Seq.Point0.Price
            && _lastSignalPointA == m30Seq.PointA.Price
            && _lastSignalPointB == (m30Seq.PointB?.Price ?? 0))
            return Blocked("Sequenz bereits signalisiert");

        if (_signalCooldown > 0 && _lastSignalSymbol == context.Symbol && _lastSignalIsLong != tradeIsLong)
            return Blocked($"Whipsaw-Schutz: {_signalCooldown} Kerzen Cooldown");

        // ───────────────────────────────────────────────────────────
        // Confluence-Score (Buch: 3-4 Bestätigungen)
        // ───────────────────────────────────────────────────────────
        var score = 0;
        var reasons = new List<string>();

        // Bestätigung 1: H4-Sequenz aktiviert
        score++; reasons.Add("H4-Seq");

        // Bestätigung 2: B im Golden Pocket (50-66.7%)
        var bInGoldenPocket = navSeq.IsInBuyZone(currentPrice) || navSeq.IsInGklZone(currentPrice);
        if (bInGoldenPocket) { score++; reasons.Add("GoldenPocket"); }

        // Bestätigung 3: M30-Trigger aktiviert
        score++; reasons.Add("M30-Aktiviert");

        // Bestätigung 4 (optional): 100er Extension übersteigt (Buch: Wahrscheinlich Ziellevel)
        if (m30Machine.Has100ExtensionReached) { score++; reasons.Add("100erExt"); }

        // Bestätigung (optional): Fahrplan-Alignment
        if (fahrplanBias.HasValue && fahrplanBias.Value == tradeIsLong)
        { score++; reasons.Add("Fahrplan"); }

        if (score < _minConfluence)
            return Blocked($"Confluence {score}/{_minConfluence} ({string.Join(", ", reasons)})");

        // ───────────────────────────────────────────────────────────
        // BCKL Re-Entry (Buch Workflow 6.6: "Korrektur der BC-Bewegung = IMMER Reentry")
        // ───────────────────────────────────────────────────────────
        (decimal Bckl500, decimal Bckl559, decimal Bckl618, decimal Bckl667)? bcklData = null;
        var isBcklReEntry = false;
        if (m30Machine.Has100ExtensionReached)
        {
            bcklData = SequenceDetector.CalculateBCKL(m30Seq);
            if (bcklData.HasValue
                && SequenceDetector.IsInBCKL(currentPrice, bcklData.Value.Bckl500, bcklData.Value.Bckl667))
            {
                isBcklReEntry = true;
                reasons.Add("BCKL-ReEntry");
            }
        }

        // ───────────────────────────────────────────────────────────
        // Multi-Entry Staffelung (Buch Cheat 50: 50er voll + 66.7er halb)
        // ───────────────────────────────────────────────────────────
        var sameSequence = _firstEntryTriggered
            && _firstEntrySeqA == navMachine.PointA
            && _firstEntrySeqB == navMachine.LockedB
            && _firstEntryIsLong == tradeIsLong;

        var isInDeepGkl = IsInDeepGklZone(navSeq, currentPrice);  // 61.8-66.7%
        var isAdditionalEntry = sameSequence && isInDeepGkl;

        // Kein dritter Entry — nach 66.7er-Nachkauf keine weiteren
        if (sameSequence && !isInDeepGkl)
            return Blocked("Multi-Entry: Bereits gesetzt, Preis nicht im Deep-GKL");

        // ───────────────────────────────────────────────────────────
        // ENTRY + STOP-LOSS
        //   BCKL-Re-Entry (Workflow 6.6): Entry am BCKL-Level, SL unter Mikrosequenz-B
        //   Sonst: Entry am Fib-Level der NavSeq, SL am 78.6er + Pip-Cap + Point0-Grenze
        // ───────────────────────────────────────────────────────────
        decimal entry, sl;
        if (isBcklReEntry && bcklData.HasValue)
        {
            // BCKL-Entry: oberstes BCKL-Level (50% der C→Ext100-Strecke)
            entry = bcklData.Value.Bckl500;
            // BCKL-SL: unter der Mikrosequenz-B (= B der M30-Sequenz).
            // Fällt Preis unter M30-B, ist die BC-Welle invalid → kein Setup mehr.
            var bcklSlBase = m30Seq.PointB?.Price ?? navSeq.Point0.Price;
            sl = PipStopLossCalculator.CalculateBookStopLoss(
                context.Symbol, context.Category, entry, tradeIsLong,
                bcklSlBase, bcklSlBase, isSingleTrade: !isAdditionalEntry);
        }
        else
        {
            // Normaler Entry: bestes Fib-Level der NavSeq (Buch S.16, Cheat 50)
            entry = ComputeFibEntry(navSeq, currentPrice, isAdditionalEntry);
            // SL: 78.6er + Pip-Cap + Point0-Grenze (Buch Cheat 36, S.13, Workflow 6.9)
            // isSingleTrade: 1 Trade (Cheat 37) = 15 Pips, Multiple (Cheat 49) = 20 Pips
            sl = PipStopLossCalculator.CalculateBookStopLoss(
                context.Symbol, context.Category, entry, tradeIsLong,
                navSeq.Retracement786, navSeq.Point0.Price, isSingleTrade: !isAdditionalEntry);
        }

        var slOnWrongSide = (tradeIsLong && sl >= entry) || (!tradeIsLong && sl <= entry);
        if (slOnWrongSide)
            return Blocked("SL auf falscher Seite");

        var slDistance = Math.Abs(entry - sl);

        // ───────────────────────────────────────────────────────────
        // TAKE-PROFIT (Buch S.13 + Workflow 4.5, S.35)
        // Tradebook: TP1 = 161.8% Extension (erste Zielzone, Teilgewinn)
        //            TP2 = 200% Extension + 20 Pips Buffer (konservatives Endziel)
        // Workflow 4.5: "20 Pips über dem 200er Extensionslevel"
        // Buch S.13: "Ziellevel 161.8% — 200%" → Staffelung TP1/TP2
        // ───────────────────────────────────────────────────────────
        var tp20PipBuffer = PipStopLossCalculator.Get20PipsBuffer(context.Symbol, context.Category, entry);
        var tp1 = navSeq.Extension1618; // Buch: Erstes Ziellevel bei 161.8% (Partial Close)
        var tp2 = tradeIsLong ? navSeq.Extension200 + tp20PipBuffer : navSeq.Extension200 - tp20PipBuffer;

        // RRR-Check (Buch S.13: min 1:1)
        var tpDist = Math.Abs(tp2 - entry);
        var rrr = slDistance > 0 ? tpDist / slDistance : 0m;
        if (rrr < 1.0m)
            return Blocked($"RRR zu klein ({rrr:F1}:1 < 1:1)");

        // ───────────────────────────────────────────────────────────
        // Signal erstellen
        // ───────────────────────────────────────────────────────────
        _lastSignalSymbol = context.Symbol;
        _lastSignalPoint0 = m30Seq.Point0.Price;
        _lastSignalPointA = m30Seq.PointA.Price;
        _lastSignalPointB = m30Seq.PointB?.Price ?? 0;
        _lastSignalIsLong = tradeIsLong;
        _signalCooldown = 8;
        _lastH4SeqPointA = navMachine.PointA;
        _lastH4SeqLockedB = navMachine.LockedB;

        if (!isAdditionalEntry)
        {
            _firstEntrySeqA = navMachine.PointA;
            _firstEntrySeqB = navMachine.LockedB;
            _firstEntryIsLong = tradeIsLong;
            _firstEntryTriggered = true;
        }

        AmpelStatus = (_curW1, _curD1, _curH4, _curH1, _curM30);
        LastStatus = $"SIGNAL! {(tradeIsLong ? "Long" : "Short")} Score={score} RRR={rrr:F1}:1"
                   + (isAdditionalEntry ? " [+1/2]" : "")
                   + (isBcklReEntry ? " [BCKL]" : "");

        var reasonText = $"SK {(tradeIsLong ? "Long" : "Short")} | "
                       + $"H4:0={navMachine.Point0:G6} A={navMachine.PointA:G6} B={navMachine.LockedB:G6} | "
                       + $"M30:0={m30Machine.Point0:G6} A={m30Machine.PointA:G6} B={m30Machine.LockedB:G6} | "
                       + $"Score={score} RRR={rrr:F1}:1 | {string.Join(", ", reasons)}";

        var side = tradeIsLong ? Signal.Long : Signal.Short;
        var confidence = Math.Clamp((decimal)score / 6m, 0m, 1m);

        return new SignalResult(
            side, confidence,
            EntryPrice: entry,  // Buch Cheat 50: Limit an Fib-Level der H4-NavSeq (50/55.9/61.8/66.7)
            StopLoss: sl,
            TakeProfit: tp1,
            Reason: reasonText,
            TakeProfit2: tp2,
            ConfluenceScore: score,
            PreferLimitOrder: true,
            DisableSmartBreakeven: DisableSmartBreakeven,
            IsAdditionalEntry: isAdditionalEntry);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper: Fahrplan (Weekly/Daily)
    // ═══════════════════════════════════════════════════════════════

    private bool? DetermineFahrplanBias(
        IReadOnlyList<Candle>? weekly,
        IReadOnlyList<Candle>? daily,
        decimal currentPrice)
    {
        // Weekly zuerst (Buch: W1 → D1 → H4 → H1)
        if (weekly is { Count: > 20 })
        {
            // SK-FIX: ATR-basiert für W1
            var w1Atr = IndicatorHelper.CalculateAtr(weekly, 14);
            var w1AtrValue = w1Atr.Count > 0 && w1Atr[^1].HasValue ? w1Atr[^1]!.Value : 0m;
            var w1AtrPct = currentPrice > 0 && w1AtrValue > 0
                ? w1AtrValue / currentPrice * 100m : 5.0m;
            var weeklySM = SequenceStateMachine.FromCandles(weekly, w1AtrPct * 1.0m, w1AtrPct * 1.5m, 0.382m, 0.786m, minPoint0Candles: 3);
            if (weeklySM != null)
            {
                foreach (var gkl in weeklySM.CompletedGkls)
                {
                    var min = Math.Min(gkl.Gkl500, gkl.Gkl667);
                    var max = Math.Max(gkl.Gkl500, gkl.Gkl667);
                    if (currentPrice >= min && currentPrice <= max)
                    {
                        _curW1 = $"W1-GKL ({(gkl.IsLong ? "Long" : "Short")})";
                        return gkl.IsLong;
                    }
                }
            }
        }

        // Daily-GKL oder BLASH (unter 30% = Long, über 70% = Short)
        if (daily is { Count: > 20 })
        {
            // SK-FIX: ATR-basiert für D1
            var d1Atr = IndicatorHelper.CalculateAtr(daily, 14);
            var d1AtrValue = d1Atr.Count > 0 && d1Atr[^1].HasValue ? d1Atr[^1]!.Value : 0m;
            var d1AtrPct = currentPrice > 0 && d1AtrValue > 0
                ? d1AtrValue / currentPrice * 100m : 2.0m;
            var dailySM = SequenceStateMachine.FromCandles(daily, d1AtrPct * 1.0m, d1AtrPct * 1.5m, 0.382m, 0.786m, minPoint0Candles: 5);
            if (dailySM != null)
            {
                foreach (var gkl in dailySM.CompletedGkls)
                {
                    var min = Math.Min(gkl.Gkl500, gkl.Gkl667);
                    var max = Math.Max(gkl.Gkl500, gkl.Gkl667);
                    if (currentPrice >= min && currentPrice <= max)
                    {
                        _curD1 = $"D1-GKL ({(gkl.IsLong ? "Long" : "Short")})";
                        return gkl.IsLong;
                    }
                }
            }

            // BLASH-Position in Daily-Range
            var ath = daily.Max(c => c.High);
            var atl = daily.Min(c => c.Low);
            var range = ath - atl;
            if (range > 0)
            {
                var pos = (currentPrice - atl) / range;
                if (pos < 0.30m) { _curD1 = "D1-BLASH Long"; return true; }
                if (pos > 0.70m) { _curD1 = "D1-BLASH Short"; return false; }
                _curD1 = "D1-Mid";
            }
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper: Fibonacci-Entry (Buch S.16, Cheat 50)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Wählt das Fibonacci-Level für den Entry (Limit-Order) nach SK-Buch.
    /// Primary: bestes verfügbares Level (nächstes im Korrektur-Richtung).
    /// Additional: 66.7% (Deep-GKL).
    /// </summary>
    private static decimal ComputeFibEntry(Sequence navSeq, decimal currentPrice, bool isAdditional)
    {
        // Additional (Deep-GKL): Entry bei 66.7% (zweiter Limit bei tieferer Korrektur)
        if (isAdditional)
            return navSeq.Retracement667;

        // Primary: "bestes verfügbares Fib-Level" (Buch Cheat 35)
        // = höchstes (Long) / niedrigstes (Short) Level das Preis noch nicht weit unter-/überschritten hat
        if (navSeq.IsLong)
        {
            // Long: Preis korrigiert RUNTER. Limit oberhalb Preis = wartet auf Retest/weitere Korrektur.
            if (currentPrice > navSeq.Retracement500) return navSeq.Retracement500;
            if (currentPrice > navSeq.Retracement559) return navSeq.Retracement559;
            if (currentPrice > navSeq.Retracement618) return navSeq.Retracement618;
            return navSeq.Retracement667;
        }
        else
        {
            // Short: Preis korrigiert HOCH. Limit unterhalb Preis = wartet auf Retest/weitere Korrektur.
            if (currentPrice < navSeq.Retracement500) return navSeq.Retracement500;
            if (currentPrice < navSeq.Retracement559) return navSeq.Retracement559;
            if (currentPrice < navSeq.Retracement618) return navSeq.Retracement618;
            return navSeq.Retracement667;
        }
    }

    private static bool IsInDeepGklZone(Sequence seq, decimal price)
    {
        // Deep-GKL = 61.8-66.7% (untere Hälfte des Golden Pockets)
        var r618 = seq.Retracement618;
        var r667 = seq.Retracement667;
        var min = Math.Min(r618, r667);
        var max = Math.Max(r618, r667);
        return price >= min && price <= max;
    }

    // ═══════════════════════════════════════════════════════════════
    // Block-Helpers
    // ═══════════════════════════════════════════════════════════════

    private SignalResult Blocked(string reason)
    {
        AmpelStatus = (_curW1, _curD1, _curH4, _curH1, _curM30);
        LastStatus = $"[W1:{_curW1}|D1:{_curD1}|H4:{_curH4}|H1:{_curH1}|M30:{_curM30}] {reason}";
        return new SignalResult(Signal.None, 0m, null, null, null, LastStatus);
    }
}
