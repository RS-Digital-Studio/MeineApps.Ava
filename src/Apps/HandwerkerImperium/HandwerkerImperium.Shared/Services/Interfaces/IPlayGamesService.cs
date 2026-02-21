namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Google Play Games Services Integration (Leaderboard + Cloud Save + Gilden).
/// </summary>
public interface IPlayGamesService
{
    /// <summary>
    /// Ob der Spieler bei Play Games angemeldet ist.
    /// </summary>
    bool IsSignedIn { get; }

    /// <summary>
    /// Ob Cloud Save unterstützt wird (Android + angemeldet).
    /// </summary>
    bool SupportsCloudSave { get; }

    /// <summary>
    /// Spieler-Anzeigename (oder null wenn nicht angemeldet).
    /// </summary>
    string? PlayerDisplayName { get; }

    /// <summary>
    /// Bei Play Games anmelden.
    /// </summary>
    Task<bool> SignInAsync();

    /// <summary>
    /// Score an ein Leaderboard senden.
    /// </summary>
    Task SubmitScoreAsync(string leaderboardId, long score);

    /// <summary>
    /// Leaderboard-UI anzeigen.
    /// </summary>
    Task ShowLeaderboardsAsync();

    /// <summary>
    /// Cloud-Spielstand laden.
    /// </summary>
    Task<string?> LoadCloudSaveAsync();

    /// <summary>
    /// Spielstand in die Cloud speichern.
    /// </summary>
    Task<bool> SaveToCloudAsync(string jsonData, string description);

    /// <summary>
    /// Leaderboard-Einträge laden (für Gilden).
    /// </summary>
    Task<List<PlayGamesLeaderboardEntry>> LoadLeaderboardScoresAsync(string leaderboardId, int maxResults);
}

/// <summary>
/// Eintrag aus einem Play Games Leaderboard.
/// </summary>
public class PlayGamesLeaderboardEntry
{
    public string PlayerName { get; set; } = "";
    public long Score { get; set; }
    public int Rank { get; set; }
}
