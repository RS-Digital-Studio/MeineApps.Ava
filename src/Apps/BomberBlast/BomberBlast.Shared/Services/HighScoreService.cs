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
        return _scores.Take(count).ToList().AsReadOnly();
    }

    public void AddScore(string playerName, int score, int level)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "PLAYER";

        var entry = new HighScoreEntry(
            playerName.ToUpper().Substring(0, Math.Min(10, playerName.Length)),
            score,
            level,
            DateTime.Now);

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
                _scores = data?.Select(d => new HighScoreEntry(d.Name, d.Score, d.Level, d.Date)).ToList()
                    ?? new List<HighScoreEntry>();
            }
            else
            {
                _scores = new List<HighScoreEntry>();
                AddDefaultScores();
            }
        }
        catch
        {
            _scores = new List<HighScoreEntry>();
            AddDefaultScores();
        }
    }

    private void AddDefaultScores()
    {
        // Add some default scores for players to beat
        var defaults = new[]
        {
            ("AAA", 50000, 10),
            ("BBB", 40000, 8),
            ("CCC", 30000, 6),
            ("DDD", 20000, 5),
            ("EEE", 10000, 3)
        };

        foreach (var (name, score, level) in defaults)
        {
            _scores.Add(new HighScoreEntry(name, score, level, DateTime.Now.AddDays(-1)));
        }

        _scores = _scores.OrderByDescending(s => s.Score).ToList();
        SaveScores();
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
                Date = s.Date
            }).ToList();

            string json = JsonSerializer.Serialize(data);
            _preferences.Set<string>(SCORES_KEY, json);
        }
        catch (Exception)
        {
            // Save failed silently
        }
    }

    private class ScoreData
    {
        public string Name { get; set; } = "";
        public int Score { get; set; }
        public int Level { get; set; }
        public DateTime Date { get; set; }
    }
}
