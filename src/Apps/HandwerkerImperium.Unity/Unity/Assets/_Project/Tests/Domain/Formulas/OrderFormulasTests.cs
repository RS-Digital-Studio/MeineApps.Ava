using System;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.Orders;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Formulas
{
    /// <summary>
    /// Verifiziert den Service-Formel-Extrakt OrderFormulas (aus OrderGeneratorService) gegen die
    /// Original-Mathematik: Reward/XP (normal + Material), Material-/Template-Counts, Schwierigkeit
    /// (Level×Prestige × roll, Reputation-Fallback), OrderType-Bestimmung, deterministischer Kundenname.
    /// </summary>
    [TestFixture]
    public class OrderFormulasTests
    {
        [Test]
        public void CustomerName_Deterministic()
        {
            Assert.That(OrderFormulas.GenerateCustomerName(12345), Is.EqualTo(OrderFormulas.GenerateCustomerName(12345)));
            var name = OrderFormulas.GenerateCustomerName(777);
            Assert.That(name, Does.Contain(" "));
            Assert.That(name.Length, Is.GreaterThan(3));
        }

        [Test]
        public void BaseRewardAndXp_AndGuildBonus()
        {
            decimal mult = WorkshopType.Carpenter.GetBaseIncomeMultiplier();
            var (r, xp) = OrderFormulas.ComputeBaseRewardAndXp(WorkshopType.Carpenter, 10, 10, 2, 0m, 0m, 0m);
            Assert.That(r, Is.EqualTo(1100m * 2.3m * mult)); // perTask 1100, taskMult 2*1.15
            Assert.That(xp, Is.EqualTo(500));

            var (rg, xpg) = OrderFormulas.ComputeBaseRewardAndXp(WorkshopType.Carpenter, 10, 10, 2, 0m, 0.5m, 0.2m);
            Assert.That(rg, Is.EqualTo(1100m * 2.3m * mult * 1.5m));
            Assert.That(xpg, Is.EqualTo(600));
        }

        [Test]
        public void MaterialOrderReward_AndCounts()
        {
            decimal mult = WorkshopType.Carpenter.GetBaseIncomeMultiplier();
            var (mr, mxp) = OrderFormulas.ComputeMaterialOrderReward(WorkshopType.Carpenter, 10, 10, 10, 0m, 0m);
            Assert.That(mr, Is.EqualTo(1100m * 2.0m * mult));
            Assert.That(mxp, Is.EqualTo(750));

            Assert.That(OrderFormulas.CalculateMaterialOrderMainCount(0), Is.EqualTo(5));
            Assert.That(OrderFormulas.CalculateMaterialOrderMainCount(500), Is.EqualTo(15));
            Assert.That(OrderFormulas.CalculateMaterialOrderSecondCount(0), Is.EqualTo(3));
            Assert.That(OrderFormulas.CalculateMaterialOrderSecondCount(1000), Is.EqualTo(8));
            Assert.That(OrderFormulas.CalculateMaxTemplateIndex(5, 10), Is.EqualTo(4));
            Assert.That(OrderFormulas.CalculateMaxTemplateIndex(5, 3), Is.EqualTo(1));
        }

        [Test]
        public void Difficulties_And_AdjustedRoll()
        {
            Assert.That(OrderFormulas.GetMaterialOrderDifficulty(75), Is.EqualTo(OrderDifficulty.Easy));
            Assert.That(OrderFormulas.GetMaterialOrderDifficulty(76), Is.EqualTo(OrderDifficulty.Medium));
            Assert.That(OrderFormulas.GetMaterialOrderDifficulty(201), Is.EqualTo(OrderDifficulty.Hard));

            Assert.That(OrderFormulas.ComputeAdjustedRoll(50, 0m, 0m, 0m), Is.EqualTo(50));
            Assert.That(OrderFormulas.ComputeAdjustedRoll(50, 0.1m, 0.1m, 0m), Is.EqualTo(30));
            Assert.That(OrderFormulas.ComputeAdjustedRoll(5, 0.1m, 0m, 0m), Is.EqualTo(0));

            Assert.That(OrderFormulas.GetDifficulty(10, 0, 80, 90), Is.EqualTo(OrderDifficulty.Medium));
            Assert.That(OrderFormulas.GetDifficulty(10, 0, 80, 50), Is.EqualTo(OrderDifficulty.Easy));
            Assert.That(OrderFormulas.GetDifficulty(500, 0, 80, 10), Is.EqualTo(OrderDifficulty.Medium));
            int repReq = OrderDifficulty.Expert.GetRequiredReputation();
            Assert.That(OrderFormulas.GetDifficulty(10, 1, repReq, 96), Is.EqualTo(OrderDifficulty.Expert));
            Assert.That(OrderFormulas.GetDifficulty(10, 1, repReq - 1, 96), Is.EqualTo(OrderDifficulty.Hard));
        }

        [Test]
        public void OrderType_LevelAndRoll()
        {
            Assert.That(OrderFormulas.DetermineOrderType(5, 3, 50), Is.EqualTo(OrderType.Standard));
            Assert.That(OrderFormulas.DetermineOrderType(12, 3, 60), Is.EqualTo(OrderType.Standard));
            Assert.That(OrderFormulas.DetermineOrderType(12, 3, 80), Is.EqualTo(OrderType.Large));
            Assert.That(OrderFormulas.DetermineOrderType(25, 2, 40), Is.EqualTo(OrderType.Standard));
            Assert.That(OrderFormulas.DetermineOrderType(25, 2, 75), Is.EqualTo(OrderType.Cooperation));
            Assert.That(OrderFormulas.DetermineOrderType(25, 2, 90), Is.EqualTo(OrderType.Weekly));
            Assert.That(OrderFormulas.DetermineOrderType(25, 1, 90), Is.EqualTo(OrderType.Weekly));
        }
    }
}
