#nullable enable
using System;
using ArcaneKingdom.Domain.Season;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class ResetWindowTests
    {
        [Test]
        public void NextDailyResetIstNaechsteMitternachtUtc()
        {
            var now = new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Utc);
            var next = ResetWindow.NextDailyResetUtc(now);
            Assert.AreEqual(new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc), next);
        }

        [Test]
        public void NextDailyResetAm00UhrIstNaechsterTag()
        {
            var now = new DateTime(2026, 6, 15, 0, 0, 1, DateTimeKind.Utc);
            var next = ResetWindow.NextDailyResetUtc(now);
            Assert.AreEqual(new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc), next);
        }

        [Test]
        public void NextWeeklyResetIstNaechsterMontag()
        {
            // 2026-06-15 ist ein Montag
            var now = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc); // Mittwoch
            var next = ResetWindow.NextWeeklyResetUtc(now);
            Assert.AreEqual(DayOfWeek.Monday, next.DayOfWeek);
            Assert.AreEqual(new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc), next);
        }

        [Test]
        public void HasCrossedDailyResetTrueNachMitternacht()
        {
            var last = new DateTime(2026, 6, 15, 22, 0, 0, DateTimeKind.Utc);
            var now = new DateTime(2026, 6, 16, 1, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(ResetWindow.HasCrossedDailyReset(last, now));
        }

        [Test]
        public void HasCrossedDailyResetFalseAmGleichenTag()
        {
            var last = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
            var now = new DateTime(2026, 6, 15, 23, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(ResetWindow.HasCrossedDailyReset(last, now));
        }

        [Test]
        public void NextSeasonResetSindGenau30Tage()
        {
            var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = ResetWindow.NextSeasonResetUtc(start);
            Assert.AreEqual(start.AddDays(30), end);
        }
    }
}
