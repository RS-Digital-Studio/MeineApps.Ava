using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für BattlePassService: XP-Vergabe, Tier-Aufstieg, Belohnungen,
/// Saison-Ablauf, SpeedBoost-Stacking und Dispose.
/// </summary>
public class BattlePassServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private static (BattlePassService service, IGameStateService mockState, GameState state) ErstelleService()
    {
        var mockState = Substitute.For<IGameStateService>();
        var mockPurchase = Substitute.For<IPurchaseService>();
        var mockWorker = Substitute.For<IWorkerService>();
        var mockCrafting = Substitute.For<ICraftingService>();
        var state = GameState.CreateNew();
        mockState.State.Returns(state);

        var service = new BattlePassService(mockState, mockPurchase, mockWorker, mockCrafting);
        return (service, mockState, state);
    }

    // ═══════════════════════════════════════════════════════════════════
    // AddXp - XP-Vergabe
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddXp_PositiverBetrag_ErhoehtCurrentXp()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var vorher = state.BattlePass.CurrentXp;

        // Ausführung
        service.AddXp(100, "test");

        // Prüfung
        state.BattlePass.CurrentXp.Should().BeGreaterThan(vorher);
    }

    [Fact]
    public void AddXp_NullBetrag_AendertNichts()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var vorher = state.BattlePass.CurrentXp;

        // Ausführung
        service.AddXp(0, "test");

        // Prüfung: 0 XP → keine Änderung
        state.BattlePass.CurrentXp.Should().Be(vorher);
    }

    [Fact]
    public void AddXp_NegativersBetrag_AendertNichts()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        var vorher = state.BattlePass.CurrentXp;

        // Ausführung
        service.AddXp(-50, "test");

        // Prüfung: Negative XP werden ignoriert
        state.BattlePass.CurrentXp.Should().Be(vorher);
    }

    [Fact]
    public void AddXp_SaisonAbgelaufen_WirdIgnoriert()
    {
        // Vorbereitung: Saison vor 50 Tagen gestartet (abgelaufen)
        var (service, _, state) = ErstelleService();
        state.BattlePass.SeasonStartDate = DateTime.UtcNow.AddDays(-50);
        var vorher = state.BattlePass.CurrentXp;

        // Ausführung
        service.AddXp(100, "test");

        // Prüfung: Abgelaufene Saison ignoriert XP
        state.BattlePass.CurrentXp.Should().Be(vorher);
    }

    [Fact]
    public void AddXp_GenugXpFuerTier_FuehrtTierUpDurch()
    {
        // Vorbereitung: Tier 0 braucht 250 XP (0+1 * 250)
        var (service, _, state) = ErstelleService();
        state.BattlePass.CurrentTier = 0;
        state.BattlePass.CurrentXp = 0;

        // Ausführung: 250 XP → Tier 1
        service.AddXp(250, "test");

        // Prüfung
        state.BattlePass.CurrentTier.Should().Be(1);
    }

    [Fact]
    public void AddXp_FeuertBattlePassUpdated()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();
        bool gefeuert = false;
        service.BattlePassUpdated += () => gefeuert = true;

        // Ausführung
        service.AddXp(50, "test");

        // Prüfung
        gefeuert.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ClaimReward - Free Track
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ClaimReward_FreeTier_TierNichtErreicht_WirdIgnoriert()
    {
        // Vorbereitung: Tier 5 beanspruchen, aber Spieler ist bei Tier 0
        var (service, mockState, state) = ErstelleService();
        state.BattlePass.CurrentTier = 0;

        // Ausführung
        service.ClaimReward(5, isPremium: false);

        // Prüfung: Keine Belohnung
        mockState.DidNotReceive().AddMoney(Arg.Any<decimal>());
    }

    [Fact]
    public void ClaimReward_FreeTier_TierErreicht_SchreibtBelohnungGut()
    {
        // Vorbereitung: Tier 0 ist immer erreichbar (CurrentTier >= 0)
        var (service, mockState, state) = ErstelleService();
        state.BattlePass.CurrentTier = 5;
        state.BattlePass.BaseIncomeAtSeasonStart = 100m;

        // Ausführung: Tier 0 beanspruchen (0-basiert)
        service.ClaimReward(0, isPremium: false);

        // Prüfung: Belohnung gutgeschrieben
        mockState.Received().AddMoney(Arg.Any<decimal>());
    }

    [Fact]
    public void ClaimReward_FreeTier_BereitsGeansprucht_WirdIgnoriert()
    {
        // Vorbereitung: Tier 0 bereits beansprucht
        var (service, mockState, state) = ErstelleService();
        state.BattlePass.CurrentTier = 5;
        state.BattlePass.ClaimedFreeTiers.Add(0);
        state.BattlePass.BaseIncomeAtSeasonStart = 100m;

        // Ausführung
        service.ClaimReward(0, isPremium: false);

        // Prüfung: Keine doppelte Belohnung
        mockState.DidNotReceive().AddMoney(Arg.Any<decimal>());
    }

    [Fact]
    public void ClaimReward_FreeTier_FuegtzuClaimedFreeTiersHinzu()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.BattlePass.CurrentTier = 5;
        state.BattlePass.BaseIncomeAtSeasonStart = 100m;

        // Ausführung
        service.ClaimReward(0, isPremium: false);

        // Prüfung
        state.BattlePass.ClaimedFreeTiers.Should().Contain(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ClaimReward - Premium Track
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ClaimReward_PremiumOhnePremiumStatus_WirdIgnoriert()
    {
        // Vorbereitung: Spieler ohne Premium
        var (service, mockState, state) = ErstelleService();
        state.BattlePass.IsPremium = false;
        state.BattlePass.CurrentTier = 5;

        // Ausführung
        service.ClaimReward(0, isPremium: true);

        // Prüfung: Kein Premium → keine Belohnung
        mockState.DidNotReceive().AddMoney(Arg.Any<decimal>());
    }

    [Fact]
    public void ClaimReward_PremiumMitPremiumStatus_SchreibtBelohnungGut()
    {
        // Vorbereitung: Spieler mit Premium
        var (service, mockState, state) = ErstelleService();
        state.BattlePass.IsPremium = true;
        state.BattlePass.CurrentTier = 5;
        state.BattlePass.BaseIncomeAtSeasonStart = 100m;

        // Ausführung
        service.ClaimReward(0, isPremium: true);

        // Prüfung
        mockState.Received().AddMoney(Arg.Any<decimal>());
    }

    // ═══════════════════════════════════════════════════════════════════
    // ClaimReward - SpeedBoost
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ClaimReward_SpeedBoostTier_SetztSpeedBoostEndTime()
    {
        // Vorbereitung: Tier 34 ist SpeedBoost (120 Minuten)
        var (service, _, state) = ErstelleService();
        state.BattlePass.IsPremium = true;
        state.BattlePass.CurrentTier = 50; // Alle Tiers erreichbar
        state.BattlePass.BaseIncomeAtSeasonStart = 100m;
        state.SpeedBoostEndTime = DateTime.MinValue;

        // Ausführung: Tier 34 beanspruchen (SpeedBoost)
        service.ClaimReward(34, isPremium: true);

        // Prüfung: SpeedBoostEndTime wurde gesetzt
        state.SpeedBoostEndTime.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void ClaimReward_SpeedBoostStackt_AddiertAufBestehenden()
    {
        // Vorbereitung: Laufender SpeedBoost endet in 1 Stunde
        var (service, _, state) = ErstelleService();
        state.BattlePass.IsPremium = true;
        state.BattlePass.CurrentTier = 50;
        state.BattlePass.BaseIncomeAtSeasonStart = 100m;
        var laufenderBoostEnde = DateTime.UtcNow.AddHours(1);
        state.SpeedBoostEndTime = laufenderBoostEnde;

        // Ausführung: Tier 34 gibt +120 Minuten
        service.ClaimReward(34, isPremium: true);

        // Prüfung: SpeedBoostEndTime > laufender Boost (addiert, nicht überschrieben)
        state.SpeedBoostEndTime.Should().BeAfter(laufenderBoostEnde);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CheckNewSeason
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CheckNewSeason_SaisonNochAktiv_AenderNichts()
    {
        // Vorbereitung: Saison seit 10 Tagen aktiv (42 Tage Dauer)
        var (service, _, state) = ErstelleService();
        state.BattlePass.SeasonStartDate = DateTime.UtcNow.AddDays(-10);
        state.BattlePass.CurrentTier = 5;
        state.BattlePass.CurrentXp = 100;
        var tierVorher = state.BattlePass.CurrentTier;

        // Ausführung
        service.CheckNewSeason();

        // Prüfung: Keine Änderung
        state.BattlePass.CurrentTier.Should().Be(tierVorher);
    }

    [Fact]
    public void CheckNewSeason_SaisonAbgelaufen_SetztTierZurueck()
    {
        // Vorbereitung: Saison vor 50 Tagen gestartet (überfällig)
        var (service, _, state) = ErstelleService();
        state.BattlePass.SeasonStartDate = DateTime.UtcNow.AddDays(-50);
        state.BattlePass.CurrentTier = 25;
        state.BattlePass.CurrentXp = 200;

        // Ausführung
        service.CheckNewSeason();

        // Prüfung: Neue Saison → Tier und XP zurückgesetzt
        state.BattlePass.CurrentTier.Should().Be(0);
        state.BattlePass.CurrentXp.Should().Be(0);
    }

    [Fact]
    public void CheckNewSeason_SaisonAbgelaufen_ErhoehrtSaisonNummer()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.BattlePass.SeasonStartDate = DateTime.UtcNow.AddDays(-50);
        state.BattlePass.SeasonNumber = 3;

        // Ausführung
        service.CheckNewSeason();

        // Prüfung
        state.BattlePass.SeasonNumber.Should().Be(4);
    }

    [Fact]
    public void CheckNewSeason_SaisonAbgelaufen_SetztPremiumZurueck()
    {
        // Vorbereitung: Premium war aktiv
        var (service, _, state) = ErstelleService();
        state.BattlePass.SeasonStartDate = DateTime.UtcNow.AddDays(-50);
        state.BattlePass.IsPremium = true;

        // Ausführung
        service.CheckNewSeason();

        // Prüfung: Premium muss pro Saison erneut gekauft werden
        state.BattlePass.IsPremium.Should().BeFalse();
    }

    [Fact]
    public void CheckNewSeason_SaisonAbgelaufen_LeertClaimedTiers()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.BattlePass.SeasonStartDate = DateTime.UtcNow.AddDays(-50);
        state.BattlePass.ClaimedFreeTiers.AddRange([0, 1, 2, 5]);
        state.BattlePass.ClaimedPremiumTiers.AddRange([0, 1]);

        // Ausführung
        service.CheckNewSeason();

        // Prüfung
        state.BattlePass.ClaimedFreeTiers.Should().BeEmpty();
        state.BattlePass.ClaimedPremiumTiers.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════
    // BattlePass.AddXp - Direkte Model-Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void BattlePassAddXp_MaxTierErreicht_XpBleibtNull()
    {
        // Vorbereitung
        var bp = new BattlePass { CurrentTier = BattlePass.MaxTier - 1, CurrentXp = 0 };

        // Ausführung: Genug XP für letzten Tier-Aufstieg (Tier 49→50 kostet 250*50*2=25000)
        bp.AddXp(30000);

        // Prüfung: Kein Overflow über MaxTier hinaus
        bp.CurrentTier.Should().Be(BattlePass.MaxTier);
        bp.CurrentXp.Should().Be(0);
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
