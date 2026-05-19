using Google.AR.Core;
using Google.AR.Core.Exceptions;
using SmartMeasure.Shared.Models;

namespace SmartMeasure.Android.Ar;

/// <summary>
/// Helfer für zusätzliche Präzisions-Verbesserungen jenseits von Anchors/Averaging.
/// Bundelt Depth-API-Sanity-Check, Ground-Plane-Referenz, Bowditch-Correction,
/// ARCore-Heading-Extraktion und Tracking-Quality-Score.
/// </summary>
public static class ArPrecisionHelpers
{
    /// <summary>
    /// Depth-Sanity-Check: liest Depth-Map am Touch-Pixel und vergleicht mit HitResult-Distanz.
    /// Liefert Confidence-Multiplikator (1.0 = neutral, >1.0 = Bonus, &lt;1.0 = Penalty).
    ///
    /// Nutzt PRIMÄR Raw Depth (unfiltered, höhere Präzision auf S25 Ultra mit Stereo-Depth)
    /// mit separater Confidence-Map. Fallback auf smoothed DepthImage.
    /// </summary>
    public static float DepthSanityMultiplier(Frame frame, float screenX, float screenY,
        float hitDistanceMeters, int viewportWidth, int viewportHeight)
    {
        // 1) Raw Depth mit Confidence-Map versuchen (präziser, aber nur auf Geräten
        //    mit aktivierter DepthMode + verfügbarem Raw-Depth-Support)
        var rawResult = TryReadRawDepth(frame, screenX, screenY, hitDistanceMeters,
            viewportWidth, viewportHeight);
        if (rawResult.HasValue) return rawResult.Value;

        // 2) Fallback auf smoothed Depth
        return ReadSmoothedDepth(frame, screenX, screenY, hitDistanceMeters,
            viewportWidth, viewportHeight);
    }

    /// <summary>
    /// Raw Depth (unfiltered) + Confidence-Image. Nur Pixel mit Confidence > 0.3 verwenden.
    /// Liefert null wenn Raw-Depth nicht verfügbar → Caller fällt auf smoothed Depth zurück.
    /// </summary>
    private static float? TryReadRawDepth(Frame frame, float screenX, float screenY,
        float hitDistanceMeters, int viewportWidth, int viewportHeight)
    {
        global::Android.Media.Image? depthImage = null;
        global::Android.Media.Image? confImage = null;
        try
        {
            depthImage = frame.AcquireRawDepthImage16Bits();
            confImage = frame.AcquireRawDepthConfidenceImage();
            if (depthImage == null || confImage == null) return null;

            var dw = depthImage.Width;
            var dh = depthImage.Height;
            if (dw <= 0 || dh <= 0) return null;

            var dx = (int)(screenX / viewportWidth * dw);
            var dy = (int)(screenY / viewportHeight * dh);
            dx = Math.Clamp(dx, 0, dw - 1);
            dy = Math.Clamp(dy, 0, dh - 1);

            var planes = depthImage.GetPlanes();
            var cPlanes = confImage.GetPlanes();
            if (planes == null || planes.Length == 0 || cPlanes == null || cPlanes.Length == 0) return null;

            var plane = planes[0];
            var cPlane = cPlanes[0];
            if (plane?.Buffer == null || cPlane?.Buffer == null) return null;

            // Confidence (uint8, 0-255) auslesen
            var confOffset = dy * cPlane.RowStride + dx * cPlane.PixelStride;
            if (confOffset >= cPlane.Buffer.Capacity()) return null;
            cPlane.Buffer.Position(confOffset);
            var conf = (cPlane.Buffer.Get() & 0xFF) / 255f;

            // Niedrige Confidence → keine verwertbaren Daten
            if (conf < 0.3f) return null;

            // Depth (uint16 LE) auslesen
            var depthOffset = dy * plane.RowStride + dx * plane.PixelStride;
            if (depthOffset + 1 >= plane.Buffer.Capacity()) return null;
            plane.Buffer.Position(depthOffset);
            var low = plane.Buffer.Get() & 0xFF;
            var high = plane.Buffer.Get() & 0xFF;
            var depthMm = low | (high << 8);
            if (depthMm == 0) return null;

            var depthMeters = depthMm / 1000f;
            var diff = MathF.Abs(depthMeters - hitDistanceMeters);
            var relDiff = hitDistanceMeters > 0.1f ? diff / hitDistanceMeters : diff;

            // Bonus durch Confidence gewichtet
            if (diff < 0.05f) return 1.0f + 0.3f * conf;
            if (relDiff < 0.15f) return 1.0f;
            if (relDiff < 0.30f) return 0.8f;
            return 0.5f;
        }
        catch (NotYetAvailableException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            try { depthImage?.Close(); } catch { }
            try { confImage?.Close(); } catch { }
        }
    }

    private static float ReadSmoothedDepth(Frame frame, float screenX, float screenY,
        float hitDistanceMeters, int viewportWidth, int viewportHeight)
    {
        global::Android.Media.Image? depthImage = null;
        try
        {
            depthImage = frame.AcquireDepthImage16Bits();
            if (depthImage == null) return 1.0f;

            var depthWidth = depthImage.Width;
            var depthHeight = depthImage.Height;
            if (depthWidth <= 0 || depthHeight <= 0) return 1.0f;

            var dx = (int)(screenX / viewportWidth * depthWidth);
            var dy = (int)(screenY / viewportHeight * depthHeight);
            dx = Math.Clamp(dx, 0, depthWidth - 1);
            dy = Math.Clamp(dy, 0, depthHeight - 1);

            var planes = depthImage.GetPlanes();
            if (planes == null || planes.Length == 0) return 1.0f;

            var plane = planes[0];
            if (plane == null) return 1.0f;

            var buffer = plane.Buffer;
            if (buffer == null) return 1.0f;

            var rowStride = plane.RowStride;
            var pixelStride = plane.PixelStride;
            var offset = dy * rowStride + dx * pixelStride;
            if (offset + 1 >= buffer.Capacity()) return 1.0f;

            // uint16 little-endian lesen
            buffer.Position(offset);
            var low = buffer.Get() & 0xFF;
            var high = buffer.Get() & 0xFF;
            var depthMm = low | (high << 8);
            if (depthMm == 0) return 1.0f;

            var depthMeters = depthMm / 1000f;
            var diff = MathF.Abs(depthMeters - hitDistanceMeters);
            var relDiff = hitDistanceMeters > 0.1f ? diff / hitDistanceMeters : diff;

            if (diff < 0.05f) return 1.2f;
            if (relDiff < 0.15f) return 1.0f;
            if (relDiff < 0.30f) return 0.8f;
            return 0.5f;
        }
        catch (NotYetAvailableException)
        {
            return 1.0f;
        }
        catch (Exception)
        {
            return 1.0f;
        }
        finally
        {
            try { depthImage?.Close(); } catch { /* harmlos */ }
        }
    }

    /// <summary>
    /// Plan Kap. 3.6: Liest die Depth-Map am Touch-Pixel und liefert die Tiefe in Metern.
    /// Wird vom Instant-Placement-Fallback genutzt — statt hardcoded 1,5 m wird die echte
    /// Distanz vom Stereo-Depth-Sensor verwendet. Bei Sky oder ungültigem Depth-Wert null.
    /// </summary>
    public static float? TryGetDepthMeters(Frame frame, float screenX, float screenY,
        int viewportWidth, int viewportHeight)
    {
        // Raw Depth bevorzugt (höhere Präzision auf S25 Ultra mit Stereo-Depth-Sensor).
        var raw = TryReadRawDepthMeters(frame, screenX, screenY, viewportWidth, viewportHeight);
        if (raw.HasValue) return raw;

        // Fallback: smoothed Depth
        return TryReadSmoothedDepthMeters(frame, screenX, screenY, viewportWidth, viewportHeight);
    }

    private static float? TryReadRawDepthMeters(Frame frame, float screenX, float screenY,
        int viewportWidth, int viewportHeight)
    {
        global::Android.Media.Image? depthImage = null;
        global::Android.Media.Image? confImage = null;
        try
        {
            depthImage = frame.AcquireRawDepthImage16Bits();
            confImage = frame.AcquireRawDepthConfidenceImage();
            if (depthImage == null || confImage == null) return null;

            var dw = depthImage.Width;
            var dh = depthImage.Height;
            if (dw <= 0 || dh <= 0) return null;

            var dx = (int)(screenX / viewportWidth * dw);
            var dy = (int)(screenY / viewportHeight * dh);
            dx = Math.Clamp(dx, 0, dw - 1);
            dy = Math.Clamp(dy, 0, dh - 1);

            var planes = depthImage.GetPlanes();
            var cPlanes = confImage.GetPlanes();
            if (planes == null || planes.Length == 0 || cPlanes == null || cPlanes.Length == 0) return null;

            var plane = planes[0];
            var cPlane = cPlanes[0];
            if (plane?.Buffer == null || cPlane?.Buffer == null) return null;

            var confOffset = dy * cPlane.RowStride + dx * cPlane.PixelStride;
            if (confOffset >= cPlane.Buffer.Capacity()) return null;
            cPlane.Buffer.Position(confOffset);
            var conf = (cPlane.Buffer.Get() & 0xFF) / 255f;
            if (conf < 0.3f) return null; // zu unsicher → Caller fällt auf 1.5m zurück

            var depthOffset = dy * plane.RowStride + dx * plane.PixelStride;
            if (depthOffset + 1 >= plane.Buffer.Capacity()) return null;
            plane.Buffer.Position(depthOffset);
            var low = plane.Buffer.Get() & 0xFF;
            var high = plane.Buffer.Get() & 0xFF;
            var depthMm = low | (high << 8);
            if (depthMm == 0) return null; // 0 = invalid (z.B. Sky)

            var depthMeters = depthMm / 1000f;
            // Sanity-Range: 30 cm – 30 m. Außerhalb → Sensor unsicher.
            if (depthMeters < 0.3f || depthMeters > 30f) return null;
            return depthMeters;
        }
        catch (NotYetAvailableException) { return null; }
        catch (Exception) { return null; }
        finally
        {
            try { depthImage?.Close(); } catch { }
            try { confImage?.Close(); } catch { }
        }
    }

    private static float? TryReadSmoothedDepthMeters(Frame frame, float screenX, float screenY,
        int viewportWidth, int viewportHeight)
    {
        global::Android.Media.Image? depthImage = null;
        try
        {
            depthImage = frame.AcquireDepthImage16Bits();
            if (depthImage == null) return null;

            var dw = depthImage.Width;
            var dh = depthImage.Height;
            if (dw <= 0 || dh <= 0) return null;

            var dx = Math.Clamp((int)(screenX / viewportWidth * dw), 0, dw - 1);
            var dy = Math.Clamp((int)(screenY / viewportHeight * dh), 0, dh - 1);

            var planes = depthImage.GetPlanes();
            if (planes == null || planes.Length == 0 || planes[0]?.Buffer == null) return null;

            var plane = planes[0];
            var offset = dy * plane.RowStride + dx * plane.PixelStride;
            if (offset + 1 >= plane.Buffer.Capacity()) return null;

            plane.Buffer.Position(offset);
            var low = plane.Buffer.Get() & 0xFF;
            var high = plane.Buffer.Get() & 0xFF;
            var depthMm = low | (high << 8);
            if (depthMm == 0) return null;

            var depthMeters = depthMm / 1000f;
            if (depthMeters < 0.3f || depthMeters > 30f) return null;
            return depthMeters;
        }
        catch (NotYetAvailableException) { return null; }
        catch (Exception) { return null; }
        finally
        {
            try { depthImage?.Close(); } catch { }
        }
    }

    /// <summary>
    /// Findet die größte getrackte horizontale Plane in der Session und liefert ihren Y-Wert.
    /// Dient als Boden-Referenz: alle Punkt-Höhen werden relativ dazu gerechnet.
    /// "Horizontal" wird via Plane-Normalvektor identifiziert (Y-Komponente > 0.9 = nach oben).
    /// </summary>
    public static float? FindGroundPlaneY(Session session)
    {
        try
        {
            var trackables = session.GetAllTrackables(Java.Lang.Class.FromType(typeof(Plane)));
            if (trackables == null) return null;

            Plane? biggest = null;
            var biggestExtent = 0f;

            foreach (var t in trackables)
            {
                if (t is not Plane plane) continue;
                if (plane.TrackingState != TrackingState.Tracking) continue;
                if (plane.SubsumedBy != null) continue;

                // Plane-Normale aus CenterPose-Rotation berechnen.
                // Bei horizontaler Plane zeigt die Y-Achse der Plane-Pose nach oben in der Welt.
                var pose = plane.CenterPose;
                if (pose == null) continue;

                var q = pose.GetRotationQuaternion();
                if (q == null || q.Length < 4) continue;

                // Y-Komponente der rotierten Y-Achse im Welt-Raum
                float qx = q[0], qy = q[1], qz = q[2], qw = q[3];
                var normalY = 1f - 2f * (qx * qx + qz * qz);

                // Nur horizontale Planes (Normale zeigt nach oben, Y > 0.9)
                if (normalY < 0.9f) continue;

                var area = plane.ExtentX * plane.ExtentZ;
                if (area > biggestExtent)
                {
                    biggestExtent = area;
                    biggest = plane;
                }
            }

            return biggest?.CenterPose?.Ty();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("ArPrecision", $"FindGroundPlaneY failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extrahiert Heading (0-360°) aus der ARCore-Kamera-Pose.
    /// Die ARCore-Rotation ist Sensor-fusioniert (Gyro+Accel+Mag) und damit stabiler als
    /// reines Magnetometer. Rückgabe: Winkel in Grad, 0 = Blickrichtung Norden.
    ///
    /// KRITISCH: Bei stark geneigter Kamera (Pitch > 60°, typisch bei Garten-Vermessung
    /// wenn nach unten geschaut wird!) liefert die Projektion der Blickrichtung auf die
    /// horizontale Ebene unzuverlässige Ergebnisse — die projizierten Vektor-Komponenten
    /// werden nahe 0, atan2 wird instabil. Dann wird null zurückgegeben und der Caller
    /// fällt auf Magnetometer-Heading zurück.
    /// </summary>
    public static float? ExtractHeadingFromCameraPose(Pose cameraPose)
    {
        try
        {
            var q = cameraPose.GetRotationQuaternion();
            if (q == null || q.Length < 4) return null;

            float qx = q[0], qy = q[1], qz = q[2], qw = q[3];

            // Blickrichtung = -Z-Achse in Kamera-Koordinaten. Ins Welt-System rotiert:
            //   forward = (-2*(qx*qz + qw*qy), -2*(qy*qz - qw*qx), -(1 - 2*(qx² + qy²)))
            var fx = -2f * (qx * qz + qw * qy);
            var fy = -2f * (qy * qz - qw * qx);
            var fz = -(1f - 2f * (qx * qx + qy * qy));

            // Pitch-Check: Wenn Blickrichtung stark nach unten/oben zeigt, ist das Heading
            // über die horizontale Projektion instabil. Pitch = Winkel zwischen forward und
            // horizontaler XZ-Ebene. Cos(Pitch) = Länge der horizontalen Komponente.
            var horizontalLen = MathF.Sqrt(fx * fx + fz * fz);
            var forwardLen = MathF.Sqrt(fx * fx + fy * fy + fz * fz);
            if (forwardLen < 0.001f) return null;

            var cosPitch = horizontalLen / forwardLen;
            // cos(60°) = 0.5. Wenn Blickrichtung mehr als 60° von der Horizontalen abweicht,
            // verwerfen wir das ARCore-Heading — horizontaler Anteil zu klein.
            if (cosPitch < 0.5f) return null;

            // Heading: atan2(east, north). ARCore-Welt-Koord: +X=Ost, -Z=Nord (bei Session-Start
            // mit Magnetometer-Kalibrierung). Also north-Vektor = -fz, east-Vektor = fx.
            var headingRad = MathF.Atan2(fx, -fz);
            var headingDeg = headingRad * 180f / MathF.PI;
            if (headingDeg < 0) headingDeg += 360f;

            return headingDeg;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extrahiert den Pitch der Kamera (Neigung gegen die horizontale Ebene) in Grad.
    /// 0 = waagerecht, +90 = direkt nach oben, -90 = direkt nach unten.
    /// Wird beim Punkt-Setzen mitgeschrieben, damit Vermessungs-PDF und Quality-Score
    /// erkennen können wenn der User stark schräg gemessen hat (große Depth-Fehler).
    /// </summary>
    public static float ExtractPitchFromCameraPose(Pose cameraPose)
    {
        try
        {
            var q = cameraPose.GetRotationQuaternion();
            if (q == null || q.Length < 4) return 0f;

            float qx = q[0], qy = q[1], qz = q[2], qw = q[3];

            // Forward = -Z im Welt-Frame, identische Formel wie in ExtractHeadingFromCameraPose
            var fx = -2f * (qx * qz + qw * qy);
            var fy = -2f * (qy * qz - qw * qx);
            var fz = -(1f - 2f * (qx * qx + qy * qy));

            var horizontalLen = MathF.Sqrt(fx * fx + fz * fz);
            // atan2(fy, horizontal) gibt Pitch: fy > 0 ⇒ nach oben, fy < 0 ⇒ nach unten
            var pitchRad = MathF.Atan2(fy, horizontalLen);
            return pitchRad * 180f / MathF.PI;
        }
        catch
        {
            return 0f;
        }
    }

    /// <summary>
    /// Bowditch-Correction (Compass Rule): Klassische Vermessungs-Technik.
    /// Wenn ein geschlossener Polygonzug einen Schlussfehler hat (letzter Punkt ≠ erster),
    /// wird der Fehler proportional zur zurückgelegten Distanz auf alle Zwischenpunkte verteilt.
    ///
    /// Nach der Korrektur wird der letzte Punkt explizit exakt auf den ersten gesetzt
    /// (Float-Akkumulations-Rundungsfehler eliminieren).
    /// </summary>
    public static void ApplyBowditchCorrection(ArContour contour)
    {
        if (!contour.IsClosed) return;
        if (contour.Points.Count < 3) return; // Dreieck ist valides Polygon

        var first = contour.Points[0];
        var last = contour.Points[^1];

        // Schlussfehler (Vector vom letzten zurück zum ersten)
        var dx = first.X - last.X;
        var dy = first.Y - last.Y;
        var dz = first.Z - last.Z;

        var errorMag = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        // Nur korrigieren wenn Fehler > 1cm aber < 2m (sonst ist es kein Bowditch-Fall)
        if (errorMag < 0.01f || errorMag > 2.0f) return;

        // Kumulative Distanzen entlang des Polygonzugs
        var cumDist = new float[contour.Points.Count];
        cumDist[0] = 0;
        for (var i = 1; i < contour.Points.Count; i++)
        {
            cumDist[i] = cumDist[i - 1] + contour.Points[i].DistanceTo(contour.Points[i - 1]);
        }

        var totalDist = cumDist[^1];
        if (totalDist < 0.01f) return;

        // Fehler proportional zur zurückgelegten Distanz auf alle Punkte verteilen
        for (var i = 1; i < contour.Points.Count; i++)
        {
            var fraction = cumDist[i] / totalDist;
            contour.Points[i].X += dx * fraction;
            contour.Points[i].Y += dy * fraction;
            contour.Points[i].Z += dz * fraction;
        }

        // Letzten Punkt exakt auf ersten setzen — Float-Rundungsfehler eliminieren.
        // Damit ist Kontur wirklich geschlossen (first == last).
        contour.Points[^1].X = first.X;
        contour.Points[^1].Y = first.Y;
        contour.Points[^1].Z = first.Z;
    }

    /// <summary>
    /// Berechnet einen Tracking-Quality-Score von 0 bis 100 aus mehreren Faktoren.
    /// Für Stats-Panel und User-Feedback.
    /// </summary>
    public static int ComputeTrackingQualityScore(
        bool isTracking,
        int planeCount,
        float stabilityScore,
        int magAccuracy,
        int anchorCount,
        float avgPositionStdDev)
    {
        if (!isTracking) return 0;

        // Basis: 50 Punkte für aktives Tracking
        var score = 50;

        // Planes: bis zu +15 (mehr Flächen = mehr Feature-Punkte in der Welt)
        score += Math.Min(15, planeCount * 3);

        // Stability: bis zu +10 (ruhige Kamera = bessere Feature-Detection)
        score += (int)(stabilityScore * 10);

        // Magnetometer-Accuracy: bis zu +10 (für Georeferenzierung)
        score += Math.Min(10, magAccuracy * 5);

        // Anchors: bis zu +10 (viele stabile Anker = gute Session)
        score += Math.Min(10, anchorCount);

        // StdDev-Penalty: -5 für jeden cm durchschnittliche Messgenauigkeit
        if (avgPositionStdDev > 0.001f)
            score -= (int)(avgPositionStdDev * 500);

        return Math.Clamp(score, 0, 100);
    }
}
