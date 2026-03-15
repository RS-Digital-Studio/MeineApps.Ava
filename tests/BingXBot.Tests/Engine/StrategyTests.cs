using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Gemeinsame Tests für alle 6 Strategien (krypto-optimiert).
/// Prüft: None bei wenig Daten, Parameters vorhanden, Clone funktioniert.
/// </summary>
public class StrategyTests
{
    public static IEnumerable<object[]> AllStrategies =>
        new List<object[]>
        {
            new object[] { new TrendFollowStrategy() },
            new object[] { new RsiStrategy() },
            new object[] { new BollingerStrategy() },
            new object[] { new MacdStrategy() },
            new object[] { new GridStrategy() },
        };

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Evaluate_TooFewCandles_ShouldReturnNone(IStrategy strategy)
    {
        var candles = TestHelper.GenerateTestCandles(5);
        var context = TestHelper.CreateContext(candles);

        var result = strategy.Evaluate(context);

        result.Signal.Should().Be(Signal.None);
        result.Reason.Should().Contain("Zu wenig Daten");
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Evaluate_EnoughCandles_ShouldNotThrow(IStrategy strategy)
    {
        // 250 Candles damit auch EMA200 genug Daten hat
        var candles = TestHelper.GenerateTestCandles(250);
        var context = TestHelper.CreateContext(candles);

        var act = () => strategy.Evaluate(context);

        act.Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Parameters_ShouldNotBeEmpty(IStrategy strategy)
    {
        strategy.Parameters.Should().NotBeEmpty();
        strategy.Parameters.Should().AllSatisfy(p =>
        {
            p.Name.Should().NotBeNullOrEmpty();
            p.Description.Should().NotBeNullOrEmpty();
            p.ValueType.Should().NotBeNullOrEmpty();
        });
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Clone_ShouldCreateIndependentInstance(IStrategy strategy)
    {
        var clone = strategy.Clone();

        clone.Should().NotBeSameAs(strategy);
        clone.Name.Should().Be(strategy.Name);
        clone.Description.Should().Be(strategy.Description);
        clone.Parameters.Should().HaveCount(strategy.Parameters.Count);
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Name_ShouldNotBeEmpty(IStrategy strategy)
    {
        strategy.Name.Should().NotBeNullOrEmpty();
        strategy.Description.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void WarmUp_ShouldNotThrow(IStrategy strategy)
    {
        var candles = TestHelper.GenerateTestCandles(50);
        var act = () => strategy.WarmUp(candles);
        act.Should().NotThrow();
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Reset_ShouldNotThrow(IStrategy strategy)
    {
        var act = () => strategy.Reset();
        act.Should().NotThrow();
    }

    // RSI Momentum spezifisch
    [Fact]
    public void RsiMomentum_Parameters_ShouldContainMomentumSettings()
    {
        var strategy = new RsiStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "Period");
        strategy.Parameters.Should().Contain(p => p.Name == "LongTrigger");
        strategy.Parameters.Should().Contain(p => p.Name == "LongEntry");
        strategy.Parameters.Should().Contain(p => p.Name == "ShortTrigger");
        strategy.Parameters.Should().Contain(p => p.Name == "ShortEntry");
        strategy.Parameters.Should().Contain(p => p.Name == "DivergenceLookback");
    }

    [Fact]
    public void RsiMomentum_Name_ShouldBeUpdated()
    {
        var strategy = new RsiStrategy();
        strategy.Name.Should().Be("RSI Momentum");
        strategy.Description.Should().Contain("Krypto");
    }

    // Bollinger Breakout spezifisch
    [Fact]
    public void BollingerBreakout_Parameters_ShouldContainSqueezeSettings()
    {
        var strategy = new BollingerStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "Period");
        strategy.Parameters.Should().Contain(p => p.Name == "StdDev");
        strategy.Parameters.Should().Contain(p => p.Name == "SqueezePeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "VolumePeriod");
    }

    [Fact]
    public void BollingerBreakout_Name_ShouldBeUpdated()
    {
        var strategy = new BollingerStrategy();
        strategy.Name.Should().Be("Bollinger Breakout");
        strategy.Description.Should().Contain("Squeeze");
    }

    // MACD spezifisch
    [Fact]
    public void Macd_Parameters_ShouldContainAllPeriods()
    {
        var strategy = new MacdStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "FastPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "SlowPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "SignalPeriod");
    }

    [Fact]
    public void Macd_Description_ShouldBeUpdated()
    {
        var strategy = new MacdStrategy();
        strategy.Description.Should().Contain("Histogram");
    }

    // Smart Grid spezifisch
    [Fact]
    public void SmartGrid_Parameters_ShouldContainDynamicSettings()
    {
        var strategy = new GridStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "GridLevels");
        strategy.Parameters.Should().Contain(p => p.Name == "GridSpacing");
        strategy.Parameters.Should().Contain(p => p.Name == "EmaPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "BollingerPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "TrendThreshold");
    }

    [Fact]
    public void SmartGrid_Name_ShouldBeUpdated()
    {
        var strategy = new GridStrategy();
        strategy.Name.Should().Be("Smart Grid");
        strategy.Description.Should().Contain("Range");
    }

    [Fact]
    public void SmartGrid_Evaluate_WithData_ShouldWork()
    {
        var strategy = new GridStrategy();
        var candles = TestHelper.GenerateTestCandles(250, startPrice: 100m, volatility: 2m);
        var context = TestHelper.CreateContext(candles);

        var result = strategy.Evaluate(context);

        result.Signal.Should().BeOneOf(Signal.None, Signal.Long, Signal.Short);
        result.Reason.Should().NotBeNullOrEmpty();
    }

    // Trend-Following spezifisch
    [Fact]
    public void TrendFollowing_Parameters_ShouldContainAllIndicators()
    {
        var strategy = new TrendFollowStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "EmaFast");
        strategy.Parameters.Should().Contain(p => p.Name == "EmaSlow");
        strategy.Parameters.Should().Contain(p => p.Name == "RsiPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "AtrPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "AtrMultiplierSl");
        strategy.Parameters.Should().Contain(p => p.Name == "AtrMultiplierTp");
        strategy.Parameters.Should().Contain(p => p.Name == "VolumePeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "VolumeMultiplier");
        strategy.Parameters.Should().Contain(p => p.Name == "MinConfidence");
    }

    [Fact]
    public void TrendFollowing_Evaluate_WithTrendingCandles_ShouldWork()
    {
        var strategy = new TrendFollowStrategy();
        // Starker Aufwärtstrend
        var candles = TestHelper.GenerateTrendingCandles(100, startPrice: 50000m, uptrend: true);
        var context = TestHelper.CreateContext(candles);

        var result = strategy.Evaluate(context);

        result.Signal.Should().BeOneOf(Signal.None, Signal.Long, Signal.Short);
        result.Reason.Should().NotBeNullOrEmpty();
    }
}
