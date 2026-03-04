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

    public event Action? CraftingUpdated;

    public CraftingService(IGameStateService gameState)
    {
        _gameState = gameState;
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

        // Crafting-Job erstellen
        var job = new CraftingJob
        {
            RecipeId = recipeId,
            StartedAt = DateTime.UtcNow,
            DurationSeconds = recipe.DurationSeconds
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

        bool anyCompleted = state.ActiveCraftingJobs.Any(j => j.IsComplete);
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
}
