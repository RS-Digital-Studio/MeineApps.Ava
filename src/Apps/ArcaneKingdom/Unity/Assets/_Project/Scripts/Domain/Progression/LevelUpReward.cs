#nullable enable
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.Progression
{
    /// <summary>
    /// Spieler-Level-Up-Belohnungen (DESIGN.md Kap. 4.3).
    /// Beim Erreichen eines Schwellen-Levels werden die definierten Items vergeben +
    /// ggf. Features (Arena, Gildenbeitritt, neue Runen-Slots) freigeschaltet.
    /// </summary>
    public sealed class LevelUpReward
    {
        public int Level { get; init; }
        public long Gold { get; init; }
        public long Diamonds { get; init; }
        public int CommonPacks { get; init; }
        public int RarePacks { get; init; }
        public int EpicPacks { get; init; }
        public string? UnlockedFeatureKey { get; init; }       // z.B. "feature.arena", "feature.guild"
        public int? RuneSlotUnlocked { get; init; }            // 2, 3, 4
        public string? AvatarFrameKey { get; init; }
    }

    /// <summary>
    /// Statische Level-Up-Belohnungs-Tabelle (Pilot aus DESIGN 4.3).
    /// </summary>
    public static class LevelUpRewardTable
    {
        public static readonly IReadOnlyDictionary<int, LevelUpReward> Rewards = new Dictionary<int, LevelUpReward>
        {
            [5]   = new() { Level = 5,   CommonPacks = 1, Diamonds = 100 },
            [10]  = new() { Level = 10,  CommonPacks = 1, Diamonds = 50,  UnlockedFeatureKey = "feature.deck_save" },
            [15]  = new() { Level = 15,  Diamonds = 100, UnlockedFeatureKey = "feature.arena" },
            [20]  = new() { Level = 20,  RarePacks = 1, RuneSlotUnlocked = 2 },
            [25]  = new() { Level = 25,  UnlockedFeatureKey = "feature.guild" },
            [30]  = new() { Level = 30,  RarePacks = 1, RuneSlotUnlocked = 3 },
            [40]  = new() { Level = 40,  EpicPacks = 1, Diamonds = 200, RuneSlotUnlocked = 4 },
            [50]  = new() { Level = 50,  UnlockedFeatureKey = "feature.klan_match", Diamonds = 300 },
            [60]  = new() { Level = 60,  EpicPacks = 2, UnlockedFeatureKey = "feature.world_5" },
            [80]  = new() { Level = 80,  EpicPacks = 1, UnlockedFeatureKey = "feature.world_7", Diamonds = 500 },
            [100] = new() { Level = 100, AvatarFrameKey = "avatar.frame.legend" }
        };

        public static bool TryGet(int level, out LevelUpReward reward)
        {
            if (Rewards.TryGetValue(level, out var r)) { reward = r; return true; }
            reward = default!;
            return false;
        }
    }
}
