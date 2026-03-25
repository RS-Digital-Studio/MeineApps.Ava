namespace HandwerkerImperium.Models;

/// <summary>
/// VIP-Stufe basierend auf Gesamtausgaben.
/// </summary>
public enum VipTier
{
    None = 0,
    Bronze = 1,
    Silver = 2,
    Gold = 3,
    Platinum = 4
}

public static class VipTierExtensions
{
    /// <summary>
    /// Mindestausgaben für die VIP-Stufe (in EUR).
    /// </summary>
    public static decimal GetMinSpend(this VipTier tier) => tier switch
    {
        VipTier.Bronze => 4.99m,
        VipTier.Silver => 9.99m,
        VipTier.Gold => 19.99m,
        VipTier.Platinum => 49.99m,
        _ => decimal.MaxValue
    };

    /// <summary>
    /// Einkommens-Bonus der VIP-Stufe (gedeckelt — kein Pay-to-Win).
    /// Maximal +5% statt vorher +25%. VIP belohnt primär mit QoL, nicht Gameplay-Vorteil.
    /// </summary>
    public static decimal GetIncomeBonus(this VipTier tier) => tier switch
    {
        VipTier.Bronze => 0.02m,
        VipTier.Silver => 0.03m,
        VipTier.Gold => 0.04m,
        VipTier.Platinum => 0.05m,
        _ => 0m
    };

    /// <summary>
    /// XP-Bonus der VIP-Stufe (gedeckelt).
    /// </summary>
    public static decimal GetXpBonus(this VipTier tier) => tier switch
    {
        VipTier.Silver => 0.02m,
        VipTier.Gold => 0.03m,
        VipTier.Platinum => 0.05m,
        _ => 0m
    };

    /// <summary>
    /// Kosten-Reduktion der VIP-Stufe (entfernt — kein Pay-to-Win).
    /// Vorher Gold=5%, Platinum=10%. Jetzt 0% für alle.
    /// </summary>
    public static decimal GetCostReduction(this VipTier tier) => 0m;

    /// <summary>
    /// Ob VIP Auto-Claim für tägliche Login-Belohnungen freigeschaltet ist (Bronze+).
    /// </summary>
    public static bool HasAutoClaimDailyRewards(this VipTier tier) => tier >= VipTier.Bronze;

    /// <summary>
    /// Ob VIP den Lieferanten-Timer sehen kann (Silver+, QoL: Countdown statt Überraschung).
    /// </summary>
    public static bool HasDeliveryTimer(this VipTier tier) => tier >= VipTier.Silver;

    /// <summary>
    /// Ob VIP einen exklusiven Avatar-Rahmen hat (Gold+, rein kosmetisch).
    /// </summary>
    public static bool HasExclusiveFrame(this VipTier tier) => tier >= VipTier.Gold;

    /// <summary>
    /// Farbe der VIP-Stufe.
    /// </summary>
    public static string GetColor(this VipTier tier) => tier switch
    {
        VipTier.Bronze => "#CD7F32",
        VipTier.Silver => "#C0C0C0",
        VipTier.Gold => "#FFD700",
        VipTier.Platinum => "#E5E4E2",
        _ => "#808080"
    };

    /// <summary>
    /// Bestimmt VIP-Stufe aus Gesamtausgaben.
    /// </summary>
    public static VipTier DetermineVipTier(decimal totalSpent)
    {
        if (totalSpent >= VipTier.Platinum.GetMinSpend()) return VipTier.Platinum;
        if (totalSpent >= VipTier.Gold.GetMinSpend()) return VipTier.Gold;
        if (totalSpent >= VipTier.Silver.GetMinSpend()) return VipTier.Silver;
        if (totalSpent >= VipTier.Bronze.GetMinSpend()) return VipTier.Bronze;
        return VipTier.None;
    }
}
