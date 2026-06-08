using System;
using System.Collections.Generic;
using System.Linq;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.State;

namespace HandwerkerImperium.Domain.Crafting
{
    /// <summary>
    /// Service-Formel-Extrakt aus <c>CraftingService</c> (Avalonia-Original): die reine Crafting-Mathematik
    /// (verfuegbare Rezepte, Dauer-Reduktion, Material-Affinitaet, Prestige-Crafting-Speed, Level-Multiplikator
    /// und Verkaufspreis-Assemblierung). 1:1 zur Vorlage.
    ///
    /// Bewusst NICHT extrahiert (state-mutierend / Service-gekoppelt, bleiben im Game-Service):
    /// StartCrafting/CollectProduct/SellProducts/UpdateTimers (mutieren GameState, brauchen WarehouseService +
    /// IncomeCalculatorService + Telemetrie). Der Income-Boost-Multiplikator wird hier als Parameter
    /// uebergeben (vgl. <see cref="IncomeFormulas.CalculateCraftingSellMultiplier"/>).
    /// </summary>
    public static class CraftingFormulas
    {
        /// <summary>Rezepte eines Workshop-Typs, deren erforderliches Level erreicht ist.</summary>
        public static List<CraftingRecipe> GetAvailableRecipes(WorkshopType workshopType, int workshopLevel)
        {
            var allRecipes = CraftingRecipe.GetAllRecipes();
            return allRecipes
                .Where(r => r.WorkshopType == workshopType && workshopLevel >= r.RequiredWorkshopLevel)
                .ToList();
        }

        /// <summary>
        /// Effektive Crafting-Dauer in Sekunden: <paramref name="craftingSpeedBonus"/> (Summe aus Prestige +
        /// Research + Material-Affinitaet + Gilden-Boni) reduziert die Dauer, gedeckelt bei 50%, Minimum 1s.
        /// </summary>
        public static int CalculateEffectiveDuration(int baseDuration, decimal craftingSpeedBonus)
        {
            if (craftingSpeedBonus <= 0) return baseDuration;
            return Math.Max(1, (int)(baseDuration * (1m - Math.Min(craftingSpeedBonus, 0.50m))));
        }

        /// <summary>Tier-1-Materialkosten in Gold = 20% des Basis-Verkaufspreises (Geld-Senke gegen Gratis-Generierung).</summary>
        public static decimal CalculateTier1MaterialCost(decimal baseValue) => baseValue * 0.20m;

        /// <summary>
        /// Level-Multiplikator des Verkaufspreises (logarithmisch):
        /// 1 + log2(1 + workshopLevel / <see cref="GameBalanceConstants.CraftingSellPriceLogDivisor"/>).
        /// </summary>
        public static decimal CalculateLevelMultiplier(int workshopLevel) =>
            // Math.Log2 ist .NET 5+/nicht netstandard2.1 -> Math.Log(x, 2.0) (wertgleich) fuer Unity.
            1.0m + (decimal)Math.Log(1.0 + workshopLevel / GameBalanceConstants.CraftingSellPriceLogDivisor, 2.0);

        /// <summary>
        /// Verkaufspreis (1 Stueck) = Round(BaseValue × LevelMultiplier × BoostMultiplier). Der
        /// <paramref name="boostMult"/> ist die Einkommens-Multiplikator-Kette (Prestige/Research/Events/…)
        /// aus <see cref="IncomeFormulas.CalculateCraftingSellMultiplier"/>.
        /// </summary>
        public static decimal CalculateSellPrice(decimal baseValue, int workshopLevel, decimal boostMult) =>
            Math.Round(baseValue * CalculateLevelMultiplier(workshopLevel) * boostMult);

        /// <summary>
        /// Material-Affinitaets-Bonus aus den arbeitenden Workern des Workshop-Typs: passt die Worker-Affinitaet
        /// zum Output-Material, gibt es bis +20% Crafting-Speed (linear nach Anteil matchender arbeitender Worker).
        /// </summary>
        public static decimal CalculateMaterialAffinityBonus(GameState state, CraftingRecipe recipe)
        {
            var targetAffinity = MaterialAffinityExtensions.GetMaterialAffinity(recipe.OutputProductId);
            if (targetAffinity == MaterialAffinity.None) return 0m;

            Workshop? workshop = null;
            for (int i = 0; i < state.Workshops.Count; i++)
            {
                if (state.Workshops[i].Type == recipe.WorkshopType)
                {
                    workshop = state.Workshops[i];
                    break;
                }
            }
            if (workshop == null || workshop.Workers.Count == 0) return 0m;

            int matchingWorking = 0;
            int totalWorking = 0;
            for (int i = 0; i < workshop.Workers.Count; i++)
            {
                var w = workshop.Workers[i];
                if (!w.IsWorking) continue;
                totalWorking++;
                if (w.MaterialAffinity == targetAffinity) matchingWorking++;
            }
            if (totalWorking == 0) return 0m;

            return 0.20m * matchingWorking / totalWorking;
        }

        /// <summary>
        /// Crafting-Speed-Bonus aus gekauften (nicht wiederholbaren) Prestige-Shop-Items. Im Game-Service
        /// wird dieses Ergebnis zusaetzlich gecacht (Dirty-Flag) — hier die reine, ungecachte Berechnung.
        /// </summary>
        public static decimal CalculatePrestigeCraftingSpeedBonus(GameState state)
        {
            var purchased = state.Prestige.PurchasedShopItems;
            if (purchased.Count == 0) return 0m;

            decimal bonus = 0m;
            var allItems = PrestigeShop.GetAllItems();
            for (int i = 0; i < allItems.Count; i++)
            {
                var item = allItems[i];
                if (!item.IsRepeatable && purchased.Contains(item.Id) && item.Effect.CraftingSpeedBonus > 0)
                    bonus += item.Effect.CraftingSpeedBonus;
            }
            return bonus;
        }
    }
}
