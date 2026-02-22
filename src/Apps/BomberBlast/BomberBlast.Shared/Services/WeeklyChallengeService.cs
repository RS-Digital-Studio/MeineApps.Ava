using System.Text.Json;
using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Wöchentliche Herausforderungen: 5 Missionen pro Woche, Reset Montag 00:00 UTC.
/// Missionen werden deterministisch aus der Kalenderwoche generiert (gleiche Woche = gleiche Missionen).
/// Bonus-Belohnung wenn alle 5 abgeschlossen.
/// </summary>
public class WeeklyChallengeService : IWeeklyChallengeService
{
    private const string WEEKLY_KEY = "WeeklyChallengeData";
    private const int MISSIONS_PER_WEEK = 5;
    private const int ALL_COMPLETE_BONUS = 2000;

    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IPreferencesService _preferences;
    private readonly IBattlePassService _battlePassService;
    private readonly ILeagueService _leagueService;
    private WeeklyChallengeData _data;
    private List<WeeklyMission> _missions;

    public IReadOnlyList<WeeklyMission> Missions => _missions;
    public bool IsAllComplete => _missions.Count > 0 && _missions.All(m => m.IsCompleted);
    public int CompletedCount => _missions.Count(m => m.IsCompleted);
    public int AllCompleteBonusCoins => ALL_COMPLETE_BONUS;
    public bool IsBonusClaimed => _data.BonusClaimed;
    public int TotalWeeksCompleted => _data.TotalWeeksCompleted;

    public DateTime NextResetDate
    {
        get
        {
            var now = DateTime.UtcNow;
            // Nächster Montag 00:00 UTC
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7; // Wenn heute Montag → nächsten Montag
            return now.Date.AddDays(daysUntilMonday);
        }
    }

    // Missions-Pool: Typ → (NameKey, DescKey, MinTarget, MaxTarget, CoinReward)
    private static readonly (WeeklyMissionType Type, string Name, string Desc, int Min, int Max, int Reward)[] MissionPool =
    [
        (WeeklyMissionType.CompleteLevels, "WeeklyCompleteLevels", "WeeklyCompleteLevelsDesc", 3, 7, 500),
        (WeeklyMissionType.DefeatEnemies, "WeeklyDefeatEnemies", "WeeklyDefeatEnemiesDesc", 20, 50, 400),
        (WeeklyMissionType.CollectPowerUps, "WeeklyCollectPowerUps", "WeeklyCollectPowerUpsDesc", 10, 25, 350),
        (WeeklyMissionType.EarnCoins, "WeeklyEarnCoins", "WeeklyEarnCoinsDesc", 1000, 5000, 600),
        (WeeklyMissionType.SurvivalKills, "WeeklySurvivalKills", "WeeklySurvivalKillsDesc", 10, 30, 500),
        (WeeklyMissionType.UseSpecialBombs, "WeeklyUseSpecialBombs", "WeeklyUseSpecialBombsDesc", 5, 15, 450),
        (WeeklyMissionType.AchieveCombo, "WeeklyAchieveCombo", "WeeklyAchieveComboDesc", 3, 8, 400),
        (WeeklyMissionType.WinBossFights, "WeeklyWinBossFights", "WeeklyWinBossFightsDesc", 1, 3, 700),

        // Phase 9.4: Neue Feature-Expansion Missionstypen
        (WeeklyMissionType.CompleteDungeonFloors, "WeeklyDungeonFloors", "WeeklyDungeonFloorsDesc", 3, 8, 500),
        (WeeklyMissionType.CollectCards, "WeeklyCollectCards", "WeeklyCollectCardsDesc", 2, 5, 450),
        (WeeklyMissionType.EarnGems, "WeeklyEarnGems", "WeeklyEarnGemsDesc", 10, 30, 550),
        (WeeklyMissionType.PlayQuickPlay, "WeeklyPlayQuickPlay", "WeeklyPlayQuickPlayDesc", 3, 7, 400),
        (WeeklyMissionType.SpinLuckyWheel, "WeeklySpinWheel", "WeeklySpinWheelDesc", 3, 7, 350),
        (WeeklyMissionType.UpgradeCards, "WeeklyUpgradeCards", "WeeklyUpgradeCardsDesc", 1, 3, 600),
    ];

    public WeeklyChallengeService(IPreferencesService preferences, IBattlePassService battlePassService, ILeagueService leagueService)
    {
        _preferences = preferences;
        _battlePassService = battlePassService;
        _leagueService = leagueService;
        _data = Load();
        _missions = [];
        CheckWeekReset();
    }

    public bool TrackProgress(WeeklyMissionType type, int amount = 1)
    {
        if (amount <= 0) return false;

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

        // Battle Pass XP bei jeder abgeschlossenen Weekly Mission
        if (anyCompleted)
            _battlePassService.AddXp(BattlePassXpSources.WeeklyMission, "weekly_mission");

        return anyCompleted;
    }

    public int ClaimAllCompleteBonus()
    {
        if (!IsAllComplete || _data.BonusClaimed) return 0;

        _data.BonusClaimed = true;
        _data.TotalWeeksCompleted++;
        Save();

        // Alle 5 Weekly Missions erledigt → Bonus-XP + Liga-Punkte
        _battlePassService.AddXp(BattlePassXpSources.WeeklyBonus, "weekly_all_complete");
        _leagueService.AddPoints(30);

        return ALL_COMPLETE_BONUS;
    }

    /// <summary>
    /// Prüft ob eine neue Woche angefangen hat und generiert ggf. neue Missionen
    /// </summary>
    private void CheckWeekReset()
    {
        var currentWeekId = GetWeekId(DateTime.UtcNow);

        if (_data.WeekId != currentWeekId)
        {
            // Neue Woche → neue Missionen generieren
            _data.WeekId = currentWeekId;
            _data.BonusClaimed = false;
            _data.MissionProgress = [];
            GenerateMissions(currentWeekId);
            SyncMissionsToData();
            Save();
        }
        else
        {
            // Bestehende Missionen aus gespeicherten Daten wiederherstellen
            RestoreMissionsFromData(currentWeekId);
        }
    }

    /// <summary>
    /// Woche-ID aus Datum (ISO-Kalenderwoche * 10000 + Jahr)
    /// </summary>
    private static int GetWeekId(DateTime date)
    {
        // ISO 8601: Montag = erster Tag, Woche 1 enthält den 4. Januar
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        int week = cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return date.Year * 100 + week;
    }

    /// <summary>
    /// 5 Missionen deterministisch aus der Woche-ID generieren (keine Duplikate)
    /// </summary>
    private void GenerateMissions(int weekId)
    {
        var rng = new Random(weekId);
        _missions = [];

        // Pool mischen und erste 5 nehmen
        var indices = Enumerable.Range(0, MissionPool.Length).ToList();
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        for (int i = 0; i < MISSIONS_PER_WEEK && i < indices.Count; i++)
        {
            var template = MissionPool[indices[i]];
            // Target zufällig zwischen Min und Max (in sinnvollen Schritten)
            int target = template.Min + rng.Next(template.Max - template.Min + 1);
            // Auf "schöne" Zahlen runden (5er-Schritte für größere Targets)
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
    private void RestoreMissionsFromData(int weekId)
    {
        // Missionen neu generieren (deterministisch gleich)
        GenerateMissions(weekId);

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

    private WeeklyChallengeData Load()
    {
        try
        {
            string json = _preferences.Get<string>(WEEKLY_KEY, "");
            if (!string.IsNullOrEmpty(json))
                return JsonSerializer.Deserialize<WeeklyChallengeData>(json, JsonOptions) ?? new WeeklyChallengeData();
        }
        catch
        {
            // Fehler beim Laden → Standardwerte
        }
        return new WeeklyChallengeData();
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(WEEKLY_KEY, json);
        }
        catch
        {
            // Speichern fehlgeschlagen
        }
    }

    private class WeeklyChallengeData
    {
        public int WeekId { get; set; }
        public List<int>? MissionProgress { get; set; }
        public bool BonusClaimed { get; set; }
        public int TotalWeeksCompleted { get; set; }
    }
}
