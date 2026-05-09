namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Mastery-Tier fuer ein einzelnes Mini-Game (v2.0.36).
/// Wird basierend auf <see cref="GameState.LifetimePerfectRatingCounts"/> berechnet —
/// permanenter Skill-Status, der NICHT bei Ascension/Prestige resettet wird.
/// </summary>
/// <remarks>
/// Belohnt manuelles Mini-Game-Spielen auch nach Auto-Complete-Threshold (30 Perfects).
/// Game-Audit-Empfehlung [DESIGN-1]: Mini-Game-Loop-Lifetime auf AAA-Niveau halten.
/// </remarks>
public enum MiniGameMasteryTier
{
    /// <summary>Noch keine Mastery erreicht (unter 50 Lifetime-Perfects).</summary>
    None = 0,

    /// <summary>Bronze-Mastery: 50 Lifetime-Perfects. Belohnung: 5 Goldschrauben.</summary>
    Bronze = 1,

    /// <summary>Silber-Mastery: 200 Lifetime-Perfects. Belohnung: 15 Goldschrauben.</summary>
    Silver = 2,

    /// <summary>Gold-Mastery: 1000 Lifetime-Perfects. Belohnung: 50 Goldschrauben.</summary>
    Gold = 3
}

/// <summary>Statische Schwellen + Belohnungen fuer Mini-Game-Mastery.</summary>
public static class MiniGameMasteryThresholds
{
    /// <summary>Lifetime-Perfect-Schwelle fuer Bronze.</summary>
    public const int BronzeThreshold = 50;
    /// <summary>Lifetime-Perfect-Schwelle fuer Silver.</summary>
    public const int SilverThreshold = 200;
    /// <summary>Lifetime-Perfect-Schwelle fuer Gold.</summary>
    public const int GoldThreshold = 1000;

    /// <summary>Goldschrauben-Belohnung pro Tier (Index 1-3, Index 0 ist None=keine Belohnung).</summary>
    public static readonly int[] GoldenScrewRewards = [0, 5, 15, 50];

    /// <summary>
    /// Berechnet das aktuelle Mastery-Tier basierend auf der Lifetime-Perfect-Zahl.
    /// </summary>
    public static MiniGameMasteryTier GetTierForCount(int lifetimePerfects)
    {
        if (lifetimePerfects >= GoldThreshold) return MiniGameMasteryTier.Gold;
        if (lifetimePerfects >= SilverThreshold) return MiniGameMasteryTier.Silver;
        if (lifetimePerfects >= BronzeThreshold) return MiniGameMasteryTier.Bronze;
        return MiniGameMasteryTier.None;
    }

    /// <summary>
    /// Liefert die Lifetime-Schwelle fuer ein Tier (None=0).
    /// </summary>
    public static int GetThresholdForTier(MiniGameMasteryTier tier) => tier switch
    {
        MiniGameMasteryTier.Bronze => BronzeThreshold,
        MiniGameMasteryTier.Silver => SilverThreshold,
        MiniGameMasteryTier.Gold => GoldThreshold,
        _ => 0
    };
}
