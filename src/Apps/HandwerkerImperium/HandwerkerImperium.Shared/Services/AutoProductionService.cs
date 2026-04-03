using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Automatische Produktion von Crafting-Items in Workshops.
/// Jeder Workshop mit Level >= 50 produziert passiv Tier-1 Items
/// basierend auf der Anzahl arbeitender Worker.
/// Rate: 1 Item pro Worker alle 180s (Standard), 120s (InnovationLab), 60s (MasterSmith).
/// </summary>
public sealed class AutoProductionService : IAutoProductionService
{
    /// <summary>
    /// Mapping: WorkshopType → Tier-1 Produkt-ID für Auto-Produktion.
    /// </summary>
    private static readonly Dictionary<WorkshopType, string> Tier1Products = new()
    {
        [WorkshopType.Carpenter] = "planks",
        [WorkshopType.Plumber] = "pipes",
        [WorkshopType.Electrician] = "cables",
        [WorkshopType.Painter] = "paint_mix",
        [WorkshopType.Roofer] = "roof_tiles",
        [WorkshopType.Contractor] = "concrete",
        [WorkshopType.Architect] = "blueprint",
        [WorkshopType.GeneralContractor] = "contract",
        [WorkshopType.MasterSmith] = "fittings",
        [WorkshopType.InnovationLab] = "prototype",
    };

    public void ProduceForAllWorkshops(GameState state)
    {
        state.CraftingInventory ??= new Dictionary<string, int>();

        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var workshop = state.Workshops[i];
            if (!IsAutoProductionUnlocked(workshop)) continue;

            // MasterSmith hat eigenen Tick im GameLoopService (ProduceMasterSmithMaterials),
            // produziert aber zusätzlich sein eigenes Produkt "fittings" hier
            var productId = GetTier1ProductId(workshop.Type);
            if (productId == null) continue;

            // Arbeitende Worker zählen (ohne LINQ)
            int workingWorkers = 0;
            for (int w = 0; w < workshop.Workers.Count; w++)
                if (workshop.Workers[w].IsWorking) workingWorkers++;
            if (workingWorkers <= 0) continue;

            // Items produzieren
            for (int w = 0; w < workingWorkers; w++)
            {
                if (state.CraftingInventory.ContainsKey(productId))
                    state.CraftingInventory[productId]++;
                else
                    state.CraftingInventory[productId] = 1;
            }

            state.Statistics.TotalItemsAutoProduced += workingWorkers;
        }
    }

    public int GetProductionInterval(WorkshopType type) => type switch
    {
        WorkshopType.MasterSmith => GameBalanceConstants.AutoProductionMasterSmithInterval,
        WorkshopType.InnovationLab => GameBalanceConstants.AutoProductionInnovationLabInterval,
        _ => GameBalanceConstants.AutoProductionIntervalSeconds,
    };

    public string? GetTier1ProductId(WorkshopType type) =>
        Tier1Products.GetValueOrDefault(type);

    public bool IsAutoProductionUnlocked(Workshop workshop) =>
        workshop.Level >= GameBalanceConstants.AutoProductionUnlockLevel;

    /// <summary>
    /// Automatisches Crafting: Konvertiert Tier-1→Tier-2 (ab WS-Level 200) und Tier-2→Tier-3 (ab WS-Level 400).
    /// Wird alle 360 Ticks (~6min) vom GameLoop aufgerufen.
    /// Pro Aufruf wird maximal 1 Rezept pro Workshop verarbeitet (verhindert sofortige Massen-Konvertierung).
    /// </summary>
    public int AutoCraftHigherTiers(GameState state)
    {
        state.CraftingInventory ??= new Dictionary<string, int>();
        var allRecipes = CraftingRecipe.GetAllRecipes();
        int totalCrafted = 0;

        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var workshop = state.Workshops[i];

            // Tier-3 zuerst prüfen (höherwertig, verbraucht Tier-2)
            if (workshop.Level >= GameBalanceConstants.AutoCraftTier3UnlockLevel)
            {
                if (TryAutoCraftRecipe(state, allRecipes, workshop.Type, 3))
                {
                    totalCrafted++;
                    continue; // Max 1 Rezept pro Workshop pro Tick
                }
            }

            // Dann Tier-2
            if (workshop.Level >= GameBalanceConstants.AutoCraftTier2UnlockLevel)
            {
                if (TryAutoCraftRecipe(state, allRecipes, workshop.Type, 2))
                    totalCrafted++;
            }
        }

        return totalCrafted;
    }

    /// <summary>
    /// Versucht ein Rezept eines bestimmten Tiers für einen Workshop-Typ automatisch herzustellen.
    /// Prüft ob genug Input-Materialien vorhanden sind und erstellt das Output-Produkt.
    /// </summary>
    private static bool TryAutoCraftRecipe(GameState state, List<CraftingRecipe> allRecipes, WorkshopType type, int tier)
    {
        for (int r = 0; r < allRecipes.Count; r++)
        {
            var recipe = allRecipes[r];
            if (recipe.WorkshopType != type || recipe.Tier != tier) continue;
            if (recipe.InputProducts.Count == 0) continue;

            // Prüfe ob genug Input-Materialien vorhanden
            bool canCraft = true;
            foreach (var (productId, required) in recipe.InputProducts)
            {
                if (state.CraftingInventory.GetValueOrDefault(productId, 0) < required)
                {
                    canCraft = false;
                    break;
                }
            }
            if (!canCraft) continue;

            // Materialien abziehen
            foreach (var (productId, required) in recipe.InputProducts)
            {
                state.CraftingInventory[productId] -= required;
                if (state.CraftingInventory[productId] <= 0)
                    state.CraftingInventory.Remove(productId);
            }

            // Output-Produkt hinzufügen
            if (state.CraftingInventory.ContainsKey(recipe.OutputProductId))
                state.CraftingInventory[recipe.OutputProductId] += recipe.OutputCount;
            else
                state.CraftingInventory[recipe.OutputProductId] = recipe.OutputCount;

            state.Statistics.TotalItemsAutoProduced++;
            return true;
        }

        return false;
    }

    public Dictionary<string, int> CalculateOfflineProduction(GameState state, double offlineSeconds)
    {
        var produced = new Dictionary<string, int>();
        if (offlineSeconds <= 0) return produced;

        // Offline-Staffelung (gleich wie Offline-Earnings)
        double effectiveSeconds = CalculateEffectiveOfflineSeconds(offlineSeconds);

        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var workshop = state.Workshops[i];
            if (!IsAutoProductionUnlocked(workshop)) continue;

            var productId = GetTier1ProductId(workshop.Type);
            if (productId == null) continue;

            int workingWorkers = 0;
            for (int w = 0; w < workshop.Workers.Count; w++)
                if (workshop.Workers[w].IsWorking) workingWorkers++;
            if (workingWorkers <= 0) continue;

            int interval = GetProductionInterval(workshop.Type);
            int itemsProduced = (int)(effectiveSeconds / interval * workingWorkers);
            if (itemsProduced <= 0) continue;

            if (produced.ContainsKey(productId))
                produced[productId] += itemsProduced;
            else
                produced[productId] = itemsProduced;
        }

        return produced;
    }

    /// <summary>
    /// Berechnet effektive Offline-Sekunden mit Staffelung (80%/35%/15%/5%).
    /// Identisch mit OfflineProgressService-Staffelung.
    /// </summary>
    private static double CalculateEffectiveOfflineSeconds(double totalSeconds)
    {
        double effective = 0;
        double remaining = totalSeconds;

        // Erste 2h: 80%
        double first2h = Math.Min(remaining, 7200);
        effective += first2h * 0.80;
        remaining -= first2h;
        if (remaining <= 0) return effective;

        // 2-4h: 35%
        double next2h = Math.Min(remaining, 7200);
        effective += next2h * 0.35;
        remaining -= next2h;
        if (remaining <= 0) return effective;

        // 4-8h: 15%
        double next4h = Math.Min(remaining, 14400);
        effective += next4h * 0.15;
        remaining -= next4h;
        if (remaining <= 0) return effective;

        // 8h+: 5%
        effective += remaining * 0.05;
        return effective;
    }
}
