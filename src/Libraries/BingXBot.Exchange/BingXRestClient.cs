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

    // Position-Modus: true = Hedge-Mode (LONG/SHORT), false = One-Way (BOTH)
    // Wird beim ersten Aufruf von DetectPositionModeAsync() erkannt und gecacht
    private bool? _isHedgeMode;
    private readonly SemaphoreSlim _modeLock = new(1, 1);

    /// <summary>Timeout fuer einzelne HTTP-Requests (30s statt Endlos-Default).</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Maximale Retry-Versuche bei transienten Fehlern (HTTP 429, 5xx, Timeout).</summary>
    private const int MaxRetries = 3;

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

    #region Position-Modus Erkennung

    /// <summary>
    /// Erkennt ob der Account im Hedge-Mode (Dual Position) oder One-Way-Mode ist.
    /// Wird beim ersten Aufruf gecacht. Thread-safe.
    /// </summary>
    public async Task<bool> IsHedgeModeAsync()
    {
        if (_isHedgeMode.HasValue) return _isHedgeMode.Value;

        await _modeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isHedgeMode.HasValue) return _isHedgeMode.Value;

            try
            {
                var data = await SendSignedRequestAsync<JsonElement>(
                    HttpMethod.Get,
                    "/openApi/swap/v1/positionSide/dual",
                    new Dictionary<string, string>(),
                    "queries");

                // Response: {"dualSidePosition": true/false}
                _isHedgeMode = data.TryGetProperty("dualSidePosition", out var prop) && prop.GetBoolean();
            }
            catch
            {
                // Bei Fehler: Default One-Way-Mode (sicherer, da häufiger)
                _isHedgeMode = false;
            }

            _logger.LogInformation("Position-Modus erkannt: {Mode}", _isHedgeMode.Value ? "Hedge (Dual)" : "One-Way");
            return _isHedgeMode.Value;
        }
        finally { _modeLock.Release(); }
    }

    /// <summary>Gibt den korrekten positionSide-Wert für die aktuelle Seite zurück.</summary>
    private async Task<string> GetPositionSideAsync(Side side)
    {
        var hedgeMode = await IsHedgeModeAsync().ConfigureAwait(false);
        if (!hedgeMode) return "BOTH"; // One-Way-Mode
        return side == Side.Buy ? "LONG" : "SHORT"; // Hedge-Mode
    }

    #endregion

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
    /// Retry bei transienten Fehlern (HTTP 429, 5xx, Timeout) mit exponentiellem Backoff.
    /// </summary>
    private async Task<T> SendSignedRequestAsync<T>(
        HttpMethod method,
        string path,
        Dictionary<string, string>? parameters,
        string rateCategory)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            await _rateLimiter.WaitForSlotAsync(rateCategory, CancellationToken.None).ConfigureAwait(false);

            var queryParams = parameters != null
                ? new Dictionary<string, string>(parameters)
                : new Dictionary<string, string>();

            // Timestamp bei jedem Versuch neu setzen (muss aktuell sein)
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            queryParams["timestamp"] = timestamp;
            queryParams["recvWindow"] = "5000"; // 5s Fenster gegen Replay-Angriffe

            // Query-String sortiert aufbauen
            var sortedParams = queryParams.OrderBy(kv => kv.Key);
            var queryString = string.Join("&", sortedParams.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

            // Signatur berechnen
            var signature = GenerateSignature(queryString, _apiSecret);
            queryString += $"&signature={signature}";

            var url = $"{BaseUrl}{path}?{queryString}";

            try
            {
                using var request = new HttpRequestMessage(method, url);
                request.Headers.Add("X-BX-APIKEY", _apiKey);

                _logger.LogDebug("{Method} {Path} (Versuch {Attempt})", method, path, attempt + 1);

                using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // Transiente Fehler: Retry bei 429 (Rate Limit) und 5xx (Server-Fehler)
                if (IsTransientError(response) && attempt < MaxRetries)
                {
                    var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)); // 2s, 4s, 8s
                    _logger.LogWarning("Transienter Fehler HTTP {StatusCode}, Retry in {Backoff}s (Versuch {Attempt}/{Max})",
                        (int)response.StatusCode, backoff.TotalSeconds, attempt + 1, MaxRetries);
                    await Task.Delay(backoff).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    // Content kürzen um Info-Leaks in externen Log-Sinks zu vermeiden
                    var truncated = content.Length > 200 ? content[..200] + "..." : content;
                    _logger.LogError("HTTP {StatusCode}: {Content}", response.StatusCode, truncated);
                    throw new BingXApiException((int)response.StatusCode,
                        $"HTTP {response.StatusCode}: {truncated}");
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
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                _logger.LogWarning(ex, "Netzwerkfehler, Retry in {Backoff}s (Versuch {Attempt}/{Max})",
                    backoff.TotalSeconds, attempt + 1, MaxRetries);
                await Task.Delay(backoff).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested && attempt < MaxRetries)
            {
                // Timeout (nicht manuell abgebrochen)
                var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                _logger.LogWarning("Request Timeout, Retry in {Backoff}s (Versuch {Attempt}/{Max})",
                    backoff.TotalSeconds, attempt + 1, MaxRetries);
                await Task.Delay(backoff).ConfigureAwait(false);
            }
        }

        // Sollte nicht erreicht werden, aber Compiler braucht einen Rückgabewert
        throw new BingXApiException(-1, $"Request fehlgeschlagen nach {MaxRetries + 1} Versuchen");
    }

    /// <summary>Prüft ob ein HTTP-Fehler transient ist (Retry sinnvoll).</summary>
    private static bool IsTransientError(HttpResponseMessage response) =>
        (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;

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
        // positionSide automatisch erkennen: Hedge-Mode → LONG/SHORT, One-Way → BOTH
        var positionSide = await GetPositionSideAsync(request.Side).ConfigureAwait(false);

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = request.Symbol,
            ["side"] = SideToString(request.Side),
            ["type"] = OrderTypeToString(request.Type),
            ["quantity"] = request.Quantity.ToString(CultureInfo.InvariantCulture),
            ["positionSide"] = positionSide
        };

        if (request.Price.HasValue)
            parameters["price"] = request.Price.Value.ToString(CultureInfo.InvariantCulture);

        if (request.StopPrice.HasValue)
            parameters["stopPrice"] = request.StopPrice.Value.ToString(CultureInfo.InvariantCulture);

        // Native SL/TP-Orders: BingX setzt SL/TP direkt auf der Position (serverseitig)
        if (request.StopLoss.HasValue && request.StopLoss.Value > 0)
        {
            parameters["stopLoss"] = JsonSerializer.Serialize(new
            {
                type = "STOP_MARKET",
                stopPrice = request.StopLoss.Value,
                workingType = "MARK_PRICE"
            });
        }

        if (request.TakeProfit.HasValue && request.TakeProfit.Value > 0)
        {
            parameters["takeProfit"] = JsonSerializer.Serialize(new
            {
                type = "TAKE_PROFIT_MARKET",
                stopPrice = request.TakeProfit.Value,
                workingType = "MARK_PRICE"
            });
        }

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

        // Close-Order: positionSide = ORIGINAL-Seite der Position
        // Hedge-Mode: LONG/SHORT, One-Way: BOTH
        var positionSide = await GetPositionSideAsync(side).ConfigureAwait(false);

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["side"] = SideToString(closeSide),
            ["type"] = "MARKET",
            ["quantity"] = position.Quantity.ToString(CultureInfo.InvariantCulture),
            ["positionSide"] = positionSide
        };

        await SendSignedRequestAsync<BingXOrderData>(
            HttpMethod.Post,
            "/openApi/swap/v2/trade/order",
            parameters,
            "orders");
    }

    public async Task CloseAllPositionsAsync()
    {
        var positions = await GetPositionsAsync().ConfigureAwait(false);
        if (positions.Count == 0) return;

        // Eindeutige Symbole sammeln (ein API-Call pro Symbol statt pro Position)
        var symbols = positions.Select(p => p.Symbol).Distinct().ToList();

        _logger.LogInformation("Schließe alle Positionen für {Count} Symbole", symbols.Count);

        // Parallel pro Symbol schließen via dediziertem BingX-Endpunkt
        var tasks = symbols.Select(symbol => Task.Run(async () =>
        {
            var parameters = new Dictionary<string, string> { ["symbol"] = symbol };
            await SendSignedRequestAsync<JsonElement>(
                HttpMethod.Post,
                "/openApi/swap/v2/trade/closeAllPositions",
                parameters,
                "orders").ConfigureAwait(false);
        }));

        await Task.WhenAll(tasks).ConfigureAwait(false);
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
            ParseDecimal(balance.UsedMargin),
            ParseDecimal(balance.Equity),
            ParseDecimal(balance.RealisedProfit));
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

        // premiumIndex statt fundingRate: Gibt einzelnes Objekt mit lastFundingRate zurück
        // (fundingRate-Endpunkt gibt Array zurück → Deserialisierung schlägt fehl)
        var data = await SendSignedRequestAsync<BingXFundingRateData>(
            HttpMethod.Get,
            "/openApi/swap/v2/quote/premiumIndex",
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

    #region User-Data-Stream (WebSocket ListenKey)

    /// <summary>
    /// Erstellt einen ListenKey für den User-Data-Stream (WebSocket).
    /// Der Key ist 60 Minuten gültig und muss alle 30 Minuten erneuert werden.
    /// </summary>
    public async Task<string> CreateListenKeyAsync()
    {
        var data = await SendSignedRequestAsync<JsonElement>(
            HttpMethod.Post,
            "/openApi/user/auth/userDataStream",
            null,
            "queries");

        var listenKey = data.TryGetProperty("listenKey", out var lk) ? lk.GetString() : null;
        if (string.IsNullOrEmpty(listenKey))
            throw new BingXApiException(-1, "ListenKey konnte nicht erstellt werden");

        _logger.LogInformation("ListenKey erstellt: {Key}", listenKey[..8] + "...");
        return listenKey;
    }

    /// <summary>
    /// Erneuert einen bestehenden ListenKey (verlängert Gültigkeit um 60 Minuten).
    /// </summary>
    public async Task RenewListenKeyAsync(string listenKey)
    {
        var parameters = new Dictionary<string, string>
        {
            ["listenKey"] = listenKey
        };

        await SendSignedRequestAsync<JsonElement>(
            HttpMethod.Put,
            "/openApi/user/auth/userDataStream",
            parameters,
            "queries");

        _logger.LogDebug("ListenKey erneuert");
    }

    /// <summary>
    /// Löscht einen ListenKey (schließt den User-Data-Stream).
    /// </summary>
    public async Task DeleteListenKeyAsync(string listenKey)
    {
        var parameters = new Dictionary<string, string>
        {
            ["listenKey"] = listenKey
        };

        try
        {
            await SendSignedRequestAsync<JsonElement>(
                HttpMethod.Delete,
                "/openApi/user/auth/userDataStream",
                parameters,
                "queries");
            _logger.LogDebug("ListenKey gelöscht");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ListenKey-Löschung fehlgeschlagen");
        }
    }

    #endregion

}
