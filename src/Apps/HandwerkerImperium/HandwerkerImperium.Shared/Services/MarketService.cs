using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// V7 (): Material-Markt mit deterministischer Preis-Dynamik.
/// Pro Spieler + UTC-Tag deterministisch — verhindert Save-Scumming. Innerhalb des Tages
/// oszilliert der Preis in einer Sinus-Welle (+/-50%) um den Basis-Preis.
///
/// Event-Modulatoren (Plan Section 3.5):
/// - <see cref="GameEventType.MaterialShortage"/>: betroffene Workshop-Materialien 3x teuerer.
/// - <see cref="GameEventType.HighDemand"/>: betroffene Workshop-Materialien 2x teuerer.
///
/// Spread: Verkaufspreis = Kaufpreis × 0.95 (5% Maklergebuehr — verhindert Sofort-Arbitrage).
/// </summary>
public sealed class MarketService : IMarketService
{
    private readonly IGameStateService _gameState;
    private readonly IWarehouseService _warehouse;
    private readonly IResearchService? _research;
    private readonly IEventService? _events;
    private readonly IAnalyticsService? _analytics;

    /// <summary>Spread Faktor — Verkaufspreis = Kauf × (1 - Spread).</summary>
    public const decimal SpreadFactor = 0.05m;

    /// <summary>Schwingungs-Amplitude um den Basis-Preis (±50%).</summary>
    public const double DailyAmplitude = 0.50;

    /// <summary>Research-Node die den Markt freischaltet.</summary>
    public const string MarketUnlockResearchId = "logi_05";

    public event Action? MarketChanged;

    public MarketService(
        IGameStateService gameState,
        IWarehouseService warehouse,
        IResearchService? research = null,
        IEventService? events = null,
        IAnalyticsService? analytics = null)
    {
        _gameState = gameState;
        _warehouse = warehouse;
        _research = research;
        _events = events;
        _analytics = analytics;
    }

    public bool IsMarketAvailable
    {
        get
        {
            var state = _gameState.State;
            // V7 (, Imperium-Pass): Premium-Spieler haben Markt-
            // Insider-Heatmap sofort frei (Plan Section 10.2). Ohne Pass: Forschung logi_05.
            if (state.IsPremium) return true;
            if (_research == null) return true;
            for (int i = 0; i < state.Researches.Count; i++)
            {
                if (state.Researches[i].Id == MarketUnlockResearchId && state.Researches[i].IsResearched)
                    return true;
            }
            return false;
        }
    }

    public decimal GetBuyPrice(string productId)
    {
        var allProducts = CraftingProduct.GetAllProducts();
        if (!allProducts.TryGetValue(productId, out var product)) return 0m;

        decimal basePrice = product.BaseValue;
        double factor = ComputeDailyFactor(productId, DateTime.UtcNow);
        decimal price = basePrice * (decimal)factor;

        // Event-Modulator
        var activeEvent = _gameState.State.ActiveEvent;
        if (activeEvent != null)
        {
            var recipe = CraftingRecipe.GetByOutputProduct(productId);
            var matchesWorkshop = recipe != null && activeEvent.Effect.AffectedWorkshop == recipe.WorkshopType;

            if (matchesWorkshop)
            {
                if (activeEvent.Type == GameEventType.MaterialShortage)
                    price *= 3m;
                else if (activeEvent.Type == GameEventType.HighDemand)
                    price *= 2m;
            }
        }

        return Math.Max(1m, Math.Round(price));
    }

    public decimal GetSellPrice(string productId) =>
        Math.Round(GetBuyPrice(productId) * (1m - SpreadFactor));

    public double GetPriceTrend(string productId)
    {
        decimal now = GetBuyPrice(productId);
        if (now <= 0) return 0;
        var allProducts = CraftingProduct.GetAllProducts();
        if (!allProducts.TryGetValue(productId, out var product)) return 0;

        // Naechste Stunde
        double factorNow = ComputeDailyFactor(productId, DateTime.UtcNow);
        double factorNext = ComputeDailyFactor(productId, DateTime.UtcNow.AddHours(1));
        double diff = factorNext - factorNow;
        return Math.Clamp(diff * 2.0, -1.0, 1.0);
    }

    public decimal[] Get24hPriceSeries(string productId)
    {
        var allProducts = CraftingProduct.GetAllProducts();
        if (!allProducts.TryGetValue(productId, out var product)) return new decimal[24];

        var basePrice = product.BaseValue;
        var result = new decimal[24];
        var startOfDay = DateTime.UtcNow.Date;
        for (int h = 0; h < 24; h++)
        {
            double factor = ComputeDailyFactor(productId, startOfDay.AddHours(h));
            result[h] = Math.Max(1m, Math.Round(basePrice * (decimal)factor));
        }
        return result;
    }

    public bool TryBuy(string productId, int count)
    {
        if (count <= 0 || !IsMarketAvailable) return false;

        decimal pricePer = GetBuyPrice(productId);
        decimal totalCost = pricePer * count;

        // Stack-Check via WarehouseService (Slot+Stack-Validierung)
        if (!_warehouse.CanAddToInventory(productId, count)) return false;
        if (!_gameState.TrySpendMoney(totalCost)) return false;

        int actuallyAdded = _warehouse.AddToInventory(productId, count);
        if (actuallyAdded < count)
        {
            // Sollte nicht passieren (CanAddToInventory hat bereits gecheckt), aber Geld zurueck
            // wenn nur teilweise eingelagert wurde.
            int shortfall = count - actuallyAdded;
            _gameState.AddMoney(pricePer * shortfall);
        }

        // V7 (Telemetrie, Plan Section 8.1): material_market_trade (buy)
        _analytics?.TrackEvent("material_market_trade", new Dictionary<string, object?>
        {
            ["product_id"] = productId,
            ["side"] = "buy",
            ["count"] = actuallyAdded,
            ["price_per_unit"] = (double)pricePer,
            ["total"] = (double)(pricePer * actuallyAdded)
        });

        MarketChanged?.Invoke();
        return true;
    }

    public decimal TrySell(string productId, int count)
    {
        if (count <= 0 || !IsMarketAvailable) return 0m;

        decimal revenue = 0m;
        decimal pricePer = 0m;
        int sellCount = 0;

        // ExecuteWithLock: Inventar-Mutation + Geldgutschrift gegen den AutoSave-Serializer
        // absichern (sonst "Collection was modified" auf CraftingInventory im Background-Thread).
        _gameState.ExecuteWithLock(() =>
        {
            var state = _gameState.State;
            int total = state.CraftingInventory.GetValueOrDefault(productId, 0);
            int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
            int sellable = Math.Max(0, total - reserved);
            if (sellable <= 0) return;

            sellCount = Math.Min(count, sellable);
            pricePer = GetSellPrice(productId);
            revenue = pricePer * sellCount;

            state.CraftingInventory[productId] = total - sellCount;
            if (state.CraftingInventory[productId] <= 0)
                state.CraftingInventory.Remove(productId);

            _gameState.AddMoney(revenue);
        });

        if (sellCount <= 0) return 0m;

        // V7 (Telemetrie, Plan Section 8.1): material_market_trade (sell)
        _analytics?.TrackEvent("material_market_trade", new Dictionary<string, object?>
        {
            ["product_id"] = productId,
            ["side"] = "sell",
            ["count"] = sellCount,
            ["price_per_unit"] = (double)pricePer,
            ["total"] = (double)revenue
        });

        MarketChanged?.Invoke();
        return revenue;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Deterministische Tages-Preisfaktor-Berechnung. Pro Material + Tag bekommt
    /// jeder Spieler eine eigene Sinus-Welle mit phasenversetztem Offset.
    /// </summary>
    private double ComputeDailyFactor(string productId, DateTime utc)
    {
        // Seed: PlayerId + Tag-Index + Material-Hash → deterministisch, aber pro Spieler/Material individuell
        var state = _gameState.State;
        string playerKey = state.PlayerGuid ?? "anonymous";
        int dayIndex = (int)(utc - new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
        // StableHash statt string.GetHashCode() — Letzteres ist pro Prozess randomisiert und
        // wuerde den Preis bei JEDEM App-Neustart auf eine andere Sinus-Phase springen lassen
        // (verletzt die zugesicherte Tages-Determinismus-Eigenschaft des Marktes).
        int seed = Helpers.StableHash.Compute(playerKey) ^ dayIndex ^ Helpers.StableHash.Compute(productId);

        // Phase-Offset aus dem Seed (deterministisch pro Tag/Material/Spieler)
        var rng = new Random(seed);
        double phaseOffset = rng.NextDouble() * Math.PI * 2; // 0..2π

        // Tageszeit als Phase
        double hourFraction = utc.TimeOfDay.TotalHours / 24.0; // 0..1
        double phase = hourFraction * Math.PI * 2 + phaseOffset;

        // Faktor zwischen (1 - amp) und (1 + amp)
        double factor = 1.0 + Math.Sin(phase) * DailyAmplitude;
        return factor;
    }
}
