using System;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.Achievements
{
    /// <summary>
    /// Ein (Spieler-)Achievement. 1:1-Port aus dem Avalonia-Original (Models/Achievement.cs).
    /// Nur Gameplay-Felder: Title/Description (Lokalisierung) und Icon (Emoji/Material-Icon) sind
    /// Präsentation und werden in der Unity-Schicht (Unity Localization + Icon-System) gemappt.
    /// Persistenz: Newtonsoft.Json.
    /// </summary>
    public class Achievement
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("category")]
        public AchievementCategory Category { get; set; }

        /// <summary>Zielwert zum Freischalten.</summary>
        [JsonProperty("targetValue")]
        public long TargetValue { get; set; } = 1;

        /// <summary>Aktueller Fortschritt.</summary>
        [JsonProperty("currentValue")]
        public long CurrentValue { get; set; }

        /// <summary>Geld-Belohnung beim Freischalten.</summary>
        [JsonProperty("moneyReward")]
        public decimal MoneyReward { get; set; }

        /// <summary>XP-Belohnung beim Freischalten.</summary>
        [JsonProperty("xpReward")]
        public int XpReward { get; set; }

        /// <summary>Goldschrauben-Belohnung für schwierige Achievements.</summary>
        [JsonProperty("goldenScrewReward")]
        public int GoldenScrewReward { get; set; }

        [JsonProperty("isUnlocked")]
        public bool IsUnlocked { get; set; }

        [JsonProperty("unlockedAt")]
        public DateTime? UnlockedAt { get; set; }

        /// <summary>Ob der Spieler bereits per Rewarded Ad einen Boost genutzt hat (max 1x pro Achievement).</summary>
        [JsonProperty("hasUsedAdBoost")]
        public bool HasUsedAdBoost { get; set; }

        /// <summary>Fortschritt in Prozent (0-100).</summary>
        [JsonIgnore]
        public double Progress => TargetValue > 0 ? Math.Min(100.0, (double)CurrentValue / TargetValue * 100) : 0;

        /// <summary>Fortschritt als Bruch (0.0-1.0).</summary>
        [JsonIgnore]
        public double ProgressFraction => Progress / 100.0;

        /// <summary>Ob das Achievement kurz vor dem Freischalten ist (>75%).</summary>
        [JsonIgnore]
        public bool IsCloseToUnlock => !IsUnlocked && Progress >= 75;
    }
}
