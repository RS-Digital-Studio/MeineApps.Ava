using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Events;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Manages the game loop timer that handles idle earnings.
/// Ticks once per second while the game is active.
/// v2.1.1 (Audit M-M05): IDisposable im Interface — die DI-Container-Kaskade
/// (App.DisposeServices) kann jetzt korrekt typisiert disposen.
/// </summary>
public interface IGameLoopService : IDisposable
{
    /// <summary>
    /// Whether the game loop is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Current session duration.
    /// </summary>
    TimeSpan SessionDuration { get; }

    /// <summary>
    /// Fired on each game tick (once per second).
    /// </summary>
    event EventHandler<GameTickEventArgs>? OnTick;

    /// <summary>
    /// Starts the game loop.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the game loop.
    /// </summary>
    void Stop();

    /// <summary>
    /// Pausiert den Game-Loop (z.B. wenn die App in den Hintergrund geht) und speichert
    /// synchron. H-H05: gibt einen Task zurueck, damit Android OnPause den Save abwarten
    /// kann, bevor das OS die App eventuell killt — sonst gehen Offline-Earnings verloren.
    /// </summary>
    Task PauseAsync();

    /// <summary>
    /// Resumes the game loop.
    /// </summary>
    void Resume();

    /// <summary>
    /// Event für neue Meisterwerkzeug-Freischaltungen.
    /// </summary>
    event EventHandler<MasterToolDefinition>? MasterToolUnlocked;

    /// <summary>
    /// Event für neue Lieferungen.
    /// </summary>
    event EventHandler<SupplierDelivery>? DeliveryArrived;

    /// <summary>
    /// Event wenn ein aktiver Auftrag wegen abgelaufener Deadline verfällt.
    /// </summary>
    event EventHandler? OrderExpired;

    /// <summary>
    /// Event wenn Automation eine Lieferung automatisch eingesammelt hat.
    /// </summary>
    event EventHandler<SupplierDelivery>? AutoCollectedDelivery;

    /// <summary>
    /// Event wenn Automation einen Auftrag automatisch angenommen hat.
    /// </summary>
    event EventHandler<Order>? AutoAcceptedOrder;

    /// <summary>
    /// Workshop-Cache invalidieren (z.B. nach Workshop-Kauf).
    /// </summary>
    void InvalidateWorkshopCache();

    /// <summary>
    /// Prestige-Effekt-Cache invalidieren (nach Prestige-Shop-Kauf oder Prestige-Reset).
    /// </summary>
    void InvalidatePrestigeEffects();
}
