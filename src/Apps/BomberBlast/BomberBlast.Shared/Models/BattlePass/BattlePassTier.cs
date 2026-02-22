namespace BomberBlast.Models.BattlePass;

/// <summary>
/// Statische Tier-Definitionen für den Battle Pass (30 Tiers).
/// XP-Skalierung: 400 (Tier 1-5), 500 (6-10), 600 (11-15), 700 (16-20), 800 (21-25), 900 (26-30).
/// Total: 19.500 XP für alle 30 Tiers.
/// </summary>
public static class BattlePassTierDefinitions
{
    public const int MaxTier = 30;
    public const int SeasonDurationDays = 30;

    /// <summary>
    /// XP benötigt für ein bestimmtes Tier (1-basiert)
    /// </summary>
    public static int GetXpForTier(int tier) => tier switch
    {
        <= 5 => 400,
        <= 10 => 500,
        <= 15 => 600,
        <= 20 => 700,
        <= 25 => 800,
        _ => 900
    };

    /// <summary>
    /// Kumulative XP bis einschließlich eines Tiers
    /// </summary>
    public static int GetCumulativeXp(int tier)
    {
        int total = 0;
        for (int i = 1; i <= tier; i++)
            total += GetXpForTier(i);
        return total;
    }

    /// <summary>
    /// Alle Free-Track Belohnungen (30 Tiers)
    /// </summary>
    public static BattlePassReward[] GetFreeRewards()
    {
        var rewards = new BattlePassReward[MaxTier];
        for (int i = 0; i < MaxTier; i++)
        {
            int tier = i + 1; // 1-basiert
            rewards[i] = BuildFreeReward(tier);
        }
        return rewards;
    }

    /// <summary>
    /// Alle Premium-Track Belohnungen (30 Tiers)
    /// </summary>
    public static BattlePassReward[] GetPremiumRewards()
    {
        var rewards = new BattlePassReward[MaxTier];
        for (int i = 0; i < MaxTier; i++)
        {
            int tier = i + 1;
            rewards[i] = BuildPremiumReward(tier);
        }
        return rewards;
    }

    private static BattlePassReward BuildFreeReward(int tier)
    {
        // Gem-Tiers: 5,10,15,20,25,30
        if (tier % 5 == 0)
        {
            int gemAmount = tier switch
            {
                5 => 2,
                10 => 3,
                15 => 4,
                20 => 5,
                25 => 6,
                30 => 8,
                _ => 2
            };
            return new BattlePassReward
            {
                Type = BattlePassRewardType.Gems,
                Amount = gemAmount,
                DescriptionKey = $"BPFreeGems{gemAmount}",
                IconName = "DiamondStone"
            };
        }

        // CardPack-Tiers: 3, 8, 13, 18, 23, 28
        if (tier is 3 or 8 or 13 or 18 or 23 or 28)
        {
            return new BattlePassReward
            {
                Type = BattlePassRewardType.CardPack,
                Amount = 1,
                DescriptionKey = "BPCardPack",
                IconName = "Cards"
            };
        }

        // Rest: Coins (skalierend)
        int coins = tier switch
        {
            <= 5 => 300 + (tier - 1) * 100,   // 300-700
            <= 10 => 600 + (tier - 6) * 100,   // 600-1000
            <= 15 => 800 + (tier - 11) * 150,  // 800-1400
            <= 20 => 1200 + (tier - 16) * 200, // 1200-2000
            <= 25 => 1500 + (tier - 21) * 250, // 1500-2500
            _ => 2000 + (tier - 26) * 300       // 2000-3200
        };
        return new BattlePassReward
        {
            Type = BattlePassRewardType.Coins,
            Amount = coins,
            DescriptionKey = $"BPFreeCoins",
            IconName = "CircleMultiple"
        };
    }

    private static BattlePassReward BuildPremiumReward(int tier)
    {
        // Cosmetic-Tiers: 10 (Rare), 20 (Epic), 30 (Legendary)
        if (tier == 10)
            return new BattlePassReward { Type = BattlePassRewardType.Cosmetic, Amount = 1, ItemId = "season_skin_rare", DescriptionKey = "BPPremiumSkinRare", IconName = "Tshirt" };
        if (tier == 20)
            return new BattlePassReward { Type = BattlePassRewardType.Cosmetic, Amount = 1, ItemId = "season_skin_epic", DescriptionKey = "BPPremiumSkinEpic", IconName = "Tshirt" };
        if (tier == 30)
            return new BattlePassReward { Type = BattlePassRewardType.Cosmetic, Amount = 1, ItemId = "season_skin_legendary", DescriptionKey = "BPPremiumSkinLegendary", IconName = "Crown" };

        // Gems: Alle 3 Tiers
        if (tier % 3 == 0)
        {
            int gems = tier switch
            {
                3 => 3,
                6 => 4,
                9 => 5,
                12 => 6,
                15 => 7,
                18 => 8,
                21 => 9,
                24 => 10,
                27 => 12,
                _ => 5
            };
            return new BattlePassReward { Type = BattlePassRewardType.Gems, Amount = gems, DescriptionKey = $"BPPremiumGems", IconName = "DiamondStone" };
        }

        // CardPack: Tier 2, 7, 12, 17, 22, 26
        if (tier is 2 or 7 or 12 or 17 or 22 or 26)
        {
            int packs = tier >= 17 ? 2 : 1;
            return new BattlePassReward { Type = BattlePassRewardType.CardPack, Amount = packs, DescriptionKey = "BPPremiumCardPack", IconName = "Cards" };
        }

        // Rest: Coins (Premium höher)
        int coins = tier switch
        {
            <= 5 => 500 + (tier - 1) * 200,
            <= 10 => 1000 + (tier - 6) * 200,
            <= 15 => 1500 + (tier - 11) * 250,
            <= 20 => 2000 + (tier - 16) * 300,
            <= 25 => 2500 + (tier - 21) * 350,
            _ => 3000 + (tier - 26) * 400
        };
        return new BattlePassReward { Type = BattlePassRewardType.Coins, Amount = coins, DescriptionKey = "BPPremiumCoins", IconName = "CircleMultiple" };
    }
}
