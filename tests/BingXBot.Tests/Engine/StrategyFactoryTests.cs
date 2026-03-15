using BingXBot.Engine.Strategies;
using BingXBot.Core.Interfaces;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

/// <summary>
/// Tests für die StrategyFactory - Erstellung aller 5 Strategien.
/// </summary>
public class StrategyFactoryTests
{
    [Theory]
    [InlineData("EMA Cross")]
    [InlineData("RSI")]
    [InlineData("Bollinger Bands")]
    [InlineData("MACD")]
    [InlineData("Grid")]
    public void Create_ShouldReturnCorrectStrategy(string name)
    {
        var strategy = StrategyFactory.Create(name);
        strategy.Should().NotBeNull();
        strategy.Should().BeAssignableTo<IStrategy>();
        strategy.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_UnknownName_ShouldThrow()
    {
        var act = () => StrategyFactory.Create("Unbekannt");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AvailableStrategies_ShouldHave5Entries()
    {
        StrategyFactory.AvailableStrategies.Should().HaveCount(5);
    }

    [Theory]
    [InlineData("EMA Cross")]
    [InlineData("RSI")]
    [InlineData("Bollinger Bands")]
    [InlineData("MACD")]
    [InlineData("Grid")]
    public void Create_ShouldReturnCloneableStrategy(string name)
    {
        var strategy = StrategyFactory.Create(name);
        var clone = strategy.Clone();
        clone.Should().NotBeSameAs(strategy);
        clone.Name.Should().Be(strategy.Name);
        clone.Parameters.Count.Should().Be(strategy.Parameters.Count);
    }
}
