using BingXBot.Core.Configuration;
using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Core.Simulation;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Core;

public class SimulatedExchangeTests
{
    private SimulatedExchange CreateExchange(decimal balance = 10000m)
    {
        return new SimulatedExchange(new BacktestSettings { InitialBalance = balance });
    }

    [Fact]
    public async Task GetAccountInfo_ShouldReturnInitialBalance()
    {
        var exchange = CreateExchange(5000m);
        var account = await exchange.GetAccountInfoAsync();
        account.Balance.Should().Be(5000m);
        account.AvailableBalance.Should().Be(5000m);
    }

    [Fact]
    public async Task PlaceOrder_Market_ShouldCreatePosition()
    {
        var exchange = CreateExchange();
        exchange.SetCurrentPrice("BTC-USDT", 50000m);

        var order = await exchange.PlaceOrderAsync(new OrderRequest(
            "BTC-USDT", Side.Buy, OrderType.Market, 0.1m));

        order.Status.Should().Be(OrderStatus.Filled);
        var positions = await exchange.GetPositionsAsync();
        positions.Should().ContainSingle(p => p.Symbol == "BTC-USDT");
    }

    [Fact]
    public async Task PlaceOrder_ShouldApplyFees()
    {
        var exchange = CreateExchange();
        exchange.SetCurrentPrice("BTC-USDT", 50000m);

        await exchange.PlaceOrderAsync(new OrderRequest(
            "BTC-USDT", Side.Buy, OrderType.Market, 0.1m));

        var account = await exchange.GetAccountInfoAsync();
        // Fee = 0.0005 * 0.1 * 50000 = 2.5 USDT
        account.Balance.Should().BeLessThan(10000m);
    }

    [Fact]
    public async Task CloseAllPositions_ShouldCloseEverything()
    {
        var exchange = CreateExchange();
        exchange.SetCurrentPrice("BTC-USDT", 50000m);
        exchange.SetCurrentPrice("ETH-USDT", 3000m);
        await exchange.PlaceOrderAsync(new OrderRequest("BTC-USDT", Side.Buy, OrderType.Market, 0.1m));
        await exchange.PlaceOrderAsync(new OrderRequest("ETH-USDT", Side.Buy, OrderType.Market, 1m));

        await exchange.CloseAllPositionsAsync();

        var positions = await exchange.GetPositionsAsync();
        positions.Should().BeEmpty();
    }

    [Fact]
    public async Task ClosePosition_ShouldGenerateCompletedTrade()
    {
        var exchange = CreateExchange();
        exchange.SetCurrentPrice("BTC-USDT", 50000m);
        await exchange.PlaceOrderAsync(new OrderRequest("BTC-USDT", Side.Buy, OrderType.Market, 0.1m));

        exchange.SetCurrentPrice("BTC-USDT", 51000m);
        await exchange.ClosePositionAsync("BTC-USDT", Side.Buy);

        var trades = exchange.GetCompletedTrades();
        trades.Should().ContainSingle();
        trades[0].Pnl.Should().BeGreaterThan(0); // Profit
    }
}
