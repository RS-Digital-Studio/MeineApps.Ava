using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für GameState: CreateNew()-Standardwerte, XP-Berechnung, Offline-Stunden,
/// Boost-Flags und Build-Cache-Invalidierung.
/// </summary>
public class GameStateTests
{
    // ═══════════════════════════════════════════════════════════════════
    // CreateNew() - Standardwerte
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CreateNew_StarterGeld_IstTausendEuro()
    {
        // Ausführung
        var state = GameState.CreateNew();

        // Prüfung: Startgeld laut CLAUDE.md ist 1.000 EUR
        state.Money.Should().Be(1000m);
    }

    [Fact]
    public void CreateNew_StarterWorkshop_IstSchreinerei()
    {
        // Ausführung
        var state = GameState.CreateNew();

        // Prüfung: Erste Werkstatt muss Schreinerei sein
        state.Workshops.Should().HaveCount(1);
        state.Workshops[0].Type.Should().Be(WorkshopType.Carpenter);
    }

    [Fact]
    public void CreateNew_SchreinereiIsFreigeschaltet()
    {
        // Ausführung
        var state = GameState.CreateNew();

        // Prüfung
        state.Workshops[0].IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public void CreateNew_StarterArbeiterAnzahl_IstZwei()
    {
        // Ausführung
        var state = GameState.CreateNew();

        // Prüfung: Laut CLAUDE.md mit 2 Arbeitern für schnelleren Einstieg
        state.Workshops[0].Workers.Should().HaveCount(2);
    }

    [Fact]
    public void CreateNew_SchreinereiFreigeschaltetInUnlockedTypes()
    {
        // Ausführung
        var state = GameState.CreateNew();

        // Prüfung: Carpenter muss in der Freischalt-Liste sein
        state.UnlockedWorkshopTypes.Should().Contain(WorkshopType.Carpenter);
    }

    [Fact]
    public void CreateNew_StarterLevel_IstEins()
    {
        // Ausführung
        var state = GameState.CreateNew();

        // Prüfung
        state.PlayerLevel.Should().Be(1);
    }

    [Fact]
    public void CreateNew_StarterGoldschrauben_SindNull()
    {
        // Ausführung
        var state = GameState.CreateNew();

        // Prüfung
        state.GoldenScrews.Should().Be(0);
    }

    [Fact]
    public void CreateNew_Research_IstInitialisiert()
    {
        // Ausführung
        var state = GameState.CreateNew();

        // Prüfung: Research Tree muss befüllt sein
        state.Researches.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateNew_Tools_SindInitialisiert()
    {
        // Ausführung
        var state = GameState.CreateNew();

        // Prüfung
        state.Tools.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateNew_StarterWorkshopLevel_IstEins()
    {
        // Ausführung
        var state = GameState.CreateNew();

        // Prüfung: Workshop startet auf Level 1
        state.Workshops[0].Level.Should().Be(1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // XP-Berechnung
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateXpForLevel_Level1_GibtNull()
    {
        // Prüfung: Level 1 braucht 0 XP (Startlevel)
        GameState.CalculateXpForLevel(1).Should().Be(0);
    }

    [Fact]
    public void CalculateXpForLevel_Level2_GibtHundert()
    {
        // Formel: 100 * (2-1)^1.2 = 100
        GameState.CalculateXpForLevel(2).Should().Be(100);
    }

    [Fact]
    public void CalculateXpForLevel_HoeheresLevel_SteigendeMenge()
    {
        // Prüfung: XP-Anforderungen steigen monoton
        var xpLevel5 = GameState.CalculateXpForLevel(5);
        var xpLevel10 = GameState.CalculateXpForLevel(10);
        var xpLevel20 = GameState.CalculateXpForLevel(20);

        xpLevel5.Should().BeLessThan(xpLevel10);
        xpLevel10.Should().BeLessThan(xpLevel20);
    }

    [Fact]
    public void CurrentXp_SetzeDirekt_WirdGespeichert()
    {
        // AddXp wurde nach GameStateService verschoben - hier nur noch Property-Tests
        var state = GameState.CreateNew();

        // Ausführung: Direkte Zuweisung (AddXp-Logik liegt jetzt im Service)
        state.CurrentXp = 100;
        state.TotalXp = 100;

        // Prüfung
        state.CurrentXp.Should().Be(100);
        state.TotalXp.Should().Be(100);
    }

    [Fact]
    public void PlayerLevel_SetzeDirekt_WirdGespeichert()
    {
        // AddXp mit Level-Up liegt jetzt im GameStateService
        var state = GameState.CreateNew();
        state.PlayerLevel.Should().Be(1);

        // Ausführung: Direkte Zuweisung
        state.PlayerLevel = 5;

        // Prüfung
        state.PlayerLevel.Should().Be(5);
    }

    // ═══════════════════════════════════════════════════════════════════
    // LevelProgress
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void LevelProgress_OhneXP_IstNull()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.CurrentXp = 0;

        // Prüfung
        state.LevelProgress.Should().Be(0.0);
    }

    [Fact]
    public void LevelProgress_IstZwischenNullUndEins()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.CurrentXp = 50; // Weniger als Level 2 benötigt

        // Prüfung
        state.LevelProgress.Should().BeInRange(0.0, 1.0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Offline-Stunden
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void BaseOfflineHours_IstImmerVier()
    {
        // Ausführung
        var state = GameState.CreateNew();

        // Prüfung: Basiswert immer 4h laut CLAUDE.md
        state.BaseOfflineHours.Should().Be(4);
    }

    [Fact]
    public void MaxOfflineHours_OhnePremiumOhneVideo_IstVier()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.IsPremium = false;
        state.OfflineVideoExtended = false;

        // Prüfung
        state.MaxOfflineHours.Should().Be(4);
    }

    [Fact]
    public void MaxOfflineHours_MitVideoExtended_IstAcht()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.IsPremium = false;
        state.OfflineVideoExtended = true;

        // Prüfung
        state.MaxOfflineHours.Should().Be(8);
    }

    [Fact]
    public void MaxOfflineHours_MitPremium_IstSechzehn()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.IsPremium = true;

        // Prüfung: Premium = 16h Offline laut CLAUDE.md
        state.MaxOfflineHours.Should().Be(16);
    }

    // ═══════════════════════════════════════════════════════════════════
    // IsFreeRushAvailable - Zeitmanipulations-Schutz
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsFreeRushAvailable_NochNieGenutzt_IstVerfuegbar()
    {
        // Vorbereitung: LastFreeRushUsed = DateTime.MinValue (Standard)
        var state = GameState.CreateNew();

        // Prüfung
        state.IsFreeRushAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsFreeRushAvailable_HeuteGenutzt_IstNichtVerfuegbar()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.LastFreeRushUsed = DateTime.UtcNow.Date; // Heute

        // Prüfung
        state.IsFreeRushAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsFreeRushAvailable_GesternGenutzt_IstVerfuegbar()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        state.LastFreeRushUsed = DateTime.UtcNow.Date.AddDays(-1); // Gestern

        // Prüfung
        state.IsFreeRushAvailable.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetOrCreateWorkshop
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetOrCreateWorkshop_NeuerTyp_FuegtWorkshopHinzu()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        var anfangsAnzahl = state.Workshops.Count;

        // Ausführung: Neuen Workshop-Typ hinzufügen
        state.GetOrCreateWorkshop(WorkshopType.Plumber);

        // Prüfung
        state.Workshops.Should().HaveCount(anfangsAnzahl + 1);
    }

    [Fact]
    public void GetOrCreateWorkshop_BestehendeSchreinerei_GibtVorhandenenZurueck()
    {
        // Vorbereitung
        var state = GameState.CreateNew();
        var original = state.Workshops[0]; // Schreinerei

        // Ausführung
        var abgerufen = state.GetOrCreateWorkshop(WorkshopType.Carpenter);

        // Prüfung: Dieselbe Instanz, kein Duplikat
        abgerufen.Should().BeSameAs(original);
        state.Workshops.Should().HaveCount(1);
    }
}
