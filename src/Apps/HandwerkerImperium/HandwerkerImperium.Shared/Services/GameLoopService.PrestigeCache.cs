using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Partial: Prestige-Shop-Effekte-Cache (Income, Rush, Delivery, UpgradeDiscount).
/// Wird nur bei Kauf oder Prestige-Reset invalidiert, nicht pro Tick berechnet.
/// </summary>
public sealed partial class GameLoopService
{
    // Gecachte Prestige-Shop-Effekte (werden nur bei Kauf invalidiert)
    private bool _prestigeEffectsDirty = true;
    private decimal _cachedPrestigeIncomeBonus;
    private decimal _cachedPrestigeRushBonus;
    private decimal _cachedPrestigeDeliveryBonus;
    private decimal _cachedPrestigeUpgradeDiscount;

    /// <summary>
    /// Prestige-Effekt-Cache invalidieren (nach Prestige-Shop-Kauf oder Prestige-Reset).
    /// </summary>
    public void InvalidatePrestigeEffects() => _prestigeEffectsDirty = true;

    /// <summary>
    /// Berechnet alle 3 Prestige-Shop-Boni in einem einzigen Durchlauf und cacht sie.
    /// </summary>
    private void RefreshPrestigeEffectsIfNeeded(GameState state)
    {
        if (!_prestigeEffectsDirty) return;
        _prestigeEffectsDirty = false;

        _cachedPrestigeIncomeBonus = 0m;
        _cachedPrestigeRushBonus = 0m;
        _cachedPrestigeDeliveryBonus = 0m;
        _cachedPrestigeUpgradeDiscount = 0m;

        var purchased = state.Prestige.PurchasedShopItems;
        var repeatableCounts = state.Prestige.RepeatableItemCounts;

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
                    if (item.Effect.IncomeMultiplier > 0)
                        _cachedPrestigeIncomeBonus += item.Effect.IncomeMultiplier * count;
                    if (item.Effect.DeliverySpeedBonus > 0)
                        _cachedPrestigeDeliveryBonus += item.Effect.DeliverySpeedBonus * count;
                }
                continue;
            }

            if (!purchased.Contains(item.Id)) continue;

            if (item.Effect.IncomeMultiplier > 0)
                _cachedPrestigeIncomeBonus += item.Effect.IncomeMultiplier;
            if (item.Effect.RushMultiplierBonus > 0)
                _cachedPrestigeRushBonus += item.Effect.RushMultiplierBonus;
            if (item.Effect.DeliverySpeedBonus > 0)
                _cachedPrestigeDeliveryBonus += item.Effect.DeliverySpeedBonus;
            if (item.Effect.UpgradeDiscount > 0)
                _cachedPrestigeUpgradeDiscount += item.Effect.UpgradeDiscount;
        }

        // Prestige-Income-Bonus auf 300% deckeln (verhindert Exploit durch wiederholbare Items)
        _cachedPrestigeIncomeBonus = Math.Min(_cachedPrestigeIncomeBonus, 3.0m);

        // Upgrade-Discount + VIP-Kosten-Reduktion auf alle Workshops setzen (nur bei Invalidierung statt pro Tick)
        // Immer setzen, auch bei 0 → nach Prestige-Reset muessen alte Discounts geloescht werden
        decimal vipCostReduction = _vipService?.CostReduction ?? 0m;
        for (int i = 0; i < state.Workshops.Count; i++)
        {
            state.Workshops[i].UpgradeDiscount = _cachedPrestigeUpgradeDiscount;
            state.Workshops[i].VipCostReduction = vipCostReduction;
        }
    }
}
