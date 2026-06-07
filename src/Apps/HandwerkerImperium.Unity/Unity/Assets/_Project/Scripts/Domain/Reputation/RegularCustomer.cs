using System;
using Newtonsoft.Json;
using HandwerkerImperium.Domain.Economy;

namespace HandwerkerImperium.Domain.Reputation
{
    /// <summary>
    /// Ein treuer Kunde, der Bonus-Belohnungen auf Aufträge gibt.
    /// Wird nach 5 perfekten Aufträgen zum Stammkunden.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/RegularCustomer.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class RegularCustomer
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("preferredWorkshop")]
        public WorkshopType PreferredWorkshop { get; set; }

        /// <summary>Anzahl perfekter Aufträge. 5 = wird Stammkunde.</summary>
        [JsonProperty("perfectOrderCount")]
        public int PerfectOrderCount { get; set; }

        /// <summary>Bonus-Multiplikator für Aufträge dieses Kunden (1.1 = +10%).</summary>
        [JsonProperty("bonusMultiplier")]
        public decimal BonusMultiplier { get; set; } = 1.1m;

        [JsonProperty("lastOrder")]
        public DateTime LastOrder { get; set; }

        /// <summary>Seed für deterministische Avatar-Generierung.</summary>
        [JsonProperty("avatarSeed")]
        public string AvatarSeed { get; set; } = Guid.NewGuid().ToString().Substring(0, 8);

        [JsonIgnore]
        public bool IsRegular => PerfectOrderCount >= 5;
    }
}
