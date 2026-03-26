namespace WorkTimePro.Services;

/// <summary>
/// Service for PDF/Excel/CSV export of time tracking data
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export monthly report as PDF
    /// </summary>
    Task<string> ExportMonthToPdfAsync(int year, int month);

    /// <summary>
    /// Export monthly report as Excel
    /// </summary>
    Task<string> ExportMonthToExcelAsync(int year, int month);

    /// <summary>
    /// Export monthly report as CSV
    /// </summary>
    Task<string> ExportMonthToCsvAsync(int year, int month);

    /// <summary>
    /// Export date range as PDF
    /// </summary>
    Task<string> ExportRangeToPdfAsync(DateTime start, DateTime end);

    /// <summary>
    /// Export date range as Excel
    /// </summary>
    Task<string> ExportRangeToExcelAsync(DateTime start, DateTime end);

    /// <summary>
    /// Export date range as CSV
    /// </summary>
    Task<string> ExportRangeToCsvAsync(DateTime start, DateTime end);

    /// <summary>
    /// Export yearly overview as PDF
    /// </summary>
    Task<string> ExportYearToPdfAsync(int year);

    /// <summary>
    /// Export date range as ICS calendar file
    /// </summary>
    Task<string> ExportRangeToIcsAsync(DateTime start, DateTime end);

    /// <summary>
    /// Export yearly overview as ICS calendar file
    /// </summary>
    Task<string> ExportYearToIcsAsync(int year);

    /// <summary>
    /// Share an exported file
    /// </summary>
    Task ShareFileAsync(string filePath);
}
