using WorkTimePro.Models;
using WorkTimePro.Services;

namespace WorkTimePro.Tests;

/// <summary>
/// Tests für CalculationService: Arbeitszeit-Arithmetik, Pausen-Abzug,
/// Überstunden-Saldo, Zeitrundung und gesetzliche Compliance-Prüfung.
/// </summary>
public class CalculationServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erstellt eine Standard-WorkSettings-Instanz für Tests.
    /// </summary>
    private static WorkSettings ErstelleStandardSettings(
        double taeglStunden = 8.0,
        bool autoPause = false,
        int rundungsMinuten = 0)
    {
        return new WorkSettings
        {
            DailyHours = taeglStunden,
            AutoPauseEnabled = autoPause,
            RoundingMinutes = rundungsMinuten,
            LegalComplianceEnabled = true,
            MaxDailyHours = 10,
            MinRestHours = 11
        };
    }

    /// <summary>
    /// Erstellt ein IDatabaseService-Mock mit vorgegebenen Rückgabewerten.
    /// </summary>
    private static IDatabaseService ErstelleDbMock(
        List<TimeEntry>? eintraege = null,
        List<PauseEntry>? pausen = null,
        WorkSettings? settings = null)
    {
        var db = Substitute.For<IDatabaseService>();
        db.GetTimeEntriesAsync(Arg.Any<int>())
            .Returns(Task.FromResult(eintraege ?? []));
        db.GetPauseEntriesAsync(Arg.Any<int>())
            .Returns(Task.FromResult(pausen ?? []));
        db.GetSettingsAsync()
            .Returns(Task.FromResult(settings ?? ErstelleStandardSettings()));
        db.SaveWorkDayAsync(Arg.Any<WorkDay>())
            .Returns(Task.FromResult(1));
        db.SavePauseEntryAsync(Arg.Any<PauseEntry>())
            .Returns(Task.FromResult(1));
        db.DeletePauseEntryAsync(Arg.Any<int>())
            .Returns(Task.CompletedTask);
        db.GetWorkDayAsync(Arg.Any<DateTime>())
            .Returns(Task.FromResult<WorkDay?>(null));
        db.GetWorkDaysAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(Task.FromResult(new List<WorkDay>()));
        return db;
    }

    /// <summary>
    /// Erstellt ein Check-In/Check-Out Paar für die angegebene Zeitspanne.
    /// </summary>
    private static List<TimeEntry> ErstelleZeiteintraege(DateTime checkIn, DateTime checkOut)
    {
        return
        [
            new TimeEntry { Id = 1, WorkDayId = 1, Type = EntryType.CheckIn, Timestamp = checkIn },
            new TimeEntry { Id = 2, WorkDayId = 1, Type = EntryType.CheckOut, Timestamp = checkOut }
        ];
    }

    // ═══════════════════════════════════════════════════════════════════
    // RecalculateWorkDayAsync - Grundlegende Berechnungen
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RecalculateWorkDayAsync_EineStundeArbeit_SetzteKorrektMinuten()
    {
        // Vorbereitung
        var basis = new DateTime(2026, 1, 13, 8, 0, 0);
        var eintraege = ErstelleZeiteintraege(basis, basis.AddHours(1));
        var db = ErstelleDbMock(eintraege: eintraege);
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay { Id = 1, TargetWorkMinutes = 480 };

        // Ausführung
        await sut.RecalculateWorkDayAsync(arbeitstag);

        // Prüfung
        arbeitstag.ActualWorkMinutes.Should().Be(60);
    }

    [Fact]
    public async Task RecalculateWorkDayAsync_AchtStundenArbeit_SaldoIstNull()
    {
        // Vorbereitung: Genau 8h Soll = 8h Ist → Saldo 0
        var basis = new DateTime(2026, 1, 13, 8, 0, 0);
        var eintraege = ErstelleZeiteintraege(basis, basis.AddHours(8));
        var db = ErstelleDbMock(eintraege: eintraege);
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay { Id = 1, TargetWorkMinutes = 480 };

        // Ausführung
        await sut.RecalculateWorkDayAsync(arbeitstag);

        // Prüfung
        arbeitstag.BalanceMinutes.Should().Be(0);
    }

    [Fact]
    public async Task RecalculateWorkDayAsync_NeunStundenArbeit_PositiverSaldo()
    {
        // Vorbereitung: 9h Arbeit, 8h Soll → +60 Minuten
        var basis = new DateTime(2026, 1, 13, 8, 0, 0);
        var eintraege = ErstelleZeiteintraege(basis, basis.AddHours(9));
        var db = ErstelleDbMock(eintraege: eintraege);
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay { Id = 1, TargetWorkMinutes = 480 };

        // Ausführung
        await sut.RecalculateWorkDayAsync(arbeitstag);

        // Prüfung
        arbeitstag.BalanceMinutes.Should().Be(60);
    }

    [Fact]
    public async Task RecalculateWorkDayAsync_SiebenstundenArbeit_NegativerSaldo()
    {
        // Vorbereitung: 7h Arbeit, 8h Soll → -60 Minuten
        var basis = new DateTime(2026, 1, 13, 8, 0, 0);
        var eintraege = ErstelleZeiteintraege(basis, basis.AddHours(7));
        var db = ErstelleDbMock(eintraege: eintraege);
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay { Id = 1, TargetWorkMinutes = 480 };

        // Ausführung
        await sut.RecalculateWorkDayAsync(arbeitstag);

        // Prüfung
        arbeitstag.BalanceMinutes.Should().Be(-60);
    }

    [Fact]
    public async Task RecalculateWorkDayAsync_KeineEintraege_NullMinutenUndNegativerSaldo()
    {
        // Vorbereitung: Kein Check-In → 0 Minuten Ist, -480 Minuten Saldo
        var db = ErstelleDbMock();
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay { Id = 1, TargetWorkMinutes = 480 };

        // Ausführung
        await sut.RecalculateWorkDayAsync(arbeitstag);

        // Prüfung
        arbeitstag.ActualWorkMinutes.Should().Be(0);
        arbeitstag.BalanceMinutes.Should().Be(-480);
    }

    [Fact]
    public async Task RecalculateWorkDayAsync_ManuellesPause30Min_WirdVonArbeitszeitAbgezogen()
    {
        // Vorbereitung: 8h Brutto - 30min Pause = 7h30 Netto
        var basis = new DateTime(2026, 1, 13, 8, 0, 0);
        var eintraege = ErstelleZeiteintraege(basis, basis.AddHours(8));
        var pausen = new List<PauseEntry>
        {
            new PauseEntry
            {
                Id = 1, WorkDayId = 1, IsAutoPause = false,
                StartTime = basis.AddHours(4),
                EndTime = basis.AddHours(4).AddMinutes(30)
            }
        };
        var db = ErstelleDbMock(eintraege: eintraege, pausen: pausen);
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay { Id = 1, TargetWorkMinutes = 480 };

        // Ausführung
        await sut.RecalculateWorkDayAsync(arbeitstag);

        // Prüfung: 480 - 30 = 450 Minuten
        arbeitstag.ActualWorkMinutes.Should().Be(450);
    }

    [Fact]
    public async Task RecalculateWorkDayAsync_MehrereCheckInCheckOut_SummiertKorrekt()
    {
        // Vorbereitung: Zwei Blöcke 4h + 3h = 7h
        var basis = new DateTime(2026, 1, 13, 8, 0, 0);
        var eintraege = new List<TimeEntry>
        {
            new() { Id = 1, WorkDayId = 1, Type = EntryType.CheckIn, Timestamp = basis },
            new() { Id = 2, WorkDayId = 1, Type = EntryType.CheckOut, Timestamp = basis.AddHours(4) },
            new() { Id = 3, WorkDayId = 1, Type = EntryType.CheckIn, Timestamp = basis.AddHours(5) },
            new() { Id = 4, WorkDayId = 1, Type = EntryType.CheckOut, Timestamp = basis.AddHours(8) },
        };
        var db = ErstelleDbMock(eintraege: eintraege);
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay { Id = 1, TargetWorkMinutes = 480 };

        // Ausführung
        await sut.RecalculateWorkDayAsync(arbeitstag);

        // Prüfung: 4h + 3h = 420 Minuten
        arbeitstag.ActualWorkMinutes.Should().Be(420);
    }

    [Fact]
    public async Task RecalculateWorkDayAsync_ErstesCheckInWirdAlsFirstCheckInGesetzt()
    {
        // Vorbereitung
        var basis = new DateTime(2026, 1, 13, 8, 30, 0);
        var eintraege = ErstelleZeiteintraege(basis, basis.AddHours(8));
        var db = ErstelleDbMock(eintraege: eintraege);
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay { Id = 1 };

        // Ausführung
        await sut.RecalculateWorkDayAsync(arbeitstag);

        // Prüfung
        arbeitstag.FirstCheckIn.Should().Be(basis);
    }

    [Fact]
    public async Task RecalculateWorkDayAsync_LetzterCheckOutWirdAlsLastCheckOutGesetzt()
    {
        // Vorbereitung
        var basis = new DateTime(2026, 1, 13, 8, 0, 0);
        var checkOut = basis.AddHours(8).AddMinutes(15);
        var eintraege = ErstelleZeiteintraege(basis, checkOut);
        var db = ErstelleDbMock(eintraege: eintraege);
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay { Id = 1 };

        // Ausführung
        await sut.RecalculateWorkDayAsync(arbeitstag);

        // Prüfung
        arbeitstag.LastCheckOut.Should().Be(checkOut);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Zeitrundung
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RecalculateWorkDayAsync_Rundung15Min_RundetKorrektAuf()
    {
        // Vorbereitung: 7h47min → gerundet auf 7h45min = 465min
        var basis = new DateTime(2026, 1, 13, 8, 0, 0);
        var eintraege = ErstelleZeiteintraege(basis, basis.AddHours(7).AddMinutes(47));
        var settings = ErstelleStandardSettings(rundungsMinuten: 15);
        var db = ErstelleDbMock(eintraege: eintraege, settings: settings);
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay { Id = 1, TargetWorkMinutes = 480 };

        // Ausführung
        await sut.RecalculateWorkDayAsync(arbeitstag);

        // Prüfung: 467 Minuten → nächstes 15-er Vielfaches = 465 (Math.Round rundet kaufmännisch)
        arbeitstag.ActualWorkMinutes.Should().Be(465);
    }

    [Fact]
    public async Task RecalculateWorkDayAsync_KeineRundung_BehaeltExakteMinuten()
    {
        // Vorbereitung: 7h47min, keine Rundung → exakt 467 Minuten
        var basis = new DateTime(2026, 1, 13, 8, 0, 0);
        var eintraege = ErstelleZeiteintraege(basis, basis.AddHours(7).AddMinutes(47));
        var settings = ErstelleStandardSettings(rundungsMinuten: 0);
        var db = ErstelleDbMock(eintraege: eintraege, settings: settings);
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay { Id = 1, TargetWorkMinutes = 480 };

        // Ausführung
        await sut.RecalculateWorkDayAsync(arbeitstag);

        // Prüfung
        arbeitstag.ActualWorkMinutes.Should().Be(467);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ISO-Wochennummer
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetIsoWeekNumber_ErsterJanuar2026_GibtWocheEins()
    {
        // Vorbereitung: 1. Januar 2026 ist ein Donnerstag → Woche 1
        var db = ErstelleDbMock();
        var sut = new CalculationService(db);

        // Ausführung
        var woche = sut.GetIsoWeekNumber(new DateTime(2026, 1, 1));

        // Prüfung
        woche.Should().Be(1);
    }

    [Fact]
    public void GetIsoWeekNumber_DreissigsterDezember2025_GibtWocheEins2026()
    {
        // Vorbereitung: 30. Dezember 2025 liegt in ISO-Woche 1 des Jahres 2026
        var db = ErstelleDbMock();
        var sut = new CalculationService(db);

        // Ausführung
        var woche = sut.GetIsoWeekNumber(new DateTime(2025, 12, 29));

        // Prüfung: 29.12.2025 ist ein Montag, gehört zu Woche 1/2026
        woche.Should().Be(1);
    }

    [Fact]
    public void GetFirstDayOfWeek_Woche1_2026_GibtMontag()
    {
        // Vorbereitung
        var db = ErstelleDbMock();
        var sut = new CalculationService(db);

        // Ausführung
        var ergebnis = sut.GetFirstDayOfWeek(2026, 1);

        // Prüfung: Ergebnis ist immer ein Montag
        ergebnis.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void GetFirstDayOfWeek_Woche10_2026_GibtKorrektenMontag()
    {
        // Vorbereitung
        var db = ErstelleDbMock();
        var sut = new CalculationService(db);

        // Ausführung
        var ergebnis = sut.GetFirstDayOfWeek(2026, 10);

        // Prüfung: Woche 10 liegt nach Woche 9, also später als Januar
        ergebnis.DayOfWeek.Should().Be(DayOfWeek.Monday);
        ergebnis.Year.Should().Be(2026);
        ergebnis.Month.Should().BeGreaterThanOrEqualTo(2); // Spätestens im Februar/März
    }

    // ═══════════════════════════════════════════════════════════════════
    // Gesetzliche Compliance (CheckLegalComplianceAsync)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckLegalComplianceAsync_NormalerArbeitstag_KeineWarnungen()
    {
        // Vorbereitung: 7h Arbeit, 30min Pause → keine Verstöße
        var db = ErstelleDbMock();
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay
        {
            Id = 1,
            Date = new DateTime(2026, 1, 13),
            ActualWorkMinutes = 420,   // 7h
            ManualPauseMinutes = 30,
            AutoPauseMinutes = 0
        };

        // Ausführung
        var warnungen = await sut.CheckLegalComplianceAsync(arbeitstag);

        // Prüfung
        warnungen.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckLegalComplianceAsync_UeberMaximalzeit_GibtWarnung()
    {
        // Vorbereitung: 11h Arbeit überschreitet ArbZG-Maximum (10h)
        var db = ErstelleDbMock();
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay
        {
            Id = 1,
            Date = new DateTime(2026, 1, 13),
            ActualWorkMinutes = 660, // 11h
            ManualPauseMinutes = 45,
            AutoPauseMinutes = 0
        };

        // Ausführung
        var warnungen = await sut.CheckLegalComplianceAsync(arbeitstag);

        // Prüfung
        warnungen.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CheckLegalComplianceAsync_UeberSechsStundenOhnePause_GibtWarnung()
    {
        // Vorbereitung: 7h Arbeit, keine Pause → Verstoß gegen 30min-Regelung
        var db = ErstelleDbMock();
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay
        {
            Id = 1,
            Date = new DateTime(2026, 1, 13),
            ActualWorkMinutes = 420, // 7h
            ManualPauseMinutes = 0,
            AutoPauseMinutes = 0
        };

        // Ausführung
        var warnungen = await sut.CheckLegalComplianceAsync(arbeitstag);

        // Prüfung
        warnungen.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CheckLegalComplianceAsync_DeaktivierteLegalCheck_KeineWarnungen()
    {
        // Vorbereitung: 11h Arbeit, aber Legal-Check deaktiviert
        var settings = ErstelleStandardSettings();
        settings.LegalComplianceEnabled = false;
        var db = ErstelleDbMock(settings: settings);
        var sut = new CalculationService(db);
        var arbeitstag = new WorkDay
        {
            Id = 1,
            Date = new DateTime(2026, 1, 13),
            ActualWorkMinutes = 660, // 11h - würde normalerweise Warnung erzeugen
            ManualPauseMinutes = 0,
            AutoPauseMinutes = 0
        };

        // Ausführung
        var warnungen = await sut.CheckLegalComplianceAsync(arbeitstag);

        // Prüfung: Keine Warnungen wenn Prüfung deaktiviert
        warnungen.Should().BeEmpty();
    }
}
