using BingXBot.Engine.Risk;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine;

public class CorrelationCheckerTests
{
    [Fact]
    public void CalculatePearson_PerfectCorrelation_ShouldReturn1()
    {
        var x = new decimal[] { 1, 2, 3, 4, 5 };
        var y = new decimal[] { 2, 4, 6, 8, 10 };
        var result = CorrelationChecker.CalculatePearson(x, y);
        result.Should().BeApproximately(1m, 0.01m);
    }

    [Fact]
    public void CalculatePearson_NegativeCorrelation_ShouldReturnMinus1()
    {
        var x = new decimal[] { 1, 2, 3, 4, 5 };
        var y = new decimal[] { 10, 8, 6, 4, 2 };
        var result = CorrelationChecker.CalculatePearson(x, y);
        result.Should().BeApproximately(-1m, 0.01m);
    }

    [Fact]
    public void CalculatePearson_NoCorrelation_ShouldBeNearZero()
    {
        var x = new decimal[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var y = new decimal[] { 5, 2, 8, 1, 7, 3, 6, 4 };
        var result = CorrelationChecker.CalculatePearson(x, y);
        Math.Abs(result).Should().BeLessThan(0.5m);
    }
}
