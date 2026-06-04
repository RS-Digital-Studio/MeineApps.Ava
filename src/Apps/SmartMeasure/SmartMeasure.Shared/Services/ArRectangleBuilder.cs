using SmartMeasure.Shared.Models;

namespace SmartMeasure.Shared.Services;

/// <summary>
/// Berechnet aus drei gemessenen AR-Punkten ein exakt rechtwinkliges Rechteck (bzw.
/// Quadrat) — die geführte 3-Punkt-Methode aus CAD/Vermessung:
///
/// <list type="number">
///   <item>Punkt&#160;1 &#8594; Punkt&#160;2 spannen die <b>Basiskante</b> auf (Länge + Richtung
///         im Grundriss X/Z).</item>
///   <item>Punkt&#160;3 legt nur die <b>Tiefe</b> fest: sein Abstand <i>senkrecht</i> zur
///         Basiskante. Die beiden Gegenecken werden so berechnet, dass alle vier Winkel
///         exakt 90° sind — der dritte Tipp muss nicht millimetergenau gesetzt werden.</item>
/// </list>
///
/// Die Rechtwinkligkeit wird im <b>Grundriss</b> (X/Z-Ebene) erzwungen — konsistent mit
/// <see cref="ArContour.CalculateArea"/> (Shoelace auf X/Z) und dem Flächen-Export. Die
/// Höhen (Y) der vier Ecken werden auf die durch die drei Messpunkte definierte Ebene
/// projiziert, sodass eine planare — bei geneigtem Gelände auch geneigte — Fläche entsteht.
///
/// Bewusst <b>stateless + plattformneutral</b> (keine ARCore-/Android-Abhängigkeit), damit
/// die Geometrie direkt per Unit-Test abgesichert werden kann.
/// </summary>
public static class ArRectangleBuilder
{
    /// <summary>Mindestlänge der Basiskante bzw. Tiefe in Metern. Darunter gilt die Eingabe
    /// als entartet (Doppel-Tipp / kollinear) und es wird <c>null</c> geliefert.</summary>
    public const float MinEdgeMeters = 0.05f;

    /// <summary>Standard-Toleranz für den Quadrat-Snap: weicht die Tiefe um weniger als diesen
    /// Anteil von der Basislänge ab, wird sie auf die Basislänge gezogen (10&#160;%).</summary>
    public const float DefaultSquareSnapTolerance = 0.10f;

    /// <summary>Eine berechnete Rechteck-Ecke in lokalen AR-Meter-Koordinaten.</summary>
    public readonly record struct Corner(float X, float Y, float Z);

    /// <summary>Ergebnis der Rechteck-Berechnung: vier umlaufende Ecken + Maße.</summary>
    public sealed class Result
    {
        /// <summary>Die vier Ecken in umlaufender Reihenfolge
        /// (P1 &#8594; P2 &#8594; Gegenecke&#160;2 &#8594; Gegenecke&#160;1). Bildet ein
        /// nicht selbst-schneidendes Polygon.</summary>
        public required Corner[] Corners { get; init; }

        /// <summary>Länge der Basiskante (P1&#8594;P2) in Metern.</summary>
        public float LengthMeters { get; init; }

        /// <summary>Tiefe senkrecht zur Basiskante in Metern (immer positiv).</summary>
        public float DepthMeters { get; init; }

        /// <summary>Grundrissfläche (Länge × Tiefe) in m².</summary>
        public float AreaMeters { get; init; }

        /// <summary>True wenn die Tiefe per Quadrat-Snap auf die Basislänge gezogen wurde.</summary>
        public bool IsSquare { get; init; }
    }

    /// <summary>
    /// Berechnet das rechtwinklige Rechteck. <paramref name="p1"/>/<paramref name="p2"/> sind
    /// die ersten beiden gesetzten Ecken (Basiskante), <paramref name="p3"/> bestimmt die Tiefe
    /// (für die Live-Vorschau ist das die aktuelle Reticle-Welt-Position).
    /// </summary>
    /// <param name="squareSnap">Wenn true und die Tiefe nahe der Basislänge liegt, wird ein
    /// Quadrat erzwungen.</param>
    /// <param name="squareSnapTolerance">Relative Toleranz für den Quadrat-Snap (Default 10&#160;%).</param>
    /// <returns>Das Rechteck oder <c>null</c>, wenn Basiskante oder Tiefe entartet sind.</returns>
    public static Result? Compute(ArPoint p1, ArPoint p2, ArPoint p3,
        bool squareSnap, float squareSnapTolerance = DefaultSquareSnapTolerance)
    {
        // Basiskante im Grundriss (X/Z)
        var ux = p2.X - p1.X;
        var uz = p2.Z - p1.Z;
        var length = MathF.Sqrt(ux * ux + uz * uz);
        if (length < MinEdgeMeters) return null;

        // Einheits-Richtung der Basiskante + Linkssenkrechte (90° im Grundriss)
        var dirX = ux / length;
        var dirZ = uz / length;
        var nX = -dirZ;
        var nZ = dirX;

        // Signierte Tiefe = Projektion von (P3 − P2) auf die Senkrechte. Das Vorzeichen
        // bestimmt, auf welche Seite der Basiskante das Rechteck aufgespannt wird.
        var wx = p3.X - p2.X;
        var wz = p3.Z - p2.Z;
        var depthSigned = wx * nX + wz * nZ;
        var depthAbs = MathF.Abs(depthSigned);
        if (depthAbs < MinEdgeMeters) return null;

        // Quadrat-Snap: Tiefe auf die Basislänge ziehen, Vorzeichen (Seite) beibehalten.
        var isSquare = false;
        if (squareSnap && MathF.Abs(depthAbs - length) <= squareSnapTolerance * length)
        {
            depthSigned = MathF.Sign(depthSigned) * length;
            depthAbs = length;
            isSquare = true;
        }

        // Höhen-Ebene durch die drei Messpunkte (Normale via Kreuzprodukt zweier Kantenvektoren).
        var v1X = ux; var v1Y = p2.Y - p1.Y; var v1Z = uz;
        var v2X = p3.X - p1.X; var v2Y = p3.Y - p1.Y; var v2Z = p3.Z - p1.Z;
        var planeNx = v1Y * v2Z - v1Z * v2Y;
        var planeNy = v1Z * v2X - v1X * v2Z;
        var planeNz = v1X * v2Y - v1Y * v2X;

        // Grundriss-Ecken (Y wird gleich auf die Ebene projiziert)
        var c3X = p2.X + depthSigned * nX;
        var c3Z = p2.Z + depthSigned * nZ;
        var c4X = p1.X + depthSigned * nX;
        var c4Z = p1.Z + depthSigned * nZ;

        // Höhe der Ecken aus der Ebenengleichung N·(r−p1)=0, nach Y aufgelöst.
        // planeNy ist algebraisch exakt −depthSigned·length; da Basislänge UND Tiefe oben
        // bereits auf >= MinEdgeMeters geprüft sind, gilt |planeNy| >= MinEdgeMeters² und
        // HeightAt ist damit immer wohldefiniert. Der 1e-4-Zweig ist ein rein defensiver
        // Division-Schutz (unter den Guards nicht regulär erreichbar) — er liefert dann eine
        // planare Fläche auf gemittelter Höhe statt NaN/Infinity.
        float HeightAt(float x, float z)
        {
            if (MathF.Abs(planeNy) < 1e-4f)
                return (p1.Y + p2.Y + p3.Y) / 3f;
            return p1.Y - (planeNx * (x - p1.X) + planeNz * (z - p1.Z)) / planeNy;
        }

        var corners = new[]
        {
            new Corner(p1.X, HeightAt(p1.X, p1.Z), p1.Z),
            new Corner(p2.X, HeightAt(p2.X, p2.Z), p2.Z),
            new Corner(c3X,  HeightAt(c3X,  c3Z),  c3Z),
            new Corner(c4X,  HeightAt(c4X,  c4Z),  c4Z),
        };

        return new Result
        {
            Corners = corners,
            LengthMeters = length,
            DepthMeters = depthAbs,
            AreaMeters = length * depthAbs,
            IsSquare = isSquare,
        };
    }
}
