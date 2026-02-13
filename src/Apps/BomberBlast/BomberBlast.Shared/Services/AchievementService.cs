using System.Text.Json;
using BomberBlast.Models;
using MeineApps.Core.Ava.Services;

namespace BomberBlast.Services;

/// <summary>
/// Achievement-Service: ~20 Achievements in 5 Kategorien.
/// Persistiert Fortschritt via IPreferencesService (JSON).
/// </summary>
public class AchievementService : IAchievementService
{
    private const string ACHIEVEMENTS_KEY = "Achievements";
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IPreferencesService _preferences;
    private readonly List<Achievement> _achievements;
    private AchievementData _data;

    public event EventHandler<Achievement>? AchievementUnlocked;

    public IReadOnlyList<Achievement> Achievements => _achievements;
    public int UnlockedCount => _achievements.Count(a => a.IsUnlocked);
    public int TotalCount => _achievements.Count;
    public int TotalEnemyKills => _data.TotalEnemyKills;

    public AchievementService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _achievements = CreateAchievements();
        _data = Load();
        ApplyProgress();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EVENT HANDLER
    // ═══════════════════════════════════════════════════════════════════════

    public Achievement? OnLevelCompleted(int level, int score, int stars, int bombsUsed, float timeRemaining, float timeUsed, bool noDamage)
    {
        Achievement? newUnlock = null;

        // Fortschritts-Achievements: Welten abschließen
        if (level == 10) newUnlock ??= TryUnlock("world1");
        if (level == 20) newUnlock ??= TryUnlock("world2");
        if (level == 30) newUnlock ??= TryUnlock("world3");
        if (level == 40) newUnlock ??= TryUnlock("world4");
        if (level == 50) newUnlock ??= TryUnlock("world5");

        // Geschick: Level ohne Treffer
        if (noDamage) newUnlock ??= TryUnlock("no_damage");

        // Geschick: ≤3 Bomben
        if (bombsUsed <= 3) newUnlock ??= TryUnlock("efficient");

        // Geschick: Level in unter 60 Sekunden abgeschlossen
        if (timeUsed > 0 && timeUsed <= 60f) newUnlock ??= TryUnlock("speedrun");

        return newUnlock;
    }

    public Achievement? OnEnemyKilled(int totalKills)
    {
        // Kampf-Achievements: kumulativer Kill-Zähler
        _data.TotalEnemyKills = totalKills;
        Save();

        Achievement? newUnlock = null;

        // Fortschritt aktualisieren
        UpdateProgress("kills_100", totalKills);
        UpdateProgress("kills_500", totalKills);
        UpdateProgress("kills_1000", totalKills);

        if (totalKills >= 100) newUnlock ??= TryUnlock("kills_100");
        if (totalKills >= 500) newUnlock ??= TryUnlock("kills_500");
        if (totalKills >= 1000) newUnlock ??= TryUnlock("kills_1000");

        return newUnlock;
    }

    public Achievement? OnArcadeWaveReached(int wave)
    {
        if (wave > _data.HighestArcadeWave)
        {
            _data.HighestArcadeWave = wave;
            Save();
        }

        UpdateProgress("arcade_10", wave);
        UpdateProgress("arcade_25", wave);

        Achievement? newUnlock = null;
        if (wave >= 10) newUnlock ??= TryUnlock("arcade_10");
        if (wave >= 25) newUnlock ??= TryUnlock("arcade_25");

        return newUnlock;
    }

    public Achievement? OnStarsUpdated(int totalStars)
    {
        _data.TotalStars = totalStars;
        Save();

        UpdateProgress("stars_50", totalStars);
        UpdateProgress("stars_100", totalStars);
        UpdateProgress("stars_150", totalStars);

        Achievement? newUnlock = null;
        if (totalStars >= 50) newUnlock ??= TryUnlock("stars_50");
        if (totalStars >= 100) newUnlock ??= TryUnlock("stars_100");
        if (totalStars >= 150) newUnlock ??= TryUnlock("stars_150");

        return newUnlock;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE
    // ═══════════════════════════════════════════════════════════════════════

    private Achievement? TryUnlock(string id)
    {
        var achievement = _achievements.Find(a => a.Id == id);
        if (achievement == null || achievement.IsUnlocked)
            return null;

        achievement.IsUnlocked = true;
        achievement.Progress = achievement.Target;
        _data.UnlockedIds.Add(id);
        Save();

        // Event feuern für Toast-Anzeige
        AchievementUnlocked?.Invoke(this, achievement);

        return achievement;
    }

    private void UpdateProgress(string id, int progress)
    {
        var achievement = _achievements.Find(a => a.Id == id);
        if (achievement != null && !achievement.IsUnlocked)
        {
            achievement.Progress = Math.Min(progress, achievement.Target);
        }
    }

    private void ApplyProgress()
    {
        // Unlock-Status wiederherstellen
        foreach (var id in _data.UnlockedIds)
        {
            var achievement = _achievements.Find(a => a.Id == id);
            if (achievement != null)
            {
                achievement.IsUnlocked = true;
                achievement.Progress = achievement.Target;
            }
        }

        // Fortschritt aktualisieren
        UpdateProgress("kills_100", _data.TotalEnemyKills);
        UpdateProgress("kills_500", _data.TotalEnemyKills);
        UpdateProgress("kills_1000", _data.TotalEnemyKills);
        UpdateProgress("arcade_10", _data.HighestArcadeWave);
        UpdateProgress("arcade_25", _data.HighestArcadeWave);
        UpdateProgress("stars_50", _data.TotalStars);
        UpdateProgress("stars_100", _data.TotalStars);
        UpdateProgress("stars_150", _data.TotalStars);
    }

    private static List<Achievement> CreateAchievements()
    {
        return
        [
            // Fortschritt (5)
            new() { Id = "world1", NameKey = "AchWorld1", DescriptionKey = "AchWorld1Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star" },
            new() { Id = "world2", NameKey = "AchWorld2", DescriptionKey = "AchWorld2Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star" },
            new() { Id = "world3", NameKey = "AchWorld3", DescriptionKey = "AchWorld3Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star" },
            new() { Id = "world4", NameKey = "AchWorld4", DescriptionKey = "AchWorld4Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Star" },
            new() { Id = "world5", NameKey = "AchWorld5", DescriptionKey = "AchWorld5Desc", Category = AchievementCategory.Progress, Target = 1, IconName = "Crown" },

            // Meisterschaft (3)
            new() { Id = "stars_50", NameKey = "AchStars50", DescriptionKey = "AchStars50Desc", Category = AchievementCategory.Mastery, Target = 50, IconName = "StarCircle" },
            new() { Id = "stars_100", NameKey = "AchStars100", DescriptionKey = "AchStars100Desc", Category = AchievementCategory.Mastery, Target = 100, IconName = "StarCircle" },
            new() { Id = "stars_150", NameKey = "AchStars150", DescriptionKey = "AchStars150Desc", Category = AchievementCategory.Mastery, Target = 150, IconName = "StarShooting" },

            // Kampf (3)
            new() { Id = "kills_100", NameKey = "AchKills100", DescriptionKey = "AchKills100Desc", Category = AchievementCategory.Combat, Target = 100, IconName = "Sword" },
            new() { Id = "kills_500", NameKey = "AchKills500", DescriptionKey = "AchKills500Desc", Category = AchievementCategory.Combat, Target = 500, IconName = "SwordCross" },
            new() { Id = "kills_1000", NameKey = "AchKills1000", DescriptionKey = "AchKills1000Desc", Category = AchievementCategory.Combat, Target = 1000, IconName = "Skull" },

            // Geschick (3)
            new() { Id = "no_damage", NameKey = "AchNoDamage", DescriptionKey = "AchNoDamageDesc", Category = AchievementCategory.Skill, Target = 1, IconName = "Shield" },
            new() { Id = "efficient", NameKey = "AchEfficient", DescriptionKey = "AchEfficientDesc", Category = AchievementCategory.Skill, Target = 1, IconName = "Target" },
            new() { Id = "speedrun", NameKey = "AchSpeedrun", DescriptionKey = "AchSpeedrunDesc", Category = AchievementCategory.Skill, Target = 1, IconName = "TimerSand" },

            // Arcade (2)
            new() { Id = "arcade_10", NameKey = "AchArcade10", DescriptionKey = "AchArcade10Desc", Category = AchievementCategory.Arcade, Target = 10, IconName = "Fire" },
            new() { Id = "arcade_25", NameKey = "AchArcade25", DescriptionKey = "AchArcade25Desc", Category = AchievementCategory.Arcade, Target = 25, IconName = "Flash" },
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

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, JsonOptions);
            _preferences.Set(ACHIEVEMENTS_KEY, json);
        }
        catch { /* Speichern fehlgeschlagen */ }
    }

    private class AchievementData
    {
        public HashSet<string> UnlockedIds { get; set; } = [];
        public int TotalEnemyKills { get; set; }
        public int HighestArcadeWave { get; set; }
        public int TotalStars { get; set; }
    }
}
