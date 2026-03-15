using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Exchange.Models;
using Microsoft.Extensions.Logging;

namespace BingXBot.Exchange;

/// <summary>
/// BingX REST API Client - implementiert IExchangeClient für Perpetual Futures.
/// </summary>
public class BingXRestClient : IExchangeClient
{
    private const string BaseUrl = "https://open-api.bingx.com";

    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly HttpClient _httpClient;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger<BingXRestClient> _logger;

    /// <summary>Timeout fuer einzelne HTTP-Requests (30s statt Endlos-Default).</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public BingXRestClient(
        string apiKey,
        string apiSecret,
        HttpClient httpClient,
        RateLimiter rateLimiter,
        ILogger<BingXRestClient> logger)
    {
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _logger = logger;

        // Timeout konfigurieren falls noch nicht gesetzt
        if (_httpClient.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
            _httpClient.Timeout = RequestTimeout;
    }

    #region Signatur

    /// <summary>
    /// HMAC-SHA256 Signatur für BingX API Requests.
    /// </summary>
    public static string GenerateSignature(string queryString, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    #endregion

    #region Hilfsmethoden

    /// <summary>
    /// Sendet einen signierten Request an die BingX API.
    /// Baut Query-String, fügt Timestamp + Signatur hinzu, parst Response.
    /// </summary>
    private async Task<T> SendSignedRequestAsync<T>(
        HttpMethod method,
        string path,
        Dictionary<string, string>? parameters,
        string rateCategory)
    {
        await _rateLimiter.WaitForSlotAsync(rateCategory, CancellationToken.None).ConfigureAwait(false);

        var queryParams = parameters ?? new Dictionary<string, string>();

        // Timestamp hinzufügen
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        queryParams["timestamp"] = timestamp;

        // Query-String sortiert aufbauen
        var sortedParams = queryParams.OrderBy(kv => kv.Key);
        var queryString = string.Join("&", sortedParams.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        // Signatur berechnen
        var signature = GenerateSignature(queryString, _apiSecret);
        queryString += $"&signature={signature}";

        var url = $"{BaseUrl}{path}?{queryString}";

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-BX-APIKEY", _apiKey);

        _logger.LogDebug("{Method} {Path}", method, path);

        using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("HTTP {StatusCode}: {Content}", response.StatusCode, content);
            throw new BingXApiException((int)response.StatusCode,
                $"HTTP {response.StatusCode}: {content}");
        }

        var apiResponse = JsonSerializer.Deserialize<BingXResponse<T>>(content);
        if (apiResponse is null)
            throw new BingXApiException(-1, "Response konnte nicht deserialisiert werden");

        if (apiResponse.Code != 0)
        {
            _logger.LogError("API Error {Code}: {Msg}", apiResponse.Code, apiResponse.Msg);
            throw new BingXApiException(apiResponse.Code, apiResponse.Msg ?? "Unbekannter Fehler");
        }

        return apiResponse.Data!;
    }

    /// <summary>
    /// Konvertiert Unix-Millisekunden in DateTime (UTC).
    /// </summary>
    private static DateTime FromUnixMs(long ms) =>
        DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

    /// <summary>
    /// Parst einen Decimal-String sicher (Invariant Culture).
    /// </summary>
    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0m;

    /// <summary>
    /// Mappt BingX Side-String auf Core Enum.
    /// </summary>
    private static Side ParseSide(string side) =>
        side.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? Side.Buy : Side.Sell;

    /// <summary>
    /// Mappt Core Side Enum auf BingX String.
    /// </summary>
    private static string SideToString(Side side) =>
        side == Side.Buy ? "BUY" : "SELL";

    /// <summary>
    /// Mappt Core OrderType Enum auf BingX String.
    /// </summary>
    private static string OrderTypeToString(OrderType type) => type switch
    {
        OrderType.Market => "MARKET",
        OrderType.Limit => "LIMIT",
        OrderType.StopMarket => "STOP_MARKET",
        OrderType.StopLimit => "STOP",
        OrderType.TakeProfitMarket => "TAKE_PROFIT_MARKET",
        _ => "MARKET"
    };

    /// <summary>
    /// Mappt BingX OrderType-String auf Core Enum.
    /// </summary>
    private static OrderType ParseOrderType(string type) => type.ToUpperInvariant() switch
    {
        "MARKET" => OrderType.Market,
        "LIMIT" => OrderType.Limit,
        "STOP_MARKET" => OrderType.StopMarket,
        "STOP" => OrderType.StopLimit,
        "TAKE_PROFIT_MARKET" => OrderType.TakeProfitMarket,
        _ => OrderType.Market
    };

    /// <summary>
    /// Mappt BingX OrderStatus-String auf Core Enum.
    /// </summary>
    private static OrderStatus ParseOrderStatus(string status) => status.ToUpperInvariant() switch
    {
        "NEW" => OrderStatus.New,
        "PENDING" => OrderStatus.New,
        "PARTIALLY_FILLED" => OrderStatus.PartiallyFilled,
        "FILLED" => OrderStatus.Filled,
        "CANCELLED" or "CANCELED" => OrderStatus.Cancelled,
        "REJECTED" => OrderStatus.Rejected,
        "EXPIRED" => OrderStatus.Expired,
        _ => OrderStatus.New
    };

    /// <summary>
    /// Mappt Core MarginType Enum auf BingX String.
    /// </summary>
    private static string MarginTypeToString(MarginType marginType) =>
        marginType == MarginType.Cross ? "CROSSED" : "ISOLATED";

    /// <summary>
    /// Mappt BingX MarginType-String auf Core Enum.
    /// </summary>
    private static MarginType ParseMarginType(string marginType) =>
        marginType.Equals("ISOLATED", StringComparison.OrdinalIgnoreCase)
            ? MarginType.Isolated
            : MarginType.Cross;

    #endregion

    #region Trading

    public async Task<Order> PlaceOrderAsync(OrderRequest request)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = request.Symbol,
            ["side"] = SideToString(request.Side),
            ["type"] = OrderTypeToString(request.Type),
            ["quantity"] = request.Quantity.ToString(CultureInfo.InvariantCulture)
        };

        if (request.Price.HasValue)
            parameters["price"] = request.Price.Value.ToString(CultureInfo.InvariantCulture);

        if (request.StopPrice.HasValue)
            parameters["stopPrice"] = request.StopPrice.Value.ToString(CultureInfo.InvariantCulture);

        _logger.LogInformation("Platziere Order: {Symbol} {Side} {Type} Qty={Quantity}",
            request.Symbol, request.Side, request.Type, request.Quantity);

        var data = await SendSignedRequestAsync<BingXOrderData>(
            HttpMethod.Post,
            "/openApi/swap/v2/trade/order",
            parameters,
            "orders");

        var order = data.Order!;
        return new Order(
            order.OrderId,
            order.Symbol,
            ParseSide(order.Side),
            ParseOrderType(order.Type),
            ParseDecimal(order.Price),
            ParseDecimal(order.Quantity),
            string.IsNullOrEmpty(order.StopPrice) ? null : ParseDecimal(order.StopPrice),
            FromUnixMs(order.CreateTime),
            ParseOrderStatus(order.Status));
    }

    public async Task<bool> CancelOrderAsync(string orderId, string symbol)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["orderId"] = orderId
        };

        _logger.LogInformation("Storniere Order: {OrderId} für {Symbol}", orderId, symbol);

        try
        {
            await SendSignedRequestAsync<BingXOrderData>(
                HttpMethod.Delete,
                "/openApi/swap/v2/trade/order",
                parameters,
                "orders");
            return true;
        }
        catch (BingXApiException ex)
        {
            _logger.LogWarning("Order-Stornierung fehlgeschlagen: {Error}", ex.ErrorMessage);
            return false;
        }
    }

    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null)
    {
        var parameters = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(symbol))
            parameters["symbol"] = symbol;

        var data = await SendSignedRequestAsync<JsonElement>(
            HttpMethod.Get,
            "/openApi/swap/v2/trade/openOrders",
            parameters,
            "queries");

        var orders = new List<Order>();

        // Response kann "orders" Array oder direkt ein Array sein
        var ordersArray = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("orders", out var arr)
            ? arr
            : data;

        if (ordersArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in ordersArray.EnumerateArray())
            {
                var detail = JsonSerializer.Deserialize<BingXOrderDetail>(item.GetRawText());
                if (detail is null) continue;

                orders.Add(new Order(
                    detail.OrderId,
                    detail.Symbol,
                    ParseSide(detail.Side),
                    ParseOrderType(detail.Type),
                    ParseDecimal(detail.Price),
                    ParseDecimal(detail.Quantity),
                    string.IsNullOrEmpty(detail.StopPrice) ? null : ParseDecimal(detail.StopPrice),
                    FromUnixMs(detail.CreateTime),
                    ParseOrderStatus(detail.Status)));
            }
        }

        return orders;
    }

    public async Task<IReadOnlyList<Position>> GetPositionsAsync()
    {
        var data = await SendSignedRequestAsync<JsonElement>(
            HttpMethod.Get,
            "/openApi/swap/v2/user/positions",
            null,
            "queries");

        var positions = new List<Position>();

        // Positionen können direkt als Array oder in einem Wrapper kommen
        var posArray = data.ValueKind == JsonValueKind.Array
            ? data
            : data.ValueKind == JsonValueKind.Object && data.TryGetProperty("positions", out var arr)
                ? arr
                : data;

        if (posArray.ValueKind != JsonValueKind.Array)
            return positions;

        foreach (var item in posArray.EnumerateArray())
        {
            var detail = JsonSerializer.Deserialize<BingXPositionDetail>(item.GetRawText());
            if (detail is null) continue;

            var quantity = ParseDecimal(detail.PositionAmt);
            if (quantity == 0) continue; // Leere Position überspringen

            // PositionSide "LONG" → Buy, "SHORT" → Sell
            var side = detail.PositionSide.Equals("LONG", StringComparison.OrdinalIgnoreCase)
                ? Side.Buy
                : Side.Sell;

            positions.Add(new Position(
                detail.Symbol,
                side,
                ParseDecimal(detail.AvgPrice),
                ParseDecimal(detail.MarkPrice),
                Math.Abs(quantity),
                ParseDecimal(detail.UnrealizedProfit),
                ParseDecimal(detail.Leverage),
                ParseMarginType(detail.MarginType),
                DateTime.UtcNow)); // BingX liefert kein OpenTime in Positions-Response
        }

        return positions;
    }

    public async Task ClosePositionAsync(string symbol, Side side)
    {
        // Gegenorder: Buy-Position schließen mit Sell, und umgekehrt
        var closeSide = side == Side.Buy ? Side.Sell : Side.Buy;

        // Erst aktuelle Position finden um die Menge zu bestimmen
        var positions = await GetPositionsAsync().ConfigureAwait(false);
        var position = positions.FirstOrDefault(p =>
            p.Symbol == symbol && p.Side == side);

        if (position is null)
        {
            _logger.LogWarning("Keine {Side} Position für {Symbol} gefunden", side, symbol);
            return;
        }

        _logger.LogInformation("Schließe {Side} Position {Symbol}: Qty={Quantity}",
            side, symbol, position.Quantity);

        await PlaceOrderAsync(new OrderRequest(
            symbol,
            closeSide,
            OrderType.Market,
            position.Quantity)).ConfigureAwait(false);
    }

    public async Task CloseAllPositionsAsync()
    {
        var positions = await GetPositionsAsync().ConfigureAwait(false);

        _logger.LogInformation("Schließe {Count} offene Positionen", positions.Count);

        foreach (var position in positions)
        {
            try
            {
                await ClosePositionAsync(position.Symbol, position.Side).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Schließen von {Symbol} {Side}",
                    position.Symbol, position.Side);
            }
        }
    }

    public async Task SetLeverageAsync(string symbol, int leverage, Side side)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["leverage"] = leverage.ToString(),
            ["side"] = SideToString(side)
        };

        _logger.LogInformation("Setze Leverage: {Symbol} {Side} = {Leverage}x",
            symbol, side, leverage);

        await SendSignedRequestAsync<JsonElement>(
            HttpMethod.Post,
            "/openApi/swap/v2/trade/leverage",
            parameters,
            "orders");
    }

    public async Task SetMarginTypeAsync(string symbol, MarginType marginType)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["marginType"] = MarginTypeToString(marginType)
        };

        _logger.LogInformation("Setze MarginType: {Symbol} = {MarginType}", symbol, marginType);

        await SendSignedRequestAsync<JsonElement>(
            HttpMethod.Post,
            "/openApi/swap/v2/trade/marginType",
            parameters,
            "orders");
    }

    #endregion

    #region Account

    public async Task<AccountInfo> GetAccountInfoAsync()
    {
        var data = await SendSignedRequestAsync<BingXBalanceData>(
            HttpMethod.Get,
            "/openApi/swap/v2/user/balance",
            null,
            "queries");

        var balance = data.Balance!;
        return new AccountInfo(
            ParseDecimal(balance.Balance),
            ParseDecimal(balance.AvailableMargin),
            ParseDecimal(balance.UnrealizedProfit),
            ParseDecimal(balance.UsedMargin));
    }

    #endregion

    #region Marktdaten

    public async Task<IReadOnlyList<Candle>> GetKlinesAsync(string symbol, TimeFrame tf, int limit)
    {
        var interval = TimeFrameHelper.ToIntervalString(tf);

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["interval"] = interval,
            ["limit"] = limit.ToString()
        };

        var data = await SendSignedRequestAsync<JsonElement>(
            HttpMethod.Get,
            "/openApi/swap/v3/quote/klines",
            parameters,
            "queries");

        var candles = new List<Candle>();

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var detail = JsonSerializer.Deserialize<BingXKlineDetail>(item.GetRawText());
                if (detail is null) continue;

                var openTime = FromUnixMs(detail.Time);
                // CloseTime basierend auf TimeFrame berechnen
                var closeTime = openTime.Add(TimeFrameHelper.ToDuration(tf));

                candles.Add(new Candle(
                    openTime,
                    ParseDecimal(detail.Open),
                    ParseDecimal(detail.High),
                    ParseDecimal(detail.Low),
                    ParseDecimal(detail.Close),
                    ParseDecimal(detail.Volume),
                    closeTime));
            }
        }

        return candles.OrderBy(c => c.OpenTime).ToList();
    }

    public async Task<IReadOnlyList<Ticker>> GetAllTickersAsync()
    {
        // Ohne Symbol-Parameter gibt BingX alle Ticker zurück
        var data = await SendSignedRequestAsync<JsonElement>(
            HttpMethod.Get,
            "/openApi/swap/v2/quote/ticker",
            null,
            "queries");

        var tickers = new List<Ticker>();

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var detail = JsonSerializer.Deserialize<BingXTickerDetail>(item.GetRawText());
                if (detail is null) continue;

                tickers.Add(new Ticker(
                    detail.Symbol,
                    ParseDecimal(detail.LastPrice),
                    ParseDecimal(detail.BidPrice),
                    ParseDecimal(detail.AskPrice),
                    ParseDecimal(detail.Volume),
                    ParseDecimal(detail.PriceChangePercent),
                    DateTime.UtcNow));
            }
        }

        return tickers;
    }

    public async Task<decimal> GetFundingRateAsync(string symbol)
    {
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol
        };

        var data = await SendSignedRequestAsync<BingXFundingRateData>(
            HttpMethod.Get,
            "/openApi/swap/v2/quote/fundingRate",
            parameters,
            "queries");

        return ParseDecimal(data.LastFundingRate);
    }

    public async Task<IReadOnlyList<string>> GetAllSymbolsAsync()
    {
        var tickers = await GetAllTickersAsync();
        return tickers.Select(t => t.Symbol).Distinct().OrderBy(s => s).ToList();
    }

    #endregion

}
