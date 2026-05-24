#nullable enable
using System.Collections.Generic;
using ArcaneKingdom.Domain.Achievement;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class AchievementProgressTests
    {
        private static readonly List<AchievementTier> Tiers = new()
        {
            new AchievementTier { Tier = 1, Threshold = 10,  TrophyPoints = 50 },
            new AchievementTier { Tier = 2, Threshold = 50,  TrophyPoints = 200 },
            new AchievementTier { Tier = 3, Threshold = 100, TrophyPoints = 500 },
            new AchievementTier { Tier = 4, Threshold = 500, TrophyPoints = 2000 }
        };

        [Test]
        public void EinzelnesTierWirdFreigeschaltet()
        {
            var p = new AchievementProgress("a");
            var newly = p.Advance(15, Tiers);
            Assert.AreEqual(1, newly.Count);
            Assert.AreEqual(1, newly[0].Tier);
            Assert.AreEqual(1, p.HighestTierUnlocked);
        }

        [Test]
        public void MehrereTiersAufEinmalMoeglich()
        {
            var p = new AchievementProgress("a");
            var newly = p.Advance(120, Tiers);
            Assert.AreEqual(3, newly.Count, "Sollte Tier 1+2+3 freigeschaltet haben.");
            Assert.AreEqual(3, p.HighestTierUnlocked);
        }

        [Test]
        public void TierWirdNichtDoppeltFreigeschaltet()
        {
            var p = new AchievementProgress("a");
            p.Advance(60, Tiers);            // Unlocked Tier 1 + 2
            var newly = p.Advance(10, Tiers); // Wert 70, kein neuer Tier (naechster bei 100)
            Assert.AreEqual(0, newly.Count);
        }

        [Test]
        public void CurrentValueAkkumuliertEinfach()
        {
            var p = new AchievementProgress("a");
            p.Advance(5, Tiers);
            p.Advance(3, Tiers);
            Assert.AreEqual(8, p.CurrentValue);
        }
    }
}
