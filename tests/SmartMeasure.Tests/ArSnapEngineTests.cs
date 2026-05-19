// ArSnapEngine ist in der Android-DLL — die ist net10.0-android, der Test-Lauf hier
// (net10.0) kann sie nicht direkt referenzieren. Wir testen die Geometrie deshalb über
// eine projizierte Mini-Implementation (Vertex- und Right-Angle-Snap), die 1:1 die
// Logik aus ArSnapEngine spiegelt. Bei Änderungen MÜSSEN beide Stellen synchron bleiben.
//
// Ziel ist nicht doppelte Implementation — eher: garantiert reproduzierbare Tests für
// die geometrische Kernlogik ohne ARCore-Abhängigkeit. Wenn Snap je in shared moved
// wird, kann dieser Test direkt darauf umgestellt werden.

using SmartMeasure.Shared.Models;

namespace SmartMeasure.Tests;

public class ArSnapEngineTests
{
    private const float VertexSnapRadius = 0.15f;
    private const float RightAngleToleranceDeg = 5f;
    private const float RightAngleMinDistance = 0.2f;

    public enum SnapType { None, Vertex, RightAngle }

    private static (float x, float y, float z, SnapType type) Apply(
        float hitX, float hitY, float hitZ,
        IReadOnlyList<ArPoint>? existing,
        IReadOnlyList<ArPoint>? activeContour)
    {
        // Vertex first
        if (existing != null && existing.Count > 0)
        {
            var bestDistSq = VertexSnapRadius * VertexSnapRadius;
            ArPoint? best = null;
            foreach (var p in existing)
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
            if (best != null) return (best.X, best.Y, best.Z, SnapType.Vertex);
        }

        // Right-Angle
        if (activeContour != null && activeContour.Count >= 2)
        {
            var prev = activeContour[^1];
            var prev2 = activeContour[^2];
            var pdx = prev.X - prev2.X;
            var pdz = prev.Z - prev2.Z;
            var prevLen = MathF.Sqrt(pdx * pdx + pdz * pdz);
            if (prevLen >= 0.05f)
            {
                pdx /= prevLen;
                pdz /= prevLen;

                var hdx = hitX - prev.X;
                var hdz = hitZ - prev.Z;
                var hitLen = MathF.Sqrt(hdx * hdx + hdz * hdz);
                if (hitLen >= RightAngleMinDistance)
                {
                    var dot = MathF.Max(-1f, MathF.Min(1f, (hdx * pdx + hdz * pdz) / hitLen));
                    var angleDeg = MathF.Acos(MathF.Abs(dot)) * 180f / MathF.PI;
                    var diffTo90 = MathF.Abs(angleDeg - 90f);
                    if (diffTo90 <= RightAngleToleranceDeg)
                    {
                        var perpAx = -pdz;
                        var perpAz = pdx;
                        var perpBx = pdz;
                        var perpBz = -pdx;
                        var projA = hdx * perpAx + hdz * perpAz;
                        var projB = hdx * perpBx + hdz * perpBz;
                        var (px, pz) = MathF.Abs(projA) > MathF.Abs(projB)
                            ? (perpAx, perpAz) : (perpBx, perpBz);
                        var sign = (px == perpAx && pz == perpAz) ? MathF.Sign(projA) : MathF.Sign(projB);
                        if (sign == 0) sign = 1;
                        var snappedX = prev.X + px * sign * hitLen;
                        var snappedZ = prev.Z + pz * sign * hitLen;
                        return (snappedX, hitY, snappedZ, SnapType.RightAngle);
                    }
                }
            }
        }

        return (hitX, hitY, hitZ, SnapType.None);
    }

    [Fact]
    public void Apply_LeereInputs_LiefertNoneZurueck()
    {
        var (x, _, z, type) = Apply(1f, 0f, 2f, null, null);
        type.Should().Be(SnapType.None);
        x.Should().Be(1f);
        z.Should().Be(2f);
    }

    [Fact]
    public void Apply_NahesterPunktInnerhalb15cm_VertexSnap()
    {
        var existing = new List<ArPoint> { new() { X = 1.05f, Z = 2.05f } };
        var (x, _, z, type) = Apply(1f, 0f, 2f, existing, null);

        type.Should().Be(SnapType.Vertex);
        x.Should().Be(1.05f);
        z.Should().Be(2.05f);
    }

    [Fact]
    public void Apply_PunktAusserhalb15cm_KeinVertexSnap()
    {
        var existing = new List<ArPoint> { new() { X = 1.2f, Z = 2.2f } };
        var (x, _, z, type) = Apply(1f, 0f, 2f, existing, null);

        type.Should().Be(SnapType.None);
        x.Should().Be(1f);
        z.Should().Be(2f);
    }

    [Fact]
    public void Apply_RightAngle_PunktNach1mInOst_EingeschnappOst()
    {
        // Vorherige Strecke: Z=0..-1 (1m Norden). Nächster Punkt bei (0.95, 0, -1) — fast
        // 90° zur Vor-Strecke, leicht abgelenkt → Snap auf exakte Ost-Richtung erwartet.
        var activeContour = new List<ArPoint>
        {
            new() { X = 0, Z = 0 },
            new() { X = 0, Z = -1 }, // letzter Punkt, Vor-Strecke = Norden
        };
        var (x, _, z, type) = Apply(0.95f, 0f, -1.05f, null, activeContour);

        type.Should().Be(SnapType.RightAngle);
        // X-Komponente sollte auf reine Ost-Strecke gerundet sein
        x.Should().BeGreaterThan(0.9f);
        // Z sollte nahe -1 bleiben (gleich wie der vorige Konturpunkt) — wegen
        // 90°-Snap-Logik kann der Wert leicht abweichen, daher Toleranz
        z.Should().BeInRange(-1.1f, -0.9f);
    }

    [Fact]
    public void Apply_RightAngle_KleineDistanz_KeinSnap()
    {
        // Hit innerhalb RightAngleMinDistance (0.2m) → Snap nicht aktiv
        var activeContour = new List<ArPoint>
        {
            new() { X = 0, Z = 0 },
            new() { X = 0, Z = -1 },
        };
        var (x, _, z, type) = Apply(0.1f, 0f, -1.05f, null, activeContour);

        type.Should().Be(SnapType.None);
        x.Should().Be(0.1f);
        z.Should().Be(-1.05f);
    }

    [Fact]
    public void Apply_RightAngle_ZuVielAbweichung_KeinSnap()
    {
        // Vor-Strecke Norden, Hit bei 45° zur Vor-Strecke → außerhalb 5°-Toleranz
        var activeContour = new List<ArPoint>
        {
            new() { X = 0, Z = 0 },
            new() { X = 0, Z = -1 },
        };
        var (_, _, _, type) = Apply(1f, 0f, -2f, null, activeContour);

        type.Should().Be(SnapType.None);
    }

    [Fact]
    public void Apply_VertexHatVorrangVorRightAngle()
    {
        // Sowohl ein nahegelegener Vertex (innerhalb 15cm) als auch eine 90°-Situation —
        // Vertex muss gewinnen weil höhere Priorität.
        var existing = new List<ArPoint> { new() { X = 0.9f, Z = -1f, Y = 0.5f } };
        var activeContour = new List<ArPoint>
        {
            new() { X = 0, Z = 0 },
            new() { X = 0, Z = -1 },
        };
        var (x, y, z, type) = Apply(0.95f, 0f, -1.0f, existing, activeContour);

        type.Should().Be(SnapType.Vertex);
        x.Should().Be(0.9f);
        z.Should().Be(-1f);
        y.Should().Be(0.5f); // Y wird vom Vertex übernommen!
    }
}
