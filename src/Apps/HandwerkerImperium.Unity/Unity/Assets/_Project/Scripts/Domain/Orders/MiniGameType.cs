using HandwerkerImperium.Domain.Economy;

namespace HandwerkerImperium.Domain.Orders
{
    /// <summary>
    /// Verfügbare Mini-Game-Typen. Jedes testet eine andere Fertigkeit.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/MiniGameType.cs). Reine Spiellogik —
    /// Routing/Lokalisierung leben in der Unity-UI-Schicht. Numerische Werte save-relevant.
    /// </summary>
    public enum MiniGameType
    {
        Sawing = 0,
        Planing = 1,
        PipePuzzle = 2,
        WiringGame = 3,
        PaintingGame = 4,
        TileLaying = 5,
        Measuring = 6,
        RoofTiling = 7,
        Blueprint = 8,
        DesignPuzzle = 9,
        Inspection = 10,
        ForgeGame = 11,
        InventGame = 12
    }

    /// <summary>
    /// Extension-Methoden für <see cref="MiniGameType"/> (reine Spiellogik-Zuordnung).
    /// </summary>
    public static class MiniGameTypeExtensions
    {
        /// <summary>Workshop-Typen, die dieses Mini-Game verwenden.</summary>
        public static WorkshopType[] GetWorkshopTypes(this MiniGameType type) => type switch
        {
            MiniGameType.Sawing => new[] { WorkshopType.Carpenter },
            MiniGameType.Planing => new[] { WorkshopType.Carpenter },
            MiniGameType.PipePuzzle => new[] { WorkshopType.Plumber },
            MiniGameType.WiringGame => new[] { WorkshopType.Electrician },
            MiniGameType.PaintingGame => new[] { WorkshopType.Painter },
            MiniGameType.TileLaying => new[] { WorkshopType.Roofer },
            MiniGameType.Measuring => new[] { WorkshopType.Contractor, WorkshopType.Carpenter },
            MiniGameType.RoofTiling => new[] { WorkshopType.Roofer },
            MiniGameType.Blueprint => new[] { WorkshopType.Contractor },
            MiniGameType.DesignPuzzle => new[] { WorkshopType.Architect },
            MiniGameType.Inspection => new[] { WorkshopType.GeneralContractor },
            MiniGameType.ForgeGame => new[] { WorkshopType.MasterSmith },
            MiniGameType.InventGame => new[] { WorkshopType.InnovationLab },
            _ => new[] { WorkshopType.Carpenter }
        };
    }
}
