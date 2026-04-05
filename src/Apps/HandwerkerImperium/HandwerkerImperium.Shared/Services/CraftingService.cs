using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet das Crafting-System mit Produktionsketten.
/// Rezepte haben 3 Tiers (ab Workshop-Level 50/150/300).
/// Höhere Tiers benötigen Produkte niedrigerer Tiers als Input.
/// </summary>
public sealed class CraftingService : ICraftingService
{
    private readonly IGameStateService _gameState;
    private readonly IIncomeCalculatorService _incomeCalculator;
    // Lock verhindert Race Condition bei schnellem Doppelklick (Materialien doppelt verbraucht)
    private readonly object _craftingLock = new();
    // Gecachter Crafting-Speed-Bonus (Dirty-Flag statt Count-Vergleich)
    private decimal _cachedCraftingSpeedBonus;
    private bool _craftingSpeedCacheDirty = true;

    public event Action? CraftingUpdated;

    /// <summary>Feuert wenn ein fertiges Crafting-Produkt eingesammelt wird.</summary>
    public event Action? CraftingProductCollected;

    /// <summary>
    /// Crafting-Speed-Cache invalidieren (nach Prestige-Shop-Kauf oder State-Load).
    /// </summary>
    public void InvalidateCraftingSpeedCache() => _craftingSpeedCacheDirty = true;

    public CraftingService(
        IGameStateService gameState,
        IIncomeCalculatorService incomeCalculator)
    {
        _gameState = gameState;
        _incomeCalculator = incomeCalculator;
        // Bei State-Wechsel (Prestige/Import/Reset) und Prestige-Shop-Kauf Cache invalidieren
        _gameState.StateLoaded += (_, _) => _craftingSpeedCacheDirty = true;
        _gameState.PrestigeShopPurchased += (_, _) => _craftingSpeedCacheDirty = true;
    }

    public List<CraftingRecipe> GetAvailableRecipes(WorkshopType workshopType, int workshopLevel)
    {
        var allRecipes = CraftingRecipe.GetAllRecipes();
        return allRecipes
            .Where(r => r.WorkshopType == workshopType && workshopLevel >= r.RequiredWorkshopLevel)
            .ToList();
    }

    public bool StartCrafting(string recipeId)
    {
        // Lock verhindert Race Condition: Doppelklick könnte Materialien doppelt verbrauchen
        // wenn Prüfung und Abzug nicht atomar erfolgen
        lock (_craftingLock)
        {
            var state = _gameState.State;

            // Rezept finden
            var recipe = CraftingRecipe.GetById(recipeId);
            if (recipe == null) return false;

            // Tier-1 Rezepte: Materialkosten in Gold (20% des Basis-Verkaufspreises)
            // Verhindert kostenlose Geld-Generierung ohne Senke
            if (recipe.Tier == 1 && recipe.InputProducts.Count == 0)
            {
                var product = CraftingProduct.GetAllProducts().GetValueOrDefault(recipe.OutputProductId);
                if (product != null)
                {
                    decimal materialCost = product.BaseValue * 0.20m;
                    if (!_gameState.CanAfford(materialCost)) return false;
                    _gameState.TrySpendMoney(materialCost);
                }
            }

            // Input-Produkte prüfen und abziehen (atomar innerhalb des Locks)
            foreach (var (productId, required) in recipe.InputProducts)
            {
                int available = state.CraftingInventory.GetValueOrDefault(productId, 0);
                if (available < required) return false;
            }

            foreach (var (productId, required) in recipe.InputProducts)
            {
                state.CraftingInventory[productId] -= required;
                if (state.CraftingInventory[productId] <= 0)
                    state.CraftingInventory.Remove(productId);
            }

            // Crafting-Job erstellen (Prestige-Shop CraftingSpeedBonus reduziert Dauer)
            int effectiveDuration = recipe.DurationSeconds;
            decimal craftingSpeedBonus = GetPrestigeCraftingSpeedBonus(state);
            if (craftingSpeedBonus > 0)
                effectiveDuration = Math.Max(1, (int)(effectiveDuration * (1m - Math.Min(craftingSpeedBonus, 0.50m))));

            var job = new CraftingJob
            {
                RecipeId = recipeId,
                StartedAt = DateTime.UtcNow,
                DurationSeconds = effectiveDuration
            };

            state.ActiveCraftingJobs.Add(job);

        }

        CraftingUpdated?.Invoke();
        return true;
    }

    public void UpdateTimers()
    {
        var state = _gameState.State;
        if (state.ActiveCraftingJobs.Count == 0) return;

        // For-Schleife statt LINQ .Any() (vermeidet Enumerator+Closure pro Sekunde)
        bool anyCompleted = false;
        for (int i = 0; i < state.ActiveCraftingJobs.Count; i++)
        {
            if (state.ActiveCraftingJobs[i].IsComplete) { anyCompleted = true; break; }
        }
        if (anyCompleted)
        {
            CraftingUpdated?.Invoke();
        }
    }

    public bool CollectProduct(string jobId)
    {
        var state = _gameState.State;

        // Job anhand der eindeutigen JobId finden (nicht RecipeId — bei mehreren gleichen Rezepten sonst falsch)
        var job = state.ActiveCraftingJobs.FirstOrDefault(j => j.JobId == jobId && j.IsComplete);
        if (job == null) return false;

        // Rezept nachschlagen für Output
        var recipe = CraftingRecipe.GetById(job.RecipeId);
        if (recipe == null) return false;

        // Produkt zum Inventar hinzufügen
        string outputId = recipe.OutputProductId;
        int outputCount = recipe.OutputCount;

        if (state.CraftingInventory.ContainsKey(outputId))
            state.CraftingInventory[outputId] += outputCount;
        else
            state.CraftingInventory[outputId] = outputCount;

        // Job entfernen
        state.ActiveCraftingJobs.Remove(job);

        CraftingUpdated?.Invoke();
        CraftingProductCollected?.Invoke();
        return true;
    }

    public bool SellProduct(string productId) => SellProducts(productId, 1) > 0;

    /// <summary>
    /// Verkauft mehrere Einheiten eines Produkts. Gibt den Gesamterlös zurück (0 bei Fehler).
    /// Verkaufspreis skaliert mit Workshop-Level und allen Einkommens-Multiplikatoren.
    /// </summary>
    public decimal SellProducts(string productId, int count)
    {
        var state = _gameState.State;

        int available = state.CraftingInventory.GetValueOrDefault(productId, 0);
        if (available <= 0 || count <= 0) return 0m;

        var allProducts = CraftingProduct.GetAllProducts();
        if (!allProducts.TryGetValue(productId, out var product)) return 0m;

        // Tatsächlich verkaufte Menge
        int sellCount = Math.Min(count, available);

        // Skalierenden Preis berechnen
        decimal pricePerUnit = GetSellPrice(productId);
        decimal totalRevenue = pricePerUnit * sellCount;

        // Inventar reduzieren
        state.CraftingInventory[productId] -= sellCount;
        if (state.CraftingInventory[productId] <= 0)
            state.CraftingInventory.Remove(productId);

        // Geld gutschreiben
        _gameState.AddMoney(totalRevenue);

        CraftingUpdated?.Invoke();
        return totalRevenue;
    }

    /// <summary>
    /// Berechnet den aktuellen Verkaufspreis eines Produkts (1 Stück) inkl. aller Multiplikatoren.
    /// Formel: BaseValue × LevelMultiplier × CraftingSellMultiplier (Prestige, Research, Events, etc.)
    /// </summary>
    public decimal GetSellPrice(string productId)
    {
        var allProducts = CraftingProduct.GetAllProducts();
        if (!allProducts.TryGetValue(productId, out var product)) return 0m;

        var state = _gameState.State;

        // Workshop-Level für dieses Produkt ermitteln
        int workshopLevel = GetWorkshopLevelForProduct(productId, state);

        // Level-Multiplikator: logarithmisch skalierend
        decimal levelMult = 1.0m + (decimal)Math.Log2(1.0 + workshopLevel / GameBalanceConstants.CraftingSellPriceLogDivisor);

        // Prestige-Shop Income-Bonus (aus PrestigeShop-Items berechnen)
        decimal prestigeIncomeBonus = GetPrestigeIncomeBonus(state);

        // Rebirth-Bonus des Workshops
        decimal rebirthBonus = GetRebirthBonusForProduct(productId, state);

        // Alle Einkommens-Multiplikatoren (ohne Soft-Cap, ohne Speed/Rush)
        decimal boostMult = _incomeCalculator.CalculateCraftingSellMultiplier(
            state, prestigeIncomeBonus, rebirthBonus);

        return Math.Round(product.BaseValue * levelMult * boostMult);
    }

    /// <summary>
    /// Ermittelt das Workshop-Level für ein Produkt anhand seines Workshop-Typs.
    /// </summary>
    private static int GetWorkshopLevelForProduct(string productId, GameState state)
    {
        var recipe = FindRecipeByProductId(productId);
        if (recipe == null) return 1;

        for (int i = 0; i < state.Workshops.Count; i++)
        {
            if (state.Workshops[i].Type == recipe.WorkshopType)
                return state.Workshops[i].Level;
        }
        return 1;
    }

    /// <summary>
    /// Ermittelt den Rebirth-Einkommensbonus des Workshops für ein Produkt.
    /// </summary>
    private static decimal GetRebirthBonusForProduct(string productId, GameState state)
    {
        var recipe = FindRecipeByProductId(productId);
        if (recipe == null) return 0m;

        for (int i = 0; i < state.Workshops.Count; i++)
        {
            if (state.Workshops[i].Type == recipe.WorkshopType)
                return state.Workshops[i].RebirthIncomeBonus;
        }
        return 0m;
    }

    /// <summary>
    /// Findet das Rezept das ein bestimmtes Produkt herstellt.
    /// </summary>
    private static CraftingRecipe? FindRecipeByProductId(string productId) =>
        CraftingRecipe.GetByOutputProduct(productId);

    /// <summary>
    /// Berechnet den Crafting-Geschwindigkeitsbonus aus gekauften Prestige-Shop-Items.
    /// Gecacht: Nur neu berechnet wenn sich PurchasedShopItems.Count aendert.
    /// </summary>
    private decimal GetPrestigeCraftingSpeedBonus(GameState state)
    {
        if (!_craftingSpeedCacheDirty) return _cachedCraftingSpeedBonus;
        _craftingSpeedCacheDirty = false;

        var purchased = state.Prestige.PurchasedShopItems;
        if (purchased.Count == 0)
        {
            _cachedCraftingSpeedBonus = 0m;
            return 0m;
        }

        decimal bonus = 0m;
        var allItems = PrestigeShop.GetAllItems();
        for (int i = 0; i < allItems.Count; i++)
        {
            var item = allItems[i];
            if (!item.IsRepeatable && purchased.Contains(item.Id) && item.Effect.CraftingSpeedBonus > 0)
                bonus += item.Effect.CraftingSpeedBonus;
        }
        _cachedCraftingSpeedBonus = bonus;
        return bonus;
    }

    /// <summary>
    /// Berechnet den Prestige-Shop-Einkommensbonus aus gekauften Items.
    /// </summary>
    private static decimal GetPrestigeIncomeBonus(GameState state)
    {
        var purchased = state.Prestige.PurchasedShopItems;
        var repeatableCounts = state.Prestige.RepeatableItemCounts;
        if (purchased.Count == 0 && repeatableCounts.Count == 0) return 0m;

        decimal bonus = 0m;
        var allItems = PrestigeShop.GetAllItems();
        for (int i = 0; i < allItems.Count; i++)
        {
            var item = allItems[i];
            if (item.IsRepeatable)
            {
                if (repeatableCounts.TryGetValue(item.Id, out var count) && count > 0
                    && item.Effect.IncomeMultiplier > 0)
                    bonus += item.Effect.IncomeMultiplier * count;
                continue;
            }
            if (purchased.Contains(item.Id) && item.Effect.IncomeMultiplier > 0)
                bonus += item.Effect.IncomeMultiplier;
        }
        return bonus;
    }
}
