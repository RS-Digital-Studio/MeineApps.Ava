#nullable enable
using System;
using ArcaneKingdom.Domain.SaisonPass;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class SaisonPassEngineTests
    {
        private static SaisonPassDefinition NewDef() => new()
        {
            Id = "test",
            Number = 1,
            StartedAtUtc = DateTime.UtcNow,
            EndsAtUtc = DateTime.UtcNow.AddDays(30),
            TotalTiers = 50,
            XpPerTier = 1000,
            HardCapTier = 100,
            FreeTrack =
            {
                new SaisonPassTierReward { Tier = 1, RewardKind = "Currency", SubType = "Gold", Amount = 100 },
                new SaisonPassTierReward { Tier = 5, RewardKind = "Scrap", SubType = "Common", Amount = 5 }
            },
            PremiumTrack =
            {
                new SaisonPassTierReward { Tier = 1, RewardKind = "Currency", SubType = "Diamond", Amount = 50 }
            }
        };

        [Test]
        public void TierForXpKorrekt()
        {
            var def = NewDef();
            Assert.AreEqual(0, SaisonPassEngine.TierForXp(500, def));
            Assert.AreEqual(1, SaisonPassEngine.TierForXp(1000, def));
            Assert.AreEqual(1, SaisonPassEngine.TierForXp(1500, def));
            Assert.AreEqual(2, SaisonPassEngine.TierForXp(2000, def));
        }

        [Test]
        public void TierWirdAufHardCapBegrenzt()
        {
            var def = NewDef();
            Assert.AreEqual(100, SaisonPassEngine.TierForXp(1_000_000, def));
        }

        [Test]
        public void XpRemainingZuNaechstemTier()
        {
            var def = NewDef();
            Assert.AreEqual(500, SaisonPassEngine.XpRemainingToNextTier(500, def));
            Assert.AreEqual(1000, SaisonPassEngine.XpRemainingToNextTier(1000, def));
            Assert.AreEqual(1, SaisonPassEngine.XpRemainingToNextTier(1999, def));
        }

        [Test]
        public void RewardsFromTierRangeBeruecksichtigtPremium()
        {
            var def = NewDef();
            var free = SaisonPassEngine.RewardsForTierRange(def, 0, 1, premiumActive: false);
            var premium = SaisonPassEngine.RewardsForTierRange(def, 0, 1, premiumActive: true);
            Assert.AreEqual(1, free.Count, "Nur Free-Track-Reward.");
            Assert.AreEqual(2, premium.Count, "Free + Premium-Track Reward.");
        }

        [Test]
        public void RewardsFromTierRangeMehrereTiers()
        {
            var def = NewDef();
            var rewards = SaisonPassEngine.RewardsForTierRange(def, 0, 5, premiumActive: false);
            Assert.AreEqual(2, rewards.Count, "Tier 1 + Tier 5 Free-Rewards.");
        }

        [Test]
        public void NegativesXpErgibtTier0()
        {
            var def = NewDef();
            Assert.AreEqual(0, SaisonPassEngine.TierForXp(-100, def));
        }
    }
}
