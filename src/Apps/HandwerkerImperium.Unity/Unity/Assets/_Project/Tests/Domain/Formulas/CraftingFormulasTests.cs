using System;
using System.Collections.Generic;
using HandwerkerImperium.Domain.Crafting;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.State;
using NUnit.Framework;

namespace HandwerkerImperium.Domain.Tests.Formulas
{
    /// <summary>
    /// Verifiziert den Service-Formel-Extrakt CraftingFormulas (aus CraftingService) gegen die
    /// Original-Mathematik: verfuegbare Rezepte, Dauer-Reduktion (Cap 50%), Tier-1-Materialkosten,
    /// Level-Multiplikator (log2), Verkaufspreis, Material-Affinitaet und Prestige-Crafting-Speed.
    /// </summary>
    [TestFixture]
    public class CraftingFormulasTests
    {
        [Test]
        public void AvailableRecipes_LevelGated()
        {
            int carpenterTotal = 0;
            foreach (var r in CraftingRecipe.GetAllRecipes())
                if (r.WorkshopType == WorkshopType.Carpenter) carpenterTotal++;

            Assert.That(CraftingFormulas.GetAvailableRecipes(WorkshopType.Carpenter, 49).Count, Is.EqualTo(0));
            Assert.That(CraftingFormulas.GetAvailableRecipes(WorkshopType.Carpenter, 50).Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(CraftingFormulas.GetAvailableRecipes(WorkshopType.Carpenter, 9999).Count, Is.EqualTo(carpenterTotal));
        }

        [Test]
        public void EffectiveDuration_Capped()
        {
            Assert.That(CraftingFormulas.CalculateEffectiveDuration(100, 0m), Is.EqualTo(100));
            Assert.That(CraftingFormulas.CalculateEffectiveDuration(100, 0.25m), Is.EqualTo(75));
            Assert.That(CraftingFormulas.CalculateEffectiveDuration(100, 0.80m), Is.EqualTo(50)); // Cap 50%
            Assert.That(CraftingFormulas.CalculateEffectiveDuration(1, 0.5m), Is.EqualTo(1));      // Minimum 1
        }

        [Test]
        public void MaterialCost_And_LevelMultiplier_And_SellPrice()
        {
            Assert.That(CraftingFormulas.CalculateTier1MaterialCost(500m), Is.EqualTo(100m));
            Assert.That(CraftingFormulas.CalculateLevelMultiplier(0), Is.EqualTo(1.0m));
            Assert.That(CraftingFormulas.CalculateLevelMultiplier(15), Is.EqualTo(2.0m).Within(1e-7m)); // log2(1+1)=1
            Assert.That(CraftingFormulas.CalculateSellPrice(500m, 15, 1.0m), Is.EqualTo(1000m));
        }

        [Test]
        public void MaterialAffinityBonus_ProRataWorking()
        {
            CraftingRecipe affRecipe = null;
            MaterialAffinity aff = MaterialAffinity.None;
            foreach (var rcp in CraftingRecipe.GetAllRecipes())
            {
                var a = MaterialAffinityExtensions.GetMaterialAffinity(rcp.OutputProductId);
                if (a != MaterialAffinity.None) { affRecipe = rcp; aff = a; break; }
            }
            Assert.That(affRecipe, Is.Not.Null, "Kein Rezept mit Material-Affinitaet gefunden");

            var st = new GameState();
            st.Workshops.Clear();
            var ws = new Workshop { Type = affRecipe.WorkshopType };
            ws.Workers.Add(new Worker { AssignedWorkshop = affRecipe.WorkshopType, MaterialAffinity = aff });
            ws.Workers.Add(new Worker { AssignedWorkshop = affRecipe.WorkshopType, MaterialAffinity = MaterialAffinity.None });
            st.Workshops.Add(ws);
            Assert.That(CraftingFormulas.CalculateMaterialAffinityBonus(st, affRecipe), Is.EqualTo(0.10m)); // 0.20 * 1/2

            var st0 = new GameState();
            st0.Workshops.Clear();
            var ws0 = new Workshop { Type = affRecipe.WorkshopType };
            ws0.Workers.Add(new Worker { MaterialAffinity = aff }); // kein AssignedWorkshop -> nicht working
            st0.Workshops.Add(ws0);
            Assert.That(CraftingFormulas.CalculateMaterialAffinityBonus(st0, affRecipe), Is.EqualTo(0m));
        }

        [Test]
        public void PrestigeCraftingSpeedBonus_SumsNonRepeatable()
        {
            Assert.That(CraftingFormulas.CalculatePrestigeCraftingSpeedBonus(new GameState()), Is.EqualTo(0m));

            PrestigeShopItem speedItem = null;
            foreach (var it in PrestigeShop.GetAllItems())
                if (!it.IsRepeatable && it.Effect.CraftingSpeedBonus > 0) { speedItem = it; break; }
            Assert.That(speedItem, Is.Not.Null, "Kein nicht-wiederholbares Prestige-Item mit CraftingSpeedBonus gefunden");

            var st = new GameState();
            st.Prestige.PurchasedShopItems.Add(speedItem.Id);
            Assert.That(CraftingFormulas.CalculatePrestigeCraftingSpeedBonus(st), Is.EqualTo(speedItem.Effect.CraftingSpeedBonus));
        }
    }
}
