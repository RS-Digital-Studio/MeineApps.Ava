using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Gemeinsame Tests für alle Strategien (RSI, Bollinger, MACD, Grid).
/// Prüft: None bei wenig Daten, Parameters vorhanden, Clone funktioniert.
/// </summary>
public class StrategyTests
{
    public static IEnumerable<object[]> AllStrategies =>
        new List<object[]>
        {
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
        var candles = TestHelper.GenerateTestCandles(100);
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

    // RSI-spezifisch
    [Fact]
    public void Rsi_Parameters_ShouldContainPeriodAndThresholds()
    {
        var strategy = new RsiStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "Period");
        strategy.Parameters.Should().Contain(p => p.Name == "Oversold");
        strategy.Parameters.Should().Contain(p => p.Name == "Overbought");
    }

    // Bollinger-spezifisch
    [Fact]
    public void Bollinger_Parameters_ShouldContainPeriodAndStdDev()
    {
        var strategy = new BollingerStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "Period");
        strategy.Parameters.Should().Contain(p => p.Name == "StdDev");
    }

    // MACD-spezifisch
    [Fact]
    public void Macd_Parameters_ShouldContainAllPeriods()
    {
        var strategy = new MacdStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "FastPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "SlowPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "SignalPeriod");
    }

    // Grid-spezifisch
    [Fact]
    public void Grid_Parameters_ShouldContainGridSettings()
    {
        var strategy = new GridStrategy();
        strategy.Parameters.Should().Contain(p => p.Name == "GridLevels");
        strategy.Parameters.Should().Contain(p => p.Name == "GridSpacing");
        strategy.Parameters.Should().Contain(p => p.Name == "UpperBound");
        strategy.Parameters.Should().Contain(p => p.Name == "LowerBound");
    }

    [Fact]
    public void Grid_Evaluate_WithBounds_ShouldWork()
    {
        var strategy = new GridStrategy();
        var candles = TestHelper.GenerateTestCandles(100, startPrice: 100m, volatility: 2m);
        var context = TestHelper.CreateContext(candles);

        var result = strategy.Evaluate(context);

        result.Signal.Should().BeOneOf(Signal.None, Signal.Long, Signal.Short);
        result.Reason.Should().NotBeNullOrEmpty();
    }
}
