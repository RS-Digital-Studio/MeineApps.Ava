using System.Text.Json;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Implementation of progress tracking using IPreferencesService
/// </summary>
public class ProgressService : IProgressService
{
    private const string PROGRESS_KEY = "GameProgress";
    private readonly IPreferencesService _preferences;
    private ProgressData _data = new();

    public int TotalLevels => 50;
    public int HighestCompletedLevel => _data.HighestCompleted;

    public ProgressService(IPreferencesService preferences)
    {
        _preferences = preferences;
        LoadProgress();
    }

    // Stern-Anforderungen pro Welt (Index = Welt-Nummer)
    private static readonly int[] WorldStarsRequired = [0, 0, 10, 25, 45, 70];

    public bool IsLevelUnlocked(int level)
    {
        if (level < 1 || level > TotalLevels)
            return false;

        // Erstes Level immer freigeschaltet
        if (level == 1)
            return true;

        // Vorheriges Level muss abgeschlossen sein
        if (level > _data.HighestCompleted + 1)
            return false;

        // Welt-Gating: Genuegend Sterne fuer die Welt erforderlich
        int requiredStars = GetWorldStarsRequired(level);
        if (requiredStars > 0 && GetTotalStars() < requiredStars)
            return false;

        return true;
    }

    public int GetWorldStarsRequired(int level)
    {
        int world = GetWorldForLevel(level);
        return world >= 1 && world < WorldStarsRequired.Length
            ? WorldStarsRequired[world]
            : 0;
    }

    public int GetWorldForLevel(int level)
    {
        if (level < 1) return 1;
        return ((level - 1) / 10) + 1;
    }

    public void CompleteLevel(int level)
    {
        if (level < 1 || level > TotalLevels)
            return;

        if (level > _data.HighestCompleted)
        {
            _data.HighestCompleted = level;
            SaveProgress();
        }
    }

    public int GetLevelBestScore(int level)
    {
        if (_data.LevelScores.TryGetValue(level, out int score))
            return score;
        return 0;
    }

    public void SetLevelBestScore(int level, int score)
    {
        if (level < 1 || level > TotalLevels)
            return;

        if (!_data.LevelScores.ContainsKey(level) || score > _data.LevelScores[level])
        {
            _data.LevelScores[level] = score;
            SaveProgress();
        }
    }

    public int GetTotalStars()
    {
        int total = 0;
        for (int i = 1; i <= TotalLevels; i++)
        {
            total += GetLevelStars(i);
        }
        return total;
    }

    public int GetLevelStars(int level)
    {
        int score = GetLevelBestScore(level);
        if (score == 0)
            return 0;

        // Stern-Schwellwerte: Level-abhaengige baseScore-Skalierung (fairer)
        // Fruehere Level haben niedrigere Schwellwerte, spaetere hoehere
        int world = GetWorldForLevel(level);
        int baseScore = world switch
        {
            1 => 800 + level * 200,   // Welt 1: 1000-2800
            2 => 1500 + level * 300,  // Welt 2: 4800-7500
            3 => 2500 + level * 400,  // Welt 3: 10500-14500
            4 => 4000 + level * 500,  // Welt 4: 19500-24500
            _ => 6000 + level * 600   // Welt 5: 30600-36000
        };

        if (score >= baseScore * 3)
            return 3;
        if (score >= baseScore * 2)
            return 2;
        if (score >= baseScore)
            return 1;

        // Abgeschlossene Level bekommen mindestens 1 Stern
        return 1;
    }

    /// <summary>Hoechstes abgeschlossenes Level zurueckgeben</summary>
    public int GetHighestCompletedLevel() => _data.HighestCompleted;

    public void ResetProgress()
    {
        _data = new ProgressData();
        SaveProgress();
    }

    private void LoadProgress()
    {
        try
        {
            string json = _preferences.Get<string>(PROGRESS_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                _data = JsonSerializer.Deserialize<ProgressData>(json) ?? new ProgressData();
            }
            else
            {
                _data = new ProgressData();
            }
        }
        catch
        {
            _data = new ProgressData();
        }
    }

    private void SaveProgress()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data);
            _preferences.Set<string>(PROGRESS_KEY, json);
        }
        catch (Exception)
        {
            // Save failed silently
        }
    }

    private class ProgressData
    {
        public int HighestCompleted { get; set; }
        public Dictionary<int, int> LevelScores { get; set; } = new();
    }
}
