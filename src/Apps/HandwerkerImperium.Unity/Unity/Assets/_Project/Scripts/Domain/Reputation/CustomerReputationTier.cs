namespace HandwerkerImperium.Domain.Reputation
{
    /// <summary>
    /// Reputations-Tier. 4 Stufen mit progressiven Spawn-Effekten:
    /// CityKnown bringt +10% Stammkunden-Spawn, RegionStar +20% + 5% Live-Order,
    /// IndustryLegend +35% + 10% Live-Order. Beginner ist die Default-Stufe ohne Bonus.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/CustomerReputationTier.cs). Die
    /// UI-Extensions (Lokalisierungs-Key, Badge-Farbe) wandern in die Präsentationsschicht.
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

    /// <summary>Gameplay-Extensions für <see cref="CustomerReputationTier"/>.</summary>
    public static class CustomerReputationTierExtensions
    {
        /// <summary>Anteil zusätzlicher Stammkunden-Spawn-Chance (additiv).</summary>
        public static decimal GetRegularCustomerBonus(this CustomerReputationTier tier) => tier switch
        {
            CustomerReputationTier.CityKnown => 0.10m,
            CustomerReputationTier.RegionStar => 0.20m,
            CustomerReputationTier.IndustryLegend => 0.35m,
            _ => 0m
        };

        /// <summary>
        /// Live-Order-Spawn-Chance für dieses Tier (absoluter Wert, nicht additiv).
        /// 0 = unveränderter Default, sonst Hard-Override.
        /// </summary>
        public static decimal GetLiveOrderSpawnChance(this CustomerReputationTier tier) => tier switch
        {
            CustomerReputationTier.RegionStar => 0.05m,
            CustomerReputationTier.IndustryLegend => 0.10m,
            _ => 0m
        };

        /// <summary>
        /// Tier-Berechnung aus Reputation-Score (0-100). Stateless — für Initial-Berechnung
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
        /// Tier-Berechnung mit Hysterese — der Tier-Down-Threshold liegt 3 Punkte unter dem
        /// Tier-Up-Threshold. Bei 61 Punkten steigt man zu RegionStar auf, fällt aber erst bei
        /// 58 Punkten zurück zu CityKnown. Verhindert UI-Flackern an der Boundary.
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
}
