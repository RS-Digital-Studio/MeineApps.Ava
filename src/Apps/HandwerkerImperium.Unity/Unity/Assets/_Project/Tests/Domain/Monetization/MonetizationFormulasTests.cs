using NUnit.Framework;
using HandwerkerImperium.Domain.Monetization;

namespace HandwerkerImperium.Domain.Tests.Monetization
{
    /// <summary>
    /// Verifiziert die Monetarisierungs-Mathematik: Free-Cash, Verdopplung, Premium-Einkommens-/Offline-Effekte,
    /// Offline-Cap-Stapelung, Auto-Collect-Flag.
    /// </summary>
    [TestFixture]
    public class MonetizationFormulasTests
    {
        [Test]
        public void FreeCashReward_IncomeTimesBlockTimesMultiplier()
        {
            Assert.That(MonetizationFormulas.FreeCashReward(10m, 30.0, 2m), Is.EqualTo(600m));
            Assert.That(MonetizationFormulas.FreeCashReward(0m, 30.0, 2m), Is.EqualTo(0m));
            Assert.That(MonetizationFormulas.FreeCashReward(10m, 30.0, 0.5m), Is.EqualTo(300m), "Multiplikator < 1 -> 1");
        }

        [Test]
        public void DoubledReward_AndPremiumIncome()
        {
            Assert.That(MonetizationFormulas.DoubledReward(100m), Is.EqualTo(200m));
            Assert.That(MonetizationFormulas.DoubledReward(0m), Is.EqualTo(0m));
            Assert.That(MonetizationFormulas.PremiumIncomeMultiplier(true, 0.5m), Is.EqualTo(1.5m));
            Assert.That(MonetizationFormulas.PremiumIncomeMultiplier(false, 0.5m), Is.EqualTo(1m));
        }

        [Test]
        public void OfflineCapHours_StacksPremiumAndPerkboard()
        {
            Assert.That(MonetizationFormulas.OfflineCapHours(2, true, 14, 0), Is.EqualTo(16));
            Assert.That(MonetizationFormulas.OfflineCapHours(2, false, 14, 0), Is.EqualTo(2));
            Assert.That(MonetizationFormulas.OfflineCapHours(2, false, 14, 4), Is.EqualTo(6));
            Assert.That(MonetizationFormulas.OfflineCapHours(2, true, 14, 4), Is.EqualTo(20));
        }

        [Test]
        public void PremiumOfflineMultiplier_AndAutoCollect()
        {
            Assert.That(MonetizationFormulas.PremiumOfflineMultiplier(true, 2m), Is.EqualTo(2m));
            Assert.That(MonetizationFormulas.PremiumOfflineMultiplier(false, 2m), Is.EqualTo(1m));
            Assert.That(MonetizationFormulas.AutoCollectEnabled(true), Is.True);
            Assert.That(MonetizationFormulas.AutoCollectEnabled(false), Is.False);
        }
    }
}
