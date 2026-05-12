using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// V7 (Phase 1 Ressourcen-Plan): Lager-Verwaltung mit Slots + Stack-Limits + Reservierung.
/// Wird von AutoProductionService, CraftingService und der Lager-UI im Imperium-Tab konsumiert.
/// </summary>
public sealed class WarehouseService : IWarehouseService
{
    private readonly IGameStateService _gameState;
    private readonly ICraftingService _crafting;
    // Lock verhindert Race zwischen AddToInventory (Auto-Production) und Reservation
    // (Order-Annahme aus UI). Inventar wuerde sonst in Dictionary inkonsistent.
    private readonly object _warehouseLock = new();

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

    public WarehouseService(IGameStateService gameState, ICraftingService crafting)
    {
        _gameState = gameState;
        _crafting = crafting;
        // Bei State-Wechsel (Prestige/Import/Reset): UI muss Inventar neu lesen
        _gameState.StateLoaded += (_, _) => InventoryChanged?.Invoke();
    }

    public int UsedSlotCount
    {
        get
        {
            int used = 0;
            foreach (var kv in _gameState.State.CraftingInventory)
                if (kv.Value > 0) used++;
            return used;
        }
    }

    public int FreeSlotCount => Math.Max(0, _gameState.State.WarehouseSlotCount - UsedSlotCount);

    public bool IsWarehouseFull => FreeSlotCount == 0;

    public int CurrentStackLimit => _gameState.State.WarehouseStackLimit;

    public int MaxSlotCount => MaxSlots;

    public decimal GetTotalWarehouseValue()
    {
        decimal total = 0m;
        foreach (var (productId, count) in _gameState.State.CraftingInventory)
        {
            if (count <= 0) continue;
            total += _crafting.GetSellPrice(productId) * count;
        }
        return total;
    }

    public bool CanAddToInventory(string productId, int count)
    {
        if (count <= 0) return true;
        var state = _gameState.State;
        int current = state.CraftingInventory.GetValueOrDefault(productId, 0);

        // Bereits belegter Slot: nur Stack-Limit pruefen
        if (current > 0)
            return current + count <= state.WarehouseStackLimit;

        // Neuer Slot: Slot-Limit pruefen
        return FreeSlotCount > 0 && count <= state.WarehouseStackLimit;
    }

    public int AddToInventory(string productId, int count, WorkshopType? sourceWorkshop = null)
    {
        if (count <= 0) return 0;

        int actuallyAdded;
        int overflow;
        bool slotBlocked; // True wenn kein Slot belegbar — Auto-Verkauf greift trotzdem

        lock (_warehouseLock)
        {
            var state = _gameState.State;
            int current = state.CraftingInventory.GetValueOrDefault(productId, 0);

            // Slot-Pruefung: Neuer Material-Typ braucht freien Slot
            if (current == 0 && FreeSlotCount <= 0)
            {
                actuallyAdded = 0;
                overflow = count;
                slotBlocked = true;
            }
            else
            {
                int spaceInStack = state.WarehouseStackLimit - current;
                actuallyAdded = Math.Max(0, Math.Min(count, spaceInStack));
                overflow = count - actuallyAdded;
                slotBlocked = false;

                if (actuallyAdded > 0)
                    state.CraftingInventory[productId] = current + actuallyAdded;
            }
        }

        // Outside lock: Auto-Sell + Pause-Event
        if (overflow > 0)
        {
            var rule = GetAutoSellRule(productId);
            if (rule.Enabled)
            {
                // Auto-Verkauf zum aktuellen Marktpreis (greift auch wenn Slot blockiert).
                decimal price = _crafting.GetSellPrice(productId);
                decimal revenue = price * overflow;
                _gameState.AddMoney(revenue);
                OverflowAutoSold?.Invoke(productId, overflow, revenue);
            }
            else if (sourceWorkshop.HasValue)
            {
                // Pause-Signal an UI (Workshop-Card zeigt gelben Warn-Badge).
                WorkshopPaused?.Invoke(sourceWorkshop.Value, productId);
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
        lock (_warehouseLock)
        {
            var state = _gameState.State;
            int current = state.CraftingInventory.GetValueOrDefault(productId, 0);
            int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
            if (current - reserved < count) return false;

            state.ReservedInventory[productId] = reserved + count;
        }
        InventoryChanged?.Invoke();
        return true;
    }

    public bool ConsumeReserved(string productId, int count)
    {
        if (count <= 0) return true;
        lock (_warehouseLock)
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
        }
        InventoryChanged?.Invoke();
        return true;
    }

    public bool ReleaseReserved(string productId, int count)
    {
        if (count <= 0) return true;
        lock (_warehouseLock)
        {
            var state = _gameState.State;
            int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
            if (reserved <= 0) return false;

            int toRelease = Math.Min(count, reserved);
            state.ReservedInventory[productId] = reserved - toRelease;
            if (state.ReservedInventory[productId] <= 0)
                state.ReservedInventory.Remove(productId);
        }
        InventoryChanged?.Invoke();
        return true;
    }

    public int GetAvailable(string productId)
    {
        var state = _gameState.State;
        int current = state.CraftingInventory.GetValueOrDefault(productId, 0);
        int reserved = state.ReservedInventory.GetValueOrDefault(productId, 0);
        return Math.Max(0, current - reserved);
    }

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
