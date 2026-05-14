using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für EventCalendarService (Phase 20 — L2).
/// Validiert ISO-Wochen-Berechnung, deterministische Pool-Rotation, Multiplier-Mapping
/// und Wochen-Range-Berechnung.
/// </summary>
public class EventCalendarServiceTests
{
    [Fact]
    public void GetEventForWeek_LiefertDeterministischesEvent()
    {
        var svc = new EventCalendarService();
        var event1 = svc.GetEventForWeek(2026, 19);
        var event2 = svc.GetEventForWeek(2026, 19);

        event1.Type.Should().Be(event2.Type, "deterministisch — gleiche Woche → gleiches Event");
        event1.IsoYear.Should().Be(2026);
        event1.IsoWeek.Should().Be(19);
    }

    [Fact]
    public void GetEventForWeek_VerschiedeneWochen_LiefertVerschiedeneEvents()
    {
        var svc = new EventCalendarService();
        var distinctTypes = new HashSet<WeeklyEventType>();
        // Über 8 aufeinanderfolgende Wochen sollten alle 8 Pool-Typen vorkommen
        for (int week = 1; week <= 8; week++)
        {
            distinctTypes.Add(svc.GetEventForWeek(2026, week).Type);
        }
        distinctTypes.Should().HaveCount(8, "8 Wochen sollten alle 8 Pool-Typen abdecken");
    }

    [Fact]
    public void GetEventForWeek_VerschiedeneJahre_RotierenAnders()
    {
        var svc = new EventCalendarService();
        var w1_2026 = svc.GetEventForWeek(2026, 19);
        var w1_2027 = svc.GetEventForWeek(2027, 19);
        // Year-Salt verschiebt Pool-Index → ungleicher Type
        w1_2026.Type.Should().NotBe(w1_2027.Type);
    }

    [Fact]
    public void GetEventForDate_GibtSelbesEventInnerhalbDerWoche()
    {
        var svc = new EventCalendarService();
        // Montag 12:00 und Sonntag 23:00 in derselben ISO-Woche → gleiches Event
        var monday = new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc); // ISO 2026-W19 Mo
        var sunday = new DateTime(2026, 5, 10, 23, 0, 0, DateTimeKind.Utc);
        svc.GetEventForDate(monday).Type.Should().Be(svc.GetEventForDate(sunday).Type);
    }

    [Fact]
    public void GetIsoWeekRange_LiefertMontagBisSonntag()
    {
        var (start, end) = EventCalendarService.GetIsoWeekRange(2026, 19);
        start.DayOfWeek.Should().Be(DayOfWeek.Monday);
        // End ist Sonntag 23:59:59.9999999
        end.DayOfWeek.Should().Be(DayOfWeek.Sunday);
        (end - start).TotalDays.Should().BeApproximately(6.999988, 0.001);
    }

    [Fact]
    public void Multiplier_LiegtImErwartetenBereich()
    {
        var svc = new EventCalendarService();
        for (int week = 1; week <= 53; week++)
        {
            var ev = svc.GetEventForWeek(2026, week);
            ev.Multiplier.Should().BeInRange(1.0f, 2.0f);
        }
    }

    [Fact]
    public void AccentColorHex_StartsWithHashtag()
    {
        var svc = new EventCalendarService();
        for (int week = 1; week <= 8; week++)
        {
            var ev = svc.GetEventForWeek(2026, week);
            ev.AccentColorHex.Should().StartWith("#");
            ev.AccentColorHex.Length.Should().Be(7);
        }
    }

    [Fact]
    public void GetUpcomingEvents_LiefertAngegebeneAnzahlWochen()
    {
        var svc = new EventCalendarService();
        var upcoming = svc.GetUpcomingEvents(5);
        upcoming.Should().HaveCount(5);

        // Wochen müssen aufeinanderfolgen (jeweils 7 Tage Abstand der WeekStartUtc)
        for (int i = 1; i < upcoming.Count; i++)
        {
            (upcoming[i].WeekStartUtc - upcoming[i - 1].WeekStartUtc).TotalDays
                .Should().BeApproximately(7, 1.5);
        }
    }

    [Fact]
    public void NameKey_FolgtRESXKonvention()
    {
        var svc = new EventCalendarService();
        var ev = svc.GetEventForWeek(2026, 19);
        ev.NameKey.Should().StartWith("WeeklyEvent_");
        ev.DescriptionKey.Should().EndWith("_Desc");
    }

    [Fact]
    public void HoursRemaining_NegativAlsNullClampt()
    {
        var svc = new EventCalendarService();
        var ev = svc.GetEventForWeek(2026, 1);
        // 5 Jahre nach dem Event-Ende → Hours sollte 0 sein, nicht negativ
        var future = ev.WeekEndUtc.AddYears(5);
        ev.HoursRemaining(future).Should().Be(0);
    }

    [Fact]
    public void GetWeekEventOverride_StubGibtNullZurueck()
    {
        var svc = new EventCalendarService();
        svc.GetWeekEventOverride(2026, 19).Should().BeNull();
    }
}
