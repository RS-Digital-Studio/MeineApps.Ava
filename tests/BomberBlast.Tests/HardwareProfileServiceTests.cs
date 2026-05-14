using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für HardwareProfileService (Phase 27 — P2/P3/P4).
/// Validiert User-Override, Particle-Cap-Skalierung pro Tier, Battery-Save + Thermal-Throttle-Override.
/// </summary>
public class HardwareProfileServiceTests
{
    [Fact]
    public void DetectedTier_LiefertGueltigenWert()
    {
        var prefs = new InMemoryPreferences();
        var svc = new HardwareProfileService(prefs);
        svc.DetectedTier.Should().BeOneOf(HardwareTier.Low, HardwareTier.Medium, HardwareTier.High, HardwareTier.Ultra);
    }

    [Fact]
    public void OhneOverride_CurrentTier_GleichDetected()
    {
        var prefs = new InMemoryPreferences();
        var svc = new HardwareProfileService(prefs);
        svc.HasUserOverride.Should().BeFalse();
        svc.CurrentTier.Should().Be(svc.DetectedTier);
    }

    [Fact]
    public void SetUserOverride_UeberschreibtDetectedTier()
    {
        var prefs = new InMemoryPreferences();
        var svc = new HardwareProfileService(prefs);

        svc.SetUserOverride(HardwareTier.Low);

        svc.HasUserOverride.Should().BeTrue();
        svc.CurrentTier.Should().Be(HardwareTier.Low);
    }

    [Fact]
    public void SetUserOverride_Null_AktiviertAutoDetectionWieder()
    {
        var prefs = new InMemoryPreferences();
        var svc = new HardwareProfileService(prefs);
        svc.SetUserOverride(HardwareTier.Low);
        svc.SetUserOverride(null);

        svc.HasUserOverride.Should().BeFalse();
        svc.CurrentTier.Should().Be(svc.DetectedTier);
    }

    [Fact]
    public void Override_Persistiert_UeberInstanzWechsel()
    {
        var prefs = new InMemoryPreferences();
        var svc1 = new HardwareProfileService(prefs);
        svc1.SetUserOverride(HardwareTier.Ultra);

        var svc2 = new HardwareProfileService(prefs);
        svc2.HasUserOverride.Should().BeTrue();
        svc2.CurrentTier.Should().Be(HardwareTier.Ultra);
    }

    [Theory]
    [InlineData(HardwareTier.Low, 300)]
    [InlineData(HardwareTier.Medium, 800)]
    [InlineData(HardwareTier.High, 1500)]
    [InlineData(HardwareTier.Ultra, 1500)]
    public void GetMaxParticles_SkaliertPerTier(HardwareTier tier, int expectedCap)
    {
        var prefs = new InMemoryPreferences();
        var svc = new HardwareProfileService(prefs);
        svc.SetUserOverride(tier);
        svc.GetMaxParticles().Should().Be(expectedCap);
    }

    [Fact]
    public void BatterySave_ReduziertEffectiveTier()
    {
        var prefs = new InMemoryPreferences();
        var svc = new HardwareProfileService(prefs);
        svc.SetUserOverride(HardwareTier.High);

        var capBefore = svc.GetMaxParticles();
        svc.BatterySaveActive = true;
        var capAfter = svc.GetMaxParticles();

        capAfter.Should().BeLessThan(capBefore, "Battery-Save senkt effektiven Tier um eine Stufe");
    }

    [Fact]
    public void ThermalThrottle_DeaktiviertBloom()
    {
        var prefs = new InMemoryPreferences();
        var svc = new HardwareProfileService(prefs);
        svc.SetUserOverride(HardwareTier.Ultra);
        svc.ShouldEnableBloom().Should().BeTrue();

        svc.ThermalThrottleActive = true;
        svc.ShouldEnableBloom().Should().BeFalse();
    }

    [Fact]
    public void QualityChanged_FiredBeiOverride()
    {
        var prefs = new InMemoryPreferences();
        var svc = new HardwareProfileService(prefs);
        var fireCount = 0;
        svc.QualityChanged += () => fireCount++;

        svc.SetUserOverride(HardwareTier.Low);
        svc.BatterySaveActive = true;
        svc.ThermalThrottleActive = true;

        fireCount.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void BatterySave_Persistiert()
    {
        var prefs = new InMemoryPreferences();
        var svc1 = new HardwareProfileService(prefs);
        svc1.BatterySaveActive = true;

        var svc2 = new HardwareProfileService(prefs);
        svc2.BatterySaveActive.Should().BeTrue();
    }

    [Fact]
    public void ThermalThrottle_NichtPersistiert()
    {
        // Thermal-State ist transient (vom OS getrieben) — darf nicht persistieren
        var prefs = new InMemoryPreferences();
        var svc1 = new HardwareProfileService(prefs);
        svc1.ThermalThrottleActive = true;

        var svc2 = new HardwareProfileService(prefs);
        svc2.ThermalThrottleActive.Should().BeFalse();
    }

    [Fact]
    public void ShouldEnableBloom_NurUltra()
    {
        var prefs = new InMemoryPreferences();
        var svc = new HardwareProfileService(prefs);

        foreach (var tier in new[] { HardwareTier.Low, HardwareTier.Medium, HardwareTier.High })
        {
            svc.SetUserOverride(tier);
            svc.ShouldEnableBloom().Should().BeFalse($"Bloom nicht für {tier}");
        }
        svc.SetUserOverride(HardwareTier.Ultra);
        svc.ShouldEnableBloom().Should().BeTrue("Bloom nur für Ultra");
    }
}
