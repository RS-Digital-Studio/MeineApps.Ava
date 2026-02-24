namespace HandwerkerImperium.Models;

/// <summary>
/// Ein Story-Kapitel von Meister Hans.
/// Wird bei bestimmtem Fortschritt freigeschaltet.
/// </summary>
public class StoryChapter
{
    public string Id { get; init; } = "";
    public int ChapterNumber { get; init; }

    /// <summary>
    /// Localization-Key für den Kapitel-Titel.
    /// </summary>
    public string TitleKey { get; init; } = "";

    /// <summary>
    /// Localization-Key für den Dialog-Text von Meister Hans.
    /// </summary>
    public string TextKey { get; init; } = "";

    /// <summary>
    /// Fallback-Text wenn Lokalisierung fehlt (Deutsch).
    /// </summary>
    public string TitleFallback { get; init; } = "";
    public string TextFallback { get; init; } = "";

    // Freischalt-Bedingungen (alle gesetzten müssen erfüllt sein)
    public int RequiredPlayerLevel { get; init; }
    public int RequiredWorkshopCount { get; init; }
    public int RequiredTotalOrders { get; init; }
    public int RequiredPrestige { get; init; }

    /// <summary>
    /// Mindest-Prestige-Tier (z.B. 4 = Platin). 0 = keine Anforderung.
    /// Prüft CurrentTier >= RequiredPrestigeTier.
    /// </summary>
    public int RequiredPrestigeTier { get; init; }

    /// <summary>
    /// Mindest-Ascension-Level. 0 = keine Anforderung.
    /// </summary>
    public int RequiredAscensionLevel { get; init; }

    // Belohnungen
    public decimal MoneyReward { get; init; }
    public int GoldenScrewReward { get; init; }
    public int XpReward { get; init; }

    /// <summary>
    /// NPC-Portrait-Stimmung für Meister Hans (happy, proud, concerned, excited).
    /// </summary>
    public string Mood { get; init; } = "happy";

    /// <summary>
    /// True für Tutorial-Kapitel (1-5), die Gameplay-Tipps geben.
    /// </summary>
    public bool IsTutorial { get; init; }
}
