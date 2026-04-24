using BingXBot.Engine.News;
using FluentAssertions;
using Xunit;

namespace BingXBot.Tests.Engine.Sk;

/// <summary>Phase 5 — Tests für News-Filter (Task 1.2).</summary>
public class NewsBlackoutTests
{
    [Fact]
    public async Task Stub_GetEventsAsync_LiefertLeereListe()
    {
        var svc = new StubEconomicCalendarService();
        var events = await svc.GetEventsAsync(DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task Stub_GetActiveBlackoutEventAsync_LiefertNull()
    {
        var svc = new StubEconomicCalendarService();
        var ev = await svc.GetActiveBlackoutEventAsync(DateTime.UtcNow, 30);
        ev.Should().BeNull();
    }

    [Fact]
    public void EconomicEvent_KannInstantiiertWerden()
    {
        var ev = new EconomicEvent(
            DateTime.UtcNow, "US", "FOMC", EconomicEventImpact.High, "USD");
        ev.Country.Should().Be("US");
        ev.Impact.Should().Be(EconomicEventImpact.High);
    }

    [Fact]
    public void EconomicEventImpact_DreiStufen()
    {
        Enum.GetNames<EconomicEventImpact>().Should().Contain(new[] { "Low", "Medium", "High" });
    }

    [Fact]
    public void RiskSettings_NewsBlackoutMinutes_DefaultDreissig()
    {
        var settings = new BingXBot.Core.Configuration.RiskSettings();
        settings.NewsBlackoutMinutes.Should().Be(30);
    }

    [Fact]
    public void RiskSettings_NewsBlackoutMinutes_KannGeaendertWerden()
    {
        var settings = new BingXBot.Core.Configuration.RiskSettings { NewsBlackoutMinutes = 60 };
        settings.NewsBlackoutMinutes.Should().Be(60);
    }
}
