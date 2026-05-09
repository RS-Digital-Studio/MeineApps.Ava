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

/// <summary>
/// Sub-Tiers innerhalb eines LeagueTiers (Phase 19 — AAA-Audit L1).
/// Bronze/Silver/Gold/Platinum sind in I/II/III unterteilt → 12 Sub-Stufen total + Diamond.
/// Diamond bleibt single-tier (Endgame-Slot, kein Sub-Reset). Spieler steigen innerhalb
/// eines Tiers von I → II → III auf, bevor sie in den nächsten Tier promoviert werden.
/// </summary>
public enum LeagueSubTier
{
    /// <summary>Untere Sub-Stufe (Einstieg in den Tier).</summary>
    I = 0,
    /// <summary>Mittlere Sub-Stufe.</summary>
    II = 1,
    /// <summary>Obere Sub-Stufe (Promotion-Zone zum nächsten Tier).</summary>
    III = 2,
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

    // === Phase 19 — Sub-Tier-Logik (AAA-Audit L1) ============================

    /// <summary>
    /// Berechnet den Sub-Tier (I/II/III) innerhalb eines Tiers basierend auf den Punkten.
    /// Diamond hat keine Sub-Tiers (returnt immer III als Endgame-Slot).
    /// Punkte-Range pro Tier wird in 3 gleiche Drittel aufgeteilt.
    /// </summary>
    public static LeagueSubTier GetSubTier(this LeagueTier tier, int points)
    {
        if (tier == LeagueTier.Diamond) return LeagueSubTier.III;

        var promotion = tier.GetPromotionThreshold();
        var nextThreshold = (tier + 1).GetPromotionThreshold();
        var range = nextThreshold - promotion;
        if (range <= 0) return LeagueSubTier.I;

        // points relativ zum Tier-Bereich
        var relative = Math.Clamp(points - promotion, 0, range - 1);
        var third = range / 3;
        if (third <= 0) return LeagueSubTier.I;

        if (relative < third) return LeagueSubTier.I;
        if (relative < 2 * third) return LeagueSubTier.II;
        return LeagueSubTier.III;
    }

    /// <summary>
    /// Display-Name kombiniert Tier + Sub-Tier (z.B. "Gold II"). Diamond bleibt "Diamond".
    /// </summary>
    public static string GetDisplayName(this LeagueTier tier, LeagueSubTier subTier)
        => tier == LeagueTier.Diamond
            ? "Diamond"
            : $"{tier} {subTier}";

    /// <summary>
    /// Punkte-Untergrenze für einen spezifischen Sub-Tier. Hilft UI um Aufstiegs-Progress
    /// als Fortschrittsbalken zu zeigen ("noch X Punkte bis Gold III").
    /// </summary>
    public static int GetSubTierThreshold(this LeagueTier tier, LeagueSubTier subTier)
    {
        if (tier == LeagueTier.Diamond) return tier.GetPromotionThreshold();

        var basePoints = tier.GetPromotionThreshold();
        var nextThreshold = (tier + 1).GetPromotionThreshold();
        var third = (nextThreshold - basePoints) / 3;
        return subTier switch
        {
            LeagueSubTier.I => basePoints,
            LeagueSubTier.II => basePoints + third,
            LeagueSubTier.III => basePoints + 2 * third,
            _ => basePoints,
        };
    }

    /// <summary>
    /// Punkte-Obergrenze für einen Sub-Tier (= Untergrenze des nächsten Sub-Tiers oder Promotion).
    /// </summary>
    public static int GetSubTierCeiling(this LeagueTier tier, LeagueSubTier subTier)
    {
        if (tier == LeagueTier.Diamond) return int.MaxValue;

        if (subTier == LeagueSubTier.III)
        {
            // III endet bei der Promotion-Schwelle des nächsten Tiers
            return (tier + 1).GetPromotionThreshold();
        }
        return tier.GetSubTierThreshold(subTier + 1);
    }
}
