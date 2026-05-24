using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

public class LeastSquaresAdjustmentTests
{
    private readonly LeastSquaresAdjustmentService _svc = new();

    [Fact]
    public void Adjust_KeinePunkte_LiefertLeerErgebnis()
    {
        var r = _svc.Adjust([], []);
        r.AdjustedPoints.Should().BeEmpty();
        r.APostererioriRmsMeters.Should().Be(0);
    }

    [Fact]
    public void Adjust_KeineConstraints_PunkteUnveraendert()
    {
        var pts = new[]
        {
            new AdjustablePoint(1, 0, 0, 0, 0.02),
            new AdjustablePoint(2, 10, 0, 0, 0.02),
        };
        var r = _svc.Adjust(pts, []);
        r.AdjustedPoints[0].X.Should().Be(0);
        r.AdjustedPoints[1].X.Should().Be(10);
    }

    [Fact]
    public void Adjust_ZweiPunkteMitBekannterDistanz_KonvergiertAufExakteDistanz()
    {
        // Punkte stehen 10.5m auseinander; Constraint sagt exakt 10m → konvergiert
        var pts = new[]
        {
            new AdjustablePoint(1, 0, 0, 0, 0.05),
            new AdjustablePoint(2, 10.5, 0, 0, 0.05),
        };
        var constraints = new[]
        {
            new DistanceConstraint(1, 2, 10.0, 0.01),
        };
        var r = _svc.Adjust(pts, constraints);

        var p1 = r.AdjustedPoints[0];
        var p2 = r.AdjustedPoints[1];
        var finalDistance = Math.Sqrt(
            (p2.X - p1.X) * (p2.X - p1.X) +
            (p2.Y - p1.Y) * (p2.Y - p1.Y) +
            (p2.Z - p1.Z) * (p2.Z - p1.Z));
        finalDistance.Should().BeApproximately(10.0, 0.02);
        r.APostererioriRmsMeters.Should().BeLessThan(0.02);
    }

    [Fact]
    public void Adjust_DreieckMitDreiKantenDistanzen_KonvergiertNahAnIdealForm()
    {
        // Dreieck mit Kanten 3m / 4m / 5m (rechtwinklig), initial leicht verzerrt.
        var pts = new[]
        {
            new AdjustablePoint(1, 0.05, 0, 0, 0.05),
            new AdjustablePoint(2, 3.1, 0.05, 0, 0.05),
            new AdjustablePoint(3, 0, 4.05, 0, 0.05),
        };
        var c = new[]
        {
            new DistanceConstraint(1, 2, 3.0, 0.01),
            new DistanceConstraint(1, 3, 4.0, 0.01),
            new DistanceConstraint(2, 3, 5.0, 0.01),
        };
        var r = _svc.Adjust(pts, c, maxIterations: 200);
        r.APostererioriRmsMeters.Should().BeLessThan(0.05);
    }
}
