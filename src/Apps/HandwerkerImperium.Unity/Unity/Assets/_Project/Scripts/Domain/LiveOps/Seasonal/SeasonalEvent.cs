using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Ein Item im saisonalen Shop. 1:1-Port aus dem Avalonia-Original (Models/SeasonalEvent.cs).
    /// Persistenz: Newtonsoft.Json.
    /// </summary>
    public class SeasonalShopItem
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("nameKey")]
        public string NameKey { get; set; } = "";

        [JsonProperty("descriptionKey")]
        public string DescriptionKey { get; set; } = "";

        [JsonProperty("cost")]
        public int Cost { get; set; }

        [JsonProperty("effect")]
        public SeasonalItemEffect Effect { get; set; } = new SeasonalItemEffect();

        [JsonProperty("icon")]
        public string Icon { get; set; } = "";
    }

    /// <summary>
    /// Ein saisonales Event (2 Wochen, 4x pro Jahr). 1:1-Port aus dem Avalonia-Original.
    /// Season-Enum ist in LiveOpsEnums.cs (Schicht 10). SeasonColor/SeasonIcon (UI) wandern in die
    /// Präsentationsschicht; CheckSeason (Datums-Gameplay) bleibt. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class SeasonalEvent
    {
        [JsonProperty("season")]
        public Season Season { get; set; }

        [JsonProperty("startDate")]
        public DateTime StartDate { get; set; }

        [JsonProperty("endDate")]
        public DateTime EndDate { get; set; }

        [JsonProperty("currency")]
        public int Currency { get; set; }

        [JsonProperty("totalPoints")]
        public int TotalPoints { get; set; }

        [JsonProperty("completedOrders")]
        public int CompletedOrders { get; set; }

        [JsonProperty("purchasedItems")]
        public List<string> PurchasedItems { get; set; } = new List<string>();

        [JsonIgnore]
        public bool IsActive => DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;

        [JsonIgnore]
        public TimeSpan TimeRemaining => IsActive ? EndDate - DateTime.UtcNow : TimeSpan.Zero;

        /// <summary>Prüft ob ein bestimmtes Datum in einem Saison-Zeitraum liegt (1.-14. des Saison-Monats).</summary>
        public static (bool isActive, Season season) CheckSeason(DateTime date)
        {
            int month = date.Month;
            int day = date.Day;

            if (month == 3 && day >= 1 && day <= 14) return (true, Season.Spring);
            if (month == 6 && day >= 1 && day <= 14) return (true, Season.Summer);
            if (month == 9 && day >= 1 && day <= 14) return (true, Season.Autumn);
            if (month == 12 && day >= 1 && day <= 14) return (true, Season.Winter);

            return (false, Season.Spring);
        }
    }
}
