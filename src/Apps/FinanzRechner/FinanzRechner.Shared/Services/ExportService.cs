using System.Text;
using FinanzRechner.Helpers;
using FinanzRechner.Models;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace FinanzRechner.Services;

/// <summary>
/// Service fuer den Export von Transaktionen (CSV + PDF).
/// </summary>
public class ExportService : IExportService
{
    private readonly IExpenseService _expenseService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileShareService _fileShareService;

    public ExportService(IExpenseService expenseService, ILocalizationService localizationService, IFileShareService fileShareService)
    {
        _expenseService = expenseService;
        _localizationService = localizationService;
        _fileShareService = fileShareService;
    }

    public async Task<string> ExportToCsvAsync(int year, int month, string? targetPath = null)
    {
        var expenses = await _expenseService.GetExpensesByMonthAsync(year, month);
        return await GenerateCsvAsync(expenses, $"transactions_{year}_{month:D2}", targetPath);
    }

    public async Task<string> ExportToCsvAsync(DateTime startDate, DateTime endDate, string? targetPath = null)
    {
        var filter = new ExpenseFilter { StartDate = startDate, EndDate = endDate };
        var expenses = await _expenseService.GetExpensesAsync(filter);
        var fileName = $"transactions_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
        return await GenerateCsvAsync(expenses, fileName, targetPath);
    }

    public async Task<string> ExportAllToCsvAsync(string? targetPath = null)
    {
        var expenses = await _expenseService.GetAllExpensesAsync();
        return await GenerateCsvAsync(expenses, "transactions_all", targetPath);
    }

    private async Task<string> GenerateCsvAsync(IEnumerable<Expense> expenses, string fileName, string? targetPath = null)
    {
        var csv = new StringBuilder();
        csv.AppendLine("sep=;"); // Excel erkennt Semikolon als Trennzeichen
        var hDate = _localizationService.GetString("Date") ?? "Date";
        var hType = _localizationService.GetString("Type") ?? "Type";
        var hCategory = _localizationService.GetString("Category") ?? "Category";
        var hDescription = _localizationService.GetString("Description") ?? "Description";
        var hAmount = _localizationService.GetString("Amount") ?? "Amount";
        var hNote = _localizationService.GetString("Note") ?? "Note";
        csv.AppendLine($"{hDate};{hType};{hCategory};{hDescription};{hAmount};{hNote}");

        foreach (var expense in expenses.OrderByDescending(e => e.Date))
        {
            var type = expense.Type == TransactionType.Expense
                ? _localizationService.GetString("Expense") ?? "Expense"
                : _localizationService.GetString("Income") ?? "Income";
            var category = CategoryLocalizationHelper.GetLocalizedName(expense.Category, _localizationService);
            var description = EscapeCsvField(expense.Description);
            var note = EscapeCsvField(expense.Note ?? string.Empty);

            csv.AppendLine($"{expense.Date:yyyy-MM-dd};{type};{category};{description};{CurrencyHelper.FormatInvariant(expense.Amount)};{note}");
        }

        string filePath;
        if (!string.IsNullOrEmpty(targetPath))
        {
            filePath = targetPath;
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
        else
        {
            var exportDir = _fileShareService.GetExportDirectory("FinanzRechner");
            filePath = Path.Combine(exportDir, $"{fileName}.csv");
        }
        await File.WriteAllTextAsync(filePath, csv.ToString(), Encoding.UTF8);
        return filePath;
    }

    public Task<string> ExportStatisticsToPdfAsync(string period, string? targetPath = null)
    {
        // Ohne Datum-Range: Alle Expenses exportieren (Fallback/Legacy)
        return ExportStatisticsToPdfInternal(period, null, null, targetPath);
    }

    public Task<string> ExportStatisticsToPdfAsync(string period, DateTime startDate, DateTime endDate, string? targetPath = null)
    {
        return ExportStatisticsToPdfInternal(period, startDate, endDate, targetPath);
    }

    private async Task<string> ExportStatisticsToPdfInternal(string period, DateTime? startDate, DateTime? endDate, string? targetPath = null)
    {
        // Gefiltert nach Datum-Range, oder alle Expenses wenn kein Range angegeben
        IReadOnlyList<Expense> allExpenses;
        if (startDate.HasValue && endDate.HasValue)
        {
            var filter = new ExpenseFilter { StartDate = startDate.Value, EndDate = endDate.Value };
            allExpenses = await _expenseService.GetExpensesAsync(filter);
        }
        else
        {
            allExpenses = await _expenseService.GetAllExpensesAsync();
        }

        using var document = new PdfDocument();
        var statsTitle = _localizationService.GetString("FinancialStatistics") ?? "Financial Statistics";
        document.Info.Title = $"{statsTitle} - {period}";
        document.Info.Author = "FinanzRechner";

        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Arial", 20, XFontStyle.Bold);
        var headerFont = new XFont("Arial", 14, XFontStyle.Bold);
        var normalFont = new XFont("Arial", 11);
        var smallFont = new XFont("Arial", 9);

        double yPos = 50;
        double leftMargin = 50;
        double pageWidth = page.Width - 100;

        // Titel
        gfx.DrawString($"{_localizationService.GetString("Statistics") ?? "Statistics"} - {period}",
            titleFont, XBrushes.Black, new XRect(leftMargin, yPos, pageWidth, 30), XStringFormats.TopLeft);
        yPos += 40;

        gfx.DrawLine(XPens.Gray, leftMargin, yPos, page.Width - leftMargin, yPos);
        yPos += 20;

        // Zusammenfassung
        var totalExpenses = allExpenses.Where(e => e.Type == TransactionType.Expense).Sum(e => e.Amount);
        var totalIncome = allExpenses.Where(e => e.Type == TransactionType.Income).Sum(e => e.Amount);
        var balance = totalIncome - totalExpenses;

        gfx.DrawString(_localizationService.GetString("Summary") ?? "Summary",
            headerFont, XBrushes.Black, new XRect(leftMargin, yPos, pageWidth, 20), XStringFormats.TopLeft);
        yPos += 25;

        gfx.DrawString($"{_localizationService.GetString("TotalExpenses") ?? "Total Expenses"}:",
            normalFont, XBrushes.Black, leftMargin, yPos);
        gfx.DrawString(CurrencyHelper.Format(totalExpenses),
            normalFont, XBrushes.Red, page.Width - leftMargin, yPos, XStringFormats.TopRight);
        yPos += 20;

        gfx.DrawString($"{_localizationService.GetString("TotalIncome") ?? "Total Income"}:",
            normalFont, XBrushes.Black, leftMargin, yPos);
        gfx.DrawString(CurrencyHelper.Format(totalIncome),
            normalFont, XBrushes.Green, page.Width - leftMargin, yPos, XStringFormats.TopRight);
        yPos += 20;

        gfx.DrawString($"{_localizationService.GetString("Balance") ?? "Balance"}:",
            headerFont, XBrushes.Black, leftMargin, yPos);
        gfx.DrawString(CurrencyHelper.Format(balance),
            headerFont, balance >= 0 ? XBrushes.Green : XBrushes.Red,
            page.Width - leftMargin, yPos, XStringFormats.TopRight);
        yPos += 35;

        // Ausgaben nach Kategorie
        var expensesByCategory = allExpenses
            .Where(e => e.Type == TransactionType.Expense)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount) })
            .OrderByDescending(x => x.Total)
            .ToList();

        if (expensesByCategory.Count > 0)
        {
            gfx.DrawString(_localizationService.GetString("ExpensesByCategory") ?? "Expenses by Category",
                headerFont, XBrushes.Black, new XRect(leftMargin, yPos, pageWidth, 20), XStringFormats.TopLeft);
            yPos += 25;

            foreach (var item in expensesByCategory)
            {
                var categoryName = CategoryLocalizationHelper.GetLocalizedName(item.Category, _localizationService);
                var percentage = totalExpenses > 0 ? (item.Total / totalExpenses * 100) : 0;

                gfx.DrawString($"{categoryName}:", normalFont, XBrushes.Black, leftMargin, yPos);
                gfx.DrawString($"{CurrencyHelper.Format(item.Total)} ({percentage:F1}%)",
                    normalFont, XBrushes.Black, page.Width - leftMargin, yPos, XStringFormats.TopRight);
                yPos += 18;

                if (yPos > page.Height - 100)
                {
                    gfx.Dispose();
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    yPos = 50;
                }
            }
        }

        // Fußzeile
        var footerText = $"{_localizationService.GetString("GeneratedBy") ?? "Generated by"} FinanzRechner - {DateTime.Now.ToString("g", System.Globalization.CultureInfo.CurrentUICulture)}";
        gfx.DrawString(footerText, smallFont, XBrushes.Gray,
            new XRect(0, page.Height - 30, page.Width, 20), XStringFormats.Center);
        gfx.Dispose();

        string filePath;
        if (!string.IsNullOrEmpty(targetPath))
        {
            filePath = targetPath;
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
        else
        {
            var exportDir = _fileShareService.GetExportDirectory("FinanzRechner");
            filePath = Path.Combine(exportDir, $"statistics_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        }
        try
        {
            document.Save(filePath);
        }
        catch (Exception ex)
        {
            throw new IOException(
                $"PDF konnte nicht gespeichert werden: {filePath} - {ex.Message}", ex);
        }
        return filePath;
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;

        // Formula-Injection-Schutz: Felder die mit =, +, -, @ beginnen könnten
        // von Excel als Formel interpretiert werden → Apostroph-Prefix
        if (field.Length > 0 && (field[0] == '=' || field[0] == '+' || field[0] == '-' || field[0] == '@'))
            field = "'" + field;

        // CR/LF normalisieren (CR allein oder CRLF → Leerzeichen)
        field = field.Replace("\r\n", " ").Replace("\r", " ");

        if (field.Contains(';') || field.Contains('"') || field.Contains('\n'))
        {
            field = field.Replace("\"", "\"\"");
            return $"\"{field}\"";
        }
        return field;
    }
}
