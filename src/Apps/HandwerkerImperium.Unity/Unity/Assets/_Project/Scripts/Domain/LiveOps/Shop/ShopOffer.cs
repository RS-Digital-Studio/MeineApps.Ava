using System;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Ein tägliches vergünstigtes Angebot im Shop. 1:1-Port aus dem Avalonia-Original (Models/ShopOffer.cs).
    /// GenerateDaily nimmt jetzt System.Random-Instanz statt Random.Shared. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class ShopOffer
    {
        [JsonProperty("itemId")]
        public string ItemId { get; set; } = "";

        [JsonProperty("nameKey")]
        public string NameKey { get; set; } = "";

        [JsonProperty("originalPrice")]
        public int OriginalPrice { get; set; }

        [JsonProperty("discountedPrice")]
        public int DiscountedPrice { get; set; }

        [JsonProperty("discount")]
        public int Discount { get; set; } = 50; // Prozent

        [JsonProperty("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        [JsonProperty("goldenScrewReward")]
        public int GoldenScrewReward { get; set; }

        [JsonProperty("moneyReward")]
        public decimal MoneyReward { get; set; }

        [JsonIgnore]
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        [JsonIgnore]
        public TimeSpan TimeRemaining => IsExpired ? TimeSpan.Zero : ExpiresAt - DateTime.UtcNow;

        /// <summary>
        /// Generiert ein zufälliges tägliches Angebot. <paramref name="rng"/> wählt das Angebot
        /// (ersetzt Random.Shared des Originals; deterministisch je Tag).
        /// </summary>
        public static ShopOffer GenerateDaily(decimal incomePerSecond, Random rng)
        {
            var offers = new (string, string, int, int, decimal)[]
            {
                ("daily_screws_10", "DailyOfferScrews10", 20, 10, 0m),
                ("daily_screws_25", "DailyOfferScrews25", 50, 25, 0m),
                ("daily_money_boost", "DailyOfferMoneyBoost", 15, 0, Math.Max(5000m, incomePerSecond * 600m)),
                ("daily_speed_boost", "DailyOfferSpeedBoost", 10, 0, 0m),
            };

            var selected = offers[rng.Next(offers.Length)];
            return new ShopOffer
            {
                ItemId = selected.Item1,
                NameKey = selected.Item2,
                OriginalPrice = selected.Item3,
                DiscountedPrice = selected.Item3 / 2,
                Discount = 50,
                GoldenScrewReward = selected.Item4,
                MoneyReward = selected.Item5,
                ExpiresAt = DateTime.UtcNow.Date.AddDays(1) // Bis Mitternacht
            };
        }
    }
}
