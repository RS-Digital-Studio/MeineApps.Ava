using System.Collections.Generic;
using UnityEngine;
using HandwerkerImperium.Domain.Idle;
using HandwerkerImperium.Domain.Config;

namespace HandwerkerImperium.Game
{
    /// <summary>
    /// Zentrales Tuning-ScriptableObject des Vollspiels (.md-Mandat "kein Hardcoding"): bündelt den P0-Idle-Loop
    /// (<see cref="idle"/> → <see cref="IdleBalancing"/>) mit allen Meta-Tunables (→ <see cref="GameBalancing"/>).
    /// Unity serialisiert kein <c>decimal</c> → float-Felder, Cast nach decimal in den ToDomain-Mappern. Per
    /// Remote-Config live tunebar (P3/P4).
    /// </summary>
    [CreateAssetMenu(fileName = "GameBalancingConfig", menuName = "HandwerkerImperium/Game Balancing")]
    public sealed class GameBalancingConfig : ScriptableObject
    {
        [Header("Idle-Loop (P0)")]
        public BalancingConfig idle;

        [Header("Prestige")]
        public float[] prestigeStageMultipliers = { 3f, 4f, 5f };
        public int maxPrestige = 3;
        public int marksPerPrestige = 5;

        [Header("Meisterschaft")]
        public float masteryBaseXp = 100f;
        public float masteryGrowth = 1.15f;
        public float masteryBonusPerLevel = 0.01f;

        [Header("Meistergrad (Endgame)")]
        public float meistergradBaseCost = 1000f;
        public float meistergradGrowth = 1.5f;
        public float meistergradBonusPerGrade = 0.02f;
        public float renommeeAccrualRate = 0.0001f;

        [Header("Stern-Rating")]
        public float[] starThresholds = { 100f, 300f, 600f, 1000f };
        public float starHysteresisBuffer = 50f;
        public float starWorkshopWeight = 50f;
        public float starRestorationWeight = 40f;
        public float starVolumeWeight = 2f;

        [Header("Soft-Cap / World-Tier")]
        public float softCapThresholdBase = 4f;
        public float starThresholdPerCityScale = 1.6f;
        public float incomeTargetPerCityScale = 8f;

        [Header("Orders / Rush")]
        public float orderSpawnInterval = 3f;
        public int orderMaxQueue = 12;
        public float orderRushRewardMult = 3f;
        public float orderRushDuration = 60f;
        public float rushMultiplier = 2f;
        public float rushDuration = 30f;
        public float rushCooldown = 86400f;

        [Header("Restoration / Daily / Offline")]
        public float restorationPhaseBaseCost = 5000f;
        public float restorationPhaseGrowth = 1.8f;
        public int restorationTotalPhases = 5;
        public int dailyLadderLength = 30;
        public float offlineBaseCapHours = 2f;
        public float offlinePremiumExtraHours = 14f;
        public float offlinePremiumMultiplier = 1.5f;

        [Header("Perkboard")]
        public int perkMarkCostBase = 10;
        public float perkMarkCostGrowth = 1.5f;
        public int perkDefaultMaxLevel = 5;
        public float perkBonusPerLevel = 0.05f;

        [Header("Monetarisierung")]
        public float freeCashAdMultiplier = 2f;
        public float freeCashBlockSeconds = 120f;
        public float premiumIncomeBonus = 0.5f;
        public int migrationBonusGems = 100;

        [Header("Referral")]
        public int[] referralTierThresholds = { 1, 5, 10 };
        public int[] referralTierRewards = { 50, 200, 500 };

        /// <summary>Idle-Loop-Balancing (P0); fällt auf Default zurück, wenn kein Idle-Config gesetzt ist.</summary>
        public IdleBalancing ToIdleBalancing() => idle != null ? idle.ToDomain() : new IdleBalancing();

        /// <summary>Meta-Balancing (Domain), mit decimal-Casts der Float-Inspector-Werte.</summary>
        public GameBalancing ToGameBalancing()
        {
            var b = new GameBalancing();
            b.Prestige.StageMultipliers = ToDecimals(prestigeStageMultipliers);
            b.Prestige.MaxPrestige = maxPrestige;
            b.Prestige.MarksPerPrestige = marksPerPrestige;

            b.Mastery.BaseXp = masteryBaseXp;
            b.Mastery.Growth = masteryGrowth;
            b.Mastery.BonusPerLevel = (decimal)masteryBonusPerLevel;

            b.Meistergrad.RenommeeBaseCost = (decimal)meistergradBaseCost;
            b.Meistergrad.Growth = meistergradGrowth;
            b.Meistergrad.BonusPerGrade = (decimal)meistergradBonusPerGrade;
            b.Meistergrad.AccrualRatePerIncome = (decimal)renommeeAccrualRate;

            b.Star.Thresholds = ToDoubles(starThresholds);
            b.Star.HysteresisBuffer = starHysteresisBuffer;
            b.Star.WorkshopWeight = starWorkshopWeight;
            b.Star.RestorationWeight = starRestorationWeight;
            b.Star.VolumeWeight = starVolumeWeight;

            b.SoftCap.ThresholdBase = (decimal)softCapThresholdBase;
            b.WorldTier.StarThresholdPerCityScale = starThresholdPerCityScale;
            b.WorldTier.IncomeTargetPerCityScale = (decimal)incomeTargetPerCityScale;

            b.Orders.SpawnIntervalSeconds = orderSpawnInterval;
            b.Orders.MaxQueue = orderMaxQueue;
            b.Orders.RushRewardMultiplier = (decimal)orderRushRewardMult;
            b.Orders.RushDurationSeconds = orderRushDuration;

            b.Rush.Multiplier = (decimal)rushMultiplier;
            b.Rush.DurationSeconds = rushDuration;
            b.Rush.CooldownSeconds = rushCooldown;

            b.Restoration.PhaseBaseCost = (decimal)restorationPhaseBaseCost;
            b.Restoration.PhaseGrowth = restorationPhaseGrowth;
            b.Restoration.TotalPhases = restorationTotalPhases;

            b.Daily.LadderLength = dailyLadderLength;

            b.Offline.BaseCapHours = offlineBaseCapHours;
            b.Offline.PremiumExtraHours = offlinePremiumExtraHours;
            b.Offline.PremiumMultiplier = (decimal)offlinePremiumMultiplier;

            b.Perkboard.MarkCostBase = perkMarkCostBase;
            b.Perkboard.MarkCostGrowth = perkMarkCostGrowth;
            b.Perkboard.DefaultMaxLevel = perkDefaultMaxLevel;
            b.Perkboard.BonusPerLevel = (decimal)perkBonusPerLevel;

            b.Monetization.FreeCashAdMultiplier = (decimal)freeCashAdMultiplier;
            b.Monetization.FreeCashBlockSeconds = freeCashBlockSeconds;
            b.Monetization.PremiumIncomeBonus = (decimal)premiumIncomeBonus;
            b.Monetization.MigrationBonusGems = migrationBonusGems;

            b.Referral.TierThresholds = new List<int>(referralTierThresholds);
            b.Referral.TierRewards = new List<int>(referralTierRewards);
            return b;
        }

        private static List<decimal> ToDecimals(float[] a)
        {
            var l = new List<decimal>();
            if (a != null) foreach (var x in a) l.Add((decimal)x);
            return l;
        }

        private static List<double> ToDoubles(float[] a)
        {
            var l = new List<double>();
            if (a != null) foreach (var x in a) l.Add(x);
            return l;
        }
    }
}
