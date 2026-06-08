using System;
using NUnit.Framework;
using HandwerkerImperium.Domain.Social;

namespace HandwerkerImperium.Domain.Tests.Social
{
    /// <summary>
    /// Verifiziert die Cross-Promo-Tagesrotation: deterministischer Index je UTC-Tag, Wechsel über Mitternacht.
    /// </summary>
    [TestFixture]
    public class CrossPromoFormulasTests
    {
        private static readonly DateTime Day0 = new DateTime(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void RotationIndex_DeterministicPerDay_InRange()
        {
            int idx = CrossPromoFormulas.RotationIndex(Day0.Ticks, 5);
            Assert.That(idx, Is.InRange(0, 4));
            Assert.That(CrossPromoFormulas.RotationIndex(Day0.Ticks, 5), Is.EqualTo(idx), "gleicher Tag -> gleicher Index");
            // Folgetag: Index rückt um 1 weiter (mod count)
            int next = CrossPromoFormulas.RotationIndex(Day0.AddDays(1).Ticks, 5);
            Assert.That(next, Is.EqualTo((idx + 1) % 5));
            Assert.That(CrossPromoFormulas.RotationIndex(Day0.Ticks, 1), Is.EqualTo(0));
        }

        [Test]
        public void ShouldRotate_OnNewUtcDay()
        {
            Assert.That(CrossPromoFormulas.ShouldRotate(0L, Day0.Ticks), Is.True);
            Assert.That(CrossPromoFormulas.ShouldRotate(Day0.Ticks, Day0.AddHours(3).Ticks), Is.False);
            Assert.That(CrossPromoFormulas.ShouldRotate(Day0.Ticks, Day0.AddDays(1).Ticks), Is.True);
        }
    }
}
