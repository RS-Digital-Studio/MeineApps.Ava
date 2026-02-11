using System.Globalization;
using System.Text;
using MeineApps.Core.Ava.Services;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using WorkTimePro.Models;
using WorkTimePro.Resources.Strings;

namespace WorkTimePro.Services;

/// <summary>
/// Export-Service fuer PDF, Excel und CSV.
/// PDF via PdfSharpCore, Excel via ClosedXML, CSV manuell.
/// </summary>
public class ExportService : IExportService
{
    private readonly IDatabaseService _database;
    private readonly ICalculationService _calculation;
    private readonly IFileShareService _fileShareService;

    public ExportService(IDatabaseService database, ICalculationService calculation, IFileShareService fileShareService)
    {
        _database = database;
        _calculation = calculation;
        _fileShareService = fileShareService;
    }

    private string ExportDirectory => _fileShareService.GetExportDirectory("WorkTimePro");

    #region PDF Export

    public async Task<string> ExportMonthToPdfAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return await ExportRangeToPdfAsync(start, end);
    }

    public async Task<string> ExportRangeToPdfAsync(DateTime start, DateTime end)
    {
        var workDays = await _database.GetWorkDaysAsync(start, end);
        var fileName = $"Arbeitszeit_{start:yyyy-MM-dd}_bis_{end:yyyy-MM-dd}.pdf";
        var filePath = Path.Combine(ExportDirectory, fileName);

        var document = new PdfDocument();
        document.Info.Title = $"{AppStrings.ExportWorkTimeReport} - {start:dd.MM.yyyy} bis {end:dd.MM.yyyy}";
        document.Info.Author = "WorkTimePro";

        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
        var headerFont = new XFont("Arial", 10, XFontStyle.Bold);
        var normalFont = new XFont("Arial", 9);
        var smallFont = new XFont("Arial", 8);

        double yPos = 40;
        double leftMargin = 40;
        double pageWidth = page.Width - 80;

        // Titel
        gfx.DrawString(AppStrings.ExportWorkTimeReport, titleFont, XBrushes.DarkBlue,
            new XRect(leftMargin, yPos, pageWidth, 25), XStringFormats.TopLeft);
        yPos += 22;
        gfx.DrawString($"{start:dd.MM.yyyy} - {end:dd.MM.yyyy}", normalFont, XBrushes.Gray,
            new XRect(leftMargin, yPos, pageWidth, 15), XStringFormats.TopLeft);
        yPos += 25;

        gfx.DrawLine(new XPen(XColors.DarkBlue, 1.5), leftMargin, yPos, page.Width - leftMargin, yPos);
        yPos += 15;

        // Tabellen-Header
        double[] colWidths = [90, 80, 50, 50, 55, 55, 50, 55];
        string[] headers = [AppStrings.TableDate, AppStrings.TableStatus, AppStrings.CheckIn, AppStrings.CheckOut, AppStrings.TableWork, AppStrings.TablePause, AppStrings.Target, AppStrings.Balance];

        // Header-Hintergrund
        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(21, 101, 192)), leftMargin, yPos - 2, pageWidth, 16);
        double xPos = leftMargin + 4;
        for (int i = 0; i < headers.Length; i++)
        {
            gfx.DrawString(headers[i], headerFont, XBrushes.White, xPos, yPos + 10, XStringFormats.BottomLeft);
            xPos += colWidths[i];
        }
        yPos += 18;

        // Daten
        int totalWork = 0, totalPause = 0, totalTarget = 0, totalBalance = 0;
        bool alternate = false;

        foreach (var day in workDays.OrderBy(d => d.Date))
        {
            // Neue Seite wenn noetig
            if (yPos > page.Height - 60)
            {
                page = document.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                yPos = 40;
            }

            // Abwechselnder Hintergrund
            if (alternate)
                gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(240, 244, 248)), leftMargin, yPos - 2, pageWidth, 14);
            alternate = !alternate;

            var timeEntries = await _database.GetTimeEntriesAsync(day.Id);
            var firstCheckIn = timeEntries.Where(e => e.Type == EntryType.CheckIn).OrderBy(e => e.Timestamp).FirstOrDefault();
            var lastCheckOut = timeEntries.Where(e => e.Type == EntryType.CheckOut).OrderByDescending(e => e.Timestamp).FirstOrDefault();

            xPos = leftMargin + 4;
            var font = normalFont;
            var brush = XBrushes.Black;

            gfx.DrawString(day.Date.ToString("ddd dd.MM", CultureInfo.CurrentCulture), font, brush, xPos, yPos + 10, XStringFormats.BottomLeft);
            xPos += colWidths[0];
            gfx.DrawString(GetStatusText(day.Status), smallFont, XBrushes.Gray, xPos, yPos + 10, XStringFormats.BottomLeft);
            xPos += colWidths[1];
            gfx.DrawString(firstCheckIn?.Timestamp.ToString("HH:mm") ?? "-", font, brush, xPos, yPos + 10, XStringFormats.BottomLeft);
            xPos += colWidths[2];
            gfx.DrawString(lastCheckOut?.Timestamp.ToString("HH:mm") ?? "-", font, brush, xPos, yPos + 10, XStringFormats.BottomLeft);
            xPos += colWidths[3];
            gfx.DrawString(FormatMinutes(day.ActualWorkMinutes), font, brush, xPos, yPos + 10, XStringFormats.BottomLeft);
            xPos += colWidths[4];
            gfx.DrawString(FormatMinutes(day.ManualPauseMinutes + day.AutoPauseMinutes), font, brush, xPos, yPos + 10, XStringFormats.BottomLeft);
            xPos += colWidths[5];
            gfx.DrawString(FormatMinutes(day.TargetWorkMinutes), font, brush, xPos, yPos + 10, XStringFormats.BottomLeft);
            xPos += colWidths[6];

            var balanceBrush = day.BalanceMinutes >= 0 ? new XSolidBrush(XColor.FromArgb(76, 175, 80)) : new XSolidBrush(XColor.FromArgb(244, 67, 54));
            gfx.DrawString(FormatBalance(day.BalanceMinutes), font, balanceBrush, xPos, yPos + 10, XStringFormats.BottomLeft);

            totalWork += day.ActualWorkMinutes;
            totalPause += day.ManualPauseMinutes + day.AutoPauseMinutes;
            totalTarget += day.TargetWorkMinutes;
            totalBalance += day.BalanceMinutes;
            yPos += 14;
        }

        // Summenzeile
        yPos += 4;
        gfx.DrawLine(new XPen(XColors.DarkBlue, 1), leftMargin, yPos, page.Width - leftMargin, yPos);
        yPos += 4;

        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(227, 236, 247)), leftMargin, yPos - 2, pageWidth, 16);
        xPos = leftMargin + 4;
        gfx.DrawString(AppStrings.ExportTotal, headerFont, XBrushes.DarkBlue, xPos, yPos + 10, XStringFormats.BottomLeft);
        xPos += colWidths[0] + colWidths[1] + colWidths[2] + colWidths[3];
        gfx.DrawString(FormatMinutes(totalWork), headerFont, XBrushes.DarkBlue, xPos, yPos + 10, XStringFormats.BottomLeft);
        xPos += colWidths[4];
        gfx.DrawString(FormatMinutes(totalPause), headerFont, XBrushes.DarkBlue, xPos, yPos + 10, XStringFormats.BottomLeft);
        xPos += colWidths[5];
        gfx.DrawString(FormatMinutes(totalTarget), headerFont, XBrushes.DarkBlue, xPos, yPos + 10, XStringFormats.BottomLeft);
        xPos += colWidths[6];
        var totalBalanceBrush = totalBalance >= 0 ? new XSolidBrush(XColor.FromArgb(76, 175, 80)) : new XSolidBrush(XColor.FromArgb(244, 67, 54));
        gfx.DrawString(FormatBalance(totalBalance), headerFont, totalBalanceBrush, xPos, yPos + 10, XStringFormats.BottomLeft);

        // Footer
        var footerText = $"WorkTimePro - {DateTime.Now:dd.MM.yyyy HH:mm}";
        gfx.DrawString(footerText, smallFont, XBrushes.Gray,
            new XRect(0, page.Height - 30, page.Width, 20), XStringFormats.Center);

        document.Save(filePath);
        return filePath;
    }

    public async Task<string> ExportYearToPdfAsync(int year)
    {
        var fileName = $"Jahresuebersicht_{year}.pdf";
        var filePath = Path.Combine(ExportDirectory, fileName);

        var document = new PdfDocument();
        document.Info.Title = $"{AppStrings.ExportYearOverviewTitle} {year}";
        document.Info.Author = "WorkTimePro";

        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
        var headerFont = new XFont("Arial", 11, XFontStyle.Bold);
        var normalFont = new XFont("Arial", 10);
        var smallFont = new XFont("Arial", 8);

        double yPos = 40;
        double leftMargin = 40;
        double pageWidth = page.Width - 80;

        // Titel
        gfx.DrawString($"{AppStrings.ExportYearOverviewTitle} {year}", titleFont, XBrushes.DarkBlue,
            new XRect(leftMargin, yPos, pageWidth, 25), XStringFormats.TopLeft);
        yPos += 35;

        gfx.DrawLine(new XPen(XColors.DarkBlue, 1.5), leftMargin, yPos, page.Width - leftMargin, yPos);
        yPos += 15;

        // Tabellen-Header
        double[] colWidths = [110, 80, 80, 80, 80];
        string[] headers = [AppStrings.Month, AppStrings.WorkDays, AppStrings.Actual, AppStrings.Target, AppStrings.Balance];

        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(21, 101, 192)), leftMargin, yPos - 2, pageWidth, 18);
        double xPos = leftMargin + 4;
        for (int i = 0; i < headers.Length; i++)
        {
            gfx.DrawString(headers[i], headerFont, XBrushes.White, xPos, yPos + 12, XStringFormats.BottomLeft);
            xPos += colWidths[i];
        }
        yPos += 22;

        int yearTotalWork = 0, yearTotalTarget = 0, yearTotalBalance = 0, yearWorkDays = 0;

        for (int month = 1; month <= 12; month++)
        {
            var monthData = await _calculation.CalculateMonthAsync(year, month);

            if (month % 2 == 0)
                gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(240, 244, 248)), leftMargin, yPos - 2, pageWidth, 16);

            xPos = leftMargin + 4;
            gfx.DrawString(new DateTime(year, month, 1).ToString("MMMM"), normalFont, XBrushes.Black, xPos, yPos + 12, XStringFormats.BottomLeft);
            xPos += colWidths[0];
            gfx.DrawString(monthData.WorkedDays.ToString(), normalFont, XBrushes.Black, xPos, yPos + 12, XStringFormats.BottomLeft);
            xPos += colWidths[1];
            gfx.DrawString(FormatMinutes(monthData.ActualWorkMinutes), normalFont, XBrushes.Black, xPos, yPos + 12, XStringFormats.BottomLeft);
            xPos += colWidths[2];
            gfx.DrawString(FormatMinutes(monthData.TargetWorkMinutes), normalFont, XBrushes.Black, xPos, yPos + 12, XStringFormats.BottomLeft);
            xPos += colWidths[3];

            var balanceBrush = monthData.BalanceMinutes >= 0 ? new XSolidBrush(XColor.FromArgb(76, 175, 80)) : new XSolidBrush(XColor.FromArgb(244, 67, 54));
            gfx.DrawString(FormatBalance(monthData.BalanceMinutes), normalFont, balanceBrush, xPos, yPos + 12, XStringFormats.BottomLeft);

            yearTotalWork += monthData.ActualWorkMinutes;
            yearTotalTarget += monthData.TargetWorkMinutes;
            yearTotalBalance += monthData.BalanceMinutes;
            yearWorkDays += monthData.WorkedDays;
            yPos += 16;
        }

        // Summenzeile
        yPos += 4;
        gfx.DrawLine(new XPen(XColors.DarkBlue, 1), leftMargin, yPos, page.Width - leftMargin, yPos);
        yPos += 4;

        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(227, 236, 247)), leftMargin, yPos - 2, pageWidth, 18);
        xPos = leftMargin + 4;
        gfx.DrawString(AppStrings.ExportTotal, headerFont, XBrushes.DarkBlue, xPos, yPos + 12, XStringFormats.BottomLeft);
        xPos += colWidths[0];
        gfx.DrawString(yearWorkDays.ToString(), headerFont, XBrushes.DarkBlue, xPos, yPos + 12, XStringFormats.BottomLeft);
        xPos += colWidths[1];
        gfx.DrawString(FormatMinutes(yearTotalWork), headerFont, XBrushes.DarkBlue, xPos, yPos + 12, XStringFormats.BottomLeft);
        xPos += colWidths[2];
        gfx.DrawString(FormatMinutes(yearTotalTarget), headerFont, XBrushes.DarkBlue, xPos, yPos + 12, XStringFormats.BottomLeft);
        xPos += colWidths[3];
        var yearBalanceBrush = yearTotalBalance >= 0 ? new XSolidBrush(XColor.FromArgb(76, 175, 80)) : new XSolidBrush(XColor.FromArgb(244, 67, 54));
        gfx.DrawString(FormatBalance(yearTotalBalance), headerFont, yearBalanceBrush, xPos, yPos + 12, XStringFormats.BottomLeft);

        // Footer
        var footerText = $"WorkTimePro - {DateTime.Now:dd.MM.yyyy HH:mm}";
        gfx.DrawString(footerText, smallFont, XBrushes.Gray,
            new XRect(0, page.Height - 30, page.Width, 20), XStringFormats.Center);

        document.Save(filePath);
        return filePath;
    }

    #endregion

    #region Excel Export

    public async Task<string> ExportMonthToExcelAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return await ExportRangeToExcelAsync(start, end);
    }

    public async Task<string> ExportRangeToExcelAsync(DateTime start, DateTime end)
    {
        var workDays = await _database.GetWorkDaysAsync(start, end);
        var fileName = $"Arbeitszeit_{start:yyyy-MM-dd}_bis_{end:yyyy-MM-dd}.xlsx";
        var filePath = Path.Combine(ExportDirectory, fileName);

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.Worksheets.Add(AppStrings.TimeEntries);

        // Header
        worksheet.Cell(1, 1).Value = AppStrings.TableDate;
        worksheet.Cell(1, 2).Value = AppStrings.TableStatus;
        worksheet.Cell(1, 3).Value = AppStrings.CheckIn;
        worksheet.Cell(1, 4).Value = AppStrings.CheckOut;
        worksheet.Cell(1, 5).Value = AppStrings.WorkTime;
        worksheet.Cell(1, 6).Value = AppStrings.ManualPause;
        worksheet.Cell(1, 7).Value = AppStrings.AutoPause;
        worksheet.Cell(1, 8).Value = AppStrings.Target;
        worksheet.Cell(1, 9).Value = AppStrings.Balance;

        var headerRange = worksheet.Range(1, 1, 1, 9);
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1565C0");
        headerRange.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
        headerRange.Style.Font.Bold = true;

        int row = 2;
        int totalWork = 0, totalPause = 0, totalTarget = 0, totalBalance = 0;

        foreach (var day in workDays.OrderBy(d => d.Date))
        {
            var timeEntries = await _database.GetTimeEntriesAsync(day.Id);
            var firstCheckIn = timeEntries.Where(e => e.Type == EntryType.CheckIn).OrderBy(e => e.Timestamp).FirstOrDefault();
            var lastCheckOut = timeEntries.Where(e => e.Type == EntryType.CheckOut).OrderByDescending(e => e.Timestamp).FirstOrDefault();

            worksheet.Cell(row, 1).Value = day.Date.ToString("ddd dd.MM.yyyy", CultureInfo.CurrentCulture);
            worksheet.Cell(row, 2).Value = GetStatusText(day.Status);
            worksheet.Cell(row, 3).Value = firstCheckIn?.Timestamp.ToString("HH:mm") ?? "-";
            worksheet.Cell(row, 4).Value = lastCheckOut?.Timestamp.ToString("HH:mm") ?? "-";
            worksheet.Cell(row, 5).Value = FormatMinutes(day.ActualWorkMinutes);
            worksheet.Cell(row, 6).Value = FormatMinutes(day.ManualPauseMinutes);
            worksheet.Cell(row, 7).Value = day.AutoPauseMinutes > 0 ? $"{FormatMinutes(day.AutoPauseMinutes)} ({AppStrings.Auto.ToLower()})" : "-";
            worksheet.Cell(row, 8).Value = FormatMinutes(day.TargetWorkMinutes);
            worksheet.Cell(row, 9).Value = FormatBalance(day.BalanceMinutes);

            if (day.BalanceMinutes < 0)
                worksheet.Cell(row, 9).Style.Font.FontColor = ClosedXML.Excel.XLColor.Red;
            else if (day.BalanceMinutes > 0)
                worksheet.Cell(row, 9).Style.Font.FontColor = ClosedXML.Excel.XLColor.Green;

            totalWork += day.ActualWorkMinutes;
            totalPause += day.ManualPauseMinutes + day.AutoPauseMinutes;
            totalTarget += day.TargetWorkMinutes;
            totalBalance += day.BalanceMinutes;
            row++;
        }

        row++;
        worksheet.Cell(row, 1).Value = AppStrings.ExportTotal;
        worksheet.Cell(row, 5).Value = FormatMinutes(totalWork);
        worksheet.Cell(row, 6).Value = FormatMinutes(totalPause);
        worksheet.Cell(row, 8).Value = FormatMinutes(totalTarget);
        worksheet.Cell(row, 9).Value = FormatBalance(totalBalance);

        var sumRange = worksheet.Range(row, 1, row, 9);
        sumRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        sumRange.Style.Font.Bold = true;

        worksheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);

        return filePath;
    }

    #endregion

    #region CSV Export

    public async Task<string> ExportMonthToCsvAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return await ExportRangeToCsvAsync(start, end);
    }

    public async Task<string> ExportRangeToCsvAsync(DateTime start, DateTime end)
    {
        var workDays = await _database.GetWorkDaysAsync(start, end);
        var fileName = $"Arbeitszeit_{start:yyyy-MM-dd}_bis_{end:yyyy-MM-dd}.csv";
        var filePath = Path.Combine(ExportDirectory, fileName);

        var sb = new StringBuilder();
        sb.AppendLine($"{AppStrings.TableDate};{AppStrings.TableStatus};{AppStrings.CheckIn};{AppStrings.CheckOut};{AppStrings.WorkTime} (min);{AppStrings.Break} (min);{AppStrings.AutoPause} (min);{AppStrings.Target} (min);{AppStrings.Balance} (min)");

        foreach (var day in workDays.OrderBy(d => d.Date))
        {
            var timeEntries = await _database.GetTimeEntriesAsync(day.Id);
            var firstCheckIn = timeEntries.Where(e => e.Type == EntryType.CheckIn).OrderBy(e => e.Timestamp).FirstOrDefault();
            var lastCheckOut = timeEntries.Where(e => e.Type == EntryType.CheckOut).OrderByDescending(e => e.Timestamp).FirstOrDefault();

            sb.AppendLine(string.Join(";",
                day.Date.ToString("yyyy-MM-dd"),
                GetStatusText(day.Status),
                firstCheckIn?.Timestamp.ToString("HH:mm") ?? "",
                lastCheckOut?.Timestamp.ToString("HH:mm") ?? "",
                day.ActualWorkMinutes,
                day.ManualPauseMinutes,
                day.AutoPauseMinutes,
                day.TargetWorkMinutes,
                day.BalanceMinutes
            ));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        return filePath;
    }

    #endregion

    #region Share

    public async Task ShareFileAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        // MIME-Typ anhand der Dateiendung bestimmen
        var mimeType = Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };

        await _fileShareService.ShareFileAsync(filePath, "WorkTimePro Export", mimeType);
    }

    #endregion

    #region Helper

    private static string FormatMinutes(int minutes)
    {
        var hours = minutes / 60;
        var mins = Math.Abs(minutes % 60);
        return $"{hours}:{mins:D2}";
    }

    private static string FormatBalance(int minutes)
    {
        var sign = minutes >= 0 ? "+" : "";
        return sign + FormatMinutes(minutes);
    }

    private static string GetStatusText(DayStatus status)
    {
        return status switch
        {
            DayStatus.WorkDay or DayStatus.Work => AppStrings.DayStatus_WorkDay,
            DayStatus.Weekend => AppStrings.DayStatus_Weekend,
            DayStatus.Holiday => AppStrings.DayStatus_Holiday,
            DayStatus.Vacation => AppStrings.DayStatus_Vacation,
            DayStatus.Sick => AppStrings.DayStatus_Sick,
            DayStatus.HomeOffice => AppStrings.DayStatus_HomeOffice,
            DayStatus.BusinessTrip => AppStrings.DayStatus_BusinessTrip,
            DayStatus.OvertimeCompensation => AppStrings.OvertimeCompensation,
            DayStatus.SpecialLeave => AppStrings.SpecialLeave,
            DayStatus.Training => AppStrings.DayStatus_Training,
            DayStatus.CompensatoryTime => AppStrings.DayStatus_CompensatoryTime,
            DayStatus.UnpaidLeave => AppStrings.UnpaidLeave,
            _ => "-"
        };
    }

    #endregion
}
