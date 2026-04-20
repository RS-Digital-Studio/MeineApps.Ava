using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Services;

namespace HandwerkerImperium.Services;

/// <summary>
/// Zentrale Navigations-Implementierung (Phase 2 — Schritte 4+5 aus velvety-booping-peacock-Plan).
/// Die gesamte Route-Parsing-Logik und alle SelectXxxTab-Methoden leben hier. MainViewModel
/// haelt nur noch RelayCommand-Wrapper + partielle OnActivePageChanged-Seiteneffekte
/// (Back-Stack-Push, GuildChat-Stop, PropertyChanged-Fan-Out).
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IGameStateService _gameStateService;
    private readonly IOrderGeneratorService _orderGeneratorService;
    private readonly IContextualHintService _contextualHintService;
    private readonly IDailyChallengeService _dailyChallengeService;
    private readonly IQuickJobService? _quickJobService;
    private readonly IMiniGameNavigator _miniGameNavigator;
    private INavigationHost? _host;

    public NavigationService(
        IGameStateService gameStateService,
        IOrderGeneratorService orderGeneratorService,
        IContextualHintService contextualHintService,
        IDailyChallengeService dailyChallengeService,
        IMiniGameNavigator miniGameNavigator,
        IQuickJobService? quickJobService = null)
    {
        _gameStateService = gameStateService;
        _orderGeneratorService = orderGeneratorService;
        _contextualHintService = contextualHintService;
        _dailyChallengeService = dailyChallengeService;
        _miniGameNavigator = miniGameNavigator;
        _quickJobService = quickJobService;
    }

    public void AttachHost(INavigationHost host) => _host = host;

    // ═══════════════════════════════════════════════════════════════════
    // TAB-AUSWAHL
    // ═══════════════════════════════════════════════════════════════════

    public void SelectDashboardTab()
    {
        if (_host == null) return;
        _host.ActivePage = ActivePage.Dashboard;
        // QuickJobs-Sub-Tab nach Prestige ggf. zuruecksetzen
        if (!_host.IsQuickJobsUnlocked)
            _host.MissionsVM?.RefreshQuickJobs(); // no-op wenn null

        // Aufträge sicherstellen (falls leer z.B. nach Spielabbruch)
        if (_gameStateService.IsInitialized && _gameStateService.State.AvailableOrders.Count == 0)
        {
            _orderGeneratorService.RefreshOrders();
            _host.RefreshOrders();
        }
        _host.SelectDashboardTab(); // Host-Hook (Side-Effects wie Ordners/Sub-Tabs)
    }

    public void SelectStatisticsTab()
    {
        if (_host == null) return;
        _host.ActivePage = ActivePage.Statistics;
        _host.SelectStatisticsTab(); // Host ruft StatisticsVM.RefreshStatisticsCommand
    }

    public void SelectAchievementsTab()
    {
        if (_host == null) return;
        _host.ActivePage = ActivePage.Achievements;
        _host.SelectAchievementsTab(); // Host ruft AchievementsVM.LoadAchievements()
    }

    public void SelectShopTab()
    {
        if (_host == null) return;
        if (_host.IsTabLocked(4)) { SelectDashboardTab(); return; }
        _host.ActivePage = ActivePage.Shop;
        _contextualHintService.TryShowHint(ContextualHints.ShopHint);
    }

    public void SelectSettingsTab()
    {
        if (_host == null) return;
        _host.ActivePage = ActivePage.Settings;
    }

    public void SelectWorkerMarketTab()
    {
        if (_host == null) return;
        _host.ActivePage = ActivePage.WorkerMarket;
        _host.SelectWorkerMarketTab(); // lädt Markt
    }

    public void SelectBuildingsTab()
    {
        if (_host == null) return;
        if (_host.IsTabLocked(1)) { SelectDashboardTab(); return; }
        _host.ActivePage = ActivePage.Buildings;
        _host.SelectBuildingsTab(); // lädt Buildings/Crafting/Research
        _contextualHintService.TryShowHint(ContextualHints.BuildingHint);
    }

    public void SelectMissionenTab()
    {
        if (_host == null) return;
        if (_host.IsTabLocked(2)) { SelectDashboardTab(); return; }
        _host.ActivePage = ActivePage.Missionen;
        _contextualHintService.TryShowHint(ContextualHints.DailyChallenge);
    }

    public void SelectResearchTab()
    {
        if (_host == null) return;
        _host.ActivePage = ActivePage.Research;
        _host.SelectResearchTab(); // lädt Tree
        _contextualHintService.TryShowHint(ContextualHints.ResearchHint);
    }

    public void SelectGuildTab()
    {
        if (_host == null) return;
        if (_host.IsTabLocked(3)) { SelectDashboardTab(); return; }
        _host.ActivePage = ActivePage.Guild;
        _host.GuildViewModel.RefreshGuild();
        _contextualHintService.TryShowHint(ContextualHints.GuildHint);
    }

    public void ShowOrdersTab()
    {
        // Dashboard-interner Umschalter — Host haelt die Sub-Tab-Properties
        _host?.SelectDashboardTab();
    }

    public void ShowQuickJobsTab()
    {
        _contextualHintService.TryShowHint(ContextualHints.QuickJobs);
        // Die eigentlichen Bool-Properties (IsOrdersTabActive/IsQuickJobsTabActive)
        // liegen weiter auf MainViewModel; der Host kennt den Umschalt-Hook.
    }

    // ═══════════════════════════════════════════════════════════════════
    // ENUM-BASIERTE NAVIGATION
    // ═══════════════════════════════════════════════════════════════════

    public void NavigateTo(ActivePage page)
    {
        if (_host == null) return;
        switch (page)
        {
            case ActivePage.Dashboard: SelectDashboardTab(); break;
            case ActivePage.Buildings: SelectBuildingsTab(); break;
            case ActivePage.Statistics: SelectStatisticsTab(); break;
            case ActivePage.Achievements: SelectAchievementsTab(); break;
            case ActivePage.Shop: SelectShopTab(); break;
            case ActivePage.Settings: SelectSettingsTab(); break;
            case ActivePage.WorkerMarket: SelectWorkerMarketTab(); break;
            case ActivePage.Missionen: SelectMissionenTab(); break;
            case ActivePage.Guild: SelectGuildTab(); break;
            case ActivePage.Research: SelectResearchTab(); break;
            default:
                _host.ActivePage = page;
                break;
        }
    }

    public void NavigateBack() => _host?.NavigateBackStack();

    // ═══════════════════════════════════════════════════════════════════
    // ROUTE-PARSER (Kernstueck Schritt 4)
    // ═══════════════════════════════════════════════════════════════════

    public void NavigateToRoute(string route)
    {
        if (_host == null) return;

        // Relative Route: "../minigame/..." → strip "../" und als MiniGame-Navigation behandeln
        if (route.StartsWith("../") && route.Length > 3 && route[3] != '.')
        {
            NavigateToRoute(route[3..]);
            return;
        }

        // Zurueck-Navigation: ".." oder "../.."
        if (route is ".." or "../..")
        {
            HandleBackRoute();
            return;
        }

        // "//main" = Reset zur Hauptseite (z.B. von Einstellungen)
        if (route == "//main")
        {
            SelectDashboardTab();
            _host.RefreshFromState();
            return;
        }

        // Direkt-Routen zu Haupt-Tabs
        switch (route)
        {
            case "dashboard": SelectDashboardTab(); return;
            case "imperium": SelectBuildingsTab(); return;
            case "statistics": SelectStatisticsTab(); return;
            case "research": SelectResearchTab(); return;
            case "workers": SelectWorkerMarketTab(); return;
            case "prestige": _host.ShowPrestigeConfirmationAsyncFireAndForget(); return;
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

            _miniGameNavigator.NavigateToMiniGame(routePart, orderId);
            return;
        }

        // Turnier-Teilnahme: MiniGame im Turnier-Kontext starten
        if (route == "tournament_enter")
        {
            var tournament = _gameStateService.State.CurrentTournament;
            if (tournament != null && !tournament.IsExpired)
            {
                _host.IsTournamentRound = true;
                var miniGameRoute = tournament.GameType.GetRoute();
                _miniGameNavigator.NavigateToMiniGame(miniGameRoute, "");
            }
            return;
        }

        // Feature-Views (Imperium/Missionen/Gilden Sub-Seiten)
        if (route is "manager" or "tournament" or "seasonal_event" or "battle_pass" or "guild" or "crafting")
        {
            if (route == "guild" && _host.IsTabLocked(3)) { SelectDashboardTab(); return; }
            if (route is "manager" or "crafting" && _host.IsTabLocked(1)) { SelectDashboardTab(); return; }

            switch (route)
            {
                case "manager": _host.ActivePage = ActivePage.Manager; _host.ManagerViewModel.RefreshManagers(); break;
                case "tournament": _host.ActivePage = ActivePage.Tournament; _host.TournamentViewModel.RefreshTournament(); break;
                case "seasonal_event": _host.ActivePage = ActivePage.SeasonalEvent; _host.SeasonalEventViewModel.RefreshEvent(); break;
                case "battle_pass":
                    _host.ActivePage = ActivePage.BattlePass;
                    _host.BattlePassViewModel.RefreshBattlePass();
                    _contextualHintService.TryShowHint(ContextualHints.BattlePass);
                    break;
                case "guild":
                    _host.ActivePage = ActivePage.Guild;
                    _host.GuildViewModel.RefreshGuild();
                    break;
                case "crafting":
                    _host.ActivePage = ActivePage.Crafting;
                    _host.CraftingViewModel.RefreshCrafting();
                    _contextualHintService.TryShowHint(ContextualHints.CraftingHint);
                    break;
            }
            return;
        }

        // Gilden-Sub-Seiten
        if (route is "guild_research" or "guild_members" or "guild_invite" or
            "guild_war_season" or "guild_boss" or "guild_hall" or "guild_achievements" or
            "guild_chat" or "guild_war")
        {
            if (_host.IsTabLocked(3)) { SelectDashboardTab(); return; }

            switch (route)
            {
                case "guild_research": _host.ActivePage = ActivePage.GuildResearch; break;
                case "guild_members": _host.ActivePage = ActivePage.GuildMembers; break;
                case "guild_invite": _host.ActivePage = ActivePage.GuildInvite; break;
                case "guild_war_season":
                    _host.ActivePage = ActivePage.GuildWarSeason;
                    _host.GuildViewModel.WarSeasonViewModel.RefreshWar();
                    break;
                case "guild_boss":
                    _host.ActivePage = ActivePage.GuildBoss;
                    _host.GuildViewModel.BossViewModel.RefreshBoss();
                    break;
                case "guild_hall":
                    _host.ActivePage = ActivePage.GuildHall;
                    _host.GuildViewModel.HallViewModel.RefreshHall();
                    break;
                case "guild_achievements":
                    _host.ActivePage = ActivePage.GuildAchievements;
                    break;
                case "guild_chat":
                    _host.ActivePage = ActivePage.GuildChat;
                    _host.GuildViewModel.LoadChatMessagesAsync().SafeFireAndForget();
                    _host.GuildViewModel.StartChatPolling();
                    break;
                case "guild_war":
                    _host.ActivePage = ActivePage.GuildWar;
                    _host.GuildViewModel.LoadWarStatusAsync().SafeFireAndForget();
                    break;
            }
            return;
        }

        // Ascension-Ansicht
        if (route == "ascension")
        {
            _host.ActivePage = ActivePage.Ascension;
            _host.AscensionViewModel.LoadData();
            return;
        }

        // Worker-Profile als Overlay (ActivePage bleibt erhalten)
        if (route.StartsWith("worker?id="))
        {
            var workerId = route.Replace("worker?id=", "");
            _host.WorkerProfileViewModel.SetWorker(workerId);
            _host.IsWorkerProfileActive = true;
            _host.HideBanner();
            return;
        }

        // Workshop-Detail
        if (route.StartsWith("workshop?type="))
        {
            var typeStr = route.Replace("workshop?type=", "");
            if (int.TryParse(typeStr, out var typeInt))
            {
                _host.WorkshopViewModel.SetWorkshopType(typeInt);
                _host.ActivePage = ActivePage.WorkshopDetail;
            }
        }
    }

    /// <summary>
    /// Behandelt ".." Back-Navigation mit allen Sonderfaellen:
    /// Worker-Profile-Overlay, Turnier-Rueckkehr, QuickJob-Belohnung.
    /// </summary>
    private void HandleBackRoute()
    {
        if (_host == null) return;

        // Worker-Profile Overlay: nur schliessen, darunterliegende View beibehalten
        if (_host.IsWorkerProfileActive)
        {
            _host.IsWorkerProfileActive = false;
            return;
        }

        // Turnier-Rueckkehr: Flag zuruecksetzen, Turnier-View aktualisieren
        if (_host.IsTournamentRound)
        {
            _host.IsTournamentRound = false;
            _host.TournamentViewModel.RefreshTournament();
            _host.ActivePage = ActivePage.Tournament;
            return;
        }

        // QuickJob-Rueckkehr: Belohnung nur vergeben wenn MiniGame tatsaechlich gespielt wurde
        var activeQuickJob = _host.ActiveQuickJob;
        if (activeQuickJob != null)
        {
            if (_host.QuickJobMiniGamePlayed)
            {
                activeQuickJob.IsCompleted = true;
                decimal reward = activeQuickJob.IsScoreDoubled ? activeQuickJob.Reward * 2 : activeQuickJob.Reward;
                int xp = activeQuickJob.IsScoreDoubled ? activeQuickJob.XpReward * 2 : activeQuickJob.XpReward;
                _gameStateService.AddMoney(reward);
                _gameStateService.AddXp(xp);
                _gameStateService.State.TotalQuickJobsCompleted++;
                _quickJobService?.NotifyJobCompleted(activeQuickJob);
                _dailyChallengeService.OnQuickJobCompleted();
            }
            _host.ActiveQuickJob = null;
            _host.QuickJobMiniGamePlayed = false;
            _gameStateService.State.ActiveQuickJob = null;
            _host.MissionsVM.RefreshQuickJobs();
        }

        _host.NavigateBackStack();
    }
}
