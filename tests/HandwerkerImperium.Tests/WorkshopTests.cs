using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für Workshop: Create(), Meilenstein-Multiplikatoren, Upgrade-Kosten,
/// Arbeiter-Slots, Bulk-Upgrade-Berechnung.
/// </summary>
public class WorkshopTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Create() - Fabrik-Methode
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Create_Schreinerei_IstFreigeschaltet()
    {
        // Ausführung
        var workshop = Workshop.Create(WorkshopType.Carpenter);

        // Prüfung: Carpenter is always unlocked (laut Code-Kommentar)
        workshop.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public void Create_Klempner_IstNichtFreigeschaltet()
    {
        // Ausführung
        var workshop = Workshop.Create(WorkshopType.Plumber);

        // Prüfung: Nur Carpenter startet freigeschaltet
        workshop.IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public void Create_StarterLevel_IstEins()
    {
        // Ausführung
        var workshop = Workshop.Create(WorkshopType.Carpenter);

        // Prüfung
        workshop.Level.Should().Be(1);
    }

    [Fact]
    public void Create_KeineArbeiterAmAnfang()
    {
        // Ausführung
        var workshop = Workshop.Create(WorkshopType.Carpenter);

        // Prüfung: Neue Werkstatt hat noch keine Arbeiter
        workshop.Workers.Should().BeEmpty();
    }

    [Fact]
    public void Create_KorrektesTyp()
    {
        // Ausführung
        var workshop = Workshop.Create(WorkshopType.Electrician);

        // Prüfung
        workshop.Type.Should().Be(WorkshopType.Electrician);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Meilenstein-Multiplikatoren
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetMilestoneMultiplier_Level1_IstEins()
    {
        // Vorbereitung
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 1;

        // Prüfung: Kein Multiplikator unter Level 25
        workshop.GetMilestoneMultiplier().Should().Be(1.0m);
    }

    [Fact]
    public void GetMilestoneMultiplier_Level25_Ist1_15()
    {
        // Vorbereitung: Level 25 = 1.15x (laut GameBalanceConstants)
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 25;

        // Prüfung
        workshop.GetMilestoneMultiplier().Should().Be(1.15m);
    }

    [Fact]
    public void GetMilestoneMultiplier_Level50_Ist1_495()
    {
        // Vorbereitung: Level 50 = 1.15 * 1.30 = 1.495x (kumulativ)
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 50;

        // Prüfung
        workshop.GetMilestoneMultiplier().Should().Be(1.495m);
    }

    [Fact]
    public void GetMilestoneMultiplier_Level100_Ist2_818()
    {
        // Vorbereitung: Level 100 = 1.15 * 1.30 * 1.30 * 1.45 = 2.818075x (kumulativ)
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 100;

        // Prüfung
        workshop.GetMilestoneMultiplier().Should().Be(2.818075m);
    }

    [Fact]
    public void GetMilestoneMultiplierForLevel_Level1000_IstDreifach()
    {
        // Vorbereitung: Level 1000 Einzel-Multiplikator = 3.0x (laut GameBalanceConstants)
        // Prüfung
        Workshop.GetMilestoneMultiplierForLevel(1000).Should().Be(3.00m);
    }

    [Fact]
    public void IsMilestoneLevel_BekannterMeilenstein_IstTrue()
    {
        // Prüfung: Alle bekannten Meilenstein-Level
        Workshop.IsMilestoneLevel(25).Should().BeTrue();
        Workshop.IsMilestoneLevel(50).Should().BeTrue();
        Workshop.IsMilestoneLevel(100).Should().BeTrue();
        Workshop.IsMilestoneLevel(250).Should().BeTrue();
        Workshop.IsMilestoneLevel(500).Should().BeTrue();
        Workshop.IsMilestoneLevel(1000).Should().BeTrue();
    }

    [Fact]
    public void IsMilestoneLevel_BeliebigeLevelZahlen_SindFalse()
    {
        // Prüfung: Keine Meilenstein-Level
        Workshop.IsMilestoneLevel(1).Should().BeFalse();
        Workshop.IsMilestoneLevel(10).Should().BeFalse();
        Workshop.IsMilestoneLevel(99).Should().BeFalse();
        Workshop.IsMilestoneLevel(101).Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Upgrade-Kosten
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void UpgradeCost_Level1_IstHundert()
    {
        // Vorbereitung: Level 1 kostet immer 100 (Sonderfall laut Code)
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 1;

        // Prüfung
        workshop.UpgradeCost.Should().Be(100m);
    }

    [Fact]
    public void UpgradeCost_Level2_IstGrösserAlsLevel1()
    {
        // Vorbereitung: Kosten steigen exponentiell
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 1;
        var kostenLevel1 = workshop.UpgradeCost;

        workshop.Level = 2;
        var kostenLevel2 = workshop.UpgradeCost;

        // Prüfung
        kostenLevel2.Should().BeGreaterThan(kostenLevel1);
    }

    [Fact]
    public void UpgradeCost_MaxLevel_IstNull()
    {
        // Vorbereitung: Workshop auf Max-Level kann nicht weiter aufgewertet werden
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = Workshop.MaxLevel;

        // Prüfung
        workshop.UpgradeCost.Should().Be(0m);
        workshop.CanUpgrade.Should().BeFalse();
    }

    [Fact]
    public void GetBulkUpgradeCost_NullUpgrades_IstNull()
    {
        // Vorbereitung
        var workshop = Workshop.Create(WorkshopType.Carpenter);

        // Prüfung
        workshop.GetBulkUpgradeCost(0).Should().Be(0m);
    }

    [Fact]
    public void GetBulkUpgradeCost_EinUpgrade_GleicheKostenWieUpgradeCost()
    {
        // Vorbereitung
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 5;

        // Prüfung: Einzeln = BulkCost(1)
        workshop.GetBulkUpgradeCost(1).Should().BeApproximately(workshop.UpgradeCost, 0.01m);
    }

    [Fact]
    public void GetMaxAffordableUpgrades_NullBudget_GibtNullUpgrades()
    {
        // Vorbereitung
        var workshop = Workshop.Create(WorkshopType.Carpenter);

        // Prüfung
        var (count, cost) = workshop.GetMaxAffordableUpgrades(0m);
        count.Should().Be(0);
        cost.Should().Be(0m);
    }

    [Fact]
    public void GetMaxAffordableUpgrades_GroßesBudget_GibtMehredeUpgrades()
    {
        // Vorbereitung: Level 1 kostet 100 → 10.000 Budget reicht für viele Upgrades
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 1;

        // Ausführung
        var (count, _) = workshop.GetMaxAffordableUpgrades(10_000m);

        // Prüfung
        count.Should().BeGreaterThan(1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Arbeiter-Slots
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void BaseMaxWorkers_Level1_IstEins()
    {
        // Vorbereitung
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 1;

        // Prüfung: 1 + (1-1)/50 = 1
        workshop.BaseMaxWorkers.Should().Be(1);
    }

    [Fact]
    public void BaseMaxWorkers_Level51_IstZwei()
    {
        // Vorbereitung: 1 + (51-1)/50 = 1 + 1 = 2
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 51;

        // Prüfung
        workshop.BaseMaxWorkers.Should().Be(2);
    }

    [Fact]
    public void BaseMaxWorkers_Level1000_IstTwanzig()
    {
        // Vorbereitung: Bei Level 1000 = max. 20 Arbeiter-Slots
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 1000;

        // Prüfung
        workshop.BaseMaxWorkers.Should().Be(20);
    }

    [Fact]
    public void CanHireWorker_LeereWerkstatt_IstTrue()
    {
        // Vorbereitung: Keine Arbeiter, Level 1 → 1 Slot verfügbar
        var workshop = Workshop.Create(WorkshopType.Carpenter);

        // Prüfung
        workshop.CanHireWorker.Should().BeTrue();
    }

    [Fact]
    public void CanHireWorker_GefüllterSlot_IstFalse()
    {
        // Vorbereitung: Level 1 = max. 1 Arbeiter, 1 Arbeiter vorhanden
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Workers.Add(Worker.CreateRandom());

        // Prüfung
        workshop.CanHireWorker.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Level-Fit-Faktor
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetWorkerLevelFitFactor_UnterLevel30_IstEins()
    {
        // Vorbereitung: Kein Malus unter Level 30
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 20;
        var worker = Worker.CreateRandom();

        // Prüfung
        workshop.GetWorkerLevelFitFactor(worker).Should().Be(1.0m);
    }

    [Fact]
    public void GetWorkerLevelFitFactor_NieUnterZwanzigProzent()
    {
        // Vorbereitung: Selbst bei sehr hohem Level bleibt Minimum 20%
        var workshop = Workshop.Create(WorkshopType.Carpenter);
        workshop.Level = 1000;
        var worker = Worker.CreateForTier(WorkerTier.F); // Niedrigster Tier

        // Prüfung: Minimum 0.20m
        workshop.GetWorkerLevelFitFactor(worker).Should().BeGreaterThanOrEqualTo(0.20m);
    }
}
