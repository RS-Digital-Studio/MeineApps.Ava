using System.Text;
using MeineApps.Core.Ava.Services;
using WorkTimePro.Models;
using WorkTimePro.Services;

namespace WorkTimePro.Tests;

/// <summary>
/// Tests für den ICS-Kalender-Export (RFC 5545).
/// </summary>
public class CalendarExportServiceTests
{
    private static IDatabaseService ErstelleDbMock(
        List<WorkDay>? arbeitstage = null,
        Dictionary<int, List<TimeEntry>>? eintraegeProTag = null,
        List<VacationEntry>? urlaube = null)
    {
        var db = Substitute.For<IDatabaseService>();
        db.GetWorkDaysAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(Task.FromResult(arbeitstage ?? []));
        db.GetTimeEntriesForWorkDaysAsync(Arg.Any<List<int>>())
            .Returns(Task.FromResult(eintraegeProTag ?? new Dictionary<int, List<TimeEntry>>()));
        db.GetVacationEntriesAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(Task.FromResult(urlaube ?? []));
        return db;
    }

    private static IFileShareService ErstelleFileShareMock()
    {
        var fs = Substitute.For<IFileShareService>();
        fs.GetExportDirectory(Arg.Any<string>())
            .Returns(Path.GetTempPath());
        return fs;
    }

    // ═══════════════════════════════════════════════════════════════════
    // RFC 5545 Grundstruktur
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportRangeToIcsAsync_LeererZeitraum_ErzeugtGueltigesVCalendar()
    {
        var db = ErstelleDbMock();
        var fs = ErstelleFileShareMock();
        var sut = new CalendarExportService(db, fs);

        var pfad = await sut.ExportRangeToIcsAsync(
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        var inhalt = await File.ReadAllTextAsync(pfad);
        inhalt.Should().Contain("BEGIN:VCALENDAR");
        inhalt.Should().Contain("END:VCALENDAR");
        inhalt.Should().Contain("VERSION:2.0");
        inhalt.Should().Contain("PRODID:");
        inhalt.Should().NotContain("BEGIN:VEVENT"); // Keine Events bei leerem Zeitraum

        File.Delete(pfad);
    }

    [Fact]
    public async Task ExportRangeToIcsAsync_NutztCrlfNichtLf()
    {
        // RFC 5545 Sec. 3.1: Zeilenenden MÜSSEN CRLF sein
        var db = ErstelleDbMock();
        var fs = ErstelleFileShareMock();
        var sut = new CalendarExportService(db, fs);

        var pfad = await sut.ExportRangeToIcsAsync(
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        var bytes = await File.ReadAllBytesAsync(pfad);
        var inhalt = Encoding.UTF8.GetString(bytes);

        // Jedes \n muss von \r begleitet werden
        for (int i = 0; i < inhalt.Length; i++)
        {
            if (inhalt[i] == '\n')
                (i > 0 && inhalt[i - 1] == '\r').Should().BeTrue(
                    $"Position {i}: LF ohne vorangehendes CR gefunden");
        }

        File.Delete(pfad);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Arbeitstag-Events
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportRangeToIcsAsync_ArbeitstMitCheckInOut_ErzeugtZeitgebundenesEvent()
    {
        var checkIn = new DateTime(2026, 3, 10, 8, 0, 0);
        var checkOut = new DateTime(2026, 3, 10, 16, 30, 0);
        var arbeitstag = new WorkDay
        {
            Id = 1, Date = new DateTime(2026, 3, 10),
            Status = DayStatus.WorkDay,
            ActualWorkMinutes = 510, TargetWorkMinutes = 480,
            BalanceMinutes = 30
        };
        var eintraege = new Dictionary<int, List<TimeEntry>>
        {
            [1] = [
                new() { Type = EntryType.CheckIn, Timestamp = checkIn },
                new() { Type = EntryType.CheckOut, Timestamp = checkOut }
            ]
        };
        var db = ErstelleDbMock(arbeitstage: [arbeitstag], eintraegeProTag: eintraege);
        var fs = ErstelleFileShareMock();
        var sut = new CalendarExportService(db, fs);

        var pfad = await sut.ExportRangeToIcsAsync(
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        var inhalt = await File.ReadAllTextAsync(pfad);
        inhalt.Should().Contain("BEGIN:VEVENT");
        inhalt.Should().Contain("DTSTART:20260310T080000");
        inhalt.Should().Contain("DTEND:20260310T163000");
        inhalt.Should().Contain("UID:worktimepro-day-20260310@meineapps");
        inhalt.Should().Contain("END:VEVENT");

        File.Delete(pfad);
    }

    [Fact]
    public async Task ExportRangeToIcsAsync_ArbeitstageOhneMinuten_WirdUebersprungen()
    {
        // Arbeitstag mit 0 Minuten und Status WorkDay = überspringen
        var arbeitstag = new WorkDay
        {
            Id = 1, Date = new DateTime(2026, 3, 10),
            Status = DayStatus.WorkDay,
            ActualWorkMinutes = 0
        };
        var db = ErstelleDbMock(arbeitstage: [arbeitstag]);
        var fs = ErstelleFileShareMock();
        var sut = new CalendarExportService(db, fs);

        var pfad = await sut.ExportRangeToIcsAsync(
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        var inhalt = await File.ReadAllTextAsync(pfad);
        inhalt.Should().NotContain("BEGIN:VEVENT");

        File.Delete(pfad);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Ganztägige Events (Feiertag, Krank, HomeOffice)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportRangeToIcsAsync_Feiertag_ErzeugtGanztaegigEsEvent()
    {
        var arbeitstag = new WorkDay
        {
            Id = 1, Date = new DateTime(2026, 12, 25),
            Status = DayStatus.Holiday,
            ActualWorkMinutes = 0
        };
        var db = ErstelleDbMock(arbeitstage: [arbeitstag]);
        var fs = ErstelleFileShareMock();
        var sut = new CalendarExportService(db, fs);

        var pfad = await sut.ExportRangeToIcsAsync(
            new DateTime(2026, 12, 1), new DateTime(2026, 12, 31));

        var inhalt = await File.ReadAllTextAsync(pfad);
        inhalt.Should().Contain("BEGIN:VEVENT");
        inhalt.Should().Contain("DTSTART;VALUE=DATE:20261225");
        inhalt.Should().Contain("DTEND;VALUE=DATE:20261226"); // +1 Tag (RFC 5545: exklusiv)
        inhalt.Should().Contain("TRANSP:TRANSPARENT");

        File.Delete(pfad);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Urlaubseinträge
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportRangeToIcsAsync_Urlaub_ErzeugtMehrtaegigEsEvent()
    {
        var urlaub = new VacationEntry
        {
            Id = 1, Year = 2026,
            StartDate = new DateTime(2026, 7, 13),
            EndDate = new DateTime(2026, 7, 24),
            Days = 10, Type = DayStatus.Vacation,
            Note = "Sommerurlaub"
        };
        var db = ErstelleDbMock(urlaube: [urlaub]);
        var fs = ErstelleFileShareMock();
        var sut = new CalendarExportService(db, fs);

        var pfad = await sut.ExportRangeToIcsAsync(
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 31));

        var inhalt = await File.ReadAllTextAsync(pfad);
        inhalt.Should().Contain("DTSTART;VALUE=DATE:20260713");
        inhalt.Should().Contain("DTEND;VALUE=DATE:20260725"); // 24+1 (exklusiv)
        inhalt.Should().Contain("Sommerurlaub");
        inhalt.Should().Contain("UID:worktimepro-vacation-1-20260713@meineapps");

        File.Delete(pfad);
    }

    // ═══════════════════════════════════════════════════════════════════
    // RFC 5545 Escaping
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportRangeToIcsAsync_SonderzeichenInNotiz_WerdenEscaped()
    {
        var arbeitstag = new WorkDay
        {
            Id = 1, Date = new DateTime(2026, 3, 10),
            Status = DayStatus.Sick,
            ActualWorkMinutes = 0,
            Note = "Arzt; Termin, um 10:00"
        };
        var db = ErstelleDbMock(arbeitstage: [arbeitstag]);
        var fs = ErstelleFileShareMock();
        var sut = new CalendarExportService(db, fs);

        var pfad = await sut.ExportRangeToIcsAsync(
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        var inhalt = await File.ReadAllTextAsync(pfad);
        // Semikolon und Komma müssen escaped sein
        inhalt.Should().Contain("Arzt\\; Termin\\, um 10:00");

        File.Delete(pfad);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Line-Folding (RFC 5545 Sec. 3.1)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportRangeToIcsAsync_LangeZeilen_WerdenGefoldet()
    {
        // RFC 5545: Keine Zeile darf 75 Octets überschreiten
        var arbeitstag = new WorkDay
        {
            Id = 1, Date = new DateTime(2026, 3, 10),
            Status = DayStatus.WorkDay,
            ActualWorkMinutes = 480, TargetWorkMinutes = 480,
            BalanceMinutes = 0,
            Note = "Dies ist eine sehr lange Notiz die definitiv mehr als 75 Bytes " +
                   "ergeben wird wenn sie in der DESCRIPTION-Zeile steht und damit " +
                   "Line-Folding auslösen muss gemäß RFC 5545"
        };
        var eintraege = new Dictionary<int, List<TimeEntry>>
        {
            [1] = [
                new() { Type = EntryType.CheckIn, Timestamp = new DateTime(2026, 3, 10, 8, 0, 0) },
                new() { Type = EntryType.CheckOut, Timestamp = new DateTime(2026, 3, 10, 16, 0, 0) }
            ]
        };
        var db = ErstelleDbMock(arbeitstage: [arbeitstag], eintraegeProTag: eintraege);
        var fs = ErstelleFileShareMock();
        var sut = new CalendarExportService(db, fs);

        var pfad = await sut.ExportRangeToIcsAsync(
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        var bytes = await File.ReadAllBytesAsync(pfad);
        var inhalt = Encoding.UTF8.GetString(bytes);

        // Prüfe dass keine physische Zeile >75 Bytes hat
        var zeilen = inhalt.Split("\r\n");
        foreach (var zeile in zeilen)
        {
            Encoding.UTF8.GetByteCount(zeile).Should().BeInRange(0, 75,
                $"Zeile '{zeile[..Math.Min(50, zeile.Length)]}...' überschreitet 75 Octets");
        }

        File.Delete(pfad);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Jahres-Export
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportYearToIcsAsync_ErzeugtDateiMitJahrImNamen()
    {
        var db = ErstelleDbMock();
        var fs = ErstelleFileShareMock();
        var sut = new CalendarExportService(db, fs);

        var pfad = await sut.ExportYearToIcsAsync(2026);

        pfad.Should().Contain("2026");
        pfad.Should().EndWith(".ics");
        File.Exists(pfad).Should().BeTrue();

        File.Delete(pfad);
    }
}
