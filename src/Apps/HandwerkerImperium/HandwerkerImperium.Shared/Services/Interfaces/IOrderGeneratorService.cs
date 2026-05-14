using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Generates new orders/contracts for the player.
/// </summary>
public interface IOrderGeneratorService
{
    /// <summary>
    /// Generates a new order for the specified workshop type.
    /// </summary>
    Order GenerateOrder(WorkshopType workshopType, int workshopLevel);

    /// <summary>
    /// Generates multiple random orders based on current game state.
    /// </summary>
    List<Order> GenerateAvailableOrders(int count = 3);

    /// <summary>
    /// Refreshes the available orders (removes old, adds new).
    /// </summary>
    void RefreshOrders();

    /// <summary>
    /// Generiert einen Lieferauftrag (MaterialOrder) mit Item-Anforderungen.
    /// Gibt null zurück wenn keine Workshops für Auto-Produktion qualifiziert sind.
    /// </summary>
    Order? GenerateMaterialOrder();

    /// <summary>
    /// Generiert einen Live-Auftrag (v2.0.35): zufaelliger Auftragstyp mit Ablaufzeit 45-180s
    /// (45-90s bei Premium). 5% Chance Premium mit 3x Reward.
    /// Fuegt den Auftrag direkt zu AvailableOrders hinzu und feuert <see cref="OrderSpawned"/>.
    /// Gibt null zurueck wenn die Cap erreicht ist oder keine Workshops freigeschaltet sind.
    /// </summary>
    Order? GenerateLiveOrder();

    /// <summary>
    /// Entfernt abgelaufene Live-Auftraege aus AvailableOrders.
    /// Fuegt keine Reputation-Penalty hinzu — Abgelaufene sind einfach weg.
    /// </summary>
    /// <returns>Anzahl entfernter Auftraege.</returns>
    int ExpireOldLiveOrders();

    /// <summary>
    /// Schneller Lock-freier Lese-Counter — wie viele Live-Auftraege
    /// sind aktuell im AvailableOrders-Pool. Vermeidet einen vollstaendigen RemoveAll-Iter
    /// in <see cref="ExpireOldLiveOrders"/> wenn es nichts zu tun gibt (Early-Exit-Guard).
    /// </summary>
    int LiveOrderCount { get; }

    /// <summary>
    /// Aktualisiert BaseReward + BaseXp aller wartenden AvailableOrders auf Basis des
    /// aktuellen NetIncomePerSecond (v2.0.35 Bugfix). Verhindert dass alte Orders
    /// "veraltete" Rewards zeigen wenn das Einkommen zwischenzeitlich gestiegen ist.
    /// Premium-Multiplikator (3x) bleibt erhalten.
    /// </summary>
    void RecalculateAvailableOrderRewards();

    /// <summary>
    /// Wird gefeuert, wenn ein Live-Auftrag spawnt — UI zeigt Toast/Banner.
    /// </summary>
    event Action<Order>? OrderSpawned;
}
