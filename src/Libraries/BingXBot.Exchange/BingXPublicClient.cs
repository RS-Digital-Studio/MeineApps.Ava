using System.Globalization;
using System.Text.Json;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Exchange.Models;
using Microsoft.Extensions.Logging;

namespace BingXBot.Exchange;

/// <summary>
/// Öffentlicher BingX Market-Data-Client. Braucht KEINEN API-Key.
/// Nutzt nur die Public Endpoints für Klines und Ticker.
/// </summary>
public class BingXPublicClient : IPublicMarketDataClient
{
    private const string BaseUrl = "https://open-api.bingx.com";
    private readonly HttpClient _httpClient;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger<BingXPublicClient> _logger;

    /// <summary>Timeout für einzelne HTTP-Requests (30s statt Endlos-Default).</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public BingXPublicClient(HttpClient httpClient, RateLimiter rateLimiter, ILogger<BingXPublicClient> logger)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _logger = logger;

        // Timeout konfigurieren falls noch nicht gesetzt
        if (_httpClient.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
            _httpClient.Timeout = RequestTimeout;
    }

    /// <summary>
    /// Lädt Klines für ein Symbol in einem Zeitraum. Paginiert automatisch (max 1440 pro Request).
    /// </summary>
    public async Task<List<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var interval = TimeFrameHelper.ToIntervalString(tf);
        var candleDuration = TimeFrameHelper.ToDuration(tf);
        var allCandles = new List<Candle>();
        var currentStart = from;

        while (currentStart < to && !ct.IsCancellationRequested)
        {
            await _rateLimiter.WaitForSlotAsync("queries", ct).ConfigureAwait(false);

            var startMs = new DateTimeOffset(DateTime.SpecifyKind(currentStart, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            var endMs = new DateTimeOffset(DateTime.SpecifyKind(to, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

            var url = $"{BaseUrl}/openApi/swap/v3/quote/klines?symbol={symbol}&interval={interval}&limit=1440&startTime={startMs}&endTime={endMs}";

            try
            {
                var response = await _httpClient.GetStringAsync(url, ct).ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<BingXResponse<List<BingXKlineDetail>>>(response);

                if (result?.Code != 0 || result.Data == null || result.Data.Count == 0)
                    break;

                foreach (var k in result.Data)
                {
                    var openTime = DateTimeOffset.FromUnixTimeMilliseconds(k.Time).UtcDateTime;
                    if (openTime < from || openTime > to) continue;

                    allCandles.Add(new Candle(
                        openTime,
                        ParseDecimal(k.Open),
                        ParseDecimal(k.High),
                        ParseDecimal(k.Low),
                        ParseDecimal(k.Close),
                        ParseDecimal(k.Volume),
                        openTime.Add(candleDuration)
                    ));
                }

                // Nächster Batch: nach der letzten Candle
                var lastTime = DateTimeOffset.FromUnixTimeMilliseconds(result.Data.Max(k => k.Time)).UtcDateTime;
                currentStart = lastTime.Add(candleDuration);

                _logger.LogDebug("Geladen: {Count} Candles bis {Time}", result.Data.Count, lastTime);

                // Wenn weniger als 1440 zurückkamen, gibt es keine weiteren
                if (result.Data.Count < 1440)
                    break;
            }
            catch (OperationCanceledException)
            {
                throw; // Weiterwerfen, nicht als Fehler loggen
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden von Klines für {Symbol}", symbol);
                break;
            }
        }

        // Duplikate entfernen und sortieren
        return allCandles
            .DistinctBy(c => c.OpenTime)
            .OrderBy(c => c.OpenTime)
            .ToList();
    }

    /// <summary>
    /// Lädt alle Ticker (öffentlich, kein API-Key nötig).
    /// </summary>
    public async Task<List<Ticker>> GetAllTickersAsync(CancellationToken ct = default)
    {
        await _rateLimiter.WaitForSlotAsync("queries", ct).ConfigureAwait(false);

        var url = $"{BaseUrl}/openApi/swap/v2/quote/ticker";
        var response = await _httpClient.GetStringAsync(url, ct).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<BingXResponse<List<BingXTickerDetail>>>(response);

        if (result?.Code != 0 || result.Data == null)
            return new List<Ticker>();

        return result.Data.Select(t => new Ticker(
            t.Symbol,
            ParseDecimal(t.LastPrice),
            ParseDecimal(t.BidPrice),
            ParseDecimal(t.AskPrice),
            ParseDecimal(t.Volume),
            ParseDecimal(t.PriceChangePercent),
            DateTime.UtcNow
        )).ToList();
    }

    /// <summary>
    /// Lädt alle verfügbaren Symbole (nach Volumen sortiert).
    /// </summary>
    public async Task<List<string>> GetAllSymbolsAsync(CancellationToken ct = default)
    {
        var tickers = await GetAllTickersAsync(ct).ConfigureAwait(false);
        return tickers
            .OrderByDescending(t => t.Volume24h)
            .Select(t => t.Symbol)
            .ToList();
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0m;
}
