using System.Globalization;
using System.Text;
using MeineApps.Core.Ava.Services;
using WorkTimePro.Helpers;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;

namespace WorkTimePro.Services;

/// <summary>
/// ICS-Kalender-Export (RFC 5545).
/// Generiert standardkonforme .ics Dateien, die in jeden Kalender importiert werden können.
/// </summary>
public sealed class CalendarExportService : ICalendarExportService
{
    private readonly IDatabaseService _database;
    private readonly IFileShareService _fileShareService;

    public CalendarExportService(IDatabaseService database, IFileShareService fileShareService)
    {
        _database = database;
        _fileShareService = fileShareService;
    }

    private string ExportDirectory => _fileShareService.GetExportDirectory("WorkTimePro");

    public async Task<string> ExportMonthToIcsAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return await ExportRangeToIcsAsync(start, end);
    }

    public async Task<string> ExportRangeToIcsAsync(DateTime start, DateTime end)
    {
        var workDays = await _database.GetWorkDaysAsync(start, end);
        var allWorkDayIds = workDays.Select(d => d.Id).ToList();
        var allEntriesByDay = await _database.GetTimeEntriesForWorkDaysAsync(allWorkDayIds);
        var vacations = await _database.GetVacationEntriesAsync(start, end);

        var fileName = $"Arbeitszeit_{start:yyyy-MM-dd}_bis_{end:yyyy-MM-dd}.ics";
        var filePath = Path.Combine(ExportDirectory, fileName);

        var ics = BuildIcsContent(workDays, allEntriesByDay, vacations);
        await File.WriteAllTextAsync(filePath, ics, Encoding.UTF8);

        return filePath;
    }

    public async Task<string> ExportYearToIcsAsync(int year)
    {
        var start = new DateTime(year, 1, 1);
        var end = new DateTime(year, 12, 31);

        var workDays = await _database.GetWorkDaysAsync(start, end);
        var allWorkDayIds = workDays.Select(d => d.Id).ToList();
        var allEntriesByDay = await _database.GetTimeEntriesForWorkDaysAsync(allWorkDayIds);
        var vacations = await _database.GetVacationEntriesAsync(start, end);

        var fileName = $"Arbeitszeit_{year}.ics";
        var filePath = Path.Combine(ExportDirectory, fileName);

        var ics = BuildIcsContent(workDays, allEntriesByDay, vacations);
        await File.WriteAllTextAsync(filePath, ics, Encoding.UTF8);

        return filePath;
    }

    // ========================================================
    // ICS-Generierung (RFC 5545)
    // ========================================================

    private static string BuildIcsContent(
        List<WorkDay> workDays,
        Dictionary<int, List<TimeEntry>> entriesByDay,
        List<VacationEntry> vacations)
    {
        var sb = new StringBuilder(4096);

        // Kalender-Header
        IcsLine(sb,"BEGIN:VCALENDAR");
        IcsLine(sb,"VERSION:2.0");
        IcsLine(sb,"PRODID:-//WorkTimePro//WorkTimePro//DE");
        IcsLine(sb,"CALSCALE:GREGORIAN");
        IcsLine(sb,"METHOD:PUBLISH");
        IcsLine(sb,"X-WR-CALNAME:WorkTimePro");
        IcsLine(sb,"X-WR-TIMEZONE:Europe/Berlin");

        // Arbeitstage als Events
        foreach (var day in workDays.OrderBy(d => d.Date))
        {
            if (day.ActualWorkMinutes == 0 && day.Status == DayStatus.WorkDay)
                continue; // Tage ohne Einträge überspringen

            // Feiertage und Wochenenden als ganztägige Events
            if (day.Status == DayStatus.Holiday || day.Status == DayStatus.Weekend)
            {
                AppendAllDayEvent(sb, day);
                continue;
            }

            // Nicht-Arbeitstage (Krank, HomeOffice etc.) ohne Arbeitszeit als ganztägig
            if (day.ActualWorkMinutes == 0 && day.Status != DayStatus.WorkDay)
            {
                AppendAllDayEvent(sb, day);
                continue;
            }

            // Arbeitstag mit Check-in/out Zeiten
            var entries = entriesByDay.TryGetValue(day.Id, out var e) ? e : [];
            AppendWorkDayEvent(sb, day, entries);
        }

        // Urlaubseinträge als ganztägige Events (Zeitspannen)
        foreach (var vacation in vacations.OrderBy(v => v.StartDate))
        {
            AppendVacationEvent(sb, vacation);
        }

        IcsLine(sb,"END:VCALENDAR");
        return sb.ToString();
    }

    /// <summary>
    /// Arbeitstag mit Check-in/out als zeitgebundenes Event.
    /// </summary>
    private static void AppendWorkDayEvent(StringBuilder sb, WorkDay day, List<TimeEntry> entries)
    {
        var checkIns = entries.Where(e => e.Type == EntryType.CheckIn).OrderBy(e => e.Timestamp).ToList();
        var checkOuts = entries.Where(e => e.Type == EntryType.CheckOut).OrderByDescending(e => e.Timestamp).ToList();

        DateTime eventStart, eventEnd;

        if (checkIns.Count > 0 && checkOuts.Count > 0)
        {
            eventStart = checkIns.First().Timestamp;
            eventEnd = checkOuts.First().Timestamp;
        }
        else
        {
            // Fallback: 08:00 bis 08:00 + Arbeitszeit
            eventStart = day.Date.AddHours(8);
            eventEnd = eventStart.AddMinutes(day.ActualWorkMinutes);
        }

        // Titel: "Arbeitszeit: 8:30 (+0:30)" oder "Arbeitszeit: 6:00 (-2:00)"
        var workDisplay = TimeFormatter.FormatMinutes(day.ActualWorkMinutes);
        var balanceDisplay = TimeFormatter.FormatBalance(day.BalanceMinutes);
        var title = $"{AppStrings.WorkTime}: {workDisplay} ({balanceDisplay})";

        // Beschreibung mit Details
        var desc = BuildWorkDayDescription(day, checkIns.FirstOrDefault(), checkOuts.FirstOrDefault());

        IcsLine(sb,"BEGIN:VEVENT");
        IcsLine(sb,$"UID:worktimepro-day-{day.Date:yyyyMMdd}@meineapps");
        IcsLine(sb,$"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}");
        IcsLine(sb,$"DTSTART:{eventStart:yyyyMMdd'T'HHmmss}");
        IcsLine(sb,$"DTEND:{eventEnd:yyyyMMdd'T'HHmmss}");
        IcsLine(sb,$"SUMMARY:{EscapeIcsText(title)}");
        IcsLine(sb,$"DESCRIPTION:{EscapeIcsText(desc)}");
        IcsLine(sb,"STATUS:CONFIRMED");
        IcsLine(sb,"TRANSP:OPAQUE");

        // Farbkategorie basierend auf Saldo
        if (day.BalanceMinutes >= 0)
            IcsLine(sb,"CATEGORIES:Arbeitszeit,Positiv");
        else
            IcsLine(sb,"CATEGORIES:Arbeitszeit,Negativ");

        IcsLine(sb,"END:VEVENT");
    }

    /// <summary>
    /// Ganztägiges Event für Feiertage, Krankheit, HomeOffice etc.
    /// </summary>
    private static void AppendAllDayEvent(StringBuilder sb, WorkDay day)
    {
        var statusName = TimeFormatter.GetStatusName(day.Status);
        var title = statusName;
        if (!string.IsNullOrEmpty(day.Note))
            title += $": {day.Note}";

        IcsLine(sb,"BEGIN:VEVENT");
        IcsLine(sb,$"UID:worktimepro-status-{day.Date:yyyyMMdd}@meineapps");
        IcsLine(sb,$"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}");
        IcsLine(sb,$"DTSTART;VALUE=DATE:{day.Date:yyyyMMdd}");
        IcsLine(sb,$"DTEND;VALUE=DATE:{day.Date.AddDays(1):yyyyMMdd}");
        IcsLine(sb,$"SUMMARY:{EscapeIcsText(title)}");
        IcsLine(sb,"STATUS:CONFIRMED");
        IcsLine(sb,"TRANSP:TRANSPARENT");
        IcsLine(sb,$"CATEGORIES:{EscapeIcsText(statusName)}");
        IcsLine(sb,"END:VEVENT");
    }

    /// <summary>
    /// Urlaubseintrag als mehrtägiges ganztägiges Event.
    /// </summary>
    private static void AppendVacationEvent(StringBuilder sb, VacationEntry vacation)
    {
        var statusName = TimeFormatter.GetStatusName(vacation.Type);
        var title = statusName;
        if (!string.IsNullOrEmpty(vacation.Note))
            title += $": {vacation.Note}";

        var daysText = string.Format(AppStrings.VacationDaysEnteredFormat ?? "{0}/{1}",
            vacation.Days, vacation.Days);

        IcsLine(sb,"BEGIN:VEVENT");
        IcsLine(sb,$"UID:worktimepro-vacation-{vacation.Id}-{vacation.StartDate:yyyyMMdd}@meineapps");
        IcsLine(sb,$"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}");
        IcsLine(sb,$"DTSTART;VALUE=DATE:{vacation.StartDate:yyyyMMdd}");
        // RFC 5545: DTEND bei ganztägig = exklusiv, daher +1 Tag
        IcsLine(sb,$"DTEND;VALUE=DATE:{vacation.EndDate.AddDays(1):yyyyMMdd}");
        IcsLine(sb,$"SUMMARY:{EscapeIcsText(title)}");
        IcsLine(sb,$"DESCRIPTION:{EscapeIcsText($"{vacation.Days} {AppStrings.Days}")}");
        IcsLine(sb,"STATUS:CONFIRMED");
        IcsLine(sb,"TRANSP:TRANSPARENT");
        IcsLine(sb,$"CATEGORIES:{EscapeIcsText(statusName)}");
        IcsLine(sb,"END:VEVENT");
    }

    // ========================================================
    // Hilfsmethoden
    // ========================================================

    private static string BuildWorkDayDescription(WorkDay day, TimeEntry? firstCheckIn, TimeEntry? lastCheckOut)
    {
        var lines = new List<string>();

        if (firstCheckIn != null)
            lines.Add($"{AppStrings.CheckIn}: {firstCheckIn.Timestamp:HH:mm}");
        if (lastCheckOut != null)
            lines.Add($"{AppStrings.CheckOut}: {lastCheckOut.Timestamp:HH:mm}");

        lines.Add($"{AppStrings.WorkTime}: {TimeFormatter.FormatMinutes(day.ActualWorkMinutes)}");
        lines.Add($"{AppStrings.Target}: {TimeFormatter.FormatMinutes(day.TargetWorkMinutes)}");

        var totalPause = day.ManualPauseMinutes + day.AutoPauseMinutes;
        if (totalPause > 0)
        {
            lines.Add($"{AppStrings.Break}: {TimeFormatter.FormatMinutes(totalPause)}");
            if (day.AutoPauseMinutes > 0)
                lines.Add($"  {string.Format(AppStrings.CalendarAutoBreak, day.AutoPauseMinutes)}");
        }

        lines.Add($"{AppStrings.Balance}: {TimeFormatter.FormatBalance(day.BalanceMinutes)}");

        if (day.Status != DayStatus.WorkDay)
            lines.Add($"Status: {TimeFormatter.GetStatusName(day.Status)}");

        if (!string.IsNullOrEmpty(day.Note))
            lines.Add($"{AppStrings.Note}: {day.Note}");

        lines.Add("");
        lines.Add(AppStrings.CalendarCreatedBy);

        return string.Join("\\n", lines);
    }

    /// <summary>
    /// Escaped Text gemäß RFC 5545:
    /// Backslash, Semikolon, Komma und Zeilenumbrüche.
    /// </summary>
    private static string EscapeIcsText(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n");
    }

    /// <summary>
    /// Schreibt eine ICS-Zeile mit CRLF (RFC 5545 Sec. 3.1).
    /// Zeilen über 75 Octets werden per Line-Folding umgebrochen.
    /// </summary>
    private static void IcsLine(StringBuilder sb, string line)
    {
        // RFC 5545 Sec. 3.1: Max 75 Octets pro Zeile, dann CRLF + SPACE
        const int maxOctets = 75;
        var bytes = Encoding.UTF8.GetByteCount(line);

        if (bytes <= maxOctets)
        {
            sb.Append(line);
            sb.Append("\r\n");
            return;
        }

        // Zeichenweise bis 75 Bytes auffüllen, dann umbrechen
        int byteCount = 0;
        bool firstLine = true;
        for (int i = 0; i < line.Length; i++)
        {
            int charBytes = Encoding.UTF8.GetByteCount(line, i, 1);
            int limit = firstLine ? maxOctets : maxOctets - 1; // Folgezeilen: SPACE zählt mit

            if (byteCount + charBytes > limit)
            {
                sb.Append("\r\n ");
                byteCount = 1; // SPACE = 1 Byte
                firstLine = false;
            }

            sb.Append(line[i]);
            byteCount += charBytes;
        }
        sb.Append("\r\n");
    }
}
