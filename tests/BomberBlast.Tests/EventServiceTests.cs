using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für EventService (v2.0.41 LOGIC-FIX-1 + v2.0.44).
/// Kritisch: Christmas spannt 22.12.-02.01., NewYear 31.12.-01.01.
/// Am 31.12. MUSS NewYear matchen (spezifischer/kürzer vor breiter).
/// </summary>
public class EventServiceTests
{
    private static EventService CreateService() => new EventService();

    [Fact]
    public void GetEventForDate_31Dezember_LiefertNewYearNichtChristmas()
    {
        var service = CreateService();
        var date = new DateTime(2026, 12, 31, 12, 0, 0, DateTimeKind.Utc);

        var ev = service.GetEventForDate(date);

        ev.Should().NotBeNull();
        ev!.Type.Should().Be(SeasonalEventType.NewYear, "NewYear ist spezifischer als Christmas am 31.12.");
    }

    [Fact]
    public void GetEventForDate_1Januar_LiefertNewYear()
    {
        var service = CreateService();
        var date = new DateTime(2027, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var ev = service.GetEventForDate(date);

        ev!.Type.Should().Be(SeasonalEventType.NewYear);
    }

    [Fact]
    public void GetEventForDate_28Dezember_LiefertChristmas()
    {
        var service = CreateService();
        var date = new DateTime(2026, 12, 28, 12, 0, 0, DateTimeKind.Utc);

        var ev = service.GetEventForDate(date);

        ev!.Type.Should().Be(SeasonalEventType.Christmas);
    }

    [Fact]
    public void GetEventForDate_2Januar_LiefertChristmasNichtNewYear()
    {
        var service = CreateService();
        // 02.01. ist letzter Christmas-Tag, NewYear ist 01.01. zuende
        var date = new DateTime(2027, 1, 2, 12, 0, 0, DateTimeKind.Utc);

        var ev = service.GetEventForDate(date);

        ev!.Type.Should().Be(SeasonalEventType.Christmas);
    }

    [Fact]
    public void GetEventForDate_31Oktober_LiefertHalloween()
    {
        var service = CreateService();
        var date = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

        var ev = service.GetEventForDate(date);

        ev!.Type.Should().Be(SeasonalEventType.Halloween);
    }

    [Fact]
    public void GetEventForDate_15Juli_LiefertSummer()
    {
        var service = CreateService();
        var date = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

        var ev = service.GetEventForDate(date);

        ev!.Type.Should().Be(SeasonalEventType.Summer);
    }

    [Fact]
    public void GetEventForDate_1Maerz_LiefertNull()
    {
        var service = CreateService();
        // Außerhalb aller 4 Events
        var date = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

        var ev = service.GetEventForDate(date);

        ev.Should().BeNull("Am 1. März gibt es kein Saisonal-Event");
    }

    [Fact]
    public void GetEventForDate_25Oktober_LiefertHalloween()
    {
        var service = CreateService();
        // Erster Halloween-Tag (25.10. - 02.11.)
        var date = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

        var ev = service.GetEventForDate(date);

        ev!.Type.Should().Be(SeasonalEventType.Halloween);
    }

    [Fact]
    public void GetEventForDate_2November_LetzterHalloweenTag()
    {
        var service = CreateService();
        // Letzter Halloween-Tag, vor dem nächsten Event
        var date = new DateTime(2026, 11, 2, 23, 59, 0, DateTimeKind.Utc);

        var ev = service.GetEventForDate(date);

        ev!.Type.Should().Be(SeasonalEventType.Halloween);
    }

    [Fact]
    public void GetEventForDate_22Dezember_ChristmasStart()
    {
        var service = CreateService();
        var date = new DateTime(2026, 12, 22, 0, 0, 0, DateTimeKind.Utc);

        var ev = service.GetEventForDate(date);

        ev!.Type.Should().Be(SeasonalEventType.Christmas);
    }
}
