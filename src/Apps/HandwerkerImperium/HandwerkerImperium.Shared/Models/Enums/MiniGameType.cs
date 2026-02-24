namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Types of mini-games available in the game.
/// Each mini-game tests a different skill.
/// </summary>
public enum MiniGameType
{
    /// <summary>Timing-based: Stop the marker in the green zone</summary>
    Sawing = 0,

    /// <summary>Timing-based: Smooth planing motion</summary>
    Planing = 1,

    /// <summary>Puzzle: Connect pipes from start to end</summary>
    PipePuzzle = 2,

    /// <summary>Drag and Drop: Match wire colors</summary>
    WiringGame = 3,

    /// <summary>Swipe: Paint without going over edges</summary>
    PaintingGame = 4,

    /// <summary>Timing: Drop tiles in the right position</summary>
    TileLaying = 5,

    /// <summary>Timing: Measure and cut accurately</summary>
    Measuring = 6,

    /// <summary>Pattern: Dachziegel im korrekten Muster platzieren</summary>
    RoofTiling = 7,

    /// <summary>Memory: Bauschritte in korrekter Reihenfolge antippen</summary>
    Blueprint = 8,

    /// <summary>Puzzle: Räume im Grundriss zuordnen</summary>
    DesignPuzzle = 9,

    /// <summary>Suchbild: Fehler auf der Baustelle finden</summary>
    Inspection = 10,

    /// <summary>Timing: Metall auf Amboss hämmern bei richtiger Temperatur</summary>
    ForgeGame = 11,

    /// <summary>Puzzle: Bauteile in richtiger Reihenfolge zusammensetzen</summary>
    InventGame = 12
}

/// <summary>
/// Extension methods for MiniGameType.
/// </summary>
public static class MiniGameTypeExtensions
{
    /// <summary>
    /// Gets the route for navigation to this mini-game.
    /// </summary>
    public static string GetRoute(this MiniGameType type) => type switch
    {
        MiniGameType.Sawing => "minigame/sawing",
        MiniGameType.Planing => "minigame/sawing", // Uses same timing mechanic
        MiniGameType.PipePuzzle => "minigame/pipes",
        MiniGameType.WiringGame => "minigame/wiring",
        MiniGameType.PaintingGame => "minigame/painting",
        MiniGameType.TileLaying => "minigame/sawing", // Uses same timing mechanic
        MiniGameType.Measuring => "minigame/sawing", // Uses same timing mechanic
        MiniGameType.RoofTiling => "minigame/rooftiling",
        MiniGameType.Blueprint => "minigame/blueprint",
        MiniGameType.DesignPuzzle => "minigame/designpuzzle",
        MiniGameType.Inspection => "minigame/inspection",
        MiniGameType.ForgeGame => "minigame/forge",
        MiniGameType.InventGame => "minigame/invent",
        _ => "minigame/sawing"
    };

    /// <summary>
    /// Gets the workshop types that use this mini-game.
    /// </summary>
    public static WorkshopType[] GetWorkshopTypes(this MiniGameType type) => type switch
    {
        MiniGameType.Sawing => [WorkshopType.Carpenter],
        MiniGameType.Planing => [WorkshopType.Carpenter],
        MiniGameType.PipePuzzle => [WorkshopType.Plumber],
        MiniGameType.WiringGame => [WorkshopType.Electrician],
        MiniGameType.PaintingGame => [WorkshopType.Painter],
        MiniGameType.TileLaying => [WorkshopType.Roofer],
        MiniGameType.Measuring => [WorkshopType.Contractor, WorkshopType.Carpenter],
        MiniGameType.RoofTiling => [WorkshopType.Roofer],
        MiniGameType.Blueprint => [WorkshopType.Contractor],
        MiniGameType.DesignPuzzle => [WorkshopType.Architect],
        MiniGameType.Inspection => [WorkshopType.GeneralContractor],
        MiniGameType.ForgeGame => [WorkshopType.MasterSmith],
        MiniGameType.InventGame => [WorkshopType.InnovationLab],
        _ => [WorkshopType.Carpenter]
    };

    /// <summary>
    /// Gets the localization key for this mini-game.
    /// </summary>
    public static string GetLocalizationKey(this MiniGameType type) => type switch
    {
        MiniGameType.Sawing => "Sawing",
        MiniGameType.Planing => "Planing",
        MiniGameType.PipePuzzle => "PipePuzzle",
        MiniGameType.WiringGame => "WiringGame",
        MiniGameType.PaintingGame => "PaintingGame",
        MiniGameType.TileLaying => "TileLaying",
        MiniGameType.Measuring => "Measuring",
        MiniGameType.RoofTiling => "RoofTiling",
        MiniGameType.Blueprint => "Blueprint",
        MiniGameType.DesignPuzzle => "DesignPuzzle",
        MiniGameType.Inspection => "Inspection",
        MiniGameType.ForgeGame => "ForgeGame",
        MiniGameType.InventGame => "InventGame",
        _ => "Unknown"
    };
}
