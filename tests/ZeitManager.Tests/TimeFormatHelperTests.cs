using FluentAssertions;
using Xunit;
using ZeitManager.Audio;

namespace ZeitManager.Tests;

/// <summary>
/// Tests fuer TimeFormatHelper - Zeitformatierung für Stoppuhr und Timer.
/// TimeSpan-Konstruktor: new TimeSpan(days, hours, minutes, seconds, milliseconds)
/// </summary>
public class TimeFormatHelperTests
{
    [Fact]
    public void Format_UnterEinerStunde_OhneStundenAnzeige()
    {
        // 5 Minuten, 30 Sekunden, 450 ms → "05:30.45"
        var zeit = new TimeSpan(0, 0, 5, 30, 450);

        var ergebnis = TimeFormatHelper.Format(zeit);

        ergebnis.Should().Be("05:30.45");
    }

    [Fact]
    public void Format_NullZeit_ZeroFormat()
    {
        var ergebnis = TimeFormatHelper.Format(TimeSpan.Zero);

        ergebnis.Should().Be("00:00.00");
    }

    [Fact]
    public void Format_EineStundeOderMehr_MitStundenAnzeige()
    {
        // 1 Stunde 23 Minuten 45 Sekunden 100ms → "01:23:45.10"
        var zeit = new TimeSpan(0, 1, 23, 45, 100);

        var ergebnis = TimeFormatHelper.Format(zeit);

        ergebnis.Should().Be("01:23:45.10");
    }

    [Fact]
    public void Format_ExaktEineStunde_MitStundenAnzeige()
    {
        // Grenzfall: genau 1:00:00.00
        var zeit = new TimeSpan(0, 1, 0, 0, 0);

        var ergebnis = TimeFormatHelper.Format(zeit);

        ergebnis.Should().Be("01:00:00.00");
    }

    [Fact]
    public void Format_Millisekunden999_ZweistelligCentiseconds()
    {
        // 0:00:00:59.990 → Sekunden=59, ms=990 → Centiseconds=99 → "00:59.99"
        var zeit = new TimeSpan(0, 0, 0, 59, 990);

        var ergebnis = TimeFormatHelper.Format(zeit);

        ergebnis.Should().Be("00:59.99");
    }

    [Fact]
    public void Format_100Millisekunden_ZweistelligMitNull()
    {
        // 0:00:01:40.100 → Sekunden=40, ms=100 → Centiseconds=10 → "01:40.10"
        var zeit = new TimeSpan(0, 0, 1, 40, 100);

        var ergebnis = TimeFormatHelper.Format(zeit);

        ergebnis.Should().Be("01:40.10");
    }

    [Fact]
    public void Format_EinstelligeMinutenUndSekunden_NullPaddingKorrekt()
    {
        // 1 Minute 5 Sekunden → "01:05.00"
        var zeit = new TimeSpan(0, 0, 1, 5, 0);

        var ergebnis = TimeFormatHelper.Format(zeit);

        ergebnis.Should().Be("01:05.00");
    }

    [Fact]
    public void Format_59Minuten59Sekunden_GrenzwertVorStunde()
    {
        // Knapp unter 1 Stunde: 59:59.00
        var zeit = new TimeSpan(0, 0, 59, 59, 0);

        var ergebnis = TimeFormatHelper.Format(zeit);

        ergebnis.Should().Be("59:59.00");
    }

    [Fact]
    public void Format_10Stunden_ZweistelligeStunden()
    {
        // 10 Stunden → "10:00:00.00"
        var zeit = new TimeSpan(0, 10, 0, 0, 0);

        var ergebnis = TimeFormatHelper.Format(zeit);

        ergebnis.Should().Be("10:00:00.00");
    }

    [Fact]
    public void Format_GibtKonsistentesFormatZurueck_KeinNullOderLeer()
    {
        // Format darf niemals null oder leer zurückgeben
        var ergebnis = TimeFormatHelper.Format(TimeSpan.FromMilliseconds(1));

        ergebnis.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Format_UnterEinerStunde_EnthaeltKeinStundenPraefix()
    {
        // 30 Minuten sollte NUR MM:SS.cs ausgeben, kein H-Präfix
        var zeit = new TimeSpan(0, 0, 30, 0, 0);

        var ergebnis = TimeFormatHelper.Format(zeit);

        // Nur ein Doppelpunkt: "30:00.00" (kein zweites ":")
        ergebnis.Count(c => c == ':').Should().Be(1);
    }

    [Fact]
    public void Format_MitStunden_EnthaeltZweiDoppelpunkte()
    {
        // 1:30:00.00 hat zwei Doppelpunkte
        var zeit = new TimeSpan(0, 1, 30, 0, 0);

        var ergebnis = TimeFormatHelper.Format(zeit);

        ergebnis.Count(c => c == ':').Should().Be(2);
    }
}
