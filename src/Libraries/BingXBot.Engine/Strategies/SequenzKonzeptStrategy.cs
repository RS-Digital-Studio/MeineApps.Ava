using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.News;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies.Confluence;
using BingXBot.Engine.Strategies.Pipeline;
using BingXBot.Engine.Strategies.Pipeline.Steps;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Stefan-Kassing (SK) Trading-System — Multi-TF Standalone (15.04.2026, M5-Navigator auf M15 umgestellt 19.04.2026).
///
/// Quelle: Tradebook SK-System (Sascha Wenzel, Stefan Kassing).
/// Erweiterung: Jede Navigator-TF (D1/H4/H1/M15) ist eigenständig — eigene Sequenz,
/// eigenes Fib-Entry-Level, eigener SL. Der Scanner evaluiert pro Symbol alle aktiven TFs.
///
/// Chart-Hierarchie pro Navigator-TF:
///   W1/D1 = Fahrplan (übergeordnete Marktanalyse)
///   NavigatorTF = Sequenz-Erkennung + Entry-Trigger
///   FilterTF = nächst tiefere TF (D1→H4, H4→H1, H1→M15, M15→M5, optional)
///
/// Sequenz (Buch S.15-17):
///   Punkt 0 → Impuls → Punkt A → Korrektur (50-66.7%) → Punkt B → Aktivierung → Punkt C (161.8-200%)
///
/// Stop-Loss (Buch S.13): Feste Pip-Werte pro Asset-Klasse. M15 moderat gedämpft (0.75) über <see cref="RiskSettings.PipScalingByTf"/>.
/// </summary>
public class SequenzKonzeptStrategy : IStrategy
{
    public string Name => "SK-System";
    public string Description => "Buch-konformes Stefan-Kassing System (Multi-TF Standalone: D1/H4/H1/M15, Pip-SL, 3-5 Bestätigungen)";

    // ═══════════════════════════════════════════════════════════════
    // Parameter — EINHEITLICH, TF-agnostisch
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Swing-Stärke für Fraktal-Erkennung auf der Navigator-TF.</summary>
    private int _swingStrength = 5;

    /// <summary>
    /// Mindest-Confluence-Fallback. BUCH-ONLY: Wird aktuell nicht als Hard-Threshold genutzt
    /// (Score ist nur Info/Log/Confidence — Buch kennt keinen quantitativen Score-Gate).
    /// </summary>
    private int _minConfluence = 3;

    /// <summary>Letzter SK-Status für UI.</summary>
    public string LastStatus { get; private set; } = "";

    /// <summary>Ampel-Status pro TF — wird pro Evaluate-Aufruf aktualisiert.</summary>
    public Dictionary<TimeFrame, string> AmpelStatus { get; private set; } = new();

    // ═══════════════════════════════════════════════════════════════
    // Laufzeit-State (pro Symbol-Klon + Navigator-TF)
    // ═══════════════════════════════════════════════════════════════

    // BUCH-ONLY (21.04.2026): Richtungs-Sperre entfernt — Buch kennt keine zeitliche
    // Sperre nach Abarbeitung. ProcessAbgearbeitet in der StateMachine resettet direkt auf Suche0.
    // Cooldown- und Dedup-Felder (`_signalCooldown`, `_lastSignal*`, `_lastNavSeq*`) wurden
    // am 24.04.2026 endgültig entfernt — User-Entscheidung "cooldown kommt nicht mehr".

    // Multi-Entry-Staffelung pro Navigator-Sequenz. Buch-konform: Single = nur Primary @ 50%,
    // Dual = Primary @ 50% + Additional @ 66.7% (beide Box-Enden).
    private decimal _entrySeqA;
    private decimal _entrySeqB;
    private bool _entrySeqIsLong;
    private bool _triggeredPrimary;      // 50% (Single+Dual)
    private bool _triggeredAdditional;   // 66.7% (Dual)

    // Task 2.1 — BCKL-IMMER-Trigger: Kerzen-Index des letzten BCKL-Re-Entry (Race-Condition-Schutz).
    // Verhindert Doppel-Trigger in derselben Kerze; Cooldown von 2 Kerzen zwischen BCKL-Entries.
    private int _lastBcklEntryCandleIndex = -1;
    private const int BcklReEntryCooldownCandles = 2;

    // ═══════════════════════════════════════════════════════════════
    // IStrategy-Interface
    // ═══════════════════════════════════════════════════════════════

    public IReadOnlyList<StrategyParameter> Parameters => new List<StrategyParameter>
    {
        new("SwingStrength", "Navigator Swing-Stärke", "int", _swingStrength, 2, 10, 1),
        new("MinConfluence", "Min. Confluence-Bestätigungen (Fallback)", "int", _minConfluence, 2, 6, 1),
    };

    public bool DisableSmartBreakeven { get; private set; } = true;

    public void WarmUp(IReadOnlyList<Candle> history) { }

    /// <summary>SK nutzt keinen Bottom-Up-Feedback-Mechanismus (nicht im Buch).</summary>
    public void RecordTradeOutcome(bool isWin, bool wasLong) { }

    public void Reset()
    {
        // BUCH-ONLY: Keine Cooldown-/Dedup-Felder mehr (endgültig am 24.04.2026 entfernt —
        // "cooldown kommt nicht mehr"). ProcessAbgearbeitet in der StateMachine übernimmt
        // den Sequenz-Reset auf Suche0.
        _entrySeqA = _entrySeqB = 0;
        _entrySeqIsLong = false;
        _triggeredPrimary = false;
        _triggeredAdditional = false;
        _lastBcklEntryCandleIndex = -1;
        AmpelStatus = new();
    }

    public IStrategy Clone() => new SequenzKonzeptStrategy
    {
        _swingStrength = _swingStrength,
        _minConfluence = _minConfluence,
        DisableSmartBreakeven = DisableSmartBreakeven,
    };

    // ═══════════════════════════════════════════════════════════════
    // Konfigurations-Enum (Task 3.1 — später Task 3.5 Quad und 4.4 Hex)
    // ═══════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════
    // Evaluate — TF-agnostisch (Navigator-TF aus context.NavigatorTimeframe)
    // ═══════════════════════════════════════════════════════════════

    public SignalResult Evaluate(MarketContext context)
    {
        var navTf = context.NavigatorTimeframe;
        var navCandles = context.Candles;
        var filterCandles = context.FilterTimeframeCandles;
        var dailyCandles = context.DailyCandles;
        var weeklyCandles = context.WeeklyCandles;
        var scannerSettings = context.ScannerSettings;

        // Ampel zurücksetzen und Standard-Einträge anlegen (Duplikate vermeiden wenn navTf=D1 oder =W1)
        AmpelStatus = new Dictionary<TimeFrame, string>();
        AmpelStatus[TimeFrame.W1] = "—";
        AmpelStatus[TimeFrame.D1] = "—";
        AmpelStatus[navTf] = "—";
        var filterTf = GetFilterTimeframe(navTf);
        if (filterTf.HasValue)
            AmpelStatus[filterTf.Value] = "—";

        if (navCandles.Count < _swingStrength * 2 + 20)
            return Blocked(navTf, $"Zu wenig {navTf}-Daten");

        var currentPrice = context.CurrentTicker.LastPrice;

        // BUCH-ONLY: Keine Cooldowns mehr zu dekrementieren.

        // ───────────────────────────────────────────────────────────
        // AMPEL W1/D1 — Fahrplan (Buch S.15: Übergeordnete Marktanalyse)
        // ───────────────────────────────────────────────────────────
        var fahrplanBias = DetermineFahrplanBias(weeklyCandles, dailyCandles, currentPrice, scannerSettings);

        // ───────────────────────────────────────────────────────────
        // NAVIGATOR-SEQUENZ (Buch S.15) — auf der Navigator-TF
        // ───────────────────────────────────────────────────────────
        var navAtr = IndicatorHelper.CalculateAtr(navCandles, 14);
        var navAtrValue = navAtr.Count > 0 && navAtr[^1].HasValue ? navAtr[^1]!.Value : 0m;
        var navAtrPercent = currentPrice > 0 && navAtrValue > 0
            ? navAtrValue / currentPrice * 100m
            : GetDefaultAtrPercent(navTf);

        var navImpulseMul = GetAtrMultiplier(scannerSettings, navTf, impulse: true, fallback: 1.0m);
        var navCorrMul = GetAtrMultiplier(scannerSettings, navTf, impulse: false, fallback: 1.5m);
        var navMinImpulse = navAtrPercent * navImpulseMul;
        var navCorrThreshold = navAtrPercent * navCorrMul;

        var navMinPoint0Candles = GetMinPoint0Candles(scannerSettings, navTf);

        // Strukturpunkte-Doku §2: |PointA - Point0| ≥ ATR_14 × ImpulseAtrMultiplier (Default 3.0).
        // Absoluter Preis-Betrag, der direkt an die State-Machine geht und in TryActivate die Sequenz verwirft,
        // wenn der Impuls zu klein ist (= Seitwärts-Rauschen statt echter Smart-Money-Bewegung).
        var impulseAtrMul = scannerSettings?.ImpulseAtrMultiplier ?? 3.0m;
        var navMinImpulseDistance = impulseAtrMul > 0 ? navAtrValue * impulseAtrMul : 0m;

        // Strukturpunkte-Doku §3: BOS-Filter IMMER aktiv ("Ohne Strukturbruch keine SK-Messung").
        // FromCandlesBoth pflegt den Anker selbst via RefreshBosAnchor pro Iteration — ein pauschal aus der
        // Gesamthistorie vorausgewählter Anker wäre oft das falsche Pivot (liegt ggf. nach Point0). Initial-Anker = 0.
        var bosRequireCloseBreak = scannerSettings?.RequireBosCloseBreak ?? true;
        var bosAnchorSwingStrength = Math.Max(2, scannerSettings?.BosAnchorSwingStrength ?? 5);

        var enableBiasFlip = scannerSettings?.EnableBiasFlip ?? true;
        var (navMachine, navLongMachine, navShortMachine) =
            SequenceStateMachine.FromCandlesBoth(navCandles, navMinImpulse, navCorrThreshold, 0.382m, 0.786m,
                minPoint0Candles: navMinPoint0Candles, enableBiasFlip: enableBiasFlip,
                minImpulseDistance: navMinImpulseDistance,
                bosRequireCloseBreak: bosRequireCloseBreak,
                bosAnchorSwingStrength: bosAnchorSwingStrength);

        // Fahrplan-Alignment (Buch: MTA — Multi-Timeframe-Alignment): Wenn primary gegen Fahrplan
        // läuft UND eine aligned Sequenz in SucheB/Aktiviert existiert, bevorzuge die aligned.
        // BUCH-ONLY: KEIN Hard-Block gegen Fahrplan — das Buch sagt nur "keine LTF-Trades in
        // HTF-Zielzone" (BlockLtfEntryWhenHtfInTargetZone), nicht "keine Trades gegen Fahrplan".
        if (navMachine != null && fahrplanBias.HasValue && navMachine.IsLong != fahrplanBias.Value)
        {
            var aligned = fahrplanBias.Value ? navLongMachine : navShortMachine;
            if (aligned.State >= SmState.SucheB)
            {
                navMachine = aligned;
            }
        }

        if (navMachine == null || navMachine.State < SmState.SucheB)
            return Blocked(navTf, $"Keine {navTf}-Sequenz (State={navMachine?.State})");

        var navSeq = navMachine.ToSequence(navCandles);
        if (navSeq == null)
            return Blocked(navTf, $"{navTf}-Sequenz nicht konstruierbar");

        AmpelStatus[navTf] = $"GELB {(navSeq.IsLong ? "Long" : "Short")} ({navMachine.State})";

        // BUCH-ONLY: Abgearbeitet-Handling erfolgt in der StateMachine (ProcessAbgearbeitet → Reset auf Suche0).
        // Die Strategy blockt NICHT mehr mit "abgearbeitet" — die neue Sequenzsuche ab Punkt 0 ist bereits
        // aktiv, und jede neue valide Gegensequenz wird normal durch das State-Machine-Feld navMachine getragen.

        // ───────────────────────────────────────────────────────────
        // Task 4.10 — Counter-Trend-Scalp (opt-in, hochriskant)
        // Buch: "Erreicht der Markt das Ziellevel C, prallt er dort fast immer kurzfristig ab."
        // Wird NUR aktiviert wenn ScannerSettings.EnableCounterTrendScalp=true.
        // Erzeugt Signal in Gegenrichtung zur Haupt-Sequenz mit 50% Position.
        // ───────────────────────────────────────────────────────────
        if (scannerSettings?.EnableCounterTrendScalp == true
            && navSeq.State == SequenceState.Active
            && filterCandles is { Count: >= 25 })
        {
            var ctBufferPips = GetSlBufferPips(context, GetFilterTimeframe(navTf) ?? TimeFrame.M15);
            var ctPipValue = context.Category switch
            {
                Core.Enums.MarketCategory.Forex => 0.0001m,
                Core.Enums.MarketCategory.Commodity => 0.1m,
                _ => currentPrice * 0.0001m,
            };
            var ctHit = CounterTrendScalper.TryDetect(navSeq, currentPrice, filterCandles, ctBufferPips * ctPipValue);
            if (ctHit != null)
            {
                // Counter-Trend-Signal: entgegengesetzte Richtung zur Haupt-Sequenz, 50% Position
                var ctSide = navSeq.IsLong ? Signal.Short : Signal.Long;
                AmpelStatus[navTf] = $"ROT {navTf} — CounterTrend-Scalp gegen {(navSeq.IsLong ? "Long" : "Short")}-Haupt";
                return new SignalResult(
                    ctSide, 0.6m,
                    EntryPrice: ctHit.EntryPrice,
                    StopLoss: ctHit.StopLoss,
                    TakeProfit: ctHit.TakeProfit,
                    Reason: ctHit.Reason,
                    PreferLimitOrder: false,
                    DisableSmartBreakeven: true,
                    IsCounterTrendScalp: true,
                    PositionScaleOverride: ctHit.PositionScaleOverride);
            }
        }

        // BUCH-ONLY: Kein Sequenztyp-Filter (Buch kennt nur eine Art Sequenz).
        // Kein Sandwich-Kill, kein BC-Overlap-Block, kein Navigator-Dedup/Time-Lock.
        var tradeIsLong = navSeq.IsLong;

        // ───────────────────────────────────────────────────────────
        // FILTER-TF (nächst tiefere TF) — optional, TF-gestaffelter ChoCH-Filter
        // ───────────────────────────────────────────────────────────
        var filterAvailable = filterCandles is { Count: > 20 } && filterTf.HasValue;
        var correctionEnding = false;

        if (filterAvailable && filterCandles is not null && filterTf.HasValue)
        {
            var filtTf = filterTf.Value;
            // Strukturpunkte-Doku §5B + §1: Filter-TF nutzt adaptive Pivot-Länge wenn aktiviert, sonst Doku-Default 3.
            var filterSwingStrength = ResolveSwingStrength(scannerSettings, filterCandles, currentPrice, filtTf, fallback: 3);
            var filterPivotPair = ResolvePivotBars(scannerSettings, filterSwingStrength);
            var filterSwings = filterPivotPair.HasValue
                ? SequenceDetector.FindSwingPoints(filterCandles, filterPivotPair.Value.LeftBars, filterPivotPair.Value.RightBars)
                : SequenceDetector.FindSwingPoints(filterCandles, filterSwingStrength);
            (correctionEnding, _) = SequenceDetector.DetectCorrectionEnd(filterCandles, filterSwings, tradeIsLong);

            // BUCH-ONLY (21.04.2026): Filter-TF prüft ausschliesslich "Korrektur-Ende" (correctionEnding).
            // Die vormals vorhandenen Zusatzfilter (aktive Gegensequenz auf Filter-TF, ChoCH gegen Richtung)
            // sind nicht in den Spec-Docs und fliegen raus. Conservative-Mode des Buchs verlangt nur ein
            // bullisches Reversal-Pattern in der B-Box — das macht correctionEnding.

            AmpelStatus[filtTf] = correctionEnding ? $"{filtTf}: ORANGE (Korrektur-Ende)"
                                                   : $"{filtTf}: ORANGE (durchgelassen)";
        }
        else if (filterTf.HasValue)
        {
            AmpelStatus[filterTf.Value] = $"{filterTf}: ORANGE (keine Daten)";
        }

        // ───────────────────────────────────────────────────────────
        // ENTRY-VALIDIERUNG auf der Navigator-TF
        // (ersetzt den alten M30-Block: Navigator ist jetzt zugleich Trigger-Chart)
        // ───────────────────────────────────────────────────────────

        if (navMachine.State != SmState.Aktiviert)
            return Blocked(navTf, $"{navTf} nicht aktiviert (State={navMachine.State})");

        // ───────────────────────────────────────────────────────────
        // Strukturpunkte-Doku §5A: Volumen-Anomaly-Filter (Hard-Block)
        // Doku-Zitat: "IF Breakout_Candle_Volume < SMA_Volume * 1.5 THEN Ignore_Sequence.
        // Der Impuls MUSS überdurchschnittliches Volumen aufweisen, sonst ist es ein Fakeout."
        // Opt-in via ScannerSettings.RequireBosVolumeBreakout.
        // User-Entscheidung 22.04.2026: Default = false. §5A ist in der Doku als
        // "Profi-Erweiterung für 100% Perfektion" gekennzeichnet — für BingX-Perps zu scharf.
        // Volumen bleibt als Bonus-Confluence aktiv (SequenceDetector.DetectEntryConfirmation +1 Score),
        // blockt aber keine Sequenz mehr.
        // ───────────────────────────────────────────────────────────
        if (scannerSettings?.RequireBosVolumeBreakout == true
            && navMachine.ActivationCandleIndex >= 0)
        {
            var volMul = scannerSettings.BosVolumeMultiplier > 0 ? scannerSettings.BosVolumeMultiplier : 1.5m;
            if (!HasBosVolumeBreakout(navCandles, navMachine.ActivationCandleIndex, volMul))
                return Blocked(navTf, $"Volumen-Filter: BOS-Kerze unter {volMul:F1}× SMA20 (Fakeout-Schutz)");
        }

        // ───────────────────────────────────────────────────────────
        // Spec §7 (B18) — MTA-Guard: HTF darf nicht in eigener Zielzone (EXT_1618-EXT_2000) stehen.
        // Doku-Zitat: "Führe Long-Trades auf dem Lower Timeframe (LTF) nur aus, wenn sich der Preis
        // nicht in der Zielzone (EXT_1618-EXT_2000) des Higher Timeframes (HTF) befindet."
        // Rationale: Wenn HTF gerade seinen TP erreicht, ist eine Trendwende wahrscheinlich — LTF-Entries
        // in dieselbe Richtung riskieren Top-/Bottom-Picking.
        // Opt-in via ScannerSettings.BlockLtfEntryWhenHtfInTargetZone (Default: false).
        // ───────────────────────────────────────────────────────────
        if (scannerSettings?.BlockLtfEntryWhenHtfInTargetZone == true
            && IsHigherTfInTargetZone(navTf, weeklyCandles, dailyCandles, scannerSettings, currentPrice, tradeIsLong))
        {
            return Blocked(navTf, "HTF in Zielzone (EXT_1618-EXT_2000) — LTF-Entry gegen drohende HTF-Wende blockiert (Spec §7)");
        }

        // BUCH-ONLY: Keine 38.2% Mindest-Aktivierung, keine 138.2% Over-Extension, kein ChoCH-Filter.
        // Die Spec-Docs (sk_handbuch, sk_techspec, strukturpunkte) kennen diese Regeln nicht.
        // Einzige Validität-Checks sind Pivot + ATR*3 + BOS + Volumen — alle in der StateMachine.

        AmpelStatus[navTf] = $"GRÜN {navTf} (0={navMachine.Point0:G6} A={navMachine.PointA:G6})";

        // BUCH-ONLY: Keine Deduplizierung, kein Whipsaw-Schutz. Die Spec-Docs kennen diese Filter nicht.
        // Jedes Signal das die State-Machine liefert ist per Definition ein neues Setup
        // (Sequence.PointA/PointB wechseln bei jeder neuen 0-A-B-Sequenz).

        // ───────────────────────────────────────────────────────────
        // BCKL Re-Entry (Buch Workflow 6.6) — Task 2.1: IMMER-Trigger nach Aktivierung.
        // Buch-Zitat: "Korrektur der BC-Bewegung stellt IMMER einen Reentry dar (bei aktivierter Sequenz)."
        // VOR dem Confluence-Score berechnet, damit BCKL-Bonus in Scorer.Add() einfließt.
        // ───────────────────────────────────────────────────────────
        (decimal Bckl500, decimal Bckl559, decimal Bckl618, decimal Bckl667)? bcklData = null;
        var isBcklReEntry = false;
        if (navMachine.State == SmState.Aktiviert
            && (navCandles.Count - 1 - _lastBcklEntryCandleIndex) >= BcklReEntryCooldownCandles)
        {
            // Bevorzugt dynamische BC-Zone (Trailing High/Low nach Aktivierung).
            var (bcTop, bcBottom) = navMachine.GetDynamicBcZone();
            if (bcTop > 0 && bcBottom > 0)
            {
                var inDynamicZone = currentPrice >= Math.Min(bcTop, bcBottom)
                                   && currentPrice <= Math.Max(bcTop, bcBottom);
                if (inDynamicZone)
                {
                    var range = Math.Abs(bcTop - bcBottom);
                    bcklData = tradeIsLong
                        ? (bcTop, bcTop - range * 0.118m, bcTop - range * 0.236m, bcBottom)
                        : (bcBottom, bcBottom + range * 0.118m, bcBottom + range * 0.236m, bcTop);
                    isBcklReEntry = true;
                }
            }

            // Fallback: Klassischer BCKL (C→Ext100-Retracement).
            if (!isBcklReEntry)
            {
                bcklData = SequenceDetector.CalculateBCKL(navSeq);
                if (bcklData.HasValue
                    && SequenceDetector.IsInBCKL(currentPrice, bcklData.Value.Bckl500, bcklData.Value.Bckl667))
                {
                    isBcklReEntry = true;
                }
            }

            if (isBcklReEntry)
                _lastBcklEntryCandleIndex = navCandles.Count - 1;
        }

        // Task 4.3 — LTF-Reversal-Detection (konservativer Entry).
        // EntryMode.Conservative: Signal NUR bei bestätigtem LTF-Reversal.
        // EntryMode.Both: Aggressive-Limits + Reversal als Bonus-Confluence.
        // Strukturpunkte-Doku §5C (A7): Optional wird Pinbar/Engulfing erzwungen (kein Micro-Sequence-Fallback).
        // Spec §4 (B12): Optional wird zusätzlich Body-Close innerhalb/oberhalb der Korrekturbox erzwungen.
        var entryMode = context.RiskSettings?.EntryMode ?? EntryMode.Both;
        var requireWickRejection = context.RiskSettings?.RequireWickRejectionInBZone ?? false;
        var requireBoxClose = context.RiskSettings?.RequireBoxCloseOnEntry ?? false;

        // Box-Grenzen für B12 (Long: Low=667%, High=500%; Short: Low=500%, High=667%).
        decimal? boxLower = null, boxUpper = null;
        if (requireBoxClose)
        {
            boxLower = Math.Min(navSeq.Retracement500, navSeq.Retracement667);
            boxUpper = Math.Max(navSeq.Retracement500, navSeq.Retracement667);
        }

        // Conservative-Mode erzwingt Reversal sowieso; A7 sorgt dafür dass auch Both/Aggressive das erzwingen.
        var mustHaveReversal = entryMode == EntryMode.Conservative || requireWickRejection;
        // Bei A7 (RequireWickRejectionInBZone) verlangen wir ausschließlich Pinbar/Engulfing — keine Micro-Sequence.
        var onlyPinbarOrEngulfing = requireWickRejection;

        LtfReversalHit? ltfReversal = null;
        if (entryMode != EntryMode.Aggressive
            && navSeq.State == SequenceState.Active
            && (navSeq.IsInBuyZone(currentPrice) || navSeq.IsInGklZone(currentPrice)))
        {
            ltfReversal = LtfReversalDetector.Detect(
                filterCandles, bullish: tradeIsLong,
                correctionBoxLower: boxLower, correctionBoxUpper: boxUpper,
                enforceBoxClose: requireBoxClose,
                requirePinbarOrEngulfingOnly: onlyPinbarOrEngulfing);
        }

        // Conservative-Only oder Wick-Rejection-Pflicht → ohne Reversal kein Entry.
        if (mustHaveReversal && ltfReversal == null)
        {
            var reasonPrefix = entryMode == EntryMode.Conservative ? "Konservativer Modus" : "Wick-Rejection-Pflicht";
            var suffix = requireBoxClose ? " + Box-Close" : "";
            var patterns = onlyPinbarOrEngulfing ? "Pinbar/Engulfing" : "Pinbar/Engulfing/Micro-Seq";
            return Blocked(navTf, $"{reasonPrefix}: Kein LTF-Reversal ({patterns}){suffix}");
        }

        // BUCH-ONLY (21.04.2026): Kein BC-Tiefen-Warnsignal. Das Buch bewertet die BC-Korrektur nicht
        // numerisch — einzige Korrektur-Grenze ist das 78.6%-Retracement, darunter ist die Sequenz
        // laut Buch "nur ein Zeichen nachlassenden Momentums", aber nicht automatisch blockiert.
        // BcDepthMonitor entfernt.

        // Hinweis: MTA-Block (§7) wird bereits oberhalb (vor dem Dedup-Check) ausgeführt — siehe
        // `BlockLtfEntryWhenHtfInTargetZone` + `IsHigherTfInTargetZone`. Keine Duplizierung hier.

        // ───────────────────────────────────────────────────────────
        // Confluence-Score nach SK-Buch (Task 2.2 — hierarchisch gewichtet, Max 10 inkl. HighProbability)
        // Buch: "Der Heilige Gral: Überschneidungen (Confluence)"
        // ───────────────────────────────────────────────────────────
        var scorer = new SkConfluenceScorer();

        // Kategorie 1 (Price-Action): Navigator-Sequenz aktiviert
        if (navMachine.State >= SmState.Aktiviert)
            scorer.Add(ConfluenceCategory.PriceAction, $"{navTf}-Aktiviert");

        // Kategorie 2 (Fibonacci): B im Golden Pocket (50-66.7%)
        if (navSeq.IsInBuyZone(currentPrice) || navSeq.IsInGklZone(currentPrice))
            scorer.Add(ConfluenceCategory.FibonacciGoldenPocket, "GoldenPocket");

        // Kategorie 3 (GKL-Masterzone, +2): Task 1.1 — MultiTfGklDetector auf W1/D1.
        // Buch: "Königsdisziplin. [...] Wer im GKL kauft, schwimmt mit dem absoluten Strom des Marktes."
        var gklHit = MultiTfGklDetector.Detect(currentPrice, weeklyCandles, dailyCandles,
            requireMatchDirection: true, preferredLong: tradeIsLong);
        if (gklHit != null)
            scorer.Add(ConfluenceCategory.GklMasterZone, $"GKL-{gklHit.Tf}");

        // SK-Spec §7 "Heiliger Gral" (+2): HTF_GKL-Zone überlappt geometrisch mit LTF_BC-Zone oder
        // LTF_EXT_1.618-Gegenrichtung. Stärkste Confluence im SK-System (Doku-Königsklasse der Bestätigung).
        // Doku-Zitat: "IF (HTF_GKL_Zone overlaps with LTF_BC_Zone) OR (HTF_GKL_Zone overlaps with
        // LTF_Target_Zone_EXT_1618 der Gegenrichtung): Markiere diese Zone als HIGH_PROBABILITY_ZONE."
        // Im Unterschied zum reinen Preis-in-GKL-Check (gklHit) prüft der Overlap-Detector echte Intervall-Geometrie:
        // auch wenn der Preis gerade nicht in der GKL steht, zählt die Konstellation als High-Probability.
        var overlapHit = false;
        if (scannerSettings?.EnableConfluenceOverlapDetection != false)
        {
            var counterMachineForOverlap = tradeIsLong ? navShortMachine : navLongMachine;
            var counterSeqForOverlap = counterMachineForOverlap?.ToSequence(navCandles);
            var overlap = SkConfluenceZoneOverlap.EvaluateFromHtf(
                weeklyCandles, dailyCandles, navSeq, counterSeqForOverlap);
            if (overlap.HasOverlap)
            {
                scorer.Add(ConfluenceCategory.HighProbabilityZone, overlap.Reason);
                overlapHit = true;
            }
        }

        // Kategorie 4 (Higher-TF): Fahrplan-Alignment (W1+D1 stimmt mit Trade-Richtung überein)
        if (fahrplanBias.HasValue && fahrplanBias.Value == tradeIsLong)
            scorer.Add(ConfluenceCategory.FahrplanAlignment, "Fahrplan");

        // Kategorie 5 (Higher-TF): Höhere TF-Sequenz aktiv in gleiche Richtung
        if (IsHigherTfSequenceAlignedActive(navTf, weeklyCandles, dailyCandles, scannerSettings, tradeIsLong))
            scorer.Add(ConfluenceCategory.HigherTfSequence, "HTF-Seq");

        // Kategorie 6 (Volume): Volume-Spike in den letzten 3 Kerzen
        if (HasVolumeSpike(navCandles))
            scorer.Add(ConfluenceCategory.VolumeSpike, "Volume");

        // Kategorie 7 (Multi-Trade): BCKL-Re-Entry-Bonus
        if (isBcklReEntry)
            scorer.Add(ConfluenceCategory.BcklReEntry, "BCKL-Bonus");

        // Task 4.3: LTF-Reversal-Bonus (+1 wenn Reversal bestätigt, Both-Mode)
        if (ltfReversal != null)
            scorer.Add(ConfluenceCategory.PriceAction, $"LTF:{ltfReversal.Reason}");

        var score = scorer.Score;
        var reasons = scorer.Reasons.ToList();

        // BUCH-ONLY (21.04.2026): Der Confluence-Score wird weiterhin berechnet (Info/Log/Confidence),
        // aber es existiert KEIN Hard-Threshold. Die Spec-Docs kennen Confluence nur qualitativ
        // ("Heiliger Gral = Überlappung HTF_GKL_Zone mit LTF_BC_Zone"). Diese qualitative Erkennung
        // passiert bereits in Step4_ConfluenceMarking / SkConfluenceScorer. Ein numerischer Mindest-
        // Score als Hard-Block ist Implementation-Extra und fliegt raus.
        var minScore = 0;

        // ───────────────────────────────────────────────────────────
        // Multi-Entry Staffelung nach SK-Buch (Cheat 37 + 49):
        //   Primary = Entry bei 50% Retracement (15 Pips SL, Cheat 37).
        //   Additional = Entry bei 66.7% Retracement (20 Pips SL, Cheat 49) — einmalig pro Sequenz.
        // ───────────────────────────────────────────────────────────
        var seqChanged = _entrySeqA != navMachine.PointA
                         || _entrySeqB != navMachine.LockedB
                         || _entrySeqIsLong != tradeIsLong;
        if (seqChanged)
        {
            _entrySeqA = navMachine.PointA;
            _entrySeqB = navMachine.LockedB;
            _entrySeqIsLong = tradeIsLong;
            _triggeredPrimary = false;
            _triggeredAdditional = false;
        }

        // Buch-konforme Entry-Staffelung: Single (nur 50%) oder Dual (50% + 66.7%).
        var entryStrategy = context.RiskSettings?.BCZoneEntryStrategy ?? BCZoneEntryStrategy.Dual;
        bool isAdditionalEntry;

        if (!_triggeredPrimary)
        {
            // Primary-Entry: 50% Retracement (Buch Masterclass §5: aggressiv @ 50%).
            isAdditionalEntry = false;
        }
        else if (entryStrategy == BCZoneEntryStrategy.Dual && !_triggeredAdditional)
        {
            // Additional-Entry: 66.7% Retracement (unteres Ende der Korrekturbox).
            isAdditionalEntry = true;
        }
        else
        {
            return Blocked(navTf, "Entries fuer diese Sequenz bereits gesetzt");
        }

        // ───────────────────────────────────────────────────────────
        // ENTRY + STOP-LOSS (SK-Buch Cheat 36, 37, 49, S.13)
        // SL = 78.6% Retracement, gecappt bei Markt-Pips, Point0 als absolute Grenze.
        // ───────────────────────────────────────────────────────────
        decimal entry, sl;
        var pipScale = GetPipScaling(context, navTf);
        // Task 4.5: Pip-Buffer unter Point0/PointB (Buch: 5-15 Pips je TF)
        var slBufferPips = GetSlBufferPips(context, navTf);

        if (isBcklReEntry && bcklData.HasValue)
        {
            // BCKL-Re-Entry (Buch Workflow 6.6): Entry am BCKL-50%, SL unter PointB (Task 3.6).
            // Buch: "Bei der BC-Korrektur: SL kommt unter Punkt B" — nicht Point 0, weil die
            // BC-Welle zwischen B und aktuellem High/Low lebt. B-Bruch = Welle tot.
            entry = bcklData.Value.Bckl500;
            var pointB = navSeq.PointB?.Price ?? navSeq.Point0.Price;
            sl = PipStopLossCalculator.CalculateBcklStopLoss(
                context.Symbol, context.Category, entry, tradeIsLong,
                pointB,
                isSingleTrade: true, pipScale: pipScale, bufferPips: slBufferPips);
        }
        else
        {
            // Buch-Entry: Primary @ 50% (Box-Oberkante) oder Additional @ 66.7% (Box-Unterkante).
            entry = isAdditionalEntry ? navSeq.Retracement667 : navSeq.Retracement500;
            sl = PipStopLossCalculator.CalculateBookStopLoss(
                context.Symbol, context.Category, entry, tradeIsLong,
                navSeq.Retracement786, navSeq.Point0.Price,
                isSingleTrade: !isAdditionalEntry, pipScale: pipScale, bufferPips: slBufferPips);
        }

        // Sanity: SL auf richtiger Seite (Buch-SL plus Pip-Cap plus Point0-Clamp sollte das sicherstellen).
        var slOnWrongSide = (tradeIsLong && sl >= entry) || (!tradeIsLong && sl <= entry);
        if (slOnWrongSide)
            return Blocked(navTf, "SL auf falscher Seite (Geometrie-Fehler)");

        var slDistance = Math.Abs(entry - sl);
        if (slDistance <= 0)
            return Blocked(navTf, "SL-Distanz = 0");

        // ───────────────────────────────────────────────────────────
        // TAKE-PROFIT (SK-Buch S.16, Workflow 4.5)
        // TP1 = 161.8% Extension (Partial-Close 50%), TP2 = 200% + 20 Pips Buffer.
        // ───────────────────────────────────────────────────────────
        var tp1 = navSeq.Extension1618;
        var tp2Buffer = PipStopLossCalculator.Get20PipsBuffer(context.Symbol, context.Category, currentPrice);
        var tp2 = tradeIsLong ? navSeq.Extension200 + tp2Buffer : navSeq.Extension200 - tp2Buffer;

        // Sanity: tp1 muss VOR tp2 liegen.
        var tp1PastTp2 = tradeIsLong ? tp1 > tp2 : tp1 < tp2;
        if (tp1PastTp2)
            tp1 = tradeIsLong ? Math.Min(tp1, tp2) : Math.Max(tp1, tp2);

        // SK-Buch S.13: MinRRR = 1:1 (relativ zum TP2-Ziellevel).
        var tpDist = Math.Abs(tp2 - entry);
        var rrr = slDistance > 0 ? tpDist / slDistance : 0m;
        const decimal minRrr = 1.0m;
        if (rrr < minRrr)
            return Blocked(navTf, $"RRR zu klein ({rrr:F1}:1 < {minRrr:F1}:1) — TP2 zu nah am Entry");

        // ───────────────────────────────────────────────────────────
        // Task 4.12 — Masterclass-Pipeline (9-Schritte-Checkliste final validieren)
        // Buch: "Wenn ein erfahrener SK-Trader den Chart öffnet, folgt er einer strikten Checkliste."
        // Alle vorherigen Berechnungen landen im Data-Dictionary, die Pipeline prüft Konsistenz.
        // ───────────────────────────────────────────────────────────
        var pipelineResult = RunMasterclassPipeline(context, navSeq, scorer, entry, sl, tp1, tp2,
            tradeIsLong, minScore, ltfReversal);
        if (!pipelineResult.success)
            return Blocked(navTf, pipelineResult.reason);

        // ───────────────────────────────────────────────────────────
        // Signal erstellen
        // ───────────────────────────────────────────────────────────
        // BUCH-ONLY (24.04.2026): Signal-Cooldown + Dedup-Tracking sind entfernt. Invalidation
        // am Point0 und Navigator-Sequenz-Promote/Reset via StateMachine reichen als Dedup.

        // Flag-Setzen entsprechend dem gewählten Entry-Level
        if (isAdditionalEntry) _triggeredAdditional = true;
        else _triggeredPrimary = true;

        var entryLabel = isAdditionalEntry ? " [Additional 66.7%]" : " [Primary 50%]";
        LastStatus = $"SIGNAL! [{navTf}] {(tradeIsLong ? "Long" : "Short")} Score={score} RRR={rrr:F1}:1"
                   + entryLabel
                   + (isBcklReEntry ? " [BCKL]" : "");

        var reasonText = $"SK [{navTf}] {(tradeIsLong ? "Long" : "Short")} | "
                       + $"{navTf}:0={navMachine.Point0:G6} A={navMachine.PointA:G6} B={navMachine.LockedB:G6} | "
                       + $"Score={score}/{minScore} RRR={rrr:F1}:1 | {string.Join(", ", reasons)}";

        var side = tradeIsLong ? Signal.Long : Signal.Short;
        // Task 3.4: Confidence dynamisch aus Scorer.MaxScore statt festem Divisor 12.
        // Mit voll ausgestatteter Bestätigung (GKL + alle Kategorien) erreicht confidence 1.0.
        var confidence = scorer.Confidence;

        // Filter-TF ATR für Trailing-Stop-Referenz
        decimal? entryAtr = null;
        if (filterCandles is { Count: > 14 })
        {
            var fAtrSeries = IndicatorHelper.CalculateAtr(filterCandles, 14);
            if (fAtrSeries.Count > 0 && fAtrSeries[^1].HasValue)
                entryAtr = fAtrSeries[^1]!.Value;
        }

        // Sequenz-ID inkl. Navigator-TF-Suffix, damit D1-Long und 5m-Long parallel möglich sind
        var tick = Math.Max(currentPrice * 0.0001m, 1e-8m);
        decimal RoundToTick(decimal v) => Math.Round(v / tick) * tick;
        var entrySuffix = isAdditionalEntry ? "_Add" : "_Prim";
        var seqId = $"{context.Symbol}_{navTf}_{RoundToTick(navSeq.Point0.Price):G8}_{RoundToTick(navSeq.PointA.Price):G8}{entrySuffix}";

        return new SignalResult(
            side, confidence,
            EntryPrice: entry,
            StopLoss: sl,
            TakeProfit: tp1,
            Reason: reasonText,
            TakeProfit2: tp2,
            ConfluenceScore: score,
            PreferLimitOrder: true,
            DisableSmartBreakeven: DisableSmartBreakeven,
            IsAdditionalEntry: isAdditionalEntry,
            EntryAtr: entryAtr,
            SequenceId: seqId,
            IsGklSetup: gklHit != null,
            GklTimeframe: gklHit?.Tf,
            NavPointA: navSeq.PointA.Price,
            RunnerHardCap: navSeq.Extension4236,
            // Spec §7 (B19): Positions-Boost bei bestätigtem High-Probability-Overlap (HTF-GKL ∩ LTF-BC/LTF-EXT-Counter).
            // Nur aktiv wenn Multiplier > 1.0 gesetzt ist — sonst kein Override (Default-Sizing).
            // RiskManager kappt die resultierende Position via MaxPositionSizePercent.
            PositionScaleOverride: overlapHit
                && (context.RiskSettings?.HighProbabilityPositionMultiplier ?? 1.0m) > 1.0m
                    ? context.RiskSettings!.HighProbabilityPositionMultiplier
                    : null);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper: Fahrplan (Weekly/Daily)
    // ═══════════════════════════════════════════════════════════════

    private bool? DetermineFahrplanBias(
        IReadOnlyList<Candle>? weekly,
        IReadOnlyList<Candle>? daily,
        decimal currentPrice,
        ScannerSettings? scannerSettings = null)
    {
        // ATR-Multiplikatoren und MinPoint0Candles kommen (sofern vorhanden) aus den
        // User-Settings, damit Fahrplan-Bias, Scorer-Multi-TF-Block und
        // Scorer-Multi-TF-Block und Fahrplan-Bias **dieselbe** W1/D1-Sequenz sehen.
        if (weekly is { Count: > 20 })
        {
            var w1Atr = IndicatorHelper.CalculateAtr(weekly, 14);
            var w1AtrValue = w1Atr.Count > 0 && w1Atr[^1].HasValue ? w1Atr[^1]!.Value : 0m;
            var w1AtrPct = currentPrice > 0 && w1AtrValue > 0
                ? w1AtrValue / currentPrice * 100m : 5.0m;
            var w1ImpMul = GetAtrMultiplier(scannerSettings, TimeFrame.W1, impulse: true, fallback: 1.0m);
            var w1CorrMul = GetAtrMultiplier(scannerSettings, TimeFrame.W1, impulse: false, fallback: 1.5m);
            var w1MinP0 = GetMinPoint0Candles(scannerSettings, TimeFrame.W1);
            var weeklySM = SequenceStateMachine.FromCandles(weekly, w1AtrPct * w1ImpMul, w1AtrPct * w1CorrMul, 0.382m, 0.786m, minPoint0Candles: w1MinP0);
            if (weeklySM != null)
            {
                foreach (var gkl in weeklySM.CompletedGkls)
                {
                    var min = Math.Min(gkl.Gkl500, gkl.Gkl667);
                    var max = Math.Max(gkl.Gkl500, gkl.Gkl667);
                    if (currentPrice >= min && currentPrice <= max)
                    {
                        AmpelStatus[TimeFrame.W1] = $"W1-GKL ({(gkl.IsLong ? "Long" : "Short")})";
                        return gkl.IsLong;
                    }
                }
            }
        }

        if (daily is { Count: > 20 })
        {
            var d1Atr = IndicatorHelper.CalculateAtr(daily, 14);
            var d1AtrValue = d1Atr.Count > 0 && d1Atr[^1].HasValue ? d1Atr[^1]!.Value : 0m;
            var d1AtrPct = currentPrice > 0 && d1AtrValue > 0
                ? d1AtrValue / currentPrice * 100m : 2.0m;
            var d1ImpMul = GetAtrMultiplier(scannerSettings, TimeFrame.D1, impulse: true, fallback: 1.0m);
            var d1CorrMul = GetAtrMultiplier(scannerSettings, TimeFrame.D1, impulse: false, fallback: 1.5m);
            var d1MinP0 = GetMinPoint0Candles(scannerSettings, TimeFrame.D1);
            var dailySM = SequenceStateMachine.FromCandles(daily, d1AtrPct * d1ImpMul, d1AtrPct * d1CorrMul, 0.382m, 0.786m, minPoint0Candles: d1MinP0);
            if (dailySM != null)
            {
                foreach (var gkl in dailySM.CompletedGkls)
                {
                    var min = Math.Min(gkl.Gkl500, gkl.Gkl667);
                    var max = Math.Max(gkl.Gkl500, gkl.Gkl667);
                    if (currentPrice >= min && currentPrice <= max)
                    {
                        AmpelStatus[TimeFrame.D1] = $"D1-GKL ({(gkl.IsLong ? "Long" : "Short")})";
                        return gkl.IsLong;
                    }
                }
            }

            var ath = daily.Max(c => c.High);
            var atl = daily.Min(c => c.Low);
            var range = ath - atl;
            if (range > 0)
            {
                var pos = (currentPrice - atl) / range;
                if (pos < 0.30m) { AmpelStatus[TimeFrame.D1] = "D1-BLASH Long"; return true; }
                if (pos > 0.70m) { AmpelStatus[TimeFrame.D1] = "D1-BLASH Short"; return false; }
                AmpelStatus[TimeFrame.D1] = "D1-Mid";
            }
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════════
    // TF-Helper
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Multi-TF Standalone ChoCH-Filter-Staffel (siehe Plan Tabelle Abschnitt 1):
    /// D1→H4, H4→H1, H1→M15, M15→M5 (optional, kann null sein → kein Filter).
    /// </summary>
    public static TimeFrame? GetFilterTimeframe(TimeFrame navTf) => navTf switch
    {
        TimeFrame.D1 => TimeFrame.H4,
        TimeFrame.H4 => TimeFrame.H1,
        TimeFrame.H1 => TimeFrame.M15,
        TimeFrame.M15 => TimeFrame.M5,
        TimeFrame.W1 => TimeFrame.D1,
        _ => null,
    };

    /// <summary>Default-ATR-Prozent pro TF wenn ATR-Wert nicht ermittelbar (Fallback).</summary>
    private static decimal GetDefaultAtrPercent(TimeFrame tf) => tf switch
    {
        TimeFrame.W1 => 5.0m,
        TimeFrame.D1 => 2.0m,
        TimeFrame.H4 => 1.5m,
        TimeFrame.H1 => 0.8m,
        TimeFrame.M15 => 0.5m,
        TimeFrame.M5 => 0.3m,
        TimeFrame.M1 => 0.1m,
        _ => 1.0m,
    };

    /// <summary>Min-Point0-Kerzen statischer Fallback (Pivot-Fenster-Parameter).</summary>
    private static int GetMinPoint0Candles(ScannerSettings? settings, TimeFrame tf)
    {
        return tf switch
        {
            TimeFrame.W1 => 3,
            TimeFrame.D1 => 5,
            TimeFrame.H4 => 5,
            TimeFrame.H1 => 3,
            TimeFrame.M15 => 3,
            TimeFrame.M5 => 2,
            _ => 2,
        };
    }

    /// <summary>
    /// BUCH-ONLY: Min-Confluence-Score entfernt. Buch kennt keinen quantitativen Score-Threshold.
    /// Rückgabe immer 0 = Score wird nur für Info-Log berechnet, nicht als Gate.
    /// </summary>
    private static int GetMinConfluenceScore(ScannerSettings? settings, TimeFrame tf, int fallback) => 0;

    /// <summary>
    /// Task 2.2 — Prüft ob eine höhere TF als navTf eine aktivierte Sequenz in die gleiche Richtung hat.
    /// Für navTf=D1 → schau auf W1. Für H4 → D1. Für H1 → H4 (daily-Proxy). Für M15 → H1 (daily-Proxy).
    /// </summary>
    private bool IsHigherTfSequenceAlignedActive(
        TimeFrame navTf,
        IReadOnlyList<Candle>? weekly,
        IReadOnlyList<Candle>? daily,
        ScannerSettings? scannerSettings,
        bool tradeIsLong)
    {
        // Vereinfachung: W1 bei navTf<=D1, D1 bei navTf<=H4, ansonsten Daily als Proxy.
        IReadOnlyList<Candle>? htfCandles = navTf switch
        {
            TimeFrame.D1 => weekly,
            TimeFrame.H4 => daily,
            TimeFrame.H1 => daily,
            TimeFrame.M15 => daily,
            _ => null,
        };
        if (htfCandles == null || htfCandles.Count < 30) return false;

        // Strukturpunkte-Compliance-Audit: HTF-ATR echt berechnen (vorher hardcoded 0.5% → TradFi/Forex random).
        // currentPrice wird aus dem letzten HTF-Close genommen (nah genug am Ticker-Preis, vermeidet Parameter-Weitergabe).
        var refPrice = htfCandles[^1].Close;
        var atrPercent = CalculateAtrPercent(htfCandles, refPrice, TimeFrame.D1);
        var impulseMul = GetAtrMultiplier(scannerSettings, navTf, impulse: true, fallback: 1.0m);
        var corrMul = GetAtrMultiplier(scannerSettings, navTf, impulse: false, fallback: 1.5m);
        var minPoint0 = GetMinPoint0Candles(scannerSettings, navTf);
        // Strukturpunkte §3: BOS-Anker + Close/Docht-Break-Setting identisch zum Nav-Call durchreichen,
        // damit das HTF-Gate weder asymmetrisch strenger noch loser als der Navigator läuft.
        var htfBosAnchor = Math.Max(2, scannerSettings?.BosAnchorSwingStrength ?? 5);
        var htfBosCloseBreak = scannerSettings?.RequireBosCloseBreak ?? true;

        var (htfPrimary, _, _) = SequenceStateMachine.FromCandlesBoth(
            htfCandles, atrPercent * impulseMul, atrPercent * corrMul, 0.382m, 0.786m, minPoint0,
            bosRequireCloseBreak: htfBosCloseBreak,
            bosAnchorSwingStrength: htfBosAnchor);
        if (htfPrimary == null) return false;
        return htfPrimary.State >= SmState.Aktiviert && htfPrimary.IsLong == tradeIsLong;
    }

    /// <summary>
    /// Berechnet die aktuelle ATR_14% der gegebenen Candle-Reihe — fallback auf TF-Default wenn nicht möglich.
    /// Einheitlich genutzt für HTF-Checks (vorher war 0.5% hardcoded → kein Crypto/Forex/Stock-Scaling).
    /// </summary>
    private static decimal CalculateAtrPercent(IReadOnlyList<Candle> candles, decimal currentPrice, TimeFrame fallbackTf)
    {
        var atr = IndicatorHelper.CalculateAtr(candles, 14);
        var atrVal = atr.Count > 0 && atr[^1].HasValue ? atr[^1]!.Value : 0m;
        if (currentPrice > 0 && atrVal > 0) return atrVal / currentPrice * 100m;
        return GetDefaultAtrPercent(fallbackTf);
    }

    /// <summary>
    /// Spec §7 (B18) — MTA-Zielzonen-Guard. True wenn eine aktivierte HTF-Sequenz in gleicher Richtung
    /// wie der geplante Trade existiert UND der aktuelle Preis im Zielbereich <c>[Extension1618, Extension200]</c>
    /// dieser HTF-Sequenz liegt. In diesem Fall soll kein LTF-Entry in dieselbe Richtung platziert werden,
    /// weil die HTF-Sequenz kurz vor ihrem TP-Exit / einer möglichen Trendwende steht.
    /// </summary>
    private bool IsHigherTfInTargetZone(
        TimeFrame navTf,
        IReadOnlyList<Candle>? weekly,
        IReadOnlyList<Candle>? daily,
        ScannerSettings? scannerSettings,
        decimal currentPrice,
        bool tradeIsLong)
    {
        IReadOnlyList<Candle>? htfCandles = navTf switch
        {
            TimeFrame.D1 => weekly,
            TimeFrame.H4 => daily,
            TimeFrame.H1 => daily,
            TimeFrame.M15 => daily,
            _ => null,
        };
        if (htfCandles == null || htfCandles.Count < 30) return false;

        // Strukturpunkte-Compliance-Audit: HTF-ATR echt statt hardcoded 0.5% (Forex/Stocks lieferten vorher Random-Sequenzen).
        var atrPercent = CalculateAtrPercent(htfCandles, currentPrice, TimeFrame.D1);
        var impulseMul = GetAtrMultiplier(scannerSettings, navTf, impulse: true, fallback: 1.0m);
        var corrMul = GetAtrMultiplier(scannerSettings, navTf, impulse: false, fallback: 1.5m);
        var minPoint0 = GetMinPoint0Candles(scannerSettings, navTf);
        // Strukturpunkte §3: BOS-Anker + Close/Docht-Break-Setting identisch zum Nav-Call durchreichen,
        // damit der MTA-Zielzonen-Guard zur selben Strukturbruch-Definition kommt.
        var htfBosAnchor = Math.Max(2, scannerSettings?.BosAnchorSwingStrength ?? 5);
        var htfBosCloseBreak = scannerSettings?.RequireBosCloseBreak ?? true;

        var (htfPrimary, _, _) = SequenceStateMachine.FromCandlesBoth(
            htfCandles, atrPercent * impulseMul, atrPercent * corrMul, 0.382m, 0.786m, minPoint0,
            bosRequireCloseBreak: htfBosCloseBreak,
            bosAnchorSwingStrength: htfBosAnchor);
        if (htfPrimary == null) return false;
        if (htfPrimary.State < SmState.Aktiviert) return false;
        if (htfPrimary.IsLong != tradeIsLong) return false; // Gegenrichtung ist separater Guard — nicht unsere Sache.

        // Zielzone [Ext1618, Ext200] mit korrekter Order je Richtung.
        var zoneLo = Math.Min(htfPrimary.Extension1618, htfPrimary.Extension200);
        var zoneHi = Math.Max(htfPrimary.Extension1618, htfPrimary.Extension200);
        return zoneLo > 0 && currentPrice >= zoneLo && currentPrice <= zoneHi;
    }

    /// <summary>
    /// Task 2.2 — Volume-Spike in den letzten 3 Kerzen. Spike = max(Volumen der letzten 3) >= 1.5× Durchschnitt der 20 davor.
    /// </summary>
    private static bool HasVolumeSpike(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 25) return false;
        var lastN = 3;
        var baseN = 20;
        var tail = candles.Count - 1;
        var maxRecent = 0m;
        for (var i = tail - lastN + 1; i <= tail; i++)
            if (candles[i].Volume > maxRecent) maxRecent = candles[i].Volume;
        var sum = 0m;
        for (var i = tail - lastN - baseN + 1; i <= tail - lastN; i++) sum += candles[i].Volume;
        var avg = sum / baseN;
        return avg > 0 && maxRecent >= avg * 1.5m;
    }

    /// <summary>
    /// Strukturpunkte-Doku §5A: Prüft ob die Aktivierungs-Kerze (BOS) ausreichend Volumen hat.
    /// Vergleicht <c>candles[activationIdx].Volume</c> gegen SMA(Volume, 20) der Kerzen davor (ausschließlich).
    /// Doku-Regel: "IF Breakout_Candle_Volume &lt; SMA_Volume * 1.5 THEN Ignore_Sequence."
    /// Liefert true, wenn der Filter PASSIERT wird (Volumen ausreichend), false = Fakeout → blockieren.
    /// Graceful-Degradation: Bei zu wenigen Kerzen (SMA nicht berechenbar) wird true zurückgegeben (kein Block).
    /// </summary>
    private static bool HasBosVolumeBreakout(IReadOnlyList<Candle> candles, int activationIdx, decimal multiplier)
    {
        if (activationIdx < 0 || activationIdx >= candles.Count) return true; // Graceful: Index ungültig → nicht blockieren
        const int smaPeriod = 20;
        var from = activationIdx - smaPeriod;
        if (from < 0) return true; // Graceful: zu wenig Historie → nicht blockieren

        var breakoutVol = candles[activationIdx].Volume;
        var sum = 0m;
        for (var i = from; i < activationIdx; i++)
            sum += candles[i].Volume;
        var avg = sum / smaPeriod;
        if (avg <= 0) return true; // Graceful: Kein Vol-Wert → nicht blockieren
        return breakoutVol >= avg * multiplier;
    }

    /// <summary>
    /// Strukturpunkte-Doku §5B: Liefert die wirksame Swing-Strength für <see cref="SequenceDetector.FindSwingPoints"/>.
    /// Wenn <see cref="ScannerSettings.AdaptiveSwingStrength"/> aktiviert ist, wird der Wert aus der aktuellen ATR_14%
    /// abgeleitet (<see cref="CalculateAdaptiveSwingStrength"/>); sonst der <paramref name="fallback"/>.
    /// </summary>
    private static int ResolveSwingStrength(ScannerSettings? settings, IReadOnlyList<Candle> candles,
        decimal currentPrice, TimeFrame tf, int fallback)
    {
        if (settings?.AdaptiveSwingStrength != true) return fallback;
        var atr = IndicatorHelper.CalculateAtr(candles, 14);
        var atrVal = atr.Count > 0 && atr[^1].HasValue ? atr[^1]!.Value : 0m;
        var atrPct = currentPrice > 0 && atrVal > 0 ? atrVal / currentPrice * 100m : 0m;
        return CalculateAdaptiveSwingStrength(
            atrPct,
            settings.SwingStrengthMin > 0 ? settings.SwingStrengthMin : 3,
            settings.SwingStrengthMax > 0 ? settings.SwingStrengthMax : 10,
            settings.SwingStrengthAtrThresholdLow > 0 ? settings.SwingStrengthAtrThresholdLow : 0.5m,
            settings.SwingStrengthAtrThresholdHigh > 0 ? settings.SwingStrengthAtrThresholdHigh : 3.0m);
    }

    /// <summary>
    /// Strukturpunkte-Doku §1: Liefert asymmetrisches (LeftBars, RightBars)-Paar wenn beide Settings &gt; 0, sonst null.
    /// Bei null fällt der Aufrufer auf das symmetrische Strength-Schema zurück. Doku-Empfehlung: 5-10 / 3-5.
    /// Wenn adaptive Swing-Strength aktiv ist, wird der resolved-Wert als Left-Anker verwendet und Right skaliert mit (links/right-Ratio).
    /// </summary>
    private static (int LeftBars, int RightBars)? ResolvePivotBars(ScannerSettings? settings, int fallbackSymmetric)
    {
        if (settings == null) return null;
        if (settings.PivotLeftBars > 0 && settings.PivotRightBars > 0)
        {
            // Adaptive + asymmetric: Left-Bars skalieren mit fallbackSymmetric (d.h. aktueller adaptiver Wert);
            // Right bleibt auf konfiguriertem Verhältnis.
            if (settings.AdaptiveSwingStrength && fallbackSymmetric > 0)
            {
                var ratio = (decimal)settings.PivotRightBars / settings.PivotLeftBars;
                var scaledLeft = fallbackSymmetric;
                var scaledRight = Math.Max(1, (int)Math.Round(scaledLeft * ratio));
                return (scaledLeft, scaledRight);
            }
            return (settings.PivotLeftBars, settings.PivotRightBars);
        }
        return null;
    }

    /// <summary>
    /// Strukturpunkte-Doku §5B: Adaptive Pivot-Länge basierend auf aktueller ATR%-Volatilität.
    /// Niedrige Vola (ATR% &lt;= thresholdLow) → <paramref name="minStrength"/> (schnelle Erkennung).
    /// Hohe Vola (ATR% &gt;= thresholdHigh) → <paramref name="maxStrength"/> (mehr Puffer gegen Noise-Spikes).
    /// Dazwischen: linear interpoliert. Doku: "Wenn die Vola hoch ist, benötigt der Bot mehr Kerzen (z.B. 10 links/rechts),
    /// um einen echten Wendepunkt zu identifizieren. Ist der Markt langsam, reichen 3 Kerzen."
    /// </summary>
    private static int CalculateAdaptiveSwingStrength(decimal atrPercent, int minStrength, int maxStrength,
        decimal thresholdLow, decimal thresholdHigh)
    {
        if (maxStrength < minStrength) (minStrength, maxStrength) = (maxStrength, minStrength);
        if (atrPercent <= thresholdLow) return minStrength;
        if (atrPercent >= thresholdHigh) return maxStrength;
        if (thresholdHigh <= thresholdLow) return minStrength;
        var ratio = (atrPercent - thresholdLow) / (thresholdHigh - thresholdLow);
        var range = maxStrength - minStrength;
        return minStrength + (int)Math.Round(range * ratio);
    }

    /// <summary>Pip-Skalierung pro TF. Primär aus <see cref="RiskSettings.PipScalingByTf"/>, Fallback 1.0 (bzw. 0.75 für M15).</summary>
    private static decimal GetPipScaling(MarketContext ctx, TimeFrame tf)
    {
        if (ctx.RiskSettings?.PipScalingByTf is { } dict
            && dict.TryGetValue(tf, out var scale) && scale > 0)
            return scale;
        return tf switch
        {
            TimeFrame.M15 => 0.75m,
            TimeFrame.M5 => 0.5m,
            _ => 1.0m,
        };
    }

    /// <summary>
    /// Task 4.12 — Orchestriert die Masterclass-Pipeline (9 Buch-Schritte) als abschließende Validation.
    /// Alle vorher berechneten Werte (entry/sl/tp/scorer) werden in ein Data-Dictionary gepackt,
    /// das die Steps prüfen. Diese Pipeline ist der strikte Buch-Checklisten-Layer.
    /// </summary>
    private (bool success, string reason) RunMasterclassPipeline(
        MarketContext context, Sequence navSeq, SkConfluenceScorer scorer,
        decimal entry, decimal sl, decimal tp1, decimal tp2,
        bool tradeIsLong, int minConfluence, LtfReversalHit? ltfReversal)
    {
        var steps = new IPipelineStep[]
        {
            new Step1_NewsCheck(context.RiskSettings?.NewsBlackoutMinutes ?? 30),
            new Step2_TopDownGkl(),
            new Step3_SequenceMapping(),
            new Step4_ConfluenceMarking(minConfluence),
            new Step5_EntryDefinition(),
            new Step6_LotSizing(),
            new Step7_StopLossSetting(),
            new Step8_TargetSetting(),
            new Step9_BreakevenArm(),
        };
        var pipeline = new SkMasterclassPipeline(steps);
        var data = new Dictionary<string, object>
        {
            ["tradeIsLong"] = tradeIsLong,
            ["navSeq"] = navSeq,
            ["scorer"] = scorer,
            ["entry"] = entry,
            ["sl"] = sl,
            ["tp1"] = tp1,
            ["tp2"] = tp2,
            ["navPointA"] = navSeq.PointA.Price,
        };
        if (ltfReversal != null) data["ltfReversal"] = ltfReversal;

        var (success, _, stepName, reason, _) = pipeline.Run(context);
        return (success, success ? "Pipeline ok" : $"Pipeline Step {stepName}: {reason}");
    }

    /// <summary>
    /// Task 4.5 — SL-Buffer in Pips je TF. Buch: "5-15 Pips je nach Zeiteinheit" unter Punkt 0.
    /// Primär aus <see cref="RiskSettings.SlBufferPipsByTf"/>, Fallback entsprechend Plan.
    /// </summary>
    private static decimal GetSlBufferPips(MarketContext ctx, TimeFrame tf)
    {
        if (ctx.RiskSettings?.SlBufferPipsByTf is { } dict
            && dict.TryGetValue(tf, out var pips) && pips > 0)
            return pips;
        return tf switch
        {
            TimeFrame.W1 => 15m,
            TimeFrame.D1 => 15m,
            TimeFrame.H4 => 12m,
            TimeFrame.H1 => 8m,
            TimeFrame.M15 => 5m,
            _ => 8m,
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // SK-Plan Helper
    // ═══════════════════════════════════════════════════════════════

    // BUCH-ONLY: Einheitliche Multiplikatoren (keine TF-spezifischen Maps mehr).
    private static decimal GetAtrMultiplier(ScannerSettings? settings, TimeFrame tf, bool impulse, decimal fallback)
        => fallback;

    // ═══════════════════════════════════════════════════════════════
    // Block-Helper
    // ═══════════════════════════════════════════════════════════════

    private SignalResult Blocked(TimeFrame navTf, string reason)
    {
        var ampelSummary = string.Join("|", AmpelStatus.Select(kv => $"{kv.Key}:{kv.Value}"));
        LastStatus = $"[{ampelSummary}] {reason}";
        return new SignalResult(Signal.None, 0m, null, null, null, LastStatus);
    }
}
