using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Saison-Typ (4 pro Jahr).
/// </summary>
public enum Season
{
    Spring,  // 1.-14. März
    Summer,  // 1.-14. Juni
    Autumn,  // 1.-14. September
    Winter   // 1.-14. Dezember
}

/// <summary>
/// Effekt eines saisonalen Shop-Items.
/// </summary>
public class SeasonalItemEffect
{
    [JsonPropertyName("incomeBonus")]
    public decimal IncomeBonus { get; set; }

    [JsonPropertyName("xpBonus")]
    public int XpBonus { get; set; }

    [JsonPropertyName("goldenScrews")]
    public int GoldenScrews { get; set; }

    [JsonPropertyName("speedBoostMinutes")]
    public int SpeedBoostMinutes { get; set; }

    // Saison-einzigartige Effekte (Phase 2.9)
    // Effekt wird in GameLoopService/OfflineProgressService ausgewertet

    /// <summary>Temporaerer Extra-Worker-Slot (Dauer in Tagen).</summary>
    [JsonPropertyName("extraWorkerDays")]
    public int ExtraWorkerDays { get; set; }

    /// <summary>Temporaerer Research-Speed-Bonus in Prozent (Dauer in Tagen).</summary>
    [JsonPropertyName("researchSpeedBonusPercent")]
    public int ResearchSpeedBonusPercent { get; set; }

    /// <summary>Dauer fuer temporaere Boni in Tagen (ExtraWorker, ResearchSpeed, OfflineEarnings).</summary>
    [JsonPropertyName("effectDurationDays")]
    public int EffectDurationDays { get; set; }

    /// <summary>Naechster Prestige gibt doppelte PP (einmalig).</summary>
    [JsonPropertyName("doubleNextPrestige")]
    public bool DoubleNextPrestige { get; set; }

    /// <summary>Temporaerer Offline-Earnings-Bonus in Prozent (Dauer via EffectDurationDays).</summary>
    [JsonPropertyName("offlineEarningsBonusPercent")]
    public int OfflineEarningsBonusPercent { get; set; }

    /// <summary>Sofortige Goldschrauben-Belohnung.</summary>
    [JsonPropertyName("instantGoldenScrews")]
    public int InstantGoldenScrews { get; set; }

    /// <summary>Setzt alle Worker-Mood auf diesen Wert (0 = inaktiv).</summary>
    [JsonPropertyName("workerMoodResetTo")]
    public int WorkerMoodResetTo { get; set; }

    /// <summary>Speed-Boost-Dauer in Stunden (2x Geschwindigkeit).</summary>
    [JsonPropertyName("speedBoostHours")]
    public int SpeedBoostHours { get; set; }

    /// <summary>Naechster Daily-Reward wird verdoppelt (einmalig).</summary>
    [JsonPropertyName("doubleDailyReward")]
    public bool DoubleDailyReward { get; set; }
}

/// <summary>
/// Ein Item im saisonalen Shop.
/// </summary>
public class SeasonalShopItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("nameKey")]
    public string NameKey { get; set; } = "";

    [JsonPropertyName("descriptionKey")]
    public string DescriptionKey { get; set; } = "";

    [JsonPropertyName("cost")]
    public int Cost { get; set; }

    [JsonPropertyName("effect")]
    public SeasonalItemEffect Effect { get; set; } = new();

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "";
}

/// <summary>
/// Ein saisonales Event (2 Wochen, 4x pro Jahr).
/// </summary>
public class SeasonalEvent
{
    [JsonPropertyName("season")]
    public Season Season { get; set; }

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("currency")]
    public int Currency { get; set; }

    [JsonPropertyName("totalPoints")]
    public int TotalPoints { get; set; }

    [JsonPropertyName("completedOrders")]
    public int CompletedOrders { get; set; }

    [JsonPropertyName("purchasedItems")]
    public List<string> PurchasedItems { get; set; } = [];

    [JsonIgnore]
    public bool IsActive => DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;

    [JsonIgnore]
    public TimeSpan TimeRemaining => IsActive ? EndDate - DateTime.UtcNow : TimeSpan.Zero;

    [JsonIgnore]
    public string SeasonColor => Season switch
    {
        Season.Spring => "#4CAF50",
        Season.Summer => "#FF9800",
        Season.Autumn => "#795548",
        Season.Winter => "#2196F3",
        _ => "#D97706"
    };

    [JsonIgnore]
    public string SeasonIcon => Season switch
    {
        Season.Spring => "Flower",
        Season.Summer => "WhiteBalanceSunny",
        Season.Autumn => "Leaf",
        Season.Winter => "Snowflake",
        _ => "CalendarStar"
    };

    /// <summary>
    /// Prüft ob ein bestimmtes Datum in einem Saison-Zeitraum liegt.
    /// </summary>
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
