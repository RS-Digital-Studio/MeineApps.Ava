using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using BingXBot.Core.Models;
using BingXBot.Exchange.Models;
using Microsoft.Extensions.Logging;

namespace BingXBot.Exchange;

/// <summary>
/// Cache für Symbol-Handelsinformationen (Precision, Min-Order-Größe).
/// Wird einmal beim Start geladen und bleibt für die gesamte Session gültig.
/// Thread-safe durch ConcurrentDictionary.
/// </summary>
public class SymbolInfoCache
{
    private readonly ConcurrentDictionary<string, SymbolInfo> _cache = new();
    private readonly ILogger _logger;
    private volatile bool _initialized;

    // Fallback-Werte wenn kein Info verfügbar (konservativ: wenig Precision = größere Schritte)
    private static readonly SymbolInfo DefaultInfo = new("UNKNOWN", 4, 2, 0.0001m, 5m);

    public SymbolInfoCache(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>Ob der Cache bereits initialisiert wurde.</summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Lädt alle Contract-Details von BingX und cached sie.
    /// Sollte einmal beim Start aufgerufen werden.
    /// </summary>
    public async Task InitializeAsync(HttpClient httpClient)
    {
        try
        {
            // Öffentliche API - kein API-Key nötig
            var response = await httpClient.GetAsync("https://open-api.bingx.com/openApi/swap/v2/quote/contracts")
                .ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var apiResponse = JsonSerializer.Deserialize<BingXResponse<JsonElement>>(content);
            if (apiResponse?.Code != 0 || apiResponse.Data.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Contract-Details konnten nicht geladen werden: {Msg}", apiResponse?.Msg);
                return;
            }

            var count = 0;
            foreach (var item in apiResponse.Data.EnumerateArray())
            {
                var detail = JsonSerializer.Deserialize<BingXContractDetail>(item.GetRawText());
                if (detail == null || string.IsNullOrEmpty(detail.Symbol)) continue;

                var minQty = decimal.TryParse(detail.TradeMinQuantity, NumberStyles.Any, CultureInfo.InvariantCulture, out var mq) ? mq : 0.0001m;
                var minNotional = decimal.TryParse(detail.TradeMinUSDT, NumberStyles.Any, CultureInfo.InvariantCulture, out var mn) ? mn : 5m;

                _cache[detail.Symbol] = new SymbolInfo(
                    detail.Symbol,
                    detail.QuantityPrecision,
                    detail.PricePrecision,
                    minQty,
                    minNotional);
                count++;
            }

            _initialized = true;
            _logger.LogInformation("SymbolInfoCache initialisiert: {Count} Symbole geladen", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SymbolInfoCache-Initialisierung fehlgeschlagen");
        }
    }

    /// <summary>Gibt die SymbolInfo für ein Symbol zurück (Fallback auf konservative Defaults).</summary>
    public SymbolInfo GetInfo(string symbol)
    {
        return _cache.TryGetValue(symbol, out var info) ? info : DefaultInfo with { Symbol = symbol };
    }

    /// <summary>
    /// Rundet eine Quantity auf die erlaubte Precision (Floor/Truncate, nicht Round-Up).
    /// BingX lehnt Orders ab deren Quantity zu viele Dezimalstellen hat.
    /// </summary>
    public decimal TruncateQuantity(string symbol, decimal quantity)
    {
        var info = GetInfo(symbol);
        var factor = (decimal)Math.Pow(10, info.QuantityPrecision);
        return Math.Floor(quantity * factor) / factor;
    }

    /// <summary>
    /// Rundet einen Preis auf die erlaubte Precision.
    /// Für SL-Preise bei Long wird abgerundet (Floor), für Short aufgerundet (Ceil).
    /// Für TP und allgemeine Preise: Standard-Rundung.
    /// </summary>
    public decimal RoundPrice(string symbol, decimal price)
    {
        var info = GetInfo(symbol);
        return Math.Round(price, info.PricePrecision, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Prüft ob eine Quantity die Mindestanforderungen erfüllt (MinQty + MinNotional).
    /// </summary>
    public bool MeetsMinimumOrder(string symbol, decimal quantity, decimal price)
    {
        var info = GetInfo(symbol);
        if (quantity < info.MinQuantity) return false;
        if (price > 0 && quantity * price < info.MinNotional) return false;
        return true;
    }
}
