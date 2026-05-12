using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Implementierung der <see cref="IWorkerFacade"/> — reiner Service-Container (Pass-Through).
/// Pattern analog zu <see cref="GuildFacade"/>. Singleton, keine eigene Logik, kein State.
/// </summary>
public sealed class WorkerFacade : IWorkerFacade
{
    public IWorkerService Worker { get; }
    public IWorkerAuctionService Auction { get; }

    public WorkerFacade(IWorkerService worker, IWorkerAuctionService auction)
    {
        Worker = worker;
        Auction = auction;
    }
}
