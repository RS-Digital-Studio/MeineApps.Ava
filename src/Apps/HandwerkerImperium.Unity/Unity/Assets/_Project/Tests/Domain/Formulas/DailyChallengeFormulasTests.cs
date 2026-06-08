using System;
using HandwerkerImperium.Domain.LiveOps;
using HandwerkerImperium.Domain.Orders;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Formulas
{
    /// <summary>
    /// Verifiziert den Service-Formel-Extrakt DailyChallengeFormulas (aus DailyChallengeService) gegen die
    /// Original-Mathematik: Tier-Kurve, verfuegbare Typen je Tier, Einkommens-Basis, Challenge-Factory
    /// (Zielwerte/Belohnungen), Alle-fertig-Bonus und Rating-Score-Mapping.
    /// </summary>
    [TestFixture]
    public class DailyChallengeFormulasTests
    {
        [Test]
        public void Tier_And_BonusScrews_And_Count()
        {
            Assert.That(DailyChallengeFormulas.GetTier(5), Is.EqualTo(0));
            Assert.That(DailyChallengeFormulas.GetTier(6), Is.EqualTo(1));
            Assert.That(DailyChallengeFormulas.GetTier(100), Is.EqualTo(4));
            Assert.That(DailyChallengeFormulas.GetTier(300), Is.EqualTo(5));
            Assert.That(DailyChallengeFormulas.GetTier(751), Is.EqualTo(8));

            Assert.That(DailyChallengeFormulas.CalculateAllCompletedBonusScrews(100), Is.EqualTo(6));
            Assert.That(DailyChallengeFormulas.CalculateAllCompletedBonusScrews(300), Is.EqualTo(8));
            Assert.That(DailyChallengeFormulas.CalculateAllCompletedBonusScrews(1000), Is.EqualTo(15));

            Assert.That(DailyChallengeFormulas.CalculateChallengeCount(0), Is.EqualTo(3));
            Assert.That(DailyChallengeFormulas.CalculateChallengeCount(1), Is.EqualTo(4));
        }

        [Test]
        public void AvailableTypes_PerTier()
        {
            Assert.That(DailyChallengeFormulas.GetAvailableTypesForTier(0).Count, Is.EqualTo(7));
            Assert.That(DailyChallengeFormulas.GetAvailableTypesForTier(5).Count, Is.EqualTo(11));
            Assert.That(DailyChallengeFormulas.GetAvailableTypesForTier(6).Count, Is.EqualTo(14));
            Assert.That(DailyChallengeFormulas.GetAvailableTypesForTier(7).Count, Is.EqualTo(15));
            Assert.That(DailyChallengeFormulas.GetAvailableTypesForTier(8).Count, Is.EqualTo(15));
        }

        [Test]
        public void IncomeBase_LevelFloorAndNet()
        {
            Assert.That(DailyChallengeFormulas.CalculateIncomeBase(10, 0m), Is.EqualTo(300m));
            Assert.That(DailyChallengeFormulas.CalculateIncomeBase(10, 1m), Is.EqualTo(600m));
            Assert.That(DailyChallengeFormulas.CalculateIncomeBase(100, 0m), Is.EqualTo(5000m));
        }

        [Test]
        public void CreateChallenge_TargetsAndRewards()
        {
            var co = DailyChallengeFormulas.CreateChallenge(DailyChallengeType.CompleteOrders, 10, 0m, 0);
            Assert.That(co.TargetValue, Is.EqualTo(3L));
            Assert.That(co.MoneyReward, Is.EqualTo(240m));
            Assert.That(co.XpReward, Is.EqualTo(40));
            Assert.That(co.GoldenScrewReward, Is.EqualTo(2));

            var em = DailyChallengeFormulas.CreateChallenge(DailyChallengeType.EarnMoney, 10, 0m, 0);
            Assert.That(em.TargetValue, Is.EqualTo(200L));
            Assert.That(em.MoneyReward, Is.EqualTo(180m));

            var uw = DailyChallengeFormulas.CreateChallenge(DailyChallengeType.UpgradeWorkshop, 10, 0m, 0);
            Assert.That(uw.TargetValue, Is.EqualTo(2L));
            Assert.That(uw.MoneyReward, Is.EqualTo(300m));

            Assert.That(DailyChallengeFormulas.CreateChallenge(DailyChallengeType.HireWorker, 10, 0m, 0).TargetValue, Is.EqualTo(1L));
            Assert.That(DailyChallengeFormulas.CreateChallenge(DailyChallengeType.HireWorker, 400, 0m, 0).TargetValue, Is.EqualTo(2L));

            // ReachWorkshopLevel L1000 (tier8): highest 200 + 50 = 250; incomeBase 500000 * 1.5 = 750000
            var rw = DailyChallengeFormulas.CreateChallenge(DailyChallengeType.ReachWorkshopLevel, 1000, 0m, 200);
            Assert.That(rw.TargetValue, Is.EqualTo(250L));
            Assert.That(rw.MoneyReward, Is.EqualTo(750000m));
            Assert.That(rw.GoldenScrewReward, Is.EqualTo(6));
        }

        [Test]
        public void RatingToScorePercent()
        {
            Assert.That(DailyChallengeFormulas.RatingToScorePercent(MiniGameRating.Perfect), Is.EqualTo(100));
            Assert.That(DailyChallengeFormulas.RatingToScorePercent(MiniGameRating.Good), Is.EqualTo(75));
            Assert.That(DailyChallengeFormulas.RatingToScorePercent(MiniGameRating.Ok), Is.EqualTo(50));
        }
    }
}
