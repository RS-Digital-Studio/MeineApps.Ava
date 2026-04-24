using System.Text.Json;
using System.Text.Json.Serialization;

namespace BingXBot.Exchange.Models;

/// <summary>
/// Konvertiert flexibel zwischen string und Zahl (BingX API ist inkonsistent:
/// manche Felder kommen als "3", andere als 3).
/// </summary>
public class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? "",
            JsonTokenType.Number => reader.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => "",
            _ => reader.GetString() ?? ""
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

// Basis-Response
public class BingXResponse<T>
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("msg")] public string? Msg { get; set; }
    [JsonPropertyName("data")] public T? Data { get; set; }
}

// Account (v3: data ist ein Array von Balance-Objekten, eines pro Settlement-Asset)
// Wrapper für Deserialisierung: SendSignedRequestAsync<List<BingXBalanceDetail>> direkt
public class BingXBalanceData
{
    [JsonPropertyName("balance")] public BingXBalanceDetail? Balance { get; set; }
}

public class BingXBalanceDetail
{
    [JsonPropertyName("asset")] public string Asset { get; set; } = "USDT";
    [JsonPropertyName("balance"), JsonConverter(typeof(FlexibleStringConverter))] public string Balance { get; set; } = "";
    [JsonPropertyName("equity"), JsonConverter(typeof(FlexibleStringConverter))] public string Equity { get; set; } = "";
    [JsonPropertyName("availableMargin"), JsonConverter(typeof(FlexibleStringConverter))] public string AvailableMargin { get; set; } = "";
    [JsonPropertyName("unrealizedProfit"), JsonConverter(typeof(FlexibleStringConverter))] public string UnrealizedProfit { get; set; } = "";
    [JsonPropertyName("realisedProfit"), JsonConverter(typeof(FlexibleStringConverter))] public string RealisedProfit { get; set; } = "";
    [JsonPropertyName("usedMargin"), JsonConverter(typeof(FlexibleStringConverter))] public string UsedMargin { get; set; } = "";
}

// Order
public class BingXOrderData
{
    [JsonPropertyName("order")] public BingXOrderDetail? Order { get; set; }
}

public class BingXOrderDetail
{
    [JsonPropertyName("orderId"), JsonConverter(typeof(FlexibleStringConverter))] public string OrderId { get; set; } = "";
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
    [JsonPropertyName("side")] public string Side { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("price"), JsonConverter(typeof(FlexibleStringConverter))] public string Price { get; set; } = "";
    [JsonPropertyName("quantity"), JsonConverter(typeof(FlexibleStringConverter))] public string Quantity { get; set; } = "";
    [JsonPropertyName("stopPrice"), JsonConverter(typeof(FlexibleStringConverter))] public string? StopPrice { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("createTime")] public long CreateTime { get; set; }
}

// Position (BingX gibt manche Felder als Zahl statt String zurück → FlexibleStringConverter)
public class BingXPositionDetail
{
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
    [JsonPropertyName("positionSide")] public string PositionSide { get; set; } = "";
    [JsonPropertyName("avgPrice"), JsonConverter(typeof(FlexibleStringConverter))] public string AvgPrice { get; set; } = "";
    [JsonPropertyName("markPrice"), JsonConverter(typeof(FlexibleStringConverter))] public string MarkPrice { get; set; } = "";
    [JsonPropertyName("positionAmt"), JsonConverter(typeof(FlexibleStringConverter))] public string PositionAmt { get; set; } = "";
    [JsonPropertyName("unrealizedProfit"), JsonConverter(typeof(FlexibleStringConverter))] public string UnrealizedProfit { get; set; } = "";
    [JsonPropertyName("leverage"), JsonConverter(typeof(FlexibleStringConverter))] public string Leverage { get; set; } = "";
    [JsonPropertyName("marginType")] public string MarginType { get; set; } = "";
}

// Kline
public class BingXKlineDetail
{
    [JsonPropertyName("open"), JsonConverter(typeof(FlexibleStringConverter))] public string Open { get; set; } = "";
    [JsonPropertyName("high"), JsonConverter(typeof(FlexibleStringConverter))] public string High { get; set; } = "";
    [JsonPropertyName("low"), JsonConverter(typeof(FlexibleStringConverter))] public string Low { get; set; } = "";
    [JsonPropertyName("close"), JsonConverter(typeof(FlexibleStringConverter))] public string Close { get; set; } = "";
    [JsonPropertyName("volume"), JsonConverter(typeof(FlexibleStringConverter))] public string Volume { get; set; } = "";
    [JsonPropertyName("time")] public long Time { get; set; }
}

// Ticker
public class BingXTickerDetail
{
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
    [JsonPropertyName("lastPrice")] public string LastPrice { get; set; } = "";
    [JsonPropertyName("bidPrice")] public string BidPrice { get; set; } = "";
    [JsonPropertyName("askPrice")] public string AskPrice { get; set; } = "";
    /// <summary>Basis-Einheiten (z.B. BTC-Kontrakte, Gold-Unzen, TSLA-Aktien). NICHT fuer Filter nutzen.</summary>
    [JsonPropertyName("volume")] public string Volume { get; set; } = "";
    /// <summary>24h-Handelsvolumen in USDT — universell vergleichbar ueber alle Kategorien.</summary>
    [JsonPropertyName("quoteVolume")] public string QuoteVolume { get; set; } = "";
    [JsonPropertyName("priceChangePercent")] public string PriceChangePercent { get; set; } = "";
}

// Funding Rate
public class BingXFundingRateData
{
    [JsonPropertyName("lastFundingRate")] public string LastFundingRate { get; set; } = "";
}

// Open Interest
public class BingXOpenInterest
{
    [JsonPropertyName("openInterest")] public string OpenInterest { get; set; } = "";
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
    [JsonPropertyName("time")] public long Time { get; set; }
}

// Contract-Details (für Quantity/Price-Precision und Min-Order-Größe)
public class BingXContractDetail
{
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
    [JsonPropertyName("quantityPrecision")] public int QuantityPrecision { get; set; }
    [JsonPropertyName("pricePrecision")] public int PricePrecision { get; set; }
    [JsonPropertyName("tradeMinQuantity"), JsonConverter(typeof(FlexibleStringConverter))] public string TradeMinQuantity { get; set; } = "0";
    [JsonPropertyName("tradeMinUSDT"), JsonConverter(typeof(FlexibleStringConverter))] public string TradeMinUSDT { get; set; } = "5";
}

// Server-Zeit Response
public class BingXServerTime
{
    [JsonPropertyName("serverTime")] public long ServerTime { get; set; }
}

// Commission-Rate Response
public class BingXCommissionData
{
    [JsonPropertyName("commission")] public BingXCommissionDetail? Commission { get; set; }
}

public class BingXCommissionDetail
{
    [JsonPropertyName("takerCommissionRate"), JsonConverter(typeof(FlexibleStringConverter))] public string TakerCommissionRate { get; set; } = "";
    [JsonPropertyName("makerCommissionRate"), JsonConverter(typeof(FlexibleStringConverter))] public string MakerCommissionRate { get; set; } = "";
}

// Fund-Flow (Income) Response
public class BingXIncomeDetail
{
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
    [JsonPropertyName("incomeType")] public string IncomeType { get; set; } = "";
    [JsonPropertyName("income"), JsonConverter(typeof(FlexibleStringConverter))] public string Income { get; set; } = "";
    [JsonPropertyName("asset")] public string Asset { get; set; } = "";
    [JsonPropertyName("info")] public string Info { get; set; } = "";
    [JsonPropertyName("time")] public long Time { get; set; }
    [JsonPropertyName("tranId"), JsonConverter(typeof(FlexibleStringConverter))] public string TranId { get; set; } = "";
}

// Cancel-All-After (Kill-Switch) Response
public class BingXCancelAllAfterData
{
    [JsonPropertyName("triggerTime")] public long TriggerTime { get; set; }
}
