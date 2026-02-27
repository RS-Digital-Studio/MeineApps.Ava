using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Services;

/// <summary>
/// Berechnet dynamisch das nächste empfohlene Ziel für den Spieler.
/// Priorisiert nach: Meilenstein nahe → Prestige verfügbar → Neuer Workshop → Gebäude.
/// </summary>
public class GoalService : IGoalService
{
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private readonly IPrestigeService _prestigeService;
    private GameGoal? _cachedGoal;
    private bool _isDirty = true;

    public GoalService(
        IGameStateService gameStateService,
        ILocalizationService localizationService,
        IPrestigeService prestigeService)
    {
        _gameStateService = gameStateService;
        _localizationService = localizationService;
        _prestigeService = prestigeService;
    }

    public GameGoal? GetCurrentGoal()
    {
        if (!_isDirty && _cachedGoal != null) return _cachedGoal;
        _cachedGoal = CalculateBestGoal();
        _isDirty = false;
        return _cachedGoal;
    }

    public void Invalidate() => _isDirty = true;

    private GameGoal? CalculateBestGoal()
    {
        var state = _gameStateService.State;
        var goals = new List<GameGoal>();

        // 1. Workshop-Meilenstein nahe (höchste Prio wenn <5 Level entfernt)
        foreach (var ws in state.Workshops.Where(w => w.IsUnlocked))
        {
            int[] milestones = [25, 50, 100, 250, 500, 1000];
            foreach (int milestone in milestones)
            {
                int diff = milestone - ws.Level;
                if (diff > 0 && diff <= 5)
                {
                    decimal mult = Workshop.GetMilestoneMultiplierForLevel(milestone);
                    var wsName = _localizationService.GetString(ws.Type.GetLocalizationKey()) ?? ws.Type.ToString();
                    goals.Add(new GameGoal
                    {
                        Description = $"{wsName} → Lv.{milestone}",
                        RewardHint = $"x{mult:0.#} {_localizationService.GetString("IncomeBoost") ?? "Einkommens-Boost"}!",
                        Progress = (double)ws.Level / milestone,
                        NavigationRoute = "dashboard",
                        IconKind = "TrendingUp",
                        Priority = 1
                    });
                    break; // Nur nächsten Meilenstein pro Workshop
                }
            }
        }

        // 2. Prestige verfügbar
        var highestTier = state.Prestige.GetHighestAvailableTier(state.PlayerLevel);
        if (highestTier != PrestigeTier.None)
        {
            var points = _prestigeService.GetPrestigePoints(state.TotalMoneyEarned);
            int tierPoints = (int)(points * highestTier.GetPointMultiplier());
            goals.Add(new GameGoal
            {
                Description = _localizationService.GetString("PrestigeAvailable") ?? "Prestige verfügbar!",
                RewardHint = $"+{tierPoints} {_localizationService.GetString("PrestigePointsShort") ?? "PP"}",
                Progress = 1.0,
                NavigationRoute = "imperium",
                IconKind = "StarFourPoints",
                Priority = 2
            });
        }

        // 3. Nächster Workshop freischaltbar (nur wenn halbwegs erreichbar)
        var lockedWorkshops = state.Workshops
            .Where(w => !w.IsUnlocked)
            .OrderBy(w => w.UnlockCost);
        foreach (var nextWs in lockedWorkshops)
        {
            decimal remaining = nextWs.UnlockCost - state.Money;
            if (remaining > 0 && remaining < state.Money * 5)
            {
                var wsName = _localizationService.GetString(nextWs.Type.GetLocalizationKey()) ?? nextWs.Type.ToString();
                goals.Add(new GameGoal
                {
                    Description = $"{wsName} {_localizationService.GetString("Unlock") ?? "freischalten"}",
                    RewardHint = $"x{nextWs.Type.GetBaseIncomeMultiplier():0.#} {_localizationService.GetString("Income") ?? "Einkommen"}",
                    Progress = Math.Min(1.0, (double)(state.Money / nextWs.UnlockCost)),
                    NavigationRoute = "dashboard",
                    IconKind = "LockOpenVariant",
                    Priority = 3
                });
                break; // Nur einen vorschlagen
            }
        }

        // 4. Gebäude-Upgrade (wenn erschwinglich)
        foreach (var building in state.Buildings.Where(b => b.IsBuilt && b.Level < 5))
        {
            var upgradeCost = building.NextLevelCost;
            if (state.Money >= upgradeCost * 0.5m)
            {
                var bName = _localizationService.GetString(building.Type.GetLocalizationKey()) ?? building.Type.ToString();
                goals.Add(new GameGoal
                {
                    Description = $"{bName} → Lv.{building.Level + 1}",
                    RewardHint = _localizationService.GetString("BuildingUpgradeHint") ?? "Bessere Boni!",
                    Progress = Math.Min(1.0, (double)(state.Money / upgradeCost)),
                    NavigationRoute = "imperium",
                    IconKind = "HomeCity",
                    Priority = 4
                });
                break; // Nur ein Gebäude vorschlagen
            }
        }

        // Bestes Ziel nach Priorität zurückgeben
        return goals.OrderBy(g => g.Priority).FirstOrDefault();
    }
}
