namespace HandwerkerImperium.Domain.Reputation
{
    /// <summary>
    /// Effekt-Typen für den Reputation-Shop.
    /// Reputation ist die 3. Währung neben Geld + Goldschrauben — sie regeneriert nicht
    /// automatisch, sondern muss durch Aufträge verdient werden.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/ReputationShopItem.cs).
    /// </summary>
    public enum ReputationShopEffect
    {
        /// <summary>Nächste 5 Aufträge werden Stammkunden-Aufträge (1.1-1.5x Reward).</summary>
        RegularCustomerGuarantee,

        /// <summary>Nächster Lieferant +50% Speed für 1h.</summary>
        FasterDelivery,

        /// <summary>Alle Worker +30 Mood (sofort).</summary>
        WorkerMoodBoost,

        /// <summary>Workshop-Skin „Holz-Premium" (kosmetisch, permanent).</summary>
        WorkshopSkinWoodPremium,

        /// <summary>Nächster Risk-Strategy-Miss kostet keine Reputation.</summary>
        ReputationInsurance
    }

    /// <summary>
    /// Ein Item im Reputation-Shop. Original nutzt init-only Properties; in Unity/netstandard2.1
    /// fehlt <c>IsExternalInit</c> (.NET 5+) → set-Accessoren (verhaltensgleich bei Object-Initializern).
    /// </summary>
    public sealed class ReputationShopItem
    {
        public string Id { get; set; } = "";
        public ReputationShopEffect Effect { get; set; }

        /// <summary>Reputation-Kosten (Score-Punkte, werden vom aktuellen ReputationScore abgezogen).</summary>
        public int ReputationCost { get; set; }

        /// <summary>Localization-Key für den Item-Namen.</summary>
        public string NameKey { get; set; } = "";

        /// <summary>Localization-Key für die Beschreibung.</summary>
        public string DescriptionKey { get; set; } = "";

        /// <summary>Fallback-Texte (Deutsch).</summary>
        public string NameFallback { get; set; } = "";
        public string DescriptionFallback { get; set; } = "";

        /// <summary>Optionales GameIcon für die UI-Darstellung.</summary>
        public string IconKind { get; set; } = "Star";
    }
}
