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
    /// v2.1.1 Mutationen unter <see cref="IGameStateService.ExecuteWithLock"/>
    /// schuetzen vor Race mit SaveAsync-Serializer (Task.Run auf ThreadPool). Events werden NACH
    /// Lock-Release gefeuert (Deadlock-Praevention bei UI-Subscribern, die wieder ExecuteWithLock
    /// aufrufen koennten).
    /// </summary>
    private void ProcessAutomation(GameState state)
    {
        SupplierDelivery? collectedDelivery = null;
        Order? acceptedOrder = null;

        _gameStateService.ExecuteWithLock(() =>
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
                        // Income-Cache invalidieren , damit Doppel-Boost-Stacking
                        // im Income-Calculator sofort wirksam wird.
                        state.InvalidateIncomeCache();
                        break;
                }

                collectedDelivery = delivery;
            }

            // AutoAccept: Besten Auftrag annehmen wenn kein aktiver vorhanden
            if (auto.AutoAcceptOrder && _gameStateService.IsAutoAcceptUnlocked && state.ActiveOrder == null && state.AvailableOrders.Count > 0)
            {
                // Besten Auftrag waehlen (hoechste Belohnung) - ohne LINQ um Allokationen zu vermeiden
                // v2.0.36: Wenn AutoAcceptOnlyStandard aktiv, werden Live-/Premium-Auftraege uebersprungen.
                Order? bestOrder = null;
                for (int i = 0; i < state.AvailableOrders.Count; i++)
                {
                    var order = state.AvailableOrders[i];
                    if (auto.AutoAcceptOnlyStandard && (order.IsLive || order.IsPremium))
                        continue;
                    if (bestOrder == null || order.BaseReward > bestOrder.BaseReward)
                        bestOrder = order;
                }

                if (bestOrder != null)
                {
                    state.ActiveOrder = bestOrder;
                    // Slot in ParallelOrdersByWorkshop spiegeln — analog zum manuellen StartOrder-Pfad.
                    // Ohne diese Zeile sieht CanStartParallelOrder den belegten Slot nicht → der
                    // Workshop koennte einen zweiten Auftrag parallel bekommen (Max-3-Cap unterlaufen).
                    state.ParallelOrdersByWorkshop[bestOrder.WorkshopType] = bestOrder;
                    state.AvailableOrders.Remove(bestOrder);
                    acceptedOrder = bestOrder;
                }
            }
        });

        // Events ausserhalb des Locks feuern (Deadlock-Praevention)
        if (collectedDelivery != null)
            AutoCollectedDelivery?.Invoke(this, collectedDelivery);
        if (acceptedOrder != null)
            AutoAcceptedOrder?.Invoke(this, acceptedOrder);
    }

    /// <summary>
    /// Verarbeitet AutoAssign: Reaktiviert ruhende Worker mit niedriger Erschöpfung (kleinergleich 20%).
    /// </summary>
    private void ProcessAutoAssign(GameState state)
    {
        if (!state.Automation.AutoAssignWorkers || !_gameStateService.IsAutoAssignUnlocked)
            return;

        // Unter dem State-Lock — die Workshop-/Worker-Enumeration racet sonst mit dem AutoSave-Serializer.
        _gameStateService.ExecuteWithLock(() =>
        {
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
        });
    }
}
