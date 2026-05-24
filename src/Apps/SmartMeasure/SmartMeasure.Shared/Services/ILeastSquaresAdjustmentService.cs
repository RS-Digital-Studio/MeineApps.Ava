namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.18: Least-Squares-Netzausgleich nach Vermessungs-Klassik.
/// Input: Punkt-Liste mit Kovarianzen + Constraints (Distanzen, geschlossene
/// Konturen, geknickte Winkel). Output: verbesserte Positionen + a-posteriori
/// RMS-Fehler. Eigene Implementierung in <c>MeineApps.CalcLib</c> spart ~5MB AAB
/// gegenueber Math.NET.Numerics.</summary>
public interface ILeastSquaresAdjustmentService
{
    /// <summary>Verbessert die Initial-Positionen so dass die Summe der gewichteten
    /// Constraint-Residuen minimal wird. Iterativ (Gauss-Newton oder
    /// Coordinate-Descent).</summary>
    AdjustmentResult Adjust(
        IReadOnlyList<AdjustablePoint> initialPoints,
        IReadOnlyList<DistanceConstraint> constraints,
        int maxIterations = 50);
}

/// <summary>Ein Punkt im Netz mit lokalen Meter-Koordinaten + Genauigkeits-Sigma.</summary>
public sealed record AdjustablePoint(int Id, double X, double Y, double Z, double SigmaMeters);

/// <summary>Bekannte Distanz zwischen zwei Punkten (z.B. aus Hand-Messung mit Massband).</summary>
public sealed record DistanceConstraint(int FromPointId, int ToPointId, double MeasuredDistanceMeters, double SigmaMeters);

/// <summary>Ergebnis des Ausgleichs: verbesserte Positionen + RMS.</summary>
public sealed record AdjustmentResult(
    IReadOnlyList<AdjustablePoint> AdjustedPoints,
    double APostererioriRmsMeters,
    int IterationsUsed);
