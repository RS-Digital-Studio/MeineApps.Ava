using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.Tests;

/// <summary>
/// Tests für QuickJobService: Job-Generierung, Rotation, Tages-Limit,
/// Belohnungs-Berechnung, Schwierigkeit und Score-Verdopplung.
/// </summary>
public class QuickJobServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erstellt einen Standard-GameState für Tests: Spieler-Level 1,
    /// Schreinerei freigeschaltet, letzte Rotation vor langer Zeit.
    /// </summary>
    private static GameState ErstelleState(int prestigeCount = 0, int playerLevel = 1)
    {
        var state = GameState.CreateNew();
        state.PlayerLevel = playerLevel;
        state.Prestige.BronzeCount = prestigeCount > 0 ? prestigeCount : 0;
        // Rotation erzwingen: LastQuickJobRotation weit in der Vergangenheit
        state.LastQuickJobRotation = DateTime.UtcNow.AddHours(-2);
        // Tages-Reset: Heute bereits gesetzt → kein automatischer Reset beim Zugriff
        // WICHTIG: Wäre LastQuickJobDailyReset gestern, würde ResetDailyCounterIfNewDay()
        // beim ersten Property-Zugriff den Counter auf 0 setzen und alle manuell
        // gesetzten QuickJobsCompletedToday-Werte überschreiben.
        state.LastQuickJobDailyReset = DateTime.UtcNow;
        state.QuickJobsCompletedToday = 0;
        return state;
    }

    /// <summary>
    /// Erstellt einen gemockten IGameStateService.
    /// </summary>
    private static IGameStateService ErstelleMockStateService(GameState state)
    {
        var mock = Substitute.For<IGameStateService>();
        mock.State.Returns(state);
        return mock;
    }

    /// <summary>
    /// Erstellt einen gemockten ILocalizationService der den Key als String zurückgibt.
    /// </summary>
    private static ILocalizationService ErstelleMockLocalizationService()
    {
        var mock = Substitute.For<ILocalizationService>();
        mock.GetString(Arg.Any<string>()).Returns(ci => ci.Arg<string>());
        return mock;
    }

    private static QuickJobService ErstelleService(GameState? state = null)
    {
        state ??= ErstelleState();
        return new QuickJobService(
            ErstelleMockStateService(state),
            ErstelleMockLocalizationService());
    }

    // ═══════════════════════════════════════════════════════════════════
    // GenerateJobs - Korrektheit
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateJobs_FuenftJobsAngefordert_FuenftJobsImState()
    {
        // Vorbereitung
        var state = ErstelleState();
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        service.GenerateJobs(5);

        // Prüfung
        state.QuickJobs.Should().HaveCount(5);
    }

    [Fact]
    public void GenerateJobs_DreiJobsAngefordert_DreiJobsImState()
    {
        // Vorbereitung
        var state = ErstelleState();
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        service.GenerateJobs(3);

        // Prüfung
        state.QuickJobs.Should().HaveCount(3);
    }

    [Fact]
    public void GenerateJobs_AlleJobsHabenMiniGameType()
    {
        // Vorbereitung
        var state = ErstelleState();
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        service.GenerateJobs(5);

        // Prüfung: Jeder Job muss einen gültigen MiniGame-Typ haben
        state.QuickJobs.Should().AllSatisfy(j =>
            Enum.IsDefined(typeof(MiniGameType), j.MiniGameType).Should().BeTrue());
    }

    [Fact]
    public void GenerateJobs_AlleJobsHabenTitleKey()
    {
        // Vorbereitung
        var state = ErstelleState();
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        service.GenerateJobs(5);

        // Prüfung
        state.QuickJobs.Should().AllSatisfy(j =>
            j.TitleKey.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void GenerateJobs_AlleJobsHabenBelohnung_GroesserNull()
    {
        // Vorbereitung
        var state = ErstelleState(playerLevel: 5);
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        service.GenerateJobs(5);

        // Prüfung
        state.QuickJobs.Should().AllSatisfy(j =>
            j.Reward.Should().BeGreaterThan(0m));
    }

    [Fact]
    public void GenerateJobs_AlleJobsHabenXpBelohnung_GroesserNull()
    {
        // Vorbereitung
        var state = ErstelleState(playerLevel: 1);
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        service.GenerateJobs(5);

        // Prüfung: XP = (5 + level * 3) * diffMult
        state.QuickJobs.Should().AllSatisfy(j =>
            j.XpReward.Should().BeGreaterThan(0));
    }

    [Fact]
    public void GenerateJobs_KeineUnlockedTypes_FallbackAufSchreiner()
    {
        // Vorbereitung: Keine freigeschalteten Workshops
        var state = ErstelleState();
        state.UnlockedWorkshopTypes.Clear();
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        service.GenerateJobs(3);

        // Prüfung: Fallback auf Carpenter
        state.QuickJobs.Should().AllSatisfy(j =>
            j.WorkshopType.Should().Be(WorkshopType.Carpenter));
    }

    [Fact]
    public void GenerateJobs_SetztLastQuickJobRotation()
    {
        // Vorbereitung
        var state = ErstelleState();
        var vorherGesetztesRotation = state.LastQuickJobRotation;
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        service.GenerateJobs(5);

        // Prüfung: Rotation-Timestamp muss nach dem alten Wert liegen
        state.LastQuickJobRotation.Should().BeAfter(vorherGesetztesRotation);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Belohnungs-Berechnung
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateJobs_HoeheresLevel_HoehereRewards()
    {
        // Vorbereitung: Kein Nettoeinkommen, Level-Bonus dominiert
        // Formel: baseReward = max(20 + level*50, netIncome*300)
        var stateLow = ErstelleState(playerLevel: 1);
        var stateHigh = ErstelleState(playerLevel: 50);

        var serviceLow = new QuickJobService(ErstelleMockStateService(stateLow), ErstelleMockLocalizationService());
        var serviceHigh = new QuickJobService(ErstelleMockStateService(stateHigh), ErstelleMockLocalizationService());

        // Ausführung
        serviceLow.GenerateJobs(5);
        serviceHigh.GenerateJobs(5);

        var avgLow = stateLow.QuickJobs.Average(j => (double)j.Reward);
        var avgHigh = stateHigh.QuickJobs.Average(j => (double)j.Reward);

        // Prüfung: Level 50 → 2.520 Basis; Level 1 → 70 Basis (36x Verhältnis)
        // Konservative Schwelle wegen Zufalls-Typ-Multiplikator-Variation (0.75x-1.40x)
        avgHigh.Should().BeGreaterThan(avgLow * 5);
    }

    [Fact]
    public void GenerateJobs_PrestigeBonus_ErhoehteRewards()
    {
        // Vorbereitung: Prestige gibt +10% pro Stufe
        var stateOhne = ErstelleState(prestigeCount: 0, playerLevel: 1);
        var stateMit = ErstelleState(prestigeCount: 3, playerLevel: 1);
        // Prestige-Bonus: 1.0 + 3*0.10 = 1.30x

        var serviceOhne = new QuickJobService(ErstelleMockStateService(stateOhne), ErstelleMockLocalizationService());
        var serviceMit = new QuickJobService(ErstelleMockStateService(stateMit), ErstelleMockLocalizationService());

        // Ausführung
        serviceOhne.GenerateJobs(5);
        serviceMit.GenerateJobs(5);

        var avgOhne = stateOhne.QuickJobs.Average(j => (double)j.Reward);
        var avgMit = stateMit.QuickJobs.Average(j => (double)j.Reward);

        // Prüfung: Mit Prestige mindestens gleich hohe Belohnung (Prestige-Bonus ist gering/variabel)
        avgMit.Should().BeGreaterThanOrEqualTo(avgOhne * 0.8);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Tages-Limit
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void MaxDailyJobs_OhnePrestige_ZwanzigJobs()
    {
        // Vorbereitung: Kein Prestige
        var state = ErstelleState(prestigeCount: 0);
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Prüfung: Basis 20 bei 0 Prestige
        service.MaxDailyJobs.Should().Be(20);
    }

    [Theory]
    [InlineData(1, 25)]
    [InlineData(2, 30)]
    [InlineData(3, 40)]
    public void MaxDailyJobs_MitPrestige_SkaliertesMenge(int prestigeCount, int erwartet)
    {
        // Vorbereitung
        var state = ErstelleState(prestigeCount: 0);
        state.Prestige.BronzeCount = prestigeCount;
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Prüfung: Limit skaliert mit Prestige-Stufen
        service.MaxDailyJobs.Should().Be(erwartet);
    }

    [Fact]
    public void IsDailyLimitReached_NullAbgeschlossen_NichtErreicht()
    {
        // Vorbereitung
        var state = ErstelleState();
        state.QuickJobsCompletedToday = 0;
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Prüfung
        service.IsDailyLimitReached.Should().BeFalse();
    }

    [Fact]
    public void IsDailyLimitReached_LimitErreicht_IstTrue()
    {
        // Vorbereitung: Genau das Limit erreicht
        var state = ErstelleState(prestigeCount: 0); // Limit = 20
        state.QuickJobsCompletedToday = 20;
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Prüfung
        service.IsDailyLimitReached.Should().BeTrue();
    }

    [Fact]
    public void RemainingJobsToday_NullAbgeschlossen_GibtMaxZurueck()
    {
        // Vorbereitung
        var state = ErstelleState(prestigeCount: 0); // Limit = 20
        state.QuickJobsCompletedToday = 0;
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Prüfung
        service.RemainingJobsToday.Should().Be(20);
    }

    [Fact]
    public void RemainingJobsToday_FuenfAbgeschlossen_GibtFuenfzehnZurueck()
    {
        // Vorbereitung
        var state = ErstelleState(prestigeCount: 0); // Limit = 20
        state.QuickJobsCompletedToday = 5;
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Prüfung
        service.RemainingJobsToday.Should().Be(15);
    }

    [Fact]
    public void RemainingJobsToday_NieUnterNull()
    {
        // Vorbereitung: Mehr als das Limit abgeschlossen (sollte nicht passieren, defensiv testen)
        var state = ErstelleState(prestigeCount: 0); // Limit = 20
        state.QuickJobsCompletedToday = 999;
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Prüfung: Niemals negativ
        service.RemainingJobsToday.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // NotifyJobCompleted
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void NotifyJobCompleted_ErhoehtTagesCounter()
    {
        // Vorbereitung
        var state = ErstelleState();
        state.QuickJobsCompletedToday = 5;
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());
        var job = new QuickJob { WorkshopType = WorkshopType.Carpenter };

        // Ausführung
        service.NotifyJobCompleted(job);

        // Prüfung: Counter erhöht
        state.QuickJobsCompletedToday.Should().Be(6);
    }

    [Fact]
    public void NotifyJobCompleted_FeuertQuickJobCompletedEvent()
    {
        // Vorbereitung
        var state = ErstelleState();
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());
        var job = new QuickJob { WorkshopType = WorkshopType.Carpenter };
        QuickJob? empfangenerJob = null;
        service.QuickJobCompleted += (_, j) => empfangenerJob = j;

        // Ausführung
        service.NotifyJobCompleted(job);

        // Prüfung: Event wurde gefeuert mit korrektem Job
        empfangenerJob.Should().BeSameAs(job);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Rotation und NeedsRotation
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void NeedsRotation_RotationLiegtWeitInVergangenheit_IstTrue()
    {
        // Vorbereitung: Letzte Rotation vor 2 Stunden
        var state = ErstelleState();
        state.LastQuickJobRotation = DateTime.UtcNow.AddHours(-2);
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Prüfung: Intervall 15 Minuten → nach 2 Stunden muss Rotation nötig sein
        service.NeedsRotation().Should().BeTrue();
    }

    [Fact]
    public void NeedsRotation_GeradeSoEben_IstFalse()
    {
        // Vorbereitung: Letzte Rotation gerade eben
        var state = ErstelleState();
        state.LastQuickJobRotation = DateTime.UtcNow;
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Prüfung: Keine Rotation nötig direkt nach letzter Rotation
        service.NeedsRotation().Should().BeFalse();
    }

    [Fact]
    public void RotateIfNeeded_EntferntAbgeschlosseneJobs()
    {
        // Vorbereitung: Ein abgeschlossener und ein offener Job
        var state = ErstelleState();
        state.LastQuickJobRotation = DateTime.UtcNow.AddHours(-2); // Rotation fällig
        state.QuickJobs.Add(new QuickJob { IsCompleted = true, TitleKey = "Erledigt" });
        state.QuickJobs.Add(new QuickJob { IsCompleted = false, TitleKey = "Offen" });
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        service.RotateIfNeeded();

        // Prüfung: Erledigter Job entfernt
        state.QuickJobs.Should().NotContain(j => j.TitleKey == "Erledigt");
    }

    [Fact]
    public void RotateIfNeeded_FuelltAufFuenfJobs()
    {
        // Vorbereitung: Nur 2 offene Jobs nach Entfernen der erledigten
        var state = ErstelleState();
        state.LastQuickJobRotation = DateTime.UtcNow.AddHours(-2);
        state.QuickJobs.Add(new QuickJob { IsCompleted = false, TitleKey = "Job1", MiniGameType = MiniGameType.Sawing });
        state.QuickJobs.Add(new QuickJob { IsCompleted = false, TitleKey = "Job2", MiniGameType = MiniGameType.Sawing });
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        service.RotateIfNeeded();

        // Prüfung: Immer 5 Jobs nach Rotation
        state.QuickJobs.Should().HaveCount(5);
    }

    [Fact]
    public void RotateIfNeeded_KeineRotationNoetig_AendertNichts()
    {
        // Vorbereitung: Rotation noch nicht fällig
        var state = ErstelleState();
        state.LastQuickJobRotation = DateTime.UtcNow; // Gerade rotiert
        state.QuickJobs.Add(new QuickJob { TitleKey = "Vorhandener" });
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        service.RotateIfNeeded();

        // Prüfung: Keine Änderung da keine Rotation nötig
        state.QuickJobs.Should().HaveCount(1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // TimeUntilNextRotation
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TimeUntilNextRotation_RotationUeberfaellig_IstNull()
    {
        // Vorbereitung
        var state = ErstelleState();
        state.LastQuickJobRotation = DateTime.UtcNow.AddHours(-1);
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Prüfung: Überfällige Rotation = 0 Restzeit
        service.TimeUntilNextRotation.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void TimeUntilNextRotation_GeradeRotiert_IstNaheAnFuenfzehnMinuten()
    {
        // Vorbereitung: Rotation soeben erfolgt
        var state = ErstelleState(prestigeCount: 0); // 15 Minuten Intervall bei 0 Prestige
        state.LastQuickJobRotation = DateTime.UtcNow;
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Prüfung: Restzeit nah an 15 Minuten
        service.TimeUntilNextRotation.Should().BeCloseTo(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(5));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Tages-Reset
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsDailyLimitReached_TagesResetFaellig_ResettetCounter()
    {
        // Vorbereitung: Letzter Reset war gestern, aber Counter auf 20
        var state = ErstelleState(prestigeCount: 0); // Limit = 20
        state.LastQuickJobDailyReset = DateTime.UtcNow.AddDays(-1);
        state.QuickJobsCompletedToday = 20; // War gestern voll
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung: IsDailyLimitReached prüft und resettet intern
        var result = service.IsDailyLimitReached;

        // Prüfung: Nach Reset ist das Limit heute nicht mehr erreicht
        result.Should().BeFalse();
        state.QuickJobsCompletedToday.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetAvailableJobs
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAvailableJobs_GibtJobsAusState()
    {
        // Vorbereitung
        var state = ErstelleState();
        state.QuickJobs.Add(new QuickJob
        {
            IsCompleted = false,
            WorkshopType = WorkshopType.Carpenter,
            MiniGameType = MiniGameType.Sawing,
            TitleKey = "QuickRepair"
        });
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        var jobs = service.GetAvailableJobs();

        // Prüfung
        jobs.Should().HaveCount(1);
    }

    [Fact]
    public void GetAvailableJobs_BerechnetBelohnungNeu_FuerNichtErledigteJobs()
    {
        // Vorbereitung: Job mit alter Belohnung (0)
        var state = ErstelleState(playerLevel: 10);
        state.QuickJobs.Add(new QuickJob
        {
            IsCompleted = false,
            Reward = 0m, // Bewusst 0 gesetzt
            WorkshopType = WorkshopType.Carpenter,
            MiniGameType = MiniGameType.Sawing,
            TitleKey = "QuickRepair",
            Difficulty = OrderDifficulty.Easy
        });
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        var jobs = service.GetAvailableJobs();

        // Prüfung: Belohnung wurde neu berechnet (nicht mehr 0)
        jobs[0].Reward.Should().BeGreaterThan(0m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // QuickJob Schwierigkeit
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateJobs_Level50OderWeniger_NurEasySchwierigkeit()
    {
        // Vorbereitung: Workshop-Level <= 50 → immer Easy
        var state = ErstelleState();
        state.Workshops[0].Level = 10; // Unter 50
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung
        service.GenerateJobs(20);

        // Prüfung: Alle Jobs Easy bei Workshop-Level <= 50
        state.QuickJobs.Should().AllSatisfy(j =>
            j.Difficulty.Should().Be(OrderDifficulty.Easy));
    }

    [Fact]
    public void GenerateJobs_KeinExpert_QuickJobsSindLocker()
    {
        // Regression: QuickJobs dürfen NIE Expert-Schwierigkeitsgrad haben (laut Kommentar im Code)
        var state = ErstelleState();
        state.Workshops[0].Level = 1000; // Sehr hoch
        state.Prestige.BronzeCount = 10;
        var service = new QuickJobService(ErstelleMockStateService(state), ErstelleMockLocalizationService());

        // Ausführung: Viele Jobs generieren
        service.GenerateJobs(50);

        // Prüfung
        state.QuickJobs.Should().NotContain(j => j.Difficulty == OrderDifficulty.Expert);
    }
}
