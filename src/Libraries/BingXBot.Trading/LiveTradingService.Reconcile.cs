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
                    await ReconcilePositionsAsync(ct).ConfigureAwait(false);
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
    /// 3) Orphan-Signale bereinigen, Unmanaged-Positionen loggen.
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

        var actions = PositionDriftAnalyzer.Analyze(
            positions,
            botKeys,
            pendingSymbolSides,
            graceWindow: TimeSpan.FromSeconds(90),
            signalCreatedAt: _signalCreatedAt);

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
            }
        }
    }
}
