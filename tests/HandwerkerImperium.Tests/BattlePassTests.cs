using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für BattlePass: AddXp(), XpForNextTier, TierProgress,
/// DaysRemaining, SeasonTheme, GenerateFreeRewards(), GeneratePremiumRewards().
/// </summary>
public class BattlePassTests
{
    // ═══════════════════════════════════════════════════════════════════
    // XpForNextTier
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void XpForNextTier_Tier0_IstZweihundertfünfzig()
    {
        // Vorbereitung
        var bp = new BattlePass { CurrentTier = 0 };

        // Prüfung: 250 * (0 + 1) = 250
        bp.XpForNextTier.Should().Be(250);
    }

    [Fact]
    public void XpForNextTier_Tier39_IstNormaleFormel()
    {
        // Vorbereitung: Tier 39 (0-basiert), noch unter Schwelle 40
        var bp = new BattlePass { CurrentTier = 39 };

        // Prüfung: 250 * 40 = 10.000
        bp.XpForNextTier.Should().Be(10_000);
    }

    [Fact]
    public void XpForNextTier_Tier40_IstDoppelt()
    {
        // Vorbereitung: Ab Tier 40 doppelte XP-Anforderung
        var bp = new BattlePass { CurrentTier = 40 };

        // Prüfung: 250 * 41 * 2 = 20.500
        bp.XpForNextTier.Should().Be(20_500);
    }

    // ═══════════════════════════════════════════════════════════════════
    // TierProgress
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TierProgress_KeineXP_IstNull()
    {
        // Vorbereitung
        var bp = new BattlePass { CurrentTier = 0, CurrentXp = 0 };

        // Prüfung
        bp.TierProgress.Should().Be(0.0);
    }

    [Fact]
    public void TierProgress_HälfteXP_IstPunktFünf()
    {
        // Vorbereitung: 125 von 250 XP
        var bp = new BattlePass { CurrentTier = 0, CurrentXp = 125 };

        // Prüfung
        bp.TierProgress.Should().BeApproximately(0.5, 0.01);
    }

    // ═══════════════════════════════════════════════════════════════════
    // DaysRemaining, IsSeasonExpired
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DaysRemaining_FrischeSaison_IstBisZuVierzigZwei()
    {
        // Vorbereitung
        var bp = new BattlePass { SeasonStartDate = DateTime.UtcNow };

        // Prüfung: Neue Saison hat maximal 42 Tage
        bp.DaysRemaining.Should().Be(42);
    }

    [Fact]
    public void DaysRemaining_AbgelaufeneSaison_IstNull()
    {
        // Vorbereitung
        var bp = new BattlePass { SeasonStartDate = DateTime.UtcNow.AddDays(-50) };

        // Prüfung: Keine negativen Tage
        bp.DaysRemaining.Should().Be(0);
    }

    [Fact]
    public void IsSeasonExpired_AlteSaison_IstTrue()
    {
        // Vorbereitung
        var bp = new BattlePass { SeasonStartDate = DateTime.UtcNow.AddDays(-43) };

        // Prüfung
        bp.IsSeasonExpired.Should().BeTrue();
    }

    [Fact]
    public void IsSeasonExpired_AktiveSaison_IstFalse()
    {
        // Vorbereitung
        var bp = new BattlePass { SeasonStartDate = DateTime.UtcNow };

        // Prüfung
        bp.IsSeasonExpired.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // SeasonTheme (zyklisch 0-3)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1, Season.Summer)]
    [InlineData(2, Season.Autumn)]
    [InlineData(3, Season.Winter)]
    [InlineData(4, Season.Spring)]  // 4 % 4 = 0 = Spring
    [InlineData(5, Season.Summer)]  // 5 % 4 = 1 = Summer
    public void SeasonTheme_SaisonNummer_ZyklisMapped(int saisonNr, Season erwarteteSaison)
    {
        // Vorbereitung
        var bp = new BattlePass { SeasonNumber = saisonNr };

        // Prüfung
        bp.SeasonTheme.Should().Be(erwarteteSaison);
    }

    // ═══════════════════════════════════════════════════════════════════
    // AddXp()
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddXp_ReichtNichtFürTierUp_GibtNullZurück()
    {
        // Vorbereitung
        var bp = new BattlePass { CurrentTier = 0, CurrentXp = 0 };

        // Ausführung: 100 XP, brauche 250 für Tier 1
        int tierUps = bp.AddXp(100);

        // Prüfung
        tierUps.Should().Be(0);
        bp.CurrentXp.Should().Be(100);
        bp.CurrentTier.Should().Be(0);
    }

    [Fact]
    public void AddXp_GenauEinTierUp_GibtEinsZurück()
    {
        // Vorbereitung
        var bp = new BattlePass { CurrentTier = 0, CurrentXp = 0 };

        // Ausführung: Genau 250 XP für Tier 0→1
        int tierUps = bp.AddXp(250);

        // Prüfung
        tierUps.Should().Be(1);
        bp.CurrentTier.Should().Be(1);
    }

    [Fact]
    public void AddXp_XPRestWirdÜbertragen()
    {
        // Vorbereitung
        var bp = new BattlePass { CurrentTier = 0, CurrentXp = 0 };

        // Ausführung: 350 XP für Tier 0→1, 100 XP Rest
        int tierUps = bp.AddXp(350);

        // Prüfung
        tierUps.Should().Be(1);
        bp.CurrentTier.Should().Be(1);
        bp.CurrentXp.Should().Be(100);
    }

    [Fact]
    public void AddXp_MehreresTierUps_AlleStufen()
    {
        // Vorbereitung
        var bp = new BattlePass { CurrentTier = 0, CurrentXp = 0 };

        // Ausführung: Genug für mehrere Tier-Ups (250+500+750=1500 für Tiers 0,1,2)
        int tierUps = bp.AddXp(1500);

        // Prüfung
        tierUps.Should().BeGreaterThanOrEqualTo(2);
        bp.CurrentTier.Should().BeGreaterThan(1);
    }

    [Fact]
    public void AddXp_MaxTierErreicht_XPNullGesetzt()
    {
        // Vorbereitung: Kurz vor MaxTier
        var bp = new BattlePass { CurrentTier = 49, CurrentXp = 0 };

        // Ausführung: Genug XP für letzten Tier (Tier 49→50 kostet 250*50*2=25000)
        bp.AddXp(30_000);

        // Prüfung: XP wird auf 0 gesetzt bei MaxTier
        bp.CurrentTier.Should().Be(50);
        bp.CurrentXp.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GenerateFreeRewards / GeneratePremiumRewards
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateFreeRewards_HatFünfzigTiers()
    {
        // Ausführung
        var rewards = BattlePass.GenerateFreeRewards(10m);

        // Prüfung
        rewards.Should().HaveCount(50);
    }

    [Fact]
    public void GeneratePremiumRewards_HatFünfzigTiers()
    {
        // Ausführung
        var rewards = BattlePass.GeneratePremiumRewards(10m);

        // Prüfung
        rewards.Should().HaveCount(50);
    }

    [Fact]
    public void GenerateFreeRewards_AlleMarkiertAlsFree()
    {
        // Ausführung
        var rewards = BattlePass.GenerateFreeRewards(10m);

        // Prüfung
        rewards.Should().AllSatisfy(r => r.IsFree.Should().BeTrue());
    }

    [Fact]
    public void GeneratePremiumRewards_AlleMarkiertAlsPremium()
    {
        // Ausführung
        var rewards = BattlePass.GeneratePremiumRewards(10m);

        // Prüfung
        rewards.Should().AllSatisfy(r => r.IsFree.Should().BeFalse());
    }

    [Fact]
    public void GenerateFreeRewards_Tier49_Capstone_HatFünfzigGoldschrauben()
    {
        // Ausführung
        var rewards = BattlePass.GenerateFreeRewards(10m);
        var capstone = rewards.FirstOrDefault(r => r.Tier == 49);

        // Prüfung: Laut Code: Tier 49 (MaxTier-1) hat 50 GS Capstone-Belohnung
        capstone.Should().NotBeNull();
        capstone!.GoldenScrewReward.Should().Be(50);
    }

    [Fact]
    public void GeneratePremiumRewards_Tier49_Capstone_HatHundertGoldschrauben()
    {
        // Ausführung
        var rewards = BattlePass.GeneratePremiumRewards(10m);
        var capstone = rewards.FirstOrDefault(r => r.Tier == 49);

        // Prüfung: Premium Capstone hat 100 GS
        capstone.Should().NotBeNull();
        capstone!.GoldenScrewReward.Should().Be(100);
    }

    [Fact]
    public void GeneratePremiumRewards_Tier34_HatSpeedBoost()
    {
        // Ausführung
        var rewards = BattlePass.GeneratePremiumRewards(10m);
        var tier34 = rewards.FirstOrDefault(r => r.Tier == 34);

        // Prüfung: Tier 35 (0-basiert: 34) hat SpeedBoost 2h
        tier34.Should().NotBeNull();
        tier34!.RewardType.Should().Be(BattlePassRewardType.SpeedBoost);
        tier34.SpeedBoostMinutes.Should().Be(120);
    }

    [Fact]
    public void GenerateFreeRewards_HöheresEinkommen_GibtHöhereBelohnungen()
    {
        // Ausführung
        var niedrig = BattlePass.GenerateFreeRewards(10m);
        var hoch = BattlePass.GenerateFreeRewards(1000m);

        // Prüfung: Höheres Einkommen = höhere Belohnungen (skaliert)
        hoch[0].MoneyReward.Should().BeGreaterThan(niedrig[0].MoneyReward);
    }
}
