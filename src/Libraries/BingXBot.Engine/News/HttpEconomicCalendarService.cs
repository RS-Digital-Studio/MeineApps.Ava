using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace BingXBot.Engine.News;

/// <summary>
/// Task 1.2 — HTTP-basierter Wirtschaftskalender-Service.
///
/// Unterstützt generische JSON-Feeds (TradingEconomics-Format als Default).
/// Cache: 24h Gesamt-Lifetime, 4h Refresh-Intervall.
/// Graceful Degradation: Service-Fehler blockieren den Bot nicht — Rückgabe leerer Liste.
///
/// Beispiel-Konfiguration (TradingEconomics Free-Tier):
/// <code>
/// var config = new HttpEconomicCalendarConfig {
///   Endpoint = "https://api.tradingeconomics.com/calendar?c=guest:guest&amp;importance=3",
///   Format = NewsFeedFormat.TradingEconomics
/// };
/// </code>
/// </summary>
public sealed class HttpEconomicCalendarService : IEconomicCalendarService, IDisposable
{
    private readonly HttpClient _http;
    private readonly HttpEconomicCalendarConfig _config;
    private readonly ILogger<HttpEconomicCalendarService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private List<EconomicEvent> _cache = new();
    private DateTime _cacheExpiresUtc = DateTime.MinValue;
    private DateTime _lastRefreshUtc = DateTime.MinValue;

    public HttpEconomicCalendarService(
        HttpClient http,
        HttpEconomicCalendarConfig config,
        ILogger<HttpEconomicCalendarService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EconomicEvent>> GetEventsAsync(
        DateTime fromUtc, DateTime toUtc,
        EconomicEventImpact minImpact = EconomicEventImpact.High,
        CancellationToken ct = default)
    {
        await EnsureCacheAsync(ct).ConfigureAwait(false);
        return _cache
            .Where(e => e.TimeUtc >= fromUtc && e.TimeUtc <= toUtc && e.Impact >= minImpact)
            .ToList();
    }

    public async Task<EconomicEvent?> GetActiveBlackoutEventAsync(
        DateTime nowUtc, int blackoutMinutes, CancellationToken ct = default)
    {
        if (blackoutMinutes <= 0) return null;
        var windowStart = nowUtc.AddMinutes(-blackoutMinutes);
        var windowEnd = nowUtc.AddMinutes(blackoutMinutes);
        var events = await GetEventsAsync(windowStart, windowEnd, EconomicEventImpact.High, ct).ConfigureAwait(false);
        return events.FirstOrDefault();
    }

    private async Task EnsureCacheAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (now < _cacheExpiresUtc && (now - _lastRefreshUtc) < _config.RefreshInterval)
            return;

        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (now < _cacheExpiresUtc && (now - _lastRefreshUtc) < _config.RefreshInterval)
                return;

            _logger.LogDebug("News-Cache wird refresht von {Endpoint}", _config.Endpoint);
            var events = await FetchAsync(ct).ConfigureAwait(false);
            _cache = events;
            _lastRefreshUtc = now;
            _cacheExpiresUtc = now.Add(_config.CacheLifetime);
            _logger.LogInformation("News-Cache refresht: {Count} High-Impact-Events geladen", events.Count(e => e.Impact == EconomicEventImpact.High));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "News-Refresh fehlgeschlagen — alter Cache bleibt aktiv (graceful degradation)");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<List<EconomicEvent>> FetchAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_config.Endpoint))
            return new List<EconomicEvent>();

        var req = new HttpRequestMessage(HttpMethod.Get, _config.Endpoint);
        if (!string.IsNullOrWhiteSpace(_config.ApiKey))
            req.Headers.Add("Authorization", _config.ApiKey);

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return _config.Format switch
        {
            NewsFeedFormat.TradingEconomics => ParseTradingEconomics(json),
            _ => ParseGeneric(json),
        };
    }

    private static List<EconomicEvent> ParseTradingEconomics(string json)
    {
        var items = JsonSerializer.Deserialize<List<TradingEconomicsItem>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<TradingEconomicsItem>();
        return items
            .Where(i => !string.IsNullOrWhiteSpace(i.Event))
            .Select(i => new EconomicEvent(
                TimeUtc: DateTime.SpecifyKind(i.Date, DateTimeKind.Utc),
                Country: i.Country ?? "",
                Name: i.Event ?? "",
                Impact: i.Importance switch
                {
                    3 => EconomicEventImpact.High,
                    2 => EconomicEventImpact.Medium,
                    _ => EconomicEventImpact.Low,
                },
                Currency: i.Currency))
            .ToList();
    }

    private static List<EconomicEvent> ParseGeneric(string json)
    {
        var items = JsonSerializer.Deserialize<List<GenericFeedItem>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<GenericFeedItem>();
        return items
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(i => new EconomicEvent(
                TimeUtc: DateTime.SpecifyKind(i.TimeUtc, DateTimeKind.Utc),
                Country: i.Country ?? "",
                Name: i.Name ?? "",
                Impact: Enum.TryParse<EconomicEventImpact>(i.Impact, true, out var imp) ? imp : EconomicEventImpact.Low,
                Currency: i.Currency))
            .ToList();
    }

    public void Dispose() => _refreshLock.Dispose();

    private sealed class TradingEconomicsItem
    {
        [JsonPropertyName("Date")] public DateTime Date { get; set; }
        [JsonPropertyName("Country")] public string? Country { get; set; }
        [JsonPropertyName("Event")] public string? Event { get; set; }
        [JsonPropertyName("Importance")] public int Importance { get; set; }
        [JsonPropertyName("Currency")] public string? Currency { get; set; }
    }

    private sealed class GenericFeedItem
    {
        public DateTime TimeUtc { get; set; }
        public string? Country { get; set; }
        public string? Name { get; set; }
        public string? Impact { get; set; }
        public string? Currency { get; set; }
    }
}

/// <summary>Task 1.2 — Konfiguration für den HTTP-News-Service.</summary>
public sealed class HttpEconomicCalendarConfig
{
    /// <summary>Kompletter HTTP-GET-Endpoint (inkl. API-Key-Parameter, wenn nicht per Header).</summary>
    public string Endpoint { get; set; } = "";
    /// <summary>Optionaler Authorization-Header-Wert (z.B. "Bearer abc123").</summary>
    public string? ApiKey { get; set; }
    /// <summary>JSON-Format des Feeds. Default: TradingEconomics-kompatibel.</summary>
    public NewsFeedFormat Format { get; set; } = NewsFeedFormat.TradingEconomics;
    /// <summary>Gesamt-Lifetime des Caches (Default 24h).</summary>
    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromHours(24);
    /// <summary>Refresh-Intervall wenn der Cache noch gültig ist (Default 4h).</summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(4);
}

/// <summary>Unterstützte Feed-Formate.</summary>
public enum NewsFeedFormat
{
    /// <summary>TradingEconomics-Kalender-API-Format (Date/Country/Event/Importance-Felder).</summary>
    TradingEconomics,
    /// <summary>Generisches Format mit TimeUtc/Country/Name/Impact/Currency-Feldern.</summary>
    Generic
}
