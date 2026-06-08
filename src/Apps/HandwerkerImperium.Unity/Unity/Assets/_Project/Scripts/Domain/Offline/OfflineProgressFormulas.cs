#nullable enable
using System;
using HandwerkerImperium.Domain.Economy;
using HandwerkerImperium.Domain.Buildings;
using HandwerkerImperium.Domain.Progression;
using HandwerkerImperium.Domain.State;

namespace HandwerkerImperium.Domain.Offline
{
    /// <summary>
    /// Reine Offline-Progress-Formeln, extrahiert aus dem Avalonia-Original
    /// (Services/OfflineProgressService.cs): gestaffelte Earnings (80/35/15/5), anteiliges Boost-Stacking
    /// (multiplikativ Speed×Rush), und die Worker-Offline-Simulation (Training/Arbeit/Ruhe/XP). Die
    /// Service-Orchestrierung (TrySpendMoney, IncomeCalculator, ChallengeConstraints, AutoProduction)
    /// bleibt zurückgestellt; hier nur die deterministische Mathematik (1:1 zum Original).
    /// </summary>
    public static class OfflineProgressFormulas
    {
        /// <summary>
        /// Gestaffelte Offline-Earnings: 0-2h 80%, 2-4h 35%, 4-8h 15%, 8h+ 5%.
        /// </summary>
        public static decimal CalculateStaggeredEarnings(decimal netPerSecond, decimal totalSeconds)
        {
            decimal first2h = Math.Min(totalSeconds, 7200m);
            decimal next2h = Math.Min(Math.Max(totalSeconds - 7200m, 0m), 7200m);
            decimal next4h = Math.Min(Math.Max(totalSeconds - 14400m, 0m), 14400m);
            decimal remaining = Math.Max(totalSeconds - 28800m, 0m);
            return netPerSecond * (first2h * 0.80m + next2h * 0.35m + next4h * 0.15m + remaining * 0.05m);
        }

        /// <summary>Maximale Offline-Dauer (aus GameState.MaxOfflineHours: free 4 / premium 16 / +Shop-Bonus).</summary>
        public static TimeSpan GetMaxOfflineDuration(GameState state) => TimeSpan.FromHours(state.MaxOfflineHours);

        /// <summary>
        /// Offline-Dauer mit Zeitmanipulations-Schutz: lastPlayed in der Zukunft → TimeSpan.Zero.
        /// </summary>
        public static TimeSpan GetOfflineDuration(DateTime lastPlayed, DateTime now)
        {
            if (lastPlayed > now)
                return TimeSpan.Zero;
            return now - lastPlayed;
        }

        /// <summary>Rush-Multiplikator-Bonus aus gekauften (nicht-wiederholbaren) Prestige-Shop-Items.</summary>
        public static decimal GetPrestigeRushBonus(GameState state)
        {
            var purchased = state.Prestige.PurchasedShopItems;
            if (purchased.Count == 0) return 0m;

            decimal bonus = 0m;
            foreach (var item in PrestigeShop.GetAllItems())
            {
                if (item.IsRepeatable) continue;
                if (purchased.Contains(item.Id) && item.Effect.RushMultiplierBonus > 0)
                    bonus += item.Effect.RushMultiplierBonus;
            }
            return bonus;
        }

        /// <summary>
        /// Anteilige SpeedBoost/RushBoost-Multiplikatoren über die Offline-Dauer (multiplikatives Stacking
        /// Speed 2x × Rush 2-4x; gewichteter Durchschnitt über die Gesamt-Dauer).
        /// </summary>
        public static decimal ApplyBoostsProRata(GameState state, decimal baseEarnings, TimeSpan effectiveDuration)
        {
            if (baseEarnings <= 0 || effectiveDuration.TotalSeconds <= 0)
                return baseEarnings;

            var lastPlayed = state.LastPlayedAt;
            decimal totalSeconds = (decimal)effectiveDuration.TotalSeconds;

            decimal speedBoostSeconds = 0m;
            if (state.SpeedBoostEndTime > lastPlayed)
            {
                var boostRemaining = state.SpeedBoostEndTime - lastPlayed;
                speedBoostSeconds = Math.Min((decimal)boostRemaining.TotalSeconds, totalSeconds);
            }

            decimal rushBoostSeconds = 0m;
            decimal rushMultiplier = 2m;
            if (state.RushBoostEndTime > lastPlayed)
            {
                var boostRemaining = state.RushBoostEndTime - lastPlayed;
                rushBoostSeconds = Math.Min((decimal)boostRemaining.TotalSeconds, totalSeconds);
                rushMultiplier = Math.Min(rushMultiplier + GetPrestigeRushBonus(state), 4m);
            }

            decimal bothSeconds = Math.Max(0m, Math.Min(speedBoostSeconds, rushBoostSeconds));
            decimal onlySpeedSeconds = Math.Max(0m, speedBoostSeconds - bothSeconds);
            decimal onlyRushSeconds = Math.Max(0m, rushBoostSeconds - bothSeconds);
            decimal unboostedSeconds = totalSeconds - bothSeconds - onlySpeedSeconds - onlyRushSeconds;

            decimal weightedMultiplier = 0m;
            weightedMultiplier += bothSeconds * 2m * rushMultiplier;
            weightedMultiplier += onlySpeedSeconds * 2m;
            weightedMultiplier += onlyRushSeconds * rushMultiplier;
            weightedMultiplier += unboostedSeconds * 1m;

            decimal averageMultiplier = weightedMultiplier / totalSeconds;
            return baseEarnings * averageMultiplier;
        }

        // ── Worker-Offline-Simulation (Leaf-Helfer, im Original bereits static + pur) ──

        /// <summary>Ob das Training eines Workers abgeschlossen ist (Maximum erreicht).</summary>
        public static bool IsTrainingComplete(Worker worker) =>
            (worker.ActiveTrainingType == TrainingType.Endurance && worker.EnduranceBonus >= 0.5m) ||
            (worker.ActiveTrainingType == TrainingType.Morale && worker.MoraleBonus >= 0.5m) ||
            (worker.ActiveTrainingType == TrainingType.Efficiency && worker.ExperienceLevel >= 10);

        /// <summary>Schätzt Trainingskosten für einen Worker ohne Fortschritt zu simulieren.</summary>
        public static decimal EstimateTrainingCosts(Worker worker, decimal offlineHours)
        {
            if (IsTrainingComplete(worker))
                return 0m;
            decimal trainingHours = CalculateTrainingHours(worker, offlineHours);
            return worker.TrainingCostPerHour * trainingHours;
        }

        /// <summary>Berechnet wie viele Stunden ein Worker maximal trainieren kann (basierend auf Fatigue).</summary>
        public static decimal CalculateTrainingHours(Worker worker, decimal offlineHours)
        {
            var trainingFatigueRate = worker.FatiguePerHour * 0.5m;
            var equipFatReduction = worker.EquippedItem?.FatigueReduction ?? 0m;
            if (equipFatReduction > 0)
                trainingFatigueRate *= (1m - equipFatReduction);

            if (trainingFatigueRate > 0 && worker.Fatigue < 100m)
            {
                decimal hoursTo100 = (100m - worker.Fatigue) / trainingFatigueRate;
                return Math.Min(offlineHours, hoursTo100);
            }
            if (worker.Fatigue >= 100m)
                return 0m;
            return offlineHours;
        }

        /// <summary>Simuliert einen trainierenden Worker (Fatigue, Fortschritt proportional zum Budget).</summary>
        public static void SimulateTrainingWorker(Worker worker, decimal offlineHours,
            decimal trainingSpeedMultiplier, decimal affordableFraction, out decimal restHours,
            decimal guildFatigueReduction, decimal prestigeMoodReduction, decimal passiveMoodRecovery)
        {
            restHours = 0m;

            if (IsTrainingComplete(worker))
            {
                worker.IsTraining = false;
                worker.TrainingStartedAt = null;
                worker.IsResting = true;
                worker.RestStartedAt = DateTime.UtcNow.AddHours(-(double)offlineHours);
                restHours = offlineHours;
                return;
            }

            if (affordableFraction <= 0)
            {
                worker.IsTraining = false;
                worker.TrainingStartedAt = null;
                SimulateWorkingWorker(worker, offlineHours, guildFatigueReduction,
                    prestigeMoodReduction, passiveMoodRecovery, out restHours);
                return;
            }

            decimal maxTrainingHours = CalculateTrainingHours(worker, offlineHours);
            decimal trainingHours = maxTrainingHours * affordableFraction;

            var trainingFatigueRate = worker.FatiguePerHour * 0.5m;
            var equipFatReduction = worker.EquippedItem?.FatigueReduction ?? 0m;
            if (equipFatReduction > 0)
                trainingFatigueRate *= (1m - equipFatReduction);
            worker.Fatigue = Math.Min(100m, worker.Fatigue + trainingFatigueRate * trainingHours);

            bool reachedMaxFatigue = worker.Fatigue >= 100m;

            if (trainingHours > 0)
                SimulateTrainingProgress(worker, trainingHours, trainingSpeedMultiplier);

            bool shouldStopTraining = reachedMaxFatigue || IsTrainingComplete(worker);

            if (shouldStopTraining)
            {
                if (reachedMaxFatigue && !IsTrainingComplete(worker))
                    worker.ResumeTrainingType = worker.ActiveTrainingType;

                worker.IsTraining = false;
                worker.TrainingStartedAt = null;
                decimal remainingHours = offlineHours - trainingHours;

                if (remainingHours > 0 && worker.Fatigue < 100m)
                {
                    SimulateWorkingWorker(worker, remainingHours, guildFatigueReduction,
                        prestigeMoodReduction, passiveMoodRecovery, out restHours);
                }
                else
                {
                    worker.IsResting = true;
                    restHours = remainingHours;
                    if (restHours > 0)
                        worker.RestStartedAt = DateTime.UtcNow.AddHours(-(double)restHours);
                    else
                        worker.RestStartedAt = DateTime.UtcNow;
                }
            }
        }

        /// <summary>Simuliert einen arbeitenden Worker (Fatigue, Mood, passiver XP-Gewinn).</summary>
        public static void SimulateWorkingWorker(Worker worker, decimal offlineHours,
            decimal guildFatigueReduction, decimal prestigeMoodReduction,
            decimal passiveMoodRecovery, out decimal restHours)
        {
            restHours = 0m;

            decimal fatigueRate = worker.FatiguePerHour;
            if (guildFatigueReduction > 0)
                fatigueRate *= (1m - guildFatigueReduction);
            var equipFatigueReduction = worker.EquippedItem?.FatigueReduction ?? 0m;
            if (equipFatigueReduction > 0)
                fatigueRate *= (1m - equipFatigueReduction);

            decimal workHours = offlineHours;
            decimal originalFatigue = worker.Fatigue;

            if (originalFatigue >= 100m)
            {
                worker.IsResting = true;
                worker.RestStartedAt = DateTime.UtcNow.AddHours(-(double)offlineHours);
                restHours = offlineHours;
                workHours = 0m;
            }
            else if (fatigueRate > 0)
            {
                decimal hoursTo100 = (100m - originalFatigue) / fatigueRate;
                if (hoursTo100 < offlineHours)
                {
                    workHours = hoursTo100;
                    restHours = offlineHours - hoursTo100;
                    worker.Fatigue = 100m;
                    worker.IsResting = true;
                    worker.RestStartedAt = DateTime.UtcNow.AddHours(-(double)restHours);
                }
                else
                {
                    worker.Fatigue = originalFatigue + fatigueRate * offlineHours;
                }
            }

            if (workHours > 0)
            {
                decimal moodDecay = worker.MoodDecayPerHour;
                if (prestigeMoodReduction > 0)
                    moodDecay *= (1m - prestigeMoodReduction);
                if (guildFatigueReduction > 0)
                    moodDecay *= (1m - guildFatigueReduction);

                var equipMoodBonus = worker.EquippedItem?.MoodBonus ?? 0m;
                if (equipMoodBonus > 0)
                    moodDecay *= (1m - equipMoodBonus);

                decimal netMoodChange = moodDecay - passiveMoodRecovery;
                if (netMoodChange > 0)
                    worker.Mood = Math.Max(0m, worker.Mood - netMoodChange * workHours);
                else
                    worker.Mood = Math.Min(100m, worker.Mood + Math.Abs(netMoodChange) * workHours);

                decimal workAcc = worker.WorkingXpAccumulator;
                SimulateXpGain(worker, worker.TrainingXpPerHour * 0.25m * workHours * worker.Personality.GetXpMultiplier(),
                    ref workAcc);
                worker.WorkingXpAccumulator = workAcc;
            }
        }

        /// <summary>Simuliert Ruhephase (Fatigue-Erholung + Mood-Erholung, mit Canteen + Ausrüstung).</summary>
        public static void SimulateRestRecovery(Worker worker, decimal restHours, Building? canteen)
        {
            decimal restMultiplier = 1m + (canteen?.RestTimeReduction ?? 0m);
            decimal restBase = worker.RestHoursNeeded > 0 ? worker.RestHoursNeeded : 4m;
            decimal fatigueRecovery = (100m / restBase) * restHours * restMultiplier;
            var equipRecoveryBoost = worker.EquippedItem?.FatigueReduction ?? 0m;
            if (equipRecoveryBoost > 0)
                fatigueRecovery *= (1m + equipRecoveryBoost);
            worker.Fatigue = Math.Max(0m, worker.Fatigue - fatigueRecovery);

            decimal moodRecovery = 1m + (canteen?.MoodRecoveryPerHour ?? 0m);
            var equipMoodBonus = worker.EquippedItem?.MoodBonus ?? 0m;
            if (equipMoodBonus > 0)
                moodRecovery *= (1m + equipMoodBonus);
            worker.Mood = Math.Min(100m, worker.Mood + moodRecovery * restHours);

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

        /// <summary>Simuliert Training-Fortschritt (Efficiency: XP+LevelUp; Endurance/Morale: +0.05/h, max 0.5).</summary>
        public static void SimulateTrainingProgress(Worker worker, decimal trainingHours, decimal trainingMultiplier)
        {
            switch (worker.ActiveTrainingType)
            {
                case TrainingType.Efficiency:
                    decimal xpGain = worker.TrainingXpPerHour * trainingHours * worker.Personality.GetXpMultiplier() * trainingMultiplier;
                    decimal trainAcc = worker.TrainingXpAccumulator;
                    SimulateXpGain(worker, xpGain, ref trainAcc);
                    worker.TrainingXpAccumulator = trainAcc;
                    break;

                case TrainingType.Endurance:
                    decimal endGain = 0.05m * trainingHours * trainingMultiplier;
                    worker.EnduranceBonus = Math.Min(0.5m, worker.EnduranceBonus + endGain);
                    break;

                case TrainingType.Morale:
                    decimal morGain = 0.05m * trainingHours * trainingMultiplier;
                    worker.MoraleBonus = Math.Min(0.5m, worker.MoraleBonus + morGain);
                    break;
            }
        }

        /// <summary>Gemeinsame XP-Verarbeitung: Akkumulator, XP-Zuweisung, Level-Ups (Efficiency-Steigerung).</summary>
        public static void SimulateXpGain(Worker worker, decimal xpGain, ref decimal accumulator)
        {
            accumulator += xpGain;
            if (accumulator >= 1m)
            {
                int wholeXp = (int)accumulator;
                worker.ExperienceXp += wholeXp;
                accumulator -= wholeXp;
            }

            while (worker.ExperienceXp >= worker.XpForNextLevel && worker.ExperienceLevel < 10)
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
