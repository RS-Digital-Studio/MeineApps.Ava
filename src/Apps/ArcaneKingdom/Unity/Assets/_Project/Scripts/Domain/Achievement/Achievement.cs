#nullable enable
using System;
using System.Collections.Generic;

namespace ArcaneKingdom.Domain.Achievement
{
    /// <summary>
    /// Achievement-Definition mit Stufen (Bronze/Silber/Gold/Platin).
    /// Bekommt Trophäen-Punkte für Leistungs-Rangliste (DESIGN.md Kap. 15.4).
    /// </summary>
    [Serializable]
    public sealed class AchievementDefinition
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayNameKey { get; init; } = string.Empty;
        public string DescriptionKey { get; init; } = string.Empty;
        public string IconAddressableKey { get; init; } = string.Empty;
        public List<AchievementTier> Tiers { get; init; } = new();
    }

    [Serializable]
    public sealed class AchievementTier
    {
        public int Tier { get; init; }                  // 1=Bronze, 2=Silber, 3=Gold, 4=Platin
        public int Threshold { get; init; }              // Wertschwelle (z.B. 100 Boss-Siege)
        public int TrophyPoints { get; init; }
        public string? TitleKey { get; init; }           // Optionaler Titel-Unlock (z.B. "Drachen-Toeter")
    }

    [Serializable]
    public sealed class AchievementProgress
    {
        public string AchievementId { get; }
        public int CurrentValue { get; private set; }
        public int HighestTierUnlocked { get; private set; }

        public AchievementProgress(string achievementId, int currentValue = 0, int highestTierUnlocked = 0)
        {
            AchievementId = achievementId;
            CurrentValue = currentValue;
            HighestTierUnlocked = highestTierUnlocked;
        }

        /// <summary>
        /// Erhöht den Wert und gibt die neu freigeschalteten Tiers zurück.
        /// </summary>
        public IReadOnlyList<AchievementTier> Advance(int delta, IReadOnlyList<AchievementTier> tiers)
        {
            if (delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            CurrentValue += delta;

            var newlyUnlocked = new List<AchievementTier>();
            foreach (var t in tiers)
            {
                if (t.Tier <= HighestTierUnlocked) continue;
                if (CurrentValue >= t.Threshold)
                {
                    newlyUnlocked.Add(t);
                    HighestTierUnlocked = t.Tier;
                }
            }
            return newlyUnlocked;
        }
    }
}
