using System.Text.Json;
using BomberBlast.Models;
using BomberBlast.Models.Entities;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Achievement-Service: 50 Achievements in 5 Kategorien.
/// Persistiert Fortschritt via IPreferencesService (JSON).
/// </summary>
public class AchievementService : IAchievementService
{
    private const string ACHIEVEMENTS_KEY = "Achievements";
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IPreferencesService _preferences;
    private readonly ICoinService _coinService;
    private readonly IPlayGamesService _playGames;
    private readonly List<Achievement> _achievements;
    // O(1) Lookup statt List.Find() bei TryUnlock/UpdateProgress (66 Achievements → ~30 Find()-Aufrufe pro ApplyProgress)
    private readonly Dictionary<string, Achievement> _achievementLookup;
    private AchievementData _data;

    // Dirty-Flag + Debounce: Verhindert JSON-Serialize + Preferences-Write bei jedem einzelnen Kill
    private bool _isDirty;
    private DateTime _lastSaveTime = DateTime.MinValue;
    private const int SaveDebounceMs = 500;

    private int _unlockedCount;

    public event EventHandler<Achievement>? AchievementUnlocked;

    public IReadOnlyList<Achievement> Achievements => _achievements;
    public int UnlockedCount => _unlockedCount;
    public int TotalCount => _achievements.Count;
    public int TotalEnemyKills => _data.TotalEnemyKills;
    public int TotalBombsKicked => _data.TotalBombsKicked;
    public int TotalPowerBombs => _data.TotalPowerBombs;
    public int TotalBossKills => _data.TotalBossKills;
    public int TotalSpecialBombs => _data.TotalSpecialBombs;
    public double BestSurvivalTime => _data.BestSurvivalTime;
    public int NoDamageLevels => _data.NoDamageLevels;
    public int SpeedrunLevels => _data.SpeedrunLevels;
    public int WeeklyCompletions => _data.WeeklyCompletions;

    public AchievementService(IPreferencesService preferences, ICoinService coinService, IPlayGamesService playGames)
    {
        _preferences = preferences;
        _coinService = coinService;
        _playGames = playGames;
        _achievements = CreateAchievements();
        _achievementLookup = new Dictionary<string, Achievement>(_achievements.Count);
        foreach (var ach in _achievements)
            _achievementLookup[ach.Id] = ach;
        _data = Load();
        ApplyProgress();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENT HANDLER
    // ═══════════════════════════════════════════════════════════════════════

    public Achievement? OnLevelCompleted(int level, int score, int stars, int bombsUsed, float timeRemaining, float timeUsed, bool noDamage)
    {
        Achievement? newUnlock = null;

        // Erstes Level abgeschlossen (nur Level 1)
        if (level == 1) newUnlock ??= TryUnlock("first_victory");

        // Fortschritts-Achievements: Welten abschließen
        if (level == 10) newUnlock ??= TryUnlock("world1");
        if (level == 20) newUnlock ??= TryUnlock("world2");
        if (level == 30) newUnlock ??= TryUnlock("world3");
        if (level == 40) newUnlock ??= TryUnlock("world4");
        if (level == 50) newUnlock ??= TryUnlock("world5");
        if (level == 60) newUnlock ??= TryUnlock("world6");
        if (level == 70) newUnlock ??= TryUnlock("world7");
        if (level == 80) newUnlock ??= TryUnlock("world8");
        if (level == 90) newUnlock ??= TryUnlock("world9");
        if (level == 100) newUnlock ??= TryUnlock("world10");

        // Geschick: Level ohne Treffer
        if (noDamage)
        {
            newUnlock ??= TryUnlock("no_damage");

            // Kumulative NoDamage-Level zählen
            _data.NoDamageLevels++;
            MarkDirty();
            UpdateProgress("no_damage_5", _data.NoDamageLevels);
            UpdateProgress("no_damage_10", _data.NoDamageLevels);
            if (_data.NoDamageLevels >= 5) newUnlock ??= TryUnlock("no_damage_5");
            if (_data.NoDamageLevels >= 10) newUnlock ??= TryUnlock("no_damage_10");
        }

        // Geschick: ≤3 Bomben
        if (bombsUsed <= 3) newUnlock ??= TryUnlock("efficient");

        // Geschick: Level in unter 60 Sekunden abgeschlossen
        if (timeUsed > 0 && timeUsed <= 60f)
        {
            newUnlock ??= TryUnlock("speedrun");

            // Kumulative Speedrun-Level zählen
            _data.SpeedrunLevels++;
            MarkDirty();
            UpdateProgress("speedrun_5", _data.SpeedrunLevels);
            UpdateProgress("speedrun_10", _data.SpeedrunLevels);
            if (_data.SpeedrunLevels >= 5) newUnlock ??= TryUnlock("speedrun_5");
            if (_data.SpeedrunLevels >= 10) newUnlock ??= TryUnlock("speedrun_10");
        }

        return newUnlock;
    }

    public Achievement? OnEnemyKilled(int totalKills)
    {
        // Kampf-Achievements: kumulativer Kill-Zähler
        _data.TotalEnemyKills = totalKills;
        MarkDirty();

        Achievement? newUnlock = null;

        // Fortschritt aktualisieren
        UpdateProgress("kills_100", totalKills);
        UpdateProgress("kills_500", totalKills);
        UpdateProgress("kills_1000", totalKills);
        UpdateProgress("kills_2500", totalKills);
        UpdateProgress("kills_5000", totalKills);

        if (totalKills >= 100) newUnlock ??= TryUnlock("kills_100");
        if (totalKills >= 500) newUnlock ??= TryUnlock("kills_500");
        if (totalKills >= 1000) newUnlock ??= TryUnlock("kills_1000");
        if (totalKills >= 2500) newUnlock ??= TryUnlock("kills_2500");
        if (totalKills >= 5000) newUnlock ??= TryUnlock("kills_5000");

        return newUnlock;
    }

    public Achievement? OnStarsUpdated(int totalStars)
    {
        _data.TotalStars = totalStars;
        Save();

        // Sterne ans Leaderboard senden
        _ = _playGames.SubmitScoreAsync(PlayGamesIds.LeaderboardTotalStars, totalStars);

        UpdateProgress("stars_50", totalStars);
        UpdateProgress("stars_100", totalStars);
        UpdateProgress("stars_150", totalStars);
        UpdateProgress("stars_200", totalStars);
        UpdateProgress("stars_250", totalStars);
        UpdateProgress("stars_300", totalStars);

        Achievement? newUnlock = null;
        if (totalStars >= 50) newUnlock ??= TryUnlock("stars_50");
        if (totalStars >= 100) newUnlock ??= TryUnlock("stars_100");
        if (totalStars >= 150) newUnlock ??= TryUnlock("stars_150");
        if (totalStars >= 200) newUnlock ??= TryUnlock("stars_200");
        if (totalStars >= 250) newUnlock ??= TryUnlock("stars_250");
        if (totalStars >= 300) newUnlock ??= TryUnlock("stars_300");

        return newUnlock;
    }

    public Achievement? OnComboReached(int comboCount)
    {
        Achievement? newUnlock = null;
        if (comboCount >= 3) newUnlock ??= TryUnlock("combo3");
        if (comboCount >= 5) newUnlock ??= TryUnlock("combo5");
        if (comboCount >= 7) newUnlock ??= TryUnlock("combo7");
        return newUnlock;
    }

    public Achievement? OnBombKicked()
    {
        _data.TotalBombsKicked++;
        MarkDirty();

        UpdateProgress("kick_master", _data.TotalBombsKicked);
        if (_data.TotalBombsKicked >= 25) return TryUnlock("kick_master");
        return null;
    }

    public Achievement? OnPowerBombUsed()
    {
        _data.TotalPowerBombs++;
        MarkDirty();

        UpdateProgress("power_bomber", _data.TotalPowerBombs);
        if (_data.TotalPowerBombs >= 10) return TryUnlock("power_bomber");
        return null;
    }

    public Achievement? OnCurseSurvived(CurseType curseType)
    {
        // Bit-Flag: Jeden überlebten Curse-Typ merken
        int curseFlag = curseType switch
        {
            CurseType.Diarrhea => 1,
            CurseType.Slow => 2,
            CurseType.Constipation => 4,
            CurseType.ReverseControls => 8,
            _ => 0
        };

        if (curseFlag == 0) return null;

        _data.CurseTypesSurvived |= curseFlag;
        MarkDirty();

        int survived = CountBits(_data.CurseTypesSurvived);
        UpdateProgress("curse_survivor", survived);
        if (survived >= 4) return TryUnlock("curse_survivor");
        return null;
    }

    public Achievement? OnDailyChallengeCompleted(int totalCompleted, int currentStreak)
    {
        Achievement? newUnlock = null;

        UpdateProgress("daily_streak7", currentStreak);
        UpdateProgress("daily_complete30", totalCompleted);

        if (currentStreak >= 7) newUnlock ??= TryUnlock("daily_streak7");
        if (totalCompleted >= 30) newUnlock ??= TryUnlock("daily_complete30");

        return newUnlock;
    }

    public Achievement? OnBossDefeated(int bossTypeFlag)
    {
        _data.TotalBossKills++;
        _data.BossTypesDefeated |= bossTypeFlag;
        MarkDirty();

        Achievement? newUnlock = null;
        newUnlock ??= TryUnlock("boss_slayer");

        int bossCount = CountBits(_data.BossTypesDefeated);
        UpdateProgress("boss_master", bossCount);
        if (bossCount >= 5) newUnlock ??= TryUnlock("boss_master");

        return newUnlock;
    }

    public Achievement? OnSpecialBombUsed()
    {
        _data.TotalSpecialBombs++;
        MarkDirty();

        UpdateProgress("special_bomb_50", _data.TotalSpecialBombs);
        UpdateProgress("special_bomb_100", _data.TotalSpecialBombs);

        Achievement? newUnlock = null;
        if (_data.TotalSpecialBombs >= 50) newUnlock ??= TryUnlock("special_bomb_50");
        if (_data.TotalSpecialBombs >= 100) newUnlock ??= TryUnlock("special_bomb_100");

        return newUnlock;
    }

    public Achievement? OnSurvivalComplete(double survivalTime)
    {
        if (survivalTime > _data.BestSurvivalTime)
            _data.BestSurvivalTime = survivalTime;
        MarkDirty();

        Achievement? newUnlock = null;
        if (survivalTime >= 60) newUnlock ??= TryUnlock("survival_60");
        if (survivalTime >= 180) newUnlock ??= TryUnlock("survival_180");
        if (survivalTime >= 300) newUnlock ??= TryUnlock("survival_300");

        return newUnlock;
    }

    public Achievement? OnWeeklyWeekCompleted()
    {
        _data.WeeklyCompletions++;
        MarkDirty();

        UpdateProgress("weekly_complete10", _data.WeeklyCompletions);
        if (_data.WeeklyCompletions >= 10) return TryUnlock("weekly_complete10");
        return null;
    }

    public Achievement? OnWorldPerfected(int world)
    {
        MarkDirty();

        Achievement? newUnlock = null;
        if (world == 1) newUnlock ??= TryUnlock("perfect_world1");
        if (world == 5) newUnlock ??= TryUnlock("perfect_world5");
        if (world == 10) newUnlock ??= TryUnlock("perfect_world10");

        return newUnlock;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PHASE 9.3: NEUE EVENT HANDLER
    // ═══════════════════════════════════════════════════════════════════════

    public Achievement? OnDungeonFloorReached(int floor)
    {
        if (floor > _data.BestDungeonFloor)
        {
            _data.BestDungeonFloor = floor;
            MarkDirty();
        }

        if (floor >= 10) return TryUnlock("dungeon_floor10");
        return null;
    }

    public Achievement? OnDungeonRunCompleted()
    {
        _data.TotalDungeonRuns++;
        MarkDirty();

        UpdateProgress("dungeon_runs10", _data.TotalDungeonRuns);
        if (_data.TotalDungeonRuns >= 10) return TryUnlock("dungeon_runs10");
        return null;
    }

    public Achievement? OnDungeonBossDefeated()
    {
        return TryUnlock("dungeon_boss");
    }

    public Achievement? OnBattlePassTierReached(int tier)
    {
        if (tier > _data.HighestBattlePassTier)
        {
            _data.HighestBattlePassTier = tier;
            MarkDirty();
        }

        Achievement? newUnlock = null;
        if (tier >= 15) newUnlock ??= TryUnlock("bp_tier15");
        if (tier >= 30) newUnlock ??= TryUnlock("bp_tier30");
        return newUnlock;
    }

    public Achievement? OnCardCollected(int uniqueCount, int maxLevel)
    {
        if (uniqueCount > _data.TotalUniqueCards)
        {
            _data.TotalUniqueCards = uniqueCount;
            MarkDirty();
        }

        Achievement? newUnlock = null;

        // Gold-Karte (Level 3) besessen
        if (maxLevel >= 3) newUnlock ??= TryUnlock("card_gold");

        // 10 verschiedene Karten
        UpdateProgress("cards_10", uniqueCount);
        if (uniqueCount >= 10) newUnlock ??= TryUnlock("cards_10");

        return newUnlock;
    }

    public Achievement? OnLeagueTierReached(int tierIndex)
    {
        if (tierIndex > _data.HighestLeagueTier)
        {
            _data.HighestLeagueTier = tierIndex;
            MarkDirty();
        }

        Achievement? newUnlock = null;
        if (tierIndex >= 2) newUnlock ??= TryUnlock("league_gold"); // Gold = Index 2
        if (tierIndex >= 4) newUnlock ??= TryUnlock("league_diamond"); // Diamant = Index 4
        return newUnlock;
    }

    public Achievement? OnDailyMissionCompleted()
    {
        _data.TotalDailyMissions++;
        MarkDirty();

        UpdateProgress("daily_mission_50", _data.TotalDailyMissions);
        if (_data.TotalDailyMissions >= 50) return TryUnlock("daily_mission_50");
        return null;
    }

    public Achievement? OnSurvivalKillsReached(int kills)
    {
        if (kills > _data.BestSurvivalKills)
        {
            _data.BestSurvivalKills = kills;
            MarkDirty();
        }

        if (kills >= 50) return TryUnlock("survival_kills_50");
        return null;
    }

    public Achievement? OnLineBombUsed()
    {
        _data.TotalLineBombs++;
        MarkDirty();

        UpdateProgress("line_bomb_master", _data.TotalLineBombs);
        if (_data.TotalLineBombs >= 25) return TryUnlock("line_bomb_master");
        return null;
    }

    public Achievement? OnDetonatorUsed()
    {
        _data.TotalDetonations++;
        MarkDirty();

        UpdateProgress("detonator_master", _data.TotalDetonations);
        if (_data.TotalDetonations >= 50) return TryUnlock("detonator_master");
        return null;
    }

    public Achievement? OnLuckyJackpot()
    {
        _data.LuckyJackpots++;
        MarkDirty();

        return TryUnlock("lucky_jackpot");
    }

    public Achievement? OnQuickPlayMaxCompleted()
    {
        return TryUnlock("quick_play_max");
    }

    public Achievement? OnCollectionProgressUpdated(int progressPercent)
    {
        Achievement? newUnlock = null;
        if (progressPercent >= 25) newUnlock ??= TryUnlock("collector_25");
        if (progressPercent >= 50) newUnlock ??= TryUnlock("collector_50");
        if (progressPercent >= 100) newUnlock ??= TryUnlock("collector_100");
        return newUnlock;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE
    // ═══════════════════════════════════════════════════════════════════════

    private Achievement? TryUnlock(string id)
    {
        if (!_achievementLookup.TryGetValue(id, out var achievement))
            return null;
        if (achievement.IsUnlocked)
            return null;

        achievement.IsUnlocked = true;
        achievement.Progress = achievement.Target;
        _data.UnlockedIds.Add(id);
        _unlockedCount++;
        Save();

        // Coin-Belohnung gutschreiben
        if (achievement.CoinReward > 0)
            _coinService.AddCoins(achievement.CoinReward);

        // Google Play Games Achievement freischalten
        var gpgsId = PlayGamesIds.GetGpgsAchievementId(id);
        if (gpgsId != null)
            _ = _playGames.UnlockAchievementAsync(gpgsId);

        // Event feuern für Toast-Anzeige
        AchievementUnlocked?.Invoke(this, achievement);

        return achievement;
    }

    private void UpdateProgress(string id, int progress)
    {
        if (_achievementLookup.TryGetValue(id, out var achievement) && !achievement.IsUnlocked)
        {
            achievement.Progress = Math.Min(progress, achievement.Target);
        }
    }

    private void ApplyProgress()
    {
        // Unlock-Status wiederherstellen (O(1) per Dictionary statt O(n) per List.Find)
        foreach (var id in _data.UnlockedIds)
        {
            if (_achievementLookup.TryGetValue(id, out var achievement))
            {
                achievement.IsUnlocked = true;
                achievement.Progress = achievement.Target;
            }
        }

        // Cache initialisieren
        _unlockedCount = _data.UnlockedIds.Count;

        // Fortschritt aktualisieren - Kills
        UpdateProgress("kills_100", _data.TotalEnemyKills);
        UpdateProgress("kills_500", _data.TotalEnemyKills);
        UpdateProgress("kills_1000", _data.TotalEnemyKills);
        UpdateProgress("kills_2500", _data.TotalEnemyKills);
        UpdateProgress("kills_5000", _data.TotalEnemyKills);

        // Sterne
        UpdateProgress("stars_50", _data.TotalStars);
        UpdateProgress("stars_100", _data.TotalStars);
        UpdateProgress("stars_150", _data.TotalStars);
        UpdateProgress("stars_200", _data.TotalStars);
        UpdateProgress("stars_250", _data.TotalStars);
        UpdateProgress("stars_300", _data.TotalStars);

        // Kampf-Fähigkeiten
        UpdateProgress("kick_master", _data.TotalBombsKicked);
        UpdateProgress("power_bomber", _data.TotalPowerBombs);

        // Spezial-Bomben
        UpdateProgress("special_bomb_50", _data.TotalSpecialBombs);
        UpdateProgress("special_bomb_100", _data.TotalSpecialBombs);

        // NoDamage + Speedrun
        UpdateProgress("no_damage_5", _data.NoDamageLevels);
        UpdateProgress("no_damage_10", _data.NoDamageLevels);
        UpdateProgress("speedrun_5", _data.SpeedrunLevels);
        UpdateProgress("speedrun_10", _data.SpeedrunLevels);

        // Boss-Typen besiegt
        int bossCount = CountBits(_data.BossTypesDefeated);
        UpdateProgress("boss_master", bossCount);

        // Weekly Completions
        UpdateProgress("weekly_complete10", _data.WeeklyCompletions);

        // Curse-Survivor: Überlebte Typen zählen
        int survived = CountBits(_data.CurseTypesSurvived);
        UpdateProgress("curse_survivor", survived);

        // Phase 9.3: Neue Tracking-Felder
        UpdateProgress("dungeon_runs10", _data.TotalDungeonRuns);
        UpdateProgress("daily_mission_50", _data.TotalDailyMissions);
        UpdateProgress("cards_10", _data.TotalUniqueCards);
        UpdateProgress("line_bomb_master", _data.TotalLineBombs);
        UpdateProgress("detonator_master", _data.TotalDetonations);
    }

    /// <summary>Zählt gesetzte Bits in einem Integer (für Bit-Flags wie BossTypesDefeated, CurseTypesSurvived)</summary>
    private static int CountBits(int value)
    {
        int count = 0;
        while (value > 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }

    private static List<Achievement> CreateAchievements()
    {
        return
        [
            // Fortschritt (10) - Coins steigen mit Welt-Schwierigkeit
            new() { Id = "world1", NameKey = "AchWorld1", DescriptionKey = "AchWorld1Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star", CoinReward = 500 },
            new() { Id = "world2", NameKey = "AchWorld2", DescriptionKey = "AchWorld2Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star", CoinReward = 750 },
            new() { Id = "world3", NameKey = "AchWorld3", DescriptionKey = "AchWorld3Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star", CoinReward = 1000 },
            new() { Id = "world4", NameKey = "AchWorld4", DescriptionKey = "AchWorld4Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star", CoinReward = 1500 },
            new() { Id = "world5", NameKey = "AchWorld5", DescriptionKey = "AchWorld5Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Crown", CoinReward = 3000 },
            new() { Id = "world6", NameKey = "AchWorld6", DescriptionKey = "AchWorld6Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star", CoinReward = 2000 },
            new() { Id = "world7", NameKey = "AchWorld7", DescriptionKey = "AchWorld7Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star", CoinReward = 2500 },
            new() { Id = "world8", NameKey = "AchWorld8", DescriptionKey = "AchWorld8Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star", CoinReward = 3000 },
            new() { Id = "world9", NameKey = "AchWorld9", DescriptionKey = "AchWorld9Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star", CoinReward = 4000 },
            new() { Id = "world10", NameKey = "AchWorld10", DescriptionKey = "AchWorld10Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Crown", CoinReward = 5000 },

            // Meisterschaft (3)
            new() { Id = "stars_50", NameKey = "AchStars50", DescriptionKey = "AchStars50Desc", Category = AchievementCategory.Mastery, Target = 50, IconName = "StarCircle", CoinReward = 1000 },
            new() { Id = "stars_100", NameKey = "AchStars100", DescriptionKey = "AchStars100Desc", Category = AchievementCategory.Mastery, Target = 100, IconName = "StarCircle", CoinReward = 2000 },
            new() { Id = "stars_150", NameKey = "AchStars150", DescriptionKey = "AchStars150Desc", Category = AchievementCategory.Mastery, Target = 150, IconName = "StarShooting", CoinReward = 5000 },

            // Kampf (3)
            new() { Id = "kills_100", NameKey = "AchKills100", DescriptionKey = "AchKills100Desc", Category = AchievementCategory.Combat, Target = 100, IconName = "Sword", CoinReward = 500 },
            new() { Id = "kills_500", NameKey = "AchKills500", DescriptionKey = "AchKills500Desc", Category = AchievementCategory.Combat, Target = 500, IconName = "SwordCross", CoinReward = 1500 },
            new() { Id = "kills_1000", NameKey = "AchKills1000", DescriptionKey = "AchKills1000Desc", Category = AchievementCategory.Combat, Target = 1000, IconName = "Skull", CoinReward = 3000 },

            // Geschick (3)
            new() { Id = "no_damage", NameKey = "AchNoDamage", DescriptionKey = "AchNoDamageDesc", Category = AchievementCategory.Skill, Target = 1, IconName = "Shield", CoinReward = 1000 },
            new() { Id = "efficient", NameKey = "AchEfficient", DescriptionKey = "AchEfficientDesc", Category = AchievementCategory.Skill, Target = 1, IconName = "Target", CoinReward = 1000 },
            new() { Id = "speedrun", NameKey = "AchSpeedrun", DescriptionKey = "AchSpeedrunDesc", Category = AchievementCategory.Skill, Target = 1, IconName = "TimerSand", CoinReward = 1500 },

            // ═══ Neue Achievements (8) ═══

            // Fortschritt: Erstes Level abgeschlossen
            new() { Id = "first_victory", NameKey = "AchFirstVictory", DescriptionKey = "AchFirstVictoryDesc", Category = AchievementCategory.Progress, Target = 1, IconName = "Flag", CoinReward = 200 },

            // Fortschritt: Daily Challenge Streak + Total
            new() { Id = "daily_streak7", NameKey = "AchDailyStreak7", DescriptionKey = "AchDailyStreak7Desc", Category = AchievementCategory.Progress, Target = 7, IconName = "CalendarCheck", CoinReward = 2000 },
            new() { Id = "daily_complete30", NameKey = "AchDailyComplete30", DescriptionKey = "AchDailyComplete30Desc", Category = AchievementCategory.Progress, Target = 30, IconName = "CalendarStar", CoinReward = 5000 },

            // Geschick: Combo x3 und x5
            new() { Id = "combo3", NameKey = "AchCombo3", DescriptionKey = "AchCombo3Desc", Category = AchievementCategory.Skill, Target = 1, IconName = "Flash", CoinReward = 500 },
            new() { Id = "combo5", NameKey = "AchCombo5", DescriptionKey = "AchCombo5Desc", Category = AchievementCategory.Skill, Target = 1, IconName = "FlashAlert", CoinReward = 2000 },

            // Geschick: Alle 4 Curse-Typen überlebt
            new() { Id = "curse_survivor", NameKey = "AchCurseSurvivor", DescriptionKey = "AchCurseSurvivorDesc", Category = AchievementCategory.Skill, Target = 4, IconName = "Skull", CoinReward = 1500 },

            // Kampf: 25 Bomben gekickt
            new() { Id = "kick_master", NameKey = "AchKickMaster", DescriptionKey = "AchKickMasterDesc", Category = AchievementCategory.Combat, Target = 25, IconName = "ShoeSneaker", CoinReward = 1000 },

            // Kampf: 10 Power-Bombs eingesetzt
            new() { Id = "power_bomber", NameKey = "AchPowerBomber", DescriptionKey = "AchPowerBomberDesc", Category = AchievementCategory.Combat, Target = 10, IconName = "Bomb", CoinReward = 1500 },

            // ═══ Neue Achievements Phase 13 (21) ═══

            // Fortschritt: Perfekte Welten (alle 30 Sterne)
            new() { Id = "perfect_world1", NameKey = "AchPerfectWorld1", DescriptionKey = "AchPerfectWorld1Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "StarCircle", CoinReward = 1500 },
            new() { Id = "perfect_world5", NameKey = "AchPerfectWorld5", DescriptionKey = "AchPerfectWorld5Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "StarCircle", CoinReward = 3000 },
            new() { Id = "perfect_world10", NameKey = "AchPerfectWorld10", DescriptionKey = "AchPerfectWorld10Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "StarCircle", CoinReward = 5000 },

            // Fortschritt: Weekly Challenges komplett
            new() { Id = "weekly_complete10", NameKey = "AchWeeklyComplete10", DescriptionKey = "AchWeeklyComplete10Desc", Category = AchievementCategory.Progress, Target = 10, IconName = "CalendarCheck", CoinReward = 3000 },

            // Meisterschaft: Höhere Sterne-Ziele
            new() { Id = "stars_200", NameKey = "AchStars200", DescriptionKey = "AchStars200Desc", Category = AchievementCategory.Mastery, Target = 200, IconName = "Star", CoinReward = 3000 },
            new() { Id = "stars_250", NameKey = "AchStars250", DescriptionKey = "AchStars250Desc", Category = AchievementCategory.Mastery, Target = 250, IconName = "Star", CoinReward = 4000 },
            new() { Id = "stars_300", NameKey = "AchStars300", DescriptionKey = "AchStars300Desc", Category = AchievementCategory.Mastery, Target = 300, IconName = "Star", CoinReward = 10000 },

            // Kampf: Höhere Kill-Ziele
            new() { Id = "kills_2500", NameKey = "AchKills2500", DescriptionKey = "AchKills2500Desc", Category = AchievementCategory.Combat, Target = 2500, IconName = "Sword", CoinReward = 5000 },
            new() { Id = "kills_5000", NameKey = "AchKills5000", DescriptionKey = "AchKills5000Desc", Category = AchievementCategory.Combat, Target = 5000, IconName = "Sword", CoinReward = 10000 },

            // Kampf: Boss-Achievements
            new() { Id = "boss_slayer", NameKey = "AchBossSlayer", DescriptionKey = "AchBossSlayerDesc", Category = AchievementCategory.Combat, Target = 1, IconName = "Crown", CoinReward = 1000 },
            new() { Id = "boss_master", NameKey = "AchBossMaster", DescriptionKey = "AchBossMasterDesc", Category = AchievementCategory.Combat, Target = 5, IconName = "Crown", CoinReward = 5000 },

            // Kampf: Spezial-Bomben
            new() { Id = "special_bomb_50", NameKey = "AchSpecialBomb50", DescriptionKey = "AchSpecialBomb50Desc", Category = AchievementCategory.Combat, Target = 50, IconName = "Bomb", CoinReward = 2000 },
            new() { Id = "special_bomb_100", NameKey = "AchSpecialBomb100", DescriptionKey = "AchSpecialBomb100Desc", Category = AchievementCategory.Combat, Target = 100, IconName = "Bomb", CoinReward = 4000 },

            // Geschick: NoDamage-Level kumulativ
            new() { Id = "no_damage_5", NameKey = "AchNoDamage5", DescriptionKey = "AchNoDamage5Desc", Category = AchievementCategory.Skill, Target = 5, IconName = "Shield", CoinReward = 2000 },
            new() { Id = "no_damage_10", NameKey = "AchNoDamage10", DescriptionKey = "AchNoDamage10Desc", Category = AchievementCategory.Skill, Target = 10, IconName = "Shield", CoinReward = 5000 },

            // Geschick: Speedrun-Level kumulativ
            new() { Id = "speedrun_5", NameKey = "AchSpeedrun5", DescriptionKey = "AchSpeedrun5Desc", Category = AchievementCategory.Skill, Target = 5, IconName = "Timer", CoinReward = 2000 },
            new() { Id = "speedrun_10", NameKey = "AchSpeedrun10", DescriptionKey = "AchSpeedrun10Desc", Category = AchievementCategory.Skill, Target = 10, IconName = "Timer", CoinReward = 5000 },

            // Geschick: 7er-Combo
            new() { Id = "combo7", NameKey = "AchCombo7", DescriptionKey = "AchCombo7Desc", Category = AchievementCategory.Skill, Target = 1, IconName = "Lightning", CoinReward = 3000 },

            // Herausforderung: Survival
            new() { Id = "survival_60", NameKey = "AchSurvival60", DescriptionKey = "AchSurvival60Desc", Category = AchievementCategory.Challenge, Target = 1, IconName = "Skull", CoinReward = 1500 },
            new() { Id = "survival_180", NameKey = "AchSurvival180", DescriptionKey = "AchSurvival180Desc", Category = AchievementCategory.Challenge, Target = 1, IconName = "Skull", CoinReward = 5000 },

            // ═══ Neue Achievements Phase 9.3 (20) ═══

            // Dungeon (3)
            new() { Id = "dungeon_floor10", NameKey = "AchDungeonFloor10", DescriptionKey = "AchDungeonFloor10Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Stairs", CoinReward = 3000 },
            new() { Id = "dungeon_runs10", NameKey = "AchDungeonRuns10", DescriptionKey = "AchDungeonRuns10Desc", Category = AchievementCategory.Progress, Target = 10, IconName = "Repeat", CoinReward = 2000 },
            new() { Id = "dungeon_boss", NameKey = "AchDungeonBoss", DescriptionKey = "AchDungeonBossDesc", Category = AchievementCategory.Combat, Target = 1, IconName = "Crown", CoinReward = 1500 },

            // Sammlung (3)
            new() { Id = "collector_25", NameKey = "AchCollector25", DescriptionKey = "AchCollector25Desc", Category = AchievementCategory.Mastery, Target = 1, IconName = "BookOpen", CoinReward = 1500 },
            new() { Id = "collector_50", NameKey = "AchCollector50", DescriptionKey = "AchCollector50Desc", Category = AchievementCategory.Mastery, Target = 1, IconName = "BookOpen", CoinReward = 3000 },
            new() { Id = "collector_100", NameKey = "AchCollector100", DescriptionKey = "AchCollector100Desc", Category = AchievementCategory.Mastery, Target = 1, IconName = "BookOpenVariant", CoinReward = 10000 },

            // Battle Pass (2)
            new() { Id = "bp_tier15", NameKey = "AchBpTier15", DescriptionKey = "AchBpTier15Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star", CoinReward = 2000 },
            new() { Id = "bp_tier30", NameKey = "AchBpTier30", DescriptionKey = "AchBpTier30Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "StarShooting", CoinReward = 5000 },

            // Karten (2)
            new() { Id = "card_gold", NameKey = "AchCardGold", DescriptionKey = "AchCardGoldDesc", Category = AchievementCategory.Mastery, Target = 1, IconName = "CardsPlaying", CoinReward = 2000 },
            new() { Id = "cards_10", NameKey = "AchCards10", DescriptionKey = "AchCards10Desc", Category = AchievementCategory.Mastery, Target = 10, IconName = "Cards", CoinReward = 2000 },

            // Tägliche Missionen (1)
            new() { Id = "daily_mission_50", NameKey = "AchDailyMission50", DescriptionKey = "AchDailyMission50Desc", Category = AchievementCategory.Progress, Target = 50, IconName = "CalendarCheck", CoinReward = 3000 },

            // Liga (2)
            new() { Id = "league_gold", NameKey = "AchLeagueGold", DescriptionKey = "AchLeagueGoldDesc", Category = AchievementCategory.Progress, Target = 1, IconName = "ShieldStar", CoinReward = 3000 },
            new() { Id = "league_diamond", NameKey = "AchLeagueDiamond", DescriptionKey = "AchLeagueDiamondDesc", Category = AchievementCategory.Progress, Target = 1, IconName = "Diamond", CoinReward = 10000 },

            // Lucky Spin (1)
            new() { Id = "lucky_jackpot", NameKey = "AchLuckyJackpot", DescriptionKey = "AchLuckyJackpotDesc", Category = AchievementCategory.Skill, Target = 1, IconName = "Clover", CoinReward = 1000 },

            // Herausforderung erweitert (3)
            new() { Id = "survival_300", NameKey = "AchSurvival300", DescriptionKey = "AchSurvival300Desc", Category = AchievementCategory.Challenge, Target = 1, IconName = "Timer", CoinReward = 5000 },
            new() { Id = "survival_kills_50", NameKey = "AchSurvivalKills50", DescriptionKey = "AchSurvivalKills50Desc", Category = AchievementCategory.Challenge, Target = 1, IconName = "Sword", CoinReward = 3000 },

            // Kampf erweitert (2)
            new() { Id = "line_bomb_master", NameKey = "AchLineBombMaster", DescriptionKey = "AchLineBombMasterDesc", Category = AchievementCategory.Combat, Target = 25, IconName = "ArrowRightBold", CoinReward = 1500 },
            new() { Id = "detonator_master", NameKey = "AchDetonatorMaster", DescriptionKey = "AchDetonatorMasterDesc", Category = AchievementCategory.Combat, Target = 50, IconName = "FlashAlert", CoinReward = 2000 },

            // Quick Play (1)
            new() { Id = "quick_play_max", NameKey = "AchQuickPlayMax", DescriptionKey = "AchQuickPlayMaxDesc", Category = AchievementCategory.Skill, Target = 1, IconName = "Dice5", CoinReward = 3000 },
        ];
    }

    private AchievementData Load()
    {
        try
        {
            string json = _preferences.Get<string>(ACHIEVEMENTS_KEY, "");
            if (!string.IsNullOrEmpty(json))
                return JsonSerializer.Deserialize<AchievementData>(json, JsonOptions) ?? new AchievementData();
        }
        catch { /* Standardwerte */ }
        return new AchievementData();
    }

    /// <summary>
    /// Markiert Daten als geändert. Speichert nur wenn das Debounce-Intervall überschritten ist.
    /// Verhindert Dutzende JSON-Serializes + Preferences-Writes pro Sekunde bei Kettenreaktionen.
    /// </summary>
    private void MarkDirty()
    {
        _isDirty = true;
        var now = DateTime.UtcNow;
        if ((now - _lastSaveTime).TotalMilliseconds >= SaveDebounceMs)
            Save();
    }

    /// <summary>
    /// Erzwingt das Speichern falls Dirty-Flag gesetzt (z.B. am Ende eines Levels).
    /// Wird vom AchievementService selbst bei TryUnlock aufgerufen.
    /// </summary>
    public void FlushIfDirty()
    {
        if (_isDirty) Save();
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(ACHIEVEMENTS_KEY, json);
            _isDirty = false;
            _lastSaveTime = DateTime.UtcNow;
        }
        catch { /* Speichern fehlgeschlagen */ }
    }

    private class AchievementData
    {
        public HashSet<string> UnlockedIds { get; set; } = [];
        public int TotalEnemyKills { get; set; }
        public int TotalStars { get; set; }
        public int TotalBombsKicked { get; set; }
        public int TotalPowerBombs { get; set; }
        /// <summary>Bit-Flags: 1=Diarrhea, 2=Slow, 4=Constipation, 8=ReverseControls</summary>
        public int CurseTypesSurvived { get; set; }
        /// <summary>Anzahl Level ohne Schaden abgeschlossen</summary>
        public int NoDamageLevels { get; set; }
        /// <summary>Anzahl Level unter 60s abgeschlossen</summary>
        public int SpeedrunLevels { get; set; }
        /// <summary>Kumulative Boss-Kills</summary>
        public int TotalBossKills { get; set; }
        /// <summary>Bit-Flags für besiegte Boss-Typen: 1=StoneGolem, 2=IceDragon, 4=FireDemon, 8=ShadowMaster, 16=FinalBoss</summary>
        public int BossTypesDefeated { get; set; }
        /// <summary>Kumulative Spezial-Bomben (Ice/Fire/Sticky)</summary>
        public int TotalSpecialBombs { get; set; }
        /// <summary>Längste Survival-Zeit in Sekunden</summary>
        public double BestSurvivalTime { get; set; }
        /// <summary>Gesamtzahl abgeschlossener Weekly-Wochen</summary>
        public int WeeklyCompletions { get; set; }

        // Phase 9.3: Neue Tracking-Felder
        /// <summary>Kumulative tägliche Missionen abgeschlossen</summary>
        public int TotalDailyMissions { get; set; }
        /// <summary>Höchster erreichter Dungeon-Floor</summary>
        public int BestDungeonFloor { get; set; }
        /// <summary>Gesamtzahl abgeschlossener Dungeon-Runs</summary>
        public int TotalDungeonRuns { get; set; }
        /// <summary>Höchstes erreichtes Battle-Pass-Tier</summary>
        public int HighestBattlePassTier { get; set; }
        /// <summary>Gesamtzahl verschiedener gesammelter Karten</summary>
        public int TotalUniqueCards { get; set; }
        /// <summary>Höchste erreichte Liga (0=Bronze...4=Diamant)</summary>
        public int HighestLeagueTier { get; set; }
        /// <summary>Beste Kills in einem Survival-Run</summary>
        public int BestSurvivalKills { get; set; }
        /// <summary>Kumulative Line-Bombs eingesetzt</summary>
        public int TotalLineBombs { get; set; }
        /// <summary>Kumulative manuelle Detonationen</summary>
        public int TotalDetonations { get; set; }
        /// <summary>Glücksrad-Jackpots gewonnen</summary>
        public int LuckyJackpots { get; set; }
    }
}
