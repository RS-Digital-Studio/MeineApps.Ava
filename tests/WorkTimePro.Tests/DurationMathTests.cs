using WorkTimePro.Helpers;

namespace WorkTimePro.Tests;

/// <summary>
/// Tests für DurationMath: DST-bewusste Dauer-Berechnung.
/// Die echten DST-Sprung-Tests laufen nur in einer mitteleuropäischen Zeitzone
/// (RealElapsed nutzt TimeZoneInfo.Local) und werden sonst übersprungen.
/// </summary>
public class DurationMathTests
{
    private static bool IstMitteleuropaeischeZeitzone()
    {
        var id = TimeZoneInfo.Local.Id;
        return id is "W. Europe Standard Time" or "Europe/Berlin" or "Europe/Vienna" or "Europe/Zurich";
    }

    [Fact]
    public void RealElapsed_BeideUtc_DirekteDifferenz()
    {
        var start = new DateTime(2026, 3, 29, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 3, 29, 8, 0, 0, DateTimeKind.Utc);

        DurationMath.RealElapsed(start, end).Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void RealElapsed_OhneZeitumstellung_NaiveDifferenz()
    {
        // Mitten im Januar, gleicher Tag → kein DST-Übergang, Offset-Delta = 0
        var start = new DateTime(2026, 1, 13, 8, 0, 0);
        var end = new DateTime(2026, 1, 13, 16, 30, 0);

        DurationMath.RealElapsed(start, end).Should().Be(TimeSpan.FromHours(8.5));
    }

    [Fact]
    public void RealElapsed_SpringForward_EineStundeWeniger()
    {
        Assert.SkipUnless(IstMitteleuropaeischeZeitzone(),
            "DST-Test benötigt eine mitteleuropäische Zeitzone (TimeZoneInfo.Local).");

        // Nachtschicht über Spring-Forward (29.03.2026, 02:00 → 03:00 MEZ→MESZ):
        // Wall-Clock 22:00 → 06:00 sieht nach 8h aus, real sind nur 7h vergangen.
        var start = new DateTime(2026, 3, 28, 22, 0, 0);
        var end = new DateTime(2026, 3, 29, 6, 0, 0);

        DurationMath.RealElapsed(start, end).Should().Be(TimeSpan.FromHours(7));
    }

    [Fact]
    public void RealElapsed_FallBack_EineStundeMehr()
    {
        Assert.SkipUnless(IstMitteleuropaeischeZeitzone(),
            "DST-Test benötigt eine mitteleuropäische Zeitzone (TimeZoneInfo.Local).");

        // Nachtschicht über Fall-Back (25.10.2026, 03:00 → 02:00 MESZ→MEZ):
        // Wall-Clock 22:00 → 06:00 sieht nach 8h aus, real sind 9h vergangen.
        var start = new DateTime(2026, 10, 24, 22, 0, 0);
        var end = new DateTime(2026, 10, 25, 6, 0, 0);

        DurationMath.RealElapsed(start, end).Should().Be(TimeSpan.FromHours(9));
    }

    [Fact]
    public void RealElapsedMinutes_OhneZeitumstellung_KorrekteMinuten()
    {
        var start = new DateTime(2026, 1, 13, 8, 0, 0);
        var end = new DateTime(2026, 1, 13, 8, 47, 0);

        DurationMath.RealElapsedMinutes(start, end).Should().Be(47.0);
    }
}
