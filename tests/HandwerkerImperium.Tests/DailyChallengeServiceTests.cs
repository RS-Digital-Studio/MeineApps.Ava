using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für DailyChallengeService: Generierung, Fortschritt, Belohnungen,
/// Tages-Reset, Retry-Logik und AllCompletedBonus.
/// </summary>
public class DailyChallengeServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private static (DailyChallengeService service, IGameStateService mockState, GameState state) ErstelleService(
        int vipExtraChallenges = 0)
    {
        var mockState = Substitute.For<IGameStateService>();
        var mockLocalization = Substitute.For<ILocalizationService>();
        var mockVip = Substitute.For<IVipService>();
        var mockWorker = Substitute.For<IWorkerService>();
        var state = GameState.CreateNew();
        mockState.State.Returns(state);
        mockVip.ExtraDailyChallenges.Returns(vipExtraChallenges);

        // Lokalisierungsservice gibt Schlüssel als Text zurück (für Tests ausreichend)
        mockLocalization.GetString(Arg.Any<string>()).Returns(x => x.Arg<string>());

        var service = new DailyChallengeService(mockState, mockLocalization, mockVip, mockWorker);
        return (service, mockState, state);
    }

    /// <summary>Erzwingt eine Generierung indem LastResetDate in die Vergangenheit gesetzt wird.</summary>
    private static void TriggerTagesReset(GameState state, DailyChallengeService service)
    {
        state.DailyChallengeState.LastResetDate = DateTime.UtcNow.AddDays(-2);
        service.CheckAndResetIfNewDay();
    }

    // ═══════════════════════════════════════════════════════════════════
    // CheckAndResetIfNewDay - Tages-Reset
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CheckAndResetIfNewDay_NeuerTag_GenerierDreiChallenges()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.DailyChallengeState.LastResetDate = DateTime.UtcNow.AddDays(-2);

        // Ausführung
        service.CheckAndResetIfNewDay();

        // Prüfung: 3 Challenges für Tier 0 (Level 1)
        state.DailyChallengeState.Challenges.Should().HaveCount(3);
    }

    [Fact]
    public void CheckAndResetIfNewDay_HeutigerTag_GeneriertKeineChallenges()
    {
        // Vorbereitung: Heute bereits zurückgesetzt
        var (service, _, state) = ErstelleService();
        state.DailyChallengeState.LastResetDate = DateTime.UtcNow;
        state.DailyChallengeState.Challenges.Add(new DailyChallenge { Type = DailyChallengeType.CompleteOrders });

        // Ausführung
        service.CheckAndResetIfNewDay();

        // Prüfung: Challenges unverändert (kein Reset)
        state.DailyChallengeState.Challenges.Should().HaveCount(1);
    }

    [Fact]
    public void CheckAndResetIfNewDay_DatumInZukunft_KeineGenerierung()
    {
        // Vorbereitung: Zeitmanipulations-Schutz
        var (service, _, state) = ErstelleService();
        state.DailyChallengeState.LastResetDate = DateTime.UtcNow.AddDays(2);

        // Ausführung
        service.CheckAndResetIfNewDay();

        // Prüfung: Kein Reset bei Datum in Zukunft
        state.DailyChallengeState.Challenges.Should().BeEmpty();
    }

    [Fact]
    public void CheckAndResetIfNewDay_NeuerTag_SetztAllCompletedBonusClaimed_Zurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.DailyChallengeState.AllCompletedBonusClaimed = true;
        state.DailyChallengeState.LastResetDate = DateTime.UtcNow.AddDays(-2);

        // Ausführung
        service.CheckAndResetIfNewDay();

        // Prüfung: Bonus-Flag zurückgesetzt
        state.DailyChallengeState.AllCompletedBonusClaimed.Should().BeFalse();
    }

    [Fact]
    public void CheckAndResetIfNewDay_VipSilver_GeneriertVierChallenges()
    {
        // Vorbereitung: VIP Silver = +1 Challenge
        var (service, _, state) = ErstelleService(vipExtraChallenges: 1);
        state.DailyChallengeState.LastResetDate = DateTime.UtcNow.AddDays(-2);

        // Ausführung
        service.CheckAndResetIfNewDay();

        // Prüfung: 4 Challenges (3 + 1 VIP)
        state.DailyChallengeState.Challenges.Should().HaveCount(4);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetState
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetState_NachGenerierung_GibtChallengesMitDisplayFieldsZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        TriggerTagesReset(state, service);

        // Ausführung
        var challengeState = service.GetState();

        // Prüfung: DisplayDescription wurde befüllt
        challengeState.Challenges.Should().AllSatisfy(c =>
            c.DisplayDescription.Should().NotBeNullOrEmpty());
    }

    // ═══════════════════════════════════════════════════════════════════
    // AreAllCompleted
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AreAllCompleted_KeineChallenges_IstFalse()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Prüfung
        service.AreAllCompleted.Should().BeFalse();
    }

    [Fact]
    public void AreAllCompleted_AlleAbgeschlossen_IstTrue()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.DailyChallengeState.Challenges.AddRange([
            new DailyChallenge { IsCompleted = true },
            new DailyChallenge { IsCompleted = true },
            new DailyChallenge { IsCompleted = true }
        ]);

        // Prüfung
        service.AreAllCompleted.Should().BeTrue();
    }

    [Fact]
    public void AreAllCompleted_EineNichtAbgeschlossen_IstFalse()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.DailyChallengeState.Challenges.AddRange([
            new DailyChallenge { IsCompleted = true },
            new DailyChallenge { IsCompleted = false },
            new DailyChallenge { IsCompleted = true }
        ]);

        // Prüfung
        service.AreAllCompleted.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // HasUnclaimedRewards
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void HasUnclaimedRewards_AbgeschlossenAbsNichtGeansprucht_IstTrue()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.DailyChallengeState.Challenges.Add(new DailyChallenge
        {
            IsCompleted = true,
            IsClaimed = false
        });

        // Prüfung
        service.HasUnclaimedRewards.Should().BeTrue();
    }

    [Fact]
    public void HasUnclaimedRewards_AlleGeansprucht_IstFalse()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.DailyChallengeState.Challenges.Add(new DailyChallenge
        {
            IsCompleted = true,
            IsClaimed = true
        });
        // HasUnclaimedRewards gibt auch true zurück wenn AreAllCompleted && !AllCompletedBonusClaimed.
        // Alle Challenges sind abgeschlossen (IsCompleted=true) → AreAllCompleted=true.
        // Damit HasUnclaimedRewards=false gilt, muss AllCompletedBonusClaimed=true sein.
        state.DailyChallengeState.AllCompletedBonusClaimed = true;

        // Prüfung
        service.HasUnclaimedRewards.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ClaimReward
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ClaimReward_AbgeschlosseneChallenge_GibtTrueZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Id = "test_id",
            IsCompleted = true,
            MoneyReward = 500m,
            XpReward = 100
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        var ergebnis = service.ClaimReward("test_id");

        // Prüfung
        ergebnis.Should().BeTrue();
    }

    [Fact]
    public void ClaimReward_AbgeschlosseneChallenge_SetztIsClaimed()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Id = "test_id",
            IsCompleted = true,
            MoneyReward = 500m,
            XpReward = 100
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        service.ClaimReward("test_id");

        // Prüfung
        challenge.IsClaimed.Should().BeTrue();
    }

    [Fact]
    public void ClaimReward_NichtAbgeschlosseneChallenge_GibtFalseZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Id = "test_id",
            IsCompleted = false
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        var ergebnis = service.ClaimReward("test_id");

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void ClaimReward_BereitsGeansprucht_GibtFalseZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Id = "test_id",
            IsCompleted = true,
            IsClaimed = true
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        var ergebnis = service.ClaimReward("test_id");

        // Prüfung: Keine doppelte Auszahlung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void ClaimReward_MitGoldschrauben_RuftAddGoldenScrewsAuf()
    {
        // Vorbereitung
        var (service, mockState, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Id = "test_id",
            IsCompleted = true,
            GoldenScrewReward = 2
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        service.ClaimReward("test_id");

        // Prüfung
        mockState.Received().AddGoldenScrews(2);
    }

    [Fact]
    public void ClaimReward_OhneGoldschrauben_RuftAddGoldenScrewsNichtAuf()
    {
        // Vorbereitung
        var (service, mockState, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Id = "test_id",
            IsCompleted = true,
            GoldenScrewReward = 0
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        service.ClaimReward("test_id");

        // Prüfung: Kein AddGoldenScrews bei 0 Schrauben
        mockState.DidNotReceive().AddGoldenScrews(Arg.Any<int>());
    }

    // ═══════════════════════════════════════════════════════════════════
    // RetryChallenge
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RetryChallenge_GueltigeChallenge_SetztFortschrittZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Id = "test_id",
            IsCompleted = false,
            CurrentValue = 3,
            TargetValue = 5
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        var ergebnis = service.RetryChallenge("test_id");

        // Prüfung
        ergebnis.Should().BeTrue();
        challenge.CurrentValue.Should().Be(0);
        challenge.HasRetriedWithAd.Should().BeTrue();
    }

    [Fact]
    public void RetryChallenge_BereitsAbgeschlossen_GibtFalseZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Id = "test_id",
            IsCompleted = true,
            CurrentValue = 5
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        var ergebnis = service.RetryChallenge("test_id");

        // Prüfung: Abgeschlossene Challenges können nicht zurückgesetzt werden
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void RetryChallenge_FortschrittIstNull_GibtFalseZurueck()
    {
        // Vorbereitung: Kein Fortschritt → Retry sinnlos
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Id = "test_id",
            IsCompleted = false,
            CurrentValue = 0
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        var ergebnis = service.RetryChallenge("test_id");

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void RetryChallenge_BereitsRetried_GibtFalseZurueck()
    {
        // Vorbereitung: Bereits einmal Retry genutzt
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Id = "test_id",
            IsCompleted = false,
            CurrentValue = 3,
            HasRetriedWithAd = true
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        var ergebnis = service.RetryChallenge("test_id");

        // Prüfung: Max 1 Retry pro Challenge
        ergebnis.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ClaimAllCompletedBonus
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ClaimAllCompletedBonus_AlleAbgeschlossen_GibtTrueZurueck()
    {
        // Vorbereitung: Alle 3 Challenges abgeschlossen
        var (service, _, state) = ErstelleService();
        state.DailyChallengeState.Challenges.AddRange([
            new DailyChallenge { IsCompleted = true },
            new DailyChallenge { IsCompleted = true },
            new DailyChallenge { IsCompleted = true }
        ]);

        // Ausführung
        var ergebnis = service.ClaimAllCompletedBonus();

        // Prüfung
        ergebnis.Should().BeTrue();
    }

    [Fact]
    public void ClaimAllCompletedBonus_NichtAlleAbgeschlossen_GibtFalseZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.DailyChallengeState.Challenges.AddRange([
            new DailyChallenge { IsCompleted = true },
            new DailyChallenge { IsCompleted = false }
        ]);

        // Ausführung
        var ergebnis = service.ClaimAllCompletedBonus();

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void ClaimAllCompletedBonus_BereitsGeansprucht_GibtFalseZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.DailyChallengeState.AllCompletedBonusClaimed = true;
        state.DailyChallengeState.Challenges.AddRange([
            new DailyChallenge { IsCompleted = true },
            new DailyChallenge { IsCompleted = true }
        ]);

        // Ausführung
        var ergebnis = service.ClaimAllCompletedBonus();

        // Prüfung: Kein doppelter Bonus
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void ClaimAllCompletedBonus_AlleAbgeschlossen_GibtGoldschraubenBonus()
    {
        // Vorbereitung: Tier 0 (Level 1) → 6 Goldschrauben Bonus
        var (service, mockState, state) = ErstelleService();
        state.PlayerLevel = 1;
        state.DailyChallengeState.Challenges.AddRange([
            new DailyChallenge { IsCompleted = true, IsClaimed = true },
            new DailyChallenge { IsCompleted = true, IsClaimed = true },
            new DailyChallenge { IsCompleted = true, IsClaimed = true }
        ]);

        // Ausführung
        service.ClaimAllCompletedBonus();

        // Prüfung: Goldschrauben-Bonus (AllCompletedBonusScrews für Tier 0 = 6)
        mockState.Received().AddGoldenScrews(6);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Fortschritts-Tracking via öffentliche Methoden
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void OnQuickJobCompleted_ErhoehtQuickJobFortschritt()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Type = DailyChallengeType.CompleteQuickJob,
            TargetValue = 3,
            CurrentValue = 0
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        service.OnQuickJobCompleted();

        // Prüfung
        challenge.CurrentValue.Should().Be(1);
    }

    [Fact]
    public void OnMiniGamePlayed_AbgeschlosseneChallenge_WirdNichtErhoeht()
    {
        // Vorbereitung: Challenge bereits abgeschlossen
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Type = DailyChallengeType.PlayMiniGames,
            TargetValue = 3,
            CurrentValue = 3,
            IsCompleted = true
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        service.OnMiniGamePlayed(75);

        // Prüfung: Abgeschlossene Challenge wird nicht erneut erhöht
        challenge.CurrentValue.Should().Be(3);
    }

    [Fact]
    public void OnMiniGamePlayed_MitScore_SetzhtScoreChallenge()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Type = DailyChallengeType.AchieveMinigameScore,
            TargetValue = 90,
            CurrentValue = 0
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        service.OnMiniGamePlayed(100);

        // Prüfung: Score-Challenge auf Maximum gesetzt
        challenge.CurrentValue.Should().Be(100);
    }

    [Fact]
    public void OnWorkerTrained_ErhoehtTrainingFortschritt()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Type = DailyChallengeType.TrainWorker,
            TargetValue = 2,
            CurrentValue = 0
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        service.OnWorkerTrained();

        // Prüfung
        challenge.CurrentValue.Should().Be(1);
    }

    [Fact]
    public void OnCraftingCompleted_ErhoehtCraftingFortschritt()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Type = DailyChallengeType.CompleteCrafting,
            TargetValue = 1,
            CurrentValue = 0
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        service.OnCraftingCompleted();

        // Prüfung: Challenge abgeschlossen
        challenge.CurrentValue.Should().Be(1);
        challenge.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void OnItemsAutoProduced_ErhoehtProduktionFortschritt()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Type = DailyChallengeType.ProduceItems,
            TargetValue = 20,
            CurrentValue = 0
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung: 10 Items produziert
        service.OnItemsAutoProduced(10);

        // Prüfung
        challenge.CurrentValue.Should().Be(10);
    }

    [Fact]
    public void OnItemsSold_ErhoehtVerkaufFortschritt()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Type = DailyChallengeType.SellItems,
            TargetValue = 10,
            CurrentValue = 5
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        service.OnItemsSold(3);

        // Prüfung
        challenge.CurrentValue.Should().Be(8);
    }

    [Fact]
    public void OnMaterialOrderCompleted_ErhoehtLieferauftragFortschritt()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var challenge = new DailyChallenge
        {
            Type = DailyChallengeType.CompleteMaterialOrder,
            TargetValue = 1,
            CurrentValue = 0
        };
        state.DailyChallengeState.Challenges.Add(challenge);

        // Ausführung
        service.OnMaterialOrderCompleted();

        // Prüfung
        challenge.CurrentValue.Should().Be(1);
        challenge.IsCompleted.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // AllCompletedBonusScrews - Tier-Staffelung
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1, 6)]    // Tier 0 → 6 GS
    [InlineData(101, 8)]  // Tier 5 (Level 101-300) → 8 GS
    [InlineData(301, 10)] // Tier 6 (Level 301-500) → 10 GS
    public void AllCompletedBonusScrews_NachTier_GibtKorrekteAnzahl(int level, int erwartet)
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = level;

        // Prüfung
        service.AllCompletedBonusScrews.Should().Be(erwartet);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Dispose
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Dispose_KannZweimalAufgerufenWerdenOhneException()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Ausführung + Prüfung
        var action = () =>
        {
            service.Dispose();
            service.Dispose();
        };
        action.Should().NotThrow();
    }
}
