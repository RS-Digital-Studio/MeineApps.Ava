using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Verdrahtet das FTUE-Step-System mit den realen Game-Events (Audit F-03).
/// Vor diesem Service: <see cref="IFtueService.OnPlayerAction"/> wurde nirgendwo
/// aufgerufen, die FTUE schritt nie voran. Singleton, IDisposable.
///
/// Lifecycle:
/// - Konstruktor: subscribed alle relevanten Events (idempotent — Events feuern erst bei Aktionen).
/// - <see cref="StartIfNeeded"/>: ruft <see cref="IFtueService.Start"/> auf (idempotent, no-op wenn
///   FTUE bereits abgeschlossen/uebersprungen).
/// - Dispose: unsubscribed alle Events (Pflicht fuer Services mit Event-Abos).
/// </summary>
public sealed class FtueProgressTracker : IDisposable
{
    private readonly IFtueService _ftueService;
    private readonly IGameStateService _gameStateService;

    public FtueProgressTracker(IFtueService ftueService, IGameStateService gameStateService)
    {
        _ftueService = ftueService;
        _gameStateService = gameStateService;

        _gameStateService.WorkshopUpgraded += OnWorkshopUpgraded;
        _gameStateService.WorkerHired += OnWorkerHired;
        _gameStateService.OrderStarted += OnOrderStarted;
        _gameStateService.OrderCompleted += OnOrderCompleted;
        _gameStateService.LevelUp += OnLevelUp;
    }

    /// <summary>
    /// Startet die FTUE-Sequenz wenn sie noch nicht laeuft + nicht abgeschlossen/uebersprungen ist.
    /// Wird vom <see cref="IGameStartupCoordinator"/> nach Spielstand-Laden aufgerufen.
    /// </summary>
    public void StartIfNeeded()
    {
        var ftue = _gameStateService.State.Tutorial.Ftue;
        if (ftue.IsCompleted || ftue.WasSkipped) return;
        // FtueService.Start() ist selbst idempotent (Re-Start nach App-Restart mitten in der FTUE).
        _ftueService.Start();
    }

    private void OnWorkshopUpgraded(object? sender, WorkshopUpgradedEventArgs e)
        => _ftueService.OnPlayerAction(FtueExpectedAction.BuyFirstUpgrade);

    private void OnWorkerHired(object? sender, WorkerHiredEventArgs e)
        => _ftueService.OnPlayerAction(FtueExpectedAction.HireFirstWorker);

    private void OnOrderStarted(object? sender, OrderStartedEventArgs e)
        => _ftueService.OnPlayerAction(FtueExpectedAction.AcceptFirstOrder);

    private void OnOrderCompleted(object? sender, OrderCompletedEventArgs e)
    {
        // Nur Auftraege mit MiniGames triggern den CompleteFirstMiniGame-Step.
        // MaterialOrders (Tasks.Count == 0) sind Lieferauftraege ohne MiniGame.
        if (e.Order.Tasks.Count > 0)
            _ftueService.OnPlayerAction(FtueExpectedAction.CompleteFirstMiniGame);
    }

    private void OnLevelUp(object? sender, LevelUpEventArgs e)
    {
        if (e.NewLevel >= 2)
            _ftueService.OnPlayerAction(FtueExpectedAction.ReachLevel2);
    }

    public void Dispose()
    {
        _gameStateService.WorkshopUpgraded -= OnWorkshopUpgraded;
        _gameStateService.WorkerHired -= OnWorkerHired;
        _gameStateService.OrderStarted -= OnOrderStarted;
        _gameStateService.OrderCompleted -= OnOrderCompleted;
        _gameStateService.LevelUp -= OnLevelUp;
    }
}
