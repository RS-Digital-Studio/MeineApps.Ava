using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für AchievementService: Freischalten, Belohnungen, BoostAchievement,
/// CheckAchievements, UnlockedCount und Dispose.
/// </summary>
public class AchievementServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private static (AchievementService service, IGameStateService mockState, GameState state) ErstelleService()
    {
        var mockState = Substitute.For<IGameStateService>();
        var mockPrestige = Substitute.For<IPrestigeService>();
        var mockAscension = Substitute.For<IAscensionService>();
        var mockRebirth = Substitute.For<IRebirthService>();
        var state = GameState.CreateNew();
        mockState.State.Returns(state);

        // Events stub (damit Subscribe nicht crasht)
        mockPrestige.PrestigeCompleted += Arg.Any<EventHandler?>();
        mockAscension.AscensionCompleted += Arg.Any<EventHandler?>();
        mockRebirth.RebirthCompleted += Arg.Any<EventHandler<WorkshopType>?>();

        var service = new AchievementService(mockState, mockPrestige, mockAscension, mockRebirth);
        return (service, mockState, state);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetAllAchievements
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAllAchievements_GibtNichtLeereListeZurueck()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Ausführung
        var achievements = service.GetAllAchievements();

        // Prüfung: Achievements vorhanden
        achievements.Should().NotBeEmpty();
    }

    [Fact]
    public void GetAllAchievements_UngesperrteAchievementsZuerst()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        // "first_order" Achievement freischalten
        state.UnlockedAchievements = ["first_order"];
        service.Reset();

        // Ausführung
        var achievements = service.GetAllAchievements();

        // Prüfung: Freigeschaltete Achievements oben
        achievements.First().IsUnlocked.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetAchievement
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAchievement_BekannterId_GibtAchievementZurueck()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Ausführung
        var achievement = service.GetAchievement("first_order");

        // Prüfung
        achievement.Should().NotBeNull();
        achievement!.Id.Should().Be("first_order");
    }

    [Fact]
    public void GetAchievement_UnbekannterIdText_GibtNullZurueck()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Ausführung
        var achievement = service.GetAchievement("NICHT_VORHANDEN_ID_XYZ");

        // Prüfung
        achievement.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // UnlockedCount
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void UnlockedCount_NeuerSpieler_IstNull()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Prüfung: Neuer Spieler hat keine freigeschalteten Achievements
        service.UnlockedCount.Should().Be(0);
    }

    [Fact]
    public void UnlockedCount_MitFreigeschaltetemAchievement_IstEins()
    {
        // Vorbereitung: Einen Achievement-Eintrag im State setzen
        var (service, _, state) = ErstelleService();
        state.UnlockedAchievements = ["first_order"];
        service.Reset(); // Lädt UnlockedAchievements aus State

        // Prüfung
        service.UnlockedCount.Should().Be(1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CheckAchievements - Auftrag freischaltet "first_order"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CheckAchievements_ErsterAuftrag_SchaltetFirstOrderFrei()
    {
        // Vorbereitung
        var (service, mockState, state) = ErstelleService();
        // Worker entfernen: GameState.CreateNew() legt 2 Worker an →
        // CheckAchievements würde sonst "worker_first" VOR "first_order" feuern
        // und das Event würde "worker_first" enthalten statt "first_order".
        // Kein Worker = "worker_first" bleibt gesperrt.
        state.Workshops[0].Workers.Clear();

        var freigeschalteteIds = new List<string>();
        service.AchievementUnlocked += (_, a) => freigeschalteteIds.Add(a.Id);

        // Auftrag abgeschlossen setzen
        state.Statistics.TotalOrdersCompleted = 1;

        // Ausführung
        service.CheckAchievements();

        // Prüfung: "first_order" muss freigeschaltet worden sein
        freigeschalteteIds.Should().Contain("first_order");
    }

    [Fact]
    public void CheckAchievements_BereitsFreigeschaltet_KeinDoppeltesFeuern()
    {
        // Vorbereitung: Achievement bereits freigeschaltet
        var (service, _, state) = ErstelleService();
        // Worker entfernen damit keine anderen Achievements feuern
        state.Workshops[0].Workers.Clear();
        state.UnlockedAchievements = ["first_order"];
        state.Statistics.TotalOrdersCompleted = 1;
        service.Reset();

        int feuernAnzahl = 0;
        service.AchievementUnlocked += (_, _) => feuernAnzahl++;

        // Ausführung
        service.CheckAchievements();

        // Prüfung: "first_order" darf nicht erneut gefeuert werden
        feuernAnzahl.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CheckAchievements - Geld-Achievements
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CheckAchievements_TausendEuroVerdient_SchaltetMoneyAchievementFrei()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        Achievement? freigeschaltet = null;
        service.AchievementUnlocked += (_, a) => freigeschaltet = a;
        state.TotalMoneyEarned = 1000m;

        // Ausführung
        service.CheckAchievements();

        // Prüfung: "money_1k" freigeschaltet
        freigeschaltet.Should().NotBeNull();
        freigeschaltet!.Id.Should().Be("money_1k");
    }

    // ═══════════════════════════════════════════════════════════════════
    // CheckAchievements - Belohnung wird gutgeschrieben
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CheckAchievements_AchievementMitGeldBelohnung_RuftAddMoneyAuf()
    {
        // Vorbereitung
        var (service, mockState, state) = ErstelleService();
        state.Statistics.TotalOrdersCompleted = 1;

        // Ausführung
        service.CheckAchievements();

        // Prüfung: AddMoney wurde aufgerufen (first_order hat MoneyReward)
        mockState.Received().AddMoney(Arg.Any<decimal>());
    }

    // ═══════════════════════════════════════════════════════════════════
    // BoostAchievement
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void BoostAchievement_UnbekannteId_GibtFalseZurueck()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Ausführung
        var ergebnis = service.BoostAchievement("NICHT_VORHANDEN", 0.20);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void BoostAchievement_BereitsFreigeschaltet_GibtFalseZurueck()
    {
        // Vorbereitung: Achievement freischalten
        var (service, _, state) = ErstelleService();
        state.UnlockedAchievements = ["first_order"];
        state.Statistics.TotalOrdersCompleted = 1;
        service.Reset();

        // Ausführung
        var ergebnis = service.BoostAchievement("first_order", 0.20);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void BoostAchievement_GueltigeId_ErhoehrtFortschritt()
    {
        // Vorbereitung: "orders_50" braucht 50 Aufträge, wir haben noch keine
        var (service, _, _) = ErstelleService();
        var achievement = service.GetAchievement("orders_50");
        achievement.Should().NotBeNull();
        var fortschrittVorher = achievement!.CurrentValue;

        // Ausführung: 20% Boost
        var ergebnis = service.BoostAchievement("orders_50", 0.20);

        // Prüfung
        ergebnis.Should().BeTrue();
        achievement.CurrentValue.Should().BeGreaterThan(fortschrittVorher);
    }

    [Fact]
    public void BoostAchievement_ZweimalBoost_GibtBeimZweitenFalseZurueck()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Ausführung
        service.BoostAchievement("orders_50", 0.20);
        var ergebnis2 = service.BoostAchievement("orders_50", 0.20);

        // Prüfung: HasUsedAdBoost verhindert zweiten Boost
        ergebnis2.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetUnlockedAchievements
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetUnlockedAchievements_NeuerSpieler_IstLeer()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Ausführung
        var liste = service.GetUnlockedAchievements();

        // Prüfung
        liste.Should().BeEmpty();
    }

    [Fact]
    public void GetUnlockedAchievements_NachFreischalten_EnthältAchievement()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Statistics.TotalOrdersCompleted = 1;
        service.CheckAchievements();

        // Ausführung
        var liste = service.GetUnlockedAchievements();

        // Prüfung
        liste.Should().Contain(a => a.Id == "first_order");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Reset
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Reset_NachFreischalten_SetztUnlockedCountZurueck()
    {
        // Vorbereitung: Achievement freischalten
        var (service, _, state) = ErstelleService();
        state.Statistics.TotalOrdersCompleted = 1;
        service.CheckAchievements();
        service.UnlockedCount.Should().BeGreaterThan(0);

        // State leeren damit Reset nichts aus UnlockedAchievements laden kann
        state.UnlockedAchievements = [];
        state.Statistics.TotalOrdersCompleted = 0;

        // Ausführung
        service.Reset();

        // Prüfung
        service.UnlockedCount.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Dispose - Events werden abgemeldet
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Dispose_KannZweimalAufgerufenWerdenOhneException()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Ausführung + Prüfung: Kein Exception
        var action = () =>
        {
            service.Dispose();
            service.Dispose();
        };
        action.Should().NotThrow();
    }
}
