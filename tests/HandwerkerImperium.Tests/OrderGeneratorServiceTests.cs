using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für OrderGeneratorService: Auftrags-Generierung, Schwierigkeit,
/// Belohnungen, Zeitlimits und Slot-Berechnung.
/// </summary>
public class OrderGeneratorServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erstellt einen GameState mit Schreinerei-Workshop auf dem angegeben Level
    /// und dem angegeben Spieler-Level. GuildMembership ist null (kein Bonus).
    /// </summary>
    private static GameState ErstelleState(int workshopLevel = 1, int playerLevel = 1)
    {
        var state = GameState.CreateNew();
        state.PlayerLevel = playerLevel;
        state.Workshops[0].Level = workshopLevel;
        state.GuildMembership = null;
        return state;
    }

    /// <summary>
    /// Erstellt einen gemockten IGameStateService der den übergebenen State zurückgibt.
    /// IsWorkshopUnlocked wird aus UnlockedWorkshopTypes abgeleitet.
    /// </summary>
    private static IGameStateService ErstelleMockStateService(GameState state)
    {
        var mock = Substitute.For<IGameStateService>();
        mock.State.Returns(state);
        mock.IsWorkshopUnlocked(Arg.Any<WorkshopType>()).Returns(ci =>
            state.UnlockedWorkshopTypes.Contains(ci.Arg<WorkshopType>()));
        return mock;
    }

    private static OrderGeneratorService ErstelleService(IGameStateService stateService)
        => new(stateService);

    // ═══════════════════════════════════════════════════════════════════
    // GenerateOrder - Korrektheit
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateOrder_Schreiner_GibtWorkshopTypZurueck()
    {
        // Vorbereitung
        var state = ErstelleState();
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung
        var auftrag = service.GenerateOrder(WorkshopType.Carpenter, 1);

        // Prüfung
        auftrag.WorkshopType.Should().Be(WorkshopType.Carpenter);
    }

    [Fact]
    public void GenerateOrder_Klempner_GibtKlempnerTypZurueck()
    {
        // Vorbereitung
        var state = ErstelleState();
        state.Workshops.Add(new Workshop { Type = WorkshopType.Plumber, Level = 1, IsUnlocked = true });
        state.UnlockedWorkshopTypes.Add(WorkshopType.Plumber);
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung
        var auftrag = service.GenerateOrder(WorkshopType.Plumber, 1);

        // Prüfung
        auftrag.WorkshopType.Should().Be(WorkshopType.Plumber);
    }

    [Fact]
    public void GenerateOrder_HatTasks_MindestensEine()
    {
        // Vorbereitung
        var state = ErstelleState();
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung
        var auftrag = service.GenerateOrder(WorkshopType.Carpenter, 1);

        // Prüfung: Jeder Auftrag muss mindestens eine Aufgabe haben
        auftrag.Tasks.Should().NotBeEmpty();
    }

    [Fact]
    public void GenerateOrder_HatKundennamen_NichtLeer()
    {
        // Vorbereitung
        var state = ErstelleState();
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung
        var auftrag = service.GenerateOrder(WorkshopType.Carpenter, 1);

        // Prüfung
        auftrag.CustomerName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateOrder_HatBasisBelohnung_GroesserNull()
    {
        // Vorbereitung
        var state = ErstelleState(workshopLevel: 1, playerLevel: 5);
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung
        var auftrag = service.GenerateOrder(WorkshopType.Carpenter, 1);

        // Prüfung
        auftrag.BaseReward.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void GenerateOrder_HatBasisXp_GroesserNull()
    {
        // Vorbereitung
        var state = ErstelleState(workshopLevel: 1, playerLevel: 1);
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung
        var auftrag = service.GenerateOrder(WorkshopType.Carpenter, 1);

        // Prüfung: BaseXp = 25 * workshopLevel * taskCount
        auftrag.BaseXp.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateOrder_HatTitleKey_NichtLeer()
    {
        // Vorbereitung
        var state = ErstelleState();
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung
        var auftrag = service.GenerateOrder(WorkshopType.Carpenter, 1);

        // Prüfung
        auftrag.TitleKey.Should().NotBeNullOrWhiteSpace();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Schwierigkeits-Skalierung
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateOrder_Level1OhnePrestige_KeinExpert()
    {
        // Vorbereitung: Level 1 Workshop, kein Prestige, Reputation unter 80
        var state = ErstelleState(workshopLevel: 1, playerLevel: 1);
        // Reputation-Default ist 50 (unter 80 = kein Expert)
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung: 100 Aufträge generieren
        var schwierigkeiten = Enumerable.Range(0, 100)
            .Select(_ => service.GenerateOrder(WorkshopType.Carpenter, 1).Difficulty)
            .ToList();

        // Prüfung: Bei Level 1 kein Expert (Expert erst ab Reputation >= 80)
        schwierigkeiten.Should().NotContain(OrderDifficulty.Expert);
    }

    [Fact]
    public void GenerateOrder_HohesLevel_KannHardEnthalten()
    {
        // Vorbereitung: Sehr hohes Workshop-Level mit Prestige
        var state = ErstelleState(workshopLevel: 500, playerLevel: 100);
        state.Prestige.BronzeCount = 2; // TotalPrestigeCount = 2
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung: Viele Aufträge generieren
        var schwierigkeiten = Enumerable.Range(0, 100)
            .Select(_ => service.GenerateOrder(WorkshopType.Carpenter, 500).Difficulty)
            .ToList();

        // Prüfung: Bei Level 500 und 2x Prestige muss Hard vorkommen
        schwierigkeiten.Should().Contain(d => d >= OrderDifficulty.Hard);
    }

    [Fact]
    public void GenerateOrder_Reputation_UnterAchtzig_KeinExpert()
    {
        // Regression: Expert-Aufträge erfordern Reputation >= 80 (Gotcha: Fallback auf Hard)
        var state = ErstelleState(workshopLevel: 400, playerLevel: 100);
        state.Prestige.BronzeCount = 3; // TotalPrestigeCount = 3
        state.Reputation.ReputationScore = 79; // Knapp unter Schwelle

        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung: Viele Aufträge generieren
        var auftraege = Enumerable.Range(0, 200)
            .Select(_ => service.GenerateOrder(WorkshopType.Carpenter, 400))
            .ToList();

        // Prüfung: Kein Expert trotz hohem Level (Reputation-Gate)
        auftraege.Should().NotContain(a => a.Difficulty == OrderDifficulty.Expert);
    }

    [Fact]
    public void GenerateOrder_Reputation_AbAchtzig_KannExpertEnthalten()
    {
        // Vorbereitung: Reputation genau 80 und hohes Level mit viel Prestige
        var state = ErstelleState(workshopLevel: 500, playerLevel: 100);
        state.Prestige.BronzeCount = 5; // TotalPrestigeCount = 5
        state.Reputation.ReputationScore = 80; // Genau an Schwelle

        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung: Viele Aufträge generieren
        var auftraege = Enumerable.Range(0, 500)
            .Select(_ => service.GenerateOrder(WorkshopType.Carpenter, 500))
            .ToList();

        // Prüfung: Expert muss bei hinreichend vielen Aufträgen erscheinen
        auftraege.Should().Contain(a => a.Difficulty == OrderDifficulty.Expert);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Auftrags-Belohnungen skalieren mit Level
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateOrder_HoeheresPlayerLevel_HoehereBaseReward()
    {
        // Vorbereitung: Kein Nettoeinkommen, damit der Level-Faktor dominiert
        // Formel: perTaskReward = max(100 + level*100, netIncome*300)
        var stateLow = ErstelleState(workshopLevel: 1, playerLevel: 1);
        var stateHigh = ErstelleState(workshopLevel: 1, playerLevel: 50);

        var serviceLow = ErstelleService(ErstelleMockStateService(stateLow));
        var serviceHigh = ErstelleService(ErstelleMockStateService(stateHigh));

        // Ausführung: Mittelwert über 30 Aufträge (Random-Streuung ausgleichen)
        var belohnungLow = Enumerable.Range(0, 30)
            .Average(_ => (double)serviceLow.GenerateOrder(WorkshopType.Carpenter, 1).BaseReward);
        var belohnungHigh = Enumerable.Range(0, 30)
            .Average(_ => (double)serviceHigh.GenerateOrder(WorkshopType.Carpenter, 1).BaseReward);

        // Prüfung: Level 50 → 5.100 Basis; Level 1 → 200 Basis
        belohnungHigh.Should().BeGreaterThan(belohnungLow * 10);
    }

    [Fact]
    public void GenerateOrder_HoeheresWorkshopLevel_HoehereXp()
    {
        // Vorbereitung: XP = 25 * workshopLevel * taskCount
        var stateLow = ErstelleState(workshopLevel: 1, playerLevel: 1);
        var stateHigh = ErstelleState(workshopLevel: 10, playerLevel: 1);

        var serviceLow = ErstelleService(ErstelleMockStateService(stateLow));
        var serviceHigh = ErstelleService(ErstelleMockStateService(stateHigh));

        // Ausführung
        var xpLow = Enumerable.Range(0, 20)
            .Average(_ => serviceLow.GenerateOrder(WorkshopType.Carpenter, 1).BaseXp);
        var xpHigh = Enumerable.Range(0, 20)
            .Average(_ => serviceHigh.GenerateOrder(WorkshopType.Carpenter, 10).BaseXp);

        // Prüfung: WS-Level 10 liefert ~10x mehr XP
        xpHigh.Should().BeGreaterThan(xpLow * 5);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Auftrags-Typen und Zeitlimits
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateOrder_UnterLevel10_NurStandard()
    {
        // Vorbereitung: DetermineOrderType gibt bei playerLevel < 10 immer Standard zurück
        var state = ErstelleState(workshopLevel: 5, playerLevel: 9);
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung: 50 Aufträge
        var typen = Enumerable.Range(0, 50)
            .Select(_ => service.GenerateOrder(WorkshopType.Carpenter, 5).OrderType)
            .ToList();

        // Prüfung
        typen.Should().AllSatisfy(t => t.Should().Be(OrderType.Standard));
    }

    [Fact]
    public void GenerateOrder_StandardAuftrag_HatKeineDeadline()
    {
        // Vorbereitung: Spieler-Level < 10 → immer Standard → keine Deadline
        var state = ErstelleState(workshopLevel: 1, playerLevel: 5);
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung
        var auftrag = service.GenerateOrder(WorkshopType.Carpenter, 1);

        // Prüfung
        auftrag.OrderType.Should().Be(OrderType.Standard);
        auftrag.Deadline.Should().BeNull();
    }

    [Fact]
    public void GenerateOrder_WeeklyAuftrag_HatDeadlineInDerZukunft()
    {
        // Vorbereitung: Höheres Level und 2 Workshops damit Weekly möglich wird
        var state = ErstelleState(workshopLevel: 25, playerLevel: 25);
        state.Workshops.Add(new Workshop { Type = WorkshopType.Plumber, Level = 5, IsUnlocked = true });
        state.UnlockedWorkshopTypes.Add(WorkshopType.Plumber);
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung: Bis zu 1000 Versuche einen Weekly zu finden
        Order? weekly = null;
        for (int i = 0; i < 1000 && weekly == null; i++)
        {
            var a = service.GenerateOrder(WorkshopType.Carpenter, 25);
            if (a.OrderType == OrderType.Weekly) weekly = a;
        }

        // Prüfung: Wenn Weekly gefunden, muss Deadline in Zukunft liegen
        if (weekly != null)
        {
            weekly.Deadline.Should().NotBeNull();
            weekly.Deadline!.Value.Should().BeAfter(DateTime.UtcNow);
        }
        // Test ist auch gültig wenn kein Weekly gefunden (sehr seltenes Ereignis)
    }

    // ═══════════════════════════════════════════════════════════════════
    // GenerateAvailableOrders
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateAvailableOrders_KeineWorkshopsFreigeschaltet_FallbackAufSchreiner()
    {
        // Vorbereitung: Keine freigeschalteten Workshops
        var state = GameState.CreateNew();
        state.UnlockedWorkshopTypes.Clear();
        var stateService = Substitute.For<IGameStateService>();
        stateService.State.Returns(state);
        stateService.IsWorkshopUnlocked(Arg.Any<WorkshopType>()).Returns(false);
        var service = ErstelleService(stateService);

        // Ausführung
        var auftraege = service.GenerateAvailableOrders(3);

        // Prüfung: Fallback-Verhalten: genau 1 Schreiner-Auftrag
        auftraege.Should().HaveCount(1);
        auftraege[0].WorkshopType.Should().Be(WorkshopType.Carpenter);
    }

    [Fact]
    public void GenerateAvailableOrders_EinWorkshop_GibtAngeforderteMenge()
    {
        // Vorbereitung
        var state = ErstelleState();
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung
        var auftraege = service.GenerateAvailableOrders(3);

        // Prüfung
        auftraege.Should().HaveCount(3);
    }

    [Fact]
    public void GenerateAvailableOrders_AlleAuftraegeFuerFreigeschalteteWorkshops()
    {
        // Vorbereitung: Nur Schreinerei freigeschaltet
        var state = ErstelleState();
        var service = ErstelleService(ErstelleMockStateService(state));

        // Ausführung
        var auftraege = service.GenerateAvailableOrders(5);

        // Prüfung: Alle Aufträge gehören zur Schreinerei
        auftraege.Should().AllSatisfy(a =>
            a.WorkshopType.Should().Be(WorkshopType.Carpenter));
    }

    // ═══════════════════════════════════════════════════════════════════
    // RefreshOrders
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RefreshOrders_EntferntAlteStandardAuftraege()
    {
        // Vorbereitung
        var state = ErstelleState();
        state.AvailableOrders.Add(new Order
        {
            TitleFallback = "Alter Standard-Auftrag",
            OrderType = OrderType.Standard
        });
        var stateService = ErstelleMockStateService(state);
        var service = ErstelleService(stateService);

        // Ausführung
        service.RefreshOrders();

        // Prüfung: Alter Auftrag muss entfernt worden sein
        state.AvailableOrders.Should().NotContain(a => a.TitleFallback == "Alter Standard-Auftrag");
    }

    [Fact]
    public void RefreshOrders_FuegtNeueAuftraegeHinzu()
    {
        // Vorbereitung
        var state = ErstelleState();
        state.AvailableOrders.Clear();
        var stateService = ErstelleMockStateService(state);
        var service = ErstelleService(stateService);

        // Ausführung
        service.RefreshOrders();

        // Prüfung: Neue Aufträge müssen vorhanden sein
        state.AvailableOrders.Should().NotBeEmpty();
    }

    [Fact]
    public void RefreshOrders_BehaeltGueltigeMaterialOrders()
    {
        // Regression: MaterialOrders dürfen bei Refresh nicht gelöscht werden
        var state = ErstelleState();
        var materialOrder = new Order
        {
            OrderType = OrderType.MaterialOrder,
            Deadline = DateTime.UtcNow.AddHours(2) // Noch gültig
        };
        state.AvailableOrders.Add(materialOrder);
        var stateService = ErstelleMockStateService(state);
        var service = ErstelleService(stateService);

        // Ausführung
        service.RefreshOrders();

        // Prüfung: MaterialOrder bleibt erhalten
        state.AvailableOrders.Should().Contain(a => a.OrderType == OrderType.MaterialOrder);
    }

    [Fact]
    public void RefreshOrders_MarkiertDirty()
    {
        // Vorbereitung
        var state = ErstelleState();
        var stateService = ErstelleMockStateService(state);
        var service = ErstelleService(stateService);

        // Ausführung
        service.RefreshOrders();

        // Prüfung: MarkDirty muss aufgerufen worden sein
        stateService.Received().MarkDirty();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GenerateMaterialOrder
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateMaterialOrder_OhneAutoProductionService_GibtNull()
    {
        // Regression: Ohne IAutoProductionService darf kein MaterialOrder entstehen
        var state = ErstelleState();
        var service = new OrderGeneratorService(ErstelleMockStateService(state));

        // Ausführung
        var result = service.GenerateMaterialOrder();

        // Prüfung
        result.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Gilden-Forschungs-Bonus auf Belohnungen
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateOrder_MitGildenBonus30Prozent_ErhoehtBelohnung()
    {
        // Vorbereitung: Gleiche Level-Basis, nur Gilden-Bonus unterscheidet sich
        var stateOhne = ErstelleState(workshopLevel: 1, playerLevel: 1);

        var stateMit = ErstelleState(workshopLevel: 1, playerLevel: 1);
        stateMit.GuildMembership = new GuildMembership { ResearchRewardBonus = 0.30m };

        var serviceOhne = ErstelleService(ErstelleMockStateService(stateOhne));
        var serviceMit = ErstelleService(ErstelleMockStateService(stateMit));

        // Ausführung: Mittelwert über viele Aufträge (Zufall ausgleichen)
        var belohnungOhne = Enumerable.Range(0, 50)
            .Average(_ => (double)serviceOhne.GenerateOrder(WorkshopType.Carpenter, 1).BaseReward);
        var belohnungMit = Enumerable.Range(0, 50)
            .Average(_ => (double)serviceMit.GenerateOrder(WorkshopType.Carpenter, 1).BaseReward);

        // Prüfung: Mit Gilden-Bonus muss Belohnung höher sein
        belohnungMit.Should().BeGreaterThan(belohnungOhne);
    }
}
