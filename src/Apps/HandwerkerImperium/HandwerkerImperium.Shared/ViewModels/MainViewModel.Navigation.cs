using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;

namespace HandwerkerImperium.ViewModels;

// Partielle Klasse: Tab-Auswahl, Navigation, Back-Button, Child-Navigation-Routing
public partial class MainViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // TAB SELECTION COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SelectDashboardTab()
    {
        DeactivateAllTabs();
        IsDashboardActive = true;
        NotifyTabBarVisibility();

        // Aufträge sicherstellen (falls leer z.B. nach Spielabbruch)
        if (_gameStateService.IsInitialized && _gameStateService.State.AvailableOrders.Count == 0)
        {
            _orderGeneratorService.RefreshOrders();
            RefreshOrders();
        }
    }

    [RelayCommand]
    private void SelectStatisticsTab()
    {
        DeactivateAllTabs();
        IsStatisticsActive = true;
        NotifyTabBarVisibility();
        StatisticsViewModel.RefreshStatisticsCommand.Execute(null);
    }

    [RelayCommand]
    private void SelectAchievementsTab()
    {
        DeactivateAllTabs();
        IsAchievementsActive = true;
        AchievementsViewModel.LoadAchievements();
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void SelectShopTab()
    {
        DeactivateAllTabs();
        IsShopActive = true;
        // Geldpakete-Beträge aktualisieren (basieren auf aktuellem Einkommen)
        ShopViewModel.LoadShopData();
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void SelectSettingsTab()
    {
        DeactivateAllTabs();
        IsSettingsActive = true;
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void SelectWorkerMarketTab()
    {
        DeactivateAllTabs();
        IsWorkerMarketActive = true;
        WorkerMarketViewModel.LoadMarket();
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void SelectBuildingsTab()
    {
        DeactivateAllTabs();
        IsBuildingsActive = true;
        BuildingsViewModel.LoadBuildings();
        CraftingViewModel.RefreshCrafting();
        ResearchViewModel.LoadResearchTree();
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void SelectMissionenTab()
    {
        DeactivateAllTabs();
        IsMissionenActive = true;
        NotifyTabBarVisibility();
    }

    /// <summary>
    /// Dashboard-interner Umschalter: Aufträge-Tab anzeigen.
    /// </summary>
    [RelayCommand]
    private void ShowOrdersTab()
    {
        IsOrdersTabActive = true;
        IsQuickJobsTabActive = false;
    }

    /// <summary>
    /// Dashboard-interner Umschalter: Schnelljobs-Tab anzeigen.
    /// </summary>
    [RelayCommand]
    private void ShowQuickJobsTab()
    {
        IsOrdersTabActive = false;
        IsQuickJobsTabActive = true;
    }

    [RelayCommand]
    private void SelectResearchTab()
    {
        DeactivateAllTabs();
        IsResearchActive = true;
        ResearchViewModel.LoadResearchTree();
        NotifyTabBarVisibility();
    }

    [RelayCommand]
    private void SelectGuildTab()
    {
        DeactivateAllTabs();
        IsGuildActive = true;
        GuildViewModel.RefreshGuild();
        NotifyTabBarVisibility();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NAVIGATE-TO COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void NavigateToSettings()
    {
        SelectSettingsTab();
    }

    [RelayCommand]
    private void NavigateToShop()
    {
        SelectShopTab();
    }

    [RelayCommand]
    private void NavigateToStatistics()
    {
        SelectStatisticsTab();
    }

    [RelayCommand]
    private void NavigateToAchievements()
    {
        SelectAchievementsTab();
    }

    /// <summary>
    /// Navigiert zum Imperium-Tab (Gebäude sind dort inline).
    /// </summary>
    [RelayCommand]
    private void NavigateToBuildings()
    {
        SelectBuildingsTab();
    }

    [RelayCommand]
    private void NavigateToWorkerMarket() => OnChildNavigation("workers");

    [RelayCommand]
    private void NavigateToResearch() => OnChildNavigation("research");

    [RelayCommand]
    private void NavigateToManager() => OnChildNavigation("manager");

    [RelayCommand]
    private void NavigateToTournament() => OnChildNavigation("tournament");

    [RelayCommand]
    private void NavigateToSeasonalEvent() => OnChildNavigation("seasonal_event");

    [RelayCommand]
    private void NavigateToBattlePass() => OnChildNavigation("battle_pass");

    [RelayCommand]
    private void NavigateToGuild() => OnChildNavigation("guild");

    [RelayCommand]
    private void NavigateToCrafting() => OnChildNavigation("crafting");

    /// <summary>
    /// Navigiert zum Prestige (Statistik-Tab wo Prestige angezeigt wird).
    /// </summary>
    [RelayCommand]
    private async Task NavigateToPrestigeAsync()
    {
        await ShowPrestigeConfirmationAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BACK-NAVIGATION (Double-Back-to-Exit)
    // ═══════════════════════════════════════════════════════════════════════

    private DateTime _lastBackPress = DateTime.MinValue;
    private const int BackPressIntervalMs = 2000;

    /// <summary>
    /// Behandelt die Zurück-Taste. Gibt true zurück wenn konsumiert (App bleibt offen),
    /// false wenn die App geschlossen werden darf (Double-Back).
    /// Reihenfolge: Dialoge → MiniGame/Detail → Sub-Tabs → Dashboard → Double-Back-to-Exit.
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. Offene Dialoge/Overlays schließen (höchste Priorität)
        if (IsLuckySpinVisible) { HideLuckySpin(); return true; }
        if (IsCombinedWelcomeDialogVisible) { DismissCombinedDialog(); return true; }
        if (IsWelcomeOfferVisible) { DismissWelcomeOffer(); return true; }
        if (IsConfirmDialogVisible) { ConfirmDialogCancel(); return true; }
        if (IsAlertDialogVisible) { DismissAlertDialog(); return true; }
        if (IsAchievementDialogVisible) { DismissAchievementDialog(); return true; }
        if (IsLevelUpDialogVisible) { DismissLevelUpDialog(); return true; }
        if (IsOfflineEarningsDialogVisible) { CollectOfflineEarningsNormal(); return true; }
        if (IsDailyRewardDialogVisible) { IsDailyRewardDialogVisible = false; return true; }
        if (IsStoryDialogVisible) { DismissStoryDialog(); return true; }

        // 2. MiniGame aktiv → zurück zum Dashboard
        if (IsSawingGameActive || IsPipePuzzleActive || IsWiringGameActive || IsPaintingGameActive ||
            IsRoofTilingGameActive || IsBlueprintGameActive || IsDesignPuzzleGameActive || IsInspectionGameActive ||
            IsForgeGameActive || IsInventGameActive)
        {
            SelectDashboardTab();
            return true;
        }

        // 3. Worker-Profile Bottom-Sheet → nur Sheet schließen (darunterliegende View bleibt)
        if (IsWorkerProfileActive)
        {
            IsWorkerProfileActive = false;
            NotifyTabBarVisibility();
            return true;
        }

        // 4. Detail-Views → zurück zum Dashboard
        if (IsWorkshopDetailActive || IsOrderDetailActive)
        {
            SelectDashboardTab();
            return true;
        }

        // 5a. Guild-Sub-Seiten → zurück zum Guild-Hub
        if (IsGuildResearchActive || IsGuildMembersActive || IsGuildInviteActive)
        {
            SelectGuildTab();
            return true;
        }

        // 5a. Imperium-Sub-Views → zurück zum Imperium-Tab (Buildings)
        if (IsWorkerMarketActive || IsResearchActive || IsManagerActive || IsCraftingActive)
        {
            SelectBuildingsTab();
            return true;
        }

        // 5b. Missionen-Sub-Views → zurück zum Missionen-Tab
        if (IsTournamentActive || IsSeasonalEventActive || IsBattlePassActive)
        {
            SelectMissionenTab();
            return true;
        }

        // 5c. Statistiken/Achievements → zurück zum Missionen-Tab (von dort erreichbar)
        if (IsStatisticsActive || IsAchievementsActive)
        {
            SelectMissionenTab();
            return true;
        }

        // 6. Nicht-Dashboard-Tabs → zum Dashboard
        if (IsShopActive || IsSettingsActive ||
            IsBuildingsActive || IsMissionenActive || IsGuildActive)
        {
            SelectDashboardTab();
            return true;
        }

        // 7. Auf Dashboard: Double-Back-to-Exit
        var now = DateTime.UtcNow;
        if ((now - _lastBackPress).TotalMilliseconds < BackPressIntervalMs)
            return false; // App beenden lassen

        _lastBackPress = now;
        var msg = _localizationService.GetString("PressBackAgainToExit") ?? "Erneut drücken zum Beenden";
        ExitHintRequested?.Invoke(msg);
        return true; // Konsumiert
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHILD NAVIGATION HANDLER
    // ═══════════════════════════════════════════════════════════════════════

    private void OnChildNavigation(string route)
    {
        // Relative route: "../minigame/..." → strip "../" and handle as minigame navigation
        if (route.StartsWith("../") && route.Length > 3 && route[3] != '.')
        {
            OnChildNavigation(route[3..]);
            return;
        }

        // Pure back navigation: ".." or "../.."
        if (route is ".." or "../..")
        {
            // Worker-Profile Bottom-Sheet: nur schließen, darunterliegende View beibehalten
            if (IsWorkerProfileActive)
            {
                IsWorkerProfileActive = false;
                NotifyTabBarVisibility();
                return;
            }

            // QuickJob-Rückkehr: Belohnung nur vergeben wenn MiniGame tatsächlich gespielt wurde
            if (_activeQuickJob != null)
            {
                if (_quickJobMiniGamePlayed)
                {
                    _activeQuickJob.IsCompleted = true;
                    _gameStateService.AddMoney(_activeQuickJob.Reward);
                    _gameStateService.AddXp(_activeQuickJob.XpReward);
                    _gameStateService.State.TotalQuickJobsCompleted++;
                    (_quickJobService as QuickJobService)?.NotifyJobCompleted(_activeQuickJob);
                    (_dailyChallengeService as DailyChallengeService)?.OnQuickJobCompleted();
                }
                _activeQuickJob = null;
                _quickJobMiniGamePlayed = false;
                _gameStateService.State.ActiveQuickJob = null;
                RefreshQuickJobs();
            }

            // Gilden-Sub-Seiten → zurück zum Guild-Hub
            if (IsGuildResearchActive || IsGuildMembersActive || IsGuildInviteActive)
            {
                SelectGuildTab();
                return;
            }

            // Imperium-Sub-Views → zurück zum Imperium-Tab (Buildings)
            if (IsWorkerMarketActive || IsResearchActive || IsManagerActive || IsCraftingActive)
            {
                SelectBuildingsTab();
                RefreshFromState();
                return;
            }

            // Missionen-Sub-Views → zurück zum Missionen-Tab
            if (IsTournamentActive || IsSeasonalEventActive || IsBattlePassActive ||
                IsStatisticsActive || IsAchievementsActive)
            {
                SelectMissionenTab();
                RefreshFromState();
                return;
            }

            SelectDashboardTab();
            RefreshFromState();
            return;
        }

        // "//main" = reset to main (from settings)
        if (route == "//main")
        {
            SelectDashboardTab();
            RefreshFromState();
            return;
        }

        // "minigame/sawing?orderId=X" or "minigame/sawing?difficulty=X" = navigate to mini-game
        if (route.StartsWith("minigame/"))
        {
            var routePart = route;
            var orderId = "";
            var queryIndex = route.IndexOf('?');
            if (queryIndex >= 0)
            {
                routePart = route[..queryIndex];
                var queryString = route[(queryIndex + 1)..];
                foreach (var param in queryString.Split('&'))
                {
                    var parts = param.Split('=');
                    if (parts.Length == 2 && parts[0] == "orderId")
                        orderId = parts[1];
                }
            }

            // If orderId not in query, get from active order (e.g. difficulty-only route from OrderVM)
            if (string.IsNullOrEmpty(orderId))
                orderId = _gameStateService.GetActiveOrder()?.Id ?? "";

            DeactivateAllTabs();
            NavigateToMiniGame(routePart, orderId);
            NotifyTabBarVisibility();
            return;
        }

        // Neue Feature-Views (Welle 1-8)
        if (route is "manager" or "tournament" or "seasonal_event" or "battle_pass" or "guild" or "crafting")
        {
            DeactivateAllTabs();
            switch (route)
            {
                case "manager": IsManagerActive = true; ManagerViewModel.RefreshManagers(); break;
                case "tournament": IsTournamentActive = true; TournamentViewModel.RefreshTournament(); break;
                case "seasonal_event": IsSeasonalEventActive = true; SeasonalEventViewModel.RefreshEvent(); break;
                case "battle_pass": IsBattlePassActive = true; BattlePassViewModel.RefreshBattlePass(); break;
                case "guild": IsGuildActive = true; GuildViewModel.RefreshGuild(); break;
                case "crafting": IsCraftingActive = true; CraftingViewModel.RefreshCrafting(); break;
            }
            NotifyTabBarVisibility();
            return;
        }

        // Gilden-Sub-Seiten (von GuildView-Hub aus)
        if (route is "guild_research" or "guild_members" or "guild_invite")
        {
            DeactivateAllTabs();
            switch (route)
            {
                case "guild_research": IsGuildResearchActive = true; break;
                case "guild_members": IsGuildMembersActive = true; break;
                case "guild_invite": IsGuildInviteActive = true; break;
            }
            NotifyTabBarVisibility();
            return;
        }

        // "research" = navigiere zum Forschungsbaum (von GuildView aus)
        if (route == "research")
        {
            SelectResearchTab();
            return;
        }

        // "workers" = navigiere zum Arbeitermarkt (von WorkshopView/GuildView aus)
        if (route == "workers")
        {
            SelectWorkerMarketTab();
            return;
        }

        // "worker?id=X" = Worker-Profile als Bottom-Sheet Overlay (ohne Tabs zu deaktivieren)
        if (route.StartsWith("worker?id="))
        {
            var workerId = route.Replace("worker?id=", "");
            WorkerProfileViewModel.SetWorker(workerId);
            IsWorkerProfileActive = true;
            _adService.HideBanner();
            NotifyTabBarVisibility();
            return;
        }

        // "workshop?type=X" = navigate to workshop detail
        if (route.StartsWith("workshop?type="))
        {
            var typeStr = route.Replace("workshop?type=", "");
            if (int.TryParse(typeStr, out var typeInt))
            {
                WorkshopViewModel.SetWorkshopType(typeInt);
                DeactivateAllTabs();
                IsWorkshopDetailActive = true;
                NotifyTabBarVisibility();
            }
            return;
        }
    }

    private void NavigateToMiniGame(string routePart, string orderId)
    {
        switch (routePart)
        {
            case "minigame/sawing":
                SawingGameViewModel.SetOrderId(orderId);
                IsSawingGameActive = true;
                break;
            case "minigame/pipes":
                PipePuzzleViewModel.SetOrderId(orderId);
                IsPipePuzzleActive = true;
                break;
            case "minigame/wiring":
                WiringGameViewModel.SetOrderId(orderId);
                IsWiringGameActive = true;
                break;
            case "minigame/painting":
                PaintingGameViewModel.SetOrderId(orderId);
                IsPaintingGameActive = true;
                break;
            case "minigame/rooftiling":
                RoofTilingGameViewModel.SetOrderId(orderId);
                IsRoofTilingGameActive = true;
                break;
            case "minigame/blueprint":
                BlueprintGameViewModel.SetOrderId(orderId);
                IsBlueprintGameActive = true;
                break;
            case "minigame/designpuzzle":
                DesignPuzzleGameViewModel.SetOrderId(orderId);
                IsDesignPuzzleGameActive = true;
                break;
            case "minigame/inspection":
                InspectionGameViewModel.SetOrderId(orderId);
                IsInspectionGameActive = true;
                break;
            case "minigame/forge":
                ForgeGameViewModel.SetOrderId(orderId);
                IsForgeGameActive = true;
                break;
            case "minigame/invent":
                InventGameViewModel.SetOrderId(orderId);
                IsInventGameActive = true;
                break;
        }
    }
}
