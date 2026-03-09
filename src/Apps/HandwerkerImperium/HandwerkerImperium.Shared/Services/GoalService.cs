using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Services;

/// <summary>
/// Berechnet dynamisch das nächste empfohlene Ziel für den Spieler.
/// Priorisiert nach: Meilenstein nahe → Prestige verfügbar → Neuer Workshop → Gebäude.
/// </summary>
public sealed class GoalService : IGoalService
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

        // 0. Anfänger-Ziele (Level <10) — gibt neuen Spielern klare Richtung
        AddBeginnerGoals(state, goals);

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
                NavigationRoute = "prestige",
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

        // 4. Gebäude-Upgrade (wenn erschwinglich und Imperium-Tab freigeschaltet Lv.5)
        if (state.PlayerLevel >= 5)
        {
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
        }

        // Bestes Ziel nach Priorität zurückgeben
        return goals.OrderBy(g => g.Priority).FirstOrDefault();
    }

    /// <summary>
    /// Anfänger-Ziele für Spieler unter Level 10:
    /// Klare, erreichbare Ziele die den Spieler durch die ersten Minuten führen.
    /// Priorität 0 = höchste, damit sie vor Meilenstein-Zielen erscheinen.
    /// </summary>
    private void AddBeginnerGoals(GameState state, List<GameGoal> goals)
    {
        // Nur für Anfänger (Level <10) und nicht nach Prestige (erfahrene Spieler)
        if (state.PlayerLevel >= 10) return;
        if (state.Prestige.TotalPrestigeCount > 0) return;

        var firstWorkshop = state.Workshops.FirstOrDefault(w => w.IsUnlocked);

        // Ziel: Erste Werkstatt upgraden (kein Upgrade gemacht, Werkstatt auf Level 1)
        if (firstWorkshop != null && firstWorkshop.Level <= 1 && state.TotalMoneySpent == 0)
        {
            var wsName = _localizationService.GetString(firstWorkshop.Type.GetLocalizationKey()) ?? firstWorkshop.Type.ToString();
            goals.Add(new GameGoal
            {
                Description = $"{wsName} {_localizationService.GetString("GoalUpgrade") ?? "upgraden"}",
                RewardHint = _localizationService.GetString("GoalMoreIncome") ?? "+Einkommen!",
                Progress = 0.0,
                NavigationRoute = "dashboard",
                IconKind = "ArrowUpBold",
                Priority = 0
            });
            return; // Nur ein Anfänger-Ziel gleichzeitig
        }

        // Ziel: Ersten Auftrag annehmen (noch nie einen Auftrag abgeschlossen)
        if (state.TotalOrdersCompleted == 0)
        {
            goals.Add(new GameGoal
            {
                Description = _localizationService.GetString("GoalFirstOrder") ?? "Ersten Auftrag annehmen",
                RewardHint = _localizationService.GetString("GoalEarnMoney") ?? "Geld verdienen!",
                Progress = 0.0,
                NavigationRoute = "dashboard",
                IconKind = "ClipboardText",
                Priority = 0
            });
            return;
        }

        // Ziel: Werkstatt auf Level 10 (erster Meilenstein-Annäherung, breiter als diff<=5)
        if (firstWorkshop != null && firstWorkshop.Level < 10)
        {
            goals.Add(new GameGoal
            {
                Description = $"{_localizationService.GetString(firstWorkshop.Type.GetLocalizationKey()) ?? "Werkstatt"} → Lv.10",
                RewardHint = _localizationService.GetString("GoalUnlockFeatures") ?? "Neue Features!",
                Progress = firstWorkshop.Level / 10.0,
                NavigationRoute = "dashboard",
                IconKind = "TrendingUp",
                Priority = 0
            });
            return;
        }

        // Ziel: Nächstes Spieler-Level (Level 5 oder 10 je nach aktuellem Stand)
        int targetLevel = state.PlayerLevel < 5 ? 5 : 10;
        int xpForTarget = GameState.CalculateXpForLevel(targetLevel);
        int xpCurrent = state.TotalXp;
        double progress = xpForTarget > 0 ? Math.Min(1.0, (double)xpCurrent / xpForTarget) : 0;

        goals.Add(new GameGoal
        {
            Description = string.Format(_localizationService.GetString("GoalReachLevel") ?? "Erreiche Level {0}", targetLevel),
            RewardHint = _localizationService.GetString("GoalUnlockFeatures") ?? "Neue Features!",
            Progress = progress,
            NavigationRoute = "dashboard",
            IconKind = "StarFourPoints",
            Priority = 0
        });
    }
}
