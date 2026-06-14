using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet Vorarbeiter/Manager: Freischaltung, Upgrade, Bonus-Berechnung.
/// </summary>
public interface IManagerService
{
    /// <summary>
    /// Wird ausgelöst wenn ein neuer Manager freigeschaltet wird (enthält Manager-ID).
    /// </summary>
    event Action<string>? ManagerUnlocked;

    /// <summary>
    /// Prüft alle Manager-Definitionen und schaltet berechtigte frei.
    /// </summary>
    void CheckAndUnlockManagers();

    /// <summary>
    /// Erhöht das Level eines Managers (max 5) für Goldschrauben.
    /// </summary>
    void UpgradeManager(string managerId);

    /// <summary>
    /// Berechnet den Gesamtbonus aller Manager für einen bestimmten Workshop-Typ und Fähigkeit.
    /// </summary>
    decimal GetManagerBonusForWorkshop(WorkshopType type, ManagerAbility ability);

    /// <summary>
    /// Berechnet den Gesamtbonus aller globalen Manager (ohne Workshop-Bindung) für eine Fähigkeit.
    /// </summary>
    decimal GetGlobalManagerBonus(ManagerAbility ability);

    /// <summary>
    /// Invalidiert den internen Bonus-Summen-Cache. MUSS aufgerufen werden, wenn die
    /// Manager-Liste in-place geändert wird (Prestige-/Ascension-Reset löscht oder
    /// resettet Manager OHNE State-Swap, daher feuert <c>StateLoaded</c> dort nicht).
    /// </summary>
    void InvalidateBonusCache();
}
