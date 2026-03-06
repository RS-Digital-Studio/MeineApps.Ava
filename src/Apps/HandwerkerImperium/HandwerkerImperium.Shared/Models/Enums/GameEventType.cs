namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Types of random and seasonal events in the game.
/// Random events occur 1-2 times per day, seasonal events are time-based.
/// </summary>
public enum GameEventType
{
    // Random events (can happen anytime)

    /// <summary>Material prices drop temporarily (-30% costs)</summary>
    MaterialSale = 0,

    /// <summary>Material shortage, prices rise (+50% costs)</summary>
    MaterialShortage = 1,

    /// <summary>High demand period (+50% order rewards)</summary>
    HighDemand = 2,

    /// <summary>Economic downturn (-30% order rewards, +reputation gain)</summary>
    EconomicDownturn = 3,

    /// <summary>Tax audit (10% of money deducted as tax)</summary>
    TaxAudit = 4,

    /// <summary>Worker strike (all workers mood drops)</summary>
    WorkerStrike = 5,

    /// <summary>Innovation fair (+30% efficiency, +XP)</summary>
    InnovationFair = 6,

    /// <summary>Celebrity endorsement (+reputation, +income)</summary>
    CelebrityEndorsement = 7,

    // Seasonal events (tied to real-world time)

    /// <summary>Spring renovation season (Mon-Fri bonus)</summary>
    SpringSeason = 10,

    /// <summary>Summer construction boom (weekday bonus)</summary>
    SummerBoom = 11,

    /// <summary>Autumn maintenance surge (all week bonus)</summary>
    AutumnSurge = 12,

    /// <summary>Winter holiday slowdown (weekend penalty, weekday normal)</summary>
    WinterSlowdown = 13
}

public static class GameEventTypeExtensions
{
    /// <summary>
    /// Whether this is a random event (vs seasonal).
    /// </summary>
    public static bool IsRandom(this GameEventType type) => (int)type < 10;

    /// <summary>
    /// Whether this event is positive for the player.
    /// </summary>
    public static bool IsPositive(this GameEventType type) => type switch
    {
        GameEventType.MaterialSale => true,
        GameEventType.HighDemand => true,
        GameEventType.InnovationFair => true,
        GameEventType.CelebrityEndorsement => true,
        GameEventType.SpringSeason => true,
        GameEventType.SummerBoom => true,
        GameEventType.AutumnSurge => true,
        _ => false
    };

    /// <summary>
    /// Default duration for this event type.
    /// </summary>
    public static TimeSpan GetDefaultDuration(this GameEventType type) => type switch
    {
        GameEventType.TaxAudit => TimeSpan.FromHours(1),          // Short, immediate impact
        GameEventType.WorkerStrike => TimeSpan.FromHours(2),
        GameEventType.MaterialSale => TimeSpan.FromHours(6),
        GameEventType.MaterialShortage => TimeSpan.FromHours(4),
        GameEventType.HighDemand => TimeSpan.FromHours(8),
        GameEventType.EconomicDownturn => TimeSpan.FromHours(6),
        GameEventType.InnovationFair => TimeSpan.FromHours(4),
        GameEventType.CelebrityEndorsement => TimeSpan.FromHours(8),
        _ => TimeSpan.FromHours(24) // Seasonal events last 24h
    };

    /// <summary>
    /// GameIconKind-String für AXAML-Bindings via StringToGameIconKindConverter.
    /// </summary>
    public static string GetIcon(this GameEventType type) => type switch
    {
        GameEventType.MaterialSale => "Star",
        GameEventType.MaterialShortage => "AlertCircle",
        GameEventType.HighDemand => "CashMultiple",
        GameEventType.EconomicDownturn => "TrendingDown",
        GameEventType.TaxAudit => "Finance",
        GameEventType.WorkerStrike => "AccountOff",
        GameEventType.InnovationFair => "LightbulbOn",
        GameEventType.CelebrityEndorsement => "StarCircle",
        GameEventType.SpringSeason => "Flower",
        GameEventType.SummerBoom => "WhiteBalanceSunny",
        GameEventType.AutumnSurge => "Forest",
        GameEventType.WinterSlowdown => "Snowflake",
        _ => "InformationOutline"
    };

    /// <summary>
    /// Localization key for event name.
    /// </summary>
    public static string GetLocalizationKey(this GameEventType type) => $"Event{type}";

    /// <summary>
    /// Localization key for event description.
    /// </summary>
    public static string GetDescriptionKey(this GameEventType type) => $"Event{type}Desc";
}
