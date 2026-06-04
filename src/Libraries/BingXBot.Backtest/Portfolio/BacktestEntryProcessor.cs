using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Backtest.Simulation;
using Microsoft.Extensions.Logging;

namespace BingXBot.Backtest.Portfolio;

/// <summary>
/// Extrahierte Entry-Logik (Trade-Eroeffnung + Close-Signale) aus <c>BacktestEngine.RunAsync</c>.
/// Reiner Refactor — wortgleicher Block, damit Single- und Portfolio-Backtest dieselbe
/// Entry-Verarbeitung teilen. Keine Verhaltensaenderung.
/// </summary>
/// <remarks>
/// Der <c>riskContext</c>-Bau bleibt bewusst im Aufrufer (er braucht viele loop-lokale Variablen);
/// der fertige <see cref="MarketContext"/> wird hier nur noch durchgereicht.
/// </remarks>
internal static class BacktestEntryProcessor
{
    /// <summary>
    /// Fuehrt einen Trade aus (Long/Short via RiskManager + PlaceOrder + Exit-State-Tracking)
    /// bzw. schliesst Positionen bei Close-Signalen.
    /// </summary>
    public static async Task ProcessEntryAsync(
        SimulatedExchange simExchange,
        IRiskManager riskManager,
        SignalResult signal,
        MarketContext riskContext,
        string symbol,
        Candle currentCandle,
        Dictionary<string, SignalResult> positionSignals,
        Dictionary<string, BacktestExitState> exitTracking,
        IReadOnlyList<Position> positions,
        ILogger logger)
    {
        // Trade ausführen wenn Signal (SK-Buch: keine Regime-Filter, SK hat eigene Workflow-Regeln)
        if (signal.Signal is Signal.Long or Signal.Short)
        {
            var riskCheck = riskManager.ValidateTrade(signal, riskContext);
            if (riskCheck.IsAllowed && riskCheck.AdjustedPositionSize > 0)
            {
                var side = signal.Signal == Signal.Long ? Side.Buy : Side.Sell;
                try
                {
                    var order = await simExchange.PlaceOrderAsync(new OrderRequest(
                        symbol, side, OrderType.Market, riskCheck.AdjustedPositionSize)).ConfigureAwait(false);

                    // Nur SL/TP-Tracking speichern wenn Order tatsaechlich gefuellt wurde
                    if (order.Status == OrderStatus.Filled)
                    {
                        var key = $"{symbol}_{side}";
                        positionSignals[key] = signal;

                        // Exit State erstellen (SK-Buch: Partial Close 50/50 + A-Bruch-BE + Time-Exit)
                        exitTracking[key] = new BacktestExitState
                        {
                            EntryPrice = order.Price,
                            OriginalQuantity = riskCheck.AdjustedPositionSize,
                            EntryTime = currentCandle.CloseTime,
                            Tp2 = signal.TakeProfit2,
                            NavPointA = signal.NavPointA ?? 0m,
                            RunnerAtrBase = signal.EntryAtr ?? 0m,
                        };
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Order fehlgeschlagen: {Error}", ex.Message);
                }
            }
        }
        else if (signal.Signal is Signal.CloseLong)
        {
            if (positions.Any(p => p.Side == Side.Buy))
            {
                await simExchange.ClosePositionAsync(symbol, Side.Buy).ConfigureAwait(false);
                positionSignals.Remove($"{symbol}_{Side.Buy}");
                exitTracking.Remove($"{symbol}_{Side.Buy}");
            }
        }
        else if (signal.Signal is Signal.CloseShort)
        {
            if (positions.Any(p => p.Side == Side.Sell))
            {
                await simExchange.ClosePositionAsync(symbol, Side.Sell).ConfigureAwait(false);
                positionSignals.Remove($"{symbol}_{Side.Sell}");
                exitTracking.Remove($"{symbol}_{Side.Sell}");
            }
        }
    }
}
