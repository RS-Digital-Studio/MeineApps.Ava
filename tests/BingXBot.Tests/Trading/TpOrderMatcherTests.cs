using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Trading.Reconciliation;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Trading;

// Phase 18 / G7 (Teil-Extraktion) — Pure-Function-Tests fuer TpOrderMatcher.
// Vorher musste man den ganzen LiveTradingService instantiieren um diesen Pfad zu testen.
public class TpOrderMatcherTests
{
    private static Order MakeOrder(string symbol, Side side, OrderType type, decimal qty, decimal price, bool reduceOnly)
        => new(OrderId: "test-1", Symbol: symbol, Side: side, Type: type, Price: price,
            Quantity: qty, StopPrice: null, CreateTime: DateTime.UtcNow, Status: OrderStatus.New, ReduceOnly: reduceOnly);

    [Fact]
    public void FindMatchingTpOrder_ExactMatch_Returns()
    {
        var orders = new[]
        {
            MakeOrder("BTC-USDT", Side.Sell, OrderType.Limit, 0.1m, 51000m, reduceOnly: true)
        };
        var match = TpOrderMatcher.FindMatchingTpOrder(orders, "BTC-USDT", Side.Sell, 0.1m, 51000m);
        match.Should().NotBeNull();
    }

    [Fact]
    public void FindMatchingTpOrder_DifferentSymbol_NoMatch()
    {
        var orders = new[]
        {
            MakeOrder("ETH-USDT", Side.Sell, OrderType.Limit, 0.1m, 51000m, reduceOnly: true)
        };
        TpOrderMatcher.FindMatchingTpOrder(orders, "BTC-USDT", Side.Sell, 0.1m, 51000m).Should().BeNull();
    }

    [Fact]
    public void FindMatchingTpOrder_DifferentSide_NoMatch()
    {
        var orders = new[]
        {
            MakeOrder("BTC-USDT", Side.Buy, OrderType.Limit, 0.1m, 51000m, reduceOnly: true)
        };
        TpOrderMatcher.FindMatchingTpOrder(orders, "BTC-USDT", Side.Sell, 0.1m, 51000m).Should().BeNull();
    }

    [Fact]
    public void FindMatchingTpOrder_NotReduceOnly_NoMatch()
    {
        var orders = new[]
        {
            MakeOrder("BTC-USDT", Side.Sell, OrderType.Limit, 0.1m, 51000m, reduceOnly: false)
        };
        TpOrderMatcher.FindMatchingTpOrder(orders, "BTC-USDT", Side.Sell, 0.1m, 51000m).Should().BeNull();
    }

    [Fact]
    public void FindMatchingTpOrder_NotLimitType_NoMatch()
    {
        var orders = new[]
        {
            MakeOrder("BTC-USDT", Side.Sell, OrderType.Market, 0.1m, 51000m, reduceOnly: true)
        };
        TpOrderMatcher.FindMatchingTpOrder(orders, "BTC-USDT", Side.Sell, 0.1m, 51000m).Should().BeNull();
    }

    [Fact]
    public void FindMatchingTpOrder_QuantityWithinTolerance_Matches()
    {
        // 0.1 ± 0.5 % = 0.0995 bis 0.1005
        var orders = new[]
        {
            MakeOrder("BTC-USDT", Side.Sell, OrderType.Limit, 0.1004m, 51000m, reduceOnly: true)
        };
        TpOrderMatcher.FindMatchingTpOrder(orders, "BTC-USDT", Side.Sell, 0.1m, 51000m).Should().NotBeNull();
    }

    [Fact]
    public void FindMatchingTpOrder_QuantityOutsideTolerance_NoMatch()
    {
        var orders = new[]
        {
            MakeOrder("BTC-USDT", Side.Sell, OrderType.Limit, 0.11m, 51000m, reduceOnly: true) // 10% Drift
        };
        TpOrderMatcher.FindMatchingTpOrder(orders, "BTC-USDT", Side.Sell, 0.1m, 51000m).Should().BeNull();
    }

    [Fact]
    public void FindMatchingTpOrder_PriceWithinTolerance_Matches()
    {
        // 51000 ± 0.05 % = 50975 bis 51025
        var orders = new[]
        {
            MakeOrder("BTC-USDT", Side.Sell, OrderType.Limit, 0.1m, 51020m, reduceOnly: true)
        };
        TpOrderMatcher.FindMatchingTpOrder(orders, "BTC-USDT", Side.Sell, 0.1m, 51000m).Should().NotBeNull();
    }

    [Fact]
    public void FindMatchingTpOrder_CustomTolerances_AppliedCorrectly()
    {
        // Strenger als Default: 0.01 % statt 0.5 %
        var orders = new[]
        {
            MakeOrder("BTC-USDT", Side.Sell, OrderType.Limit, 0.1004m, 51000m, reduceOnly: true)
        };
        // Mit strenger Toleranz scheitert der Match.
        TpOrderMatcher.FindMatchingTpOrder(orders, "BTC-USDT", Side.Sell, 0.1m, 51000m,
            quantityTolerancePercent: 0.0001m).Should().BeNull();
    }

    [Fact]
    public void FindMatchingTpOrder_MultipleOrders_ReturnsFirst()
    {
        var orders = new[]
        {
            MakeOrder("BTC-USDT", Side.Sell, OrderType.Limit, 0.1m, 51000m, reduceOnly: true) with { OrderId = "first" },
            MakeOrder("BTC-USDT", Side.Sell, OrderType.Limit, 0.1m, 51000m, reduceOnly: true) with { OrderId = "second" }
        };
        var match = TpOrderMatcher.FindMatchingTpOrder(orders, "BTC-USDT", Side.Sell, 0.1m, 51000m);
        match.Should().NotBeNull();
        match!.OrderId.Should().Be("first");
    }
}
