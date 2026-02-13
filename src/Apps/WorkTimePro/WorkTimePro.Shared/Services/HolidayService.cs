using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Holiday service with calculation for all German states
/// </summary>
public class HolidayService : IHolidayService
{
    private readonly IDatabaseService _database;
    private readonly Dictionary<int, List<HolidayEntry>> _cache = new();
    private string? _cachedRegion;

    public HolidayService(IDatabaseService database)
    {
        _database = database;
    }

    /// <summary>
    /// Cache invalidieren (z.B. bei Region-Wechsel in Settings)
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _cachedRegion = null;
    }

    public async Task<List<HolidayEntry>> GetHolidaysAsync(int year)
    {
        var settings = await _database.GetSettingsAsync();
        return await Task.FromResult(GetHolidaysForRegion(year, settings.HolidayRegion));
    }

    public async Task<List<HolidayEntry>> GetHolidaysAsync(DateTime start, DateTime end)
    {
        var settings = await _database.GetSettingsAsync();
        var holidays = new List<HolidayEntry>();

        for (int year = start.Year; year <= end.Year; year++)
        {
            var yearHolidays = GetHolidaysForRegion(year, settings.HolidayRegion);
            holidays.AddRange(yearHolidays.Where(h => h.Date >= start && h.Date <= end));
        }

        return holidays;
    }

    public async Task<HolidayEntry?> GetHolidayForDateAsync(DateTime date)
    {
        var holidays = await GetHolidaysAsync(date.Year);
        return holidays.FirstOrDefault(h => h.Date.Date == date.Date);
    }

    public List<HolidayEntry> CalculateHolidays(int year, string region)
    {
        return GetHolidaysForRegion(year, region);
    }

    public List<(string Code, string Name)> GetAvailableRegions()
    {
        return new List<(string, string)>
        {
            ("DE-BW", "Baden-Württemberg"),
            ("DE-BY", "Bayern"),
            ("DE-BE", "Berlin"),
            ("DE-BB", "Brandenburg"),
            ("DE-HB", "Bremen"),
            ("DE-HH", "Hamburg"),
            ("DE-HE", "Hessen"),
            ("DE-MV", "Mecklenburg-Vorpommern"),
            ("DE-NI", "Niedersachsen"),
            ("DE-NW", "Nordrhein-Westfalen"),
            ("DE-RP", "Rheinland-Pfalz"),
            ("DE-SL", "Saarland"),
            ("DE-SN", "Sachsen"),
            ("DE-ST", "Sachsen-Anhalt"),
            ("DE-SH", "Schleswig-Holstein"),
            ("DE-TH", "Thüringen")
        };
    }

    #region Private Methods

    private List<HolidayEntry> GetHolidaysForRegion(int year, string region)
    {
        // Cache invalidieren wenn Region gewechselt wurde
        if (_cachedRegion != null && _cachedRegion != region)
        {
            _cache.Clear();
        }
        _cachedRegion = region;

        var cacheKey = year * 100 + GetRegionIndex(region);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var holidays = CalculateGermanHolidays(year, region);
        _cache[cacheKey] = holidays;
        return holidays;
    }

    private static int GetRegionIndex(string region)
    {
        return region switch
        {
            "DE-BW" => 1, "DE-BY" => 2, "DE-BE" => 3, "DE-BB" => 4,
            "DE-HB" => 5, "DE-HH" => 6, "DE-HE" => 7, "DE-MV" => 8,
            "DE-NI" => 9, "DE-NW" => 10, "DE-RP" => 11, "DE-SL" => 12,
            "DE-SN" => 13, "DE-ST" => 14, "DE-SH" => 15, "DE-TH" => 16,
            _ => 0
        };
    }

    private static List<HolidayEntry> CalculateGermanHolidays(int year, string region)
    {
        var holidays = new List<HolidayEntry>();

        // Fixed national holidays
        holidays.Add(new HolidayEntry
        {
            Date = new DateTime(year, 1, 1),
            Name = "Neujahr",
            IsNational = true
        });

        holidays.Add(new HolidayEntry
        {
            Date = new DateTime(year, 5, 1),
            Name = "Tag der Arbeit",
            IsNational = true
        });

        holidays.Add(new HolidayEntry
        {
            Date = new DateTime(year, 10, 3),
            Name = "Tag der Deutschen Einheit",
            IsNational = true
        });

        holidays.Add(new HolidayEntry
        {
            Date = new DateTime(year, 12, 25),
            Name = "1. Weihnachtstag",
            IsNational = true
        });

        holidays.Add(new HolidayEntry
        {
            Date = new DateTime(year, 12, 26),
            Name = "2. Weihnachtstag",
            IsNational = true
        });

        // Easter calculation (Gaussian Easter formula)
        var easter = CalculateEaster(year);

        // Movable national holidays
        holidays.Add(new HolidayEntry
        {
            Date = easter.AddDays(-2),
            Name = "Karfreitag",
            IsNational = true
        });

        holidays.Add(new HolidayEntry
        {
            Date = easter.AddDays(1),
            Name = "Ostermontag",
            IsNational = true
        });

        holidays.Add(new HolidayEntry
        {
            Date = easter.AddDays(39),
            Name = "Christi Himmelfahrt",
            IsNational = true
        });

        holidays.Add(new HolidayEntry
        {
            Date = easter.AddDays(50),
            Name = "Pfingstmontag",
            IsNational = true
        });

        // Regional holidays
        switch (region)
        {
            case "DE-BW":
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 1, 6), Name = "Heilige Drei Könige" });
                holidays.Add(new HolidayEntry { Date = easter.AddDays(60), Name = "Fronleichnam" });
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 11, 1), Name = "Allerheiligen" });
                break;

            case "DE-BY":
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 1, 6), Name = "Heilige Drei Könige" });
                holidays.Add(new HolidayEntry { Date = easter.AddDays(60), Name = "Fronleichnam" });
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 8, 15), Name = "Mariä Himmelfahrt" });
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 11, 1), Name = "Allerheiligen" });
                break;

            case "DE-BE":
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 3, 8), Name = "Internationaler Frauentag" });
                break;

            case "DE-BB":
                holidays.Add(new HolidayEntry { Date = easter, Name = "Ostersonntag" });
                holidays.Add(new HolidayEntry { Date = easter.AddDays(49), Name = "Pfingstsonntag" });
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 10, 31), Name = "Reformationstag" });
                break;

            case "DE-HB":
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 10, 31), Name = "Reformationstag" });
                break;

            case "DE-HH":
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 10, 31), Name = "Reformationstag" });
                break;

            case "DE-HE":
                holidays.Add(new HolidayEntry { Date = easter.AddDays(60), Name = "Fronleichnam" });
                break;

            case "DE-MV":
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 10, 31), Name = "Reformationstag" });
                break;

            case "DE-NI":
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 10, 31), Name = "Reformationstag" });
                break;

            case "DE-NW":
                holidays.Add(new HolidayEntry { Date = easter.AddDays(60), Name = "Fronleichnam" });
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 11, 1), Name = "Allerheiligen" });
                break;

            case "DE-RP":
                holidays.Add(new HolidayEntry { Date = easter.AddDays(60), Name = "Fronleichnam" });
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 11, 1), Name = "Allerheiligen" });
                break;

            case "DE-SL":
                holidays.Add(new HolidayEntry { Date = easter.AddDays(60), Name = "Fronleichnam" });
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 8, 15), Name = "Mariä Himmelfahrt" });
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 11, 1), Name = "Allerheiligen" });
                break;

            case "DE-SN":
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 10, 31), Name = "Reformationstag" });
                holidays.Add(new HolidayEntry { Date = CalculateBussUndBettag(year), Name = "Buß- und Bettag" });
                break;

            case "DE-ST":
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 1, 6), Name = "Heilige Drei Könige" });
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 10, 31), Name = "Reformationstag" });
                break;

            case "DE-SH":
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 10, 31), Name = "Reformationstag" });
                break;

            case "DE-TH":
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 10, 31), Name = "Reformationstag" });
                holidays.Add(new HolidayEntry { Date = new DateTime(year, 9, 20), Name = "Weltkindertag" });
                break;
        }

        return holidays.OrderBy(h => h.Date).ToList();
    }

    /// <summary>
    /// Calculate Easter date using the Gaussian Easter formula
    /// </summary>
    private static DateTime CalculateEaster(int year)
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
    /// Calculate Buss- und Bettag (Wednesday before November 23rd)
    /// </summary>
    private static DateTime CalculateBussUndBettag(int year)
    {
        var nov23 = new DateTime(year, 11, 23);
        var dayOfWeek = (int)nov23.DayOfWeek;

        int daysBack = dayOfWeek >= 3 ? dayOfWeek - 3 : dayOfWeek + 4;
        return nov23.AddDays(-daysBack);
    }

    #endregion
}
