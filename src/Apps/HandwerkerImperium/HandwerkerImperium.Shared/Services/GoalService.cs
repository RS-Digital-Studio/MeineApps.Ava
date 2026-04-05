using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Services;

/// <summary>
/// Berechnet dynamisch das nächste empfohlene Ziel für den Spieler.
/// Priorisiert nach: Meilenstein nahe → Prestige/Ascension/Rebirth → Neuer Workshop → Gebäude → Worker → Late-Game-Ziele → Stretch-Goals.
/// </summary>
public sealed class GoalService : IGoalService
{
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private readonly IPrestigeService _prestigeService;
    private readonly IAscensionService _ascensionService;
    private readonly IRebirthService _rebirthService;
    private GameGoal? _cachedGoal;
    private bool _isDirty = true;

    public GoalService(
        IGameStateService gameStateService,
        ILocalizationService localizationService,
        IPrestigeService prestigeService,
        IAscensionService ascensionService,
        IRebirthService rebirthService)
    {
        _gameStateService = gameStateService;
        _localizationService = localizationService;
        _prestigeService = prestigeService;
        _ascensionService = ascensionService;
        _rebirthService = rebirthService;

        // Bei State-Wechsel (Prestige/Import/Reset) Cache invalidieren
        _gameStateService.StateLoaded += (_, _) => Invalidate();
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
            var points = _prestigeService.GetPrestigePoints(state.CurrentRunMoney);
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

        if (bestGoal != null && bestGoal.Priority <= 5)
            return bestGoal;

        // ═══════════════════════════════════════════════════════════════════
        // LATE-GAME-ZIELE (Priorität 6-10, nach den Basis-Zielen)
        // ═══════════════════════════════════════════════════════════════════

        // 6. Workshop-Rebirth verfügbar (hohe Prio, wie Meilenstein)
        bestGoal = FindWorkshopRebirthGoal(state);
        if (bestGoal != null) return bestGoal;

        // 7. Ascension verfügbar (hohe Prio, wie Prestige)
        bestGoal = FindAscensionGoal();
        if (bestGoal != null) return bestGoal;

        // 8. Alle Workshops auf Level 1000 (mittlere Prio)
        bestGoal = FindAllWorkshopsMaxGoal(state);
        if (bestGoal != null) return bestGoal;

        // 9. Nächster Rebirth-Stern (mittlere Prio, Workshop nahe Level 1000)
        bestGoal = FindNextRebirthStarGoal(state);
        if (bestGoal != null) return bestGoal;

        // 10. Endlose Stretch-Goals (niedrigste Prio, Fallback)
        return FindStretchGoal(state);
    }

    // ═══════════════════════════════════════════════════════════════════
    // LATE-GAME-ZIEL-METHODEN
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Workshop-Rebirth: Workshop hat Level >= 995 und Sterne kleiner 5 → Rebirth empfehlen.
    /// </summary>
    private GameGoal? FindWorkshopRebirthGoal(GameState state)
    {
        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var ws = state.Workshops[i];
            if (!ws.IsUnlocked || ws.Level < 995) continue;

            int stars = _rebirthService.GetStars(ws.Type);
            if (stars >= 5) continue;

            var wsName = _localizationService.GetString(ws.Type.GetLocalizationKey()) ?? ws.Type.ToString();
            return new GameGoal
            {
                Description = $"{wsName} {_localizationService.GetString("GoalReadyForRebirth") ?? "bereit für Wiedergeburt!"}",
                RewardHint = $"★{stars + 1} (+{GetRebirthBonusText(stars + 1)})",
                Progress = (double)ws.Level / Workshop.MaxLevel,
                NavigationRoute = "dashboard",
                IconKind = "StarShooting",
                Priority = 6
            };
        }

        return null;
    }

    /// <summary>
    /// Ascension verfügbar: Meta-Prestige kann durchgeführt werden.
    /// </summary>
    private GameGoal? FindAscensionGoal()
    {
        if (!_ascensionService.CanAscend) return null;

        int ap = _ascensionService.CalculateAscensionPoints();
        return new GameGoal
        {
            Description = _localizationService.GetString("GoalAscensionAvailable") ?? "Aufstieg verfügbar!",
            RewardHint = $"+{ap} {_localizationService.GetString("AscensionPointsShort") ?? "AP"}",
            Progress = 1.0,
            NavigationRoute = "prestige",
            IconKind = "ArrowUpBoldCircle",
            Priority = 7
        };
    }

    /// <summary>
    /// Alle Workshops auf Level 1000: Fortschrittsziel wenn mindestens 6 aber nicht alle 8 dort sind.
    /// Zählt nur die 8 Basis-Workshops (ohne Prestige-exklusive MasterSmith/InnovationLab).
    /// </summary>
    private GameGoal? FindAllWorkshopsMaxGoal(GameState state)
    {
        int atMax = 0;
        int totalUnlocked = 0;
        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var ws = state.Workshops[i];
            if (!ws.IsUnlocked) continue;
            totalUnlocked++;
            if (ws.Level >= Workshop.MaxLevel) atMax++;
        }

        // Nur anzeigen wenn mindestens 6 auf Max, aber noch nicht alle
        if (atMax < 6 || atMax >= totalUnlocked || totalUnlocked == 0) return null;

        return new GameGoal
        {
            Description = string.Format(
                _localizationService.GetString("GoalAllWorkshopsMax") ?? "Alle Werkstätten auf Lv.1000 ({0}/{1})",
                atMax, totalUnlocked),
            RewardHint = _localizationService.GetString("GoalMaxRebirthAccess") ?? "Maximale Rebirth-Möglichkeiten!",
            Progress = (double)atMax / totalUnlocked,
            NavigationRoute = "dashboard",
            IconKind = "ChartBar",
            Priority = 8
        };
    }

    /// <summary>
    /// Nächster Rebirth-Stern: Workshop mit Level >= 900, Sterne kleiner 5 → zeigt verbleibende Level.
    /// </summary>
    private GameGoal? FindNextRebirthStarGoal(GameState state)
    {
        Workshop? bestCandidate = null;
        int bestLevel = 0;

        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var ws = state.Workshops[i];
            if (!ws.IsUnlocked || ws.Level < 900) continue;

            int stars = _rebirthService.GetStars(ws.Type);
            if (stars >= 5) continue;

            // Höchsten Level-Kandidat wählen (am nächsten an 1000)
            if (ws.Level > bestLevel)
            {
                bestLevel = ws.Level;
                bestCandidate = ws;
            }
        }

        if (bestCandidate == null) return null;

        int remaining = Workshop.MaxLevel - bestCandidate.Level;
        var wsName = _localizationService.GetString(bestCandidate.Type.GetLocalizationKey()) ?? bestCandidate.Type.ToString();
        return new GameGoal
        {
            Description = string.Format(
                _localizationService.GetString("GoalNextRebirthStar") ?? "{0}: Noch {1} Level bis zum nächsten Stern",
                wsName, remaining),
            RewardHint = _localizationService.GetString("GoalPermanentBonus") ?? "Permanenter Bonus!",
            Progress = (double)bestCandidate.Level / Workshop.MaxLevel,
            NavigationRoute = "dashboard",
            IconKind = "Star",
            Priority = 9
        };
    }

    /// <summary>
    /// Endlose Stretch-Goals als Fallback: Nächste 100er-Stufe oder Rebirth-Sterne sammeln.
    /// </summary>
    private GameGoal? FindStretchGoal(GameState state)
    {
        // Zähle Gesamt-Rebirth-Sterne und Maximum
        int totalStars = 0;
        int maxPossibleStars = 0;
        int lowestMaxLevel = int.MaxValue;
        Workshop? lowestWorkshop = null;

        for (int i = 0; i < state.Workshops.Count; i++)
        {
            var ws = state.Workshops[i];
            if (!ws.IsUnlocked) continue;

            int stars = _rebirthService.GetStars(ws.Type);
            totalStars += stars;
            maxPossibleStars += 5;

            // Workshop mit dem niedrigsten Level finden (für Level-Stretch-Ziel)
            if (ws.Level < lowestMaxLevel)
            {
                lowestMaxLevel = ws.Level;
                lowestWorkshop = ws;
            }
        }

        // Variante A: Rebirth-Sterne sammeln (wenn welche möglich sind)
        if (totalStars > 0 && totalStars < maxPossibleStars)
        {
            return new GameGoal
            {
                Description = string.Format(
                    _localizationService.GetString("GoalCollectRebirthStars") ?? "Sammle Rebirth-Sterne ({0}/{1})",
                    totalStars, maxPossibleStars),
                RewardHint = _localizationService.GetString("GoalUltimatePower") ?? "Ultimative Macht!",
                Progress = maxPossibleStars > 0 ? (double)totalStars / maxPossibleStars : 0,
                NavigationRoute = "dashboard",
                IconKind = "StarCircle",
                Priority = 10
            };
        }

        // Variante B: Nächste 100er-Stufe für den niedrigsten Workshop
        if (lowestWorkshop != null && lowestWorkshop.Level < Workshop.MaxLevel)
        {
            int nextHundred = ((lowestWorkshop.Level / 100) + 1) * 100;
            nextHundred = Math.Min(nextHundred, Workshop.MaxLevel);
            var wsName = _localizationService.GetString(lowestWorkshop.Type.GetLocalizationKey()) ?? lowestWorkshop.Type.ToString();

            return new GameGoal
            {
                Description = string.Format(
                    _localizationService.GetString("GoalNextHundred") ?? "{0} auf Level {1} bringen",
                    wsName, nextHundred),
                RewardHint = _localizationService.GetString("GoalKeepGrowing") ?? "Wachse weiter!",
                Progress = (double)lowestWorkshop.Level / nextHundred,
                NavigationRoute = "dashboard",
                IconKind = "TrendingUp",
                Priority = 10
            };
        }

        return null;
    }

    /// <summary>
    /// Gibt einen kurzen Bonus-Text für die jeweilige Rebirth-Stern-Stufe zurück.
    /// </summary>
    private static string GetRebirthBonusText(int stars) => stars switch
    {
        1 => "+15%",
        2 => "+35%",
        3 => "+60%",
        4 => "+100%",
        5 => "+150%",
        _ => ""
    };

    // ═══════════════════════════════════════════════════════════════════
    // ANFÄNGER-ZIELE
    // ═══════════════════════════════════════════════════════════════════

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
        if (state.Statistics.TotalOrdersCompleted == 0)
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
