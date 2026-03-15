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
    // Gecachter Crafting-Speed-Bonus (wird bei Prestige-Shop-Kauf invalidiert)
    private decimal _cachedCraftingSpeedBonus = -1m;
    private int _lastPurchasedCount = -1;

    public event Action? CraftingUpdated;

    public CraftingService(IGameStateService gameState)
    {
        _gameState = gameState;
        // Bei State-Wechsel (Prestige/Import/Reset) Crafting-Speed-Cache invalidieren
        _gameState.StateLoaded += (_, _) =>
        {
            _cachedCraftingSpeedBonus = -1m;
            _lastPurchasedCount = -1;
        };
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
        var state = _gameState.State;

        // Rezept finden
        var recipe = CraftingRecipe.GetAllRecipes().FirstOrDefault(r => r.Id == recipeId);
        if (recipe == null) return false;

        // Input-Produkte prüfen und abziehen
        foreach (var (productId, required) in recipe.InputProducts)
        {
            int available = state.CraftingInventory.GetValueOrDefault(productId, 0);
            if (available < required) return false;
        }

        // Inputs abziehen
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

        _gameState.MarkDirty();
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

        // Job anhand der RecipeId finden (CraftingJob hat keine eigene ID)
        var job = state.ActiveCraftingJobs.FirstOrDefault(j => j.RecipeId == jobId && j.IsComplete);
        if (job == null) return false;

        // Rezept nachschlagen für Output
        var recipe = CraftingRecipe.GetAllRecipes().FirstOrDefault(r => r.Id == job.RecipeId);
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

        _gameState.MarkDirty();
        CraftingUpdated?.Invoke();
        return true;
    }

    public bool SellProduct(string productId)
    {
        var state = _gameState.State;

        // Produkt im Inventar prüfen
        int available = state.CraftingInventory.GetValueOrDefault(productId, 0);
        if (available <= 0) return false;

        // Verkaufspreis ermitteln
        var allProducts = CraftingProduct.GetAllProducts();
        if (!allProducts.TryGetValue(productId, out var product)) return false;

        // Produkt verkaufen (1 Stück)
        state.CraftingInventory[productId]--;
        if (state.CraftingInventory[productId] <= 0)
            state.CraftingInventory.Remove(productId);

        // Geld gutschreiben
        _gameState.AddMoney(product.BaseValue);

        _gameState.MarkDirty();
        CraftingUpdated?.Invoke();
        return true;
    }

    /// <summary>
    /// Berechnet den Crafting-Geschwindigkeitsbonus aus gekauften Prestige-Shop-Items.
    /// Gecacht: Nur neu berechnet wenn sich PurchasedShopItems.Count aendert.
    /// </summary>
    private decimal GetPrestigeCraftingSpeedBonus(GameState state)
    {
        var purchased = state.Prestige.PurchasedShopItems;
        if (purchased.Count == 0) return 0m;

        // Cache invalidieren wenn sich Kauf-Anzahl aendert
        if (purchased.Count == _lastPurchasedCount && _cachedCraftingSpeedBonus >= 0m)
            return _cachedCraftingSpeedBonus;

        decimal bonus = 0m;
        var allItems = PrestigeShop.GetAllItems();
        for (int i = 0; i < allItems.Count; i++)
        {
            var item = allItems[i];
            if (!item.IsRepeatable && purchased.Contains(item.Id) && item.Effect.CraftingSpeedBonus > 0)
                bonus += item.Effect.CraftingSpeedBonus;
        }
        _cachedCraftingSpeedBonus = bonus;
        _lastPurchasedCount = purchased.Count;
        return bonus;
    }
}
