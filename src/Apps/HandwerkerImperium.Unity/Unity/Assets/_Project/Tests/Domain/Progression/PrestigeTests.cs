using System;
using System.Collections.Generic;
using System.Linq;
using HandwerkerImperium.Domain.Progression;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Progression
{
    /// <summary>
    /// Verifiziert das portierte Prestige-/Ascension-/EternalMastery-Subsystem gegen die
    /// Original-Werte (Avalonia Models/Enums/PrestigeTier.cs, PrestigeData.cs, AscensionPerk.cs,
    /// PrestigeShop.cs, SpeedrunRewards.cs + Services/EternalMasteryService.cs/PrestigeService.cs).
    /// </summary>
    [TestFixture]
    public class PrestigeTests
    {
        [Test]
        public void PrestigeTier_Values_MatchOriginal()
        {
            Assert.That(PrestigeTier.Bronze.GetRequiredLevel(), Is.EqualTo(30));
            Assert.That(PrestigeTier.Legende.GetRequiredLevel(), Is.EqualTo(1200));
            Assert.That(PrestigeTier.None.GetRequiredLevel(), Is.EqualTo(int.MaxValue));
            Assert.That(PrestigeTier.Legende.GetRequiredPreviousTierCount(), Is.EqualTo(3));
            Assert.That(PrestigeTier.Legende.GetPointMultiplier(), Is.EqualTo(64m));
            Assert.That(PrestigeTier.Bronze.GetPermanentMultiplierBonus(), Is.EqualTo(0.20m));
            Assert.That(PrestigeTier.Legende.GetPermanentMultiplierBonus(), Is.EqualTo(8.00m));
            Assert.That(PrestigeTier.Legende.GetTierStartMoney(), Is.EqualTo(25_000_000_000m));
            Assert.That(PrestigeTier.None.GetTierStartMoney(), Is.EqualTo(100m));
        }

        [Test]
        public void PrestigeTier_PreservationGates_MatchOriginal()
        {
            Assert.That(PrestigeTier.Silver.KeepsResearch(), Is.False);
            Assert.That(PrestigeTier.Gold.KeepsResearch(), Is.True);
            Assert.That(PrestigeTier.Platin.KeepsShopItems(), Is.True);
            Assert.That(PrestigeTier.Diamant.KeepsMasterTools(), Is.True);
            Assert.That(PrestigeTier.Meister.KeepsBuildings(), Is.True);
            Assert.That(PrestigeTier.Meister.KeepsEquipment(), Is.True);
            Assert.That(PrestigeTier.Legende.KeepsManagers(), Is.True);
            Assert.That(PrestigeTier.Meister.KeepsManagers(), Is.False);
            Assert.That(PrestigeTier.None.GetNextTier(), Is.EqualTo(PrestigeTier.Bronze));
            Assert.That(PrestigeTier.Legende.GetNextTier(), Is.EqualTo(PrestigeTier.None));
        }

        [Test]
        public void PrestigeChallenge_Bonuses_MatchOriginal()
        {
            Assert.That(PrestigeChallengeExtensions.MaxActiveChallenges, Is.EqualTo(3));
            Assert.That(PrestigeChallengeType.Spartaner.GetPpBonus(), Is.EqualTo(0.45m));
            Assert.That(PrestigeChallengeType.SoloMeister.GetPpBonus(), Is.EqualTo(0.50m));
            Assert.That(PrestigeChallengeType.KeinNetz.GetPpBonus(), Is.EqualTo(0.20m));

            var none = (IReadOnlyList<PrestigeChallengeType>)new List<PrestigeChallengeType>();
            Assert.That(none.GetTotalPpMultiplier(), Is.EqualTo(1.0m));
            var two = (IReadOnlyList<PrestigeChallengeType>)new List<PrestigeChallengeType>
            { PrestigeChallengeType.Spartaner, PrestigeChallengeType.Sprint };
            Assert.That(two.GetTotalPpMultiplier(), Is.EqualTo(1.80m));
        }

        [Test]
        public void PrestigeData_PointsAndAvailability_MatchOriginal()
        {
            Assert.That(PrestigeData.CalculatePrestigePoints(0), Is.EqualTo(0));
            Assert.That(PrestigeData.CalculatePrestigePoints(-5), Is.EqualTo(0));
            Assert.That(PrestigeData.CalculatePrestigePoints(100_000m), Is.EqualTo(1));
            Assert.That(PrestigeData.CalculatePrestigePoints(400_000m), Is.EqualTo(2));
            Assert.That(PrestigeData.CalculatePrestigePoints(1_000_000m), Is.EqualTo(3));

            var pd = new PrestigeData();
            Assert.That(pd.CanPrestige(PrestigeTier.Bronze, 30), Is.True);
            Assert.That(pd.CanPrestige(PrestigeTier.Bronze, 29), Is.False);
            Assert.That(pd.CanPrestige(PrestigeTier.Silver, 100), Is.False);
            pd.BronzeCount = 1;
            Assert.That(pd.CanPrestige(PrestigeTier.Silver, 100), Is.True);
            Assert.That(pd.GetHighestAvailableTier(100), Is.EqualTo(PrestigeTier.Silver));
            pd.SilverCount = 3; pd.GoldCount = 1;
            Assert.That(pd.TotalPrestigeCount, Is.EqualTo(5));
        }

        [Test]
        public void AscensionPerks_MatchOriginal()
        {
            var perks = AscensionPerk.GetAll();
            Assert.That(perks.Count, Is.EqualTo(6));
            Assert.That(perks.All(p => p.MaxLevel == 3), Is.True);

            var startCap = perks.First(p => p.Id == "asc_start_capital");
            Assert.That(startCap.CostsPerLevel, Is.EqualTo(new[] { 1, 3, 5 }));
            Assert.That(startCap.ValuesPerLevel, Is.EqualTo(new decimal[] { 1.00m, 5.00m, 10.00m }));
            Assert.That(startCap.GetCost(4), Is.EqualTo(int.MaxValue));
            Assert.That(startCap.GetValue(0), Is.EqualTo(0m));
            Assert.That(startCap.GetValue(5), Is.EqualTo(10.00m)); // Clamp auf MaxLevel
            Assert.That(perks.First(p => p.Id == "asc_legendary_reputation").ValuesPerLevel,
                Is.EqualTo(new decimal[] { 65m, 80m, 100m }));
        }

        [Test]
        public void PrestigeShop_Catalog_MatchesOriginal()
        {
            var items = PrestigeShop.GetAllItems();
            Assert.That(items.Count, Is.EqualTo(25));
            Assert.That(items.Count(i => i.IsRepeatable), Is.EqualTo(3));
            Assert.That(PrestigeShop.GetValidIds().Count, Is.EqualTo(25));
            Assert.That(PrestigeShop.IsValidId("pp_income_10"), Is.True);
            Assert.That(PrestigeShop.IsValidId("bogus"), Is.False);

            var inc100 = items.First(i => i.Id == "pp_income_100");
            Assert.That(inc100.Cost, Is.EqualTo(80));
            Assert.That(inc100.Effect.IncomeMultiplier, Is.EqualTo(1.00m));
            var resTier = items.First(i => i.Id == "pp_research_speed_tier");
            Assert.That(resTier.RequiredTier, Is.EqualTo(PrestigeTier.Diamant));
            Assert.That(resTier.Effect.ResearchSpeedBonus, Is.EqualTo(0.25m));
        }

        [Test]
        public void SpeedrunRewards_MatchOriginal()
        {
            Assert.That(SpeedrunRewards.CalculateReward(PrestigeTier.Bronze, TimeSpan.FromHours(1.5)), Is.EqualTo(5));
            Assert.That(SpeedrunRewards.CalculateReward(PrestigeTier.Bronze, TimeSpan.FromHours(0.5)), Is.EqualTo(15));
            Assert.That(SpeedrunRewards.CalculateReward(PrestigeTier.Bronze, TimeSpan.FromHours(3)), Is.EqualTo(0));
            Assert.That(SpeedrunRewards.CalculateReward(PrestigeTier.Bronze, TimeSpan.Zero), Is.EqualTo(0));
            Assert.That(SpeedrunRewards.CalculateReward(PrestigeTier.Legende, TimeSpan.FromHours(5)), Is.EqualTo(400));
            Assert.That(SpeedrunRewards.GetGoldBracketHours(PrestigeTier.Legende), Is.EqualTo(10.0));
        }

        [Test]
        public void EternalMastery_Bonus_MatchOriginal()
        {
            Assert.That(EternalMasteryFormulas.CalculateBonus(0), Is.EqualTo(0m));
            Assert.That(EternalMasteryFormulas.CalculateBonus(1), Is.EqualTo(0.005m));
            Assert.That(EternalMasteryFormulas.CalculateBonus(5), Is.EqualTo(0.05m));
            Assert.That(EternalMasteryFormulas.CalculateBonus(10), Is.EqualTo(0.15m));
            Assert.That(EternalMasteryFormulas.CalculateBonus(50), Is.EqualTo(0.75m));
            Assert.That(EternalMasteryFormulas.CalculateBonus(150), Is.EqualTo(1.05m)); // Soft-Cap
            Assert.That(EternalMasteryFormulas.PrestigesUntilNextTier(3), Is.EqualTo(2));
            Assert.That(EternalMasteryFormulas.PrestigesUntilNextMegaTier(7), Is.EqualTo(3));
        }

        [Test]
        public void PrestigeFormulas_MatchOriginal()
        {
            Assert.That(PrestigeFormulas.MaxPermanentMultiplier, Is.EqualTo(20.0m));

            var rep = PrestigeShop.GetAllItems().First(i => i.Id == "pp_income_repeatable");
            Assert.That(PrestigeFormulas.GetRepeatableItemCost(rep, 0), Is.EqualTo(15));
            Assert.That(PrestigeFormulas.GetRepeatableItemCost(rep, 2), Is.EqualTo(60));
            Assert.That(PrestigeFormulas.GetRepeatableItemCost(rep, 16),
                Is.EqualTo(PrestigeFormulas.GetRepeatableItemCost(rep, 15))); // Clamp 2^15

            Assert.That(PrestigeFormulas.CalculateDiminishedMultiplierBonus(0.20m, 0), Is.EqualTo(0.20m));
            Assert.That(PrestigeFormulas.CalculateDiminishedMultiplierBonus(0.20m, 5), Is.EqualTo(0.10m));
            Assert.That(PrestigeFormulas.ApplyDiminishedBonus(1.0m, 0.20m, 0), Is.EqualTo(1.20m));
            Assert.That(PrestigeFormulas.ApplyDiminishedBonus(19.95m, 0.20m, 0), Is.EqualTo(20.0m)); // Cap
        }
    }
}
