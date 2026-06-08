using System;
using NUnit.Framework;
using HandwerkerImperium.Domain.LiveOps;

namespace HandwerkerImperium.Domain.Tests.LiveOps
{
    /// <summary>
    /// Verifiziert die Tagesaufgaben-Logik: Abschluss/Fortschritt + UTC-Tagesreset.
    /// </summary>
    [TestFixture]
    public class DailyTaskFormulasTests
    {
        private static readonly DateTime Day0 = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void IsComplete_And_Progress()
        {
            Assert.That(DailyTaskFormulas.IsComplete(20, 20), Is.True);
            Assert.That(DailyTaskFormulas.IsComplete(19, 20), Is.False);
            Assert.That(DailyTaskFormulas.Progress01(5, 20), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(DailyTaskFormulas.Progress01(40, 20), Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void ShouldReset_OnNewUtcDay()
        {
            Assert.That(DailyTaskFormulas.ShouldReset(0L, Day0.Ticks), Is.True, "nie gesetzt -> reset");
            Assert.That(DailyTaskFormulas.ShouldReset(Day0.Ticks, Day0.AddHours(5).Ticks), Is.False, "gleicher UTC-Tag");
            Assert.That(DailyTaskFormulas.ShouldReset(Day0.Ticks, Day0.AddDays(1).Ticks), Is.True, "neuer UTC-Tag");
        }
    }
}
