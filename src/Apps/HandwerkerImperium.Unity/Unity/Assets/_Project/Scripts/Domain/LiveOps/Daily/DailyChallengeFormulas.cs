using System;
using System.Collections.Generic;
using HandwerkerImperium.Domain.Orders;

namespace HandwerkerImperium.Domain.LiveOps
{
    /// <summary>
    /// Service-Formel-Extrakt aus <c>DailyChallengeService</c> (Avalonia-Original): die reine,
    /// balancing-relevante Daily-Challenge-Mathematik (Tier-Berechnung, verfuegbare Typen je Tier,
    /// Einkommens-Basis, Challenge-Factory mit Zielwerten/Belohnungen, Alle-fertig-Bonus,
    /// Rating-Score-Mapping). 1:1 zur Vorlage.
    ///
    /// Bewusst NICHT extrahiert (state-mutierend / Service-/Event-gekoppelt, bleiben im Game-Service):
    /// GenerateDailyChallenges/ClaimReward/RetryChallenge/IncrementChallenge + alle Event-Handler +
    /// PopulateDisplayFields (Lokalisierung). Server-/Service-Eingaben (NetIncomePerSecond, hoechstes
    /// Workshop-Level, VIP-Extra) werden als Parameter uebergeben.
    /// </summary>
    public static class DailyChallengeFormulas
    {
        /// <summary>Geld-Bonus wenn alle Daily-Challenges abgeschlossen sind.</summary>
        public const decimal AllCompletedBonusAmount = 500m;

        /// <summary>
        /// Tier nach Spieler-Level. Tiers 0-4 bestehend, 5-8 Late-Game.
        /// </summary>
        public static int GetTier(int level) => level switch
        {
            <= 5 => 0,
            <= 15 => 1,
            <= 30 => 2,
            <= 50 => 3,
            <= 100 => 4,
            <= 300 => 5,
            <= 500 => 6,
            <= 750 => 7,
            _ => 8
        };

        /// <summary>Tier-basierter Alle-fertig-Bonus (GS): Tiers 0-4 = 6, dann 8/10/12/15.</summary>
        public static int CalculateAllCompletedBonusScrews(int level)
        {
            int tier = GetTier(level);
            return tier switch
            {
                <= 4 => 6,
                5 => 8,
                6 => 10,
                7 => 12,
                _ => 15
            };
        }

        /// <summary>Anzahl Daily-Challenges pro Tag: 3 + VIP-Extra (Silver+ +1).</summary>
        public static int CalculateChallengeCount(int vipExtraDailyChallenges) => 3 + vipExtraDailyChallenges;

        /// <summary>
        /// Fuer einen Tier verfuegbare Challenge-Typen. Neue Typen schalten ab bestimmten Tiers frei.
        /// </summary>
        public static List<DailyChallengeType> GetAvailableTypesForTier(int tier)
        {
            var types = new List<DailyChallengeType>
            {
                DailyChallengeType.CompleteOrders,
                DailyChallengeType.EarnMoney,
                DailyChallengeType.UpgradeWorkshop,
                DailyChallengeType.HireWorker,
                DailyChallengeType.CompleteQuickJob,
                DailyChallengeType.PlayMiniGames,
                DailyChallengeType.AchieveMinigameScore
            };

            if (tier >= 5)
            {
                types.Add(DailyChallengeType.TrainWorker);
                types.Add(DailyChallengeType.CompleteCrafting);
            }
            if (tier >= 6)
                types.Add(DailyChallengeType.AchievePerfectStreak);
            if (tier >= 7)
                types.Add(DailyChallengeType.ReachWorkshopLevel);
            if (tier >= 5)
            {
                types.Add(DailyChallengeType.ProduceItems);
                types.Add(DailyChallengeType.SellItems);
            }
            if (tier >= 6)
            {
                types.Add(DailyChallengeType.CompleteMaterialOrder);
                types.Add(DailyChallengeType.CollectEquipment);
            }

            return types;
        }

        /// <summary>
        /// Einkommens-Basis der Belohnungen: ~10 Minuten Netto-Einkommen, mit progressivem Level-Floor
        /// (max(level*30, level^2/2)) gegen Post-Prestige-Kollaps.
        /// </summary>
        public static decimal CalculateIncomeBase(int level, decimal netIncomePerSecond)
        {
            var netPerSecond = Math.Max(0m, netIncomePerSecond);
            var levelFloor = Math.Max(level * 30m, (decimal)level * level / 2m);
            return Math.Max(levelFloor, netPerSecond * 600m);
        }

        /// <summary>
        /// Erzeugt eine Daily-Challenge eines Typs mit Zielwert + Belohnungen (Geld/XP/GS) 1:1 zur Vorlage.
        /// <paramref name="highestWorkshopLevel"/> wird nur fuer <see cref="DailyChallengeType.ReachWorkshopLevel"/>
        /// benoetigt, <paramref name="netIncomePerSecond"/> fuer die Einkommens-Basis.
        /// </summary>
        public static DailyChallenge CreateChallenge(DailyChallengeType type, int level, decimal netIncomePerSecond, int highestWorkshopLevel)
        {
            int tier = GetTier(level);
            decimal incomeBase = CalculateIncomeBase(level, netIncomePerSecond);

            var challenge = new DailyChallenge { Type = type };

            switch (type)
            {
                case DailyChallengeType.CompleteOrders:
                    challenge.TargetValue = tier switch { 0 => 2, 1 => 3, 2 => 4, 3 => 5, 4 => 5, 5 => 6, 6 => 7, 7 => 8, _ => 10 };
                    challenge.MoneyReward = Math.Round(incomeBase * 0.8m, 0);
                    challenge.XpReward = 20 + level * 2;
                    break;

                case DailyChallengeType.EarnMoney:
                    challenge.TargetValue = (long)Math.Max(200, incomeBase * 0.5m);
                    challenge.MoneyReward = Math.Round(incomeBase * 0.6m, 0);
                    challenge.XpReward = 15 + level * 2;
                    break;

                case DailyChallengeType.UpgradeWorkshop:
                    challenge.TargetValue = tier switch { 0 => 1, 1 => 2, 2 => 2, 3 => 3, 4 => 3, 5 => 4, 6 => 5, 7 => 6, _ => 8 };
                    challenge.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                    challenge.XpReward = 25 + level * 2;
                    break;

                case DailyChallengeType.HireWorker:
                    challenge.TargetValue = tier >= 6 ? 2 : 1;
                    challenge.MoneyReward = Math.Round(incomeBase * 0.7m, 0);
                    challenge.XpReward = 20 + level * 2;
                    break;

                case DailyChallengeType.CompleteQuickJob:
                    challenge.TargetValue = tier switch { 0 => 1, 1 => 2, 2 => 3, 3 => 4, 4 => 4, 5 => 5, 6 => 6, 7 => 7, _ => 8 };
                    challenge.MoneyReward = Math.Round(incomeBase * 0.5m, 0);
                    challenge.XpReward = 15 + level * 2;
                    break;

                case DailyChallengeType.PlayMiniGames:
                    challenge.TargetValue = tier switch { 0 => 3, 1 => 4, 2 => 5, 3 => 7, 4 => 7, 5 => 8, 6 => 10, 7 => 12, _ => 15 };
                    challenge.MoneyReward = Math.Round(incomeBase * 0.7m, 0);
                    challenge.XpReward = 20 + level * 2;
                    break;

                case DailyChallengeType.AchieveMinigameScore:
                    challenge.TargetValue = tier switch { 0 => 70, 1 => 75, 2 => 80, 3 => 90, _ => 90 };
                    challenge.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                    challenge.XpReward = 25 + level * 2;
                    break;

                case DailyChallengeType.TrainWorker:
                    challenge.TargetValue = tier switch { 5 => 2, 6 => 3, 7 => 4, _ => 5 };
                    challenge.MoneyReward = Math.Round(incomeBase * 0.9m, 0);
                    challenge.XpReward = 25 + level * 2;
                    break;

                case DailyChallengeType.CompleteCrafting:
                    challenge.TargetValue = tier switch { 5 => 1, 6 => 2, 7 => 3, _ => 4 };
                    challenge.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                    challenge.XpReward = 30 + level * 2;
                    break;

                case DailyChallengeType.AchievePerfectStreak:
                    challenge.TargetValue = tier switch { 6 => 3, 7 => 5, _ => 7 };
                    challenge.MoneyReward = Math.Round(incomeBase * 1.2m, 0);
                    challenge.XpReward = 35 + level * 2;
                    break;

                case DailyChallengeType.ReachWorkshopLevel:
                    int increment = tier >= 8 ? 50 : tier >= 7 ? 10 : 10;
                    challenge.TargetValue = highestWorkshopLevel + increment;
                    challenge.MoneyReward = Math.Round(incomeBase * 1.5m, 0);
                    challenge.XpReward = 40 + level * 2;
                    break;

                case DailyChallengeType.ProduceItems:
                    challenge.TargetValue = tier switch { 5 => 20, 6 => 50, 7 => 100, _ => 200 };
                    challenge.MoneyReward = Math.Round(incomeBase * 0.8m, 0);
                    challenge.XpReward = 25 + level * 2;
                    break;

                case DailyChallengeType.SellItems:
                    challenge.TargetValue = tier switch { 5 => 10, 6 => 25, 7 => 50, _ => 100 };
                    challenge.MoneyReward = Math.Round(incomeBase * 0.9m, 0);
                    challenge.XpReward = 25 + level * 2;
                    break;

                case DailyChallengeType.CompleteMaterialOrder:
                    challenge.TargetValue = tier switch { 6 => 1, 7 => 2, _ => 3 };
                    challenge.MoneyReward = Math.Round(incomeBase * 1.3m, 0);
                    challenge.XpReward = 35 + level * 2;
                    break;

                case DailyChallengeType.CollectEquipment:
                    challenge.TargetValue = tier switch { 6 => 1, 7 => 2, _ => 3 };
                    challenge.MoneyReward = Math.Round(incomeBase * 1.0m, 0);
                    challenge.XpReward = 30 + level * 2;
                    break;
            }

            challenge.GoldenScrewReward = tier switch
            {
                <= 4 => Math.Min(1 + tier, 2),
                5 => 3,
                6 => 4,
                7 => 5,
                _ => 6
            };

            return challenge;
        }

        /// <summary>Score-Prozent aus einem MiniGame-Rating: Perfect 100 / Good 75 / Ok 50 / sonst 0.</summary>
        public static int RatingToScorePercent(MiniGameRating rating) => rating switch
        {
            MiniGameRating.Perfect => 100,
            MiniGameRating.Good => 75,
            MiniGameRating.Ok => 50,
            _ => 0
        };
    }
}
