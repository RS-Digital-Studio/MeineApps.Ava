using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für Manager: GetBonus(), IsMaxLevel, UpgradeCost,
/// GetAllDefinitions(), GetDefinitionById().
/// </summary>
public class ManagerTests
{
    // ═══════════════════════════════════════════════════════════════════
    // IsMaxLevel, UpgradeCost
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsMaxLevel_Level5_IstTrue()
    {
        // Vorbereitung
        var manager = new Manager { Level = 5 };

        // Prüfung
        manager.IsMaxLevel.Should().BeTrue();
    }

    [Fact]
    public void IsMaxLevel_Level4_IstFalse()
    {
        // Vorbereitung
        var manager = new Manager { Level = 4 };

        // Prüfung
        manager.IsMaxLevel.Should().BeFalse();
    }

    [Fact]
    public void UpgradeCost_Level1_IstZehnGoldschrauben()
    {
        // Vorbereitung
        var manager = new Manager { Level = 1 };

        // Prüfung: Level * 10 = 1 * 10 = 10
        manager.UpgradeCost.Should().Be(10);
    }

    [Fact]
    public void UpgradeCost_Level5_IstFünfzigGoldschrauben()
    {
        // Vorbereitung
        var manager = new Manager { Level = 5 };

        // Prüfung: 5 * 10 = 50
        manager.UpgradeCost.Should().Be(50);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetBonus()
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetBonus_NichtFreigeschaltet_GibtNull()
    {
        // Vorbereitung
        var manager = new Manager { Id = "mgr_hans", Level = 3, IsUnlocked = false };

        // Prüfung: Nicht freigeschaltete Manager geben keinen Bonus
        manager.GetBonus(ManagerAbility.EfficiencyBoost).Should().Be(0m);
    }

    [Fact]
    public void GetBonus_FalscheAbility_GibtNull()
    {
        // Vorbereitung: mgr_hans hat EfficiencyBoost, aber wir fragen nach IncomeBoost
        var manager = new Manager { Id = "mgr_hans", Level = 3, IsUnlocked = true };

        // Prüfung
        manager.GetBonus(ManagerAbility.IncomeBoost).Should().Be(0m);
    }

    [Fact]
    public void GetBonus_EfficiencyBoost_BerechnetLevel5Korrekt()
    {
        // Vorbereitung: mgr_hans hat EfficiencyBoost (+5% pro Level)
        var manager = new Manager { Id = "mgr_hans", Level = 5, IsUnlocked = true };

        // Prüfung: 0.05 * 5 = 0.25 (25%)
        manager.GetBonus(ManagerAbility.EfficiencyBoost).Should().Be(0.25m);
    }

    [Fact]
    public void GetBonus_IncomeBoost_BerechnetKorrekt()
    {
        // Vorbereitung: mgr_kurt hat IncomeBoost (+5% pro Level)
        var manager = new Manager { Id = "mgr_kurt", Level = 3, IsUnlocked = true };

        // Prüfung: 0.05 * 3 = 0.15 (15%)
        manager.GetBonus(ManagerAbility.IncomeBoost).Should().Be(0.15m);
    }

    [Fact]
    public void GetBonus_TrainingSpeedUp_BerechnetKorrekt()
    {
        // Vorbereitung: mgr_schmidt hat TrainingSpeedUp (+10% pro Level)
        var manager = new Manager { Id = "mgr_schmidt", Level = 2, IsUnlocked = true };

        // Prüfung: 0.10 * 2 = 0.20 (20%)
        manager.GetBonus(ManagerAbility.TrainingSpeedUp).Should().Be(0.20m);
    }

    [Fact]
    public void GetBonus_MoodBoost_BerechnetKorrekt()
    {
        // Vorbereitung: mgr_lisa hat MoodBoost (+4% pro Level)
        var manager = new Manager { Id = "mgr_lisa", Level = 4, IsUnlocked = true };

        // Prüfung: 0.04 * 4 = 0.16
        manager.GetBonus(ManagerAbility.MoodBoost).Should().Be(0.16m);
    }

    [Fact]
    public void GetBonus_AutoCollectOrders_GibtLevelZurück()
    {
        // Vorbereitung: mgr_weber hat AutoCollectOrders (Anzahl pro Check = Level)
        var manager = new Manager { Id = "mgr_weber", Level = 3, IsUnlocked = true };

        // Prüfung: Level direkt zurückgegeben
        manager.GetBonus(ManagerAbility.AutoCollectOrders).Should().Be(3m);
    }

    [Fact]
    public void GetBonus_FatigueReduction_BerechnetKorrekt()
    {
        // Vorbereitung: mgr_fritz hat FatigueReduction (-3% pro Level)
        var manager = new Manager { Id = "mgr_fritz", Level = 5, IsUnlocked = true };

        // Prüfung: 0.03 * 5 = 0.15
        manager.GetBonus(ManagerAbility.FatigueReduction).Should().Be(0.15m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Statische Definitionen
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAllDefinitions_HatVierzehnManager()
    {
        // Ausführung
        var defs = Manager.GetAllDefinitions();

        // Prüfung: Laut CLAUDE.md 14 Manager-Definitionen
        defs.Should().HaveCount(14);
    }

    [Fact]
    public void GetDefinitionById_GültigeId_GibtDefinitionZurück()
    {
        // Ausführung
        var def = Manager.GetDefinitionById("mgr_hans");

        // Prüfung
        def.Should().NotBeNull();
        def!.Id.Should().Be("mgr_hans");
        def.Ability.Should().Be(ManagerAbility.EfficiencyBoost);
    }

    [Fact]
    public void GetDefinitionById_UngültigeId_GibtNullZurück()
    {
        // Ausführung
        var def = Manager.GetDefinitionById("mgr_nichtvorhanden");

        // Prüfung
        def.Should().BeNull();
    }

    [Fact]
    public void GetDefinitionById_GlobaleManager_HabenKeineWorkshopBeschränkung()
    {
        // Prüfung: mgr_schmidt, mgr_weber, mgr_mueller, mgr_kaiser haben Workshop=null
        var globalManagers = new[] { "mgr_schmidt", "mgr_weber", "mgr_mueller", "mgr_kaiser" };
        foreach (var id in globalManagers)
        {
            var def = Manager.GetDefinitionById(id);
            def.Should().NotBeNull($"Manager {id} sollte existieren");
            def!.Workshop.Should().BeNull($"Manager {id} sollte kein Workshop-Lock haben");
        }
    }
}
