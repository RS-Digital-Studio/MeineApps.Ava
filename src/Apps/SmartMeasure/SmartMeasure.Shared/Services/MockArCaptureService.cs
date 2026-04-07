using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>Desktop-Mock fuer AR-Capture (simuliert ein kleines Grundstueck)</summary>
public class MockArCaptureService : IArCaptureService
{
    public Task<bool> IsAvailableAsync() => Task.FromResult(true);

    public async Task<ArCaptureResult?> CaptureAsync()
    {
        // Simuliere kurze Verzoegerung wie eine echte AR-Session
        await Task.Delay(500);

        // Generiere ein rechteckiges Grundstueck (12x8m) mit ein paar Punkten
        var random = new Random(42);
        var result = new ArCaptureResult
        {
            StartedAt = DateTime.UtcNow,
            SessionDuration = TimeSpan.FromMinutes(5),
            GpsLatitude = 48.7758,   // Beispiel: Muenchen
            GpsLongitude = 9.1829,
            GpsAltitude = 520.0,
            GpsAccuracy = 3.5f,
            MagneticHeading = 15f,   // leicht nach Osten verdreht
            BarometricAltitude = 520.5f,
        };

        // Eckpunkte des Grundstuecks
        var corners = new[]
        {
            (x: 0f, z: 0f),
            (x: 12f, z: 0f),
            (x: 12f, z: 8f),
            (x: 0f, z: 8f),
        };

        // Einzelpunkte mit leichten Hoehenunterschieden
        foreach (var (x, z) in corners)
        {
            result.Points.Add(new ArPoint
            {
                X = x,
                Y = -0.05f + (float)(random.NextDouble() * 0.3), // Gelaende-Hoehe
                Z = z,
                Confidence = 0.85f + (float)(random.NextDouble() * 0.15),
                Label = $"Ecke ({x:F0}/{z:F0})",
            });
        }

        // Zusaetzliche Zwischenpunkte
        for (var i = 0; i < 6; i++)
        {
            result.Points.Add(new ArPoint
            {
                X = 1f + (float)(random.NextDouble() * 10),
                Y = (float)(random.NextDouble() * 0.4 - 0.1),
                Z = 1f + (float)(random.NextDouble() * 6),
                Confidence = 0.7f + (float)(random.NextDouble() * 0.3),
            });
        }

        // Beispiel-Kontur: Weg (Linie)
        result.Contours.Add(new ArContour
        {
            ContourType = ArContourType.Weg,
            Label = "Hauptweg",
            IsClosed = false,
            Points =
            [
                new ArPoint { X = 0f, Y = 0f, Z = 4f, Confidence = 0.9f },
                new ArPoint { X = 6f, Y = 0.1f, Z = 4f, Confidence = 0.9f },
                new ArPoint { X = 12f, Y = 0.05f, Z = 4f, Confidence = 0.9f },
            ],
        });

        // Beispiel-Kontur: Beet (geschlossenes Polygon)
        result.Contours.Add(new ArContour
        {
            ContourType = ArContourType.Beet,
            Label = "Gemuesebeet",
            IsClosed = true,
            Points =
            [
                new ArPoint { X = 2f, Y = 0.1f, Z = 1f, Confidence = 0.85f },
                new ArPoint { X = 5f, Y = 0.15f, Z = 1f, Confidence = 0.85f },
                new ArPoint { X = 5f, Y = 0.12f, Z = 3f, Confidence = 0.85f },
                new ArPoint { X = 2f, Y = 0.08f, Z = 3f, Confidence = 0.85f },
            ],
        });

        return result;
    }
}
