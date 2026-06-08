#nullable enable
using HandwerkerImperium.Domain.LiveOps;

namespace HandwerkerImperium.Domain.Onboarding
{
    /// <summary>
    /// Saison-Storyline: Pro Battle-Pass-Saison 5 Story-Kapitel, gebunden an BP-Tier 1, 10, 25, 40, 50.
    /// 1:1-Port aus dem Avalonia-Original (Models/SeasonStoryline.cs). init → set (IsExternalInit ist .NET 5+).
    /// Season-Enum ist in LiveOpsEnums.cs (Schicht 10).
    /// </summary>
    public sealed class SeasonStoryline
    {
        /// <summary>Saison dieses Storylines (Spring/Summer/Autumn/Winter).</summary>
        public Season Theme { get; set; }

        /// <summary>Lokalisierungs-Key für den Saison-Titel.</summary>
        public string ThemeKey { get; set; } = "";

        /// <summary>Die 5 Kapitel-IDs (verweisen auf StoryChapter.Id).</summary>
        public string[] ChapterIds { get; set; } = new string[5];

        /// <summary>BP-Tier-Trigger pro Kapitel — Default: 1, 10, 25, 40, 50.</summary>
        public int[] TierTriggers { get; set; } = new[] { 1, 10, 25, 40, 50 };

        /// <summary>Liefert die Kapitel-ID für den übergebenen Tier — null wenn der Tier kein Trigger ist.</summary>
        public string? GetChapterIdForTier(int tier)
        {
            for (int i = 0; i < TierTriggers.Length; i++)
            {
                if (TierTriggers[i] == tier && i < ChapterIds.Length)
                    return ChapterIds[i];
            }
            return null;
        }
    }
}
