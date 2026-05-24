#nullable enable
using ArcaneKingdom.Domain.Economy;
using ArcaneKingdom.Domain.Player;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class PlayerCurrenciesTests
    {
        [Test]
        public void AddGoldErhoehtSaldo()
        {
            var c = new PlayerCurrencies();
            c.AddGold(100);
            Assert.AreEqual(100, c.Gold);
        }

        [Test]
        public void SpendGoldReduziertSaldoUndGibtTrueZurueck()
        {
            var c = new PlayerCurrencies();
            c.AddGold(500);
            var ok = c.SpendGold(200);
            Assert.IsTrue(ok);
            Assert.AreEqual(300, c.Gold);
        }

        [Test]
        public void SpendGoldGibtFalseBeiUnzureichendemSaldo()
        {
            var c = new PlayerCurrencies();
            c.AddGold(100);
            var ok = c.SpendGold(200);
            Assert.IsFalse(ok);
            Assert.AreEqual(100, c.Gold);
        }

        [Test]
        public void NormalEnergieIstAufCapBegrenzt()
        {
            var c = new PlayerCurrencies();
            c.AddEnergy(120);
            Assert.AreEqual(PlayerCurrencies.EnergyDefaultCap, c.Energy);
            Assert.AreEqual(0, c.EnergyBonus);
        }

        [Test]
        public void BonusEnergieKannUeberCapGehen()
        {
            var c = new PlayerCurrencies();
            c.AddEnergy(60);
            c.AddEnergyBonus(20);
            Assert.AreEqual(80, c.TotalEnergy);
        }

        [Test]
        public void SpendEnergieVerbrauchtZuerstBonus()
        {
            var c = new PlayerCurrencies();
            c.AddEnergy(60);
            c.AddEnergyBonus(20);
            Assert.IsTrue(c.SpendEnergy(15));
            Assert.AreEqual(60, c.Energy);
            Assert.AreEqual(5, c.EnergyBonus);
        }

        [Test]
        public void SpendEnergieGreiftAufNormalEnergieZurueckWennBonusErschoepft()
        {
            var c = new PlayerCurrencies();
            c.AddEnergy(50);
            c.AddEnergyBonus(10);
            Assert.IsTrue(c.SpendEnergy(30));
            Assert.AreEqual(30, c.Energy);
            Assert.AreEqual(0, c.EnergyBonus);
        }

        [Test]
        public void SpendEnergieGibtFalseBeiUnzureichendemTotal()
        {
            var c = new PlayerCurrencies();
            c.AddEnergy(20);
            Assert.IsFalse(c.SpendEnergy(50));
        }

        [Test]
        public void MeritPunkteCappenBei199999()
        {
            var c = new PlayerCurrencies();
            c.AddMeritPoints(500_000);
            Assert.AreEqual(199_999, c.MeritPoints);
        }

        [Test]
        public void ScrapsKoennenProTypUnabhaengigVerwaltetWerden()
        {
            var c = new PlayerCurrencies();
            c.AddScraps(ScrapType.Common, 100);
            c.AddScraps(ScrapType.Rare, 10);
            Assert.AreEqual(100, c.CommonScraps);
            Assert.AreEqual(10, c.RareScraps);
            Assert.AreEqual(0, c.EpicScraps);

            Assert.IsTrue(c.SpendScraps(ScrapType.Common, 60));
            Assert.AreEqual(40, c.CommonScraps);
            Assert.IsFalse(c.SpendScraps(ScrapType.Rare, 50));
            Assert.AreEqual(10, c.RareScraps);
        }
    }
}
