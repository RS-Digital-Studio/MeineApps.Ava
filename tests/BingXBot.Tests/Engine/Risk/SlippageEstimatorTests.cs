using BingXBot.Core.Enums;
using BingXBot.Core.Models;
using BingXBot.Engine.Risk;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Risk;

// v1.6.2 Phase 12 — Slippage-Estimate fuer Market-Orders.
public class SlippageEstimatorTests
{
    [Fact]
    public void EmptyBook_ReturnsInsufficientLiquidity()
    {
        var book = new OrderBook("BTC-USDT", DateTime.UtcNow, Array.Empty<OrderBookLevel>(), Array.Empty<OrderBookLevel>());
        var est = SlippageEstimator.Estimate(book, Side.Buy, quantity: 0.1m, referencePrice: 50000m);
        est.InsufficientLiquidity.Should().BeTrue();
        est.FilledQuantity.Should().Be(0m);
    }

    [Fact]
    public void EnoughLiquidityFirstLevel_LowSlippage()
    {
        // First Ask 50000, Bid 49999. Buy 0.1 → kompletter Fill am ersten Ask = 50000.
        // Slippage zu RefPrice 50000 = 0 %.
        var book = MakeBook(
            asks: new[] { (50000m, 1m), (50100m, 1m) },
            bids: new[] { (49999m, 1m), (49900m, 1m) });
        var est = SlippageEstimator.Estimate(book, Side.Buy, quantity: 0.1m, referencePrice: 50000m);
        est.FilledQuantity.Should().Be(0.1m);
        est.InsufficientLiquidity.Should().BeFalse();
        est.EstimatedAvgFillPrice.Should().Be(50000m);
        est.SlippagePercent.Should().Be(0m);
    }

    [Fact]
    public void Walks_Multiple_Levels()
    {
        // Buy 1.5: 1.0 @ 50000 + 0.5 @ 50100 = 75050 / 1.5 ≈ 50033.33.
        // Slippage = (50033.33 - 50000) / 50000 * 100 ≈ 0.067 %.
        var book = MakeBook(
            asks: new[] { (50000m, 1m), (50100m, 1m) },
            bids: new[] { (49999m, 1m) });
        var est = SlippageEstimator.Estimate(book, Side.Buy, quantity: 1.5m, referencePrice: 50000m);
        est.FilledQuantity.Should().Be(1.5m);
        est.InsufficientLiquidity.Should().BeFalse();
        est.SlippagePercent.Should().BeApproximately(0.0667m, 0.001m);
    }

    [Fact]
    public void Buy_Walks_Asks_Sell_Walks_Bids()
    {
        var book = MakeBook(
            asks: new[] { (50100m, 0.1m) },
            bids: new[] { (49900m, 0.1m) });

        var buyEst = SlippageEstimator.Estimate(book, Side.Buy, 0.1m, 50000m);
        buyEst.EstimatedAvgFillPrice.Should().Be(50100m);
        buyEst.SlippagePercent.Should().BeApproximately(0.2m, 0.001m);

        var sellEst = SlippageEstimator.Estimate(book, Side.Sell, 0.1m, 50000m);
        sellEst.EstimatedAvgFillPrice.Should().Be(49900m);
        sellEst.SlippagePercent.Should().BeApproximately(0.2m, 0.001m, "Sell-Slippage gegen Bid");
    }

    [Fact]
    public void InsufficientLiquidity_ReturnsPartialEstimate()
    {
        // Buy 5.0, aber Ask hat nur 1.0 verfuegbar → InsufficientLiquidity.
        var book = MakeBook(
            asks: new[] { (50000m, 1m) },
            bids: new[] { (49999m, 1m) });
        var est = SlippageEstimator.Estimate(book, Side.Buy, quantity: 5m, referencePrice: 50000m);
        est.InsufficientLiquidity.Should().BeTrue();
        est.FilledQuantity.Should().Be(1m);
    }

    [Fact]
    public void ZeroQuantity_ReturnsRefPriceWithZeroSlippage()
    {
        var book = MakeBook(asks: new[] { (50000m, 1m) }, bids: new[] { (49999m, 1m) });
        var est = SlippageEstimator.Estimate(book, Side.Buy, 0m, 50000m);
        est.SlippagePercent.Should().Be(0m);
    }

    [Fact]
    public void ZeroReferencePrice_HandlesGracefully()
    {
        var book = MakeBook(asks: new[] { (50000m, 1m) }, bids: new[] { (49999m, 1m) });
        var est = SlippageEstimator.Estimate(book, Side.Buy, 0.1m, 0m);
        est.SlippagePercent.Should().Be(0m, "RefPrice=0 ist invalid → defensiver Fallback");
    }

    private static OrderBook MakeBook((decimal price, decimal qty)[] asks, (decimal price, decimal qty)[] bids)
    {
        return new OrderBook(
            "BTC-USDT",
            DateTime.UtcNow,
            Bids: bids.Select(b => new OrderBookLevel(b.price, b.qty)).ToList(),
            Asks: asks.Select(a => new OrderBookLevel(a.price, a.qty)).ToList());
    }
}
