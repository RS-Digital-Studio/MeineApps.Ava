using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BingXBacktestLab;

/// <summary>
/// Laedt + cached die historische Funding-Rate-Historie pro Symbol von BingX
/// (<c>/openApi/swap/v2/quote/fundingRate</c>, public, 8h-Settlements). Rueckwaerts paginiert
/// (max 1000/Request) bis 'from'. JSON-Cache analog zum Kline-Cache (Re-Runs instant).
/// Fuer den Funding-Carry-Backtest, der per-Symbol-Funding statt einer Konstante braucht.
/// </summary>
internal sealed class FundingHistoryProvider
{
    private const string BaseUrl = "https://open-api.bingx.com";
    private readonly HttpClient _http;
    private readonly string _cacheDir;
    private readonly int _delayMs;

    public FundingHistoryProvider(HttpClient http, string cacheDir, int delayMs = 150)
    {
        _http = http;
        _cacheDir = cacheDir;
        _delayMs = delayMs;
        Directory.CreateDirectory(_cacheDir);
    }

    public readonly record struct FundingPoint(DateTime TimeUtc, decimal Rate);

    /// <summary>Funding-Historie [from..to] aufsteigend nach Zeit. Cache-Key = Symbol+from+to.</summary>
    public async Task<List<FundingPoint>> GetAsync(string symbol, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var key = Hash($"{symbol}_{from:yyyyMMdd}_{to:yyyyMMdd}");
        var cacheFile = Path.Combine(_cacheDir, $"{symbol}_{key}.json");
        if (File.Exists(cacheFile))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<List<FundingPoint>>(await File.ReadAllTextAsync(cacheFile, ct));
                if (cached is { Count: > 0 }) return cached;
            }
            catch { /* korrupt → neu laden */ }
        }

        var all = new Dictionary<long, decimal>();   // fundingTime(ms) → rate (dedupe)
        var currentEnd = to;
        var fromMs = ToMs(from);
        const int maxBatches = 80;   // 80 × 1000 × 8h ≈ 73 Jahre Schutz-Cap
        for (var batch = 0; batch < maxBatches && currentEnd > from && !ct.IsCancellationRequested; batch++)
        {
            var endMs = ToMs(currentEnd);
            var url = $"{BaseUrl}/openApi/swap/v2/quote/fundingRate?symbol={symbol}&startTime={fromMs}&endTime={endMs}&limit=1000";
            List<(long Time, decimal Rate)> batchData;
            try { batchData = await FetchBatchAsync(url, ct).ConfigureAwait(false); }
            catch { break; }
            if (batchData.Count == 0) break;

            foreach (var (time, rate) in batchData) all[time] = rate;
            var oldest = batchData.Min(x => x.Time);
            if (oldest <= fromMs) break;
            var newEnd = DateTimeOffset.FromUnixTimeMilliseconds(oldest - 1).UtcDateTime;
            if (newEnd >= currentEnd) break;   // kein Fortschritt → Historie-Anfang
            currentEnd = newEnd;
            if (_delayMs > 0) await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        }

        var result = all
            .Where(kv => kv.Key >= fromMs && kv.Key <= ToMs(to))
            .OrderBy(kv => kv.Key)
            .Select(kv => new FundingPoint(DateTimeOffset.FromUnixTimeMilliseconds(kv.Key).UtcDateTime, kv.Value))
            .ToList();
        try { await File.WriteAllTextAsync(cacheFile, JsonSerializer.Serialize(result), ct); } catch { }
        return result;
    }

    private async Task<List<(long Time, decimal Rate)>> FetchBatchAsync(string url, CancellationToken ct)
    {
        var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return [];
        var list = new List<(long, decimal)>();
        foreach (var el in data.EnumerateArray())
        {
            if (!el.TryGetProperty("fundingTime", out var ftEl)) continue;
            var ft = ftEl.ValueKind == JsonValueKind.Number ? ftEl.GetInt64()
                : long.TryParse(ftEl.GetString(), out var p) ? p : 0L;
            if (ft <= 0) continue;
            var rate = 0m;
            if (el.TryGetProperty("fundingRate", out var frEl))
            {
                var s = frEl.ValueKind == JsonValueKind.String ? frEl.GetString() : frEl.GetRawText();
                decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out rate);
            }
            list.Add((ft, rate));
        }
        return list;
    }

    private static long ToMs(DateTime t) =>
        new DateTimeOffset(DateTime.SpecifyKind(t, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static string Hash(string s)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes)[..16];
    }
}
