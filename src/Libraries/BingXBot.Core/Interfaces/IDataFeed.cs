using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Core.Interfaces;

public interface IDataFeed : IAsyncDisposable
{
    IAsyncEnumerable<Candle> StreamKlinesAsync(string symbol, TimeFrame tf, CancellationToken ct);
    IAsyncEnumerable<Ticker> StreamTickerAsync(string symbol, CancellationToken ct);
    Task<IReadOnlyList<Candle>> GetHistoricalKlinesAsync(string symbol, TimeFrame tf, DateTime from, DateTime to);
    event EventHandler<bool>? ConnectionStateChanged;
    bool IsConnected { get; }
}
