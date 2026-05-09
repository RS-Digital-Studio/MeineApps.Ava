using BomberBlast.Services;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests;

/// <summary>Tests für BattlePassPlusService (Phase 23b — AAA-Audit M1).</summary>
public class BattlePassPlusServiceTests
{
    [Fact]
    public void NeueInstanz_HasPlus_False()
    {
        var prefs = new InMemoryPreferences();
        var svc = new BattlePassPlusService(prefs);
        svc.HasPlus.Should().BeFalse();
        svc.PlusSeasonNumber.Should().Be(0);
        svc.XpMultiplier.Should().Be(1.0f);
        svc.BonusGemsPerTier.Should().Be(0);
    }

    [Fact]
    public void Activate_AktiviertPlus()
    {
        var prefs = new InMemoryPreferences();
        var svc = new BattlePassPlusService(prefs);
        svc.ActivatePlus(5);

        svc.HasPlus.Should().BeTrue();
        svc.PlusSeasonNumber.Should().Be(5);
        svc.XpMultiplier.Should().Be(1.5f);
        svc.BonusGemsPerTier.Should().Be(10);
    }

    [Fact]
    public void Activate_Persistiert()
    {
        var prefs = new InMemoryPreferences();
        var svc1 = new BattlePassPlusService(prefs);
        svc1.ActivatePlus(3);

        var svc2 = new BattlePassPlusService(prefs);
        svc2.HasPlus.Should().BeTrue();
        svc2.PlusSeasonNumber.Should().Be(3);
    }

    [Fact]
    public void ResetForNewSeason_DeaktiviertBeiSpaeterSaison()
    {
        var prefs = new InMemoryPreferences();
        var svc = new BattlePassPlusService(prefs);
        svc.ActivatePlus(3);

        svc.ResetForNewSeason(4);

        svc.HasPlus.Should().BeFalse();
    }

    [Fact]
    public void ResetForNewSeason_BleibtAktivBeiGleicherSaison()
    {
        var prefs = new InMemoryPreferences();
        var svc = new BattlePassPlusService(prefs);
        svc.ActivatePlus(3);

        svc.ResetForNewSeason(3); // gleiche Saison

        svc.HasPlus.Should().BeTrue();
    }

    [Fact]
    public void TierSkipOnPurchase_Konstant25()
    {
        var prefs = new InMemoryPreferences();
        var svc = new BattlePassPlusService(prefs);
        svc.TierSkipOnPurchase.Should().Be(25);
    }
}
