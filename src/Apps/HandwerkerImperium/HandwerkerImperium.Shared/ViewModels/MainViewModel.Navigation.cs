using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels.MiniGames;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.ViewModels;

// Partial Class: Navigation-Forwarder (Phase 2 — velvety-booping-peacock).
// Die eigentliche Route-/Tab-Logik lebt in NavigationService, DialogOrchestrator, MiniGameNavigator.
// Hier verbleiben nur RelayCommand-Wrapper + ActivePage-Setter-Side-Effects (Back-Stack,
// GuildChat-Stop) und Host-Interface-Implementierung fuer Zugriffe der Services.
public sealed partial class MainViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // TAB-AUSWAHL COMMANDS (RelayCommand-Forwarder an NavigationService)
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SelectDashboardTab()
    {
        ActivePage = ActivePage.Dashboard;
        // Dashboard-Sub-Tabs zuruecksetzen wenn QuickJobs noch nicht freigeschaltet
        if (!IsQuickJobsUnlocked && IsQuickJobsTabActive)
        {
            IsOrdersTabActive = true;
            IsQuickJobsTabActive = false;
        }
        if (_gameStateService.IsInitialized && _gameStateService.State.AvailableOrders.Count == 0)
        {
            _orderGeneratorService.RefreshOrders();
            RefreshOrders();
        }
        else if (_gameStateService.IsInitialized)
        {
            // v2.0.35 Bugfix: Auch wenn Orders schon existieren, Werte neu berechnen.
            // Sonst zeigen alte Orders veraltete Rewards (vor Workshop-Upgrade/Prestige).
            RefreshOrders();
        }
    }

    [RelayCommand]
    private void SelectStatisticsTab()
    {
        ActivePage = ActivePage.Statistics;
        StatisticsViewModel.RefreshStatisticsCommand.Execute(null);
    }

    [RelayCommand]
    private void SelectAchievementsTab()
    {
        ActivePage = ActivePage.Achievements;
        AchievementsViewModel.LoadAchievements();
    }

    [RelayCommand]
    private void SelectShopTab()
    {
        if (IsTabLocked(4)) { SelectDashboardTab(); return; }
        ActivePage = ActivePage.Shop;
        ShopViewModel.LoadShopData();
        _contextualHintService.TryShowHint(ContextualHints.ShopHint);
    }

    [RelayCommand]
    private void SelectSettingsTab() => ActivePage = ActivePage.Settings;

    [RelayCommand]
    internal void SelectWorkerMarketTab()
    {
        ActivePage = ActivePage.WorkerMarket;
        WorkerMarketViewModel.LoadMarket();
    }

    [RelayCommand]
    private void SelectBuildingsTab()
    {
        if (IsTabLocked(1)) { SelectDashboardTab(); return; }
        ActivePage = ActivePage.Buildings;
        BuildingsViewModel.LoadBuildings();
        CraftingViewModel.RefreshCrafting();
        ResearchViewModel.LoadResearchTree();
        _contextualHintService.TryShowHint(ContextualHints.BuildingHint);
    }

    [RelayCommand]
    private void SelectMissionenTab()
    {
        if (IsTabLocked(2)) { SelectDashboardTab(); return; }
        ActivePage = ActivePage.Missionen;
        _contextualHintService.TryShowHint(ContextualHints.DailyChallenge);
    }

    /// <summary>Dashboard-interner Umschalter: Auftraege-Tab anzeigen.</summary>
    [RelayCommand]
    private void ShowOrdersTab()
    {
        IsOrdersTabActive = true;
        IsQuickJobsTabActive = false;
    }

    /// <summary>Dashboard-interner Umschalter: Schnelljobs-Tab anzeigen.</summary>
    [RelayCommand]
    private void ShowQuickJobsTab()
    {
        IsOrdersTabActive = false;
        IsQuickJobsTabActive = true;
        _contextualHintService.TryShowHint(ContextualHints.QuickJobs);
    }

    [RelayCommand]
    private void SelectResearchTab()
    {
        ActivePage = ActivePage.Research;
        ResearchViewModel.LoadResearchTree();
        _contextualHintService.TryShowHint(ContextualHints.ResearchHint);
    }

    [RelayCommand]
    private void SelectGuildTab()
    {
        if (IsTabLocked(3)) { SelectDashboardTab(); return; }
        ActivePage = ActivePage.Guild;
        GuildViewModel.RefreshGuild();
        _contextualHintService.TryShowHint(ContextualHints.GuildHint);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NAVIGATE-TO COMMANDS (Route-basiert, Forwarder an NavigationService)
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand] private void NavigateToSettings() => SelectSettingsTab();
    [RelayCommand] private void NavigateToShop() => SelectShopTab();
    [RelayCommand] private void NavigateToStatistics() => SelectStatisticsTab();
    [RelayCommand] private void NavigateToAchievements() => SelectAchievementsTab();
    [RelayCommand] private void NavigateToBuildings() => SelectBuildingsTab();
    [RelayCommand] private void NavigateToWorkerMarket() => OnChildNavigation("workers");

    /// <summary>Navigiert direkt zum schlimmsten Worker (aus Worker-Warning-Chip).</summary>
    [RelayCommand]
    private void NavigateToWorkerWarning()
    {
        if (!string.IsNullOrEmpty(_worstWorkerId))
            OnChildNavigation($"worker?id={_worstWorkerId}");
        else
            OnChildNavigation("workers");
    }

    [RelayCommand] private void NavigateToResearch() => OnChildNavigation("research");
    [RelayCommand] private void NavigateToManager() => OnChildNavigation("manager");
    [RelayCommand] private void NavigateToTournament() => OnChildNavigation("tournament");
    [RelayCommand] private void NavigateToSeasonalEvent() => OnChildNavigation("seasonal_event");
    [RelayCommand] private void NavigateToBattlePass() => OnChildNavigation("battle_pass");
    [RelayCommand] private void NavigateToGuild() => OnChildNavigation("guild");
    [RelayCommand] private void NavigateToCrafting() => OnChildNavigation("crafting");

    [RelayCommand]
    private async Task NavigateToPrestigeAsync() => await ShowPrestigeConfirmationAsync();

    /// <summary>
    /// v2.0.37: Public RelayCommand fuer die PrestigeView-Back-Aktion (und andere Views,
    /// die an MainViewModel gebunden sind). Delegiert an die interne Stack-Navigation.
    /// </summary>
    [RelayCommand]
    private void GoBack() => NavigateBack();

    // ═══════════════════════════════════════════════════════════════════════
    // BACK-NAVIGATION
    // ═══════════════════════════════════════════════════════════════════════

    private readonly BackPressHelper _backPressHelper = new();

    /// <summary>Stack-basierte Rueck-Navigation. Fallback: Dashboard.</summary>
    internal void NavigateBack()
    {
        // v2.0.35 Bugfix: Beim Back aus MiniGame oder OrderDetail pausieren wir den aktiven
        // Auftrag (bleibt im ParallelOrdersByWorkshop → Fortsetzen-Banner) und springen
        // direkt zum Dashboard. Stack-Pop wuerde auf OrderDetail fuer einen gerade pausierten
        // Auftrag landen — der OrderDetail-Screen haette dann keinen Order mehr (Dead-End).
        bool fromActiveOrderScreen = (ActivePage == ActivePage.OrderDetail
                                       || s_miniGamePages.Contains(ActivePage))
                                      && _gameStateService.GetActiveOrder() != null;

        if (fromActiveOrderScreen)
        {
            // v2.0.35 Hotfix-2: MiniGame-Timer explizit stoppen BEVOR pausiert wird.
            // Ohne StopCurrent laeuft der VM-interne DispatcherTimer (IsPlaying=true)
            // nach dem Navigationswechsel weiter → pseudo-Miss nach Timeout auf dem
            // Dashboard (Sound + Stat-Inkrement + PerfectStreak-Reset).
            _miniGameNavigator?.StopCurrent();

            _gameStateService.PauseActiveOrder();
            HasActiveOrder = false;
            ActiveOrder = null;

            _navigationStack.Clear();
            _isNavigatingBack = true;
            try { ActivePage = ActivePage.Dashboard; }
            finally { _isNavigatingBack = false; }
            RefreshFromState();
            return;
        }

        _isNavigatingBack = true;
        try
        {
            if (_navigationStack.Count > 0)
            {
                var target = _navigationStack.Pop();
                ActivePage = target;
            }
            else
            {
                ActivePage = ActivePage.Dashboard;
            }
        }
        finally
        {
            _isNavigatingBack = false;
        }
        RefreshFromState();
    }

    /// <summary>
    /// Set der MiniGame-Seiten fuer schnellen Contains-Check in NavigateBack.
    /// </summary>
    private static readonly HashSet<ActivePage> s_miniGamePages = new()
    {
        ActivePage.SawingGame, ActivePage.PipePuzzle, ActivePage.WiringGame,
        ActivePage.PaintingGame, ActivePage.RoofTilingGame, ActivePage.BlueprintGame,
        ActivePage.DesignPuzzleGame, ActivePage.InspectionGame,
        ActivePage.ForgeGame, ActivePage.InventGame
    };

    /// <summary>
    /// Behandelt die Zurueck-Taste. Reihenfolge:
    /// 1. DialogOrchestrator.TryDismissTopmost (Dialoge + Overlays)
    /// 2. Stack-basierte Rueck-Navigation
    /// 3. Dashboard → Double-Back-to-Exit
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. Dialog-/Overlay-Kaskade (delegiert an DialogOrchestrator)
        if (_dialogOrchestrator?.TryDismissTopmost() == true) return true;

        // 2. Auf Dashboard: Double-Back-to-Exit
        if (ActivePage == ActivePage.Dashboard)
        {
            var msg = _localizationService.GetString("PressBackAgainToExit") ?? "Press back again to exit";
            return _backPressHelper.HandleDoubleBack(msg);
        }

        // 3. Ansonsten: Stack-basiert zurueck
        NavigateBack();
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHILD NAVIGATION — Forwarder an NavigationService (Schritt 4)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Nimmt Routes von Child-VMs entgegen und delegiert an den NavigationService.
    /// Die Logik lebt vollstaendig in <see cref="NavigationService.NavigateToRoute"/>.
    /// </summary>
    private void OnChildNavigation(string route)
    {
        _navigationService?.NavigateToRoute(route);
    }

    /// <summary>
    /// Handler fuer MissionsFeatureVM: QuickJob-State setzen und zum MiniGame navigieren.
    /// Bleibt im MainViewModel, weil _activeQuickJob ein reiner MainViewModel-State ist.
    /// </summary>
    private void OnMissionsNavigateToMiniGame(string route, string orderId)
    {
        var job = MissionsVM.QuickJobs.FirstOrDefault(j => j.MiniGameType.GetRoute() == route && !j.IsCompleted);
        if (job != null)
        {
            _activeQuickJob = job;
            _quickJobMiniGamePlayed = false;
            _gameStateService.State.ActiveQuickJob = job;
        }

        _miniGameNavigator?.NavigateToMiniGame(route, orderId);
    }

    // Legacy-Helpers die noch an einigen Stellen aus MainViewModel aufgerufen werden
    private bool IsAnyMiniGamePlaying() => _miniGameNavigator?.IsAnyMiniGamePlaying() ?? false;
    private Task ConfirmMiniGameAbortAsync() => _miniGameNavigator?.ConfirmMiniGameAbortAsync() ?? Task.CompletedTask;
    private void StopCurrentMiniGame() => _miniGameNavigator?.StopCurrent();
    private void NavigateToMiniGame(string routePart, string orderId) => _miniGameNavigator?.NavigateToMiniGame(routePart, orderId);
}
