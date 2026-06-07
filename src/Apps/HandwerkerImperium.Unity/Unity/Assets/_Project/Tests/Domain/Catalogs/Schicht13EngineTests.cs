using System;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.Events;
using HandwerkerImperium.Domain.LiveOps;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Catalogs
{
    /// <summary>
    /// Verifiziert die portierte Event-/Seasonal-/BattlePass-Engine (Schicht 13) gegen die
    /// Original-Werte (Avalonia Models/GameEvent.cs, SeasonalEvent.cs, BattlePass.cs).
    /// </summary>
    [TestFixture]
    public class Schicht13EngineTests
    {
        [Test]
        public void GameEvent_Create_MatchOriginal()
        {
            var sale = GameEvent.Create(GameEventType.MaterialSale, new Random(1));
            Assert.That(sale.Effect.CostMultiplier, Is.EqualTo(0.7m));
            Assert.That(sale.Effect.AffectedWorkshop, Is.Null);
            Assert.That(sale.Duration, Is.EqualTo(TimeSpan.FromHours(6)));

            var strike = GameEvent.Create(GameEventType.WorkerStrike, new Random(1));
            Assert.That(strike.Effect.SpecialEffect, Is.EqualTo("mood_drop_all_20"));
            Assert.That(strike.Effect.MarketRestriction, Is.EqualTo(WorkerTier.C));

            // HighDemand wählt einen Workshop deterministisch je Seed
            var hd1 = GameEvent.Create(GameEventType.HighDemand, new Random(7));
            var hd2 = GameEvent.Create(GameEventType.HighDemand, new Random(7));
            Assert.That(hd1.Effect.RewardMultiplier, Is.EqualTo(1.5m));
            Assert.That(hd1.Effect.AffectedWorkshop, Is.Not.Null);
            Assert.That(hd1.Effect.AffectedWorkshop, Is.EqualTo(hd2.Effect.AffectedWorkshop));
        }

        [Test]
        public void SeasonalEvent_CheckSeason_MatchOriginal()
        {
            Assert.That(SeasonalEvent.CheckSeason(new DateTime(2026, 3, 5)), Is.EqualTo((true, Season.Spring)));
            Assert.That(SeasonalEvent.CheckSeason(new DateTime(2026, 3, 14)), Is.EqualTo((true, Season.Spring)));
            Assert.That(SeasonalEvent.CheckSeason(new DateTime(2026, 3, 15)), Is.EqualTo((false, Season.Spring)));
            Assert.That(SeasonalEvent.CheckSeason(new DateTime(2026, 6, 10)), Is.EqualTo((true, Season.Summer)));
            Assert.That(SeasonalEvent.CheckSeason(new DateTime(2026, 9, 9)), Is.EqualTo((true, Season.Autumn)));
            Assert.That(SeasonalEvent.CheckSeason(new DateTime(2026, 12, 14)), Is.EqualTo((true, Season.Winter)));
            Assert.That(SeasonalEvent.CheckSeason(new DateTime(2026, 1, 1)).isActive, Is.False);
        }

        [Test]
        public void BattlePass_Xp_And_Tiers_MatchOriginal()
        {
            Assert.That(BattlePass.MaxTier, Is.EqualTo(50));
            Assert.That(BattlePass.SeasonDurationDays, Is.EqualTo(30));
            Assert.That(new BattlePass { CurrentTier = 0 }.XpForNextTier, Is.EqualTo(250));
            Assert.That(new BattlePass { CurrentTier = 39 }.XpForNextTier, Is.EqualTo(10000));
            Assert.That(new BattlePass { CurrentTier = 40 }.XpForNextTier, Is.EqualTo(20500));
            Assert.That(new BattlePass { SeasonNumber = 1 }.SeasonTheme, Is.EqualTo(Season.Summer));
            Assert.That(new BattlePass { SeasonNumber = 4 }.SeasonTheme, Is.EqualTo(Season.Spring));

            var bp = new BattlePass { CurrentTier = 0, CurrentXp = 0 };
            Assert.That(bp.AddXp(250), Is.EqualTo(1));
            Assert.That(bp.CurrentTier, Is.EqualTo(1));
            Assert.That(bp.AddXp(250), Is.EqualTo(0)); // Tier 1 braucht 500
            Assert.That(bp.CurrentXp, Is.EqualTo(250));
        }

        [Test]
        public void BattlePass_RewardGeneration_MatchOriginal()
        {
            var free = BattlePass.GenerateFreeRewards(10m); // baseMoney = 600
            Assert.That(free.Count, Is.EqualTo(50));
            Assert.That(free[0].MoneyReward, Is.EqualTo(600m));
            Assert.That(free[0].XpReward, Is.EqualTo(50));
            Assert.That(free[4].GoldenScrewReward, Is.EqualTo(3));
            Assert.That(free[34].GoldenScrewReward, Is.EqualTo(15)); // Meilenstein
            Assert.That(free[49].GoldenScrewReward, Is.EqualTo(50)); // Capstone

            var prem = BattlePass.GeneratePremiumRewards(10m, 1); // baseMoney 1800, season Summer
            Assert.That(prem.Count, Is.EqualTo(50));
            Assert.That(prem[0].MoneyReward, Is.EqualTo(1800m));
            Assert.That(prem[0].GoldenScrewReward, Is.EqualTo(3));
            Assert.That(prem[34].RewardType, Is.EqualTo(BattlePassRewardType.SpeedBoost));
            Assert.That(prem[34].SpeedBoostMinutes, Is.EqualTo(120));
            Assert.That(prem[44].SpeedBoostMinutes, Is.EqualTo(240));
            Assert.That(prem[49].GoldenScrewReward, Is.EqualTo(150));
            Assert.That(prem[49].DescriptionKey, Is.EqualTo("BPCapstoneSummer"));
        }
    }
}
