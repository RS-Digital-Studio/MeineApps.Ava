using System.Globalization;
using System.Text.Json;
using BomberBlast.Models.Entities;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Weekly Boss-Rush mit Wochen-Best-Persistenz (v2.0.41, Plan Task 3.3).
/// JSON in IPreferencesService, Key "BossRushData".
/// </summary>
public sealed class BossRushService : IBossRushService
{
    private const string PREFS_KEY = "BossRushData";

    /// <summary>Fixe Boss-Reihenfolge: leicht → schwer.</summary>
    private static readonly BossType[] _bossSequence =
    [
        BossType.StoneGolem,
        BossType.IceDragon,
        BossType.FireDemon,
        BossType.ShadowMaster,
        BossType.FinalBoss,
    ];

    private readonly IPreferencesService _preferences;
    private BossRushData _data;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public BossRushService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _data = Load();
        EnsureWeekFreshness();
    }

    public IReadOnlyList<BossType> BossSequence => _bossSequence;
    public int WeeklyBestScore => _data.WeeklyBestScore;
    public float WeeklyBestTime => _data.WeeklyBestTime;
    public string LastWeekId => _data.LastWeekId;
    public bool HasRunThisWeek => _data.LastWeekId == CurrentWeekId && _data.WeeklyAttempts > 0;
    public bool HasEverCompleted => _data.TotalCompletions > 0;
    public int TotalCompletions => _data.TotalCompletions;

    public string CurrentWeekId
    {
        get
        {
            var now = DateTime.UtcNow;
            // ISO 8601 Year-Week: yyyy-Www (W01 bis W52/53)
            var calendar = CultureInfo.InvariantCulture.Calendar;
            int week = calendar.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            // Bei Jahres-Grenzen muss das Year ggf. auf das Vorjahr zeigen — vereinfacht: now.Year nehmen,
            // ISO-genauer waere ISOWeek.GetYear/GetWeekOfYear (.NET 5+) — aber unser Reset ist Wochen-genau,
            // ein Off-by-One in der Jahres-Grenze ist akzeptabel weil der Reset-Vergleich auf Equality basiert.
            int isoYear = System.Globalization.ISOWeek.GetYear(now);
            int isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(now);
            return $"{isoYear:D4}-W{isoWeek:D2}";
        }
    }

    public bool SubmitRun(int finalScore, float totalTimeSeconds, bool completedAllBosses)
    {
        EnsureWeekFreshness();

        _data.WeeklyAttempts++;
        if (completedAllBosses)
            _data.TotalCompletions++;

        bool isNewBest = false;
        // Wochen-Best ist score-getrieben (mehr Score = besser), Time als Tiebreaker (weniger Zeit = besser).
        // Beim ersten Run dieser Woche ist alles ein neuer Best.
        if (finalScore > _data.WeeklyBestScore ||
            (finalScore == _data.WeeklyBestScore && totalTimeSeconds < _data.WeeklyBestTime && _data.WeeklyBestTime > 0) ||
            _data.WeeklyBestScore == 0)
        {
            _data.WeeklyBestScore = finalScore;
            _data.WeeklyBestTime = totalTimeSeconds;
            isNewBest = true;
        }

        Save();
        return isNewBest;
    }

    /// <summary>
    /// Wochen-Wechsel-Reset: Wenn die aktuell persistierte Wochen-ID nicht mehr aktuell ist,
    /// werden Best-Score/Time auf 0 zurueckgesetzt. Total-Completions bleiben (lifetime).
    /// </summary>
    private void EnsureWeekFreshness()
    {
        var now = CurrentWeekId;
        if (_data.LastWeekId != now)
        {
            _data.WeeklyBestScore = 0;
            _data.WeeklyBestTime = 0;
            _data.WeeklyAttempts = 0;
            _data.LastWeekId = now;
            Save();
        }
    }

    private BossRushData Load()
    {
        var json = _preferences.Get(PREFS_KEY, "");
        if (string.IsNullOrEmpty(json)) return new BossRushData();
        try
        {
            return JsonSerializer.Deserialize<BossRushData>(json, JsonOptions) ?? new BossRushData();
        }
        catch (Exception ex)
        {
            PersistenceHealth.ReportCorruption(nameof(BossRushService), ex);
            return new BossRushData();
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(PREFS_KEY, json);
        }
        catch
        {
            // Save-Fehler werden beim naechsten Save erneut versucht.
        }
    }

    private class BossRushData
    {
        public int WeeklyBestScore { get; set; }
        public float WeeklyBestTime { get; set; }
        public string LastWeekId { get; set; } = "";
        public int WeeklyAttempts { get; set; }
        public int TotalCompletions { get; set; }
    }
}
