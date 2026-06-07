using System.Collections.Generic;
using System.Linq;
using HandwerkerImperium.Domain.Guild;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Guild
{
    /// <summary>
    /// Verifiziert die portierten Gilden-Kataloge (Research/Hall/Boss/Achievement) gegen die
    /// Original-Werte (Avalonia Models/Guild*.cs) inkl. Katalog-Integrität.
    /// </summary>
    [TestFixture]
    public class GuildCatalogTests
    {
        [Test]
        public void Research_Catalog_And_Effects_MatchOriginal()
        {
            var research = GuildResearchDefinition.GetAll();
            Assert.That(research.Count, Is.EqualTo(18));
            Assert.That(research.Select(r => r.Id).Distinct().Count(), Is.EqualTo(18));

            var byId = research.ToDictionary(r => r.Id);
            Assert.That(byId["guild_expand_1"].EffectValue, Is.EqualTo(5));
            Assert.That(byId["guild_income_4"].EffectValue, Is.EqualTo(0.15m));
            Assert.That(byId["guild_income_4"].Cost, Is.EqualTo(10_000_000_000));

            Assert.That(GuildResearchDefinition.GetResearchDurationHours(99_999_999), Is.EqualTo(1.0));
            Assert.That(GuildResearchDefinition.GetResearchDurationHours(100_000_000), Is.EqualTo(4.0));
            Assert.That(GuildResearchDefinition.GetResearchDurationHours(2_000_000_001), Is.EqualTo(12.0));

            var eff = GuildResearchEffects.Calculate(new HashSet<string>(research.Select(r => r.Id)));
            Assert.That(eff.MaxMembersBonus, Is.EqualTo(20));
            Assert.That(eff.IncomeBonus, Is.EqualTo(0.20m));
            Assert.That(eff.RewardBonus, Is.EqualTo(0.30m));
            Assert.That(eff.PrestigePointBonus, Is.EqualTo(0.10m));
            Assert.That(GuildResearchEffects.Calculate(new HashSet<string>()).IncomeBonus, Is.EqualTo(0m));
        }

        [Test]
        public void Hall_Catalog_And_Effects_MatchOriginal()
        {
            var hall = GuildBuildingDefinition.GetAll();
            Assert.That(hall.Count, Is.EqualTo(10));
            var byId = hall.ToDictionary(b => b.BuildingId);
            Assert.That(byId[GuildBuildingId.Workshop].EffectPerLevel, Is.EqualTo(0.02m));
            Assert.That(byId[GuildBuildingId.MasterThrone].UnlockHallLevel, Is.EqualTo(10));

            var c1 = byId[GuildBuildingId.Workshop].GetUpgradeCost(1);
            Assert.That(c1.GoldenScrews, Is.EqualTo(10));
            Assert.That(c1.GuildMoney, Is.EqualTo(500_000));
            var c2 = byId[GuildBuildingId.Workshop].GetUpgradeCost(2);
            Assert.That(c2.GoldenScrews, Is.EqualTo(20));
            Assert.That(c2.GuildMoney, Is.EqualTo(1_250_000));

            var he = GuildHallEffects.Calculate(new Dictionary<GuildBuildingId, int>
            {
                { GuildBuildingId.Workshop, 5 },
                { GuildBuildingId.AssemblyHall, 3 }
            });
            Assert.That(he.CraftingSpeedBonus, Is.EqualTo(0.10m));
            Assert.That(he.MaxMembersBonus, Is.EqualTo(6));
            // Level-Clamp auf MaxLevel
            var clamp = GuildHallEffects.Calculate(new Dictionary<GuildBuildingId, int> { { GuildBuildingId.Workshop, 99 } });
            Assert.That(clamp.CraftingSpeedBonus, Is.EqualTo(0.10m));
        }

        [Test]
        public void Boss_Catalog_MatchOriginal()
        {
            var bosses = GuildBossDefinition.GetAll();
            Assert.That(bosses.Count, Is.EqualTo(6));
            var byType = bosses.ToDictionary(b => b.BossType);

            var golem = byType[GuildBossType.StoneGolem];
            Assert.That(golem.HpPerLevel, Is.EqualTo(5_000));
            Assert.That(golem.DurationHours, Is.EqualTo(48));
            Assert.That(golem.CalculateHp(3), Is.EqualTo(15_000));
            Assert.That(golem.CalculateHp(0), Is.EqualTo(5_000)); // Max(1)

            var colossus = byType[GuildBossType.ClockworkColossus];
            Assert.That(colossus.HpPerLevel, Is.EqualTo(10_000));
            Assert.That(colossus.DurationHours, Is.EqualTo(24));
            Assert.That(colossus.CraftingDamageMultiplier, Is.EqualTo(1.5m));
            Assert.That(byType[GuildBossType.ShadowTrader].MoneyDonationDamageMultiplier, Is.EqualTo(3.0m));
        }

        [Test]
        public void Achievement_Catalog_MatchOriginal()
        {
            var achs = GuildAchievementDefinition.GetAll();
            Assert.That(achs.Count, Is.EqualTo(33)); // 11 Typen x 3 Tiers
            Assert.That(achs.Select(a => a.Id).Distinct().Count(), Is.EqualTo(33));

            Assert.That(achs.Count(a => a.Category == GuildAchievementCategory.StrongerTogether), Is.EqualTo(9));
            Assert.That(achs.Count(a => a.Category == GuildAchievementCategory.WarHeroes), Is.EqualTo(9));
            Assert.That(achs.Count(a => a.Category == GuildAchievementCategory.DragonSlayers), Is.EqualTo(9));
            Assert.That(achs.Count(a => a.Category == GuildAchievementCategory.Builders), Is.EqualTo(6));

            // Reward-Staffel pro Tier konsistent
            Assert.That(achs.Where(a => a.Tier == AchievementTier.Bronze).All(a => a.GoldenScrewReward == 5), Is.True);
            Assert.That(achs.Where(a => a.Tier == AchievementTier.Silver).All(a => a.GoldenScrewReward == 25), Is.True);
            Assert.That(achs.Where(a => a.Tier == AchievementTier.Gold).All(a => a.GoldenScrewReward == 50), Is.True);

            var byId = achs.ToDictionary(a => a.Id);
            Assert.That(byId["guild_ach_money_bronze"].Target, Is.EqualTo(100_000));
            Assert.That(byId["guild_ach_hall_gold"].Target, Is.EqualTo(10));
        }
    }
}
