using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Service fuer Auftrags-Operationen und MiniGame Auto-Complete.
/// Teil der IGameStateService Interface Segregation.
/// </summary>
public interface IGameOrderService
{
    // ===================================================================
    // EVENTS
    // ===================================================================

    /// <summary>Wird ausgeloest wenn ein Auftrag abgeschlossen wird.</summary>
    event EventHandler<OrderCompletedEventArgs>? OrderCompleted;

    /// <summary>Wird ausgeloest wenn ein MiniGame-Ergebnis aufgezeichnet wird.</summary>
    event EventHandler<MiniGameResultRecordedEventArgs>? MiniGameResultRecorded;

    // ===================================================================
    // AUFTRAGS-OPERATIONEN
    // ===================================================================

    /// <summary>
    /// Startet einen Auftrag (verschiebt ihn in den aktiven Status).
    /// </summary>
    void StartOrder(Order order);

    /// <summary>
    /// Gibt den aktuell aktiven Auftrag zurueck.
    /// </summary>
    Order? GetActiveOrder();

    /// <summary>
    /// Zeichnet ein MiniGame-Ergebnis fuer den aktiven Auftrag auf.
    /// </summary>
    void RecordMiniGameResult(MiniGameRating rating);

    /// <summary>
    /// Schliesst den aktiven Auftrag ab und vergibt Belohnungen.
    /// </summary>
    void CompleteActiveOrder();

    /// <summary>
    /// Berechnet den kombinierten Auftrags-Belohnungsmultiplikator aus Research, Gebaeuden,
    /// Reputation, Events und Stammkunden. Wird fuer korrekte Belohnungsanzeige in MiniGames verwendet.
    /// </summary>
    decimal GetOrderRewardMultiplier(Order order);

    /// <summary>
    /// Bricht den aktiven Auftrag ohne Belohnungen ab.
    /// </summary>
    void CancelActiveOrder();

    /// <summary>
    /// Schliesst einen Lieferauftrag ab: Items abziehen, Belohnung gutschreiben.
    /// Gibt den Geld-Ertrag zurueck (0 wenn nicht moeglich).
    /// </summary>
    decimal CompleteMaterialOrder(Order order);

    // ===================================================================
    // MINIGAME AUTO-COMPLETE
    // ===================================================================

    /// <summary>
    /// Zaehlt ein Perfect-Rating fuer den angegebenen MiniGame-Typ.
    /// </summary>
    void RecordPerfectRating(MiniGameType type);

    /// <summary>
    /// Prueft ob Auto-Complete fuer diesen MiniGame-Typ verfuegbar ist.
    /// Premium-Spieler: 25 Perfects, Free-Spieler: 50 Perfects.
    /// </summary>
    bool CanAutoComplete(MiniGameType type, bool isPremium);
}
