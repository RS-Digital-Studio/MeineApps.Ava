using System.Globalization;
using System.Text;
using System.Text.Json;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>CSV + GeoJSON + PDF Export</summary>
public class ExportService : IExportService
{
    // PDF-Fonts
    private static readonly XFont TitleFont = new("Arial", 18, XFontStyle.Bold);
    private static readonly XFont SubtitleFont = new("Arial", 12, XFontStyle.Bold);
    private static readonly XFont HeaderFont = new("Arial", 9, XFontStyle.Bold);
    private static readonly XFont BodyFont = new("Arial", 9, XFontStyle.Regular);
    private static readonly XFont SmallFont = new("Arial", 7.5, XFontStyle.Regular);
    private static readonly XFont FooterFont = new("Arial", 7, XFontStyle.Italic);

    // PDF-Farben
    private static readonly XColor PrimaryColor = XColor.FromArgb(255, 107, 0); // #FF6B00
    private static readonly XColor HeaderBgColor = XColor.FromArgb(42, 42, 64);  // Dunkel
    private static readonly XColor AltRowColor = XColor.FromArgb(245, 245, 250);
    private static readonly XColor BorderColor = XColor.FromArgb(200, 200, 210);

    public string ExportToCsv(SurveyProject project)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Nr;Label;Latitude;Longitude;Hoehe_m;H-Genauigkeit_cm;V-Genauigkeit_cm;Fix;Satelliten;Zeitpunkt");

        int nr = 1;
        foreach (var point in project.Points)
        {
            sb.AppendLine(string.Join(";",
                nr++,
                point.Label ?? "",
                point.Latitude.ToString("F8", CultureInfo.InvariantCulture),
                point.Longitude.ToString("F8", CultureInfo.InvariantCulture),
                point.Altitude.ToString("F3", CultureInfo.InvariantCulture),
                point.HorizontalAccuracy.ToString("F1", CultureInfo.InvariantCulture),
                point.VerticalAccuracy.ToString("F1", CultureInfo.InvariantCulture),
                point.FixQuality,
                point.SatelliteCount,
                point.Timestamp.ToString("O")));
        }

        return sb.ToString();
    }

    public string ExportToGeoJson(SurveyProject project)
    {
        var features = new List<object>();

        // Einzelne Punkte als Features
        foreach (var point in project.Points)
        {
            features.Add(new
            {
                type = "Feature",
                geometry = new
                {
                    type = "Point",
                    coordinates = new[] { point.Longitude, point.Latitude, point.Altitude }
                },
                properties = new
                {
                    label = point.Label ?? "",
                    altitude_m = Math.Round(point.Altitude, 3),
                    h_accuracy_cm = Math.Round(point.HorizontalAccuracy, 1),
                    v_accuracy_cm = Math.Round(point.VerticalAccuracy, 1),
                    fix_quality = point.FixQuality,
                    satellites = point.SatelliteCount,
                    timestamp = point.Timestamp.ToString("O")
                }
            });
        }

        // Polygon wenn >= 3 Punkte
        if (project.Points.Count >= 3)
        {
            var coordinates = project.Points
                .Select(p => new[] { p.Longitude, p.Latitude, p.Altitude })
                .ToList();
            // Polygon schliessen
            coordinates.Add(coordinates[0]);

            features.Add(new
            {
                type = "Feature",
                geometry = new
                {
                    type = "Polygon",
                    coordinates = new[] { coordinates }
                },
                properties = new
                {
                    name = project.Name,
                    area_m2 = Math.Round(project.AreaSquareMeters, 1),
                    perimeter_m = Math.Round(project.PerimeterMeters, 1),
                    point_count = project.PointCount
                }
            });
        }

        var geoJson = new
        {
            type = "FeatureCollection",
            features
        };

        return JsonSerializer.Serialize(geoJson, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public Task<string> ExportPdfAsync(SurveyProject project, List<SurveyPoint> points,
        List<GardenElement> elements, TerrainMesh? mesh, string outputDir)
    {
        return Task.Run(() =>
        {
            var sanitizedName = SanitizeFileName(project.Name);
            var fileName = $"SmartMeasure_{sanitizedName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(outputDir, fileName);

            Directory.CreateDirectory(outputDir);

            using var document = new PdfDocument();
            document.Info.Title = $"SmartMeasure - {project.Name}";
            document.Info.Author = "SmartMeasure";
            document.Info.Subject = "Vermessungsbericht";

            // Seite 1: Projekt-Info + Punkt-Tabelle
            DrawProjectInfoPage(document, project, points, mesh);

            // Seite 2 (optional): Materialliste
            if (elements.Count > 0)
                DrawMaterialPage(document, project, elements);

            document.Save(filePath);
            return filePath;
        });
    }

    /// <summary>Seite 1: Titel, Projekt-Uebersicht, Punkt-Tabelle</summary>
    private static void DrawProjectInfoPage(PdfDocument document, SurveyProject project,
        List<SurveyPoint> points, TerrainMesh? mesh)
    {
        var page = document.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);
        var yPos = 40.0;
        var leftMargin = 40.0;
        var rightMargin = page.Width - 40;
        var contentWidth = rightMargin - leftMargin;

        // Titel-Bereich mit Farbbalken
        gfx.DrawRectangle(new XSolidBrush(PrimaryColor), 0, 0, page.Width, 6);

        // Titel
        gfx.DrawString("SmartMeasure", TitleFont, new XSolidBrush(PrimaryColor),
            leftMargin, yPos + 18);
        gfx.DrawString("Vermessungsbericht", SubtitleFont, XBrushes.DarkGray,
            leftMargin, yPos + 36);
        yPos += 56;

        // Trennlinie
        gfx.DrawLine(new XPen(PrimaryColor, 1.5), leftMargin, yPos, rightMargin, yPos);
        yPos += 16;

        // Projekt-Info Box
        var infoData = new (string label, string value)[]
        {
            ("Projekt:", project.Name),
            ("Typ:", project.ProjectType),
            ("Erstellt:", project.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")),
            ("Geaendert:", project.ModifiedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")),
            ("Messpunkte:", project.PointCount.ToString()),
            ("Flaeche:", $"{project.AreaSquareMeters:F1} m\u00B2"),
            ("Umfang:", $"{project.PerimeterMeters:F1} m"),
        };

        if (mesh != null)
        {
            var heightRange = mesh.MaxZ - mesh.MinZ;
            infoData =
            [
                ..infoData,
                ("Hoehendifferenz:", $"{heightRange:F2} m ({mesh.MinZ:F2} - {mesh.MaxZ:F2} m)"),
                ("Dreiecke:", mesh.TriangleCount.ToString()),
            ];
        }

        // Info-Tabelle (2 Spalten: Label + Wert)
        foreach (var (label, value) in infoData)
        {
            gfx.DrawString(label, HeaderFont, XBrushes.DarkGray, leftMargin, yPos);
            gfx.DrawString(value, BodyFont, XBrushes.Black, leftMargin + 100, yPos);
            yPos += 16;
        }

        yPos += 12;

        // Punkt-Tabelle
        gfx.DrawString("Messpunkte", SubtitleFont, new XSolidBrush(PrimaryColor),
            leftMargin, yPos);
        yPos += 20;

        if (points.Count == 0)
        {
            gfx.DrawString("Keine Messpunkte vorhanden.", BodyFont, XBrushes.Gray,
                leftMargin, yPos);
            yPos += 16;
        }
        else
        {
            // Tabellen-Header
            var colWidths = new double[] { 30, 80, 90, 90, 60, 55, 55, 55 };
            var colHeaders = new[] { "Nr", "Label", "Latitude", "Longitude", "Hoehe m", "H-Acc cm", "V-Acc cm", "Fix" };

            // Header-Zeile
            var headerBrush = new XSolidBrush(HeaderBgColor);
            gfx.DrawRectangle(headerBrush, leftMargin, yPos - 10, contentWidth, 16);

            var xCol = leftMargin + 4;
            for (int c = 0; c < colHeaders.Length; c++)
            {
                gfx.DrawString(colHeaders[c], HeaderFont, XBrushes.White, xCol, yPos);
                xCol += colWidths[c];
            }
            yPos += 14;

            // Datenzeilen
            var altBrush = new XSolidBrush(AltRowColor);
            var borderPen = new XPen(BorderColor, 0.5);

            for (int i = 0; i < points.Count; i++)
            {
                // Seitenumbruch pruefen
                if (yPos > page.Height - 60)
                {
                    DrawFooter(gfx, page, project.Name);
                    page = document.AddPage();
                    page.Size = PdfSharpCore.PageSize.A4;
                    gfx = XGraphics.FromPdfPage(page);
                    yPos = 40;

                    // Header wiederholen
                    gfx.DrawRectangle(headerBrush, leftMargin, yPos - 10, contentWidth, 16);
                    xCol = leftMargin + 4;
                    for (int c = 0; c < colHeaders.Length; c++)
                    {
                        gfx.DrawString(colHeaders[c], HeaderFont, XBrushes.White, xCol, yPos);
                        xCol += colWidths[c];
                    }
                    yPos += 14;
                }

                // Alternierende Zeilenfarbe
                if (i % 2 == 1)
                    gfx.DrawRectangle(altBrush, leftMargin, yPos - 10, contentWidth, 14);

                var pt = points[i];
                var rowData = new[]
                {
                    (i + 1).ToString(),
                    pt.Label ?? "",
                    pt.Latitude.ToString("F8", CultureInfo.InvariantCulture),
                    pt.Longitude.ToString("F8", CultureInfo.InvariantCulture),
                    pt.Altitude.ToString("F3", CultureInfo.InvariantCulture),
                    pt.HorizontalAccuracy.ToString("F1", CultureInfo.InvariantCulture),
                    pt.VerticalAccuracy.ToString("F1", CultureInfo.InvariantCulture),
                    FormatFixQuality(pt.FixQuality)
                };

                xCol = leftMargin + 4;
                for (int c = 0; c < rowData.Length; c++)
                {
                    gfx.DrawString(rowData[c], SmallFont, XBrushes.Black, xCol, yPos);
                    xCol += colWidths[c];
                }

                // Unterlinie
                gfx.DrawLine(borderPen, leftMargin, yPos + 4, rightMargin, yPos + 4);
                yPos += 14;
            }
        }

        DrawFooter(gfx, page, project.Name);
    }

    /// <summary>Seite 2: Gartenelemente + Materialliste</summary>
    private static void DrawMaterialPage(PdfDocument document, SurveyProject project,
        List<GardenElement> elements)
    {
        var page = document.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);
        var yPos = 40.0;
        var leftMargin = 40.0;
        var rightMargin = page.Width - 40;
        var contentWidth = rightMargin - leftMargin;

        // Farbbalken oben
        gfx.DrawRectangle(new XSolidBrush(PrimaryColor), 0, 0, page.Width, 6);

        // Titel
        gfx.DrawString("Gartenelemente", SubtitleFont, new XSolidBrush(PrimaryColor),
            leftMargin, yPos);
        yPos += 24;

        // Element-Tabelle
        var elemColWidths = new double[] { 30, 80, 80, 80, 70, 70, contentWidth - 410 };
        var elemHeaders = new[] { "Nr", "Typ", "Material", "Untertyp", "Flaeche m\u00B2", "Laenge m", "Notizen" };

        // Header
        var headerBrush = new XSolidBrush(HeaderBgColor);
        gfx.DrawRectangle(headerBrush, leftMargin, yPos - 10, contentWidth, 16);
        var xCol = leftMargin + 4;
        for (int c = 0; c < elemHeaders.Length; c++)
        {
            gfx.DrawString(elemHeaders[c], HeaderFont, XBrushes.White, xCol, yPos);
            xCol += elemColWidths[c];
        }
        yPos += 14;

        var altBrush = new XSolidBrush(AltRowColor);
        var borderPen = new XPen(BorderColor, 0.5);

        for (int i = 0; i < elements.Count; i++)
        {
            if (yPos > page.Height - 200) // Platz fuer Materialliste lassen
            {
                DrawFooter(gfx, page, project.Name);
                page = document.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                yPos = 40;
            }

            if (i % 2 == 1)
                gfx.DrawRectangle(altBrush, leftMargin, yPos - 10, contentWidth, 14);

            var elem = elements[i];
            var rowData = new[]
            {
                (i + 1).ToString(),
                elem.ElementType.ToString(),
                elem.Material,
                elem.SubType,
                elem.AreaSquareMeters > 0 ? elem.AreaSquareMeters.ToString("F1", CultureInfo.InvariantCulture) : "—",
                elem.LengthMeters > 0 ? elem.LengthMeters.ToString("F1", CultureInfo.InvariantCulture) : "—",
                elem.Notes ?? ""
            };

            xCol = leftMargin + 4;
            for (int c = 0; c < rowData.Length; c++)
            {
                gfx.DrawString(rowData[c], SmallFont, XBrushes.Black, xCol, yPos);
                xCol += elemColWidths[c];
            }

            gfx.DrawLine(borderPen, leftMargin, yPos + 4, rightMargin, yPos + 4);
            yPos += 14;
        }

        yPos += 20;

        // Materialliste (berechnet aus Elementen)
        gfx.DrawString("Materialliste (inkl. 15% Sicherheitszuschlag)", SubtitleFont,
            new XSolidBrush(PrimaryColor), leftMargin, yPos);
        yPos += 24;

        // Material-Schaetzungen direkt berechnen (gleiche Logik wie GardenPlanService)
        var materialSummary = CalculateMaterialSummary(elements);

        if (materialSummary.Count == 0)
        {
            gfx.DrawString("Keine Materialien berechnet.", BodyFont, XBrushes.Gray,
                leftMargin, yPos);
        }
        else
        {
            var matColWidths = new double[] { contentWidth * 0.5, contentWidth * 0.25, contentWidth * 0.25 };
            var matHeaders = new[] { "Material", "Menge (inkl. 15%)", "Einheit" };

            // Header
            gfx.DrawRectangle(headerBrush, leftMargin, yPos - 10, contentWidth, 16);
            xCol = leftMargin + 4;
            for (int c = 0; c < matHeaders.Length; c++)
            {
                gfx.DrawString(matHeaders[c], HeaderFont, XBrushes.White, xCol, yPos);
                xCol += matColWidths[c];
            }
            yPos += 14;

            for (int i = 0; i < materialSummary.Count; i++)
            {
                if (yPos > page.Height - 60)
                {
                    DrawFooter(gfx, page, project.Name);
                    page = document.AddPage();
                    page.Size = PdfSharpCore.PageSize.A4;
                    gfx = XGraphics.FromPdfPage(page);
                    yPos = 40;
                }

                if (i % 2 == 1)
                    gfx.DrawRectangle(altBrush, leftMargin, yPos - 10, contentWidth, 14);

                var (material, quantity, unit) = materialSummary[i];
                var safetyQuantity = quantity * 1.15;

                xCol = leftMargin + 4;
                gfx.DrawString(material, SmallFont, XBrushes.Black, xCol, yPos);
                xCol += matColWidths[0];
                gfx.DrawString(safetyQuantity.ToString("F1", CultureInfo.InvariantCulture),
                    SmallFont, XBrushes.Black, xCol, yPos);
                xCol += matColWidths[1];
                gfx.DrawString(unit, SmallFont, XBrushes.Black, xCol, yPos);

                gfx.DrawLine(borderPen, leftMargin, yPos + 4, rightMargin, yPos + 4);
                yPos += 14;
            }
        }

        DrawFooter(gfx, page, project.Name);
    }

    /// <summary>Fusszeile mit Projektname und Seitenzahl</summary>
    private static void DrawFooter(XGraphics gfx, PdfPage page, string projectName)
    {
        var footerY = page.Height - 30;
        gfx.DrawLine(new XPen(BorderColor, 0.5), 40, footerY - 4, page.Width - 40, footerY - 4);
        gfx.DrawString($"SmartMeasure — {projectName}", FooterFont, XBrushes.Gray, 40, footerY);
        gfx.DrawString($"Erstellt: {DateTime.Now:dd.MM.yyyy HH:mm}", FooterFont, XBrushes.Gray,
            page.Width - 170, footerY);
    }

    /// <summary>Fix-Quality als lesbaren Text formatieren</summary>
    private static string FormatFixQuality(int fix) => fix switch
    {
        0 => "Kein Fix",
        1 => "GPS",
        2 => "DGPS",
        4 => "RTK-Fix",
        5 => "RTK-Float",
        10 => "AR",
        _ => fix.ToString()
    };

    /// <summary>Material-Zusammenfassung aus Elementen berechnen (gleiche Materialien zusammenfassen)</summary>
    private static List<(string material, double quantity, string unit)> CalculateMaterialSummary(
        List<GardenElement> elements)
    {
        var grouped = new Dictionary<string, (double quantity, string unit)>();

        foreach (var elem in elements)
        {
            switch (elem.ElementType)
            {
                case GardenElementType.Weg:
                    AddMaterial(grouped, $"{elem.Material} Belag", elem.AreaSquareMeters, "m\u00B2");
                    if (elem.LayerThicknessCm > 0)
                        AddMaterial(grouped, $"Unterbau ({elem.Material})",
                            elem.AreaSquareMeters * elem.LayerThicknessCm / 100.0, "m\u00B3");
                    break;

                case GardenElementType.Beet:
                    AddMaterial(grouped, "Beet-Flaeche", elem.AreaSquareMeters, "m\u00B2");
                    AddMaterial(grouped, "Muttererde",
                        elem.AreaSquareMeters * (elem.LayerThicknessCm > 0 ? elem.LayerThicknessCm : 30f) / 100.0, "m\u00B3");
                    break;

                case GardenElementType.Rasen:
                    AddMaterial(grouped, "Rasen", elem.AreaSquareMeters, "m\u00B2");
                    break;

                case GardenElementType.Mauer:
                case GardenElementType.Zaun:
                    AddMaterial(grouped, $"{elem.Material} {elem.ElementType}", elem.LengthMeters, "lfm");
                    if (elem.Height > 0)
                        AddMaterial(grouped, $"{elem.Material} Flaeche",
                            elem.LengthMeters * elem.Height, "m\u00B2");
                    break;

                case GardenElementType.Terrasse:
                    AddMaterial(grouped, $"{elem.Material} Terrasse", elem.AreaSquareMeters, "m\u00B2");
                    if (elem.LayerThicknessCm > 0)
                        AddMaterial(grouped, "Unterbau Terrasse",
                            elem.AreaSquareMeters * elem.LayerThicknessCm / 100.0, "m\u00B3");
                    break;
            }
        }

        return grouped.Select(kvp => (kvp.Key, kvp.Value.quantity, kvp.Value.unit)).ToList();
    }

    /// <summary>Material zu Dictionary addieren (gleiche zusammenfassen)</summary>
    private static void AddMaterial(Dictionary<string, (double quantity, string unit)> dict,
        string material, double quantity, string unit)
    {
        if (quantity <= 0) return;
        if (dict.TryGetValue(material, out var existing))
            dict[material] = (existing.quantity + quantity, unit);
        else
            dict[material] = (quantity, unit);
    }

    /// <summary>Dateiname bereinigen (Sonderzeichen entfernen)</summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (Array.IndexOf(invalid, c) < 0)
                sanitized.Append(c);
            else
                sanitized.Append('_');
        }
        return sanitized.ToString();
    }
}
