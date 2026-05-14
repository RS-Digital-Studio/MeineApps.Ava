using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Partielle Klasse: Forwarding-Stubs zu EconomyFeatureViewModel.
/// Geschäftslogik extrahiert nach EconomyFeatureViewModel.cs (03.04.2026).
/// [RelayCommand]-Attribute bleiben hier (AXAML-Bindings unverändert).
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Economy-Geschäftslogik (Workshop-Kauf/Upgrade, Orders, Rush, Delivery, Prestige-Banner).</summary>
    internal EconomyFeatureViewModel EconomyVM { get; private set; } = null!;

    private void InitializeEconomyVM()
    {
        // V7 (): Material-Lieferungen + Stack-Limits brauchen
        // Warehouse + Research zur Bonus-Berechnung. Beide optional injiziert via DI.
        var serviceProvider = App.Services;
        var warehouseService = serviceProvider?.GetService(typeof(IWarehouseService)) as IWarehouseService;
        var researchService = serviceProvider?.GetService(typeof(IResearchService)) as IResearchService;

        EconomyVM = new EconomyFeatureViewModel(
            this,
            _gameStateService,
            _audioService,
            _localizationService,
            _orderGeneratorService,
            _rewardedAdService,
            _prestigeService,
            _challengeConstraints,
            _contextualHintService,
            _purchaseService,
            _dailyChallengeService,
            _weeklyMissionService,
            _eventService,
            DialogVM,
            _analyticsService,
            warehouseService,
            researchService);

        // Events verdrahten (benannte Delegates fuer Dispose-Unsubscribe)
        _economyFloatingTextHandler = (text, cat) => FloatingTextRequested?.Invoke(text, cat);
        _economyCelebrationHandler = () => CelebrationRequested?.Invoke();
        EconomyVM.FloatingTextRequested += _economyFloatingTextHandler;
        EconomyVM.CelebrationRequested += _economyCelebrationHandler;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RELAY-COMMANDS (Forwarding, AXAML-Bindings bleiben unverändert)
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private Task SelectWorkshopAsync(WorkshopDisplayModel workshop) => EconomyVM.SelectWorkshopAsync(workshop);

    [RelayCommand]
    private void CycleBulkBuy() => EconomyVM.CycleBulkBuy();

    [RelayCommand]
    private Task UpgradeWorkshopAsync(WorkshopDisplayModel workshop) => EconomyVM.UpgradeWorkshopAsync(workshop);

    [RelayCommand]
    private Task HireWorkerAsync(WorkshopDisplayModel workshop) => EconomyVM.HireWorkerAsync(workshop);

    [RelayCommand]
    private Task StartOrderAsync(Order order) => EconomyVM.StartOrderAsync(order);

    /// <summary>
    /// V7 (): Annimmt einen Auftrag MIT Material-Offer
    /// (reserviert Material und aktiviert Bonus-Reward beim Complete).
    /// </summary>
    [RelayCommand]
    private Task StartOrderWithMaterialAsync(Order order)
        => EconomyVM.StartOrderAsync(order, acceptMaterialOffer: true);

    /// <summary>
    /// Setzt einen parallelen Auftrag fort (v2.0.35 Feature A).
    /// Parameter ist der Workshop-Typ als object — akzeptiert sowohl Enum-Wert direkt
    /// (XAML-Binding auf WorkshopType-Property) als auch String ("Carpenter" etc.).
    /// Vgl. CLAUDE.md Gotcha "XAML CommandParameter ist IMMER string" — wir muessen
    /// hier object akzeptieren, weil der Data-Binding-Fall Enum liefert.
    /// </summary>
    [RelayCommand]
    private async Task ResumeParallelOrderAsync(object? workshopTypeParam)
    {
        if (workshopTypeParam is null) return;

        Models.Enums.WorkshopType type;
        switch (workshopTypeParam)
        {
            case Models.Enums.WorkshopType direct:
                type = direct;
                break;
            case string s when Enum.TryParse<Models.Enums.WorkshopType>(s, out var parsed):
                type = parsed;
                break;
            default:
                return;
        }

        await EconomyVM.ResumeParallelOrderAsync(type);
    }

    [RelayCommand]
    private Task RefreshOrdersAsync() => EconomyVM.RefreshOrdersAsync();

    [RelayCommand]
    private Task ActivateRushAsync() => EconomyVM.ActivateRushAsync();

    [RelayCommand]
    private void ClaimDelivery() => EconomyVM.ClaimDelivery();

    [RelayCommand]
    private void ToggleChallenge(string challengeName) => EconomyVM.ToggleChallenge(challengeName);

    [RelayCommand]
    private Task AbandonChallengeRun() => EconomyVM.AbandonChallengeRun();

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC API (Code-Behind Zugriff)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Flag: Hold-to-Upgrade aktiv.</summary>
    public bool IsHoldingUpgrade { get => EconomyVM.IsHoldingUpgrade; set => EconomyVM.IsHoldingUpgrade = value; }

    /// <summary>Stilles Upgrade ohne Sound/FloatingText - für Hold-to-Upgrade.</summary>
    public bool UpgradeWorkshopSilent(WorkshopType type) => EconomyVM.UpgradeWorkshopSilent(type);

    /// <summary>Spielt den Upgrade-Sound ab (für Hold-to-Upgrade Ende).</summary>
    public void PlayUpgradeSound() => EconomyVM.PlayUpgradeSound();

    /// <summary>Aktualisiert eine einzelne Workshop-Anzeige.</summary>
    public void RefreshSingleWorkshopPublic(WorkshopType type) => EconomyVM.RefreshSingleWorkshopPublic(type);

    /// <summary>GameState für SkiaSharp-Rendering.</summary>
    public GameState? GetGameStateForRendering() => _gameStateService.State;

    /// <summary>Lokalisierte Tab-Labels für SkiaSharp Tab-Bar.</summary>
    public string[] GetTabLabels() => EconomyVM.GetTabLabels();

    /// <summary>Lokalisierte Loading-Tipps.</summary>
    public string[] GetLoadingTips() => EconomyVM.GetLoadingTips();

    // ═══════════════════════════════════════════════════════════════════════
    // INTERNE FORWARDING (von anderen Partial-Dateien aufgerufen)
    // ═══════════════════════════════════════════════════════════════════════

    private void RefreshFromState() => EconomyVM.RefreshFromState();
    private void RefreshWorkshops() => EconomyVM.RefreshWorkshops();
    private void RefreshSingleWorkshop(WorkshopType type) => EconomyVM.RefreshSingleWorkshop(type);
    private void RefreshOrders() => EconomyVM.RefreshOrders();
    private void UpdateRushDisplay() => EconomyVM.UpdateRushDisplay();
    private void UpdateDeliveryDisplay() => EconomyVM.UpdateDeliveryDisplay();
    private void UpdateBoostIndicator() => EconomyVM.UpdateBoostIndicator();
    private void RefreshPrestigeBanner(GameState state) => EconomyVM.RefreshPrestigeBanner(state);
    private void RefreshReputation(GameState state) => EconomyVM.RefreshReputation(state);
    private void SetBulkUpgradeCost(WorkshopDisplayModel model, Workshop? workshop, decimal money) => EconomyVM.SetBulkUpgradeCost(model, workshop, money);
}
