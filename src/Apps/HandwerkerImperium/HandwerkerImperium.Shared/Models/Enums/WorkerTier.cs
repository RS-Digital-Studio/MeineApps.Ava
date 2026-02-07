namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Worker quality tiers from F (lowest) to S (highest).
/// Higher tiers have better efficiency ranges and cost more wages.
/// </summary>
public enum WorkerTier
{
    F = 0,
    E = 1,
    D = 2,
    C = 3,
    B = 4,
    A = 5,
    S = 6
}

public static class WorkerTierExtensions
{
    /// <summary>
    /// Minimum base efficiency for this tier (before mood/fatigue).
    /// </summary>
    public static decimal GetMinEfficiency(this WorkerTier tier) => tier switch
    {
        WorkerTier.F => 0.30m,
        WorkerTier.E => 0.50m,
        WorkerTier.D => 0.65m,
        WorkerTier.C => 0.80m,
        WorkerTier.B => 0.95m,
        WorkerTier.A => 1.10m,
        WorkerTier.S => 1.30m,
        _ => 0.50m
    };

    /// <summary>
    /// Maximum base efficiency for this tier (before mood/fatigue).
    /// </summary>
    public static decimal GetMaxEfficiency(this WorkerTier tier) => tier switch
    {
        WorkerTier.F => 0.50m,
        WorkerTier.E => 0.70m,
        WorkerTier.D => 0.85m,
        WorkerTier.C => 1.00m,
        WorkerTier.B => 1.20m,
        WorkerTier.A => 1.40m,
        WorkerTier.S => 1.50m,
        _ => 0.70m
    };

    /// <summary>
    /// Hourly wage for workers of this tier.
    /// </summary>
    public static decimal GetWagePerHour(this WorkerTier tier) => tier switch
    {
        WorkerTier.F => 5m,
        WorkerTier.E => 10m,
        WorkerTier.D => 20m,
        WorkerTier.C => 40m,
        WorkerTier.B => 80m,
        WorkerTier.A => 160m,
        WorkerTier.S => 320m,
        _ => 10m
    };

    /// <summary>
    /// Hiring cost for workers of this tier.
    /// </summary>
    public static decimal GetHiringCost(this WorkerTier tier) => tier switch
    {
        WorkerTier.F => 50m,
        WorkerTier.E => 200m,
        WorkerTier.D => 1_000m,
        WorkerTier.C => 5_000m,
        WorkerTier.B => 25_000m,
        WorkerTier.A => 100_000m,
        WorkerTier.S => 500_000m,
        _ => 200m
    };

    /// <summary>
    /// Player level required to unlock this tier in the worker market.
    /// </summary>
    public static int GetUnlockLevel(this WorkerTier tier) => tier switch
    {
        WorkerTier.F => 1,
        WorkerTier.E => 1,
        WorkerTier.D => 8,
        WorkerTier.C => 15,
        WorkerTier.B => 25,
        WorkerTier.A => 35,
        WorkerTier.S => 45,  // Also requires research unlock
        _ => 1
    };

    /// <summary>
    /// Color key for UI display of this tier.
    /// </summary>
    public static string GetColorKey(this WorkerTier tier) => tier switch
    {
        WorkerTier.F => "#9E9E9E",  // Grey
        WorkerTier.E => "#4CAF50",  // Green
        WorkerTier.D => "#2196F3",  // Blue
        WorkerTier.C => "#9C27B0",  // Purple
        WorkerTier.B => "#FFC107",  // Gold
        WorkerTier.A => "#F44336",  // Red
        WorkerTier.S => "#FF9800",  // Rainbow/Orange (animated in UI)
        _ => "#9E9E9E"
    };

    /// <summary>
    /// Localization key for tier name.
    /// </summary>
    public static string GetLocalizationKey(this WorkerTier tier) => $"Tier{tier}";
}
