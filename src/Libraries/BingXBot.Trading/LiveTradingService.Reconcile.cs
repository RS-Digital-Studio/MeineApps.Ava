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
                    await CancelExpiredPendingLimitOrdersAsync().ConfigureAwait(false);
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

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            botKeys,
            pendingSymbolSides,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: _signalCreatedAt,
            openOrders: openOrders,
            positionOpenedAt: positionOpenedAt,
            missingStopGraceWindow: TimeSpan.FromSeconds(30));

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
