using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Models.Events;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Services;

/// <summary>
/// Berechnet Offline-Einnahmen mit denselben Modifikatoren wie der GameLoop:
/// Research-Effizienz, Prestige-Shop-Income, Meisterwerkzeuge, Events, Boosts, Saison-Multiplikator.
/// Kosten werden ebenfalls berücksichtigt (Research/Prestige-Shop CostReduction).
/// Worker-States werden vollständig simuliert (Fatigue, Mood, Training-Fortschritt, XP, Level-Ups).
/// </summary>
public sealed class OfflineProgressService : IOfflineProgressService
{
    private readonly IGameStateService _gameStateService;
    private readonly IEventService? _eventService;
    private readonly IResearchService? _researchService;
    private readonly IPrestigeService? _prestigeService;

    public event EventHandler<OfflineEarningsEventArgs>? OfflineEarningsCalculated;

    public OfflineProgressService(
        IGameStateService gameStateService,
        IEventService? eventService = null,
        IResearchService? researchService = null,
        IPrestigeService? prestigeService = null)
    {
        _gameStateService = gameStateService;
        _eventService = eventService;
        _researchService = researchService;
        _prestigeService = prestigeService;
    }

    public decimal CalculateOfflineProgress()
    {
        var state = _gameStateService.State;

        // Offline-Dauer berechnen
        var offlineDuration = GetOfflineDuration();
        if (offlineDuration.TotalSeconds < 60) // Unter 1 Minute, keine Belohnung
        {
            return 0;
        }

        // Offline-Dauer begrenzen
        var maxDuration = GetMaxOfflineDuration();
        bool wasCapped = offlineDuration > maxDuration;
        var effectiveDuration = wasCapped ? maxDuration : offlineDuration;

        // === Research-Effekte laden + Level-Resistenz-Bonus auf Workshops setzen ===
        var researchEffects = _researchService?.GetTotalEffects();
        decimal levelResistance = Math.Min(researchEffects?.LevelResistanceBonus ?? 0m, 0.50m);
        foreach (var ws in state.Workshops)
            ws.LevelResistanceBonus = levelResistance;

        // === Brutto-Einkommen berechnen (identisch mit GameLoopService) ===
        decimal grossIncome = state.TotalIncomePerSecond;

        // Prestige-Shop Income-Boni (pp_income_10/25/50)
        decimal shopIncomeBonus = GetPrestigeIncomeBonus(state);
        if (shopIncomeBonus > 0)
            grossIncome *= (1m + shopIncomeBonus);
        if (researchEffects != null && researchEffects.EfficiencyBonus > 0)
            grossIncome *= (1m + Math.Min(researchEffects.EfficiencyBonus, 0.50m));

        // Event-Multiplikatoren (IncomeMultiplier enthält bereits saisonalen Multiplikator)
        var eventEffects = _eventService?.GetCurrentEffects();
        if (eventEffects != null)
            grossIncome *= eventEffects.IncomeMultiplier;

        // TaxAudit: 10% Steuer auf Einkommen (dauerhaft während Event)
        if (eventEffects?.SpecialEffect == "tax_10_percent")
            grossIncome *= 0.90m;

        // Meisterwerkzeuge: Passiver Einkommens-Bonus
        decimal masterToolBonus = MasterTool.GetTotalIncomeBonus(state.CollectedMasterTools);
        if (masterToolBonus > 0)
            grossIncome *= (1m + masterToolBonus);

        // Gilden-Bonus: +1% pro Gilden-Level, max +20%
        if (state.GuildMembership != null && state.GuildMembership.IncomeBonus > 0)
            grossIncome *= (1m + state.GuildMembership.IncomeBonus);

        // Gilden-Forschungs-Boni: Einkommen + Effizienz (identisch mit GameLoopService)
        if (state.GuildMembership != null)
        {
            var gm = state.GuildMembership;

            // Einkommens-Bonus (+20% bei allen Forschungen)
            if (gm.ResearchIncomeBonus > 0)
                grossIncome *= (1m + gm.ResearchIncomeBonus);

            // Effizienz-Bonus (+5%) - stackt mit Research-Effizienz
            if (gm.ResearchEfficiencyBonus > 0)
                grossIncome *= (1m + gm.ResearchEfficiencyBonus);
        }

        // Soft-Cap: Diminishing Returns ab 10.0x (identisch mit GameLoopService)
        if (state.TotalIncomePerSecond > 0)
        {
            decimal effectiveMultiplier = grossIncome / state.TotalIncomePerSecond;
            if (effectiveMultiplier > 10.0m)
            {
                decimal excess = effectiveMultiplier - 10.0m;
                decimal softened = 10.0m + (decimal)Math.Log(1.0 + (double)excess, 2.0);
                grossIncome = state.TotalIncomePerSecond * softened;
            }
        }

        // === Kosten berechnen (wie GameLoop) ===
        decimal costs = state.TotalCostsPerSecond;

        decimal totalCostReduction = 0m;
        if (_prestigeService != null)
            totalCostReduction += _prestigeService.GetCostReduction();
        if (researchEffects != null)
            totalCostReduction += researchEffects.CostReduction + researchEffects.WageReduction;

        // Storage-Gebäude: Materialkosten-Reduktion
        var storage = state.GetBuilding(BuildingType.Storage);
        if (storage != null)
            totalCostReduction += storage.MaterialCostReduction * 0.5m;

        if (totalCostReduction > 0)
            costs *= (1m - Math.Min(totalCostReduction, 0.50m));

        // Event-Kosteneffekte (z.B. MaterialShortage)
        if (eventEffects != null)
            costs *= eventEffects.CostMultiplier;

        // Gilden-Forschungs-Boni: Kosten-Reduktion (multiplikativ, identisch mit GameLoopService)
        if (state.GuildMembership?.ResearchCostReduction > 0)
            costs *= (1m - Math.Min(state.GuildMembership.ResearchCostReduction, 0.50m));

        // Netto-Einkommen (mindestens 0 - offline kein Geld verlieren)
        decimal netPerSecond = Math.Max(0, grossIncome - costs);

        // Gestaffelte Offline-Earnings: 4 Stufen
        // Optimiert für Mobile-Rhythmus: 2x am Tag reinschauen = optimaler Ertrag.
        // 100% erste 2h, 40% bis 4h, 25% bis 8h, 10% danach
        decimal totalSeconds = (decimal)effectiveDuration.TotalSeconds;
        decimal first2h = Math.Min(totalSeconds, 7200m);                                    // 0-2h: 100%
        decimal next2h = Math.Min(Math.Max(totalSeconds - 7200m, 0m), 7200m);               // 2-4h: 40%
        decimal next4h = Math.Min(Math.Max(totalSeconds - 14400m, 0m), 14400m);             // 4-8h: 25%
        decimal remaining = Math.Max(totalSeconds - 28800m, 0m);                             // 8h+: 10%
        decimal earnings = netPerSecond * (first2h + next2h * 0.40m + next4h * 0.25m + remaining * 0.10m);

        // SpeedBoost/RushBoost: Anteilig für verbleibende Boost-Dauer anwenden
        earnings = ApplyBoostsProRata(state, earnings, effectiveDuration);

        // Worker-States simulieren: Mood-Decay, Fatigue, Training-Fortschritt, XP
        SimulateWorkerStatesOffline(state, effectiveDuration);
        _gameStateService.MarkDirty(); // Worker-State-Änderungen persistieren

        // Event feuern
        OfflineEarningsCalculated?.Invoke(this, new OfflineEarningsEventArgs(
            earnings,
            effectiveDuration,
            wasCapped));

        return earnings;
    }

    /// <summary>
    /// Berechnet anteilige SpeedBoost/RushBoost-Multiplikatoren für die Offline-Dauer.
    /// Boosts laufen zeitbasiert ab → nur die verbleibende Boost-Zeit wird multipliziert.
    /// Stacking ist multiplikativ (identisch mit GameLoopService): SpeedBoost 2x * RushBoost 2-3x = 4-6x.
    /// Saisonaler Multiplikator wird bereits über EventService.GetCurrentEffects() im Einkommen berücksichtigt.
    /// </summary>
    private decimal ApplyBoostsProRata(GameState state, decimal baseEarnings, TimeSpan effectiveDuration)
    {
        if (baseEarnings <= 0 || effectiveDuration.TotalSeconds <= 0)
            return baseEarnings;

        var lastPlayed = state.LastPlayedAt;
        decimal totalSeconds = (decimal)effectiveDuration.TotalSeconds;

        // SpeedBoost: 2x Multiplikator für verbleibende Boost-Zeit
        decimal speedBoostSeconds = 0m;
        if (state.SpeedBoostEndTime > lastPlayed)
        {
            var boostRemaining = state.SpeedBoostEndTime - lastPlayed;
            speedBoostSeconds = Math.Min((decimal)boostRemaining.TotalSeconds, totalSeconds);
        }

        // RushBoost: 2-3x Multiplikator (Prestige-Shop Rush-Verstärker)
        decimal rushBoostSeconds = 0m;
        decimal rushMultiplier = 2m;
        if (state.RushBoostEndTime > lastPlayed)
        {
            var boostRemaining = state.RushBoostEndTime - lastPlayed;
            rushBoostSeconds = Math.Min((decimal)boostRemaining.TotalSeconds, totalSeconds);
            rushMultiplier += GetPrestigeRushBonus(state);
        }

        // Multiplikatives Stacking (identisch mit GameLoopService):
        // Für jede Sekunde wird der Gesamt-Multiplikator berechnet.
        // Ohne Boost: 1x, nur Speed: 2x, nur Rush: 2-3x, beide: 4-6x.
        // Gewichteter Durchschnitt über die gesamte Offline-Dauer.
        decimal unboostedSeconds = totalSeconds;
        decimal weightedMultiplier = 0m;

        // 4 mögliche Zeitfenster: beide aktiv, nur Speed, nur Rush, keiner
        decimal bothSeconds = Math.Max(0m, Math.Min(speedBoostSeconds, rushBoostSeconds));
        decimal onlySpeedSeconds = Math.Max(0m, speedBoostSeconds - bothSeconds);
        decimal onlyRushSeconds = Math.Max(0m, rushBoostSeconds - bothSeconds);
        unboostedSeconds = totalSeconds - bothSeconds - onlySpeedSeconds - onlyRushSeconds;

        weightedMultiplier += bothSeconds * 2m * rushMultiplier;      // Speed * Rush (multiplikativ)
        weightedMultiplier += onlySpeedSeconds * 2m;                  // Nur Speed
        weightedMultiplier += onlyRushSeconds * rushMultiplier;       // Nur Rush
        weightedMultiplier += unboostedSeconds * 1m;                  // Kein Boost

        decimal averageMultiplier = weightedMultiplier / totalSeconds;
        return baseEarnings * averageMultiplier;
    }

    /// <summary>
    /// Berechnet Rush-Multiplikator-Bonus aus gekauften Prestige-Shop-Items.
    /// </summary>
    private static decimal GetPrestigeRushBonus(GameState state)
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

    public TimeSpan GetMaxOfflineDuration()
    {
        int maxHours = _gameStateService.State.MaxOfflineHours;
        return TimeSpan.FromHours(maxHours);
    }

    public TimeSpan GetOfflineDuration()
    {
        var lastPlayed = _gameStateService.State.LastPlayedAt;
        var now = DateTime.UtcNow;

        // Zeitmanipulations-Schutz: Wenn lastPlayed in der Zukunft liegt,
        // wurde die Systemuhr zurückgestellt → keine Offline-Einnahmen
        if (lastPlayed > now)
            return TimeSpan.Zero;

        return now - lastPlayed;
    }

    /// <summary>
    /// Berechnet Income-Multiplikator-Bonus aus gekauften Prestige-Shop-Items.
    /// </summary>
    private static decimal GetPrestigeIncomeBonus(GameState state)
    {
        var purchased = state.Prestige.PurchasedShopItems;
        var repeatableCounts = state.Prestige.RepeatableItemCounts;
        if (purchased.Count == 0 && repeatableCounts.Count == 0) return 0m;

        decimal bonus = 0m;
        foreach (var item in PrestigeShop.GetAllItems())
        {
            // Wiederholbare Items: Effekt * Kaufanzahl
            if (item.IsRepeatable)
            {
                if (repeatableCounts.TryGetValue(item.Id, out var count) && count > 0
                    && item.Effect.IncomeMultiplier > 0)
                    bonus += item.Effect.IncomeMultiplier * count;
                continue;
            }

            if (purchased.Contains(item.Id) && item.Effect.IncomeMultiplier > 0)
                bonus += item.Effect.IncomeMultiplier;
        }
        return bonus;
    }

    /// <summary>
    /// Simuliert Worker-Stimmung, Erschöpfung, Training und XP während Offline-Zeit.
    /// 2-Phasen-Architektur pro Worker:
    /// Phase 1: Aktivität simulieren (Training/Arbeit) → restHours berechnen
    /// Phase 2: Rest-Recovery für alle Worker die am Ende ruhen
    /// Berücksichtigt alle Modifikatoren identisch mit WorkerService/GameLoopService.
    /// </summary>
    private void SimulateWorkerStatesOffline(GameState state, TimeSpan offlineDuration)
    {
        decimal offlineHours = (decimal)offlineDuration.TotalHours;
        if (offlineHours <= 0) return;

        // Prestige-Shop MoodDecay-Reduktion
        decimal prestigeMoodReduction = 0m;
        if (_prestigeService != null)
            prestigeMoodReduction = _prestigeService.GetMoodDecayReduction();

        // Gilden-Forschung: Fatigue/Mood-Reduktion (guild_workforce_3: reduziert beides)
        decimal guildFatigueReduction = state.GuildMembership?.ResearchFatigueReduction ?? 0m;

        // Canteen-Gebäude: Passive Stimmungs-Erholung
        var canteen = state.GetBuilding(BuildingType.Canteen);
        decimal passiveMoodRecovery = canteen?.MoodRecoveryPerHour ?? 0m;

        // TrainingCenter + Gilden-Forschung: Training-Geschwindigkeit
        var trainingCenter = state.GetBuilding(BuildingType.TrainingCenter);
        decimal trainingSpeedMultiplier = trainingCenter?.TrainingSpeedMultiplier ?? 1m;
        if (state.GuildMembership?.ResearchTrainingSpeedBonus > 0)
            trainingSpeedMultiplier *= (1m + state.GuildMembership.ResearchTrainingSpeedBonus);

        // Vorlauf: Trainingskosten berechnen und bezahlbaren Anteil bestimmen
        // Verhindert Exploit (kostenloses Training bei 0 EUR, identisch mit WorkerService)
        decimal totalTrainingCosts = 0m;
        foreach (var ws in state.Workshops)
        {
            foreach (var worker in ws.Workers)
            {
                if (!worker.IsTraining) continue;
                totalTrainingCosts += EstimateTrainingCosts(worker, offlineHours);
            }
        }

        // Bezahlbarer Anteil: 0.0 (kein Geld) bis 1.0 (alles leistbar)
        decimal affordableFraction = 1m;
        if (totalTrainingCosts > 0)
        {
            decimal availableMoney = state.Money;
            if (availableMoney < totalTrainingCosts)
                affordableFraction = totalTrainingCosts > 0 ? Math.Max(0m, availableMoney / totalTrainingCosts) : 0m;

            // Kosten abziehen (maximal was vorhanden ist)
            decimal actualCosts = Math.Min(totalTrainingCosts, availableMoney);
            if (actualCosts > 0)
                _gameStateService.TrySpendMoney(actualCosts);
        }

        foreach (var ws in state.Workshops)
        {
            foreach (var worker in ws.Workers)
            {
                // Phase 1: Aktivität simulieren, restHours für anschließende Ruhe berechnen
                decimal restHours = 0m;

                if (worker.IsTraining)
                {
                    SimulateTrainingWorker(worker, offlineHours, trainingSpeedMultiplier, affordableFraction, out restHours,
                        guildFatigueReduction, prestigeMoodReduction, passiveMoodRecovery);
                }
                else if (worker.IsResting)
                {
                    // War bereits in Ruhe → volle Offline-Zeit zur Erholung
                    restHours = offlineHours;
                }
                else
                {
                    SimulateWorkingWorker(worker, offlineHours, guildFatigueReduction,
                        prestigeMoodReduction, passiveMoodRecovery, out restHours);
                }

                // Phase 2: Rest-Recovery für ALLE Worker die jetzt ruhen
                if (worker.IsResting && restHours > 0)
                    SimulateRestRecovery(worker, restHours, canteen);

                // QuitDeadline-Handling (identisch mit WorkerService.UpdateWorking)
                if (worker.WillQuit && worker.QuitDeadline == null)
                    worker.QuitDeadline = DateTime.UtcNow.AddHours(24);
                else if (!worker.WillQuit)
                    worker.QuitDeadline = null;
            }
        }
    }

    /// <summary>
    /// Prüft ob das Training eines Workers abgeschlossen ist (Maximum erreicht).
    /// Gemeinsame Methode um dreifache Duplikation zu vermeiden.
    /// </summary>
    private static bool IsTrainingComplete(Worker worker) =>
        (worker.ActiveTrainingType == TrainingType.Endurance && worker.EnduranceBonus >= 0.5m) ||
        (worker.ActiveTrainingType == TrainingType.Morale && worker.MoraleBonus >= 0.5m) ||
        (worker.ActiveTrainingType == TrainingType.Efficiency && worker.ExperienceLevel >= 10);

    /// <summary>
    /// Schätzt Trainingskosten für einen Worker ohne Fortschritt zu simulieren.
    /// Wird im Vorlauf verwendet um den bezahlbaren Anteil zu berechnen.
    /// </summary>
    private static decimal EstimateTrainingCosts(Worker worker, decimal offlineHours)
    {
        // Bereits abgeschlossenes Training kostet nichts
        if (IsTrainingComplete(worker))
            return 0m;

        decimal trainingHours = CalculateTrainingHours(worker, offlineHours);
        return worker.TrainingCostPerHour * trainingHours;
    }

    /// <summary>
    /// Berechnet wie viele Stunden ein Worker maximal trainieren kann (basierend auf Fatigue).
    /// </summary>
    private static decimal CalculateTrainingHours(Worker worker, decimal offlineHours)
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

        // fatigueRate = 0 → Worker ermüdet nie
        return offlineHours;
    }

    /// <summary>
    /// Simuliert einen trainierenden Worker: Fatigue, Fortschritt (proportional zum Budget).
    /// Training wird NUR beendet wenn 100% Fatigue erreicht wird oder Training abgeschlossen ist.
    /// Bei Geldmangel (affordableFraction=0) wechselt der Worker zum Arbeiten (wie WorkerService online).
    /// affordableFraction (0-1): Anteil der Trainingszeit der bezahlt werden kann.
    /// </summary>
    private static void SimulateTrainingWorker(Worker worker, decimal offlineHours,
        decimal trainingSpeedMultiplier, decimal affordableFraction, out decimal restHours,
        decimal guildFatigueReduction, decimal prestigeMoodReduction, decimal passiveMoodRecovery)
    {
        restHours = 0m;

        // Prüfen ob Training bereits abgeschlossen
        if (IsTrainingComplete(worker))
        {
            // Training war schon fertig → Worker wechselt in Ruhe
            worker.IsTraining = false;
            worker.TrainingStartedAt = null;
            worker.IsResting = true;
            worker.RestStartedAt = DateTime.UtcNow.AddHours(-(double)offlineHours);
            restHours = offlineHours;
            return;
        }

        // Bei Geldmangel: Training abbrechen und zum Arbeiten wechseln
        // Bewusste Abweichung vom Online-Verhalten (dort bleibt Worker idle):
        // Offline kann der Spieler nicht eingreifen → Worker soll produktiv bleiben
        if (affordableFraction <= 0)
        {
            worker.IsTraining = false;
            worker.TrainingStartedAt = null;
            // Gesamte Offline-Zeit als Arbeitszeit simulieren
            SimulateWorkingWorker(worker, offlineHours, guildFatigueReduction,
                prestigeMoodReduction, passiveMoodRecovery, out restHours);
            return;
        }

        // Dynamische Trainingsdauer basierend auf Fatigue
        decimal maxTrainingHours = CalculateTrainingHours(worker, offlineHours);

        // Bezahlbare Trainingsstunden (proportional zum Budget)
        decimal trainingHours = maxTrainingHours * affordableFraction;

        // Training-Fatigue für die tatsächlichen Trainingsstunden addieren
        var trainingFatigueRate = worker.FatiguePerHour * 0.5m;
        var equipFatReduction = worker.EquippedItem?.FatigueReduction ?? 0m;
        if (equipFatReduction > 0)
            trainingFatigueRate *= (1m - equipFatReduction);
        worker.Fatigue = Math.Min(100m, worker.Fatigue + trainingFatigueRate * trainingHours);

        // Fatigue-Check NACH Addition (Finding 2: Edge Case bei exakt 100%)
        bool reachedMaxFatigue = worker.Fatigue >= 100m;

        // Training-Fortschritt simulieren (nur für bezahlte Zeit)
        if (trainingHours > 0)
            SimulateTrainingProgress(worker, trainingHours, trainingSpeedMultiplier);

        // Training beenden wenn: 100% Fatigue oder Training abgeschlossen
        bool shouldStopTraining = reachedMaxFatigue || IsTrainingComplete(worker);

        if (shouldStopTraining)
        {
            worker.IsTraining = false;
            worker.TrainingStartedAt = null;
            decimal remainingHours = offlineHours - trainingHours;

            if (remainingHours > 0 && worker.Fatigue < 100m)
            {
                // Verbleibende Zeit als Arbeitszeit nutzen (statt zu verfallen)
                SimulateWorkingWorker(worker, remainingHours, guildFatigueReduction,
                    prestigeMoodReduction, passiveMoodRecovery, out restHours);
            }
            else
            {
                // Bei 100% Fatigue direkt in Ruhe
                worker.IsResting = true;
                restHours = remainingHours;
                if (restHours > 0)
                    worker.RestStartedAt = DateTime.UtcNow.AddHours(-(double)restHours);
                else
                    worker.RestStartedAt = DateTime.UtcNow;
            }
        }
        // Sonst: Worker trainiert weiter (kein State-Wechsel, wie online)
    }

    /// <summary>
    /// Simuliert einen arbeitenden Worker: Fatigue, Mood, passiver XP-Gewinn.
    /// Bei 100% Fatigue wechselt der Worker automatisch in Ruhe.
    /// </summary>
    private static void SimulateWorkingWorker(Worker worker, decimal offlineHours,
        decimal guildFatigueReduction, decimal prestigeMoodReduction,
        decimal passiveMoodRecovery, out decimal restHours)
    {
        restHours = 0m;

        // Fatigue-Rate berechnen (mit Gilden-Forschung + Ausrüstung)
        decimal fatigueRate = worker.FatiguePerHour;
        if (guildFatigueReduction > 0)
            fatigueRate *= (1m - guildFatigueReduction);
        var equipFatigueReduction = worker.EquippedItem?.FatigueReduction ?? 0m;
        if (equipFatigueReduction > 0)
            fatigueRate *= (1m - equipFatigueReduction);

        // Berechnen wie lange der Worker arbeitet bevor 100% Fatigue erreicht wird
        decimal workHours = offlineHours;
        decimal originalFatigue = worker.Fatigue;

        if (originalFatigue >= 100m)
        {
            // Bereits bei 100% → sofort ruhen
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
                // Worker erreicht 100% Fatigue vor Ende der Offline-Zeit → Rest für verbleibende Zeit
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
        // fatigueRate == 0: Worker ermüdet nie → arbeitet die ganze Offline-Zeit durch, kein Rest

        // Mood-Decay und XP nur für die tatsächliche Arbeitszeit anwenden
        if (workHours > 0)
        {
            // Mood-Decay (identisch mit WorkerService.UpdateWorking)
            decimal moodDecay = worker.MoodDecayPerHour;
            if (prestigeMoodReduction > 0)
                moodDecay *= (1m - prestigeMoodReduction);
            if (guildFatigueReduction > 0)
                moodDecay *= (1m - guildFatigueReduction);

            // Ausrüstungs-Bonus: MoodBonus reduziert Stimmungsabfall
            var equipMoodBonus = worker.EquippedItem?.MoodBonus ?? 0m;
            if (equipMoodBonus > 0)
                moodDecay *= (1m - equipMoodBonus);

            // Canteen-Effekt: Netto-Stimmungsänderung
            decimal netMoodChange = moodDecay - passiveMoodRecovery;
            if (netMoodChange > 0)
                worker.Mood = Math.Max(0m, worker.Mood - netMoodChange * workHours);
            else
                worker.Mood = Math.Min(100m, worker.Mood + Math.Abs(netMoodChange) * workHours);

            // Passiver XP-Gewinn beim Arbeiten (25% der Trainingsrate, identisch mit WorkerService)
            decimal workAcc = worker.WorkingXpAccumulator;
            SimulateXpGain(worker, worker.TrainingXpPerHour * 0.25m * workHours * worker.Personality.GetXpMultiplier(),
                ref workAcc);
            worker.WorkingXpAccumulator = workAcc;
        }
    }

    /// <summary>
    /// Simuliert Ruhephase: Fatigue-Erholung + Mood-Erholung (mit Canteen + Ausrüstung).
    /// </summary>
    private static void SimulateRestRecovery(Worker worker, decimal restHours, Building? canteen)
    {
        // Fatigue-Erholung während Ruhe (mit Canteen + Ausrüstung)
        decimal restMultiplier = 1m + (canteen?.RestTimeReduction ?? 0m);
        decimal restBase = worker.RestHoursNeeded > 0 ? worker.RestHoursNeeded : 4m;
        decimal fatigueRecovery = (100m / restBase) * restHours * restMultiplier;
        var equipRecoveryBoost = worker.EquippedItem?.FatigueReduction ?? 0m;
        if (equipRecoveryBoost > 0)
            fatigueRecovery *= (1m + equipRecoveryBoost);
        worker.Fatigue = Math.Max(0m, worker.Fatigue - fatigueRecovery);

        // Mood-Erholung während Ruhe (mit Canteen + Ausrüstung)
        decimal moodRecovery = 1m + (canteen?.MoodRecoveryPerHour ?? 0m);
        var equipMoodBonus = worker.EquippedItem?.MoodBonus ?? 0m;
        if (equipMoodBonus > 0)
            moodRecovery *= (1m + equipMoodBonus);
        worker.Mood = Math.Min(100m, worker.Mood + moodRecovery * restHours);

        // Automatisch Ruhe beenden wenn voll erholt
        if (worker.Fatigue <= 0m)
        {
            worker.IsResting = false;
            worker.RestStartedAt = null;
        }
    }

    /// <summary>
    /// Simuliert Training-Fortschritt offline (identisch mit WorkerService.UpdateTraining).
    /// Efficiency: XP + Level-Up. Endurance: +0.05/h (max 0.5). Morale: +0.05/h (max 0.5).
    /// </summary>
    private static void SimulateTrainingProgress(Worker worker, decimal trainingHours, decimal trainingMultiplier)
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

    /// <summary>
    /// Gemeinsame XP-Verarbeitung: Akkumulator, XP-Zuweisung, Level-Ups.
    /// Vermeidet Code-Duplikation zwischen Training-XP und Arbeits-XP.
    /// </summary>
    private static void SimulateXpGain(Worker worker, decimal xpGain, ref decimal accumulator)
    {
        accumulator += xpGain;
        if (accumulator >= 1m)
        {
            int wholeXp = (int)accumulator;
            worker.ExperienceXp += wholeXp;
            accumulator -= wholeXp;
        }

        // Level-Ups prüfen (mehrere möglich bei langer Offline-Zeit)
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
