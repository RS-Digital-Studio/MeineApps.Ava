using System;
using System.Collections.Generic;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verwaltet Vorarbeiter/Manager: Prüft Freischalt-Bedingungen (Level, Prestige, Perfect-Ratings),
/// ermöglicht Upgrades (max Level 5) und berechnet Workshop-spezifische sowie globale Boni.
/// </summary>
public sealed class ManagerService : IManagerService
{
    private readonly IGameStateService _gameStateService;
    private readonly IAnalyticsService? _analyticsService;

    // Gecachte Manager-Bonus-Summen (analog Prestige-/MasterTool-Cache). Die Summen aendern sich
    // NUR bei CheckAndUnlockManagers (neuer Manager), UpgradeManager (Level++) und State-Load
    // (Prestige/Ascension/Import tauschen den GameState). Sie werden aber pro Tick mehrfach
    // abgefragt (IncomeCalculator: O(Workshops) + 2 global; WorkerService: 3 global + 3×O(Workshops)).
    // Lazy-Rebuild auf Dirty-Flag, danach O(1)-Lookup. Bit-identisch: gleiche Summanden, gleiche
    // Reihenfolge (state.Managers-Reihenfolge) wie die fruehere Per-Call-Schleife.
    private bool _bonusCacheDirty = true;
    private readonly Dictionary<(WorkshopType, ManagerAbility), decimal> _workshopBonusCache = new();
    private readonly Dictionary<ManagerAbility, decimal> _globalBonusCache = new();

    public event Action<string>? ManagerUnlocked;

    public ManagerService(IGameStateService gameStateService, IAnalyticsService? analyticsService = null)
    {
        _gameStateService = gameStateService;
        _analyticsService = analyticsService;

        // Bei State-Wechsel (Load/Import/Reset/Prestige/Ascension) Cache invalidieren — sonst
        // liefern die Bonus-Summen Werte aus der alten Manager-Liste (analog PrestigeService).
        _gameStateService.StateLoaded += (_, _) => _bonusCacheDirty = true;
    }

    public void CheckAndUnlockManagers()
    {
        var state = _gameStateService.State;
        var definitions = Manager.GetAllDefinitions();

        for (int d = 0; d < definitions.Count; d++)
        {
            var def = definitions[d];
            // Bereits freigeschaltet? (For-Schleife statt LINQ .Any())
            bool alreadyUnlocked = false;
            for (int m = 0; m < state.Managers.Count; m++)
            {
                if (state.Managers[m].Id == def.Id) { alreadyUnlocked = true; break; }
            }
            if (alreadyUnlocked) continue;

            // Bedingungen prüfen
            if (!IsEligible(def, state))
                continue;

            // Neuen Manager freischalten
            var manager = new Manager
            {
                Id = def.Id,
                Level = 1,
                IsUnlocked = true
            };

            state.Managers.Add(manager);
            _bonusCacheDirty = true; // Neuer Manager → Bonus-Summen neu berechnen

            ManagerUnlocked?.Invoke(def.Id);

            // Manager-Unlock ist Mid-Game-Meilenstein.
            _analyticsService?.TrackEvent(AnalyticsEvents.ManagerUnlocked, new Dictionary<string, object?>
            {
                ["manager_id"] = def.Id,
                ["player_level"] = state.PlayerLevel,
                ["total_managers"] = state.Managers.Count
            });
        }
    }

    public void UpgradeManager(string managerId)
    {
        var state = _gameStateService.State;
        Manager? manager = null;
        for (int i = 0; i < state.Managers.Count; i++)
        {
            if (state.Managers[i].Id == managerId) { manager = state.Managers[i]; break; }
        }

        if (manager == null || !manager.IsUnlocked || manager.IsMaxLevel)
            return;

        int cost = manager.UpgradeCost;
        if (!_gameStateService.TrySpendGoldenScrews(cost))
            return;

        manager.Level++;
        _bonusCacheDirty = true; // Level geaendert → Bonus-Summen neu berechnen
    }

    public decimal GetManagerBonusForWorkshop(WorkshopType type, ManagerAbility ability)
    {
        if (_bonusCacheDirty) RebuildBonusCache();
        return _workshopBonusCache.GetValueOrDefault((type, ability), 0m);
    }

    public decimal GetGlobalManagerBonus(ManagerAbility ability)
    {
        if (_bonusCacheDirty) RebuildBonusCache();
        return _globalBonusCache.GetValueOrDefault(ability, 0m);
    }

    public void InvalidateBonusCache() => _bonusCacheDirty = true;

    /// <summary>
    /// Baut die Bonus-Summen-Caches aus der aktuellen Manager-Liste neu auf. Pro Manager genau
    /// EIN <see cref="Manager.GetDefinitionById"/>-Lookup (statt je Abfrage einer in der Schleife
    /// und einer in <see cref="Manager.GetBonus"/>). Jeder Manager traegt nur zu seiner eigenen
    /// <see cref="ManagerDefinition.Ability"/> bei — exakt wie die frueheren Per-Call-Schleifen,
    /// daher bit-identische Summen.
    /// </summary>
    private void RebuildBonusCache()
    {
        _workshopBonusCache.Clear();
        _globalBonusCache.Clear();

        var state = _gameStateService.State;
        for (int i = 0; i < state.Managers.Count; i++)
        {
            var manager = state.Managers[i];
            if (!manager.IsUnlocked) continue;

            var def = Manager.GetDefinitionById(manager.Id);
            if (def == null) continue;

            // Ein Manager liefert nur fuer seine eigene Faehigkeit einen Bonus (GetBonus gibt fuer
            // jede andere Ability 0 zurueck) — also genau in den Bucket (def.Workshop, def.Ability).
            decimal bonus = manager.GetBonus(def.Ability);
            if (bonus == 0m) continue;

            if (def.Workshop is { } ws)
            {
                var key = (ws, def.Ability);
                _workshopBonusCache[key] = _workshopBonusCache.GetValueOrDefault(key, 0m) + bonus;
            }
            else
            {
                _globalBonusCache[def.Ability] = _globalBonusCache.GetValueOrDefault(def.Ability, 0m) + bonus;
            }
        }

        _bonusCacheDirty = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Prüft ob die Freischalt-Bedingungen für einen Manager erfüllt sind.
    /// </summary>
    private static bool IsEligible(ManagerDefinition def, GameState state)
    {
        // Level-Anforderung
        if (def.RequiredLevel > 0 && state.PlayerLevel < def.RequiredLevel)
            return false;

        // Prestige-Anforderung
        if (def.RequiredPrestige > 0 && state.Prestige.TotalPrestigeCount < def.RequiredPrestige)
            return false;

        // Perfect-Ratings-Anforderung
        if (def.RequiredPerfectRatings > 0 && state.Statistics.PerfectRatings < def.RequiredPerfectRatings)
            return false;

        return true;
    }
}
