using BomberBlast.Core;
using BomberBlast.Services;
using BomberBlast.ViewModels;
using BomberBlast.ViewModels.Navigation;
using MeineApps.Core.Ava.Localization;
using Microsoft.Extensions.Logging;

namespace BomberBlast.Navigation;

/// <summary>
/// Default-Implementation von <see cref="INavigationCoordinator"/>.
///
/// <para>
/// Haelt <see cref="ActiveView"/> als Source-of-Truth und die komplette Routing-Logik
/// (NavigateToRouteAsync mit ~26 Routen-Cases, NavigateTo(NavigationRequest), HideAll).
/// MainViewModel ist nur noch Forwarder + Subscriber auf <see cref="ActiveViewChanged"/>.
/// </para>
///
/// <para>
/// CloudSave-Init-Race-Guard: Routen wie Game/LevelSelect/Dungeon awaiten (mit 3s-Cap) den
/// Cloud-Pull-Task bevor sie freigegeben werden — verhindert dass der lokale Leer-State eines
/// frischen Geraets ueber den noch nicht geladenen Cloud-State geschoben wird.
/// </para>
/// </summary>
public sealed class NavigationCoordinator : INavigationCoordinator
{
    private readonly IChildViewModelRegistry _registry;
    private readonly IBottomTabController _tabController;
    private readonly IGameEventBus _eventBus;
    private readonly ICoinService _coinService;
    private readonly SoundManager _soundManager;
    private readonly ILocalizationService _localization;
    private readonly ILogger<NavigationCoordinator> _logger;
    private readonly Func<Task?> _cloudSaveInitTaskProvider;

    private ActiveView _activeView = ActiveView.MainMenu;

    /// <summary>Merkt ob Einstellungen aus dem Spiel geoeffnet wurden (fuer Zurueck-Navigation).</summary>
    private bool _returnToGameFromSettings;

    /// <summary>Zaehlt Fehlversuche pro Level (fuer Level-Skip nach 3x Game Over).</summary>
    private readonly Dictionary<int, int> _levelFailCounts = new();

    public ActiveView ActiveView => _activeView;

    public event Action<ActiveView>? ActiveViewChanged;

    public NavigationCoordinator(
        IChildViewModelRegistry registry,
        IBottomTabController tabController,
        IGameEventBus eventBus,
        ICoinService coinService,
        SoundManager soundManager,
        ILocalizationService localization,
        ILogger<NavigationCoordinator> logger,
        Func<Task?> cloudSaveInitTaskProvider)
    {
        _registry = registry;
        _tabController = tabController;
        _eventBus = eventBus;
        _coinService = coinService;
        _soundManager = soundManager;
        _localization = localization;
        _logger = logger;
        _cloudSaveInitTaskProvider = cloudSaveInitTaskProvider;
    }

    public async void NavigateTo(NavigationRequest request)
    {
        try
        {
            await NavigateToRouteAsync(NavigationRouteMapper.ToRoute(request));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NavigateTo(NavigationRequest) unbehandelte Exception fuer {RequestType}", request?.GetType().Name);
        }
    }

    public async Task NavigateToRouteAsync(string route)
    {
        try
        {
            var (baseRoute, query) = NavigationRouteParser.Parse(route);

            // Cloud-Save-Init MUSS abgeschlossen sein bevor wir in Game/LevelSelect navigieren —
            // sonst kann ein "Continue"-Tap auf frischem Geraet den leeren lokalen State
            // mit Cloud-Progress racen und die Cloud ueberschreiben.
            if (NavigationRouteParser.RequiresCloudSaveInit(baseRoute))
            {
                if (_cloudSaveInitTaskProvider() is { IsCompleted: false } task)
                {
                    try
                    {
                        // 3s-Cap: Bei Netzproblemen kein endloses Blocken — lokaler State wird genutzt.
                        var completed = await Task.WhenAny(task, Task.Delay(3000));
                        if (completed != task)
                            _logger.LogWarning("CloudSave-Init dauert >3s - navigiere ohne Cloud-Sync");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "CloudSave-Init Fehler vor Navigation");
                    }
                }
            }

            var wasGameActive = _activeView == ActiveView.Game;

            // Lifecycle: Game-Loop stoppen beim Verlassen der Game-View.
            if (wasGameActive && baseRoute != "Game")
                _registry.GameVm?.OnDisappearing();

            HideAll();

            // Navigations-Sound (nicht beim Game-Start, das hat eigene Sounds).
            if (baseRoute != "Game")
                _soundManager.PlaySound(SoundManager.SFX_MENU_SELECT);

            switch (baseRoute)
            {
                case "MainMenu":
                    _returnToGameFromSettings = false;
                    SetActiveView(ActiveView.MainMenu);
                    _registry.MenuVm.OnAppearing();
                    break;

                case "Game":
                    // EnsureGame zuerst (loest Lazy<GameViewModel> auf), erst DANACH ActiveView setzen —
                    // sonst ein Frame mit IsGameActive=true + GameVm=null (leerer GameBorder).
                    var gameVm = _registry.EnsureGame();
                    SetActiveView(ActiveView.Game);
                    if (query is not null)
                    {
                        var p = NavigationQueryParser.ParseGame(query);
                        gameVm.SetParameters(p.Mode, p.Level, p.Continue, p.Boost, p.Difficulty, p.Floor, p.Seed, p.Master);
                    }
                    await gameVm.OnAppearingAsync();
                    break;

                case "LevelSelect":
                    SetActiveView(ActiveView.LevelSelect);
                    _registry.LevelSelectVm.OnAppearing();
                    break;

                case "HighScores":
                    SetActiveView(ActiveView.HighScores);
                    _registry.HighScoresVm.OnAppearing();
                    break;

                case "GameOver":
                    SetActiveView(ActiveView.GameOver);
                    if (query is not null)
                        ApplyGameOverParameters(query);
                    break;

                case "Shop":
                    SetActiveView(ActiveView.Shop);
                    _tabController.IsShopSpinTab = false;
                    _registry.EnsureShop().OnAppearing();
                    break;

                case "LuckySpin":
                    SetActiveView(ActiveView.Shop);
                    _tabController.IsShopSpinTab = true;
                    _registry.EnsureLuckySpin().OnAppearing();
                    break;

                case "Profile":
                    SetActiveView(ActiveView.Profile);
                    _registry.EnsureProfile().OnAppearing();
                    break;

                case "Achievements":
                    SetActiveView(ActiveView.Profile);
                    {
                        var p = _registry.EnsureProfile();
                        p.OnAppearing();
                        p.SelectTabCommand.Execute("Achievements");
                    }
                    break;

                case "Settings":
                    _returnToGameFromSettings = wasGameActive;
                    SetActiveView(ActiveView.Settings);
                    _tabController.IsSettingsHelpTab = false;
                    _registry.SettingsVm.OnAppearing();
                    break;

                case "Help":
                    SetActiveView(ActiveView.Settings);
                    _tabController.IsSettingsHelpTab = true;
                    break;

                case "Cards":
                case "Deck":
                    SetActiveView(ActiveView.Cards);
                    _registry.EnsureDeck().OnAppearing();
                    break;

                case "Collection":
                    SetActiveView(ActiveView.Profile);
                    {
                        var p = _registry.EnsureProfile();
                        p.OnAppearing();
                        p.SelectTabCommand.Execute("Collection");
                    }
                    break;

                case "Challenges":
                case "DailyChallenge":
                    SetActiveView(ActiveView.Challenges);
                    _tabController.IsChallengesMissionsTab = false;
                    _registry.EnsureDailyChallenge().OnAppearing();
                    break;

                case "WeeklyChallenge":
                    SetActiveView(ActiveView.Challenges);
                    _tabController.IsChallengesMissionsTab = true;
                    _registry.EnsureWeeklyChallenge().OnAppearing();
                    break;

                case "Statistics":
                    SetActiveView(ActiveView.Profile);
                    {
                        var p = _registry.EnsureProfile();
                        p.OnAppearing();
                        p.SelectTabCommand.Execute("Statistics");
                    }
                    break;

                case "QuickPlay":
                    SetActiveView(ActiveView.QuickPlay);
                    _registry.EnsureQuickPlay().OnAppearing();
                    break;

                case "PlayHub":
                    SetActiveView(ActiveView.PlayHub);
                    _registry.PlayHubVm.OnAppearing();
                    break;

                case "Dungeon":
                    SetActiveView(ActiveView.Dungeon);
                    _registry.EnsureDungeon().OnAppearing();
                    break;

                case "BattlePass":
                    SetActiveView(ActiveView.BattlePass);
                    _registry.EnsureBattlePass().OnAppearing();
                    break;

                case "League":
                    SetActiveView(ActiveView.League);
                    _registry.EnsureLeague().OnAppearing();
                    break;

                case "GemShop":
                    SetActiveView(ActiveView.GemShop);
                    _registry.EnsureGemShop().OnAppearing();
                    break;

                case "BossRush":
                    SetActiveView(ActiveView.BossRush);
                    _registry.BossRushVm.OnAppearing();
                    break;

                case "DailyRace":
                    // Daily Race nutzt den Daily-Challenge-Tab in der Liga-View — Routing via League.
                    SetActiveView(ActiveView.League);
                    _registry.EnsureLeague().OnAppearing();
                    break;

                case "Victory":
                    SetActiveView(ActiveView.Victory);
                    _registry.VictoryVm.OnAppearing();
                    if (query is not null)
                    {
                        var p = NavigationQueryParser.ParseVictory(query);
                        _registry.VictoryVm.SetScore(p.Score);
                        if (p.Coins > 0) _coinService.AddCoins(p.Coins);
                    }
                    _eventBus.RaiseCelebration();
                    _eventBus.RaiseFloatingText(
                        _localization.GetString("VictoryTitle") ?? "Victory!", "gold");
                    break;

                case "..":
                    // Zurueck-Navigation: zum Spiel zurueckkehren wenn Einstellungen aus dem Spiel geoeffnet wurden.
                    if (_returnToGameFromSettings)
                    {
                        _returnToGameFromSettings = false;
                        SetActiveView(ActiveView.Game);
                        await _registry.EnsureGame().OnAppearingAsync();
                    }
                    else
                    {
                        SetActiveView(ActiveView.MainMenu);
                        _registry.MenuVm.OnAppearing();
                    }
                    break;

                default:
                    SetActiveView(ActiveView.MainMenu);
                    _registry.MenuVm.OnAppearing();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NavigateTo Fehler bei Route '{Route}'", route);
            // Fallback: Zurueck zum Hauptmenue damit die App nicht haengt.
            try
            {
                HideAll();
                SetActiveView(ActiveView.MainMenu);
                _registry.MenuVm.OnAppearing();
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "NavigateTo Fallback fehlgeschlagen fuer Route '{Route}'", route);
            }
        }
    }

    public void HideAll()
    {
        SetActiveView(ActiveView.None);
        _tabController.ResetTabStates();
    }

    /// <summary>
    /// GameOver-Routen-Parameter anwenden: Query-Parsing, Fail-Counter-Tracking,
    /// VM-Parameter-Setting, Quick/Daily-Score-Weiterreichen, Level-Complete-Celebration.
    /// </summary>
    private void ApplyGameOverParameters(string query)
    {
        var p = NavigationQueryParser.ParseGameOver(query);

        // Fehlversuche pro Level tracken (fuer Level-Skip).
        var fails = 0;
        if (!p.LevelComplete && p.Mode == "story" && p.Level > 0)
        {
            fails = _levelFailCounts.GetValueOrDefault(p.Level) + 1;
            _levelFailCounts[p.Level] = fails;
        }
        else if (p.LevelComplete && p.Level > 0)
        {
            _levelFailCounts.Remove(p.Level);
        }

        _registry.GameOverVm.SetParameters(p.Score, p.Level, p.IsHighScore, p.Mode, p.Coins,
            p.LevelComplete, p.CanContinue, fails,
            p.EnemyPoints, p.TimeBonus, p.EfficiencyBonus, p.Multiplier, p.Kills, p.SurvivalTime);

        // Quick-Play Score an QuickPlayVM fuer Challenge-Sharing weiterreichen.
        if (p.Mode == "quick" && p.Score > 0)
            _registry.EnsureQuickPlay().SetLastScore(p.Score);

        // Daily Challenge: Score melden + Streak-Bonus.
        if (p.Mode == "daily" && p.Score > 0)
            _registry.EnsureDailyChallenge().SubmitScore(p.Score);

        // Level Complete → Confetti + Floating Text.
        if (p.LevelComplete)
        {
            _eventBus.RaiseCelebration();
            _eventBus.RaiseFloatingText(
                _localization.GetString("LevelComplete") ?? "Level Complete!", "success");
        }
    }

    private void SetActiveView(ActiveView view)
    {
        if (_activeView == view) return;
        _activeView = view;
        ActiveViewChanged?.Invoke(view);
    }
}
