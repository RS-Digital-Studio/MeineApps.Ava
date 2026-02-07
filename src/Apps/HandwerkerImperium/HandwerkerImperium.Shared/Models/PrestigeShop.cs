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
                Icon = "\ud83d\udcb0",
                Cost = 5,
                Effect = new PrestigeEffect { IncomeMultiplier = 0.10m }
            },
            new PrestigeShopItem
            {
                Id = "pp_income_25",
                NameKey = "PrestigeIncome25",
                DescriptionKey = "PrestigeIncome25Desc",
                Icon = "\ud83d\udcb0",
                Cost = 15,
                Effect = new PrestigeEffect { IncomeMultiplier = 0.25m }
            },
            new PrestigeShopItem
            {
                Id = "pp_xp_15",
                NameKey = "PrestigeXp15",
                DescriptionKey = "PrestigeXp15Desc",
                Icon = "\u2b50",
                Cost = 8,
                Effect = new PrestigeEffect { XpMultiplier = 0.15m }
            },
            new PrestigeShopItem
            {
                Id = "pp_xp_30",
                NameKey = "PrestigeXp30",
                DescriptionKey = "PrestigeXp30Desc",
                Icon = "\u2b50",
                Cost = 20,
                Effect = new PrestigeEffect { XpMultiplier = 0.30m }
            },
            new PrestigeShopItem
            {
                Id = "pp_mood_slow",
                NameKey = "PrestigeMoodSlow",
                DescriptionKey = "PrestigeMoodSlowDesc",
                Icon = "\ud83d\ude0a",
                Cost = 10,
                Effect = new PrestigeEffect { MoodDecayReduction = 0.25m }
            },
            new PrestigeShopItem
            {
                Id = "pp_cost_15",
                NameKey = "PrestigeCost15",
                DescriptionKey = "PrestigeCost15Desc",
                Icon = "\ud83d\udcc9",
                Cost = 12,
                Effect = new PrestigeEffect { CostReduction = 0.15m }
            },
            new PrestigeShopItem
            {
                Id = "pp_start_money",
                NameKey = "PrestigeStartMoney",
                DescriptionKey = "PrestigeStartMoneyDesc",
                Icon = "\ud83c\udfe6",
                Cost = 6,
                Effect = new PrestigeEffect { ExtraStartMoney = 5_000m }
            },
            new PrestigeShopItem
            {
                Id = "pp_start_money_big",
                NameKey = "PrestigeStartMoneyBig",
                DescriptionKey = "PrestigeStartMoneyBigDesc",
                Icon = "\ud83c\udfe6",
                Cost = 18,
                Effect = new PrestigeEffect { ExtraStartMoney = 50_000m }
            },
            new PrestigeShopItem
            {
                Id = "pp_better_start_worker",
                NameKey = "PrestigeBetterStartWorker",
                DescriptionKey = "PrestigeBetterStartWorkerDesc",
                Icon = "\ud83d\udc77",
                Cost = 10,
                Effect = new PrestigeEffect { StartingWorkerTier = "D" }
            },
            new PrestigeShopItem
            {
                Id = "pp_income_50",
                NameKey = "PrestigeIncome50",
                DescriptionKey = "PrestigeIncome50Desc",
                Icon = "\ud83d\udc8e",
                Cost = 40,
                Effect = new PrestigeEffect { IncomeMultiplier = 0.50m }
            },
        ];
    }
}
