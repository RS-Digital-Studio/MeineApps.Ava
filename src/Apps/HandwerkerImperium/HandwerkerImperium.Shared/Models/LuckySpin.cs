using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Typ eines Glücksrad-Gewinns.
/// </summary>
public enum LuckySpinPrizeType
{
    MoneySmall,
    MoneyMedium,
    MoneyLarge,
    XpBoost,
    GoldenScrews5,
    SpeedBoost,
    ToolUpgrade,
    Jackpot50
}

/// <summary>
/// Ein einzelner Gewinn auf dem Glücksrad.
/// </summary>
public class LuckySpinPrize
{
    public LuckySpinPrizeType Type { get; set; }
    public string Icon { get; set; } = "";
    public string Color { get; set; } = "#D97706";
    public int Weight { get; set; } = 10;

    /// <summary>
    /// Berechnet den Gewinnwert basierend auf dem aktuellen Einkommen.
    /// </summary>
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
/// Zustand des Glücksrads.
/// </summary>
public class LuckySpinState
{
    [JsonPropertyName("lastFreeSpinDate")]
    public DateTime LastFreeSpinDate { get; set; } = DateTime.MinValue;

    [JsonPropertyName("totalSpins")]
    public int TotalSpins { get; set; }

    /// <summary>
    /// Anzahl kostenpflichtiger Spins heute (für steigende Kosten).
    /// </summary>
    [JsonPropertyName("paidSpinsToday")]
    public int PaidSpinsToday { get; set; }

    /// <summary>
    /// Datum des letzten kostenpflichtigen Spins (für Tages-Reset).
    /// </summary>
    [JsonPropertyName("lastPaidSpinDate")]
    public DateTime LastPaidSpinDate { get; set; } = DateTime.MinValue;

    [JsonIgnore]
    public bool HasFreeSpin => LastFreeSpinDate.Date < DateTime.UtcNow.Date;

    /// <summary>
    /// Prüft ob PaidSpinsToday zurückgesetzt werden muss (neuer Tag).
    /// </summary>
    public void ResetDailyIfNeeded()
    {
        if (LastPaidSpinDate.Date < DateTime.UtcNow.Date)
            PaidSpinsToday = 0;
    }
}
