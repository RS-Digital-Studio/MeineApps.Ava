using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet Gilden-Achievements (30 Stück, 3 Tiers, Belohnungen).
/// Fortschritt wird automatisch bei relevanten Aktionen aktualisiert.
/// </summary>
public interface IGuildAchievementService
{
    /// <summary>Lädt alle Gilden-Achievements mit aktuellem Fortschritt.</summary>
    Task<List<GuildAchievementDisplay>> GetAchievementsAsync();

    /// <summary>Aktualisiert den Fortschritt eines bestimmten Achievements.</summary>
    Task UpdateProgressAsync(string achievementId, long progress);

    /// <summary>Prüft alle Achievements auf Abschluss und verteilt Belohnungen.</summary>
    Task CheckAllAchievementsAsync();

    /// <summary>Feuert wenn ein Achievement abgeschlossen wurde (für UI-Celebration).</summary>
    event Action<GuildAchievementDisplay>? AchievementCompleted;
}
