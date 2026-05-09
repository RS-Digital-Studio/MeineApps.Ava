using System.Text.Json;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Implementierung von IGameTrackingService.
/// Bündelt Aufrufe an Achievement-, Weekly-, Daily-, Collection-, League-, BattlePass- und Card-Service.
/// </summary>
public sealed class GameTrackingService : IGameTrackingService
{
    private readonly IAchievementService _achievements;
    private readonly IWeeklyChallengeService _weekly;
    private readonly IDailyMissionService _daily;
    private readonly ICollectionService _collection;
    private readonly ILeagueService _league;
    private readonly IBattlePassService _battlePass;
    private readonly IGemService _gems;
    private readonly ICoinService _coins;
    private readonly IPreferencesService _preferences;
    private readonly IMasterModeService _masterMode;
    private readonly ICustomizationService _customization;

    // Survival-Meilensteine: Schwelle in Sekunden → (Coins, Gems)
    private static readonly (int Seconds, int Coins, int Gems)[] SurvivalMilestones =
    [
        (60, 500, 0),
        (120, 1500, 3),
        (180, 3000, 5),
        (300, 5000, 10)
    ];

    private const string SURVIVAL_MILESTONES_KEY = "SurvivalMilestonesReached";

    // Seeded Random für Boss-Gem-Drops (Thread-sicher genug für Single-Thread Game-Loop)
    private readonly Random _rng = new();

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
        IGemService gems,
        ICoinService coins,
        IPreferencesService preferences,
        IMasterModeService masterMode,
        ICustomizationService customization)
    {
        _achievements = achievements;
        _weekly = weekly;
        _daily = daily;
        _collection = collection;
        _league = league;
        _battlePass = battlePass;
        Cards = cards;
        _gems = gems;
        _masterMode = masterMode;
        _customization = customization;
        _coins = coins;
        _preferences = preferences;
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
        // v2.0.37: Mission-Schwellen angehoben — Daily zaehlt ab x6, Weekly ab x8.
        // Vorher zaehlte jeder x2-Combo, was die Mission trivial machte.
        if (comboCount >= 6) _daily.TrackProgress(WeeklyMissionType.AchieveCombo);
        if (comboCount >= 8) _weekly.TrackProgress(WeeklyMissionType.AchieveCombo);
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

        // Boss-Kill Gem-Drop: 50% Chance auf 2-3 Gems
        if (_rng.Next(2) == 0)
        {
            int gemDrop = _rng.Next(2, 4); // 2 oder 3
            _gems.AddGems(gemDrop);
            _weekly.TrackProgress(WeeklyMissionType.EarnGems, gemDrop);
            _daily.TrackProgress(WeeklyMissionType.EarnGems, gemDrop);
        }
    }

    public void OnBossEncountered(BossType bossType) => _collection.RecordBossEncounter(bossType);

    // --- Level ---

    public void OnStoryLevelCompleted(int level, int score, int stars, int bombsUsed,
        float timeRemaining, float timeUsed, bool noDamage, int totalStars,
        bool isDailyChallenge, bool isMutatorLevel = false)
    {
        _achievements.OnLevelCompleted(level, score, stars, bombsUsed, timeRemaining, timeUsed, noDamage);
        _achievements.OnStarsUpdated(totalStars);

        _weekly.TrackProgress(WeeklyMissionType.CompleteLevels);
        _daily.TrackProgress(WeeklyMissionType.CompleteLevels);

        // v2.0.34: Skill-basierte Missions-Tracking
        if (stars == 3)
        {
            _weekly.TrackProgress(WeeklyMissionType.CompleteThreeStar);
            _daily.TrackProgress(WeeklyMissionType.CompleteThreeStar);
        }
        if (noDamage)
        {
            _weekly.TrackProgress(WeeklyMissionType.NoDamageLevel);
            _daily.TrackProgress(WeeklyMissionType.NoDamageLevel);
        }
        // Mutator-Level Fortschritt: Nur wenn das Level WIRKLICH einen Mutator hat
        // (Level.Mutator != None). Vorher: level > 50 — das traf auch Nicht-Mutator-Levels
        // in Welt 6-10 und gab falschen Missions-Fortschritt.
        if (isMutatorLevel)
        {
            _weekly.TrackProgress(WeeklyMissionType.CompleteMutatorLevel);
            _daily.TrackProgress(WeeklyMissionType.CompleteMutatorLevel);
        }

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

    public void OnMasterLevelCompleted(int level, int score, int stars, bool noDamage)
    {
        // Record-and-Reward: MasterModeService entscheidet ob Erstmalig/Verbesserung.
        bool improved = _masterMode.RecordLevelCompleted(level, stars);
        if (!improved) return;

        // Gem-Reward: +1 Gem bei erstmaligem 3-Sterne-Master-Clear (100 Levels × 1G = 100G Endgame).
        // improved==true + stars==3 garantiert dass dies der erste 3-Sterne-Clear für diesen Level ist
        // (RecordLevelCompleted returnt false bei gleichem/schlechterem Stern-Stand).
        if (stars == 3)
        {
            _gems.AddGems(1);
        }

        // Battle-Pass-XP-Bonus für Master-Clears (höher als Normal, weil schwerer)
        _battlePass.AddXp(150, "master_level_complete");
        if (stars == 3) _battlePass.AddXp(100, "master_three_stars");
        if (noDamage) _battlePass.AddXp(75, "master_no_damage");

        // Liga-Punkte: Gleiche Basis wie Normal, aber mit Master-Bonus
        int leaguePoints = 15 + level / 10;
        if (level % 10 == 0) leaguePoints += 25; // Master-Boss-Bonus
        _league.AddPoints(leaguePoints);

        // Master-Achievements (master_first, master_25, master_100) + deren Coin-Rewards
        _achievements.OnMasterLevelCompleted(_masterMode.TotalMasterClears, _masterMode.TotalMaster3Stars);

        // Champion-Skin: 100 Master-3-Sterne-Clears schalten master_champion frei
        if (_masterMode.TotalMaster3Stars >= 100 && !_customization.IsPlayerSkinOwned("master_champion"))
        {
            _customization.GrantPlayerSkin("master_champion");
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

        // Survival-Meilensteine: Coins + Gems bei erreichten Schwellen.
        // Reihenfolge KRITISCH: HashSet-Update + Save VOR AddCoins/AddGems,
        // damit bei Crash zwischen Reward-Vergabe und Persistenz KEIN Meilenstein doppelt vergeben wird.
        var reached = GetReachedMilestones();
        bool anyFirstTime = false;
        var pendingRewards = new List<(int coins, int gems)>();
        foreach (var (seconds, coins, gems) in SurvivalMilestones)
        {
            if (timeElapsed < seconds) break;

            bool isFirstTime = !reached.Contains(seconds);
            int awardedCoins = isFirstTime ? coins : coins / 5; // Erstmalig voll, danach 20%
            int awardedGems = isFirstTime ? gems : 0; // Gems nur beim ersten Mal

            if (isFirstTime)
            {
                reached.Add(seconds);
                anyFirstTime = true;
            }

            pendingRewards.Add((awardedCoins, awardedGems));
        }

        // 1. HashSet-Update persistieren BEVOR Belohnungen vergeben werden.
        if (anyFirstTime)
            SaveReachedMilestones(reached);

        // 2. Belohnungen vergeben. Bei Crash zwischen 1 und 2 geht der Coin-Reward verloren,
        //    aber der Milestone wird NICHT doppelt vergeben (Anti-Exploit > Anti-Reward-Loss).
        foreach (var (awardedCoins, awardedGems) in pendingRewards)
        {
            if (awardedCoins > 0)
                _coins.AddCoins(awardedCoins);
            if (awardedGems > 0)
            {
                _gems.AddGems(awardedGems);
                _weekly.TrackProgress(WeeklyMissionType.EarnGems, awardedGems);
                _daily.TrackProgress(WeeklyMissionType.EarnGems, awardedGems);
            }
        }
    }

    /// <summary>Geladene Survival-Meilensteine aus Preferences</summary>
    private HashSet<int> GetReachedMilestones()
    {
        var json = _preferences.Get(SURVIVAL_MILESTONES_KEY, "");
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<HashSet<int>>(json) ?? []; }
        catch { return []; }
    }

    private void SaveReachedMilestones(HashSet<int> reached)
    {
        _preferences.Set(SURVIVAL_MILESTONES_KEY, JsonSerializer.Serialize(reached));
    }

    // --- Gems ---

    public void OnBossLevelFirstComplete(int level)
    {
        // 5 Gems bei Erst-Abschluss eines Boss-Levels (L10, L20, ..., L100)
        _gems.AddGems(5);
        _weekly.TrackProgress(WeeklyMissionType.EarnGems, 5);
        _daily.TrackProgress(WeeklyMissionType.EarnGems, 5);
    }

    public void OnFirstThreeStars()
    {
        // 3 Gems bei erstmaligem 3-Sterne-Abschluss (erhoeht von 2G -> 3G)
        // 100 Level x 3G = 300G Story-Gesamt. Reicht fuer 1 Legendary (200G) + 5. Deck-Slot (20G) + Crystal-Skin (50G).
        // Frueher: 200G Story-Gesamt wurde komplett von einem Legendary-Skin absorbiert -> zu stark kopplung an Premium-Kauf.
        _gems.AddGems(3);
        _weekly.TrackProgress(WeeklyMissionType.EarnGems, 3);
        _daily.TrackProgress(WeeklyMissionType.EarnGems, 3);
    }

    // --- Persistenz ---

    public void FlushIfDirty()
    {
        _achievements.FlushIfDirty();
        _collection.FlushIfDirty();
    }
}
