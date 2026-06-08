using System;
using NUnit.Framework;
using HandwerkerImperium.Domain.LiveOps;

namespace HandwerkerImperium.Domain.Tests.LiveOps
{
    /// <summary>
    /// Verifiziert die Tagesbelohnung: einkommens-skalierte Auszahlung (Basis-Floor, 15-Min-Cap) und
    /// die UTC-tagbasierte Streak-Auswertung (gleicher Tag/Folgetag/Lücke).
    /// </summary>
    [TestFixture]
    public class DailyRewardFormulasTests
    {
        private static readonly DateTime Day0 = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void GetScaledMoney_FloorAtBase_WhenIncomeLow()
        {
            Assert.That(DailyRewardFormulas.GetScaledMoney(1500m, 4, 0m), Is.EqualTo(1500m), "kein Einkommen -> Basis");
            // net=10, day=4: sqrt(4)*10*60=1200 < Basis 1500 -> Basis
            Assert.That(DailyRewardFormulas.GetScaledMoney(1500m, 4, 10m), Is.EqualTo(1500m));
        }

        [Test]
        public void GetScaledMoney_ScalesWithIncome_AndCapsAt15Min()
        {
            // net=100, day=4: sqrt(4)*100*60=12000 > Basis 1500 -> 12000
            Assert.That(DailyRewardFormulas.GetScaledMoney(1500m, 4, 100m), Is.EqualTo(12000m));
            // net=100, day=400: sqrt(400)*100*60=120000, Cap net*900=90000 -> 90000
            Assert.That(DailyRewardFormulas.GetScaledMoney(1m, 400, 100m), Is.EqualTo(90000m));
        }

        [Test]
        public void Evaluate_FirstClaim_StartsAtDayOne()
        {
            var r = DailyRewardFormulas.Evaluate(0, 0L, Day0.Ticks, 30);
            Assert.That(r.CanClaim, Is.True);
            Assert.That(r.Day, Is.EqualTo(1));
            Assert.That(r.StreakReset, Is.False);
        }

        [Test]
        public void Evaluate_SameDay_NoClaim()
        {
            var r = DailyRewardFormulas.Evaluate(3, Day0.Ticks, Day0.AddHours(5).Ticks, 30);
            Assert.That(r.CanClaim, Is.False);
            Assert.That(r.Day, Is.EqualTo(3));
        }

        [Test]
        public void Evaluate_NextDay_AdvancesStreak_WithWrap()
        {
            var r = DailyRewardFormulas.Evaluate(3, Day0.Ticks, Day0.AddDays(1).Ticks, 30);
            Assert.That(r.CanClaim, Is.True);
            Assert.That(r.Day, Is.EqualTo(4));
            Assert.That(r.StreakReset, Is.False);

            var wrap = DailyRewardFormulas.Evaluate(30, Day0.Ticks, Day0.AddDays(1).Ticks, 30);
            Assert.That(wrap.Day, Is.EqualTo(1), "nach Tag 30 wieder Tag 1");
        }

        [Test]
        public void Evaluate_Gap_ResetsToDayOne()
        {
            var r = DailyRewardFormulas.Evaluate(7, Day0.Ticks, Day0.AddDays(3).Ticks, 30);
            Assert.That(r.CanClaim, Is.True);
            Assert.That(r.Day, Is.EqualTo(1));
            Assert.That(r.StreakReset, Is.True);
        }
    }
}
