using System.Collections.Generic;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.Orders;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Economy
{
    /// <summary>
    /// Verifiziert das portierte Workshop-Modell + WorkshopSpecialization + OrderStrategy
    /// gegen die Original-Werte (Avalonia Models/Workshop.cs, WorkshopSpecialization.cs, OrderStrategy.cs).
    /// </summary>
    [TestFixture]
    public class WorkshopModelTests
    {
        [Test]
        public void OrderStrategy_Multipliers_MatchOriginal()
        {
            Assert.That(OrderStrategy.Safe.GetRewardMultiplier(), Is.EqualTo(0.75m));
            Assert.That(OrderStrategy.Risk.GetRewardMultiplier(), Is.EqualTo(2.0m));
            Assert.That(OrderStrategy.Risk.GetXpMultiplier(), Is.EqualTo(1.75m));
            Assert.That(OrderStrategy.Safe.GetToleranceMultiplier(), Is.EqualTo(1.5));
            Assert.That(OrderStrategy.Risk.GetSpeedMultiplier(), Is.EqualTo(1.3));
            Assert.That(OrderStrategy.Safe.GetTimeMultiplier(), Is.EqualTo(1.3));
            Assert.That(OrderStrategy.Risk.HasHardFail(), Is.True);
            Assert.That(OrderStrategy.Standard.HasHardFail(), Is.False);
            Assert.That(OrderStrategy.Risk.GetReputationPenaltyOnMiss(), Is.EqualTo(-10));
        }

        [Test]
        public void Specialization_Modifiers_MatchOriginal()
        {
            var eff = new WorkshopSpecialization { Type = SpecializationType.Efficiency };
            var qual = new WorkshopSpecialization { Type = SpecializationType.Quality };
            var eco = new WorkshopSpecialization { Type = SpecializationType.Economy };
            Assert.That(eff.IncomeModifier, Is.EqualTo(0.30m));
            Assert.That(eff.WorkerCapacityModifier, Is.EqualTo(-1));
            Assert.That(qual.CostModifier, Is.EqualTo(0.15m));
            Assert.That(qual.EfficiencyModifier, Is.EqualTo(0.20m));
            Assert.That(qual.AuraBonusMultiplier, Is.EqualTo(2.0m));
            Assert.That(eco.CostModifier, Is.EqualTo(-0.25m));
            Assert.That(eco.IncomeModifier, Is.EqualTo(-0.05m));
            Assert.That(eco.OrderRewardBonus, Is.EqualTo(0.15m));
        }

        [Test]
        public void Workshop_Create_UnlocksOnlyCarpenter()
        {
            Assert.That(Workshop.Create(WorkshopType.Carpenter).IsUnlocked, Is.True);
            Assert.That(Workshop.Create(WorkshopType.Plumber).IsUnlocked, Is.False);
        }

        [Test]
        public void Workshop_BaseMaxWorkers_ScalesEvery50Levels()
        {
            Assert.That(new Workshop { Level = 1 }.BaseMaxWorkers, Is.EqualTo(1));
            Assert.That(new Workshop { Level = 51 }.BaseMaxWorkers, Is.EqualTo(2));
            Assert.That(new Workshop { Level = 1000 }.BaseMaxWorkers, Is.EqualTo(20));
        }

        [Test]
        public void Workshop_RebirthBonuses_MatchOriginal()
        {
            var ws = new Workshop { Type = WorkshopType.Carpenter, Level = 1 };
            Assert.That(ws.RebirthIncomeBonus, Is.EqualTo(0m));
            ws.RebirthStars = 1;
            Assert.That(ws.RebirthIncomeBonus, Is.EqualTo(0.15m));
            ws.RebirthStars = 5;
            Assert.That(ws.RebirthIncomeBonus, Is.EqualTo(1.50m));
            Assert.That(ws.RebirthExtraWorkers, Is.EqualTo(3));
            Assert.That(ws.RebirthUpgradeDiscount, Is.EqualTo(0.25m));
        }

        [Test]
        public void Workshop_GrossIncome_AppliesEfficiencySpecialization()
        {
            var ws = new Workshop { Type = WorkshopType.Carpenter, Level = 1 };
            var w = new Worker { Tier = WorkerTier.F, Efficiency = 1m, Mood = 80m, Fatigue = 0m, Talent = 1, ExperienceLevel = 1, Personality = WorkerPersonality.Steady };
            ws.Workers = new List<Worker> { w };
            // 1 F-Worker EffectiveEfficiency 1.03, base GrossIncome = 1.03
            Assert.That(ws.GrossIncomePerSecond, Is.EqualTo(1.03m));
            // Efficiency-Spezialisierung: +30% Einkommen
            ws.WorkshopSpecialization = new WorkshopSpecialization { Type = SpecializationType.Efficiency };
            Assert.That(ws.GrossIncomePerSecond, Is.EqualTo(1.03m * 1.30m));
            // -1 Worker-Slot, aber Floor >= 1
            Assert.That(ws.MaxWorkers, Is.EqualTo(1));
        }

        [Test]
        public void Workshop_UpgradeAndUnlockCosts_MatchOriginal()
        {
            var ws = new Workshop { Type = WorkshopType.Carpenter, Level = 1 };
            Assert.That(ws.UpgradeCost, Is.EqualTo(100m));
            Assert.That(ws.UnlockCost, Is.EqualTo(0m));
            Assert.That(ws.HireWorkerCost, Is.EqualTo(50m));
            Assert.That(new Workshop { Type = WorkshopType.Plumber }.UnlockCost, Is.EqualTo(5_000m));
        }
    }
}
