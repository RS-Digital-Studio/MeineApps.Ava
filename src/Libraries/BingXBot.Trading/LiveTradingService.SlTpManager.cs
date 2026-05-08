using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Trading;

// Partial fuer Native-SL/TP-Management auf BingX.
// Split-Teil von LiveTradingService (24.04.2026, P1-1 Gott-Klasse-Split).
//
// Enthaelt:
// - CancelNativeSlTpOrdersAsync: Alle STOP_MARKET + TAKE_PROFIT_MARKET-Orders fuer Symbol canceln
//   (wird VOR Position-Close aufgerufen damit keine Ghost-Orders uebrigbleiben).
// - OnStopLossAdjustedAsync: Native SL-Anpassung nach BE-Trigger, mit 3-Versuchs-Retry — bei Crash
//   muss der neue SL auf BingX sein, sonst traegt User Original-SL-Verlust.
public partial class LiveTradingService
{
    /// <summary>
    /// Cancelt alle SL/TP-Orders fuer ein Symbol. Muss VOR Position-Close aufgerufen werden,
    /// damit keine Ghost-Orders uebrigbleiben.
    ///
    /// v1.4.0 Phase 0.1 (Finding 0.1) — Filter erweitert:
    /// - Native: <see cref="OrderType.StopMarket"/>, <see cref="OrderType.TakeProfitMarket"/>,
    ///   <see cref="OrderType.TakeProfitLimit"/> (von <c>BingXRestClient.PlaceTpLimitOrderAsync</c> /
    ///   <c>SetPositionSlTpAsync</c> platziert).
    /// - Bot-platzierte TP-Reduce-Only-LIMITs: <see cref="OrderType.Limit"/> mit
    ///   <see cref="Order.ReduceOnly"/> true (von <c>PlaceTpReduceOnlyLimitAsync</c>).
    ///   Vor v1.4.0 wurden diese hier nicht erkannt → Ghost-Orders im BingX-Orderbuch nach
    ///   regulaerem Close (Open-Order-Limit aufgefressen, falsche Reconcile-Stats, im Worst-Case
    ///   Cross-Match mit neuer Position auf demselben Symbol+Side).
    /// </summary>
    /// <param name="symbol">Symbol der zu schliessenden Position.</param>
    /// <param name="originalPositionSide">
    /// Original-Side der zu schliessenden Position (Buy=Long, Sell=Short). Wird genutzt um
    /// Reduce-Only-Limits auf der Schliess-Seite zu filtern. Im Hedge-Mode kann dasselbe Symbol
    /// gleichzeitig Long+Short halten — dann sollen NUR die Reduce-Only-Limits der zu schliessenden
    /// Seite gecancelt werden, die der parallelen Gegenposition NICHT.
    /// Bei <c>null</c> (Notfall-Cleanup ohne Side-Kontext): alle Reduce-Only-Limits canceln.
    /// </param>
    private async Task CancelNativeSlTpOrdersAsync(string symbol, Side? originalPositionSide = null)
    {
        try
        {
            var openOrders = await _restClient.GetOpenOrdersAsync(symbol).ConfigureAwait(false);
            // Reduce-Only-Order = Schliess-Seite der Original-Position
            // (Long-Position → Sell-Order, Short-Position → Buy-Order).
            var roCloseSide = originalPositionSide.HasValue
                ? (originalPositionSide.Value == Side.Buy ? Side.Sell : Side.Buy)
                : (Side?)null;

            foreach (var order in openOrders)
            {
                var isNativeSlTp = order.Type is OrderType.StopMarket
                                              or OrderType.TakeProfitMarket
                                              or OrderType.TakeProfitLimit;

                var isBotTpReduceOnly = order.Type == OrderType.Limit
                    && order.ReduceOnly
                    && (roCloseSide == null || order.Side == roCloseSide.Value);

                if (isNativeSlTp || isBotTpReduceOnly)
                {
                    try { await _restClient.CancelOrderAsync(order.OrderId, symbol).ConfigureAwait(false); }
                    catch { /* Order möglicherweise bereits gecancelled */ }
                }
            }
        }
        catch (Exception ex)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Debug, "Trade",
                $"Native SL/TP-Cancel für {symbol} fehlgeschlagen: {ex.Message}", symbol));
        }
    }

    /// <summary>
    /// Aktualisiert den nativen SL auf BingX wenn SL angepasst wird (halbiert nach 4.1 oder BE nach 4.2).
    /// Mit 3 Retries — bei Crash muss der neue SL auf BingX sein, sonst trägt User Original-SL-Verlust.
    /// </summary>
    protected override async Task OnStopLossAdjustedAsync(string symbol, Side side, decimal newStopLoss)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await _restClient.SetPositionSlTpAsync(symbol, side, newStopLoss, null).ConfigureAwait(false);
                _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Info, "Trade",
                    $"LIVE: {symbol} Nativer SL aktualisiert: {newStopLoss:F8}", symbol));
                return;
            }
            catch (Exception ex)
            {
                if (attempt < 3)
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Warning, "Trade",
                        $"LIVE: {symbol} SL-Update Versuch {attempt}/3 fehlgeschlagen: {ex.Message} - Retry", symbol));
                    await Task.Delay(2000).ConfigureAwait(false);
                }
                else
                {
                    _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                        $"LIVE: {symbol} KRITISCH: SL-Update konnte nach 3 Versuchen nicht durchgeführt werden: {ex.Message}", symbol));
                    if (_botSettings.EnableDesktopNotifications)
                        _eventBus.PublishNotification("SL-Update FEHLT", $"{symbol}: Nativer SL nicht aktualisiert!");
                }
            }
        }
    }
}
