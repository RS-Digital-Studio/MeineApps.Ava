using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

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
/// 3. <b>Parallel-Snap</b>: Bei aktiver Kontur mit ≥1 Punkt → die neue Strecke wird auf
///    eine bestehende Konturkante (parallel-Richtung) gezwungen, wenn der Winkel zur
///    Kante innerhalb 3° ist. Hilft bei rechtwinkligen Grundrissen (parallele Hauswände
///    ohne Right-Angle-Erinnerung beim Anvisieren).
/// 4. <b>Extension-Snap</b>: Verlängerung einer bestehenden Kante. Wenn der Hit
///    innerhalb 10 cm an der Verlängerungs-Linie liegt, wird er orthogonal darauf
///    projiziert. Klassiker für "die Kante geht hier weiter" — z.B. Mauer-Fortsetzung.
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

    /// <summary>Parallel-Snap-Toleranz in Grad (Plan: 3°). Strenger als Right-Angle weil
    /// Parallelitaet visuell sehr sichtbar ist — ein 5°-Versatz wird vom User noch als
    /// "schief" erkannt.</summary>
    public const float ParallelToleranceDeg = 3f;

    /// <summary>Extension-Snap-Reichweite zur Hilfslinie in Metern (Plan: 10 cm).</summary>
    public const float ExtensionSnapRadius = 0.10f;

    /// <summary>Mindest-Verlaengerung jenseits eines Kanten-Endes, ab der Extension-Snap
    /// aktiv wird. Sonst springt jeder Hit nahe einer Kante auf die Kante — das wuerde
    /// Vertex-Snap und Parallel-Snap rauben.</summary>
    public const float ExtensionMinBeyondEdge = 0.05f;

    /// <summary>Snap-Typ — wird für Haptic + Hint-Anzeige verwendet.</summary>
    public enum SnapType
    {
        None = 0,
        /// <summary>Hit wurde auf einen existierenden Punkt gezogen (Endpunkt/Schließen).</summary>
        Vertex = 1,
        /// <summary>Hit wurde auf einen 90°-Winkel zur letzten Strecke gezwungen.</summary>
        RightAngle = 2,
        /// <summary>Hit wurde parallel zu einer bestehenden Konturkante ausgerichtet.</summary>
        Parallel = 3,
        /// <summary>Hit wurde auf die Verlaengerung einer bestehenden Kante projiziert.</summary>
        Extension = 4,
    }

    /// <summary>Eine gerichtete Linie aus zwei <see cref="ArPoint"/>en (Konturkante oder
    /// Verbindung zwischen zwei Einzelpunkten). Wird von der Activity einmalig pro
    /// <see cref="Apply"/>-Call erzeugt und durchgereicht.</summary>
    public readonly record struct Edge(ArPoint A, ArPoint B);

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
    /// <param name="existingEdges">Bestehende Kanten (aus allen Konturen). Optional;
    /// wenn null oder leer, sind Parallel- und Extension-Snap deaktiviert.</param>
    public static (float x, float y, float z, SnapType type) Apply(
        float hitX, float hitY, float hitZ,
        IReadOnlyList<ArPoint>? existingPoints,
        IReadOnlyList<ArPoint>? activeContourPoints,
        IReadOnlyList<Edge>? existingEdges = null)
    {
        // 1. Vertex-Snap (höchste Priorität)
        var vertex = TryVertexSnap(hitX, hitY, hitZ, existingPoints);
        if (vertex.HasValue)
            return (vertex.Value.x, vertex.Value.y, vertex.Value.z, SnapType.Vertex);

        // 2. Right-Angle-Snap (nur wenn aktive Kontur ≥ 2 Punkte hat)
        var rightAngle = TryRightAngleSnap(hitX, hitY, hitZ, activeContourPoints);
        if (rightAngle.HasValue)
            return (rightAngle.Value.x, rightAngle.Value.y, rightAngle.Value.z, SnapType.RightAngle);

        // 3. Parallel-Snap (braucht aktive Kontur ≥ 1 Punkt + ≥ 1 Referenz-Edge)
        var parallel = TryParallelSnap(hitX, hitY, hitZ, activeContourPoints, existingEdges);
        if (parallel.HasValue)
            return (parallel.Value.x, parallel.Value.y, parallel.Value.z, SnapType.Parallel);

        // 4. Extension-Snap (Verlaengerung einer bestehenden Kante)
        var extension = TryExtensionSnap(hitX, hitY, hitZ, existingEdges);
        if (extension.HasValue)
            return (extension.Value.x, extension.Value.y, extension.Value.z, SnapType.Extension);

        return (hitX, hitY, hitZ, SnapType.None);
    }

    /// <summary>Findet den nächsten existierenden Punkt innerhalb des Snap-Radius (2D auf X/Z).
    /// Nur X/Z werden gesnappt — die gemessene Höhe (<paramref name="hitY"/>) bleibt erhalten,
    /// sonst erbt der Punkt am Hang die fremde Höhe des Snap-Ziels und verfälscht das
    /// 3D-Geländemodell. Konsistent mit Right-Angle/Parallel/Extension (alle reichen hitY durch);
    /// ein Höhensprung beim Loop-Schließen wird ohnehin von der Bowditch-Korrektur verteilt.</summary>
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
        return (best.X, hitY, best.Z);
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

    /// <summary>
    /// Parallel-Snap (Plan-Kap. 3.7): Erzwingt eine Strecke parallel zu einer bestehenden
    /// Konturkante. Geometrie analog Right-Angle, aber Match-Winkel ist 0/180° statt 90°,
    /// und die Referenzrichtung ist die naechstliegende existierende Kante (nicht die vor-
    /// letzte Strecke der aktiven Kontur).
    /// </summary>
    private static (float x, float y, float z)? TryParallelSnap(
        float hitX, float hitY, float hitZ,
        IReadOnlyList<ArPoint>? activeContour,
        IReadOnlyList<Edge>? edges)
    {
        if (activeContour == null || activeContour.Count < 1) return null;
        if (edges == null || edges.Count == 0) return null;

        var prev = activeContour[^1];

        // Vektor vom letzten Konturpunkt zum Hit (X/Z-Ebene)
        var hdx = hitX - prev.X;
        var hdz = hitZ - prev.Z;
        var hitLen = MathF.Sqrt(hdx * hdx + hdz * hdz);
        if (hitLen < RightAngleMinDistance) return null;

        // Beste Kandidaten-Edge: kleinste Winkel-Abweichung zur Hit-Richtung
        var bestDiffDeg = ParallelToleranceDeg;
        var bestEdgeDirX = 0f;
        var bestEdgeDirZ = 0f;
        var foundEdge = false;

        foreach (var e in edges)
        {
            var edx = e.B.X - e.A.X;
            var edz = e.B.Z - e.A.Z;
            var elen = MathF.Sqrt(edx * edx + edz * edz);
            if (elen < 0.05f) continue;
            edx /= elen;
            edz /= elen;

            // Selbstreferenz vermeiden: wenn die Edge mit dem prev-Punkt aus der aktiven
            // Kontur identisch waere (gleicher Endpunkt), wuerde Parallel-Snap auf die
            // eigene Verlaengerung snappen → das ist Extension-Job, kein Parallel.
            if (ReferenceEquals(e.A, prev) || ReferenceEquals(e.B, prev)) continue;

            // Winkel zwischen Edge-Richtung und Hit-Richtung via Dot-Product
            var dot = (hdx * edx + hdz * edz) / hitLen;
            dot = MathF.Max(-1f, MathF.Min(1f, dot));
            // Abs-Dot bedeutet wir akzeptieren parallel UND anti-parallel
            var absDot = MathF.Abs(dot);
            var angleRad = MathF.Acos(absDot);
            var angleDeg = angleRad * 180f / MathF.PI;

            if (angleDeg < bestDiffDeg)
            {
                bestDiffDeg = angleDeg;
                bestEdgeDirX = edx;
                bestEdgeDirZ = edz;
                foundEdge = true;
            }
        }

        if (!foundEdge) return null;

        // Hit auf die parallele Richtung projizieren — Original-Distanz bleibt erhalten,
        // nur die Richtung wird angeglichen. Vorzeichen vom ursprünglichen Hit übernehmen
        // (sonst kippt der Punkt bei anti-paralleler Edge).
        var dotForward = hdx * bestEdgeDirX + hdz * bestEdgeDirZ;
        var sign = MathF.Sign(dotForward);
        if (sign == 0) sign = 1;

        var snappedX = prev.X + bestEdgeDirX * sign * hitLen;
        var snappedZ = prev.Z + bestEdgeDirZ * sign * hitLen;
        return (snappedX, hitY, snappedZ);
    }

    /// <summary>
    /// Extension-Snap (Plan-Kap. 3.7): Projiziert den Hit orthogonal auf die Verlaengerung
    /// einer bestehenden Kante, wenn er innerhalb <see cref="ExtensionSnapRadius"/> liegt
    /// UND mindestens <see cref="ExtensionMinBeyondEdge"/> jenseits eines Kanten-Endes —
    /// sonst wuerde der Hit auch innerhalb der Kante snappen und Vertex-/Right-Angle-Snap
    /// die Show stehlen.
    /// </summary>
    private static (float x, float y, float z)? TryExtensionSnap(
        float hitX, float hitY, float hitZ,
        IReadOnlyList<Edge>? edges)
    {
        if (edges == null || edges.Count == 0) return null;

        var bestDistSq = ExtensionSnapRadius * ExtensionSnapRadius;
        float bestX = 0f, bestZ = 0f;
        var found = false;

        foreach (var e in edges)
        {
            var edx = e.B.X - e.A.X;
            var edz = e.B.Z - e.A.Z;
            var elen = MathF.Sqrt(edx * edx + edz * edz);
            if (elen < 0.05f) continue;

            // Hit auf die Linie projizieren — t ist der Parameter (0 = A, 1 = B,
            // <0 vor A, >1 hinter B).
            var hxA = hitX - e.A.X;
            var hzA = hitZ - e.A.Z;
            var t = (hxA * edx + hzA * edz) / (elen * elen);

            // Extension nur jenseits der Kanten-Enden. Inside-Range
            // (0 ≤ t ≤ 1) kollidiert mit Vertex/Right-Angle.
            var beyondMinT = ExtensionMinBeyondEdge / elen;
            if (t > -beyondMinT && t < 1f + beyondMinT) continue;

            // Projektion auf die Linie
            var projX = e.A.X + edx * t;
            var projZ = e.A.Z + edz * t;

            // Perpendikulär-Abstand zum Hit
            var pdx = hitX - projX;
            var pdz = hitZ - projZ;
            var dSq = pdx * pdx + pdz * pdz;

            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                bestX = projX;
                bestZ = projZ;
                found = true;
            }
        }

        if (!found) return null;
        return (bestX, hitY, bestZ);
    }
}
