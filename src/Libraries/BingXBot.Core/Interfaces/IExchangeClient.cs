using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Core.Interfaces;

/// <summary>
/// Exchange-Abstraktion fuer Trading-Operationen. Einzige Grenze zwischen Trading-Services und
/// konkretem Exchange-SDK (BingX). FakeExchangeClient fuer Tests, BingXRestClient fuer Produktion.
/// </summary>
public interface IExchangeClient
{
    // ─────────── Market Data ───────────
    Task<IReadOnlyList<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, int limit);
    Task<IReadOnlyList<Ticker>> GetAllTickersAsync();
    Task<decimal> GetFundingRateAsync(string symbol);
    Task<IReadOnlyList<string>> GetAllSymbolsAsync();

    // ─────────── Account / Config ───────────
    Task<AccountInfo> GetAccountInfoAsync();
    Task<(decimal TakerRate, decimal MakerRate)> GetCommissionRateAsync();
    Task SetLeverageAsync(string symbol, int leverage, Side side);
    Task SetMarginTypeAsync(string symbol, MarginType marginType);
    Task<bool> IsHedgeModeAsync();
    Task<bool> SetHedgeModeAsync(bool enableHedge);
    Task SyncServerTimeAsync();
    Task InitializeSymbolInfoAsync();

    /// <summary>
    /// Prueft ob (quantity, price) die Min-Order-Anforderungen des Symbols erfuellt (MinQty + MinNotional).
    /// Wird vor TP-Mengen-Splits genutzt, um BingX-Rejects bei winzigen Teilmengen zu vermeiden
    /// (z.B. ETH-USDT Min-Qty 0.01 → ein 50/50-Split auf 0.005 ist ungueltig). Default-Impl gibt true
    /// zurueck (SimulatedExchange/Tests ohne echte Contract-Restriktionen).
    /// </summary>
    bool MeetsMinimumOrder(string symbol, decimal quantity, decimal price) => true;

    // ─────────── Positions ───────────
    Task<IReadOnlyList<Position>> GetPositionsAsync();
    Task<IReadOnlyList<Position>> GetPositionsAsync(CancellationToken ct);
    Task ClosePositionAsync(string symbol, Side side);
    Task ClosePartialAsync(string symbol, Side originalSide, decimal quantity);
    Task CloseAllPositionsAsync();
    Task CloseAllPositionsAsync(CancellationToken ct);

    // ─────────── Orders ───────────
    Task<Order> PlaceOrderAsync(OrderRequest request, decimal lastPrice = 0m);
    Task<bool> CancelOrderAsync(string orderId, string symbol);
    Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null);
    Task<Order> AmendOrderAsync(string orderId, string symbol, decimal? newPrice = null, decimal? newStopPrice = null, decimal? newQuantity = null);

    // ─────────── SL / TP Management ───────────
    Task SetPositionSlTpAsync(string symbol, Side positionSide, decimal? stopLoss, decimal? takeProfit);
    Task<Order> PlaceTpLimitOrderAsync(string symbol, Side positionSide, decimal quantity, decimal triggerPrice);
    Task<Order> PlaceTpMarketOrderAsync(string symbol, Side positionSide, decimal quantity, decimal triggerPrice);
    Task<Order> PlaceTpReduceOnlyLimitAsync(string symbol, Side positionSide, decimal quantity, decimal limitPrice);

    // ─────────── Safety / Kill-Switch ───────────
    Task ActivateKillSwitchAsync(int timeoutMs = 120_000);
    Task DeactivateKillSwitchAsync();

    // ─────────── WebSocket Listen-Key ───────────
    Task<string> CreateListenKeyAsync();
    Task RenewListenKeyAsync(string listenKey);
    Task DeleteListenKeyAsync(string listenKey);

    // ─────────── Income / Fees ───────────
    Task<List<IncomeRecord>> GetIncomeHistoryAsync(string? symbol = null, string? incomeType = null, DateTime? startTime = null, DateTime? endTime = null, int limit = 100);
}
