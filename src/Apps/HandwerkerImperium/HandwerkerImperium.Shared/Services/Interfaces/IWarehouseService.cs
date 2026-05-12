using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// V7 (Phase 1 Ressourcen-Plan): Lager-Verwaltung mit Slots + Stack-Limits + Reservierung.
/// Wird von <see cref="IAutoProductionService"/>, <see cref="ICraftingService"/> und der
/// Lager-UI im Imperium-Tab konsumiert.
/// </summary>
public interface IWarehouseService
{
    // ===================================================================
    // EVENTS
    // ===================================================================

    /// <summary>Lagerbestand hat sich geaendert (UI-Refresh-Trigger).</summary>
    event Action? InventoryChanged;

    /// <summary>
    /// Ein Workshop wurde wegen vollem Stack pausiert (UI zeigt gelben Warn-Badge auf der Card).
    /// </summary>
    event Action<WorkshopType, string>? WorkshopPaused;

    /// <summary>Ueberlauf wurde per Auto-Verkauf konvertiert (FloatingText "+12 Holzbrett verkauft").</summary>
    event Action<string, int, decimal>? OverflowAutoSold;

    // ===================================================================
    // SLOT/STACK MANAGEMENT
    // ===================================================================

    /// <summary>
    /// Anzahl belegter Slots (verschiedene Material-Typen in <see cref="GameState.CraftingInventory"/>).
    /// </summary>
    int UsedSlotCount { get; }

    /// <summary>Anzahl freier Slots.</summary>
    int FreeSlotCount { get; }

    /// <summary>True wenn keine freien Slots mehr verfuegbar sind.</summary>
    bool IsWarehouseFull { get; }

    /// <summary>
    /// Aktueller Lagerwert (Summe BaseValue × CraftingSellMultiplier × Count).
    /// Wird in der Lager-UI als Header gezeigt.
    /// </summary>
    decimal GetTotalWarehouseValue();

    // ===================================================================
    // INVENTAR-OPERATIONEN (mit Slot/Stack-Validierung)
    // ===================================================================

    /// <summary>
    /// Prueft ob <paramref name="count"/> Stueck von <paramref name="productId"/> noch
    /// hineinpassen ohne Stack-Limit zu sprengen.
    /// </summary>
    bool CanAddToInventory(string productId, int count);

    /// <summary>
    /// Fuegt Material zum Lager hinzu. Respektiert Stack-Limits + Auto-Verkauf bei Ueberlauf.
    /// Gibt die *tatsaechlich eingelagerte* Menge zurueck. Ueberlauf wird:
    /// - bei aktivem Auto-Verkauf zum Marktpreis verkauft (event <see cref="OverflowAutoSold"/>),
    /// - sonst verworfen UND <see cref="WorkshopPaused"/> gefeuert.
    /// </summary>
    /// <param name="sourceWorkshop">Workshop der das Material produziert hat (fuer Pause-Event), null = kein Workshop-Pause-Signal.</param>
    int AddToInventory(string productId, int count, WorkshopType? sourceWorkshop = null);

    /// <summary>
    /// Reserviert Material fuer akzeptierte Auftraege (Phase 2). Verhindert dass die gleiche
    /// Menge ein zweites Mal verbraucht wird. Gibt false zurueck wenn nicht genug verfuegbar.
    /// </summary>
    bool TryReserve(string productId, int count);

    /// <summary>
    /// Konsumiert reserviertes Material (z.B. nach MiniGame-Complete mit Material-Order).
    /// Verringert <see cref="GameState.CraftingInventory"/> UND <see cref="GameState.ReservedInventory"/>.
    /// </summary>
    bool ConsumeReserved(string productId, int count);

    /// <summary>
    /// Gibt reserviertes Material zurueck (Order abgebrochen, abgelaufen oder Risk-Miss ohne Verlust).
    /// Schreibt es zurueck in den freien Bestand.
    /// </summary>
    bool ReleaseReserved(string productId, int count);

    /// <summary>
    /// Tatsaechlich verfuegbare Menge = CraftingInventory[id] - ReservedInventory[id].
    /// Wird von der Crafting-UI als "verfuegbar" angezeigt.
    /// </summary>
    int GetAvailable(string productId);

    // ===================================================================
    // AUTO-SELL RULES
    // ===================================================================

    /// <summary>Holt die Auto-Verkaufs-Regel fuer einen Material-Typ (legt Default an wenn fehlt).</summary>
    AutoSellRule GetAutoSellRule(string productId);

    /// <summary>Toggle Auto-Verkauf fuer einen Material-Typ.</summary>
    void SetAutoSellEnabled(string productId, bool enabled);

    // ===================================================================
    // SLOT-UPGRADE
    // ===================================================================

    /// <summary>
    /// Kosten fuer den naechsten +5-Slot-Upgrade (Geld). Skaliert exponentiell.
    /// Returnt 0 wenn Max-Slot-Cap erreicht.
    /// </summary>
    decimal GetNextSlotUpgradeCost();

    /// <summary>True wenn der Spieler sich den naechsten Slot-Upgrade leisten kann.</summary>
    bool CanUpgradeSlots();

    /// <summary>
    /// Kauft +5 Lager-Slots. Gibt true bei Erfolg zurueck. Cap bei 200 Slots.
    /// </summary>
    bool TryUpgradeSlots();

    /// <summary>Aktuelle Stack-Limit-Stufe (1 = 50, 2 = 100, ...). Phase 1: nur statisch.</summary>
    int CurrentStackLimit { get; }

    /// <summary>Maximale Slot-Anzahl (Hard-Cap).</summary>
    int MaxSlotCount { get; }
}
