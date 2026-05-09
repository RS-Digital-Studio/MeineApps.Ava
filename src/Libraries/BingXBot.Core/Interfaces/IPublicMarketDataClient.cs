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

    /// <summary>
    /// Phase 18 / A3 + C2 — Lädt die Server-Zeit von BingX als UTC-DateTime (vom Endpoint
    /// <c>/openApi/swap/v2/server/time</c>, ~50 Bytes Response). Verwendet als Lightweight-Probe
    /// im <c>ServerHealthWatchdog</c> (statt 80 kB Tickers) und für Clock-Drift-Detection.
    /// Bei Fehler/Timeout wird eine Exception geworfen — Aufrufer behandelt das als Probe-Failure.
    /// </summary>
    Task<DateTime> GetServerTimeAsync(CancellationToken ct = default);
}
