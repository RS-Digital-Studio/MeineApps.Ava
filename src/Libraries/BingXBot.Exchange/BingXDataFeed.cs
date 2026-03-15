using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace BingXBot.Exchange;

/// <summary>
/// BingX Data Feed - Echtzeit-Marktdaten via WebSocket + historische Daten via REST.
/// </summary>
public class BingXDataFeed : IDataFeed
{
    private const int MaxKlinesPerRequest = 1440;

    private readonly BingXWebSocketClient _wsClient;
    private readonly BingXRestClient _restClient;
    private readonly ILogger<BingXDataFeed> _logger;

    public event EventHandler<bool>? ConnectionStateChanged;
    public bool IsConnected => _wsClient.IsConnected;

    public BingXDataFeed(
        BingXWebSocketClient wsClient,
        BingXRestClient restClient,
        ILogger<BingXDataFeed> logger)
    {
        _wsClient = wsClient;
        _restClient = restClient;
        _logger = logger;

        // WebSocket Connection-Events durchreichen
        _wsClient.ConnectionStateChanged += (_, connected) =>
            ConnectionStateChanged?.Invoke(this, connected);
    }

    /// <summary>
    /// Streamt Kline-Updates via WebSocket.
    /// Channel-Format: {symbol}@kline_{interval} (z.B. "BTC-USDT@kline_1m")
    /// </summary>
    public async IAsyncEnumerable<Candle> StreamKlinesAsync(
        string symbol,
        TimeFrame tf,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var interval = TimeFrameHelper.ToIntervalString(tf);
        var channel = $"{symbol}@kline_{interval}";
        var candleChannel = Channel.CreateUnbounded<Candle>(
            new UnboundedChannelOptions { SingleReader = true });

        // Handler registrieren der JSON → Candle parst
        await _wsClient.SubscribeAsync(channel, message =>
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                // BingX Kline-Format: data-Objekt mit Kline-Feldern
                if (!root.TryGetProperty("data", out var data)) return;

                var kline = data.ValueKind == JsonValueKind.Array
                    ? data.EnumerateArray().FirstOrDefault()
                    : data;

                if (kline.ValueKind == JsonValueKind.Undefined) return;

                var openTime = kline.TryGetProperty("T", out var tProp)
                    ? DateTimeOffset.FromUnixTimeMilliseconds(tProp.GetInt64()).UtcDateTime
                    : DateTime.UtcNow;

                var candle = new Candle(
                    openTime,
                    ParseDecimal(kline, "o"),
                    ParseDecimal(kline, "h"),
                    ParseDecimal(kline, "l"),
                    ParseDecimal(kline, "c"),
                    ParseDecimal(kline, "v"),
                    openTime.Add(TimeFrameHelper.ToDuration(tf)));

                candleChannel.Writer.TryWrite(candle);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Fehler beim Parsen der Kline-Nachricht");
            }
        });

        try
        {
            await foreach (var candle in candleChannel.Reader.ReadAllAsync(ct))
            {
                yield return candle;
            }
        }
        finally
        {
            await _wsClient.UnsubscribeAsync(channel);
            candleChannel.Writer.Complete();
        }
    }

    /// <summary>
    /// Streamt Ticker-Updates via WebSocket.
    /// Channel-Format: {symbol}@ticker
    /// </summary>
    public async IAsyncEnumerable<Ticker> StreamTickerAsync(
        string symbol,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = $"{symbol}@ticker";
        var tickerChannel = Channel.CreateUnbounded<Ticker>(
            new UnboundedChannelOptions { SingleReader = true });

        await _wsClient.SubscribeAsync(channel, message =>
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                if (!root.TryGetProperty("data", out var data)) return;

                var ticker = new Ticker(
                    symbol,
                    ParseDecimal(data, "c"),  // Last price
                    ParseDecimal(data, "b"),  // Bid
                    ParseDecimal(data, "a"),  // Ask
                    ParseDecimal(data, "v"),  // Volume
                    ParseDecimal(data, "p"),  // Price change percent
                    DateTime.UtcNow);

                tickerChannel.Writer.TryWrite(ticker);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Fehler beim Parsen der Ticker-Nachricht");
            }
        });

        try
        {
            await foreach (var ticker in tickerChannel.Reader.ReadAllAsync(ct))
            {
                yield return ticker;
            }
        }
        finally
        {
            await _wsClient.UnsubscribeAsync(channel);
            tickerChannel.Writer.Complete();
        }
    }

    /// <summary>
    /// Holt historische Klines via REST mit Paginierung.
    /// BingX erlaubt max. 1440 Klines pro Request.
    /// </summary>
    public async Task<IReadOnlyList<Candle>> GetHistoricalKlinesAsync(
        string symbol,
        TimeFrame tf,
        DateTime from,
        DateTime to)
    {
        var allCandles = new List<Candle>();
        var duration = TimeFrameHelper.ToDuration(tf);
        var currentFrom = from;

        _logger.LogInformation("Lade historische Klines: {Symbol} {TF} von {From} bis {To}",
            symbol, tf, from, to);

        while (currentFrom < to)
        {
            // Berechne wie viele Candles in diesen Zeitraum passen
            var remaining = (int)Math.Ceiling((to - currentFrom) / duration);
            var limit = Math.Min(remaining, MaxKlinesPerRequest);

            var candles = await _restClient.GetKlinesAsync(symbol, tf, limit);

            if (candles.Count == 0)
                break;

            // Nur Candles im gewünschten Zeitraum hinzufügen
            var filtered = candles
                .Where(c => c.OpenTime >= from && c.OpenTime < to)
                .ToList();

            allCandles.AddRange(filtered);

            // Nächste Seite: nach der letzten empfangenen Candle
            var lastTime = candles.Max(c => c.OpenTime);
            if (lastTime <= currentFrom)
                break; // Keine neuen Daten → Abbruch

            currentFrom = lastTime + duration;
        }

        // Duplikate entfernen und sortieren
        return allCandles
            .DistinctBy(c => c.OpenTime)
            .OrderBy(c => c.OpenTime)
            .ToList();
    }

    /// <summary>
    /// Parst einen Decimal-Wert aus einem JsonElement Property.
    /// </summary>
    private static decimal ParseDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return 0m;

        return prop.ValueKind == JsonValueKind.String
            ? decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
                ? v
                : 0m
            : prop.TryGetDecimal(out var d) ? d : 0m;
    }

    public async ValueTask DisposeAsync()
    {
        await _wsClient.DisposeAsync();
    }
}
