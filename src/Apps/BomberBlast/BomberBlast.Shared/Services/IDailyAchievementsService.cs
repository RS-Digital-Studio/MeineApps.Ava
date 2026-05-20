using System.Globalization;
using System.Text.Json;
using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// v2.0.60 (B-D13): Daily-Achievements — neue Sub-Kategorie der Achievements (3-5 Items,
/// täglich Reset, 200 Coins pro Item). Im Gegensatz zu permanenten Achievements
/// (66 Items in <see cref="IAchievementService"/>) bietet diese Sub-Kategorie einen D1-Hook
/// jeden Tag — Spieler bekommt täglich erreichbare Mini-Ziele.
///
/// <para>Beispiele: "Besiege heute 3 Gegner", "Sammle 1 PowerUp", "Spiele 1 Level".
/// Reset um Mitternacht lokal. Deterministische Auswahl aus Pool basierend auf Tages-ID.</para>
/// </summary>
public interface IDailyAchievementsService
{
    /// <summary>Aktuelle Daily-Achievements für heute (3-5 Items).</summary>
    IReadOnlyList<DailyAchievement> TodaysAchievements { get; }

    /// <summary>Aktualisiert den Fortschritt eines Achievements vom angegebenen Typ.</summary>
    void TrackProgress(DailyAchievementType type, int amount = 1);

    /// <summary>Beansprucht die Coin-Belohnung wenn das Achievement abgeschlossen ist.</summary>
    bool TryClaim(DailyAchievementType type);

    /// <summary>Anzahl heute abgeschlossener Daily-Achievements.</summary>
    int CompletedCountToday { get; }

    /// <summary>Wird gefeuert wenn ein Daily-Achievement abgeschlossen wird (für Toast/Sound).</summary>
    event Action<DailyAchievement>? AchievementCompleted;
}

/// <summary>
/// v2.0.60 (B-D13): Typen der Daily-Achievements.
/// </summary>
public enum DailyAchievementType
{
    PlayOneLevel,
    DefeatThreeEnemies,
    CollectOnePowerUp,
    EarnFiveHundredCoins,
    AchieveCombo,
}

/// <summary>v2.0.60 (B-D13): Daily-Achievement-Modell.</summary>
public sealed class DailyAchievement
{
    public DailyAchievementType Type { get; init; }
    public string NameKey { get; init; } = "";
    public string DescriptionKey { get; init; } = "";
    public int TargetCount { get; init; } = 1;
    public int CurrentCount { get; set; }
    public int CoinReward { get; init; } = 200;
    public bool IsCompleted => CurrentCount >= TargetCount;
    public bool IsClaimed { get; set; }
}

/// <summary>
/// Default-Implementation. Persistiert via Preferences-Key "DailyAchievementsData".
/// Tagesreset bei lokalem Mitternacht (konsistent mit DailyRewardService nach B-C14).
/// </summary>
public sealed class DailyAchievementsService : IDailyAchievementsService
{
    private const string DataKey = "DailyAchievementsData";
    private const int AchievementsPerDay = 3;

    private static readonly JsonSerializerOptions JsonOptions = new();

    private static readonly (DailyAchievementType Type, string Name, string Desc, int Min, int Max, int Reward)[] Pool =
    [
        (DailyAchievementType.PlayOneLevel, "DailyAchPlayLevel", "DailyAchPlayLevelDesc", 1, 1, 200),
        (DailyAchievementType.DefeatThreeEnemies, "DailyAchDefeatEnemies", "DailyAchDefeatEnemiesDesc", 3, 5, 200),
        (DailyAchievementType.CollectOnePowerUp, "DailyAchCollectPowerUp", "DailyAchCollectPowerUpDesc", 1, 2, 200),
        (DailyAchievementType.EarnFiveHundredCoins, "DailyAchEarnCoins", "DailyAchEarnCoinsDesc", 500, 1000, 200),
        (DailyAchievementType.AchieveCombo, "DailyAchCombo", "DailyAchComboDesc", 1, 2, 200),
    ];

    private readonly IPreferencesService _preferences;
    private readonly ICoinService _coinService;
    private PersistenceData _data;

    public event Action<DailyAchievement>? AchievementCompleted;

    public DailyAchievementsService(IPreferencesService preferences, ICoinService coinService)
    {
        _preferences = preferences;
        _coinService = coinService;
        _data = Load();
        EnsureTodayAchievements();
    }

    public IReadOnlyList<DailyAchievement> TodaysAchievements => _data.Achievements;
    public int CompletedCountToday => _data.Achievements.Count(a => a.IsCompleted);

    public void TrackProgress(DailyAchievementType type, int amount = 1)
    {
        EnsureTodayAchievements();
        var ach = _data.Achievements.FirstOrDefault(a => a.Type == type);
        if (ach == null || ach.IsCompleted) return;

        ach.CurrentCount += amount;
        if (ach.IsCompleted)
        {
            AchievementCompleted?.Invoke(ach);
        }
        Save();
    }

    public bool TryClaim(DailyAchievementType type)
    {
        var ach = _data.Achievements.FirstOrDefault(a => a.Type == type);
        if (ach == null || !ach.IsCompleted || ach.IsClaimed) return false;

        _coinService.AddCoins(ach.CoinReward);
        ach.IsClaimed = true;
        Save();
        return true;
    }

    private void EnsureTodayAchievements()
    {
        // v2.0.60: Tagesreset auf lokale Mitternacht (konsistent mit DailyRewardService B-C14).
        int todayId = GetTodayId();
        if (_data.TodayId == todayId && _data.Achievements.Count > 0) return;

        // Neuer Tag → deterministische Auswahl aus Pool.
        var rng = new Random(todayId);
        var indices = Enumerable.Range(0, Pool.Length).ToList();
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        var newAchievements = new List<DailyAchievement>(AchievementsPerDay);
        for (int i = 0; i < AchievementsPerDay && i < indices.Count; i++)
        {
            var template = Pool[indices[i]];
            int target = template.Min + rng.Next(template.Max - template.Min + 1);
            newAchievements.Add(new DailyAchievement
            {
                Type = template.Type,
                NameKey = template.Name,
                DescriptionKey = template.Desc,
                TargetCount = target,
                CoinReward = template.Reward,
            });
        }

        _data.TodayId = todayId;
        _data.Achievements = newAchievements;
        Save();
    }

    private static int GetTodayId()
    {
        var today = DateTime.Now.Date;
        return today.Year * 10000 + today.Month * 100 + today.Day;
    }

    private PersistenceData Load()
    {
        var json = _preferences.Get<string>(DataKey, "");
        if (string.IsNullOrEmpty(json)) return new PersistenceData();
        try
        {
            return JsonSerializer.Deserialize<PersistenceData>(json, JsonOptions) ?? new PersistenceData();
        }
        catch (Exception ex)
        {
            PersistenceHealth.ReportCorruption(nameof(DailyAchievementsService), ex);
            return new PersistenceData();
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(DataKey, json);
        }
        catch
        {
            // Best-effort persistence.
        }
    }

    private sealed class PersistenceData
    {
        public int TodayId { get; set; }
        public List<DailyAchievement> Achievements { get; set; } = new();
    }
}
