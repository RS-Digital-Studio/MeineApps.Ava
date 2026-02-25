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

    private int _unlockedCount;
    public int UnlockedCount => _unlockedCount;
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
        _unlockedCount = 0;

        foreach (var achievement in _achievements)
        {
            if (unlockedIds.Contains(achievement.Id))
            {
                achievement.IsUnlocked = true;
                _unlockedCount++;
            }
        }

        UpdateProgress();
    }

    private void UpdateProgress()
    {
        var state = _gameStateService.State;

        // Vorab-Berechnung aller abgeleiteten Werte (vermeidet LINQ-Kaskaden im switch)
        int maxWsLevel = 0, totalWorkers = 0, wsLevel100Count = 0;
        int builtCount = 0, maxBldLevel = 0;
        bool hasCanteen = false, hasTraining = false;
        bool hasSS = false, hasSSS = false, hasLegendary = false;

        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var ws = state.Workshops[i];
            if (ws.Level > maxWsLevel) maxWsLevel = ws.Level;
            if (ws.Level >= 100) wsLevel100Count++;
            totalWorkers += ws.Workers.Count;
            for (int w = 0; w < ws.Workers.Count; w++)
            {
                var tier = ws.Workers[w].Tier;
                if (tier >= WorkerTier.Legendary) hasLegendary = true;
                else if (tier >= WorkerTier.SSS) hasSSS = true;
                else if (tier >= WorkerTier.SS) hasSS = true;
            }
        }
        // SS/SSS implizieren niedrigere Tiers
        if (hasLegendary) { hasSSS = true; hasSS = true; }
        else if (hasSSS) hasSS = true;

        for (int i = 0; i < state.Buildings.Count; i++)
        {
            var bld = state.Buildings[i];
            if (bld.IsBuilt)
            {
                builtCount++;
                if (bld.Level > maxBldLevel) maxBldLevel = bld.Level;
                if (bld.Type == BuildingType.Canteen) hasCanteen = true;
                if (bld.Type == BuildingType.TrainingCenter) hasTraining = true;
            }
        }

        // Equipment-Raritäten zählen (HashSet vermeiden, 4 Raritäten → Bitflags)
        int rarityFlags = 0;
        for (int i = 0; i < state.EquipmentInventory.Count; i++)
            rarityFlags |= 1 << (int)state.EquipmentInventory[i].Rarity;
        int distinctRarities = 0;
        for (int b = rarityFlags; b != 0; b &= b - 1) distinctRarities++;

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

                // Workshops (vorab berechnet)
                "workshop_level10" or "workshop_level25" or "workshop_level50"
                or "workshop_level100" or "workshop_level250" or "workshop_level500" or "workshop_level1000"
                    => maxWsLevel,
                "all_workshops"
                    => state.UnlockedWorkshopTypes.Count,
                "worker_first"
                    => totalWorkers > 0 ? 1 : 0,
                "workers_10" or "workers_25"
                    => totalWorkers,

                // Buildings (vorab berechnet)
                "building_first"
                    => builtCount > 0 ? 1 : 0,
                "building_all"
                    => builtCount,
                "building_max"
                    => maxBldLevel,
                "canteen_built"
                    => hasCanteen ? 1 : 0,
                "training_center"
                    => hasTraining ? 1 : 0,

                // Money (long statt int-Cast für große Beträge)
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

                // Worker-Tier Achievements (vorab berechnet)
                "worker_ss_tier" => hasSS ? 1 : 0,
                "worker_sss_tier" => hasSSS ? 1 : 0,
                "worker_legendary" => hasLegendary ? 1 : 0,

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

                // Workshop-Endgame (vorab berechnet)
                "all_ws_level100" => wsLevel100Count,

                // Gilden
                "guild_founder" => state.GuildMembership != null ? 1 : 0,
                "guild_member" => state.GuildMembership != null ? 1 : 0,
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
                "equipment_all_rarities" => distinctRarities,

                _ => achievement.CurrentValue
            };
        }
    }

    private void UnlockAchievement(Achievement achievement)
    {
        if (achievement.IsUnlocked) return;

        _unlockedCount++;
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
