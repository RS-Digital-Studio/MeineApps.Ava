using System.Globalization;
using System.Text.Json;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Tägliche Herausforderung: Jeden Tag ein einzigartiges Level mit globalem Seed.
/// Spieler können einmal pro Tag spielen und versuchen den besten Score zu erreichen.
/// Streak-System belohnt konsistentes tägliches Spielen.
/// </summary>
public sealed class DailyChallengeService : IDailyChallengeService
{
    private const string DAILY_CHALLENGE_KEY = "DailyChallengeData";
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IPreferencesService _preferences;
    private DailyChallengeData _data;

    // Streak-Bonus: Pro aufeinanderfolgenden Tag mehr Coins
    private static readonly int[] StreakBonusCoins = [200, 400, 600, 1000, 1500, 2000, 3000];

    public bool IsCompletedToday => IsTodayCompleted();
    public int TodayBestScore => GetTodayScore();
    public int TotalCompleted => _data.TotalCompleted;
    public int CurrentStreak => _data.CurrentStreak;
    public int LongestStreak => _data.LongestStreak;

    public DailyChallengeService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _data = Load();
        CheckStreakReset();
    }

    public bool SubmitScore(int score)
    {
        if (score <= 0) return false;

        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        bool isNewBest = false;

        // Erster Versuch heute?
        if (_data.LastPlayDate != today)
        {
            // Neuer Tag: Streak aktualisieren
            if (IsConsecutiveDay())
                _data.CurrentStreak++;
            else
                _data.CurrentStreak = 1;

            if (_data.CurrentStreak > _data.LongestStreak)
                _data.LongestStreak = _data.CurrentStreak;

            _data.TotalCompleted++;
            _data.LastPlayDate = today;
            _data.TodayScore = score;
            isNewBest = true;
        }
        else if (score > _data.TodayScore)
        {
            // Besserer Score heute
            _data.TodayScore = score;
            isNewBest = true;
        }

        Save();
        return isNewBest;
    }

    public int GetTodaySeed()
    {
        var today = DateTime.UtcNow.Date;
        return today.Year * 10000 + today.Month * 100 + today.Day;
    }

    public int GetStreakBonus()
    {
        if (_data.CurrentStreak <= 0) return 0;
        int index = Math.Min(_data.CurrentStreak - 1, StreakBonusCoins.Length - 1);
        return StreakBonusCoins[index];
    }

    private bool IsTodayCompleted()
    {
        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        return _data.LastPlayDate == today;
    }

    private int GetTodayScore()
    {
        if (!IsTodayCompleted()) return 0;
        return _data.TodayScore;
    }

    private bool IsConsecutiveDay()
    {
        if (string.IsNullOrEmpty(_data.LastPlayDate)) return false;

        try
        {
            var lastDate = DateTime.ParseExact(_data.LastPlayDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var daysDiff = (DateTime.UtcNow.Date - lastDate).Days;
            return daysDiff == 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Prüft ob die Streak abgebrochen ist (mehr als 1 Tag verpasst)
    /// </summary>
    private void CheckStreakReset()
    {
        if (string.IsNullOrEmpty(_data.LastPlayDate)) return;

        try
        {
            var lastDate = DateTime.ParseExact(_data.LastPlayDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var daysDiff = (DateTime.UtcNow.Date - lastDate).Days;

            // Mehr als 1 Tag verpasst → Streak zurücksetzen
            if (daysDiff > 1)
            {
                _data.CurrentStreak = 0;
                Save();
            }
        }
        catch
        {
            _data.CurrentStreak = 0;
            Save();
        }
    }

    private DailyChallengeData Load()
    {
        try
        {
            string json = _preferences.Get<string>(DAILY_CHALLENGE_KEY, "");
            if (!string.IsNullOrEmpty(json))
                return JsonSerializer.Deserialize<DailyChallengeData>(json, JsonOptions) ?? new DailyChallengeData();
        }
        catch
        {
            // Fehler beim Laden → Standardwerte
        }
        return new DailyChallengeData();
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(DAILY_CHALLENGE_KEY, json);
        }
        catch
        {
            // Speichern fehlgeschlagen
        }
    }

    private class DailyChallengeData
    {
        public string? LastPlayDate { get; set; }
        public int TodayScore { get; set; }
        public int TotalCompleted { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
    }
}
