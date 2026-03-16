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
        // Direkte "bester Kandidat"-Akkumulation statt List + OrderBy + FirstOrDefault
        GameGoal? bestGoal = null;

        // 0. Anfänger-Ziele (Level <10) — gibt neuen Spielern klare Richtung
        bestGoal = FindBeginnerGoal(state);
        if (bestGoal != null) return bestGoal; // Priorität 0 = höchste, sofort zurückgeben

        // 1. Workshop-Meilenstein nahe (höchste Prio wenn <5 Level entfernt)
        int[] milestones = [25, 50, 75, 100, 150, 200, 225, 250, 350, 500, 1000];
        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var ws = state.Workshops[i];
            if (!ws.IsUnlocked) continue;
            for (int m = 0; m < milestones.Length; m++)
            {
                int diff = milestones[m] - ws.Level;
                if (diff > 0 && diff <= 5)
                {
                    decimal mult = Workshop.GetMilestoneMultiplierForLevel(milestones[m]);
                    var wsName = _localizationService.GetString(ws.Type.GetLocalizationKey()) ?? ws.Type.ToString();
                    var goal = new GameGoal
                    {
                        Description = $"{wsName} → Lv.{milestones[m]}",
                        RewardHint = $"x{mult:0.#} {_localizationService.GetString("IncomeBoost") ?? "Einkommens-Boost"}!",
                        Progress = (double)ws.Level / milestones[m],
                        NavigationRoute = "dashboard",
                        IconKind = "TrendingUp",
                        Priority = 1
                    };
                    if (bestGoal == null || goal.Priority < bestGoal.Priority)
                        bestGoal = goal;
                    break; // Nur nächsten Meilenstein pro Workshop
                }
            }
        }

        // Bei Priorität 1 schon gefunden → weiter prüfen ob niedrigere existiert (nein, 1 ist max)
        if (bestGoal != null && bestGoal.Priority <= 1)
            return bestGoal;

        // 2. Prestige verfügbar
        var highestTier = state.Prestige.GetHighestAvailableTier(state.PlayerLevel);
        if (highestTier != PrestigeTier.None)
        {
            var points = _prestigeService.GetPrestigePoints(state.TotalMoneyEarned);
            int tierPoints = (int)(points * highestTier.GetPointMultiplier());
            var goal = new GameGoal
            {
                Description = _localizationService.GetString("PrestigeAvailable") ?? "Prestige verfügbar!",
                RewardHint = $"+{tierPoints} {_localizationService.GetString("PrestigePointsShort") ?? "PP"}",
                Progress = 1.0,
                NavigationRoute = "prestige",
                IconKind = "StarFourPoints",
                Priority = 2
            };
            if (bestGoal == null || goal.Priority < bestGoal.Priority)
                bestGoal = goal;
        }

        if (bestGoal != null && bestGoal.Priority <= 2)
            return bestGoal;

        // 3. Nächster Workshop freischaltbar (günstigsten zuerst finden, ohne LINQ OrderBy)
        Workshop? cheapestLocked = null;
        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var ws = state.Workshops[i];
            if (ws.IsUnlocked) continue;
            if (cheapestLocked == null || ws.UnlockCost < cheapestLocked.UnlockCost)
                cheapestLocked = ws;
        }
        if (cheapestLocked != null)
        {
            decimal remaining = cheapestLocked.UnlockCost - state.Money;
            if (remaining > 0 && remaining < state.Money * 5)
            {
                var wsName = _localizationService.GetString(cheapestLocked.Type.GetLocalizationKey()) ?? cheapestLocked.Type.ToString();
                var goal = new GameGoal
                {
                    Description = $"{wsName} {_localizationService.GetString("Unlock") ?? "freischalten"}",
                    RewardHint = $"x{cheapestLocked.Type.GetBaseIncomeMultiplier():0.#} {_localizationService.GetString("Income") ?? "Einkommen"}",
                    Progress = Math.Min(1.0, (double)(state.Money / cheapestLocked.UnlockCost)),
                    NavigationRoute = "dashboard",
                    IconKind = "LockOpenVariant",
                    Priority = 3
                };
                if (bestGoal == null || goal.Priority < bestGoal.Priority)
                    bestGoal = goal;
            }
        }

        if (bestGoal != null && bestGoal.Priority <= 3)
            return bestGoal;

        // 4. Gebäude-Upgrade (wenn erschwinglich und Imperium-Tab freigeschaltet Lv.5)
        if (state.PlayerLevel >= 5)
        {
            for (int i = 0; i < state.Buildings.Count; i++)
            {
                var building = state.Buildings[i];
                if (!building.IsBuilt || building.Level >= 5) continue;

                var upgradeCost = building.NextLevelCost;
                if (state.Money >= upgradeCost * 0.5m)
                {
                    var bName = _localizationService.GetString(building.Type.GetLocalizationKey()) ?? building.Type.ToString();
                    var goal = new GameGoal
                    {
                        Description = $"{bName} → Lv.{building.Level + 1}",
                        RewardHint = _localizationService.GetString("BuildingUpgradeHint") ?? "Bessere Boni!",
                        Progress = Math.Min(1.0, (double)(state.Money / upgradeCost)),
                        NavigationRoute = "imperium",
                        IconKind = "HomeCity",
                        Priority = 4
                    };
                    if (bestGoal == null || goal.Priority < bestGoal.Priority)
                        bestGoal = goal;
                    break; // Nur ein Gebäude vorschlagen
                }
            }
        }

        // 5. Nächster Worker-Tier (wenn Spieler sich bald besseren Worker leisten kann)
        if (bestGoal == null)
        {
            // Höchsten Tier aller aktuellen Worker finden
            var highestWorkerTier = WorkerTier.F;
            for (int i = 0; i < state.Workshops.Count; i++)
            {
                if (!state.Workshops[i].IsUnlocked) continue;
                for (int w = 0; w < state.Workshops[i].Workers.Count; w++)
                {
                    if (state.Workshops[i].Workers[w].Tier > highestWorkerTier)
                        highestWorkerTier = state.Workshops[i].Workers[w].Tier;
                }
            }

            // Nächsten erreichbaren Tier prüfen
            var nextTier = (WorkerTier)Math.Min((int)highestWorkerTier + 1, (int)WorkerTier.Legendary);
            if (nextTier > highestWorkerTier && state.PlayerLevel >= nextTier.GetUnlockLevel())
            {
                decimal hiringCost = nextTier.GetHiringCost(state.PlayerLevel);
                if (state.Money >= hiringCost * 0.3m && state.Money < hiringCost * 3m)
                {
                    var tierName = _localizationService.GetString(nextTier.GetLocalizationKey()) ?? nextTier.ToString();
                    bestGoal = new GameGoal
                    {
                        Description = $"{tierName}-{_localizationService.GetString("Worker") ?? "Arbeiter"}",
                        RewardHint = $"{nextTier.GetMinEfficiency():0.#}-{nextTier.GetMaxEfficiency():0.#}x {_localizationService.GetString("Efficiency") ?? "Effizienz"}",
                        Progress = Math.Min(1.0, (double)(state.Money / hiringCost)),
                        NavigationRoute = "workers",
                        IconKind = "AccountArrowUp",
                        Priority = 5
                    };
                }
            }
        }

        return bestGoal;
    }

    /// <summary>
    /// Sucht das passende Anfänger-Ziel (Priorität 0).
    /// Extrahiert aus AddBeginnerGoals für allokationsfreie Logik.
    /// </summary>
    private GameGoal? FindBeginnerGoal(GameState state)
    {
        if (state.PlayerLevel >= 10) return null;
        if (state.Prestige.TotalPrestigeCount > 0) return null;

        Workshop? firstWorkshop = null;
        for (int i = 0; i < state.Workshops.Count; i++)
        {
            if (state.Workshops[i].IsUnlocked)
            {
                firstWorkshop = state.Workshops[i];
                break;
            }
        }

        // Ziel: Erste Werkstatt upgraden
        if (firstWorkshop != null && firstWorkshop.Level <= 1 && state.TotalMoneySpent == 0)
        {
            var wsName = _localizationService.GetString(firstWorkshop.Type.GetLocalizationKey()) ?? firstWorkshop.Type.ToString();
            return new GameGoal
            {
                Description = $"{wsName} {_localizationService.GetString("GoalUpgrade") ?? "upgraden"}",
                RewardHint = _localizationService.GetString("GoalMoreIncome") ?? "+Einkommen!",
                Progress = 0.0,
                NavigationRoute = "dashboard",
                IconKind = "ArrowUpBold",
                Priority = 0
            };
        }

        // Ziel: Ersten Auftrag annehmen
        if (state.TotalOrdersCompleted == 0)
        {
            return new GameGoal
            {
                Description = _localizationService.GetString("GoalFirstOrder") ?? "Ersten Auftrag annehmen",
                RewardHint = _localizationService.GetString("GoalEarnMoney") ?? "Geld verdienen!",
                Progress = 0.0,
                NavigationRoute = "dashboard",
                IconKind = "ClipboardText",
                Priority = 0
            };
        }

        // Ziel: Werkstatt auf Level 10
        if (firstWorkshop != null && firstWorkshop.Level < 10)
        {
            return new GameGoal
            {
                Description = $"{_localizationService.GetString(firstWorkshop.Type.GetLocalizationKey()) ?? "Werkstatt"} → Lv.10",
                RewardHint = _localizationService.GetString("GoalUnlockFeatures") ?? "Neue Features!",
                Progress = firstWorkshop.Level / 10.0,
                NavigationRoute = "dashboard",
                IconKind = "TrendingUp",
                Priority = 0
            };
        }

        // Ziel: Nächstes Spieler-Level
        int targetLevel = state.PlayerLevel < 5 ? 5 : 10;
        int xpForTarget = GameState.CalculateXpForLevel(targetLevel);
        int xpCurrent = state.TotalXp;
        double progress = xpForTarget > 0 ? Math.Min(1.0, (double)xpCurrent / xpForTarget) : 0;

        return new GameGoal
        {
            Description = string.Format(_localizationService.GetString("GoalReachLevel") ?? "Erreiche Level {0}", targetLevel),
            RewardHint = _localizationService.GetString("GoalUnlockFeatures") ?? "Neue Features!",
            Progress = progress,
            NavigationRoute = "dashboard",
            IconKind = "StarFourPoints",
            Priority = 0
        };
    }

    // AddBeginnerGoals wurde durch FindBeginnerGoal ersetzt (keine List-Allokation)
}
