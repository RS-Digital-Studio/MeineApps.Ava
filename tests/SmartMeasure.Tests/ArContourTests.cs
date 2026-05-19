using SmartMeasure.Shared.Models;

namespace SmartMeasure.Tests;

public class ArContourTests
{
    [Fact]
    public void CalculateLength_LeereKontur_IstNull()
    {
        var c = new ArContour();
        c.CalculateLength().Should().Be(0);
    }

    [Fact]
    public void CalculateLength_EinPunkt_IstNull()
    {
        var c = new ArContour { Points = [new ArPoint()] };
        c.CalculateLength().Should().Be(0);
    }

    [Fact]
    public void CalculateLength_DreiPunkteAufLinie_AddiertSegmente()
    {
        var c = new ArContour
        {
            Points = [
                new ArPoint { X = 0 },
                new ArPoint { X = 10 },
                new ArPoint { X = 20 },
            ],
        };
        c.CalculateLength().Should().BeApproximately(20f, 1e-4f);
    }

    [Fact]
    public void CalculateLength_GeschlossenesQuadrat_4Kanten()
    {
        var c = new ArContour
        {
            IsClosed = true,
            Points = [
                new ArPoint { X = 0, Z = 0 },
                new ArPoint { X = 10, Z = 0 },
                new ArPoint { X = 10, Z = 10 },
                new ArPoint { X = 0, Z = 10 },
            ],
        };
        c.CalculateLength().Should().BeApproximately(40f, 1e-4f);
    }

    [Fact]
    public void CalculateLength_OffenesQuadrat_3Kanten()
    {
        var c = new ArContour
        {
            IsClosed = false,
            Points = [
                new ArPoint { X = 0, Z = 0 },
                new ArPoint { X = 10, Z = 0 },
                new ArPoint { X = 10, Z = 10 },
                new ArPoint { X = 0, Z = 10 },
            ],
        };
        c.CalculateLength().Should().BeApproximately(30f, 1e-4f);
    }

    [Fact]
    public void CalculateArea_OffeneKontur_IstNull()
    {
        var c = new ArContour
        {
            IsClosed = false,
            Points = [
                new ArPoint { X = 0, Z = 0 },
                new ArPoint { X = 10, Z = 0 },
                new ArPoint { X = 10, Z = 10 },
            ],
        };
        c.CalculateArea().Should().Be(0);
    }

    [Fact]
    public void CalculateArea_GeschlossenesQuadrat_IstSeitenlaengeQuadriert()
    {
        var c = new ArContour
        {
            IsClosed = true,
            Points = [
                new ArPoint { X = 0, Z = 0 },
                new ArPoint { X = 10, Z = 0 },
                new ArPoint { X = 10, Z = 10 },
                new ArPoint { X = 0, Z = 10 },
            ],
        };
        c.CalculateArea().Should().BeApproximately(100f, 1e-4f);
    }

    [Fact]
    public void CalculateArea_Dreieck_HalbeBasisMalHoehe()
    {
        // Rechtwinkliges Dreieck mit Kathete 6 und 8 → Flaeche = 24
        var c = new ArContour
        {
            IsClosed = true,
            Points = [
                new ArPoint { X = 0, Z = 0 },
                new ArPoint { X = 6, Z = 0 },
                new ArPoint { X = 0, Z = 8 },
            ],
        };
        c.CalculateArea().Should().BeApproximately(24f, 1e-4f);
    }

    [Fact]
    public void CalculateArea_GegenUhrzeigersinn_AbsolutbetragGleich()
    {
        // Shoelace gibt negatives Vorzeichen bei CW vs CCW — der CalculateArea muss
        // absolute Wert zurueckgeben.
        var ccw = new ArContour
        {
            IsClosed = true,
            Points = [
                new ArPoint { X = 0, Z = 0 },
                new ArPoint { X = 10, Z = 0 },
                new ArPoint { X = 10, Z = 10 },
                new ArPoint { X = 0, Z = 10 },
            ],
        };
        var cw = new ArContour
        {
            IsClosed = true,
            Points = [
                new ArPoint { X = 0, Z = 0 },
                new ArPoint { X = 0, Z = 10 },
                new ArPoint { X = 10, Z = 10 },
                new ArPoint { X = 10, Z = 0 },
            ],
        };
        ccw.CalculateArea().Should().Be(cw.CalculateArea());
    }
}

public class ArPointTests
{
    [Fact]
    public void DistanceTo_ZweiPunkte_PythagorasIn3D()
    {
        var a = new ArPoint { X = 0, Y = 0, Z = 0 };
        var b = new ArPoint { X = 3, Y = 4, Z = 0 };
        a.DistanceTo(b).Should().BeApproximately(5f, 1e-4f);
    }

    [Fact]
    public void Distance2DTo_IgnoriertY()
    {
        var a = new ArPoint { X = 0, Y = 100, Z = 0 };
        var b = new ArPoint { X = 3, Y = 0, Z = 4 };
        // 3D-Abstand wuerde sqrt(9 + 10000 + 16) = ~100.13 sein.
        // 2D ignoriert Y: sqrt(9 + 16) = 5.
        a.Distance2DTo(b).Should().BeApproximately(5f, 1e-4f);
    }
}
