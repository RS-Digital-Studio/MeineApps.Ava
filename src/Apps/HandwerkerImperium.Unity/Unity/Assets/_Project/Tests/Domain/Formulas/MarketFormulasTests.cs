using System;
using HandwerkerImperium.Domain.Common;
using HandwerkerImperium.Domain.Crafting;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.Events;
using HandwerkerImperium.Domain.State;
using HandwerkerImperium.Domain.Warehouse;
using NUnit.Framework;
using ResearchModel = HandwerkerImperium.Domain.Research.Research;

namespace HandwerkerImperium.Domain.Tests.Formulas
{
    /// <summary>
    /// Verifiziert den Service-Formel-Extrakt MarketFormulas (aus MarketService) gegen die
    /// Original-Markt-Mathematik: deterministischer Tages-Preisfaktor (StableHash-Seed, ±50% Sinus),
    /// Event-Modulatoren (3x/2x), 5% Spread, Markt-Freischaltung.
    /// </summary>
    [TestFixture]
    public class MarketFormulasTests
    {
        private static readonly DateTime Utc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);

        private static (string prod, CraftingRecipe rec) FirstProductWithRecipe()
        {
            foreach (var kv in CraftingProduct.GetAllProducts())
            {
                var r = CraftingRecipe.GetByOutputProduct(kv.Key);
                if (r != null) return (kv.Key, r);
            }
            Assert.Fail("Kein Produkt mit Rezept gefunden");
            return (null, null);
        }

        [Test]
        public void BuyPrice_BaseFormula_And_Band()
        {
            var (prod, _) = FirstProductWithRecipe();
            var state = GameState.CreateNew();
            state.PlayerGuid = "test-player-guid-abc";

            decimal baseVal = CraftingProduct.GetAllProducts()[prod].BaseValue;
            double factor = MarketFormulas.ComputeDailyFactor(state.PlayerGuid, prod, Utc);

            Assert.That(factor, Is.InRange(1.0 - MarketFormulas.DailyAmplitude, 1.0 + MarketFormulas.DailyAmplitude));
            Assert.That(MarketFormulas.GetBuyPrice(state, prod, Utc),
                Is.EqualTo(Math.Max(1m, Math.Round(baseVal * (decimal)factor))));
        }

        [Test]
        public void EventModulators_TripleAndDouble_OnlyOnMatchingWorkshop()
        {
            var (prod, rec) = FirstProductWithRecipe();
            var state = GameState.CreateNew();
            state.PlayerGuid = "test-player-guid-abc";
            decimal baseVal = CraftingProduct.GetAllProducts()[prod].BaseValue;
            double factor = MarketFormulas.ComputeDailyFactor(state.PlayerGuid, prod, Utc);
            decimal plain = Math.Max(1m, Math.Round(baseVal * (decimal)factor));

            var shortage = GameState.CreateNew();
            shortage.PlayerGuid = state.PlayerGuid;
            shortage.ActiveEvent = new GameEvent { Type = GameEventType.MaterialShortage, Effect = new GameEventEffect { AffectedWorkshop = rec.WorkshopType } };
            Assert.That(MarketFormulas.GetBuyPrice(shortage, prod, Utc), Is.EqualTo(Math.Max(1m, Math.Round(baseVal * (decimal)factor * 3m))));

            var demand = GameState.CreateNew();
            demand.PlayerGuid = state.PlayerGuid;
            demand.ActiveEvent = new GameEvent { Type = GameEventType.HighDemand, Effect = new GameEventEffect { AffectedWorkshop = rec.WorkshopType } };
            Assert.That(MarketFormulas.GetBuyPrice(demand, prod, Utc), Is.EqualTo(Math.Max(1m, Math.Round(baseVal * (decimal)factor * 2m))));

            var otherWs = rec.WorkshopType == WorkshopType.Carpenter ? WorkshopType.Plumber : WorkshopType.Carpenter;
            var mismatch = GameState.CreateNew();
            mismatch.PlayerGuid = state.PlayerGuid;
            mismatch.ActiveEvent = new GameEvent { Type = GameEventType.MaterialShortage, Effect = new GameEventEffect { AffectedWorkshop = otherWs } };
            Assert.That(MarketFormulas.GetBuyPrice(mismatch, prod, Utc), Is.EqualTo(plain));
        }

        [Test]
        public void SellPrice_AppliesSpread()
        {
            var (prod, _) = FirstProductWithRecipe();
            var state = GameState.CreateNew();
            state.PlayerGuid = "test-player-guid-abc";
            decimal buy = MarketFormulas.GetBuyPrice(state, prod, Utc);
            Assert.That(MarketFormulas.GetSellPrice(state, prod, Utc), Is.EqualTo(Math.Round(buy * (1m - MarketFormulas.SpreadFactor))));
        }

        [Test]
        public void DailyFactor_Deterministic_BySeedNotIdentity()
        {
            var (prod, _) = FirstProductWithRecipe();
            const string guid = "test-player-guid-abc";
            double a = MarketFormulas.ComputeDailyFactor(guid, prod, Utc);
            double b = MarketFormulas.ComputeDailyFactor(guid, prod, Utc);
            Assert.That(b, Is.EqualTo(a), "Gleicher Seed muss exakt gleichen Faktor liefern");
            Assert.That(MarketFormulas.ComputeDailyFactor("voellig-anderer-guid-xyz", prod, Utc), Is.Not.EqualTo(a));
            Assert.That(StableHash.Compute("logi_05"), Is.EqualTo(StableHash.Compute("logi_05")));
            Assert.That(StableHash.Compute(""), Is.EqualTo(0));
        }

        [Test]
        public void Series24h_And_Trend_And_Unknown()
        {
            var (prod, _) = FirstProductWithRecipe();
            var state = GameState.CreateNew();
            state.PlayerGuid = "test-player-guid-abc";

            var series = MarketFormulas.Get24hPriceSeries(state, prod, Utc);
            Assert.That(series.Length, Is.EqualTo(24));
            foreach (var p in series) Assert.That(p, Is.GreaterThanOrEqualTo(1m));

            decimal baseVal = CraftingProduct.GetAllProducts()[prod].BaseValue;
            double f0 = MarketFormulas.ComputeDailyFactor(state.PlayerGuid, prod, Utc.Date);
            Assert.That(series[0], Is.EqualTo(Math.Max(1m, Math.Round(baseVal * (decimal)f0))));

            Assert.That(MarketFormulas.GetPriceTrend(state, prod, Utc), Is.InRange(-1.0, 1.0));
            Assert.That(MarketFormulas.GetBuyPrice(state, "does_not_exist", Utc), Is.EqualTo(0m));
            Assert.That(MarketFormulas.Get24hPriceSeries(state, "does_not_exist", Utc).Length, Is.EqualTo(24));
        }

        [Test]
        public void IsMarketAvailable_PremiumOrResearchOrNoGating()
        {
            var prem = GameState.CreateNew(); prem.IsPremium = true;
            Assert.That(MarketFormulas.IsMarketAvailable(prem), Is.True);

            var noRes = GameState.CreateNew(); noRes.IsPremium = false;
            Assert.That(MarketFormulas.IsMarketAvailable(noRes, true), Is.False);
            Assert.That(MarketFormulas.IsMarketAvailable(noRes, false), Is.True);

            var withRes = GameState.CreateNew(); withRes.IsPremium = false;
            withRes.Researches.Add(new ResearchModel { Id = "logi_05", IsResearched = true });
            Assert.That(MarketFormulas.IsMarketAvailable(withRes, true), Is.True);

            var notDone = GameState.CreateNew(); notDone.IsPremium = false;
            notDone.Researches.Add(new ResearchModel { Id = "logi_05", IsResearched = false });
            Assert.That(MarketFormulas.IsMarketAvailable(notDone, true), Is.False);
        }
    }
}
