namespace HandwerkerImperium.Domain.Economy;

/// <summary>
/// Werkstatt-/Gewerk-Typen im Spiel. Jeder Typ hat eigene Mini-Games und Progression.
///
/// 1:1-Port aus dem Avalonia-Original (Models/Enums/WorkshopType.cs). Reine Spiellogik —
/// Präsentations-Methoden (Icon, Farbe, Lokalisierungs-Key) leben in der Unity-UI-Schicht,
/// nicht in der Domain. Numerische Enum-Werte sind save-relevant und unveränderlich.
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
    GeneralContractor = 7,

    /// <summary>Meisterschmiede - Schmiedekunst, Metallbearbeitung (Prestige 4 exclusive)</summary>
    MasterSmith = 8,

    /// <summary>Innovationslabor - Erfindungen, Prototypen (Prestige 5 exclusive)</summary>
    InnovationLab = 9
}

/// <summary>
/// Extension-Methoden für <see cref="WorkshopType"/> (reine Spiellogik-Werte).
/// </summary>
public static class WorkshopTypeExtensions
{
    /// <summary>Spielerlevel, das zum Freischalten dieser Werkstatt nötig ist.</summary>
    public static int GetUnlockLevel(this WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => 1,
        WorkshopType.Plumber => 5,
        WorkshopType.Electrician => 15,
        WorkshopType.Painter => 22,
        WorkshopType.Roofer => 40,
        WorkshopType.Contractor => 80,
        WorkshopType.Architect => 1,            // Ab Level 1 verfügbar, braucht aber Prestige 1
        WorkshopType.GeneralContractor => 1,    // Ab Level 1 verfügbar, braucht aber Prestige 3
        WorkshopType.MasterSmith => 500,        // Endgame: Level 500 + Platin-Prestige
        WorkshopType.InnovationLab => 750,      // Endgame: Level 750 + Diamant-Prestige
        _ => 1
    };

    /// <summary>Prestige-Stufe, die zum Freischalten dieser Werkstatt nötig ist (0 = keine).</summary>
    public static int GetRequiredPrestige(this WorkshopType type) => type switch
    {
        WorkshopType.Architect => 1,
        WorkshopType.GeneralContractor => 3,
        WorkshopType.MasterSmith => 4,          // Platin-Prestige
        WorkshopType.InnovationLab => 5,        // Diamant-Prestige
        _ => 0
    };

    /// <summary>Kosten zum Kaufen/Freischalten dieser Werkstatt (zusätzlich zur Level-Anforderung).</summary>
    public static decimal GetUnlockCost(this WorkshopType type) => type switch
    {
        WorkshopType.Carpenter => 0m,
        WorkshopType.Plumber => 5_000m,
        WorkshopType.Electrician => 250_000m,
        WorkshopType.Painter => 2_500_000m,
        WorkshopType.Roofer => 10_000_000m,
        WorkshopType.Contractor => 100_000_000m,
        WorkshopType.Architect => 2_500_000_000m,
        WorkshopType.GeneralContractor => 25_000_000_000m,
        WorkshopType.MasterSmith => 30_000_000_000m,
        WorkshopType.InnovationLab => 50_000_000_000m,
        _ => 0m
    };

    /// <summary>Basis-Einkommens-Multiplikator dieses Werkstatt-Typs (höhere Tiers verdienen mehr pro Worker).</summary>
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
        WorkshopType.MasterSmith => 3.0m,          // Endgame: moderater Multiplikator, Spezial: Crafting-Materialien
        WorkshopType.InnovationLab => 5.0m,        // Endgame: hoher Multiplikator, Spezial: Research-Speed
        _ => 1.0m
    };

    /// <summary>Ob diese Werkstatt Prestige zum Freischalten benötigt.</summary>
    public static bool IsPrestigeExclusive(this WorkshopType type) =>
        type.GetRequiredPrestige() > 0;
}
