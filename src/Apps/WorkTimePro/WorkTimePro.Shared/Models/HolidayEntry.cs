using SQLite;

namespace WorkTimePro.Models;

/// <summary>
/// Ein gesetzlicher Feiertag
/// </summary>
[Table("Holidays")]
public class HolidayEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Datum des Feiertags
    /// </summary>
    [Indexed]
    public DateTime Date { get; set; }

    /// <summary>
    /// Name des Feiertags (lokalisiert)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Region/Bundesland (z.B. "DE-BY")
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Ist bundesweit?
    /// </summary>
    public bool IsNational { get; set; }

    /// <summary>
    /// Jahr
    /// </summary>
    [Indexed]
    public int Year { get; set; }

    // === Berechnete Properties ===

    /// <summary>
    /// Formatiertes Datum
    /// </summary>
    [Ignore]
    public string DateDisplay => Date.ToString("d");

    /// <summary>
    /// Wochentag
    /// </summary>
    [Ignore]
    public string WeekdayDisplay => Date.ToString("dddd");

    /// <summary>
    /// Icon
    /// </summary>
    [Ignore]
    public string Icon => Helpers.Icons.PartyPopper;
}

/// <summary>
/// Deutsche Feiertage Berechnung (statische Klasse)
/// </summary>
public static class GermanHolidays
{
    /// <summary>
    /// Berechnet alle Feiertage für ein Jahr und Bundesland
    /// </summary>
    public static List<HolidayEntry> GetHolidays(int year, GermanState state)
    {
        var holidays = new List<HolidayEntry>();
        var stateCode = $"DE-{state}";

        // Feste Feiertage (bundesweit)
        AddHoliday(holidays, new DateTime(year, 1, 1), "Neujahr", stateCode, true, year);
        AddHoliday(holidays, new DateTime(year, 5, 1), "Tag der Arbeit", stateCode, true, year);
        AddHoliday(holidays, new DateTime(year, 10, 3), "Tag der Deutschen Einheit", stateCode, true, year);
        AddHoliday(holidays, new DateTime(year, 12, 25), "1. Weihnachtsfeiertag", stateCode, true, year);
        AddHoliday(holidays, new DateTime(year, 12, 26), "2. Weihnachtsfeiertag", stateCode, true, year);

        // Bewegliche Feiertage (Ostersonntag-basiert)
        var easterSunday = CalculateEasterSunday(year);
        AddHoliday(holidays, easterSunday.AddDays(-2), "Karfreitag", stateCode, true, year);
        AddHoliday(holidays, easterSunday.AddDays(1), "Ostermontag", stateCode, true, year);
        AddHoliday(holidays, easterSunday.AddDays(39), "Christi Himmelfahrt", stateCode, true, year);
        AddHoliday(holidays, easterSunday.AddDays(50), "Pfingstmontag", stateCode, true, year);

        // Regionale Feiertage
        switch (state)
        {
            case GermanState.BW:
                AddHoliday(holidays, new DateTime(year, 1, 6), "Heilige Drei Könige", stateCode, false, year);
                AddHoliday(holidays, easterSunday.AddDays(60), "Fronleichnam", stateCode, false, year);
                AddHoliday(holidays, new DateTime(year, 11, 1), "Allerheiligen", stateCode, false, year);
                break;

            case GermanState.BY:
                AddHoliday(holidays, new DateTime(year, 1, 6), "Heilige Drei Könige", stateCode, false, year);
                AddHoliday(holidays, easterSunday.AddDays(60), "Fronleichnam", stateCode, false, year);
                AddHoliday(holidays, new DateTime(year, 8, 15), "Mariä Himmelfahrt", stateCode, false, year);
                AddHoliday(holidays, new DateTime(year, 11, 1), "Allerheiligen", stateCode, false, year);
                break;

            case GermanState.BE:
                AddHoliday(holidays, new DateTime(year, 3, 8), "Internationaler Frauentag", stateCode, false, year);
                break;

            case GermanState.BB:
                AddHoliday(holidays, easterSunday, "Ostersonntag", stateCode, false, year);
                AddHoliday(holidays, easterSunday.AddDays(49), "Pfingstsonntag", stateCode, false, year);
                AddHoliday(holidays, new DateTime(year, 10, 31), "Reformationstag", stateCode, false, year);
                break;

            case GermanState.HB:
                AddHoliday(holidays, new DateTime(year, 10, 31), "Reformationstag", stateCode, false, year);
                break;

            case GermanState.HH:
                AddHoliday(holidays, new DateTime(year, 10, 31), "Reformationstag", stateCode, false, year);
                break;

            case GermanState.HE:
                AddHoliday(holidays, easterSunday.AddDays(60), "Fronleichnam", stateCode, false, year);
                break;

            case GermanState.MV:
                AddHoliday(holidays, new DateTime(year, 10, 31), "Reformationstag", stateCode, false, year);
                break;

            case GermanState.NI:
                AddHoliday(holidays, new DateTime(year, 10, 31), "Reformationstag", stateCode, false, year);
                break;

            case GermanState.NW:
                AddHoliday(holidays, easterSunday.AddDays(60), "Fronleichnam", stateCode, false, year);
                AddHoliday(holidays, new DateTime(year, 11, 1), "Allerheiligen", stateCode, false, year);
                break;

            case GermanState.RP:
                AddHoliday(holidays, easterSunday.AddDays(60), "Fronleichnam", stateCode, false, year);
                AddHoliday(holidays, new DateTime(year, 11, 1), "Allerheiligen", stateCode, false, year);
                break;

            case GermanState.SL:
                AddHoliday(holidays, easterSunday.AddDays(60), "Fronleichnam", stateCode, false, year);
                AddHoliday(holidays, new DateTime(year, 8, 15), "Mariä Himmelfahrt", stateCode, false, year);
                AddHoliday(holidays, new DateTime(year, 11, 1), "Allerheiligen", stateCode, false, year);
                break;

            case GermanState.SN:
                AddHoliday(holidays, new DateTime(year, 10, 31), "Reformationstag", stateCode, false, year);
                AddHoliday(holidays, CalculateBussUndBettag(year), "Buß- und Bettag", stateCode, false, year);
                break;

            case GermanState.ST:
                AddHoliday(holidays, new DateTime(year, 1, 6), "Heilige Drei Könige", stateCode, false, year);
                AddHoliday(holidays, new DateTime(year, 10, 31), "Reformationstag", stateCode, false, year);
                break;

            case GermanState.SH:
                AddHoliday(holidays, new DateTime(year, 10, 31), "Reformationstag", stateCode, false, year);
                break;

            case GermanState.TH:
                AddHoliday(holidays, new DateTime(year, 9, 20), "Weltkindertag", stateCode, false, year);
                AddHoliday(holidays, new DateTime(year, 10, 31), "Reformationstag", stateCode, false, year);
                break;
        }

        return holidays.OrderBy(h => h.Date).ToList();
    }

    private static void AddHoliday(List<HolidayEntry> list, DateTime date, string name, string region, bool isNational, int year)
    {
        list.Add(new HolidayEntry
        {
            Date = date,
            Name = name,
            Region = region,
            IsNational = isNational,
            Year = year
        });
    }

    /// <summary>
    /// Berechnet Ostersonntag nach Gaußscher Osterformel
    /// </summary>
    public static DateTime CalculateEasterSunday(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;

        return new DateTime(year, month, day);
    }

    /// <summary>
    /// Berechnet Buß- und Bettag (Mittwoch vor dem letzten Sonntag im Kirchenjahr)
    /// </summary>
    public static DateTime CalculateBussUndBettag(int year)
    {
        // 11 Tage vor dem 1. Advent
        // 1. Advent = 4. Sonntag vor Weihnachten
        var christmas = new DateTime(year, 12, 25);
        var daysUntilSunday = ((int)christmas.DayOfWeek + 6) % 7 + 1;
        var advent1 = christmas.AddDays(-daysUntilSunday - 21);
        return advent1.AddDays(-11);
    }
}
