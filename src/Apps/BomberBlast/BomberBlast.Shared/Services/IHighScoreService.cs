namespace BomberBlast.Services;

/// <summary>
/// Verwaltet die Bestenliste
/// </summary>
public interface IHighScoreService
{
    /// <summary>Get top scores</summary>
    IReadOnlyList<HighScoreEntry> GetTopScores(int count = 10);

    /// <summary>Add a new score</summary>
    void AddScore(string playerName, int score, int level);

    /// <summary>Check if score qualifies for top 10</summary>
    bool IsHighScore(int score);

    /// <summary>Get the minimum score needed to be in top 10</summary>
    int GetMinHighScore();

    /// <summary>Clear all high scores</summary>
    void ClearScores();
}

/// <summary>
/// High score entry
/// </summary>
public record HighScoreEntry(string PlayerName, int Score, int Level, DateTime Date);
