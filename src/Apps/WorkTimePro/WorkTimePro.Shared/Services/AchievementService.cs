using MeineApps.Core.Ava.Localization;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;

namespace WorkTimePro.Services;

/// <summary>
/// Implementierung des Achievement-Systems.
/// Prueft Fortschritte gegen DB-Daten und verwaltet Freischaltungen.
/// Optimiert: Batch-Queries statt N+1, gemeinsame Daten einmal laden,
/// nur geaenderte Achievements speichern.
/// </summary>
public class AchievementService : IAchievementService
{
    private readonly IDatabaseService _database;
    private readonly ILocalizationService _localization;

    /// <summary>
    /// Hardcodierte Achievement-Definitionen mit Key, Target und Icon.
    /// </summary>
    private static readonly (string Key, int Target, string Icon)[] Definitions =
    {
        ("hours_100", 6000, "Timer"),           // 100 Stunden = 6000 Minuten
        ("hours_500", 30000, "TimerStar"),       // 500 Stunden = 30000 Minuten
        ("hours_1000", 60000, "Trophy"),         // 1000 Stunden = 60000 Minuten
        ("streak_7", 7, "Fire"),                 // 7-Tage-Streak
        ("streak_30", 30, "Whatshot"),            // 30-Tage-Streak
        ("streak_100", 100, "LocalFireDepartment"), // 100-Tage-Streak
        ("perfect_week", 1, "Star"),             // Eine perfekte Woche (exakt im Soll)
        ("no_absence_month", 1, "CalendarCheck"), // Kein Fehltag im Monat
        ("early_bird", 10, "WeatherSunny"),      // 10x vor Soll eingecheckt
        ("overtime_king", 3000, "Crown"),         // 50h Ueberstunden kumuliert = 3000 Minuten
        ("night_owl", 10, "WeatherNight"),       // 10x nach 20 Uhr ausgecheckt
        ("marathon", 600, "Run"),                // 1 Tag mit 10h+ Arbeitszeit = 600 Minuten
        ("half_year", 130, "CalendarRange"),     // 130 Arbeitstage (ca. halbes Jahr)
        ("full_year", 250, "CalendarStar"),      // 250 Arbeitstage (ca. ganzes Jahr)
        ("pause_master", 50, "Coffee"),          // 50 Pausen korrekt eingetragen
    };

    public event EventHandler<Achievement>? AchievementUnlocked;

    public AchievementService(IDatabaseService database, ILocalizationService localization)
    {
        _database = database;
        _localization = localization;
    }

    public async Task InitializeAsync()
    {
        // Achievement-Tabelle in der DB erstellen
        await _database.CreateAchievementTableAsync();

        // Fehlende Achievements anlegen (neue Definitionen bei App-Update)
        var existing = await _database.GetAllAchievementsAsync();
        var existingKeys = existing.Select(a => a.Key).ToHashSet();

        foreach (var (key, target, _) in Definitions)
        {
            if (!existingKeys.Contains(key))
            {
                var achievement = new Achievement
                {
                    Key = key,
                    Target = target,
                    Progress = 0,
                    IsUnlocked = false
                };
                await _database.SaveAchievementAsync(achievement);
            }
        }
    }

    public async Task CheckAchievementsAsync()
    {
        var achievements = await _database.GetAllAchievementsAsync();
        var pendingAchievements = achievements.Where(a => !a.IsUnlocked).ToList();
        if (pendingAchievements.Count == 0) return;

        // Gemeinsame Daten einmal laden fuer alle Fortschritts-Berechnungen
        var sharedData = await LoadSharedDataAsync(pendingAchievements);

        foreach (var achievement in pendingAchievements)
        {
            var def = Definitions.FirstOrDefault(d => d.Key == achievement.Key);
            if (def == default) continue;

            int newProgress = CalculateProgressFromSharedData(achievement.Key, sharedData);

            // Nur speichern wenn sich der Fortschritt geaendert hat
            if (newProgress == achievement.Progress) continue;

            var oldProgress = achievement.Progress;
            achievement.Progress = newProgress;

            // Achievement freigeschaltet?
            if (newProgress >= def.Target)
            {
                achievement.IsUnlocked = true;
                achievement.UnlockedAt = DateTime.UtcNow;
                achievement.Progress = def.Target; // Nicht ueber Target hinaus

                await _database.SaveAchievementAsync(achievement);

                // Lokalisierten Namen setzen fuer das Event
                PopulateDisplayData(achievement);
                AchievementUnlocked?.Invoke(this, achievement);
            }
            else
            {
                await _database.SaveAchievementAsync(achievement);
            }
        }
    }

    public async Task<List<Achievement>> GetAllAsync()
    {
        var achievements = await _database.GetAllAchievementsAsync();

        foreach (var achievement in achievements)
        {
            PopulateDisplayData(achievement);
        }

        // Sortierung: Freigeschaltete zuerst (nach Datum), dann gesperrte (nach Fortschritt absteigend)
        return achievements
            .OrderByDescending(a => a.IsUnlocked)
            .ThenByDescending(a => a.UnlockedAt)
            .ThenByDescending(a => a.ProgressPercent)
            .ToList();
    }

    public async Task<List<Achievement>> GetUnlockedAsync()
    {
        var all = await GetAllAsync();
        return all.Where(a => a.IsUnlocked).ToList();
    }

    /// <summary>
    /// Oeffentliche Methode fuer Streak-Berechnung (wird auch von MainViewModel genutzt).
    /// Nutzt Batch-Query statt N+1.
    /// </summary>
    public async Task<int> GetCurrentStreakAsync()
    {
        try
        {
            // Letzte 120 Tage in einem Batch laden
            var startDate = DateTime.Today.AddDays(-120);
            var workDays = await _database.GetWorkDaysAsync(startDate, DateTime.Today);
            return CalculateStreakFromDays(workDays);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Streak-Berechnung Fehler: {ex.Message}");
            return 0;
        }
    }

    // === Private Hilfsmethoden ===

    /// <summary>
    /// Container fuer gemeinsam geladene Daten, um redundante DB-Aufrufe zu vermeiden.
    /// </summary>
    private sealed class SharedProgressData
    {
        public int TotalWorkMinutes { get; set; }
        public int CurrentStreak { get; set; }
        public bool HasPerfectWeek { get; set; }
        public bool HasNoAbsenceMonth { get; set; }
        public int EarlyCheckInCount { get; set; }
        public int TotalOvertimeMinutes { get; set; }
        public DateTime? FirstDate { get; set; }
        public int NightOwlCount { get; set; }
        public int MarathonMaxMinutes { get; set; }
        public int TotalWorkDays { get; set; }
        public int TotalPauseEntries { get; set; }
    }

    /// <summary>
    /// Laedt alle benoetigten Daten einmal und berechnet alle Fortschritte.
    /// Vermeidet redundante DB-Aufrufe fuer Stunden/Ueberstunden (gleiche Query-Daten).
    /// </summary>
    private async Task<SharedProgressData> LoadSharedDataAsync(List<Achievement> pendingAchievements)
    {
        var data = new SharedProgressData();
        var pendingKeys = pendingAchievements.Select(a => a.Key).ToHashSet();

        try
        {
            // FirstDate wird von mehreren Achievements benoetigt
            bool needsFirstDate = pendingKeys.Any(k =>
                k is "hours_100" or "hours_500" or "hours_1000" or "overtime_king");

            if (needsFirstDate)
            {
                data.FirstDate = await _database.GetFirstWorkDayDateAsync();
            }

            // Stunden-basierte Achievements: Eine Query fuer Gesamtminuten
            if (data.FirstDate.HasValue && pendingKeys.Any(k => k is "hours_100" or "hours_500" or "hours_1000"))
            {
                data.TotalWorkMinutes = await _database.GetTotalWorkMinutesAsync(data.FirstDate.Value, DateTime.Today);
            }

            // Ueberstunden-Achievement: Eine Query
            if (data.FirstDate.HasValue && pendingKeys.Contains("overtime_king"))
            {
                var overtime = await _database.GetTotalOvertimeMinutesAsync(data.FirstDate.Value, DateTime.Today);
                data.TotalOvertimeMinutes = Math.Max(0, overtime);
            }

            // Streak-Achievements: Eine Batch-Query fuer die letzten 120 Tage
            if (pendingKeys.Any(k => k is "streak_7" or "streak_30" or "streak_100"))
            {
                var startDate = DateTime.Today.AddDays(-120);
                var workDays = await _database.GetWorkDaysAsync(startDate, DateTime.Today);
                data.CurrentStreak = CalculateStreakFromDays(workDays);
            }

            // Perfekte Woche: Eine Bulk-Query fuer 52 Wochen
            if (pendingKeys.Contains("perfect_week"))
            {
                data.HasPerfectWeek = await HasPerfectWeekBulkAsync();
            }

            // Volle Anwesenheit: Bereits optimiert (eine Query)
            if (pendingKeys.Contains("no_absence_month"))
            {
                data.HasNoAbsenceMonth = await HasNoAbsenceMonthAsync();
            }

            // Early Bird: Bereits optimiert (eine Query)
            if (pendingKeys.Contains("early_bird"))
            {
                data.EarlyCheckInCount = await CountEarlyCheckInsAsync();
            }

            // Night Owl: Späte CheckOuts zählen
            if (pendingKeys.Contains("night_owl"))
            {
                data.NightOwlCount = await CountNightOwlCheckOutsAsync();
            }

            // Marathon + Arbeitstage + Pausen: Aus WorkDays-Daten berechnen
            if (pendingKeys.Any(k => k is "marathon" or "half_year" or "full_year" or "pause_master"))
            {
                var allDays = data.FirstDate.HasValue
                    ? await _database.GetWorkDaysAsync(data.FirstDate.Value, DateTime.Today)
                    : await _database.GetWorkDaysAsync(DateTime.Today.AddDays(-365), DateTime.Today);

                if (pendingKeys.Contains("marathon"))
                    data.MarathonMaxMinutes = allDays.Count > 0 ? allDays.Max(d => d.ActualWorkMinutes) : 0;

                if (pendingKeys.Any(k => k is "half_year" or "full_year"))
                    data.TotalWorkDays = allDays.Count(d => d.ActualWorkMinutes > 0);

                if (pendingKeys.Contains("pause_master"))
                    data.TotalPauseEntries = await CountPauseEntriesAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SharedData Ladefehler: {ex.Message}");
        }

        return data;
    }

    /// <summary>
    /// Berechnet den Fortschritt aus den bereits geladenen SharedData.
    /// Keine weiteren DB-Aufrufe.
    /// </summary>
    private static int CalculateProgressFromSharedData(string key, SharedProgressData data)
    {
        return key switch
        {
            "hours_100" or "hours_500" or "hours_1000" => data.TotalWorkMinutes,
            "streak_7" or "streak_30" or "streak_100" => data.CurrentStreak,
            "perfect_week" => data.HasPerfectWeek ? 1 : 0,
            "no_absence_month" => data.HasNoAbsenceMonth ? 1 : 0,
            "early_bird" => data.EarlyCheckInCount,
            "overtime_king" => data.TotalOvertimeMinutes,
            "night_owl" => data.NightOwlCount,
            "marathon" => data.MarathonMaxMinutes,
            "half_year" or "full_year" => data.TotalWorkDays,
            "pause_master" => data.TotalPauseEntries,
            _ => 0
        };
    }

    /// <summary>
    /// Berechnet die Streak aus einer bereits geladenen Liste von WorkDays.
    /// Wochenenden werden uebersprungen.
    /// </summary>
    private static int CalculateStreakFromDays(List<WorkDay> workDays)
    {
        // WorkDays nach Datum indizieren fuer schnellen Zugriff
        var daysByDate = workDays
            .Where(d => d.ActualWorkMinutes > 0)
            .Select(d => d.Date.Date)
            .ToHashSet();

        var streak = 0;
        var date = DateTime.Today;

        for (int i = 0; i < 120; i++)
        {
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                date = date.AddDays(-1);
                continue;
            }

            if (daysByDate.Contains(date))
            {
                streak++;
                date = date.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    /// <summary>
    /// Prueft ob es mindestens eine perfekte Woche gab.
    /// Optimiert: Eine Bulk-Query fuer alle 52 Wochen statt 52 einzelne Queries.
    /// </summary>
    private async Task<bool> HasPerfectWeekBulkAsync()
    {
        // Alle WorkDays der letzten 52 Wochen in einer Query laden
        var startDate = DateTime.Today.AddDays(-364);
        var allWorkDays = await _database.GetWorkDaysAsync(startDate, DateTime.Today);
        if (allWorkDays.Count == 0) return false;

        // In-Memory nach Wochen gruppieren und pruefen
        for (int w = 0; w < 52; w++)
        {
            var referenceDate = DateTime.Today.AddDays(-7 * w);
            var dayOfWeek = ((int)referenceDate.DayOfWeek + 6) % 7;
            var monday = referenceDate.AddDays(-dayOfWeek);
            var sunday = monday.AddDays(6);

            var weekDays = allWorkDays.Where(d => d.Date >= monday && d.Date <= sunday).ToList();
            if (weekDays.Count == 0) continue;

            // Nur Wochen pruefen die mindestens 3 Arbeitstage haben
            var actualWorkDays = weekDays.Where(d => d.TargetWorkMinutes > 0).ToList();
            if (actualWorkDays.Count < 3) continue;

            // Gesamtabweichung berechnen
            var totalDeviation = actualWorkDays.Sum(d => Math.Abs(d.BalanceMinutes));
            if (totalDeviation <= 30) return true; // Max 30 Minuten Abweichung gesamt
        }

        return false;
    }

    /// <summary>
    /// Prueft ob im aktuellen Monat keine Fehltage vorhanden sind.
    /// </summary>
    private async Task<bool> HasNoAbsenceMonthAsync()
    {
        var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);

        // Nur vergangene und aktuelle Tage pruefen
        var endDate = DateTime.Today < lastOfMonth ? DateTime.Today : lastOfMonth;
        var workDays = await _database.GetWorkDaysAsync(firstOfMonth, endDate);

        // Mindestens 5 Arbeitstage im Monat (nicht am 1. Tag pruefen)
        if (workDays.Count < 5) return false;

        // Pruefe ob ein Fehltag vorliegt
        foreach (var day in workDays)
        {
            if (day.Status is DayStatus.Vacation or DayStatus.Sick or DayStatus.UnpaidLeave
                or DayStatus.SpecialLeave or DayStatus.CompensatoryTime)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Zaehlt wie oft der User vor der regulaeren Soll-Startzeit eingecheckt hat.
    /// "Vor dem Soll" = Check-In vor 8:00 (typischer Arbeitsbeginn).
    /// </summary>
    private async Task<int> CountEarlyCheckInsAsync()
    {
        var count = 0;
        var settings = await _database.GetSettingsAsync();
        var earlyThreshold = new TimeSpan(8, 0, 0); // 08:00 als Schwelle

        // Letzte 365 Tage pruefen (eine Query)
        var startDate = DateTime.Today.AddDays(-365);
        var workDays = await _database.GetWorkDaysAsync(startDate, DateTime.Today);

        foreach (var day in workDays)
        {
            if (day.ActualWorkMinutes <= 0) continue;
            if (!settings.IsWorkDay(day.Date.DayOfWeek)) continue;

            // Ersten Check-In des Tages finden
            if (day.FirstCheckIn.HasValue && day.FirstCheckIn.Value.TimeOfDay < earlyThreshold)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Setzt lokalisierte Anzeige-Daten auf einem Achievement.
    /// </summary>
    private void PopulateDisplayData(Achievement achievement)
    {
        var def = Definitions.FirstOrDefault(d => d.Key == achievement.Key);
        if (def == default) return;

        achievement.IconName = def.Icon;

        // Lokalisierte Namen und Beschreibungen
        achievement.DisplayName = _localization.GetString($"Achievement_{achievement.Key}_Name")
                                  ?? GetFallbackName(achievement.Key);
        achievement.Description = _localization.GetString($"Achievement_{achievement.Key}_Desc")
                                  ?? GetFallbackDescription(achievement.Key);

        // Fortschritts-Anzeige
        achievement.ProgressDisplay = achievement.Key switch
        {
            "hours_100" or "hours_500" or "hours_1000" =>
                $"{achievement.Progress / 60}/{achievement.Target / 60}h",
            "streak_7" or "streak_30" or "streak_100" =>
                $"{achievement.Progress}/{achievement.Target}",
            "overtime_king" =>
                $"{achievement.Progress / 60}/{achievement.Target / 60}h",
            "marathon" =>
                $"{achievement.Progress / 60}/{achievement.Target / 60}h",
            "early_bird" or "night_owl" or "pause_master" =>
                $"{achievement.Progress}/{achievement.Target}",
            "half_year" or "full_year" =>
                $"{achievement.Progress}/{achievement.Target}",
            _ => achievement.IsUnlocked
                ? (_localization.GetString("Completed") ?? "Completed")
                : $"{achievement.Progress}/{achievement.Target}"
        };
    }

    /// <summary>Zählt wie oft nach 20 Uhr ausgecheckt wurde.</summary>
    private async Task<int> CountNightOwlCheckOutsAsync()
    {
        var count = 0;
        var startDate = DateTime.Today.AddDays(-365);
        var workDays = await _database.GetWorkDaysAsync(startDate, DateTime.Today);

        foreach (var day in workDays)
        {
            if (day.ActualWorkMinutes <= 0) continue;
            if (day.LastCheckOut.HasValue && day.LastCheckOut.Value.TimeOfDay >= new TimeSpan(20, 0, 0))
                count++;
        }

        return count;
    }

    /// <summary>Zählt die Gesamtzahl der Pausen-Einträge.</summary>
    private async Task<int> CountPauseEntriesAsync()
    {
        var startDate = DateTime.Today.AddDays(-365);
        var workDays = await _database.GetWorkDaysAsync(startDate, DateTime.Today);
        var count = 0;
        foreach (var day in workDays)
        {
            if (day.ManualPauseMinutes + day.AutoPauseMinutes > 0)
                count++;
        }
        return count;
    }

    /// <summary>Fallback-Name falls kein RESX-Key vorhanden.</summary>
    private static string GetFallbackName(string key) => key switch
    {
        "hours_100" => "100 Hours",
        "hours_500" => "500 Hours",
        "hours_1000" => "1000 Hours",
        "streak_7" => "7-Day Streak",
        "streak_30" => "30-Day Streak",
        "streak_100" => "100-Day Streak",
        "perfect_week" => "Perfect Week",
        "no_absence_month" => "No Absence Month",
        "early_bird" => "Early Bird",
        "overtime_king" => "Overtime King",
        "night_owl" => "Night Owl",
        "marathon" => "Marathon Day",
        "half_year" => "Half Year",
        "full_year" => "Full Year",
        "pause_master" => "Pause Master",
        _ => key
    };

    /// <summary>Fallback-Beschreibung falls kein RESX-Key vorhanden.</summary>
    private static string GetFallbackDescription(string key) => key switch
    {
        "hours_100" => "Work 100 hours total",
        "hours_500" => "Work 500 hours total",
        "hours_1000" => "Work 1000 hours total",
        "streak_7" => "Work 7 consecutive days",
        "streak_30" => "Work 30 consecutive days",
        "streak_100" => "Work 100 consecutive days",
        "perfect_week" => "Complete a week exactly on target",
        "no_absence_month" => "Zero absence days in a month",
        "early_bird" => "Check in before 8:00 ten times",
        "overtime_king" => "Accumulate 50 hours of overtime",
        "night_owl" => "Check out after 20:00 ten times",
        "marathon" => "Work 10+ hours in a single day",
        "half_year" => "Log 130 work days",
        "full_year" => "Log 250 work days",
        "pause_master" => "Take 50 breaks",
        _ => key
    };
}
