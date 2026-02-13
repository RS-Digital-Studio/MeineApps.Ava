using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace HandwerkerRechner.Services;

/// <summary>
/// Erstellt PDF-Dokumente fuer Materiallisten und Projektberichte.
/// Nutzt PdfSharpCore fuer plattformuebergreifende PDF-Generierung.
/// </summary>
public class MaterialExportService : IMaterialExportService
{
    private readonly ILocalizationService _localization;
    private readonly IFileShareService _fileShareService;

    // Farben
    private static readonly XColor HeaderColor = XColor.FromArgb(255, 99, 102, 241); // Indigo Primary
    private static readonly XColor TextColor = XColor.FromArgb(255, 30, 30, 30);
    private static readonly XColor LabelColor = XColor.FromArgb(255, 100, 100, 100);
    private static readonly XColor LineColor = XColor.FromArgb(255, 220, 220, 220);

    public MaterialExportService(ILocalizationService localization, IFileShareService fileShareService)
    {
        _localization = localization;
        _fileShareService = fileShareService;
    }

    public Task<string> ExportToPdfAsync(string calculatorType, Dictionary<string, string> inputs, Dictionary<string, string> results)
    {
        var title = _localization.GetString("MaterialList") ?? "Material List";
        return GeneratePdfAsync(title, calculatorType, null, inputs, results);
    }

    public Task<string> ExportProjectToPdfAsync(string projectName, string calculatorType, Dictionary<string, string> inputs, Dictionary<string, string> results)
    {
        var title = _localization.GetString("ProjectReport") ?? "Project Report";
        return GeneratePdfAsync(title, calculatorType, projectName, inputs, results);
    }

    private Task<string> GeneratePdfAsync(string documentTitle, string calculatorType, string? projectName, Dictionary<string, string> inputs, Dictionary<string, string> results)
    {
        return Task.Run(() =>
        {
            var document = new PdfDocument();
            document.Info.Title = documentTitle;
            document.Info.Author = "HandwerkerRechner";

            var page = document.AddPage();
            page.Width = XUnit.FromMillimeter(210);  // A4
            page.Height = XUnit.FromMillimeter(297);
            var gfx = XGraphics.FromPdfPage(page);

            // Schriftarten
            var fontTitle = new XFont("Arial", 20, XFontStyle.Bold);
            var fontSubtitle = new XFont("Arial", 14, XFontStyle.Regular);
            var fontSection = new XFont("Arial", 12, XFontStyle.Bold);
            var fontBody = new XFont("Arial", 10, XFontStyle.Regular);
            var fontSmall = new XFont("Arial", 8, XFontStyle.Regular);

            double x = 50;
            double y = 50;
            double pageWidth = page.Width.Point - 100;
            double maxY = page.Height.Point - 60; // Unterer Seitenrand

            // Hilfsfunktion: Seitenumbruch wenn nötig
            void EnsureSpace(double needed)
            {
                if (y + needed <= maxY) return;
                // Neue Seite erstellen
                gfx.Dispose();
                page = document.AddPage();
                page.Width = XUnit.FromMillimeter(210);
                page.Height = XUnit.FromMillimeter(297);
                gfx = XGraphics.FromPdfPage(page);
                y = 50;
            }

            // Header-Balken
            gfx.DrawRectangle(new XSolidBrush(HeaderColor), 0, 0, page.Width.Point, 80);

            // Titel
            gfx.DrawString(documentTitle, fontTitle, XBrushes.White, new XPoint(x, 35));

            // Rechner-Typ
            gfx.DrawString(calculatorType, fontSubtitle, XBrushes.White, new XPoint(x, 60));

            y = 100;

            // Projektname (wenn vorhanden)
            if (!string.IsNullOrEmpty(projectName))
            {
                var projectLabel = _localization.GetString("ProjectName") ?? "Project name:";
                gfx.DrawString($"{projectLabel} {projectName}", fontSection, new XSolidBrush(TextColor), new XPoint(x, y));
                y += 25;
            }

            // Datum
            var dateLabel = _localization.GetString("Date") ?? "Date";
            gfx.DrawString($"{dateLabel}: {DateTime.Now:dd.MM.yyyy HH:mm}", fontBody, new XSolidBrush(LabelColor), new XPoint(x, y));
            y += 30;

            // Trennlinie
            gfx.DrawLine(new XPen(LineColor, 1), x, y, x + pageWidth, y);
            y += 20;

            // Eingabewerte
            EnsureSpace(40);
            var inputsLabel = _localization.GetString("RoomDimensions") ?? "Input Values";
            gfx.DrawString(inputsLabel, fontSection, new XSolidBrush(HeaderColor), new XPoint(x, y));
            y += 20;

            foreach (var input in inputs)
            {
                EnsureSpace(18);
                gfx.DrawString(input.Key, fontBody, new XSolidBrush(LabelColor), new XPoint(x + 10, y));
                gfx.DrawString(input.Value, fontBody, new XSolidBrush(TextColor), new XPoint(x + 200, y));
                y += 18;
            }

            y += 15;

            // Trennlinie
            EnsureSpace(40);
            gfx.DrawLine(new XPen(LineColor, 1), x, y, x + pageWidth, y);
            y += 20;

            // Ergebnisse
            var resultLabel = _localization.GetString("Result") ?? "Result";
            gfx.DrawString(resultLabel, fontSection, new XSolidBrush(HeaderColor), new XPoint(x, y));
            y += 20;

            foreach (var result in results)
            {
                EnsureSpace(18);
                gfx.DrawString(result.Key, fontBody, new XSolidBrush(LabelColor), new XPoint(x + 10, y));
                gfx.DrawString(result.Value, fontBody, new XSolidBrush(TextColor), new XPoint(x + 200, y));
                y += 18;
            }

            y += 30;

            // Trennlinie
            EnsureSpace(30);
            gfx.DrawLine(new XPen(LineColor, 1), x, y, x + pageWidth, y);
            y += 15;

            // Footer
            var generatedBy = _localization.GetString("GeneratedBy") ?? "Generated by HandwerkerRechner";
            gfx.DrawString(generatedBy, fontSmall, new XSolidBrush(LabelColor), new XPoint(x, y));

            // Letztes Graphics-Objekt freigeben vor dem Speichern
            gfx?.Dispose();

            // Speichern
            var exportDir = _fileShareService.GetExportDirectory("HandwerkerRechner");
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var safeCalcType = string.Join("_", calculatorType.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{safeCalcType}_{timestamp}.pdf";
            var filePath = Path.Combine(exportDir, fileName);

            document.Save(filePath);

            return filePath;
        });
    }
}
