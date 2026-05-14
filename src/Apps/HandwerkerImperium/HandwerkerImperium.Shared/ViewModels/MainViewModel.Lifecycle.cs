using HandwerkerImperium.Models;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// App-Lifecycle (Pause/Resume), Live-Order-Spawn-Handler, Dispose.
/// aus MainViewModel.cs extrahiert (12.05.2026).
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>
    /// Wird gefeuert wenn die App pausiert/fortgesetzt wird (Android-Lifecycle).
    /// MainView nutzt das um den Render-Timer zu stoppen (Battery-Saving).
    /// </summary>
    public event Action<bool>? PauseStateChanged;

    /// <summary>
    /// Pausiert den Game-Loop (z.B. wenn die App in den Hintergrund geht) und speichert
    /// synchron. H-H05: async, damit Android OnPause den Save abwarten kann.
    /// </summary>
    public async Task PauseGameLoopAsync()
    {
        // H-H05: UI- und State-Operationen zuerst synchron auf dem UI-Thread, dann den
        // Save abwarten. Diese Methode laeuft komplett synchron bis zum await — der
        // DispatcherTimer kann in der Zeit nicht ticken (gleicher Thread).

        // v2.0.37: Live-Orders pausieren — Countdown laeuft im Background nicht weiter (Cap 5min).
        _gameStateService.PauseAllLiveOrders();

        // Benachrichtigungen planen wenn aktiviert
        if (_gameStateService.Settings.NotificationsEnabled)
            _notificationService?.ScheduleGameNotifications(_gameStateService.State);

        // Render-Timer der MainView ebenfalls pausieren (Battery-Saving)
        PauseStateChanged?.Invoke(true);

        // Game-Loop pausieren (Timer stoppen) + Save synchron abwarten.
        if (_gameLoopService.IsRunning)
            await _gameLoopService.PauseAsync().ConfigureAwait(false);

        // Bei App-Pause sind keine Worker-Renders aktiv → die gepruneten
        // Bitmaps koennen sicher Native-Memory freigeben.
        Graphics.WorkerAvatarRenderer.FlushPendingDispose();
    }

    /// <summary>
    /// Resumes the game loop (e.g., when app is foregrounded).
    /// </summary>
    public void ResumeGameLoop()
    {
        // Geplante Benachrichtigungen stornieren
        _notificationService?.CancelAllNotifications();

        if (!_gameLoopService.IsRunning)
            _gameLoopService.Resume();

        // v2.0.37: Live-Orders fortsetzen — akkumulierte Pause wird auf 5min gecappt.
        _gameStateService.ResumeAllLiveOrders();

        // Render-Timer der MainView wieder starten
        PauseStateChanged?.Invoke(false);
    }

    /// <summary>
    /// v2.0.35 Feature D: Neuer Live-Auftrag gespawnt — Toast/FloatingText fuer Sichtbarkeit.
    /// </summary>
    private void OnLiveOrderSpawned(Order order)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var text = order.IsPremium
                ? _localizationService.GetString("LiveOrderSpawnedPremiumToast") ?? "VIP order incoming!"
                : _localizationService.GetString("LiveOrderSpawnedToast") ?? "New live order available!";
            _uiEffectBus.RaiseFloatingText(text, order.IsPremium ? "premium" : "info");
            // Nach Spawn neu rendern, damit der Auftrag direkt sichtbar ist
            EconomyVM.RefreshOrders();
        });
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Phase 9: Money-Animation Flag zurücksetzen
        _moneyAnimActive = false;

        // v2.0.35 Feature D: OrderSpawned-Subscribe abmelden
        _orderGeneratorService.OrderSpawned -= OnLiveOrderSpawned;

        // Stop the game loop and save
        _gameLoopService.Stop();

        // NavigationRequested per Schleife abmelden (symmetrisch zum Konstruktor)
        foreach (var child in _navigableChildren)
            child.NavigationRequested -= OnChildNavigation;
        LuckySpinViewModel.NavigationRequested -= _luckySpinNavHandler;
        HeaderVM.PropertyChanged -= _headerVmPropertyChangedHandler;

        // Child-VM Events abmelden (symmetrisch zum Konstruktor)
        WorkerProfileViewModel.FloatingTextRequested -= _workerProfileFloatingTextHandler;
        BuildingsViewModel.FloatingTextRequested -= _buildingsFloatingTextHandler;
        GuildViewModel.CelebrationRequested -= _guildCelebrationHandler;
        GuildViewModel.FloatingTextRequested -= _guildFloatingTextHandler;
        AscensionViewModel.FloatingTextRequested -= _ascensionFloatingTextHandler;
        AscensionViewModel.CelebrationRequested -= _ascensionCelebrationHandler;

        // Lambda-basierte Service-Subscriptions abmelden
        _rewardedAdService.AdUnavailable -= _adUnavailableHandler;
        _saveGameService.ErrorOccurred -= _saveGameErrorHandler;

        // Progression-Feedback-Subscriptions liegen im ProgressionFeedbackCoordinator
        // (Level/GoldenScrews/Xp/Workshop/Worker/MasterTool/Achievement/Prestige/Rebirth).
        _gameStateService.MoneyChanged -= OnMoneyChanged;
        _gameStateService.OrderCompleted -= OnOrderCompleted;
        _gameStateService.StateLoaded -= OnStateLoaded;
        _gameStateService.MiniGameResultRecorded -= OnMiniGameResultRecorded;
        _gameStateService.ReputationTierChanged -= OnReputationTierChanged;
        _gameLoopService.OnTick -= OnGameTick;
        _gameLoopService.DeliveryArrived -= OnDeliveryArrived;
        _gameLoopService.OrderExpired -= OnOrderExpired;
        _gameLoopService.AutoCollectedDelivery -= OnAutoCollectedDelivery;
        _gameLoopService.AutoAcceptedOrder -= OnAutoAcceptedOrder;
        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _eventService.EventStarted -= OnEventStarted;
        _eventService.EventEnded -= OnEventEnded;
        // Daily/Weekly/WelcomeBack Event-Unsubscribes sind in MissionsFeatureViewModel.Dispose()

        DialogVM.DeferredDialogCheckRequested -= WelcomeFlowVM.CheckDeferredDialogs;
        DialogVM.PrestigeSummaryGoToShopRequested -= _dialogPrestigeSummaryGoToShopHandler;
        DialogVM.FloatingTextRequested -= _dialogFloatingTextHandler;
        DialogVM.Cleanup();

        // v2.0.36: Notification-Center Event abmelden
        NotificationCenterVM.ItemActivated -= OnNotificationItemActivated;

        // v2.1.0: BP-Tier-Up Event abmelden
        BattlePassViewModel.Service.TierUpReached -= OnBattlePassTierUp;

        // Cinematic-Subscription via Coordinator
        if (_cinematicCoordinator != null)
            _cinematicCoordinator.CinematicReady -= OnCinematicReadyFromCoordinator;
        else
            _prestigeService.CinematicReady -= OnPrestigeCinematicReady;
        _backPressHelper.ExitHintRequested -= OnBackPressExitHint;
        // ProgressionFeedbackCoordinator unsubscribed seine eigenen Service-Events selbst —
        // er ist ein DI-Singleton und wird von App.DisposeServices() kaskadiert disposed.

        // EconomyFeatureVM Events abmelden
        EconomyVM.FloatingTextRequested -= _economyFloatingTextHandler;
        EconomyVM.CelebrationRequested -= _economyCelebrationHandler;

        // MissionsFeatureVM Events abmelden + disposen
        MissionsVM.FloatingTextRequested -= _missionsFloatingTextHandler;
        MissionsVM.CelebrationRequested -= _missionsCelebrationHandler;
        MissionsVM.StreakRescued -= _missionsStreakRescuedHandler;
        MissionsVM.NavigateToMiniGameRequested -= OnMissionsNavigateToMiniGame;
        MissionsVM.CheckDeferredDialogsRequested -= WelcomeFlowVM.CheckDeferredDialogs;
        MissionsVM.Dispose();

        GuildViewModel.Dispose();
        ShopViewModel.Dispose();
        WorkshopViewModel.Dispose();
        foreach (var mg in MiniGames.All)
            mg.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
