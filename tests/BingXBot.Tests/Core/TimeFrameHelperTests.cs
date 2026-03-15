using BingXBot.Core.Enums;
using BingXBot.Core.Helpers;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Core;

/// <summary>
/// Tests für TimeFrameHelper - Interval-Strings und Dauer-Berechnungen.
/// </summary>
public class TimeFrameHelperTests
{
    [Theory]
    [InlineData(TimeFrame.M1, "1m")]
    [InlineData(TimeFrame.H1, "1h")]
    [InlineData(TimeFrame.D1, "1d")]
    [InlineData(TimeFrame.W1, "1w")]
    public void ToIntervalString_ShouldMapCorrectly(TimeFrame tf, string expected)
    {
        TimeFrameHelper.ToIntervalString(tf).Should().Be(expected);
    }

    [Theory]
    [InlineData(TimeFrame.M1, 1)]
    [InlineData(TimeFrame.M5, 5)]
    [InlineData(TimeFrame.H1, 60)]
    [InlineData(TimeFrame.H4, 240)]
    [InlineData(TimeFrame.D1, 1440)]
    public void ToDuration_ShouldReturnCorrectMinutes(TimeFrame tf, int expectedMinutes)
    {
        TimeFrameHelper.ToDuration(tf).TotalMinutes.Should().Be(expectedMinutes);
    }
}
