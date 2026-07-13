using FluentAssertions;
using SunSeeker.Shared.Models;
using SunSeeker.Shared.Services;
using Xunit;

namespace SunSeeker.Tests;

/// <summary>
/// Tests fuer Soll-Ausrichtung (je Ziel/Panel) und die Live-Einfallswinkel-Bewertung
/// (cosine-loss). Die AOI-Tests pruefen exakte mathematische Eigenschaften.
/// </summary>
public class AlignmentServiceTests
{
    private readonly SolarPositionService _solar = new();
    private readonly AlignmentService _align;
    private static readonly GeoLocation Berlin = new(52.5200, 13.4050, 38);
    private static readonly DateTime Noon = new(2024, 6, 21, 11, 8, 0, DateTimeKind.Utc);

    public AlignmentServiceTests() => _align = new AlignmentService(_solar);

    [Fact]
    public void AnnualYield_ZeigtNachSueden()
    {
        var rec = _align.GetRecommendation(Berlin, Noon, AlignmentGoal.AnnualYield, PanelProfile.Generic);

        rec.TargetAzimuth.Should().Be(180);
        // 52.52 * 0.76 + 3.1 = 43.0
        rec.TargetTilt.Should().BeApproximately(43.0, 0.5);
    }

    [Fact]
    public void WinterYield_SteilerAlsAnnual()
    {
        var annual = _align.GetRecommendation(Berlin, Noon, AlignmentGoal.AnnualYield, PanelProfile.Generic);
        var winter = _align.GetRecommendation(Berlin, Noon, AlignmentGoal.WinterYield, PanelProfile.Generic);

        winter.TargetTilt.Should().BeGreaterThan(annual.TargetTilt);
        // 52.52 * 0.875 + 19.2 = 65.2
        winter.TargetTilt.Should().BeApproximately(65.2, 0.5);
    }

    [Fact]
    public void SeasonYield_FlacherAlsAnnual_ZeigtNachSueden()
    {
        var season = _align.GetRecommendation(Berlin, Noon, AlignmentGoal.SeasonYield, PanelProfile.Generic);
        var annual = _align.GetRecommendation(Berlin, Noon, AlignmentGoal.AnnualYield, PanelProfile.Generic);

        season.TargetAzimuth.Should().Be(180);
        season.TargetTilt.Should().BeLessThan(annual.TargetTilt);
        // 52.52 * 0.94 - 17.0 = 32.4
        season.TargetTilt.Should().BeApproximately(32.4, 0.5);
    }

    [Fact]
    public void NowMaximum_ZeigtAufDieAktuelleSonne()
    {
        var sun = _solar.GetPosition(Berlin, Noon);
        var rec = _align.GetRecommendation(Berlin, Noon, AlignmentGoal.NowMaximum, PanelProfile.Generic);

        rec.TargetAzimuth.Should().BeApproximately(sun.Azimuth, 0.01);
        rec.TargetTilt.Should().BeApproximately(sun.Zenith, 0.01);
    }

    [Fact]
    public void TodayYield_NeigungSenkrechtZurMittagssonne()
    {
        var times = _solar.GetSunTimes(Berlin, DateOnly.FromDateTime(Noon));
        var rec = _align.GetRecommendation(Berlin, Noon, AlignmentGoal.TodayYield, PanelProfile.Generic);

        rec.TargetAzimuth.Should().Be(180);
        rec.TargetTilt.Should().BeApproximately(90 - times.NoonElevation, 0.5);
    }

    [Fact]
    public void Kickstand_Ps400_SnaptAufNaechstenFestwinkel()
    {
        // AnnualYield ~43 Grad -> naechster PS400-Winkel ist 40.
        var rec = _align.GetRecommendation(Berlin, Noon, AlignmentGoal.AnnualYield, PanelProfile.Ps400);
        rec.RecommendedKickstandTilt.Should().Be(40);
    }

    [Fact]
    public void Kickstand_Ps400Bifacial_StufenlosUebernimmtWunschwinkel()
    {
        // PS400 Bifazial ist stufenlos verstellbar -> kein Snapping, Kickstand == Optimum.
        var rec = _align.GetRecommendation(Berlin, Noon, AlignmentGoal.WinterYield, PanelProfile.Ps400Bifacial);
        rec.RecommendedKickstandTilt.Should().Be(rec.TargetTilt);
    }

    [Fact]
    public void Kickstand_GenerischesPanel_UebernimmtWunschwinkel()
    {
        var rec = _align.GetRecommendation(Berlin, Noon, AlignmentGoal.AnnualYield, PanelProfile.Generic);
        rec.RecommendedKickstandTilt.Should().Be(rec.TargetTilt);
    }

    [Fact]
    public void Evaluate_PanelDirektAufSonne_VollerErtrag()
    {
        var sun = new SolarPosition(150, 40, Noon);
        var rec = new AlignmentRecommendation(AlignmentGoal.NowMaximum, 150, 50, 50);

        // Panel-Azimut = Sonnen-Azimut, Neigung = Zenitwinkel (90 - 40) -> Sonne senkrecht.
        var state = _align.Evaluate(sun, panelAzimuth: 150, panelTilt: 50, rec);

        state.AngleOfIncidence.Should().BeApproximately(0, 0.5);
        state.DirectGainFactor.Should().BeApproximately(1.0, 0.001);
        state.SunBehindPanel.Should().BeFalse();
        state.Quality.Should().Be(AlignmentQuality.Excellent);
    }

    [Fact]
    public void Evaluate_HorizontalesPanel_GainIstSinusDerElevation()
    {
        // Bei tilt = 0 gilt cos(AOI) = sin(Elevation), unabhaengig vom Azimut.
        var sun = new SolarPosition(123, 30, Noon);
        var rec = new AlignmentRecommendation(AlignmentGoal.TodayYield, 180, 0, 0);

        var state = _align.Evaluate(sun, panelAzimuth: 0, panelTilt: 0, rec);

        state.DirectGainFactor.Should().BeApproximately(Math.Sin(30 * Math.PI / 180), 0.001);
    }

    [Fact]
    public void Evaluate_SonneHinterPanel_KeinDirektErtrag()
    {
        // Sonne im Sueden, Panel steil nach Norden gerichtet.
        var sun = new SolarPosition(180, 40, Noon);
        var rec = new AlignmentRecommendation(AlignmentGoal.NowMaximum, 0, 80, 80);

        var state = _align.Evaluate(sun, panelAzimuth: 0, panelTilt: 80, rec);

        state.SunBehindPanel.Should().BeTrue();
        state.DirectGainFactor.Should().Be(0);
    }

    [Fact]
    public void Evaluate_NachtsKeinErtrag()
    {
        var sun = new SolarPosition(0, -10, Noon); // Sonne unter Horizont
        var rec = new AlignmentRecommendation(AlignmentGoal.NowMaximum, 180, 35, 35);

        var state = _align.Evaluate(sun, panelAzimuth: 180, panelTilt: 35, rec);

        state.DirectGainFactor.Should().Be(0);
        state.SunBehindPanel.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_AzimutFehler_IstVorzeichenbehaftetUndNormalisiert()
    {
        var sun = new SolarPosition(180, 45, Noon);
        var rec = new AlignmentRecommendation(AlignmentGoal.AnnualYield, 180, 35, 35);

        // Panel 10 Grad oestlich von Sued.
        var state = _align.Evaluate(sun, panelAzimuth: 190, panelTilt: 35, rec);
        state.AzimuthError.Should().BeApproximately(10, 0.001);

        // Wrap-around: Panel bei 170 -> Fehler -10 (nicht +350).
        var state2 = _align.Evaluate(sun, panelAzimuth: 170, panelTilt: 35, rec);
        state2.AzimuthError.Should().BeApproximately(-10, 0.001);
    }
}
