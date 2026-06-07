namespace HandwerkerImperium.Domain.Orders
{
    /// <summary>
    /// Schwierigkeitsgrade für Aufträge.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/OrderDifficulty.cs). Reine Spiellogik —
    /// Stern-/Farb-Anzeige lebt in der Unity-UI-Schicht. Numerische Werte save-relevant.
    /// </summary>
    public enum OrderDifficulty
    {
        /// <summary>Leicht - 1 Stern, großzügiges Timing</summary>
        Easy = 1,

        /// <summary>Mittel - 2 Sterne, normales Timing</summary>
        Medium = 2,

        /// <summary>Schwer - 3 Sterne, präzises Timing nötig</summary>
        Hard = 3,

        /// <summary>Experte - 4 Sterne, braucht Reputation 80+, sehr präzise</summary>
        Expert = 4
    }

    /// <summary>
    /// Extension-Methoden für <see cref="OrderDifficulty"/> (reine Spiellogik-Werte).
    /// </summary>
    public static class OrderDifficultyExtensions
    {
        /// <summary>Belohnungs-Multiplikator für diese Schwierigkeit.</summary>
        public static decimal GetRewardMultiplier(this OrderDifficulty difficulty) => difficulty switch
        {
            OrderDifficulty.Easy => 1.0m,
            OrderDifficulty.Medium => 1.5m,
            OrderDifficulty.Hard => 3.5m,
            OrderDifficulty.Expert => 5.0m,
            _ => 1.0m
        };

        /// <summary>XP-Multiplikator für diese Schwierigkeit.</summary>
        public static decimal GetXpMultiplier(this OrderDifficulty difficulty) => difficulty switch
        {
            OrderDifficulty.Easy => 1.0m,
            OrderDifficulty.Medium => 1.75m,
            OrderDifficulty.Hard => 3.0m,
            OrderDifficulty.Expert => 5.5m,
            _ => 1.0m
        };

        /// <summary>Größe der "Perfect"-Zone im Timing-Balken (0.0 - 1.0). Kleiner = schwerer.</summary>
        public static double GetPerfectZoneSize(this OrderDifficulty difficulty) => difficulty switch
        {
            OrderDifficulty.Easy => 0.20,
            OrderDifficulty.Medium => 0.12,
            OrderDifficulty.Hard => 0.09,
            OrderDifficulty.Expert => 0.06,
            _ => 0.12
        };

        /// <summary>Geschwindigkeits-Multiplikator des Timing-Balkens. Höher = schneller = schwerer.</summary>
        public static double GetSpeedMultiplier(this OrderDifficulty difficulty) => difficulty switch
        {
            OrderDifficulty.Easy => 0.9,
            OrderDifficulty.Medium => 1.2,
            OrderDifficulty.Hard => 1.6,
            OrderDifficulty.Expert => 2.2,
            _ => 1.2
        };

        /// <summary>Mindest-Reputation für diese Schwierigkeit.</summary>
        public static int GetRequiredReputation(this OrderDifficulty difficulty) => difficulty switch
        {
            OrderDifficulty.Expert => 80,
            _ => 0
        };
    }
}
