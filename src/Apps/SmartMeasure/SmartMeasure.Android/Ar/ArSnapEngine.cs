using SmartMeasure.Shared.Models;

namespace SmartMeasure.Android.Ar;

/// <summary>
/// Geometrische Snap-Hilfen für die AR-Punkt-Erfassung (Plan Kap. 3.7).
///
/// Wird nach einem erfolgreichen Hit-Test mit dem rohen Hit-Punkt aufgerufen. Wenn ein
/// passender Snap-Kandidat gefunden wird, wird der Hit-Punkt auf die Snap-Position
/// verschoben. Die Activity zeigt dem User danach optional eine kurze Hint und
/// einen Haptic-Tick — das macht das Anvisieren von rechtwinkligen Ecken / Vertices
/// deutlich entspannter, weil keine 1-cm-Präzision beim Zielen mehr nötig ist.
///
/// Snap-Reihenfolge (höchste Priorität zuerst):
/// 1. <b>Vertex-Snap</b>: Reichweite 15 cm zu bestehenden Punkten — User will eine Linie
///    zu einem schon gesetzten Punkt schließen oder dort eine neue Kontur anhängen.
/// 2. <b>Right-Angle-Snap</b>: Bei aktiver Kontur mit ≥2 Punkten → der nächste Punkt
///    wird auf einen 90°-Winkel zur vorherigen Strecke gezwungen, wenn er innerhalb 5°
///    davon liegt. Klassisches Haus-Eck-Snap.
///
/// Snap-Engine ist intentionally stateless — alle Daten kommen aus dem Argument.
/// </summary>
public static class ArSnapEngine
{
    /// <summary>Vertex-Snap-Radius in Metern (Plan: 15 cm).</summary>
    public const float VertexSnapRadius = 0.15f;

    /// <summary>Right-Angle-Snap-Toleranz in Grad (Plan: 5°).</summary>
    public const float RightAngleToleranceDeg = 5f;

    /// <summary>Mindest-Strecke vom letzten Punkt um Right-Angle-Snap zu aktivieren —
    /// sonst snappt jeder Punkt direkt nach dem letzten auf 90°.</summary>
    public const float RightAngleMinDistance = 0.2f;

    /// <summary>Snap-Typ — wird für Haptic + Hint-Anzeige verwendet.</summary>
    public enum SnapType
    {
        None = 0,
        /// <summary>Hit wurde auf einen existierenden Punkt gezogen (Endpunkt/Schließen).</summary>
        Vertex = 1,
        /// <summary>Hit wurde auf einen 90°-Winkel zur letzten Strecke gezwungen.</summary>
        RightAngle = 2,
    }

    /// <summary>
    /// Wendet die passende Snap-Operation auf den gegebenen Hit-Punkt an. Liefert die
    /// finale Position (eventuell unverändert wenn kein Snap zutrifft) plus den Typ.
    /// </summary>
    /// <param name="hitX">X-Koordinate des Hit-Punkts (lokales AR-Frame).</param>
    /// <param name="hitY">Y-Koordinate des Hit-Punkts.</param>
    /// <param name="hitZ">Z-Koordinate des Hit-Punkts.</param>
    /// <param name="existingPoints">Alle bereits gesetzten Punkte der Session (Einzel +
    /// Kontur-Punkte). Können null sein.</param>
    /// <param name="activeContourPoints">Die zwei letzten Punkte der aktiven Kontur in
    /// Reihenfolge — wenn null oder &lt;2 Punkte, kein Right-Angle-Snap.</param>
    public static (float x, float y, float z, SnapType type) Apply(
        float hitX, float hitY, float hitZ,
        IReadOnlyList<ArPoint>? existingPoints,
        IReadOnlyList<ArPoint>? activeContourPoints)
    {
        // 1. Vertex-Snap (höchste Priorität)
        var vertex = TryVertexSnap(hitX, hitY, hitZ, existingPoints);
        if (vertex.HasValue)
            return (vertex.Value.x, vertex.Value.y, vertex.Value.z, SnapType.Vertex);

        // 2. Right-Angle-Snap (nur wenn aktive Kontur ≥ 2 Punkte hat)
        var rightAngle = TryRightAngleSnap(hitX, hitY, hitZ, activeContourPoints);
        if (rightAngle.HasValue)
            return (rightAngle.Value.x, rightAngle.Value.y, rightAngle.Value.z, SnapType.RightAngle);

        return (hitX, hitY, hitZ, SnapType.None);
    }

    /// <summary>Findet den nächsten existierenden Punkt innerhalb des Snap-Radius (2D auf X/Z).
    /// Y wird vom Snap-Ziel übernommen — sonst springt der Punkt höhenmäßig.</summary>
    private static (float x, float y, float z)? TryVertexSnap(
        float hitX, float hitY, float hitZ,
        IReadOnlyList<ArPoint>? points)
    {
        if (points == null || points.Count == 0) return null;

        var bestDistSq = VertexSnapRadius * VertexSnapRadius;
        ArPoint? best = null;

        foreach (var p in points)
        {
            var dx = p.X - hitX;
            var dz = p.Z - hitZ;
            var dSq = dx * dx + dz * dz;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                best = p;
            }
        }

        if (best == null) return null;
        return (best.X, best.Y, best.Z);
    }

    /// <summary>
    /// Erzwingt 90°-Winkel zur Richtung der vorherigen Strecke. Der Hit wird orthogonal
    /// auf die Right-Angle-Linie projiziert — die Original-Distanz bleibt erhalten, nur
    /// die Richtung wird auf das nächste 90°-Vielfache von der vorigen Strecke gerundet.
    /// </summary>
    private static (float x, float y, float z)? TryRightAngleSnap(
        float hitX, float hitY, float hitZ,
        IReadOnlyList<ArPoint>? activeContour)
    {
        if (activeContour == null || activeContour.Count < 2) return null;

        var prev = activeContour[^1];   // Aktuell letzter Konturpunkt
        var prev2 = activeContour[^2];  // Vor-vorletzter — definiert die Vorrichtung

        // Vorherige Strecken-Richtung (normalisiert, X/Z-Ebene)
        var pdx = prev.X - prev2.X;
        var pdz = prev.Z - prev2.Z;
        var prevLen = MathF.Sqrt(pdx * pdx + pdz * pdz);
        if (prevLen < 0.05f) return null; // zu kurze Strecke, Richtung unzuverlässig
        pdx /= prevLen;
        pdz /= prevLen;

        // Vektor vom letzten Punkt zum Hit
        var hdx = hitX - prev.X;
        var hdz = hitZ - prev.Z;
        var hitLen = MathF.Sqrt(hdx * hdx + hdz * hdz);
        if (hitLen < RightAngleMinDistance) return null;

        // Winkel zwischen den beiden Richtungen via Dot-Product
        var dotForward = (hdx * pdx + hdz * pdz) / hitLen;
        // Clamping wegen Float-Rundung
        dotForward = MathF.Max(-1f, MathF.Min(1f, dotForward));
        var angleRad = MathF.Acos(MathF.Abs(dotForward));
        var angleDeg = angleRad * 180f / MathF.PI;
        // Wenn parallel/antiparallel: Abstand zur 0° oder 180°-Linie, nicht 90°
        // Wir wollen 90°-Snap, d.h. Differenz zu 90° muss klein sein.
        var diffTo90 = MathF.Abs(angleDeg - 90f);
        if (diffTo90 > RightAngleToleranceDeg) return null;

        // Richtung senkrecht zur Vor-Richtung: (-pdz, pdx) ODER (pdz, -pdx).
        // Wähle die Variante, die näher am aktuellen Hit liegt.
        var perpAx = -pdz;
        var perpAz = pdx;
        var perpBx = pdz;
        var perpBz = -pdx;

        // Projektion des Hit-Vektors auf die zwei Senkrechten
        var projA = hdx * perpAx + hdz * perpAz;
        var projB = hdx * perpBx + hdz * perpBz;

        var (px, pz) = MathF.Abs(projA) > MathF.Abs(projB)
            ? (perpAx, perpAz)
            : (perpBx, perpBz);
        var sign = (px == perpAx && pz == perpAz) ? MathF.Sign(projA) : MathF.Sign(projB);
        if (sign == 0) sign = 1;

        var snappedX = prev.X + px * sign * hitLen;
        var snappedZ = prev.Z + pz * sign * hitLen;
        // Y wird unverändert übernommen — Höhe bleibt vom Hit-Test
        return (snappedX, hitY, snappedZ);
    }
}
