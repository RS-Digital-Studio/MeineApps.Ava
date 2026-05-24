#nullable enable
using System;
using ArcaneKingdom.Domain.Thief;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class ThiefRewardTests
    {
        [TestCase(0.005f, false, ThiefRewardTier.Pity)]
        [TestCase(0.03f,  false, ThiefRewardTier.Basic)]
        [TestCase(0.07f,  false, ThiefRewardTier.Standard)]
        [TestCase(0.20f,  false, ThiefRewardTier.Increased)]
        [TestCase(0.40f,  false, ThiefRewardTier.Premium)]
        [TestCase(0.001f, true,  ThiefRewardTier.TopAttacker)]
        public void TierForShareErgibtErwarteten(float share, bool isTopAttacker, ThiefRewardTier expected)
        {
            Assert.AreEqual(expected, ThiefRewardCalculator.TierForShare(share, isTopAttacker));
        }

        [Test]
        public void ContributionShareIstAnteiligAmGesamtSchaden()
        {
            var thief = new ActiveThief("t", ThiefType.Elite, 50, 1000,
                DateTime.UtcNow, DateTime.UtcNow.AddHours(2), "discoverer");
            thief.ApplyDamage("player_a", 300);
            thief.ApplyDamage("player_b", 700);
            Assert.AreEqual(0.30f, thief.ContributionShare("player_a"), 0.001f);
            Assert.AreEqual(0.70f, thief.ContributionShare("player_b"), 0.001f);
        }

        [Test]
        public void IsAliveFalseBeiHpZero()
        {
            var thief = new ActiveThief("t", ThiefType.Mysterious, 1, 500,
                DateTime.UtcNow, DateTime.UtcNow.AddHours(1), "discoverer");
            thief.ApplyDamage("p", 500);
            Assert.AreEqual(0, thief.CurrentHealth);
            Assert.IsFalse(thief.IsAlive);
        }

        [Test]
        public void HealthPercentIstKorrekt()
        {
            var thief = new ActiveThief("t", ThiefType.Mysterious, 1, 1000,
                DateTime.UtcNow, DateTime.UtcNow.AddHours(1), "discoverer");
            thief.ApplyDamage("p", 250);
            Assert.AreEqual(0.75f, thief.HealthPercent, 0.001f);
        }
    }
}
