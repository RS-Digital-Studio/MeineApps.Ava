using FluentAssertions;
using SunSeeker.Shared.Services;
using Xunit;

namespace SunSeeker.Tests;

/// <summary>
/// Verifiziert die AR-Projektion (Welt-Richtung → Bildschirm) für das Sonnenbahn-Overlay:
/// Zentrierung, FOV-Skalierung, Vorne/Hinten, FOV-Grenze und Roll-Korrektur.
/// </summary>
public class SunArProjectionTests
{
    private const float W = 1000f;
    private const float H = 1000f;

    [Fact]
    public void Project_ZielInBlickrichtung_LandetInDerBildmitte()
    {
        var p = SunArProjection.Project(180, 30, 180, 30, 0, 60, 45, W, H);

        p.X.Should().BeApproximately(500f, 0.5f);
        p.Y.Should().BeApproximately(500f, 0.5f);
        p.OnScreen.Should().BeTrue();
        p.InFront.Should().BeTrue();
    }

    [Fact]
    public void Project_15GradRechts_VerschiebtNachRechtsGemaessFov()
    {
        // 15° bei 60° hFOV → halbes FOV = 30° → nx = 0.5 → x = Mitte + 0.5*Halbbreite = 750.
        var p = SunArProjection.Project(195, 30, 180, 30, 0, 60, 45, W, H);

        p.X.Should().BeApproximately(750f, 0.5f);
        p.Y.Should().BeApproximately(500f, 0.5f);
        p.OnScreen.Should().BeTrue();
    }

    [Fact]
    public void Project_HoehereSonne_VerschiebtNachOben()
    {
        var p = SunArProjection.Project(180, 45, 180, 30, 0, 60, 60, W, H);

        p.Y.Should().BeLessThan(500f); // oben = kleineres Screen-Y
        p.OnScreen.Should().BeTrue();
    }

    [Fact]
    public void Project_SonneHinterKamera_IstNichtImBild()
    {
        var p = SunArProjection.Project(0, 30, 180, 30, 0, 60, 45, W, H);

        p.InFront.Should().BeFalse();
        p.OnScreen.Should().BeFalse();
    }

    [Fact]
    public void Project_AusserhalbDesFov_IstVorneAberNichtImBild()
    {
        // 40° bei 60° hFOV → über den Bildrand hinaus.
        var p = SunArProjection.Project(220, 30, 180, 30, 0, 60, 45, W, H);

        p.InFront.Should().BeTrue();
        p.OnScreen.Should().BeFalse();
        p.X.Should().BeGreaterThan(W);
    }

    [Fact]
    public void Project_Roll90Grad_DrehtHorizontalenVersatzNachVertikal()
    {
        // Sonne 15° rechts, Gerät um 90° gerollt → erscheint mittig-vertikal verschoben statt seitlich.
        var p = SunArProjection.Project(195, 30, 180, 30, 90, 60, 45, W, H);

        p.X.Should().BeApproximately(500f, 1f);
        p.Y.Should().NotBe(500f);
    }

    [Theory]
    [InlineData(350, -10)]
    [InlineData(-350, 10)]
    [InlineData(190, -170)]
    [InlineData(180, 180)]
    [InlineData(0, 0)]
    public void NormalizeDelta_KlemmtAufPlusMinus180(double input, double expected)
    {
        SunArProjection.NormalizeDelta(input).Should().BeApproximately(expected, 1e-9);
    }
}
