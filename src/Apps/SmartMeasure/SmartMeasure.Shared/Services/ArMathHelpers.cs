using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// Mathematische AR-Helfer ohne ARCore-Abhaengigkeit. Bowditch-Compass-Rule fuer
/// Polygon-Schlussfehler + Quaternion → Heading/Pitch — beides ist klassische Vermessungs-
/// und 3D-Mathematik und kann ohne Plattform-API in Unit-Tests gefahren werden.
/// </summary>
public static class ArMathHelpers
{
    /// <summary>Ergebnis von <see cref="ApplyBowditchCorrection"/> — erlaubt dem Aufrufer,
    /// einen zu großen Schlussfehler dem Nutzer als Warnung zu melden statt ihn still zu
    /// schlucken.</summary>
    public enum BowditchResult
    {
        /// <summary>Korrektur angewandt (Schlussfehler 1 cm – 2 m).</summary>
        Applied,
        /// <summary>Schlussfehler &lt; 1 cm — Korrektur unnötig, Kontur ist sauber geschlossen.</summary>
        TooSmall,
        /// <summary>Schlussfehler &gt; 2 m — NICHT korrigiert. Deutet auf einen Tracking-/Mess-
        /// Fehler hin; Fläche/Umfang sind unzuverlässig, der Nutzer sollte gewarnt werden.</summary>
        TooLarge,
        /// <summary>Kontur entartet (offen, &lt; 3 Punkte oder Gesamtstrecke ~0).</summary>
        Degenerate,
        /// <summary>Korrektur bewusst NICHT angewandt: Der letzte Punkt ist keine
        /// Wiederholungsmessung des Startpunkts (implizites Schließen via Button/Moduswechsel/
        /// Fertig) — die Schlusskante ist eine echte Polygonkante, kein Messfehler. Eine
        /// Verteilung würde das Polygon verzerren.</summary>
        NotApplied,
    }

    /// <summary>
    /// Bowditch-Correction (Compass Rule): Klassische Vermessungs-Technik. Wenn ein
    /// geschlossener Polygonzug einen Schlussfehler hat (letzter Punkt ≠ erster), wird der
    /// Fehler proportional zur zurückgelegten Distanz auf alle Zwischenpunkte verteilt.
    /// Nach der Korrektur wird der letzte Punkt explizit exakt auf den ersten gesetzt
    /// (Float-Akkumulations-Rundungsfehler eliminieren).
    /// Nur aktiv bei 1 cm – 2 m Schlussfehler. Der Rückgabewert macht einen zu großen
    /// Schlussfehler (&gt; 2 m) sichtbar, damit der Aufrufer warnen kann — vorher still verworfen.
    /// </summary>
    public static BowditchResult ApplyBowditchCorrection(ArContour contour)
    {
        if (!contour.IsClosed || contour.Points.Count < 3) return BowditchResult.Degenerate;

        var first = contour.Points[0];
        var last = contour.Points[^1];

        // Schlussfehler-Vektor vom letzten zurueck zum ersten Punkt
        var dx = first.X - last.X;
        var dy = first.Y - last.Y;
        var dz = first.Z - last.Z;

        var errorMag = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        if (errorMag < 0.01f) return BowditchResult.TooSmall;
        if (errorMag > 2.0f) return BowditchResult.TooLarge;

        // Kumulative Distanzen entlang des Polygonzugs
        var cumDist = new float[contour.Points.Count];
        cumDist[0] = 0;
        for (var i = 1; i < contour.Points.Count; i++)
            cumDist[i] = cumDist[i - 1] + contour.Points[i].DistanceTo(contour.Points[i - 1]);

        var totalDist = cumDist[^1];
        if (totalDist < 0.01f) return BowditchResult.Degenerate;

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
        return BowditchResult.Applied;
    }

    /// <summary>
    /// Extrahiert das Yaw (0-360°) der Kamera-Blickrichtung (-Z lokal) RELATIV ZUM
    /// ARCORE-SESSION-WELTFRAME aus einem Rotations-Quaternion.
    /// WICHTIG: Der Session-Weltframe ist azimutal WILLKUERLICH (ARCore richtet ihn beim
    /// Start grob an der Blickrichtung aus) — dieses Yaw ist also KEIN Kompass-Heading.
    /// Erst die Fusion mit einer Nordreferenz (Magnetometer- oder VPS-Heading desselben
    /// Moments: Frame-Azimut = Referenz − Yaw) macht daraus eine geografische Richtung.
    /// Nur fuer den EUS-Frame der Geospatial-API gilt "+X = Ost, -Z = Nord" exakt.
    /// Bei steiler Kamera (Pitch &gt;60°, cos(Pitch) &lt; 0.5) ist die horizontale Projektion
    /// instabil → null. Caller muss dann auf eine andere Heading-Quelle zurueckfallen.
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
