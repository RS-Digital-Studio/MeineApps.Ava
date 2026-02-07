using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Manages achievements and tracks progress.
/// </summary>
public class AchievementService : IAchievementService, IDisposable
{
    private bool _disposed;
    private readonly IGameStateService _gameStateService;
    private readonly List<Achievement> _achievements;

    public event EventHandler<Achievement>? AchievementUnlocked;

    public AchievementService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
        _achievements = Achievements.GetAll();

        // Load unlocked status from game state
        LoadFromGameState();

        // Subscribe to game events for automatic tracking
        _gameStateService.OrderCompleted += OnOrderCompleted;
        _gameStateService.LevelUp += OnLevelUp;
        _gameStateService.WorkerHired += OnWorkerHired;
        _gameStateService.WorkshopUpgraded += OnWorkshopUpgraded;
        _gameStateService.MoneyChanged += OnMoneyChanged;
    }

    public int UnlockedCount => _achievements.Count(a => a.IsUnlocked);
    public int TotalCount => _achievements.Count;

    public List<Achievement> GetAllAchievements()
    {
        // Update current values before returning
        UpdateProgress();
        return _achievements.OrderByDescending(a => a.IsUnlocked)
                           .ThenByDescending(a => a.Progress)
                           .ThenBy(a => a.Category)
                           .ToList();
    }

    public List<Achievement> GetUnlockedAchievements()
    {
        return _achievements.Where(a => a.IsUnlocked)
                           .OrderByDescending(a => a.UnlockedAt)
                           .ToList();
    }

    public Achievement? GetAchievement(string id)
    {
        return _achievements.FirstOrDefault(a => a.Id == id);
    }

    public void Reset()
    {
        foreach (var achievement in _achievements)
        {
            achievement.IsUnlocked = false;
            achievement.UnlockedAt = null;
            achievement.CurrentValue = 0;
        }
        LoadFromGameState();
    }

    public void CheckAchievements()
    {
        UpdateProgress();

        foreach (var achievement in _achievements.Where(a => !a.IsUnlocked))
        {
            if (achievement.CurrentValue >= achievement.TargetValue)
            {
                UnlockAchievement(achievement);
            }
        }
    }

    private void LoadFromGameState()
    {
        var unlockedIds = _gameStateService.State.UnlockedAchievements ?? [];

        foreach (var achievement in _achievements)
        {
            if (unlockedIds.Contains(achievement.Id))
            {
                achievement.IsUnlocked = true;
            }
        }

        UpdateProgress();
    }

    private void UpdateProgress()
    {
        var state = _gameStateService.State;

        foreach (var achievement in _achievements)
        {
            achievement.CurrentValue = achievement.Id switch
            {
                // Orders
                "first_order" or "orders_10" or "orders_50" or "orders_100" or "orders_500"
                    => state.TotalOrdersCompleted,

                // Mini-Games
                "perfect_first" or "perfect_10" or "perfect_50"
                    => state.PerfectRatings,
                "streak_5" or "streak_10"
                    => state.BestPerfectStreak,
                "games_100"
                    => state.TotalMiniGamesPlayed,

                // Workshops
                "workshop_level10" or "workshop_level25"
                    => state.Workshops.Count > 0 ? state.Workshops.Max(w => w.Level) : 0,
                "all_workshops"
                    => state.Workshops.Count,
                "worker_first"
                    => state.Workshops.Sum(w => w.Workers.Count) > 0 ? 1 : 0,
                "workers_10" or "workers_25"
                    => state.Workshops.Sum(w => w.Workers.Count),

                // Money
                "money_1k" or "money_10k" or "money_100k" or "money_1m"
                    => (int)state.TotalMoneyEarned,

                // Time
                "play_1h" or "play_10h"
                    => (int)state.TotalPlayTimeSeconds,
                "daily_7"
                    => state.DailyRewardStreak,

                // Special
                "level_10" or "level_25" or "level_50"
                    => state.PlayerLevel,
                "prestige_1"
                    => state.PrestigeLevel,

                _ => 0
            };
        }
    }

    private void UnlockAchievement(Achievement achievement)
    {
        if (achievement.IsUnlocked) return;

        achievement.IsUnlocked = true;
        achievement.UnlockedAt = DateTime.UtcNow;
        achievement.CurrentValue = achievement.TargetValue;

        // Save to game state
        _gameStateService.State.UnlockedAchievements ??= [];
        if (!_gameStateService.State.UnlockedAchievements.Contains(achievement.Id))
        {
            _gameStateService.State.UnlockedAchievements.Add(achievement.Id);
        }

        // Apply rewards
        if (achievement.MoneyReward > 0)
        {
            _gameStateService.AddMoney(achievement.MoneyReward);
        }

        if (achievement.XpReward > 0)
        {
            _gameStateService.AddXp(achievement.XpReward);
        }

        if (achievement.GoldenScrewReward > 0)
        {
            _gameStateService.AddGoldenScrews(achievement.GoldenScrewReward);
        }

        // Notify listeners
        AchievementUnlocked?.Invoke(this, achievement);
    }

    // Event handlers for automatic tracking
    private void OnOrderCompleted(object? sender, Models.Events.OrderCompletedEventArgs e)
    {
        CheckAchievements();
    }

    private void OnLevelUp(object? sender, Models.Events.LevelUpEventArgs e)
    {
        CheckAchievements();
    }

    private void OnWorkerHired(object? sender, Models.Events.WorkerHiredEventArgs e)
    {
        CheckAchievements();
    }

    private void OnWorkshopUpgraded(object? sender, Models.Events.WorkshopUpgradedEventArgs e)
    {
        CheckAchievements();
    }

    private void OnMoneyChanged(object? sender, Models.Events.MoneyChangedEventArgs e)
    {
        // Only check when money increases (earnings)
        if (e.NewAmount > e.OldAmount)
        {
            CheckAchievements();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _gameStateService.OrderCompleted -= OnOrderCompleted;
        _gameStateService.LevelUp -= OnLevelUp;
        _gameStateService.WorkerHired -= OnWorkerHired;
        _gameStateService.WorkshopUpgraded -= OnWorkshopUpgraded;
        _gameStateService.MoneyChanged -= OnMoneyChanged;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
