using System.Linq;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Achievements;
using HandwerkerImperium.Domain.LiveOps;
using HandwerkerImperium.Domain.Orders;
using NUnit.Framework;
using AchievementsCatalog = HandwerkerImperium.Domain.Achievements.Achievements;

namespace HandwerkerImperium.Domain.Tests.Catalogs
{
    /// <summary>
    /// Verifiziert die portierten Schicht-12-Kataloge (Achievements, Tool, Manager, MasterTool, LuckySpin)
    /// gegen die Original-Werte (Avalonia Models/*). Achievement-Werte wurden zusätzlich per Skript-Diff
    /// (109 Einträge, 545 Feld-Werte, 0 Abweichungen) gegen das Original geprüft.
    /// </summary>
    [TestFixture]
    public class Schicht12CatalogTests
    {
        [Test]
        public void Achievements_Catalog_MatchOriginal()
        {
            var achs = AchievementsCatalog.GetAll();
            Assert.That(achs.Count, Is.EqualTo(109));
            Assert.That(achs.Select(a => a.Id).Distinct().Count(), Is.EqualTo(109));
            Assert.That(achs.Count(a => a.Category == AchievementCategory.Workshops), Is.EqualTo(15));
            Assert.That(achs.Count(a => a.Category == AchievementCategory.Prestige), Is.EqualTo(14));

            var byId = achs.ToDictionary(a => a.Id);
            Assert.That(byId["first_order"].MoneyReward, Is.EqualTo(100m));
            Assert.That(byId["money_10b"].TargetValue, Is.EqualTo(10_000_000_000));
            Assert.That(byId["asc_perks_max"].GoldenScrewReward, Is.EqualTo(1000));
            Assert.That(byId["level_10"].XpReward, Is.EqualTo(0)); // Feedback-Loop-Schutz
        }

        [Test]
        public void Achievement_Progress_Computed()
        {
            var a = new Achievement { TargetValue = 100, CurrentValue = 50 };
            Assert.That(a.Progress, Is.EqualTo(50.0));
            Assert.That(a.ProgressFraction, Is.EqualTo(0.5));
            Assert.That(new Achievement { TargetValue = 100, CurrentValue = 76 }.IsCloseToUnlock, Is.True);
            Assert.That(new Achievement { TargetValue = 10, CurrentValue = 50 }.Progress, Is.EqualTo(100.0));
        }

        [Test]
        public void Tool_Catalog_And_Bonuses()
        {
            Assert.That(Tool.CreateDefaults().Count, Is.EqualTo(8));
            Assert.That(new Tool { Level = 0 }.UpgradeCostScrews, Is.EqualTo(5));
            Assert.That(new Tool { Level = 4 }.UpgradeCostScrews, Is.EqualTo(120));
            Assert.That(new Tool { Level = 5 }.ZoneBonus, Is.EqualTo(0.25));
            Assert.That(new Tool { Level = 5 }.TimeBonus, Is.EqualTo(15));
            Assert.That(new Tool { Type = ToolType.Saw }.RelatedMiniGame, Is.EqualTo(MiniGameType.Sawing));
            Assert.That(new Tool { Type = ToolType.Compass }.RelatedMiniGame, Is.EqualTo(MiniGameType.DesignPuzzle));
        }

        [Test]
        public void Manager_Catalog_And_Bonus()
        {
            Assert.That(Manager.GetAllDefinitions().Count, Is.EqualTo(14));
            Assert.That(Manager.GetDefinitionById("mgr_anna")!.RequiredPerfectRatings, Is.EqualTo(25));
            Assert.That(Manager.GetDefinitionById("mgr_kaiser")!.Workshop, Is.Null);
            Assert.That(Manager.GetDefinitionById("nope"), Is.Null);

            var mgr = new Manager { Id = "mgr_hans", IsUnlocked = true, Level = 3 };
            Assert.That(mgr.GetBonus(ManagerAbility.EfficiencyBoost), Is.EqualTo(0.15m));
            Assert.That(mgr.GetBonus(ManagerAbility.IncomeBoost), Is.EqualTo(0m)); // falsche Ability
            Assert.That(new Manager { Id = "mgr_hans", IsUnlocked = false, Level = 3 }.GetBonus(ManagerAbility.EfficiencyBoost), Is.EqualTo(0m));
            Assert.That(new Manager { Id = "mgr_weber", IsUnlocked = true, Level = 2 }.GetBonus(ManagerAbility.AutoCollectOrders), Is.EqualTo(2m));
        }

        [Test]
        public void MasterTool_Catalog_And_Eligibility()
        {
            var mts = MasterTool.GetAllDefinitions();
            Assert.That(mts.Count, Is.EqualTo(12));
            Assert.That(MasterTool.GetValidIds().Count, Is.EqualTo(12));
            Assert.That(MasterTool.GetTotalIncomeBonus(mts.Select(d => d.Id).ToList()), Is.EqualTo(0.74m));

            Assert.That(MasterTool.CheckEligibility("mt_golden_hammer", new MasterToolEligibilityContext { MaxWorkshopLevel = 75 }), Is.True);
            Assert.That(MasterTool.CheckEligibility("mt_golden_hammer", new MasterToolEligibilityContext { MaxWorkshopLevel = 74 }), Is.False);
            Assert.That(MasterTool.CheckEligibility("mt_titanium_pliers", new MasterToolEligibilityContext { TotalOrdersCompleted = 150 }), Is.True);
            Assert.That(MasterTool.CheckEligibility("mt_master_crown", new MasterToolEligibilityContext { CollectedMasterToolsCount = 11 }), Is.True);
            Assert.That(MasterTool.CheckEligibility("mt_master_crown", new MasterToolEligibilityContext { CollectedMasterToolsCount = 10 }), Is.False);
        }

        [Test]
        public void LuckySpin_Rewards()
        {
            Assert.That(LuckySpinPrize.CalculateReward(LuckySpinPrizeType.MoneyMedium, 10m).money, Is.EqualTo(3000m));
            Assert.That(LuckySpinPrize.CalculateReward(LuckySpinPrizeType.MoneyLarge, 10m).money, Is.EqualTo(6000m));
            Assert.That(LuckySpinPrize.CalculateReward(LuckySpinPrizeType.Jackpot50, 10m).screws, Is.EqualTo(50));
            Assert.That(LuckySpinPrize.CalculateReward(LuckySpinPrizeType.XpBoost, 10m).xp, Is.EqualTo(500));
            Assert.That(LuckySpinPrize.CalculateReward(LuckySpinPrizeType.MoneySmall, 0m).money, Is.EqualTo(500m));
        }
    }
}
