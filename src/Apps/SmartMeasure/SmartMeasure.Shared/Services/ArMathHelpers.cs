using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// Mathematische AR-Helfer ohne ARCore-Abhaengigkeit. Bowditch-Compass-Rule fuer
/// Polygon-Schlussfehler + Quaternion → Heading/Pitch — beides ist klassische Vermessungs-
/// und 3D-Mathematik und kann ohne Plattform-API in Unit-Tests gefahren werden.
/// </summary>
public static class ArMathHelpers
{
    /// <summary>
    /// Bowditch-Correction (Compass Rule): Klassische Vermessungs-Technik. Wenn ein
    /// geschlossener Polygonzug einen Schlussfehler hat (letzter Punkt ≠ erster), wird der
    /// Fehler proportional zur zurückgelegten Distanz auf alle Zwischenpunkte verteilt.
    /// Nach der Korrektur wird der letzte Punkt explizit exakt auf den ersten gesetzt
    /// (Float-Akkumulations-Rundungsfehler eliminieren).
    /// Nur aktiv bei 1 cm – 2 m Schlussfehler (kleiner: unnoetig, groesser: Fehler-Detect).
    /// </summary>
    public static void ApplyBowditchCorrection(ArContour contour)
    {
        if (!contour.IsClosed) return;
        if (contour.Points.Count < 3) return;

        var first = contour.Points[0];
        var last = contour.Points[^1];

        // Schlussfehler-Vektor vom letzten zurueck zum ersten Punkt
        var dx = first.X - last.X;
        var dy = first.Y - last.Y;
        var dz = first.Z - last.Z;

        var errorMag = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        if (errorMag < 0.01f || errorMag > 2.0f) return;

        // Kumulative Distanzen entlang des Polygonzugs
        var cumDist = new float[contour.Points.Count];
        cumDist[0] = 0;
        for (var i = 1; i < contour.Points.Count; i++)
            cumDist[i] = cumDist[i - 1] + contour.Points[i].DistanceTo(contour.Points[i - 1]);

        var totalDist = cumDist[^1];
        if (totalDist < 0.01f) return;

        for (var i = 1; i < contour.Points.Count; i++)
        {
            var fraction = cumDist[i] / totalDist;
            contour.Points[i].X += dx * fraction;
            contour.Points[i].Y += dy * fraction;
            contour.Points[i].Z += dz * fraction;
        }

        // Letzten Punkt exakt auf den ersten setzen (Float-Rest eliminieren).
        contour.Points[^1].X = first.X;
        contour.Points[^1].Y = first.Y;
        contour.Points[^1].Z = first.Z;
    }

    /// <summary>
    /// Extrahiert Heading (0-360°) aus einem Kamera-Rotations-Quaternion. ARCore-Welt:
    /// +X = Ost, -Z = Nord. Blickrichtung der Kamera ist -Z lokal, rotiert ins Welt-System.
    /// Bei steiler Kamera (Pitch &gt;60°, cos(Pitch) &lt; 0.5) ist die horizontale Projektion
    /// instabil → null. Caller muss dann auf Magnetometer-Heading zurueckfallen.
    /// </summary>
    public static float? ExtractHeadingFromQuaternion(float qx, float qy, float qz, float qw)
    {
        // Blickrichtung (kamera -Z) im Welt-Frame:
        var fx = -2f * (qx * qz + qw * qy);
        var fy = -2f * (qy * qz - qw * qx);
        var fz = -(1f - 2f * (qx * qx + qy * qy));

        var horizontalLen = MathF.Sqrt(fx * fx + fz * fz);
        var forwardLen = MathF.Sqrt(fx * fx + fy * fy + fz * fz);
        if (forwardLen < 0.001f) return null;

        var cosPitch = horizontalLen / forwardLen;
        if (cosPitch < 0.5f) return null; // > 60° → horizontaler Anteil zu klein

        var headingRad = MathF.Atan2(fx, -fz);
        var headingDeg = headingRad * 180f / MathF.PI;
        if (headingDeg < 0) headingDeg += 360f;
        return headingDeg;
    }

    /// <summary>Pitch (Neigung gegen horizontale Ebene) in Grad. 0=waagerecht,
    /// +90=nach oben, -90=nach unten.</summary>
    public static float ExtractPitchFromQuaternion(float qx, float qy, float qz, float qw)
    {
        var fx = -2f * (qx * qz + qw * qy);
        var fy = -2f * (qy * qz - qw * qx);
        var fz = -(1f - 2f * (qx * qx + qy * qy));

        var horizontalLen = MathF.Sqrt(fx * fx + fz * fz);
        var pitchRad = MathF.Atan2(fy, horizontalLen);
        return pitchRad * 180f / MathF.PI;
    }
}
