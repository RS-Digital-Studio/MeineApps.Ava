using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Core.Interfaces;

/// <summary>
/// Interface für öffentliche Marktdaten (kein API-Key nötig).
/// Unterstützt Klines mit from/to-Paginierung und Ticker-Abfragen.
/// </summary>
public interface IPublicMarketDataClient
{
    /// <summary>
    /// Lädt Klines für ein Symbol in einem Zeitraum. Paginiert automatisch.
    /// </summary>
    Task<List<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Lädt alle Ticker (öffentlich).
    /// </summary>
    Task<List<Ticker>> GetAllTickersAsync(CancellationToken ct = default);

    /// <summary>
    /// Lädt alle verfügbaren Symbole.
    /// </summary>
    Task<List<string>> GetAllSymbolsAsync(CancellationToken ct = default);
}
