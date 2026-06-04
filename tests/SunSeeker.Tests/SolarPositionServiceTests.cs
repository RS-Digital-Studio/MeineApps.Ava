using FluentAssertions;
using SunSeeker.Shared.Models;
using SunSeeker.Shared.Services;
using Xunit;

namespace SunSeeker.Tests;

/// <summary>
/// Korrektheits-Tests fuer die Sonnenstandsberechnung. Bewusst algorithmus-unabhaengig:
/// statt Magic-Numbers werden bekannte astronomische Eigenschaften geprueft (Mittags-Elevation
/// an Sonnenwenden/Aequinoktium = 90 - |lat - decl|, Azimut-Symmetrie, Aequinoktium-Aufgang
/// im Osten, Polartag/-nacht, Suedhalbkugel-Azimut im Norden).
/// </summary>
public class SolarPositionServiceTests
{
    private readonly SolarPositionService _svc = new();
    private static readonly GeoLocation Berlin = new(52.5200, 13.4050, 38);

    private static DateOnly SummerSolstice => new(2024, 6, 21);
    private static DateOnly WinterSolstice => new(2024, 12, 21);
    private static DateOnly Equinox => new(2024, 3, 20);

    [Fact]
    public void MittagsElevation_Sommersonnenwende_Berlin()
    {
        // 90 - lat + 23.44 (Deklination zur Sommersonnenwende)
        var arc = _svc.GetDayArc(Berlin, SummerSolstice, 5);
        var maxElevation = arc.Max(p => p.Elevation);

        maxElevation.Should().BeApproximately(90 - 52.52 + 23.44, 1.5);
    }

    [Fact]
    public void MittagsElevation_Wintersonnenwende_Berlin()
    {
        var arc = _svc.GetDayArc(Berlin, WinterSolstice, 5);
        var maxElevation = arc.Max(p => p.Elevation);

        maxElevation.Should().BeApproximately(90 - 52.52 - 23.44, 1.5);
    }

    [Fact]
    public void MittagsElevation_Aequinoktium_Berlin()
    {
        var arc = _svc.GetDayArc(Berlin, Equinox, 5);
        var maxElevation = arc.Max(p => p.Elevation);

        maxElevation.Should().BeApproximately(90 - 52.52, 1.5);
    }

    [Fact]
    public void Azimut_BeiHoechststand_ZeigtNachSueden()
    {
        var arc = _svc.GetDayArc(Berlin, SummerSolstice, 5);
        var peak = arc.OrderByDescending(p => p.Elevation).First();

        peak.Azimuth.Should().BeApproximately(180, 3);
    }

    [Fact]
    public void Azimut_VormittagsOst_NachmittagsWest()
    {
        var times = _svc.GetSunTimes(Berlin, SummerSolstice);
        var noon = times.SolarNoonUtc;

        _svc.GetPosition(Berlin, noon.AddHours(-3)).Azimuth.Should().BeLessThan(180);
        _svc.GetPosition(Berlin, noon.AddHours(3)).Azimuth.Should().BeGreaterThan(180);
    }

    [Fact]
    public void Aequinoktium_SonneGehtImOstenAuf()
    {
        var times = _svc.GetSunTimes(Berlin, Equinox);
        times.SunriseUtc.Should().NotBeNull();

        var atSunrise = _svc.GetPosition(Berlin, times.SunriseUtc!.Value);
        atSunrise.Azimuth.Should().BeApproximately(90, 4); // Ost
    }

    [Fact]
    public void SunTimes_AufgangVorUntergang_HoechststandDazwischen()
    {
        var times = _svc.GetSunTimes(Berlin, SummerSolstice);

        times.SunriseUtc.Should().NotBeNull();
        times.SunsetUtc.Should().NotBeNull();
        times.SunriseUtc!.Value.Should().BeBefore(times.SolarNoonUtc);
        times.SolarNoonUtc.Should().BeBefore(times.SunsetUtc!.Value);
    }

    [Fact]
    public void Polartag_AmNordkapImSommer()
    {
        var nordkap = new GeoLocation(71.17, 25.78);
        var times = _svc.GetSunTimes(nordkap, SummerSolstice);

        times.PolarDay.Should().BeTrue();
        times.PolarNight.Should().BeFalse();
    }

    [Fact]
    public void Polarnacht_AmNordkapImWinter()
    {
        var nordkap = new GeoLocation(71.17, 25.78);
        var times = _svc.GetSunTimes(nordkap, WinterSolstice);

        times.PolarNight.Should().BeTrue();
        times.PolarDay.Should().BeFalse();
    }

    [Fact]
    public void Suedhalbkugel_HoechststandZeigtNachNorden()
    {
        // Sydney: zur Suedsommer-Sonnenwende steht die Sonne mittags im NORDEN (Azimut ~0/360).
        var sydney = new GeoLocation(-33.87, 151.21);
        var arc = _svc.GetDayArc(sydney, WinterSolstice, 5);
        var peak = arc.OrderByDescending(p => p.Elevation).First();

        (peak.Azimuth < 5 || peak.Azimuth > 355).Should().BeTrue(
            $"Azimut war {peak.Azimuth:0.0}°, erwartet nahe Nord (0/360)");
    }

    [Fact]
    public void Elevation_NachtsNegativ()
    {
        // Mitternacht Ortszeit in Berlin (ca. 23:00 UTC): Sonne deutlich unter dem Horizont.
        var midnight = new DateTime(2024, 6, 21, 23, 0, 0, DateTimeKind.Utc);
        var pos = _svc.GetPosition(Berlin, midnight);

        pos.IsDaylight.Should().BeFalse();
        pos.Elevation.Should().BeLessThan(0);
    }

    [Fact]
    public void Azimut_ImmerImGueltigenBereich()
    {
        var arc = _svc.GetDayArc(Berlin, Equinox, 10);

        arc.Should().OnlyContain(p => p.Azimuth >= 0 && p.Azimuth < 360);
    }
}
