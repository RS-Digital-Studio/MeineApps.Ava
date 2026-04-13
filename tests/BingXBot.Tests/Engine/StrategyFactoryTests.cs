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
    public void Create_SkSystem_ShouldReturnSequenzKonzeptStrategy()
    {
        var strategy = StrategyFactory.Create("SK-System");
        strategy.Should().NotBeNull();
        strategy.Should().BeOfType<SequenzKonzeptStrategy>();
        strategy.Should().BeAssignableTo<IStrategy>();
    }

    [Fact]
    public void Create_UnknownName_ShouldThrow()
    {
        var act = () => StrategyFactory.Create("Unbekannt");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AvailableStrategies_ShouldContainOnlySkSystem()
    {
        StrategyFactory.AvailableStrategies.Should().ContainSingle();
        StrategyFactory.AvailableStrategies[0].Should().Be("SK-System");
    }

    [Fact]
    public void Create_SkSystem_ShouldBeCloneable()
    {
        var strategy = StrategyFactory.Create("SK-System");
        var clone = strategy.Clone();
        clone.Should().NotBeSameAs(strategy);
        clone.Name.Should().Be(strategy.Name);
        clone.Parameters.Count.Should().Be(strategy.Parameters.Count);
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
