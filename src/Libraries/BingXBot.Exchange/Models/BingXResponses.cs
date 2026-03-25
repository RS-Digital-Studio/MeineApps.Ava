using System.Text.Json.Serialization;

namespace BingXBot.Exchange.Models;

// Basis-Response
public class BingXResponse<T>
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("msg")] public string? Msg { get; set; }
    [JsonPropertyName("data")] public T? Data { get; set; }
}

// Account
public class BingXBalanceData
{
    [JsonPropertyName("balance")] public BingXBalanceDetail? Balance { get; set; }
}

public class BingXBalanceDetail
{
    [JsonPropertyName("balance")] public string Balance { get; set; } = "";
    [JsonPropertyName("equity")] public string Equity { get; set; } = "";
    [JsonPropertyName("availableMargin")] public string AvailableMargin { get; set; } = "";
    [JsonPropertyName("unrealizedProfit")] public string UnrealizedProfit { get; set; } = "";
    [JsonPropertyName("realisedProfit")] public string RealisedProfit { get; set; } = "";
    [JsonPropertyName("usedMargin")] public string UsedMargin { get; set; } = "";
}

// Order
public class BingXOrderData
{
    [JsonPropertyName("order")] public BingXOrderDetail? Order { get; set; }
}

public class BingXOrderDetail
{
    [JsonPropertyName("orderId")] public string OrderId { get; set; } = "";
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
    [JsonPropertyName("side")] public string Side { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("price")] public string Price { get; set; } = "";
    [JsonPropertyName("quantity")] public string Quantity { get; set; } = "";
    [JsonPropertyName("stopPrice")] public string? StopPrice { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("createTime")] public long CreateTime { get; set; }
}

// Position
public class BingXPositionDetail
{
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
    [JsonPropertyName("positionSide")] public string PositionSide { get; set; } = "";
    [JsonPropertyName("avgPrice")] public string AvgPrice { get; set; } = "";
    [JsonPropertyName("markPrice")] public string MarkPrice { get; set; } = "";
    [JsonPropertyName("positionAmt")] public string PositionAmt { get; set; } = "";
    [JsonPropertyName("unrealizedProfit")] public string UnrealizedProfit { get; set; } = "";
    [JsonPropertyName("leverage")] public string Leverage { get; set; } = "";
    [JsonPropertyName("marginType")] public string MarginType { get; set; } = "";
}

// Kline
public class BingXKlineDetail
{
    [JsonPropertyName("open")] public string Open { get; set; } = "";
    [JsonPropertyName("high")] public string High { get; set; } = "";
    [JsonPropertyName("low")] public string Low { get; set; } = "";
    [JsonPropertyName("close")] public string Close { get; set; } = "";
    [JsonPropertyName("volume")] public string Volume { get; set; } = "";
    [JsonPropertyName("time")] public long Time { get; set; }
}

// Ticker
public class BingXTickerDetail
{
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
    [JsonPropertyName("lastPrice")] public string LastPrice { get; set; } = "";
    [JsonPropertyName("bidPrice")] public string BidPrice { get; set; } = "";
    [JsonPropertyName("askPrice")] public string AskPrice { get; set; } = "";
    [JsonPropertyName("volume")] public string Volume { get; set; } = "";
    [JsonPropertyName("priceChangePercent")] public string PriceChangePercent { get; set; } = "";
}

// Funding Rate
public class BingXFundingRateData
{
    [JsonPropertyName("lastFundingRate")] public string LastFundingRate { get; set; } = "";
}
