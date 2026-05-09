using FluentAssertions;
using HandwerkerImperium.Graphics;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// P0.3 AAA-Audit (08.05.2026): Tests für den 4-Phasen-Cinematic-Renderer.
/// Verifiziert Phasen-Übergänge, Skip-Logik, Auto-Dismiss und Daten-Snapshot.
/// </summary>
public class PrestigeCinematicRendererTests
{
    private static PrestigeCinematicData TestData() => new()
    {
        MoneyAtPrestige = 1_000_000m,
        Tier = PrestigeTier.Silver,
        BasePrestigePoints = 100,
        BonusPrestigePoints = 5,
        TierMultiplierRaw = 0.35,
        TierMultiplierEffective = 0.35,
        DiminishingReturnsFactor = 1.0,
        TierCount = 1,
        RunDurationSeconds = 3600,
        ActiveChallengeCount = 0,
        TierDisplayName = "Silver",
    };

    [Fact]
    public void Start_AktiviertCinematic()
    {
        var renderer = new PrestigeCinematicRenderer();

        renderer.IsActive.Should().BeFalse();
        renderer.Start(TestData());

        renderer.IsActive.Should().BeTrue();
        renderer.CurrentPhase.Should().Be(0); // Phase Money
    }

    [Fact]
    public void Update_DurchlaeuftAlleVierPhasen()
    {
        var renderer = new PrestigeCinematicRenderer();
        renderer.Start(TestData());

        renderer.CurrentPhase.Should().Be(0);

        // Phase 1: Money (0-3s)
        renderer.Update(2.0f);
        renderer.CurrentPhase.Should().Be(0);

        // Phase 2: Badge (3-6s)
        renderer.Update(2.0f); // Total 4s
        renderer.CurrentPhase.Should().Be(1);

        // Phase 3: Multiplier (6-11s)
        renderer.Update(4.0f); // Total 8s
        renderer.CurrentPhase.Should().Be(2);

        // Phase 4: Reward (11-14s)
        renderer.Update(4.0f); // Total 12s
        renderer.CurrentPhase.Should().Be(3);
        renderer.IsReadyForDismiss.Should().BeTrue();
    }

    [Fact]
    public void Skip_SpringtInRewardPhase()
    {
        var renderer = new PrestigeCinematicRenderer();
        renderer.Start(TestData());
        renderer.Update(3.5f); // Phase Badge

        renderer.IsSkipEnabled.Should().BeTrue("Skip-Button wird nach 2s sichtbar");
        renderer.Skip();
        renderer.Update(0.1f); // Tick

        renderer.CurrentPhase.Should().Be(3, "Skip springt direkt in die Reward-Phase");
        renderer.IsReadyForDismiss.Should().BeTrue();
    }

    [Fact]
    public void Skip_VorZweiSekunden_NichtErlaubt()
    {
        var renderer = new PrestigeCinematicRenderer();
        renderer.Start(TestData());
        renderer.Update(1.0f); // 1s elapsed

        renderer.IsSkipEnabled.Should().BeFalse("Skip ist erst nach 2s aktiv");
    }

    [Fact]
    public void Dismiss_DeaktiviertCinematic()
    {
        var renderer = new PrestigeCinematicRenderer();
        renderer.Start(TestData());

        renderer.Dismiss();

        renderer.IsActive.Should().BeFalse();
    }

    [Fact]
    public void AutoDismiss_NachAcht_Sekunden_Reward_Phase()
    {
        // P0.3-Bugfix: Cinematic schaltet sich auch ohne Tap selbst aus.
        var renderer = new PrestigeCinematicRenderer();
        renderer.Start(TestData());

        // Bis Phase 4 vorspulen (14s + 8s Auto-Dismiss-Window)
        renderer.Update(11.5f); // Phase 4 erreicht
        renderer.IsActive.Should().BeTrue();

        renderer.Update(3.0f); // PhaseRewardEnd erreicht (14s gesamt)
        renderer.IsActive.Should().BeTrue();

        renderer.Update(8.5f); // > PhaseRewardEnd + 8s Auto-Dismiss
        renderer.IsActive.Should().BeFalse("Auto-Dismiss greift 8s nach Phase 4");
    }

    [Theory]
    [InlineData(PrestigeTier.Bronze)]
    [InlineData(PrestigeTier.Silver)]
    [InlineData(PrestigeTier.Gold)]
    [InlineData(PrestigeTier.Platin)]
    [InlineData(PrestigeTier.Diamant)]
    [InlineData(PrestigeTier.Meister)]
    [InlineData(PrestigeTier.Legende)]
    public void Start_FuerAlleTiers_AktiviertOhneException(PrestigeTier tier)
    {
        var renderer = new PrestigeCinematicRenderer();
        var data = new PrestigeCinematicData
        {
            MoneyAtPrestige = 1_000_000m,
            Tier = tier,
            BasePrestigePoints = 100,
            BonusPrestigePoints = 5,
            TierMultiplierRaw = 0.35,
            TierMultiplierEffective = 0.35,
            DiminishingReturnsFactor = 1.0,
            TierCount = 1,
            RunDurationSeconds = 3600,
            ActiveChallengeCount = 0,
            TierDisplayName = tier.ToString(),
        };

        var act = () => renderer.Start(data);

        act.Should().NotThrow();
        renderer.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_OhneStart_NoOp()
    {
        var renderer = new PrestigeCinematicRenderer();
        // Update auf nicht-aktivem Renderer darf nicht crashen
        var act = () => renderer.Update(0.1f);
        act.Should().NotThrow();
        renderer.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Dispose_KannMehrfachAufgerufenWerden()
    {
        var renderer = new PrestigeCinematicRenderer();
        renderer.Start(TestData());

        renderer.Dispose();
        var act = () => renderer.Dispose();

        act.Should().NotThrow("Dispose ist idempotent");
    }
}
