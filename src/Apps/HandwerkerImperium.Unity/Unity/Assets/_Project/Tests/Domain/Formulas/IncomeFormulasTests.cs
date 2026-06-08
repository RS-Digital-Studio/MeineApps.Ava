using System;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.State;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.Research;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Formulas
{
    /// <summary>
    /// Verifiziert den Service-Formel-Extrakt IncomeFormulas (aus IncomeCalculatorService) gegen die
    /// Original-Mathematik. Service-Boni werden als Parameter übergeben; die Multiplikator-Ketten/Caps
    /// sind 1:1.
    /// </summary>
    [TestFixture]
    public class IncomeFormulasTests
    {
        private const decimal Tol = 0.0000001m;

        [Test]
        public void HeirloomBonus_MatchOriginal()
        {
            var s = new GameState();
            s.HeirloomItems.Add("villa"); s.HeirloomItems.Add("villa"); s.HeirloomItems.Add("skyscraper");
            s.Ascension.PermanentHeirlooms.Add("villa"); s.Ascension.PermanentHeirlooms.Add("villa");
            Assert.That(IncomeFormulas.GetTotalHeirloomBonus(s), Is.EqualTo(0.07m)); // 3*0.02 + 2*0.005
            Assert.That(IncomeFormulas.GetTotalHeirloomBonus(new GameState()), Is.EqualTo(0m));
        }

        [Test]
        public void PrestigeIncomeBonus_MatchOriginal()
        {
            var s = new GameState();
            s.Prestige.PurchasedShopItems.Add("pp_income_100");
            s.Prestige.RepeatableItemCounts["pp_income_repeatable"] = 3;
            Assert.That(IncomeFormulas.GetPrestigeIncomeBonus(s), Is.EqualTo(1.15m)); // 1.0 + 3*0.05
        }

        [Test]
        public void GrossIncome_AppliesMultipliers()
        {
            var s = GameState.CreateNew();
            decimal t = s.TotalIncomePerSecond;
            Assert.That(t, Is.GreaterThan(0m));
            // Nur Prestige 1.0 → 2x, alle anderen Boni 0/null, kein Premium
            decimal gross = IncomeFormulas.CalculateGrossIncome(s, 1.0m, null, null, 0m, 0m, 0m, 0m, 0m, false);
            Assert.That(Math.Abs(gross - t * 2.0m), Is.LessThanOrEqualTo(Tol));

            var prem = GameState.CreateNew(); prem.IsPremium = true;
            decimal grossPrem = IncomeFormulas.CalculateGrossIncome(prem, 0m, null, null, 0m, 0m, 0m, 0m, 0m, false);
            Assert.That(Math.Abs(grossPrem - prem.TotalIncomePerSecond * 1.5m), Is.LessThanOrEqualTo(Tol));
        }

        [Test]
        public void CraftingSellMultiplier_AppliesChain_And_Caps()
        {
            var mult = IncomeFormulas.CalculateCraftingSellMultiplier(new GameState(), 0.10m, 0.20m, 0m,
                new ResearchEffect { EfficiencyBonus = 0.10m }, null, 0.05m);
            Assert.That(Math.Abs(mult - (1.10m * 1.10m * 1.05m * 1.20m)), Is.LessThanOrEqualTo(Tol));

            var big = IncomeFormulas.CalculateCraftingSellMultiplier(new GameState { IsPremium = true }, 5.0m, 5.0m, 5.0m, null, null, 5.0m);
            Assert.That(big, Is.LessThanOrEqualTo(12.0m)); // Hard-Cap
            Assert.That(big, Is.GreaterThan(8.0m));        // Soft-Cap-Bereich
        }

        [Test]
        public void ApplySoftCap_TierThresholds()
        {
            var s = GameState.CreateNew();
            s.Prestige.CurrentTier = PrestigeTier.None; // Threshold 4
            decimal t = s.TotalIncomePerSecond;
            decimal capped = IncomeFormulas.ApplySoftCap(s, t * 10m);
            decimal expected = t * (4m + (decimal)Math.Log(1.0 + 6.0, 2.0));
            Assert.That(Math.Abs(capped - expected), Is.LessThanOrEqualTo(Tol));
            Assert.That(s.IsSoftCapActive, Is.True);

            var s2 = GameState.CreateNew();
            s2.Prestige.CurrentTier = PrestigeTier.Legende; // Threshold 20
            Assert.That(Math.Abs(IncomeFormulas.ApplySoftCap(s2, s2.TotalIncomePerSecond * 19m) - s2.TotalIncomePerSecond * 19m),
                Is.LessThanOrEqualTo(Tol)); // 19x < 20 → keine Dämpfung
        }
    }
}
