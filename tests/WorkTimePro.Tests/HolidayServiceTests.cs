using WorkTimePro.Models;
using WorkTimePro.Services;

namespace WorkTimePro.Tests;

/// <summary>
/// Tests für HolidayService: Feiertagsberechnung für alle deutschen Bundesländer,
/// Ostern-Algorithmus, Buß- und Bettag, Caching-Verhalten.
/// </summary>
public class HolidayServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // HILFSMETHODEN
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Erstellt ein IDatabaseService-Mock das für das angegebene Bundesland konfiguriert ist.
    /// </summary>
    private static IDatabaseService ErstelleDbMockFuerRegion(string region)
    {
        var settings = new WorkSettings { HolidayRegion = region };
        var db = Substitute.For<IDatabaseService>();
        db.GetSettingsAsync().Returns(Task.FromResult(settings));
        return db;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Nationale Feiertage (alle Bundesländer)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateHolidays_2026_EnthältNeujahr()
    {
        // Vorbereitung
        var db = ErstelleDbMockFuerRegion("DE-BY");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2026, "DE-BY");

        // Prüfung
        feiertage.Should().Contain(f => f.Date == new DateTime(2026, 1, 1) && f.Name == "Neujahr");
    }

    [Fact]
    public void CalculateHolidays_2026_EnthältTagDerDeutschenEinheit()
    {
        // Vorbereitung
        var db = ErstelleDbMockFuerRegion("DE-NW");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2026, "DE-NW");

        // Prüfung
        feiertage.Should().Contain(f => f.Date == new DateTime(2026, 10, 3));
    }

    [Fact]
    public void CalculateHolidays_2026_EnthältBeidWeihnachtstage()
    {
        // Vorbereitung
        var db = ErstelleDbMockFuerRegion("DE-HH");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2026, "DE-HH");

        // Prüfung
        feiertage.Should().Contain(f => f.Date == new DateTime(2026, 12, 25) && f.Name == "1. Weihnachtstag");
        feiertage.Should().Contain(f => f.Date == new DateTime(2026, 12, 26) && f.Name == "2. Weihnachtstag");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Ostern-Algorithmus
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateHolidays_2026_OsterndatumKorrekt()
    {
        // Vorbereitung: Ostern 2026 fällt auf den 5. April
        var db = ErstelleDbMockFuerRegion("DE-BY");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2026, "DE-BY");

        // Prüfung: Ostermontag = Ostersonntag + 1 Tag = 6. April 2026
        feiertage.Should().Contain(f => f.Name == "Ostermontag" && f.Date == new DateTime(2026, 4, 6));
    }

    [Fact]
    public void CalculateHolidays_2025_OsterndatumKorrekt()
    {
        // Vorbereitung: Ostern 2025 fällt auf den 20. April
        var db = ErstelleDbMockFuerRegion("DE-BY");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2025, "DE-BY");

        // Prüfung: Karfreitag = Ostersonntag - 2 = 18. April 2025
        feiertage.Should().Contain(f => f.Name == "Karfreitag" && f.Date == new DateTime(2025, 4, 18));
    }

    [Fact]
    public void CalculateHolidays_2024_HimmelfahrtKorrekt()
    {
        // Vorbereitung: Ostern 2024 = 31. März → Himmelfahrt = +39 Tage = 9. Mai
        var db = ErstelleDbMockFuerRegion("DE-BE");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2024, "DE-BE");

        // Prüfung
        feiertage.Should().Contain(f => f.Name == "Christi Himmelfahrt" && f.Date == new DateTime(2024, 5, 9));
    }

    [Fact]
    public void CalculateHolidays_2024_PfingstmontagKorrekt()
    {
        // Vorbereitung: Ostern 2024 = 31. März → Pfingstmontag = +50 Tage = 20. Mai
        var db = ErstelleDbMockFuerRegion("DE-NI");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2024, "DE-NI");

        // Prüfung
        feiertage.Should().Contain(f => f.Name == "Pfingstmontag" && f.Date == new DateTime(2024, 5, 20));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Regionale Feiertage
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateHolidays_BayernHeiligeDreiKoenige_Enthalten()
    {
        // Vorbereitung: Bayern hat Heilige Drei Könige (6.1.)
        var db = ErstelleDbMockFuerRegion("DE-BY");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2026, "DE-BY");

        // Prüfung
        feiertage.Should().Contain(f => f.Name == "Heilige Drei Könige" && f.Date.Month == 1 && f.Date.Day == 6);
    }

    [Fact]
    public void CalculateHolidays_BerlinFrauentag_Enthalten()
    {
        // Vorbereitung: Berlin hat Internationalen Frauentag (8.3.)
        var db = ErstelleDbMockFuerRegion("DE-BE");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2026, "DE-BE");

        // Prüfung
        feiertage.Should().Contain(f => f.Name == "Internationaler Frauentag" && f.Date.Month == 3 && f.Date.Day == 8);
    }

    [Fact]
    public void CalculateHolidays_BerlinKeinAllerheiligen_NichtEnthalten()
    {
        // Vorbereitung: Berlin hat NICHT Allerheiligen (nur BY, BW, NW, RP, SL)
        var db = ErstelleDbMockFuerRegion("DE-BE");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2026, "DE-BE");

        // Prüfung
        feiertage.Should().NotContain(f => f.Name == "Allerheiligen");
    }

    [Fact]
    public void CalculateHolidays_SachsenBussUndBettag_Enthalten()
    {
        // Vorbereitung: Sachsen hat Buß- und Bettag
        var db = ErstelleDbMockFuerRegion("DE-SN");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2026, "DE-SN");

        // Prüfung
        feiertage.Should().Contain(f => f.Name == "Buß- und Bettag");
    }

    [Fact]
    public void CalculateHolidays_SachsenBussUndBettag2026_IstMittwoch()
    {
        // Vorbereitung: Buß- und Bettag = Mittwoch vor dem 23. November
        var db = ErstelleDbMockFuerRegion("DE-SN");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2026, "DE-SN");
        var bussUndBettag = feiertage.First(f => f.Name == "Buß- und Bettag");

        // Prüfung: Muss ein Mittwoch sein und vor dem 23. November liegen
        bussUndBettag.Date.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
        bussUndBettag.Date.Should().BeBefore(new DateTime(2026, 11, 23));
    }

    [Fact]
    public void CalculateHolidays_ThueringenWeltkindertag_Enthalten()
    {
        // Vorbereitung: Thüringen hat Weltkindertag (20.9.)
        var db = ErstelleDbMockFuerRegion("DE-TH");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2026, "DE-TH");

        // Prüfung
        feiertage.Should().Contain(f => f.Name == "Weltkindertag" && f.Date.Month == 9 && f.Date.Day == 20);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Reihenfolge und Vollständigkeit
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CalculateHolidays_Ergebnis_IstChronologischSortiert()
    {
        // Vorbereitung
        var db = ErstelleDbMockFuerRegion("DE-BY");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2026, "DE-BY");

        // Prüfung: Jeder Feiertag muss vor dem nächsten liegen
        feiertage.Should().BeInAscendingOrder(f => f.Date);
    }

    [Fact]
    public void CalculateHolidays_Bayern_HatMindestensNeunFeiertage()
    {
        // Vorbereitung: Bayern hat 5 nationale + 4 regionale = mind. 9 gesetzliche Feiertage
        var db = ErstelleDbMockFuerRegion("DE-BY");
        var sut = new HolidayService(db);

        // Ausführung
        var feiertage = sut.CalculateHolidays(2026, "DE-BY");

        // Prüfung
        feiertage.Should().HaveCountGreaterThanOrEqualTo(9);
    }

    [Fact]
    public void GetAvailableRegions_Gibt16Bundeslaender()
    {
        // Vorbereitung
        var db = ErstelleDbMockFuerRegion("DE-BY");
        var sut = new HolidayService(db);

        // Ausführung
        var regionen = sut.GetAvailableRegions();

        // Prüfung: 16 Bundesländer
        regionen.Should().HaveCount(16);
    }

    [Fact]
    public void GetAvailableRegions_AlleRegionenHabenDEPräfix()
    {
        // Vorbereitung
        var db = ErstelleDbMockFuerRegion("DE-BY");
        var sut = new HolidayService(db);

        // Ausführung
        var regionen = sut.GetAvailableRegions();

        // Prüfung: Alle Codes beginnen mit "DE-"
        regionen.Should().AllSatisfy(r => r.Code.Should().StartWith("DE-"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Cache-Verhalten
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ClearCache_NachClearCache_BerechnetNeu()
    {
        // Vorbereitung: Cache befüllen, dann leeren
        var db = ErstelleDbMockFuerRegion("DE-BY");
        var sut = new HolidayService(db);
        var ersteAbfrage = sut.CalculateHolidays(2026, "DE-BY");

        // Ausführung: Cache leeren und erneut abfragen
        sut.ClearCache();
        var zweiteAbfrage = sut.CalculateHolidays(2026, "DE-BY");

        // Prüfung: Gleiche Anzahl und Daten (korrekte Neuberechnung)
        zweiteAbfrage.Should().HaveCount(ersteAbfrage.Count);
        zweiteAbfrage.Select(f => f.Date).Should().BeEquivalentTo(ersteAbfrage.Select(f => f.Date));
    }
}
