using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet das Crafting-System mit Produktionsketten.
/// Rezepte haben 3 Tiers (ab Workshop-Level 50/150/300), Phase 4 ergaenzt Tier 4.
/// Höhere Tiers benötigen Produkte niedrigerer Tiers als Input.
///
/// V7 (Phase 1 Ressourcen-Plan):
/// - Cross-Workshop-Inputs werden ab Spielerlevel 100 gefordert (Onboarding-Schutz).
/// - Output-Stack-Limits werden vor StartCrafting validiert (kein Material-Burn).
/// - Reservierungen aus <see cref="IWarehouseService"/> werden bei Input-Verfuegbarkeit beruecksichtigt.
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

            // V7: Cross-Workshop-Inputs erst ab Spielerlevel 100. Casual-Spieler brauchen
            // nur eigene Workshop-Inputs (Onboarding-Schutz, siehe Plan Section 5.3).
            var effectiveInputs = CraftingRecipe.GetEffectiveInputs(recipe, state.PlayerLevel);

            // Tier-1 Rezepte: Materialkosten in Gold (20% des Basis-Verkaufspreises)
            // Verhindert kostenlose Geld-Generierung ohne Senke
            if (recipe.Tier == 1 && effectiveInputs.Count == 0)
            {
                var product = CraftingProduct.GetAllProducts().GetValueOrDefault(recipe.OutputProductId);
                if (product != null)
                {
                    decimal materialCost = product.BaseValue * 0.20m;
                    if (!_gameState.CanAfford(materialCost)) return false;
                    _gameState.TrySpendMoney(materialCost);
                }
            }

            // V7: Output-Stack-Limit pruefen — kein Crafting starten, wenn das Output
            // nicht ins Lager passt (sonst Material verschwendet).
            int currentOutput = state.CraftingInventory.GetValueOrDefault(recipe.OutputProductId, 0);
            if (currentOutput == 0)
            {
                // Neuer Slot noetig
                int usedSlots = 0;
                foreach (var kv in state.CraftingInventory)
                    if (kv.Value > 0) usedSlots++;
                if (usedSlots >= state.WarehouseSlotCount) return false;
            }
            else if (currentOutput + recipe.OutputCount > state.WarehouseStackLimit)
            {
                return false; // Stack-Limit wuerde gesprengt
            }

            // V7: Input-Produkte pruefen UND Reservierungen abziehen (atomar im Lock)
            foreach (var (productId, required) in effectiveInputs)
            {
                int total = state.CraftingInventory.GetValueOrDefault(productId, 0);
                int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
                int available = total - reserved;
                if (available < required) return false;
            }

            foreach (var (productId, required) in effectiveInputs)
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

        // V7: Stack-Limit harter Check — wenn Lager voll, bleibt der Job auf "completed" stehen
        // bis Spieler Platz schafft. Verhindert Material-Burn nach langer Crafting-Zeit.
        int currentStock = state.CraftingInventory.GetValueOrDefault(outputId, 0);
        if (currentStock == 0)
        {
            int usedSlots = 0;
            foreach (var kv in state.CraftingInventory)
                if (kv.Value > 0) usedSlots++;
            if (usedSlots >= state.WarehouseSlotCount) return false; // Lager voll
        }
        else if (currentStock + outputCount > state.WarehouseStackLimit)
        {
            return false; // Stack-Limit
        }

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
    /// V7: Reservierte Mengen (Order-Annahme) sind ausgeschlossen — der Spieler kann nur
    /// das verkaufen was nicht fuer einen akzeptierten Auftrag gehalten wird.
    /// </summary>
    public decimal SellProducts(string productId, int count)
    {
        var state = _gameState.State;

        int total = state.CraftingInventory.GetValueOrDefault(productId, 0);
        int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
        int sellable = Math.Max(0, total - reserved);
        if (sellable <= 0 || count <= 0) return 0m;

        var allProducts = CraftingProduct.GetAllProducts();
        if (!allProducts.TryGetValue(productId, out var product)) return 0m;

        // Tatsächlich verkaufte Menge
        int sellCount = Math.Min(count, sellable);

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

        // Prestige-Shop Income-Bonus (zentrale Berechnung in IncomeCalculatorService)
        decimal prestigeIncomeBonus = IncomeCalculatorService.GetPrestigeIncomeBonus(state);

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

}
