#nullable enable
using System;
using System.Collections.Generic;
using ArcaneKingdom.Domain.Economy;

namespace ArcaneKingdom.Domain.Quest
{
    public enum QuestPeriod
    {
        Daily = 0,
        Weekly = 1,
        Achievement = 2,
        EventBound = 3
    }

    public enum QuestObjectiveType
    {
        WinBattles = 0,
        PlayCardsOfElement = 1,
        DealDamage = 2,
        WinArenaMatches = 3,
        AttackThieves = 4,
        DonateGuildPoints = 5,
        ReachWorldStars = 6,
        BeatBosses = 7,
        LoginDays = 8,
        SpendDiamonds = 9
    }

    /// <summary>
    /// Quest-Definition (Datenstruktur für JSON-Pflege). Vergleich mit Spieler-Fortschritt
    /// über den QuestService.
    /// </summary>
    [Serializable]
    public sealed class QuestDefinition
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayNameKey { get; init; } = string.Empty;
        public string DescriptionKey { get; init; } = string.Empty;
        public QuestPeriod Period { get; init; }
        public QuestObjectiveType Objective { get; init; }
        public int TargetCount { get; init; }
        public string? FilterElement { get; init; }        // z.B. "Feuer" für "Spiele 5 Feuerkarten"
        public List<QuestReward> Rewards { get; init; } = new();
    }

    [Serializable]
    public sealed class QuestReward
    {
        public string Type { get; init; } = "Currency";     // "Currency" | "Scrap" | "Card" | "Rune"
        public string SubType { get; init; } = string.Empty; // Currency-Enum-Name oder ScrapType-Name oder Card/Rune-Id
        public long Amount { get; init; }
    }

    /// <summary>
    /// Spieler-Fortschritt einer Quest.
    /// </summary>
    [Serializable]
    public sealed class QuestProgress
    {
        public string QuestId { get; }
        public int CurrentCount { get; private set; }
        public bool Completed { get; private set; }
        public bool RewardClaimed { get; private set; }
        public DateTime LastUpdateUtc { get; private set; }

        public QuestProgress(string questId, int currentCount = 0)
        {
            QuestId = questId;
            CurrentCount = currentCount;
            LastUpdateUtc = DateTime.UtcNow;
        }

        /// <summary>Rekonstruiert den vollen Fortschritt aus einem persistierten Save (Count + Claimed + Ziel).</summary>
        public QuestProgress(string questId, int currentCount, bool rewardClaimed, int target)
        {
            QuestId = questId;
            CurrentCount = currentCount;
            Completed = currentCount >= target;
            RewardClaimed = rewardClaimed;
            LastUpdateUtc = DateTime.UtcNow;
        }

        public void Advance(int delta, int target)
        {
            if (delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            if (RewardClaimed) return;
            CurrentCount = Math.Min(CurrentCount + delta, target);
            Completed = CurrentCount >= target;
            LastUpdateUtc = DateTime.UtcNow;
        }

        public bool TryClaim()
        {
            if (!Completed || RewardClaimed) return false;
            RewardClaimed = true;
            LastUpdateUtc = DateTime.UtcNow;
            return true;
        }
    }
}
