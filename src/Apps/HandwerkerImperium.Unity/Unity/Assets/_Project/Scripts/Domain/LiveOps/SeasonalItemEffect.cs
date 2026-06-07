using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Effekt eines saisonalen Shop-Items.
    /// 1:1-Port aus dem Avalonia-Original (Models/SeasonalEvent.cs). Persistenz: Newtonsoft.Json.
    /// </summary>
    public class SeasonalItemEffect
    {
        [JsonProperty("incomeBonus")]
        public decimal IncomeBonus { get; set; }

        [JsonProperty("xpBonus")]
        public int XpBonus { get; set; }

        [JsonProperty("goldenScrews")]
        public int GoldenScrews { get; set; }

        [JsonProperty("speedBoostMinutes")]
        public int SpeedBoostMinutes { get; set; }

        /// <summary>Temporärer Extra-Worker-Slot (Dauer in Tagen).</summary>
        [JsonProperty("extraWorkerDays")]
        public int ExtraWorkerDays { get; set; }

        /// <summary>Temporärer Research-Speed-Bonus in Prozent (Dauer in Tagen).</summary>
        [JsonProperty("researchSpeedBonusPercent")]
        public int ResearchSpeedBonusPercent { get; set; }

        /// <summary>Dauer für temporäre Boni in Tagen (ExtraWorker, ResearchSpeed, OfflineEarnings).</summary>
        [JsonProperty("effectDurationDays")]
        public int EffectDurationDays { get; set; }

        /// <summary>Nächster Prestige gibt doppelte PP (einmalig).</summary>
        [JsonProperty("doubleNextPrestige")]
        public bool DoubleNextPrestige { get; set; }

        /// <summary>Temporärer Offline-Earnings-Bonus in Prozent (Dauer via EffectDurationDays).</summary>
        [JsonProperty("offlineEarningsBonusPercent")]
        public int OfflineEarningsBonusPercent { get; set; }

        /// <summary>Sofortige Goldschrauben-Belohnung.</summary>
        [JsonProperty("instantGoldenScrews")]
        public int InstantGoldenScrews { get; set; }

        /// <summary>Setzt alle Worker-Mood auf diesen Wert (0 = inaktiv).</summary>
        [JsonProperty("workerMoodResetTo")]
        public int WorkerMoodResetTo { get; set; }

        /// <summary>Speed-Boost-Dauer in Stunden (2x Geschwindigkeit).</summary>
        [JsonProperty("speedBoostHours")]
        public int SpeedBoostHours { get; set; }

        /// <summary>Nächster Daily-Reward wird verdoppelt (einmalig).</summary>
        [JsonProperty("doubleDailyReward")]
        public bool DoubleDailyReward { get; set; }
    }
}
