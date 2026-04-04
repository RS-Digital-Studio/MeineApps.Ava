using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für Building: NextLevelCost, CanUpgrade, MoodRecoveryPerHour,
/// RestTimeReduction, MaterialCostReduction, ExtraOrderSlots,
/// DailyReputationGain, TrainingSpeedMultiplier, OrderRewardBonus, ExtraWorkerSlots.
/// </summary>
public class BuildingTests
{
    // ═══════════════════════════════════════════════════════════════════
    // NextLevelCost
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void NextLevelCost_NichtGebaut_IstBasispreis()
    {
        // Vorbereitung
        var building = Building.Create(BuildingType.Canteen);

        // Prüfung: Noch nicht gebaut → BaseCost
        building.NextLevelCost.Should().Be(BuildingType.Canteen.GetBaseCost());
    }

    [Fact]
    public void NextLevelCost_Level1_IstBasispreisMalZwei()
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.Canteen, Level = 1, IsBuilt = true };

        // Prüfung: BaseCost * 2^1 = BaseCost * 2
        building.NextLevelCost.Should().Be(BuildingType.Canteen.GetBaseCost() * 2m);
    }

    [Fact]
    public void NextLevelCost_MaxLevel_IstNull()
    {
        // Vorbereitung
        int maxLevel = BuildingType.Canteen.GetMaxLevel();
        var building = new Building { Type = BuildingType.Canteen, Level = maxLevel, IsBuilt = true };

        // Prüfung: Kein Upgrade mehr möglich
        building.NextLevelCost.Should().Be(0m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CanUpgrade
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CanUpgrade_NichtGebaut_IstFalse()
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.Canteen, IsBuilt = false };

        // Prüfung
        building.CanUpgrade.Should().BeFalse();
    }

    [Fact]
    public void CanUpgrade_GebautUndNichtMaxLevel_IstTrue()
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.Canteen, Level = 1, IsBuilt = true };

        // Prüfung
        building.CanUpgrade.Should().BeTrue();
    }

    [Fact]
    public void CanUpgrade_MaxLevel_IstFalse()
    {
        // Vorbereitung
        var building = new Building
        {
            Type = BuildingType.Canteen,
            Level = BuildingType.Canteen.GetMaxLevel(),
            IsBuilt = true
        };

        // Prüfung
        building.CanUpgrade.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // MoodRecoveryPerHour (Kantine)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void MoodRecoveryPerHour_KantineLevel1_IstEinsProzent()
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.Canteen, Level = 1 };

        // Prüfung: Level * 1% = 1%
        building.MoodRecoveryPerHour.Should().Be(1m);
    }

    [Fact]
    public void MoodRecoveryPerHour_KantineLevel5_IstFünfProzent()
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.Canteen, Level = 5 };

        // Prüfung
        building.MoodRecoveryPerHour.Should().Be(5m);
    }

    [Fact]
    public void MoodRecoveryPerHour_AnderesBuildingType_IstNull()
    {
        // Vorbereitung: Kantine-Bonus gilt nur für Kantine
        var building = new Building { Type = BuildingType.Storage, Level = 5 };

        // Prüfung
        building.MoodRecoveryPerHour.Should().Be(0m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // RestTimeReduction (Kantine)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1, 0.50)]
    [InlineData(3, 0.60)]
    [InlineData(5, 0.80)]
    public void RestTimeReduction_KantineVerschiedeneLevel_GibtKorrekteWerte(int level, decimal erwartet)
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.Canteen, Level = level };

        // Prüfung
        building.RestTimeReduction.Should().Be(erwartet);
    }

    [Fact]
    public void RestTimeReduction_NichtKantine_IstNull()
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.Office, Level = 5 };

        // Prüfung
        building.RestTimeReduction.Should().Be(0m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ExtraOrderSlots (Büro)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    [InlineData(5, 6)]
    public void ExtraOrderSlots_BüroVerschiedeneLevel_GibtKorrekteWerte(int level, int erwartet)
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.Office, Level = level };

        // Prüfung: Level + 1 Slots
        building.ExtraOrderSlots.Should().Be(erwartet);
    }

    [Fact]
    public void ExtraOrderSlots_NichtBüro_IstNull()
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.Canteen, Level = 5 };

        // Prüfung
        building.ExtraOrderSlots.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // DailyReputationGain (Showroom)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1, 0.5)]
    [InlineData(2, 1.0)]
    [InlineData(5, 2.5)]
    public void DailyReputationGain_ShowroomLevel_GibtKorrekteWerte(int level, decimal erwartet)
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.Showroom, Level = level };

        // Prüfung: Level * 0.5
        building.DailyReputationGain.Should().Be(erwartet);
    }

    // ═══════════════════════════════════════════════════════════════════
    // TrainingSpeedMultiplier (Trainingszentrum)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1, 2.0)]   // 1.0 + 1*0.5 + 0.5 = 2.0
    [InlineData(3, 3.0)]   // 1.0 + 3*0.5 + 0.5 = 3.0
    [InlineData(5, 4.0)]   // 1.0 + 5*0.5 + 0.5 = 4.0
    public void TrainingSpeedMultiplier_TrainingszentraleLevel_GibtKorrekteWerte(int level, decimal erwartet)
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.TrainingCenter, Level = level };

        // Prüfung
        building.TrainingSpeedMultiplier.Should().Be(erwartet);
    }

    [Fact]
    public void TrainingSpeedMultiplier_NichtTrainingszentrum_IstEins()
    {
        // Vorbereitung: Default-Multiplikator = 1x (kein Bonus)
        var building = new Building { Type = BuildingType.Canteen, Level = 5 };

        // Prüfung
        building.TrainingSpeedMultiplier.Should().Be(1.0m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // OrderRewardBonus (Fahrzeugflotte)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1, 0.20)]
    [InlineData(3, 0.40)]
    [InlineData(5, 0.60)]
    public void OrderRewardBonus_FahrzeugflotteLevel_GibtKorrekteWerte(int level, decimal erwartet)
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.VehicleFleet, Level = level };

        // Prüfung
        building.OrderRewardBonus.Should().Be(erwartet);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ExtraWorkerSlots (Workshop-Erweiterung)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1, 2)]
    [InlineData(5, 6)]
    public void ExtraWorkerSlots_WorkshopErweiterungLevel_GibtKorrekteWerte(int level, int erwartet)
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.WorkshopExtension, Level = level };

        // Prüfung: Level + 1 Worker-Slots
        building.ExtraWorkerSlots.Should().Be(erwartet);
    }

    [Fact]
    public void ExtraWorkerSlots_NichtWorkshopErweiterung_IstNull()
    {
        // Vorbereitung
        var building = new Building { Type = BuildingType.Office, Level = 5 };

        // Prüfung
        building.ExtraWorkerSlots.Should().Be(0);
    }
}
