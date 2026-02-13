using MeineApps.Core.Ava.Services;

namespace FitnessRechner.Services;

/// <summary>
/// Preferences-basierter Level/XP-Service.
/// Level-Formel: XpForLevel(n) = 100 * n * (n+1) / 2
/// Level 1=100, 2=300, 3=600, 4=1000, ..., Max Level 50.
/// </summary>
public class LevelService : ILevelService
{
    private const string XpKey = "fitness_xp";
    private const string LevelKey = "fitness_level";
    private const int MaxLevel = 50;

    private readonly IPreferencesService _preferences;
    private int _totalXp;
    private int _currentLevel;

    public event Action<int>? LevelUp;

    public LevelService(IPreferencesService preferences)
    {
        _preferences = preferences;
        _totalXp = preferences.Get(XpKey, 0);
        _currentLevel = preferences.Get(LevelKey, 1);

        // Level konsistent berechnen (falls Preferences korrupt)
        _currentLevel = CalculateLevelFromXp(_totalXp);
    }

    public int CurrentLevel => _currentLevel;
    public int TotalXp => _totalXp;

    public double LevelProgress
    {
        get
        {
            if (_currentLevel >= MaxLevel) return 1.0;
            var currentLevelXp = XpForLevel(_currentLevel);
            var nextLevelXp = XpForLevel(_currentLevel + 1);
            var xpInLevel = _totalXp - currentLevelXp;
            var xpNeeded = nextLevelXp - currentLevelXp;
            return xpNeeded > 0 ? Math.Min((double)xpInLevel / xpNeeded, 1.0) : 1.0;
        }
    }

    public string XpDisplay
    {
        get
        {
            if (_currentLevel >= MaxLevel) return "MAX";
            var currentLevelXp = XpForLevel(_currentLevel);
            var nextLevelXp = XpForLevel(_currentLevel + 1);
            var xpInLevel = _totalXp - currentLevelXp;
            var xpNeeded = nextLevelXp - currentLevelXp;
            return $"{xpInLevel}/{xpNeeded} XP";
        }
    }

    public void AddXp(int amount)
    {
        if (amount <= 0 || _currentLevel >= MaxLevel) return;

        _totalXp += amount;
        _preferences.Set(XpKey, _totalXp);

        var newLevel = CalculateLevelFromXp(_totalXp);
        if (newLevel > _currentLevel)
        {
            _currentLevel = newLevel;
            _preferences.Set(LevelKey, _currentLevel);
            LevelUp?.Invoke(_currentLevel);
        }
    }

    /// <summary>
    /// Kumulative XP für ein bestimmtes Level.
    /// </summary>
    private static int XpForLevel(int level) => 100 * level * (level + 1) / 2;

    /// <summary>
    /// Berechnet Level aus Gesamt-XP.
    /// </summary>
    private static int CalculateLevelFromXp(int xp)
    {
        var level = 1;
        while (level < MaxLevel && xp >= XpForLevel(level + 1))
            level++;
        return level;
    }
}
