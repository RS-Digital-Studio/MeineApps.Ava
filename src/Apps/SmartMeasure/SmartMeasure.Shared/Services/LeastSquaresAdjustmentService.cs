namespace SmartMeasure.Shared.Services;

/// <summary>Plan-Kap. 5.18 (MVP): Position-based Dynamics (PBD). Iteriert ueber alle
/// Constraints; pro Constraint wird die Distanz-Verletzung anteilig auf beide Punkte
/// verteilt (gewichtet via 1/sigma^2). Konvergiert robust ohne Matrix-Inversion und
/// ohne empfindliche Schrittweite. Sehr verbreitet in Physik-Simulationen — fuer
/// Vermessungs-Netze ein guter Match.</summary>
public sealed class LeastSquaresAdjustmentService : ILeastSquaresAdjustmentService
{
    /// <summary>Stiffness-Faktor pro Iteration (0..1). 0.5 ist konservativ stabil.</summary>
    private const double Stiffness = 0.5;

    /// <summary>Konvergenz-Schwelle in Meter: wenn die maximale Punkt-Verschiebung
    /// pro Iteration kleiner ist, brechen wir ab.</summary>
    private const double ConvergenceMeters = 0.0005; // 0.5 mm

    public AdjustmentResult Adjust(
        IReadOnlyList<AdjustablePoint> initialPoints,
        IReadOnlyList<DistanceConstraint> constraints,
        int maxIterations = 50)
    {
        if (initialPoints.Count == 0)
            return new AdjustmentResult([], 0, 0);

        // Mutable Working-Copy + per-Punkt-Gewicht (1/sigma^2 — sichere Punkte
        // bewegen sich weniger als unsichere).
        var pts = new Dictionary<int, double[]>(initialPoints.Count);
        var weights = new Dictionary<int, double>(initialPoints.Count);
        foreach (var p in initialPoints)
        {
            pts[p.Id] = [p.X, p.Y, p.Z];
            var s = Math.Max(p.SigmaMeters, 0.001);
            weights[p.Id] = 1.0 / (s * s);
        }

        var iteration = 0;
        for (; iteration < maxIterations; iteration++)
        {
            double maxStep = 0;

            foreach (var c in constraints)
            {
                if (!pts.TryGetValue(c.FromPointId, out var a)) continue;
                if (!pts.TryGetValue(c.ToPointId, out var b)) continue;

                var dx = b[0] - a[0];
                var dy = b[1] - a[1];
                var dz = b[2] - a[2];
                var current = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (current < 1e-6) continue;

                var residual = current - c.MeasuredDistanceMeters;
                if (Math.Abs(residual) < ConvergenceMeters) continue;

                // PBD: Korrektur anteilig nach Gewicht aufteilen. Total = wA + wB.
                // dA = -correction * (wA / total)
                // dB = +correction * (wB / total)
                var wA = weights[c.FromPointId];
                var wB = weights[c.ToPointId];
                var total = wA + wB;
                if (total < 1e-9) continue;

                var inv = 1.0 / current;
                var nx = dx * inv;
                var ny = dy * inv;
                var nz = dz * inv;
                var correction = residual * Stiffness;
                var fracA = wA / total;
                var fracB = wB / total;

                var aShift = correction * fracA;
                var bShift = -correction * fracB;

                a[0] += nx * aShift;
                a[1] += ny * aShift;
                a[2] += nz * aShift;
                b[0] += nx * bShift;
                b[1] += ny * bShift;
                b[2] += nz * bShift;

                var step = Math.Max(Math.Abs(aShift), Math.Abs(bShift));
                if (step > maxStep) maxStep = step;
            }

            if (maxStep < ConvergenceMeters) { iteration++; break; }
        }

        // A-posteriori RMS aus den finalen Residuen
        double sumSq = 0;
        var n = 0;
        foreach (var c in constraints)
        {
            if (!pts.TryGetValue(c.FromPointId, out var a)) continue;
            if (!pts.TryGetValue(c.ToPointId, out var b)) continue;
            var dx = b[0] - a[0];
            var dy = b[1] - a[1];
            var dz = b[2] - a[2];
            var current = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            var residual = current - c.MeasuredDistanceMeters;
            sumSq += residual * residual;
            n++;
        }
        var rms = n > 0 ? Math.Sqrt(sumSq / n) : 0;

        var result = new List<AdjustablePoint>(pts.Count);
        foreach (var p in initialPoints)
        {
            var pos = pts[p.Id];
            result.Add(new AdjustablePoint(p.Id, pos[0], pos[1], pos[2], p.SigmaMeters));
        }
        return new AdjustmentResult(result, rms, iteration);
    }
}
