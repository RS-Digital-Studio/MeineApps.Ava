using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Tägliche Missionen: 3 Missionen pro Tag, Reset Mitternacht UTC.
/// Missionen werden deterministisch aus dem Datum generiert (gleicher Tag = gleiche Missionen).
/// Bonus-Belohnung wenn alle 3 abgeschlossen.
/// Erbt gemeinsame Logik von TimedMissionServiceBase.
/// </summary>
public sealed class DailyMissionService : TimedMissionServiceBase, IDailyMissionService
{
    private IAchievementService? _achievementService;

    // Missions-Pool: Typ → (NameKey, DescKey, MinTarget, MaxTarget, CoinReward)
    private static readonly (WeeklyMissionType Type, string Name, string Desc, int Min, int Max, int Reward)[] DailyMissionPool =
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
        : base(preferences, battlePassService, leagueService)
    {
    }

    // --- Interface-Properties (delegieren an Basisklasse) ---

    public List<WeeklyMission> Missions => MissionsList;
    public int TotalDaysCompleted => TotalPeriodsCompleted;

    // --- Abstrakte Implementierungen ---

    protected override string PreferencesKey => "DailyMissionData";
    protected override int MissionsPerPeriod => 3;
    protected override int AllCompleteBonusCoinAmount => 500;

    protected override (WeeklyMissionType Type, string Name, string Desc, int Min, int Max, int Reward)[] GetMissionPool()
        => DailyMissionPool;

    /// <summary>Tag-ID aus Datum (Jahr * 10000 + Monat * 100 + Tag)</summary>
    protected override int GetPeriodId(DateTime date)
        => date.Year * 10000 + date.Month * 100 + date.Day;

    /// <summary>Nächster Tag 00:00 UTC</summary>
    public override DateTime NextResetDate => DateTime.UtcNow.Date.AddDays(1);

    /// <summary>Battle Pass XP + Achievement-Tracking bei abgeschlossener Daily Mission</summary>
    protected override void OnMissionCompleted()
    {
        BattlePassService.AddXp(BattlePassXpSources.DailyMission, "daily_mission");
        _achievementService?.OnDailyMissionCompleted();
    }

    /// <summary>Alle 3 Daily Missions erledigt → Bonus-XP + Liga-Punkte</summary>
    protected override void OnAllCompleteBonusClaimed()
    {
        BattlePassService.AddXp(BattlePassXpSources.DailyMissionBonus, "daily_all_complete");
        LeagueService.AddPoints(15);
    }

    // --- Interface-Methoden ---

    /// <summary>Lazy-Injection um zirkuläre DI-Abhängigkeit zu vermeiden</summary>
    public void SetAchievementService(IAchievementService achievementService) => _achievementService = achievementService;

    public void TrackProgress(WeeklyMissionType type, int amount = 1)
    {
        TrackProgressInternal(type, amount);
    }
}
