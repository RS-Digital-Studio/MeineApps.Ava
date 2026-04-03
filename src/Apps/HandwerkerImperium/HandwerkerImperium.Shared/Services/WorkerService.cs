using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Manages worker lifecycle: hiring, firing, training, resting, mood, fatigue.
/// </summary>
public sealed class WorkerService : IWorkerService
{
    private readonly IGameStateService _gameState;
    private readonly IPrestigeService? _prestigeService;
    private readonly IResearchService? _researchService;
    private readonly IManagerService? _managerService;
    private readonly object _lock = new();

    // Wiederverwendbare Liste für Kündigungen (vermeidet Allokation pro Tick)
    private readonly List<(Workshop ws, Worker worker)> _workersToRemove = new();

    public event EventHandler<Worker>? WorkerMoodWarning;
    public event EventHandler<Worker>? WorkerQuit;
    public event EventHandler<Worker>? WorkerLevelUp;

    public WorkerService(IGameStateService gameState, IPrestigeService? prestigeService = null,
        IResearchService? researchService = null, IManagerService? managerService = null)
    {
        _gameState = gameState;
        _prestigeService = prestigeService;
        _researchService = researchService;
        _managerService = managerService;
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

            state.Statistics.TotalWorkersHired++;
            state.InvalidateIncomeCache();
            return true;
        }
    }

    public bool FireWorker(string workerId)
    {
        lock (_lock)
        {
            var state = _gameState.State;
            // Doppelte For-Schleife statt LINQ FirstOrDefault (konsistent mit GetWorker)
            for (int i = 0; i < state.Workshops.Count; i++)
            {
                var workers = state.Workshops[i].Workers;
                for (int j = 0; j < workers.Count; j++)
                {
                    if (workers[j].Id == workerId)
                    {
                        workers.RemoveAt(j);
                        state.Statistics.TotalWorkersFired++;
                        state.InvalidateIncomeCache();
                        return true;
                    }
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
            if (state.Statistics.TotalWorkersFired > 0) state.Statistics.TotalWorkersFired--;
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

            // Doppelte For-Schleife statt LINQ FirstOrDefault (konsistent mit GetWorker)
            Worker? worker = null;
            Workshop? sourceWs = null;
            for (int i = 0; i < state.Workshops.Count; i++)
            {
                var ws = state.Workshops[i];
                for (int j = 0; j < ws.Workers.Count; j++)
                {
                    if (ws.Workers[j].Id == workerId)
                    {
                        worker = ws.Workers[j];
                        sourceWs = ws;
                        break;
                    }
                }
                if (worker != null) break;
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
            worker.ResumeTrainingType = null; // Manuell gestoppt → kein Auto-Resume
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
            _workersToRemove.Clear();

            // Lookups VOR der Schleife cachen (vermeidet 50+ redundante Aufrufe/s bei vielen Workern)
            var canteen = state.GetBuilding(BuildingType.Canteen);
            var trainingCenter = state.GetBuilding(BuildingType.TrainingCenter);
            decimal moodDecayReduction = _prestigeService?.GetMoodDecayReduction() ?? 0m;
            decimal guildFatigueReduction = state.GuildMembership?.ResearchFatigueReduction ?? 0m;
            decimal guildTrainingSpeedBonus = state.GuildMembership?.ResearchTrainingSpeedBonus ?? 0m;
            // Globale Manager-Boni (einmal pro Tick statt pro Worker)
            decimal globalMgrFatigue = _managerService?.GetGlobalManagerBonus(ManagerAbility.FatigueReduction) ?? 0m;
            decimal globalMgrMood = _managerService?.GetGlobalManagerBonus(ManagerAbility.MoodBoost) ?? 0m;
            decimal globalMgrTraining = _managerService?.GetGlobalManagerBonus(ManagerAbility.TrainingSpeedUp) ?? 0m;

            foreach (var ws in state.Workshops)
            {
                // Workshop-spezifische Manager-Boni (einmal pro Workshop statt pro Worker)
                decimal wsMgrFatigue = (_managerService?.GetManagerBonusForWorkshop(ws.Type, ManagerAbility.FatigueReduction) ?? 0m) + globalMgrFatigue;
                decimal wsMgrMood = (_managerService?.GetManagerBonusForWorkshop(ws.Type, ManagerAbility.MoodBoost) ?? 0m) + globalMgrMood;
                decimal wsMgrTraining = (_managerService?.GetManagerBonusForWorkshop(ws.Type, ManagerAbility.TrainingSpeedUp) ?? 0m) + globalMgrTraining;

                foreach (var worker in ws.Workers)
                {
                    if (worker.IsResting)
                    {
                        UpdateResting(worker, deltaHours, canteen);
                    }
                    else if (worker.IsTraining)
                    {
                        UpdateTraining(worker, deltaHours, trainingCenter, guildTrainingSpeedBonus + wsMgrTraining);
                    }
                    else
                    {
                        UpdateWorking(worker, deltaHours, moodDecayReduction, guildFatigueReduction + wsMgrFatigue, canteen, wsMgrMood);
                    }

                    // Kündigungsbedingungen prüfen
                    if (worker.WillQuit)
                    {
                        if (worker.QuitDeadline == null)
                        {
                            worker.QuitDeadline = DateTime.UtcNow.AddHours(24);
                            WorkerMoodWarning?.Invoke(this, worker);
                        }
                        else if (DateTime.UtcNow >= worker.QuitDeadline)
                        {
                            _workersToRemove.Add((ws, worker));
                        }
                    }
                    else
                    {
                        worker.QuitDeadline = null;
                    }
                }
            }

            // Gekündigte Worker entfernen
            foreach (var (ws, worker) in _workersToRemove)
            {
                ws.Workers.Remove(worker);
                state.Statistics.TotalWorkersFired++;
                WorkerQuit?.Invoke(this, worker);
            }
        }
    }

    private static void UpdateResting(Worker worker, decimal deltaHours, Building? canteen)
    {
        // Canteen-Gebäude: Erholungszeit-Reduktion (gecacht übergeben)
        decimal restMultiplier = 1m + (canteen?.RestTimeReduction ?? 0m);

        // Fatigue-Erholung (schneller mit Canteen + Ausrüstung)
        decimal fatigueRecovery = (100m / worker.RestHoursNeeded) * deltaHours * restMultiplier;
        var equipFatigueReduction = worker.EquippedItem?.FatigueReduction ?? 0m;
        if (equipFatigueReduction > 0)
            fatigueRecovery *= (1m + equipFatigueReduction);
        worker.Fatigue = Math.Max(0m, worker.Fatigue - fatigueRecovery);

        // Stimmungs-Erholung beim Ruhen (Canteen-Bonus + Ausrüstung)
        decimal moodRecovery = 1m + (canteen?.MoodRecoveryPerHour ?? 0m);
        var equipMoodBonus = worker.EquippedItem?.MoodBonus ?? 0m;
        if (equipMoodBonus > 0)
            moodRecovery *= (1m + equipMoodBonus);
        worker.Mood = Math.Min(100m, worker.Mood + moodRecovery * deltaHours);

        // Automatisch Ruhe beenden wenn voll erholt
        if (worker.Fatigue <= 0m)
        {
            worker.IsResting = false;
            worker.RestStartedAt = null;

            // Training automatisch fortsetzen wenn der Worker vor der Ruhe trainiert hat
            if (worker.ResumeTrainingType != null)
            {
                var trainingType = worker.ResumeTrainingType.Value;
                worker.ResumeTrainingType = null;

                // Nur fortsetzen wenn Training noch nicht abgeschlossen ist
                bool canResume = trainingType switch
                {
                    TrainingType.Efficiency => worker.ExperienceLevel < 10,
                    TrainingType.Endurance => worker.EnduranceBonus < 0.5m,
                    TrainingType.Morale => worker.MoraleBonus < 0.5m,
                    _ => false
                };

                if (canResume)
                {
                    worker.IsTraining = true;
                    worker.ActiveTrainingType = trainingType;
                    worker.TrainingStartedAt = DateTime.UtcNow;
                }
            }
        }
    }

    private void UpdateTraining(Worker worker, decimal deltaHours, Building? trainingCenter, decimal guildTrainingSpeedBonus)
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

        // TrainingCenter-Gebäude + Gilden-Forschung: Trainings-Geschwindigkeit (gecacht übergeben)
        decimal trainingMultiplier = trainingCenter?.TrainingSpeedMultiplier ?? 1m;
        if (guildTrainingSpeedBonus > 0)
            trainingMultiplier *= (1m + guildTrainingSpeedBonus);

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

        // Training erhöht Erschöpfung (langsamer als Arbeiten, mit Ausrüstungs-Bonus)
        var trainingFatigueRate = worker.FatiguePerHour * 0.5m;
        var equipFatReduction = worker.EquippedItem?.FatigueReduction ?? 0m;
        if (equipFatReduction > 0)
            trainingFatigueRate *= (1m - equipFatReduction);
        worker.Fatigue = Math.Min(100m, worker.Fatigue + trainingFatigueRate * deltaHours);

        // Auto-Rest bei 100% Erschöpfung: Training-Typ merken für automatische Fortsetzung
        if (worker.Fatigue >= 100m)
        {
            worker.ResumeTrainingType = worker.ActiveTrainingType;
            worker.IsTraining = false;
            worker.TrainingStartedAt = null;
            worker.IsResting = true;
            worker.RestStartedAt = DateTime.UtcNow;
        }
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

    private void UpdateWorking(Worker worker, decimal deltaHours, decimal moodDecayReduction, decimal guildFatigueReduction, Building? canteen, decimal managerMoodBonus = 0m)
    {
        // Stimmungsabfall beim Arbeiten (gecachte Prestige-Shop MoodDecayReduction)
        var moodDecay = worker.MoodDecayPerHour;
        if (moodDecayReduction > 0)
            moodDecay *= (1m - moodDecayReduction);

        // Manager-MoodBoost: Reduziert Stimmungsabfall
        if (managerMoodBonus > 0)
            moodDecay *= (1m - Math.Min(managerMoodBonus, 0.50m));

        // Gilden-Forschung: Ermüdungs-/Stimmungs-Reduktion (gecacht übergeben)
        if (guildFatigueReduction > 0)
            moodDecay *= (1m - guildFatigueReduction);

        // Ausrüstungs-Bonus: MoodBonus reduziert Stimmungsabfall
        var equipMoodBonus = worker.EquippedItem?.MoodBonus ?? 0m;
        if (equipMoodBonus > 0)
            moodDecay *= (1m - equipMoodBonus);

        // Canteen-Gebäude: Passive Stimmungs-Erholung auch beim Arbeiten (gecacht übergeben)
        decimal passiveMoodRecovery = canteen?.MoodRecoveryPerHour ?? 0m;
        decimal netMoodChange = moodDecay - passiveMoodRecovery;

        if (netMoodChange > 0)
            worker.Mood = Math.Max(0m, worker.Mood - netMoodChange * deltaHours);
        else
            worker.Mood = Math.Min(100m, worker.Mood + Math.Abs(netMoodChange) * deltaHours);

        // Fatigue steigt beim Arbeiten (gecachte Gilden-Forschung Reduktion + Ausrüstung)
        var fatigueRate = worker.FatiguePerHour;
        if (guildFatigueReduction > 0)
            fatigueRate *= (1m - guildFatigueReduction);
        var equipFatigueReduction = worker.EquippedItem?.FatigueReduction ?? 0m;
        if (equipFatigueReduction > 0)
            fatigueRate *= (1m - equipFatigueReduction);
        worker.Fatigue = Math.Min(100m, worker.Fatigue + fatigueRate * deltaHours);

        // Auto-Rest bei 100% Erschöpfung
        if (worker.Fatigue >= 100m)
        {
            worker.IsResting = true;
            worker.RestStartedAt = DateTime.UtcNow;
        }

        // Passiver XP-Gewinn beim Arbeiten (25% der Trainingsrate = 12.5 XP/h)
        decimal xpGain = worker.TrainingXpPerHour * 0.25m * deltaHours * worker.Personality.GetXpMultiplier();
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
            var effects = _researchService?.GetTotalEffects();
            bool hasHeadhunter = effects?.UnlocksHeadhunter ?? false;
            bool hasSTier = effects?.UnlocksSTierWorkers ?? false;

            if (state.WorkerMarket == null)
            {
                state.WorkerMarket = new WorkerMarketPool();
                state.WorkerMarket.GeneratePool(state.PlayerLevel, state.Prestige?.TotalPrestigeCount ?? 0, hasHeadhunter, hasSTier);
            }
            else if (state.WorkerMarket.NeedsRotation)
            {
                state.WorkerMarket.GeneratePool(state.PlayerLevel, state.Prestige?.TotalPrestigeCount ?? 0, hasHeadhunter, hasSTier);
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
            var effects = _researchService?.GetTotalEffects();
            bool hasHeadhunter = effects?.UnlocksHeadhunter ?? false;
            bool hasSTier = effects?.UnlocksSTierWorkers ?? false;

            // FreeRefreshUsed-Flag bewahren - GeneratePool setzt ihn zurück
            // (nur bei Rotation soll er zurückgesetzt werden, nicht bei manuellem Refresh)
            var freeRefreshUsed = state.WorkerMarket.FreeRefreshUsedThisRotation;
            state.WorkerMarket.GeneratePool(state.PlayerLevel, state.Prestige?.TotalPrestigeCount ?? 0, hasHeadhunter, hasSTier);
            state.WorkerMarket.FreeRefreshUsedThisRotation = freeRefreshUsed;
            return state.WorkerMarket;
        }
    }

    public Worker? GetWorker(string id)
    {
        lock (_lock)
        {
            // Doppelte For-Schleife statt LINQ SelectMany (vermeidet Enumerator-Allokation)
            var workshops = _gameState.State.Workshops;
            for (int i = 0; i < workshops.Count; i++)
            {
                var workers = workshops[i].Workers;
                for (int j = 0; j < workers.Count; j++)
                {
                    if (workers[j].Id == id)
                        return workers[j];
                }
            }
            return null;
        }
    }
}
