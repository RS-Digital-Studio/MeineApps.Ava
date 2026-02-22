using System.Text.Json;
using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Tägliche Missionen: 3 Missionen pro Tag, Reset Mitternacht UTC.
/// Missionen werden deterministisch aus dem Datum generiert (gleicher Tag = gleiche Missionen).
/// Bonus-Belohnung wenn alle 3 abgeschlossen.
/// </summary>
public class DailyMissionService : IDailyMissionService
{
    private const string DAILY_KEY = "DailyMissionData";
    private const int MISSIONS_PER_DAY = 3;
    private const int ALL_COMPLETE_BONUS = 500;

    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IPreferencesService _preferences;
    private readonly IBattlePassService _battlePassService;
    private readonly ILeagueService _leagueService;
    private IAchievementService? _achievementService;
    private DailyMissionData _data;
    private List<WeeklyMission> _missions;

    public List<WeeklyMission> Missions => _missions;
    public bool IsAllComplete => _missions.Count > 0 && _missions.All(m => m.IsCompleted);
    public int CompletedCount => _missions.Count(m => m.IsCompleted);
    public int AllCompleteBonusCoins => ALL_COMPLETE_BONUS;
    public bool IsBonusClaimed => _data.BonusClaimed;
    public int TotalDaysCompleted => _data.TotalDaysCompleted;

    /// <summary>Lazy-Injection um zirkuläre DI-Abhängigkeit zu vermeiden</summary>
    public void SetAchievementService(IAchievementService achievementService) => _achievementService = achievementService;

    public DateTime NextResetDate
    {
        get
        {
            var now = DateTime.UtcNow;
            // Nächster Tag 00:00 UTC
            return now.Date.AddDays(1);
        }
    }

    // Missions-Pool: Typ -> (NameKey, DescKey, MinTarget, MaxTarget, CoinReward)
    private static readonly (WeeklyMissionType Type, string Name, string Desc, int Min, int Max, int Reward)[] MissionPool =
    [
        (WeeklyMissionType.CompleteLevels, "DailyCompleteLevels", "DailyCompleteLevelsDesc", 1, 3, 200),
        (WeeklyMissionType.DefeatEnemies, "DailyDefeatEnemies", "DailyDefeatEnemiesDesc", 5, 15, 150),
        (WeeklyMissionType.CollectPowerUps, "DailyCollectPowerUps", "DailyCollectPowerUpsDesc", 3, 10, 100),
        (WeeklyMissionType.EarnCoins, "DailyEarnCoins", "DailyEarnCoinsDesc", 300, 1500, 250),
        (WeeklyMissionType.SurvivalKills, "DailySurvivalKills", "DailySurvivalKillsDesc", 5, 15, 200),
        (WeeklyMissionType.UseSpecialBombs, "DailyUseSpecialBombs", "DailyUseSpecialBombsDesc", 2, 5, 150),
        (WeeklyMissionType.AchieveCombo, "DailyAchieveCombo", "DailyAchieveComboDesc", 1, 3, 150),
        (WeeklyMissionType.WinBossFights, "DailyWinBossFights", "DailyWinBossFightsDesc", 1, 1, 300),

        // Phase 9.4: Neue Feature-Expansion Missionstypen
        (WeeklyMissionType.CompleteDungeonFloors, "DailyDungeonFloors", "DailyDungeonFloorsDesc", 1, 3, 200),
        (WeeklyMissionType.CollectCards, "DailyCollectCards", "DailyCollectCardsDesc", 1, 2, 150),
        (WeeklyMissionType.EarnGems, "DailyEarnGems", "DailyEarnGemsDesc", 3, 10, 200),
        (WeeklyMissionType.PlayQuickPlay, "DailyPlayQuickPlay", "DailyPlayQuickPlayDesc", 1, 2, 150),
        (WeeklyMissionType.SpinLuckyWheel, "DailySpinWheel", "DailySpinWheelDesc", 1, 1, 100),
        (WeeklyMissionType.UpgradeCards, "DailyUpgradeCards", "DailyUpgradeCardsDesc", 1, 1, 250),
    ];

    public DailyMissionService(IPreferencesService preferences, IBattlePassService battlePassService, ILeagueService leagueService)
    {
        _preferences = preferences;
        _battlePassService = battlePassService;
        _leagueService = leagueService;
        _data = Load();
        _missions = [];
        CheckDayReset();
    }

    public void TrackProgress(WeeklyMissionType type, int amount = 1)
    {
        if (amount <= 0) return;

        bool anyCompleted = false;
        foreach (var mission in _missions)
        {
            if (mission.Type == type && !mission.IsCompleted)
            {
                mission.CurrentCount = Math.Min(mission.CurrentCount + amount, mission.TargetCount);
                if (mission.IsCompleted)
                    anyCompleted = true;
            }
        }

        if (anyCompleted || amount > 0)
        {
            // Fortschritt in Data synchronisieren
            SyncMissionsToData();
            Save();
        }

        // Battle Pass XP + Achievement bei jeder abgeschlossenen Daily Mission
        if (anyCompleted)
        {
            _battlePassService.AddXp(BattlePassXpSources.DailyMission, "daily_mission");
            _achievementService?.OnDailyMissionCompleted();
        }
    }

    public int ClaimAllCompleteBonus()
    {
        if (!IsAllComplete || _data.BonusClaimed) return 0;

        _data.BonusClaimed = true;
        _data.TotalDaysCompleted++;
        Save();

        // Alle 3 Daily Missions erledigt → Bonus-XP + Liga-Punkte
        _battlePassService.AddXp(BattlePassXpSources.DailyMissionBonus, "daily_all_complete");
        _leagueService.AddPoints(15);

        return ALL_COMPLETE_BONUS;
    }

    /// <summary>
    /// Prüft ob ein neuer Tag angefangen hat und generiert ggf. neue Missionen
    /// </summary>
    private void CheckDayReset()
    {
        var currentDayId = GetDayId(DateTime.UtcNow);

        if (_data.DayId != currentDayId)
        {
            // Neuer Tag -> neue Missionen generieren
            _data.DayId = currentDayId;
            _data.BonusClaimed = false;
            _data.MissionProgress = [];
            GenerateMissions(currentDayId);
            SyncMissionsToData();
            Save();
        }
        else
        {
            // Bestehende Missionen aus gespeicherten Daten wiederherstellen
            RestoreMissionsFromData(currentDayId);
        }
    }

    /// <summary>
    /// Tag-ID aus Datum (Jahr * 10000 + Monat * 100 + Tag)
    /// </summary>
    private static int GetDayId(DateTime date)
    {
        return date.Year * 10000 + date.Month * 100 + date.Day;
    }

    /// <summary>
    /// 3 Missionen deterministisch aus der Tag-ID generieren (keine Duplikate)
    /// </summary>
    private void GenerateMissions(int dayId)
    {
        var rng = new Random(dayId);
        _missions = [];

        // Pool mischen und erste 3 nehmen
        var indices = Enumerable.Range(0, MissionPool.Length).ToList();
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        for (int i = 0; i < MISSIONS_PER_DAY && i < indices.Count; i++)
        {
            var template = MissionPool[indices[i]];
            // Target zufällig zwischen Min und Max
            int target = template.Min + rng.Next(template.Max - template.Min + 1);
            // Auf "schöne" Zahlen runden (5er-Schritte nur für größere Targets)
            if (target >= 10)
                target = (target / 5) * 5;

            _missions.Add(new WeeklyMission
            {
                Type = template.Type,
                NameKey = template.Name,
                DescriptionKey = template.Desc,
                TargetCount = Math.Max(target, template.Min),
                CurrentCount = 0,
                CoinReward = template.Reward,
            });
        }
    }

    /// <summary>
    /// Missionen aus gespeicherten Daten wiederherstellen
    /// </summary>
    private void RestoreMissionsFromData(int dayId)
    {
        // Missionen neu generieren (deterministisch gleich)
        GenerateMissions(dayId);

        // Gespeicherten Fortschritt anwenden
        if (_data.MissionProgress != null)
        {
            for (int i = 0; i < _missions.Count && i < _data.MissionProgress.Count; i++)
            {
                _missions[i].CurrentCount = _data.MissionProgress[i];
            }
        }
    }

    /// <summary>
    /// Fortschritt der Missionen in _data.MissionProgress synchronisieren
    /// </summary>
    private void SyncMissionsToData()
    {
        _data.MissionProgress = _missions.Select(m => m.CurrentCount).ToList();
    }

    private DailyMissionData Load()
    {
        try
        {
            string json = _preferences.Get<string>(DAILY_KEY, "");
            if (!string.IsNullOrEmpty(json))
                return JsonSerializer.Deserialize<DailyMissionData>(json, JsonOptions) ?? new DailyMissionData();
        }
        catch
        {
            // Fehler beim Laden -> Standardwerte
        }
        return new DailyMissionData();
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(DAILY_KEY, json);
        }
        catch
        {
            // Speichern fehlgeschlagen
        }
    }

    private class DailyMissionData
    {
        public int DayId { get; set; }
        public List<int>? MissionProgress { get; set; }
        public bool BonusClaimed { get; set; }
        public int TotalDaysCompleted { get; set; }
    }
}
