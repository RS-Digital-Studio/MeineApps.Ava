using BomberBlast.Models;
using BomberBlast.Models.Entities;

namespace BomberBlast.Services;

/// <summary>
/// Implementierung von IGameTrackingService.
/// Bündelt Aufrufe an Achievement-, Weekly-, Daily-, Collection-, League-, BattlePass- und Card-Service.
/// </summary>
public class GameTrackingService : IGameTrackingService
{
    private readonly IAchievementService _achievements;
    private readonly IWeeklyChallengeService _weekly;
    private readonly IDailyMissionService _daily;
    private readonly ICollectionService _collection;
    private readonly ILeagueService _league;
    private readonly IBattlePassService _battlePass;
    private readonly IGemService _gems;

    public ICardService Cards { get; }
    public int TotalEnemyKills => _achievements.TotalEnemyKills;

    public GameTrackingService(
        IAchievementService achievements,
        IWeeklyChallengeService weekly,
        IDailyMissionService daily,
        ICollectionService collection,
        ILeagueService league,
        IBattlePassService battlePass,
        ICardService cards,
        IGemService gems)
    {
        _achievements = achievements;
        _weekly = weekly;
        _daily = daily;
        _collection = collection;
        _league = league;
        _battlePass = battlePass;
        Cards = cards;
        _gems = gems;
    }

    // --- Bomben ---

    public void OnSpecialBombUsed()
    {
        _achievements.OnSpecialBombUsed();
        _weekly.TrackProgress(WeeklyMissionType.UseSpecialBombs);
        _daily.TrackProgress(WeeklyMissionType.UseSpecialBombs);
    }

    public void OnPowerBombUsed() => _achievements.OnPowerBombUsed();
    public void OnLineBombUsed() => _achievements.OnLineBombUsed();
    public void OnDetonatorUsed() => _achievements.OnDetonatorUsed();
    public void OnBombKicked() => _achievements.OnBombKicked();

    // --- Spieler ---

    public void OnCurseSurvived(CurseType curseType) => _achievements.OnCurseSurvived(curseType);

    public void OnComboReached(int comboCount)
    {
        _achievements.OnComboReached(comboCount);
        _weekly.TrackProgress(WeeklyMissionType.AchieveCombo);
        _daily.TrackProgress(WeeklyMissionType.AchieveCombo);
    }

    // --- Items ---

    public void OnPowerUpCollected(string powerUpType)
    {
        _weekly.TrackProgress(WeeklyMissionType.CollectPowerUps);
        _daily.TrackProgress(WeeklyMissionType.CollectPowerUps);
        _collection.RecordPowerUpCollected(powerUpType);
    }

    // --- Gegner ---

    public void OnEnemyKilled(EnemyType type, bool isSurvival)
    {
        _achievements.OnEnemyKilled(_achievements.TotalEnemyKills + 1);
        _collection.RecordEnemyEncounter(type);
        _collection.RecordEnemyDefeat(type);
        _weekly.TrackProgress(WeeklyMissionType.DefeatEnemies);
        _daily.TrackProgress(WeeklyMissionType.DefeatEnemies);
        if (isSurvival)
        {
            _weekly.TrackProgress(WeeklyMissionType.SurvivalKills);
            _daily.TrackProgress(WeeklyMissionType.SurvivalKills);
        }
    }

    public void OnBossKilled(BossType kind, int bossFlag, float bossTime)
    {
        _achievements.OnBossDefeated(bossFlag);
        _league.AddPoints(25);
        _battlePass.AddXp(200, "boss_kill");
        _collection.RecordBossDefeat(kind, bossTime);
        _weekly.TrackProgress(WeeklyMissionType.WinBossFights);
        _daily.TrackProgress(WeeklyMissionType.WinBossFights);
    }

    public void OnBossEncountered(BossType bossType) => _collection.RecordBossEncounter(bossType);

    // --- Level ---

    public void OnStoryLevelCompleted(int level, int score, int stars, int bombsUsed,
        float timeRemaining, float timeUsed, bool noDamage, int totalStars,
        bool isDailyChallenge)
    {
        _achievements.OnLevelCompleted(level, score, stars, bombsUsed, timeRemaining, timeUsed, noDamage);
        _achievements.OnStarsUpdated(totalStars);

        _weekly.TrackProgress(WeeklyMissionType.CompleteLevels);
        _daily.TrackProgress(WeeklyMissionType.CompleteLevels);

        // Liga-Punkte: Basis + Welt-Bonus + Boss-Bonus
        int leaguePoints = 10 + level / 10;
        if (level % 10 == 0) leaguePoints += 20;
        _league.AddPoints(leaguePoints);

        // Battle Pass XP
        _battlePass.AddXp(100, "level_complete");
        if (level % 10 == 0)
            _battlePass.AddXp(200, "boss_kill");
        if (stars == 3)
            _battlePass.AddXp(50, "three_stars");

        // Daily Challenge: Extra XP + Liga-Punkte
        if (isDailyChallenge)
        {
            _battlePass.AddXp(200, "daily_challenge");
            _league.AddPoints(20);
        }
    }

    public void OnWorldPerfected(int world) => _achievements.OnWorldPerfected(world);

    public void OnQuickPlayCompleted(int difficulty)
    {
        if (difficulty >= 10)
            _achievements.OnQuickPlayMaxCompleted();
        _weekly.TrackProgress(WeeklyMissionType.PlayQuickPlay);
        _daily.TrackProgress(WeeklyMissionType.PlayQuickPlay);
    }

    // --- Dungeon ---

    public void OnDungeonFloorCompleted(int floor)
    {
        _battlePass.AddXp(50, "dungeon_floor");
        _league.AddPoints(5);
        _achievements.OnDungeonFloorReached(floor);
        _weekly.TrackProgress(WeeklyMissionType.CompleteDungeonFloors);
        _daily.TrackProgress(WeeklyMissionType.CompleteDungeonFloors);
    }

    public void OnDungeonBossDefeated()
    {
        _battlePass.AddXp(100, "dungeon_boss");
        _league.AddPoints(25);
        _achievements.OnDungeonBossDefeated();
    }

    public void OnDungeonRunCompleted() => _achievements.OnDungeonRunCompleted();

    // --- Survival ---

    public void OnSurvivalEnded(float timeElapsed, int enemiesKilled)
    {
        _achievements.OnSurvivalComplete(timeElapsed);
        _achievements.OnSurvivalKillsReached(enemiesKilled);
        if (timeElapsed >= 60f)
            _battlePass.AddXp(100, "survival_60s");
    }

    // --- Gems ---

    public void OnBossLevelFirstComplete(int level)
    {
        // 5 Gems bei Erst-Abschluss eines Boss-Levels (L10, L20, ..., L100)
        _gems.AddGems(5);
        _weekly.TrackProgress(WeeklyMissionType.EarnGems, 5);
        _daily.TrackProgress(WeeklyMissionType.EarnGems, 5);
    }

    // --- Persistenz ---

    public void FlushIfDirty()
    {
        _achievements.FlushIfDirty();
        _collection.FlushIfDirty();
    }
}
