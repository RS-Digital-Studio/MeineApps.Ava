using System;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Ein einzelner Gewinn auf dem Glücksrad. 1:1-Port aus dem Avalonia-Original (Models/LuckySpin.cs).
    /// LuckySpinPrizeType-Enum ist in LiveOpsEnums.cs (Schicht 10). Icon/Color sind Konfig-Daten.
    /// </summary>
    public class LuckySpinPrize
    {
        public LuckySpinPrizeType Type { get; set; }
        public string Icon { get; set; } = "";
        public string Color { get; set; } = "#D97706";
        public int Weight { get; set; } = 10;

        /// <summary>Berechnet den Gewinnwert basierend auf dem aktuellen Einkommen.</summary>
        public static (decimal money, int screws, int xp, string description) CalculateReward(
            LuckySpinPrizeType type, decimal incomePerSecond)
        {
            decimal baseMoney = Math.Max(1000m, incomePerSecond * 300m);
            return type switch
            {
                LuckySpinPrizeType.MoneySmall => (baseMoney * 0.5m, 0, 0, ""),
                LuckySpinPrizeType.MoneyMedium => (baseMoney, 0, 0, ""),
                LuckySpinPrizeType.MoneyLarge => (baseMoney * 2m, 0, 0, ""),
                LuckySpinPrizeType.XpBoost => (0, 0, 500, ""),
                LuckySpinPrizeType.GoldenScrews5 => (0, 5, 0, ""),
                LuckySpinPrizeType.SpeedBoost => (0, 0, 0, "2x 30min"),
                LuckySpinPrizeType.ToolUpgrade => (0, 0, 0, ""),
                LuckySpinPrizeType.Jackpot50 => (0, 50, 0, ""),
                _ => (0, 0, 0, "")
            };
        }
    }

    /// <summary>
    /// Zustand des Glücksrads. 1:1-Port aus dem Avalonia-Original. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class LuckySpinState
    {
        [JsonProperty("lastFreeSpinDate")]
        public DateTime LastFreeSpinDate { get; set; } = DateTime.MinValue;

        [JsonProperty("totalSpins")]
        public int TotalSpins { get; set; }

        /// <summary>Anzahl kostenpflichtiger Spins heute (für steigende Kosten).</summary>
        [JsonProperty("paidSpinsToday")]
        public int PaidSpinsToday { get; set; }

        /// <summary>Datum des letzten kostenpflichtigen Spins (für Tages-Reset).</summary>
        [JsonProperty("lastPaidSpinDate")]
        public DateTime LastPaidSpinDate { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Gratis-Spin verfügbar wenn letzter Spin vor heute war. Bei Zeitmanipulation
        /// (LastFreeSpinDate in Zukunft) → false bis Datum aufholt (selbstbestrafend).
        /// </summary>
        [JsonIgnore]
        public bool HasFreeSpin => LastFreeSpinDate.Date < DateTime.UtcNow.Date;

        /// <summary>Täglicher Ad-Spin (1x/Tag per Video, nach Gratis-Spin).</summary>
        [JsonProperty("lastAdSpinDate")]
        public DateTime LastAdSpinDate { get; set; } = DateTime.MinValue;

        /// <summary>Datum des Bonus-Free-Spins (zweiter Gratis-Spin pro Tag, nur für Premium-Spieler).</summary>
        [JsonProperty("lastBonusFreeSpinDate")]
        public DateTime LastBonusFreeSpinDate { get; set; } = DateTime.MinValue;

        /// <summary>True wenn der Premium-Bonus-Spin heute noch nicht genutzt wurde.</summary>
        [JsonIgnore]
        public bool HasBonusFreeSpin => LastBonusFreeSpinDate.Date < DateTime.UtcNow.Date;

        /// <summary>Ad-Spin verfügbar wenn Gratis-Spin verbraucht und Ad-Spin heute noch nicht genutzt.</summary>
        [JsonIgnore]
        public bool HasAdSpin => !HasFreeSpin && LastAdSpinDate.Date < DateTime.UtcNow.Date;

        /// <summary>Prüft ob PaidSpinsToday zurückgesetzt werden muss (neuer Tag).</summary>
        public void ResetDailyIfNeeded()
        {
            if (LastPaidSpinDate.Date < DateTime.UtcNow.Date)
                PaidSpinsToday = 0;
        }
    }
}
