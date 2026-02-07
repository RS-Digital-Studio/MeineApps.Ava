using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service for the 3-tier prestige system (Bronze / Silver / Gold).
/// Players reset progress to gain permanent multipliers and prestige points.
/// </summary>
public interface IPrestigeService
{
    /// <summary>
    /// Whether the player can prestige at the given tier.
    /// Checks player level and previous-tier completion requirements.
    /// </summary>
    bool CanPrestige(PrestigeTier tier);

    /// <summary>
    /// Calculates how many prestige points the player would earn.
    /// Formula: floor(sqrt(totalMoneyEarned / 100_000)) * tier multiplier
    /// </summary>
    int GetPrestigePoints(decimal totalMoneyEarned);

    /// <summary>
    /// Performs the prestige reset for the given tier.
    /// Resets: PlayerLevel, CurrentXp, Money (100 + shop bonus), Workshops (only Carpenter with 1 E worker),
    ///         AvailableOrders, ActiveOrder, Statistics, Boosts, DailyRewards.
    /// Preserves: Achievements, Premium, Settings, PrestigeData, Tutorial, TotalMoneyEarned, TotalPlayTimeSeconds.
    /// Silver preserves Research. Gold preserves Shop items.
    /// </summary>
    Task<bool> DoPrestige(PrestigeTier tier);

    /// <summary>
    /// Returns all prestige shop items with their purchase state.
    /// </summary>
    List<PrestigeShopItem> GetShopItems();

    /// <summary>
    /// Buys a prestige shop item by ID. Deducts prestige points.
    /// Returns true if successful.
    /// </summary>
    bool BuyShopItem(string itemId);

    /// <summary>
    /// Gets the current permanent income multiplier (base 1.0 + all bonuses).
    /// Includes tier-based multiplier bonus + shop item income multipliers.
    /// </summary>
    decimal GetPermanentMultiplier();

    /// <summary>
    /// Event fired when prestige is completed.
    /// </summary>
    event EventHandler? PrestigeCompleted;
}
