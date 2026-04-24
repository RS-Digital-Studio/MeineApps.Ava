using BingXBot.Core.Enums;
using BingXBot.Engine.Risk;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für Pip-Buffer unter Point 0 (Task 4.5).</summary>
public class SlBufferPipsTests
{
    [Fact]
    public void Long_BufferVerschiebtSlUnterPoint0()
    {
        // Forex EURUSD: Point0=1.0900, Buffer 8 Pips (0.0008)
        var slOhneBuffer = PipStopLossCalculator.CalculateBookStopLoss(
            "EURUSD-USDT", MarketCategory.Forex,
            entryPrice: 1.1000m, isLong: true,
            fib786: 1.0950m, point0: 1.0900m,
            isSingleTrade: true, bufferPips: 0m);

        var slMitBuffer = PipStopLossCalculator.CalculateBookStopLoss(
            "EURUSD-USDT", MarketCategory.Forex,
            entryPrice: 1.1000m, isLong: true,
            fib786: 1.0950m, point0: 1.0900m,
            isSingleTrade: true, bufferPips: 8m);

        // Mit Buffer darf der SL bis zu 8 Pips unter Point 0 liegen
        (slOhneBuffer - slMitBuffer).Should().BeInRange(0m, 0.001m,
            "Mit Buffer kann SL maximal 8 Pips weiter weg sein, aber nur wenn fib786/pipCap es zulassen");
    }

    [Fact]
    public void Short_BufferVerschiebtSlUeberPoint0()
    {
        // Short: Buffer geht nach oben
        var slMitBuffer = PipStopLossCalculator.CalculateBookStopLoss(
            "EURUSD-USDT", MarketCategory.Forex,
            entryPrice: 1.0900m, isLong: false,
            fib786: 1.0950m, point0: 1.1000m,
            isSingleTrade: true, bufferPips: 8m);

        // SL muss zwischen Entry und Point0+Buffer liegen
        slMitBuffer.Should().BeGreaterThan(1.0900m);
        slMitBuffer.Should().BeLessThanOrEqualTo(1.1000m + 8m * 0.0001m + 0.0001m);
    }

    [Fact]
    public void Point0ClampBleibt_WennFib786Gross()
    {
        // fib786 weit weg, Pip-Cap greift, Point0 klammert
        var sl = PipStopLossCalculator.CalculateBookStopLoss(
            "EURUSD-USDT", MarketCategory.Forex,
            entryPrice: 1.1000m, isLong: true,
            fib786: 1.0500m, point0: 1.0900m,  // fib786 unter Point0 → egal, Point0 klammert
            isSingleTrade: true, bufferPips: 0m);

        sl.Should().BeGreaterThanOrEqualTo(1.0900m, "Ohne Buffer darf SL nicht unter Point 0");
    }

    [Fact]
    public void BcklBuffer_ClampetUnterPointB()
    {
        var slMitBuffer = PipStopLossCalculator.CalculateBcklStopLoss(
            "EURUSD-USDT", MarketCategory.Forex,
            entryPrice: 1.0950m, isLong: true,
            pointB: 1.0930m,
            isSingleTrade: true, bufferPips: 5m);

        // PointB - 5 Pips = 1.0925
        slMitBuffer.Should().BeGreaterThanOrEqualTo(1.0925m - 0.0001m);
        slMitBuffer.Should().BeLessThan(1.0950m);
    }

    [Fact]
    public void Crypto_BufferPipsAngewandt_ProzentualZumPreis()
    {
        // BTC @ 76000, 12 Pips × (76000 × 0.0001) = 91.2 USD Buffer
        // fib786=75500 ist näher am Entry als der effectivePoint0 (74908.8).
        // Der SL nutzt entweder fib786 oder pipCap — bufferPips verschiebt nur die untere Grenze.
        var sl = PipStopLossCalculator.CalculateBookStopLoss(
            "BTC-USDT", MarketCategory.Crypto,
            entryPrice: 76000m, isLong: true,
            fib786: 75500m, point0: 75000m,
            isSingleTrade: false, bufferPips: 12m);

        // SL liegt zwischen effectivePoint0 (74908.8) und Entry (76000)
        sl.Should().BeGreaterThan(74908m);
        sl.Should().BeLessThan(76000m);
    }

    [Fact]
    public void BufferPipsNull_VerhaeltSichWieOhneBuffer()
    {
        var slA = PipStopLossCalculator.CalculateBookStopLoss(
            "BTC-USDT", MarketCategory.Crypto,
            entryPrice: 76000m, isLong: true,
            fib786: 75500m, point0: 75000m,
            isSingleTrade: false, bufferPips: 0m);
        var slB = PipStopLossCalculator.CalculateBookStopLoss(
            "BTC-USDT", MarketCategory.Crypto,
            entryPrice: 76000m, isLong: true,
            fib786: 75500m, point0: 75000m,
            isSingleTrade: false);
        slA.Should().Be(slB);
    }

    [Fact]
    public void BufferPips_SlNiemalsJenseitsEntry()
    {
        var sl = PipStopLossCalculator.CalculateBookStopLoss(
            "EURUSD-USDT", MarketCategory.Forex,
            entryPrice: 1.1000m, isLong: true,
            fib786: 1.0995m, point0: 1.0990m,
            isSingleTrade: true, bufferPips: 1000m);  // riesiger Buffer
        // SL muss trotzdem unter Entry bleiben
        sl.Should().BeLessThan(1.1000m);
    }
}
