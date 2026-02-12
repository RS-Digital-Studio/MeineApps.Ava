using BomberBlast.Models;

namespace BomberBlast.Services;

/// <summary>
/// Service für Achievements/Badges
/// </summary>
public interface IAchievementService
{
    /// <summary>Alle Achievements</summary>
    IReadOnlyList<Achievement> Achievements { get; }

    /// <summary>Anzahl freigeschalteter Achievements</summary>
    int UnlockedCount { get; }

    /// <summary>Gesamtzahl Achievements</summary>
    int TotalCount { get; }

    /// <summary>Level abgeschlossen - prüft Fortschritts-Achievements</summary>
    Achievement? OnLevelCompleted(int level, int score, int stars, int bombsUsed, float timeRemaining, bool noDamage);

    /// <summary>Gegner getötet - prüft Kampf-Achievements</summary>
    Achievement? OnEnemyKilled(int totalKills);

    /// <summary>Arcade Wave erreicht - prüft Arcade-Achievements</summary>
    Achievement? OnArcadeWaveReached(int wave);

    /// <summary>Stern-Fortschritt aktualisieren</summary>
    Achievement? OnStarsUpdated(int totalStars);
}
