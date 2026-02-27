using System.Globalization;
using System.Text.Json;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Implementation of high score management
/// </summary>
public class HighScoreService : IHighScoreService
{
    private const string SCORES_KEY = "HighScores";
    private const int MAX_SCORES = 10;
    private readonly IPreferencesService _preferences;
    private List<HighScoreEntry> _scores = [];

    public HighScoreService(IPreferencesService preferences)
    {
        _preferences = preferences;
        LoadScores();
    }

    public IReadOnlyList<HighScoreEntry> GetTopScores(int count = 10)
    {
        // _scores ist bereits sortiert + auf MAX_SCORES begrenzt → kein LINQ nötig
        if (count >= _scores.Count)
            return _scores.AsReadOnly();
        return _scores.GetRange(0, count).AsReadOnly();
    }

    public void AddScore(string playerName, int score, int level)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "PLAYER";

        var entry = new HighScoreEntry(
            playerName.ToUpper().Substring(0, Math.Min(10, playerName.Length)),
            score,
            level,
            DateTime.UtcNow);

        _scores.Add(entry);
        _scores = _scores.OrderByDescending(s => s.Score).Take(MAX_SCORES).ToList();
        SaveScores();
    }

    public bool IsHighScore(int score)
    {
        if (_scores.Count < MAX_SCORES)
            return true;

        return score > _scores.Last().Score;
    }

    public int GetMinHighScore()
    {
        if (_scores.Count < MAX_SCORES)
            return 0;

        return _scores.Last().Score;
    }

    public void ClearScores()
    {
        _scores.Clear();
        SaveScores();
    }

    private void LoadScores()
    {
        try
        {
            string json = _preferences.Get<string>(SCORES_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonSerializer.Deserialize<List<ScoreData>>(json);
                _scores = data?.Select(d => new HighScoreEntry(
                    d.Name, d.Score, d.Level,
                    ParseDateSafe(d.DateUtc))).ToList()
                    ?? new List<HighScoreEntry>();
            }
            else
            {
                _scores = new List<HighScoreEntry>();
            }
        }
        catch
        {
            _scores = new List<HighScoreEntry>();
        }
    }

    private void SaveScores()
    {
        try
        {
            var data = _scores.Select(s => new ScoreData
            {
                Name = s.PlayerName,
                Score = s.Score,
                Level = s.Level,
                // ISO 8601 "O" Format mit UTC-Kind beibehalten
                DateUtc = s.Date.ToString("O")
            }).ToList();

            string json = JsonSerializer.Serialize(data);
            _preferences.Set<string>(SCORES_KEY, json);
        }
        catch (Exception)
        {
            // Speichern fehlgeschlagen - wird beim nächsten Mal erneut versucht
        }
    }

    /// <summary>
    /// Parst einen ISO 8601 UTC-String mit RoundtripKind (Projekt-Convention).
    /// Fallback auf DateTime.UtcNow bei ungültigem Format.
    /// </summary>
    private static DateTime ParseDateSafe(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
            return DateTime.UtcNow;

        // RoundtripKind bewahrt DateTimeKind.Utc aus dem "O"-Format
        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
            return result;

        return DateTime.UtcNow;
    }

    /// <summary>
    /// Serialisierungs-DTO für HighScore-Einträge.
    /// DateUtc als String im ISO 8601 "O" Format (Projekt-Convention).
    /// Abwärtskompatibel: Akzeptiert auch altes "Date"-Property via ParseDateSafe().
    /// </summary>
    private class ScoreData
    {
        public string Name { get; set; } = "";
        public int Score { get; set; }
        public int Level { get; set; }
        /// <summary>ISO 8601 "O" UTC-String (neues Format)</summary>
        public string? DateUtc { get; set; }
        /// <summary>Altes DateTime-Property für Abwärtskompatibilität beim Deserialisieren</summary>
        public DateTime? Date { set => DateUtc ??= value?.ToString("O"); }
    }
}
