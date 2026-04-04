using HandwerkerImperium.Models;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für VipService: Tier-Berechnung, Boni, RecordPurchase, StateLoaded-Reaktion.
/// </summary>
public class VipServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private static (IGameStateService mock, GameState state, VipService sut) ErstelleService(decimal totalSpent = 0m)
    {
        var mock = Substitute.For<IGameStateService>();
        var state = new GameState { TotalPurchaseAmount = totalSpent };
        mock.State.Returns(state);
        var sut = new VipService(mock);
        return (mock, state, sut);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CurrentTier - Initialisierung
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CurrentTier_KeinAusgaben_IstNone()
    {
        // Vorbereitung
        var (_, _, sut) = ErstelleService(totalSpent: 0m);

        // Prüfung: Kein Kauf → kein VIP
        sut.CurrentTier.Should().Be(VipTier.None);
    }

    [Fact]
    public void CurrentTier_NachKleinstKauf_IstNone()
    {
        // Vorbereitung: Knapp unter Bronze-Schwelle (4,99 EUR)
        var (_, _, sut) = ErstelleService(totalSpent: 4.98m);

        // Prüfung
        sut.CurrentTier.Should().Be(VipTier.None);
    }

    // ═══════════════════════════════════════════════════════════════════
    // RecordPurchase
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RecordPurchase_BronzeSchwelle_AktualistertAufBronze()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService(totalSpent: 0m);

        // Ausführung: Kauf von 4,99 EUR → Bronze
        sut.RecordPurchase(4.99m);

        // Prüfung
        sut.CurrentTier.Should().Be(VipTier.Bronze);
    }

    [Fact]
    public void RecordPurchase_SilberSchwelle_AktualisertAufSilver()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService(totalSpent: 5.0m);

        // Ausführung: Weiterer Kauf über Silber-Schwelle (9,99 EUR gesamt)
        sut.RecordPurchase(4.99m);

        // Prüfung: Gesamt = 9,99 EUR → Silver
        sut.CurrentTier.Should().Be(VipTier.Silver);
    }

    [Fact]
    public void RecordPurchase_GoldSchwelle_AktualisertAufGold()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService(totalSpent: 15.0m);

        // Ausführung: Kauf → Gesamt über 19,99 EUR
        sut.RecordPurchase(5.0m);

        // Prüfung
        sut.CurrentTier.Should().Be(VipTier.Gold);
    }

    [Fact]
    public void RecordPurchase_PlatinumSchwelle_AktualisertAufPlatinum()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService(totalSpent: 45.0m);

        // Ausführung: Kauf → Gesamt über 49,99 EUR
        sut.RecordPurchase(5.0m);

        // Prüfung
        sut.CurrentTier.Should().Be(VipTier.Platinum);
    }

    [Fact]
    public void RecordPurchase_TierAendertSich_FeuertVipLevelChangedEvent()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService(totalSpent: 0m);
        bool eventFired = false;
        sut.VipLevelChanged += () => eventFired = true;

        // Ausführung: Bronze-Schwelle erreichen
        sut.RecordPurchase(4.99m);

        // Prüfung
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void RecordPurchase_TierAendertSichNicht_FeuertKeinEvent()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService(totalSpent: 0m);
        bool eventFired = false;
        sut.VipLevelChanged += () => eventFired = true;

        // Ausführung: Kleiner Betrag, noch kein Bronze
        sut.RecordPurchase(0.99m);

        // Prüfung: Tier bleibt None → kein Event
        eventFired.Should().BeFalse();
    }

    [Fact]
    public void RecordPurchase_ErhoehtTotalPurchaseAmount()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService(totalSpent: 10m);

        // Ausführung
        sut.RecordPurchase(5m);

        // Prüfung
        state.TotalPurchaseAmount.Should().Be(15m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Boni-Properties
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IncomeBonus_NoneVip_IstNull()
    {
        // Vorbereitung
        var (_, _, sut) = ErstelleService(totalSpent: 0m);

        // Prüfung
        sut.IncomeBonus.Should().Be(0m);
    }

    [Fact]
    public void IncomeBonus_PlatinumVip_IstFuenfProzent()
    {
        // Vorbereitung
        var (_, _, sut) = ErstelleService(totalSpent: 50m);
        sut.RefreshVipLevel();

        // Prüfung: Platinum = +5% laut VipTierExtensions
        sut.IncomeBonus.Should().Be(0.05m);
    }

    [Fact]
    public void CostReduction_AlleVipTiers_IstImmerNull()
    {
        // Prüfung: CostReduction wurde entfernt (kein Pay-to-Win, laut CLAUDE.md)
        foreach (VipTier tier in Enum.GetValues<VipTier>())
        {
            tier.GetCostReduction().Should().Be(0m, $"VIP-Stufe {tier} darf keine Kosten-Reduktion haben (kein Pay-to-Win)");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // ExtraDailyChallenges / ExtraWeeklyMissions
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ExtraDailyChallenges_SilverVipOderHoeher_IstEins()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService(totalSpent: 10m);
        sut.RefreshVipLevel();

        // Prüfung: Silver+ bekommt +1 Daily Challenge
        sut.ExtraDailyChallenges.Should().Be(1);
    }

    [Fact]
    public void ExtraDailyChallenges_BronzeVip_IstNull()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService(totalSpent: 5m);
        sut.RefreshVipLevel();

        // Prüfung: Bronze hat noch keine Extra-Challenges
        sut.ExtraDailyChallenges.Should().Be(0);
    }

    [Fact]
    public void ExtraWeeklyMissions_GoldVipOderHoeher_IstEins()
    {
        // Vorbereitung
        var (mock, state, sut) = ErstelleService(totalSpent: 20m);
        sut.RefreshVipLevel();

        // Prüfung
        sut.ExtraWeeklyMissions.Should().Be(1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // VipTierExtensions (direkte Model-Tests)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DetermineVipTier_NullEuro_GibtNone()
    {
        VipTierExtensions.DetermineVipTier(0m).Should().Be(VipTier.None);
    }

    [Fact]
    public void DetermineVipTier_4_99Euro_GibtBronze()
    {
        VipTierExtensions.DetermineVipTier(4.99m).Should().Be(VipTier.Bronze);
    }

    [Fact]
    public void DetermineVipTier_9_99Euro_GibtSilver()
    {
        VipTierExtensions.DetermineVipTier(9.99m).Should().Be(VipTier.Silver);
    }

    [Fact]
    public void DetermineVipTier_19_99Euro_GibtGold()
    {
        VipTierExtensions.DetermineVipTier(19.99m).Should().Be(VipTier.Gold);
    }

    [Fact]
    public void DetermineVipTier_49_99Euro_GibtPlatinum()
    {
        VipTierExtensions.DetermineVipTier(49.99m).Should().Be(VipTier.Platinum);
    }

    [Fact]
    public void DetermineVipTier_100Euro_GibtPlatinum()
    {
        VipTierExtensions.DetermineVipTier(100m).Should().Be(VipTier.Platinum);
    }
}
