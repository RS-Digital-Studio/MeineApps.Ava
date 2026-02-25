using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Events;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Manages the game loop timer that handles idle earnings.
/// Ticks once per second while the game is active.
/// </summary>
public interface IGameLoopService
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
    /// Pauses the game loop (e.g., when app is backgrounded).
    /// </summary>
    void Pause();

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
    /// Workshop-Cache invalidieren (z.B. nach Workshop-Kauf).
    /// </summary>
    void InvalidateWorkshopCache();

    /// <summary>
    /// Prestige-Effekt-Cache invalidieren (nach Prestige-Shop-Kauf oder Prestige-Reset).
    /// </summary>
    void InvalidatePrestigeEffects();
}
