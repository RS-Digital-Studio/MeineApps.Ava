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
    private readonly IPrestigeService? _prestigeService;
    private readonly object _lock = new();

    public event EventHandler<Worker>? WorkerMoodWarning;
    public event EventHandler<Worker>? WorkerQuit;
    public event EventHandler<Worker>? WorkerLevelUp;

    public WorkerService(IGameStateService gameState, IPrestigeService? prestigeService = null)
    {
        _gameState = gameState;
        _prestigeService = prestigeService;
    }

    public bool HireWorker(Worker worker, WorkshopType workshop)
    {
        lock (_lock)
        {
            var state = _gameState.State;
            var ws = state.GetOrCreateWorkshop(workshop);

            // Check if workshop can accept more workers
            if (ws.Workers.Count >= ws.MaxWorkers) return false;

            // Kosten: Level-skalierte Anstellungskosten vom Worker (bereits in LoadMarket berechnet)
            var hiringCost = worker.HiringCost > 0 ? worker.HiringCost : worker.Tier.GetHiringCost(state.PlayerLevel);
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
            state.InvalidateIncomeCache();
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
                    state.InvalidateIncomeCache();
                    return true;
                }
            }
            return false;
        }
    }

    public bool ReinstateWorker(Worker worker, WorkshopType workshop)
    {
        lock (_lock)
        {
            var state = _gameState.State;
            var ws = state.GetOrCreateWorkshop(workshop);

            // Prüfe ob noch Platz im Workshop (MaxWorkers)
            if (ws.Workers.Count >= ws.MaxWorkers) return false;

            worker.AssignedWorkshop = workshop;
            ws.Workers.Add(worker);

            // Zähler rückgängig machen
            if (state.TotalWorkersFired > 0) state.TotalWorkersFired--;
            state.InvalidateIncomeCache();
            return true;
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
            state.InvalidateIncomeCache();
            return true;
        }
    }

    public bool StartTraining(string workerId, TrainingType trainingType = TrainingType.Efficiency)
    {
        lock (_lock)
        {
            var worker = GetWorker(workerId);
            if (worker == null || worker.IsTraining || worker.IsResting) return false;

            // Effizienz-Training nur bis Level 10
            if (trainingType == TrainingType.Efficiency && worker.ExperienceLevel >= 10) return false;
            // Ausdauer-Training nur bis 50% Reduktion
            if (trainingType == TrainingType.Endurance && worker.EnduranceBonus >= 0.5m) return false;
            // Stimmungs-Training nur bis 50% Reduktion
            if (trainingType == TrainingType.Morale && worker.MoraleBonus >= 0.5m) return false;

            worker.IsTraining = true;
            worker.ActiveTrainingType = trainingType;
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
                        UpdateResting(worker, deltaHours, state);
                    }
                    else if (worker.IsTraining)
                    {
                        UpdateTraining(worker, deltaHours, state);
                    }
                    else
                    {
                        UpdateWorking(worker, deltaHours, state);
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

    private void UpdateResting(Worker worker, decimal deltaHours, GameState state)
    {
        // Canteen-Gebäude: Erholungszeit-Reduktion
        var canteen = state.GetBuilding(BuildingType.Canteen);
        decimal restMultiplier = 1m + (canteen?.RestTimeReduction ?? 0m); // z.B. 1.5 = 50% schneller

        // Fatigue-Erholung (schneller mit Canteen)
        decimal fatigueRecovery = (100m / worker.RestHoursNeeded) * deltaHours * restMultiplier;
        worker.Fatigue = Math.Max(0m, worker.Fatigue - fatigueRecovery);

        // Stimmungs-Erholung beim Ruhen (Canteen-Bonus addiert)
        decimal moodRecovery = 1m + (canteen?.MoodRecoveryPerHour ?? 0m);
        worker.Mood = Math.Min(100m, worker.Mood + moodRecovery * deltaHours);

        // Automatisch Ruhe beenden wenn voll erholt
        if (worker.Fatigue <= 0m)
        {
            worker.IsResting = false;
            worker.RestStartedAt = null;
        }
    }

    private void UpdateTraining(Worker worker, decimal deltaHours, GameState state)
    {
        // Training-Kosten pro Tick
        var trainingCost = worker.TrainingCostPerHour * deltaHours;
        if (!_gameState.CanAfford(trainingCost))
        {
            // Training stoppen wenn nicht leistbar
            worker.IsTraining = false;
            worker.TrainingStartedAt = null;
            return;
        }
        _gameState.TrySpendMoney(trainingCost);

        // TrainingCenter-Gebäude + Research: Trainings-Geschwindigkeit
        var trainingCenter = state.GetBuilding(BuildingType.TrainingCenter);
        decimal trainingMultiplier = trainingCenter?.TrainingSpeedMultiplier ?? 1m;

        // Gilden-Forschung: Training-Geschwindigkeit (+25%)
        if (state.GuildMembership?.ResearchTrainingSpeedBonus > 0)
            trainingMultiplier *= (1m + state.GuildMembership.ResearchTrainingSpeedBonus);

        switch (worker.ActiveTrainingType)
        {
            case TrainingType.Efficiency:
                UpdateEfficiencyTraining(worker, deltaHours, trainingMultiplier);
                break;
            case TrainingType.Endurance:
                UpdateEnduranceTraining(worker, deltaHours, trainingMultiplier);
                break;
            case TrainingType.Morale:
                UpdateMoraleTraining(worker, deltaHours, trainingMultiplier);
                break;
        }

        // Training erhöht Erschöpfung (langsamer als Arbeiten)
        worker.Fatigue = Math.Min(100m, worker.Fatigue + worker.FatiguePerHour * 0.5m * deltaHours);
    }

    private void UpdateEfficiencyTraining(Worker worker, decimal deltaHours, decimal trainingMultiplier)
    {
        // XP-Gewinn (mit Gebäude-Multiplikator, Akkumulator für fraktionale XP)
        decimal xpGain = worker.TrainingXpPerHour * deltaHours * worker.Personality.GetXpMultiplier() * trainingMultiplier;
        worker.TrainingXpAccumulator += xpGain;
        if (worker.TrainingXpAccumulator >= 1m)
        {
            int wholeXp = (int)worker.TrainingXpAccumulator;
            worker.ExperienceXp += wholeXp;
            worker.TrainingXpAccumulator -= wholeXp;
        }

        // Level up check
        if (worker.ExperienceXp >= worker.XpForNextLevel && worker.ExperienceLevel < 10)
        {
            worker.ExperienceXp -= worker.XpForNextLevel;
            worker.ExperienceLevel++;

            // Effizienz-Steigerung bei Level-Up
            var tierMax = worker.Tier.GetMaxEfficiency();
            var tierMin = worker.Tier.GetMinEfficiency();
            worker.Efficiency = Math.Min(tierMax, worker.Efficiency + (tierMax - tierMin) * 0.05m);

            WorkerLevelUp?.Invoke(this, worker);
        }
    }

    private static void UpdateEnduranceTraining(Worker worker, decimal deltaHours, decimal trainingMultiplier)
    {
        // Ausdauer-Bonus: +0.05 pro Stunde Training (max 0.5 = 50% Reduktion)
        decimal gain = 0.05m * deltaHours * trainingMultiplier;
        worker.EnduranceBonus = Math.Min(0.5m, worker.EnduranceBonus + gain);

        // Automatisch stoppen wenn Maximum erreicht
        if (worker.EnduranceBonus >= 0.5m)
        {
            worker.EnduranceBonus = 0.5m;
            worker.IsTraining = false;
            worker.TrainingStartedAt = null;
        }
    }

    private static void UpdateMoraleTraining(Worker worker, decimal deltaHours, decimal trainingMultiplier)
    {
        // Stimmungs-Bonus: +0.05 pro Stunde Training (max 0.5 = 50% Reduktion)
        decimal gain = 0.05m * deltaHours * trainingMultiplier;
        worker.MoraleBonus = Math.Min(0.5m, worker.MoraleBonus + gain);

        // Automatisch stoppen wenn Maximum erreicht
        if (worker.MoraleBonus >= 0.5m)
        {
            worker.MoraleBonus = 0.5m;
            worker.IsTraining = false;
            worker.TrainingStartedAt = null;
        }
    }

    private void UpdateWorking(Worker worker, decimal deltaHours, GameState state)
    {
        // Stimmungsabfall beim Arbeiten (mit Prestige-Shop MoodDecayReduction)
        var moodDecay = worker.MoodDecayPerHour;
        if (_prestigeService != null)
        {
            var reduction = _prestigeService.GetMoodDecayReduction();
            if (reduction > 0)
                moodDecay *= (1m - reduction);
        }

        // Gilden-Forschung: Ermüdungs-/Stimmungs-Reduktion (-20%)
        if (state.GuildMembership?.ResearchFatigueReduction > 0)
            moodDecay *= (1m - state.GuildMembership.ResearchFatigueReduction);

        // Canteen-Gebäude: Passive Stimmungs-Erholung auch beim Arbeiten
        var canteen = state.GetBuilding(BuildingType.Canteen);
        decimal passiveMoodRecovery = canteen?.MoodRecoveryPerHour ?? 0m;
        decimal netMoodChange = moodDecay - passiveMoodRecovery;

        if (netMoodChange > 0)
            worker.Mood = Math.Max(0m, worker.Mood - netMoodChange * deltaHours);
        else
            worker.Mood = Math.Min(100m, worker.Mood + Math.Abs(netMoodChange) * deltaHours);

        // Fatigue increases while working (mit Gilden-Forschung Reduktion)
        var fatigueRate = worker.FatiguePerHour;
        if (state.GuildMembership?.ResearchFatigueReduction > 0)
            fatigueRate *= (1m - state.GuildMembership.ResearchFatigueReduction);
        worker.Fatigue = Math.Min(100m, worker.Fatigue + fatigueRate * deltaHours);

        // Auto-Rest bei 100% Erschöpfung
        if (worker.Fatigue >= 100m)
        {
            worker.IsResting = true;
            worker.RestStartedAt = DateTime.UtcNow;
        }

        // Langsamer XP-Gewinn beim Arbeiten (10% der Trainingsrate)
        decimal xpGain = worker.TrainingXpPerHour * 0.1m * deltaHours * worker.Personality.GetXpMultiplier();
        worker.WorkingXpAccumulator += xpGain;
        if (worker.WorkingXpAccumulator >= 1m)
        {
            int wholeXp = (int)worker.WorkingXpAccumulator;
            worker.ExperienceXp += wholeXp;
            worker.WorkingXpAccumulator -= wholeXp;
        }

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
            // FreeRefreshUsed-Flag bewahren - GeneratePool setzt ihn zurück
            // (nur bei Rotation soll er zurückgesetzt werden, nicht bei manuellem Refresh)
            var freeRefreshUsed = state.WorkerMarket.FreeRefreshUsedThisRotation;
            state.WorkerMarket.GeneratePool(state.PlayerLevel, state.Prestige?.TotalPrestigeCount ?? 0);
            state.WorkerMarket.FreeRefreshUsedThisRotation = freeRefreshUsed;
            return state.WorkerMarket;
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
