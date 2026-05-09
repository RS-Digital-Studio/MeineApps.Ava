namespace HandwerkerImperium.Models;

/// <summary>
/// Saison-Storyline (v2.1.0): Pro 6-Wochen-Battle-Pass-Saison gibt es 5 Story-Kapitel,
/// gebunden an BP-Tier 1, 10, 25, 40 und 50. Damit wird die Saison-Reise zur erzaehlerischen
/// Ebene statt nur Tier-Grind.
/// </summary>
public sealed class SeasonStoryline
{
    /// <summary>Saison dieses Storylines (Spring/Summer/Autumn/Winter — siehe <see cref="Season"/>).</summary>
    public Season Theme { get; init; }

    /// <summary>Lokalisierungs-Key fuer den Saison-Titel (z.B. „Spring: City Expansion").</summary>
    public string ThemeKey { get; init; } = "";

    /// <summary>Die 5 Kapitel-IDs (verweisen auf <see cref="StoryChapter.Id"/>).</summary>
    public string[] ChapterIds { get; init; } = new string[5];

    /// <summary>BP-Tier-Trigger pro Kapitel — Default: 1, 10, 25, 40, 50.</summary>
    public int[] TierTriggers { get; init; } = [1, 10, 25, 40, 50];

    /// <summary>
    /// Liefert die Kapitel-ID fuer den uebergebenen Tier — null wenn der Tier kein Trigger ist.
    /// </summary>
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
