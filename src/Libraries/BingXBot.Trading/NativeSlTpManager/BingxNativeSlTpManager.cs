using BingXBot.Contracts.Dto;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Core.Services;

namespace BingXBot.Trading.NativeSlTpManager;

/// <summary>
/// Phase 18 / H7 — Composition-Extraktion aus <see cref="LiveTradingService"/>.SlTpManager.cs.
/// Konkrete Implementation gegen <see cref="IExchangeClient"/>.
///
/// Logik vorher in <c>LiveTradingService.CancelNativeSlTpOrdersAsync</c> +
/// <c>OnStopLossAdjustedAsync</c> — beide nun hier gekapselt; LiveTradingService delegiert.
/// </summary>
public sealed class BingxNativeSlTpManager : INativeSlTpManager
{
    private readonly IExchangeClient _restClient;
    private readonly BotEventBus _eventBus;
    private readonly bool _enableDesktopNotifications;

    public BingxNativeSlTpManager(IExchangeClient restClient, BotEventBus eventBus, bool enableDesktopNotifications)
    {
        _restClient = restClient;
        _eventBus = eventBus;
        _enableDesktopNotifications = enableDesktopNotifications;
    }

    public async Task CancelNativeSlTpOrdersAsync(string symbol, Side? originalPositionSide = null)
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

    public async Task UpdateNativeStopLossAsync(string symbol, Side side, decimal newStopLoss)
    {
        // Snapshot-Report-Fix Befund 3 / A0.6 — Sanity-Guard:
        // Validierung mit BE/Partial/Runner=null fuehrt zu Reject sobald SL falsch herum vom Entry liegt.
        // Im BE/Trail-Pfad rufen die Caller (TradingServiceBase / LiveTradingManager) den Validator
        // bereits mit den korrekten Flags auf — hier passiert eine zusaetzliche Floor-Pruefung ohne
        // Entry-Wissen. Da der Manager keinen Entry-Preis kennt, kann er nur Sentinel/Negative-Werte
        // ablehnen.
        if (newStopLoss <= 0m)
        {
            _eventBus.PublishLog(new LogEntry(DateTime.UtcNow, LogLevel.Error, "Trade",
                $"LIVE: {symbol} SL-Update abgelehnt — newStopLoss <= 0 ({newStopLoss}). Sentinel-Wert, kein Push.",
                symbol));
            return;
        }

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
                    if (_enableDesktopNotifications)
                        _eventBus.PublishNotification("SL-Update FEHLT", $"{symbol}: Nativer SL nicht aktualisiert!");
                }
            }
        }
    }
}
