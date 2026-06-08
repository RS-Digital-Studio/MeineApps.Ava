using System;
using NUnit.Framework;
using HandwerkerImperium.Domain.LiveOps;

namespace HandwerkerImperium.Domain.Tests.LiveOps
{
    /// <summary>
    /// Verifiziert die Saison-Erkennung: aktiv in den ersten 14 Tagen von März/Juni/September/Dezember,
    /// sonst inaktiv (UTC-datumsbasiert).
    /// </summary>
    [TestFixture]
    public class SeasonalFormulasTests
    {
        [Test]
        public void TryGetActiveSeason_FirstFourteenDays_PerSeasonMonth()
        {
            Assert.That(Active(2026, 3, 10, out var sp), Is.True); Assert.That(sp, Is.EqualTo(Season.Spring));
            Assert.That(Active(2026, 3, 14, out _), Is.True);
            Assert.That(Active(2026, 6, 1, out var su), Is.True); Assert.That(su, Is.EqualTo(Season.Summer));
            Assert.That(Active(2026, 9, 5, out var au), Is.True); Assert.That(au, Is.EqualTo(Season.Autumn));
            Assert.That(Active(2026, 12, 14, out var wi), Is.True); Assert.That(wi, Is.EqualTo(Season.Winter));
        }

        [Test]
        public void TryGetActiveSeason_OutsideWindow_Inactive()
        {
            Assert.That(Active(2026, 3, 15, out _), Is.False, "Tag 15 ausserhalb");
            Assert.That(Active(2026, 1, 10, out _), Is.False, "Januar keine Saison");
            Assert.That(Active(2026, 12, 20, out _), Is.False);
            Assert.That(SeasonalFormulas.IsAnySeasonActive(new DateTime(2026, 6, 7, 0, 0, 0, DateTimeKind.Utc)), Is.True);
            Assert.That(SeasonalFormulas.IsAnySeasonActive(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)), Is.False);
        }

        private static bool Active(int y, int m, int d, out Season season) =>
            SeasonalFormulas.TryGetActiveSeason(new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc), out season);
    }
}
