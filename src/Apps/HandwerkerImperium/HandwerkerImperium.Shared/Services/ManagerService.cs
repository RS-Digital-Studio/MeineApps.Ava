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

        foreach (var def in definitions)
        {
            // Bereits freigeschaltet?
            if (state.Managers.Any(m => m.Id == def.Id))
                continue;

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
            _gameStateService.MarkDirty();

            ManagerUnlocked?.Invoke(def.Id);
        }
    }

    public void UpgradeManager(string managerId)
    {
        var state = _gameStateService.State;
        var manager = state.Managers.FirstOrDefault(m => m.Id == managerId);

        if (manager == null || !manager.IsUnlocked || manager.IsMaxLevel)
            return;

        int cost = manager.UpgradeCost;
        if (!_gameStateService.TrySpendGoldenScrews(cost))
            return;

        manager.Level++;
        _gameStateService.MarkDirty();
    }

    public decimal GetManagerBonusForWorkshop(WorkshopType type, ManagerAbility ability)
    {
        var state = _gameStateService.State;
        var definitions = Manager.GetAllDefinitions();
        decimal totalBonus = 0m;

        foreach (var manager in state.Managers.Where(m => m.IsUnlocked))
        {
            // Definition finden um den Workshop-Typ zu prüfen
            var def = definitions.FirstOrDefault(d => d.Id == manager.Id);
            if (def == null || def.Workshop != type)
                continue;

            // Bonus für diese Fähigkeit berechnen
            totalBonus += manager.GetBonus(ability);
        }

        return totalBonus;
    }

    public decimal GetGlobalManagerBonus(ManagerAbility ability)
    {
        var state = _gameStateService.State;
        var definitions = Manager.GetAllDefinitions();
        decimal totalBonus = 0m;

        foreach (var manager in state.Managers.Where(m => m.IsUnlocked))
        {
            // Nur globale Manager (Workshop = null)
            var def = definitions.FirstOrDefault(d => d.Id == manager.Id);
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
        if (def.RequiredPerfectRatings > 0 && state.PerfectRatings < def.RequiredPerfectRatings)
            return false;

        return true;
    }
}
