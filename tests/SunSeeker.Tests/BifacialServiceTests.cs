using FluentAssertions;
using SunSeeker.Shared.Models;
using SunSeeker.Shared.Services;
using Xunit;

namespace SunSeeker.Tests;

/// <summary>Tests fuer die Bifazial-Logik (Albedo, Mehrertrags-Bereich, Steilwinkel-Zuschlag, Tipps).</summary>
public class BifacialServiceTests
{
    private readonly BifacialService _svc = new();

    [Fact]
    public void NichtBifazialesPanel_KeinMehrertrag()
    {
        var advice = _svc.GetAdvice(GroundType.Snow, PanelProfile.Ps400);

        advice.EstimatedGainLow.Should().Be(0);
        advice.EstimatedGainHigh.Should().Be(0);
        advice.TiltBonusDegrees.Should().Be(0);
    }

    [Fact]
    public void BifazialAufSchnee_HoherMehrertragUndSteilerWinkel()
    {
        var advice = _svc.GetAdvice(GroundType.Snow, PanelProfile.Ps400Bifacial);

        advice.EstimatedGainHigh.Should().BeGreaterThan(advice.EstimatedGainLow);
        advice.EstimatedGainLow.Should().BeGreaterThan(0);
        advice.TiltBonusDegrees.Should().BeApproximately(11, 0.5); // hoechste Albedo -> max. Zuschlag
    }

    [Fact]
    public void BifazialAufGras_KeinNennenswerterSteilwinkelZuschlag()
    {
        // Gras-Albedo (0.20) ist die untere Schwelle -> Zuschlag ~0.
        var advice = _svc.GetAdvice(GroundType.Grass, PanelProfile.Ps400Bifacial);

        advice.TiltBonusDegrees.Should().BeApproximately(0, 0.01);
    }

    [Fact]
    public void MehrertragSteigtMitAlbedo()
    {
        var grass = _svc.GetAdvice(GroundType.Grass, PanelProfile.Ps400Bifacial);
        var concrete = _svc.GetAdvice(GroundType.Concrete, PanelProfile.Ps400Bifacial);
        var snow = _svc.GetAdvice(GroundType.Snow, PanelProfile.Ps400Bifacial);

        concrete.EstimatedGainHigh.Should().BeGreaterThan(grass.EstimatedGainHigh);
        snow.EstimatedGainHigh.Should().BeGreaterThan(concrete.EstimatedGainHigh);
    }

    [Fact]
    public void MehrertragIstGedeckelt()
    {
        var snow = _svc.GetAdvice(GroundType.Snow, PanelProfile.Ps400Bifacial);

        snow.EstimatedGainHigh.Should().BeLessThanOrEqualTo(0.30);
    }

    [Theory]
    [InlineData(GroundType.Asphalt, 0.12)]
    [InlineData(GroundType.Grass, 0.20)]
    [InlineData(GroundType.Concrete, 0.30)]
    [InlineData(GroundType.Snow, 0.85)]
    public void AlbedoWerte_StimmenMitLiteratur(GroundType ground, double expectedAlbedo)
    {
        var advice = _svc.GetAdvice(ground, PanelProfile.Ps400Bifacial);
        advice.Albedo.Should().BeApproximately(expectedAlbedo, 0.001);
    }

    [Fact]
    public void BifazialesPanel_LiefertTipps()
    {
        var advice = _svc.GetAdvice(GroundType.Grass, PanelProfile.Ps400Bifacial);

        advice.Tips.Should().NotBeEmpty();
    }

    [Fact]
    public void DunklerUntergrund_EmpfiehltHelleUnterlage()
    {
        var advice = _svc.GetAdvice(GroundType.Asphalt, PanelProfile.Ps400Bifacial);

        advice.Tips.Should().Contain(t => t.Contains("Plane") || t.Contains("Kies"));
    }
}
