using System.Text.Json.Serialization;

namespace HandwerkerImperium.Models;

/// <summary>
/// Belohnung auf einem Battle-Pass-Tier.
/// </summary>
public class BattlePassReward
{
    [JsonPropertyName("tier")]
    public int Tier { get; set; }

    [JsonPropertyName("isFree")]
    public bool IsFree { get; set; }

    [JsonPropertyName("moneyReward")]
    public decimal MoneyReward { get; set; }

    [JsonPropertyName("xpReward")]
    public int XpReward { get; set; }

    [JsonPropertyName("goldenScrewReward")]
    public int GoldenScrewReward { get; set; }

    [JsonPropertyName("descriptionKey")]
    public string DescriptionKey { get; set; } = "";
}

/// <summary>
/// Der aktuelle Battle-Pass-Zustand.
/// </summary>
public class BattlePass
{
    [JsonPropertyName("seasonNumber")]
    public int SeasonNumber { get; set; } = 1;

    [JsonPropertyName("currentTier")]
    public int CurrentTier { get; set; }

    [JsonPropertyName("currentXp")]
    public int CurrentXp { get; set; }

    [JsonPropertyName("isPremium")]
    public bool IsPremium { get; set; }

    [JsonPropertyName("seasonStartDate")]
    public DateTime SeasonStartDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("claimedFreeTiers")]
    public List<int> ClaimedFreeTiers { get; set; } = [];

    [JsonPropertyName("claimedPremiumTiers")]
    public List<int> ClaimedPremiumTiers { get; set; } = [];

    /// <summary>
    /// Maximale Tier-Anzahl.
    /// </summary>
    [JsonIgnore]
    public const int MaxTier = 30;

    /// <summary>
    /// XP benötigt für das nächste Tier.
    /// </summary>
    [JsonIgnore]
    public int XpForNextTier => 1000 * (CurrentTier + 1);

    /// <summary>
    /// Fortschritt zum nächsten Tier (0-1).
    /// </summary>
    [JsonIgnore]
    public double TierProgress => XpForNextTier > 0
        ? Math.Clamp((double)CurrentXp / XpForNextTier, 0.0, 1.0) : 0.0;

    /// <summary>
    /// Verbleibende Tage in der Saison.
    /// </summary>
    [JsonIgnore]
    public int DaysRemaining => Math.Max(0, 30 - (int)(DateTime.UtcNow - SeasonStartDate).TotalDays);

    /// <summary>
    /// Ob die Saison abgelaufen ist.
    /// </summary>
    [JsonIgnore]
    public bool IsSeasonExpired => (DateTime.UtcNow - SeasonStartDate).TotalDays > 30;

    /// <summary>
    /// Saisonales Theme basierend auf der Saison-Nummer (zyklisch 0-3).
    /// </summary>
    [JsonIgnore]
    public Season SeasonTheme => (SeasonNumber % 4) switch
    {
        0 => Season.Spring,
        1 => Season.Summer,
        2 => Season.Autumn,
        3 => Season.Winter,
        _ => Season.Spring
    };

    /// <summary>
    /// Farbe des saisonalen Themes.
    /// </summary>
    [JsonIgnore]
    public string SeasonThemeColor => SeasonTheme switch
    {
        Season.Spring => "#4CAF50",
        Season.Summer => "#FF9800",
        Season.Autumn => "#795548",
        Season.Winter => "#2196F3",
        _ => "#4CAF50"
    };

    /// <summary>
    /// MaterialIconKind-String für das saisonale Theme-Icon.
    /// </summary>
    [JsonIgnore]
    public string SeasonThemeIcon => SeasonTheme switch
    {
        Season.Spring => "Flower",
        Season.Summer => "WhiteBalanceSunny",
        Season.Autumn => "Leaf",
        Season.Winter => "Snowflake",
        _ => "Flower"
    };

    /// <summary>
    /// Lokalisierungs-Key für die exklusive Capstone-Belohnung auf Tier 30.
    /// </summary>
    [JsonIgnore]
    public string CapstoneRewardKey => $"BPCapstone{SeasonTheme}";

    /// <summary>
    /// Fügt XP hinzu und prüft Tier-Aufstieg.
    /// </summary>
    public int AddXp(int amount)
    {
        int tierUps = 0;
        CurrentXp += amount;

        while (CurrentTier < MaxTier && CurrentXp >= XpForNextTier)
        {
            CurrentXp -= XpForNextTier;
            CurrentTier++;
            tierUps++;
        }

        // XP-Cap wenn Max-Tier erreicht
        if (CurrentTier >= MaxTier)
            CurrentXp = 0;

        return tierUps;
    }

    /// <summary>
    /// Generiert die Free-Track-Belohnungen für alle 30 Tiers.
    /// </summary>
    public static List<BattlePassReward> GenerateFreeRewards(decimal baseIncome)
    {
        var rewards = new List<BattlePassReward>();
        decimal baseMoney = Math.Max(500m, baseIncome * 60m);

        for (int i = 0; i < MaxTier; i++)
        {
            rewards.Add(new BattlePassReward
            {
                Tier = i,
                IsFree = true,
                MoneyReward = baseMoney * (1 + i * 0.5m),
                XpReward = 50 + i * 25,
                GoldenScrewReward = (i + 1) % 5 == 0 ? 3 : 0,
                DescriptionKey = $"BPFree_{i}"
            });
        }
        return rewards;
    }

    /// <summary>
    /// Generiert die Premium-Track-Belohnungen für alle 30 Tiers.
    /// Tier 30 (Index 29) enthält einen exklusiven saisonalen Capstone-Reward mit 20 Goldschrauben.
    /// </summary>
    public static List<BattlePassReward> GeneratePremiumRewards(decimal baseIncome, int seasonNumber = 1)
    {
        var rewards = new List<BattlePassReward>();
        decimal baseMoney = Math.Max(1000m, baseIncome * 120m);

        for (int i = 0; i < MaxTier; i++)
        {
            // Tier 30 (Index 29): Exklusiver saisonaler Capstone-Reward
            if (i == MaxTier - 1)
            {
                // Saison-Theme aus seasonNumber berechnen
                var season = (seasonNumber % 4) switch
                {
                    0 => Season.Spring,
                    1 => Season.Summer,
                    2 => Season.Autumn,
                    3 => Season.Winter,
                    _ => Season.Spring
                };

                rewards.Add(new BattlePassReward
                {
                    Tier = i,
                    IsFree = false,
                    MoneyReward = baseMoney * (1 + i * 0.75m),
                    XpReward = 100 + i * 50,
                    GoldenScrewReward = 20,
                    DescriptionKey = $"BPCapstone{season}"
                });
            }
            else
            {
                rewards.Add(new BattlePassReward
                {
                    Tier = i,
                    IsFree = false,
                    MoneyReward = baseMoney * (1 + i * 0.75m),
                    XpReward = 100 + i * 50,
                    GoldenScrewReward = (i + 1) % 3 == 0 ? 5 : 2,
                    DescriptionKey = $"BPPremium_{i}"
                });
            }
        }
        return rewards;
    }
}
