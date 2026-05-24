using System.IO;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.14: Vermessungs-PDF-Bericht via PdfSharpCore. Layout mit
/// Titelblatt, Projekt-Metadaten, Punkt-Tabelle mit Foto-Thumbnails (aus
/// <see cref="IAppPaths.PhotosFolder"/>), Konturen-Liste, Material-Schaetzung
/// (optional via <see cref="IVolumeService"/>) und Differential-Block (optional via
/// <see cref="IDifferentialSnapshotService"/>).</summary>
public sealed class SurveyReportService : ISurveyReportService
{
    private readonly IAppPaths _appPaths;
    private readonly IProjectService _projectService;
    private readonly IVolumeService _volumeService;
    private readonly IDifferentialSnapshotService _differentialService;

    private static readonly XColor PrimaryColor = XColor.FromArgb(255, 255, 107, 0);

    public SurveyReportService(IAppPaths appPaths, IProjectService projectService,
        IVolumeService volumeService, IDifferentialSnapshotService differentialService)
    {
        _appPaths = appPaths;
        _projectService = projectService;
        _volumeService = volumeService;
        _differentialService = differentialService;
    }

    public Task<string> GeneratePdfReportAsync(SurveyProject project, SurveyReportOptions options, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var fileName = $"SurveyReport_{Sanitize(project.Name)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(_appPaths.ExportFolder, fileName);
            Directory.CreateDirectory(_appPaths.ExportFolder);

            using var doc = new PdfDocument();
            doc.Info.Title = $"Vermessungsbericht — {project.Name}";
            doc.Info.Author = options.SurveyorName;
            doc.Info.Subject = "SmartMeasure Vermessung";

            DrawCoverPage(doc, project, options);
            DrawPointsPage(doc, project, options);
            if (options.IncludeMaterials && project.Points.Count > 0)
                DrawMaterialEstimatePage(doc, project);
            if (options.IncludeDifferential && options.PreviousSession != null)
                DrawDifferentialPage(doc, project, options.PreviousSession);

            doc.Save(filePath);
            return filePath;
        }, ct);
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    private static void DrawCoverPage(PdfDocument doc, SurveyProject project, SurveyReportOptions o)
    {
        var page = doc.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);
        var leftMargin = 50.0;

        gfx.DrawRectangle(new XSolidBrush(PrimaryColor), 0, 0, page.Width, 12);
        gfx.DrawString("SmartMeasure", new XFont("Verdana", 28, XFontStyle.Bold),
            new XSolidBrush(PrimaryColor), leftMargin, 80);
        gfx.DrawString("Vermessungsbericht", new XFont("Verdana", 18, XFontStyle.Regular),
            XBrushes.DarkGray, leftMargin, 110);

        var yPos = 180.0;
        var rows = new[]
        {
            ("Projekt", project.Name),
            ("Auftraggeber", o.ClientName),
            ("Vermesser", o.SurveyorName),
            ("Genauigkeits-Klasse", o.AccuracyClass),
            ("Punkte erfasst", project.Points.Count.ToString()),
            ("Erstellt am", DateTime.Now.ToString("dd.MM.yyyy HH:mm")),
        };
        var labelFont = new XFont("Verdana", 11, XFontStyle.Bold);
        var valueFont = new XFont("Verdana", 11, XFontStyle.Regular);
        foreach (var (label, value) in rows)
        {
            gfx.DrawString(label + ":", labelFont, XBrushes.Black, leftMargin, yPos);
            gfx.DrawString(value, valueFont, XBrushes.Black, leftMargin + 160, yPos);
            yPos += 22;
        }
    }

    private void DrawPointsPage(PdfDocument doc, SurveyProject project, SurveyReportOptions o)
    {
        var page = doc.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);
        var leftMargin = 30.0;
        var yPos = 40.0;

        gfx.DrawString("Punkte", new XFont("Verdana", 16, XFontStyle.Bold),
            new XSolidBrush(PrimaryColor), leftMargin, yPos);
        yPos += 24;

        var header = new XFont("Verdana", 9, XFontStyle.Bold);
        var body = new XFont("Verdana", 9, XFontStyle.Regular);
        gfx.DrawString("#", header, XBrushes.Black, leftMargin, yPos);
        gfx.DrawString("Lat", header, XBrushes.Black, leftMargin + 30, yPos);
        gfx.DrawString("Lon", header, XBrushes.Black, leftMargin + 110, yPos);
        gfx.DrawString("Hoehe", header, XBrushes.Black, leftMargin + 190, yPos);
        gfx.DrawString("±H", header, XBrushes.Black, leftMargin + 240, yPos);
        gfx.DrawString("Label", header, XBrushes.Black, leftMargin + 285, yPos);
        gfx.DrawString("Foto", header, XBrushes.Black, leftMargin + 420, yPos);
        yPos += 14;
        gfx.DrawLine(XPens.Gray, leftMargin, yPos, page.Width - leftMargin, yPos);
        yPos += 6;

        var idx = 1;
        foreach (var p in project.Points)
        {
            if (yPos > page.Height - 60)
            {
                page = doc.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                yPos = 40;
            }

            gfx.DrawString(idx.ToString(), body, XBrushes.Black, leftMargin, yPos);
            gfx.DrawString(p.Latitude.ToString("F7"), body, XBrushes.Black, leftMargin + 30, yPos);
            gfx.DrawString(p.Longitude.ToString("F7"), body, XBrushes.Black, leftMargin + 110, yPos);
            gfx.DrawString(p.Altitude.ToString("F2") + " m", body, XBrushes.Black, leftMargin + 190, yPos);
            gfx.DrawString(p.HorizontalAccuracy.ToString("F1") + " cm", body, XBrushes.Black, leftMargin + 240, yPos);
            gfx.DrawString(p.Label ?? "-", body, XBrushes.Black, leftMargin + 285, yPos);

            // Foto-Thumbnail wenn vorhanden
            if (o.IncludePhotos && !string.IsNullOrEmpty(p.PhotoPath))
            {
                var photoFull = Path.Combine(_appPaths.PhotosFolder, p.PhotoPath);
                if (File.Exists(photoFull))
                {
                    try
                    {
                        var img = XImage.FromFile(photoFull);
                        gfx.DrawImage(img, leftMargin + 420, yPos - 16, 80, 60);
                        yPos += 50; // Mehr Platz fuer Thumbnail
                    }
                    catch { /* harmlos — Foto kaputt */ }
                }
            }
            yPos += 14;
            idx++;
        }
    }

    private void DrawMaterialEstimatePage(PdfDocument doc, SurveyProject project)
    {
        var elements = project.GardenElements;
        if (elements == null || elements.Count == 0) return;

        var page = doc.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);
        var leftMargin = 40.0;
        var yPos = 40.0;

        gfx.DrawString("Material-Schaetzung", new XFont("Verdana", 16, XFontStyle.Bold),
            new XSolidBrush(PrimaryColor), leftMargin, yPos);
        yPos += 30;

        var defaultDepth = 0.30;
        var body = new XFont("Verdana", 10, XFontStyle.Regular);
        var headerF = new XFont("Verdana", 11, XFontStyle.Bold);

        foreach (var el in elements)
        {
            if (yPos > page.Height - 80)
            {
                page = doc.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                yPos = 40;
            }

            gfx.DrawString($"{el.ElementType} — {el.Notes}", headerF, XBrushes.Black, leftMargin, yPos);
            yPos += 16;
            try
            {
                var est = _volumeService.EstimatePrism(el, defaultDepth);
                gfx.DrawString($"  Flaeche: {est.BaseAreaSquareMeters:F2} m²  ·  Volumen (Tiefe {defaultDepth:F2}m): {est.VolumeCubicMeters:F2} m³",
                    body, XBrushes.Black, leftMargin, yPos);
                yPos += 16;
                foreach (var mat in est.MaterialOptions.Take(4))
                {
                    gfx.DrawString($"    {mat.Material}: {mat.Tonnes:F2} t",
                        body, XBrushes.Gray, leftMargin, yPos);
                    yPos += 13;
                }
            }
            catch (Exception ex)
            {
                gfx.DrawString($"  Schaetzung fehlgeschlagen: {ex.Message}", body, XBrushes.Red,
                    leftMargin, yPos);
                yPos += 16;
            }
            yPos += 8;
        }
    }

    private void DrawDifferentialPage(PdfDocument doc, SurveyProject current, SurveyProject previous)
    {
        var page = doc.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);
        var leftMargin = 40.0;
        var yPos = 40.0;

        gfx.DrawString("Differential-Vergleich", new XFont("Verdana", 16, XFontStyle.Bold),
            new XSolidBrush(PrimaryColor), leftMargin, yPos);
        yPos += 24;

        gfx.DrawString($"Vorherige Session: {previous.Name}",
            new XFont("Verdana", 11, XFontStyle.Regular), XBrushes.Black, leftMargin, yPos);
        yPos += 24;

        var result = _differentialService.Compare(previous.Points, current.Points);
        var moved = result.Matches.Count(m => m.Change == DifferentialChange.Moved);
        var unchanged = result.Matches.Count - moved;

        var stats = new[]
        {
            $"Unveraendert: {unchanged}",
            $"Verschoben (>10cm): {moved}",
            $"Neu hinzugekommen: {result.Added.Count}",
            $"Verschwunden: {result.Removed.Count}",
        };
        foreach (var s in stats)
        {
            gfx.DrawString(s, new XFont("Verdana", 11, XFontStyle.Regular), XBrushes.Black, leftMargin, yPos);
            yPos += 18;
        }
    }
}
