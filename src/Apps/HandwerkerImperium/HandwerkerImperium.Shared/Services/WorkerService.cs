using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Manages worker lifecycle: hiring, firing, training, resting, mood, fatigue.
/// </summary>
public class WorkerService : IWorkerService
{
    private readonly IGameStateService _gameState;
    private readonly object _lock = new();

    public event EventHandler<Worker>? WorkerMoodWarning;
    public event EventHandler<Worker>? WorkerQuit;
    public event EventHandler<Worker>? WorkerLevelUp;

    public WorkerService(IGameStateService gameState)
    {
        _gameState = gameState;
    }

    public bool HireWorker(Worker worker, WorkshopType workshop)
    {
        lock (_lock)
        {
            var state = _gameState.State;
            var ws = state.GetOrCreateWorkshop(workshop);

            // Check if workshop can accept more workers
            if (ws.Workers.Count >= ws.MaxWorkers) return false;

            // Check if player can afford the hiring cost (Euro + ggf. Goldschrauben)
            var hiringCost = worker.Tier.GetHiringCost();
            var hiringScrewCost = worker.Tier.GetHiringScrewCost();
            if (!_gameState.CanAfford(hiringCost)) return false;
            if (hiringScrewCost > 0 && !_gameState.CanAffordGoldenScrews(hiringScrewCost)) return false;

            _gameState.TrySpendMoney(hiringCost);
            if (hiringScrewCost > 0)
                _gameState.TrySpendGoldenScrews(hiringScrewCost);

            worker.AssignedWorkshop = workshop;
            worker.HiredAt = DateTime.UtcNow;
            ws.Workers.Add(worker);

            // Remove from market
            state.WorkerMarket?.RemoveWorker(worker.Id);

            state.TotalWorkersHired++;
            return true;
        }
    }

    public bool FireWorker(string workerId)
    {
        lock (_lock)
        {
            var state = _gameState.State;
            foreach (var ws in state.Workshops)
            {
                var worker = ws.Workers.FirstOrDefault(w => w.Id == workerId);
                if (worker != null)
                {
                    ws.Workers.Remove(worker);
                    state.TotalWorkersFired++;
                    return true;
                }
            }
            return false;
        }
    }

    public bool TransferWorker(string workerId, WorkshopType targetWorkshop)
    {
        lock (_lock)
        {
            var state = _gameState.State;
            var targetWs = state.GetOrCreateWorkshop(targetWorkshop);

            if (targetWs.Workers.Count >= targetWs.MaxWorkers) return false;

            Worker? worker = null;
            Workshop? sourceWs = null;
            foreach (var ws in state.Workshops)
            {
                worker = ws.Workers.FirstOrDefault(w => w.Id == workerId);
                if (worker != null)
                {
                    sourceWs = ws;
                    break;
                }
            }

            if (worker == null || sourceWs == null) return false;
            if (sourceWs.Type == targetWorkshop) return false;

            sourceWs.Workers.Remove(worker);
            worker.AssignedWorkshop = targetWorkshop;

            // Small mood hit from transfer (unless Specialist personality)
            if (worker.Personality != WorkerPersonality.Specialist)
                worker.Mood = Math.Max(0m, worker.Mood - 5m);

            targetWs.Workers.Add(worker);
            return true;
        }
    }

    public bool StartTraining(string workerId)
    {
        lock (_lock)
        {
            var worker = GetWorker(workerId);
            if (worker == null || worker.IsTraining || worker.IsResting) return false;
            if (worker.ExperienceLevel >= 10) return false;

            worker.IsTraining = true;
            worker.TrainingStartedAt = DateTime.UtcNow;
            return true;
        }
    }

    public void StopTraining(string workerId)
    {
        lock (_lock)
        {
            var worker = GetWorker(workerId);
            if (worker == null || !worker.IsTraining) return;

            worker.IsTraining = false;
            worker.TrainingStartedAt = null;
        }
    }

    public bool StartResting(string workerId)
    {
        lock (_lock)
        {
            var worker = GetWorker(workerId);
            if (worker == null || worker.IsResting || worker.IsTraining) return false;

            worker.IsResting = true;
            worker.RestStartedAt = DateTime.UtcNow;
            return true;
        }
    }

    public void StopResting(string workerId)
    {
        lock (_lock)
        {
            var worker = GetWorker(workerId);
            if (worker == null || !worker.IsResting) return;

            worker.IsResting = false;
            worker.RestStartedAt = null;
        }
    }

    public bool GiveBonus(string workerId)
    {
        lock (_lock)
        {
            var worker = GetWorker(workerId);
            if (worker == null) return false;

            // Bonus costs 1 day's wage (8h)
            var bonusCost = worker.WagePerHour * 8m;
            if (!_gameState.CanAfford(bonusCost)) return false;

            _gameState.TrySpendMoney(bonusCost);
            worker.Mood = Math.Min(100m, worker.Mood + 30m);
            worker.QuitDeadline = null; // Cancel quit timer
            return true;
        }
    }

    public void UpdateWorkerStates(double deltaSeconds)
    {
        lock (_lock)
        {
            var state = _gameState.State;
            var deltaHours = (decimal)deltaSeconds / 3600m;
            var workersToRemove = new List<(Workshop ws, Worker worker)>();

            foreach (var ws in state.Workshops)
            {
                foreach (var worker in ws.Workers)
                {
                    if (worker.IsResting)
                    {
                        UpdateResting(worker, deltaHours);
                    }
                    else if (worker.IsTraining)
                    {
                        UpdateTraining(worker, deltaHours, state);
                    }
                    else
                    {
                        UpdateWorking(worker, deltaHours);
                    }

                    // Check quit conditions
                    if (worker.WillQuit)
                    {
                        if (worker.QuitDeadline == null)
                        {
                            worker.QuitDeadline = DateTime.UtcNow.AddHours(24);
                            WorkerMoodWarning?.Invoke(this, worker);
                        }
                        else if (DateTime.UtcNow >= worker.QuitDeadline)
                        {
                            workersToRemove.Add((ws, worker));
                        }
                    }
                    else
                    {
                        worker.QuitDeadline = null;
                    }
                }
            }

            // Remove workers who quit
            foreach (var (ws, worker) in workersToRemove)
            {
                ws.Workers.Remove(worker);
                state.TotalWorkersFired++;
                WorkerQuit?.Invoke(this, worker);
            }
        }
    }

    private void UpdateResting(Worker worker, decimal deltaHours)
    {
        // Reduce fatigue during rest
        decimal fatigueRecovery = (100m / worker.RestHoursNeeded) * deltaHours;
        worker.Fatigue = Math.Max(0m, worker.Fatigue - fatigueRecovery);

        // Slight mood recovery during rest
        worker.Mood = Math.Min(100m, worker.Mood + 1m * deltaHours);

        // Auto-stop resting when fully rested
        if (worker.Fatigue <= 0m)
        {
            worker.IsResting = false;
            worker.RestStartedAt = null;
        }
    }

    private void UpdateTraining(Worker worker, decimal deltaHours, GameState state)
    {
        // Training costs money per tick
        var trainingCost = worker.TrainingCostPerHour * deltaHours;
        if (!_gameState.CanAfford(trainingCost))
        {
            // Stop training if can't afford
            worker.IsTraining = false;
            worker.TrainingStartedAt = null;
            return;
        }
        _gameState.TrySpendMoney(trainingCost);

        // Gain XP
        decimal xpGain = worker.TrainingXpPerHour * deltaHours * worker.Personality.GetXpMultiplier();
        worker.ExperienceXp += (int)xpGain;

        // Level up check
        if (worker.ExperienceXp >= worker.XpForNextLevel && worker.ExperienceLevel < 10)
        {
            worker.ExperienceXp -= worker.XpForNextLevel;
            worker.ExperienceLevel++;

            // Increase base efficiency on level up
            var tierMax = worker.Tier.GetMaxEfficiency();
            var tierMin = worker.Tier.GetMinEfficiency();
            worker.Efficiency = Math.Min(tierMax, worker.Efficiency + (tierMax - tierMin) * 0.05m);

            WorkerLevelUp?.Invoke(this, worker);
        }

        // Training also increases fatigue (slower than working)
        worker.Fatigue = Math.Min(100m, worker.Fatigue + worker.FatiguePerHour * 0.5m * deltaHours);
    }

    private void UpdateWorking(Worker worker, decimal deltaHours)
    {
        // Mood decays while working
        worker.Mood = Math.Max(0m, worker.Mood - worker.MoodDecayPerHour * deltaHours);

        // Fatigue increases while working
        worker.Fatigue = Math.Min(100m, worker.Fatigue + worker.FatiguePerHour * deltaHours);

        // Auto-Rest bei 100% Erschöpfung
        if (worker.Fatigue >= 100m)
        {
            worker.IsResting = true;
            worker.RestStartedAt = DateTime.UtcNow;
        }

        // Small XP gain from working (10% of training rate)
        decimal xpGain = worker.TrainingXpPerHour * 0.1m * deltaHours * worker.Personality.GetXpMultiplier();
        worker.ExperienceXp += Math.Max(1, (int)xpGain);

        // Level up check
        if (worker.ExperienceXp >= worker.XpForNextLevel && worker.ExperienceLevel < 10)
        {
            worker.ExperienceXp -= worker.XpForNextLevel;
            worker.ExperienceLevel++;

            var tierMax = worker.Tier.GetMaxEfficiency();
            var tierMin = worker.Tier.GetMinEfficiency();
            worker.Efficiency = Math.Min(tierMax, worker.Efficiency + (tierMax - tierMin) * 0.05m);

            WorkerLevelUp?.Invoke(this, worker);
        }
    }

    public WorkerMarketPool GetWorkerMarket()
    {
        lock (_lock)
        {
            var state = _gameState.State;
            if (state.WorkerMarket == null)
            {
                state.WorkerMarket = new WorkerMarketPool();
                state.WorkerMarket.GeneratePool(state.PlayerLevel, state.Prestige?.TotalPrestigeCount ?? 0);
            }
            else if (state.WorkerMarket.NeedsRotation)
            {
                state.WorkerMarket.GeneratePool(state.PlayerLevel, state.Prestige?.TotalPrestigeCount ?? 0);
            }
            return state.WorkerMarket;
        }
    }

    public WorkerMarketPool RefreshMarket()
    {
        lock (_lock)
        {
            var state = _gameState.State;
            state.WorkerMarket ??= new WorkerMarketPool();
            state.WorkerMarket.GeneratePool(state.PlayerLevel, state.Prestige?.TotalPrestigeCount ?? 0);
            return state.WorkerMarket;
        }
    }

    public List<Worker> GetAllWorkers()
    {
        lock (_lock)
        {
            return _gameState.State.Workshops.SelectMany(w => w.Workers).ToList();
        }
    }

    public Worker? GetWorker(string id)
    {
        lock (_lock)
        {
            return _gameState.State.Workshops
                .SelectMany(w => w.Workers)
                .FirstOrDefault(w => w.Id == id);
        }
    }
}
