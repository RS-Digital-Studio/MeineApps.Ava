using System.Collections.ObjectModel;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// EconomyFeatureViewModel — Aufträge: Start, Resume, Material-Order, Refresh + Parallel-Orders.
/// Reiner Partial-Split (v2.1.4 Datei-Aufteilung) — keine Verhaltensänderung.
/// </summary>
internal sealed partial class EconomyFeatureViewModel
{
    internal async Task StartOrderAsync(Order order, bool acceptMaterialOffer = false)
    {
        // Lieferaufträge: Items direkt abgeben, kein MiniGame
        if (order.OrderType == OrderType.MaterialOrder)
        {
            await CompleteMaterialOrderAsync(order);
            return;
        }

        // v2.0.35 Feature A: Multi-Order-System.
        // Wenn der Workshop bereits einen laufenden Auftrag hat, koennen keine
        // weiteren angenommen werden — die parallelen Slots sind pro-Workshop 1.
        if (!_gameStateService.CanStartParallelOrder(order.WorkshopType))
        {
            ShowAlertDialog(
                _localizationService.GetString("ParallelOrderLimitTitle") ?? "Limit reached",
                _localizationService.GetString("ParallelOrderLimitMessage")
                    ?? "This workshop already has a running order or the global parallel limit has been reached.",
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        // V7 (): Material-Offer-Annahme (optional). Muss VOR StartOrder
        // erfolgen, damit Reservierungen atomar mit dem Auftrags-Start zusammenfallen.
        if (acceptMaterialOffer && order.HasMaterialOffer)
        {
            if (!_gameStateService.TryAcceptMaterialOffer(order))
            {
                // Spieler hat nicht genug Material — Alert + abbruch ohne StartOrder.
                ShowAlertDialog(
                    _localizationService.GetString("MaterialOfferInsufficientTitle") ?? "Not enough material",
                    _localizationService.GetString("MaterialOfferInsufficientMessage")
                        ?? "Your warehouse doesn't have the materials this order asks for.",
                    _localizationService.GetString("OK") ?? "OK");
                return;
            }

            // V7 (Telemetrie, Plan Section 8.1): order_accepted_with_material
            _analyticsService?.TrackEvent("order_accepted_with_material", new Dictionary<string, object?>
            {
                ["order_type"] = order.OrderType.ToString(),
                ["bonus_multiplier"] = order.MaterialOfferBonusMultiplier,
                ["material_count"] = order.MaterialOffer?.Sum(kv => kv.Value) ?? 0
            });
        }

        // Bestehenden Vordergrund-Auftrag pausieren (falls vorhanden) statt zu blockieren.
        // Der vorherige Auftrag bleibt in ParallelOrdersByWorkshop erhalten.
        if (_host.HasActiveOrder)
        {
            _gameStateService.PauseActiveOrder();
            _host.HasActiveOrder = false;
            _host.ActiveOrder = null;
        }

        _gameStateService.StartOrder(order);
        _host.HasActiveOrder = true;
        _host.ActiveOrder = order;
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Hint beim ersten Auftrag
        _contextualHintService.TryShowHint(ContextualHints.FirstOrder);

        // Auftragsdetail anzeigen
        _host.OrderViewModel.SetOrder(order);
        _host.ActivePage = ActivePage.OrderDetail;
    }

    /// <summary>
    /// Setzt einen parallelen Auftrag als aktiv und oeffnet den OrderDetail-Screen
    /// zum Fortsetzen (v2.0.35 Feature A, atomar seit v2.0.36).
    /// </summary>
    internal async Task ResumeParallelOrderAsync(WorkshopType workshopType)
    {
        // Atomarer Wechsel: kein Beobachtungs-Slot mit ActiveOrder=null zwischen
        // Pause + Resume → kein Doppel-Tap-Race, kein GameLoop-Tick-Sandwich.
        var order = _gameStateService.SwapToParallelOrder(workshopType);
        if (order == null) return;

        _host.HasActiveOrder = true;
        _host.ActiveOrder = order;
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        _host.OrderViewModel.SetOrder(order);
        _host.ActivePage = ActivePage.OrderDetail;
    }

    /// <summary>
    /// Schließt einen Lieferauftrag ab: Items prüfen, abziehen, Belohnung gutschreiben.
    /// </summary>
    private async Task CompleteMaterialOrderAsync(Order order)
    {
        if (order.RequiredMaterials == null) return;

        // Prüfen ob alle Items vorhanden — reservierte Mengen ausschliessen (konsistent mit
        // CompleteMaterialOrder im Service; sonst Alert-Inkonsistenz bzw. Material-Doppelverwertung).
        var state = _gameStateService.State;
        foreach (var (productId, required) in order.RequiredMaterials)
        {
            int available = state.CraftingInventory.GetValueOrDefault(productId, 0)
                          - state.ReservedInventory.GetValueOrDefault(productId, 0);
            if (available < required)
            {
                // Nicht genug Materialien
                var msg = _localizationService.GetString("InsufficientMaterials") ?? "Not enough materials";
                _host.DialogVM.ShowAlertDialog(
                    _localizationService.GetString("MaterialsRequired") ?? "Materials Required",
                    msg,
                    "OK");
                return;
            }
        }

        // Auftrag abschließen
        var reward = _gameStateService.CompleteMaterialOrder(order);
        if (reward <= 0) return;

        // Tracking für Daily/Weekly Challenges
        _dailyChallengeService?.OnMaterialOrderCompleted();
        _weeklyMissionService?.OnMaterialOrderCompleted();

        await _audioService.PlaySoundAsync(GameSound.Perfect);
        FloatingTextRequested?.Invoke($"+{MoneyFormatter.Format(reward, 0)}", "money");
        CelebrationRequested?.Invoke();

        // Orders aktualisieren
        _orderGeneratorService.RefreshOrders();
        RefreshOrders();
    }

    internal async Task RefreshOrdersAsync()
    {
        _orderGeneratorService.RefreshOrders();
        RefreshOrders();
        await _audioService.PlaySoundAsync(GameSound.ButtonTap);
    }

    internal void RefreshOrders()
    {
        var state = _gameStateService.State;

        // v2.0.35 Bugfix: Alte Orders haben BaseReward vom Zeitpunkt ihrer Generation.
        // Nach Workshop-Upgrades/Prestige steigt NetIncomePerSecond — die Orders zeigen
        // aber alte Werte. Rekalkulieren bei jedem Dashboard-Render damit der Spieler
        // immer aktuelle Rewards sieht (konsistent mit dem Refresh-Button-Verhalten).
        _orderGeneratorService.RecalculateAvailableOrderRewards();

        // Collection-Referenz ersetzen statt Clear()+Add() → 1 statt N+1 Change-Notifications
        var newOrders = new ObservableCollection<Order>();

        foreach (var order in state.AvailableOrders)
        {
            // Lokalisierte Display-Felder befüllen
            var localizedTitle = _localizationService.GetString(order.TitleKey);
            order.DisplayTitle = string.IsNullOrEmpty(localizedTitle) ? order.TitleFallback : localizedTitle;
            order.DisplayWorkshopName = _localizationService.GetString(order.WorkshopType.GetLocalizationKey());

            // Auftragstyp Display-Properties (Task #3)
            order.DisplayOrderType = _localizationService.GetString(order.OrderType.GetLocalizationKey())
                                     ?? order.OrderType.ToString();
            order.OrderTypeIcon = order.OrderType.GetIcon();
            order.OrderTypeBadgeColor = order.OrderType switch
            {
                OrderType.Large => "#EA580C",
                OrderType.Weekly => "#FFD700",
                OrderType.Cooperation => "#0E7490",
                OrderType.MaterialOrder => "#10B981",
                _ => ""
            };
            order.ShowOrderTypeBadge = order.OrderType != OrderType.Standard && order.OrderType != OrderType.Quick;

            // Lieferaufträge: Materialien-Info im Titel anzeigen
            if (order.OrderType == OrderType.MaterialOrder && order.RequiredMaterials != null)
            {
                var allProducts = CraftingProduct.GetAllProducts();
                var parts = new List<string>();
                foreach (var (productId, count) in order.RequiredMaterials)
                {
                    string name = allProducts.TryGetValue(productId, out var p)
                        ? _localizationService.GetString(p.NameKey) ?? p.NameKey
                        : productId;
                    int have = state.CraftingInventory.GetValueOrDefault(productId, 0);
                    parts.Add($"{name} {have}/{count}");
                }
                order.DisplayDescription = string.Join(", ", parts);
            }

            // V7 (): Material-Offer-Display
            if (order.HasMaterialOffer)
            {
                var allProducts = CraftingProduct.GetAllProducts();
                var parts = new List<string>();
                foreach (var (productId, count) in order.MaterialOffer!)
                {
                    string name = allProducts.TryGetValue(productId, out var p)
                        ? _localizationService.GetString(p.NameKey) ?? p.NameKey
                        : productId;
                    parts.Add($"{count}x {name}");
                }
                int bonusPct = (int)Math.Round(order.MaterialOfferBonusMultiplier * 100);
                string bonusLabel = string.Format(
                    _localizationService.GetString("MaterialOfferDisplayFormat") ?? "+{0}% with {1}",
                    bonusPct, string.Join(", ", parts));
                order.MaterialOfferDisplay = bonusLabel;
            }
            else
            {
                order.MaterialOfferDisplay = string.Empty;
            }

            newOrders.Add(order);
        }

        _host.AvailableOrders = newOrders;
        // Empty State (Task #8)
        _host.HasNoOrders = _host.AvailableOrders.Count == 0;

        // v2.0.35 Feature A: ParallelOrders-Collection ebenfalls refreshen (Fortsetzen-Banner).
        RefreshParallelOrders();

        // ONB-1: Auftragstypen-Hint bei erstem non-Standard-Auftrag
        for (int i = 0; i < newOrders.Count; i++)
        {
            if (newOrders[i].ShowOrderTypeBadge)
            {
                _contextualHintService.TryShowHint(ContextualHints.OrderTypes);
                break;
            }
        }
    }

    /// <summary>
    /// Aktualisiert die sichtbare Liste paralleler Auftraege fuer das Fortsetzen-Banner (v2.0.35).
    /// Wird nach StartOrder/CompleteActiveOrder/CancelActiveOrder aufgerufen.
    /// </summary>
    internal void RefreshParallelOrders()
    {
        var state = _gameStateService.State;
        var newList = new ObservableCollection<Order>();

        foreach (var kvp in state.ParallelOrdersByWorkshop)
        {
            var order = kvp.Value;
            // Lokalisierte Display-Felder befuellen (analog zu RefreshOrders)
            var localizedTitle = _localizationService.GetString(order.TitleKey);
            order.DisplayTitle = string.IsNullOrEmpty(localizedTitle) ? order.TitleFallback : localizedTitle;
            order.DisplayWorkshopName = _localizationService.GetString(order.WorkshopType.GetLocalizationKey());
            newList.Add(order);
        }

        _host.ParallelOrders = newList;
    }
}
