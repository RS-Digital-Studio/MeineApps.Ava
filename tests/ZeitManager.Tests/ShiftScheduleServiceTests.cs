using FluentAssertions;
using NSubstitute;
using Xunit;
using ZeitManager.Models;
using ZeitManager.Services;

namespace ZeitManager.Tests;

/// <summary>
/// Tests fuer ShiftScheduleService - Schichtplan-Berechnungslogik.
/// Schwerpunkt auf GetShiftForDate(): 15-Schicht und 21-Schicht Muster.
/// </summary>
public class ShiftScheduleServiceTests
{
    // IDatabaseService wird gemockt - wir testen nur die Berechnungslogik
    private readonly IDatabaseService _databaseMock = Substitute.For<IDatabaseService>();
    private readonly ShiftScheduleService _sut;

    public ShiftScheduleServiceTests()
    {
        _sut = new ShiftScheduleService(_databaseMock);
    }

    // Hilfsmethode: Erstellt einen ShiftSchedule mit definiertem Startdatum
    private static ShiftSchedule ErstelloFifteenShiftPlan(DateOnly startDatum, int gruppe = 1) =>
        new()
        {
            Name = "Test 15-Schicht",
            PatternType = ShiftPatternType.FifteenShift,
            ShiftGroupNumber = gruppe,
            StartDateValue = startDatum
        };

    private static ShiftSchedule ErstelleTwentyOneShiftPlan(DateOnly startDatum, int gruppe = 1) =>
        new()
        {
            Name = "Test 21-Schicht",
            PatternType = ShiftPatternType.TwentyOneShift,
            ShiftGroupNumber = gruppe,
            StartDateValue = startDatum
        };

    #region 15-Schicht Tests (Gruppe 1)

    [Fact]
    public void GetShiftForDate_15Schicht_Gruppe1_ErsteMontag_Fruehschicht()
    {
        // Gruppe 1, Woche 0: cycleWeek = (0 + 0) % 3 = 0 → Früh
        var startMontag = new DateOnly(2026, 1, 5); // Montag
        var plan = ErstelloFifteenShiftPlan(startMontag, gruppe: 1);

        var ergebnis = _sut.GetShiftForDate(plan, startMontag);

        ergebnis.Should().Be(ShiftType.Early);
    }

    [Fact]
    public void GetShiftForDate_15Schicht_Gruppe1_ZweiteWocheMontag_Spaetschicht()
    {
        // Woche 1: cycleWeek = (1 + 0) % 3 = 1 → Spät
        var startMontag = new DateOnly(2026, 1, 5);
        var plan = ErstelloFifteenShiftPlan(startMontag, gruppe: 1);
        var zweiteWocheMontag = startMontag.AddDays(7);

        var ergebnis = _sut.GetShiftForDate(plan, zweiteWocheMontag);

        ergebnis.Should().Be(ShiftType.Late);
    }

    [Fact]
    public void GetShiftForDate_15Schicht_Gruppe1_DritteWocheMontag_Nachtschicht()
    {
        // Woche 2: cycleWeek = (2 + 0) % 3 = 2 → Nacht
        var startMontag = new DateOnly(2026, 1, 5);
        var plan = ErstelloFifteenShiftPlan(startMontag, gruppe: 1);
        var dritteWocheMontag = startMontag.AddDays(14);

        var ergebnis = _sut.GetShiftForDate(plan, dritteWocheMontag);

        ergebnis.Should().Be(ShiftType.Night);
    }

    [Fact]
    public void GetShiftForDate_15Schicht_VierteWocheMontag_ZyklusWiederholt_Fruehschicht()
    {
        // Woche 3: cycleWeek = (3 + 0) % 3 = 0 → Früh (Zyklus wiederholt)
        var startMontag = new DateOnly(2026, 1, 5);
        var plan = ErstelloFifteenShiftPlan(startMontag, gruppe: 1);
        var vierteWocheMontag = startMontag.AddDays(21);

        var ergebnis = _sut.GetShiftForDate(plan, vierteWocheMontag);

        ergebnis.Should().Be(ShiftType.Early);
    }

    [Fact]
    public void GetShiftForDate_15Schicht_Samstag_FreierTag()
    {
        // 15-Schicht: Nur Mo-Fr (Tage 0-4), Sa+So = frei
        var startMontag = new DateOnly(2026, 1, 5);
        var plan = ErstelloFifteenShiftPlan(startMontag, gruppe: 1);
        var samstag = new DateOnly(2026, 1, 10); // 5 Tage nach Start = Samstag

        var ergebnis = _sut.GetShiftForDate(plan, samstag);

        ergebnis.Should().Be(ShiftType.Free);
    }

    [Fact]
    public void GetShiftForDate_15Schicht_Sonntag_FreierTag()
    {
        var startMontag = new DateOnly(2026, 1, 5);
        var plan = ErstelloFifteenShiftPlan(startMontag, gruppe: 1);
        var sonntag = new DateOnly(2026, 1, 11); // 6 Tage nach Start = Sonntag

        var ergebnis = _sut.GetShiftForDate(plan, sonntag);

        ergebnis.Should().Be(ShiftType.Free);
    }

    [Fact]
    public void GetShiftForDate_15Schicht_Gruppe2_VersatztUmEineWoche()
    {
        // Gruppe 2: cycleWeek = (weekNumber + 1) % 3
        // Woche 0 Gruppe 2: cycleWeek = 1 → Spät (während Gruppe 1 Früh hat)
        var startMontag = new DateOnly(2026, 1, 5);
        var planGruppe1 = ErstelloFifteenShiftPlan(startMontag, gruppe: 1);
        var planGruppe2 = ErstelloFifteenShiftPlan(startMontag, gruppe: 2);

        var schichtGruppe1 = _sut.GetShiftForDate(planGruppe1, startMontag);
        var schichtGruppe2 = _sut.GetShiftForDate(planGruppe2, startMontag);

        // Gruppen müssen verschiedene Schichten haben
        schichtGruppe1.Should().NotBe(schichtGruppe2);
        schichtGruppe1.Should().Be(ShiftType.Early);
        schichtGruppe2.Should().Be(ShiftType.Late);
    }

    [Fact]
    public void GetShiftForDate_15Schicht_Gruppe3_ZweiWochenVersatzt()
    {
        // Gruppe 3, Woche 0: cycleWeek = (0 + 2) % 3 = 2 → Nacht
        var startMontag = new DateOnly(2026, 1, 5);
        var plan = ErstelloFifteenShiftPlan(startMontag, gruppe: 3);

        var ergebnis = _sut.GetShiftForDate(plan, startMontag);

        ergebnis.Should().Be(ShiftType.Night);
    }

    #endregion

    #region 21-Schicht Tests

    [Fact]
    public void GetShiftForDate_21Schicht_Gruppe1_Tag0_Fruehschicht()
    {
        // dayInCycle = (0 + 0) % 10 = 0 → Early
        var start = new DateOnly(2026, 1, 5);
        var plan = ErstelleTwentyOneShiftPlan(start, gruppe: 1);

        var ergebnis = _sut.GetShiftForDate(plan, start);

        ergebnis.Should().Be(ShiftType.Early);
    }

    [Fact]
    public void GetShiftForDate_21Schicht_Gruppe1_Tag1_NochFruehschicht()
    {
        // dayInCycle = 1 → Early (2 Frühtage)
        var start = new DateOnly(2026, 1, 5);
        var plan = ErstelleTwentyOneShiftPlan(start, gruppe: 1);

        var ergebnis = _sut.GetShiftForDate(plan, start.AddDays(1));

        ergebnis.Should().Be(ShiftType.Early);
    }

    [Fact]
    public void GetShiftForDate_21Schicht_Gruppe1_Tag2_Spaetschicht()
    {
        // dayInCycle = 2 → Late
        var start = new DateOnly(2026, 1, 5);
        var plan = ErstelleTwentyOneShiftPlan(start, gruppe: 1);

        var ergebnis = _sut.GetShiftForDate(plan, start.AddDays(2));

        ergebnis.Should().Be(ShiftType.Late);
    }

    [Fact]
    public void GetShiftForDate_21Schicht_Gruppe1_Tag4_Nachtschicht()
    {
        // dayInCycle = 4 → Night
        var start = new DateOnly(2026, 1, 5);
        var plan = ErstelleTwentyOneShiftPlan(start, gruppe: 1);

        var ergebnis = _sut.GetShiftForDate(plan, start.AddDays(4));

        ergebnis.Should().Be(ShiftType.Night);
    }

    [Fact]
    public void GetShiftForDate_21Schicht_Gruppe1_Tag6Bis9_Frei()
    {
        // dayInCycle 6-9 = Free (4 Freitage)
        var start = new DateOnly(2026, 1, 5);
        var plan = ErstelleTwentyOneShiftPlan(start, gruppe: 1);

        for (int i = 6; i <= 9; i++)
        {
            var ergebnis = _sut.GetShiftForDate(plan, start.AddDays(i));
            ergebnis.Should().Be(ShiftType.Free, $"Tag {i} sollte frei sein");
        }
    }

    [Fact]
    public void GetShiftForDate_21Schicht_Gruppe1_Tag10_ZyklusWiederholt_Fruehschicht()
    {
        // dayInCycle = 10 % 10 = 0 → Early (Zyklus wiederholt)
        var start = new DateOnly(2026, 1, 5);
        var plan = ErstelleTwentyOneShiftPlan(start, gruppe: 1);

        var ergebnis = _sut.GetShiftForDate(plan, start.AddDays(10));

        ergebnis.Should().Be(ShiftType.Early);
    }

    [Fact]
    public void GetShiftForDate_21Schicht_GruppenHabenVerschiedeneSchichten()
    {
        // Gruppen sind um 2 Tage versetzt: Gruppe 1 Früh ≠ Gruppe 2 Früh am selben Tag
        var start = new DateOnly(2026, 1, 5);
        var plan1 = ErstelleTwentyOneShiftPlan(start, gruppe: 1);
        var plan2 = ErstelleTwentyOneShiftPlan(start, gruppe: 2);

        var schicht1 = _sut.GetShiftForDate(plan1, start);
        var schicht2 = _sut.GetShiftForDate(plan2, start);

        // Gruppe 1 Tag 0: dayInCycle=0 → Early
        // Gruppe 2 Tag 0: dayInCycle=(0+2)%10=2 → Late
        schicht1.Should().Be(ShiftType.Early);
        schicht2.Should().Be(ShiftType.Late);
    }

    #endregion

    #region Ausnahmen-Tests

    [Fact]
    public void GetShiftForDate_MitUrlaub_AusnahmeUeberschreibtRegelplandienst()
    {
        // Ausnahme soll den normalen Schichtplan überschreiben
        var start = new DateOnly(2026, 1, 5);
        var plan = ErstelloFifteenShiftPlan(start, gruppe: 1);
        var urlaubstag = start; // Montag wäre normalerweise Frühschicht

        var ausnahmen = new List<ShiftException>
        {
            new()
            {
                ShiftScheduleId = plan.Id,
                DateValue = urlaubstag,
                ExceptionType = ExceptionType.Vacation,
                NewShiftType = ShiftType.Vacation
            }
        };

        var ergebnis = _sut.GetShiftForDate(plan, urlaubstag, ausnahmen);

        ergebnis.Should().Be(ShiftType.Vacation);
    }

    [Fact]
    public void GetShiftForDate_AusnahmeAufAnderemDatum_OriginalSchichtBleibt()
    {
        // Ausnahme gilt nur für ihr spezifisches Datum, nicht für andere Tage
        var start = new DateOnly(2026, 1, 5);
        var plan = ErstelloFifteenShiftPlan(start, gruppe: 1);

        var ausnahmen = new List<ShiftException>
        {
            new()
            {
                DateValue = start.AddDays(1), // Dienstag
                ExceptionType = ExceptionType.Sick,
                NewShiftType = ShiftType.Sick
            }
        };

        // Montag ist nicht betroffen
        var ergebnis = _sut.GetShiftForDate(plan, start, ausnahmen);

        ergebnis.Should().Be(ShiftType.Early);
    }

    [Fact]
    public void GetShiftForDate_AusnahmeOhneNewShiftType_GibtFreiZurueck()
    {
        // NewShiftType = null → Fallback auf Free
        var start = new DateOnly(2026, 1, 5);
        var plan = ErstelloFifteenShiftPlan(start, gruppe: 1);

        var ausnahmen = new List<ShiftException>
        {
            new()
            {
                DateValue = start,
                ExceptionType = ExceptionType.Other,
                NewShiftType = null
            }
        };

        var ergebnis = _sut.GetShiftForDate(plan, start, ausnahmen);

        ergebnis.Should().Be(ShiftType.Free);
    }

    [Fact]
    public void GetShiftForDate_LeereAusnahmenListe_VerwendetNormalenPlan()
    {
        var start = new DateOnly(2026, 1, 5);
        var plan = ErstelloFifteenShiftPlan(start, gruppe: 1);

        var ergebnis = _sut.GetShiftForDate(plan, start, new List<ShiftException>());

        ergebnis.Should().Be(ShiftType.Early);
    }

    #endregion

    #region MaxGroupNumber Tests

    [Fact]
    public void MaxGroupNumber_15Schicht_Gibt3Zurueck()
    {
        var plan = new ShiftSchedule { PatternType = ShiftPatternType.FifteenShift };

        plan.MaxGroupNumber.Should().Be(3);
    }

    [Fact]
    public void MaxGroupNumber_21Schicht_Gibt5Zurueck()
    {
        var plan = new ShiftSchedule { PatternType = ShiftPatternType.TwentyOneShift };

        plan.MaxGroupNumber.Should().Be(5);
    }

    [Fact]
    public void MaxGroupNumber_Custom_Gibt10Zurueck()
    {
        var plan = new ShiftSchedule { PatternType = ShiftPatternType.Custom };

        plan.MaxGroupNumber.Should().Be(10);
    }

    #endregion

    #region ShiftSchedule Computed Properties Tests

    [Fact]
    public void StartDateValue_SetzenUndLesen_RoundtripKorrekt()
    {
        // DateOnly-Roundtrip über String-Persistenz prüfen
        var datum = new DateOnly(2026, 6, 15);
        var plan = new ShiftSchedule();

        plan.StartDateValue = datum;

        plan.StartDateValue.Should().Be(datum);
    }

    [Fact]
    public void EarlyShiftWakeTime_SetzenUndLesen_RoundtripKorrekt()
    {
        var weckzeit = new TimeOnly(5, 30);
        var plan = new ShiftSchedule();

        plan.EarlyShiftWakeTime = weckzeit;

        plan.EarlyShiftWakeTime.Should().Be(weckzeit);
    }

    #endregion
}
