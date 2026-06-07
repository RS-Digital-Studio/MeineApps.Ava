using HandwerkerImperium.Domain;
using HandwerkerImperium.Domain.Economy;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Economy
{
    /// <summary>
    /// Verifiziert die portierte Economy-Basis (WorkshopType, WorkerTier, WorkshopFormulas,
    /// GameBalanceConstants) gegen die verbindlichen Werte aus ORIGINAL_WERTE.md / Avalonia-Code.
    /// Reine Wert-/Formel-Treue — kein Unity-API.
    /// </summary>
    [TestFixture]
    public class WorkshopEconomyTests
    {
        // ───────────────────────── WorkshopType ─────────────────────────

        [Test]
        public void GetUnlockCost_MatchesOriginal()
        {
            Assert.That(WorkshopType.Carpenter.GetUnlockCost(), Is.EqualTo(0m));
            Assert.That(WorkshopType.Plumber.GetUnlockCost(), Is.EqualTo(5_000m));
            Assert.That(WorkshopType.Electrician.GetUnlockCost(), Is.EqualTo(250_000m));
            Assert.That(WorkshopType.GeneralContractor.GetUnlockCost(), Is.EqualTo(25_000_000_000m));
            Assert.That(WorkshopType.InnovationLab.GetUnlockCost(), Is.EqualTo(50_000_000_000m));
        }

        [Test]
        public void GetBaseIncomeMultiplier_MatchesOriginal()
        {
            Assert.That(WorkshopType.Carpenter.GetBaseIncomeMultiplier(), Is.EqualTo(1.0m));
            Assert.That(WorkshopType.GeneralContractor.GetBaseIncomeMultiplier(), Is.EqualTo(7.0m));
            Assert.That(WorkshopType.InnovationLab.GetBaseIncomeMultiplier(), Is.EqualTo(5.0m));
        }

        [Test]
        public void PrestigeExclusivity_MatchesOriginal()
        {
            Assert.That(WorkshopType.Carpenter.GetRequiredPrestige(), Is.EqualTo(0));
            Assert.That(WorkshopType.MasterSmith.GetRequiredPrestige(), Is.EqualTo(4));
            Assert.That(WorkshopType.Architect.IsPrestigeExclusive(), Is.True);
            Assert.That(WorkshopType.Carpenter.IsPrestigeExclusive(), Is.False);
        }

        [Test]
        public void GetUnlockLevel_MatchesOriginal()
        {
            Assert.That(WorkshopType.Plumber.GetUnlockLevel(), Is.EqualTo(5));
            Assert.That(WorkshopType.Roofer.GetUnlockLevel(), Is.EqualTo(40));
            Assert.That(WorkshopType.MasterSmith.GetUnlockLevel(), Is.EqualTo(500));
        }

        // ───────────────────────── WorkerTier ─────────────────────────

        [Test]
        public void WorkerTier_EfficiencyRanges_MatchOriginal()
        {
            Assert.That(WorkerTier.F.GetMinEfficiency(), Is.EqualTo(0.30m));
            Assert.That(WorkerTier.F.GetMaxEfficiency(), Is.EqualTo(0.50m));
            Assert.That(WorkerTier.Legendary.GetMinEfficiency(), Is.EqualTo(13.00m));
            Assert.That(WorkerTier.Legendary.GetMaxEfficiency(), Is.EqualTo(22.00m));
        }

        [Test]
        public void WorkerTier_WageAndHiring_MatchOriginal()
        {
            Assert.That(WorkerTier.S.GetWagePerHour(), Is.EqualTo(160m));
            Assert.That(WorkerTier.Legendary.GetBaseHiringCost(), Is.EqualTo(50_000_000m));
            // Level-Skalierung: +2% pro Level über 1
            Assert.That(WorkerTier.F.GetHiringCost(1), Is.EqualTo(50m));   // 1.0x
            Assert.That(WorkerTier.F.GetHiringCost(11), Is.EqualTo(60m));  // 1.2x
            Assert.That(WorkerTier.F.GetHiringCost(51), Is.EqualTo(100m)); // 2.0x
        }

        [Test]
        public void WorkerTier_ResistanceAuraScrews_MatchOriginal()
        {
            Assert.That(WorkerTier.F.GetLevelResistance(), Is.EqualTo(0.00m));
            Assert.That(WorkerTier.Legendary.GetLevelResistance(), Is.EqualTo(1.00m));
            Assert.That(WorkerTier.B.GetAuraBonus(), Is.EqualTo(0m));
            Assert.That(WorkerTier.S.GetAuraBonus(), Is.EqualTo(0.05m));
            Assert.That(WorkerTier.Legendary.GetAuraBonus(), Is.EqualTo(0.20m));
            Assert.That(WorkerTier.B.GetHiringScrewCost(), Is.EqualTo(0));
            Assert.That(WorkerTier.Legendary.GetHiringScrewCost(), Is.EqualTo(750));
        }

        // ───────────────────── WorkshopFormulas (exakt, reines decimal) ─────────────────────

        [Test]
        public void BaseIncomePerWorker_Level1Carpenter_IsOne()
        {
            // 1.02^0 * 1.0 (Carpenter) * Milestone(1)=1.0 = 1.0
            Assert.That(WorkshopFormulas.CalculateBaseIncomePerWorker(1, WorkshopType.Carpenter), Is.EqualTo(1.0m));
        }

        [Test]
        public void MilestoneMultiplier_MatchesOriginal()
        {
            Assert.That(WorkshopFormulas.CalculateMilestoneMultiplier(1), Is.EqualTo(1.0m));
            Assert.That(WorkshopFormulas.CalculateMilestoneMultiplier(24), Is.EqualTo(1.0m));
            Assert.That(WorkshopFormulas.CalculateMilestoneMultiplier(25), Is.EqualTo(1.15m));
            Assert.That(WorkshopFormulas.CalculateMilestoneMultiplier(50), Is.EqualTo(1.15m * 1.30m));
            Assert.That(WorkshopFormulas.IsMilestoneLevel(100), Is.True);
            Assert.That(WorkshopFormulas.IsMilestoneLevel(101), Is.False);
            // Kumulativ bei 1000: tatsächliches Produkt aller Milestones (Doc-Kommentar "~921x" ist veraltet,
            // Werte wurden angehoben). Code ist die Wahrheit -> Plausibilitätsfenster.
            decimal m1000 = WorkshopFormulas.CalculateMilestoneMultiplier(1000);
            Assert.That(m1000, Is.GreaterThan(1400m).And.LessThan(1600m));
        }

        [Test]
        public void LevelFitFactor_MatchesOriginal()
        {
            Assert.That(WorkshopFormulas.CalculateLevelFitFactor(30, 0m, 0m), Is.EqualTo(1.0m));   // <=30 voll
            Assert.That(WorkshopFormulas.CalculateLevelFitFactor(60, 0m, 0m), Is.EqualTo(0.96m));  // steps 2 -> -0.04
            Assert.That(WorkshopFormulas.CalculateLevelFitFactor(1000, 0m, 0m), Is.EqualTo(0.34m)); // steps 33 -> -0.66
            Assert.That(WorkshopFormulas.CalculateLevelFitFactor(1000, 1.0m, 0m), Is.EqualTo(1.0m)); // volle Resistenz
            // Minimum-Floor greift bei extremem Malus ohne Resistenz nicht unter 0.20
            Assert.That(WorkshopFormulas.CalculateLevelFitFactor(3000, 0m, 0m), Is.EqualTo(0.20m));
        }

        // ─────────────── WorkshopFormulas (double-abgeleitet, Toleranz) ───────────────

        [Test]
        public void UpgradeCost_MatchesOriginal()
        {
            Assert.That(WorkshopFormulas.CalculateUpgradeCost(1, 0m, 0m, 0m), Is.EqualTo(100m)); // Level-1-Sonderfall, exakt
            Assert.That(WorkshopFormulas.CalculateUpgradeCost(2, 0m, 0m, 0m), Is.EqualTo(214m).Within(0.001m)); // 200*1.07
            // MaxLevel -> 0
            Assert.That(WorkshopFormulas.CalculateUpgradeCost(GameBalanceConstants.WorkshopMaxLevel, 0m, 0m, 0m), Is.EqualTo(0m));
        }

        [Test]
        public void HireRentMaterial_MatchOriginal()
        {
            Assert.That(WorkshopFormulas.CalculateHireWorkerCost(0), Is.EqualTo(50m));   // 50*1.5^0
            Assert.That(WorkshopFormulas.CalculateHireWorkerCost(1), Is.EqualTo(75m).Within(0.001m)); // 50*1.5
            Assert.That(WorkshopFormulas.CalculateRentPerHour(50), Is.EqualTo(500m));    // linear 10*50
            Assert.That(WorkshopFormulas.CalculateRentPerHour(100), Is.EqualTo(1000m));
            Assert.That(WorkshopFormulas.CalculateMaterialCostPerHour(50, WorkshopType.Carpenter), Is.EqualTo(250m)); // 5*50*1.0
        }

        [Test]
        public void DiscountFactor_CapsPrestigeAt50Percent()
        {
            // PrestigeDiscount wird auf 0.50 gedeckelt
            Assert.That(WorkshopFormulas.CalculateDiscountFactor(0m, 0.80m, 0m), Is.EqualTo(0.50m));
            Assert.That(WorkshopFormulas.CalculateDiscountFactor(0.10m, 0m, 0m), Is.EqualTo(0.90m));
        }

        // ─────────────── CalculateGrossIncome (Worker-abhängig) ───────────────

        [Test]
        public void GrossIncome_NoWorkers_IsZero()
        {
            Assert.That(WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter,
                new System.Collections.Generic.List<Worker>(), 0m, 0m), Is.EqualTo(0m));
        }

        [Test]
        public void GrossIncome_SingleFullEffWorker_MatchesBaseIncome()
        {
            // Worker mit Efficiency=1, Mood=80 (Faktor 1.0), Fatigue=0 (1.0), Talent=1 (1.0),
            // ExperienceLevel=1 (1.03), Steady (1.0), keine Spezialisierung/Equip.
            // EffectiveEfficiency = 1 * 1.03 * 1.0 * 1.0 * 1.0 * 1.0 * 1.0 = 1.03
            var w = new Worker { Tier = WorkerTier.F, Efficiency = 1m, Mood = 80m, Fatigue = 0m, Talent = 1, ExperienceLevel = 1, Personality = WorkerPersonality.Steady };
            var workers = new System.Collections.Generic.List<Worker> { w };
            // Level 1 Carpenter: BaseIncomePerWorker=1.0, LevelFit(1,..)=1.0, F-Tier Aura=0, kein Rebirth.
            decimal income = WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter, workers, 0m, 0m);
            Assert.That(income, Is.EqualTo(1.03m));
        }

        [Test]
        public void GrossIncome_LegendaryWorker_AppliesAuraBonus()
        {
            // Legendary-Worker gibt +20% Aura. EffectiveEfficiency (Efficiency=1, sonst wie oben) = 1.03.
            // income = 1.0 (base) * 1.03 * LevelFit(1)=1.0 -> 1.03, dann *1.20 Aura = 1.236
            var w = new Worker { Tier = WorkerTier.Legendary, Efficiency = 1m, Mood = 80m, Fatigue = 0m, Talent = 1, ExperienceLevel = 1, Personality = WorkerPersonality.Steady };
            var workers = new System.Collections.Generic.List<Worker> { w };
            decimal income = WorkshopFormulas.CalculateGrossIncome(1, WorkshopType.Carpenter, workers, 0m, 0m);
            Assert.That(income, Is.EqualTo(1.03m * 1.20m));
        }
    }
}
