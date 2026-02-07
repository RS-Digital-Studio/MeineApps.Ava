using HandwerkerImperium.Models;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Manages random and seasonal game events.
/// </summary>
public interface IEventService
{
    GameEvent? ActiveEvent { get; }

    /// <summary>
    /// Checks and potentially triggers a new random event (1-2 per day).
    /// </summary>
    void CheckForNewEvent();

    /// <summary>
    /// Gets the current combined event effects (random + seasonal).
    /// </summary>
    GameEventEffect GetCurrentEffects();

    event EventHandler<GameEvent>? EventStarted;
    event EventHandler<GameEvent>? EventEnded;
}
