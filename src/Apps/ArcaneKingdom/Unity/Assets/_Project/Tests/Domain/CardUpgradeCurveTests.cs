#nullable enable
using ArcaneKingdom.Domain.Battle;
using ArcaneKingdom.Domain.Economy;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class CardUpgradeCurveTests
    {
        [Test]
        public void Lv4Auf5VerlangtEineKopieUndRareScrap()
        {
            var cost = CardUpgradeCurve.GetCostForUpgrade(4);
            Assert.IsNotNull(cost);
            Assert.AreEqual(1, cost!.Value.CopiesRequired);
            Assert.AreEqual(ScrapType.Rare, cost.Value.ScrapKind);
        }

        [Test]
        public void Lv9Auf10VerlangtZweiKopienUndEpicScrap()
        {
            var cost = CardUpgradeCurve.GetCostForUpgrade(9);
            Assert.IsNotNull(cost);
            Assert.AreEqual(2, cost!.Value.CopiesRequired);
            Assert.AreEqual(ScrapType.Epic, cost.Value.ScrapKind);
        }

        [Test]
        public void Lv14Auf15VerlangtDreiKopienUndLegendaryScrap()
        {
            var cost = CardUpgradeCurve.GetCostForUpgrade(14);
            Assert.IsNotNull(cost);
            Assert.AreEqual(3, cost!.Value.CopiesRequired);
            Assert.AreEqual(ScrapType.Legendary, cost.Value.ScrapKind);
        }

        [Test]
        public void Lv15HatKeineWeitereUpgradeKosten()
        {
            Assert.IsNull(CardUpgradeCurve.GetCostForUpgrade(15));
            Assert.IsNull(CardUpgradeCurve.GetCostForUpgrade(99));
        }

        [Test]
        public void GoldKostenSteigenMonotonBisLv14()
        {
            long? prev = null;
            for (var lv = 0; lv <= 14; lv++)
            {
                var c = CardUpgradeCurve.GetCostForUpgrade(lv);
                Assert.IsNotNull(c, $"Upgrade von LV{lv} muss definiert sein.");
                if (prev.HasValue)
                    Assert.GreaterOrEqual(c!.Value.GoldCost, prev.Value, $"Gold-Kosten LV{lv} sollten >= LV{lv - 1} sein.");
                prev = c!.Value.GoldCost;
            }
        }
    }
}
