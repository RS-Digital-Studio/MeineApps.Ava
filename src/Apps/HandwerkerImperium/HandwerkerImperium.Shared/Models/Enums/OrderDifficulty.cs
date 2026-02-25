namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Difficulty levels for orders/contracts.
/// </summary>
public enum OrderDifficulty
{
    /// <summary>Easy - 1 star, forgiving timing</summary>
    Easy = 1,

    /// <summary>Medium - 2 stars, normal timing</summary>
    Medium = 2,

    /// <summary>Hard - 3 stars, precise timing required</summary>
    Hard = 3,

    /// <summary>Expert - 4 stars, requires Reputation 80+, very precise</summary>
    Expert = 4
}

/// <summary>
/// Extension methods for OrderDifficulty.
/// </summary>
public static class OrderDifficultyExtensions
{
    /// <summary>
    /// Gets the star display string.
    /// </summary>
    public static string GetStars(this OrderDifficulty difficulty) => difficulty switch
    {
        OrderDifficulty.Easy => "\u2b50",
        OrderDifficulty.Medium => "\u2b50\u2b50",
        OrderDifficulty.Hard => "\u2b50\u2b50\u2b50",
        OrderDifficulty.Expert => "\u2b50\u2b50\u2b50\u2b50",
        _ => "\u2b50"
    };

    /// <summary>
    /// Gets the reward multiplier for this difficulty.
    /// </summary>
    public static decimal GetRewardMultiplier(this OrderDifficulty difficulty) => difficulty switch
    {
        OrderDifficulty.Easy => 1.0m,
        OrderDifficulty.Medium => 1.5m,
        OrderDifficulty.Hard => 3.5m,
        OrderDifficulty.Expert => 3.5m, // war 8.0m - gesenkt für besseres Balancing
        _ => 1.0m
    };

    /// <summary>
    /// Gets the XP multiplier for this difficulty.
    /// </summary>
    public static decimal GetXpMultiplier(this OrderDifficulty difficulty) => difficulty switch
    {
        OrderDifficulty.Easy => 1.0m,
        OrderDifficulty.Medium => 1.75m,
        OrderDifficulty.Hard => 3.0m,
        OrderDifficulty.Expert => 5.5m, // war 7.0m - gesenkt für besseres Balancing
        _ => 1.0m
    };

    /// <summary>
    /// Gets the size of the "perfect" zone in the timing bar (0.0 - 1.0).
    /// Smaller = harder.
    /// </summary>
    public static double GetPerfectZoneSize(this OrderDifficulty difficulty) => difficulty switch
    {
        OrderDifficulty.Easy => 0.20,
        OrderDifficulty.Medium => 0.12,
        OrderDifficulty.Hard => 0.09,
        OrderDifficulty.Expert => 0.06,
        _ => 0.12
    };

    /// <summary>
    /// Gets the timing bar speed multiplier.
    /// Higher = faster = harder.
    /// </summary>
    public static double GetSpeedMultiplier(this OrderDifficulty difficulty) => difficulty switch
    {
        OrderDifficulty.Easy => 0.9,
        OrderDifficulty.Medium => 1.2,
        OrderDifficulty.Hard => 1.6,
        OrderDifficulty.Expert => 2.2,
        _ => 1.2
    };

    /// <summary>
    /// Minimum reputation required for this difficulty.
    /// </summary>
    public static int GetRequiredReputation(this OrderDifficulty difficulty) => difficulty switch
    {
        OrderDifficulty.Expert => 80,
        _ => 0
    };
}
