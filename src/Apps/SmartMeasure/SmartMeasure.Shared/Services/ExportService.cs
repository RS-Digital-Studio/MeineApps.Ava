using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>CSV + GeoJSON + DXF + KMZ + PDF Export</summary>
public class ExportService : IExportService
{
    private readonly ICoordinateService _coordinateService;
    private readonly IGardenPlanService _gardenPlanService;

    public ExportService(ICoordinateService coordinateService, IGardenPlanService gardenPlanService)
    {
        _coordinateService = coordinateService;
        _gardenPlanService = gardenPlanService;
    }

    // PDF-Fonts werden lazy initialisiert (PdfSharpCore braucht registrierten FontResolver,
    // der auf Android nicht automatisch verfuegbar ist → static-Init wuerde crashen)
    private static XFont? _titleFont;
    private static XFont? _subtitleFont;
    private static XFont? _headerFont;
    private static XFont? _bodyFont;
    private static XFont? _smallFont;
    private static XFont? _footerFont;
    private static bool _fontsInitialized;

    private static XFont TitleFont => _titleFont ??= new("Arial", 18, XFontStyle.Bold);
    private static XFont SubtitleFont => _subtitleFont ??= new("Arial", 12, XFontStyle.Bold);
    private static XFont HeaderFont => _headerFont ??= new("Arial", 9, XFontStyle.Bold);
    private static XFont BodyFont => _bodyFont ??= new("Arial", 9, XFontStyle.Regular);
    private static XFont SmallFont => _smallFont ??= new("Arial", 7.5, XFontStyle.Regular);
    private static XFont FooterFont => _footerFont ??= new("Arial", 7, XFontStyle.Italic);

    // PDF-Farben (keine PdfSharp-Abhaengigkeit, sicher als static)
    private static readonly XColor PrimaryColor = XColor.FromArgb(255, 107, 0); // #FF6B00
    private static readonly XColor HeaderBgColor = XColor.FromArgb(42, 42, 64);  // Dunkel
    private static readonly XColor AltRowColor = XColor.FromArgb(245, 245, 250);
    private static readonly XColor BorderColor = XColor.FromArgb(200, 200, 210);

    /// <summary>FontResolver fuer Android registrieren (muss vor dem ersten PDF-Export aufgerufen werden)</summary>
    public static void EnsureFontResolver()
    {
        if (_fontsInitialized) return;
        _fontsInitialized = true;

        try
        {
            // Pruefen ob bereits ein Resolver gesetzt ist (Desktop hat einen Default)
            _ = GlobalFontSettings.FontResolver;
        }
        catch
        {
            // Android hat keinen Default-Resolver → Fallback registrieren
            GlobalFontSettings.FontResolver = new AndroidFontResolver();
        }
    }

    /// <summary>Einfacher Font-Resolver fuer Android (/system/fonts/).
    /// Mapped alle Font-Anfragen auf Roboto (auf Android immer verfuegbar).</summary>
    private class AndroidFontResolver : IFontResolver
    {
        public string DefaultFontName => "Roboto";

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Alle Font-Anfragen auf Roboto mappen (Android-System-Font)
            var suffix = isBold && isItalic ? "-BoldItalic" : isBold ? "-Bold" : isItalic ? "-Italic" : "-Regular";
            var faceName = $"Roboto{suffix}";
            return new FontResolverInfo(faceName, isBold, isItalic);
        }

        public byte[] GetFont(string faceName)
        {
            // Roboto-Varianten aus /system/fonts/ laden
            var paths = new[]
            {
                $"/system/fonts/{faceName}.ttf",
                "/system/fonts/Roboto-Regular.ttf",
                "/system/fonts/DroidSans.ttf"
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                    return File.ReadAllBytes(path);
            }

            // Letzter Fallback: irgendeinen .ttf Font nehmen
            var fallback = Directory.GetFiles("/system/fonts/", "*.ttf").FirstOrDefault();
            if (fallback != null)
                return File.ReadAllBytes(fallback);

            throw new FileNotFoundException($"Kein Font gefunden fuer: {faceName}");
        }
    }

    public string ExportToCsv(SurveyProject project)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Nr;Label;Latitude;Longitude;Hoehe_m;H-Genauigkeit_cm;V-Genauigkeit_cm;Fix;Satelliten;Zeitpunkt");

        int nr = 1;
        foreach (var point in project.Points)
        {
            // RFC 4180-kompatibles Quoting: Felder mit Semikolon, Newline oder " werden gequoted,
            // " im Text wird verdoppelt. Ohne das zerstören Labels wie 'Ecke; Baum' die Struktur.
            sb.AppendLine(string.Join(";",
                nr++.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(point.Label ?? string.Empty),
                point.Latitude.ToString("F8", CultureInfo.InvariantCulture),
                point.Longitude.ToString("F8", CultureInfo.InvariantCulture),
                point.Altitude.ToString("F3", CultureInfo.InvariantCulture),
                point.HorizontalAccuracy.ToString("F1", CultureInfo.InvariantCulture),
                point.VerticalAccuracy.ToString("F1", CultureInfo.InvariantCulture),
                point.FixQuality.ToString(CultureInfo.InvariantCulture),
                point.SatelliteCount.ToString(CultureInfo.InvariantCulture),
                point.Timestamp.ToString("O")));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Quoted Feld gemäß RFC 4180 wenn es problematische Zeichen enthält.
    /// Trennzeichen = Semikolon (europäisches CSV).
    /// </summary>
    private static string EscapeCsv(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;

        var needsQuoting = field.IndexOfAny([';', '"', '\n', '\r']) >= 0;
        if (!needsQuoting) return field;

        return "\"" + field.Replace("\"", "\"\"") + "\"";
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
            WriteIndented = true
        });
    }

    public Task<string> ExportPdfAsync(SurveyProject project, List<SurveyPoint> points,
        List<GardenElement> elements, TerrainMesh? mesh, string outputDir)
    {
        EnsureFontResolver();
        return Task.Run(() =>
        {
            var sanitizedName = SanitizeFileName(project.Name);
            var fileName = $"SmartMeasure_{sanitizedName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
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
        gfx.DrawString($"Erstellt: {DateTime.UtcNow.ToLocalTime():dd.MM.yyyy HH:mm}", FooterFont, XBrushes.Gray,
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

    #region DXF Export

    /// <summary>
    /// AutoCAD DXF R12 ASCII-Format. Kein NuGet, ~200 Zeilen direktes Schreiben.
    /// Koordinaten: UTM-Easting/Northing in Metern, relativ zum Projekt-Schwerpunkt
    /// (sonst hätten Koordinaten 7-stellige Werte, die meisten CAD-Tools
    /// nicht gut visualisieren). Layer-Prefixes: P_ (Punkte), G_ (Gartenelemente).
    /// </summary>
    public string ExportToDxf(SurveyProject project)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        // Referenz-Schwerpunkt aus Messpunkten — fixiert die UTM-Zone + bringt Koords
        // in kleine Zahlen (lokaler Nullpunkt).
        var points = project.Points;
        var elements = project.GardenElements;
        var (refLat, refLon) = ComputeReference(points, elements);

        // Layer-Typen sammeln für TABLES-Section
        var elementLayers = elements.Select(e => e.ElementType).Distinct().ToList();

        // HEADER
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("HEADER");
        sb.AppendLine("9");
        sb.AppendLine("$ACADVER");
        sb.AppendLine("1");
        sb.AppendLine("AC1009"); // AutoCAD R12 — breitester Kompatibilitätsgrad
        sb.AppendLine("9");
        sb.AppendLine("$INSUNITS");
        sb.AppendLine("70");
        sb.AppendLine("6"); // 6 = Meter
        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");

        // TABLES - Layer-Definitionen
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("TABLES");
        sb.AppendLine("0");
        sb.AppendLine("TABLE");
        sb.AppendLine("2");
        sb.AppendLine("LAYER");

        WriteLayerDefinition(sb, "P_Messpunkte", 1);  // Rot
        WriteLayerDefinition(sb, "P_Labels", 7);       // Weiß
        var colorMap = new Dictionary<GardenElementType, int>
        {
            [GardenElementType.Weg] = 8,        // Grau
            [GardenElementType.Beet] = 42,       // Braun
            [GardenElementType.Rasen] = 3,       // Grün
            [GardenElementType.Mauer] = 5,       // Blau
            [GardenElementType.Zaun] = 6,        // Magenta
            [GardenElementType.Terrasse] = 2,    // Gelb
            [GardenElementType.Grenze] = 1,      // Rot
            [GardenElementType.Gebaeude] = 9,    // Hellgrau
            [GardenElementType.Wasser] = 4,      // Cyan
            [GardenElementType.Kante] = 7,       // Weiß
        };
        foreach (var layer in elementLayers)
            WriteLayerDefinition(sb, $"G_{layer}", colorMap.GetValueOrDefault(layer, 7));

        sb.AppendLine("0");
        sb.AppendLine("ENDTAB");
        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");

        // ENTITIES
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("ENTITIES");

        // Messpunkte als POINT + optional TEXT-Label
        foreach (var p in points)
        {
            var (x, y, _) = _coordinateService.LatLonToLocal(p.Latitude, p.Longitude, p.Altitude,
                refLat, refLon, 0);
            WriteDxfPoint(sb, inv, "P_Messpunkte", x, y, p.Altitude);
            if (!string.IsNullOrWhiteSpace(p.Label))
                WriteDxfText(sb, inv, "P_Labels", x + 0.2, y + 0.2, 0.3, p.Label);
        }

        // Gartenelemente als LWPOLYLINE (geschlossen für Flächen, offen für Linien)
        foreach (var el in elements)
        {
            var localPoints = _gardenPlanService.GetLocalPoints(el, refLat, refLon, _coordinateService);
            if (localPoints.Count < 2) continue;

            var closed = el.ElementType is GardenElementType.Beet or GardenElementType.Rasen
                or GardenElementType.Terrasse or GardenElementType.Gebaeude or GardenElementType.Wasser;

            WriteDxfPolyline(sb, inv, $"G_{el.ElementType}", localPoints, closed);
        }

        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");
        sb.AppendLine("0");
        sb.AppendLine("EOF");

        return sb.ToString();
    }

    private static void WriteLayerDefinition(StringBuilder sb, string name, int color)
    {
        sb.AppendLine("0");
        sb.AppendLine("LAYER");
        sb.AppendLine("2");
        sb.AppendLine(name);
        sb.AppendLine("70");
        sb.AppendLine("0"); // Flags: sichtbar, nicht gesperrt
        sb.AppendLine("62");
        sb.AppendLine(color.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("6");
        sb.AppendLine("CONTINUOUS");
    }

    private static void WriteDxfPoint(StringBuilder sb, CultureInfo inv, string layer,
        double x, double y, double z)
    {
        sb.AppendLine("0");
        sb.AppendLine("POINT");
        sb.AppendLine("8"); sb.AppendLine(layer);
        sb.AppendLine("10"); sb.AppendLine(x.ToString("F4", inv));
        sb.AppendLine("20"); sb.AppendLine(y.ToString("F4", inv));
        sb.AppendLine("30"); sb.AppendLine(z.ToString("F4", inv));
    }

    private static void WriteDxfText(StringBuilder sb, CultureInfo inv, string layer,
        double x, double y, double height, string text)
    {
        sb.AppendLine("0");
        sb.AppendLine("TEXT");
        sb.AppendLine("8"); sb.AppendLine(layer);
        sb.AppendLine("10"); sb.AppendLine(x.ToString("F4", inv));
        sb.AppendLine("20"); sb.AppendLine(y.ToString("F4", inv));
        sb.AppendLine("30"); sb.AppendLine("0.0000");
        sb.AppendLine("40"); sb.AppendLine(height.ToString("F4", inv));
        sb.AppendLine("1"); sb.AppendLine(text);
    }

    private static void WriteDxfPolyline(StringBuilder sb, CultureInfo inv, string layer,
        List<(double x, double y)> pts, bool closed)
    {
        sb.AppendLine("0");
        sb.AppendLine("LWPOLYLINE");
        sb.AppendLine("8"); sb.AppendLine(layer);
        sb.AppendLine("90"); sb.AppendLine(pts.Count.ToString(CultureInfo.InvariantCulture)); // Vertex-Count
        sb.AppendLine("70"); sb.AppendLine(closed ? "1" : "0"); // Closed-Flag
        foreach (var (x, y) in pts)
        {
            sb.AppendLine("10"); sb.AppendLine(x.ToString("F4", inv));
            sb.AppendLine("20"); sb.AppendLine(y.ToString("F4", inv));
        }
    }

    #endregion

    #region KMZ Export

    /// <summary>
    /// KMZ = ZIP-Archiv mit einer doc.kml-Datei drin. Google Earth / Maps kompatibel.
    /// Placemarks für Punkte mit Labels + LineString für Polygon-Umrandung des Grundstücks.
    /// Koordinaten direkt WGS84 (Longitude, Latitude, Altitude) — KML-Standard.
    /// </summary>
    public async Task<string> ExportToKmzAsync(SurveyProject project, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var sanitized = SanitizeFileName(project.Name);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var kmzPath = Path.Combine(outputDir, $"{sanitized}_{timestamp}.kmz");

        var kml = BuildKmlContent(project);

        // ZIP schreiben (synchrones I/O in Task gekapselt — Android-Storage kann langsam sein)
        await Task.Run(() =>
        {
            using var fs = File.Create(kmzPath);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create);
            var entry = archive.CreateEntry("doc.kml", CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(kml);
        });

        return kmzPath;
    }

    private string BuildKmlContent(SurveyProject project)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
        sb.AppendLine("<Document>");
        sb.AppendLine($"  <name>{EscapeXml(project.Name)}</name>");
        sb.AppendLine($"  <description>SmartMeasure Export — {project.PointCount} Punkte, " +
                      $"{project.AreaSquareMeters.ToString("F1", inv)} m²</description>");

        // Style für Messpunkte (orange Icon)
        sb.AppendLine("  <Style id=\"measurePoint\">");
        sb.AppendLine("    <IconStyle><color>ff006bff</color><scale>1.0</scale>" +
                      "<Icon><href>http://maps.google.com/mapfiles/kml/shapes/placemark_circle.png</href></Icon>" +
                      "</IconStyle>");
        sb.AppendLine("  </Style>");
        sb.AppendLine("  <Style id=\"boundary\">");
        sb.AppendLine("    <LineStyle><color>ff00ffff</color><width>2</width></LineStyle>");
        sb.AppendLine("    <PolyStyle><color>3300ffff</color><fill>1</fill></PolyStyle>");
        sb.AppendLine("  </Style>");

        // Placemarks für jeden Messpunkt
        foreach (var p in project.Points)
        {
            sb.AppendLine("  <Placemark>");
            sb.AppendLine($"    <name>{EscapeXml(p.Label ?? "Punkt")}</name>");
            sb.AppendLine($"    <description>NN: {p.Altitude.ToString("F2", inv)}m | " +
                          $"±{p.HorizontalAccuracy.ToString("F1", inv)}cm | " +
                          $"{StickState.GetFixStatusText(p.FixQuality)}</description>");
            sb.AppendLine("    <styleUrl>#measurePoint</styleUrl>");
            sb.AppendLine("    <Point>");
            sb.AppendLine("      <altitudeMode>absolute</altitudeMode>");
            sb.AppendLine($"      <coordinates>{p.Longitude.ToString("F8", inv)}," +
                          $"{p.Latitude.ToString("F8", inv)}," +
                          $"{p.Altitude.ToString("F2", inv)}</coordinates>");
            sb.AppendLine("    </Point>");
            sb.AppendLine("  </Placemark>");
        }

        // Polygon als LinearRing wenn >= 3 Punkte
        if (project.Points.Count >= 3)
        {
            sb.AppendLine("  <Placemark>");
            sb.AppendLine("    <name>Grundstück-Umriss</name>");
            sb.AppendLine("    <styleUrl>#boundary</styleUrl>");
            sb.AppendLine("    <Polygon>");
            sb.AppendLine("      <altitudeMode>clampToGround</altitudeMode>");
            sb.AppendLine("      <outerBoundaryIs><LinearRing><coordinates>");
            foreach (var p in project.Points)
                sb.AppendLine($"        {p.Longitude.ToString("F8", inv)}," +
                              $"{p.Latitude.ToString("F8", inv)},0");
            // Polygon schließen
            var first = project.Points[0];
            sb.AppendLine($"        {first.Longitude.ToString("F8", inv)}," +
                          $"{first.Latitude.ToString("F8", inv)},0");
            sb.AppendLine("      </coordinates></LinearRing></outerBoundaryIs>");
            sb.AppendLine("    </Polygon>");
            sb.AppendLine("  </Placemark>");
        }

        // Gartenelemente als LineString/Polygon
        foreach (var el in project.GardenElements)
        {
            var wgs = _gardenPlanService.ParsePointsWgs84(el.PointsJson);
            if (wgs == null || wgs.Count < 2) continue;

            var isPolygon = el.ElementType is GardenElementType.Beet or GardenElementType.Rasen
                or GardenElementType.Terrasse or GardenElementType.Gebaeude or GardenElementType.Wasser;

            sb.AppendLine("  <Placemark>");
            sb.AppendLine($"    <name>{EscapeXml(el.ElementType.ToString())}</name>");
            if (!string.IsNullOrWhiteSpace(el.Notes))
                sb.AppendLine($"    <description>{EscapeXml(el.Notes)}</description>");

            if (isPolygon)
            {
                sb.AppendLine("    <Polygon>");
                sb.AppendLine("      <altitudeMode>clampToGround</altitudeMode>");
                sb.AppendLine("      <outerBoundaryIs><LinearRing><coordinates>");
                foreach (var (lat, lon) in wgs)
                    sb.AppendLine($"        {lon.ToString("F8", inv)},{lat.ToString("F8", inv)},0");
                // Polygon schließen
                sb.AppendLine($"        {wgs[0].longitude.ToString("F8", inv)}," +
                              $"{wgs[0].latitude.ToString("F8", inv)},0");
                sb.AppendLine("      </coordinates></LinearRing></outerBoundaryIs>");
                sb.AppendLine("    </Polygon>");
            }
            else
            {
                sb.AppendLine("    <LineString>");
                sb.AppendLine("      <altitudeMode>clampToGround</altitudeMode>");
                sb.AppendLine("      <coordinates>");
                foreach (var (lat, lon) in wgs)
                    sb.AppendLine($"        {lon.ToString("F8", inv)},{lat.ToString("F8", inv)},0");
                sb.AppendLine("      </coordinates>");
                sb.AppendLine("    </LineString>");
            }
            sb.AppendLine("  </Placemark>");
        }

        sb.AppendLine("</Document>");
        sb.AppendLine("</kml>");
        return sb.ToString();
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    /// <summary>Referenz-Schwerpunkt für DXF-Projektion. Messpunkte bevorzugt,
    /// Fallback auf Gartenelemente (z.B. bei reinem AR-Projekt).</summary>
    private (double refLat, double refLon) ComputeReference(
        List<SurveyPoint> points, List<GardenElement> elements)
    {
        if (points.Count > 0)
            return (points.Average(p => p.Latitude), points.Average(p => p.Longitude));

        var allWgs = new List<(double lat, double lon)>();
        foreach (var el in elements)
        {
            var wgs = _gardenPlanService.ParsePointsWgs84(el.PointsJson);
            if (wgs != null) allWgs.AddRange(wgs);
        }
        if (allWgs.Count > 0)
            return (allWgs.Average(w => w.lat), allWgs.Average(w => w.lon));

        return (0, 0); // Fallback — DXF wird leer sein
    }

    #endregion
}
