using BingXBot.Engine.Strategies;
using BingXBot.Core.Interfaces;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Tests für die StrategyFactory. Nach Buch-Refactoring (12.04.2026): nur noch SK-System.
/// </summary>
public class StrategyFactoryTests
{
    [Fact]
    public void Create_UnknownName_ShouldThrow()
    {
        var act = () => StrategyFactory.Create("Unbekannt");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("TrendFollow")]
    [InlineData("TrendFollow-Fast")]
    [InlineData("TrendFollow-Wide")]
    [InlineData("TrendFollow-Strong")]
    public void Create_TrendFamily_ShouldReturnCloneableStrategy(string name)
    {
        var strategy = StrategyFactory.Create(name);
        strategy.Should().NotBeNull();
        strategy.Should().BeAssignableTo<IStrategy>();
        var clone = strategy.Clone();
        clone.Should().NotBeSameAs(strategy);
        clone.Name.Should().Be(strategy.Name);
    }

    [Theory]
    [InlineData("CryptoTrendPro")]
    [InlineData("Trend-Following")]
    [InlineData("EMA Cross")]
    [InlineData("RSI Momentum")]
    [InlineData("Bollinger Breakout")]
    [InlineData("MACD")]
    [InlineData("Smart Grid")]
    [InlineData("Breakout-Pullback")]
    public void Create_RemovedStrategies_ShouldThrow(string removedName)
    {
        // Nach Buch-Refactoring (12.04.2026) sind alle Non-SK-Strategien entfernt
        var act = () => StrategyFactory.Create(removedName);
        act.Should().Throw<ArgumentException>();
    }
}
