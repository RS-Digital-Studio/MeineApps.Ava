using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service fuer Werkstatt-Operationen (Upgrade, Kauf, Worker-Einstellung).
/// Teil der IGameStateService Interface Segregation.
/// </summary>
public interface IGameWorkshopService
{
    // ===================================================================
    // EVENTS
    // ===================================================================

    /// <summary>Wird ausgeloest wenn eine Werkstatt aufgewertet wird.</summary>
    event EventHandler<WorkshopUpgradedEventArgs>? WorkshopUpgraded;

    /// <summary>Wird ausgeloest wenn ein Arbeiter eingestellt wird.</summary>
    event EventHandler<WorkerHiredEventArgs>? WorkerHired;

    // ===================================================================
    // WERKSTATT-OPERATIONEN
    // ===================================================================

    /// <summary>
    /// Gibt eine Werkstatt nach Typ zurueck.
    /// </summary>
    Workshop? GetWorkshop(WorkshopType type);

    /// <summary>
    /// Versucht eine Werkstatt aufzuwerten. Gibt true zurueck bei Erfolg.
    /// </summary>
    bool TryUpgradeWorkshop(WorkshopType type);

    /// <summary>
    /// Upgradet einen Workshop mehrfach (Bulk Buy). Gibt Anzahl Upgrades zurueck.
    /// count=0 bedeutet Max (so viele wie bezahlbar).
    /// </summary>
    int TryUpgradeWorkshopBulk(WorkshopType type, int count);

    /// <summary>
    /// Versucht einen Arbeiter fuer eine Werkstatt einzustellen. Gibt true zurueck bei Erfolg.
    /// </summary>
    bool TryHireWorker(WorkshopType type);

    /// <summary>
    /// Prueft ob eine Werkstatt beim aktuellen Spieler-Level freigeschaltet ist.
    /// </summary>
    bool IsWorkshopUnlocked(WorkshopType type);

    /// <summary>
    /// Kauft eine Werkstatt frei (Level-Anforderung muss erfuellt sein, Kosten werden abgezogen).
    /// </summary>
    bool TryPurchaseWorkshop(WorkshopType type, decimal costOverride = -1);

    /// <summary>
    /// Prueft ob eine Werkstatt kaufbar ist (Level erreicht, nicht bereits freigeschaltet).
    /// </summary>
    bool CanPurchaseWorkshop(WorkshopType type);
}
