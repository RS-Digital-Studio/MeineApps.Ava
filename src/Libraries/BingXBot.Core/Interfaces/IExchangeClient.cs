using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Core.Interfaces;

public interface IExchangeClient
{
    Task<Order> PlaceOrderAsync(OrderRequest request);
    Task<bool> CancelOrderAsync(string orderId, string symbol);
    Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null);
    Task<IReadOnlyList<Position>> GetPositionsAsync();
    Task ClosePositionAsync(string symbol, Side side);
    Task CloseAllPositionsAsync();
    Task<AccountInfo> GetAccountInfoAsync();
    Task SetLeverageAsync(string symbol, int leverage, Side side);
    Task SetMarginTypeAsync(string symbol, MarginType marginType);
    Task<IReadOnlyList<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, int limit);
    Task<IReadOnlyList<Ticker>> GetAllTickersAsync();
    Task<decimal> GetFundingRateAsync(string symbol);
    Task<IReadOnlyList<string>> GetAllSymbolsAsync();
}
