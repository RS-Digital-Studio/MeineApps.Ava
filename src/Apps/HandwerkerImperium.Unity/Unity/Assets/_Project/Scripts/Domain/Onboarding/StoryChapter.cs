#nullable enable
using HandwerkerImperium.Domain.LiveOps;

namespace HandwerkerImperium.Domain.Onboarding
{
    /// <summary>
    /// Ein Story-Kapitel von Meister Hans. Wird bei bestimmtem Fortschritt freigeschaltet.
    /// 1:1-Port aus dem Avalonia-Original (Models/StoryChapter.cs). init → set (IsExternalInit ist .NET 5+).
    /// Season-Enum ist in LiveOpsEnums.cs (Schicht 10). Lokalisierungs-Keys/Fallbacks/Mood sind
    /// Präsentations-Daten; die Freischalt-Bedingungen + Belohnungen sind Gameplay.
    /// </summary>
    public class StoryChapter
    {
        public string Id { get; set; } = "";
        public int ChapterNumber { get; set; }

        public string TitleKey { get; set; } = "";
        public string TextKey { get; set; } = "";
        public string TitleFallback { get; set; } = "";
        public string TextFallback { get; set; } = "";

        // Freischalt-Bedingungen (alle gesetzten müssen erfüllt sein)
        public int RequiredPlayerLevel { get; set; }
        public int RequiredWorkshopCount { get; set; }
        public int RequiredTotalOrders { get; set; }
        public int RequiredPrestige { get; set; }

        /// <summary>Mindestanzahl abgeschlossener QuickJobs. 0 = keine Anforderung.</summary>
        public int RequiredQuickJobsCompleted { get; set; }

        /// <summary>Mindest-Battle-Pass-Tier. 0 = keine Anforderung (Saison-Kapitel binden an Tier 1/10/25/40/50).</summary>
        public int RequiredBattlePassTier { get; set; }

        /// <summary>Erforderliche Saison. null = keine Anforderung.</summary>
        public Season? RequiredSeasonTheme { get; set; }

        /// <summary>Mindest-Prestige-Tier (z.B. 4 = Platin). 0 = keine Anforderung.</summary>
        public int RequiredPrestigeTier { get; set; }

        /// <summary>Mindest-Ascension-Level. 0 = keine Anforderung.</summary>
        public int RequiredAscensionLevel { get; set; }

        // Belohnungen
        public decimal MoneyReward { get; set; }
        public int GoldenScrewReward { get; set; }
        public int XpReward { get; set; }

        /// <summary>NPC-Portrait-Stimmung für Meister Hans (happy, proud, concerned, excited).</summary>
        public string Mood { get; set; } = "happy";

        /// <summary>True für Tutorial-Kapitel (1-5), die Gameplay-Tipps geben.</summary>
        public bool IsTutorial { get; set; }
    }
}
