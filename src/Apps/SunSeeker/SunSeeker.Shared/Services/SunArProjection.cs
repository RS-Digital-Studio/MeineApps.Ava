namespace SunSeeker.Shared.Services;

/// <summary>
/// Projiziert eine Welt-Richtung (Azimut/Elevation, true north) auf den Kamera-Bildschirm — für das
/// AR-Sonnenbahn-Overlay. Keine ARCore-Welt-Verankerung nötig: Die Sonnenrichtung ist eine reine
/// Funktion aus Ort + Zeit; ihre Bildschirmposition ergibt sich aus der Geräteorientierung (wohin
/// die Kamera zeigt) und dem Sichtfeld (FOV) der Kamera.
///
/// Vereinfachte (lineare) gnomonische Näherung: für moderate FOV und einen Marker-Overlay genau
/// genug. Roll-Korrektur dreht die projizierten Punkte um die Bildmitte, damit das Overlay beim
/// Kippen des Geräts stehen bleibt. Reine Mathematik → plattformneutral + unit-getestet.
/// </summary>
public static class SunArProjection
{
    /// <summary>Projiziertes Ziel: Bildschirm-Pixel + ob es vor der Kamera bzw. im Bild liegt.</summary>
    public readonly record struct Projected(float X, float Y, bool InFront, bool OnScreen, double ScreenAngleDeg);

    /// <param name="cameraAzimuth">Himmelsrichtung, in die die Kamera zeigt (0=N, 90=O, true north).</param>
    /// <param name="cameraElevation">Höhenwinkel der Kamera-Achse (0=horizontal, +oben).</param>
    /// <param name="cameraRollDeg">Roll des Geräts um die Kamera-Achse (Grad, im Uhrzeigersinn).</param>
    public static Projected Project(
        double targetAzimuth, double targetElevation,
        double cameraAzimuth, double cameraElevation, double cameraRollDeg,
        double horizontalFovDeg, double verticalFovDeg,
        float screenWidth, float screenHeight)
    {
        var dAz = NormalizeDelta(targetAzimuth - cameraAzimuth);
        var dEl = targetElevation - cameraElevation;
        var inFront = Math.Abs(dAz) < 90.0 && Math.Abs(dEl) < 90.0;

        // Normiert auf das halbe FOV (±1 = Bildrand). Oben = negatives Screen-Y.
        var nx = dAz / (horizontalFovDeg * 0.5);
        var ny = -dEl / (verticalFovDeg * 0.5);

        // Roll-Korrektur: Welt erscheint um -roll gedreht.
        var r = -cameraRollDeg * Math.PI / 180.0;
        var cos = Math.Cos(r);
        var sin = Math.Sin(r);
        var rx = nx * cos - ny * sin;
        var ry = nx * sin + ny * cos;

        var x = (float)(screenWidth * 0.5 + rx * screenWidth * 0.5);
        var y = (float)(screenHeight * 0.5 + ry * screenHeight * 0.5);

        var onScreen = inFront && x >= 0 && x <= screenWidth && y >= 0 && y <= screenHeight;
        var screenAngle = Math.Atan2(ry, rx) * 180.0 / Math.PI; // 0=rechts, 90=unten

        return new Projected(x, y, inFront, onScreen, screenAngle);
    }

    /// <summary>Differenz zweier Azimute auf [-180, 180].</summary>
    public static double NormalizeDelta(double deg)
    {
        deg %= 360.0;
        if (deg > 180.0) deg -= 360.0;
        if (deg < -180.0) deg += 360.0;
        return deg;
    }
}
