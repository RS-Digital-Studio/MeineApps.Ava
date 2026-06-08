using System;
using Newtonsoft.Json;
using HandwerkerImperium.Domain.Economy;

namespace HandwerkerImperium.Domain.Orders
{
    /// <summary>
    /// Schnell-Auftrag: Direkter Minigame-Zugang mit kleiner Belohnung. Rotiert alle 15 Minuten.
    /// 1:1-Port aus dem Avalonia-Original (Models/QuickJob.cs). Display-Felder (DisplayTitle/
    /// DisplayWorkshopName/RewardDisplay) wandern in die Präsentationsschicht. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class QuickJob
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("workshopType")]
        public WorkshopType WorkshopType { get; set; }

        [JsonProperty("difficulty")]
        public OrderDifficulty Difficulty { get; set; } = OrderDifficulty.Easy;

        [JsonProperty("miniGameType")]
        public MiniGameType MiniGameType { get; set; }

        [JsonProperty("reward")]
        public decimal Reward { get; set; }

        [JsonProperty("xpReward")]
        public int XpReward { get; set; }

        [JsonProperty("titleKey")]
        public string TitleKey { get; set; } = string.Empty;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("isCompleted")]
        public bool IsCompleted { get; set; }

        /// <summary>Ob die Belohnung per Rewarded-Ad verdoppelt wurde.</summary>
        [JsonIgnore] public bool IsScoreDoubled { get; set; }
    }
}
