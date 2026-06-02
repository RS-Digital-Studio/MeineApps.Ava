using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// V7 (): Lager-Verwaltung mit Slots + Stack-Limits + Reservierung.
/// Wird von AutoProductionService, CraftingService und der Lager-UI im Imperium-Tab konsumiert.
/// </summary>
public sealed class WarehouseService : IWarehouseService
{
    private readonly IGameStateService _gameState;
    private readonly ICraftingService _crafting;
    private readonly IResearchService? _research;
    private readonly IAnalyticsService? _analytics;
    // Kein eigener Lock mehr: ALLE Mutationen + enumerierenden Reads von CraftingInventory/
    // ReservedInventory laufen ueber _gameState.ExecuteWithLock (= zentraler _stateLock). Nur so
    // schliessen sie gegen den AutoSave-Serializer aus (der unter demselben Lock serialisiert) —
    // ein separater Lock garantierte keinen gegenseitigen Ausschluss ("Collection was modified").

    public event Action? InventoryChanged;
    public event Action<WorkshopType, string>? WorkshopPaused;
    public event Action<string, int, decimal>? OverflowAutoSold;

    /// <summary>Slot-Upgrade-Schritt: +5 Slots pro Kauf.</summary>
    public const int SlotsPerUpgrade = 5;

    /// <summary>Hard-Cap fuer Lager-Slots (200, siehe Plan Section 3.4).</summary>
    public const int MaxSlots = 200;

    /// <summary>Basis-Kosten fuer den ersten Slot-Upgrade.</summary>
    private const decimal SlotUpgradeBaseCost = 50_000m;

    /// <summary>Exponent fuer Slot-Upgrade-Kosten.</summary>
    private const double SlotUpgradeExponent = 1.5;

    public WarehouseService(
        IGameStateService gameState,
        ICraftingService crafting,
        IResearchService? research = null,
        IAnalyticsService? analytics = null)
    {
        _gameState = gameState;
        _crafting = crafting;
        _research = research;
        _analytics = analytics;
        // Bei State-Wechsel (Prestige/Import/Reset): UI muss Inventar neu lesen
        _gameState.StateLoaded += (_, _) => InventoryChanged?.Invoke();
    }

    /// <summary>
    /// V7 (): Bonus-Slots aus Logistik-Forschung.
    /// </summary>
    public int BonusSlotsFromResearch
    {
        get
        {
            var effects = _research?.GetTotalEffects();
            return effects?.BonusWarehouseSlots ?? 0;
        }
    }

    /// <summary>
    /// V7 (): Stack-Limit-Multiplikator aus Logistik-Forschung (Default 1.0).
    /// </summary>
    public decimal StackLimitMultiplierFromResearch
    {
        get
        {
            var effects = _research?.GetTotalEffects();
            decimal mult = effects?.StackLimitMultiplier ?? 0m;
            return mult > 1.0m ? mult : 1.0m;
        }
    }

    public int UsedSlotCount => _gameState.ExecuteWithLock(() =>
    {
        int used = 0;
        foreach (var kv in _gameState.State.CraftingInventory)
            if (kv.Value > 0) used++;
        return used;
    });

    public int FreeSlotCount => Math.Max(0, EffectiveSlotCount - UsedSlotCount);

    public bool IsWarehouseFull => FreeSlotCount == 0;

    public int CurrentStackLimit
    {
        get
        {
            decimal mult = StackLimitMultiplierFromResearch;
            int boosted = (int)Math.Round(_gameState.State.WarehouseStackLimit * mult);
            return Math.Min(9999, Math.Max(_gameState.State.WarehouseStackLimit, boosted));
        }
    }

    /// <summary>
    /// V7 (): Effektive Slot-Anzahl (Geld-Upgrade + Research-Bonus).
    /// </summary>
    public int EffectiveSlotCount =>
        Math.Min(MaxSlots, _gameState.State.WarehouseSlotCount
                          + BonusSlotsFromResearch
                          + BonusSlotsFromGuildMegaProject);

    /// <summary>
    /// V7 (, Plan Section 3.9): Bonus-Slots aus abgeschlossenen
    /// Gilden-Mega-Projekten (Kathedrale +3, Hauptquartier +5, beide gestapelt +8).
    /// </summary>
    public int BonusSlotsFromGuildMegaProject
        => _gameState.State.GuildMembership?.MegaProjectBonusWarehouseSlots ?? 0;

    public int MaxSlotCount => MaxSlots;

    public decimal GetTotalWarehouseValue() => _gameState.ExecuteWithLock(() =>
    {
        decimal total = 0m;
        foreach (var (productId, count) in _gameState.State.CraftingInventory)
        {
            if (count <= 0) continue;
            total += _crafting.GetSellPrice(productId) * count;
        }
        return total;
    });

    public bool CanAddToInventory(string productId, int count)
    {
        if (count <= 0) return true;
        return _gameState.ExecuteWithLock(() =>
        {
            var state = _gameState.State;
            int current = state.CraftingInventory.GetValueOrDefault(productId, 0);
            int effectiveLimit = CurrentStackLimit;

            // Bereits belegter Slot: nur Stack-Limit pruefen
            if (current > 0)
                return current + count <= effectiveLimit;

            // Neuer Slot: Slot-Limit pruefen
            return FreeSlotCount > 0 && count <= effectiveLimit;
        });
    }

    public int AddToInventory(string productId, int count, WorkshopType? sourceWorkshop = null)
    {
        if (count <= 0) return 0;

        int actuallyAdded;
        int overflow;
        bool slotBlocked; // True wenn kein Slot belegbar — Auto-Verkauf greift trotzdem

        int capturedAdded = 0;
        int capturedOverflow = 0;
        bool capturedBlocked = false;
        _gameState.ExecuteWithLock(() =>
        {
            var state = _gameState.State;
            int current = state.CraftingInventory.GetValueOrDefault(productId, 0);

            // Slot-Pruefung: Neuer Material-Typ braucht freien Slot
            if (current == 0 && FreeSlotCount <= 0)
            {
                capturedAdded = 0;
                capturedOverflow = count;
                capturedBlocked = true;
            }
            else
            {
                int effectiveLimit = CurrentStackLimit;
                int spaceInStack = effectiveLimit - current;
                capturedAdded = Math.Max(0, Math.Min(count, spaceInStack));
                capturedOverflow = count - capturedAdded;
                capturedBlocked = false;

                if (capturedAdded > 0)
                    state.CraftingInventory[productId] = current + capturedAdded;
            }
        });
        actuallyAdded = capturedAdded;
        overflow = capturedOverflow;
        slotBlocked = capturedBlocked;

        // Outside lock: Auto-Sell + Pause-Event
        if (overflow > 0)
        {
            var rule = GetAutoSellRule(productId);
            if (rule.Enabled)
            {
                // Auto-Verkauf zum aktuellen Marktpreis (greift auch wenn Slot blockiert).
                // V7 (, Plan Section 3.9): Gilden-Mega-Projekte
                // koennen +10/+20% Auto-Verkaufs-Preis-Bonus geben.
                decimal price = _crafting.GetSellPrice(productId);
                decimal megaBonus = _gameState.State.GuildMembership?.MegaProjectAutoSellPriceBonus ?? 0m;
                if (megaBonus > 0)
                    price *= (1m + megaBonus);
                decimal revenue = price * overflow;
                _gameState.AddMoney(revenue);
                OverflowAutoSold?.Invoke(productId, overflow, revenue);
            }
            else if (sourceWorkshop.HasValue)
            {
                // Pause-Signal an UI (Workshop-Card zeigt gelben Warn-Badge).
                WorkshopPaused?.Invoke(sourceWorkshop.Value, productId);
                // V7 (Telemetrie, Plan Section 8.1): warehouse_full_pause
                _analytics?.TrackEvent("warehouse_full_pause", new Dictionary<string, object?>
                {
                    ["workshop"] = sourceWorkshop.Value.ToString(),
                    ["product_id"] = productId,
                    ["slot_count"] = _gameState.State.WarehouseSlotCount,
                    ["used_slots"] = UsedSlotCount
                });
            }
            // sonst: stilles Verwerfen (z.B. Offline-Earnings — nicht-kritisch).
        }

        if (actuallyAdded > 0)
            InventoryChanged?.Invoke();

        _ = slotBlocked; // Suppress unused warning — Variable dokumentiert die Logik
        return actuallyAdded;
    }

    public bool TryReserve(string productId, int count)
    {
        if (count <= 0) return true;
        bool ok = _gameState.ExecuteWithLock(() =>
        {
            var state = _gameState.State;
            int current = state.CraftingInventory.GetValueOrDefault(productId, 0);
            int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
            if (current - reserved < count) return false;

            state.ReservedInventory[productId] = reserved + count;
            return true;
        });
        if (ok) InventoryChanged?.Invoke();
        return ok;
    }

    public bool ConsumeReserved(string productId, int count)
    {
        if (count <= 0) return true;
        bool ok = _gameState.ExecuteWithLock(() =>
        {
            var state = _gameState.State;
            int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
            int current = state.CraftingInventory.GetValueOrDefault(productId, 0);

            if (reserved < count) return false; // mehr verbrauchen als reserviert war = Bug
            if (current < count) return false;

            state.ReservedInventory[productId] = reserved - count;
            if (state.ReservedInventory[productId] <= 0)
                state.ReservedInventory.Remove(productId);

            state.CraftingInventory[productId] = current - count;
            if (state.CraftingInventory[productId] <= 0)
                state.CraftingInventory.Remove(productId);
            return true;
        });
        if (ok) InventoryChanged?.Invoke();
        return ok;
    }

    public bool ReleaseReserved(string productId, int count)
    {
        if (count <= 0) return true;
        bool ok = _gameState.ExecuteWithLock(() =>
        {
            var state = _gameState.State;
            int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
            if (reserved <= 0) return false;

            int toRelease = Math.Min(count, reserved);
            state.ReservedInventory[productId] = reserved - toRelease;
            if (state.ReservedInventory[productId] <= 0)
                state.ReservedInventory.Remove(productId);
            return true;
        });
        if (ok) InventoryChanged?.Invoke();
        return ok;
    }

    public int GetAvailable(string productId) => _gameState.ExecuteWithLock(() =>
    {
        var state = _gameState.State;
        int current = state.CraftingInventory.GetValueOrDefault(productId, 0);
        int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
        return Math.Max(0, current - reserved);
    });

    public AutoSellRule GetAutoSellRule(string productId)
    {
        var state = _gameState.State;
        if (!state.AutoSellRules.TryGetValue(productId, out var rule))
        {
            rule = new AutoSellRule { Enabled = false };
            state.AutoSellRules[productId] = rule;
        }
        return rule;
    }

    public void SetAutoSellEnabled(string productId, bool enabled)
    {
        var rule = GetAutoSellRule(productId);
        if (rule.Enabled == enabled) return;
        rule.Enabled = enabled;
        InventoryChanged?.Invoke();
    }

    public decimal GetNextSlotUpgradeCost()
    {
        int currentSlots = _gameState.State.WarehouseSlotCount;
        if (currentSlots >= MaxSlots) return 0m;

        // Wie viele Upgrades wurden schon gekauft? (Default = 20 Slots = 0 Upgrades)
        int upgradesDone = Math.Max(0, (currentSlots - 20) / SlotsPerUpgrade);
        return SlotUpgradeBaseCost * (decimal)Math.Pow(SlotUpgradeExponent, upgradesDone);
    }

    public bool CanUpgradeSlots()
    {
        if (_gameState.State.WarehouseSlotCount >= MaxSlots) return false;
        return _gameState.CanAfford(GetNextSlotUpgradeCost());
    }

    public bool TryUpgradeSlots()
    {
        if (_gameState.State.WarehouseSlotCount >= MaxSlots) return false;
        decimal cost = GetNextSlotUpgradeCost();
        if (!_gameState.TrySpendMoney(cost)) return false;

        _gameState.State.WarehouseSlotCount = Math.Min(MaxSlots, _gameState.State.WarehouseSlotCount + SlotsPerUpgrade);
        InventoryChanged?.Invoke();
        return true;
    }
}
