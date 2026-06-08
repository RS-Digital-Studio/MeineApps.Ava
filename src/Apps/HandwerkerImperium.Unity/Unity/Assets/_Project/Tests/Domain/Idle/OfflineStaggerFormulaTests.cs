using HandwerkerImperium.Domain.Offline;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Idle
{
    /// <summary>
    /// Verifiziert die pure Offline-Staffel-Formel (0.80/0.35/0.15/0.05) — die einzige aus dem
    /// alten Port übernommene Mathematik, die der Idle-Kern via IdleEconomyFormulas nutzt.
    /// </summary>
    [TestFixture]
    public class OfflineStaggerFormulaTests
    {
        [Test]
        public void StaggeredEarnings_MatchExpectedTiers()
        {
            // 1h@10/s = 3600*0.8*10 = 28800
            Assert.That(OfflineProgressFormulas.CalculateStaggeredEarnings(10m, 3600m), Is.EqualTo(28800m));
            // 8h@1/s = 7200*0.8 + 7200*0.35 + 14400*0.15 = 10440
            Assert.That(OfflineProgressFormulas.CalculateStaggeredEarnings(1m, 28800m), Is.EqualTo(10440m));
            // 10h@1/s = 10440 + 7200*0.05 = 10800
            Assert.That(OfflineProgressFormulas.CalculateStaggeredEarnings(1m, 36000m), Is.EqualTo(10800m));
        }
    }
}
