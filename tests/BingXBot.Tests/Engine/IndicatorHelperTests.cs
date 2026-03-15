using BingXBot.Engine.Indicators;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

public class IndicatorHelperTests
{
    [Fact]
    public void ToQuotes_ShouldConvertAllCandles()
    {
        var candles = TestHelper.GenerateTestCandles(10);
        var quotes = IndicatorHelper.ToQuotes(candles).ToList();

        quotes.Should().HaveCount(10);
        quotes[0].Open.Should().Be(candles[0].Open);
        quotes[0].Close.Should().Be(candles[0].Close);
        quotes[0].High.Should().Be(candles[0].High);
        quotes[0].Low.Should().Be(candles[0].Low);
        quotes[0].Volume.Should().Be(candles[0].Volume);
    }

    [Fact]
    public void CalculateEma_ShouldReturnCorrectCount()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var ema = IndicatorHelper.CalculateEma(candles, 10);

        ema.Should().HaveCount(50);
    }

    [Fact]
    public void CalculateEma_WarmupPeriod_ShouldHaveNulls()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var ema = IndicatorHelper.CalculateEma(candles, 10);

        // Erste Werte sind null (Warmup)
        ema[0].Should().BeNull();

        // Nach Warmup sollten Werte vorhanden sein
        ema[^1].Should().NotBeNull();
        ema[^1]!.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateEma_ShouldBeNearPrice()
    {
        var candles = TestHelper.GenerateTestCandles(50, startPrice: 100m);
        var ema = IndicatorHelper.CalculateEma(candles, 10);

        // EMA sollte in der Nähe der Preise liegen
        var lastEma = ema[^1]!.Value;
        lastEma.Should().BeInRange(80m, 120m);
    }

    [Fact]
    public void CalculateSma_ShouldReturnValues()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var sma = IndicatorHelper.CalculateSma(candles, 20);

        sma.Should().HaveCount(50);
        sma[^1].Should().NotBeNull();
    }

    [Fact]
    public void CalculateRsi_ShouldBeInRange()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var rsi = IndicatorHelper.CalculateRsi(candles, 14);

        rsi.Should().HaveCount(50);

        // RSI-Werte müssen zwischen 0 und 100 liegen
        var validValues = rsi.Where(r => r.HasValue).Select(r => r!.Value).ToList();
        validValues.Should().NotBeEmpty();
        validValues.Should().AllSatisfy(v =>
        {
            v.Should().BeGreaterThanOrEqualTo(0m);
            v.Should().BeLessThanOrEqualTo(100m);
        });
    }

    [Fact]
    public void CalculateMacd_ShouldReturnThreeComponents()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var (macd, signal, histogram) = IndicatorHelper.CalculateMacd(candles);

        macd.Should().HaveCount(50);
        signal.Should().HaveCount(50);
        histogram.Should().HaveCount(50);
    }

    [Fact]
    public void CalculateBollinger_UpperShouldBeAboveLower()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var (upper, middle, lower) = IndicatorHelper.CalculateBollinger(candles, 20, 2m);

        upper.Should().HaveCount(50);

        // Nach Warmup: Upper > Middle > Lower
        var lastUpper = upper[^1];
        var lastMiddle = middle[^1];
        var lastLower = lower[^1];

        lastUpper.Should().NotBeNull();
        lastMiddle.Should().NotBeNull();
        lastLower.Should().NotBeNull();
        lastUpper!.Value.Should().BeGreaterThan(lastMiddle!.Value);
        lastMiddle.Value.Should().BeGreaterThan(lastLower!.Value);
    }

    [Fact]
    public void CalculateAtr_ShouldBePositive()
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var atr = IndicatorHelper.CalculateAtr(candles, 14);

        atr.Should().HaveCount(50);
        var lastAtr = atr[^1];
        lastAtr.Should().NotBeNull();
        lastAtr!.Value.Should().BeGreaterThan(0m);
    }
}
