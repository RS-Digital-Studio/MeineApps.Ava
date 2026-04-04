using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für BuildingService: Bauen, Upgraden, Gebäude-Boni-Abfragen.
/// </summary>
public class BuildingServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Erstellt einen Mock-GameStateService mit konfiguriertem GameState.</summary>
    private static (IGameStateService mock, GameState state) ErstelleMock(int playerLevel = 99)
    {
        var mock = Substitute.For<IGameStateService>();
        var state = new GameState { PlayerLevel = playerLevel };
        mock.State.Returns(state);
        return (mock, state);
    }

    // ═══════════════════════════════════════════════════════════════════
    // TryBuildBuilding - Erfolgreich bauen
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TryBuildBuilding_AusreichendGeldUndLevel_GibtTrueZurueck()
    {
        // Vorbereitung
        var (mock, state) = ErstelleMock(playerLevel: 99);
        mock.CanAfford(Arg.Any<decimal>()).Returns(true);
        mock.TrySpendMoney(Arg.Any<decimal>()).Returns(true);
        var sut = new BuildingService(mock);

        // Ausführung
        bool result = sut.TryBuildBuilding(BuildingType.Canteen);

        // Prüfung
        result.Should().BeTrue();
    }

    [Fact]
    public void TryBuildBuilding_AusreichendGeld_FuegtGebaeudeMitLevel1Hinzu()
    {
        // Vorbereitung
        var (mock, state) = ErstelleMock(playerLevel: 99);
        mock.CanAfford(Arg.Any<decimal>()).Returns(true);
        mock.TrySpendMoney(Arg.Any<decimal>()).Returns(true);
        var sut = new BuildingService(mock);

        // Ausführung
        sut.TryBuildBuilding(BuildingType.Canteen);

        // Prüfung: Gebäude muss mit Level 1 und IsBuilt=true vorhanden sein
        state.Buildings.Should().Contain(b => b.Type == BuildingType.Canteen && b.IsBuilt && b.Level == 1);
    }

    [Fact]
    public void TryBuildBuilding_ZuWenigGeld_GibtFalseZurueck()
    {
        // Vorbereitung
        var (mock, state) = ErstelleMock(playerLevel: 99);
        mock.CanAfford(Arg.Any<decimal>()).Returns(false);
        var sut = new BuildingService(mock);

        // Ausführung
        bool result = sut.TryBuildBuilding(BuildingType.Canteen);

        // Prüfung
        result.Should().BeFalse();
    }

    [Fact]
    public void TryBuildBuilding_BereitsGebaut_GibtFalseZurueck()
    {
        // Vorbereitung
        var (mock, state) = ErstelleMock(playerLevel: 99);
        state.Buildings.Add(new Building { Type = BuildingType.Canteen, IsBuilt = true, Level = 1 });
        mock.CanAfford(Arg.Any<decimal>()).Returns(true);
        var sut = new BuildingService(mock);

        // Ausführung
        bool result = sut.TryBuildBuilding(BuildingType.Canteen);

        // Prüfung: Doppeltes Bauen verhindert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryBuildBuilding_SpielerLevelZuNiedrig_GibtFalseZurueck()
    {
        // Vorbereitung: Level 1 Spieler kann kein Gebäude mit hohem Unlock-Level bauen
        var (mock, state) = ErstelleMock(playerLevel: 1);
        mock.CanAfford(Arg.Any<decimal>()).Returns(true);
        var sut = new BuildingService(mock);

        // Ausführung: VehicleFleet hat hohes Unlock-Level
        bool result = sut.TryBuildBuilding(BuildingType.VehicleFleet);

        // Prüfung
        result.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // TryUpgradeBuilding
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TryUpgradeBuilding_GebaudesGebaeude_ErhoehtLevel()
    {
        // Vorbereitung
        var (mock, state) = ErstelleMock();
        state.Buildings.Add(new Building { Type = BuildingType.Canteen, IsBuilt = true, Level = 1 });
        mock.CanAfford(Arg.Any<decimal>()).Returns(true);
        mock.TrySpendMoney(Arg.Any<decimal>()).Returns(true);
        var sut = new BuildingService(mock);

        // Ausführung
        bool result = sut.TryUpgradeBuilding(BuildingType.Canteen);

        // Prüfung
        result.Should().BeTrue();
        state.Buildings[0].Level.Should().Be(2);
    }

    [Fact]
    public void TryUpgradeBuilding_NichtGebaut_GibtFalseZurueck()
    {
        // Vorbereitung: Kein Gebäude vorhanden
        var (mock, state) = ErstelleMock();
        var sut = new BuildingService(mock);

        // Ausführung
        bool result = sut.TryUpgradeBuilding(BuildingType.Canteen);

        // Prüfung
        result.Should().BeFalse();
    }

    [Fact]
    public void TryUpgradeBuilding_MaxLevel_GibtFalseZurueck()
    {
        // Vorbereitung: Gebäude auf MaxLevel (5)
        var (mock, state) = ErstelleMock();
        state.Buildings.Add(new Building { Type = BuildingType.Canteen, IsBuilt = true, Level = 5 });
        mock.CanAfford(Arg.Any<decimal>()).Returns(true);
        var sut = new BuildingService(mock);

        // Ausführung
        bool result = sut.TryUpgradeBuilding(BuildingType.Canteen);

        // Prüfung
        result.Should().BeFalse();
    }

    [Fact]
    public void TryUpgradeBuilding_NichtGenugGeld_GibtFalseZurueck()
    {
        // Vorbereitung
        var (mock, state) = ErstelleMock();
        state.Buildings.Add(new Building { Type = BuildingType.Canteen, IsBuilt = true, Level = 1 });
        mock.CanAfford(Arg.Any<decimal>()).Returns(false);
        var sut = new BuildingService(mock);

        // Ausführung
        bool result = sut.TryUpgradeBuilding(BuildingType.Canteen);

        // Prüfung
        result.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Boni-Abfragen ohne Gebäude
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetMoodRecoveryBonus_KantineNichtGebaut_GibtNullZurueck()
    {
        // Vorbereitung
        var (mock, _) = ErstelleMock();
        var sut = new BuildingService(mock);

        // Ausführung & Prüfung
        sut.GetMoodRecoveryBonus().Should().Be(0m);
    }

    [Fact]
    public void GetExtraOrderSlots_OfficeNichtGebaut_GibtNullZurueck()
    {
        // Vorbereitung
        var (mock, _) = ErstelleMock();
        var sut = new BuildingService(mock);

        // Ausführung & Prüfung
        sut.GetExtraOrderSlots().Should().Be(0);
    }

    [Fact]
    public void GetTrainingSpeedMultiplier_TrainingCenterNichtGebaut_GibtEinsZurueck()
    {
        // Vorbereitung
        var (mock, _) = ErstelleMock();
        var sut = new BuildingService(mock);

        // Ausführung & Prüfung: Kein Gebäude → Multiplikator 1.0
        sut.GetTrainingSpeedMultiplier().Should().Be(1.0m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Boni mit vorhandenem Gebäude
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetMoodRecoveryBonus_KantineLevel2_GibtKorrekteBonusZurueck()
    {
        // Vorbereitung
        var (mock, state) = ErstelleMock();
        state.Buildings.Add(new Building { Type = BuildingType.Canteen, IsBuilt = true, Level = 2 });
        var sut = new BuildingService(mock);

        // Prüfung: Level 2 = 2.0% laut Building.cs
        sut.GetMoodRecoveryBonus().Should().Be(2.0m);
    }

    [Fact]
    public void GetExtraWorkerSlots_WorkshopExtensionGebaut_GibtKorrekteSlots()
    {
        // Vorbereitung
        var (mock, state) = ErstelleMock();
        state.Buildings.Add(new Building { Type = BuildingType.WorkshopExtension, IsBuilt = true, Level = 1 });
        var sut = new BuildingService(mock);

        // Ausführung & Prüfung
        sut.GetExtraWorkerSlots().Should().BeGreaterThan(0);
    }
}
