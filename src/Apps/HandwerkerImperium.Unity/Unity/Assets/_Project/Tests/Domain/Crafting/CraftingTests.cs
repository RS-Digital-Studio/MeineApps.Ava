using System;
using System.Linq;
using HandwerkerImperium.Domain.Crafting;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Crafting
{
    /// <summary>
    /// Verifiziert den portierten Crafting-Katalog (CraftingRecipe/Product/Job) gegen die
    /// Original-Werte (Avalonia Models/CraftingRecipe.cs) inkl. Katalog-Integrität.
    /// </summary>
    [TestFixture]
    public class CraftingTests
    {
        [Test]
        public void Catalog_Counts_MatchOriginal()
        {
            Assert.That(CraftingRecipe.GetAllRecipes().Count, Is.EqualTo(33));
            Assert.That(CraftingProduct.GetAllProducts().Count, Is.EqualTo(33));
            Assert.That(CraftingRecipe.GetAllRecipes().Count(r => r.Tier == 4), Is.EqualTo(3));
            Assert.That(CraftingProduct.GetAllProducts().Values.Count(p => p.Tier == 1), Is.EqualTo(10));
        }

        [Test]
        public void Lookups_And_Values_MatchOriginal()
        {
            var villa = CraftingRecipe.GetById("r_villa");
            Assert.That(villa, Is.Not.Null);
            Assert.That(villa!.Tier, Is.EqualTo(4));
            Assert.That(villa.RequiredWorkshopLevel, Is.EqualTo(500));
            Assert.That(villa.OutputProductId, Is.EqualTo("villa"));
            Assert.That(villa.InputProducts["luxury_furniture"], Is.EqualTo(5));
            Assert.That(villa.InputProducts["smart_home"], Is.EqualTo(3));

            var furn = CraftingRecipe.GetByOutputProduct("furniture");
            Assert.That(furn!.Id, Is.EqualTo("r_furniture"));
            Assert.That(furn.InputProducts["planks"], Is.EqualTo(3));
            Assert.That(furn.InputProducts["paint_mix"], Is.EqualTo(1));

            Assert.That(CraftingProduct.GetAllProducts()["villa"].BaseValue, Is.EqualTo(2_500_000m));
            Assert.That(CraftingProduct.GetAllProducts()["villa"].IsHeirloomEligible, Is.True);
            Assert.That(CraftingProduct.GetAllProducts()["planks"].BaseValue, Is.EqualTo(500m));
        }

        [Test]
        public void EffectiveInputs_RespectCrossWorkshopOnboarding()
        {
            var furn = CraftingRecipe.GetByOutputProduct("furniture")!;
            // < Lv100: nur eigene Workshop-Inputs (planks), kein Cross (paint_mix von Painter)
            var below = CraftingRecipe.GetEffectiveInputs(furn, 50);
            Assert.That(below.ContainsKey("planks"), Is.True);
            Assert.That(below.ContainsKey("paint_mix"), Is.False);
            // >= Lv100: alle Inputs
            var above = CraftingRecipe.GetEffectiveInputs(furn, 100);
            Assert.That(above.ContainsKey("planks"), Is.True);
            Assert.That(above.ContainsKey("paint_mix"), Is.True);
        }

        [Test]
        public void Catalog_Integrity_AllReferencesResolve()
        {
            var products = CraftingProduct.GetAllProducts();
            foreach (var r in CraftingRecipe.GetAllRecipes())
            {
                Assert.That(products.ContainsKey(r.OutputProductId), Is.True, $"Output ohne Produkt: {r.Id}");
                foreach (var input in r.InputProducts.Keys)
                    Assert.That(products.ContainsKey(input), Is.True, $"Input ohne Produkt: {r.Id} -> {input}");
            }
            // Jedes Produkt ist craftbar (hat ein produzierendes Rezept)
            foreach (var p in products.Values)
                Assert.That(CraftingRecipe.GetByOutputProduct(p.Id), Is.Not.Null, $"Produkt ohne Rezept: {p.Id}");
        }

        [Test]
        public void CraftingJob_Progress_And_Completion()
        {
            var fresh = new CraftingJob { StartedAt = DateTime.UtcNow, DurationSeconds = 60 };
            Assert.That(fresh.IsComplete, Is.False);
            Assert.That(fresh.Progress, Is.LessThan(0.1));

            var done = new CraftingJob { StartedAt = DateTime.UtcNow.AddSeconds(-120), DurationSeconds = 60 };
            Assert.That(done.IsComplete, Is.True);
            Assert.That(done.Progress, Is.EqualTo(1.0));
            Assert.That(done.TimeRemaining, Is.EqualTo(TimeSpan.Zero));
        }
    }
}
