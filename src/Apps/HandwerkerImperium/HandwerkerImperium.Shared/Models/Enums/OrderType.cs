namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Types of orders/contracts with varying complexity and rewards.
/// </summary>
public enum OrderType
{
    /// <summary>1 mini-game, quick reward, no cooldown</summary>
    Quick = 0,

    /// <summary>2-3 mini-games, standard reward</summary>
    Standard = 1,

    /// <summary>4-6 mini-games, high reward</summary>
    Large = 2,

    /// <summary>10 mini-games, 7 day deadline, very high reward</summary>
    Weekly = 3,

    /// <summary>3 mini-games across 2+ workshop types, bonus reward</summary>
    Cooperation = 4
}

public static class OrderTypeExtensions
{
    /// <summary>
    /// Number of tasks (mini-games) for this order type.
    /// Returns (min, max) range.
    /// </summary>
    public static (int Min, int Max) GetTaskCount(this OrderType type) => type switch
    {
        OrderType.Quick => (1, 1),
        OrderType.Standard => (2, 3),
        OrderType.Large => (4, 6),
        OrderType.Weekly => (10, 10),
        OrderType.Cooperation => (3, 3),
        _ => (2, 3)
    };

    /// <summary>
    /// Reward multiplier compared to a standard order.
    /// </summary>
    public static decimal GetRewardMultiplier(this OrderType type) => type switch
    {
        OrderType.Quick => 0.6m,
        OrderType.Standard => 1.0m,
        OrderType.Large => 1.8m,
        OrderType.Weekly => 4.0m,
        OrderType.Cooperation => 2.5m,
        _ => 1.0m
    };

    /// <summary>
    /// XP multiplier compared to a standard order.
    /// </summary>
    public static decimal GetXpMultiplier(this OrderType type) => type switch
    {
        OrderType.Quick => 0.5m,
        OrderType.Standard => 1.0m,
        OrderType.Large => 2.0m,
        OrderType.Weekly => 5.0m,
        OrderType.Cooperation => 3.0m,
        _ => 1.0m
    };

    /// <summary>
    /// Minimum player level to access this order type.
    /// </summary>
    public static int GetUnlockLevel(this OrderType type) => type switch
    {
        OrderType.Quick => 1,
        OrderType.Standard => 1,
        OrderType.Large => 10,
        OrderType.Weekly => 20,
        OrderType.Cooperation => 15,
        _ => 1
    };

    /// <summary>
    /// Whether this order type has a time limit.
    /// </summary>
    public static bool HasDeadline(this OrderType type) => type switch
    {
        OrderType.Weekly => true,
        _ => false
    };

    /// <summary>
    /// Default deadline duration (only for timed orders).
    /// </summary>
    public static TimeSpan? GetDeadline(this OrderType type) => type switch
    {
        OrderType.Weekly => TimeSpan.FromDays(7),
        _ => null
    };

    /// <summary>
    /// Whether this order type requires multiple workshop types.
    /// </summary>
    public static bool RequiresMultipleWorkshops(this OrderType type) => type == OrderType.Cooperation;

    /// <summary>
    /// Icon for this order type.
    /// </summary>
    public static string GetIcon(this OrderType type) => type switch
    {
        OrderType.Quick => "\u26a1",           // Lightning bolt
        OrderType.Standard => "\ud83d\udccb",  // Clipboard
        OrderType.Large => "\ud83d\udce6",     // Package
        OrderType.Weekly => "\ud83d\udcc5",    // Calendar
        OrderType.Cooperation => "\ud83e\udd1d", // Handshake
        _ => "\ud83d\udccb"
    };

    /// <summary>
    /// Localization key for order type name.
    /// </summary>
    public static string GetLocalizationKey(this OrderType type) => $"OrderType{type}";
}
