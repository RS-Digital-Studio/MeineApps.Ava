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
    Cooperation = 4,

    /// <summary>Kein MiniGame - Crafting-Items liefern für sofortige Belohnung</summary>
    MaterialOrder = 5
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
        OrderType.MaterialOrder => (0, 0), // Kein MiniGame
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
        OrderType.Weekly => 3.0m,  // BAL-14: Von 2.5 auf 3.0 angehoben (war identisch mit Cooperation, Weekly braucht eigene Identität)
        OrderType.Cooperation => 2.5m,
        OrderType.MaterialOrder => GameBalanceConstants.MaterialOrderRewardMultiplier,
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
        OrderType.Weekly => 3.0m,  // BAL-11: XP proportional angepasst
        OrderType.Cooperation => 3.0m,
        OrderType.MaterialOrder => GameBalanceConstants.MaterialOrderXpMultiplier,
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
        OrderType.MaterialOrder => GameBalanceConstants.AutoProductionUnlockLevel,
        _ => 1
    };

    /// <summary>
    /// Whether this order type has a time limit.
    /// </summary>
    public static bool HasDeadline(this OrderType type) => type switch
    {
        OrderType.Weekly => true,
        OrderType.MaterialOrder => true,
        _ => false
    };

    /// <summary>
    /// Default deadline duration (only for timed orders).
    /// </summary>
    public static TimeSpan? GetDeadline(this OrderType type) => type switch
    {
        OrderType.Weekly => TimeSpan.FromDays(7),
        OrderType.MaterialOrder => TimeSpan.FromHours(GameBalanceConstants.MaterialOrderDeadlineHours),
        _ => null
    };

    /// <summary>
    /// Whether this order type requires multiple workshop types.
    /// </summary>
    public static bool RequiresMultipleWorkshops(this OrderType type) =>
        type == OrderType.Cooperation;

    /// <summary>
    /// Ob dieser Auftragstyp ein Lieferauftrag ist (kein MiniGame, Items liefern).
    /// </summary>
    public static bool IsMaterialOrder(this OrderType type) => type == OrderType.MaterialOrder;

    /// <summary>
    /// GameIconKind-String für AXAML-Bindings via StringToGameIconKindConverter.
    /// </summary>
    public static string GetIcon(this OrderType type) => type switch
    {
        OrderType.Quick => "Flash",
        OrderType.Standard => "ClipboardList",
        OrderType.Large => "ClipboardTextMultiple",
        OrderType.Weekly => "CalendarCheck",
        OrderType.Cooperation => "Handshake",
        OrderType.MaterialOrder => "PackageVariantClosed",
        _ => "ClipboardList"
    };

    /// <summary>
    /// Localization key for order type name.
    /// </summary>
    public static string GetLocalizationKey(this OrderType type) => $"OrderType{type}";
}
