namespace BomberBlast.Services;

/// <summary>
/// Abstraktion für Google Play Games Services v2.
/// Desktop: NullPlayGamesService, Android: AndroidPlayGamesService.
/// </summary>
public interface IPlayGamesService
{
    /// <summary>Ob der Spieler eingeloggt ist</summary>
    bool IsSignedIn { get; }

    /// <summary>Spielername (null wenn nicht eingeloggt)</summary>
    string? PlayerName { get; }

    /// <summary>Ob GPGS aktiviert ist (Benutzer-Preference)</summary>
    bool IsEnabled { get; set; }

    /// <summary>Wird gefeuert wenn sich der Sign-In-Status ändert</summary>
    event EventHandler<bool>? SignInStatusChanged;

    /// <summary>Auto-Sign-In (GPGS v2 Standard)</summary>
    Task<bool> SignInAsync();

    /// <summary>Score an ein Leaderboard senden</summary>
    Task SubmitScoreAsync(string leaderboardId, long score);

    /// <summary>Alle Leaderboards anzeigen (Android Intent)</summary>
    Task ShowLeaderboardsAsync();

    /// <summary>Alle GPGS-Achievements anzeigen (Android Intent)</summary>
    Task ShowAchievementsAsync();

    /// <summary>Achievement freischalten</summary>
    Task UnlockAchievementAsync(string achievementId);

    /// <summary>Inkrementelles Achievement (z.B. "100 Gegner besiegt")</summary>
    Task IncrementAchievementAsync(string achievementId, int steps);
}
