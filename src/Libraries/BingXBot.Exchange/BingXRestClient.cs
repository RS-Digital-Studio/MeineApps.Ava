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

    // Symbol-Info-Cache für Quantity/Price-Precision und Min-Order-Größe
    private readonly SymbolInfoCache _symbolInfoCache;

    // Server-Zeitversatz in Millisekunden (lokal - server). Wird bei SyncServerTimeAsync() berechnet.
    // BingX erlaubt nur ±5s (recvWindow) — bei Systemzeit-Abweichung kommt Error 100421.
    private long _serverTimeOffsetMs;

    /// <summary>Timeout fuer einzelne HTTP-Requests (30s statt Endlos-Default).</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Maximale Retry-Versuche bei transienten Fehlern (HTTP 429, 5xx, Timeout).</summary>
    private const int MaxRetries = 3;

    public BingXRestClient(
        string apiKey,
        string apiSecret,
        HttpClient httpClient,
        RateLimiter rateLimiter,
        ILogger<BingXRestClient> logger,
        SymbolInfoCache? symbolInfoCache = null)
    {
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _logger = logger;
        _symbolInfoCache = symbolInfoCache ?? new SymbolInfoCache(logger);

        // Timeout konfigurieren falls noch nicht gesetzt
        if (_httpClient.Timeout == System.Threading.Timeout.InfiniteTimeSpan)
            _httpClient.Timeout = RequestTimeout;
    }

    /// <summary>Zugriff auf den SymbolInfoCache (für externe Validierung, z.B. Min-Order-Check).</summary>
    public SymbolInfoCache SymbolInfoCache => _symbolInfoCache;

    /// <summary>
    /// Initialisiert den SymbolInfoCache (lädt Contract-Details von BingX).
    /// Sollte einmal nach der Erstellung aufgerufen werden.
    /// </summary>
    public Task InitializeSymbolInfoAsync() => _symbolInfoCache.InitializeAsync(_httpClient);

    /// <summary>
    /// Synchronisiert die lokale Uhr mit dem BingX-Server.
    /// Berechnet den Offset (lokal - server) und nutzt ihn für alle signierten Requests.
    /// BingX Error 100421 tritt auf wenn die Abweichung > 5s (recvWindow) ist.
    /// </summary>
    public async Task SyncServerTimeAsync()
    {
        try
        {
            var localBefore = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var response = await _httpClient.GetStringAsync($"{BaseUrl}/openApi/swap/v2/server/time").ConfigureAwait(false);
            var localAfter = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var result = JsonSerializer.Deserialize<BingXResponse<BingXServerTime>>(response);
            if (result?.Code == 0 && result.Data != null)
            {
                var serverTime = result.Data.ServerTime;
                // Netzwerk-Latenz halbieren: Schätzung der Server-Zeit zum Zeitpunkt der Anfrage
                var localEstimate = (localBefore + localAfter) / 2;
                _serverTimeOffsetMs = localEstimate - serverTime;
                _logger.LogInformation("Server-Zeit synchronisiert (Offset: {Offset}ms)", _serverTimeOffsetMs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Server-Zeit-Synchronisation fehlgeschlagen: {Error}", ex.Message);
            // Fallback: kein Offset, lokale Zeit verwenden
        }
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

                // Response: {"dualSidePosition": true/false} oder {"dualSidePosition": "true"/"false"}
                // BingX gibt den Wert je nach API-Version als boolean ODER als String zurück
                if (data.TryGetProperty("dualSidePosition", out var prop))
                {
                    _isHedgeMode = prop.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String => prop.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true,
                        _ => false
                    };
                }
                else
                {
                    _isHedgeMode = false;
                }
            }
            catch (Exception ex)
            {
                // NICHT cachen bei Fehler: Nächster Aufruf soll erneut versuchen.
                // Fehlerhafte Erkennung führt zu falschen positionSide-Werten (BOTH vs LONG/SHORT)
                _logger.LogWarning("Position-Modus-Erkennung fehlgeschlagen: {Error}. Wird erneut versucht.", ex.Message);
                _isHedgeMode = null;
                return false; // Temporärer Fallback, wird beim nächsten Aufruf erneut geprüft
            }

            _logger.LogInformation("Position-Modus erkannt: {Mode}", _isHedgeMode.Value ? "Hedge (Dual)" : "One-Way");
            return _isHedgeMode.Value;
        }
        finally { _modeLock.Release(); }
    }

    /// <summary>
    /// Schaltet den Position-Modus auf Hedge (Dual-Side) oder One-Way.
    /// ACHTUNG: Funktioniert nur wenn KEINE offenen Positionen vorhanden sind!
    /// BingX gibt Error zurück wenn Positionen offen sind.
    /// </summary>
    public async Task<bool> SetHedgeModeAsync(bool enableHedge)
    {
        await _modeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var data = await SendSignedRequestAsync<JsonElement>(
                HttpMethod.Post,
                "/openApi/swap/v1/positionSide/dual",
                new Dictionary<string, string>
                {
                    ["dualSidePosition"] = enableHedge.ToString().ToLower()
                },
                "queries");

            _isHedgeMode = enableHedge;
            _logger.LogInformation("Position-Modus umgeschaltet: {Mode}", enableHedge ? "Hedge (Dual)" : "One-Way");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Position-Modus konnte nicht umgeschaltet werden: {Error}", ex.Message);
            return false;
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
    private Task<T> SendSignedRequestAsync<T>(
        HttpMethod method,
        string path,
        Dictionary<string, string>? parameters,
        string rateCategory)
        => SendSignedRequestAsync<T>(method, path, parameters, rateCategory, CancellationToken.None);

    private async Task<T> SendSignedRequestAsync<T>(
        HttpMethod method,
        string path,
        Dictionary<string, string>? parameters,
        string rateCategory,
        CancellationToken ct)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            await _rateLimiter.WaitForSlotAsync(rateCategory, ct).ConfigureAwait(false);

            var queryParams = parameters != null
                ? new Dictionary<string, string>(parameters)
                : new Dictionary<string, string>();

            // Timestamp bei jedem Versuch neu setzen (muss aktuell sein)
            // Server-Offset abziehen falls synchronisiert (BingX Error 100421 bei >5s Abweichung)
            var timestamp = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _serverTimeOffsetMs).ToString();
            queryParams["timestamp"] = timestamp;
            queryParams["recvWindow"] = "5000"; // 5s Fenster gegen Replay-Angriffe

            // Query-String aufbauen: BingX berechnet Signatur über den RAW Query-String (ohne URL-Encoding)
            // Dann wird der Query-String URL-encodiert für die tatsächliche URL
            var sortedParams = queryParams.OrderBy(kv => kv.Key);

            // Signatur über rohen Query-String (OHNE URL-Encoding, so wie BingX es erwartet)
            var rawQueryString = string.Join("&", sortedParams.Select(kv => $"{kv.Key}={kv.Value}"));
            var signature = GenerateSignature(rawQueryString, _apiSecret);

            // URL mit URL-encodierten Parametern aufbauen
            var encodedQueryString = string.Join("&", sortedParams.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            encodedQueryString += $"&signature={signature}";

            var url = $"{BaseUrl}{path}?{encodedQueryString}";

            try
            {
                using var request = new HttpRequestMessage(method, url);
                request.Headers.Add("X-BX-APIKEY", _apiKey);

                _logger.LogDebug("{Method} {Path} (Versuch {Attempt})", method, path, attempt + 1);

                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                // Transiente Fehler: Retry bei 429 (Rate Limit) und 5xx (Server-Fehler)
                if (IsTransientError(response) && attempt < MaxRetries)
                {
                    var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)); // 2s, 4s, 8s
                    _logger.LogWarning("Transienter Fehler HTTP {StatusCode}, Retry in {Backoff}s (Versuch {Attempt}/{Max})",
                        (int)response.StatusCode, backoff.TotalSeconds, attempt + 1, MaxRetries);
                    await Task.Delay(backoff, ct).ConfigureAwait(false);
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
        "TAKE_PROFIT" => OrderType.TakeProfitLimit,
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

        // Quantity und Preise auf erlaubte Precision truncaten/runden (BingX lehnt zu viele Dezimalstellen ab)
        var adjustedQty = _symbolInfoCache.TruncateQuantity(request.Symbol, request.Quantity);
        if (adjustedQty <= 0)
        {
            _logger.LogWarning("Quantity nach Precision-Truncation ist 0 (Original: {Qty}, Symbol: {Symbol})",
                request.Quantity, request.Symbol);
            throw new BingXApiException(-2, $"Quantity {request.Quantity} ist nach Truncation auf Precision 0 für {request.Symbol}");
        }

        // Min-Order-Check: Quantity und Notional müssen Mindestwerte erfüllen
        var checkPrice = request.Price ?? 0m;
        if (!_symbolInfoCache.MeetsMinimumOrder(request.Symbol, adjustedQty, checkPrice))
        {
            var info = _symbolInfoCache.GetInfo(request.Symbol);
            _logger.LogWarning("Order unterschreitet Minimum: Qty={Qty} (Min={MinQty}), Notional={Notional} (Min={MinNotional})",
                adjustedQty, info.MinQuantity, adjustedQty * checkPrice, info.MinNotional);
            throw new BingXApiException(-3, $"Order unterschreitet Minimum für {request.Symbol}: Qty={adjustedQty} < MinQty={info.MinQuantity} oder Notional < {info.MinNotional} USDT");
        }

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = request.Symbol,
            ["side"] = SideToString(request.Side),
            ["type"] = OrderTypeToString(request.Type),
            ["quantity"] = adjustedQty.ToString(CultureInfo.InvariantCulture),
            ["positionSide"] = positionSide
        };

        if (request.Price.HasValue)
            parameters["price"] = _symbolInfoCache.RoundPrice(request.Symbol, request.Price.Value)
                .ToString(CultureInfo.InvariantCulture);

        if (request.StopPrice.HasValue)
            parameters["stopPrice"] = _symbolInfoCache.RoundPrice(request.Symbol, request.StopPrice.Value)
                .ToString(CultureInfo.InvariantCulture);

        // Native SL/TP-Orders: BingX setzt SL/TP direkt auf der Position (serverseitig)
        if (request.StopLoss.HasValue && request.StopLoss.Value > 0)
        {
            var roundedSl = _symbolInfoCache.RoundPrice(request.Symbol, request.StopLoss.Value);
            parameters["stopLoss"] = JsonSerializer.Serialize(new
            {
                type = "STOP_MARKET",
                stopPrice = roundedSl,
                workingType = "MARK_PRICE"
            });
        }

        // TP wird NICHT nativ gesetzt: BingX native TP schließt die GESAMTE Position,
        // aber der Bot nutzt Pyramid-Exit (TP1=30%, TP2=30%, Rest Trailing).
        // TP-Management läuft komplett bot-seitig im PriceTickerLoop.
        // Nur SL bleibt nativ als Sicherheitsnetz bei App-Crash.

        _logger.LogInformation("Platziere Order: {Symbol} {Side} {Type} Qty={Quantity} (Original: {OrigQty})",
            request.Symbol, request.Side, request.Type, adjustedQty, request.Quantity);

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

    /// <summary>
    /// Setzt/aktualisiert serverseitige SL/TP auf einer offenen Position (BingX Margin-SL/TP).
    /// Verwendet die Cancel-Replace-API: Bestehende SL/TP-Orders werden gelöscht und neue erstellt.
    /// </summary>
    public async Task SetPositionSlTpAsync(string symbol, Side positionSide, decimal? stopLoss, decimal? takeProfit)
    {
        // 1. Nur bestehende SL-Orders canceln (TP wird bot-seitig verwaltet, nicht nativ)
        if (stopLoss.HasValue && stopLoss.Value > 0)
        {
            try
            {
                var openOrders = await GetOpenOrdersAsync(symbol).ConfigureAwait(false);
                foreach (var order in openOrders)
                {
                    // Nur STOP_MARKET canceln, nicht TAKE_PROFIT_MARKET
                    if (order.Type == OrderType.StopMarket && order.Symbol == symbol)
                        await CancelOrderAsync(order.OrderId, symbol).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Konnte bestehende SL-Orders nicht canceln: {Error}", ex.Message);
            }
        }

        // 2. Aktuelle Positionsgröße abfragen (BingX akzeptiert quantity=0/closePosition nicht zuverlässig)
        decimal positionQty = 0;
        try
        {
            var positions = await GetPositionsAsync().ConfigureAwait(false);
            var pos = positions.FirstOrDefault(p => p.Symbol == symbol && p.Side == positionSide);
            if (pos != null)
                positionQty = _symbolInfoCache.TruncateQuantity(symbol, pos.Quantity);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Konnte Positionsgröße nicht abfragen für SL: {Error}", ex.Message);
        }

        // 3. Neue SL-Order platzieren
        var closeSide = positionSide == Side.Buy ? Side.Sell : Side.Buy;
        var positionSideStr = await GetPositionSideAsync(positionSide).ConfigureAwait(false);

        if (stopLoss.HasValue && stopLoss.Value > 0)
        {
            var roundedSlPrice = _symbolInfoCache.RoundPrice(symbol, stopLoss.Value);
            // Guard: Wenn Rundung den Preis auf 0 setzt (Micro-Cap mit zu niedriger Precision),
            // den ungerundeten Wert mit 8 Dezimalstellen verwenden statt 0 zu senden
            if (roundedSlPrice <= 0)
            {
                roundedSlPrice = Math.Round(stopLoss.Value, 8, MidpointRounding.AwayFromZero);
                _logger.LogWarning("SL-Preis für {Symbol} auf 0 gerundet, verwende 8 Dezimalstellen: {Price}", symbol, roundedSlPrice);
            }

            var slParams = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["side"] = SideToString(closeSide),
                ["type"] = "STOP_MARKET",
                ["stopPrice"] = roundedSlPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["positionSide"] = positionSideStr,
                ["workingType"] = "MARK_PRICE"
            };

            // Echte Positionsgröße verwenden wenn verfügbar, sonst reduceOnly als Fallback
            if (positionQty > 0)
            {
                slParams["quantity"] = positionQty.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                // Fallback: reduceOnly statt closePosition (BingX V2 unterstützt closePosition nicht zuverlässig)
                slParams["quantity"] = "0";
                slParams["closePosition"] = "true";
            }

            // Exception NICHT verschlucken: Wenn alte SL gecancelt aber neue fehlschlägt,
            // ist die Position ungeschützt. Caller muss das wissen.
            await SendSignedRequestAsync<BingXOrderData>(
                HttpMethod.Post, "/openApi/swap/v2/trade/order", slParams, "orders");
            _logger.LogInformation("SL-Order gesetzt: {Symbol} @ {Price} Qty={Qty}", symbol, stopLoss.Value, positionQty);
        }

        // TP wird NICHT nativ gesetzt: BingX native TP schließt die gesamte Position.
        // Pyramid-Exit (TP1 30%, TP2 30%, Trailing 40%) wird bot-seitig im PriceTickerLoop gesteuert.
        // Nur SL bleibt nativ als Sicherheitsnetz.
        if (false && takeProfit.HasValue && takeProfit.Value > 0) // Deaktiviert - TP bot-seitig
        {
            var tpParams = new Dictionary<string, string>
            {
                ["symbol"] = symbol,
                ["side"] = SideToString(closeSide),
                ["type"] = "TAKE_PROFIT_MARKET",
                ["quantity"] = "0",
                ["stopPrice"] = takeProfit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["positionSide"] = positionSideStr,
                ["workingType"] = "MARK_PRICE",
                ["closePosition"] = "true"
            };

            try
            {
                await SendSignedRequestAsync<BingXOrderData>(
                    HttpMethod.Post, "/openApi/swap/v2/trade/order", tpParams, "orders");
                _logger.LogInformation("TP-Order gesetzt: {Symbol} @ {Price}", symbol, takeProfit.Value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("TP-Order fehlgeschlagen: {Error}", ex.Message);
            }
        }
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
            ["quantity"] = _symbolInfoCache.TruncateQuantity(symbol, position.Quantity)
                .ToString(CultureInfo.InvariantCulture),
            ["positionSide"] = positionSide
        };

        await SendSignedRequestAsync<BingXOrderData>(
            HttpMethod.Post,
            "/openApi/swap/v2/trade/order",
            parameters,
            "orders");
    }

    /// <summary>
    /// Schließt einen Teil einer Position (Partial Close).
    /// Verwendet die ORIGINAL-Seite für positionSide (nicht die Close-Seite).
    /// </summary>
    public async Task ClosePartialAsync(string symbol, Side originalSide, decimal quantity)
    {
        var closeSide = originalSide == Side.Buy ? Side.Sell : Side.Buy;
        // positionSide = ORIGINAL-Seite: Hedge-Mode braucht LONG/SHORT der bestehenden Position
        var positionSide = await GetPositionSideAsync(originalSide).ConfigureAwait(false);

        // Quantity auf erlaubte Precision truncaten
        var adjustedQty = _symbolInfoCache.TruncateQuantity(symbol, quantity);
        if (adjustedQty <= 0)
        {
            _logger.LogWarning("Partial Close: Quantity nach Truncation ist 0 für {Symbol} (Original: {Qty})", symbol, quantity);
            return;
        }

        _logger.LogInformation("Partial Close: {Symbol} {Side} Qty={Quantity} (Original: {OrigQty})", symbol, originalSide, adjustedQty, quantity);

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["side"] = SideToString(closeSide),
            ["type"] = "MARKET",
            ["quantity"] = adjustedQty.ToString(CultureInfo.InvariantCulture),
            ["positionSide"] = positionSide
        };

        await SendSignedRequestAsync<BingXOrderData>(
            HttpMethod.Post,
            "/openApi/swap/v2/trade/order",
            parameters,
            "orders");
    }

    /// <summary>
    /// Platziert eine Limit Take-Profit Order für einen Teil der Position (Partial TP).
    /// Wird als TAKE_PROFIT Limit-Order mit spezifischer Quantity platziert (nicht closePosition).
    /// Maker-Fee 0.02% statt Taker 0.05%.
    /// </summary>
    public async Task<Order> PlaceTpLimitOrderAsync(string symbol, Side positionSide, decimal quantity, decimal triggerPrice)
    {
        var closeSide = positionSide == Side.Buy ? Side.Sell : Side.Buy;
        var positionSideStr = await GetPositionSideAsync(positionSide).ConfigureAwait(false);

        // Precision anwenden
        var adjustedQty = _symbolInfoCache.TruncateQuantity(symbol, quantity);
        var roundedPrice = _symbolInfoCache.RoundPrice(symbol, triggerPrice);
        if (adjustedQty <= 0) return new Order("", symbol, closeSide, OrderType.TakeProfitMarket,
            triggerPrice, quantity, triggerPrice, DateTime.UtcNow, OrderStatus.Rejected);

        _logger.LogInformation("TP Limit-Order: {Symbol} {Side} Qty={Quantity} @ {Price} (Original: Qty={OrigQty}, Price={OrigPrice})",
            symbol, positionSide, adjustedQty, roundedPrice, quantity, triggerPrice);

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["side"] = SideToString(closeSide),
            ["type"] = "TAKE_PROFIT",
            ["quantity"] = adjustedQty.ToString(CultureInfo.InvariantCulture),
            ["stopPrice"] = roundedPrice.ToString(CultureInfo.InvariantCulture),
            ["price"] = roundedPrice.ToString(CultureInfo.InvariantCulture),
            ["positionSide"] = positionSideStr,
            ["workingType"] = "MARK_PRICE"
        };

        var data = await SendSignedRequestAsync<BingXOrderData>(
            HttpMethod.Post,
            "/openApi/swap/v2/trade/order",
            parameters,
            "orders").ConfigureAwait(false);
        var detail = data.Order;
        return detail != null
            ? new Order(detail.OrderId, symbol, closeSide, OrderType.TakeProfitMarket,
                triggerPrice, quantity, triggerPrice, DateTime.UtcNow, OrderStatus.New)
            : new Order("", symbol, closeSide, OrderType.TakeProfitMarket,
                triggerPrice, quantity, triggerPrice, DateTime.UtcNow, OrderStatus.Rejected);
    }

    /// <summary>
    /// Platziert eine native TAKE_PROFIT_MARKET Order für einen Teil der Position (Partial TP).
    /// Wird bei TriggerPrice als Market-Order ausgeführt → garantierter Fill, Taker-Fee.
    /// Vorteil gegenüber Limit: Kein Matching-Risiko, nativ auf BingX auch bei Bot-Ausfall.
    /// </summary>
    public async Task<Order> PlaceTpMarketOrderAsync(string symbol, Side positionSide, decimal quantity, decimal triggerPrice)
    {
        var closeSide = positionSide == Side.Buy ? Side.Sell : Side.Buy;
        var positionSideStr = await GetPositionSideAsync(positionSide).ConfigureAwait(false);

        var adjustedQty = _symbolInfoCache.TruncateQuantity(symbol, quantity);
        var roundedPrice = _symbolInfoCache.RoundPrice(symbol, triggerPrice);
        if (adjustedQty <= 0) return new Order("", symbol, closeSide, OrderType.TakeProfitMarket,
            triggerPrice, quantity, triggerPrice, DateTime.UtcNow, OrderStatus.Rejected);

        _logger.LogInformation("TP Market-Order: {Symbol} {Side} Qty={Quantity} @ {TriggerPrice}",
            symbol, positionSide, adjustedQty, roundedPrice);

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["side"] = SideToString(closeSide),
            ["type"] = "TAKE_PROFIT_MARKET",
            ["quantity"] = adjustedQty.ToString(CultureInfo.InvariantCulture),
            ["stopPrice"] = roundedPrice.ToString(CultureInfo.InvariantCulture),
            ["positionSide"] = positionSideStr,
            ["workingType"] = "MARK_PRICE"
        };

        var data = await SendSignedRequestAsync<BingXOrderData>(
            HttpMethod.Post, "/openApi/swap/v2/trade/order", parameters, "orders").ConfigureAwait(false);
        var detail = data.Order;
        return detail != null
            ? new Order(detail.OrderId, symbol, closeSide, OrderType.TakeProfitMarket,
                triggerPrice, quantity, triggerPrice, DateTime.UtcNow, OrderStatus.New)
            : new Order("", symbol, closeSide, OrderType.TakeProfitMarket,
                triggerPrice, quantity, triggerPrice, DateTime.UtcNow, OrderStatus.Rejected);
    }

    /// <summary>
    /// Platziert eine reguläre LIMIT Reduce-Only Order für Partial TP.
    /// Stackbar: BingX erlaubt beliebig viele LIMIT-Orders pro Position.
    /// Maker-Fee (0.02%) statt Taker (0.05%). Wird für TP1 + TP2 bei Pyramid-Exit verwendet.
    /// </summary>
    public async Task<Order> PlaceTpReduceOnlyLimitAsync(string symbol, Side positionSide, decimal quantity, decimal limitPrice)
    {
        var closeSide = positionSide == Side.Buy ? Side.Sell : Side.Buy;
        var positionSideStr = await GetPositionSideAsync(positionSide).ConfigureAwait(false);

        var adjustedQty = _symbolInfoCache.TruncateQuantity(symbol, quantity);
        var roundedPrice = _symbolInfoCache.RoundPrice(symbol, limitPrice);
        if (adjustedQty <= 0) return new Order("", symbol, closeSide, OrderType.Limit,
            limitPrice, quantity, null, DateTime.UtcNow, OrderStatus.Rejected);

        _logger.LogInformation("TP Limit: {Symbol} {Side} Qty={Quantity} @ {Price} (positionSide={PosSide})",
            symbol, closeSide, adjustedQty, roundedPrice, positionSideStr);

        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["side"] = SideToString(closeSide),
            ["type"] = "LIMIT",
            ["quantity"] = adjustedQty.ToString(CultureInfo.InvariantCulture),
            ["price"] = roundedPrice.ToString(CultureInfo.InvariantCulture),
            ["positionSide"] = positionSideStr,
            ["timeInForce"] = "GTC"
        };
        // reduceOnly nur im One-Way-Mode (BOTH). Im Hedge-Mode (LONG/SHORT) ist
        // side+positionSide bereits eindeutig — reduceOnly wird abgelehnt.
        if (positionSideStr == "BOTH")
            parameters["reduceOnly"] = "true";

        try
        {
            var data = await SendSignedRequestAsync<BingXOrderData>(
                HttpMethod.Post, "/openApi/swap/v2/trade/order", parameters, "orders").ConfigureAwait(false);
            var detail = data.Order;
            return detail != null
                ? new Order(detail.OrderId, symbol, closeSide, OrderType.Limit,
                    roundedPrice, adjustedQty, null, DateTime.UtcNow, ParseOrderStatus(detail.Status))
                : new Order("", symbol, closeSide, OrderType.Limit,
                    roundedPrice, adjustedQty, null, DateTime.UtcNow, OrderStatus.Rejected);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("TP Limit fehlgeschlagen: {Error}", ex.Message);
            return new Order("", symbol, closeSide, OrderType.Limit,
                roundedPrice, adjustedQty, null, DateTime.UtcNow, OrderStatus.Rejected,
                RejectionReason: ex.Message);
        }
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
        // BingX Leverage-API erwartet "LONG"/"SHORT"/"BOTH" (nicht "BUY"/"SELL")
        var positionSide = await GetPositionSideAsync(side).ConfigureAwait(false);
        var parameters = new Dictionary<string, string>
        {
            ["symbol"] = symbol,
            ["leverage"] = leverage.ToString(),
            ["side"] = positionSide
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

    /// <summary>
    /// Kill-Switch: Aktiviert Auto-Cancel-Countdown auf BingX.
    /// Wenn der Bot nicht innerhalb von timeoutMs refresht, cancelt BingX ALLE offenen Orders.
    /// Muss periodisch (z.B. alle 60s) mit neuem Timeout aufgerufen werden.
    /// </summary>
    public async Task ActivateKillSwitchAsync(int timeoutMs = 120_000)
    {
        await SendSignedRequestAsync<BingXCancelAllAfterData>(
            HttpMethod.Post,
            "/openApi/swap/v2/trade/cancelAllAfter",
            new Dictionary<string, string>
            {
                ["type"] = "ACTIVATE",
                ["timeOut"] = timeoutMs.ToString()
            },
            "orders");
    }

    /// <summary>
    /// Kill-Switch deaktivieren (bei sauberem Bot-Stop).
    /// </summary>
    public async Task DeactivateKillSwitchAsync()
    {
        await SendSignedRequestAsync<BingXCancelAllAfterData>(
            HttpMethod.Post,
            "/openApi/swap/v2/trade/cancelAllAfter",
            new Dictionary<string, string>
            {
                ["type"] = "CANCEL"
            },
            "orders");
    }

    /// <summary>
    /// Ändert eine bestehende Order atomar (ohne Cancel+Replace).
    /// Nützlich für SL-Nachziehen ohne schutzlose Lücke.
    /// </summary>
    public async Task<Order> AmendOrderAsync(string orderId, string symbol, decimal? newPrice = null, decimal? newStopPrice = null, decimal? newQuantity = null)
    {
        var parameters = new Dictionary<string, string>
        {
            ["orderId"] = orderId,
            ["symbol"] = symbol
        };
        if (newPrice.HasValue)
            parameters["price"] = newPrice.Value.ToString(CultureInfo.InvariantCulture);
        if (newStopPrice.HasValue)
            parameters["stopPrice"] = newStopPrice.Value.ToString(CultureInfo.InvariantCulture);
        if (newQuantity.HasValue)
            parameters["quantity"] = newQuantity.Value.ToString(CultureInfo.InvariantCulture);

        var data = await SendSignedRequestAsync<BingXOrderData>(
            HttpMethod.Post,
            "/openApi/swap/v1/trade/amend",
            parameters,
            "orders");

        var order = data.Order;
        if (order == null)
            return new Order("", symbol, Side.Buy, OrderType.Market, 0, 0, null, DateTime.UtcNow, OrderStatus.Rejected);

        return new Order(
            order.OrderId, order.Symbol, ParseSide(order.Side), ParseOrderType(order.Type),
            ParseDecimal(order.Price), ParseDecimal(order.Quantity),
            string.IsNullOrEmpty(order.StopPrice) ? null : ParseDecimal(order.StopPrice),
            FromUnixMs(order.CreateTime), ParseOrderStatus(order.Status));
    }

    #endregion

    #region Account

    public async Task<AccountInfo> GetAccountInfoAsync()
    {
        // v3: Response ist ein Array von Balance-Objekten (eines pro Settlement-Asset)
        // Wir handeln nur USDT-M Futures → USDT-Eintrag nehmen
        var balances = await SendSignedRequestAsync<List<BingXBalanceDetail>>(
            HttpMethod.Get,
            "/openApi/swap/v3/user/balance",
            null,
            "queries");

        // USDT-Balance finden (Standard für Perpetual Futures)
        var balance = balances.FirstOrDefault(b => b.Asset == "USDT") ?? balances.FirstOrDefault();
        if (balance == null)
            return new AccountInfo(0, 0, 0, 0);

        return new AccountInfo(
            ParseDecimal(balance.Balance),
            ParseDecimal(balance.AvailableMargin),
            ParseDecimal(balance.UnrealizedProfit),
            ParseDecimal(balance.UsedMargin),
            ParseDecimal(balance.Equity),
            ParseDecimal(balance.RealisedProfit));
    }

    /// <summary>
    /// Liest die tatsächlichen Maker/Taker-Gebühren für den Account.
    /// Ersetzt hardcoded Fee-Konstanten durch echte Werte (VIP-Level-abhängig).
    /// </summary>
    public async Task<(decimal TakerRate, decimal MakerRate)> GetCommissionRateAsync()
    {
        var data = await SendSignedRequestAsync<BingXCommissionData>(
            HttpMethod.Get,
            "/openApi/swap/v2/user/commissionRate",
            null,
            "queries");

        var commission = data.Commission;
        if (commission == null) return (0.0005m, 0.0002m); // Fallback: Standard-Raten
        return (ParseDecimal(commission.TakerCommissionRate), ParseDecimal(commission.MakerCommissionRate));
    }

    /// <summary>
    /// Liest Fund-Flow-History (realisierte PnL, Funding-Fees, Trading-Fees etc.).
    /// Für echtes Performance-Tracking und Steuer-Reporting.
    /// </summary>
    public async Task<List<IncomeRecord>> GetIncomeHistoryAsync(
        string? symbol = null,
        string? incomeType = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100)
    {
        var parameters = new Dictionary<string, string>
        {
            ["limit"] = limit.ToString()
        };
        if (!string.IsNullOrEmpty(symbol)) parameters["symbol"] = symbol;
        if (!string.IsNullOrEmpty(incomeType)) parameters["incomeType"] = incomeType;
        if (startTime.HasValue) parameters["startTime"] = new DateTimeOffset(startTime.Value).ToUnixTimeMilliseconds().ToString();
        if (endTime.HasValue) parameters["endTime"] = new DateTimeOffset(endTime.Value).ToUnixTimeMilliseconds().ToString();

        var data = await SendSignedRequestAsync<List<BingXIncomeDetail>>(
            HttpMethod.Get,
            "/openApi/swap/v2/user/income",
            parameters,
            "queries");

        return data.Select(d => new IncomeRecord(
            d.Symbol, d.IncomeType, ParseDecimal(d.Income), d.Asset,
            d.Info, FromUnixMs(d.Time))).ToList();
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
