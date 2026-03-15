using BingXBot.Core.Enums;
using BingXBot.Engine.Strategies;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

public class EmaCrossStrategyTests
{
    [Fact]
    public void Evaluate_TooFewCandles_ShouldReturnNone()
    {
        var strategy = new EmaCrossStrategy();
        var candles = TestHelper.GenerateTestCandles(10);
        var context = TestHelper.CreateContext(candles);

        var result = strategy.Evaluate(context);

        result.Signal.Should().Be(Signal.None);
        result.Reason.Should().Contain("Zu wenig Daten");
    }

    [Fact]
    public void Evaluate_EnoughCandles_ShouldNotThrow()
    {
        var strategy = new EmaCrossStrategy();
        // Braucht 200+ Candles wegen EMA200 Trend-Filter
        var candles = TestHelper.GenerateTestCandles(250);
        var context = TestHelper.CreateContext(candles);

        var act = () => strategy.Evaluate(context);

        act.Should().NotThrow();
    }

    [Fact]
    public void Evaluate_WithData_ShouldReturnValidSignal()
    {
        var strategy = new EmaCrossStrategy();
        var candles = TestHelper.GenerateTestCandles(250);
        var context = TestHelper.CreateContext(candles);

        var result = strategy.Evaluate(context);

        result.Signal.Should().BeOneOf(Signal.None, Signal.Long, Signal.Short);
        result.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parameters_ShouldContainAllNewParameters()
    {
        var strategy = new EmaCrossStrategy();

        strategy.Parameters.Should().Contain(p => p.Name == "FastPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "SlowPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "TrendPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "AtrPeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "VolumePeriod");
        strategy.Parameters.Should().Contain(p => p.Name == "TpMultiplier");
        strategy.Parameters.Should().Contain(p => p.Name == "MinAtrPercent");
    }

    [Fact]
    public void Clone_ShouldCreateIndependentInstance()
    {
        var strategy = new EmaCrossStrategy();
        var clone = strategy.Clone();

        clone.Should().NotBeSameAs(strategy);
        clone.Name.Should().Be(strategy.Name);
        clone.Parameters.Should().HaveCount(strategy.Parameters.Count);
    }

    [Fact]
    public void Name_ShouldBeEmaCross()
    {
        var strategy = new EmaCrossStrategy();
        strategy.Name.Should().Be("EMA Cross");
    }

    [Fact]
    public void Description_ShouldMentionKrypto()
    {
        var strategy = new EmaCrossStrategy();
        strategy.Description.Should().Contain("Krypto");
    }

    [Fact]
    public void DefaultPeriods_ShouldBeBroader()
    {
        var strategy = new EmaCrossStrategy();
        // Fast=12, Slow=26 statt 9/21 (weniger Noise)
        strategy.Parameters.Should().Contain(p => p.Name == "FastPeriod" && (int)p.DefaultValue == 12);
        strategy.Parameters.Should().Contain(p => p.Name == "SlowPeriod" && (int)p.DefaultValue == 26);
    }

    [Fact]
    public void WarmUp_ShouldNotThrow()
    {
        var strategy = new EmaCrossStrategy();
        var candles = TestHelper.GenerateTestCandles(50);

        var act = () => strategy.WarmUp(candles);
        act.Should().NotThrow();
    }

    [Fact]
    public void Reset_ShouldNotThrow()
    {
        var strategy = new EmaCrossStrategy();
        var act = () => strategy.Reset();
        act.Should().NotThrow();
    }
}
