using System;
using HandwerkerImperium.Domain.Buildings;

namespace HandwerkerImperium.Domain.Economy
{
    /// <summary>
    /// Service-Formel-Extrakt aus <c>WorkerService</c> (Avalonia-Original): die reinen Per-Tick-
    /// Simulationsmethoden fuer Worker (Ruhen, Training, Arbeiten) — mutieren den uebergebenen Worker,
    /// frei von GameState/Services. 1:1 zur Vorlage (UpdateResting/UpdateEfficiency-/Endurance-/Morale-
    /// Training/UpdateWorking).
    ///
    /// Bewusst NICHT extrahiert (state-/service-/event-gekoppelt, bleiben im Game-Service):
    /// Hire/Fire/Transfer/Start-/StopTraining/Resting, UpdateWorkerStates (Lock + Event-Sammlung),
    /// Trainings-Kosten-Abzug (_gameState.TrySpendMoney) und Markt-Generierung.
    /// </summary>
    public static class WorkerTickFormulas
    {
        /// <summary>Ruhe-Tick: Fatigue- und Mood-Erholung (Canteen + Ausruestung), Auto-Stop bei Fatigue 0 inkl. Training-Resume.</summary>
        public static void ApplyResting(Worker worker, decimal deltaHours, Building? canteen)
        {
            decimal restMultiplier = 1m + (canteen?.RestTimeReduction ?? 0m);

            decimal fatigueRecovery = (100m / worker.RestHoursNeeded) * deltaHours * restMultiplier;
            var equipFatigueReduction = worker.EquippedItem?.FatigueReduction ?? 0m;
            if (equipFatigueReduction > 0)
                fatigueRecovery *= (1m + equipFatigueReduction);
            worker.Fatigue = Math.Max(0m, worker.Fatigue - fatigueRecovery);

            decimal moodRecovery = 1m + (canteen?.MoodRecoveryPerHour ?? 0m);
            var equipMoodBonus = worker.EquippedItem?.MoodBonus ?? 0m;
            if (equipMoodBonus > 0)
                moodRecovery *= (1m + equipMoodBonus);
            worker.Mood = Math.Min(100m, worker.Mood + moodRecovery * deltaHours);

            if (worker.Fatigue <= 0m)
            {
                worker.IsResting = false;
                worker.RestStartedAt = null;

                if (worker.ResumeTrainingType != null)
                {
                    var trainingType = worker.ResumeTrainingType.Value;
                    worker.ResumeTrainingType = null;

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

        /// <summary>
        /// Trainings-Tick (Dispatch nach <see cref="Worker.ActiveTrainingType"/>) + Fatigue-Anstieg (halbe
        /// Arbeitsrate, mit Ausruestung) + Auto-Rest bei 100% Fatigue (merkt Training-Typ fuer Resume).
        /// Der Kosten-Abzug (TrainingCostPerHour) bleibt im Game-Service.
        /// </summary>
        public static void ApplyTrainingTick(Worker worker, decimal deltaHours, decimal trainingMultiplier)
        {
            switch (worker.ActiveTrainingType)
            {
                case TrainingType.Efficiency:
                    ApplyEfficiencyTraining(worker, deltaHours, trainingMultiplier);
                    break;
                case TrainingType.Endurance:
                    ApplyEnduranceTraining(worker, deltaHours, trainingMultiplier);
                    break;
                case TrainingType.Morale:
                    ApplyMoraleTraining(worker, deltaHours, trainingMultiplier);
                    break;
            }

            var trainingFatigueRate = worker.FatiguePerHour * 0.5m;
            var equipFatReduction = worker.EquippedItem?.FatigueReduction ?? 0m;
            if (equipFatReduction > 0)
                trainingFatigueRate *= (1m - equipFatReduction);
            worker.Fatigue = Math.Min(100m, worker.Fatigue + trainingFatigueRate * deltaHours);

            if (worker.Fatigue >= 100m)
            {
                worker.ResumeTrainingType = worker.ActiveTrainingType;
                worker.IsTraining = false;
                worker.TrainingStartedAt = null;
                worker.IsResting = true;
                worker.RestStartedAt = DateTime.UtcNow;
            }
        }

        /// <summary>Effizienz-Training: XP (Personality- + Gebaeude-Multiplikator, Akkumulator), Level-Up bis 10 + Efficiency-Step.</summary>
        public static void ApplyEfficiencyTraining(Worker worker, decimal deltaHours, decimal trainingMultiplier)
        {
            decimal xpGain = worker.TrainingXpPerHour * deltaHours * worker.Personality.GetXpMultiplier() * trainingMultiplier;
            worker.TrainingXpAccumulator += xpGain;
            if (worker.TrainingXpAccumulator >= 1m)
            {
                int wholeXp = (int)worker.TrainingXpAccumulator;
                worker.ExperienceXp += wholeXp;
                worker.TrainingXpAccumulator -= wholeXp;
            }

            if (worker.ExperienceXp >= worker.XpForNextLevel && worker.ExperienceLevel < 10)
            {
                worker.ExperienceXp -= worker.XpForNextLevel;
                worker.ExperienceLevel++;

                var tierMax = worker.Tier.GetMaxEfficiency();
                var tierMin = worker.Tier.GetMinEfficiency();
                worker.Efficiency = Math.Min(tierMax, worker.Efficiency + (tierMax - tierMin) * 0.05m);
            }
        }

        /// <summary>Ausdauer-Training: +0.05/h × Multiplikator, Cap 0.5 (50% Reduktion), Auto-Stop bei Max.</summary>
        public static void ApplyEnduranceTraining(Worker worker, decimal deltaHours, decimal trainingMultiplier)
        {
            decimal gain = 0.05m * deltaHours * trainingMultiplier;
            worker.EnduranceBonus = Math.Min(0.5m, worker.EnduranceBonus + gain);

            if (worker.EnduranceBonus >= 0.5m)
            {
                worker.EnduranceBonus = 0.5m;
                worker.IsTraining = false;
                worker.TrainingStartedAt = null;
            }
        }

        /// <summary>Stimmungs-Training: +0.05/h × Multiplikator, Cap 0.5 (50% Reduktion), Auto-Stop bei Max.</summary>
        public static void ApplyMoraleTraining(Worker worker, decimal deltaHours, decimal trainingMultiplier)
        {
            decimal gain = 0.05m * deltaHours * trainingMultiplier;
            worker.MoraleBonus = Math.Min(0.5m, worker.MoraleBonus + gain);

            if (worker.MoraleBonus >= 0.5m)
            {
                worker.MoraleBonus = 0.5m;
                worker.IsTraining = false;
                worker.TrainingStartedAt = null;
            }
        }

        /// <summary>
        /// Arbeits-Tick: Stimmungsabfall (Prestige-/Manager-/Gilden-/Ausruestungs-Reduktion + Canteen-Passiv-Erholung),
        /// Fatigue-Anstieg, Auto-Rest bei 100%, passiver XP-Gewinn (25% der Trainingsrate) + Level-Up.
        /// Boni werden vom Game-Service vorberechnet uebergeben.
        /// </summary>
        public static void ApplyWorking(Worker worker, decimal deltaHours, decimal moodDecayReduction,
            decimal guildFatigueReduction, Building? canteen, decimal managerMoodBonus = 0m)
        {
            var moodDecay = worker.MoodDecayPerHour;
            if (moodDecayReduction > 0)
                moodDecay *= (1m - moodDecayReduction);
            if (managerMoodBonus > 0)
                moodDecay *= (1m - Math.Min(managerMoodBonus, 0.50m));
            if (guildFatigueReduction > 0)
                moodDecay *= (1m - guildFatigueReduction);
            var equipMoodBonus = worker.EquippedItem?.MoodBonus ?? 0m;
            if (equipMoodBonus > 0)
                moodDecay *= (1m - equipMoodBonus);

            decimal passiveMoodRecovery = canteen?.MoodRecoveryPerHour ?? 0m;
            decimal netMoodChange = moodDecay - passiveMoodRecovery;

            if (netMoodChange > 0)
                worker.Mood = Math.Max(0m, worker.Mood - netMoodChange * deltaHours);
            else
                worker.Mood = Math.Min(100m, worker.Mood + Math.Abs(netMoodChange) * deltaHours);

            var fatigueRate = worker.FatiguePerHour;
            if (guildFatigueReduction > 0)
                fatigueRate *= (1m - guildFatigueReduction);
            var equipFatigueReduction = worker.EquippedItem?.FatigueReduction ?? 0m;
            if (equipFatigueReduction > 0)
                fatigueRate *= (1m - equipFatigueReduction);
            worker.Fatigue = Math.Min(100m, worker.Fatigue + fatigueRate * deltaHours);

            if (worker.Fatigue >= 100m)
            {
                worker.IsResting = true;
                worker.RestStartedAt = DateTime.UtcNow;
            }

            decimal xpGain = worker.TrainingXpPerHour * 0.25m * deltaHours * worker.Personality.GetXpMultiplier();
            worker.WorkingXpAccumulator += xpGain;
            if (worker.WorkingXpAccumulator >= 1m)
            {
                int wholeXp = (int)worker.WorkingXpAccumulator;
                worker.ExperienceXp += wholeXp;
                worker.WorkingXpAccumulator -= wholeXp;
            }

            if (worker.ExperienceXp >= worker.XpForNextLevel && worker.ExperienceLevel < 10)
            {
                worker.ExperienceXp -= worker.XpForNextLevel;
                worker.ExperienceLevel++;

                var tierMax = worker.Tier.GetMaxEfficiency();
                var tierMin = worker.Tier.GetMinEfficiency();
                worker.Efficiency = Math.Min(tierMax, worker.Efficiency + (tierMax - tierMin) * 0.05m);
            }
        }
    }
}
