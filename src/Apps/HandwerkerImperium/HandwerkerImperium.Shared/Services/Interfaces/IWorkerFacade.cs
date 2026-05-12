namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Bounded-Context "Worker": Bündelt alle Services rund um Arbeiter (Lifecycle,
/// Auktionen). AAA-Audit P1 Service-Sprawl-Reduction (12.05.2026).
///
/// Additive Einführung — bestehende Konsumenten von <see cref="IWorkerService"/> und
/// <see cref="IWorkerAuctionService"/> funktionieren unverändert. Neue Konsumenten können
/// optional die Facade injizieren statt zwei Einzel-Dependencies.
/// </summary>
public interface IWorkerFacade
{
    /// <summary>Kern-Worker-Lifecycle: Hire, Fire, Mood, Fatigue, Training, Aura.</summary>
    IWorkerService Worker { get; }

    /// <summary>Worker-Auktionen (Bid-Logik, NPC-Bots, HMAC-Signing).</summary>
    IWorkerAuctionService Auction { get; }
}
