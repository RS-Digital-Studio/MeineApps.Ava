namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Per-Tick-UI-Orchestrierung (1 Hz vom GameLoopService getrieben), aus
/// MainViewModel.GameTick.cs extrahiert. Subscribed selbst auf IGameLoopService.OnTick
/// und verteilt die Updates an die Feature-VMs — tab-spezifisch gated über den
/// IGameTickHost. Singleton im DI.
/// </summary>
public interface IGameTickCoordinator
{
    /// <summary>Verbindet die Host-Bruecke (MainViewModel) — einmalig im MainViewModel-Ctor.</summary>
    void AttachHost(IGameTickHost host);

    /// <summary>Aktiviert die OnTick-Subscription. Idempotent — mehrfacher Aufruf ist sicher.</summary>
    void StartListening();
}
