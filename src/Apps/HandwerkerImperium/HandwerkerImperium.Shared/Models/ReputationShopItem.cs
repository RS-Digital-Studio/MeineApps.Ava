namespace HandwerkerImperium.Models;

/// <summary>
/// Effekt-Typen fuer den Reputation-Shop (v2.1.0).
/// Reputation ist die 3. Waehrung neben Geld + Goldschrauben — sie regeneriert nicht
/// automatisch, sondern muss durch Auftraege verdient werden.
/// </summary>
public enum ReputationShopEffect
{
    /// <summary>Naechste 5 Auftraege werden Stammkunden-Auftraege (1.1-1.5x Reward).</summary>
    RegularCustomerGuarantee,

    /// <summary>Naechster Lieferant +50% Speed fuer 1h.</summary>
    FasterDelivery,

    /// <summary>Alle Worker +30 Mood (sofort).</summary>
    WorkerMoodBoost,

    /// <summary>Workshop-Skin „Holz-Premium" (kosmetisch, permanent).</summary>
    WorkshopSkinWoodPremium,

    /// <summary>Naechster Risk-Strategy-Miss kostet keine Reputation.</summary>
    ReputationInsurance
}

/// <summary>
/// Ein Item im Reputation-Shop (v2.1.0).
/// </summary>
public sealed class ReputationShopItem
{
    public string Id { get; init; } = "";
    public ReputationShopEffect Effect { get; init; }

    /// <summary>Reputation-Kosten (Score-Punkte, werden vom aktuellen ReputationScore abgezogen).</summary>
    public int ReputationCost { get; init; }

    /// <summary>Localization-Key fuer den Item-Namen.</summary>
    public string NameKey { get; init; } = "";

    /// <summary>Localization-Key fuer die Beschreibung.</summary>
    public string DescriptionKey { get; init; } = "";

    /// <summary>Fallback-Texte (Deutsch).</summary>
    public string NameFallback { get; init; } = "";
    public string DescriptionFallback { get; init; } = "";

    /// <summary>Optionales GameIcon fuer die UI-Darstellung.</summary>
    public string IconKind { get; init; } = "Star";
}
