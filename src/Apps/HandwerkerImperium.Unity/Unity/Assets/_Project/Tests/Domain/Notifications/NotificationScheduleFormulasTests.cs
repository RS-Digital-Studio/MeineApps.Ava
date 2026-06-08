using System;
using NUnit.Framework;
using HandwerkerImperium.Domain.Notifications;

namespace HandwerkerImperium.Domain.Tests.Notifications
{
    /// <summary>
    /// Verifiziert das Notification-Scheduling: Offline-Cap-Zeitpunkt, nächste UTC-Mitternacht, Verfügbarkeits-Zeit.
    /// </summary>
    [TestFixture]
    public class NotificationScheduleFormulasTests
    {
        private const long TicksPerSecond = 10_000_000L;

        [Test]
        public void OfflineCapFullAt_AddsCapHours()
        {
            long lastSeen = 5_000_000_000_000L;
            long expected = lastSeen + (long)(2 * 3600.0 * TicksPerSecond);
            Assert.That(NotificationScheduleFormulas.OfflineCapFullAt(lastSeen, 2.0), Is.EqualTo(expected));
        }

        [Test]
        public void NextUtcMidnight_IsNextDayZeroHour()
        {
            var now = new DateTime(2026, 6, 8, 15, 30, 0, DateTimeKind.Utc);
            var expected = new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(NotificationScheduleFormulas.NextUtcMidnight(now.Ticks), Is.EqualTo(expected.Ticks));
        }

        [Test]
        public void AvailableAt_NeverInPast()
        {
            long now = 1000L;
            Assert.That(NotificationScheduleFormulas.AvailableAt(2000L, now), Is.EqualTo(2000L));
            Assert.That(NotificationScheduleFormulas.AvailableAt(500L, now), Is.EqualTo(now), "Cooldown vorbei -> jetzt");
        }

        [Test]
        public void InSeconds_AddsRelative()
        {
            Assert.That(NotificationScheduleFormulas.InSeconds(1000L, 1.0), Is.EqualTo(1000L + TicksPerSecond));
        }
    }
}
