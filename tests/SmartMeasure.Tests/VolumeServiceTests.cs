using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

/// <summary>Plan-Kap. 5.4: Tests fuer Shoelace-Area + Material-Schaetzung. Der
/// GardenElement-basierte Pfad braucht einen IGardenPlanService-Stub; wir testen
/// die mathematische Kernlogik (internal ShoelaceArea).</summary>
public class VolumeServiceTests
{
    [Fact]
    public void ShoelaceArea_EinheitsQuadrat_Liefert1()
    {
        var pts = new[] { (0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0) };
        VolumeService.ShoelaceArea(pts).Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void ShoelaceArea_GleichseitigesDreieck_BerechnetKorrekt()
    {
        // Dreieck mit Basis 2 + Hoehe 3 → Flaeche 3
        var pts = new[] { (0.0, 0.0), (2.0, 0.0), (1.0, 3.0) };
        VolumeService.ShoelaceArea(pts).Should().BeApproximately(3.0, 0.0001);
    }

    [Fact]
    public void ShoelaceArea_Rechteck5x10_Liefert50()
    {
        var pts = new[] { (0.0, 0.0), (5.0, 0.0), (5.0, 10.0), (0.0, 10.0) };
        VolumeService.ShoelaceArea(pts).Should().BeApproximately(50.0, 0.0001);
    }

    [Fact]
    public void ShoelaceArea_UnterDreiPunkten_LiefertNull()
    {
        VolumeService.ShoelaceArea(new[] { (0.0, 0.0), (1.0, 0.0) }).Should().Be(0);
        VolumeService.ShoelaceArea([]).Should().Be(0);
    }

    [Fact]
    public void ShoelaceArea_UmgekehrteOrientierung_LiefertBetrag()
    {
        // CCW
        var ccw = new[] { (0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0) };
        // CW
        var cw = new[] { (0.0, 0.0), (0.0, 1.0), (1.0, 1.0), (1.0, 0.0) };
        VolumeService.ShoelaceArea(ccw).Should().BeApproximately(1.0, 0.0001);
        VolumeService.ShoelaceArea(cw).Should().BeApproximately(1.0, 0.0001);
    }
}
