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
}
