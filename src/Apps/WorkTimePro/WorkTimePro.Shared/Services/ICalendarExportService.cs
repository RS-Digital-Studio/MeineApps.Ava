namespace WorkTimePro.Services;

/// <summary>
/// Service für ICS-Kalender-Export (RFC 5545).
/// Generiert .ics Dateien aus Arbeitstagen und Urlaubseinträgen.
/// </summary>
public interface ICalendarExportService
{
    /// <summary>
    /// Exportiert Arbeitszeiten eines Monats als ICS-Datei.
    /// </summary>
    Task<string> ExportMonthToIcsAsync(int year, int month);

    /// <summary>
    /// Exportiert Arbeitszeiten eines Zeitraums als ICS-Datei.
    /// </summary>
    Task<string> ExportRangeToIcsAsync(DateTime start, DateTime end);

    /// <summary>
    /// Exportiert Jahresübersicht (Arbeitstage + Urlaub) als ICS-Datei.
    /// </summary>
    Task<string> ExportYearToIcsAsync(int year);
}
