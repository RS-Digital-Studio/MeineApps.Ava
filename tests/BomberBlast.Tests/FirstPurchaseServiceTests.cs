using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>
/// Tests für FirstPurchaseService (Phase 23 — M5).
/// Validiert First-Time-Purchase-Bonus-Logik (×2 Multiplier vor erstem Kauf, ×1 danach),
/// Persistenz und Cloud-Save-Konsistenz.
/// </summary>
public class FirstPurchaseServiceTests
{
    [Fact]
    public void NeueInstanz_BonusVerfuegbar()
    {
        var prefs = new InMemoryPreferences();
        var svc = new FirstPurchaseService(prefs);

        svc.HasClaimed.Should().BeFalse();
        svc.IsAvailable.Should().BeTrue();
        svc.GetBonusMultiplier().Should().Be(2.0f);
    }

    [Fact]
    public void NachClaim_KeinBonusMehr()
    {
        var prefs = new InMemoryPreferences();
        var svc = new FirstPurchaseService(prefs);

        svc.MarkAsClaimed();

        svc.HasClaimed.Should().BeTrue();
        svc.IsAvailable.Should().BeFalse();
        svc.GetBonusMultiplier().Should().Be(1.0f);
    }

    [Fact]
    public void Persistenz_UeberInstanzWechselErhalten()
    {
        var prefs = new InMemoryPreferences();
        var svc1 = new FirstPurchaseService(prefs);
        svc1.MarkAsClaimed();

        // Neue Instanz mit gleichen Prefs (App-Restart-Simulation)
        var svc2 = new FirstPurchaseService(prefs);
        svc2.HasClaimed.Should().BeTrue();
        svc2.GetBonusMultiplier().Should().Be(1.0f);
    }

    [Fact]
    public void MarkAsClaimed_Idempotent()
    {
        var prefs = new InMemoryPreferences();
        var svc = new FirstPurchaseService(prefs);

        svc.MarkAsClaimed();
        svc.MarkAsClaimed();
        svc.MarkAsClaimed();

        svc.HasClaimed.Should().BeTrue();
    }
}
