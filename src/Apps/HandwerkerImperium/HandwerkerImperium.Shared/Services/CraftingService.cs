using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet das Crafting-System mit Produktionsketten.
/// Rezepte haben 3 Tiers (ab Workshop-Level 50/150/300), ergaenzt Tier 4.
/// Höhere Tiers benötigen Produkte niedrigerer Tiers als Input.
///
/// V7 ():
/// - Cross-Workshop-Inputs werden ab Spielerlevel 100 gefordert (Onboarding-Schutz).
/// - Output-Stack-Limits werden vor StartCrafting validiert (kein Material-Burn).
/// - Reservierungen aus <see cref="IWarehouseService"/> werden bei Input-Verfuegbarkeit beruecksichtigt.
/// </summary>
public sealed class CraftingService : ICraftingService
{
    private readonly IGameStateService _gameState;
    private readonly IIncomeCalculatorService _incomeCalculator;
    private readonly IResearchService? _research;
    // V7 (-4 Ressourcen-Plan, Section 8.1): Telemetrie-Events fuer Material-Loop.
    private readonly IAnalyticsService? _analytics;
    // Lazy gegen DI-Zirkel (WarehouseService haengt von ICraftingService ab). Liefert die
    // effektiven Lager-Grenzen (Logistik-Forschung + Mega-Projekt-Slots) statt der rohen state-Werte,
    // damit manuelles Crafting dieselben Boni ehrt wie die Auto-Produktion.
    private readonly Lazy<IWarehouseService>? _warehouse;
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
        IIncomeCalculatorService incomeCalculator,
        IResearchService? research = null,
        IAnalyticsService? analytics = null,
        Lazy<IWarehouseService>? warehouse = null)
    {
        _gameState = gameState;
        _incomeCalculator = incomeCalculator;
        _research = research;
        _analytics = analytics;
        _warehouse = warehouse;
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
        // Rezept finden (read-only, kein Lock noetig)
        var recipe = CraftingRecipe.GetById(recipeId);
        if (recipe == null) return false;

        // Alle State-Mutationen (Material-Pruefung + Abzug + Job-Add) atomar unter dem ZENTRALEN
        // State-Lock. Das schuetzt gleichzeitig gegen Doppelklick UND gegen den AutoSave-Serializer
        // (Background-Thread, enumeriert CraftingInventory/ActiveCraftingJobs unter demselben Lock).
        bool started = _gameState.ExecuteWithLock(() =>
        {
            var state = _gameState.State;

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

            // V7: Output-Stack-/Slot-Limit pruefen — kein Crafting starten, wenn das Output nicht
            // ins Lager passt (sonst Material verschwendet). Effektive Grenzen via WarehouseService
            // (Logistik-Forschung + Mega-Projekt-Slots), Fallback auf rohe state-Werte ohne DI.
            if (!CanStoreOutput(state, recipe.OutputProductId, recipe.OutputCount)) return false;

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

            // Crafting-Job erstellen (Prestige-Shop + Research + Material-Affinity + Mega-Projekt
            // reduzieren die Dauer kumulativ, mit Cap 50%).
            int effectiveDuration = recipe.DurationSeconds;
            decimal craftingSpeedBonus = GetPrestigeCraftingSpeedBonus(state)
                                       + (_research?.GetTotalEffects().CraftingSpeedBonus ?? 0m)
                                       + GetMaterialAffinityBonus(state, recipe)
                                       + (state.GuildMembership?.MegaProjectCraftingSpeedBonus ?? 0m);
            if (craftingSpeedBonus > 0)
                effectiveDuration = Math.Max(1, (int)(effectiveDuration * (1m - Math.Min(craftingSpeedBonus, 0.50m))));

            state.ActiveCraftingJobs.Add(new CraftingJob
            {
                RecipeId = recipeId,
                StartedAt = DateTime.UtcNow,
                DurationSeconds = effectiveDuration
            });
            return true;
        });

        if (started) CraftingUpdated?.Invoke();
        return started;
    }

    /// <summary>
    /// Prueft ob <paramref name="count"/> Einheiten von <paramref name="outputId"/> ins Lager passen.
    /// Nutzt die effektiven Lager-Grenzen aus <see cref="IWarehouseService"/> (Logistik-Forschung +
    /// Mega-Projekt-Slots); ohne injizierten WarehouseService Fallback auf die rohen state-Werte.
    /// Aufrufer muss bereits den State-Lock halten.
    /// </summary>
    private bool CanStoreOutput(GameState state, string outputId, int count)
    {
        if (_warehouse != null)
            return _warehouse.Value.CanAddToInventory(outputId, count);

        int current = state.CraftingInventory.GetValueOrDefault(outputId, 0);
        if (current == 0)
        {
            int usedSlots = 0;
            foreach (var kv in state.CraftingInventory)
                if (kv.Value > 0) usedSlots++;
            return usedSlots < state.WarehouseSlotCount;
        }
        return current + count <= state.WarehouseStackLimit;
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
        string? collectedOutputId = null;
        int collectedCount = 0;
        WorkshopType collectedWorkshop = default;

        // Mutation atomar unter dem State-Lock (schuetzt gegen den AutoSave-Serializer, der
        // CraftingInventory/ActiveCraftingJobs auf dem Background-Thread enumeriert).
        bool collected = _gameState.ExecuteWithLock(() =>
        {
            var state = _gameState.State;

            // Job anhand der eindeutigen JobId finden (nicht RecipeId — bei mehreren gleichen Rezepten sonst falsch)
            var job = state.ActiveCraftingJobs.FirstOrDefault(j => j.JobId == jobId && j.IsComplete);
            if (job == null) return false;

            var recipe = CraftingRecipe.GetById(job.RecipeId);
            if (recipe == null) return false;

            string outputId = recipe.OutputProductId;
            int outputCount = recipe.OutputCount;

            // V7: Stack-Limit harter Check — wenn Lager voll, bleibt der Job auf "completed" stehen
            // bis Spieler Platz schafft. Verhindert Material-Burn nach langer Crafting-Zeit.
            if (!CanStoreOutput(state, outputId, outputCount)) return false;

            if (state.CraftingInventory.ContainsKey(outputId))
                state.CraftingInventory[outputId] += outputCount;
            else
                state.CraftingInventory[outputId] = outputCount;

            state.ActiveCraftingJobs.Remove(job);

            collectedOutputId = outputId;
            collectedCount = outputCount;
            collectedWorkshop = recipe.WorkshopType;
            return true;
        });

        if (!collected) return false;

        // V7 (-4 Telemetrie, Plan Section 8.1): material_crafted — ausserhalb des Locks
        _analytics?.TrackEvent("material_crafted", new Dictionary<string, object?>
        {
            ["product_id"] = collectedOutputId,
            ["tier"] = CraftingProduct.GetAllProducts().GetValueOrDefault(collectedOutputId!)?.Tier ?? 0,
            ["workshop"] = collectedWorkshop.ToString(),
            ["count"] = collectedCount
        });

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
        if (count <= 0) return 0m;

        int sellCount = 0;
        decimal pricePerUnit = 0m;
        decimal totalRevenue = 0m;
        int tier = 0;

        // Verfuegbarkeits-Pruefung + Inventar-Reduktion atomar unter dem State-Lock (schuetzt
        // gegen den AutoSave-Serializer der CraftingInventory enumeriert).
        _gameState.ExecuteWithLock(() =>
        {
            var state = _gameState.State;
            int total = state.CraftingInventory.GetValueOrDefault(productId, 0);
            int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
            int sellable = Math.Max(0, total - reserved);
            if (sellable <= 0) return;

            var allProducts = CraftingProduct.GetAllProducts();
            if (!allProducts.TryGetValue(productId, out var product)) return;

            sellCount = Math.Min(count, sellable);
            tier = product.Tier;
            pricePerUnit = GetSellPrice(productId);
            totalRevenue = pricePerUnit * sellCount;

            state.CraftingInventory[productId] -= sellCount;
            if (state.CraftingInventory[productId] <= 0)
                state.CraftingInventory.Remove(productId);
        });

        if (sellCount <= 0) return 0m;

        // Geld gutschreiben + Telemetrie ausserhalb des Locks
        _gameState.AddMoney(totalRevenue);

        // V7 (-4 Telemetrie, Plan Section 8.1): material_sold
        _analytics?.TrackEvent("material_sold", new Dictionary<string, object?>
        {
            ["product_id"] = productId,
            ["tier"] = tier,
            ["count"] = sellCount,
            ["price_per_unit"] = (double)pricePerUnit,
            ["total_revenue"] = (double)totalRevenue,
            ["source"] = "warehouse"
        });

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
    /// V7 (): Material-Affinity-Bonus aus den Workern des
    /// Workshop-Typs. Wenn Worker-Affinitaet zum Output-Material des Rezepts passt,
    /// gibt es bis zu +20% Crafting-Speed (pro arbeitendem Worker mit Match, gecapped).
    /// </summary>
    private static decimal GetMaterialAffinityBonus(GameState state, CraftingRecipe recipe)
    {
        var targetAffinity = MaterialAffinityExtensions.GetMaterialAffinity(recipe.OutputProductId);
        if (targetAffinity == MaterialAffinity.None) return 0m;

        // Workshop suchen
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

        // Anteil der arbeitenden Worker mit Affinity-Match
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

        // Linear: 0 Match = 0%, alle Match = +20%
        return 0.20m * matchingWorking / totalWorking;
    }

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
