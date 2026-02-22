namespace BomberBlast.Models.League;

/// <summary>
/// Liga-Ränge: Bronze → Silber → Gold → Platin → Diamant
/// </summary>
public enum LeagueTier
{
    Bronze,
    Silver,
    Gold,
    Platinum,
    Diamond
}

public static class LeagueTierExtensions
{
    /// <summary>Punkteschwelle für Aufstieg in diese Liga</summary>
    public static int GetPromotionThreshold(this LeagueTier tier) => tier switch
    {
        LeagueTier.Bronze => 0,
        LeagueTier.Silver => 400,
        LeagueTier.Gold => 900,
        LeagueTier.Platinum => 1600,
        LeagueTier.Diamond => 2500,
        _ => 0
    };

    /// <summary>Haupt-Farbe der Liga</summary>
    public static string GetColor(this LeagueTier tier) => tier switch
    {
        LeagueTier.Bronze => "#CD7F32",
        LeagueTier.Silver => "#C0C0C0",
        LeagueTier.Gold => "#FFD700",
        LeagueTier.Platinum => "#00CED1",
        LeagueTier.Diamond => "#B9F2FF",
        _ => "#FFFFFF"
    };

    /// <summary>Glow-Farbe der Liga</summary>
    public static string GetGlowColor(this LeagueTier tier) => tier switch
    {
        LeagueTier.Bronze => "#8B4513",
        LeagueTier.Silver => "#808080",
        LeagueTier.Gold => "#DAA520",
        LeagueTier.Platinum => "#008B8B",
        LeagueTier.Diamond => "#87CEEB",
        _ => "#808080"
    };

    /// <summary>Material-Icon-Name für Liga-Emblem</summary>
    public static string GetIconName(this LeagueTier tier) => tier switch
    {
        LeagueTier.Bronze => "ShieldOutline",
        LeagueTier.Silver => "Shield",
        LeagueTier.Gold => "ShieldStar",
        LeagueTier.Platinum => "ShieldCrown",
        LeagueTier.Diamond => "Diamond",
        _ => "ShieldOutline"
    };

    /// <summary>RESX-Key für Liga-Namen</summary>
    public static string GetNameKey(this LeagueTier tier) => $"League{tier}";

    /// <summary>Saison-Belohnungen (Coins, Gems)</summary>
    public static (int Coins, int Gems) GetSeasonReward(this LeagueTier tier) => tier switch
    {
        LeagueTier.Bronze => (2_000, 10),
        LeagueTier.Silver => (5_000, 20),
        LeagueTier.Gold => (10_000, 35),
        LeagueTier.Platinum => (18_000, 50),
        LeagueTier.Diamond => (30_000, 75),
        _ => (0, 0)
    };

    /// <summary>Aufstieg: Top 30% der Liga steigen auf</summary>
    public static float GetPromotionPercent(this LeagueTier tier) => tier switch
    {
        LeagueTier.Diamond => 0f, // Diamant: kein Aufstieg möglich
        _ => 0.30f
    };

    /// <summary>Abstieg: Bottom 20% steigen ab (Bronze: kein Abstieg)</summary>
    public static float GetRelegationPercent(this LeagueTier tier) => tier switch
    {
        LeagueTier.Bronze => 0f,
        _ => 0.20f
    };
}
