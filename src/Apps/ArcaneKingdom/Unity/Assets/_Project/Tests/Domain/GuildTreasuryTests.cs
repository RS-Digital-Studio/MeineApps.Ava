#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Guild;
using NUnit.Framework;

namespace ArcaneKingdom.Domain.Tests
{
    [TestFixture]
    public sealed class GuildTreasuryTests
    {
        [Test]
        public void DailyTickAggregiertBoniProGebiet()
        {
            var territories = new List<Territory>
            {
                new("t1", "k", TerritoryRarity.Common),
                new("t2", "k", TerritoryRarity.Epic),
                new("t3", "k", TerritoryRarity.Legendaer)
            };
            var tick = TerritoryBonusEngine.ComputeDailyBonus(territories, memberCount: 30);

            // Common 1k + Epic 8k + Legendaer 20k = 29k pro Mitglied
            // × 30 Mitglieder = 870k
            Assert.AreEqual(29_000 * 30, tick.AddedGold);
            // Diamonds: Common 0 + Epic 50 + Legendaer 100 = 150 pro Mitglied × 30 = 4500
            Assert.AreEqual(150 * 30, tick.AddedDiamonds);
        }

        [Test]
        public void ApplyMutiertTreasurySauber()
        {
            var treasury = new GuildTreasury();
            var tick = new TerritoryBonusEngine.DailyTickResult
            {
                AddedGold = 100_000,
                AddedDiamonds = 500,
                AddedScraps = new Dictionary<string, long> { ["Common"] = 50 }
            };
            var now = DateTime.UtcNow;
            TerritoryBonusEngine.Apply(treasury, tick, now);
            Assert.AreEqual(100_000, treasury.Gold);
            Assert.AreEqual(500, treasury.Diamonds);
            Assert.AreEqual(50, treasury.ScrapsByType["Common"]);
            Assert.AreEqual(now, treasury.LastTickAtUtc);
        }

        [Test]
        public void ApplyAkkumuliertBeiZweitemTick()
        {
            var treasury = new GuildTreasury { Gold = 50_000 };
            treasury.ScrapsByType["Common"] = 10;
            var tick = new TerritoryBonusEngine.DailyTickResult
            {
                AddedGold = 30_000,
                AddedScraps = new Dictionary<string, long> { ["Common"] = 5, ["Rare"] = 2 }
            };
            TerritoryBonusEngine.Apply(treasury, tick, DateTime.UtcNow);
            Assert.AreEqual(80_000, treasury.Gold);
            Assert.AreEqual(15, treasury.ScrapsByType["Common"]);
            Assert.AreEqual(2, treasury.ScrapsByType["Rare"]);
        }
    }
}
