using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Partial: Automatisierungs-Logik (AutoCollect, AutoAccept, AutoAssign).
/// </summary>
public sealed partial class GameLoopService
{
    /// <summary>
    /// Event wenn Automation eine Lieferung eingesammelt hat.
    /// </summary>
    public event EventHandler<SupplierDelivery>? AutoCollectedDelivery;

    /// <summary>
    /// Event wenn Automation einen Auftrag angenommen hat.
    /// </summary>
    public event EventHandler<Order>? AutoAcceptedOrder;

    /// <summary>
    /// Verarbeitet AutoCollect und AutoAccept Automation.
    /// </summary>
    private void ProcessAutomation(GameState state)
    {
        var auto = state.Automation;

        // AutoCollect: Lieferung einsammeln wenn vorhanden
        if (auto.AutoCollectDelivery && _gameStateService.IsAutoCollectUnlocked && state.PendingDelivery != null && !state.PendingDelivery.IsExpired)
        {
            var delivery = state.PendingDelivery;
            state.PendingDelivery = null;
            state.Statistics.TotalDeliveriesClaimed++;

            // Lieferungs-Effekt anwenden
            switch (delivery.Type)
            {
                case DeliveryType.Money:
                    _gameStateService.AddMoney(delivery.Amount);
                    break;
                case DeliveryType.GoldenScrews:
                    _gameStateService.AddGoldenScrews((int)Math.Round(delivery.Amount));
                    break;
                case DeliveryType.Experience:
                    _gameStateService.AddXp((int)delivery.Amount);
                    break;
                case DeliveryType.MoodBoost:
                    foreach (var ws in state.Workshops)
                    foreach (var w in ws.Workers)
                        w.Mood = Math.Min(100m, w.Mood + delivery.Amount);
                    break;
                case DeliveryType.SpeedBoost:
                    state.SpeedBoostEndTime = DateTime.UtcNow.AddMinutes((double)delivery.Amount);
                    break;
            }

            AutoCollectedDelivery?.Invoke(this, delivery);
        }

        // AutoAccept: Besten Auftrag annehmen wenn kein aktiver vorhanden
        if (auto.AutoAcceptOrder && _gameStateService.IsAutoAcceptUnlocked && state.ActiveOrder == null && state.AvailableOrders.Count > 0)
        {
            // Besten Auftrag waehlen (hoechste Belohnung) - ohne LINQ um Allokationen zu vermeiden
            Order? bestOrder = null;
            for (int i = 0; i < state.AvailableOrders.Count; i++)
            {
                var order = state.AvailableOrders[i];
                if (bestOrder == null || order.BaseReward > bestOrder.BaseReward)
                    bestOrder = order;
            }

            if (bestOrder != null)
            {
                state.ActiveOrder = bestOrder;
                state.AvailableOrders.Remove(bestOrder);
                AutoAcceptedOrder?.Invoke(this, bestOrder);
            }
        }
    }

    /// <summary>
    /// Verarbeitet AutoAssign: Weist idle Worker dem Workshop mit den meisten freien Plaetzen zu.
    /// </summary>
    private void ProcessAutoAssign(GameState state)
    {
        if (!state.Automation.AutoAssignWorkers || !_gameStateService.IsAutoAssignUnlocked)
            return;

        // Idle Worker finden (nicht zugewiesen zu einem Workshop)
        foreach (var ws in state.Workshops)
        {
            if (ws.Workers.Count >= ws.MaxWorkers) continue;

            // AutoAssign: Ruhende Worker mit niedriger Erschoepfung wieder arbeiten lassen
            foreach (var worker in ws.Workers)
            {
                if (worker.IsResting && worker.Fatigue <= 20m)
                {
                    worker.IsResting = false;
                }
            }
        }
    }
}
