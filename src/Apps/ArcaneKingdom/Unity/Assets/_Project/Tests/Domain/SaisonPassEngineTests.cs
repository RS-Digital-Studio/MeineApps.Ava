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

        // ============================================================
        // Non-lineare Kurve (Oekosystem v4 Kap. 4.1) — K7
        // ============================================================

        /// <summary>Definition mit der echten non-linearen Saison-1-Kurve (31 kumulative Schwellen).</summary>
        private static SaisonPassDefinition NewCurveDef() => new()
        {
            Id = "curve",
            Number = 1,
            StartedAtUtc = DateTime.UtcNow,
            EndsAtUtc = DateTime.UtcNow.AddDays(30),
            TotalTiers = 30,
            XpPerTier = 1167,
            HardCapTier = 30,
            XpThresholds =
            {
                0, 500, 1000, 1500, 2000, 2500, 3400, 4300, 5200, 6100, 7000,
                8000, 9000, 10000, 11000, 12000, 13200, 14400, 15600, 16800, 18000,
                19400, 20800, 22200, 23600, 25000, 27000, 29000, 31000, 33000, 35000
            }
        };

        [Test]
        public void KurveMeilensteineExakt()
        {
            var def = NewCurveDef();
            Assert.AreEqual(4, SaisonPassEngine.TierForXp(2499, def));
            Assert.AreEqual(5, SaisonPassEngine.TierForXp(2500, def));
            Assert.AreEqual(10, SaisonPassEngine.TierForXp(7000, def));
            Assert.AreEqual(15, SaisonPassEngine.TierForXp(12000, def));
            Assert.AreEqual(20, SaisonPassEngine.TierForXp(18000, def));
            Assert.AreEqual(25, SaisonPassEngine.TierForXp(25000, def));
            Assert.AreEqual(30, SaisonPassEngine.TierForXp(35000, def));
        }

        [Test]
        public void KurveZwischenstufenUndCap()
        {
            var def = NewCurveDef();
            Assert.AreEqual(0, SaisonPassEngine.TierForXp(499, def));
            Assert.AreEqual(1, SaisonPassEngine.TierForXp(500, def));
            Assert.AreEqual(6, SaisonPassEngine.TierForXp(3400, def));
            Assert.AreEqual(29, SaisonPassEngine.TierForXp(34999, def));
            Assert.AreEqual(30, SaisonPassEngine.TierForXp(999999, def));
        }

        [Test]
        public void KurveXpRemaining()
        {
            var def = NewCurveDef();
            Assert.AreEqual(500, SaisonPassEngine.XpRemainingToNextTier(0, def));
            Assert.AreEqual(900, SaisonPassEngine.XpRemainingToNextTier(2500, def));
            Assert.AreEqual(0, SaisonPassEngine.XpRemainingToNextTier(35000, def));
        }

        [Test]
        public void KurveXpForTierUndMaxXp()
        {
            var def = NewCurveDef();
            Assert.AreEqual(0, SaisonPassEngine.XpForTier(0, def));
            Assert.AreEqual(2500, SaisonPassEngine.XpForTier(5, def));
            Assert.AreEqual(35000, SaisonPassEngine.XpForTier(30, def));
            Assert.AreEqual(35000, SaisonPassEngine.MaxXp(def));
        }

        [Test]
        public void KurveProgressInTier()
        {
            var def = NewCurveDef();
            Assert.AreEqual((0, 900), SaisonPassEngine.ProgressInTier(2500, def));
            Assert.AreEqual((500, 900), SaisonPassEngine.ProgressInTier(3000, def));
            Assert.AreEqual((0, 500), SaisonPassEngine.ProgressInTier(0, def));
            Assert.AreEqual((0, 0), SaisonPassEngine.ProgressInTier(35000, def));
        }

        [Test]
        public void KurveIstMonoton()
        {
            var def = NewCurveDef();
            var prev = 0;
            for (var xp = 0; xp <= 35000; xp += 100)
            {
                var tier = SaisonPassEngine.TierForXp(xp, def);
                Assert.GreaterOrEqual(tier, prev, $"Tier darf bei xp={xp} nicht sinken.");
                prev = tier;
            }
        }

        [Test]
        public void LegacyFallbackOhneSchwellen()
        {
            // Def ohne XpThresholds -> lineare Berechnung (Abwaerts-Kompatibilitaet).
            var def = NewDef();
            Assert.AreEqual(2, SaisonPassEngine.TierForXp(2000, def));
            Assert.AreEqual(1000, SaisonPassEngine.XpRemainingToNextTier(1000, def));
        }
    }
}
