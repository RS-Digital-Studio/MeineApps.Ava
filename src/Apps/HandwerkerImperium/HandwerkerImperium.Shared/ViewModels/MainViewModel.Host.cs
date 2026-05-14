using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels.MiniGames;

namespace HandwerkerImperium.ViewModels;

// INavigationHost-Implementierung ( — velvety-booping-peacock).
// Explizite Implementierung: Die Services rufen MainViewModel ausschliesslich ueber
// diesen Contract. Haelt MainViewModel public-API sauber und verhindert ungewolltes
// Leaken von internen Details.
public sealed partial class MainViewModel
{
    // ── Seiten-Zustand ───────────────────────────────────────────────
    ActivePage INavigationHost.ActivePage
    {
        get => ActivePage;
        set => ActivePage = value;
    }

    bool INavigationHost.IsWorkerProfileActive
    {
        get => IsWorkerProfileActive;
        set => IsWorkerProfileActive = value;
    }

    bool INavigationHost.IsLuckySpinVisible
    {
        get => IsLuckySpinVisible;
        set => IsLuckySpinVisible = value;
    }

    bool INavigationHost.IsCombinedWelcomeDialogVisible => WelcomeFlowVM.IsCombinedWelcomeDialogVisible;
    bool INavigationHost.IsOfflineEarningsDialogVisible => WelcomeFlowVM.IsOfflineEarningsDialogVisible;

    bool INavigationHost.IsDailyRewardDialogVisible
    {
        get => WelcomeFlowVM.IsDailyRewardDialogVisible;
        set => WelcomeFlowVM.IsDailyRewardDialogVisible = value;
    }

    bool INavigationHost.IsQuickJobsUnlocked => IsQuickJobsUnlocked;

    bool INavigationHost.IsTabLocked(int tabIndex) => IsTabLocked(tabIndex);

    // ── Tab-Auswahl (ruft die RelayCommand-Impl-Methoden aus Navigation.cs) ──
    void INavigationHost.SelectDashboardTab() => SelectDashboardTab();
    void INavigationHost.SelectBuildingsTab() => SelectBuildingsTab();
    void INavigationHost.SelectStatisticsTab() => SelectStatisticsTab();
    void INavigationHost.SelectAchievementsTab() => SelectAchievementsTab();
    void INavigationHost.SelectResearchTab() => SelectResearchTab();
    void INavigationHost.SelectWorkerMarketTab() => SelectWorkerMarketTab();

    void INavigationHost.RefreshOrders() => RefreshOrders();
    void INavigationHost.RefreshFromState() => RefreshFromState();
    void INavigationHost.NavigateBackStack() => NavigateBack();
    void INavigationHost.ClearNavigationStack() => _navigationStack.Clear();

    void INavigationHost.ShowPrestigeConfirmationAsyncFireAndForget()
        => ShowPrestigeConfirmationAsync().SafeFireAndForget();

    // ── Child-VM-Zugriff ─────────────────────────────────────────────
    ManagerViewModel INavigationHost.ManagerViewModel => ManagerViewModel;
    TournamentViewModel INavigationHost.TournamentViewModel => TournamentViewModel;
    SeasonalEventViewModel INavigationHost.SeasonalEventViewModel => SeasonalEventViewModel;
    BattlePassViewModel INavigationHost.BattlePassViewModel => BattlePassViewModel;
    GuildViewModel INavigationHost.GuildViewModel => GuildViewModel;
    CraftingViewModel INavigationHost.CraftingViewModel => CraftingViewModel;
    AscensionViewModel INavigationHost.AscensionViewModel => AscensionViewModel;
    WorkerProfileViewModel INavigationHost.WorkerProfileViewModel => WorkerProfileViewModel;
    WorkshopViewModel INavigationHost.WorkshopViewModel => WorkshopViewModel;
    MissionsFeatureViewModel INavigationHost.MissionsVM => MissionsVM;
    DialogViewModel INavigationHost.DialogVM => DialogVM;
    MiniGameViewModels INavigationHost.MiniGames => MiniGames;

    BaseMiniGameViewModel? INavigationHost.ActiveMiniGameViewModel => ActiveMiniGameViewModel;

    // ── QuickJob-State ───────────────────────────────────────────────
    QuickJob? INavigationHost.ActiveQuickJob
    {
        get => _activeQuickJob;
        set => _activeQuickJob = value;
    }

    bool INavigationHost.QuickJobMiniGamePlayed
    {
        get => _quickJobMiniGamePlayed;
        set => _quickJobMiniGamePlayed = value;
    }

    bool INavigationHost.IsTournamentRound
    {
        get => _isTournamentRound;
        set => _isTournamentRound = value;
    }

    // ── Hints/Services ───────────────────────────────────────────────
    void INavigationHost.HideBanner() => _adService.HideBanner();

    string INavigationHost.GetLocalizedString(string key, string fallback)
        => _localizationService.GetString(key) ?? fallback;

    // ── Dialog-Dismiss-Hilfen (werden vom DialogOrchestrator benutzt) ──
    // Welcome-Flow-Dialoge liegen in WelcomeFlowVM — hier nur Weiterleitung.
    void INavigationHost.CollectOfflineEarningsNormal() => WelcomeFlowVM.CollectOfflineEarningsNormal();
    void INavigationHost.DismissCombinedDialog() => WelcomeFlowVM.DismissCombinedDialog();
    void INavigationHost.CheckDeferredDialogs() => WelcomeFlowVM.CheckDeferredDialogs();
    void INavigationHost.HideLuckySpinOverlay() => HideLuckySpinOverlay();

    // ── Double-Back-to-Exit ──────────────────────────────────────────
    bool INavigationHost.HandleDoubleBackExit()
    {
        var msg = _localizationService.GetString("PressBackAgainToExit") ?? "Press back again to exit";
        return _backPressHelper.HandleDoubleBack(msg);
    }

    // ── IWelcomeFlowHost (schmale Bruecke fuer WelcomeFlowViewModel) ──
    bool Services.Interfaces.IWelcomeFlowHost.IsHoldingUpgrade => IsHoldingUpgrade;
    void Services.Interfaces.IWelcomeFlowHost.NavigateToShop() => NavigateToShop();

    // ── IStartupHost (schmale Bruecke fuer GameStartupCoordinator) ──
    bool Services.Interfaces.IStartupHost.IsLoading
    {
        get => IsLoading;
        set => IsLoading = value;
    }
    void Services.Interfaces.IStartupHost.RefreshFromState() => RefreshFromState();
    void Services.Interfaces.IStartupHost.RefreshOrders() => RefreshOrders();

    // ── IProgressionFeedbackHost (schmale Bruecke fuer ProgressionFeedbackCoordinator) ──
    bool Services.Interfaces.IProgressionFeedbackHost.IsHoldingUpgrade => IsHoldingUpgrade;
    bool Services.Interfaces.IProgressionFeedbackHost.IsAnyDialogVisible => IsAnyDialogVisible;
    void Services.Interfaces.IProgressionFeedbackHost.RefreshWorkshops() => RefreshWorkshops();
    void Services.Interfaces.IProgressionFeedbackHost.RefreshSingleWorkshop(Models.Enums.WorkshopType type)
        => RefreshSingleWorkshop(type);
    void Services.Interfaces.IProgressionFeedbackHost.RefreshEternalMastery() => RefreshEternalMastery();
    void Services.Interfaces.IProgressionFeedbackHost.NotifyAutomationUnlockChanged()
    {
        OnPropertyChanged(nameof(IsAutoCollectUnlocked));
        OnPropertyChanged(nameof(IsAutoAcceptUnlocked));
        OnPropertyChanged(nameof(IsAutoAssignUnlocked));
    }
    void Services.Interfaces.IProgressionFeedbackHost.SetTutorialHintVisible(bool visible)
        => ShowTutorialHint = visible;

    // ── IGameTickHost (Bruecke fuer GameTickCoordinator) ──
    Models.Enums.ActivePage Services.Interfaces.IGameTickHost.ActivePage => ActivePage;
    bool Services.Interfaces.IGameTickHost.IsWorkerProfileActive => IsWorkerProfileActive;
    bool Services.Interfaces.IGameTickHost.IsRushActive => IsRushActive;
    bool Services.Interfaces.IGameTickHost.CanActivateRush => CanActivateRush;
    bool Services.Interfaces.IGameTickHost.ShowBoostIndicator => ShowBoostIndicator;
    bool Services.Interfaces.IGameTickHost.HasActiveEvent => HasActiveEvent;
    void Services.Interfaces.IGameTickHost.UpdateNetIncomeHeader(Models.GameState state) => UpdateNetIncomeHeader(state);
    void Services.Interfaces.IGameTickHost.UpdateRushDisplay() => UpdateRushDisplay();
    void Services.Interfaces.IGameTickHost.UpdateBoostIndicator() => UpdateBoostIndicator();
    void Services.Interfaces.IGameTickHost.UpdateDeliveryDisplay() => UpdateDeliveryDisplay();
    void Services.Interfaces.IGameTickHost.UpdateEventDisplay() => UpdateEventDisplay();
    void Services.Interfaces.IGameTickHost.UpdateEventTimer() => UpdateEventTimer();
    void Services.Interfaces.IGameTickHost.RefreshReputation(Models.GameState state) => RefreshReputation(state);
    void Services.Interfaces.IGameTickHost.RefreshPrestigeBanner(Models.GameState state) => RefreshPrestigeBanner(state);
    void Services.Interfaces.IGameTickHost.UpdateWorkerWarning(Models.GameState state) => UpdateWorkerWarning(state);
}
