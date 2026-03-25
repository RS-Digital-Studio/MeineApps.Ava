using System.Globalization;
using System.Text;
using System.Text.Json;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>CSV + GeoJSON Export</summary>
public class ExportService : IExportService
{
    public string ExportToCsv(SurveyProject project)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Nr;Label;Latitude;Longitude;Höhe_m;H-Genauigkeit_cm;V-Genauigkeit_cm;Fix;Satelliten;Zeitpunkt");

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
}
