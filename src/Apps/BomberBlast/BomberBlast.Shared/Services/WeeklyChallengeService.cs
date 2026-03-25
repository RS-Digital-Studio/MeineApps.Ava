using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Wöchentliche Herausforderungen: 5 Missionen pro Woche, Reset Montag 00:00 UTC.
/// Missionen werden deterministisch aus der Kalenderwoche generiert (gleiche Woche = gleiche Missionen).
/// Bonus-Belohnung wenn alle 5 abgeschlossen.
/// Erbt gemeinsame Logik von TimedMissionServiceBase.
/// </summary>
public sealed class WeeklyChallengeService : TimedMissionServiceBase, IWeeklyChallengeService
{
    private readonly IGemService _gemService;

    // Missions-Pool: Typ → (NameKey, DescKey, MinTarget, MaxTarget, CoinReward)
    private static readonly (WeeklyMissionType Type, string Name, string Desc, int Min, int Max, int Reward)[] WeeklyMissionPool =
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
        (WeeklyMissionType.EarnGems, "WeeklyEarnGems", "WeeklyEarnGemsDesc", 5, 15, 550),
        (WeeklyMissionType.PlayQuickPlay, "WeeklyPlayQuickPlay", "WeeklyPlayQuickPlayDesc", 3, 7, 400),
        (WeeklyMissionType.SpinLuckyWheel, "WeeklySpinWheel", "WeeklySpinWheelDesc", 3, 7, 350),
        (WeeklyMissionType.UpgradeCards, "WeeklyUpgradeCards", "WeeklyUpgradeCardsDesc", 1, 3, 600),
    ];

    public WeeklyChallengeService(IPreferencesService preferences, IBattlePassService battlePassService, ILeagueService leagueService, IGemService gemService)
        : base(preferences, battlePassService, leagueService)
    {
        _gemService = gemService;
    }

    // --- Interface-Properties (delegieren an Basisklasse) ---

    public IReadOnlyList<WeeklyMission> Missions => MissionsList;
    public int TotalWeeksCompleted => TotalPeriodsCompleted;

    // --- Abstrakte Implementierungen ---

    protected override string PreferencesKey => "WeeklyChallengeData";
    protected override int MissionsPerPeriod => 5;
    protected override int AllCompleteBonusCoinAmount => 2000;

    protected override (WeeklyMissionType Type, string Name, string Desc, int Min, int Max, int Reward)[] GetMissionPool()
        => WeeklyMissionPool;

    /// <summary>
    /// Woche-ID aus Datum (ISO-Kalenderwoche: Jahr * 100 + Wochennummer).
    /// ISO 8601: Montag = erster Tag, Woche 1 enthält den 4. Januar.
    /// </summary>
    protected override int GetPeriodId(DateTime date)
    {
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        int week = cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return date.Year * 100 + week;
    }

    /// <summary>Nächster Montag 00:00 UTC</summary>
    public override DateTime NextResetDate
    {
        get
        {
            var now = DateTime.UtcNow;
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7; // Wenn heute Montag → nächsten Montag
            return now.Date.AddDays(daysUntilMonday);
        }
    }

    /// <summary>Battle Pass XP bei abgeschlossener Weekly Mission</summary>
    protected override void OnMissionCompleted()
    {
        BattlePassService.AddXp(BattlePassXpSources.WeeklyMission, "weekly_mission");
    }

    /// <summary>Alle 5 Weekly Missions erledigt → Bonus-XP + Liga-Punkte + Gem-Bonus</summary>
    protected override void OnAllCompleteBonusClaimed()
    {
        BattlePassService.AddXp(BattlePassXpSources.WeeklyBonus, "weekly_all_complete");
        LeagueService.AddPoints(30);
        _gemService.AddGems(5);
    }

    // --- Interface-Methoden ---

    public bool TrackProgress(WeeklyMissionType type, int amount = 1)
    {
        return TrackProgressInternal(type, amount);
    }
}
