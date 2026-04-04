using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für AscensionService: Verfügbarkeit, AP-Berechnung, Perk-Upgrades,
/// Perk-Effekte und Ascension-Reset-Logik.
/// </summary>
public class AscensionServiceTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // Hilfsmethoden (Test-Setup)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Erstellt einen AscensionService mit gemockten Abhängigkeiten.</summary>
    private static (AscensionService service, IGameStateService gameStateMock, GameState state) ErstelleService()
    {
        var gameStateMock = Substitute.For<IGameStateService>();
        var saveGameMock = Substitute.For<ISaveGameService>();
        var audioMock = Substitute.For<IAudioService>();

        var state = GameState.CreateNew();
        gameStateMock.State.Returns(state);

        saveGameMock.SaveAsync().Returns(Task.CompletedTask);
        audioMock.PlaySoundAsync(Arg.Any<GameSound>()).Returns(Task.CompletedTask);

        var service = new AscensionService(gameStateMock, saveGameMock, audioMock);
        return (service, gameStateMock, state);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CanAscend
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void CanAscend_OhneLegendePrestige_IstFalse()
    {
        // Vorbereitung: Keine Legende-Prestiges
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 0;

        // Prüfung
        service.CanAscend.Should().BeFalse();
    }

    [Fact]
    public void CanAscend_ZweiLegendePrestige_IstFalse()
    {
        // Vorbereitung: 2 Legendes sind nicht genug (Minimum: 3)
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 2;

        // Prüfung
        service.CanAscend.Should().BeFalse();
    }

    [Fact]
    public void CanAscend_DreiLegendePrestige_IstTrue()
    {
        // Vorbereitung: Genau 3 Legendes = Schwellenwert
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;

        // Prüfung
        service.CanAscend.Should().BeTrue();
    }

    [Fact]
    public void CanAscend_MehrAlsDreiLegenden_IstTrue()
    {
        // Vorbereitung: Mehr als Minimum
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 10;

        // Prüfung
        service.CanAscend.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CalculateAscensionPoints
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateAscensionPoints_Minimum_IstFuenf()
    {
        // Vorbereitung: Frischer State = Minimum 5 AP
        var (service, _, state) = ErstelleService();
        state.Prestige.TotalPrestigePoints = 0;
        state.Prestige.LegendeCount = 3;
        state.CollectedMasterTools.Clear();

        // Ausführung
        int ap = service.CalculateAscensionPoints();

        // Prüfung: Mindestens 5 AP (Regression: Math.Max(5, ...))
        ap.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void CalculateAscensionPoints_HoeheTotalPrestigePunkte_ErhoehenAP()
    {
        // Vorbereitung: 2500 PP = 5 AP aus PP-Quelle (2500 / 500), 3 Legendes = 1 AP
        // → Sum = 6, Math.Max(5, 6) = 6 > 5
        var (service, _, state) = ErstelleService();
        state.Prestige.TotalPrestigePoints = 2500;
        state.Prestige.LegendeCount = 3;

        // Ausführung
        int ap = service.CalculateAscensionPoints();

        // Prüfung: Mehr als Minimum (Summe 6 > Minimum 5)
        ap.Should().BeGreaterThan(5);
    }

    [Fact]
    public void CalculateAscensionPoints_ZweiLegendesGibenEinAPBonus()
    {
        // Vorbereitung: 2500 PP → apFromPP=5, LegendeCount=2 → apFromLegende=1 → Sum=6
        // LegendeCount=4 → apFromLegende=2 → Sum=7
        // Math.Max(5, 6)=6 und Math.Max(5, 7)=7 → Delta=1 (sichtbar oberhalb des Minimums)
        var (service, _, state) = ErstelleService();
        state.Prestige.TotalPrestigePoints = 2500;
        state.Prestige.LegendeCount = 2;
        state.CollectedMasterTools.Clear();

        int apOhneBonus = service.CalculateAscensionPoints();

        state.Prestige.LegendeCount = 4; // 2 AP Bonus statt 1
        int apMitBonus = service.CalculateAscensionPoints();

        // Prüfung: Mehr Legendes = mehr AP (sichtbar weil Basis > Minimum)
        apMitBonus.Should().BeGreaterThan(apOhneBonus);
    }

    [Fact]
    public void CalculateAscensionPoints_PremiumSpieler_ErhaeeltEinExtraAP()
    {
        // Vorbereitung: 2500 PP → apFromPP=5, 3 Legendes → apFromLegende=1 → Sum=6
        // Ohne Premium: Math.Max(5, 6)=6
        // Mit Premium: Math.Max(5, 7)=7 → Delta=+1
        var (service, _, state) = ErstelleService();
        state.Prestige.TotalPrestigePoints = 2500;
        state.Prestige.LegendeCount = 3;
        state.IsPremium = false;
        int apOhnePremium = service.CalculateAscensionPoints();

        state.IsPremium = true;
        int apMitPremium = service.CalculateAscensionPoints();

        // Prüfung: +1 AP für Premium (sichtbar weil Basis > Minimum)
        apMitPremium.Should().Be(apOhnePremium + 1);
    }

    [Fact]
    public void CalculateAscensionPoints_AlleAchtWorkshopsMaxLevel_ZweiExtraAP()
    {
        // Vorbereitung: 2500 PP → apFromPP=5, 3 Legendes → apFromLegende=1 → Sum=6
        // Ohne MaxLevel: Math.Max(5, 6)=6
        // Mit 8 Workshops auf MaxLevel: apFromMaxLevel=+2 → Sum=8 → Math.Max(5,8)=8 → Delta=+2
        var (service, _, state) = ErstelleService();
        state.Prestige.TotalPrestigePoints = 2500;
        state.Prestige.LegendeCount = 3;
        state.IsPremium = false;
        int apOhneMax = service.CalculateAscensionPoints();

        // 8 Workshops auf MaxLevel setzen
        state.Workshops.Clear();
        for (int i = 0; i < 8; i++)
        {
            var ws = Workshop.Create(WorkshopType.Carpenter);
            ws.Level = Workshop.MaxLevel;
            state.Workshops.Add(ws);
        }
        int apMitMax = service.CalculateAscensionPoints();

        // Prüfung: +2 AP (sichtbar weil Basis > Minimum)
        apMitMax.Should().Be(apOhneMax + 2);
    }

    [Fact]
    public void CalculateAscensionPoints_AscensionLevelSkaliert()
    {
        // Vorbereitung: 2500 PP → apFromPP=5, 3 Legendes → apFromLegende=1 → Sum=6
        // AscensionLevel=0: Math.Max(5, 6)=6
        // AscensionLevel=3: apFromScaling=3*2=6 → Sum=12 → Math.Max(5,12)=12 → Delta=+6
        var (service, _, state) = ErstelleService();
        state.Prestige.TotalPrestigePoints = 2500;
        state.Prestige.LegendeCount = 3;
        state.Ascension.AscensionLevel = 0;
        int apLevel0 = service.CalculateAscensionPoints();

        state.Ascension.AscensionLevel = 3;
        int apLevel3 = service.CalculateAscensionPoints();

        // Prüfung: +6 AP für 3 Ascension-Levels (sichtbar weil Basis > Minimum)
        apLevel3.Should().Be(apLevel0 + 6);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DoAscension
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DoAscension_OhneVoraussetzungen_GibtFalse()
    {
        // Vorbereitung: Nicht genug Legendes
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 2;

        // Ausführung
        var ergebnis = await service.DoAscension();

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public async Task DoAscension_MitVoraussetzungen_GibtTrue()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;

        // Ausführung
        var ergebnis = await service.DoAscension();

        // Prüfung
        ergebnis.Should().BeTrue();
    }

    [Fact]
    public async Task DoAscension_ErhoehtAscensionLevel()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;
        int altesLevel = state.Ascension.AscensionLevel;

        // Ausführung
        await service.DoAscension();

        // Prüfung
        state.Ascension.AscensionLevel.Should().Be(altesLevel + 1);
    }

    [Fact]
    public async Task DoAscension_VergibtAscensionPunkte()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;

        // Ausführung
        await service.DoAscension();

        // Prüfung: AP wurden vergeben
        state.Ascension.AscensionPoints.Should().BeGreaterThan(0);
        state.Ascension.TotalAscensionPoints.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DoAscension_SetztPrestigeDatenZurueck()
    {
        // Vorbereitung: Prestige-Daten vorhanden
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;
        state.Prestige.BronzeCount = 5;
        state.Prestige.PrestigePoints = 100;
        state.Prestige.PermanentMultiplier = 3.0m;

        // Ausführung
        await service.DoAscension();

        // Prüfung: Prestige-Counter wurden zurückgesetzt (neues PrestigeData-Objekt)
        state.Prestige.BronzeCount.Should().Be(0);
        state.Prestige.PrestigePoints.Should().Be(0);
        state.Prestige.PermanentMultiplier.Should().Be(1.0m);
        state.PrestigeLevel.Should().Be(0);
        state.PrestigeMultiplier.Should().Be(1.0m);
    }

    [Fact]
    public async Task DoAscension_BewahrtGekauftePP_ShopItems()
    {
        // Vorbereitung: Prestige-Shop-Items bleiben nach Ascension (permanent)
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;
        state.Prestige.PurchasedShopItems.Add("test_item_id");

        // Ausführung
        await service.DoAscension();

        // Prüfung: Shop-Items bleiben erhalten (Regression)
        state.Prestige.PurchasedShopItems.Should().Contain("test_item_id");
    }

    [Fact]
    public async Task DoAscension_BewahrtMeilensteine()
    {
        // Vorbereitung: Meilensteine sind permanent
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;
        state.Prestige.ClaimedMilestones.Add("pm_first");

        // Ausführung
        await service.DoAscension();

        // Prüfung
        state.Prestige.ClaimedMilestones.Should().Contain("pm_first");
    }

    [Fact]
    public async Task DoAscension_SetztPlayerLevelZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;
        state.PlayerLevel = 500;

        // Ausführung
        await service.DoAscension();

        // Prüfung
        state.PlayerLevel.Should().Be(1);
    }

    [Fact]
    public async Task DoAscension_SetztGeldZurueck()
    {
        // Vorbereitung: Kein StartCapital-Perk → Basis 1000 EUR
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;
        state.Money = 1_000_000_000m;
        // Kein asc_start_capital Perk aktiv

        // Ausführung
        await service.DoAscension();

        // Prüfung: Startgeld = 1000 * GetStartCapitalMultiplier (= 1.0 ohne Perk)
        state.Money.Should().Be(1000m);
    }

    [Fact]
    public async Task DoAscension_FeuertAscensionCompletedEvent()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;
        bool eventGefeuert = false;
        service.AscensionCompleted += (_, _) => eventGefeuert = true;

        // Ausführung
        await service.DoAscension();

        // Prüfung
        eventGefeuert.Should().BeTrue();
    }

    [Fact]
    public async Task DoAscension_LoeschtMeisterwerkzeuge_OhneEternalToolsPerk()
    {
        // Vorbereitung: Ohne Eternal-Tools werden alle Werkzeuge gelöscht
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;
        state.CollectedMasterTools.Add("mt_golden_hammer");
        state.CollectedMasterTools.Add("mt_diamond_saw");
        // Kein Perk aktiv → AscensionData.Perks leer

        // Ausführung
        await service.DoAscension();

        // Prüfung
        state.CollectedMasterTools.Should().BeEmpty();
    }

    [Fact]
    public async Task DoAscension_EternalToolsLevel3_BehältAlleWerkzeuge()
    {
        // Vorbereitung: EternalTools Level 3 = alle Tools behalten
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;
        state.CollectedMasterTools.Add("mt_golden_hammer");
        state.CollectedMasterTools.Add("mt_diamond_saw");
        state.Ascension.Perks["asc_eternal_tools"] = 3;

        // Ausführung
        await service.DoAscension();

        // Prüfung: Alle Tools erhalten (Level 3 = alle behalten)
        state.CollectedMasterTools.Should().Contain("mt_golden_hammer");
        state.CollectedMasterTools.Should().Contain("mt_diamond_saw");
    }

    [Fact]
    public async Task DoAscension_EternalToolsLevel1_BehältZweiWerkzeuge()
    {
        // Vorbereitung: EternalTools Level 1 = erste 2 Tools behalten
        var (service, _, state) = ErstelleService();
        state.Prestige.LegendeCount = 3;
        state.CollectedMasterTools.Clear();
        state.CollectedMasterTools.Add("mt_tool_1");
        state.CollectedMasterTools.Add("mt_tool_2");
        state.CollectedMasterTools.Add("mt_tool_3");
        state.Ascension.Perks["asc_eternal_tools"] = 1;

        // Ausführung
        await service.DoAscension();

        // Prüfung: Erste 2 behalten (1 * 2 = 2)
        state.CollectedMasterTools.Should().HaveCount(2);
        state.CollectedMasterTools.Should().Contain("mt_tool_1");
        state.CollectedMasterTools.Should().Contain("mt_tool_2");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UpgradePerk
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void UpgradePerk_UnbekannteId_GibtFalse()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Ausführung + Prüfung
        service.UpgradePerk("nicht_existierend").Should().BeFalse();
    }

    [Fact]
    public void UpgradePerk_ZuWenigAP_GibtFalse()
    {
        // Vorbereitung: asc_start_capital Level 1 kostet 1 AP
        var (service, _, state) = ErstelleService();
        state.Ascension.AscensionPoints = 0;

        // Ausführung + Prüfung
        service.UpgradePerk("asc_start_capital").Should().BeFalse();
    }

    [Fact]
    public void UpgradePerk_GenugAP_GibtTrueUndZiehtAB()
    {
        // Vorbereitung: asc_start_capital Level 1 kostet 1 AP
        var (service, _, state) = ErstelleService();
        state.Ascension.AscensionPoints = 10;
        int apVorher = state.Ascension.AscensionPoints;

        // Ausführung
        bool ergebnis = service.UpgradePerk("asc_start_capital");

        // Prüfung
        ergebnis.Should().BeTrue();
        state.Ascension.AscensionPoints.Should().Be(apVorher - 1); // Level 1 kostet 1 AP
    }

    [Fact]
    public void UpgradePerk_ErhoehPerkLevel()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Ascension.AscensionPoints = 100;

        // Ausführung
        service.UpgradePerk("asc_start_capital");

        // Prüfung: Perk ist jetzt Level 1
        state.Ascension.GetPerkLevel("asc_start_capital").Should().Be(1);
    }

    [Fact]
    public void UpgradePerk_MaxLevel_GibtFalse()
    {
        // Vorbereitung: Perk auf MaxLevel (3 für alle Perks)
        var (service, _, state) = ErstelleService();
        state.Ascension.AscensionPoints = 9999;
        state.Ascension.Perks["asc_start_capital"] = 3; // MaxLevel

        // Ausführung + Prüfung
        service.UpgradePerk("asc_start_capital").Should().BeFalse();
    }

    [Fact]
    public void UpgradePerk_ZweiteUpgradeKostetMehr()
    {
        // Vorbereitung: asc_start_capital Level 2 kostet 3 AP
        var (service, _, state) = ErstelleService();
        state.Ascension.AscensionPoints = 9999;
        service.UpgradePerk("asc_start_capital"); // Level 1 → 2 AP bleiben noch übrig
        int apNachLevel1 = state.Ascension.AscensionPoints; // 9999 - 1 = 9998

        service.UpgradePerk("asc_start_capital"); // Level 2 kostet 3 AP
        int apNachLevel2 = state.Ascension.AscensionPoints;

        // Prüfung: Level 2 kostet mehr als Level 1
        (apNachLevel1 - apNachLevel2).Should().Be(3); // Kosten Level 2 = 3
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Perk-Abfragen
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetPerkValue_NichtGekauft_IstNull()
    {
        // Vorbereitung: Kein Perk gekauft
        var (service, _, _) = ErstelleService();

        // Prüfung
        service.GetPerkValue("asc_start_capital").Should().Be(0m);
    }

    [Fact]
    public void GetPerkValue_Level1_GibtKorrektenwert()
    {
        // Vorbereitung: asc_start_capital Level 1 = +100% Startgeld (Wert = 1.00)
        var (service, _, state) = ErstelleService();
        state.Ascension.Perks["asc_start_capital"] = 1;

        // Prüfung
        service.GetPerkValue("asc_start_capital").Should().Be(1.00m);
    }

    [Fact]
    public void GetStartCapitalMultiplier_OhnePerk_IstEins()
    {
        // Vorbereitung: Kein Perk
        var (service, _, _) = ErstelleService();

        // Prüfung
        service.GetStartCapitalMultiplier().Should().Be(1.0m);
    }

    [Fact]
    public void GetStartCapitalMultiplier_Level1_IstZwei()
    {
        // Vorbereitung: +100% → Multiplikator = 1 + 1.00 = 2.0
        var (service, _, state) = ErstelleService();
        state.Ascension.Perks["asc_start_capital"] = 1;

        // Prüfung
        service.GetStartCapitalMultiplier().Should().Be(2.0m);
    }

    [Fact]
    public void GetStartCapitalMultiplier_Level3_IstElf()
    {
        // Vorbereitung: +1000% → Multiplikator = 1 + 10.00 = 11.0
        var (service, _, state) = ErstelleService();
        state.Ascension.Perks["asc_start_capital"] = 3;

        // Prüfung
        service.GetStartCapitalMultiplier().Should().Be(11.0m);
    }

    [Fact]
    public void GetStartReputation_OhnePerk_IstFuenfzig()
    {
        // Vorbereitung: Standard-Startwert
        var (service, _, _) = ErstelleService();

        // Prüfung
        service.GetStartReputation().Should().Be(50);
    }

    [Fact]
    public void GetStartReputation_Level1_IstFuenfundsechzig()
    {
        // Vorbereitung: asc_legendary_reputation Level 1 = 65 Startreputation
        var (service, _, state) = ErstelleService();
        state.Ascension.Perks["asc_legendary_reputation"] = 1;

        // Prüfung
        service.GetStartReputation().Should().Be(65);
    }

    [Fact]
    public void GetStartReputation_Level3_IstHundert()
    {
        // Vorbereitung: asc_legendary_reputation Level 3 = 100 Startreputation (Maximum)
        var (service, _, state) = ErstelleService();
        state.Ascension.Perks["asc_legendary_reputation"] = 3;

        // Prüfung
        service.GetStartReputation().Should().Be(100);
    }

    [Fact]
    public void GetQuickStartWorkshops_OhnePerk_IstNull()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Prüfung
        service.GetQuickStartWorkshops().Should().Be(0);
    }

    [Fact]
    public void GetQuickStartWorkshops_Level1_IstZwei()
    {
        // Vorbereitung: asc_quick_start Level 1 = 2 Workshops sofort freigeschaltet
        var (service, _, state) = ErstelleService();
        state.Ascension.Perks["asc_quick_start"] = 1;

        // Prüfung
        service.GetQuickStartWorkshops().Should().Be(2);
    }

    [Fact]
    public void GetQuickStartWorkshops_Level3_IstAcht()
    {
        // Vorbereitung: asc_quick_start Level 3 = alle 8 Workshops
        var (service, _, state) = ErstelleService();
        state.Ascension.Perks["asc_quick_start"] = 3;

        // Prüfung
        service.GetQuickStartWorkshops().Should().Be(8);
    }

    [Fact]
    public void GetEternalToolsLevel_OhnePerk_IstNull()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Prüfung
        service.GetEternalToolsLevel().Should().Be(0);
    }

    [Fact]
    public void GetEternalToolsLevel_Level2_IstZwei()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.Ascension.Perks["asc_eternal_tools"] = 2;

        // Prüfung
        service.GetEternalToolsLevel().Should().Be(2);
    }

    [Fact]
    public void GetGoldenScrewBonus_Level1_IstZwanzigProzent()
    {
        // Vorbereitung: asc_golden_era Level 1 = +20% GS
        var (service, _, state) = ErstelleService();
        state.Ascension.Perks["asc_golden_era"] = 1;

        // Prüfung
        service.GetGoldenScrewBonus().Should().Be(0.20m);
    }

    [Fact]
    public void GetResearchSpeedBonus_Level1_IstFuenfzehnProzent()
    {
        // Vorbereitung: asc_timeless_research Level 1 = -15% Research-Dauer
        var (service, _, state) = ErstelleService();
        state.Ascension.Perks["asc_timeless_research"] = 1;

        // Prüfung
        service.GetResearchSpeedBonus().Should().Be(0.15m);
    }

    [Fact]
    public void GetAllPerks_GibtSechsPerks()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Prüfung: Genau 6 Ascension-Perks definiert
        service.GetAllPerks().Should().HaveCount(6);
    }

    [Fact]
    public void GetAllPerks_AlleHabenMaxLevel3()
    {
        // Vorbereitung
        var (service, _, _) = ErstelleService();

        // Prüfung: Alle Perks haben MaxLevel 3 (Regression: laut AscensionPerk.GetAll())
        service.GetAllPerks().Should().AllSatisfy(p => p.MaxLevel.Should().Be(3));
    }
}
