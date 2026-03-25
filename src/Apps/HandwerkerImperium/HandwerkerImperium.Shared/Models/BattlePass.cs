using System.Text.Json.Serialization;
using HandwerkerImperium.Models.Enums;

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

    [JsonPropertyName("rewardType")]
    public BattlePassRewardType RewardType { get; set; } = BattlePassRewardType.Standard;

    [JsonPropertyName("descriptionKey")]
    public string DescriptionKey { get; set; } = "";

    /// <summary>
    /// Dauer des SpeedBoosts in Minuten (nur bei RewardType == SpeedBoost).
    /// </summary>
    [JsonPropertyName("speedBoostMinutes")]
    public int SpeedBoostMinutes { get; set; }
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
    public const int MaxTier = 50;

    /// <summary>
    /// XP benötigt für das nächste Tier.
    /// </summary>
    [JsonIgnore]
    // BAL-41: Von 1000 auf 500 gesenkt - Tier 50 war in 30 Tagen unerreichbar
    // Tiers 31-50: Doppelte XP-Anforderung (Hardcore-Spieler, endgame-würdig)
    public int XpForNextTier
    {
        get
        {
            int baseXp = 500 * (CurrentTier + 1);
            return CurrentTier >= 30 ? baseXp * 2 : baseXp;
        }
    }

    /// <summary>
    /// Fortschritt zum nächsten Tier (0-1).
    /// </summary>
    [JsonIgnore]
    public double TierProgress => XpForNextTier > 0
        ? Math.Clamp((double)CurrentXp / XpForNextTier, 0.0, 1.0) : 0.0;

    /// <summary>
    /// Saison-Dauer in Tagen (6 Wochen fuer 50 Tiers).
    /// </summary>
    [JsonIgnore]
    public const int SeasonDurationDays = 42;

    /// <summary>
    /// Verbleibende Tage in der Saison.
    /// </summary>
    [JsonIgnore]
    public int DaysRemaining => Math.Max(0, SeasonDurationDays - (int)(DateTime.UtcNow - SeasonStartDate).TotalDays);

    /// <summary>
    /// Ob die Saison abgelaufen ist.
    /// </summary>
    [JsonIgnore]
    public bool IsSeasonExpired => (DateTime.UtcNow - SeasonStartDate).TotalDays > SeasonDurationDays;

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
    /// GameIconKind-String fuer das saisonale Theme-Icon.
    /// </summary>
    [JsonIgnore]
    public string SeasonThemeIcon => SeasonTheme switch
    {
        Season.Spring => "Flower",
        Season.Summer => "WhiteBalanceSunny",
        Season.Autumn => "Forest",
        Season.Winter => "Snowflake",
        _ => "Flower"
    };

    /// <summary>
    /// Lokalisierungs-Key für die exklusive Capstone-Belohnung auf dem letzten Tier.
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
    /// Generiert die Free-Track-Belohnungen für alle 50 Tiers.
    /// Tiers 0-29: Basis-Belohnungen.
    /// Tiers 30-49: Verbesserte Belohnungen mit Meilenstein-GS auf Tier 35/40/45.
    /// Tier 49 (Capstone): 50 GS + großer Geldbetrag.
    /// </summary>
    public static List<BattlePassReward> GenerateFreeRewards(decimal baseIncome)
    {
        var rewards = new List<BattlePassReward>();
        decimal baseMoney = Math.Max(500m, baseIncome * 60m);

        for (int i = 0; i < MaxTier; i++)
        {
            // Tiers 0-29: Bestehende Formel (unverändert)
            if (i < 30)
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
            // Tier 49 (Capstone): Großer Bonus
            else if (i == MaxTier - 1)
            {
                rewards.Add(new BattlePassReward
                {
                    Tier = i,
                    IsFree = true,
                    MoneyReward = baseMoney * (1 + i * 0.75m),
                    XpReward = 200 + i * 30,
                    GoldenScrewReward = 50,
                    DescriptionKey = "BPFreeCapstone"
                });
            }
            // Tiers 30-48: Verbesserte Belohnungen
            else
            {
                // Meilenstein-Tiers: Größere GS-Belohnungen
                int gsReward = i switch
                {
                    34 => 15, // Tier 35 (0-basiert: 34)
                    39 => 20, // Tier 40
                    44 => 25, // Tier 45
                    _ => i % 2 == 0 ? 3 : 0 // Gerade Tiers: 3 GS
                };

                // Ungerade Tiers: Mehr Geld, Gerade Tiers: XP + GS
                decimal moneyMult = i % 2 != 0 ? 0.75m : 0.6m;

                rewards.Add(new BattlePassReward
                {
                    Tier = i,
                    IsFree = true,
                    MoneyReward = baseMoney * (1 + i * moneyMult),
                    XpReward = 100 + i * 30,
                    GoldenScrewReward = gsReward,
                    DescriptionKey = $"BPFree_{i}"
                });
            }
        }
        return rewards;
    }

    /// <summary>
    /// Generiert die Premium-Track-Belohnungen für alle 50 Tiers.
    /// Tiers 0-29: Bestehende Formel (10 GS alle 3 Tiers, sonst 2 GS).
    /// Tiers 30-49: Verbesserte Rewards + SpeedBoost auf Tier 35/45.
    /// Tier 49 (Capstone): 100 GS + saisonaler Reward.
    /// </summary>
    public static List<BattlePassReward> GeneratePremiumRewards(decimal baseIncome, int seasonNumber = 1)
    {
        var rewards = new List<BattlePassReward>();
        decimal baseMoney = Math.Max(1000m, baseIncome * 120m);

        // Saison-Theme aus seasonNumber berechnen (für Capstone-Reward)
        var season = (seasonNumber % 4) switch
        {
            0 => Season.Spring,
            1 => Season.Summer,
            2 => Season.Autumn,
            3 => Season.Winter,
            _ => Season.Spring
        };

        for (int i = 0; i < MaxTier; i++)
        {
            // Tiers 0-29: Bestehende Formel (unverändert)
            if (i < 30)
            {
                rewards.Add(new BattlePassReward
                {
                    Tier = i,
                    IsFree = false,
                    MoneyReward = baseMoney * (1 + i * 0.75m),
                    XpReward = 100 + i * 50,
                    GoldenScrewReward = (i + 1) % 3 == 0 ? 10 : 2,
                    DescriptionKey = $"BPPremium_{i}"
                });
            }
            // Tier 49 (Premium Capstone): 100 GS + saisonaler Reward
            else if (i == MaxTier - 1)
            {
                rewards.Add(new BattlePassReward
                {
                    Tier = i,
                    IsFree = false,
                    MoneyReward = baseMoney * (1 + i * 1.0m),
                    XpReward = 200 + i * 60,
                    GoldenScrewReward = 100,
                    DescriptionKey = $"BPCapstone{season}"
                });
            }
            // Tier 34 (35): SpeedBoost 2x für 2h
            else if (i == 34)
            {
                rewards.Add(new BattlePassReward
                {
                    Tier = i,
                    IsFree = false,
                    RewardType = BattlePassRewardType.SpeedBoost,
                    SpeedBoostMinutes = 120,
                    MoneyReward = baseMoney * (1 + i * 0.85m),
                    XpReward = 150 + i * 55,
                    GoldenScrewReward = 5,
                    DescriptionKey = "BPPremiumSpeedBoost2h"
                });
            }
            // Tier 39 (40): 30 GS Meilenstein
            else if (i == 39)
            {
                rewards.Add(new BattlePassReward
                {
                    Tier = i,
                    IsFree = false,
                    MoneyReward = baseMoney * (1 + i * 0.85m),
                    XpReward = 150 + i * 55,
                    GoldenScrewReward = 30,
                    DescriptionKey = "BPPremiumMilestone40"
                });
            }
            // Tier 44 (45): SpeedBoost 2x für 4h
            else if (i == 44)
            {
                rewards.Add(new BattlePassReward
                {
                    Tier = i,
                    IsFree = false,
                    RewardType = BattlePassRewardType.SpeedBoost,
                    SpeedBoostMinutes = 240,
                    MoneyReward = baseMoney * (1 + i * 0.85m),
                    XpReward = 150 + i * 55,
                    GoldenScrewReward = 10,
                    DescriptionKey = "BPPremiumSpeedBoost4h"
                });
            }
            // Tiers 30-48 (regulär): Verbesserte Version der Basis-Formel
            else
            {
                rewards.Add(new BattlePassReward
                {
                    Tier = i,
                    IsFree = false,
                    MoneyReward = baseMoney * (1 + i * 0.85m),
                    XpReward = 150 + i * 55,
                    GoldenScrewReward = (i + 1) % 3 == 0 ? 12 : 3,
                    DescriptionKey = $"BPPremium_{i}"
                });
            }
        }
        return rewards;
    }
}
