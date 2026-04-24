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
    /// Cancelt alle nativen SL/TP-Orders (STOP_MARKET + TAKE_PROFIT_MARKET) für ein Symbol.
    /// Muss VOR Position-Close aufgerufen werden, damit keine Ghost-Orders übrigbleiben.
    /// </summary>
    private async Task CancelNativeSlTpOrdersAsync(string symbol)
    {
        try
        {
            var openOrders = await _restClient.GetOpenOrdersAsync(symbol).ConfigureAwait(false);
            foreach (var order in openOrders)
            {
                if (order.Type is OrderType.StopMarket or OrderType.TakeProfitMarket or OrderType.TakeProfitLimit)
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
