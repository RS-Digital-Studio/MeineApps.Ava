using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

public class DifferentialSnapshotServiceTests
{
    private readonly DifferentialSnapshotService _svc = new(new CoordinateService());

    private static SurveyPoint Pt(int id, double lat, double lon, double alt = 100, string? label = null) => new()
    {
        Id = id,
        Latitude = lat,
        Longitude = lon,
        Altitude = alt,
        Label = label,
    };

    [Fact]
    public void Compare_BeideListenLeer_LiefertLeeresErgebnis()
    {
        var r = _svc.Compare([], []);
        r.Matches.Should().BeEmpty();
        r.Added.Should().BeEmpty();
        r.Removed.Should().BeEmpty();
    }

    [Fact]
    public void Compare_NurNeuePunkte_AlleAlsAddedMarkiert()
    {
        var newPts = new[] { Pt(1, 48.0, 9.0), Pt(2, 48.0001, 9.0) };
        var r = _svc.Compare([], newPts);
        r.Added.Should().HaveCount(2);
        r.Matches.Should().BeEmpty();
        r.Removed.Should().BeEmpty();
    }

    [Fact]
    public void Compare_NurAltePunkte_AlleAlsRemovedMarkiert()
    {
        var oldPts = new[] { Pt(1, 48.0, 9.0), Pt(2, 48.0001, 9.0) };
        var r = _svc.Compare(oldPts, []);
        r.Removed.Should().HaveCount(2);
        r.Matches.Should().BeEmpty();
        r.Added.Should().BeEmpty();
    }

    [Fact]
    public void Compare_GleichePosition_LiefertUnchangedMatch()
    {
        var old = new[] { Pt(1, 48.0, 9.0) };
        var nu = new[] { Pt(99, 48.0, 9.0) };
        var r = _svc.Compare(old, nu);
        r.Matches.Should().HaveCount(1);
        r.Matches[0].Change.Should().Be(DifferentialChange.Unchanged);
        r.Matches[0].DistanceMeters.Should().BeApproximately(0.0, 0.001);
        r.Added.Should().BeEmpty();
        r.Removed.Should().BeEmpty();
    }

    [Fact]
    public void Compare_PunktVerschobenUmMehrAls10cm_LiefertMovedMatch()
    {
        // 0.5m Verschiebung in Nord-Richtung (~4.5e-6 Grad-Lat)
        var old = new[] { Pt(1, 48.0, 9.0) };
        var nu = new[] { Pt(99, 48.0 + 0.0000045, 9.0) };
        var r = _svc.Compare(old, nu, matchRadiusMeters: 1.0, movedThresholdMeters: 0.10);
        r.Matches.Should().HaveCount(1);
        r.Matches[0].Change.Should().Be(DifferentialChange.Moved);
        r.Matches[0].DistanceMeters.Should().BeApproximately(0.5, 0.05);
    }

    [Fact]
    public void Compare_PunktAusserhalbMatchRadius_LiefertAddedUndRemoved()
    {
        // 5m Verschiebung, MatchRadius 1m → kein Match
        var old = new[] { Pt(1, 48.0, 9.0) };
        var nu = new[] { Pt(99, 48.0 + 0.000045, 9.0) };
        var r = _svc.Compare(old, nu, matchRadiusMeters: 1.0);
        r.Matches.Should().BeEmpty();
        r.Added.Should().HaveCount(1);
        r.Removed.Should().HaveCount(1);
    }

    [Fact]
    public void Compare_MehrereKandidaten_NaechsterMatchGewinnt()
    {
        // Alt: 1 Punkt bei (48.0, 9.0)
        // Neu: 2 Punkte — einer sehr nah, einer weiter weg
        // Erwartet: nur der nahe wird gepaart, der entfernte ist "Added".
        var old = new[] { Pt(1, 48.0, 9.0) };
        var nu = new[]
        {
            Pt(10, 48.0000027, 9.0),       // ~0.3 m
            Pt(11, 48.0 + 0.0000045, 9.0), // ~0.5 m
        };
        var r = _svc.Compare(old, nu, matchRadiusMeters: 1.0, movedThresholdMeters: 0.10);
        r.Matches.Should().HaveCount(1);
        r.Matches[0].New.Id.Should().Be(10);
        r.Added.Should().HaveCount(1);
        r.Added[0].Id.Should().Be(11);
        r.Removed.Should().BeEmpty();
    }

    [Fact]
    public void Compare_HoehenverschiebungZaehlt()
    {
        // Gleiche Lat/Lon, aber 20cm Hoehe-Differenz → Moved
        var old = new[] { Pt(1, 48.0, 9.0, alt: 100.0) };
        var nu = new[] { Pt(99, 48.0, 9.0, alt: 100.20) };
        var r = _svc.Compare(old, nu, matchRadiusMeters: 1.0, movedThresholdMeters: 0.10);
        r.Matches.Should().HaveCount(1);
        r.Matches[0].Change.Should().Be(DifferentialChange.Moved);
        r.Matches[0].DistanceMeters.Should().BeApproximately(0.20, 0.001);
    }
}
