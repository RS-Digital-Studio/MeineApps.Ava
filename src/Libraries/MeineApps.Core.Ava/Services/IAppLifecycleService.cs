namespace MeineApps.Core.Ava.Services;

/// <summary>
/// Zentraler App-Lifecycle-Broker: meldet, ob die App im Vordergrund sichtbar ist, und feuert
/// <see cref="Resumed"/>/<see cref="Paused"/> bei jedem Wechsel.
///
/// <para><b>Gespeist von der Plattform</b> — auf Android aus <c>MainActivity.OnResume</c>/
/// <c>OnPause</c>. Desktop bleibt bewusst dauerhaft im Vordergrund (dort kein Akku-Thema; ein an
/// <c>Window.Deactivated</c> gekoppeltes Pausieren würde bei blossem Fokusverlust falsch auslösen
/// und sichtbares Rendering stoppen).</para>
///
/// <para><b>Konsumenten</b> (ViewModels/Services mit teuren Hintergrund-Ressourcen: Sensoren, MQTT,
/// SignalR, REST-Polling, DB-Timer — sowie App-MainView-Render-Loops) abonnieren <see cref="Paused"/>,
/// um diese im Hintergrund zu stoppen, und <see cref="Resumed"/>, um sie wieder aufzunehmen.
/// Hintergrund: Avalonia detacht Views beim App-Backgrounding <b>nicht</b> (kein
/// <c>OnDetachedFromVisualTree</c>), daher greifen die üblichen View-Cleanups dort nicht — ohne
/// diesen Broker laufen Timer/Loops/Verbindungen endlos weiter und halten das Gerät aus dem Doze.</para>
/// </summary>
public interface IAppLifecycleService
{
    /// <summary>True, solange die App im Vordergrund sichtbar ist. Startwert: <c>true</c>.</summary>
    bool IsForeground { get; }

    /// <summary>
    /// Die App kam in den Vordergrund. Handler laufen synchron auf dem aufrufenden (UI-)Thread.
    /// </summary>
    event Action? Resumed;

    /// <summary>
    /// Die App ging in den Hintergrund. Handler laufen synchron auf dem aufrufenden (UI-)Thread.
    /// </summary>
    event Action? Paused;

    /// <summary>
    /// Plattform-Hook: App wurde sichtbar. Idempotent (mehrfaches <c>OnResume</c> schadet nicht).
    /// MUSS auf dem UI-Thread aufgerufen werden (Android <c>OnResume</c> erfüllt das).
    /// </summary>
    void NotifyResumed();

    /// <summary>
    /// Plattform-Hook: App ging in den Hintergrund. Idempotent. MUSS auf dem UI-Thread aufgerufen
    /// werden — Handler dürfen synchron Timer stoppen und (kurz) persistieren, bevor das OS den
    /// Prozess einfriert.
    /// </summary>
    void NotifyPaused();
}

/// <summary>
/// Standard-Implementierung von <see cref="IAppLifecycleService"/>. Als Singleton registrieren.
/// Hält das Foreground-Flag und feuert die Events flankengetriggert (nur bei echtem Wechsel).
/// </summary>
public sealed class AppLifecycleService : IAppLifecycleService
{
    /// <inheritdoc/>
    public bool IsForeground { get; private set; } = true;

    /// <inheritdoc/>
    public event Action? Resumed;

    /// <inheritdoc/>
    public event Action? Paused;

    /// <inheritdoc/>
    public void NotifyResumed()
    {
        if (IsForeground) return; // flankengetriggert — kein Doppel-Feuern bei wiederholtem OnResume
        IsForeground = true;
        Invoke(Resumed);
    }

    /// <inheritdoc/>
    public void NotifyPaused()
    {
        if (!IsForeground) return;
        IsForeground = false;
        Invoke(Paused);
    }

    /// <summary>
    /// Feuert ein Event und isoliert jeden Abonnenten: ein werfender Handler darf die übrigen
    /// nicht blockieren — besonders im <see cref="NotifyPaused"/>-Pfad, wo nachgelagerte Handler
    /// noch Timer stoppen oder speichern müssen, bevor das OS den Prozess killt.
    /// </summary>
    private static void Invoke(Action? evt)
    {
        if (evt is null) return;
        foreach (var d in evt.GetInvocationList())
        {
            try
            {
                ((Action)d)();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppLifecycle] Handler-Fehler: {ex}");
            }
        }
    }
}
