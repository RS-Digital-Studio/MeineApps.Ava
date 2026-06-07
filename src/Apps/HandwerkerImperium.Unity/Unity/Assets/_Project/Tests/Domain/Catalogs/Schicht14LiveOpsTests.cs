using System;
using System.Linq;
using HandwerkerImperium.Domain.LiveOps;
using HandwerkerImperium.Domain.Orders;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Catalogs
{
    /// <summary>
    /// Verifiziert die portierten Schicht-14-Live-Ops (DailyReward, DailyChallenge, WeeklyMission,
    /// Tournament, ShopOffer) gegen die Original-Werte (Avalonia Models/*).
    /// </summary>
    [TestFixture]
    public class Schicht14LiveOpsTests
    {
        [Test]
        public void DailyReward_Schedule_And_Scaling()
        {
            var sched = DailyReward.GetRewardSchedule();
            Assert.That(sched.Count, Is.EqualTo(30));
            Assert.That(sched[0].Money, Is.EqualTo(500m));
            Assert.That(sched[6].BonusType, Is.EqualTo(DailyBonusType.SpeedBoost));
            Assert.That(sched[29].Money, Is.EqualTo(100_000m));
            Assert.That(sched[29].GoldenScrews, Is.EqualTo(25));

            Assert.That(new DailyReward { Day = 1, Money = 500m }.GetScaledMoney(0m), Is.EqualTo(500m));
            Assert.That(new DailyReward { Day = 1, Money = 500m }.GetScaledMoney(10m), Is.EqualTo(600m));
            Assert.That(new DailyReward { Day = 30, Money = 100_000m }.GetScaledMoney(10m), Is.EqualTo(100_000m));
        }

        [Test]
        public void DailyChallenge_And_WeeklyMission_Progress()
        {
            var dc = new DailyChallenge { TargetValue = 100, CurrentValue = 50 };
            Assert.That(dc.Progress, Is.EqualTo(0.5));
            Assert.That(dc.CanRetryWithAd, Is.True);
            Assert.That(new DailyChallenge { TargetValue = 100, CurrentValue = 100, IsCompleted = true }.CanRetryWithAd, Is.False);

            Assert.That(new WeeklyMission { TargetValue = 10, CurrentValue = 10 }.IsCompleted, Is.True);
            Assert.That(new WeeklyMission { TargetValue = 10, CurrentValue = 9 }.IsCompleted, Is.False);
            Assert.That(new WeeklyMission { TargetValue = 10, CurrentValue = 50 }.Progress, Is.EqualTo(1.0));
        }

        [Test]
        public void Tournament_RewardTier_And_Scores()
        {
            TournamentRewardTier RankTier(int rank)
            {
                var t = new Tournament();
                t.Leaderboard.Add(new TournamentLeaderboardEntry { IsPlayer = true, Rank = rank });
                return t.GetRewardTier();
            }
            Assert.That(RankTier(1), Is.EqualTo(TournamentRewardTier.Gold));
            Assert.That(RankTier(5), Is.EqualTo(TournamentRewardTier.Silver));
            Assert.That(RankTier(8), Is.EqualTo(TournamentRewardTier.Bronze));
            Assert.That(RankTier(10), Is.EqualTo(TournamentRewardTier.None));
            Assert.That(new Tournament().GetRewardTier(), Is.EqualTo(TournamentRewardTier.None));

            var t2 = new Tournament();
            t2.AddScore(100); t2.AddScore(300); t2.AddScore(200); t2.AddScore(50);
            Assert.That(t2.BestScores.Count, Is.EqualTo(3));
            Assert.That(t2.TotalScore, Is.EqualTo(600));
            Assert.That(t2.BestScores[0], Is.EqualTo(300));

            var a = Tournament.GenerateSimulatedOpponents(10, new Random(5));
            var b = Tournament.GenerateSimulatedOpponents(10, new Random(5));
            Assert.That(a.Count, Is.EqualTo(9));
            Assert.That(a.Select(o => o.Score), Is.EqualTo(b.Select(o => o.Score))); // deterministisch
            Assert.That(a[0].Name, Is.EqualTo("HandwerkerMax"));
        }

        [Test]
        public void ShopOffer_GenerateDaily()
        {
            var offer = ShopOffer.GenerateDaily(10m, new Random(3));
            var offer2 = ShopOffer.GenerateDaily(10m, new Random(3));
            Assert.That(offer.DiscountedPrice, Is.EqualTo(offer.OriginalPrice / 2));
            Assert.That(offer.Discount, Is.EqualTo(50));
            Assert.That(offer.ItemId, Is.EqualTo(offer2.ItemId)); // deterministisch
            Assert.That(new[] { "daily_screws_10", "daily_screws_25", "daily_money_boost", "daily_speed_boost" },
                Does.Contain(offer.ItemId));
        }
    }
}
