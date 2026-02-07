namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Types of workshops/trades in the game.
/// Each type has unique mini-games and progression.
/// </summary>
public enum WorkshopType
{
    /// <summary>Woodworking - Sawing, Planing, Assembly</summary>
    Carpenter = 0,

    /// <summary>Plumbing - Pipe puzzles, Fittings</summary>
    Plumber = 1,

    /// <summary>Electrical - Wiring, Circuits</summary>
    Electrician = 2,

    /// <summary>Painting - Brush strokes, Color mixing</summary>
    Painter = 3,

    /// <summary>Roofing - Tile laying, Measurements</summary>
    Roofer = 4,

    /// <summary>General Contractor - Large projects, Management</summary>
    Contractor = 5,

    /// <summary>Architecture - Design, Planning (Prestige 1 exclusive)</summary>
    Architect = 6,

    /// <summary>General Contractor Plus - Full-service (Prestige 3 exclusive)</summary>
    GeneralContractor = 7
}

/// <summary>
/// Extension methods for WorkshopType.
/// </summary>
public static class WorkshopTypeExtensions
{
    /// <summary>
    /// Gets the player level required to unlock this workshop.
    /// </summary>
    public static int GetUnlockLevel(this WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => 1,
        WorkshopType.Plumber => 8,
        WorkshopType.Electrician => 15,
        WorkshopType.Painter => 22,
        WorkshopType.Roofer => 30,
        WorkshopType.Contractor => 40,
        WorkshopType.Architect => 1,            // Available at level 1 but requires Prestige 1
        WorkshopType.GeneralContractor => 1,    // Available at level 1 but requires Prestige 3
        _ => 1
    };

    /// <summary>
    /// Gets the prestige tier required to unlock this workshop.
    /// 0 = no prestige needed, 1 = Bronze, 3 = Gold.
    /// </summary>
    public static int GetRequiredPrestige(this WorkshopType type) => type switch
    {
        WorkshopType.Architect => 1,
        WorkshopType.GeneralContractor => 3,
        _ => 0
    };

    /// <summary>
    /// Gets the cost to unlock/purchase this workshop.
    /// Must be paid in addition to meeting the level requirement.
    /// </summary>
    public static decimal GetUnlockCost(this WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => 0m,
        WorkshopType.Plumber => 5_000m,
        WorkshopType.Electrician => 50_000m,
        WorkshopType.Painter => 500_000m,
        WorkshopType.Roofer => 5_000_000m,
        WorkshopType.Contractor => 50_000_000m,
        WorkshopType.Architect => 500_000_000m,
        WorkshopType.GeneralContractor => 5_000_000_000m,
        _ => 0m
    };

    /// <summary>
    /// Gets the icon/emoji for this workshop type.
    /// </summary>
    public static string GetIcon(this WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => "\ud83e\ude9a",
        WorkshopType.Plumber => "\ud83d\udd27",
        WorkshopType.Electrician => "\u26a1",
        WorkshopType.Painter => "\ud83c\udfa8",
        WorkshopType.Roofer => "\ud83c\udfe0",
        WorkshopType.Contractor => "\ud83c\udfd7\ufe0f",
        WorkshopType.Architect => "\ud83d\udcd0",       // Triangular ruler
        WorkshopType.GeneralContractor => "\ud83c\udff0", // Castle
        _ => "\ud83d\udd27"
    };

    /// <summary>
    /// Gets the localization key for this workshop type.
    /// </summary>
    public static string GetLocalizationKey(this WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => "Carpenter",
        WorkshopType.Plumber => "Plumber",
        WorkshopType.Electrician => "Electrician",
        WorkshopType.Painter => "Painter",
        WorkshopType.Roofer => "Roofer",
        WorkshopType.Contractor => "Contractor",
        WorkshopType.Architect => "Architect",
        WorkshopType.GeneralContractor => "GeneralContractor",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the base income multiplier for this workshop type.
    /// Higher-tier workshops earn more per worker.
    /// </summary>
    public static decimal GetBaseIncomeMultiplier(this WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => 1.0m,
        WorkshopType.Plumber => 1.5m,
        WorkshopType.Electrician => 2.0m,
        WorkshopType.Painter => 2.5m,
        WorkshopType.Roofer => 3.0m,
        WorkshopType.Contractor => 4.0m,
        WorkshopType.Architect => 5.0m,
        WorkshopType.GeneralContractor => 7.0m,
        _ => 1.0m
    };

    /// <summary>
    /// Whether this workshop requires prestige to unlock.
    /// </summary>
    public static bool IsPrestigeExclusive(this WorkshopType type) =>
        type.GetRequiredPrestige() > 0;
}
