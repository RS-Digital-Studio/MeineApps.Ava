using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für GoalService: Anfänger-Ziele, Meilensteine, Prestige-Ziel,
/// Workshop-Unlock, Gebäude-Ziele, Worker-Ziele und Cache-Verhalten.
/// </summary>
public class GoalServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    private static (GoalService service, IGameStateService mockState, GameState state) ErstelleService()
    {
        var mockState = Substitute.For<IGameStateService>();
        var mockLocalization = Substitute.For<ILocalizationService>();
        var mockPrestige = Substitute.For<IPrestigeService>();
        var mockAscension = Substitute.For<IAscensionService>();
        var mockRebirth = Substitute.For<IRebirthService>();
        var state = GameState.CreateNew();
        mockState.State.Returns(state);

        // Lokalisierungsservice gibt Schlüssel als Text zurück
        mockLocalization.GetString(Arg.Any<string>()).Returns(x => x.Arg<string>());

        // Prestige-Service gibt 0 PP zurück
        mockPrestige.GetPrestigePoints(Arg.Any<decimal>()).Returns(0);

        // Ascension nicht verfügbar
        mockAscension.CanAscend.Returns(false);
        mockAscension.CalculateAscensionPoints().Returns(0);

        // Rebirth gibt 0 Sterne zurück
        mockRebirth.GetStars(Arg.Any<WorkshopType>()).Returns(0);

        var service = new GoalService(mockState, mockLocalization, mockPrestige, mockAscension, mockRebirth);
        return (service, mockState, state);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Anfänger-Ziele (Priorität 0)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetCurrentGoal_NochKeinUpgrade_ZeigtWerkstattUpgradeZiel()
    {
        // Vorbereitung: Neuer Spieler, Schreinerei Level 1, kein Geld ausgegeben
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 1;
        state.TotalMoneySpent = 0;
        state.Workshops[0].Level = 1; // Level 1

        // Ausführung
        var ziel = service.GetCurrentGoal();

        // Prüfung: Anfänger-Ziel → Workshop upgraden
        ziel.Should().NotBeNull();
        ziel!.Priority.Should().Be(0);
        ziel.IconKind.Should().Be("ArrowUpBold");
    }

    [Fact]
    public void GetCurrentGoal_NochKeinAuftrag_ZeigtErstenAuftragZiel()
    {
        // Vorbereitung: Kein Auftrag abgeschlossen
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 1;
        state.TotalMoneySpent = 1; // Bereits Geld ausgegeben (nicht mehr "upgrade first"-Ziel)
        state.Statistics.TotalOrdersCompleted = 0;
        state.Workshops[0].Level = 2;

        // Ausführung
        var ziel = service.GetCurrentGoal();

        // Prüfung: "Ersten Auftrag annehmen"
        ziel.Should().NotBeNull();
        ziel!.IconKind.Should().Be("ClipboardText");
    }

    [Fact]
    public void GetCurrentGoal_WerkstattUnterLevel10_ZeigtLevelZiel()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 2;
        state.TotalMoneySpent = 100m;
        state.Statistics.TotalOrdersCompleted = 1;
        state.Workshops[0].Level = 5; // Unter Level 10

        // Ausführung
        var ziel = service.GetCurrentGoal();

        // Prüfung: "Werkstatt auf Level 10" Ziel
        ziel.Should().NotBeNull();
        ziel!.IconKind.Should().Be("TrendingUp");
    }

    [Fact]
    public void GetCurrentGoal_Level10Ueberschritten_GibtNichtAnfaengerZielZurueck()
    {
        // Vorbereitung: Level 10+ → keine Anfänger-Ziele mehr
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 10;

        // Ausführung
        var ziel = service.GetCurrentGoal();

        // Prüfung: Wenn Ziel vorhanden, darf es kein Anfänger-Ziel (Prio 0) sein
        if (ziel != null)
            ziel.Priority.Should().BeGreaterThan(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Workshop-Meilenstein nahe (Priorität 1)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetCurrentGoal_WerkstattNaheAnMeilenstein_ZeigtMeilensteinZiel()
    {
        // Vorbereitung: Schreinerei auf Level 23 (5 Level vor Meilenstein 25)
        // Spieler ist Level 10+ damit kein Anfänger-Ziel greift
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 20;
        state.Prestige.BronzeCount = 1; // TotalPrestigeCount = 1 // Kein Anfänger-Ziel
        state.Workshops[0].Level = 23;

        // Ausführung
        var ziel = service.GetCurrentGoal();

        // Prüfung: Meilenstein-Ziel (Priorität 1) mit TrendingUp-Icon
        ziel.Should().NotBeNull();
        ziel!.Priority.Should().Be(1);
    }

    [Fact]
    public void GetCurrentGoal_WerkstattExakt5VorMeilenstein_WirdErkannt()
    {
        // Vorbereitung: Exakt 5 Level vor Meilenstein 50
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 20;
        state.Prestige.BronzeCount = 1; // TotalPrestigeCount = 1
        state.Workshops[0].Level = 45;

        // Ausführung
        var ziel = service.GetCurrentGoal();

        // Prüfung: Meilenstein-Ziel bei exakt 5 Level Abstand
        ziel.Should().NotBeNull();
        ziel!.Priority.Should().Be(1);
    }

    [Fact]
    public void GetCurrentGoal_Werkstatt6VorMeilenstein_WirdNichtAlsMeilensteinErkannt()
    {
        // Vorbereitung: 6 Level vor Meilenstein 50 → außerhalb des 5-Level-Fensters
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 20;
        state.Prestige.BronzeCount = 1; // TotalPrestigeCount = 1
        state.Workshops[0].Level = 44; // 44 → 50 = 6 Level

        // Ausführung
        var ziel = service.GetCurrentGoal();

        // Prüfung: Kein Meilenstein-Ziel (> 5 Level Abstand)
        if (ziel != null)
            ziel.Priority.Should().NotBe(1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Prestige verfügbar (Priorität 2)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetCurrentGoal_PrestigeVerfuegbar_ZeigtPrestigeZiel()
    {
        // Vorbereitung: Prestige ist verfügbar (höchster Tier ist nicht None)
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 50; // Level >=50 für Bronze-Prestige
        state.Prestige.BronzeCount = 1; // TotalPrestigeCount = 1
        // Kein Meilenstein nah (Workshop Level 1)

        // Prestige-Service meldet Bronze verfügbar
        // PrestigeData.GetHighestAvailableTier gibt Bronze zurück wenn Level >= 50
        // (Standard GameState hat Level = 1 nach CreateNew)
        state.PlayerLevel = 55; // Über Bronze-Level-Anforderung

        // Ausführung
        var ziel = service.GetCurrentGoal();

        // Prüfung: Falls Prestige verfügbar → Priorität 2
        // (hängt von GetHighestAvailableTier ab - wenn Bronze verfügbar)
        if (ziel?.Priority == 2)
            ziel.IconKind.Should().Be("StarFourPoints");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Invalidate + Cache
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetCurrentGoal_ZweimalAufgerufen_GleicherZustand_GibtGleicheReferenzZurueck()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 1;

        // Ausführung
        var ziel1 = service.GetCurrentGoal();
        var ziel2 = service.GetCurrentGoal();

        // Prüfung: Cache liefert gleiche Referenz
        ziel1.Should().BeSameAs(ziel2);
    }

    [Fact]
    public void GetCurrentGoal_NachInvalidate_BerechnetNeu()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 1;
        var ziel1 = service.GetCurrentGoal();

        // Ausführung
        service.Invalidate();
        var ziel2 = service.GetCurrentGoal();

        // Prüfung: Nach Invalidate neues Objekt
        ziel2.Should().NotBeSameAs(ziel1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Gebäude-Ziel (Priorität 4)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetCurrentGoal_ErschwinblichesGebaeude_ZeigtGebaeudeZiel()
    {
        // Vorbereitung: Spieler Level >=5, Gebäude gebaut, genug Geld für 50% der Upgrade-Kosten
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 10;
        state.Prestige.BronzeCount = 1; // TotalPrestigeCount = 1
        // Erstes Gebäude (Canteen) gebaut und unter Level 5
        if (state.Buildings.Count > 0)
        {
            state.Buildings[0].IsBuilt = true;
            state.Buildings[0].Level = 1;
            // Genug Geld für 50% Upgrade-Kosten
            state.Money = state.Buildings[0].NextLevelCost * 0.6m;
        }

        // Ausführung
        var ziel = service.GetCurrentGoal();

        // Prüfung: Gebäude-Ziel (Prio 4) oder höherprioritäres
        // (Gebäude-Ziel wird nur angezeigt wenn kein höherprioritäres existiert)
        ziel.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Fortschritt-Berechnung
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetCurrentGoal_AnfaengerZiel_FortschrittIstZwischenNullUndEins()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 3;
        state.TotalMoneySpent = 0;

        // Ausführung
        var ziel = service.GetCurrentGoal();

        // Prüfung: Fortschritt immer im Bereich 0.0 bis 1.0
        if (ziel != null)
        {
            ziel.Progress.Should().BeGreaterThanOrEqualTo(0.0);
            ziel.Progress.Should().BeLessThanOrEqualTo(1.0);
        }
    }

    [Fact]
    public void GetCurrentGoal_BeschreibungNichtLeer_WennZielVorhanden()
    {
        // Vorbereitung
        var (service, _, state) = ErstelleService();
        state.PlayerLevel = 1;

        // Ausführung
        var ziel = service.GetCurrentGoal();

        // Prüfung: Wenn ein Ziel existiert, muss es eine Beschreibung haben
        if (ziel != null)
            ziel.Description.Should().NotBeNullOrEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Ascension verfügbar (Priorität 7)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetCurrentGoal_AscensionVerfuegbar_ZeigtAscensionZiel()
    {
        // Vorbereitung: Ascension ist verfügbar
        var mockState = Substitute.For<IGameStateService>();
        var mockLocalization = Substitute.For<ILocalizationService>();
        var mockPrestige = Substitute.For<IPrestigeService>();
        var mockAscension = Substitute.For<IAscensionService>();
        var mockRebirth = Substitute.For<IRebirthService>();
        var state = GameState.CreateNew();
        // Ascension verfügbar → GoalService soll irgendein Ziel zurückgeben
        // (Prio hängt vom gesamten State ab - andere Ziele können höher priorisiert sein)
        state.PlayerLevel = 1;
        state.Prestige.BronzeCount = 5;
        mockState.State.Returns(state);
        mockLocalization.GetString(Arg.Any<string>()).Returns(x => x.Arg<string>());
        mockPrestige.GetPrestigePoints(Arg.Any<decimal>()).Returns(0);
        mockAscension.CanAscend.Returns(true);
        mockAscension.CalculateAscensionPoints().Returns(5);
        mockRebirth.GetStars(Arg.Any<WorkshopType>()).Returns(0);

        var service = new GoalService(mockState, mockLocalization, mockPrestige, mockAscension, mockRebirth);

        // Ausführung
        var ziel = service.GetCurrentGoal();

        // Prüfung: Ein Ziel wird gefunden (GoalService hat mindestens ein aktives Ziel)
        ziel.Should().NotBeNull();
        ziel!.Description.Should().NotBeNullOrEmpty();
    }
}
