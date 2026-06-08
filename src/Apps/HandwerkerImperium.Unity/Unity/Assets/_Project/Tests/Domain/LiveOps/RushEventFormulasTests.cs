using NUnit.Framework;
using HandwerkerImperium.Domain.LiveOps;

namespace HandwerkerImperium.Domain.Tests.LiveOps
{
    /// <summary>
    /// Verifiziert das Rush-Event: Start-Bedingung, aktive Phase + Multiplikator, Ablauf und Cooldown-Sperre.
    /// </summary>
    [TestFixture]
    public class RushEventFormulasTests
    {
        private const long TicksPerSecond = 10_000_000L;

        [Test]
        public void Start_ActivatesRush_WithMultiplier()
        {
            var s = new RushEventState();
            long now = 5_000_000_000_000L;
            Assert.That(RushEventFormulas.CanStart(s, now), Is.True);
            Assert.That(RushEventFormulas.Start(s, 2m, 30, 86400, now), Is.True);
            Assert.That(RushEventFormulas.IsActive(s, now), Is.True);
            Assert.That(RushEventFormulas.CurrentMultiplier(s, now), Is.EqualTo(2m));
        }

        [Test]
        public void Rush_Expires_AndEntersCooldown()
        {
            var s = new RushEventState();
            long now = 5_000_000_000_000L;
            RushEventFormulas.Start(s, 2m, 30, 86400, now);

            long after = now + 31L * TicksPerSecond;
            Assert.That(RushEventFormulas.IsActive(s, after), Is.False);
            Assert.That(RushEventFormulas.ExpireIfDue(s, after), Is.True);
            Assert.That(RushEventFormulas.CurrentMultiplier(s, after), Is.EqualTo(1m));
            // Cooldown 1 Tag -> kurz nach Ablauf noch gesperrt
            Assert.That(RushEventFormulas.IsOnCooldown(s, after), Is.True);
            Assert.That(RushEventFormulas.CanStart(s, after), Is.False);
            // Nach Cooldown wieder startbar
            long afterCooldown = now + 86401L * TicksPerSecond;
            Assert.That(RushEventFormulas.CanStart(s, afterCooldown), Is.True);
        }

        [Test]
        public void Start_ClampsMultiplierToOne()
        {
            var s = new RushEventState();
            RushEventFormulas.Start(s, 0.5m, 10, 60, 0);
            Assert.That(s.Multiplier, Is.EqualTo(1m));
        }

        [Test]
        public void CannotStart_WhileActive()
        {
            var s = new RushEventState();
            long now = 1_000L * TicksPerSecond;
            RushEventFormulas.Start(s, 2m, 30, 60, now);
            Assert.That(RushEventFormulas.CanStart(s, now + 5L * TicksPerSecond), Is.False);
        }
    }
}
