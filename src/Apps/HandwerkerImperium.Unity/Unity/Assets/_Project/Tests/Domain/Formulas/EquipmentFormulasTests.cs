using System;
using HandwerkerImperium.Domain.Economy;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Formulas
{
    /// <summary>
    /// Verifiziert den Service-Formel-Extrakt EquipmentFormulas (aus EquipmentService) gegen die
    /// Original-Mathematik: Drop-Chance (+5%/Stufe, Perfect +5%) und Shop-Rotation (3-4 Items, difficulty 1-3).
    /// </summary>
    [TestFixture]
    public class EquipmentFormulasTests
    {
        [Test]
        public void DropChance_MatchOriginal()
        {
            Assert.That(EquipmentFormulas.CalculateDropChance(0, false), Is.EqualTo(0.05).Within(1e-9));
            Assert.That(EquipmentFormulas.CalculateDropChance(1, false), Is.EqualTo(0.10).Within(1e-9));
            Assert.That(EquipmentFormulas.CalculateDropChance(2, false), Is.EqualTo(0.15).Within(1e-9));
            Assert.That(EquipmentFormulas.CalculateDropChance(3, false), Is.EqualTo(0.20).Within(1e-9));
            Assert.That(EquipmentFormulas.CalculateDropChance(0, true), Is.EqualTo(0.10).Within(1e-9));
            Assert.That(EquipmentFormulas.CalculateDropChance(3, true), Is.EqualTo(0.25).Within(1e-9));
        }

        [Test]
        public void RollDrop_GuaranteedAboveOne()
        {
            // dropChance(20,true) = 1.10 > 1 -> NextDouble in [0,1) faellt immer
            for (int i = 0; i < 50; i++)
                Assert.That(EquipmentFormulas.RollDrop(20, true, new Random(i)), Is.Not.Null);
        }

        [Test]
        public void RollDrop_ConsistentWithFirstRoll()
        {
            const int seed = 12345;
            double first = new Random(seed).NextDouble();
            var res = EquipmentFormulas.RollDrop(0, false, new Random(seed));
            bool shouldDrop = first < EquipmentFormulas.CalculateDropChance(0, false);
            Assert.That(res != null, Is.EqualTo(shouldDrop));
        }

        [Test]
        public void GenerateShopItems_CountAndValidity()
        {
            var shop = EquipmentFormulas.GenerateShopItems(new Random(7));
            Assert.That(shop.Count, Is.InRange(EquipmentFormulas.MinShopItems, EquipmentFormulas.MaxShopItems));
            foreach (var e in shop)
            {
                Assert.That(e, Is.Not.Null);
                Assert.That(e.ShopPrice, Is.GreaterThan(0));
            }
        }
    }
}
