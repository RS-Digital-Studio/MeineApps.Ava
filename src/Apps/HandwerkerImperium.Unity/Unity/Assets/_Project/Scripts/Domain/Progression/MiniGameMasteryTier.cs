namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Mastery-Tier für ein einzelnes Mini-Game. Permanenter Skill-Status, der NICHT bei
    /// Ascension/Prestige resettet wird (basiert auf Lifetime-Perfect-Ratings).
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/MiniGameMasteryTier.cs). Enum-Reihenfolge = Persistenz-Integer.
    /// </summary>
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

    /// <summary>Statische Schwellen + Belohnungen für Mini-Game-Mastery.</summary>
    public static class MiniGameMasteryThresholds
    {
        public const int BronzeThreshold = 50;
        public const int SilverThreshold = 200;
        public const int GoldThreshold = 1000;

        /// <summary>Goldschrauben-Belohnung pro Tier (Index 1-3, Index 0 = None = keine Belohnung).</summary>
        public static readonly int[] GoldenScrewRewards = new int[] { 0, 5, 15, 50 };

        /// <summary>Berechnet das aktuelle Mastery-Tier basierend auf der Lifetime-Perfect-Zahl.</summary>
        public static MiniGameMasteryTier GetTierForCount(int lifetimePerfects)
        {
            if (lifetimePerfects >= GoldThreshold) return MiniGameMasteryTier.Gold;
            if (lifetimePerfects >= SilverThreshold) return MiniGameMasteryTier.Silver;
            if (lifetimePerfects >= BronzeThreshold) return MiniGameMasteryTier.Bronze;
            return MiniGameMasteryTier.None;
        }

        /// <summary>Liefert die Lifetime-Schwelle für ein Tier (None = 0).</summary>
        public static int GetThresholdForTier(MiniGameMasteryTier tier) => tier switch
        {
            MiniGameMasteryTier.Bronze => BronzeThreshold,
            MiniGameMasteryTier.Silver => SilverThreshold,
            MiniGameMasteryTier.Gold => GoldThreshold,
            _ => 0
        };
    }
}
