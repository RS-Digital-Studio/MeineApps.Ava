using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;



/// <summary>
/// Manages worker hiring, firing, training, resting, and market rotation.
/// </summary>
public interface IWorkerService
{
    /// <summary>
    /// Hires a worker from the market and assigns to a workshop.
    /// </summary>
    bool HireWorker(Worker worker, WorkshopType workshop);

    /// <summary>
    /// Fires a worker (removes from workshop).
    /// </summary>
    bool FireWorker(string workerId);

    /// <summary>
    /// Stellt einen gefeuerten Worker wieder ein (Undo-Funktion).
    /// Worker wird zurück in seinen vorherigen Workshop eingefügt.
    /// </summary>
    bool ReinstateWorker(Worker worker, WorkshopType workshop);

    /// <summary>
    /// Transfers a worker to a different workshop.
    /// </summary>
    bool TransferWorker(string workerId, WorkshopType targetWorkshop);

    /// <summary>
    /// Starts worker training with the specified type.
    /// </summary>
    bool StartTraining(string workerId, TrainingType trainingType = TrainingType.Efficiency);

    /// <summary>
    /// Stops worker training.
    /// </summary>
    void StopTraining(string workerId);

    /// <summary>
    /// Sends a worker to rest (recovers fatigue, does not earn).
    /// </summary>
    bool StartResting(string workerId);

    /// <summary>
    /// Wakes a worker from rest.
    /// </summary>
    void StopResting(string workerId);

    /// <summary>
    /// Gives a worker a bonus (costs 1 day's wage, +30% mood).
    /// </summary>
    bool GiveBonus(string workerId);

    /// <summary>
    /// Updates all worker states (mood decay, fatigue, XP) per game tick.
    /// </summary>
    void UpdateWorkerStates(double deltaSeconds);

    /// <summary>
    /// Gets the current worker market pool.
    /// </summary>
    WorkerMarketPool GetWorkerMarket();

    /// <summary>
    /// Forces a market refresh (e.g., via rewarded video).
    /// </summary>
    WorkerMarketPool RefreshMarket();

    /// <summary>
    /// Gets a specific worker by ID.
    /// </summary>
    Worker? GetWorker(string id);

    /// <summary>
    /// Fired when a worker's mood drops to a warning threshold.
    /// </summary>
    event EventHandler<Worker>? WorkerMoodWarning;

    /// <summary>
    /// Fired when a worker quits due to low mood.
    /// </summary>
    event EventHandler<Worker>? WorkerQuit;

    /// <summary>
    /// Fired when a worker levels up.
    /// </summary>
    event EventHandler<Worker>? WorkerLevelUp;
}
