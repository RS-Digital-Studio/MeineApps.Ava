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

    public bool BoostAchievement(string achievementId, double boostPercent)
    {
        var achievement = _achievements.FirstOrDefault(a => a.Id == achievementId);
        if (achievement == null || achievement.IsUnlocked || achievement.HasUsedAdBoost)
            return false;

        // Fortschritt um boostPercent des Zielwerts erhoehen
        var boost = (long)(achievement.TargetValue * boostPercent);
        if (boost < 1) boost = 1;
        achievement.CurrentValue += boost;
        achievement.HasUsedAdBoost = true;

        // Pruefen ob Achievement jetzt freigeschaltet
        if (achievement.CurrentValue >= achievement.TargetValue)
        {
            UnlockAchievement(achievement);
        }

        _gameStateService.MarkDirty();
        return true;
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
                "workshop_level10" or "workshop_level25" or "workshop_level50"
                or "workshop_level100" or "workshop_level250" or "workshop_level500" or "workshop_level1000"
                    => state.Workshops.Count > 0 ? state.Workshops.Max(w => w.Level) : 0,
                "all_workshops"
                    => state.UnlockedWorkshopTypes.Count,
                "worker_first"
                    => state.Workshops.Sum(w => w.Workers.Count) > 0 ? 1 : 0,
                "workers_10" or "workers_25"
                    => state.Workshops.Sum(w => w.Workers.Count),

                // Buildings
                "building_first"
                    => state.Buildings.Count(b => b.IsBuilt) > 0 ? 1 : 0,
                "building_all"
                    => state.Buildings.Count(b => b.IsBuilt),
                "building_max"
                    => state.Buildings.Count > 0
                        ? state.Buildings.Where(b => b.IsBuilt).DefaultIfEmpty().Max(b => b?.Level ?? 0)
                        : 0,
                "canteen_built"
                    => state.Buildings.Any(b => b.Type == BuildingType.Canteen && b.IsBuilt) ? 1 : 0,
                "training_center"
                    => state.Buildings.Any(b => b.Type == BuildingType.TrainingCenter && b.IsBuilt) ? 1 : 0,

                // Money (long statt int-Cast fuer grosse Betraege)
                "money_1k" or "money_10k" or "money_100k" or "money_1m"
                or "money_10m" or "money_100m" or "money_1b" or "money_10b"
                    => (long)Math.Min(state.TotalMoneyEarned, long.MaxValue),

                // Time
                "play_1h" or "play_10h"
                    => (long)state.TotalPlayTimeSeconds,
                "daily_7"
                    => state.DailyRewardStreak,

                // Special (Level + Prestige)
                "level_10" or "level_25" or "level_50"
                or "level_100" or "level_250" or "level_500" or "level_1000"
                    => state.PlayerLevel,
                "prestige_1"
                    => state.Prestige.TotalPrestigeCount,

                // Worker-Tier Achievements: Prüft ob ein Worker des entsprechenden Tiers existiert
                "worker_ss_tier"
                    => state.Workshops.SelectMany(w => w.Workers).Any(w => w.Tier >= WorkerTier.SS) ? 1 : 0,
                "worker_sss_tier"
                    => state.Workshops.SelectMany(w => w.Workers).Any(w => w.Tier >= WorkerTier.SSS) ? 1 : 0,
                "worker_legendary"
                    => state.Workshops.SelectMany(w => w.Workers).Any(w => w.Tier >= WorkerTier.Legendary) ? 1 : 0,

                // === NEUE ACHIEVEMENTS (Phase 2.2) ===

                // Prestige-Tier
                "prestige_platin" => state.Prestige.PlatinCount,
                "prestige_diamant" => state.Prestige.DiamantCount,
                "prestige_meister" => state.Prestige.MeisterCount,
                "prestige_legende" => state.Prestige.LegendeCount,

                // MiniGame-Mastery
                "perfect_100" => state.PerfectRatings,
                "games_500" => state.TotalMiniGamesPlayed,
                "all_minigames_perfect" => state.PerfectMiniGameTypes?.Count ?? 0,

                // Workshop-Endgame: Alle Workshops mit Level >= 100
                "all_ws_level100" => state.Workshops.Count(w => w.Level >= 100),

                // Gilden
                "guild_founder" => state.GuildMembership != null ? 1 : 0, // Vereinfacht: Mitgliedschaft = 1
                "guild_member" => state.GuildMembership != null ? 1 : 0,
                // Guild-Level als Proxy (steigt durch abgeschlossene Wochenziele)
                "guild_weekly_goal" => state.GuildMembership?.GuildLevel > 1 ? 1 : 0,
                "guild_level_10" => state.GuildMembership?.GuildLevel ?? 0,

                // Worker-Training
                "workers_trained_50" => state.TotalWorkersTrained,

                // Crafting
                "crafting_100" => state.TotalItemsCrafted,
                "all_recipes" => state.CompletedRecipeIds?.Count ?? 0,

                // Turniere
                "tournament_gold" => state.TotalTournamentsWon > 0 ? 1 : 0,
                "tournaments_10" => state.TotalTournamentsWon,

                // Sammler/Collection
                "all_mastertools" => state.CollectedMasterTools?.Count ?? 0,
                "equipment_all_rarities" => state.EquipmentInventory
                    .Select(e => e.Rarity).Distinct().Count(),

                _ => achievement.CurrentValue
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
