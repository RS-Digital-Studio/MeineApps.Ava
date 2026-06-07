using System.Collections.Generic;
using Newtonsoft.Json;
using HandwerkerImperium.Domain.Orders;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Ein Werkzeug, das im Shop aufgewertet werden kann und Minigame-Boni gibt.
    /// 1:1-Port aus dem Avalonia-Original (Models/Tool.cs). ToolType-Enum ist in ToolType.cs (Schicht 10).
    /// NameKey (Lokalisierung) wandert in die Präsentationsschicht. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class Tool
    {
        [JsonProperty("type")]
        public ToolType Type { get; set; }

        [JsonProperty("level")]
        public int Level { get; set; }

        public const int MaxLevel = 5;

        [JsonIgnore] public bool IsUnlocked => Level > 0;
        [JsonIgnore] public bool CanUpgrade => Level < MaxLevel;

        /// <summary>Kosten in Goldschrauben für das nächste Upgrade.</summary>
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

        /// <summary>Alte Euro-Kosten (Legacy, nicht mehr verwendet).</summary>
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

        /// <summary>Säge: Zone-Bonus als Faktor (0.05 = +5%).</summary>
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

        /// <summary>Rohrzange/Schraubendreher/Pinsel: Extra-Sekunden.</summary>
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
        public MiniGameType RelatedMiniGame => Type switch
        {
            ToolType.Saw => MiniGameType.Sawing,
            ToolType.PipeWrench => MiniGameType.PipePuzzle,
            ToolType.Screwdriver => MiniGameType.WiringGame,
            ToolType.Paintbrush => MiniGameType.PaintingGame,
            ToolType.Hammer => MiniGameType.RoofTiling,
            ToolType.SpiritLevel => MiniGameType.Blueprint,
            ToolType.Magnifier => MiniGameType.Inspection,
            ToolType.Compass => MiniGameType.DesignPuzzle,
            _ => MiniGameType.Sawing
        };

        public static List<Tool> CreateDefaults() => new List<Tool>
        {
            new Tool { Type = ToolType.Saw },
            new Tool { Type = ToolType.PipeWrench },
            new Tool { Type = ToolType.Screwdriver },
            new Tool { Type = ToolType.Paintbrush },
            new Tool { Type = ToolType.Hammer },
            new Tool { Type = ToolType.SpiritLevel },
            new Tool { Type = ToolType.Magnifier },
            new Tool { Type = ToolType.Compass }
        };
    }
}
