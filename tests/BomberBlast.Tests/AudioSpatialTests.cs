using BomberBlast.Core.Audio;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für AudioSpatial (Phase 16). Validiert Pan-Berechnung, Distance-Falloff und
/// Equal-Power-Crossfade-Kurve. Pure-Funktionen ohne Mocks testbar.
/// </summary>
public class AudioSpatialTests
{
    [Theory]
    [InlineData(7, 7, 15, 0f)]      // Spieler auf gleicher Cell → mittig
    [InlineData(0, 7, 15, -1f)]     // Spieler ganz rechts, Sound ganz links → -1
    [InlineData(14, 7, 15, +1f)]    // Spieler ganz links, Sound ganz rechts → ~+1
    public void CalculatePan_LinearAusGridDistanz(int soundX, int playerX, int gridW, float expected)
    {
        var pan = AudioSpatial.CalculatePan(soundX, playerX, gridW);
        // Toleranz weil 14 statt 15 marginal vom Vollausschlag wegliegt
        pan.Should().BeApproximately(expected, 0.15f);
    }

    [Fact]
    public void CalculatePan_NullGridWidth_GibtNullZurueck()
    {
        AudioSpatial.CalculatePan(5, 10, 0).Should().Be(0f);
    }

    [Fact]
    public void CalculateDistanceVolume_InnerhalbFullVolumeRadius_GibtEinsZurueck()
    {
        var vol = AudioSpatial.CalculateDistanceVolume(5, 5, 5, 5, 3, 12);
        vol.Should().Be(1f);

        // 2 Cells weit (innerhalb FullRadius=3)
        var vol2 = AudioSpatial.CalculateDistanceVolume(7, 5, 5, 5, 3, 12);
        vol2.Should().Be(1f);
    }

    [Fact]
    public void CalculateDistanceVolume_AusserhalbSilenceRadius_GibtNullZurueck()
    {
        var vol = AudioSpatial.CalculateDistanceVolume(20, 0, 0, 0, 3, 12);
        vol.Should().Be(0f);
    }

    [Fact]
    public void CalculateDistanceVolume_LinearImInterpolationsbereich()
    {
        // Distanz 5, FullRadius 3, SilenceRadius 12 → range=9, dist im range=2
        // falloff = (12 - 5) / 9 = 7/9 ≈ 0.778
        var vol = AudioSpatial.CalculateDistanceVolume(5, 0, 0, 0, 3, 12);
        vol.Should().BeApproximately(0.778f, 0.01f);
    }

    [Fact]
    public void EqualPowerCrossfade_t0_AlteFullNeueZero()
    {
        var (oldVol, newVol) = AudioSpatial.EqualPowerCrossfade(0f);
        oldVol.Should().BeApproximately(1f, 0.001f);
        newVol.Should().BeApproximately(0f, 0.001f);
    }

    [Fact]
    public void EqualPowerCrossfade_t1_NeueFullAlteZero()
    {
        var (oldVol, newVol) = AudioSpatial.EqualPowerCrossfade(1f);
        oldVol.Should().BeApproximately(0f, 0.001f);
        newVol.Should().BeApproximately(1f, 0.001f);
    }

    [Fact]
    public void EqualPowerCrossfade_AtMidpoint_GleichesPower()
    {
        var (oldVol, newVol) = AudioSpatial.EqualPowerCrossfade(0.5f);
        // sin(45°) = cos(45°) ≈ 0.7071
        oldVol.Should().BeApproximately(0.7071f, 0.001f);
        newVol.Should().BeApproximately(0.7071f, 0.001f);
        // Equal-Power-Garantie: oldVol² + newVol² ≈ 1
        (oldVol * oldVol + newVol * newVol).Should().BeApproximately(1f, 0.001f);
    }

    [Fact]
    public void EqualPowerCrossfade_ClampedAussenAuf0Bis1()
    {
        var (oldA, newA) = AudioSpatial.EqualPowerCrossfade(-0.5f);
        oldA.Should().BeApproximately(1f, 0.001f);
        newA.Should().BeApproximately(0f, 0.001f);

        var (oldB, newB) = AudioSpatial.EqualPowerCrossfade(2f);
        oldB.Should().BeApproximately(0f, 0.001f);
        newB.Should().BeApproximately(1f, 0.001f);
    }

    [Theory]
    [InlineData(0, ReverbPreset.LargeRoom)]
    [InlineData(1, ReverbPreset.SmallRoom)]
    [InlineData(2, ReverbPreset.Cave)]
    [InlineData(3, ReverbPreset.Hall)]
    [InlineData(4, ReverbPreset.Outdoor)]
    [InlineData(5, ReverbPreset.None)]
    [InlineData(99, ReverbPreset.None)]
    public void WorldReverbMap_KorrekteZuordnung(int worldIndex, ReverbPreset expected)
    {
        WorldReverbMap.GetPresetForWorld(worldIndex).Should().Be(expected);
    }

    [Fact]
    public void WorldReverbMap_DungeonPreset_IstCave()
    {
        WorldReverbMap.DungeonPreset.Should().Be(ReverbPreset.Cave);
    }
}
