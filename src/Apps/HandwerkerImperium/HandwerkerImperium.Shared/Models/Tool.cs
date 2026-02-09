using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Models;

/// <summary>
/// Werkzeug-Typ fuer Minigame-Boni.
/// </summary>
public enum ToolType
{
    Saw,
    PipeWrench,
    Screwdriver,
    Paintbrush
}

/// <summary>
/// Ein Werkzeug, das im Shop aufgewertet werden kann und Minigame-Boni gibt.
/// </summary>
public class Tool
{
    [JsonPropertyName("type")]
    public ToolType Type { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    public const int MaxLevel = 5;

    [JsonIgnore] public bool IsUnlocked => Level > 0;
    [JsonIgnore] public bool CanUpgrade => Level < MaxLevel;

    /// <summary>
    /// Kosten in Goldschrauben fuer das naechste Upgrade.
    /// </summary>
    [JsonIgnore]
    public int UpgradeCostScrews => Level switch
    {
        0 => 5,
        1 => 15,
        2 => 35,
        3 => 70,
        4 => 120,
        _ => 0
    };

    /// <summary>
    /// Alte Euro-Kosten (Legacy, nicht mehr verwendet).
    /// </summary>
    [JsonIgnore]
    public decimal UpgradeCost => Level switch
    {
        0 => 50m,
        1 => 150m,
        2 => 400m,
        3 => 1000m,
        4 => 2500m,
        _ => 0m
    };

    /// <summary>Saege: Zone-Bonus als Faktor (0.05 = +5%)</summary>
    [JsonIgnore]
    public double ZoneBonus => Level switch
    {
        1 => 0.05,
        2 => 0.10,
        3 => 0.15,
        4 => 0.20,
        5 => 0.25,
        _ => 0.0
    };

    /// <summary>Rohrzange/Schraubendreher/Pinsel: Extra-Sekunden</summary>
    [JsonIgnore]
    public int TimeBonus => Level switch
    {
        1 => 5,
        2 => 8,
        3 => 10,
        4 => 12,
        5 => 15,
        _ => 0
    };

    [JsonIgnore]
    public string NameKey => Type switch
    {
        ToolType.Saw => "ToolSaw",
        ToolType.PipeWrench => "ToolPipeWrench",
        ToolType.Screwdriver => "ToolScrewdriver",
        ToolType.Paintbrush => "ToolPaintbrush",
        _ => "Unknown"
    };

    [JsonIgnore]
    public MiniGameType RelatedMiniGame => Type switch
    {
        ToolType.Saw => MiniGameType.Sawing,
        ToolType.PipeWrench => MiniGameType.PipePuzzle,
        ToolType.Screwdriver => MiniGameType.WiringGame,
        ToolType.Paintbrush => MiniGameType.PaintingGame,
        _ => MiniGameType.Sawing
    };

    public static List<Tool> CreateDefaults() =>
    [
        new() { Type = ToolType.Saw },
        new() { Type = ToolType.PipeWrench },
        new() { Type = ToolType.Screwdriver },
        new() { Type = ToolType.Paintbrush }
    ];
}
