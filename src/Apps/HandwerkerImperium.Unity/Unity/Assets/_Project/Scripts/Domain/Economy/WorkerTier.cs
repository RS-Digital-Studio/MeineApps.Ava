namespace HandwerkerImperium.Domain.Economy;

/// <summary>
/// Worker-Qualitätsstufen von F (niedrigste) bis Legendary (höchste).
/// Höhere Tiers haben bessere Effizienz-Spannen und kosten mehr Lohn.
///
/// 1:1-Port aus dem Avalonia-Original (Models/Enums/WorkerTier.cs). Reine Spiellogik —
/// Farb-/Lokalisierungs-Methoden leben in der Unity-UI-Schicht. Numerische Werte save-relevant.
/// </summary>
public enum WorkerTier
{
    F = 0,
    E = 1,
    D = 2,
    C = 3,
    B = 4,
    A = 5,
    S = 6,
    SS = 7,
    SSS = 8,
    Legendary = 9
}

/// <summary>
/// Extension-Methoden für <see cref="WorkerTier"/> (reine Spiellogik-Werte).
/// </summary>
public static class WorkerTierExtensions
{
    /// <summary>Minimale Basis-Effizienz dieses Tiers (vor Stimmung/Müdigkeit).</summary>
    public static decimal GetMinEfficiency(this WorkerTier tier) => tier switch
    {
        WorkerTier.F => 0.30m,
        WorkerTier.E => 0.50m,
        WorkerTier.D => 0.75m,
        WorkerTier.C => 1.10m,
        WorkerTier.B => 1.70m,
        WorkerTier.A => 2.50m,
        WorkerTier.S => 3.80m,
        WorkerTier.SS => 5.50m,
        WorkerTier.SSS => 8.50m,
        WorkerTier.Legendary => 13.00m,
        _ => 0.50m
    };

    /// <summary>Maximale Basis-Effizienz dieses Tiers (vor Stimmung/Müdigkeit).</summary>
    public static decimal GetMaxEfficiency(this WorkerTier tier) => tier switch
    {
        WorkerTier.F => 0.50m,
        WorkerTier.E => 0.80m,
        WorkerTier.D => 1.25m,
        WorkerTier.C => 1.90m,
        WorkerTier.B => 2.80m,
        WorkerTier.A => 4.20m,
        WorkerTier.S => 6.00m,
        WorkerTier.SS => 9.00m,
        WorkerTier.SSS => 14.00m,
        WorkerTier.Legendary => 22.00m,
        _ => 0.80m
    };

    /// <summary>Stundenlohn für Worker dieses Tiers.</summary>
    public static decimal GetWagePerHour(this WorkerTier tier) => tier switch
    {
        WorkerTier.F => 5m,
        WorkerTier.E => 9m,
        WorkerTier.D => 16m,
        WorkerTier.C => 28m,
        WorkerTier.B => 50m,
        WorkerTier.A => 90m,
        WorkerTier.S => 160m,
        WorkerTier.SS => 280m,
        WorkerTier.SSS => 500m,
        WorkerTier.Legendary => 900m,
        _ => 9m
    };

    /// <summary>Basis-Anstellungskosten pro Tier (ohne Level-Skalierung).</summary>
    public static decimal GetBaseHiringCost(this WorkerTier tier) => tier switch
    {
        WorkerTier.F => 50m,
        WorkerTier.E => 200m,
        WorkerTier.D => 1_000m,
        WorkerTier.C => 5_000m,
        WorkerTier.B => 25_000m,
        WorkerTier.A => 100_000m,
        WorkerTier.S => 500_000m,
        WorkerTier.SS => 2_000_000m,
        WorkerTier.SSS => 10_000_000m,
        WorkerTier.Legendary => 50_000_000m,
        _ => 200m
    };

    /// <summary>
    /// Anstellungskosten mit Level-Skalierung.
    /// Pro 10 Level +20% (Level 10 = 1.2x, Level 50 = 2.0x, Level 100 = 3.0x).
    /// </summary>
    public static decimal GetHiringCost(this WorkerTier tier, int playerLevel = 1)
    {
        var baseCost = tier.GetBaseHiringCost();
        decimal levelMultiplier = 1.0m + Math.Max(0, playerLevel - 1) * 0.02m;
        return Math.Round(baseCost * levelMultiplier);
    }

    /// <summary>Spielerlevel, das zum Freischalten dieses Tiers im Worker-Markt nötig ist.</summary>
    public static int GetUnlockLevel(this WorkerTier tier) => tier switch
    {
        WorkerTier.F => 1,
        WorkerTier.E => 1,
        WorkerTier.D => 8,
        WorkerTier.C => 15,
        WorkerTier.B => 25,
        WorkerTier.A => 35,
        WorkerTier.S => 45,       // Braucht zusätzlich Research-Unlock
        WorkerTier.SS => 100,     // Braucht zusätzlich Research-Unlock
        WorkerTier.SSS => 250,    // Braucht zusätzlich Research-Unlock
        WorkerTier.Legendary => 500, // Braucht zusätzlich Research-Unlock
        _ => 1
    };

    /// <summary>
    /// Resistenz gegen den Workshop-Level-Anforderungsmalus (0.0 = voller Malus, 1.0 = immun).
    /// Höhere Tiers können besser mit anspruchsvolleren Workshops umgehen.
    /// </summary>
    public static decimal GetLevelResistance(this WorkerTier tier) => tier switch
    {
        WorkerTier.F => 0.00m,
        WorkerTier.E => 0.10m,
        WorkerTier.D => 0.20m,
        WorkerTier.C => 0.30m,
        WorkerTier.B => 0.40m,
        WorkerTier.A => 0.55m,
        WorkerTier.S => 0.70m,
        WorkerTier.SS => 0.80m,
        WorkerTier.SSS => 0.90m,
        WorkerTier.Legendary => 1.00m,
        _ => 0.00m
    };

    /// <summary>
    /// Workshop-Einkommens-Aura-Bonus für hochrangige Worker.
    /// S-Tier und höher geben einen passiven Bonus auf das gesamte Workshop-Einkommen.
    /// </summary>
    public static decimal GetAuraBonus(this WorkerTier tier) => tier switch
    {
        WorkerTier.S => 0.05m,         // +5%
        WorkerTier.SS => 0.08m,        // +8%
        WorkerTier.SSS => 0.12m,       // +12%
        WorkerTier.Legendary => 0.20m, // +20%
        _ => 0m
    };

    /// <summary>
    /// Zusätzliche Goldschrauben-Kosten beim Einstellen (nur für hohe Tiers).
    /// </summary>
    public static int GetHiringScrewCost(this WorkerTier tier) => tier switch
    {
        WorkerTier.A => 20,
        WorkerTier.S => 60,
        WorkerTier.SS => 120,
        WorkerTier.SSS => 300,
        WorkerTier.Legendary => 750,
        _ => 0
    };
}
