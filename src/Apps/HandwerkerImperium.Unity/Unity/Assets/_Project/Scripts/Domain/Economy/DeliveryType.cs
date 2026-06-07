namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Art der Lieferanten-Lieferung.
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/DeliveryType.cs). Enum-Reihenfolge = Persistenz-Integer.
    /// </summary>
    public enum DeliveryType
    {
        /// <summary>Geld-Bonus (basierend auf aktuellem Einkommen).</summary>
        Money,
        /// <summary>Goldschrauben (1-3 Stück).</summary>
        GoldenScrews,
        /// <summary>Erfahrungspunkte (20-100 XP).</summary>
        Experience,
        /// <summary>Stimmungs-Boost (+10 Mood für alle Worker).</summary>
        MoodBoost,
        /// <summary>Kurzer Geschwindigkeits-Boost (30min 2x Einkommen).</summary>
        SpeedBoost,
        /// <summary>Tier-1-Material-Lieferung (1-10 Stück; verdrängt Geld-Lieferung mit 25% Wahrscheinlichkeit).</summary>
        Material
    }
}
