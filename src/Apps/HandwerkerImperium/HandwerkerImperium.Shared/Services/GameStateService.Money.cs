using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Geld- und Goldschrauben-Operationen inkl. Prestige-Bonus-Cache.
/// </summary>
public sealed partial class GameStateService
{
    // Gecachte Prestige-Shop-Boni (GS, XP, OrderReward) - werden bei Kauf/Prestige/StateLoad invalidiert
    private bool _prestigeBonusCacheDirty = true;
    private decimal _cachedGoldenScrewBonus;
    private decimal _cachedXpBonus;
    private decimal _cachedOrderRewardBonus;

    /// <summary>
    /// Prestige-Shop-Bonus-Cache invalidieren (nach Prestige-Shop-Kauf, Prestige-Reset oder State-Load).
    /// Feuert PrestigeShopPurchased Event für abhängige Services (z.B. CraftingService).
    /// </summary>
    public void InvalidatePrestigeBonusCache()
    {
        _prestigeBonusCacheDirty = true;
        PrestigeShopPurchased?.Invoke(this, EventArgs.Empty);
    }

    // ===================================================================
    // GELD-OPERATIONEN (Thread-safe)
    // ===================================================================

    public void AddMoney(decimal amount)
    {
        if (amount <= 0) return;

        decimal oldAmount;
        decimal newAmount;

        lock (_stateLock)
        {
            oldAmount = _state.Money;
            _state.Money += amount;
            _state.TotalMoneyEarned += amount;
            _state.CurrentRunMoney += amount;
            newAmount = _state.Money;
        }

        MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(oldAmount, newAmount));
    }

    public bool TrySpendMoney(decimal amount)
    {
        decimal oldAmount;
        decimal newAmount;

        lock (_stateLock)
        {
            if (amount <= 0 || _state.Money < amount)
                return false;

            oldAmount = _state.Money;
            _state.Money -= amount;
            _state.TotalMoneySpent += amount;
            newAmount = _state.Money;
        }

        MoneyChanged?.Invoke(this, new MoneyChangedEventArgs(oldAmount, newAmount));
        return true;
    }

    public bool CanAfford(decimal amount)
    {
        lock (_stateLock)
        {
            return _state.Money >= amount;
        }
    }

    // ===================================================================
    // GOLDSCHRAUBEN-OPERATIONEN (Thread-safe)
    // ===================================================================

    /// <summary>
    /// Externer GS-Bonus-Provider (z.B. Ascension Golden-Era-Perk).
    /// Wird nach DI-Auflösung gesetzt, um zirkuläre Dependencies zu vermeiden.
    /// Gibt den Bonus als Dezimalwert zurück (z.B. 0.2 = +20%).
    /// </summary>
    public Func<decimal>? ExternalGoldenScrewBonusProvider { get; set; }

    public void AddGoldenScrews(int amount, bool fromPurchase = false)
    {
        if (amount <= 0) return;

        // Prestige-Shop GoldenScrewBonus anwenden (z.B. +25%) - nur für Gameplay-Quellen
        if (!fromPurchase)
        {
            decimal bonus = GetGoldenScrewBonus();

            // Ascension Golden-Era-Perk: GS-Verdienst-Bonus (stackt additiv mit Prestige-Shop)
            decimal ascensionBonus = ExternalGoldenScrewBonusProvider?.Invoke() ?? 0m;
            bonus += ascensionBonus;

            if (bonus > 0)
                amount = (int)Math.Ceiling(amount * (1m + bonus));

            // Premium: +100% Goldschrauben aus Gameplay-Quellen (Mini-Games, Challenges, Meilensteine etc.)
            if (_state.IsPremium)
                amount *= 2;
        }

        int oldAmount;
        int newAmount;

        lock (_stateLock)
        {
            oldAmount = _state.GoldenScrews;
            _state.GoldenScrews += amount;
            _state.TotalGoldenScrewsEarned += amount;
            newAmount = _state.GoldenScrews;
        }

        GoldenScrewsChanged?.Invoke(this, new GoldenScrewsChangedEventArgs(oldAmount, newAmount));
    }

    /// <summary>
    /// Gibt den gecachten Goldschrauben-Bonus zurück (refresht bei Bedarf).
    /// </summary>
    private decimal GetGoldenScrewBonus()
    {
        RefreshPrestigeBonusCacheIfNeeded();
        return _cachedGoldenScrewBonus;
    }

    public bool TrySpendGoldenScrews(int amount)
    {
        int oldAmount;
        int newAmount;

        lock (_stateLock)
        {
            if (amount <= 0 || _state.GoldenScrews < amount)
                return false;

            oldAmount = _state.GoldenScrews;
            _state.GoldenScrews -= amount;
            _state.TotalGoldenScrewsSpent += amount;
            newAmount = _state.GoldenScrews;
        }

        GoldenScrewsChanged?.Invoke(this, new GoldenScrewsChangedEventArgs(oldAmount, newAmount));
        return true;
    }

    public bool CanAffordGoldenScrews(int amount)
    {
        lock (_stateLock)
        {
            return _state.GoldenScrews >= amount;
        }
    }

    // ===================================================================
    // PRESTIGE-BONUS-CACHE
    // ===================================================================

    /// <summary>
    /// Berechnet alle Prestige-Shop-Boni (GS, XP, OrderReward) in einem einzigen Durchlauf und cacht sie.
    /// Wird nur bei Dirty-Flag neu berechnet (nach Kauf, Prestige, StateLoad).
    /// </summary>
    private void RefreshPrestigeBonusCacheIfNeeded()
    {
        if (!_prestigeBonusCacheDirty) return;
        _prestigeBonusCacheDirty = false;

        _cachedGoldenScrewBonus = 0m;
        _cachedXpBonus = 0m;
        _cachedOrderRewardBonus = 0m;

        var purchased = _state.Prestige.PurchasedShopItems;
        var repeatableCounts = _state.Prestige.RepeatableItemCounts;
        if (purchased.Count == 0 && repeatableCounts.Count == 0) return;

        var allItems = PrestigeShop.GetAllItems();
        for (int i = 0; i < allItems.Count; i++)
        {
            var item = allItems[i];

            // Wiederholbare Items: Effekt * Kaufanzahl
            if (item.IsRepeatable)
            {
                if (repeatableCounts.TryGetValue(item.Id, out var count) && count > 0)
                {
                    if (item.Effect.OrderRewardBonus > 0)
                        _cachedOrderRewardBonus += item.Effect.OrderRewardBonus * count;
                }
                continue;
            }

            if (!purchased.Contains(item.Id)) continue;

            if (item.Effect.GoldenScrewBonus > 0)
                _cachedGoldenScrewBonus += item.Effect.GoldenScrewBonus;
            if (item.Effect.XpMultiplier > 0)
                _cachedXpBonus += item.Effect.XpMultiplier;
            if (item.Effect.OrderRewardBonus > 0)
                _cachedOrderRewardBonus += item.Effect.OrderRewardBonus;
        }
    }
}
