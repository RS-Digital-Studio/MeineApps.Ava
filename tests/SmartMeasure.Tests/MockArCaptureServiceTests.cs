using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

/// <summary>Tests fuer den Desktop-Mock: stellt sicher dass beide Pfade von
/// ArTransferService getestet werden koennen (Heading-Fallback + Geospatial).</summary>
public class MockArCaptureServiceTests
{
    [Fact]
    public async Task Default_SetztGroundPlaneY()
    {
        var mock = new MockArCaptureService();
        var result = await mock.CaptureAsync();

        result.Should().NotBeNull();
        result!.GroundPlaneY.Should().NotBeNull();
        result.GroundPlaneY.Should().BeApproximately(-1.5f, 0.01f);
    }

    [Fact]
    public async Task Default_KeineGeospatialKoords()
    {
        var mock = new MockArCaptureService();
        var result = await mock.CaptureAsync();

        result!.GeospatialActive.Should().BeFalse();
        result.Points.Should().AllSatisfy(p =>
        {
            p.GeoLatitude.Should().BeNull();
            p.GeoLongitude.Should().BeNull();
        });
    }

    [Fact]
    public async Task SimulateGeospatial_SetztVpsKoordsAufAllePunkte()
    {
        var mock = new MockArCaptureService { SimulateGeospatial = true };
        var result = await mock.CaptureAsync();

        result!.GeospatialActive.Should().BeTrue();
        result.Points.Should().AllSatisfy(p =>
        {
            p.GeoLatitude.Should().NotBeNull();
            p.GeoLongitude.Should().NotBeNull();
            p.GeoAltitude.Should().NotBeNull();
            p.GeoHorizontalAccuracy.Should().NotBeNull();
        });
        result.Contours.Should().AllSatisfy(c =>
        {
            c.Points.Should().AllSatisfy(p =>
            {
                p.GeoLatitude.Should().NotBeNull();
            });
        });
    }

    [Fact]
    public async Task SimulateNoisyPoint_FuegtPunktMitNiedrigerConfidenceHinzu()
    {
        var mock = new MockArCaptureService { SimulateNoisyPoint = true };
        var result = await mock.CaptureAsync();

        result!.Points.Should().Contain(p => p.Confidence < 0.5f);
    }

    [Fact]
    public async Task Deterministic_GleichSeed_GleichErgebnis()
    {
        // Mock nutzt fixed Seed 42 → identische Punkte bei jedem Aufruf
        var a = await new MockArCaptureService().CaptureAsync();
        var b = await new MockArCaptureService().CaptureAsync();

        a!.Points.Count.Should().Be(b!.Points.Count);
        for (var i = 0; i < a.Points.Count; i++)
        {
            a.Points[i].X.Should().Be(b.Points[i].X);
            a.Points[i].Z.Should().Be(b.Points[i].Z);
        }
    }

    [Fact]
    public async Task Default_HatRechteckSchlauchUndBeet()
    {
        var mock = new MockArCaptureService();
        var result = await mock.CaptureAsync();

        result!.Contours.Should().HaveCount(2);
        result.Contours.Should().Contain(c => c.ContourType == ArContourType.Weg);
        result.Contours.Should().Contain(c => c.ContourType == ArContourType.Beet && c.IsClosed);
    }
}
