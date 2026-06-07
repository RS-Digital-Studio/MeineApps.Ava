using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Eine Tagesbelohnung für einen Tag im 30-Tage-Zyklus.
    /// 1:1-Port aus dem Avalonia-Original (Models/DailyReward.cs). DailyBonusType-Enum ist in
    /// LiveOpsEnums.cs (Schicht 10). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class DailyReward
    {
        /// <summary>Tag im Zyklus (1-30).</summary>
        [JsonProperty("day")]
        public int Day { get; set; }

        [JsonProperty("money")]
        public decimal Money { get; set; }

        [JsonProperty("xp")]
        public int Xp { get; set; }

        [JsonProperty("goldenScrews")]
        public int GoldenScrews { get; set; }

        [JsonProperty("bonusType")]
        public DailyBonusType BonusType { get; set; } = DailyBonusType.None;

        [JsonIgnore]
        public bool IsClaimed { get; set; }

        [JsonIgnore]
        public bool IsToday { get; set; }

        [JsonIgnore]
        public bool IsAvailable => IsToday && !IsClaimed;

        /// <summary>
        /// Dynamisch skalierte Geld-Belohnung: max(FesterBetrag, sqrt(Tag) * Minuten-Einkommen),
        /// gedeckelt auf 15 Minuten Einkommen.
        /// </summary>
        public decimal GetScaledMoney(decimal netIncomePerSecond)
        {
            if (netIncomePerSecond <= 0) return Money;

            decimal minutesFactor = (decimal)Math.Sqrt(Day);
            decimal minutesWorth = minutesFactor * netIncomePerSecond * 60m;

            decimal maxReward = netIncomePerSecond * 900m;
            minutesWorth = Math.Min(minutesWorth, maxReward);

            return Math.Max(Money, minutesWorth);
        }

        // Gecachter 30-Tage-Belohnungsplan (statisch, nur einmal erstellt).
        private static readonly List<DailyReward> s_cachedSchedule = new List<DailyReward>
        {
            // Woche 1: Einstieg
            new DailyReward { Day = 1, Money = 500m, Xp = 0, GoldenScrews = 0 },
            new DailyReward { Day = 2, Money = 750m, Xp = 0, GoldenScrews = 1 },
            new DailyReward { Day = 3, Money = 1_000m, Xp = 25, GoldenScrews = 0 },
            new DailyReward { Day = 4, Money = 1_500m, Xp = 0, GoldenScrews = 2 },
            new DailyReward { Day = 5, Money = 2_000m, Xp = 50, GoldenScrews = 0 },
            new DailyReward { Day = 6, Money = 2_500m, Xp = 0, GoldenScrews = 3 },
            new DailyReward { Day = 7, Money = 5_000m, Xp = 100, GoldenScrews = 5, BonusType = DailyBonusType.SpeedBoost },
            // Woche 2: Aufbau
            new DailyReward { Day = 8, Money = 3_000m, Xp = 50, GoldenScrews = 0 },
            new DailyReward { Day = 9, Money = 3_500m, Xp = 0, GoldenScrews = 3 },
            new DailyReward { Day = 10, Money = 4_000m, Xp = 75, GoldenScrews = 0 },
            new DailyReward { Day = 11, Money = 5_000m, Xp = 0, GoldenScrews = 4 },
            new DailyReward { Day = 12, Money = 6_000m, Xp = 100, GoldenScrews = 0 },
            new DailyReward { Day = 13, Money = 7_000m, Xp = 0, GoldenScrews = 5 },
            new DailyReward { Day = 14, Money = 10_000m, Xp = 200, GoldenScrews = 8, BonusType = DailyBonusType.XpBoost },
            // Woche 3: Steigerung
            new DailyReward { Day = 15, Money = 8_000m, Xp = 100, GoldenScrews = 0 },
            new DailyReward { Day = 16, Money = 9_000m, Xp = 0, GoldenScrews = 5 },
            new DailyReward { Day = 17, Money = 10_000m, Xp = 150, GoldenScrews = 0 },
            new DailyReward { Day = 18, Money = 12_000m, Xp = 0, GoldenScrews = 6 },
            new DailyReward { Day = 19, Money = 15_000m, Xp = 200, GoldenScrews = 0 },
            new DailyReward { Day = 20, Money = 18_000m, Xp = 0, GoldenScrews = 8 },
            new DailyReward { Day = 21, Money = 25_000m, Xp = 300, GoldenScrews = 10, BonusType = DailyBonusType.SpeedBoost },
            // Woche 4: Jackpot-Finale
            new DailyReward { Day = 22, Money = 15_000m, Xp = 150, GoldenScrews = 0 },
            new DailyReward { Day = 23, Money = 18_000m, Xp = 0, GoldenScrews = 8 },
            new DailyReward { Day = 24, Money = 20_000m, Xp = 200, GoldenScrews = 0 },
            new DailyReward { Day = 25, Money = 25_000m, Xp = 0, GoldenScrews = 10 },
            new DailyReward { Day = 26, Money = 30_000m, Xp = 300, GoldenScrews = 0 },
            new DailyReward { Day = 27, Money = 35_000m, Xp = 0, GoldenScrews = 12 },
            new DailyReward { Day = 28, Money = 40_000m, Xp = 400, GoldenScrews = 15, BonusType = DailyBonusType.XpBoost },
            new DailyReward { Day = 29, Money = 50_000m, Xp = 500, GoldenScrews = 15 },
            new DailyReward { Day = 30, Money = 100_000m, Xp = 1_000, GoldenScrews = 25, BonusType = DailyBonusType.SpeedBoost },
        };

        /// <summary>Gibt den gecachten 30-Tage-Belohnungsplan zurück (IsToday/IsClaimed nicht direkt ändern).</summary>
        public static List<DailyReward> GetRewardSchedule() => s_cachedSchedule;
    }
}
