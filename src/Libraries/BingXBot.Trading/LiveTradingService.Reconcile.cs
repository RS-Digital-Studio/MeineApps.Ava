using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Trading.Reconciliation;

namespace BingXBot.Trading;

// Partial fuer den Reconcile-Loop (P0-1, 24.04.2026).
// Zweck: Bot-State gegen Exchange-Realitaet abgleichen. Schuetzt gegen WS-Reconnect-Luecken,
// Pi-Crashes und manuelle User-Eingriffe auf BingX.
//
// Split-Teil von LiveTradingService. Kein Verhaltensunterschied zur monolithischen Datei —
// Partial Class ist reines File-Organization, die IL-Ausgabe ist identisch. Ziel: die
// Hauptdatei LiveTradingService.cs unter 1500 Zeilen halten (war nach Audit bei 1777 Zeilen).
public partial class LiveTradingService
{
    /// <summary>
    /// Reconcile-Intervall (60 s). Schneller als Drift-Szenarien normalerweise eskalieren,
    /// langsamer als Rate-Limits vertragen.
    /// </summary>
    private const int ReconcileIntervalSeconds = 60;

    private async Task ReconcileLoopAsync(CancellationToken ct)
    {
        // Initial-Delay: 30 s, damit nach Engine-Start genug Zeit fuer erste Scans+Position-Opens ist.
        try { await Task.Delay(30_000, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_isPaused)
                {
                    await ReconcilePositionsAsync(ct).ConfigureAwait(false);
                    // 04.05.2026 — Stale Pending-Limit-Orders mit Time-Expiry cancel (Default 6h).
                    // Schützt gegen "Symbol aus Top-100 gefallen, Pending hängt tagelang" — siehe
                    // RiskSettings.PendingLimitOrderMaxAgeHours.
                    // Snapshot-Report-Fix Befund 3 / A0.7 — zusaetzlich OpenOrders durchreichen,
                    // damit der Cleanup Recovery-Pending-Orders die auf BingX nicht mehr existieren
                    // sofort entfernt (statt 6h zu warten).
                    IReadOnlyList<Order>? openOrdersForPending = null;
                    try { openOrdersForPending = await _restClient.GetOpenOrdersAsync().ConfigureAwait(false); }
                    catch { /* best-effort — fallback auf Time-Expiry-only */ }
                    await CancelExpiredPendingLimitOrdersAsync(openOrdersForPending).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Engine",
                    $"Reconcile-Loop-Fehler: {ex.Message} — naechster Versuch in {ReconcileIntervalSeconds}s"));
            }

            try { await Task.Delay(ReconcileIntervalSeconds * 1000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Einmaliger Reconcile-Durchlauf:
    /// 1) Exchange-Positionen abrufen,
    /// 2) Drift gegen <see cref="TradingServiceBase._positionSignals"/> analysieren,
    /// 3) Orphan-Signale bereinigen, Unmanaged-Positionen loggen,
    /// 4) v1.5.1 Phase 3 — Missing-Stop-Loss-Detection (Re-Place wenn Signal-SL bekannt).
    /// Internal fuer Testbarkeit (InternalsVisibleTo=BingXBot.Tests).
    /// </summary>
    internal async Task ReconcilePositionsAsync(CancellationToken ct)
    {
        var positions = await _restClient.GetPositionsAsync(ct).ConfigureAwait(false);

        // Snapshot der Bot-Keys (ConcurrentDictionary.Keys ist konsistent, nicht blockierend).
        var botKeys = _positionSignals.Keys.ToArray();

        // Pending-Symbol/Side — wenn Limit-Entry noch nicht gefuellt ist, ist "keine Position" OK.
        var pendingSymbolSides = _pendingLimitOrders.Values
            .Select(v => (v.Symbol, v.IsLong ? Side.Buy : Side.Sell))
            .ToHashSet();

        // v1.5.1 Phase 3 — Open-Orders fuer Missing-Stop-Loss-Detection abrufen.
        IReadOnlyList<Order>? openOrders = null;
        try
        {
            openOrders = await _restClient.GetOpenOrdersAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Reconcile",
                $"OpenOrders-Abruf fehlgeschlagen — Missing-Stop-Detection in dieser Iteration uebersprungen: {ex.Message}"));
        }

        // PositionOpenedAt-Lookup (fuer 30-s-Grace-Window).
        var positionOpenedAt = (IReadOnlyDictionary<string, DateTime>)_positionOpenTimes;

        // NF1 Fix — Set der Signale die einen TakeProfit erwarten (fuer MissingTakeProfit-Detection).
        // Ohne dieses Set war der DriftKind.MissingTakeProfit-Branch im Analyzer toter Code; Bot
        // konnte nach Restart Positionen ohne TP-Order behalten und lief bis SL/manuellem Close.
        var signalsExpectingTp = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kv in _positionSignals)
        {
            var sig = kv.Value;
            if (sig.TakeProfit.HasValue && sig.TakeProfit.Value > 0)
                signalsExpectingTp.Add(kv.Key);
        }

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            botKeys,
            pendingSymbolSides,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: _signalCreatedAt,
            openOrders: openOrders,
            positionOpenedAt: positionOpenedAt,
            missingStopGraceWindow: TimeSpan.FromSeconds(30),
            signalsExpectingTakeProfit: signalsExpectingTp);

        // Snapshot-Report-Fix Befund 3 / A0.5: Stale-ExitStates aufraeumen — Eintraege die weder
        // einer offenen BingX-Position noch einer Pending-Limit-Order noch einer Reduce-Only-Order
        // zugeordnet sind. Tritt auf wenn:
        //  - User manuell auf BingX schliesst → Position weg, ExitState bleibt im Bot.
        //  - Liquidation/Funding-Strafe → Position weg, ExitState bleibt im Bot.
        //  - Recovery-Pfad legt einen ExitState ohne echte Position an (Quantity=0).
        // Im Snapshot vom 2026-05-17 lagen 11 ExitStates seit 22-30 Tagen im Bot, obwohl BingX
        // sie laengst nicht mehr fuehrt — Folge: Positionen werden im Dashboard angezeigt, der
        // Reconcile-Drift-Analyzer rauscht im Log, Stats sind falsch.
        await CleanupStaleExitStatesAsync(positions, openOrders, ct).ConfigureAwait(false);

        // Snapshot-Report-Fix 2026-05-17 (TP-Duplicate-Loop, LTC/SUI/ETHFI): doppelte Reduce-Only-
        // Limit-Orders gleichen Preises pro (Symbol, Side) cancellen. Entstehen wenn der
        // Missing-TP-Re-Place vor dem Idempotenz-Fix mehrfach lief und jeden Tick ein neues
        // TP1-Limit dazustellte (Position-Reduce-Only-Pool wird ueber-belegt → naechstes TP2
        // failed mit "insufficient amount" → Endlos-Spam). Cleanup ist defensiv: behaelt immer
        // den juengsten Eintrag pro (Symbol, Side, Preis-Bucket).
        if (openOrders != null && openOrders.Count > 0)
            await CleanupDuplicateReduceOnlyOrdersAsync(openOrders).ConfigureAwait(false);

        if (actions.Count == 0) return;

        foreach (var action in actions)
        {
            var key = $"{action.Symbol}_{action.Side}";
            switch (action.Kind)
            {
                case PositionDriftAnalyzer.DriftKind.OrphanSignalRemove:
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Reconcile",
                        $"{LogPrefix}{action.Symbol} {action.Side}: Orphan-Signal entfernt — {action.Reason}",
                        action.Symbol));
                    RemoveSignalByKey(key);
                    break;

                case PositionDriftAnalyzer.DriftKind.UnmanagedPositionWarning:
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Reconcile",
                        $"{LogPrefix}{action.Symbol} {action.Side}: {action.Reason}",
                        action.Symbol));
                    break;

                case PositionDriftAnalyzer.DriftKind.MissingStopLoss:
                    await ReplaceMissingStopAsync(action, key).ConfigureAwait(false);
                    break;

                case PositionDriftAnalyzer.DriftKind.MissingTakeProfit:
                    await ReplaceMissingTakeProfitAsync(action, key, positions).ConfigureAwait(false);
                    break;
            }
        }
    }

    /// <summary>
    /// NF1 Fix — Re-platziert fehlende TP-Reduce-Only-LIMIT-Orders fuer eine offene Position.
    /// Tritt auf wenn Bot zwischen Limit-Entry-Fill und PlaceTpLimitOrdersAfterFillAsync crasht
    /// (oder Stage-3-Retry den 30s-Timeout ueberschritten hat).
    ///
    /// Snapshot-Report-Fix 2026-05-17 (TP-Duplicate-Loop): vorher rief diese Methode
    /// <c>PlaceTpLimitOrdersAfterFillAsync</c> auf, das IMMER beide TPs neu setzte. Wenn TP1
    /// erfolgreich plaziert, TP2 fehlschlaegt (z.B. "available amount insufficient" weil ein
    /// TP1-Duplikat den Reduce-Only-Pool fast voll macht), feuert der naechste Reconcile-Tick
    /// die gleiche Logik nochmal → TP1-Duplikat-2, TP2 fails wieder, usw. Bei LTC/SUI wurden so
    /// 3 TP1-Duplikate pro Position aufgebaut und ~50 Trade-Errors pro Minute erzeugt.
    ///
    /// Idempotente Variante: Pruefe EXAKT pro TP-Seite ob die OrderId aus dem ExitState noch
    /// in den OpenOrders existiert. Nur fehlende Seite re-placen, mit der Restmenge die nach
    /// der bereits existierenden TP-Seite uebrig bleibt.
    /// </summary>
    private async Task ReplaceMissingTakeProfitAsync(PositionDriftAnalyzer.DriftAction action, string posKey,
        IReadOnlyList<Position> positions)
    {
        if (!_positionSignals.TryGetValue(posKey, out var signal) || !signal.TakeProfit.HasValue)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Reconcile",
                $"{LogPrefix}{action.Symbol} {action.Side}: Missing-TP gemeldet, aber kein Signal-TP bekannt — uebersprungen",
                action.Symbol));
            return;
        }

        var pos = positions.FirstOrDefault(p => p.Symbol == action.Symbol && p.Side == action.Side && p.Quantity > 0);
        if (pos == null)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Reconcile",
                $"{LogPrefix}{action.Symbol} {action.Side}: Missing-TP gemeldet, aber Position nicht mehr offen — uebersprungen",
                action.Symbol));
            return;
        }

        try
        {
            // Aktuelle Open-Orders pro Symbol ziehen, um zu sehen welche TP-Seiten bereits leben.
            IReadOnlyList<Order> openOrders;
            try
            {
                openOrders = await _restClient.GetOpenOrdersAsync(action.Symbol).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Reconcile",
                    $"{LogPrefix}{action.Symbol} {action.Side}: OpenOrders-Probe vor TP-Re-Place fehlgeschlagen ({ex.Message}) — uebersprungen, naechster Tick versucht es",
                    action.Symbol));
                return;
            }

            _exitStates.TryGetValue(posKey, out var exitState);
            var closeSide = action.Side == Side.Buy ? Side.Sell : Side.Buy;

            // Pro Position kann es mehrere Reduce-Only-LIMITs geben (idealerweise TP1 + TP2).
            // Wir matchen zwei Wege: per OrderId aus ExitState (zuverlaessig) oder per Preis-Naehe
            // (Fallback, wenn ExitState-OrderId nicht mehr matched z.B. nach Re-Place).
            var tp1Price = signal.TakeProfit!.Value;
            var hasTp2Signal = signal.TakeProfit2.HasValue && signal.TakeProfit2.Value > 0
                              && signal.TakeProfit2.Value != tp1Price;
            decimal? tp2Price = hasTp2Signal ? signal.TakeProfit2 : null;

            bool tp1Alive = false, tp2Alive = false;
            foreach (var o in openOrders)
            {
                if (o.Side != closeSide) continue;
                if (o.Type != OrderType.Limit) continue;
                // ReduceOnly NICHT als Filter benutzen: im Hedge-Mode wird das Flag von BingX
                // nicht akzeptiert (PlaceTpReduceOnlyLimitAsync laesst es im Hedge-Mode weg) und
                // kommt beim Read als false zurueck. Im Hedge-Mode reicht (Symbol, Side=closeSide,
                // Type=Limit) als TP-Indikator, weil eine Long-Position andere Side+PositionSide hat.
                // Match auf ExitState-OrderId hat hoechste Prioritaet.
                if (exitState != null)
                {
                    if (!string.IsNullOrEmpty(exitState.Tp1LimitOrderId) && o.OrderId == exitState.Tp1LimitOrderId) { tp1Alive = true; continue; }
                    if (!string.IsNullOrEmpty(exitState.Tp2LimitOrderId) && o.OrderId == exitState.Tp2LimitOrderId) { tp2Alive = true; continue; }
                }
                // Fallback: Preis-Naehe (0.05 % Toleranz fuer Tick-Rounding).
                var p = o.Price;
                if (p <= 0) continue;
                if (Math.Abs(p - tp1Price) / tp1Price < 0.0005m) { tp1Alive = true; continue; }
                if (tp2Price.HasValue && Math.Abs(p - tp2Price.Value) / tp2Price.Value < 0.0005m) tp2Alive = true;
            }

            if (tp1Alive && (!hasTp2Signal || tp2Alive))
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Reconcile",
                    $"{LogPrefix}{action.Symbol} {action.Side}: Missing-TP gemeldet, aber TP-Orders existieren bereits ({(hasTp2Signal ? "TP1+TP2" : "TP1")}) — kein Re-Place",
                    action.Symbol));
                return;
            }

            // Mengen-Aufteilung wie in PlaceTpLimitOrdersAfterFillAsync, aber nur die FEHLENDE
            // Seite wird wirklich platziert. Wenn TP1 bereits liegt, kalkuliert sich die TP2-Qty
            // aus dem Rest (Position - Tp1-Reservierung).
            var tp1Ratio = _riskSettings.Tp1CloseRatio;
            var tp1Qty = hasTp2Signal
                ? Math.Round(pos.Quantity * tp1Ratio, 6)
                : Math.Round(pos.Quantity, 6);
            var tp2Qty = hasTp2Signal ? Math.Round(pos.Quantity - tp1Qty, 6) : 0m;

            string? placedTp1 = null, placedTp2 = null;
            if (!tp1Alive && tp1Qty > 0)
                placedTp1 = await PlaceTpWithRetryAsync(action.Symbol, action.Side, tp1Qty, tp1Price, "TP1 Re-Place").ConfigureAwait(false);
            if (hasTp2Signal && !tp2Alive && tp2Qty > 0)
                placedTp2 = await PlaceTpWithRetryAsync(action.Symbol, action.Side, tp2Qty, tp2Price!.Value, "TP2 Re-Place").ConfigureAwait(false);

            // ExitState mit den frischen OrderIds aktualisieren + synchron persistieren.
            if (exitState != null)
            {
                var mutated = false;
                if (!string.IsNullOrEmpty(placedTp1)) { exitState.Tp1LimitOrderId = placedTp1; mutated = true; }
                if (!string.IsNullOrEmpty(placedTp2)) { exitState.Tp2LimitOrderId = placedTp2; mutated = true; }
                if (mutated)
                    try { await PersistExitStatesAsync().ConfigureAwait(false); } catch { /* best-effort */ }
            }

            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Reconcile",
                $"{LogPrefix}{action.Symbol} {action.Side}: Missing-TP re-placed — TP1Alive={tp1Alive} TP2Alive={tp2Alive} → placedTp1={(placedTp1 != null)} placedTp2={(placedTp2 != null)} (Qty={pos.Quantity:F8})",
                action.Symbol));
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Reconcile",
                $"{LogPrefix}{action.Symbol} {action.Side}: TP-Re-Place fehlgeschlagen ({ex.Message}) — Position ohne TP bis naechster Reconcile",
                action.Symbol));
        }
    }

    /// <summary>
    /// Snapshot-Report-Fix 2026-05-17 — Duplikat-Reduce-Only-Limits pro (Symbol, Side, Preis)
    /// cancellen. Schuetzt vor dem TP-Duplicate-Loop, in dem das alte ReplaceMissingTakeProfit
    /// jeden Reconcile-Tick ein neues TP1-Limit auflegte und damit den Reduce-Only-Pool ueber-
    /// belegte. Behaelt pro Bucket den juengsten Eintrag (groesste OrderId).
    /// </summary>
    private async Task CleanupDuplicateReduceOnlyOrdersAsync(IReadOnlyList<Order> openOrders)
    {
        // Gruppieren nach (Symbol, Side, Preis-Bucket). Preis-Bucket = Preis auf 6 Nachkommastellen
        // gerundet, deckt typische Tick-Rounding-Toleranz ab.
        // ReduceOnly NICHT als Filter: im Hedge-Mode kommt das Flag immer als false zurueck
        // (BingX akzeptiert es dort nicht beim Place). Wir betrachten alle gleichpreisigen
        // Limit-Orders einer Side als Duplikate — die TP-Orders haben pro Position eindeutige
        // Preise (TP1 != TP2 != Entry-Limit).
        var groups = new Dictionary<(string Sym, Side Side, decimal Bucket), List<Order>>();
        foreach (var o in openOrders)
        {
            if (o.Type != OrderType.Limit) continue;
            if (o.Price <= 0) continue;
            var bucket = Math.Round(o.Price, 6);
            var key = (o.Symbol, o.Side, bucket);
            if (!groups.TryGetValue(key, out var list))
                groups[key] = list = new List<Order>();
            list.Add(o);
        }

        foreach (var ((sym, side, bucket), list) in groups)
        {
            if (list.Count <= 1) continue;
            // Behalten: juengster Eintrag (= groesste numerische OrderId, BingX vergibt monoton).
            // Cancel-Kandidaten: alle anderen.
            list.Sort((a, b) => string.CompareOrdinal(b.OrderId, a.OrderId));
            for (var i = 1; i < list.Count; i++)
            {
                var killId = list[i].OrderId;
                try
                {
                    await _restClient.CancelOrderAsync(killId, sym).ConfigureAwait(false);
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Reconcile",
                        $"{LogPrefix}{sym} {side}: Duplikat-Reduce-Only @{bucket} gecancelt (OrderId={killId}; behalten={list[0].OrderId})",
                        sym));

                    // ExitState-OrderIds nachpflegen, falls die OrderId dort referenziert war —
                    // sonst denkt der Bot beim naechsten Reconcile-Tick wieder TP fehlt.
                    var posKey = $"{sym}_{(side == Side.Sell ? Side.Buy : Side.Sell)}";
                    if (_exitStates.TryGetValue(posKey, out var es))
                    {
                        if (es.Tp1LimitOrderId == killId) es.Tp1LimitOrderId = list[0].OrderId;
                        else if (es.Tp2LimitOrderId == killId) es.Tp2LimitOrderId = list[0].OrderId;
                    }
                }
                catch (Exception ex)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Reconcile",
                        $"{LogPrefix}{sym} {side}: Duplikat-Cancel fehlgeschlagen ({killId}): {ex.Message}",
                        sym));
                }
            }
        }
    }

    /// <summary>
    /// Snapshot-Report-Fix Befund 3 / A0.5 — Stale-ExitStates aufraeumen.
    ///
    /// Definition "stale":
    ///   1. Keine offene BingX-Position mit (Symbol, Side) und Quantity &gt; 0.
    ///   2. Kein Pending-Limit-Entry fuer (Symbol, Side) in <see cref="_pendingLimitOrders"/>.
    ///   3. Kein Reduce-Only-Limit (Tp1/Tp2OrderId) in den aktuellen <paramref name="openOrders"/>.
    ///   4. Alter des Eintrags &gt;= Grace-Window (Recovery 1 h, normal 5 min).
    ///
    /// Wenn alle vier zutreffen, ist der ExitState verwaist und wird inkl. Signal entfernt.
    /// Persistiert danach den neuen Snapshot, damit Pi-Restart die geleerten States nicht
    /// wieder einliest.
    /// </summary>
    internal async Task CleanupStaleExitStatesAsync(
        IReadOnlyList<Position> positions,
        IReadOnlyList<Order>? openOrders,
        CancellationToken ct)
    {
        if (_exitStates.IsEmpty) return;

        var openOrderIds = openOrders == null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(openOrders.Select(o => o.OrderId).Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);

        var openPositionKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in positions)
        {
            if (p.Quantity > 0)
                openPositionKeys.Add($"{p.Symbol}_{p.Side}");
        }

        var pendingKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in _pendingLimitOrders.Values)
            pendingKeys.Add($"{v.Symbol}_{(v.IsLong ? Side.Buy : Side.Sell)}");

        var now = DateTime.UtcNow;
        var staleRecoveryAge = TimeSpan.FromHours(1);
        var staleNormalAge = TimeSpan.FromMinutes(5);
        var removed = 0;
        var staleKeys = new List<(string Key, string Reason)>();

        foreach (var kv in _exitStates)
        {
            ct.ThrowIfCancellationRequested();
            var state = kv.Value;
            var key = kv.Key;

            if (openPositionKeys.Contains(key)) continue;
            if (pendingKeys.Contains(key)) continue;
            if (!string.IsNullOrEmpty(state.Tp1LimitOrderId) && openOrderIds.Contains(state.Tp1LimitOrderId!)) continue;
            if (!string.IsNullOrEmpty(state.Tp2LimitOrderId) && openOrderIds.Contains(state.Tp2LimitOrderId!)) continue;

            var age = now - state.EntryTime;
            var threshold = state.IsRecovered ? staleRecoveryAge : staleNormalAge;
            if (age < threshold) continue;

            // OpenOrders konnten nicht abgerufen werden (Rate-Limit / Netzwerk) — wir koennten falsch-
            // positiv loeschen. In dem Fall nur Eintraege >= 24 h droppen (sehr konservativ).
            if (openOrders == null && age < TimeSpan.FromHours(24)) continue;

            var reasonBits = new List<string>();
            reasonBits.Add($"Age={age.TotalHours:F1}h");
            if (state.IsRecovered) reasonBits.Add("IsRecovered");
            if (state.OriginalQuantity == 0m) reasonBits.Add("Qty=0");
            staleKeys.Add((key, string.Join(",", reasonBits)));
        }

        foreach (var (key, reason) in staleKeys)
        {
            if (_exitStates.TryRemove(key, out var removedState))
            {
                _positionSignals.TryRemove(key, out _);
                OnSignalRemoved(key);
                removed++;
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Reconcile",
                    $"{LogPrefix}{removedState.Symbol} {removedState.Side}: Stale ExitState entfernt ({reason}, EntryTime={removedState.EntryTime:O}) — kein BingX-Match",
                    removedState.Symbol));
            }
        }

        if (removed > 0)
        {
            try { await PersistExitStatesAsync().ConfigureAwait(false); }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Reconcile",
                    $"{LogPrefix}ExitStates-Persist nach Stale-Cleanup fehlgeschlagen: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// v1.5.1 Phase 3 — Re-platziert den nativen SL fuer eine Position deren STOP_MARKET-Order
    /// auf BingX fehlt. Liest den SL-Wert aus dem Signal (<see cref="TradingServiceBase._positionSignals"/>).
    /// Wenn kein Signal-SL bekannt: Warning + kein Auto-Close (User-Eingriff erwartet,
    /// Auto-Close wuerde manuell gestartete Trades plattmachen).
    /// </summary>
    private async Task ReplaceMissingStopAsync(PositionDriftAnalyzer.DriftAction action, string posKey)
    {
        if (!_positionSignals.TryGetValue(posKey, out var signal) || signal.StopLoss is null)
        {
            // Kein bekannter Signal-SL (manuell gestarteter Trade oder Recovery ohne Signal-Rekonstruktion).
            // FRUEHER: nur Error-Log → die Echtgeld-Position blieb dauerhaft ungeschuetzt. Eine
            // ungeschuetzte Position ist das groessere Risiko als ein evtl. zu weiter Auto-SL → wir setzen
            // einen leverage-skalierten Notfall-SL (mind. 1.5 % vom Entry) und alarmieren den User.
            try
            {
                var positions = await _restClient.GetPositionsAsync().ConfigureAwait(false);
                var pos = positions.FirstOrDefault(p => p.Symbol == action.Symbol && p.Side == action.Side);
                if (pos is null || pos.Quantity <= 0 || pos.EntryPrice <= 0)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Reconcile",
                        $"{LogPrefix}{action.Symbol} {action.Side}: Missing-SL ohne Signal — Position nicht (mehr) abrufbar, kein Notfall-SL noetig",
                        action.Symbol));
                    return;
                }

                var fallbackPercent = Math.Max(0.015m, pos.Leverage > 0 ? 0.03m / pos.Leverage : 0.03m);
                var emergencySl = pos.Side == Side.Buy
                    ? pos.EntryPrice * (1m - fallbackPercent)
                    : pos.EntryPrice * (1m + fallbackPercent);

                await _restClient.SetPositionSlTpAsync(action.Symbol, action.Side, emergencySl, null).ConfigureAwait(false);

                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Reconcile",
                    $"{LogPrefix}{action.Symbol} {action.Side}: Missing-SL OHNE Signal — NOTFALL-SL @ {emergencySl:F8} gesetzt ({fallbackPercent:P1} vom Entry). Manuelle Pruefung empfohlen!",
                    action.Symbol));
                _eventBus.PublishNotification("Notfall-SL gesetzt",
                    $"{action.Symbol} {action.Side}: Position hatte keinen SL — Auto-SL @ {emergencySl:F4} platziert.");
            }
            catch (Exception ex)
            {
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Reconcile",
                    $"{LogPrefix}{action.Symbol} {action.Side}: Notfall-SL fehlgeschlagen ({ex.Message}) — Position UNGESCHUETZT bis naechster Reconcile!",
                    action.Symbol));
            }
            return;
        }

        try
        {
            await _restClient.SetPositionSlTpAsync(action.Symbol, action.Side, signal.StopLoss, signal.TakeProfit)
                .ConfigureAwait(false);
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Reconcile",
                $"{LogPrefix}{action.Symbol} {action.Side}: Missing-SL → re-placed @ {signal.StopLoss:F8}",
                action.Symbol));
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Reconcile",
                $"{LogPrefix}{action.Symbol} {action.Side}: SL-Re-Place fehlgeschlagen ({ex.Message}) — Position UNGESCHUETZT bis naechster Reconcile-Durchlauf!",
                action.Symbol));
        }
    }
}
