using System;
using HandwerkerImperium.Domain.Economy;

namespace HandwerkerImperium.Domain.Orders
{
    /// <summary>
    /// Service-Formel-Extrakt aus <c>OrderGeneratorService</c> (Avalonia-Original): die reinen,
    /// balancing-kritischen Auftrags-Formeln (Reward/XP normal + Material, Schwierigkeit, OrderType-
    /// Bestimmung, deterministischer Kundenname, Material-/Template-Counts). 1:1 zur Vorlage.
    ///
    /// Bewusst NICHT extrahiert (state-mutierend / RNG-/Service-gekoppelt, bleiben im Game-Service):
    /// GenerateOrder/GenerateAvailableOrders/RefreshOrders/GenerateLiveOrder/ExpireOldLiveOrders/
    /// GenerateMaterialOrder/TryRollMaterialOffer (Order-Assemblierung, Random-Auswahl, AvailableOrders-
    /// Mutation). RNG-Rolls werden als Parameter uebergeben; Service-/Gilden-Boni ebenso.
    /// </summary>
    public static class OrderFormulas
    {
        /// <summary>Premium/VIP-Auftrag: Reward ×3.</summary>
        public const decimal PremiumRewardMultiplier = 3m;
        /// <summary>Premium/VIP-Auftrag: XP ×2.5.</summary>
        public const double PremiumXpMultiplier = 2.5;

        private static readonly string[] FirstNames =
        {
            "Hans", "Klaus", "Werner", "Petra", "Sabine", "Ingrid", "Thomas", "Michael",
            "Monika", "Helga", "Stefan", "Andreas", "Brigitte", "Ursula", "Frank",
            "Jürgen", "Renate", "Dieter", "Gabriele", "Gerhard", "Manfred", "Erika",
            "Wolfgang", "Heike", "Ralf", "Ulrike", "Heinz", "Karin", "Bernd", "Martina"
        };

        private static readonly string[] LastNames =
        {
            "Müller", "Schmidt", "Schneider", "Fischer", "Weber", "Meyer", "Wagner",
            "Becker", "Schulz", "Hoffmann", "Schäfer", "Koch", "Bauer", "Richter",
            "Klein", "Wolf", "Schröder", "Neumann", "Schwarz", "Zimmermann", "Braun",
            "Krüger", "Hartmann", "Lange", "Schmitt", "Werner", "Krause", "Meier",
            "Lehmann", "Schmid"
        };

        /// <summary>Deterministischer Kundenname aus einem Seed (Vor- + Nachname).</summary>
        public static string GenerateCustomerName(int seed)
        {
            var rng = new Random(seed);
            return $"{FirstNames[rng.Next(FirstNames.Length)]} {LastNames[rng.Next(LastNames.Length)]}";
        }

        /// <summary>
        /// Zentrale Reward+XP-Formel: perTask = max(100 + level*100, netIncome*300),
        /// reward = perTask × taskCount × (1 + (taskCount-1)*0.15) × WorkshopMultiplier × (1+GildeReward),
        /// xp = 25 × workshopLevel × taskCount × (1+GildeXp).
        /// </summary>
        public static (decimal baseReward, int baseXp) ComputeBaseRewardAndXp(
            WorkshopType workshopType, int workshopLevel, int playerLevel, int taskCount,
            decimal netIncomePerSecond, decimal guildRewardBonus, decimal guildXpBonus)
        {
            var net = Math.Max(0m, netIncomePerSecond);
            var perTaskReward = Math.Max(100m + playerLevel * 100m, net * 300m);
            decimal taskMultiplier = taskCount * (1.0m + (taskCount - 1) * 0.15m);
            decimal baseReward = perTaskReward * taskMultiplier * workshopType.GetBaseIncomeMultiplier();
            if (guildRewardBonus > 0)
                baseReward *= (1m + guildRewardBonus);

            int baseXp = 25 * workshopLevel * taskCount;
            if (guildXpBonus > 0)
                baseXp = (int)(baseXp * (1m + guildXpBonus));

            return (baseReward, baseXp);
        }

        /// <summary>
        /// Material-Auftrags-Reward: perItem = max(100 + level*100, netIncome*300),
        /// reward = perItem × (1 + totalItems*0.1) × WorkshopMultiplier × (1+GildeReward),
        /// xp = 25 × workshopLevel × max(1, totalItems/3).
        /// </summary>
        public static (decimal baseReward, int baseXp) ComputeMaterialOrderReward(
            WorkshopType workshopType, int workshopLevel, int playerLevel, int totalItems,
            decimal netIncomePerSecond, decimal guildRewardBonus)
        {
            var net = Math.Max(0m, netIncomePerSecond);
            var perItemReward = Math.Max(100m + playerLevel * 100m, net * 300m);
            decimal baseReward = perItemReward * (1.0m + totalItems * 0.1m) * workshopType.GetBaseIncomeMultiplier();
            if (guildRewardBonus > 0)
                baseReward *= (1m + guildRewardBonus);

            int baseXp = 25 * workshopLevel * Math.Max(1, totalItems / 3);
            return (baseReward, baseXp);
        }

        /// <summary>Material-Auftrag: Anzahl Haupt-Items = 5 + min(level/50, 10).</summary>
        public static int CalculateMaterialOrderMainCount(int playerLevel) => 5 + Math.Min(playerLevel / 50, 10);

        /// <summary>Material-Auftrag (Cross-Workshop ab Lv100): Anzahl Zweit-Items = 3 + min(level/100, 5).</summary>
        public static int CalculateMaterialOrderSecondCount(int playerLevel) => 3 + Math.Min(playerLevel / 100, 5);

        /// <summary>Template-Index-Cap: min(templateCount-1, (workshopLevel-1)/2) — hoehere Level = schwerere Templates.</summary>
        public static int CalculateMaxTemplateIndex(int templateCount, int workshopLevel) =>
            Math.Min(templateCount - 1, (workshopLevel - 1) / 2);

        /// <summary>MaterialOrder-Schwierigkeit nach Workshop-Level: ≤75 Easy, ≤200 Medium, sonst Hard (kein Expert).</summary>
        public static OrderDifficulty GetMaterialOrderDifficulty(int workshopLevel) => workshopLevel switch
        {
            <= 75 => OrderDifficulty.Easy,
            <= 200 => OrderDifficulty.Medium,
            _ => OrderDifficulty.Hard
        };

        /// <summary>
        /// Adjustierter Roll fuer die OrderType-Bestimmung: roll minus (Reputation + Gilde + Research-Premium)×100,
        /// geklemmt auf [0,100]. Senkt die Standard-Wahrscheinlichkeit zugunsten hoeherwertiger Auftraege.
        /// </summary>
        public static int ComputeAdjustedRoll(int roll, decimal reputationBonus, decimal guildOrderQuality, decimal researchPremiumChance) =>
            Math.Clamp((int)(roll - (reputationBonus + guildOrderQuality + researchPremiumChance) * 100), 0, 100);

        /// <summary>
        /// OrderType nach Spieler-Level + freigeschalteten Workshops + <paramref name="adjustedRoll"/>
        /// (aus <see cref="ComputeAdjustedRoll"/>). 1:1 zur Vorlage.
        /// </summary>
        public static OrderType DetermineOrderType(int playerLevel, int unlockedWorkshops, int adjustedRoll) => playerLevel switch
        {
            < 10 => OrderType.Standard,
            < 15 => adjustedRoll < 70 ? OrderType.Standard : OrderType.Large,
            < 20 => unlockedWorkshops >= 2
                ? adjustedRoll < 55 ? OrderType.Standard
                    : adjustedRoll < 80 ? OrderType.Large
                    : OrderType.Cooperation
                : adjustedRoll < 70 ? OrderType.Standard : OrderType.Large,
            _ => unlockedWorkshops >= 2
                ? adjustedRoll < 45 ? OrderType.Standard
                    : adjustedRoll < 70 ? OrderType.Large
                    : adjustedRoll < 85 ? OrderType.Cooperation
                    : OrderType.Weekly
                : adjustedRoll < 55 ? OrderType.Standard
                    : adjustedRoll < 80 ? OrderType.Large
                    : OrderType.Weekly
        };

        /// <summary>
        /// Auftrags-Schwierigkeit nach Workshop-Level × Prestige-Stufe (<paramref name="roll"/> = Random 0-99).
        /// Expert faellt auf Hard zurueck, wenn die Reputation die Anforderung nicht erreicht. 1:1 zur Vorlage.
        /// </summary>
        public static OrderDifficulty GetDifficulty(int workshopLevel, int prestigeCount, int reputation, int roll)
        {
            var result = (workshopLevel, prestigeCount) switch
            {
                (<= 25, 0) => roll < 80 ? OrderDifficulty.Easy : OrderDifficulty.Medium,
                (<= 25, 1) => roll < 65 ? OrderDifficulty.Easy : roll < 90 ? OrderDifficulty.Medium : roll < 100 - 5 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
                (<= 25, 2) => roll < 50 ? OrderDifficulty.Easy : roll < 80 ? OrderDifficulty.Medium : roll < 95 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
                (<= 25, >= 3) => roll < 40 ? OrderDifficulty.Easy : roll < 70 ? OrderDifficulty.Medium : roll < 90 ? OrderDifficulty.Hard : OrderDifficulty.Expert,

                (<= 100, 0) => roll < 45 ? OrderDifficulty.Easy : roll < 90 ? OrderDifficulty.Medium : OrderDifficulty.Hard,
                (<= 100, 1) => roll < 25 ? OrderDifficulty.Easy : roll < 65 ? OrderDifficulty.Medium : roll < 90 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
                (<= 100, 2) => roll < 15 ? OrderDifficulty.Easy : roll < 45 ? OrderDifficulty.Medium : roll < 80 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
                (<= 100, >= 3) => roll < 5 ? OrderDifficulty.Easy : roll < 30 ? OrderDifficulty.Medium : roll < 65 ? OrderDifficulty.Hard : OrderDifficulty.Expert,

                (<= 300, 0) => roll < 15 ? OrderDifficulty.Easy : roll < 60 ? OrderDifficulty.Medium : OrderDifficulty.Hard,
                (<= 300, 1) => roll < 5 ? OrderDifficulty.Easy : roll < 30 ? OrderDifficulty.Medium : roll < 75 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
                (<= 300, 2) => roll < 15 ? OrderDifficulty.Medium : roll < 60 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
                (<= 300, >= 3) => roll < 10 ? OrderDifficulty.Medium : roll < 50 ? OrderDifficulty.Hard : OrderDifficulty.Expert,

                (<= 700, 0) => roll < 5 ? OrderDifficulty.Easy : roll < 35 ? OrderDifficulty.Medium : OrderDifficulty.Hard,
                (<= 700, 1) => roll < 10 ? OrderDifficulty.Medium : roll < 60 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
                (<= 700, 2) => roll < 5 ? OrderDifficulty.Medium : roll < 45 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
                (<= 700, >= 3) => roll < 30 ? OrderDifficulty.Hard : OrderDifficulty.Expert,

                (_, 0) => roll < 20 ? OrderDifficulty.Medium : OrderDifficulty.Hard,
                (_, 1) => roll < 5 ? OrderDifficulty.Medium : roll < 45 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
                (_, 2) => roll < 30 ? OrderDifficulty.Hard : OrderDifficulty.Expert,
                (_, >= 3) => roll < 20 ? OrderDifficulty.Hard : OrderDifficulty.Expert,

                _ => OrderDifficulty.Easy
            };

            if (result == OrderDifficulty.Expert && reputation < OrderDifficulty.Expert.GetRequiredReputation())
                return OrderDifficulty.Hard;

            return result;
        }
    }
}
