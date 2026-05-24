// ArSnapEngine lebt seit Plan-Kap. 3.7 (Parallel + Extension) in SmartMeasure.Shared.Services —
// damit koennen wir die echte Klasse direkt testen statt eine Mini-Kopie zu pflegen.

using SmartMeasure.Shared.Models;
using SmartMeasure.Shared.Services;

namespace SmartMeasure.Tests;

public class ArSnapEngineTests
{
    [Fact]
    public void Apply_LeereInputs_LiefertNoneZurueck()
    {
        var (x, _, z, type) = ArSnapEngine.Apply(1f, 0f, 2f, null, null);
        type.Should().Be(ArSnapEngine.SnapType.None);
        x.Should().Be(1f);
        z.Should().Be(2f);
    }

    [Fact]
    public void Apply_NahesterPunktInnerhalb15cm_VertexSnap()
    {
        var existing = new List<ArPoint> { new() { X = 1.05f, Z = 2.05f } };
        var (x, _, z, type) = ArSnapEngine.Apply(1f, 0f, 2f, existing, null);

        type.Should().Be(ArSnapEngine.SnapType.Vertex);
        x.Should().Be(1.05f);
        z.Should().Be(2.05f);
    }

    [Fact]
    public void Apply_PunktAusserhalb15cm_KeinVertexSnap()
    {
        var existing = new List<ArPoint> { new() { X = 1.2f, Z = 2.2f } };
        var (x, _, z, type) = ArSnapEngine.Apply(1f, 0f, 2f, existing, null);

        type.Should().Be(ArSnapEngine.SnapType.None);
        x.Should().Be(1f);
        z.Should().Be(2f);
    }

    [Fact]
    public void Apply_RightAngle_PunktNach1mInOst_EingeschnappOst()
    {
        // Vorherige Strecke: Z=0..-1 (1m Norden). Naechster Punkt bei (0.95, 0, -1) — fast
        // 90° zur Vor-Strecke, leicht abgelenkt → Snap auf exakte Ost-Richtung erwartet.
        var activeContour = new List<ArPoint>
        {
            new() { X = 0, Z = 0 },
            new() { X = 0, Z = -1 }, // letzter Punkt, Vor-Strecke = Norden
        };
        var (x, _, z, type) = ArSnapEngine.Apply(0.95f, 0f, -1.05f, null, activeContour);

        type.Should().Be(ArSnapEngine.SnapType.RightAngle);
        x.Should().BeGreaterThan(0.9f);
        z.Should().BeInRange(-1.1f, -0.9f);
    }

    [Fact]
    public void Apply_RightAngle_KleineDistanz_KeinSnap()
    {
        var activeContour = new List<ArPoint>
        {
            new() { X = 0, Z = 0 },
            new() { X = 0, Z = -1 },
        };
        var (x, _, z, type) = ArSnapEngine.Apply(0.1f, 0f, -1.05f, null, activeContour);

        type.Should().Be(ArSnapEngine.SnapType.None);
        x.Should().Be(0.1f);
        z.Should().Be(-1.05f);
    }

    [Fact]
    public void Apply_RightAngle_ZuVielAbweichung_KeinSnap()
    {
        // Vor-Strecke Norden, Hit bei 45° → ausserhalb 5°-Toleranz, ABER innerhalb
        // 3°-Parallel-Toleranz... nein, 45° ist auch nicht parallel. Beides None.
        var activeContour = new List<ArPoint>
        {
            new() { X = 0, Z = 0 },
            new() { X = 0, Z = -1 },
        };
        var (_, _, _, type) = ArSnapEngine.Apply(1f, 0f, -2f, null, activeContour);

        type.Should().Be(ArSnapEngine.SnapType.None);
    }

    [Fact]
    public void Apply_VertexHatVorrangVorRightAngle()
    {
        var existing = new List<ArPoint> { new() { X = 0.9f, Z = -1f, Y = 0.5f } };
        var activeContour = new List<ArPoint>
        {
            new() { X = 0, Z = 0 },
            new() { X = 0, Z = -1 },
        };
        var (x, y, z, type) = ArSnapEngine.Apply(0.95f, 0f, -1.0f, existing, activeContour);

        type.Should().Be(ArSnapEngine.SnapType.Vertex);
        x.Should().Be(0.9f);
        z.Should().Be(-1f);
        y.Should().Be(0.5f); // Y wird vom Vertex uebernommen
    }

    // ===== Parallel-Snap (Plan-Kap. 3.7) =====

    [Fact]
    public void Apply_ParallelSnap_HitFastParallelZuEdge_AufExakteRichtung()
    {
        // Bestehende Edge: (0,0,0)→(0,0,-2) — zeigt Norden.
        // Aktive Kontur: ein Punkt bei (5,0,0). Hit bei (5.05, 0, -1) — fast genau
        // parallel zur Norden-Edge (~1° Versatz). Erwartet: Snap auf reines Nord.
        var north = new ArPoint { X = 0, Z = 0 };
        var northEnd = new ArPoint { X = 0, Z = -2 };
        var edges = new List<ArSnapEngine.Edge> { new(north, northEnd) };

        var activeContour = new List<ArPoint> { new() { X = 5, Z = 0 } };
        var (x, _, z, type) = ArSnapEngine.Apply(5.05f, 0f, -1f, null, activeContour, edges);

        type.Should().Be(ArSnapEngine.SnapType.Parallel);
        x.Should().BeApproximately(5f, 0.001f); // X-Komponente exakt auf 5 gesnappt
        z.Should().BeLessThan(0f); // Richtung negativ-Z (Norden) erhalten
    }

    [Fact]
    public void Apply_ParallelSnap_HitZuSchief_KeinSnap()
    {
        // Edge Norden, Hit 45° schief → ausserhalb 3°-Toleranz → kein Parallel-Snap.
        var north = new ArPoint { X = 0, Z = 0 };
        var northEnd = new ArPoint { X = 0, Z = -2 };
        var edges = new List<ArSnapEngine.Edge> { new(north, northEnd) };

        var activeContour = new List<ArPoint> { new() { X = 5, Z = 0 } };
        var (_, _, _, type) = ArSnapEngine.Apply(6f, 0f, -1f, null, activeContour, edges);

        type.Should().Be(ArSnapEngine.SnapType.None);
    }

    [Fact]
    public void Apply_RightAngle_HatVorrangVorParallel()
    {
        // Vorherige Strecke Norden, Hit fast 90° dazu UND ungefaehr parallel zu einer
        // anderen Edge → Right-Angle gewinnt (hoehere Prio).
        var activeContour = new List<ArPoint>
        {
            new() { X = 0, Z = 0 },
            new() { X = 0, Z = -1 },
        };

        // Edge zeigt Ost — der 90°-Snap zur Vor-Strecke Norden liefert auch Ost.
        var eastA = new ArPoint { X = 0, Z = -5 };
        var eastB = new ArPoint { X = 2, Z = -5 };
        var edges = new List<ArSnapEngine.Edge> { new(eastA, eastB) };

        // Hit ~89° zur Vor-Strecke (Right-Angle-Match) UND ~0° zu eastA-eastB (Parallel-Match)
        var (_, _, _, type) = ArSnapEngine.Apply(1.0f, 0f, -1.05f, null, activeContour, edges);

        type.Should().Be(ArSnapEngine.SnapType.RightAngle);
    }

    // ===== Extension-Snap (Plan-Kap. 3.7) =====

    [Fact]
    public void Apply_ExtensionSnap_HitNahVerlaengerung_AufLinieProjiziert()
    {
        // Edge: (0,0,0) → (0,0,-2). Verlaengerung Richtung Norden geht durch (0,0,-3),
        // (0,0,-4) usw. Hit bei (0.05, 0, -3.5) liegt 5cm seitlich der Verlaengerung,
        // 1.5m jenseits B. → Snap auf (0, 0, -3.5).
        var a = new ArPoint { X = 0, Z = 0 };
        var b = new ArPoint { X = 0, Z = -2 };
        var edges = new List<ArSnapEngine.Edge> { new(a, b) };

        var (x, _, z, type) = ArSnapEngine.Apply(0.05f, 0f, -3.5f, null, null, edges);

        type.Should().Be(ArSnapEngine.SnapType.Extension);
        x.Should().BeApproximately(0f, 0.001f);
        z.Should().BeApproximately(-3.5f, 0.001f);
    }

    [Fact]
    public void Apply_ExtensionSnap_HitZuWeitVonLinie_KeinSnap()
    {
        // Hit 20cm seitlich der Verlaengerungslinie → ausserhalb 10cm-Reichweite.
        var a = new ArPoint { X = 0, Z = 0 };
        var b = new ArPoint { X = 0, Z = -2 };
        var edges = new List<ArSnapEngine.Edge> { new(a, b) };

        var (_, _, _, type) = ArSnapEngine.Apply(0.2f, 0f, -3.5f, null, null, edges);
        type.Should().Be(ArSnapEngine.SnapType.None);
    }

    [Fact]
    public void Apply_ExtensionSnap_HitInnerhalbEdgeBereich_KeinSnap()
    {
        // Hit auf der Edge selbst (t=0.5, zwischen A und B) → kein Extension-Snap
        // (das waere Vertex/RightAngle-Job, nicht Extension).
        var a = new ArPoint { X = 0, Z = 0 };
        var b = new ArPoint { X = 0, Z = -2 };
        var edges = new List<ArSnapEngine.Edge> { new(a, b) };

        var (_, _, _, type) = ArSnapEngine.Apply(0.05f, 0f, -1.0f, null, null, edges);
        type.Should().Be(ArSnapEngine.SnapType.None);
    }

    [Fact]
    public void Apply_VertexHatVorrangVorExtension()
    {
        // Vertex (B) liegt innerhalb 15cm vom Hit, Hit ist gleichzeitig auf Verlaengerung
        // — Vertex muss gewinnen.
        var a = new ArPoint { X = 0, Z = 0 };
        var b = new ArPoint { X = 0, Z = -2, Y = 0.3f };
        var edges = new List<ArSnapEngine.Edge> { new(a, b) };
        var existing = new List<ArPoint> { b };

        var (x, y, z, type) = ArSnapEngine.Apply(0.05f, 0f, -2.1f, existing, null, edges);

        type.Should().Be(ArSnapEngine.SnapType.Vertex);
        x.Should().Be(0f);
        y.Should().Be(0.3f);
        z.Should().Be(-2f);
    }
}
