#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Achievement;

namespace ArcaneKingdom.Domain.Save
{
    /// <summary>
    /// Persistierter Achievement-State pro Spieler (Schema v2).
    /// </summary>
    [Serializable]
    public sealed class AchievementSaveSlice
    {
        public Dictionary<string, AchievementProgressDto> Progress { get; } = new();
        public HashSet<string> UnlockedTitleKeys { get; } = new();
        public long TotalTrophyPoints { get; set; }
    }

    [Serializable]
    public sealed class AchievementProgressDto
    {
        public int CurrentValue { get; set; }
        public int HighestTierUnlocked { get; set; }
    }
}
