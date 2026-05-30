namespace BomberBlast.ViewModels;

/// <summary>
/// Bundles app-weite Lifecycle-Hooks: Android-BackPress (Double-Back-to-Exit),
/// CloudSave-Init-Race-Guard und Rewarded-Ad-Unavailable-Handling.
///
/// <para>
/// Konkret:
/// </para>
/// <list type="bullet">
/// <item><c>HandleBackPressed</c> ist die Source-of-Truth fuer den Back-Button. Behandelt
///       offene Dialoge, Game-Pause-Toggle, Sub-View-Back, Double-Back-Exit-Hint.</item>
/// <item><c>CloudSaveInitTask</c> ist der parallel im Ctor gestartete First-Pull der Cloud —
///       <see cref="BomberBlast.Navigation.INavigationCoordinator"/> awaitet ihn (3s-Cap)
///       bevor Routen wie Game/LevelSelect/Dungeon freigegeben werden.</item>
/// <item><c>OnAdUnavailable</c> wird vom <c>IRewardedAdService.AdUnavailable</c>-Event geroutet
///       und zeigt einen erklaerenden Alert via <see cref="BomberBlast.Services.IDialogPresenter"/>.</item>
/// </list>
/// </summary>
public interface ILifecycleHub
{
    /// <summary>Im Ctor gestartet (Task.Run). NavigationCoordinator awaitet ihn vor Game-Routen.</summary>
    Task CloudSaveInitTask { get; }

    /// <summary>Wird gefeuert wenn der Double-Back-Exit-Hint angezeigt werden soll ("Erneut druecken um zu beenden").</summary>
    event Action<string>? ExitHintRequested;

    /// <summary>
    /// Android-BackPress-Handler. Liefert true wenn der Back-Press konsumiert wurde
    /// (App soll NICHT beendet werden).
    /// </summary>
    bool HandleBackPressed();

    /// <summary>
    /// App geht in den Hintergrund (Android OnPause). Bricht offene modale Dialoge ab, pausiert ein
    /// laufendes Spiel und stoppt die Musik — sonst liefe Engine/Musik im Hintergrund weiter.
    /// </summary>
    void OnAppPaused();

    /// <summary>
    /// App kommt zurueck in den Vordergrund (Android OnResume). Nimmt die Musik wieder auf, sofern
    /// der Spieler nicht im Pause-Overlay steht (dort startet erst der Resume-Button die Musik).
    /// </summary>
    void OnAppResumed();
}
