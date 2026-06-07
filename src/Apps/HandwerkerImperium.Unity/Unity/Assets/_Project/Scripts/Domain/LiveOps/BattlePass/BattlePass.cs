using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Der aktuelle Battle-Pass-Zustand (30-Tage-Saison, 50 Tiers, Free/Premium-Track).
    /// 1:1-Port aus dem Avalonia-Original (Models/BattlePass.cs). BattlePassReward + Enum sind in
    /// Schicht 10/11. SeasonThemeColor/-Icon + CapstoneRewardKey (UI/Lokalisierung) wandern in die
    /// Präsentationsschicht; SeasonTheme (Gameplay) + Reward-Generatoren bleiben. Persistenz: Newtonsoft.Json.
    /// </summary>
    public class BattlePass
    {
        [JsonProperty("seasonNumber")]
        public int SeasonNumber { get; set; } = 1;

        [JsonProperty("currentTier")]
        public int CurrentTier { get; set; }

        [JsonProperty("currentXp")]
        public int CurrentXp { get; set; }

        [JsonProperty("isPremium")]
        public bool IsPremium { get; set; }

        [JsonProperty("seasonStartDate")]
        public DateTime SeasonStartDate { get; set; } = DateTime.UtcNow;

        [JsonProperty("claimedFreeTiers")]
        public List<int> ClaimedFreeTiers { get; set; } = new List<int>();

        [JsonProperty("claimedPremiumTiers")]
        public List<int> ClaimedPremiumTiers { get; set; } = new List<int>();

        /// <summary>Fixiertes Basis-Einkommen beim Saisonstart (verhindert überhöhte späte Tier-Rewards).</summary>
        [JsonProperty("baseIncomeAtSeasonStart")]
        public decimal BaseIncomeAtSeasonStart { get; set; }

        /// <summary>Maximale Tier-Anzahl.</summary>
        public const int MaxTier = 50;

        /// <summary>
        /// XP für das nächste Tier. Basis 250*(Tier+1); Tiers 41-50 (Index ≥40) doppelt.
        /// </summary>
        [JsonIgnore]
        public int XpForNextTier
        {
            get
            {
                int baseXp = 250 * (CurrentTier + 1);
                return CurrentTier >= 40 ? baseXp * 2 : baseXp;
            }
        }

        /// <summary>Fortschritt zum nächsten Tier (0-1).</summary>
        [JsonIgnore]
        public double TierProgress => XpForNextTier > 0
            ? Math.Clamp((double)CurrentXp / XpForNextTier, 0.0, 1.0) : 0.0;

        /// <summary>Saison-Dauer in Tagen.</summary>
        public const int SeasonDurationDays = 30;

        /// <summary>Verbleibende Tage in der Saison.</summary>
        [JsonIgnore]
        public int DaysRemaining => Math.Max(0, SeasonDurationDays - (int)(DateTime.UtcNow - SeasonStartDate).TotalDays);

        /// <summary>Ob die Saison abgelaufen ist.</summary>
        [JsonIgnore]
        public bool IsSeasonExpired => (DateTime.UtcNow - SeasonStartDate).TotalDays > SeasonDurationDays;

        /// <summary>Saisonales Theme basierend auf der Saison-Nummer (zyklisch 0-3).</summary>
        [JsonIgnore]
        public Season SeasonTheme => (SeasonNumber % 4) switch
        {
            0 => Season.Spring,
            1 => Season.Summer,
            2 => Season.Autumn,
            3 => Season.Winter,
            _ => Season.Spring
        };

        /// <summary>Fügt XP hinzu und prüft Tier-Aufstieg. Liefert die Anzahl der Tier-Ups.</summary>
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
        /// Generiert die Free-Track-Belohnungen für alle 50 Tiers. Tiers 0-29 Basis, 30-48 verbessert
        /// (Meilenstein-GS auf 35/40/45), Tier 49 Capstone (50 GS).
        /// </summary>
        public static List<BattlePassReward> GenerateFreeRewards(decimal baseIncome)
        {
            var rewards = new List<BattlePassReward>();
            decimal baseMoney = Math.Max(500m, baseIncome * 60m);

            for (int i = 0; i < MaxTier; i++)
            {
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
                else
                {
                    int gsReward = i switch
                    {
                        34 => 15, // Tier 35
                        39 => 20, // Tier 40
                        44 => 25, // Tier 45
                        _ => i % 2 == 0 ? 3 : 0 // Gerade Tiers: 3 GS
                    };

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
        /// Generiert die Premium-Track-Belohnungen für alle 50 Tiers. Tiers 0-29 Basis (12/3 GS),
        /// SpeedBoost auf 35/45, Meilenstein 40, Tier 49 Capstone (150 GS + saisonaler Reward).
        /// </summary>
        public static List<BattlePassReward> GeneratePremiumRewards(decimal baseIncome, int seasonNumber = 1)
        {
            var rewards = new List<BattlePassReward>();
            decimal baseMoney = Math.Max(1500m, baseIncome * 180m);

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
                if (i < 30)
                {
                    rewards.Add(new BattlePassReward
                    {
                        Tier = i,
                        IsFree = false,
                        MoneyReward = baseMoney * (1 + i * 0.75m),
                        XpReward = 100 + i * 50,
                        GoldenScrewReward = (i + 1) % 3 == 0 ? 12 : 3,
                        DescriptionKey = $"BPPremium_{i}"
                    });
                }
                else if (i == MaxTier - 1)
                {
                    rewards.Add(new BattlePassReward
                    {
                        Tier = i,
                        IsFree = false,
                        MoneyReward = baseMoney * (1 + i * 1.0m),
                        XpReward = 200 + i * 60,
                        GoldenScrewReward = 150,
                        DescriptionKey = $"BPCapstone{season}"
                    });
                }
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
                else if (i == 39)
                {
                    rewards.Add(new BattlePassReward
                    {
                        Tier = i,
                        IsFree = false,
                        MoneyReward = baseMoney * (1 + i * 0.85m),
                        XpReward = 150 + i * 55,
                        GoldenScrewReward = 50,
                        DescriptionKey = "BPPremiumMilestone40"
                    });
                }
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
}
