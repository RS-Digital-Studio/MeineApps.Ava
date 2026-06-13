using WorkTimePro.Models;
using WorkTimePro.Services;

namespace WorkTimePro.Tests;

/// <summary>
/// Tests für VacationService: Arbeitstage-Berechnung, Urlaubsquoten-Logik
/// ohne Datenbankzugriff (Mock-basiert).
/// </summary>
public class VacationServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erstellt Standard-WorkSettings mit Mo-Fr als Arbeitstage.
    /// </summary>
    private static WorkSettings ErstelleStandardSettings(string arbeitstage = "1,2,3,4,5")
    {
        return new WorkSettings
        {
            WorkDays = arbeitstage,
            DailyHours = 8.0,
            HolidayRegion = "DE-BY"
        };
    }

    /// <summary>
    /// Erstellt ein vollständiges Mock-Setup für VacationService.
    /// </summary>
    private static (IDatabaseService Db, IHolidayService HolidaySvc, VacationService Sut) ErstelleSetup(
        WorkSettings? settings = null,
        List<HolidayEntry>? feiertage = null,
        VacationQuota? quota = null,
        List<VacationEntry>? eintraege = null)
    {
        var db = Substitute.For<IDatabaseService>();
        var holidaySvc = Substitute.For<IHolidayService>();

        db.GetSettingsAsync().Returns(Task.FromResult(settings ?? ErstelleStandardSettings()));
        holidaySvc.GetHolidaysAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(Task.FromResult(feiertage ?? []));
        db.GetVacationQuotaAsync(Arg.Any<int>(), Arg.Any<int?>())
            .Returns(Task.FromResult(quota));
        db.SaveVacationQuotaAsync(Arg.Any<VacationQuota>())
            .Returns(Task.CompletedTask);
        db.GetVacationEntriesAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(Task.FromResult(eintraege ?? []));

        var sut = new VacationService(db, holidaySvc);
        return (db, holidaySvc, sut);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CalculateWorkDaysAsync - Kernlogik ohne DB-Zugriff
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CalculateWorkDaysAsync_EineWocheOhneFeiertage_FuenfArbeitstage()
    {
        // Vorbereitung: Mo 12.01.2026 bis Fr 16.01.2026
        var (_, _, sut) = ErstelleSetup();
        var start = new DateTime(2026, 1, 12);
        var ende = new DateTime(2026, 1, 16);

        // Ausführung
        var tage = await sut.CalculateWorkDaysAsync(start, ende);

        // Prüfung
        tage.Should().Be(5);
    }

    [Fact]
    public async Task CalculateWorkDaysAsync_WocheEndePlusSamstag_NullArbeitstage()
    {
        // Vorbereitung: Samstag 17.01.2026 bis Sonntag 18.01.2026
        var (_, _, sut) = ErstelleSetup();
        var start = new DateTime(2026, 1, 17); // Samstag
        var ende = new DateTime(2026, 1, 18);  // Sonntag

        // Ausführung
        var tage = await sut.CalculateWorkDaysAsync(start, ende);

        // Prüfung
        tage.Should().Be(0);
    }

    [Fact]
    public async Task CalculateWorkDaysAsync_EinzelnerArbeitstag_EinTag()
    {
        // Vorbereitung: Einzelner Montag
        var (_, _, sut) = ErstelleSetup();
        var montag = new DateTime(2026, 1, 12); // Montag

        // Ausführung
        var tage = await sut.CalculateWorkDaysAsync(montag, montag);

        // Prüfung
        tage.Should().Be(1);
    }

    [Fact]
    public async Task CalculateWorkDaysAsync_WocheWithFeiertag_FeiertagAbgezogen()
    {
        // Vorbereitung: Neujahrstag liegt in der Woche → nur 4 Arbeitstage
        var neujahr = new List<HolidayEntry>
        {
            new() { Date = new DateTime(2026, 1, 1), Name = "Neujahr" }
        };
        var (_, _, sut) = ErstelleSetup(feiertage: neujahr);
        var start = new DateTime(2025, 12, 29); // Montag
        var ende = new DateTime(2026, 1, 2);    // Freitag

        // Ausführung: 5 Arbeitstage - 1 Feiertag (1.1. = Donnerstag) = 4
        var tage = await sut.CalculateWorkDaysAsync(start, ende);

        // Prüfung
        tage.Should().Be(4);
    }

    [Fact]
    public async Task CalculateWorkDaysAsync_EinzelnerFeiertag_NullArbeitstage()
    {
        // Vorbereitung: Einzelner Feiertag
        var feiertag = new List<HolidayEntry>
        {
            new() { Date = new DateTime(2026, 5, 1), Name = "Tag der Arbeit" }
        };
        var (_, _, sut) = ErstelleSetup(feiertage: feiertag);
        var tagDerArbeit = new DateTime(2026, 5, 1); // Freitag

        // Ausführung
        var tage = await sut.CalculateWorkDaysAsync(tagDerArbeit, tagDerArbeit);

        // Prüfung
        tage.Should().Be(0);
    }

    [Fact]
    public async Task CalculateWorkDaysAsync_ZweiWochen_ZehnArbeitstage()
    {
        // Vorbereitung: 2 Kalenderwochen, keine Feiertage
        var (_, _, sut) = ErstelleSetup();
        var start = new DateTime(2026, 1, 12); // Montag
        var ende = new DateTime(2026, 1, 23);  // Freitag der Folgewoche

        // Ausführung
        var tage = await sut.CalculateWorkDaysAsync(start, ende);

        // Prüfung
        tage.Should().Be(10);
    }

    [Fact]
    public async Task CalculateWorkDaysAsync_NurSamstage_NullArbeitstage()
    {
        // Vorbereitung: 3 Samstage, keine Feiertage
        var (_, _, sut) = ErstelleSetup();
        var start = new DateTime(2026, 1, 3);  // Samstag
        var ende = new DateTime(2026, 1, 17);  // Samstag (3 Samstage: 3,10,17)

        // Ausführung - nur Samstage, keine regulären Arbeitstage
        var tage = await sut.CalculateWorkDaysAsync(
            new DateTime(2026, 1, 3),
            new DateTime(2026, 1, 3));

        // Prüfung: Samstag ist kein Arbeitstag
        tage.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // VacationQuota - Standard-Kontingent
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetQuotaAsync_KeineQuotaInDB_ErstelltStandardQuota()
    {
        // Vorbereitung: Kein Quota in DB → Standard 30 Tage
        var (db, _, sut) = ErstelleSetup(quota: null);

        // Ausführung
        var quota = await sut.GetQuotaAsync(2026);

        // Prüfung: Standard-Quota wurde erstellt
        quota.Should().NotBeNull();
        quota.TotalDays.Should().Be(30);
    }

    [Fact]
    public async Task GetQuotaAsync_BestehenderQuota_GibtExistierendenZurueck()
    {
        // Vorbereitung: Quota mit 25 Tagen in DB. Verfall deaktiviert, damit der Test
        // unabhängig vom heutigen Datum ist (Stichtag-Logik hat eigene Tests unten).
        var settings = ErstelleStandardSettings();
        settings.VacationCarryOverExpires = false;
        var bestehendeQuota = new VacationQuota
        {
            Id = 1,
            Year = 2026,
            TotalDays = 25,
            CarryOverDays = 3
        };
        var (_, _, sut) = ErstelleSetup(settings: settings, quota: bestehendeQuota);

        // Ausführung
        var quota = await sut.GetQuotaAsync(2026);

        // Prüfung
        quota.TotalDays.Should().Be(25);
        quota.CarryOverDays.Should().Be(3);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Resturlaub-Verfall zum Stichtag (BUrlG 31.03.)
    // ═══════════════════════════════════════════════════════════════════
    // Die Tests nutzen ein VERGANGENES Jahr (2024) — der Stichtag 31.03.2024
    // liegt damit garantiert vor "heute" (deterministisch, kein Clock-Mock nötig).

    [Fact]
    public async Task GetQuotaAsync_KeinUrlaubBisStichtag_UebertragVerfaelltKomplett()
    {
        // Vorbereitung: 5 Tage Übertrag, Urlaub erst NACH dem Stichtag (August)
        // → Übertrag muss komplett verfallen (Urlaub im August stammt aus dem
        // regulären Jahresanspruch, nicht aus dem Übertrag).
        var bestehendeQuota = new VacationQuota
        {
            Id = 1,
            Year = 2024,
            TotalDays = 30,
            CarryOverDays = 5
        };
        var eintraege = new List<VacationEntry>
        {
            new()
            {
                Id = 1,
                Type = DayStatus.Vacation,
                StartDate = new DateTime(2024, 8, 5),  // Montag
                EndDate = new DateTime(2024, 8, 16),   // Freitag der Folgewoche
                Days = 10
            }
        };
        var (_, _, sut) = ErstelleSetup(quota: bestehendeQuota, eintraege: eintraege);

        // Ausführung
        var quota = await sut.GetQuotaAsync(2024);

        // Prüfung: Trotz 10 genommener Tage im Jahr verfällt der volle Übertrag
        quota.CarryOverDays.Should().Be(0);
    }

    [Fact]
    public async Task GetQuotaAsync_UrlaubVorStichtag_UebertragBleibtTeilweiseErhalten()
    {
        // Vorbereitung: 5 Tage Übertrag, 2 Werktage Urlaub im Februar (vor Stichtag)
        // → nur die 2 bis zum Stichtag genutzten Tage bleiben erhalten.
        var bestehendeQuota = new VacationQuota
        {
            Id = 1,
            Year = 2024,
            TotalDays = 30,
            CarryOverDays = 5
        };
        var eintraege = new List<VacationEntry>
        {
            new()
            {
                Id = 1,
                Type = DayStatus.Vacation,
                StartDate = new DateTime(2024, 2, 5),  // Montag
                EndDate = new DateTime(2024, 2, 6),    // Dienstag
                Days = 2
            }
        };
        var (_, _, sut) = ErstelleSetup(quota: bestehendeQuota, eintraege: eintraege);

        // Ausführung
        var quota = await sut.GetQuotaAsync(2024);

        // Prüfung
        quota.CarryOverDays.Should().Be(2);
    }

    [Fact]
    public async Task GetQuotaAsync_UrlaubUeberStichtag_NurTageBisStichtagZaehlen()
    {
        // Vorbereitung: 5 Tage Übertrag, Urlaub Do 28.03. bis Do 04.04.2024 (über den
        // Stichtag 31.03.) → nur Do 28.03. + Fr 29.03. (2 Werktage, Karfreitag ignoriert,
        // da Mock keine Feiertage liefert) zählen für den Übertrag-Erhalt.
        var bestehendeQuota = new VacationQuota
        {
            Id = 1,
            Year = 2024,
            TotalDays = 30,
            CarryOverDays = 5
        };
        var eintraege = new List<VacationEntry>
        {
            new()
            {
                Id = 1,
                Type = DayStatus.Vacation,
                StartDate = new DateTime(2024, 3, 28), // Donnerstag
                EndDate = new DateTime(2024, 4, 4),    // Donnerstag der Folgewoche
                Days = 6
            }
        };
        var (_, _, sut) = ErstelleSetup(quota: bestehendeQuota, eintraege: eintraege);

        // Ausführung
        var quota = await sut.GetQuotaAsync(2024);

        // Prüfung: Do 28.03. + Fr 29.03. = 2 Werktage bis Stichtag (30./31.03. = Wochenende)
        quota.CarryOverDays.Should().Be(2);
    }

    [Fact]
    public async Task GetQuotaAsync_VerfallDeaktiviert_UebertragBleibtVollErhalten()
    {
        // Vorbereitung: Verfall aus → Übertrag bleibt auch ohne genommenen Urlaub stehen
        var settings = ErstelleStandardSettings();
        settings.VacationCarryOverExpires = false;
        var bestehendeQuota = new VacationQuota
        {
            Id = 1,
            Year = 2024,
            TotalDays = 30,
            CarryOverDays = 5
        };
        var (_, _, sut) = ErstelleSetup(settings: settings, quota: bestehendeQuota);

        // Ausführung
        var quota = await sut.GetQuotaAsync(2024);

        // Prüfung
        quota.CarryOverDays.Should().Be(5);
    }

    [Fact]
    public async Task GetQuotaAsync_MitVergangenenUrlaubseintraegen_TakenDaysBerechnet()
    {
        // Vorbereitung: 5 genommene Urlaubstage in der Vergangenheit
        var letzterMonat = DateTime.Today.AddMonths(-1);
        var eintraege = new List<VacationEntry>
        {
            new()
            {
                Id = 1,
                Type = DayStatus.Vacation,
                StartDate = letzterMonat,
                EndDate = letzterMonat.AddDays(4),
                Days = 5
            }
        };
        var (_, _, sut) = ErstelleSetup(eintraege: eintraege);

        // Ausführung
        var quota = await sut.GetQuotaAsync(letzterMonat.Year);

        // Prüfung
        quota.TakenDays.Should().Be(5);
        quota.PlannedDays.Should().Be(0);
    }

    [Fact]
    public async Task GetQuotaAsync_MitZukuenftigenUrlaubseintraegen_PlannedDaysBerechnet()
    {
        // Vorbereitung: 3 geplante Urlaubstage in der Zukunft
        var naechsteWoche = DateTime.Today.AddDays(7);
        var eintraege = new List<VacationEntry>
        {
            new()
            {
                Id = 1,
                Type = DayStatus.Vacation,
                StartDate = naechsteWoche,
                EndDate = naechsteWoche.AddDays(2),
                Days = 3
            }
        };
        var (_, _, sut) = ErstelleSetup(eintraege: eintraege);

        // Ausführung
        var quota = await sut.GetQuotaAsync(naechsteWoche.Year);

        // Prüfung
        quota.PlannedDays.Should().Be(3);
        quota.TakenDays.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // WorkSettings.WorkDaysArray - Arbeitstage-Parsing
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CalculateWorkDaysAsync_NurMontag_EineWoche_EinTag()
    {
        // Vorbereitung: Nur Montag ist Arbeitstag
        var nurMontagSettings = new WorkSettings { WorkDays = "1", HolidayRegion = "DE-BY" };
        var (_, _, sut) = ErstelleSetup(settings: nurMontagSettings);
        var start = new DateTime(2026, 1, 12); // Montag
        var ende = new DateTime(2026, 1, 16);  // Freitag

        // Ausführung: Nur Montag 12.1. ist Arbeitstag
        var tage = await sut.CalculateWorkDaysAsync(start, ende);

        // Prüfung
        tage.Should().Be(1);
    }

    [Fact]
    public async Task CalculateWorkDaysAsync_LeereArbeitstage_NullTage()
    {
        // Vorbereitung: Keine Arbeitstage konfiguriert
        var keineArbeitstage = new WorkSettings { WorkDays = "", HolidayRegion = "DE-BY" };
        var (_, _, sut) = ErstelleSetup(settings: keineArbeitstage);

        // Ausführung
        var tage = await sut.CalculateWorkDaysAsync(
            new DateTime(2026, 1, 12),
            new DateTime(2026, 1, 16));

        // Prüfung
        tage.Should().Be(0);
    }
}
