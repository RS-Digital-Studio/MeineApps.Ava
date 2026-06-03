using WorkTimePro.Models;

namespace WorkTimePro.Services;

/// <summary>
/// Feiertagsberechnung für Deutschland (16 Bundesländer), Österreich (9 Bundesländer) und Schweiz (12 Kantone)
/// </summary>
public sealed class HolidayService : IHolidayService
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
            // Deutschland (16 Bundesländer)
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
            ("DE-TH", "Thüringen"),

            // Österreich (9 Bundesländer)
            ("AT-1", "Burgenland"),
            ("AT-2", "Kärnten"),
            ("AT-3", "Niederösterreich"),
            ("AT-4", "Oberösterreich"),
            ("AT-5", "Salzburg"),
            ("AT-6", "Steiermark"),
            ("AT-7", "Tirol"),
            ("AT-8", "Vorarlberg"),
            ("AT-9", "Wien"),

            // Schweiz (12 Kantone)
            ("CH-ZH", "Zürich"),
            ("CH-BE", "Bern"),
            ("CH-LU", "Luzern"),
            ("CH-SG", "St. Gallen"),
            ("CH-AG", "Aargau"),
            ("CH-BS", "Basel-Stadt"),
            ("CH-BL", "Basel-Landschaft"),
            ("CH-TI", "Tessin"),
            ("CH-GE", "Genf"),
            ("CH-VD", "Waadt"),
            ("CH-VS", "Wallis"),
            ("CH-GR", "Graubünden")
        };
    }


    private List<HolidayEntry> GetHolidaysForRegion(int year, string region)
    {
        // Cache invalidieren wenn Region gewechselt wurde
        if (_cachedRegion != null && _cachedRegion != region)
        {
            _cache.Clear();
        }
        _cachedRegion = region;

        var cacheKey = year * 100 + HolidayCalculator.GetRegionIndex(region);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // Reine Berechnung in HolidayCalculator ausgelagert (auch von DatabaseService genutzt,
        // ohne DI-Zyklus). HolidayService kümmert sich nur um Caching + Region aus Settings.
        var holidays = HolidayCalculator.Calculate(year, region);

        _cache[cacheKey] = holidays;
        return holidays;
    }
}
