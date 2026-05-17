using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Engine.Indicators;
using BingXBot.Engine.News;
using BingXBot.Engine.Risk;
using BingXBot.Engine.Strategies.Confluence;

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

    /// <summary>
    /// Phase 18 / B4 — Letzte Exception aus dem News-Blackout-Check (graceful-degradation-Pfad).
    /// Wird vom TradingServiceBase nach jedem Evaluate gepollt + in einen Failure-Counter aggregiert.
    /// </summary>
    public Exception? LastNewsCheckException { get; private set; }

    /// <summary>Ampel-Status pro TF — wird pro Evaluate-Aufruf aktualisiert.</summary>
    public Dictionary<TimeFrame, string> AmpelStatus { get; private set; } = new();

    /// <summary>
    /// v1.5.2 Phase 4 — Letzte gefaellte Entscheidung (Reject-Reason oder Success).
    /// Aufrufer (TradingServiceBase) liest dies nach jedem Evaluate-Call und publisht
    /// in <c>BotEventBus.EvaluationDecided</c>. Wird nur gesetzt wenn der Decision-Trail
    /// aktiv ist — bei <c>BotSettings.EnableDecisionTrail = false</c> bleibt das Feld null.
    /// </summary>
    public BingXBot.Core.Diagnostics.EvaluationDecision? LastEvaluationDecision { get; private set; }

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

        // v1.5.2 Phase 4 — Eval-Kontext fuer Decision-Trail vorbereiten (Symbol/State/Punkte
        // werden im Lauf von Evaluate gesetzt, sodass Blocked() einen aktuellen Snapshot hat).
        _lastEvalSymbol = context.Symbol;
        _lastEvalSequenceState = "Unknown";
        _lastEvalPoint0 = null;
        _lastEvalPointA = null;
        _lastEvalPointB = null;
        _lastEvalScore = 0;
        _lastEvalCategories = null;
        LastEvaluationDecision = null;

        // Ampel zurücksetzen und Standard-Einträge anlegen (Duplikate vermeiden wenn navTf=D1 oder =W1)
        AmpelStatus = new Dictionary<TimeFrame, string>();
        AmpelStatus[TimeFrame.W1] = "—";
        AmpelStatus[TimeFrame.D1] = "—";
        AmpelStatus[navTf] = "—";
        var filterTf = GetFilterTimeframe(navTf);
        if (filterTf.HasValue)
            AmpelStatus[filterTf.Value] = "—";

        // Phase 18 / D1 — Swing-Strength + Min-Candles-Offset aus ScannerSettings, mit Field-Fallback.
        var navSwingStrength = scannerSettings?.NavigatorSwingStrength is > 0 ? scannerSettings.NavigatorSwingStrength : _swingStrength;
        var minCandlesOffset = scannerSettings?.NavigatorMinCandlesOffset is > 0 ? scannerSettings.NavigatorMinCandlesOffset : 20;
        if (navCandles.Count < navSwingStrength * 2 + minCandlesOffset)
            return Blocked(navTf, $"Zu wenig {navTf}-Daten",
                BingXBot.Core.Diagnostics.RejectionReasons.InsufficientData);

        // ───────────────────────────────────────────────────────────
        // News-Blackout (Buch S.7: "Wirtschaftskalender checken — stehen große News an?")
        // Phase 18 / B1 — Hot-Path nutzt pre-resolved Cache (TradingServiceBase fragt 1x/Tick).
        // Vorher: GetAwaiter().GetResult() pro Symbol-Tick = N Sync-over-Async-Calls.
        // Backwards-Compat: Wenn ResolvedNewsBlackoutEvent null + Delegate nicht null, fallen wir
        // auf den alten synchron-await-Pfad zurueck (B4 Logging zeigt Failures auf).
        // ───────────────────────────────────────────────────────────
        var newsBlackoutMinutes = context.RiskSettings?.NewsBlackoutMinutes ?? 30;
        if (newsBlackoutMinutes > 0)
        {
            // 1) Pre-resolved aus dem TradingServiceBase-Pre-Computing-Schritt.
            if (!string.IsNullOrEmpty(context.ResolvedNewsBlackoutEvent))
                return Blocked(navTf, $"News-Blackout: {context.ResolvedNewsBlackoutEvent}",
                    BingXBot.Core.Diagnostics.RejectionReasons.NewsBlackout);

            // 2) Legacy-Fallback (Delegate nur dann, wenn kein Pre-Resolved gesetzt ist).
            //    Tests + Backtest-Pfade die MarketContext direkt bauen ohne Pre-Compute landen hier.
            if (context.ResolvedNewsBlackoutEvent == null && context.NewsBlackoutCheck != null)
            {
                try
                {
                    var nowForNews = context.NowUtc ?? DateTime.UtcNow;
                    var blackoutEvent = context.NewsBlackoutCheck(nowForNews, newsBlackoutMinutes, CancellationToken.None)
                        .GetAwaiter().GetResult();
                    if (!string.IsNullOrEmpty(blackoutEvent))
                        return Blocked(navTf, $"News-Blackout: {blackoutEvent}",
                            BingXBot.Core.Diagnostics.RejectionReasons.NewsBlackout);
                }
                catch (Exception ex)
                {
                    // Phase 18 / B4 — vorher stiller Catch. Jetzt LastNewsCheckException
                    // wird pro Strategy-Instanz gemerkt; TradingServiceBase pollt das + zaehlt.
                    LastNewsCheckException = ex;
                }
            }
        }

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
        // 25.04.2026: Asymmetrisches Pivot-Fenster (5/3 Default) hat Vorrang vor symmetrischem Strength-Fallback.
        var bosRequireCloseBreak = scannerSettings?.RequireBosCloseBreak ?? true;
        var bosAnchorSwingStrength = Math.Max(2, scannerSettings?.BosAnchorSwingStrength ?? 5);
        var bosAnchorLeftBars = scannerSettings?.BosAnchorLeftBars ?? 0;
        var bosAnchorRightBars = scannerSettings?.BosAnchorRightBars ?? 0;

        var enableBiasFlip = scannerSettings?.EnableBiasFlip ?? true;
        var (navMachine, navLongMachine, navShortMachine) =
            SequenceStateMachine.FromCandlesBoth(navCandles, navMinImpulse, navCorrThreshold, 0.382m, 0.786m,
                minPoint0Candles: navMinPoint0Candles, enableBiasFlip: enableBiasFlip,
                minImpulseDistance: navMinImpulseDistance,
                bosRequireCloseBreak: bosRequireCloseBreak,
                bosAnchorSwingStrength: bosAnchorSwingStrength,
                bosAnchorLeftBars: bosAnchorLeftBars,
                bosAnchorRightBars: bosAnchorRightBars);

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
            return Blocked(navTf, $"Keine {navTf}-Sequenz (State={navMachine?.State})",
                BingXBot.Core.Diagnostics.RejectionReasons.NoSequence);

        var navSeq = navMachine.ToSequence(navCandles);
        if (navSeq == null)
            return Blocked(navTf, $"{navTf}-Sequenz nicht konstruierbar",
                BingXBot.Core.Diagnostics.RejectionReasons.SequenceNotConstructable);

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
            // 04.05.2026: Inline-Pip-Berechnung entfernt — divergierte vom kanonischen Helper
            // (Stock: hier `*0.0001` statt korrekt `*0.00005`; Index: hier `*0.0001` statt `1`).
            // Counter-Trend-SL-Buffer war auf NCSI/NCSK falsch.
            var ctPipValue = Risk.PipStopLossCalculator.GetPipValue(context.Symbol, context.Category, currentPrice);
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
        // FILTER-TF (nächst tiefere TF) — Korrektur-Ende-Bestätigung (Buch-only)
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

        // Aktiviert ist das Standard-Gate. EntryMode.Conservative darf zusätzlich in SucheB
        // einsteigen, sofern später ein LTF-Reversal bestätigt wird (mustHaveReversal=true).
        // Hintergrund: Conservative-Trader handeln am Reversal-Pattern, nicht am State-Promotion-
        // Zeitpunkt — der Lag zwischen "Preis in Korrekturbox + Reversal" und "State-Machine
        // bestätigt Aktivierung" kostete bisher viele saubere Setups.
        var conservativeMode = (context.RiskSettings?.EntryMode ?? EntryMode.Both) == EntryMode.Conservative;
        var allowsCorrectionZoneEntry = conservativeMode && navMachine.State == SmState.SucheB;
        var isCorrectionZoneEntry = allowsCorrectionZoneEntry;
        if (navMachine.State != SmState.Aktiviert && !allowsCorrectionZoneEntry)
            return Blocked(navTf, $"{navTf} nicht aktiviert (State={navMachine.State})", BingXBot.Core.Diagnostics.RejectionReasons.StateNotActivated);

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
                return Blocked(navTf, $"Volumen-Filter: BOS-Kerze unter {volMul:F1}× SMA20 (Fakeout-Schutz)",
                    BingXBot.Core.Diagnostics.RejectionReasons.BosVolumeBelowThreshold);
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
            return Blocked(navTf, "HTF in Zielzone (EXT_1618-EXT_2000) — LTF-Entry gegen drohende HTF-Wende blockiert (Spec §7)", BingXBot.Core.Diagnostics.RejectionReasons.MtaTargetZoneBlock);
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
        // Phase 18 / D1 — Cooldown-Kerzen aus Settings (Default 2, Field-Fallback const).
        var bcklCooldown = scannerSettings?.BcklReEntryCooldownCandles is > 0 ? scannerSettings.BcklReEntryCooldownCandles : BcklReEntryCooldownCandles;
        if (navMachine.State == SmState.Aktiviert
            && (navCandles.Count - 1 - _lastBcklEntryCandleIndex) >= bcklCooldown)
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

        // Conservative-Mode darf auch in der CorrectionZone (SucheB) prüfen, weil dort der Entry
        // ohnehin durch das Reversal-Pattern bestätigt wird (siehe State-Gate oben).
        var reversalEligibleState = navSeq.State == SequenceState.Active
            || (isCorrectionZoneEntry && navSeq.State == SequenceState.CorrectionZone);

        LtfReversalHit? ltfReversal = null;
        if (entryMode != EntryMode.Aggressive
            && reversalEligibleState
            && (navSeq.IsInBuyZone(currentPrice) || navSeq.IsInGklZone(currentPrice)))
        {
            ltfReversal = LtfReversalDetector.Detect(
                filterCandles, bullish: tradeIsLong,
                correctionBoxLower: boxLower, correctionBoxUpper: boxUpper,
                enforceBoxClose: requireBoxClose,
                requirePinbarOrEngulfingOnly: onlyPinbarOrEngulfing);
        }

        // Conservative-Only oder Wick-Rejection-Pflicht → ohne Reversal kein Entry.
        // Bei CorrectionZone-Entry ist Reversal eine Pflicht (sonst hat das State-Gate
        // sinnlos aufgeweicht); deshalb explizit erzwingen.
        var requireReversalForCorrectionZone = isCorrectionZoneEntry;
        if ((mustHaveReversal || requireReversalForCorrectionZone) && ltfReversal == null)
        {
            var reasonPrefix = isCorrectionZoneEntry
                ? "Konservativer Modus (SucheB)"
                : entryMode == EntryMode.Conservative ? "Konservativer Modus" : "Wick-Rejection-Pflicht";
            var suffix = requireBoxClose ? " + Box-Close" : "";
            var patterns = onlyPinbarOrEngulfing ? "Pinbar/Engulfing" : "Pinbar/Engulfing/Micro-Seq";
            return Blocked(navTf, $"{reasonPrefix}: Kein LTF-Reversal ({patterns}){suffix}",
                BingXBot.Core.Diagnostics.RejectionReasons.MissingWickRejection);
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
        // v1.5.0 Phase 1 — Hard-Gate aktiv? Dann Mikro-Touch-Filter (0.1 % der LTF-B-Box-Spanne)
        // erzwingen, damit ein Pixel-Schnitt nicht als Confluence zaehlt. Bei deaktiviertem Hard-Gate
        // bleibt das Verhalten wie bisher (Soft-Bonus bei jedem Touch).
        var hardGateActive = context.RiskSettings?.RequireHtfConfluenceForEntry ?? false;
        var minOverlapWidthPct = hardGateActive ? 0.1m : 0m;

        var overlapHit = false;
        if (scannerSettings?.EnableConfluenceOverlapDetection != false)
        {
            var counterMachineForOverlap = tradeIsLong ? navShortMachine : navLongMachine;
            var counterSeqForOverlap = counterMachineForOverlap?.ToSequence(navCandles);
            var overlap = SkConfluenceZoneOverlap.EvaluateFromHtf(
                weeklyCandles, dailyCandles, navSeq, counterSeqForOverlap, minOverlapWidthPct);
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

        // v1.5.4 Phase 7 — Funding-Rate Soft-Bonus (User-Erweiterung, nicht im Buch).
        // Long bei stark negativer Funding (Markt zahlt Longs) bzw. Short bei stark positiver
        // Funding (Markt zahlt Shorts) bekommt +1 Score. Schwelle aus ScannerSettings.
        // BingX-Funding ist ueblicherweise Dezimalbruchteil (z.B. 0.0001 = 0.01 %).
        var fundingBonusEnabled = scannerSettings?.EnableFundingRateBonus ?? true;
        if (fundingBonusEnabled && context.FundingRatePercent != 0m)
        {
            var basePct = scannerSettings?.FundingRateBonusThresholdPercent ?? 0.05m;
            // Phase 18 / F5 — Symbol-spezifischer Multiplier auf den Threshold. Memecoins haben
            // strukturell hoehere Funding-Spitzen (0.5-1 %), Majors selten ueber 0.05 %. Ohne
            // Multiplier loest der Bonus auf Memecoins zu oft aus. Default-Multiplier 1.0 = kein Effekt.
            var multiplier = scannerSettings?.GetFundingThresholdMultiplier(context.Category) ?? 1.0m;
            var thresholdPct = basePct * multiplier;
            var thresholdDec = thresholdPct / 100m; // Prozent → Decimal
            var fundingRate = context.FundingRatePercent;
            if (tradeIsLong && fundingRate < -thresholdDec)
                scorer.Add(ConfluenceCategory.FavorableFundingRate, $"Funding {fundingRate:P4} fuer Long (Threshold {thresholdPct:F4} %)");
            else if (!tradeIsLong && fundingRate > thresholdDec)
                scorer.Add(ConfluenceCategory.FavorableFundingRate, $"Funding {fundingRate:P4} fuer Short (Threshold {thresholdPct:F4} %)");
        }

        var score = scorer.Score;
        var reasons = scorer.Reasons.ToList();

        // v1.5.2 Phase 4 — Eval-Snapshot fuer Decision-Trail. Wird in Blocked()-Helper gelesen
        // sobald ein Reject feuert; bei Erfolg unten mit Triggered=true ueberschrieben.
        _lastEvalSequenceState = navMachine.State.ToString();
        _lastEvalPoint0 = navMachine.Point0;
        _lastEvalPointA = navMachine.PointA;
        _lastEvalPointB = navMachine.LockedB;
        _lastEvalScore = score;
        _lastEvalCategories = scorer.Reasons.ToList();

        // 04.05.2026 — Phase 3 "Heiliger Gral als Hard-Gate" (opt-in via RiskSettings).
        // Standard-Default beider Settings ist AUS, sodass das Verhalten unverändert bleibt.
        // Nur User die aktiv 5% Risk fahren und Quality-Boost wollen, schalten das ein.
        //
        // v1.5.0 Phase 1 — Spezialfall: Wenn Navigator-TF bereits W1/D1 ist, gibt es keinen
        // "hoeheren" Timeframe fuer GKL/Overlap. In dem Fall ist das Gate no-op (kein Block) —
        // sonst wuerde jeder W1/D1-Trade mit aktivem Flag stumm verworfen.
        // Mikro-Touch-Filter: bei aktiviertem Hard-Gate verlangt der Overlap-Check eine
        // Mindestbreite von 0.1 % der LTF-B-Box-Spanne (siehe minOverlapWidthPct oben).
        var requireHtfConfluence = hardGateActive;
        var isNavigatorTopTf = navTf == TimeFrame.W1 || navTf == TimeFrame.D1;
        if (requireHtfConfluence && !isNavigatorTopTf && gklHit == null && !overlapHit)
        {
            return Blocked(navTf, $"Hard-Gate: keine HTF-Confluence (weder GKL-Hit noch Zone-Overlap) — {tradeIsLong}", BingXBot.Core.Diagnostics.RejectionReasons.NoHtfConfluence);
        }

        var minScore = context.RiskSettings?.MinConfluenceScore ?? 0;
        if (minScore > 0 && score < minScore)
        {
            return Blocked(navTf, $"Hard-Gate: Confluence-Score {score} < {minScore}", BingXBot.Core.Diagnostics.RejectionReasons.ScoreBelowMin);
        }

        // ───────────────────────────────────────────────────────────
        // Multi-Entry Staffelung nach SK-Buch (Cheat 37 + 49):
        //   Primary = Entry bei 50% Retracement (15 Pips SL, Cheat 37).
        //   Additional = Entry bei 66.7% Retracement (20 Pips SL, Cheat 49) — einmalig pro Sequenz.
        // CorrectionZone-Entries (Conservative + SucheB) sind grundsätzlich Single-Entries:
        // PotentialB ist noch nicht gelocked, also kein additional bei 66.7 % möglich; der
        // Staffel-State wird durch sie nicht "verbraucht", damit ein Primary @ 50 % nach
        // Promotion zu Aktiviert weiterhin möglich bleibt (falls noch keine Position offen ist).
        // ───────────────────────────────────────────────────────────
        bool isAdditionalEntry;
        if (isCorrectionZoneEntry)
        {
            isAdditionalEntry = false;
        }
        else
        {
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
                return Blocked(navTf, "Entries fuer diese Sequenz bereits gesetzt",
                    BingXBot.Core.Diagnostics.RejectionReasons.EntriesAlreadyTriggered);
            }
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
        else if (isCorrectionZoneEntry)
        {
            // Conservative-Mode-Entry in SucheB: Reversal-Signal hat den Einstieg bestätigt,
            // also direkt am aktuellen Preis (Market-Order). SL weiterhin am 78.6 % Retracement.
            entry = currentPrice;
            sl = PipStopLossCalculator.CalculateBookStopLoss(
                context.Symbol, context.Category, entry, tradeIsLong,
                navSeq.Retracement786, navSeq.Point0.Price,
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
            return Blocked(navTf, "SL auf falscher Seite (Geometrie-Fehler)",
                BingXBot.Core.Diagnostics.RejectionReasons.SlGeometryError);

        var slDistance = Math.Abs(entry - sl);
        if (slDistance <= 0)
            return Blocked(navTf, "SL-Distanz = 0",
                BingXBot.Core.Diagnostics.RejectionReasons.SlDistanceZero);

        // ───────────────────────────────────────────────────────────
        // TAKE-PROFIT (SK-Buch S.16, Workflow 4.5)
        // TP1 = 161.8% Extension (Partial-Close 50%), TP2 = 200% + 20 Pips Buffer.
        // ───────────────────────────────────────────────────────────
        var ltfTp1 = navSeq.Extension1618;
        var tp2Buffer = PipStopLossCalculator.Get20PipsBuffer(context.Symbol, context.Category, currentPrice);
        var ltfTp2 = tradeIsLong ? navSeq.Extension200 + tp2Buffer : navSeq.Extension200 - tp2Buffer;

        var tp1 = ltfTp1;
        var tp2 = ltfTp2;
        TimeFrame? tpSourceTimeframe = null;

        // v1.5.0 Phase 2 — Asymmetrisches CRV: SL aus LTF (oben), TPs aus HTF-Sequenz wenn GKL-Setup.
        // SignalResult.IsGklSetup ist hier noch nicht gebaut, aber gklHit haelt dieselbe Information.
        var useAsymmetricCrv = (context.RiskSettings?.UseAsymmetricCrv ?? false) && gklHit != null;
        if (useAsymmetricCrv)
        {
            var htfSeq = TryBuildHtfPrimarySequence(gklHit!.Tf, weeklyCandles, dailyCandles,
                scannerSettings, currentPrice, tradeIsLong);
            if (htfSeq != null && htfSeq.Extension1618 > 0m && htfSeq.Extension200 > 0m)
            {
                var htfTp1 = htfSeq.Extension1618;
                var htfTp2 = tradeIsLong
                    ? htfSeq.Extension200 + tp2Buffer
                    : htfSeq.Extension200 - tp2Buffer;

                // Sanity-Cap: HTF-Ext1618 darf LTF-Ext1618 (Distanz vom Entry) nicht um Faktor > 5 ueberschreiten.
                // Schuetzt vor kaputten HTF-Sequenzen (z.B. wenn ein W1-Pivot durch fehlende Daten extrem weit liegt).
                var ltfTp1Distance = Math.Abs(ltfTp1 - entry);
                var htfTp1Distance = Math.Abs(htfTp1 - entry);
                if (ltfTp1Distance > 0m && htfTp1Distance > ltfTp1Distance * 5m)
                {
                    var capped = ltfTp1Distance * 3m;
                    htfTp1 = tradeIsLong ? entry + capped : entry - capped;
                    var ltfTp2Distance = Math.Abs(ltfTp2 - entry);
                    var htfTp2Distance = Math.Abs(htfTp2 - entry);
                    if (htfTp2Distance > ltfTp2Distance * 5m)
                    {
                        var cappedTp2 = ltfTp2Distance * 3m;
                        htfTp2 = tradeIsLong ? entry + cappedTp2 : entry - cappedTp2;
                    }
                    LastStatus = $"AsymCrv-SanityCap: HTF-TP > 5× LTF — gecappt auf 3× LTF";
                }

                tp1 = htfTp1;
                tp2 = htfTp2;
                tpSourceTimeframe = gklHit.Tf;
            }
        }

        // Sanity: tp1 muss VOR tp2 liegen.
        var tp1PastTp2 = tradeIsLong ? tp1 > tp2 : tp1 < tp2;
        if (tp1PastTp2)
            tp1 = tradeIsLong ? Math.Min(tp1, tp2) : Math.Max(tp1, tp2);

        // SK-Buch S.13: MinRRR = 1:1 (relativ zum TP2-Ziellevel).
        var tpDist = Math.Abs(tp2 - entry);
        var rrr = slDistance > 0 ? tpDist / slDistance : 0m;
        const decimal minRrr = 1.0m;
        if (rrr < minRrr)
            return Blocked(navTf, $"RRR zu klein ({rrr:F1}:1 < {minRrr:F1}:1) — TP2 zu nah am Entry", BingXBot.Core.Diagnostics.RejectionReasons.RrrTooSmall);

        // ───────────────────────────────────────────────────────────
        // Buch-Checkliste (9 Schritte) — alle Punkte sind jetzt inline in Evaluate umgesetzt:
        //   1. News-Check       → Early-Gate ganz oben in Evaluate (siehe dort)
        //   2. Top-Down-GKL     → gklHit via MultiTfGklDetector (siehe oben)
        //   3. Sequenz-Mapping  → navMachine.ToSequence + State-Gate (navMachine.State != Aktiviert)
        //   4. Confluence-Check → scorer + minScore-Vergleich (siehe oben)
        //   5. Einstieg         → entry/isAdditionalEntry, Conservative-Mode erzwingt LTF-Reversal
        //   6. Lot-Size         → RiskManager (nicht Strategy-Verantwortung; Risk-Cap in RiskSettings)
        //   7. Stop-Loss        → sl unter Point0 + Buffer, Seitenprüfung implizit durch RRR-Check
        //   8. Ziele            → tp1 (1.618) + tp2 (2.000 + Buffer), Sanity tp1<tp2 oben erzwungen
        //   9. Breakeven-Arm    → NavPointA im SignalResult (ProtectTrade nutzt das beim A-Bruch)
        //
        // HISTORIE: Die frühere Masterclass-Pipeline (`SkMasterclassPipeline` + 9 Step-Klassen) war
        // ein nachgelagerter Validator, der nur die inline bereits geprüften Werte noch einmal
        // durch ein Dictionary reichte. Sie wurde am 24.04.2026 entfernt — der Orchestrator hatte
        // einen Bug (`Run()` ignorierte das vorbefüllte Data-Dict → Step3 scheiterte immer an
        // "Keine Navigator-Sequenz gemappt"). Die 9 Schritte sind durch die Inline-Checks
        // vollständig abgedeckt und brauchen keinen redundanten Layer.

        // ───────────────────────────────────────────────────────────
        // Signal erstellen
        // ───────────────────────────────────────────────────────────
        // BUCH-ONLY (24.04.2026): Signal-Cooldown + Dedup-Tracking sind entfernt. Invalidation
        // am Point0 und Navigator-Sequenz-Promote/Reset via StateMachine reichen als Dedup.

        // Flag-Setzen entsprechend dem gewählten Entry-Level. CorrectionZone-Entries (SucheB)
        // verändern den Staffel-State nicht — siehe Kommentar oben bei der Multi-Entry-Logik.
        if (!isCorrectionZoneEntry)
        {
            if (isAdditionalEntry) _triggeredAdditional = true;
            else _triggeredPrimary = true;
        }

        var entryLabel = isCorrectionZoneEntry
            ? " [Conservative SucheB]"
            : isAdditionalEntry ? " [Additional 66.7%]" : " [Primary 50%]";
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

        // Sequenz-ID inkl. Navigator-TF-Suffix, damit D1-Long und 5m-Long parallel möglich sind.
        // Conservative-SucheB hat ein eigenes Suffix, damit ein späterer Primary-Entry nach
        // Promotion zu Aktiviert nicht als Re-Reconciliation derselben Order gesehen wird.
        var tick = Math.Max(currentPrice * 0.0001m, 1e-8m);
        decimal RoundToTick(decimal v) => Math.Round(v / tick) * tick;
        var entrySuffix = isCorrectionZoneEntry ? "_CzCons" : isAdditionalEntry ? "_Add" : "_Prim";
        var seqId = $"{context.Symbol}_{navTf}_{RoundToTick(navSeq.Point0.Price):G8}_{RoundToTick(navSeq.PointA.Price):G8}{entrySuffix}";

        // v1.5.2 Phase 4 — Erfolgsentscheidung im Decision-Trail.
        LastEvaluationDecision = new BingXBot.Core.Diagnostics.EvaluationDecision(
            Symbol: context.Symbol,
            Tf: navTf,
            UtcTimestamp: DateTime.UtcNow,
            SequenceState: navMachine.State.ToString(),
            Point0: navMachine.Point0,
            PointA: navMachine.PointA,
            PointB: navMachine.LockedB,
            Triggered: true,
            RejectionReason: null,
            ConfluenceScore: score,
            ConfluenceCategories: reasons,
            HardFiltersFailed: Array.Empty<string>());

        return new SignalResult(
            side, confidence,
            EntryPrice: entry,
            StopLoss: sl,
            TakeProfit: tp1,
            Reason: reasonText,
            TakeProfit2: tp2,
            ConfluenceScore: score,
            // Conservative-SucheB-Entry feuert am bestätigten Reversal → Market-Order;
            // klassische Fib-Entries warten weiter via Limit auf das Retracement-Level.
            PreferLimitOrder: !isCorrectionZoneEntry,
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
                    : null,
            // v1.5.0 Phase 2 — Asymmetrisches CRV: TF-Quelle der TPs (W1 oder D1) für UI-Badge.
            TpSourceTimeframe: tpSourceTimeframe);
    }

    /// <summary>
    /// v1.5.0 Phase 2 — Helper fuer asymmetrisches CRV: baut die HTF-Primary-Sequenz aus
    /// W1- oder D1-Candles je nach GKL-Treffer-TF. Liefert null bei nicht aktivierten / falsch
    /// gerichteten Sequenzen — Aufrufer faellt dann auf LTF-Ziele zurueck.
    /// </summary>
    private static Sequence? TryBuildHtfPrimarySequence(
        TimeFrame gklTf,
        IReadOnlyList<Candle>? weekly,
        IReadOnlyList<Candle>? daily,
        ScannerSettings? scannerSettings,
        decimal currentPrice,
        bool tradeIsLong)
    {
        var htfCandles = gklTf switch
        {
            TimeFrame.W1 => weekly,
            TimeFrame.D1 => daily,
            _ => null,
        };
        if (htfCandles == null || htfCandles.Count < 30) return null;

        var atrPercent = CalculateAtrPercent(htfCandles, currentPrice, gklTf);
        var impulseMul = GetAtrMultiplier(scannerSettings, gklTf, impulse: true, fallback: 1.0m);
        var corrMul = GetAtrMultiplier(scannerSettings, gklTf, impulse: false, fallback: 1.5m);
        var minPoint0 = GetMinPoint0Candles(scannerSettings, gklTf);
        var htfBosAnchor = Math.Max(2, scannerSettings?.BosAnchorSwingStrength ?? 5);
        var htfBosCloseBreak = scannerSettings?.RequireBosCloseBreak ?? true;
        var htfBosLeft = scannerSettings?.BosAnchorLeftBars ?? 0;
        var htfBosRight = scannerSettings?.BosAnchorRightBars ?? 0;
        var htfEnableBiasFlip = scannerSettings?.EnableBiasFlip ?? true;

        var (htfPrimary, _, _) = SequenceStateMachine.FromCandlesBoth(
            htfCandles, atrPercent * impulseMul, atrPercent * corrMul, 0.382m, 0.786m, minPoint0,
            enableBiasFlip: htfEnableBiasFlip,
            bosRequireCloseBreak: htfBosCloseBreak,
            bosAnchorSwingStrength: htfBosAnchor,
            bosAnchorLeftBars: htfBosLeft,
            bosAnchorRightBars: htfBosRight);

        if (htfPrimary == null) return null;
        // Richtung muss zum geplanten Trade passen — sonst sind die Extensions in die falsche Richtung berechnet.
        if (htfPrimary.IsLong != tradeIsLong) return null;
        // State muss mindestens Aktiviert sein, sonst sind Ext1618/Ext200 unzuverlaessig.
        if (htfPrimary.State < SmState.Aktiviert) return null;

        return htfPrimary.ToSequence(htfCandles);
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
    /// Multi-TF Standalone Filter-TF-Staffel (siehe Plan Tabelle Abschnitt 1):
    /// D1→H4, H4→H1, H1→M15, M15→M5. Buch-only: Liefert die nächst-tiefere TF
    /// für MTA-Confluence-Bewertung (kein ChoCH-Filter mehr seit Buch-Only Strip Phase 2).
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
        // Strukturpunkte §1+§3: BOS-Anker + Close/Docht-Break-Setting identisch zum Nav-Call durchreichen,
        // damit das HTF-Gate weder asymmetrisch strenger noch loser als der Navigator läuft.
        var htfBosAnchor = Math.Max(2, scannerSettings?.BosAnchorSwingStrength ?? 5);
        var htfBosCloseBreak = scannerSettings?.RequireBosCloseBreak ?? true;
        var htfBosLeft = scannerSettings?.BosAnchorLeftBars ?? 0;
        var htfBosRight = scannerSettings?.BosAnchorRightBars ?? 0;
        // 04.05.2026: EnableBiasFlip muss zwischen Navigator-Call und HTF-Calls konsistent sein,
        // sonst sieht das MTA-Gate eine andere Sequenz-Richtung als der Navigator.
        var htfEnableBiasFlip = scannerSettings?.EnableBiasFlip ?? true;

        var (htfPrimary, _, _) = SequenceStateMachine.FromCandlesBoth(
            htfCandles, atrPercent * impulseMul, atrPercent * corrMul, 0.382m, 0.786m, minPoint0,
            enableBiasFlip: htfEnableBiasFlip,
            bosRequireCloseBreak: htfBosCloseBreak,
            bosAnchorSwingStrength: htfBosAnchor,
            bosAnchorLeftBars: htfBosLeft,
            bosAnchorRightBars: htfBosRight);
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
        // Strukturpunkte §1+§3: BOS-Anker + Close/Docht-Break-Setting identisch zum Nav-Call durchreichen,
        // damit der MTA-Zielzonen-Guard zur selben Strukturbruch-Definition kommt.
        var htfBosAnchor = Math.Max(2, scannerSettings?.BosAnchorSwingStrength ?? 5);
        var htfBosCloseBreak = scannerSettings?.RequireBosCloseBreak ?? true;
        var htfBosLeft = scannerSettings?.BosAnchorLeftBars ?? 0;
        var htfBosRight = scannerSettings?.BosAnchorRightBars ?? 0;
        // 04.05.2026: EnableBiasFlip muss zwischen Navigator-Call und HTF-Calls konsistent sein.
        var htfEnableBiasFlip = scannerSettings?.EnableBiasFlip ?? true;

        var (htfPrimary, _, _) = SequenceStateMachine.FromCandlesBoth(
            htfCandles, atrPercent * impulseMul, atrPercent * corrMul, 0.382m, 0.786m, minPoint0,
            enableBiasFlip: htfEnableBiasFlip,
            bosRequireCloseBreak: htfBosCloseBreak,
            bosAnchorSwingStrength: htfBosAnchor,
            bosAnchorLeftBars: htfBosLeft,
            bosAnchorRightBars: htfBosRight);
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

    private SignalResult Blocked(TimeFrame navTf, string reason, string rejectionCode = BingXBot.Core.Diagnostics.RejectionReasons.Other)
    {
        var ampelSummary = string.Join("|", AmpelStatus.Select(kv => $"{kv.Key}:{kv.Value}"));
        LastStatus = $"[{ampelSummary}] {reason}";
        // v1.5.2 Phase 4 — Decision-Trail-Eintrag fuer Reject-Pfad. Publish-Pfad uebernimmt
        // der Aufrufer (TradingServiceBase) — die Strategy haelt nur die letzte Entscheidung.
        LastEvaluationDecision = new BingXBot.Core.Diagnostics.EvaluationDecision(
            Symbol: _lastEvalSymbol ?? "",
            Tf: navTf,
            UtcTimestamp: DateTime.UtcNow,
            SequenceState: _lastEvalSequenceState ?? "Unknown",
            Point0: _lastEvalPoint0,
            PointA: _lastEvalPointA,
            PointB: _lastEvalPointB,
            Triggered: false,
            RejectionReason: rejectionCode,
            ConfluenceScore: _lastEvalScore,
            ConfluenceCategories: _lastEvalCategories ?? Array.Empty<string>(),
            HardFiltersFailed: new[] { rejectionCode });
        return new SignalResult(Signal.None, 0m, null, null, null, LastStatus);
    }

    // v1.5.2 Phase 4 — Eval-Kontext fuer Decision-Trail-Lookup (gesetzt am Evaluate-Anfang).
    private string? _lastEvalSymbol;
    private string? _lastEvalSequenceState;
    private decimal? _lastEvalPoint0;
    private decimal? _lastEvalPointA;
    private decimal? _lastEvalPointB;
    private int _lastEvalScore;
    private IReadOnlyList<string>? _lastEvalCategories;
}
