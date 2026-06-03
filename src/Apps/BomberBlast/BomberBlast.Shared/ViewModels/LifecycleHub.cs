using BomberBlast.Core;
using BomberBlast.Navigation;
using BomberBlast.Resources.Strings;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Premium.Ava.Services;
using Microsoft.Extensions.Logging;

namespace BomberBlast.ViewModels;

/// <summary>
/// Default-Implementation von <see cref="ILifecycleHub"/>.
///
/// <para>
/// Buendelt app-weite Lifecycle-Hooks:
/// </para>
/// <list type="bullet">
/// <item><c>CloudSaveInitTask</c> — im Ctor gestarteter Cloud-Pull. NavigationCoordinator
///       awaitet ihn (3s-Cap) vor Game-Routen.</item>
/// <item><c>HandleBackPressed</c> — hierarchische Android-Back-Navigation: Dialoge schliessen,
///       Score-Double-Overlay ueberspringen, Game-Pause/Resume, Sub-View-Back, Double-Back-Exit.</item>
/// <item><c>OnAdUnavailable</c> — auf <c>IRewardedAdService.AdUnavailable</c> abonniert,
///       zeigt einen erklaerenden Alert.</item>
/// </list>
/// </summary>
public sealed class LifecycleHub : ILifecycleHub, IDisposable
{
    private readonly IDialogPresenter _dialogPresenter;
    private readonly IChildViewModelRegistry _registry;
    private readonly INavigationCoordinator _navigationCoordinator;
    private readonly ILocalizationService _localization;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly ISoundService _soundService;
    private readonly ILogger<LifecycleHub> _logger;
    private readonly BackPressHelper _backPressHelper = new();

    public Task CloudSaveInitTask { get; }

    public event Action<string>? ExitHintRequested;

    public LifecycleHub(
        ICloudSaveService cloudSaveService,
        IDialogPresenter dialogPresenter,
        IChildViewModelRegistry registry,
        INavigationCoordinator navigationCoordinator,
        ILocalizationService localization,
        IRewardedAdService rewardedAdService,
        ISoundService soundService,
        ILogger<LifecycleHub> logger)
    {
        _dialogPresenter = dialogPresenter;
        _registry = registry;
        _navigationCoordinator = navigationCoordinator;
        _localization = localization;
        _rewardedAdService = rewardedAdService;
        _soundService = soundService;
        _logger = logger;

        _backPressHelper.ExitHintRequested += msg => ExitHintRequested?.Invoke(msg);
        _rewardedAdService.AdUnavailable += OnAdUnavailable;

        // Cloud-Save: Bei App-Start Cloud-Stand laden (Task gespeichert, kein Fire-and-Forget).
        // Der NavigationCoordinator awaitet diesen Task vor Game-Routen.
        CloudSaveInitTask = Task.Run(async () =>
        {
            try { await cloudSaveService.TryLoadFromCloudAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "CloudSave Init fehlgeschlagen"); }
        });
    }

    /// <summary>
    /// Hierarchische Back-Navigation. Liefert true wenn der Back-Press konsumiert wurde
    /// (App soll NICHT beendet werden).
    /// </summary>
    public bool HandleBackPressed()
    {
        // 1. Offene Dialoge schliessen (hoechste Prioritaet).
        if (_dialogPresenter.IsConfirmDialogVisible)
        {
            _dialogPresenter.CancelConfirm();
            return true;
        }
        if (_dialogPresenter.IsAlertDialogVisible)
        {
            _dialogPresenter.DismissAlert();
            return true;
        }

        var gameVm = _registry.GameVm;

        // 2. Score-Double-Overlay → ueberspringen.
        if (gameVm is { ShowScoreDoubleOverlay: true })
        {
            gameVm.SkipDoubleScoreCommand.Execute(null);
            return true;
        }

        var activeView = _navigationCoordinator.ActiveView;

        // 3. Im Spiel: Pause/Resume.
        if (activeView == ActiveView.Game && gameVm is not null)
        {
            if (gameVm.IsContextHelpVisible)
                gameVm.CloseContextHelpCommand.Execute(null);
            else if (gameVm.IsPaused)
                gameVm.ResumeCommand.Execute(null);
            else if (gameVm.State == GameState.Playing)
                gameVm.PauseCommand.Execute(null);
            else
                // Andere Game-States (Starting, PlayerDied etc.) → zum Menue.
                _navigationCoordinator.NavigateTo(new GoMainMenu());
            return true;
        }

        // 4. Settings → zurueck (zum Spiel oder Menue).
        if (activeView == ActiveView.Settings)
        {
            _navigationCoordinator.NavigateTo(new GoBack());
            return true;
        }

        // 5. Alle anderen Sub-Views → zurueck zum Hauptmenue.
        if (activeView is not ActiveView.MainMenu and not ActiveView.None
            and not ActiveView.Game and not ActiveView.Settings)
        {
            _navigationCoordinator.NavigateTo(new GoMainMenu());
            return true;
        }

        // 6. Hauptmenue → Double-Back-to-Exit.
        if (activeView == ActiveView.MainMenu)
        {
            var msg = _localization.GetString("PressBackAgainToExit") ?? "Press back again to exit";
            return _backPressHelper.HandleDoubleBack(msg);
        }

        return false;
    }

    /// <summary>
    /// Benannter Handler fuer <c>IRewardedAdService.AdUnavailable</c> (statt Lambda,
    /// damit Unsubscribe moeglich bliebe). Zeigt einen erklaerenden Alert.
    /// </summary>
    private void OnAdUnavailable()
        => _dialogPresenter.ShowAlert(
            AppStrings.AdVideoNotAvailableTitle,
            AppStrings.AdVideoNotAvailableMessage,
            AppStrings.OK);

    /// <inheritdoc />
    public void OnAppPaused()
    {
        // Offene modale Dialoge abbrechen (sonst macht ein Awaiter nach Resume mit "false" weiter).
        _dialogPresenter.CancelAllDialogsOnBackground();

        // Laufendes Spiel pausieren (GameEngine.Pause stoppt Game-Loop + Spiel-Musik).
        var gameVm = _registry.GameVm;
        if (gameVm is { State: GameState.Playing, IsPaused: false })
            gameVm.PauseCommand.Execute(null);

        // Restliche Musik (z.B. Menue) explizit stoppen — sonst liefe sie im Hintergrund weiter.
        _soundService.PauseMusic();
    }

    /// <inheritdoc />
    public void OnAppResumed()
    {
        // Musik NICHT wieder aufnehmen, wenn der Spieler im Game-Pause-Overlay steht — dort startet
        // erst der Resume-Button (GameEngine.Resume) die Musik. Sonst (Menue etc.) wieder aufnehmen.
        var gameVm = _registry.GameVm;
        if (gameVm is { IsPaused: true })
            return;

        _soundService.ResumeMusic();
    }

    public void Dispose()
    {
        // Service-Event abmelden (Singleton-Lifetime, aber sauberes Unsubscribe).
        _rewardedAdService.AdUnavailable -= OnAdUnavailable;
    }
}
