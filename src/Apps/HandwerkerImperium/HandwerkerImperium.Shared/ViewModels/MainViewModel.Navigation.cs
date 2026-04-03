using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.ViewModels.MiniGames;
using MeineApps.Core.Ava.Services;
using HandwerkerImperium.Helpers;

namespace HandwerkerImperium.ViewModels;

// Partielle Klasse: Tab-Auswahl, Navigation, Back-Button, Child-Navigation-Routing
public sealed partial class MainViewModel
{
    // ═══════════════════════════════════════════════════════════════════════
    // TAB SELECTION COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SelectDashboardTab()
    {
        ActivePage = ActivePage.Dashboard;

        // Dashboard-Sub-Tabs zurücksetzen wenn QuickJobs noch nicht freigeschaltet (z.B. nach Prestige)
        if (!IsQuickJobsUnlocked && IsQuickJobsTabActive)
        {
            IsOrdersTabActive = true;
            IsQuickJobsTabActive = false;
        }

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
    private void SelectSettingsTab()
    {
        ActivePage = ActivePage.Settings;
    }

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
    // NAVIGATE-TO COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void NavigateToSettings() => SelectSettingsTab();

    [RelayCommand]
    private void NavigateToShop() => SelectShopTab();

    [RelayCommand]
    private void NavigateToStatistics() => SelectStatisticsTab();

    [RelayCommand]
    private void NavigateToAchievements() => SelectAchievementsTab();

    /// <summary>
    /// Navigiert zum Imperium-Tab (Gebäude sind dort inline).
    /// </summary>
    [RelayCommand]
    private void NavigateToBuildings() => SelectBuildingsTab();

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

    private readonly BackPressHelper _backPressHelper = new();

    /// <summary>
    /// Behandelt die Zurück-Taste. Gibt true zurück wenn konsumiert (App bleibt offen),
    /// false wenn die App geschlossen werden darf (Double-Back).
    /// Reihenfolge: Dialoge → Overlays → Sub-Seiten → Haupt-Tabs → Dashboard → Double-Back-to-Exit.
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. Offene Dialoge schließen (höchste Priorität)
        if (DialogVM.IsHintVisible) { DialogVM.DismissHintCommand.Execute(null); return true; }
        if (IsLuckySpinVisible) { HideLuckySpinOverlay(); return true; }
        if (IsCombinedWelcomeDialogVisible) { DismissCombinedDialog(); return true; }
        if (MissionsVM.IsWelcomeOfferVisible) { MissionsVM.DismissWelcomeOfferCommand.Execute(null); return true; }
        if (DialogVM.IsConfirmDialogVisible) { DialogVM.ConfirmDialogCancelCommand.Execute(null); return true; }
        if (DialogVM.IsPrestigeSummaryVisible) { DialogVM.DismissPrestigeSummaryCommand.Execute(null); return true; }
        if (DialogVM.IsAlertDialogVisible) { DialogVM.DismissAlertDialogCommand.Execute(null); return true; }
        if (DialogVM.IsAchievementDialogVisible) { DialogVM.DismissAchievementDialogCommand.Execute(null); return true; }
        if (DialogVM.IsLevelUpDialogVisible) { DialogVM.DismissLevelUpDialogCommand.Execute(null); return true; }
        if (IsOfflineEarningsDialogVisible) { CollectOfflineEarningsNormal(); return true; }
        if (IsDailyRewardDialogVisible) { IsDailyRewardDialogVisible = false; CheckDeferredDialogs(); return true; }
        if (DialogVM.IsStoryDialogVisible) { DialogVM.DismissStoryDialogCommand.Execute(null); return true; }

        // 2. Overlay schließen (ActivePage bleibt erhalten)
        if (IsWorkerProfileActive)
        {
            IsWorkerProfileActive = false;
            return true;
        }

        // 3. Seitenbasierte Rücknavigation via ActivePage
        switch (ActivePage)
        {
            // MiniGames → Dashboard
            case ActivePage.SawingGame or ActivePage.PipePuzzle or ActivePage.WiringGame or
                 ActivePage.PaintingGame or ActivePage.RoofTilingGame or ActivePage.BlueprintGame or
                 ActivePage.DesignPuzzleGame or ActivePage.InspectionGame or
                 ActivePage.ForgeGame or ActivePage.InventGame:
            case ActivePage.WorkshopDetail or ActivePage.OrderDetail:
                SelectDashboardTab();
                return true;

            // Guild-Sub-Seiten → Guild-Hub
            case ActivePage.GuildResearch or ActivePage.GuildMembers or ActivePage.GuildInvite or
                 ActivePage.GuildWarSeason or ActivePage.GuildBoss or ActivePage.GuildHall or
                 ActivePage.GuildAchievements or ActivePage.GuildChat or ActivePage.GuildWar:
                SelectGuildTab();
                return true;

            // Imperium-Sub-Views → Imperium-Tab
            case ActivePage.WorkerMarket or ActivePage.Research or ActivePage.Manager or
                 ActivePage.Crafting or ActivePage.Ascension:
                SelectBuildingsTab();
                return true;

            // Missionen-Sub-Views → Missionen-Tab
            case ActivePage.Tournament or ActivePage.SeasonalEvent or ActivePage.BattlePass:
            case ActivePage.Statistics or ActivePage.Achievements:
                SelectMissionenTab();
                return true;

            // Nicht-Dashboard Haupt-Tabs → Dashboard
            case ActivePage.Shop or ActivePage.Settings or
                 ActivePage.Buildings or ActivePage.Missionen or ActivePage.Guild:
                SelectDashboardTab();
                return true;

            // Dashboard → Double-Back-to-Exit
            case ActivePage.Dashboard:
                var msg = _localizationService.GetString("PressBackAgainToExit") ?? "Erneut drücken zum Beenden";
                return _backPressHelper.HandleDoubleBack(msg);

            default:
                SelectDashboardTab();
                return true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHILD NAVIGATION HANDLER
    // ═══════════════════════════════════════════════════════════════════════

    private void OnChildNavigation(string route)
    {
        // Relative Route: "../minigame/..." → strip "../" und als MiniGame-Navigation behandeln
        if (route.StartsWith("../") && route.Length > 3 && route[3] != '.')
        {
            OnChildNavigation(route[3..]);
            return;
        }

        // Zurück-Navigation: ".." oder "../.."
        if (route is ".." or "../..")
        {
            // Worker-Profile Overlay: nur schließen, darunterliegende View beibehalten
            if (IsWorkerProfileActive)
            {
                IsWorkerProfileActive = false;
                return;
            }

            // QuickJob-Rückkehr: Belohnung nur vergeben wenn MiniGame tatsächlich gespielt wurde
            if (_activeQuickJob != null)
            {
                if (_quickJobMiniGamePlayed)
                {
                    _activeQuickJob.IsCompleted = true;
                    decimal reward = _activeQuickJob.IsScoreDoubled ? _activeQuickJob.Reward * 2 : _activeQuickJob.Reward;
                    int xp = _activeQuickJob.IsScoreDoubled ? _activeQuickJob.XpReward * 2 : _activeQuickJob.XpReward;
                    _gameStateService.AddMoney(reward);
                    _gameStateService.AddXp(xp);
                    _gameStateService.State.TotalQuickJobsCompleted++;
                    _quickJobService?.NotifyJobCompleted(_activeQuickJob);
                    _dailyChallengeService.OnQuickJobCompleted();
                }
                _activeQuickJob = null;
                _quickJobMiniGamePlayed = false;
                _gameStateService.State.ActiveQuickJob = null;
                MissionsVM.RefreshQuickJobs();
            }

            // Rücknavigation basierend auf aktuellem ActivePage
            switch (ActivePage)
            {
                // Guild-Sub-Seiten → Guild-Hub
                case ActivePage.GuildResearch or ActivePage.GuildMembers or ActivePage.GuildInvite or
                     ActivePage.GuildWarSeason or ActivePage.GuildBoss or ActivePage.GuildHall or
                     ActivePage.GuildAchievements or ActivePage.GuildChat or ActivePage.GuildWar:
                    SelectGuildTab();
                    return;

                // Imperium-Sub-Views → Imperium-Tab
                case ActivePage.WorkerMarket or ActivePage.Research or ActivePage.Manager or
                     ActivePage.Crafting or ActivePage.Ascension:
                    SelectBuildingsTab();
                    RefreshFromState();
                    return;

                // Missionen-Sub-Views → Missionen-Tab
                case ActivePage.Tournament or ActivePage.SeasonalEvent or ActivePage.BattlePass or
                     ActivePage.Statistics or ActivePage.Achievements:
                    SelectMissionenTab();
                    RefreshFromState();
                    return;
            }

            SelectDashboardTab();
            RefreshFromState();
            return;
        }

        // "//main" = Reset zur Hauptseite (z.B. von Einstellungen)
        if (route == "//main")
        {
            SelectDashboardTab();
            RefreshFromState();
            return;
        }

        // Direkt-Routen zu Haupt-Tabs
        if (route == "dashboard") { SelectDashboardTab(); return; }
        if (route == "imperium") { SelectBuildingsTab(); return; }
        if (route == "statistics") { SelectStatisticsTab(); return; }
        if (route == "research") { SelectResearchTab(); return; }
        if (route == "workers") { SelectWorkerMarketTab(); return; }

        // Prestige-Bestätigung (z.B. von GoalService)
        if (route == "prestige")
        {
            NavigateToPrestigeAsync().SafeFireAndForget();
            return;
        }

        // MiniGame-Navigation: "minigame/sawing?orderId=X"
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

            if (string.IsNullOrEmpty(orderId))
                orderId = _gameStateService.GetActiveOrder()?.Id ?? "";

            NavigateToMiniGame(routePart, orderId);
            return;
        }

        // Feature-Views (Imperium/Missionen/Gilden Sub-Seiten)
        if (route is "manager" or "tournament" or "seasonal_event" or "battle_pass" or "guild" or "crafting")
        {
            // Tab-Lock-Guards
            if (route == "guild" && IsTabLocked(3)) { SelectDashboardTab(); return; }
            if (route is "manager" or "crafting" && IsTabLocked(1)) { SelectDashboardTab(); return; }

            switch (route)
            {
                case "manager": ActivePage = ActivePage.Manager; ManagerViewModel.RefreshManagers(); break;
                case "tournament": ActivePage = ActivePage.Tournament; TournamentViewModel.RefreshTournament(); break;
                case "seasonal_event": ActivePage = ActivePage.SeasonalEvent; SeasonalEventViewModel.RefreshEvent(); break;
                case "battle_pass":
                    ActivePage = ActivePage.BattlePass;
                    BattlePassViewModel.RefreshBattlePass();
                    _contextualHintService.TryShowHint(ContextualHints.BattlePass);
                    break;
                case "guild":
                    ActivePage = ActivePage.Guild;
                    GuildViewModel.RefreshGuild();
                    break;
                case "crafting":
                    ActivePage = ActivePage.Crafting;
                    CraftingViewModel.RefreshCrafting();
                    _contextualHintService.TryShowHint(ContextualHints.CraftingHint);
                    break;
            }
            return;
        }

        // Gilden-Sub-Seiten (von GuildView-Hub aus)
        if (route is "guild_research" or "guild_members" or "guild_invite" or
            "guild_war_season" or "guild_boss" or "guild_hall" or "guild_achievements" or
            "guild_chat" or "guild_war")
        {
            if (IsTabLocked(3)) { SelectDashboardTab(); return; }

            switch (route)
            {
                case "guild_research": ActivePage = ActivePage.GuildResearch; break;
                case "guild_members": ActivePage = ActivePage.GuildMembers; break;
                case "guild_invite": ActivePage = ActivePage.GuildInvite; break;
                case "guild_war_season":
                    ActivePage = ActivePage.GuildWarSeason;
                    GuildViewModel.WarSeasonViewModel.RefreshWar();
                    break;
                case "guild_boss":
                    ActivePage = ActivePage.GuildBoss;
                    GuildViewModel.BossViewModel.RefreshBoss();
                    break;
                case "guild_hall":
                    ActivePage = ActivePage.GuildHall;
                    GuildViewModel.HallViewModel.RefreshHall();
                    break;
                case "guild_achievements":
                    ActivePage = ActivePage.GuildAchievements;
                    break;
                case "guild_chat":
                    ActivePage = ActivePage.GuildChat;
                    GuildViewModel.LoadChatMessagesAsync().SafeFireAndForget();
                    GuildViewModel.StartChatPolling();
                    break;
                case "guild_war":
                    ActivePage = ActivePage.GuildWar;
                    GuildViewModel.LoadWarStatusAsync().SafeFireAndForget();
                    break;
            }
            return;
        }

        // Ascension-Ansicht
        if (route == "ascension")
        {
            ActivePage = ActivePage.Ascension;
            AscensionViewModel.LoadData();
            return;
        }

        // Worker-Profile als Overlay (ActivePage bleibt erhalten)
        if (route.StartsWith("worker?id="))
        {
            var workerId = route.Replace("worker?id=", "");
            WorkerProfileViewModel.SetWorker(workerId);
            IsWorkerProfileActive = true;
            _adService.HideBanner();
            return;
        }

        // Workshop-Detail
        if (route.StartsWith("workshop?type="))
        {
            var typeStr = route.Replace("workshop?type=", "");
            if (int.TryParse(typeStr, out var typeInt))
            {
                WorkshopViewModel.SetWorkshopType(typeInt);
                ActivePage = ActivePage.WorkshopDetail;
            }
            return;
        }
    }

    // NAV-2: MiniGame-Abbruch-Warnung Hilfsmethoden
    private bool IsAnyMiniGamePlaying()
    {
        var vm = ActiveMiniGameViewModel;
        return vm != null && (vm.IsPlaying || vm.IsCountdownActive);
    }

    private async Task ConfirmMiniGameAbortAsync()
    {
        var title = _localizationService.GetString("MiniGameAbortTitle") ?? "MiniGame abbrechen?";
        var msg = _localizationService.GetString("MiniGameAbortMessage") ?? "Dein Fortschritt geht verloren.";
        var confirm = _localizationService.GetString("MiniGameAbortConfirm") ?? "Abbrechen";
        var cancel = _localizationService.GetString("Cancel") ?? "Zurück";
        bool confirmed = await DialogVM.ShowConfirmDialog(title, msg, confirm, cancel);
        if (confirmed) { StopCurrentMiniGame(); SelectDashboardTab(); }
    }

    private void StopCurrentMiniGame()
    {
        ActiveMiniGameViewModel?.StopGame();
    }

    /// <summary>
    /// Handler fuer MissionsFeatureVM: QuickJob-State setzen und zum MiniGame navigieren.
    /// </summary>
    private void OnMissionsNavigateToMiniGame(string route, string orderId)
    {
        // QuickJob-State wird hier im MainViewModel verwaltet (Navigation braucht ihn)
        var job = MissionsVM.QuickJobs.FirstOrDefault(j => j.MiniGameType.GetRoute() == route && !j.IsCompleted);
        if (job != null)
        {
            _activeQuickJob = job;
            _quickJobMiniGamePlayed = false;
            _gameStateService.State.ActiveQuickJob = job;
        }

        NavigateToMiniGame(route, orderId);
    }

    /// <summary>
    /// Navigiert zum MiniGame basierend auf der Route.
    /// </summary>
    /// <summary>
    /// Route-Mapping: MiniGame-Route → (VM-Accessor, ActivePage). Statisch, keine Allokation.
    /// </summary>
    private static readonly Dictionary<string, ActivePage> s_miniGameRoutes = new()
    {
        ["minigame/sawing"] = ActivePage.SawingGame,
        ["minigame/pipes"] = ActivePage.PipePuzzle,
        ["minigame/wiring"] = ActivePage.WiringGame,
        ["minigame/painting"] = ActivePage.PaintingGame,
        ["minigame/rooftiling"] = ActivePage.RoofTilingGame,
        ["minigame/blueprint"] = ActivePage.BlueprintGame,
        ["minigame/designpuzzle"] = ActivePage.DesignPuzzleGame,
        ["minigame/inspection"] = ActivePage.InspectionGame,
        ["minigame/forge"] = ActivePage.ForgeGame,
        ["minigame/invent"] = ActivePage.InventGame,
    };

    private void NavigateToMiniGame(string routePart, string orderId)
    {
        if (!s_miniGameRoutes.TryGetValue(routePart, out var page)) return;

        // Seite setzen damit ActiveMiniGameViewModel das richtige VM liefert
        ActivePage = page;
        ActiveMiniGameViewModel?.SetOrderId(orderId);
    }
}
