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
    /// (oder Stage-3-Retry den 30s-Timeout ueberschritten hat). Nutzt das Original-Signal
    /// (TakeProfit + TakeProfit2) und die echte Position-Quantity von BingX.
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
            await PlaceTpLimitOrdersAfterFillAsync(action.Symbol, action.Side, pos.Quantity, signal).ConfigureAwait(false);
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Reconcile",
                $"{LogPrefix}{action.Symbol} {action.Side}: Missing-TP → re-placed (Qty={pos.Quantity:F8}, TP1={signal.TakeProfit:F8}, TP2={signal.TakeProfit2?.ToString("F8") ?? "---"})",
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
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Reconcile",
                $"{LogPrefix}{action.Symbol} {action.Side}: Missing-SL erkannt, ABER kein Signal-SL bekannt — manueller Eingriff noetig (Position UNGESCHUETZT!)",
                action.Symbol));
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
