using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

public class ArRectangleBuilderTests
{
    private static ArPoint P(float x, float y, float z) => new() { X = x, Y = y, Z = z };

    /// <summary>Winkel an Ecke <paramref name="i"/> (zwischen Kante i-1→i und i→i+1) in Grad.</summary>
    private static float CornerAngleDeg(ArRectangleBuilder.Corner[] c, int i)
    {
        var prev = c[(i + c.Length - 1) % c.Length];
        var curr = c[i];
        var next = c[(i + 1) % c.Length];
        var ax = prev.X - curr.X; var az = prev.Z - curr.Z;
        var bx = next.X - curr.X; var bz = next.Z - curr.Z;
        var dot = ax * bx + az * bz;
        var mag = MathF.Sqrt(ax * ax + az * az) * MathF.Sqrt(bx * bx + bz * bz);
        return MathF.Acos(Math.Clamp(dot / mag, -1f, 1f)) * 180f / MathF.PI;
    }

    [Fact]
    public void Compute_AchsenparalleleEingabe_LiefertRechteck4x2()
    {
        // Basiskante 4 m entlang X, dritter Punkt 2 m in +Z
        var r = ArRectangleBuilder.Compute(P(0, 0, 0), P(4, 0, 0), P(4, 0, 2), squareSnap: false);

        r.Should().NotBeNull();
        r!.LengthMeters.Should().BeApproximately(4f, 1e-4f);
        r.DepthMeters.Should().BeApproximately(2f, 1e-4f);
        r.AreaMeters.Should().BeApproximately(8f, 1e-3f);
        r.IsSquare.Should().BeFalse();
        r.Corners.Should().HaveCount(4);
    }

    [Fact]
    public void Compute_ErzwingtRechteWinkelAnAllenVierEcken()
    {
        // Dritter Punkt absichtlich schief (nicht senkrecht) — Ecken müssen trotzdem 90° sein
        var r = ArRectangleBuilder.Compute(P(0, 0, 0), P(3, 0, 0), P(5, 0, 2.5f), squareSnap: false);

        r.Should().NotBeNull();
        for (var i = 0; i < 4; i++)
            CornerAngleDeg(r!.Corners, i).Should().BeApproximately(90f, 0.01f);
    }

    [Fact]
    public void Compute_DritterPunktBestimmtNurTiefe_NichtDieEckenPosition()
    {
        // P3 liegt 2 m hinter P2 in X-Richtung verschoben — die senkrechte Tiefe ist trotzdem 2 m
        var r = ArRectangleBuilder.Compute(P(0, 0, 0), P(4, 0, 0), P(1, 0, 2), squareSnap: false);

        r.Should().NotBeNull();
        r!.DepthMeters.Should().BeApproximately(2f, 1e-4f);
        // Gegenecken liegen exakt senkrecht über der Basiskante (X bleibt 0 bzw. 4)
        r.Corners[2].X.Should().BeApproximately(4f, 1e-3f);
        r.Corners[3].X.Should().BeApproximately(0f, 1e-3f);
        r.Corners[2].Z.Should().BeApproximately(2f, 1e-3f);
        r.Corners[3].Z.Should().BeApproximately(2f, 1e-3f);
    }

    [Fact]
    public void Compute_OrientierteBasiskante_RechteckFolgtRichtung()
    {
        // Basiskante diagonal (45°), Länge sqrt(2)*... → Winkel müssen 90° bleiben
        var r = ArRectangleBuilder.Compute(P(0, 0, 0), P(2, 0, 2), P(3, 0, 1), squareSnap: false);

        r.Should().NotBeNull();
        r!.LengthMeters.Should().BeApproximately(MathF.Sqrt(8f), 1e-4f);
        for (var i = 0; i < 4; i++)
            CornerAngleDeg(r!.Corners, i).Should().BeApproximately(90f, 0.01f);
    }

    [Fact]
    public void Compute_QuadratSnap_ZiehtTiefeAufBasislaenge()
    {
        // Tiefe 1.92 liegt innerhalb 10 % von 2.0 → snappt auf 2.0
        var r = ArRectangleBuilder.Compute(P(0, 0, 0), P(2, 0, 0), P(2, 0, 1.92f), squareSnap: true);

        r.Should().NotBeNull();
        r!.IsSquare.Should().BeTrue();
        r.DepthMeters.Should().BeApproximately(2f, 1e-4f);
        r.AreaMeters.Should().BeApproximately(4f, 1e-3f);
    }

    [Fact]
    public void Compute_QuadratSnap_AusserhalbToleranz_KeinSnap()
    {
        // Tiefe 1.5 ist 25 % weg von 2.0 → kein Snap trotz aktivem Flag
        var r = ArRectangleBuilder.Compute(P(0, 0, 0), P(2, 0, 0), P(2, 0, 1.5f), squareSnap: true);

        r.Should().NotBeNull();
        r!.IsSquare.Should().BeFalse();
        r.DepthMeters.Should().BeApproximately(1.5f, 1e-4f);
    }

    [Fact]
    public void Compute_QuadratSnap_BehaeltSeite_BeiNegativerTiefe()
    {
        // Dritter Punkt auf der −Z-Seite → Snap darf die Seite nicht spiegeln
        var r = ArRectangleBuilder.Compute(P(0, 0, 0), P(2, 0, 0), P(2, 0, -1.95f), squareSnap: true);

        r.Should().NotBeNull();
        r!.IsSquare.Should().BeTrue();
        r.Corners[2].Z.Should().BeApproximately(-2f, 1e-3f);
        r.Corners[3].Z.Should().BeApproximately(-2f, 1e-3f);
    }

    [Fact]
    public void Compute_FlaecheStimmtMitShoelaceUeberein()
    {
        var r = ArRectangleBuilder.Compute(P(0, 0, 0), P(3, 0, 0), P(3, 0, 2), squareSnap: false);
        r.Should().NotBeNull();

        var contour = new ArContour
        {
            IsClosed = true,
            Points = r!.Corners.Select(c => new ArPoint { X = c.X, Y = c.Y, Z = c.Z }).ToList(),
        };
        contour.CalculateArea().Should().BeApproximately(r.AreaMeters, 1e-3f);
    }

    [Fact]
    public void Compute_GeneigteEbene_EckenLiegenPlanar()
    {
        // Drei Punkte definieren eine geneigte Ebene (P2 + P3 angehoben) → 4. Ecke muss
        // exakt auf dieser Ebene liegen (planare Fläche, kein Höhensprung).
        var p1 = P(0, 0, 0);
        var p2 = P(4, 1, 0);     // Basiskante steigt 1 m über 4 m
        var p3 = P(4, 1, 2);
        var r = ArRectangleBuilder.Compute(p1, p2, p3, squareSnap: false);

        r.Should().NotBeNull();
        // Ebene durch p1,p2,p3: Y hängt nur von X ab (Steigung 0.25). Ecke 4 bei X=0 → Y=0.
        r!.Corners[3].Y.Should().BeApproximately(0f, 1e-3f);
        r.Corners[2].Y.Should().BeApproximately(1f, 1e-3f);
    }

    [Fact]
    public void Compute_KleineTiefeAufGeneigterEbene_LiefertEndlicheWerte()
    {
        // Knapp valide Tiefe (6 cm) auf einer steil geneigten Ebene → keine Division-Artefakte
        // (NaN/Infinity), Höhen bleiben endlich und planar.
        var r = ArRectangleBuilder.Compute(P(0, 0, 0), P(2, 0, 0), P(2, 1.5f, 0.06f), squareSnap: false);

        r.Should().NotBeNull();
        r!.DepthMeters.Should().BeApproximately(0.06f, 1e-4f);
        foreach (var c in r.Corners)
        {
            float.IsFinite(c.X).Should().BeTrue();
            float.IsFinite(c.Y).Should().BeTrue();
            float.IsFinite(c.Z).Should().BeTrue();
        }
    }

    [Fact]
    public void Compute_EntarteteBasiskante_LiefertNull()
    {
        // P1 und P2 zu nah beieinander (< 5 cm)
        ArRectangleBuilder.Compute(P(0, 0, 0), P(0.02f, 0, 0), P(0.02f, 0, 2), squareSnap: false)
            .Should().BeNull();
    }

    [Fact]
    public void Compute_EntarteteTiefe_LiefertNull()
    {
        // Dritter Punkt liegt auf der Basiskante (keine senkrechte Ausdehnung)
        ArRectangleBuilder.Compute(P(0, 0, 0), P(4, 0, 0), P(2, 0, 0), squareSnap: false)
            .Should().BeNull();
    }
}
