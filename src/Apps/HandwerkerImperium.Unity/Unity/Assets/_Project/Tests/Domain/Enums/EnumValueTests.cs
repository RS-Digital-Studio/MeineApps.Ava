using System;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Events;
using HandwerkerImperium.Domain.Achievements;
using HandwerkerImperium.Domain.Onboarding;
using HandwerkerImperium.Domain.Notifications;
using HandwerkerImperium.Domain.Guild;
using HandwerkerImperium.Domain.LiveOps;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Enums
{
    /// <summary>
    /// Verifiziert die portierten Wert-Enums (Schicht 10) gegen die Original-Werte — inkl.
    /// Persistenz-Integer-Stabilität (explizite Enum-Werte) und der Gameplay-Extensions.
    /// </summary>
    [TestFixture]
    public class EnumValueTests
    {
        [Test]
        public void EnumIntegerValues_StableForPersistence()
        {
            Assert.That((int)DailyChallengeType.AchieveMinigameScore, Is.EqualTo(6));
            Assert.That((int)DailyChallengeType.TrainWorker, Is.EqualTo(7));
            Assert.That((int)DailyChallengeType.CollectEquipment, Is.EqualTo(14));
            Assert.That((int)WeeklyMissionType.TrainWorkers, Is.EqualTo(7));
            Assert.That((int)WeeklyMissionType.CollectEquipment, Is.EqualTo(14));
            Assert.That((int)GameEventType.MaterialSale, Is.EqualTo(0));
            Assert.That((int)GameEventType.CelebrityEndorsement, Is.EqualTo(7));
            Assert.That((int)GameEventType.SpringSeason, Is.EqualTo(10));
            Assert.That((int)GameEventType.WinterSlowdown, Is.EqualTo(13));
            Assert.That((int)GuildMegaProjectType.Cathedral, Is.EqualTo(0));
            Assert.That((int)GuildMegaProjectType.Headquarters, Is.EqualTo(1));
            Assert.That((int)VipTier.Platinum, Is.EqualTo(4));
            Assert.That((int)BattlePassRewardType.SpeedBoost, Is.EqualTo(1));
            Assert.That((int)MiniGameMasteryTier.Gold, Is.EqualTo(3));
            Assert.That((int)DailyBonusType.FreeWorker, Is.EqualTo(3));
        }

        [Test]
        public void EnumMemberCounts_MatchOriginal()
        {
            Assert.That(Enum.GetValues(typeof(ToolType)).Length, Is.EqualTo(8));
            Assert.That(Enum.GetValues(typeof(ManagerAbility)).Length, Is.EqualTo(6));
            Assert.That(Enum.GetValues(typeof(DeliveryType)).Length, Is.EqualTo(6));
            Assert.That(Enum.GetValues(typeof(AchievementCategory)).Length, Is.EqualTo(17));
            Assert.That(Enum.GetValues(typeof(LuckySpinPrizeType)).Length, Is.EqualTo(8));
            Assert.That(Enum.GetValues(typeof(MasterToolRarity)).Length, Is.EqualTo(5));
            Assert.That(Enum.GetValues(typeof(Season)).Length, Is.EqualTo(4));
            Assert.That(Enum.GetValues(typeof(NotificationKind)).Length, Is.EqualTo(7));
            Assert.That(Enum.GetValues(typeof(HintPosition)).Length, Is.EqualTo(2));
        }

        [Test]
        public void VipTier_Extensions_MatchOriginal()
        {
            Assert.That(VipTier.Bronze.GetMinSpend(), Is.EqualTo(4.99m));
            Assert.That(VipTier.Platinum.GetMinSpend(), Is.EqualTo(49.99m));
            Assert.That(VipTier.None.GetMinSpend(), Is.EqualTo(decimal.MaxValue));
            Assert.That(VipTier.Platinum.GetIncomeBonus(), Is.EqualTo(0.05m));
            Assert.That(VipTier.Bronze.GetXpBonus(), Is.EqualTo(0m)); // erst ab Silver
            Assert.That(VipTier.Platinum.GetXpBonus(), Is.EqualTo(0.05m));
            Assert.That(VipTier.Platinum.GetCostReduction(), Is.EqualTo(0m)); // kein Pay-to-Win
            Assert.That(VipTier.Bronze.HasAutoClaimDailyRewards(), Is.True);
            Assert.That(VipTier.Silver.HasDeliveryTimer(), Is.True);
            Assert.That(VipTier.Bronze.HasDeliveryTimer(), Is.False);
            Assert.That(VipTier.Gold.HasExclusiveFrame(), Is.True);
            Assert.That(VipTierExtensions.DetermineVipTier(4.98m), Is.EqualTo(VipTier.None));
            Assert.That(VipTierExtensions.DetermineVipTier(4.99m), Is.EqualTo(VipTier.Bronze));
            Assert.That(VipTierExtensions.DetermineVipTier(50m), Is.EqualTo(VipTier.Platinum));
        }

        [Test]
        public void GameEventType_Extensions_MatchOriginal()
        {
            Assert.That(GameEventType.TaxAudit.IsRandom(), Is.True);
            Assert.That(GameEventType.SpringSeason.IsRandom(), Is.False);
            Assert.That(GameEventType.HighDemand.IsPositive(), Is.True);
            Assert.That(GameEventType.TaxAudit.IsPositive(), Is.False);
            Assert.That(GameEventType.TaxAudit.GetDefaultDuration(), Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(GameEventType.HighDemand.GetDefaultDuration(), Is.EqualTo(TimeSpan.FromHours(8)));
            Assert.That(GameEventType.SpringSeason.GetDefaultDuration(), Is.EqualTo(TimeSpan.FromHours(24)));
        }

        [Test]
        public void MiniGameMasteryThresholds_MatchOriginal()
        {
            Assert.That(MiniGameMasteryThresholds.GetTierForCount(49), Is.EqualTo(MiniGameMasteryTier.None));
            Assert.That(MiniGameMasteryThresholds.GetTierForCount(50), Is.EqualTo(MiniGameMasteryTier.Bronze));
            Assert.That(MiniGameMasteryThresholds.GetTierForCount(200), Is.EqualTo(MiniGameMasteryTier.Silver));
            Assert.That(MiniGameMasteryThresholds.GetTierForCount(1000), Is.EqualTo(MiniGameMasteryTier.Gold));
            Assert.That(MiniGameMasteryThresholds.GoldenScrewRewards, Is.EqualTo(new[] { 0, 5, 15, 50 }));
            Assert.That(MiniGameMasteryThresholds.GetThresholdForTier(MiniGameMasteryTier.Gold), Is.EqualTo(1000));
        }
    }
}
