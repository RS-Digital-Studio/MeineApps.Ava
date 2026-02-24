namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet globale Leaderboards via Firebase.
/// </summary>
public interface ILeaderboardService
{
    /// <summary>Aktualisiert alle Leaderboard-Scores des Spielers.</summary>
    Task SubmitScoresAsync();

    /// <summary>Lädt die Top-50 eines Leaderboards.</summary>
    Task<List<LeaderboardDisplayEntry>> GetTopEntriesAsync(string boardId);

    /// <summary>Lädt den eigenen Rang auf einem Board.</summary>
    Task<LeaderboardDisplayEntry?> GetOwnEntryAsync(string boardId);

    /// <summary>Aktualisiert das öffentliche Spieler-Profil.</summary>
    Task UpdatePlayerProfileAsync();
}

/// <summary>
/// Anzeige-Eintrag für das UI.
/// </summary>
public class LeaderboardDisplayEntry
{
    public int Rank { get; set; }
    public string Name { get; set; } = "";
    public long Score { get; set; }
    public bool IsCurrentPlayer { get; set; }
}
