namespace HandwerkerImperium.Models;

/// <summary>
/// Static definition of all prestige shop items.
/// </summary>
public static class PrestigeShop
{
    public static List<PrestigeShopItem> GetAllItems()
    {
        return
        [
            new PrestigeShopItem
            {
                Id = "pp_income_10",
                NameKey = "PrestigeIncome10",
                DescriptionKey = "PrestigeIncome10Desc",
                Icon = "Cash",
                Cost = 5,
                Effect = new PrestigeEffect { IncomeMultiplier = 0.10m }
            },
            new PrestigeShopItem
            {
                Id = "pp_income_25",
                NameKey = "PrestigeIncome25",
                DescriptionKey = "PrestigeIncome25Desc",
                Icon = "Cash",
                Cost = 15,
                Effect = new PrestigeEffect { IncomeMultiplier = 0.25m }
            },
            new PrestigeShopItem
            {
                Id = "pp_xp_15",
                NameKey = "PrestigeXp15",
                DescriptionKey = "PrestigeXp15Desc",
                Icon = "Star",
                Cost = 8,
                Effect = new PrestigeEffect { XpMultiplier = 0.15m }
            },
            new PrestigeShopItem
            {
                Id = "pp_xp_30",
                NameKey = "PrestigeXp30",
                DescriptionKey = "PrestigeXp30Desc",
                Icon = "Star",
                Cost = 20,
                Effect = new PrestigeEffect { XpMultiplier = 0.30m }
            },
            new PrestigeShopItem
            {
                Id = "pp_mood_slow",
                NameKey = "PrestigeMoodSlow",
                DescriptionKey = "PrestigeMoodSlowDesc",
                Icon = "EmoticonHappy",
                Cost = 10,
                Effect = new PrestigeEffect { MoodDecayReduction = 0.25m }
            },
            new PrestigeShopItem
            {
                Id = "pp_cost_15",
                NameKey = "PrestigeCost15",
                DescriptionKey = "PrestigeCost15Desc",
                Icon = "TrendingDown",
                Cost = 12,
                Effect = new PrestigeEffect { CostReduction = 0.15m }
            },
            new PrestigeShopItem
            {
                Id = "pp_start_money",
                NameKey = "PrestigeStartMoney",
                DescriptionKey = "PrestigeStartMoneyDesc",
                Icon = "Bank",
                Cost = 6,
                Effect = new PrestigeEffect { ExtraStartMoney = 5_000m }
            },
            new PrestigeShopItem
            {
                Id = "pp_start_money_big",
                NameKey = "PrestigeStartMoneyBig",
                DescriptionKey = "PrestigeStartMoneyBigDesc",
                Icon = "Bank",
                Cost = 18,
                Effect = new PrestigeEffect { ExtraStartMoney = 50_000m }
            },
            new PrestigeShopItem
            {
                Id = "pp_better_start_worker",
                NameKey = "PrestigeBetterStartWorker",
                DescriptionKey = "PrestigeBetterStartWorkerDesc",
                Icon = "HardHat",
                Cost = 10,
                Effect = new PrestigeEffect { StartingWorkerTier = "D" }
            },
            new PrestigeShopItem
            {
                Id = "pp_income_50",
                NameKey = "PrestigeIncome50",
                DescriptionKey = "PrestigeIncome50Desc",
                Icon = "DiamondStone",
                Cost = 40,
                Effect = new PrestigeEffect { IncomeMultiplier = 0.50m }
            },

            // Neue Items: Rush, Lieferant, Goldschrauben
            new PrestigeShopItem
            {
                Id = "pp_rush_boost",
                NameKey = "PrestigeRushBoost",
                DescriptionKey = "PrestigeRushBoostDesc",
                Icon = "LightningBolt",
                Cost = 15,
                Effect = new PrestigeEffect { RushMultiplierBonus = 0.50m }
            },
            new PrestigeShopItem
            {
                Id = "pp_delivery_speed",
                NameKey = "PrestigeDeliverySpeed",
                DescriptionKey = "PrestigeDeliverySpeedDesc",
                Icon = "TruckDelivery",
                Cost = 12,
                Effect = new PrestigeEffect { DeliverySpeedBonus = 0.30m }
            },
            new PrestigeShopItem
            {
                Id = "pp_golden_screw_25",
                NameKey = "PrestigeGoldenScrew25",
                DescriptionKey = "PrestigeGoldenScrew25Desc",
                Icon = "Screwdriver",
                Cost = 25,
                Effect = new PrestigeEffect { GoldenScrewBonus = 0.25m }
            },

            // === Endgame-Items (höhere Kosten) ===
            new PrestigeShopItem
            {
                Id = "pp_income_100",
                NameKey = "PrestigeIncome100",
                DescriptionKey = "PrestigeIncome100Desc",
                Icon = "CashMultiple",
                Cost = 80,
                Effect = new PrestigeEffect { IncomeMultiplier = 1.00m }
            },
            new PrestigeShopItem
            {
                Id = "pp_cost_30",
                NameKey = "PrestigeCost30",
                DescriptionKey = "PrestigeCost30Desc",
                Icon = "TrendingDown",
                Cost = 30,
                Effect = new PrestigeEffect { CostReduction = 0.30m }
            },
            new PrestigeShopItem
            {
                Id = "pp_mood_immunity",
                NameKey = "PrestigeMoodImmunity",
                DescriptionKey = "PrestigeMoodImmunityDesc",
                Icon = "ShieldHalfFull",
                Cost = 25,
                Effect = new PrestigeEffect { MoodDecayReduction = 0.50m }
            },
            new PrestigeShopItem
            {
                Id = "pp_offline_hours",
                NameKey = "PrestigeOfflineHours",
                DescriptionKey = "PrestigeOfflineHoursDesc",
                Icon = "ClockPlus",
                Cost = 35,
                Effect = new PrestigeEffect { OfflineHoursBonus = 4 }
            },
            new PrestigeShopItem
            {
                Id = "pp_quickjob_limit",
                NameKey = "PrestigeQuickJobLimit",
                DescriptionKey = "PrestigeQuickJobLimitDesc",
                Icon = "LightningBoltCircle",
                Cost = 22,
                Effect = new PrestigeEffect { ExtraQuickJobLimit = 10 }
            },
            new PrestigeShopItem
            {
                Id = "pp_start_worker_b",
                NameKey = "PrestigeStartWorkerB",
                DescriptionKey = "PrestigeStartWorkerBDesc",
                Icon = "AccountStar",
                Cost = 30,
                Effect = new PrestigeEffect { StartingWorkerTier = "B" }
            },
            new PrestigeShopItem
            {
                Id = "pp_crafting_speed",
                NameKey = "PrestigeCraftingSpeed",
                DescriptionKey = "PrestigeCraftingSpeedDesc",
                Icon = "Hammer",
                Cost = 18,
                Effect = new PrestigeEffect { CraftingSpeedBonus = 0.25m }
            },
        ];
    }
}
