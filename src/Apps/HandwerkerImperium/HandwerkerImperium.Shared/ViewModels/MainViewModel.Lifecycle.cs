using HandwerkerImperium.Models;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// App-Lifecycle (Pause/Resume), Live-Order-Spawn-Handler, Dispose.
/// AAA-Audit P0 Aufspaltung: aus MainViewModel.cs extrahiert (12.05.2026).
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>
    /// Wird gefeuert wenn die App pausiert/fortgesetzt wird (Android-Lifecycle).
    /// MainView nutzt das um den Render-Timer zu stoppen (Battery-Saving).
    /// </summary>
    public event Action<bool>? PauseStateChanged;

    /// <summary>
    /// Pauses the game loop (e.g., when app is backgrounded).
    /// </summary>
    public void PauseGameLoop()
    {
        if (_gameLoopService.IsRunning)
            _gameLoopService.Pause();

        // v2.0.37: Live-Orders pausieren — Countdown laeuft im Background nicht weiter (Cap 5min).
        _gameStateService.PauseAllLiveOrders();

        // Benachrichtigungen planen wenn aktiviert
        if (_gameStateService.Settings.NotificationsEnabled)
            _notificationService?.ScheduleGameNotifications(_gameStateService.State);

        // Render-Timer der MainView ebenfalls pausieren (Battery-Saving)
        PauseStateChanged?.Invoke(true);
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
            FloatingTextRequested?.Invoke(text, order.IsPremium ? "premium" : "info");
            // Nach Spawn neu rendern, damit der Auftrag direkt sichtbar ist
            EconomyVM.RefreshOrders();
        });
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Phase 9: Money-Animation Flag zurücksetzen
        _moneyAnimActive = false;
        _levelPulseTimer?.Stop();

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

        _gameStateService.MoneyChanged -= OnMoneyChanged;
        _gameStateService.GoldenScrewsChanged -= OnGoldenScrewsChanged;
        _gameStateService.LevelUp -= OnLevelUp;
        _gameStateService.XpGained -= OnXpGained;
        _gameStateService.WorkshopUpgraded -= OnWorkshopUpgraded;
        _gameStateService.WorkerHired -= OnWorkerHired;
        _gameStateService.OrderCompleted -= OnOrderCompleted;
        _gameStateService.StateLoaded -= OnStateLoaded;
        _gameStateService.MiniGameResultRecorded -= OnMiniGameResultRecorded;
        _gameStateService.ReputationTierChanged -= OnReputationTierChanged;
        _gameLoopService.OnTick -= OnGameTick;
        _gameLoopService.MasterToolUnlocked -= OnMasterToolUnlocked;
        _gameLoopService.DeliveryArrived -= OnDeliveryArrived;
        _gameLoopService.OrderExpired -= OnOrderExpired;
        _gameLoopService.AutoCollectedDelivery -= OnAutoCollectedDelivery;
        _gameLoopService.AutoAcceptedOrder -= OnAutoAcceptedOrder;
        _achievementService.AchievementUnlocked -= OnAchievementUnlocked;
        _purchaseService.PremiumStatusChanged -= OnPremiumStatusChanged;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _eventService.EventStarted -= OnEventStarted;
        _eventService.EventEnded -= OnEventEnded;
        // Daily/Weekly/WelcomeBack Event-Unsubscribes sind in MissionsFeatureViewModel.Dispose()

        DialogVM.DeferredDialogCheckRequested -= CheckDeferredDialogs;
        DialogVM.PrestigeSummaryGoToShopRequested -= _dialogPrestigeSummaryGoToShopHandler;
        DialogVM.FloatingTextRequested -= _dialogFloatingTextHandler;
        DialogVM.Cleanup();

        // v2.0.36: Notification-Center Event abmelden
        NotificationCenterVM.ItemActivated -= OnNotificationItemActivated;

        // v2.1.0: BP-Tier-Up Event abmelden
        BattlePassViewModel.Service.TierUpReached -= OnBattlePassTierUp;

        _prestigeService.PrestigeCompleted -= OnPrestigeCompleted;
        _prestigeService.MilestoneReached -= OnPrestigeMilestoneReached;
        // AAA-Audit P0 Zerlegungs-Sprint: Cinematic-Subscription via Coordinator
        if (_cinematicCoordinator != null)
            _cinematicCoordinator.CinematicReady -= OnCinematicReadyFromCoordinator;
        else
            _prestigeService.CinematicReady -= OnPrestigeCinematicReady;
        if (_rebirthService != null)
            _rebirthService.RebirthCompleted -= OnRebirthCompleted;
        _workerService.WorkerLevelUp -= OnWorkerLevelUp;
        _workerService.InternReadyForPromotion -= OnInternReadyForPromotion;
        _backPressHelper.ExitHintRequested -= OnBackPressExitHint;

        // EconomyFeatureVM Events abmelden
        EconomyVM.FloatingTextRequested -= _economyFloatingTextHandler;
        EconomyVM.CelebrationRequested -= _economyCelebrationHandler;

        // MissionsFeatureVM Events abmelden + disposen
        MissionsVM.FloatingTextRequested -= _missionsFloatingTextHandler;
        MissionsVM.CelebrationRequested -= _missionsCelebrationHandler;
        MissionsVM.StreakRescued -= _missionsStreakRescuedHandler;
        MissionsVM.NavigateToMiniGameRequested -= OnMissionsNavigateToMiniGame;
        MissionsVM.CheckDeferredDialogsRequested -= CheckDeferredDialogs;
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
