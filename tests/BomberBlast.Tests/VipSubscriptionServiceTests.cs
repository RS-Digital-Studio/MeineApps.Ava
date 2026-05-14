using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>Tests für VipSubscriptionService (Phase 23b — M2).</summary>
public class VipSubscriptionServiceTests
{
    [Fact]
    public void NeueInstanz_IstNichtAktiv()
    {
        var prefs = new InMemoryPreferences();
        var svc = new VipSubscriptionService(prefs);
        svc.IsActive.Should().BeFalse();
        svc.ExpiresAtUtc.Should().BeNull();
        svc.CoinMultiplier.Should().Be(1.0f);
        svc.ContinueCooldownSeconds.Should().Be(60);
    }

    [Fact]
    public void Activate_AktiviertSubscription()
    {
        var prefs = new InMemoryPreferences();
        var svc = new VipSubscriptionService(prefs);
        var expires = DateTime.UtcNow.AddDays(30);
        svc.Activate(expires);

        svc.IsActive.Should().BeTrue();
        svc.ExpiresAtUtc.Should().NotBeNull();
        svc.ExpiresAtUtc!.Value.Should().BeCloseTo(expires, TimeSpan.FromSeconds(1));
        svc.CoinMultiplier.Should().Be(1.25f);
        svc.ContinueCooldownSeconds.Should().Be(30);
    }

    [Fact]
    public void IsActive_FalseWennAbgelaufen()
    {
        var prefs = new InMemoryPreferences();
        var svc = new VipSubscriptionService(prefs);
        svc.Activate(DateTime.UtcNow.AddSeconds(-10));

        svc.IsActive.Should().BeFalse();
        svc.CoinMultiplier.Should().Be(1.0f);
    }

    [Fact]
    public void Deactivate_BeendetSubscription()
    {
        var prefs = new InMemoryPreferences();
        var svc = new VipSubscriptionService(prefs);
        svc.Activate(DateTime.UtcNow.AddDays(30));

        svc.Deactivate();

        svc.IsActive.Should().BeFalse();
    }

    [Fact]
    public void CanClaimDailyGems_TrueWennAktivUndNochNichtClaimed()
    {
        var prefs = new InMemoryPreferences();
        var svc = new VipSubscriptionService(prefs);
        svc.Activate(DateTime.UtcNow.AddDays(30));

        svc.CanClaimDailyGems.Should().BeTrue();
    }

    [Fact]
    public void CanClaimDailyGems_FalseNachClaim()
    {
        var prefs = new InMemoryPreferences();
        var svc = new VipSubscriptionService(prefs);
        svc.Activate(DateTime.UtcNow.AddDays(30));

        svc.MarkDailyGemsClaimed();

        svc.CanClaimDailyGems.Should().BeFalse();
    }

    [Fact]
    public void CanClaimDailyGems_FalseWennNichtAktiv()
    {
        var prefs = new InMemoryPreferences();
        var svc = new VipSubscriptionService(prefs);
        // Nicht aktiviert
        svc.CanClaimDailyGems.Should().BeFalse();
    }

    [Fact]
    public void Activate_Persistiert()
    {
        var prefs = new InMemoryPreferences();
        var expires = DateTime.UtcNow.AddDays(30);
        var svc1 = new VipSubscriptionService(prefs);
        svc1.Activate(expires);

        var svc2 = new VipSubscriptionService(prefs);
        svc2.IsActive.Should().BeTrue();
    }

    [Fact]
    public void DailyGems_Konstant100()
    {
        var prefs = new InMemoryPreferences();
        var svc = new VipSubscriptionService(prefs);
        svc.DailyGems.Should().Be(100);
    }
}
