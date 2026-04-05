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

    public event Action<string>? ManagerUnlocked;

    public ManagerService(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
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

            ManagerUnlocked?.Invoke(def.Id);
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
    }

    public decimal GetManagerBonusForWorkshop(WorkshopType type, ManagerAbility ability)
    {
        var state = _gameStateService.State;
        decimal totalBonus = 0m;

        for (int i = 0; i < state.Managers.Count; i++)
        {
            var manager = state.Managers[i];
            if (!manager.IsUnlocked) continue;

            // O(1) Dictionary-Lookup statt FirstOrDefault über 14 Definitionen
            var def = Manager.GetDefinitionById(manager.Id);
            if (def == null || def.Workshop != type)
                continue;

            totalBonus += manager.GetBonus(ability);
        }

        return totalBonus;
    }

    public decimal GetGlobalManagerBonus(ManagerAbility ability)
    {
        var state = _gameStateService.State;
        decimal totalBonus = 0m;

        for (int i = 0; i < state.Managers.Count; i++)
        {
            var manager = state.Managers[i];
            if (!manager.IsUnlocked) continue;

            // Nur globale Manager (Workshop = null)
            var def = Manager.GetDefinitionById(manager.Id);
            if (def == null || def.Workshop != null)
                continue;

            totalBonus += manager.GetBonus(ability);
        }

        return totalBonus;
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
