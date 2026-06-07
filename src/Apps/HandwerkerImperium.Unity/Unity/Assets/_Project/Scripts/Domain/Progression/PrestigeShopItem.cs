#nullable enable
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Prestige-Shop Kategorien für die gruppierte Anzeige.
    /// </summary>
    public enum PrestigeShopCategory
    {
        /// <summary>Einkommen-Boosts und Kostenreduzierung</summary>
        IncomeAndCosts,

        /// <summary>Arbeiter-Upgrades und Stimmung</summary>
        WorkerAndMood,

        /// <summary>Rush, Lieferant, Crafting, Offline, QuickJobs</summary>
        SpeedAndAutomation,

        /// <summary>Startkapital, Goldschrauben, XP</summary>
        CurrencyAndStart
    }

    /// <summary>
    /// Ein mit Prestige-Punkten kaufbares Item.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/PrestigeShopItem.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class PrestigeShopItem
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("nameKey")]
        public string NameKey { get; set; } = string.Empty;

        [JsonProperty("descriptionKey")]
        public string DescriptionKey { get; set; } = string.Empty;

        [JsonProperty("icon")]
        public string Icon { get; set; } = string.Empty;

        /// <summary>Kosten in Prestige-Punkten.</summary>
        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("isPurchased")]
        public bool IsPurchased { get; set; }

        [JsonProperty("effect")]
        public PrestigeEffect Effect { get; set; } = new PrestigeEffect();

        /// <summary>Kategorie für die gruppierte Shop-Anzeige.</summary>
        [JsonIgnore]
        public PrestigeShopCategory Category { get; set; }

        /// <summary>Wiederholbar kaufbar (steigende Kosten: Basis * 2^(Kaufanzahl)).</summary>
        [JsonIgnore]
        public bool IsRepeatable { get; set; }

        /// <summary>Aktuelle Kaufanzahl (nur für wiederholbare Items, aus PrestigeData geladen).</summary>
        [JsonIgnore]
        public int PurchaseCount { get; set; }

        /// <summary>
        /// Mindest-Prestige-Tier um dieses Item im Shop zu sehen.
        /// None = immer sichtbar. Bereits gekaufte Items bleiben unabhängig vom Tier wirksam.
        /// </summary>
        [JsonIgnore]
        public PrestigeTier RequiredTier { get; set; } = PrestigeTier.None;
    }

    /// <summary>
    /// Permanente Boni aus Prestige-Shop-Items.
    /// </summary>
    public class PrestigeEffect
    {
        [JsonProperty("incomeMultiplier")]
        public decimal IncomeMultiplier { get; set; }

        [JsonProperty("xpMultiplier")]
        public decimal XpMultiplier { get; set; }

        [JsonProperty("moodDecayReduction")]
        public decimal MoodDecayReduction { get; set; }

        [JsonProperty("costReduction")]
        public decimal CostReduction { get; set; }

        [JsonProperty("extraStartMoney")]
        public decimal ExtraStartMoney { get; set; }

        [JsonProperty("startingWorkerTier")]
        public string? StartingWorkerTier { get; set; }

        /// <summary>Rush-Verstärker: Bonus-Multiplikator auf Rush-Boost (z.B. 0.5 = Rush gibt 3x statt 2x).</summary>
        [JsonProperty("rushMultiplierBonus")]
        public decimal RushMultiplierBonus { get; set; }

        /// <summary>Lieferant-Turbo: Reduzierung des Lieferintervalls (z.B. 0.3 = 30% schneller).</summary>
        [JsonProperty("deliverySpeedBonus")]
        public decimal DeliverySpeedBonus { get; set; }

        /// <summary>Goldschrauben-Boost: Bonus auf alle Goldschrauben-Quellen (z.B. 0.25 = +25%).</summary>
        [JsonProperty("goldenScrewBonus")]
        public decimal GoldenScrewBonus { get; set; }

        /// <summary>Extra Offline-Stunden (z.B. 4 = +4h max. Offline-Berechnung).</summary>
        [JsonProperty("offlineHoursBonus")]
        public int OfflineHoursBonus { get; set; }

        /// <summary>Extra QuickJob-Limit pro Tag (z.B. 10 = +10 QuickJobs/Tag).</summary>
        [JsonProperty("extraQuickJobLimit")]
        public int ExtraQuickJobLimit { get; set; }

        /// <summary>Crafting-Geschwindigkeitsbonus (z.B. 0.25 = 25% schneller).</summary>
        [JsonProperty("craftingSpeedBonus")]
        public decimal CraftingSpeedBonus { get; set; }

        /// <summary>Rabatt auf Workshop-Upgrade-Kosten (z.B. 0.15 = -15%).</summary>
        [JsonProperty("upgradeDiscount")]
        public decimal UpgradeDiscount { get; set; }

        /// <summary>Bonus auf Auftragsbelohnungen (z.B. 0.05 = +5%).</summary>
        [JsonProperty("orderRewardBonus")]
        public decimal OrderRewardBonus { get; set; }

        /// <summary>Forschungs-Geschwindigkeitsbonus (z.B. 0.25 = -25% Forschungszeit).</summary>
        [JsonProperty("researchSpeedBonus")]
        public decimal ResearchSpeedBonus { get; set; }
    }
}
