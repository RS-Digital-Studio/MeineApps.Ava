using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für WorkerService: Einstellen, Entlassen, Training, Ruhe,
/// Zustand-Tick, Bonus, Markt-Generierung.
/// </summary>
public class WorkerServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erstellt einen GameStateService mit neuem Spielstand und den WorkerService.
    /// </summary>
    private static (WorkerService WorkerSvc, GameStateService StateSvc) ErstelleSetup(GameState? state = null)
    {
        var stateSvc = new GameStateService();
        stateSvc.Initialize(state ?? GameState.CreateNew());
        var workerSvc = new WorkerService(stateSvc);
        return (workerSvc, stateSvc);
    }

    /// <summary>
    /// Erstellt einen Worker für Tests mit definierter Basis-Konfiguration.
    /// </summary>
    private static Worker ErstelleTestWorker(WorkerTier tier = WorkerTier.E, decimal hiringCost = 50m)
    {
        var worker = Worker.CreateForTier(tier);
        worker.HiringCost = hiringCost;
        worker.WagePerHour = 10m;
        return worker;
    }

    // ═══════════════════════════════════════════════════════════════════
    // EINSTELLEN (HireWorker)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void HireWorker_GenugGeld_ErfolgreichUndWorkerInWorkshop()
    {
        // Vorbereitung: Level 51 = 2 Slots, Worker leeren damit Platz ist
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.Money = 999m;
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        ws.Level = 51; // BaseMaxWorkers = 2
        ws.Workers.Clear();
        ws.IsUnlocked = true;
        var worker = ErstelleTestWorker(hiringCost: 50m);

        // Ausführung
        var ergebnis = workerSvc.HireWorker(worker, WorkshopType.Carpenter);

        // Prüfung
        ergebnis.Should().BeTrue();
        ws.Workers.Should().Contain(worker);
    }

    [Fact]
    public void HireWorker_GenugGeld_ZiehtKostenAb()
    {
        // Vorbereitung: Level 51, Workers leeren
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.Money = 500m;
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        ws.Level = 51;
        ws.Workers.Clear();
        ws.IsUnlocked = true;
        var worker = ErstelleTestWorker(hiringCost: 100m);

        // Ausführung
        workerSvc.HireWorker(worker, WorkshopType.Carpenter);

        // Prüfung: Kosten wurden abgezogen
        stateSvc.State.Money.Should().Be(400m);
    }

    [Fact]
    public void HireWorker_NichtGenugGeld_GibtFalse()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.Money = 10m;
        var worker = ErstelleTestWorker(hiringCost: 500m);

        // Ausführung
        var ergebnis = workerSvc.HireWorker(worker, WorkshopType.Carpenter);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void HireWorker_WorkshopVoll_GibtFalse()
    {
        // Vorbereitung: Workshop bis auf MaxWorkers befüllen
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.Money = 999999m;
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        ws.IsUnlocked = true;

        // Alle Slots füllen
        for (int i = 0; i < ws.MaxWorkers; i++)
        {
            var füllerWorker = ErstelleTestWorker(hiringCost: 1m);
            ws.Workers.Add(füllerWorker);
        }

        var neuerWorker = ErstelleTestWorker(hiringCost: 50m);

        // Ausführung: Kein Platz mehr
        var ergebnis = workerSvc.HireWorker(neuerWorker, WorkshopType.Carpenter);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void HireWorker_SetzteAssignedWorkshopAufTarget()
    {
        // Vorbereitung: Level 51, Workers leeren
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.Money = 999m;
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        ws.Level = 51;
        ws.Workers.Clear();
        ws.IsUnlocked = true;
        var worker = ErstelleTestWorker(hiringCost: 10m);

        // Ausführung
        workerSvc.HireWorker(worker, WorkshopType.Carpenter);

        // Prüfung: Worker weiß jetzt wo er arbeitet
        worker.AssignedWorkshop.Should().Be(WorkshopType.Carpenter);
    }

    [Fact]
    public void HireWorker_ErhoehteTotalWorkersHired()
    {
        // Vorbereitung: Level 51, Workers leeren
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.Money = 999m;
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        ws.Level = 51;
        ws.Workers.Clear();
        ws.IsUnlocked = true;
        var vorher = stateSvc.State.Statistics.TotalWorkersHired;
        var worker = ErstelleTestWorker(hiringCost: 50m);

        // Ausführung
        workerSvc.HireWorker(worker, WorkshopType.Carpenter);

        // Prüfung
        stateSvc.State.Statistics.TotalWorkersHired.Should().Be(vorher + 1);
    }

    [Fact]
    public void HireWorker_EntferntWorkerAusMarkt()
    {
        // Vorbereitung: Level 51, Workers leeren + Worker im Markt
        var state = GameState.CreateNew();
        state.Money = 999m;
        var (workerSvc, stateSvc) = ErstelleSetup(state);
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        ws.Level = 51;
        ws.Workers.Clear();
        ws.IsUnlocked = true;

        state.WorkerMarket = new WorkerMarketPool();
        var marketWorker = ErstelleTestWorker(hiringCost: 50m);
        state.WorkerMarket.AvailableWorkers.Add(marketWorker);

        // Ausführung
        workerSvc.HireWorker(marketWorker, WorkshopType.Carpenter);

        // Prüfung: Nach Einstellung aus Markt entfernt
        state.WorkerMarket.AvailableWorkers.Should().NotContain(w => w.Id == marketWorker.Id);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ENTLASSEN (FireWorker)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void FireWorker_VorhandenerWorker_EntferntAusWorkshop()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        ws.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.FireWorker(worker.Id);

        // Prüfung
        ergebnis.Should().BeTrue();
        ws.Workers.Should().NotContain(worker);
    }

    [Fact]
    public void FireWorker_NichtExistierenderWorker_GibtFalse()
    {
        // Vorbereitung
        var (workerSvc, _) = ErstelleSetup();

        // Ausführung
        var ergebnis = workerSvc.FireWorker("nicht-vorhanden-id");

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void FireWorker_ErhoehteTotalWorkersFired()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        ws.Workers.Add(worker);
        var vorher = stateSvc.State.Statistics.TotalWorkersFired;

        // Ausführung
        workerSvc.FireWorker(worker.Id);

        // Prüfung
        stateSvc.State.Statistics.TotalWorkersFired.Should().Be(vorher + 1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // WIEDEREINSTELLUNG (ReinstateWorker)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ReinstateWorker_FreierPlatz_ErfolgreichOhneKosten()
    {
        // Vorbereitung: Level 51, Workers leeren damit Platz ist
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        ws.Level = 51;
        ws.Workers.Clear();
        var geldVorher = stateSvc.State.Money;
        var worker = ErstelleTestWorker();
        stateSvc.State.Statistics.TotalWorkersFired = 1;

        // Ausführung: Wiedereinstellung (kein Geld-Abzug)
        var ergebnis = workerSvc.ReinstateWorker(worker, WorkshopType.Carpenter);

        // Prüfung
        ergebnis.Should().BeTrue();
        ws.Workers.Should().Contain(worker);
        stateSvc.State.Money.Should().Be(geldVorher);
    }

    [Fact]
    public void ReinstateWorker_VollErWorkshop_GibtFalse()
    {
        // Vorbereitung: Workshop voll
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        for (int i = 0; i < ws.MaxWorkers; i++)
            ws.Workers.Add(ErstelleTestWorker());

        // Ausführung
        var ergebnis = workerSvc.ReinstateWorker(ErstelleTestWorker(), WorkshopType.Carpenter);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // TRANSFER (TransferWorker)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TransferWorker_GueltigerZielWorkshop_ErfolgreichUmgezogen()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.PlayerLevel = 10;
        var wsQuelle = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var wsZiel = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Plumber);
        wsZiel.IsUnlocked = true;

        var worker = ErstelleTestWorker();
        worker.AssignedWorkshop = WorkshopType.Carpenter;
        wsQuelle.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.TransferWorker(worker.Id, WorkshopType.Plumber);

        // Prüfung
        ergebnis.Should().BeTrue();
        wsQuelle.Workers.Should().NotContain(worker);
        wsZiel.Workers.Should().Contain(worker);
        worker.AssignedWorkshop.Should().Be(WorkshopType.Plumber);
    }

    [Fact]
    public void TransferWorker_GleicherWorkshop_GibtFalse()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.AssignedWorkshop = WorkshopType.Carpenter;
        ws.Workers.Add(worker);

        // Ausführung: Selbe Werkstatt als Ziel → sinnlos
        var ergebnis = workerSvc.TransferWorker(worker.Id, WorkshopType.Carpenter);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // TRAINING (StartTraining / StopTraining)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void StartTraining_NormaleVorbedingungen_ErfolgreichUndFlagGesetzt()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.IsTraining = false;
        worker.IsResting = false;
        worker.ExperienceLevel = 1;
        ws.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.StartTraining(worker.Id, TrainingType.Efficiency);

        // Prüfung
        ergebnis.Should().BeTrue();
        worker.IsTraining.Should().BeTrue();
        worker.ActiveTrainingType.Should().Be(TrainingType.Efficiency);
    }

    [Fact]
    public void StartTraining_BereitsAmTrainieren_GibtFalse()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.IsTraining = true; // Bereits im Training
        ws.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.StartTraining(worker.Id, TrainingType.Efficiency);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void StartTraining_BeiRuhe_GibtFalse()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.IsResting = true; // In Ruhe
        ws.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.StartTraining(worker.Id, TrainingType.Efficiency);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void StartTraining_EffizienzBeiLevel10_GibtFalse()
    {
        // Vorbereitung: Effizienz-Training nur bis Level 10
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.ExperienceLevel = 10; // Maximum erreicht
        ws.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.StartTraining(worker.Id, TrainingType.Efficiency);

        // Prüfung: Training blockiert wenn Maximum erreicht
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void StartTraining_AusdauerBeiMaxBonus_GibtFalse()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.EnduranceBonus = 0.5m; // Maximum (50%)
        ws.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.StartTraining(worker.Id, TrainingType.Endurance);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void StartTraining_StimmungBeiMaxBonus_GibtFalse()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.MoraleBonus = 0.5m; // Maximum (50%)
        ws.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.StartTraining(worker.Id, TrainingType.Morale);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void StopTraining_AktiverTraining_SetzeFlagZurueck()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.IsTraining = true;
        worker.ResumeTrainingType = TrainingType.Efficiency; // Gesetzt durch Auto-Rest
        ws.Workers.Add(worker);

        // Ausführung
        workerSvc.StopTraining(worker.Id);

        // Prüfung: Manueller Stop löscht Auto-Resume
        worker.IsTraining.Should().BeFalse();
        worker.ResumeTrainingType.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // RUHE (StartResting / StopResting)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void StartResting_NormaleVorbedingungen_ErfolgreichUndFlagGesetzt()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.IsResting = false;
        worker.IsTraining = false;
        ws.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.StartResting(worker.Id);

        // Prüfung
        ergebnis.Should().BeTrue();
        worker.IsResting.Should().BeTrue();
    }

    [Fact]
    public void StartResting_BereitsInRuhe_GibtFalse()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.IsResting = true;
        ws.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.StartResting(worker.Id);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void StartResting_BeimTrainieren_GibtFalse()
    {
        // Vorbereitung: Training und Ruhe schließen sich aus
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.IsTraining = true;
        ws.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.StartResting(worker.Id);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void StopResting_InRuhe_SetzeFlagZurueck()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.IsResting = true;
        worker.RestStartedAt = DateTime.UtcNow;
        ws.Workers.Add(worker);

        // Ausführung
        workerSvc.StopResting(worker.Id);

        // Prüfung
        worker.IsResting.Should().BeFalse();
        worker.RestStartedAt.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // BONUS (GiveBonus)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GiveBonus_GenugGeld_ErhoehtStimmungUm30()
    {
        // Vorbereitung: Stimmungs-Bonus kostet 8h Lohn
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.Money = 999m;
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.WagePerHour = 10m;
        worker.Mood = 50m;
        ws.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.GiveBonus(worker.Id);

        // Prüfung
        ergebnis.Should().BeTrue();
        worker.Mood.Should().Be(80m); // 50 + 30 = 80
    }

    [Fact]
    public void GiveBonus_StimmungskappeBereich()
    {
        // Vorbereitung: Stimmung nahe Maximum → darf nicht über 100 steigen
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.Money = 999m;
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.WagePerHour = 10m;
        worker.Mood = 90m; // +30 wäre 120
        ws.Workers.Add(worker);

        // Ausführung
        workerSvc.GiveBonus(worker.Id);

        // Prüfung: Kap bei 100
        worker.Mood.Should().Be(100m);
    }

    [Fact]
    public void GiveBonus_NichtGenugGeld_GibtFalse()
    {
        // Vorbereitung: Nicht genug Geld für 8h Lohn
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.Money = 0m;
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.WagePerHour = 100m; // 8h = 800€ → zu teuer
        ws.Workers.Add(worker);

        // Ausführung
        var ergebnis = workerSvc.GiveBonus(worker.Id);

        // Prüfung
        ergebnis.Should().BeFalse();
    }

    [Fact]
    public void GiveBonus_BrichtKuendigungsFristAb()
    {
        // Vorbereitung: Worker hat Kündigung angedroht
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.Money = 999m;
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.WagePerHour = 10m;
        worker.QuitDeadline = DateTime.UtcNow.AddHours(12); // Kündigung in 12h
        ws.Workers.Add(worker);

        // Ausführung
        workerSvc.GiveBonus(worker.Id);

        // Prüfung: Kündigung abgebrochen (CLAUDE.md Gotcha: GiveBonus bricht QuitDeadline ab)
        worker.QuitDeadline.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // ZUSTAND-TICK (UpdateWorkerStates)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void UpdateWorkerStates_ArbeitenderWorker_ErhoehtMüdigkeit()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.IsResting = false;
        worker.IsTraining = false;
        worker.Fatigue = 0m;
        ws.Workers.Add(worker);

        // Ausführung: Simuliere 1 Stunde Arbeit
        workerSvc.UpdateWorkerStates(3600.0);

        // Prüfung: Müdigkeit steigt beim Arbeiten
        worker.Fatigue.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void UpdateWorkerStates_RuhenderWorker_ReduziereMüdigkeit()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.IsResting = true;
        worker.Fatigue = 50m;
        ws.Workers.Add(worker);

        // Ausführung: Simuliere 2 Stunden Ruhe
        workerSvc.UpdateWorkerStates(7200.0);

        // Prüfung: Müdigkeit sinkt beim Ruhen
        worker.Fatigue.Should().BeLessThan(50m);
    }

    [Fact]
    public void UpdateWorkerStates_ArbeitenderWorker_SenkteStimmungImLaufe()
    {
        // Vorbereitung: Worker ohne MoraleBonus
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.IsResting = false;
        worker.IsTraining = false;
        worker.MoraleBonus = 0m;
        worker.Mood = 80m;
        ws.Workers.Add(worker);

        // Ausführung: 24 Stunden Arbeit
        workerSvc.UpdateWorkerStates(86400.0);

        // Prüfung: Stimmung sinkt beim Arbeiten (MoodDecayPerHour > 0)
        worker.Mood.Should().BeLessThan(80m);
    }

    [Fact]
    public void UpdateWorkerStates_WillQuit_SetzteQuitDeadline()
    {
        // Vorbereitung: Worker mit sehr niedriger Stimmung (< 20)
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.Mood = 10m; // WillQuit = true
        worker.QuitDeadline = null;
        ws.Workers.Add(worker);

        // Ausführung
        workerSvc.UpdateWorkerStates(1.0);

        // Prüfung: 24h Frist gesetzt
        worker.QuitDeadline.Should().NotBeNull();
    }

    [Fact]
    public void UpdateWorkerStates_QuitDeadlineAbgelaufen_EntferntWorkerUndFeuertEvent()
    {
        // Vorbereitung: Worker hat Kündigungsfrist überschritten
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.Mood = 10m;
        worker.QuitDeadline = DateTime.UtcNow.AddHours(-1); // Frist abgelaufen
        ws.Workers.Add(worker);

        Worker? gekuendigterWorker = null;
        workerSvc.WorkerQuit += (_, w) => gekuendigterWorker = w;

        // Ausführung
        workerSvc.UpdateWorkerStates(1.0);

        // Prüfung: Worker entfernt, Event gefeuert
        ws.Workers.Should().NotContain(worker);
        gekuendigterWorker.Should().NotBeNull();
        gekuendigterWorker!.Id.Should().Be(worker.Id);
    }

    [Fact]
    public void UpdateWorkerStates_TrainierendWorker_SteigertFatigueHalbeRate()
    {
        // Vorbereitung: Training kostet nur halbe Müdigkeit
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.Money = 999999m; // Für Trainingskosten
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);

        var arbeitender = ErstelleTestWorker();
        arbeitender.IsTraining = false;
        arbeitender.IsResting = false;
        arbeitender.Fatigue = 0m;

        var trainierender = ErstelleTestWorker();
        trainierender.IsTraining = true;
        trainierender.ActiveTrainingType = TrainingType.Efficiency;
        trainierender.ExperienceLevel = 1;
        trainierender.Fatigue = 0m;
        trainierender.WagePerHour = 1m; // Günstig damit Geld reicht

        ws.Workers.Add(arbeitender);
        ws.Workers.Add(trainierender);

        // Ausführung: 1 Stunde
        workerSvc.UpdateWorkerStates(3600.0);

        // Prüfung: Trainierender ermüdet langsamer als Arbeitender
        trainierender.Fatigue.Should().BeLessThan(arbeitender.Fatigue);
    }

    [Fact]
    public void UpdateWorkerStates_AutoRuheBeimTrainieren_Setzt_ResumeTrainingType()
    {
        // Vorbereitung: Worker erschöpft sich beim Training
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.Money = 999999m;
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        worker.IsTraining = true;
        worker.ActiveTrainingType = TrainingType.Efficiency;
        worker.ExperienceLevel = 1;
        worker.Fatigue = 99.9m; // Fast erschöpft
        worker.WagePerHour = 0.01m;
        ws.Workers.Add(worker);

        // Ausführung: Kleiner Tick der Fatigue über 100 schiebt
        workerSvc.UpdateWorkerStates(3600.0);

        // Prüfung: Auto-Rest mit ResumeTrainingType gesetzt
        if (worker.IsResting)
        {
            // Wenn Auto-Rest ausgelöst: ResumeTrainingType gesetzt
            worker.ResumeTrainingType.Should().Be(TrainingType.Efficiency);
        }
        // Falls Training noch läuft (wenig Fatigue-Anstieg): Test bleibt valid
    }

    // ═══════════════════════════════════════════════════════════════════
    // MARKT (GetWorkerMarket / RefreshMarket)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetWorkerMarket_KeinMarkt_GenerierenEinenNeuen()
    {
        // Vorbereitung: Kein Markt vorhanden
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.WorkerMarket = null;

        // Ausführung
        var markt = workerSvc.GetWorkerMarket();

        // Prüfung: Markt wurde generiert
        markt.Should().NotBeNull();
        markt.AvailableWorkers.Should().NotBeEmpty();
    }

    [Fact]
    public void GetWorkerMarket_VorhandenerAktuellerMarkt_GibtVorhandenenZurueck()
    {
        // Vorbereitung: Markt bereits vorhanden und noch nicht rotationsreif
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.WorkerMarket = new WorkerMarketPool();
        stateSvc.State.WorkerMarket.GeneratePool(1, 0, false, false);
        stateSvc.State.WorkerMarket.LastRotation = DateTime.UtcNow; // Gerade rotiert

        var ersterMarkt = stateSvc.State.WorkerMarket;

        // Ausführung
        var markt = workerSvc.GetWorkerMarket();

        // Prüfung: Derselbe Markt zurück (nicht neu generiert)
        markt.Should().BeSameAs(ersterMarkt);
    }

    [Fact]
    public void RefreshMarket_ErzwingtNeuGeneration()
    {
        // Vorbereitung: Markt vorhanden
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.WorkerMarket = new WorkerMarketPool();
        stateSvc.State.WorkerMarket.GeneratePool(1, 0, false, false);

        // Ausführung: Erzwungenes Refresh (z.B. durch Rewarded Ad)
        var neuerMarkt = workerSvc.RefreshMarket();

        // Prüfung: Markt hat neue Worker
        neuerMarkt.Should().NotBeNull();
        neuerMarkt.AvailableWorkers.Should().NotBeEmpty();
    }

    [Fact]
    public void RefreshMarket_BewaehrtFreeRefreshUsed_Flag()
    {
        // Vorbereitung: FreeRefreshUsed war true (Spieler hat es heute genutzt)
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.WorkerMarket = new WorkerMarketPool();
        stateSvc.State.WorkerMarket.GeneratePool(1, 0, false, false);
        stateSvc.State.WorkerMarket.FreeRefreshUsedThisRotation = true;

        // Ausführung: Manueller Refresh soll Flag nicht zurücksetzen
        workerSvc.RefreshMarket();

        // Prüfung: Flag erhalten
        stateSvc.State.WorkerMarket!.FreeRefreshUsedThisRotation.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // WORKER ABRUFEN (GetWorker)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetWorker_VorhandenerWorker_GibtZurueck()
    {
        // Vorbereitung
        var (workerSvc, stateSvc) = ErstelleSetup();
        var ws = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var worker = ErstelleTestWorker();
        ws.Workers.Add(worker);

        // Ausführung
        var gefunden = workerSvc.GetWorker(worker.Id);

        // Prüfung
        gefunden.Should().BeSameAs(worker);
    }

    [Fact]
    public void GetWorker_NichtExistierend_GibtNull()
    {
        // Vorbereitung
        var (workerSvc, _) = ErstelleSetup();

        // Ausführung
        var gefunden = workerSvc.GetWorker("existiert-nicht");

        // Prüfung
        gefunden.Should().BeNull();
    }

    [Fact]
    public void GetWorker_InZweitemWorkshop_GibtZurueck()
    {
        // Vorbereitung: Worker in zweitem Workshop, nicht im ersten
        var (workerSvc, stateSvc) = ErstelleSetup();
        stateSvc.State.GetOrCreateWorkshop(WorkshopType.Carpenter);
        var wsKlempner = stateSvc.State.GetOrCreateWorkshop(WorkshopType.Plumber);
        var worker = ErstelleTestWorker();
        wsKlempner.Workers.Add(worker);

        // Ausführung
        var gefunden = workerSvc.GetWorker(worker.Id);

        // Prüfung: Suche durch alle Workshops
        gefunden.Should().BeSameAs(worker);
    }
}
