#nullable enable
using System;
using ArcaneKingdom.Domain.Hero;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class HeroCooldownTests
    {
        [Test]
        public void StartZustandIstBereit()
        {
            var cd = new HeroCooldown(5);
            Assert.IsTrue(cd.IsReady);
            Assert.AreEqual(0, cd.RemainingTurns);
        }

        [Test]
        public void AktivierenStartetCooldown()
        {
            var cd = new HeroCooldown(5);
            Assert.IsTrue(cd.TryActivate());
            Assert.IsFalse(cd.IsReady);
            Assert.AreEqual(5, cd.RemainingTurns);
        }

        [Test]
        public void AktivierenWaehrendCooldownGibtFalse()
        {
            var cd = new HeroCooldown(5);
            cd.TryActivate();
            Assert.IsFalse(cd.TryActivate());
            Assert.AreEqual(5, cd.RemainingTurns, "Restdauer bleibt unveraendert.");
        }

        [Test]
        public void TickRoundReduziertProRunde()
        {
            var cd = new HeroCooldown(3);
            cd.TryActivate();
            cd.TickRound();
            Assert.AreEqual(2, cd.RemainingTurns);
            cd.TickRound();
            Assert.AreEqual(1, cd.RemainingTurns);
            cd.TickRound();
            Assert.AreEqual(0, cd.RemainingTurns);
            Assert.IsTrue(cd.IsReady);
        }

        [Test]
        public void TickUnterNullClamptAuf0()
        {
            var cd = new HeroCooldown(2);
            cd.TickRound();
            cd.TickRound();
            cd.TickRound();
            Assert.AreEqual(0, cd.RemainingTurns);
        }

        [Test]
        public void UngueltigeKonfigurationWirftException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HeroCooldown(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HeroCooldown(-1));
        }

        [Test]
        public void StartWithCooldownWirdGeclamped()
        {
            var cd = new HeroCooldown(totalCooldown: 5, startWithCooldown: 10);
            Assert.AreEqual(5, cd.RemainingTurns, "startWithCooldown auf totalCooldown geclamped.");
        }
    }
}
