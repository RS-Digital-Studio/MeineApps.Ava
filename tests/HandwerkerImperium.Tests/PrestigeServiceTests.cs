using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für PrestigeService: Verfügbarkeit, PP-Berechnung, Shop-Käufe,
/// Reset-Logik, Bonus-PP, Meilensteine und Challenge-Abbruch.
/// </summary>
public class PrestigeServiceTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // Hilfsmethoden (Test-Setup)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Erstellt einen PrestigeService mit gemockten Abhängigkeiten und frischem GameState.</summary>
    private static (PrestigeService service, IGameStateService gameStateMock, GameState state) ErstelleService()
    {
        var gameStateMock = Substitute.For<IGameStateService>();
        var saveGameMock = Substitute.For<ISaveGameService>();
        var ascensionMock = Substitute.For<IAscensionService>();

        var state = GameState.CreateNew();
        gameStateMock.State.Returns(state);

        // Standard: Keine Ascension-Boni
        ascensionMock.GetStartCapitalMultiplier().Returns(1.0m);
        ascensionMock.GetQuickStartWorkshops().Returns(0);
        ascensionMock.GetStartReputation().Returns(50);
        ascensionMock.GetEternalToolsLevel().Returns(0);

        // SaveAsync gibt sofort eine fertige Task zurück
        saveGameMock.SaveAsync().Returns(Task.CompletedTask);

        var service = new PrestigeService(gameStateMock, saveGameMock, ascensionMock);
        return (service, gameStateMock, state);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CanPrestige - Verfügbarkeit
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void CanPrestige_TierNone_IstFalse()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Ausführung + Prüfung
        service.CanPrestige(PrestigeTier.None).Should().BeFalse();
    }

    [Fact]
    public void CanPrestige_Bronze_ZuNiedrigesLevel_IstFalse()
    {
        // Vorbereitung: Bronze erfordert Level 30
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 29;

        // Ausführung + Prüfung
        service.CanPrestige(PrestigeTier.Bronze).Should().BeFalse();
    }

    [Fact]
    public void CanPrestige_Bronze_Level30_IstTrue()
    {
        // Vorbereitung: Genau das Mindest-Level
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;

        // Ausführung + Prüfung
        service.CanPrestige(PrestigeTier.Bronze).Should().BeTrue();
    }

    [Fact]
    public void CanPrestige_Silver_OhneBronze_IstFalse()
    {
        // Vorbereitung: Silver erfordert 1x Bronze, BronzeCount = 0
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 100;
        state.Prestige.BronzeCount = 0;

        // Ausführung + Prüfung
        service.CanPrestige(PrestigeTier.Silver).Should().BeFalse();
    }

    [Fact]
    public void CanPrestige_Silver_MitBronzeUndLevel100_IstTrue()
    {
        // Vorbereitung: Silver erfordert Level 100 + 1x Bronze
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 100;
        state.Prestige.BronzeCount = 1;

        // Ausführung + Prüfung
        service.CanPrestige(PrestigeTier.Silver).Should().BeTrue();
    }

    [Fact]
    public void CanPrestige_Legende_ErfordertDreiMeister()
    {
        // Vorbereitung: Legende erfordert Level 1200 + 3x Meister
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 1200;
        state.Prestige.MeisterCount = 2; // Zu wenig

        // Ausführung + Prüfung
        service.CanPrestige(PrestigeTier.Legende).Should().BeFalse();
    }

    [Fact]
    public void CanPrestige_Legende_MitAllenVoraussetzungen_IstTrue()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 1200;
        state.Prestige.MeisterCount = 3;

        // Ausführung + Prüfung
        service.CanPrestige(PrestigeTier.Legende).Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GetPrestigePoints - PP-Berechnung (Regression: Formel floor(sqrt(x/100_000)))
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetPrestigePoints_KeinGeld_IstNull()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Prüfung: Kein Geld = keine PP
        service.GetPrestigePoints(0m).Should().Be(0);
    }

    [Fact]
    public void GetPrestigePoints_HundertTausend_IstEins()
    {
        // Vorbereitung: floor(sqrt(100_000 / 100_000)) = floor(sqrt(1)) = 1
        var (service, _, _) = ErstelleService();

        // Prüfung
        service.GetPrestigePoints(100_000m).Should().Be(1);
    }

    [Fact]
    public void GetPrestigePoints_EineMillion_IstDrei()
    {
        // Vorbereitung: floor(sqrt(1_000_000 / 100_000)) = floor(sqrt(10)) = floor(3.16) = 3
        var (service, _, _) = ErstelleService();

        // Prüfung
        service.GetPrestigePoints(1_000_000m).Should().Be(3);
    }

    [Fact]
    public void GetPrestigePoints_ZehnMillionen_IstZehn()
    {
        // Vorbereitung: floor(sqrt(10_000_000 / 100_000)) = floor(sqrt(100)) = 10
        var (service, _, _) = ErstelleService();

        // Prüfung
        service.GetPrestigePoints(10_000_000m).Should().Be(10);
    }

    [Fact]
    public void GetPrestigePoints_NegativesGeld_IstNull()
    {
        // Grenzfall: Negative Werte sollen 0 zurückgeben
        var (service, _, _) = ErstelleService();

        // Prüfung
        service.GetPrestigePoints(-1000m).Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DoPrestige - Ausführung und Reset
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DoPrestige_VoraussetzungenNichtErfüllt_GibtFalse()
    {
        // Vorbereitung: Level zu niedrig für Bronze
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 1;

        // Ausführung
        var ergebnis = await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public async Task DoPrestige_Bronze_GibtTrueUndSpeichert()
    {
        // Vorbereitung: Alle Voraussetzungen erfüllt
        var (service, gameStateMock, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 1_000_000m;
        var saveGameMock = Substitute.For<ISaveGameService>();
        saveGameMock.SaveAsync().Returns(Task.CompletedTask);

        // Neue Service-Instanz mit kontrollierbarem SaveGame-Mock
        var service2 = new PrestigeService(gameStateMock, saveGameMock, Substitute.For<IAscensionService>());
        gameStateMock.State.Returns(state);
        var ascMock = Substitute.For<IAscensionService>();
        ascMock.GetStartCapitalMultiplier().Returns(1.0m);
        ascMock.GetQuickStartWorkshops().Returns(0);
        ascMock.GetStartReputation().Returns(50);
        var s = new PrestigeService(gameStateMock, saveGameMock, ascMock);

        // Ausführung
        var ergebnis = await s.DoPrestige(PrestigeTier.Bronze);

        // Prüfung
        ergebnis.Should().BeTrue();
        await saveGameMock.Received(1).SaveAsync();
    }

    [Fact]
    public async Task DoPrestige_Bronze_Setzt_PlayerLevel_Zurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 50;
        state.CurrentRunMoney = 1_000_000m;

        // Ausführung
        await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung: Spieler-Level muss nach Reset auf 1 sein (Regression)
        state.PlayerLevel.Should().Be(1);
    }

    [Fact]
    public async Task DoPrestige_Bronze_Setzt_CurrentRunMoney_Zurueck()
    {
        // Vorbereitung: CurrentRunMoney ist Basis für PP, muss nach Prestige 0 sein
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 5_000_000m;

        // Ausführung
        await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung: Regression-Test — CurrentRunMoney wird zurückgesetzt
        state.CurrentRunMoney.Should().Be(0);
    }

    [Fact]
    public async Task DoPrestige_Bronze_ErhoehBronzeCount()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 100_000m;
        int alteBronzeCount = state.Prestige.BronzeCount;

        // Ausführung
        await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung
        state.Prestige.BronzeCount.Should().Be(alteBronzeCount + 1);
    }

    [Fact]
    public async Task DoPrestige_Bronze_VergibtPrestigePunkte()
    {
        // Vorbereitung: 100k Geld = floor(sqrt(1)) = 1 Basis-PP, Bronze-Multi=1.0, Minimum=15
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 100_000m;

        // Ausführung
        await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung: Bronze-Minimum ist 15 PP (BAL-12, Regression)
        state.Prestige.PrestigePoints.Should().BeGreaterThanOrEqualTo(15);
    }

    [Fact]
    public async Task DoPrestige_Bronze_ErhoehPermanentMultiplier()
    {
        // Vorbereitung: Erster Bronze = +20% Bonus (Basis ohne Diminishing Returns)
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 100_000m;
        decimal altMultiplikator = state.Prestige.PermanentMultiplier;

        // Ausführung
        await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung: Multiplikator muss gestiegen sein
        state.Prestige.PermanentMultiplier.Should().BeGreaterThan(altMultiplikator);
    }

    [Fact]
    public async Task DoPrestige_Bronze_MultiplikatorUeberschreitetNichtMaximum()
    {
        // Vorbereitung: Nach vielen Prestiges darf der Multiplikator nicht über 20x steigen
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 100_000m;
        state.Prestige.PermanentMultiplier = 19.99m;
        state.Prestige.BronzeCount = 0; // Erster Bronze-Prestige

        // Ausführung
        await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung: Cap bei 20x (MaxPermanentMultiplier = 20)
        state.Prestige.PermanentMultiplier.Should().BeLessThanOrEqualTo(20.0m);
    }

    [Fact]
    public async Task DoPrestige_Bronze_FeuertPrestigeCompletedEvent()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 100_000m;
        bool eventGefeuert = false;
        service.PrestigeCompleted += (_, _) => eventGefeuert = true;

        // Ausführung
        await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung
        eventGefeuert.Should().BeTrue();
    }

    [Fact]
    public async Task DoPrestige_Bronze_FugtHistoryEintragHinzu()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 100_000m;

        // Ausführung
        await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung: History-Eintrag wurde angelegt
        state.Prestige.History.Should().HaveCount(1);
        state.Prestige.History[0].Tier.Should().Be(PrestigeTier.Bronze);
    }

    [Fact]
    public async Task DoPrestige_History_MaxZwanzigEintraege()
    {
        // Vorbereitung: 21 Prestiges sollen auf 20 Einträge begrenzt werden
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;

        for (int i = 0; i < 21; i++)
        {
            state.CurrentRunMoney = 100_000m;
            state.PlayerLevel = 30; // Reset-Schutz (DoPrestige setzt Level zurück)
            await service.DoPrestige(PrestigeTier.Bronze);
        }

        // Prüfung: Auf 20 begrenzt (Regression)
        state.Prestige.History.Should().HaveCountLessThanOrEqualTo(20);
    }

    [Fact]
    public async Task DoPrestige_PrestigePass_ErhoehtPPUmFuenfzigProzent()
    {
        // Vorbereitung: Prestige-Pass aktiv → +50% PP
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 1_000_000m; // 3 Basis-PP
        state.IsPrestigePassActive = false;
        await service.DoPrestige(PrestigeTier.Bronze);
        int ppOhnePass = state.Prestige.PrestigePoints;

        // Neuen State mit Pass
        var (service2, _, state2) = ErstelleService();
        state2.PlayerLevel = 30;
        state2.CurrentRunMoney = 1_000_000m;
        state2.IsPrestigePassActive = true;
        await service2.DoPrestige(PrestigeTier.Bronze);
        int ppMitPass = state2.Prestige.PrestigePoints;

        // Prüfung: Mit Pass mehr PP als ohne
        ppMitPass.Should().BeGreaterThan(ppOhnePass);
    }

    [Fact]
    public async Task DoPrestige_Bronze_SetztStartGeld()
    {
        // Vorbereitung: Bronze-Startgeld = 10.000 EUR
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 100_000m;

        // Ausführung
        await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung: Startgeld entspricht Tier-Wert
        state.Money.Should().BeGreaterThanOrEqualTo(10_000m);
    }

    [Fact]
    public async Task DoPrestige_Bronze_SetztWorkshopsZurueck()
    {
        // Vorbereitung: Mehrere Workshops vorhanden
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 100_000m;
        state.Workshops.Add(Workshop.Create(WorkshopType.Plumber));

        // Ausführung
        await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung: Nach Bronze-Reset nur Schreinerei (kein QuickStart-Perk aktiv)
        state.Workshops.Should().HaveCount(1);
        state.Workshops[0].Type.Should().Be(WorkshopType.Carpenter);
    }

    [Fact]
    public async Task DoPrestige_Bronze_SetztSpeedBoostFuerDreissigMinuten()
    {
        // BAL-12: Nach erstem Bronze-Prestige gibt es 30min 3x Speed-Boost
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 100_000m;

        // Ausführung
        await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung: SpeedBoostEndTime muss in der Zukunft liegen
        state.SpeedBoostEndTime.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task DoPrestige_Gold_BehältForschung()
    {
        // Vorbereitung: Gold-Prestige erhält Research (Regression)
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 250;
        state.CurrentRunMoney = 100_000m;
        state.Prestige.SilverCount = 1;

        // Forschungen als abgeschlossen markieren
        state.Researches = ResearchTree.CreateAll();
        state.Researches[0].IsResearched = true;

        // Ausführung
        await service.DoPrestige(PrestigeTier.Gold);

        // Prüfung: Forschung wurde erhalten (Gold behält Research)
        state.Researches.Should().NotBeEmpty();
        state.Researches.Any(r => r.IsResearched).Should().BeTrue();
    }

    [Fact]
    public async Task DoPrestige_Bronze_LoeschtForschung()
    {
        // Vorbereitung: Bronze-Prestige löscht Research
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 100_000m;
        state.Researches = ResearchTree.CreateAll();
        state.Researches[0].IsResearched = true;

        // Ausführung
        await service.DoPrestige(PrestigeTier.Bronze);

        // Prüfung: Alle Forschungen wurden zurückgesetzt
        state.Researches.All(r => !r.IsResearched).Should().BeTrue();
    }

    [Fact]
    public async Task DoPrestige_DiminishingReturns_ZweiterBronzeGibtWenigerBonus()
    {
        // Vorbereitung: Diminishing Returns — 2. Bronze-Prestige bringt weniger Multiplikator
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 100_000m;

        // Erster Prestige
        await service.DoPrestige(PrestigeTier.Bronze);
        decimal multiplierNachErstem = state.Prestige.PermanentMultiplier;

        // Zweiter Prestige (Diminishing Returns wirken)
        state.PlayerLevel = 30;
        state.CurrentRunMoney = 100_000m;
        await service.DoPrestige(PrestigeTier.Bronze);
        decimal zuwachsZweiter = state.Prestige.PermanentMultiplier - multiplierNachErstem;

        // Prüfung: Zweiter Bonus kleiner als erster (0.20m)
        zuwachsZweiter.Should().BeLessThan(0.20m);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GetPermanentMultiplier
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetPermanentMultiplier_KeinePrestige_IstEins()
    {
        // Vorbereitung: Frischer State
        var (service, _, state) = ErstelleService();
        state.Prestige.PermanentMultiplier = 1.0m;

        // Prüfung: Startwert ist 1.0
        service.GetPermanentMultiplier().Should().Be(1.0m);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BuyShopItem
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BuyShopItem_UnbekannteId_GibtFalse()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Ausführung + Prüfung
        service.BuyShopItem("nicht_existierende_id").Should().BeFalse();
    }

    [Fact]
    public void BuyShopItem_ZuWenigPP_GibtFalse()
    {
        // Vorbereitung: Keine Prestige-Punkte vorhanden
        var (service, _, state) = ErstelleService();
        state.Prestige.PrestigePoints = 0;

        // Das billigste nicht-wiederholbare Item aus dem Shop finden
        var shopItems = PrestigeShop.GetAllItems();
        var erstesItem = shopItems.FirstOrDefault(i => !i.IsRepeatable);

        if (erstesItem == null) return; // Guard wenn Shop leer

        // Ausführung + Prüfung
        service.BuyShopItem(erstesItem.Id).Should().BeFalse();
    }

    [Fact]
    public void BuyShopItem_GenugPP_GibtTrue()
    {
        // Vorbereitung: Genug Prestige-Punkte
        var (service, _, state) = ErstelleService();

        var shopItems = PrestigeShop.GetAllItems();
        var erstesItem = shopItems.FirstOrDefault(i => !i.IsRepeatable);
        if (erstesItem == null) return;

        state.Prestige.PrestigePoints = erstesItem.Cost + 100;

        // Ausführung + Prüfung
        service.BuyShopItem(erstesItem.Id).Should().BeTrue();
    }

    [Fact]
    public void BuyShopItem_AbzugDerPrestigePunkte()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();

        var shopItems = PrestigeShop.GetAllItems();
        var erstesItem = shopItems.FirstOrDefault(i => !i.IsRepeatable);
        if (erstesItem == null) return;

        state.Prestige.PrestigePoints = erstesItem.Cost + 100;
        int ppVorher = state.Prestige.PrestigePoints;

        // Ausführung
        service.BuyShopItem(erstesItem.Id);

        // Prüfung: PP wurden korrekt abgezogen
        state.Prestige.PrestigePoints.Should().Be(ppVorher - erstesItem.Cost);
    }

    [Fact]
    public void BuyShopItem_BereitsGekauft_GibtFalse()
    {
        // Vorbereitung: Item bereits in PurchasedShopItems
        var (service, _, state) = ErstelleService();

        var shopItems = PrestigeShop.GetAllItems();
        var erstesItem = shopItems.FirstOrDefault(i => !i.IsRepeatable);
        if (erstesItem == null) return;

        state.Prestige.PrestigePoints = 9999;
        service.BuyShopItem(erstesItem.Id);

        // Zweiter Kauf-Versuch
        state.Prestige.PrestigePoints = 9999;
        service.BuyShopItem(erstesItem.Id).Should().BeFalse();
    }

    [Fact]
    public void BuyShopItem_WiederholbaresItem_KannMehrfachGekauftWerden()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();

        var shopItems = PrestigeShop.GetAllItems();
        var wiederholbaresItem = shopItems.FirstOrDefault(i => i.IsRepeatable);
        if (wiederholbaresItem == null) return;

        state.Prestige.PrestigePoints = 9999;

        // Erster Kauf
        bool erstKauf = service.BuyShopItem(wiederholbaresItem.Id);

        // Zweiter Kauf mit genug PP
        state.Prestige.PrestigePoints = 9999;
        bool zweitKauf = service.BuyShopItem(wiederholbaresItem.Id);

        // Prüfung: Beide Käufe erfolgreich
        erstKauf.Should().BeTrue();
        zweitKauf.Should().BeTrue();
    }

    [Fact]
    public void BuyShopItem_WiederholbaresItem_KostenSteigenExponentiell()
    {
        // Vorbereitung: Exponentiell steigende Kosten: Cost * 2^(Kaufanzahl)
        var shopItems = PrestigeShop.GetAllItems();
        var item = shopItems.FirstOrDefault(i => i.IsRepeatable);
        if (item == null) return;

        // Prüfung: Zweiter Kauf kostet doppelt so viel wie erster
        int erstKauf = PrestigeService.GetRepeatableItemCost(item, 0);
        int zweitKauf = PrestigeService.GetRepeatableItemCost(item, 1);

        zweitKauf.Should().Be(erstKauf * 2);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CalculateBonusPrestigePoints
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateBonusPrestigePoints_LeererState_IstNull()
    {
        // Vorbereitung: Kein Level-Überschuss, keine Perfects, keine Forschung, keine Gebäude
        var (service, _, state) = ErstelleService();
        state.Statistics.PerfectRatings = 0;
        state.Researches.Clear();
        state.Buildings.Clear();
        state.PlayerLevel = 30; // Genau Tier-Minimum Bronze

        // Ausführung
        int bonusPp = service.CalculateBonusPrestigePoints(PrestigeTier.Bronze);

        // Prüfung: Kein Bonus bei leerem State
        bonusPp.Should().Be(0);
    }

    [Fact]
    public void CalculateBonusPrestigePoints_ZehnPerfectRatings_GibtEinPP()
    {
        // Vorbereitung: +1 PP pro 10 Perfects
        var (service, _, state) = ErstelleService();
        state.Statistics.PerfectRatings = 10;
        state.Researches.Clear();
        state.Buildings.Clear();
        state.PlayerLevel = 30;

        // Ausführung
        int bonusPp = service.CalculateBonusPrestigePoints(PrestigeTier.Bronze);

        // Prüfung
        bonusPp.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void CalculateBonusPrestigePoints_PerfectRatings_MaxCap()
    {
        // Vorbereitung: Cap bei +5 PP egal wie viele Perfects
        var (service, _, state) = ErstelleService();
        state.Statistics.PerfectRatings = 10000; // Weit über Cap
        state.Researches.Clear();
        state.Buildings.Clear();
        state.PlayerLevel = 30;

        // Ausführung
        int bonusPp = service.CalculateBonusPrestigePoints(PrestigeTier.Bronze);

        // Prüfung: Perfect-Ratings-Beitrag max 5
        bonusPp.Should().BeLessThanOrEqualTo(5 + 6 + 1 + 5); // Max aller Bonus-Quellen
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CheckAndAwardMilestones
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void CheckAndAwardMilestones_ErsterPrestige_VergibtGoldenScrews()
    {
        // Vorbereitung: 1 Prestige = Meilenstein pm_first
        var (service, gameStateMock, state) = ErstelleService();
        state.Prestige.BronzeCount = 1; // TotalPrestigeCount = 1

        // Ausführung
        int gsVerdient = service.CheckAndAwardMilestones();

        // Prüfung: GS wurden vergeben
        gsVerdient.Should().BeGreaterThan(0);
        gameStateMock.Received().AddGoldenScrews(Arg.Any<int>(), false);
    }

    [Fact]
    public void CheckAndAwardMilestones_BereitsGemeldet_NichtNochmal()
    {
        // Vorbereitung: Meilenstein bereits beansprucht
        var (service, gameStateMock, state) = ErstelleService();
        state.Prestige.BronzeCount = 1;
        state.Prestige.ClaimedMilestones.Add("pm_first");

        // Ausführung
        int gsVerdient = service.CheckAndAwardMilestones();

        // Prüfung: Keine GS nochmal vergeben
        gsVerdient.Should().Be(0);
        gameStateMock.DidNotReceive().AddGoldenScrews(Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void CheckAndAwardMilestones_MeilensteinEvent_WirdGefeuert()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Prestige.BronzeCount = 1;
        PrestigeMilestoneEventArgs? empfangeneArgs = null;
        service.MilestoneReached += (_, args) => empfangeneArgs = args;

        // Ausführung
        service.CheckAndAwardMilestones();

        // Prüfung
        empfangeneArgs.Should().NotBeNull();
        empfangeneArgs!.MilestoneId.Should().Be("pm_first");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HasActiveChallenges / AbandonChallengeRun
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void HasActiveChallenges_OhneChallenge_IstFalse()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Prestige.ActiveChallenges.Clear();

        // Prüfung
        service.HasActiveChallenges.Should().BeFalse();
    }

    [Fact]
    public void HasActiveChallenges_MitChallenge_IstTrue()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Prestige.ActiveChallenges.Add(PrestigeChallengeType.Spartaner);

        // Prüfung
        service.HasActiveChallenges.Should().BeTrue();
    }

    [Fact]
    public void AbandonChallengeRun_OhneAktiveChallenge_GibtNull()
    {
        // Vorbereitung: Kein aktiver Challenge-Run
        var (service, _, state) = ErstelleService();
        state.Prestige.ActiveChallenges.Clear();

        // Ausführung
        int ppVergeben = service.AbandonChallengeRun();

        // Prüfung
        ppVergeben.Should().Be(0);
    }

    [Fact]
    public void AbandonChallengeRun_MitChallenge_GibtMindestensEinPP()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Prestige.ActiveChallenges.Add(PrestigeChallengeType.Spartaner);
        state.CurrentRunMoney = 200_000m; // 1 Basis-PP → 0 nach /2, aber Minimum ist 1

        // Ausführung
        int ppVergeben = service.AbandonChallengeRun();

        // Prüfung: Mindestens 1 PP (Math.Max(1, ...))
        ppVergeben.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void AbandonChallengeRun_DeaktiviertChallenges()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Prestige.ActiveChallenges.Add(PrestigeChallengeType.Sprint);
        state.Prestige.ActiveChallenges.Add(PrestigeChallengeType.KeinNetz);

        // Ausführung
        service.AbandonChallengeRun();

        // Prüfung: Challenges wurden deaktiviert
        state.Prestige.ActiveChallenges.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GetShopItems
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetShopItems_TierGesperrteItems_SindNichtSichtbar()
    {
        // Vorbereitung: Spieler hat noch kein Diamant-Prestige
        var (service, _, state) = ErstelleService();
        state.Prestige.CurrentTier = PrestigeTier.Bronze;

        // Ausführung
        var items = service.GetShopItems();

        // Prüfung: Diamant-required Items sind nicht in der Liste (wenn nicht bereits gekauft)
        items.All(i => i.RequiredTier == PrestigeTier.None
                       || i.RequiredTier <= state.Prestige.CurrentTier
                       || i.IsPurchased)
             .Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ActivatePrestigePass
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ActivatePrestigePass_SetzIsPrestigePassActive()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.IsPrestigePassActive = false;

        // Ausführung
        service.ActivatePrestigePass();

        // Prüfung
        state.IsPrestigePassActive.Should().BeTrue();
    }
}
