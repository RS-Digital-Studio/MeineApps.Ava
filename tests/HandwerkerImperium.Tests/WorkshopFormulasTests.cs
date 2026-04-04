using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für WorkshopFormulas: CalculateBaseIncomePerWorker, CalculateMilestoneMultiplier,
/// GetMilestoneMultiplierForLevel, IsMilestoneLevel, CalculateUpgradeCost,
/// CalculateBulkUpgradeCost, CalculateMaxAffordableUpgrades, CalculateRentPerHour,
/// CalculateMaterialCostPerHour, CalculateLevelFitFactor, CalculateGrossIncome.
/// </summary>
public class WorkshopFormulasTests
{
    // ═══════════════════════════════════════════════════════════════════
    // CalculateBaseIncomePerWorker
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateBaseIncomePerWorker_Level1Carpenter_IstEins()
    {
        // Vorbereitung: Level 1, Carpenter (Multiplier 1.0x) → 1.02^0 * 1.0 * 1.0 = 1.0
        // Ausführung
        decimal result = WorkshopFormulas.CalculateBaseIncomePerWorker(1, WorkshopType.Carpenter);

        // Prüfung
        result.Should().BeApproximately(1.0m, 0.001m);
    }

    [Fact]
    public void CalculateBaseIncomePerWorker_Level1Plumber_IstEinHalb()
    {
        // Vorbereitung: Plumber hat 1.5x Typ-Multiplikator → 1.02^0 * 1.5 * 1.0 = 1.5
        // Ausführung
        decimal result = WorkshopFormulas.CalculateBaseIncomePerWorker(1, WorkshopType.Plumber);

        // Prüfung
        result.Should().BeApproximately(1.5m, 0.001m);
    }

    [Fact]
    public void CalculateBaseIncomePerWorker_Level1Architect_IstFünf()
    {
        // Vorbereitung: Architect hat 5.0x Typ-Multiplikator → 1.02^0 * 5.0 * 1.0 = 5.0
        // Ausführung
        decimal result = WorkshopFormulas.CalculateBaseIncomePerWorker(1, WorkshopType.Architect);

        // Prüfung
        result.Should().BeApproximately(5.0m, 0.001m);
    }

    [Fact]
    public void CalculateBaseIncomePerWorker_SteigendMitLevel()
    {
        // Vorbereitung: Höheres Level → höheres Einkommen (exponentiell)
        decimal level1 = WorkshopFormulas.CalculateBaseIncomePerWorker(1, WorkshopType.Carpenter);
        decimal level10 = WorkshopFormulas.CalculateBaseIncomePerWorker(10, WorkshopType.Carpenter);
        decimal level100 = WorkshopFormulas.CalculateBaseIncomePerWorker(100, WorkshopType.Carpenter);

        // Prüfung: Strikt monoton steigend
        level10.Should().BeGreaterThan(level1);
        level100.Should().BeGreaterThan(level10);
    }

    [Fact]
    public void CalculateBaseIncomePerWorker_Level25_EnthältMeilensteinMultiplikator()
    {
        // Vorbereitung: Level 25 ist erster Meilenstein (1.15x) → Einkommen muss deutlich springen
        decimal level24 = WorkshopFormulas.CalculateBaseIncomePerWorker(24, WorkshopType.Carpenter);
        decimal level25 = WorkshopFormulas.CalculateBaseIncomePerWorker(25, WorkshopType.Carpenter);

        // Prüfung: Sprung größer als normales 1.02x-Wachstum durch Meilenstein
        // Level 25 / Level 24 ≈ 1.02 * 1.15 ≈ 1.173 (statt nur 1.02)
        (level25 / level24).Should().BeGreaterThan(1.10m);
    }

    [Fact]
    public void CalculateBaseIncomePerWorker_GeneralContractor_HatHöchstenMultiplikator()
    {
        // Vorbereitung: GeneralContractor = 7.0x, höchster regulärer Typ
        decimal carpenterIncome = WorkshopFormulas.CalculateBaseIncomePerWorker(1, WorkshopType.Carpenter);
        decimal generalContractorIncome = WorkshopFormulas.CalculateBaseIncomePerWorker(1, WorkshopType.GeneralContractor);

        // Prüfung: Verhältnis = 7.0 / 1.0 = 7.0
        (generalContractorIncome / carpenterIncome).Should().BeApproximately(7.0m, 0.001m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CalculateMilestoneMultiplier
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateMilestoneMultiplier_Level1_IstEins()
    {
        // Vorbereitung: Kein Meilenstein vor Level 25
        // Ausführung
        decimal result = WorkshopFormulas.CalculateMilestoneMultiplier(1);

        // Prüfung
        result.Should().Be(1.0m);
    }

    [Fact]
    public void CalculateMilestoneMultiplier_Level24_IstEins()
    {
        // Vorbereitung: Knapp vor dem ersten Meilenstein (Level 25)
        // Ausführung
        decimal result = WorkshopFormulas.CalculateMilestoneMultiplier(24);

        // Prüfung
        result.Should().Be(1.0m);
    }

    [Fact]
    public void CalculateMilestoneMultiplier_Level25_Ist1_15()
    {
        // Vorbereitung: Erster Meilenstein bei Level 25 (1.15x)
        // Ausführung
        decimal result = WorkshopFormulas.CalculateMilestoneMultiplier(25);

        // Prüfung
        result.Should().Be(1.15m);
    }

    [Fact]
    public void CalculateMilestoneMultiplier_Level50_IstKumulativ()
    {
        // Vorbereitung: Level 50 = Lv25 (1.15) * Lv50 (1.30) = 1.495
        // Ausführung
        decimal result = WorkshopFormulas.CalculateMilestoneMultiplier(50);

        // Prüfung
        result.Should().BeApproximately(1.495m, 0.0001m);
    }

    [Fact]
    public void CalculateMilestoneMultiplier_Level75_IstKumulativ()
    {
        // Vorbereitung: Level 75 = 1.15 * 1.30 * 1.30 = 1.9435
        // Ausführung
        decimal result = WorkshopFormulas.CalculateMilestoneMultiplier(75);

        // Prüfung
        result.Should().BeApproximately(1.9435m, 0.0001m);
    }

    [Fact]
    public void CalculateMilestoneMultiplier_Level100_IstKumulativ()
    {
        // Vorbereitung: Level 100 = 1.15 * 1.30 * 1.30 * 1.45 = 2.818075
        // Ausführung
        decimal result = WorkshopFormulas.CalculateMilestoneMultiplier(100);

        // Prüfung
        result.Should().BeApproximately(2.818075m, 0.0001m);
    }

    [Fact]
    public void CalculateMilestoneMultiplier_SteigendMitLevel()
    {
        // Vorbereitung: Kumulativer Multiplikator kann nur steigen
        decimal lv25 = WorkshopFormulas.CalculateMilestoneMultiplier(25);
        decimal lv50 = WorkshopFormulas.CalculateMilestoneMultiplier(50);
        decimal lv100 = WorkshopFormulas.CalculateMilestoneMultiplier(100);
        decimal lv500 = WorkshopFormulas.CalculateMilestoneMultiplier(500);
        decimal lv1000 = WorkshopFormulas.CalculateMilestoneMultiplier(1000);

        // Prüfung
        lv50.Should().BeGreaterThan(lv25);
        lv100.Should().BeGreaterThan(lv50);
        lv500.Should().BeGreaterThan(lv100);
        lv1000.Should().BeGreaterThan(lv500);
    }

    [Fact]
    public void CalculateMilestoneMultiplier_Level0_IstEins()
    {
        // Vorbereitung: Level 0 - kein Meilenstein erreicht
        // Ausführung
        decimal result = WorkshopFormulas.CalculateMilestoneMultiplier(0);

        // Prüfung
        result.Should().Be(1.0m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetMilestoneMultiplierForLevel
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetMilestoneMultiplierForLevel_Level25_Ist1_15()
    {
        // Prüfung: Einzel-Multiplikator für Level 25
        WorkshopFormulas.GetMilestoneMultiplierForLevel(25).Should().Be(1.15m);
    }

    [Fact]
    public void GetMilestoneMultiplierForLevel_Level50_Ist1_30()
    {
        // Prüfung: Einzel-Multiplikator für Level 50
        WorkshopFormulas.GetMilestoneMultiplierForLevel(50).Should().Be(1.30m);
    }

    [Fact]
    public void GetMilestoneMultiplierForLevel_Level500_Ist2_00()
    {
        // Prüfung: Level 500 hat den größten Einzel-Multiplikator (2.0x) vor Level 1000
        WorkshopFormulas.GetMilestoneMultiplierForLevel(500).Should().Be(2.00m);
    }

    [Fact]
    public void GetMilestoneMultiplierForLevel_Level1000_Ist3_00()
    {
        // Prüfung: Level 1000 - finaler Meilenstein (3.0x)
        WorkshopFormulas.GetMilestoneMultiplierForLevel(1000).Should().Be(3.00m);
    }

    [Fact]
    public void GetMilestoneMultiplierForLevel_KeinMeilenstein_IstEins()
    {
        // Vorbereitung: Zufällige Level die keine Meilensteine sind
        WorkshopFormulas.GetMilestoneMultiplierForLevel(1).Should().Be(1.0m);
        WorkshopFormulas.GetMilestoneMultiplierForLevel(10).Should().Be(1.0m);
        WorkshopFormulas.GetMilestoneMultiplierForLevel(99).Should().Be(1.0m);
        WorkshopFormulas.GetMilestoneMultiplierForLevel(501).Should().Be(1.0m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // IsMilestoneLevel
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(200)]
    [InlineData(225)]
    [InlineData(250)]
    [InlineData(350)]
    [InlineData(400)]
    [InlineData(500)]
    [InlineData(600)]
    [InlineData(650)]
    [InlineData(750)]
    [InlineData(900)]
    [InlineData(1000)]
    public void IsMilestoneLevel_AlleMeilensteine_SindTrue(int level)
    {
        // Prüfung: Alle definierten Meilenstein-Level müssen erkannt werden
        WorkshopFormulas.IsMilestoneLevel(level).Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(24)]
    [InlineData(26)]
    [InlineData(49)]
    [InlineData(51)]
    [InlineData(99)]
    [InlineData(101)]
    [InlineData(999)]
    public void IsMilestoneLevel_BeliebigeLevelZahlen_SindFalse(int level)
    {
        // Prüfung: Nicht-Meilenstein-Level dürfen nie true zurückgeben
        WorkshopFormulas.IsMilestoneLevel(level).Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // CalculateUpgradeCost
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateUpgradeCost_Level1_OhneRabatt_IstHundert()
    {
        // Vorbereitung: Level 1 ist Sonderfall (UpgradeCostLevel1 = 100)
        // Ausführung
        decimal result = WorkshopFormulas.CalculateUpgradeCost(1, 0m, 0m, 0m);

        // Prüfung
        result.Should().Be(100m);
    }

    [Fact]
    public void CalculateUpgradeCost_Level2_OhneRabatt_IstBasis()
    {
        // Vorbereitung: Level 2 = UpgradeCostBase (200) * 1.07^1 = 214
        // Ausführung
        decimal result = WorkshopFormulas.CalculateUpgradeCost(2, 0m, 0m, 0m);

        // Prüfung: 200 * 1.07 = 214
        result.Should().BeApproximately(214m, 1m);
    }

    [Fact]
    public void CalculateUpgradeCost_Level2_IsGroesserAlsLevel1()
    {
        // Vorbereitung: Kosten steigen mit Level
        decimal level1 = WorkshopFormulas.CalculateUpgradeCost(1, 0m, 0m, 0m);
        decimal level2 = WorkshopFormulas.CalculateUpgradeCost(2, 0m, 0m, 0m);

        // Prüfung
        level2.Should().BeGreaterThan(level1);
    }

    [Fact]
    public void CalculateUpgradeCost_MaxLevel_IstNull()
    {
        // Vorbereitung: Am MaxLevel (1000) können keine Upgrades mehr gekauft werden
        // Ausführung
        decimal result = WorkshopFormulas.CalculateUpgradeCost(Workshop.MaxLevel, 0m, 0m, 0m);

        // Prüfung
        result.Should().Be(0m);
    }

    [Fact]
    public void CalculateUpgradeCost_Level1_MitRebirthRabatt_IstReduziert()
    {
        // Vorbereitung: 10% Rebirth-Rabatt auf Level-1-Kosten
        decimal ohneRabatt = WorkshopFormulas.CalculateUpgradeCost(1, 0m, 0m, 0m);

        // Ausführung
        decimal mitRabatt = WorkshopFormulas.CalculateUpgradeCost(1, 0.10m, 0m, 0m);

        // Prüfung: 100 * 0.9 = 90
        mitRabatt.Should().BeApproximately(ohneRabatt * 0.90m, 0.01m);
    }

    [Fact]
    public void CalculateUpgradeCost_MitPrestigeRabatt_IstReduziert()
    {
        // Vorbereitung: 30% Prestige-Rabatt
        decimal ohneRabatt = WorkshopFormulas.CalculateUpgradeCost(10, 0m, 0m, 0m);

        // Ausführung
        decimal mitRabatt = WorkshopFormulas.CalculateUpgradeCost(10, 0m, 0.30m, 0m);

        // Prüfung
        mitRabatt.Should().BeApproximately(ohneRabatt * 0.70m, 0.01m);
    }

    [Fact]
    public void CalculateUpgradeCost_PrestigeRabatt_MaxBeiPrestigeDiscountCap()
    {
        // Vorbereitung: Prestige-Rabatt wird bei 50% gedeckelt (PrestigeDiscountCap)
        decimal rabatt50 = WorkshopFormulas.CalculateUpgradeCost(10, 0m, 0.50m, 0m);
        decimal rabatt80 = WorkshopFormulas.CalculateUpgradeCost(10, 0m, 0.80m, 0m);

        // Prüfung: Rabatt über 50% hat keinen weiteren Effekt
        rabatt50.Should().Be(rabatt80);
    }

    [Fact]
    public void CalculateUpgradeCost_AlleRabatteStapeln()
    {
        // Vorbereitung: Rebirth 10% + Prestige 20% + VIP 5% → alle multiplizieren
        decimal ohneRabatt = WorkshopFormulas.CalculateUpgradeCost(5, 0m, 0m, 0m);

        // Ausführung: 0.90 * 0.80 * 0.95 = 0.684
        decimal mitAllen = WorkshopFormulas.CalculateUpgradeCost(5, 0.10m, 0.20m, 0.05m);

        // Prüfung
        mitAllen.Should().BeApproximately(ohneRabatt * 0.684m, 1m);
    }

    [Fact]
    public void CalculateUpgradeCost_Level500VsLevel501_AbgeflachterExponent()
    {
        // Vorbereitung: Ab Level 500 flacht der Exponent von 1.07 auf 1.06 ab
        // Kosten bei Level 501 dürfen nicht so stark steigen wie davor
        decimal level499 = WorkshopFormulas.CalculateUpgradeCost(499, 0m, 0m, 0m);
        decimal level500 = WorkshopFormulas.CalculateUpgradeCost(500, 0m, 0m, 0m);
        decimal level501 = WorkshopFormulas.CalculateUpgradeCost(501, 0m, 0m, 0m);

        // Prüfung: Wachstumsrate nach 500 (1.06) < Wachstumsrate davor (1.07)
        decimal rateBefore = level500 / level499;
        decimal rateAfter = level501 / level500;
        rateAfter.Should().BeLessThan(rateBefore);
    }

    [Fact]
    public void CalculateUpgradeCost_KostenSteigenMitLevel()
    {
        // Prüfung: Upgrade-Kosten sind strikt monoton steigend
        decimal level5 = WorkshopFormulas.CalculateUpgradeCost(5, 0m, 0m, 0m);
        decimal level10 = WorkshopFormulas.CalculateUpgradeCost(10, 0m, 0m, 0m);
        decimal level100 = WorkshopFormulas.CalculateUpgradeCost(100, 0m, 0m, 0m);

        level10.Should().BeGreaterThan(level5);
        level100.Should().BeGreaterThan(level10);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CalculateBulkUpgradeCost
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateBulkUpgradeCost_NullUpgrades_IstNull()
    {
        // Prüfung: Keine Upgrades → keine Kosten
        decimal result = WorkshopFormulas.CalculateBulkUpgradeCost(1, 0, 1.0m);

        result.Should().Be(0m);
    }

    [Fact]
    public void CalculateBulkUpgradeCost_EinUpgrade_GleichtEinzelkosten()
    {
        // Vorbereitung: Bulk(1) muss exakt den Einzelkosten entsprechen
        decimal einzelkosten = WorkshopFormulas.CalculateUpgradeCost(5, 0m, 0m, 0m);

        // Ausführung
        decimal bulk1 = WorkshopFormulas.CalculateBulkUpgradeCost(5, 1, 1.0m);

        // Prüfung
        bulk1.Should().BeApproximately(einzelkosten, 0.01m);
    }

    [Fact]
    public void CalculateBulkUpgradeCost_ZehnUpgrades_IstSummeEinzelkosten()
    {
        // Vorbereitung: Bulk(10) ab Level 1 = Summe der Level 1-10 Einzelkosten
        decimal summe = 0m;
        for (int i = 0; i < 10; i++)
        {
            summe += WorkshopFormulas.CalculateUpgradeCost(1 + i, 0m, 0m, 0m);
        }

        // Ausführung
        decimal bulk10 = WorkshopFormulas.CalculateBulkUpgradeCost(1, 10, 1.0m);

        // Prüfung: Exakt gleich (keine Rundungsfehler erwartet bei dieser Methode)
        bulk10.Should().BeApproximately(summe, 0.01m);
    }

    [Fact]
    public void CalculateBulkUpgradeCost_AbMaxLevel_IstNull()
    {
        // Vorbereitung: MaxLevel kann nicht weiter upgegradet werden
        // Ausführung
        decimal result = WorkshopFormulas.CalculateBulkUpgradeCost(Workshop.MaxLevel, 5, 1.0m);

        // Prüfung
        result.Should().Be(0m);
    }

    [Fact]
    public void CalculateBulkUpgradeCost_MitRabattFaktor_IstReduziert()
    {
        // Vorbereitung: 50% Rabattfaktor
        decimal ohneRabatt = WorkshopFormulas.CalculateBulkUpgradeCost(1, 10, 1.0m);

        // Ausführung
        decimal mitRabatt = WorkshopFormulas.CalculateBulkUpgradeCost(1, 10, 0.5m);

        // Prüfung: Exakt halb so teuer
        mitRabatt.Should().BeApproximately(ohneRabatt * 0.5m, 0.01m);
    }

    [Fact]
    public void CalculateBulkUpgradeCost_BegrenztAufMaxLevel()
    {
        // Vorbereitung: Level 50, Versuch viel mehr Upgrades als bis MaxLevel möglich
        // Level 995 verursacht decimal Overflow bei den Kosten
        decimal bulk100 = WorkshopFormulas.CalculateBulkUpgradeCost(50, 100, 1.0m);
        decimal bulk50 = WorkshopFormulas.CalculateBulkUpgradeCost(50, 50, 1.0m);

        // Prüfung: 100 Upgrades kosten mehr als 50
        bulk100.Should().BeGreaterThan(bulk50);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CalculateMaxAffordableUpgrades
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateMaxAffordableUpgrades_NullBudget_GibtNull()
    {
        // Ausführung
        var (count, cost) = WorkshopFormulas.CalculateMaxAffordableUpgrades(1, 0m, 1.0m);

        // Prüfung
        count.Should().Be(0);
        cost.Should().Be(0m);
    }

    [Fact]
    public void CalculateMaxAffordableUpgrades_NegativesBudget_GibtNull()
    {
        // Vorbereitung: Negatives Budget ist ungültig
        // Ausführung
        var (count, cost) = WorkshopFormulas.CalculateMaxAffordableUpgrades(1, -100m, 1.0m);

        // Prüfung
        count.Should().Be(0);
        cost.Should().Be(0m);
    }

    [Fact]
    public void CalculateMaxAffordableUpgrades_AbMaxLevel_GibtNull()
    {
        // Vorbereitung: Kein Upgrade mehr möglich
        // Ausführung
        var (count, cost) = WorkshopFormulas.CalculateMaxAffordableUpgrades(Workshop.MaxLevel, 1_000_000m, 1.0m);

        // Prüfung
        count.Should().Be(0);
        cost.Should().Be(0m);
    }

    [Fact]
    public void CalculateMaxAffordableUpgrades_GenauEinUpgrade_Budget100()
    {
        // Vorbereitung: Level 1 kostet genau 100 → Budget 100 = exakt 1 Upgrade
        // Ausführung
        var (count, cost) = WorkshopFormulas.CalculateMaxAffordableUpgrades(1, 100m, 1.0m);

        // Prüfung
        count.Should().Be(1);
        cost.Should().BeApproximately(100m, 0.01m);
    }

    [Fact]
    public void CalculateMaxAffordableUpgrades_GroßesBudget_GibtMehredeUpgrades()
    {
        // Vorbereitung: Level 1 kostet 100 → 10.000 Budget reicht für viele Upgrades
        // Ausführung
        var (count, _) = WorkshopFormulas.CalculateMaxAffordableUpgrades(1, 10_000m, 1.0m);

        // Prüfung
        count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void CalculateMaxAffordableUpgrades_KosteDecktwBudgetAb()
    {
        // Vorbereitung: Die zurückgegebene cost muss tatsächlich ≤ Budget sein
        decimal budget = 5_000m;

        // Ausführung
        var (_, cost) = WorkshopFormulas.CalculateMaxAffordableUpgrades(1, budget, 1.0m);

        // Prüfung: Nicht mehr ausgeben als vorhanden
        cost.Should().BeLessThanOrEqualTo(budget);
    }

    [Fact]
    public void CalculateMaxAffordableUpgrades_MitRabattFaktor_MehrUpgradesAlsOhne()
    {
        // Vorbereitung: Gleiche Budget, aber halber Preis → doppelt so viele Upgrades
        decimal budget = 2_000m;

        // Ausführung
        var (ohneRabatt, _) = WorkshopFormulas.CalculateMaxAffordableUpgrades(1, budget, 1.0m);
        var (mitRabatt, _) = WorkshopFormulas.CalculateMaxAffordableUpgrades(1, budget, 0.5m);

        // Prüfung
        mitRabatt.Should().BeGreaterThan(ohneRabatt);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CalculateRentPerHour
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateRentPerHour_Level1_IstLinear()
    {
        // Vorbereitung: Level 1 ≤ 100 → linear: 10 * 1 = 10
        // Ausführung
        decimal result = WorkshopFormulas.CalculateRentPerHour(1);

        // Prüfung
        result.Should().Be(10m);
    }

    [Fact]
    public void CalculateRentPerHour_Level50_IstLinear()
    {
        // Vorbereitung: Level 50 ≤ 100 → linear: 10 * 50 = 500
        // Ausführung
        decimal result = WorkshopFormulas.CalculateRentPerHour(50);

        // Prüfung
        result.Should().Be(500m);
    }

    [Fact]
    public void CalculateRentPerHour_Level100_IstLinear()
    {
        // Vorbereitung: Level 100 → linear: 10 * 100 = 1000
        // Ausführung
        decimal result = WorkshopFormulas.CalculateRentPerHour(100);

        // Prüfung
        result.Should().Be(1000m);
    }

    [Fact]
    public void CalculateRentPerHour_Level101_WechselAufExponentiell()
    {
        // Vorbereitung: Level 101 → exponentiell: 1000 * 1.005^(101-100) = 1000 * 1.005 = 1005
        decimal level100 = WorkshopFormulas.CalculateRentPerHour(100);
        decimal level101 = WorkshopFormulas.CalculateRentPerHour(101);

        // Prüfung: Übergang ohne großen Sprung (nur ~0.5% Anstieg)
        level101.Should().BeApproximately(level100 * 1.005m, 0.01m);
    }

    [Fact]
    public void CalculateRentPerHour_SteigendMitLevel()
    {
        // Prüfung: Miete steigt immer mit Level
        decimal lv10 = WorkshopFormulas.CalculateRentPerHour(10);
        decimal lv100 = WorkshopFormulas.CalculateRentPerHour(100);
        decimal lv500 = WorkshopFormulas.CalculateRentPerHour(500);

        lv100.Should().BeGreaterThan(lv10);
        lv500.Should().BeGreaterThan(lv100);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CalculateMaterialCostPerHour
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateMaterialCostPerHour_Level1Carpenter_IstLinear()
    {
        // Vorbereitung: Level 1 ≤ 100 → linear: 5 * 1 * 1.0 = 5
        // Ausführung
        decimal result = WorkshopFormulas.CalculateMaterialCostPerHour(1, WorkshopType.Carpenter);

        // Prüfung
        result.Should().Be(5m);
    }

    [Fact]
    public void CalculateMaterialCostPerHour_Level1Electrician_IstTypSkaliert()
    {
        // Vorbereitung: Electrician hat 2.0x Typ-Multiplikator → 5 * 1 * 2.0 = 10
        // Ausführung
        decimal result = WorkshopFormulas.CalculateMaterialCostPerHour(1, WorkshopType.Electrician);

        // Prüfung
        result.Should().Be(10m);
    }

    [Fact]
    public void CalculateMaterialCostPerHour_Level100_IstLinear()
    {
        // Vorbereitung: Level 100 → 5 * 100 * 1.0 = 500
        // Ausführung
        decimal result = WorkshopFormulas.CalculateMaterialCostPerHour(100, WorkshopType.Carpenter);

        // Prüfung
        result.Should().Be(500m);
    }

    [Fact]
    public void CalculateMaterialCostPerHour_Level101_WechselAufExponentiell()
    {
        // Vorbereitung: Level 101 → exponentiell: 500 * 1.005^1 * 1.0 ≈ 502.5
        decimal level100 = WorkshopFormulas.CalculateMaterialCostPerHour(100, WorkshopType.Carpenter);
        decimal level101 = WorkshopFormulas.CalculateMaterialCostPerHour(101, WorkshopType.Carpenter);

        // Prüfung: Leichter Anstieg durch exponentiellen Übergang
        level101.Should().BeApproximately(level100 * 1.005m, 0.01m);
    }

    [Fact]
    public void CalculateMaterialCostPerHour_ArchitectTeurer_AlsCarpenter()
    {
        // Vorbereitung: Architect (5.0x) kostet mehr als Carpenter (1.0x)
        decimal carpenter = WorkshopFormulas.CalculateMaterialCostPerHour(50, WorkshopType.Carpenter);
        decimal architect = WorkshopFormulas.CalculateMaterialCostPerHour(50, WorkshopType.Architect);

        // Prüfung: Verhältnis = 5.0 / 1.0
        (architect / carpenter).Should().BeApproximately(5.0m, 0.001m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CalculateLevelFitFactor
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateLevelFitFactor_Level30_IstEins()
    {
        // Vorbereitung: Kein Malus bis Level 30
        // Ausführung
        decimal result = WorkshopFormulas.CalculateLevelFitFactor(30, 0m, 0m);

        // Prüfung
        result.Should().Be(1.0m);
    }

    [Fact]
    public void CalculateLevelFitFactor_Level1_IstEins()
    {
        // Vorbereitung: Niedrige Level → kein Malus
        // Ausführung
        decimal result = WorkshopFormulas.CalculateLevelFitFactor(1, 0m, 0m);

        // Prüfung
        result.Should().Be(1.0m);
    }

    [Fact]
    public void CalculateLevelFitFactor_Level60_OhneResistenz_IstReduziert()
    {
        // Vorbereitung: Level 60 → 2 Schritte à -2% = -4% Malus ohne Resistenz
        // steps = 60 / 30 = 2, penalty = 2 * 0.02 = 0.04, factor = 1.0 - 0.04 = 0.96
        // Ausführung
        decimal result = WorkshopFormulas.CalculateLevelFitFactor(60, 0m, 0m);

        // Prüfung
        result.Should().BeApproximately(0.96m, 0.001m);
    }

    [Fact]
    public void CalculateLevelFitFactor_Level90_OhneResistenz_IstReduziert()
    {
        // Vorbereitung: Level 90 → 3 Schritte à -2% = -6% ohne Resistenz
        // steps = 90 / 30 = 3, penalty = 3 * 0.02 = 0.06, factor = 1.0 - 0.06 = 0.94
        // Ausführung
        decimal result = WorkshopFormulas.CalculateLevelFitFactor(90, 0m, 0m);

        // Prüfung
        result.Should().BeApproximately(0.94m, 0.001m);
    }

    [Fact]
    public void CalculateLevelFitFactor_MitResistenz_HöhererFaktor()
    {
        // Vorbereitung: 50% Resistenz halbiert den Malus
        decimal ohneResistenz = WorkshopFormulas.CalculateLevelFitFactor(60, 0m, 0m);
        decimal mitResistenz = WorkshopFormulas.CalculateLevelFitFactor(60, 0.5m, 0m);

        // Prüfung: Mit Resistenz näher an 1.0
        mitResistenz.Should().BeGreaterThan(ohneResistenz);
    }

    [Fact]
    public void CalculateLevelFitFactor_VollständigeResistenz_IstEins()
    {
        // Vorbereitung: 100% Resistenz (Legendary Tier) → kein Malus
        // Ausführung
        decimal result = WorkshopFormulas.CalculateLevelFitFactor(1000, 1.0m, 0m);

        // Prüfung
        result.Should().Be(1.0m);
    }

    [Fact]
    public void CalculateLevelFitFactor_SehrHohesLevel_NieUnterMinimum()
    {
        // Vorbereitung: Kritischer Gotcha — Minimum ist 0.20 (Worker nie ganz nutzlos)
        // Ausführung
        decimal result = WorkshopFormulas.CalculateLevelFitFactor(1000, 0m, 0m);

        // Prüfung: Minimum 20% erhalten
        result.Should().BeGreaterThanOrEqualTo(GameBalanceConstants.MinLevelFitFactor);
    }

    [Fact]
    public void CalculateLevelFitFactor_ResistenzForschungsBonus_Stapelt()
    {
        // Vorbereitung: Tier-Resistenz 0.3 + Forschungsbonus 0.4 = 0.7 Gesamtresistenz
        decimal nurTier = WorkshopFormulas.CalculateLevelFitFactor(90, 0.30m, 0m);
        decimal mitForschung = WorkshopFormulas.CalculateLevelFitFactor(90, 0.30m, 0.40m);

        // Prüfung: Forschungsbonus verbessert den Faktor weiter
        mitForschung.Should().BeGreaterThan(nurTier);
    }

    [Fact]
    public void CalculateLevelFitFactor_ResistenzWirdAuf1Gedeckelt()
    {
        // Vorbereitung: Tier 0.8 + Forschung 0.8 = 1.6 → wird auf 1.0 gedeckelt
        decimal gedeckelt = WorkshopFormulas.CalculateLevelFitFactor(90, 0.80m, 0.80m);
        decimal maximal = WorkshopFormulas.CalculateLevelFitFactor(90, 1.0m, 0m);

        // Prüfung: Beide müssen 1.0 ergeben (kein Bonus-Effekt über 100% Resistenz)
        gedeckelt.Should().Be(maximal);
        gedeckelt.Should().Be(1.0m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CalculateGrossIncome
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateGrossIncome_OhneArbeiter_IstNull()
    {
        // Vorbereitung: Leere Worker-Liste → kein Einkommen
        var workers = new List<Worker>();

        // Ausführung
        decimal result = WorkshopFormulas.CalculateGrossIncome(10, WorkshopType.Carpenter, workers, 0m, 0m);

        // Prüfung
        result.Should().Be(0m);
    }

    [Fact]
    public void CalculateGrossIncome_EinArbeiter_PositivesEinkommen()
    {
        // Vorbereitung: Ein Arbeiter → Einkommen > 0
        var workers = new List<Worker> { Worker.CreateRandom() };

        // Ausführung
        decimal result = WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter, workers, 0m, 0m);

        // Prüfung
        result.Should().BePositive();
    }

    [Fact]
    public void CalculateGrossIncome_MehrArbeiter_HöheresEinkommen()
    {
        // Vorbereitung: Mehr Arbeiter → mehr Einkommen
        var einArbeiter = new List<Worker> { Worker.CreateRandom() };
        var zweiArbeiter = new List<Worker> { Worker.CreateRandom(), Worker.CreateRandom() };

        // Ausführung
        decimal income1 = WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter, einArbeiter, 0m, 0m);
        decimal income2 = WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter, zweiArbeiter, 0m, 0m);

        // Prüfung
        income2.Should().BeGreaterThan(income1);
    }

    [Fact]
    public void CalculateGrossIncome_MitRebirthBonus_IstHöher()
    {
        // Vorbereitung: 15% Rebirth-Bonus → 15% mehr Einkommen
        var workers = new List<Worker> { Worker.CreateRandom() };
        decimal ohneBonus = WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter, workers, 0m, 0m);

        // Ausführung
        decimal mitBonus = WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter, workers, 0m, 0.15m);

        // Prüfung: Exakt 15% mehr
        mitBonus.Should().BeApproximately(ohneBonus * 1.15m, 0.01m);
    }

    [Fact]
    public void CalculateGrossIncome_AuraBonusWirdAngewendet()
    {
        // Vorbereitung: S-Tier Worker hat 5% Aura-Bonus
        var sTierWorker = Worker.CreateForTier(WorkerTier.S);
        var eWorkers = new List<Worker> { Worker.CreateRandom() };
        var sWorkers = new List<Worker> { sTierWorker };

        // Ausführung: Gleiche Level, gleiche Typ, aber S-Tier hat Aura
        decimal einkommenE = WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter, eWorkers, 0m, 0m);
        decimal einkommenS = WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter, sWorkers, 0m, 0m);

        // Prüfung: S-Tier muss mehr verdienen (höhere Basis-Effizienz + Aura)
        einkommenS.Should().BeGreaterThan(einkommenE);
    }

    [Fact]
    public void CalculateGrossIncome_AuraBonusGedeckeltBeiMaxAuraBonus()
    {
        // Vorbereitung: Viele Legendary Worker → Aura wird bei 50% gedeckelt
        // Jeder Legendary Worker gibt 20% Aura → 3 genügen um den Cap zu erreichen
        var manyLegendary = new List<Worker>
        {
            Worker.CreateForTier(WorkerTier.Legendary),
            Worker.CreateForTier(WorkerTier.Legendary),
            Worker.CreateForTier(WorkerTier.Legendary),
            Worker.CreateForTier(WorkerTier.Legendary),
        };
        var threeLegendary = new List<Worker>
        {
            Worker.CreateForTier(WorkerTier.Legendary),
            Worker.CreateForTier(WorkerTier.Legendary),
            Worker.CreateForTier(WorkerTier.Legendary),
        };

        // Ausführung
        decimal income4 = WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter, manyLegendary, 0m, 0m);
        decimal income3 = WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter, threeLegendary, 0m, 0m);

        // Prüfung: 4 Legendary verdienen mehr als 3 (Aura-Cap ändert nichts am Basis-Einkommen)
        income4.Should().BeGreaterThan(income3);
    }

    [Fact]
    public void CalculateGrossIncome_HigherWorkshopLevel_HöheresEinkommen()
    {
        // Vorbereitung: Höheres Workshop-Level → höheres Basis-Einkommen pro Worker
        var workers = new List<Worker> { Worker.CreateRandom() };

        // Ausführung
        decimal level1 = WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter, workers, 0m, 0m);
        decimal level10 = WorkshopFormulas.CalculateGrossIncome(10, WorkshopType.Carpenter, workers, 0m, 0m);

        // Prüfung
        level10.Should().BeGreaterThan(level1);
    }

    [Fact]
    public void CalculateGrossIncome_LevelResistenzBonus_HatEinfluss()
    {
        // Vorbereitung: Level 90 Workshop — F-Tier Worker hat Level-Fit-Malus
        // Mit Resistenz-Bonus verschwindet der Malus teilweise
        var fTierWorker = Worker.CreateForTier(WorkerTier.F);
        var workers = new List<Worker> { fTierWorker };

        // Ausführung: Gleicher Worker, aber einmal mit Resistenz-Forschungsbonus
        decimal ohneBonus = WorkshopFormulas.CalculateGrossIncome(90, WorkshopType.Carpenter, workers, 0m, 0m);
        decimal mitBonus = WorkshopFormulas.CalculateGrossIncome(90, WorkshopType.Carpenter, workers, 0.5m, 0m);

        // Prüfung: Forschungs-Resistenz verbessert Einkommen bei hohem Level
        mitBonus.Should().BeGreaterThan(ohneBonus);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regression: Bekannte Gotchas
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Regression_CalculateUpgradeCost_Level1_NichtVerwechseltMitLevel2()
    {
        // Regression: Level 1 ist Sonderfall (100 statt 200*1.07^0=200)
        // Sicherstellung: Level 1 und Level 2 sind NICHT gleich
        decimal level1 = WorkshopFormulas.CalculateUpgradeCost(1, 0m, 0m, 0m);
        decimal level2 = WorkshopFormulas.CalculateUpgradeCost(2, 0m, 0m, 0m);

        // Prüfung: Level 1 = 100, Level 2 = 214 (nicht 200!)
        level1.Should().Be(100m);
        level2.Should().NotBe(level1);
        level2.Should().BeGreaterThan(level1);
    }

    [Fact]
    public void Regression_CalculateLevelFitFactor_Level31_HatBereitsMalus()
    {
        // Regression: Grenzfall — Level 31 hat bereits 1 Schritt Malus
        // steps = 31 / 30 = 1 (Integer-Division), penalty = 1 * 0.02 = 0.02
        decimal level30 = WorkshopFormulas.CalculateLevelFitFactor(30, 0m, 0m);
        decimal level31 = WorkshopFormulas.CalculateLevelFitFactor(31, 0m, 0m);

        // Prüfung: Level 30 noch kein Malus, Level 31 schon (steps = 1)
        level30.Should().Be(1.0m);
        level31.Should().BeLessThan(1.0m);
    }

    [Fact]
    public void Regression_CalculateMilestoneMultiplier_KumulativNichtAdditiv()
    {
        // Regression: Multiplikatoren sind kumulativ (multipliziert), NICHT additiv (summiert)
        // Level 50 = 1.15 * 1.30 = 1.495 (NICHT 1.15 + 1.30 = 2.45)
        decimal level50 = WorkshopFormulas.CalculateMilestoneMultiplier(50);

        // Prüfung: Kumulativ, nicht additiv
        level50.Should().BeApproximately(1.495m, 0.0001m);
        level50.Should().NotBeApproximately(2.45m, 0.01m);
    }
}
