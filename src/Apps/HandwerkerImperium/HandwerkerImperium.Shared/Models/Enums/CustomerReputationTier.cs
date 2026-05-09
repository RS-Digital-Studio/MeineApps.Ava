namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Reputations-Tier (v2.0.37). 4 Stufen mit progressiven Spawn-Effekten:
/// CityKnown bringt +10% Stammkunden-Spawn, RegionStar +20% + 5% Live-Order,
/// IndustryLegend +35% + 10% Live-Order. Beginner ist die Default-Stufe ohne Bonus.
/// </summary>
public enum CustomerReputationTier
{
    /// <summary>0-30 Reputation. Kein Bonus, kein Badge im Header.</summary>
    Beginner = 0,

    /// <summary>31-60 Reputation. Bronze-Badge, +10% Stammkunden-Spawn.</summary>
    CityKnown = 1,

    /// <summary>61-80 Reputation. Silber-Badge, +20% Stammkunden, +5% Live-Order-Spawn.</summary>
    RegionStar = 2,

    /// <summary>81-100 Reputation. Gold-Badge mit Aura, +35% Stammkunden, 10% Live-Order-Spawn.</summary>
    IndustryLegend = 3
}

/// <summary>
/// Extension-Helfer fuer <see cref="CustomerReputationTier"/>.
/// </summary>
public static class CustomerReputationTierExtensions
{
    /// <summary>Localization-Key fuer den Tier-Namen.</summary>
    public static string GetLocalizationKey(this CustomerReputationTier tier) => tier switch
    {
        CustomerReputationTier.Beginner => "RepTierBeginner",
        CustomerReputationTier.CityKnown => "RepTierCityKnown",
        CustomerReputationTier.RegionStar => "RepTierRegionStar",
        CustomerReputationTier.IndustryLegend => "RepTierIndustryLegend",
        _ => "RepTierBeginner"
    };

    /// <summary>Hex-Farbe fuer das Tier-Badge (Anfaenger=keine, dann Bronze/Silber/Gold).</summary>
    public static string GetBadgeColor(this CustomerReputationTier tier) => tier switch
    {
        CustomerReputationTier.CityKnown => "#CD7F32",     // Bronze
        CustomerReputationTier.RegionStar => "#C0C0C0",    // Silber
        CustomerReputationTier.IndustryLegend => "#FFD700", // Gold
        _ => "#8B6F47"                                     // Holz (Anfaenger, faktisch unsichtbar)
    };

    /// <summary>Anteil zusaetzlicher Stammkunden-Spawn-Chance (additiv).</summary>
    public static decimal GetRegularCustomerBonus(this CustomerReputationTier tier) => tier switch
    {
        CustomerReputationTier.CityKnown => 0.10m,
        CustomerReputationTier.RegionStar => 0.20m,
        CustomerReputationTier.IndustryLegend => 0.35m,
        _ => 0m
    };

    /// <summary>
    /// Live-Order-Spawn-Chance fuer dieses Tier (absoluter Wert, nicht additiv).
    /// 0 = unveraenderter Default, sonst Hard-Override.
    /// </summary>
    public static decimal GetLiveOrderSpawnChance(this CustomerReputationTier tier) => tier switch
    {
        CustomerReputationTier.RegionStar => 0.05m,
        CustomerReputationTier.IndustryLegend => 0.10m,
        _ => 0m
    };

    /// <summary>
    /// Tier-Berechnung aus Reputation-Score (0-100). Stateless — fuer Initial-Berechnung
    /// oder wenn kein vorheriger Tier-Wert bekannt ist.
    /// </summary>
    public static CustomerReputationTier FromScore(int reputationScore) => reputationScore switch
    {
        >= 81 => CustomerReputationTier.IndustryLegend,
        >= 61 => CustomerReputationTier.RegionStar,
        >= 31 => CustomerReputationTier.CityKnown,
        _ => CustomerReputationTier.Beginner
    };

    /// <summary>
    /// v2.0.37 Audit-Fix L5: Tier-Berechnung mit Hysterese — der Tier-Down-Threshold liegt
    /// 3 Punkte unter dem Tier-Up-Threshold. Beispiel: bei 61 Punkten steigt man zu RegionStar
    /// auf, faellt aber erst bei 58 Punkten zurueck zu CityKnown. Verhindert UI-Flackern an
    /// der Boundary (z.B. Stammkunden-Reputation bei +1 / Reputation-Decay -1 abwechselnd).
    /// </summary>
    public static CustomerReputationTier FromScoreWithHysteresis(int reputationScore, CustomerReputationTier currentTier)
    {
        // Tier-Up: harter Threshold (gleich wie FromScore)
        if (reputationScore >= 81 && currentTier < CustomerReputationTier.IndustryLegend) return CustomerReputationTier.IndustryLegend;
        if (reputationScore >= 61 && currentTier < CustomerReputationTier.RegionStar) return CustomerReputationTier.RegionStar;
        if (reputationScore >= 31 && currentTier < CustomerReputationTier.CityKnown) return CustomerReputationTier.CityKnown;

        // Tier-Down: 3 Punkte unter Tier-Up-Schwelle
        if (reputationScore < 78 && currentTier == CustomerReputationTier.IndustryLegend) return CustomerReputationTier.RegionStar;
        if (reputationScore < 58 && currentTier == CustomerReputationTier.RegionStar) return CustomerReputationTier.CityKnown;
        if (reputationScore < 28 && currentTier == CustomerReputationTier.CityKnown) return CustomerReputationTier.Beginner;

        return currentTier;
    }
}
