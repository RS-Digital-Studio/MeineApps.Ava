using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

public class TotalStationServiceTests
{
    private readonly TotalStationService _svc = new(new CoordinateService());

    [Fact]
    public void Station_OhneOrigin_LiefertNull()
    {
        ((ITotalStationService)_svc).Station.Should().BeNull();
    }

    [Fact]
    public void ProjectTarget_OhneStation_WirftException()
    {
        var act = () => _svc.ProjectTarget(10, 0, 0);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ProjectTarget_StationiertNorden_LiefertPositionNoerdlich()
    {
        // Station bei (48.0, 9.0, 100m NN), Heading=0 (Nord), 10m nach vorne (Nord)
        _svc.SetStationOrigin(48.0, 9.0, 100.0, 0);
        var (lat, lon, alt) = _svc.ProjectTarget(10.0, 0, 0);

        // Erwartet: Lat erhoeht sich um ~0.00009° (10m Norden auf 48°)
        lat.Should().BeApproximately(48.0 + 0.0000898, 0.00001);
        lon.Should().BeApproximately(9.0, 0.00005);
        alt.Should().BeApproximately(100.0, 0.01);
    }

    [Fact]
    public void ProjectTarget_PitchNachOben_LiefertHoehereAltitude()
    {
        // 45° nach oben, 10m → ~7.07m horizontal + ~7.07m vertikal
        _svc.SetStationOrigin(48.0, 9.0, 100.0, 0);
        var (_, _, alt) = _svc.ProjectTarget(10.0, 0, 45.0);
        alt.Should().BeApproximately(100.0 + 7.07, 0.05);
    }

    [Fact]
    public void ProjectTarget_BearingOst_VerschiebtNurLongitude()
    {
        _svc.SetStationOrigin(48.0, 9.0, 100.0, 0);
        var (lat, lon, _) = _svc.ProjectTarget(10.0, 90.0, 0);
        lat.Should().BeApproximately(48.0, 0.00005);
        lon.Should().BeApproximately(9.0 + 0.0001343, 0.00002); // ~10m Ost auf 48°
    }
}
