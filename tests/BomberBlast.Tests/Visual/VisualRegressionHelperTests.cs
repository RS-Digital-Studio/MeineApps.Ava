using SkiaSharp;
using FluentAssertions;
using Xunit;

namespace BomberBlast.Tests.Visual;

/// <summary>
/// Tests für VisualRegressionHelper (Phase 26b — T4).
/// Validiert die Helper-Methoden ohne echte Renderer-Output abhängig zu machen.
/// </summary>
public class VisualRegressionHelperTests
{
    [Fact]
    public void ComputeDiffRate_IdentischeBitmaps_LiefertNull()
    {
        using var a = CreateSolidBitmap(100, 100, SKColors.Red);
        using var b = CreateSolidBitmap(100, 100, SKColors.Red);
        VisualRegressionHelper.ComputeDiffRate(a, b).Should().Be(0f);
    }

    [Fact]
    public void ComputeDiffRate_KomplettAndere_LiefertEins()
    {
        using var a = CreateSolidBitmap(50, 50, SKColors.Red);
        using var b = CreateSolidBitmap(50, 50, SKColors.Blue);
        VisualRegressionHelper.ComputeDiffRate(a, b).Should().BeApproximately(1f, 0.01f);
    }

    [Fact]
    public void ComputeDiffRate_TeilweiseAnders_LiefertProportional()
    {
        // Bitmap A: oben rot, unten rot
        // Bitmap B: oben rot, unten blau (50% anders)
        using var a = new SKBitmap(10, 10);
        using var b = new SKBitmap(10, 10);
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                a.SetPixel(x, y, SKColors.Red);
                b.SetPixel(x, y, y < 5 ? SKColors.Red : SKColors.Blue);
            }
        }
        VisualRegressionHelper.ComputeDiffRate(a, b).Should().BeApproximately(0.5f, 0.01f);
    }

    [Fact]
    public void ComputeDiffRate_ToleranzWirdRespektiert()
    {
        // Pixel-Diff von 3 mit Tolerance 5 → kein Diff
        using var a = CreateSolidBitmap(10, 10, new SKColor(100, 100, 100));
        using var b = CreateSolidBitmap(10, 10, new SKColor(103, 103, 103));
        VisualRegressionHelper.ComputeDiffRate(a, b, tolerance: 5).Should().Be(0f);
        // Mit Tolerance 1 → alle anders
        VisualRegressionHelper.ComputeDiffRate(a, b, tolerance: 1).Should().BeApproximately(1f, 0.01f);
    }

    [Fact]
    public void ComputeDiffRate_VerschiedeneDimensionen_Wirft()
    {
        using var a = CreateSolidBitmap(100, 100, SKColors.Red);
        using var b = CreateSolidBitmap(50, 50, SKColors.Red);
        var act = () => VisualRegressionHelper.ComputeDiffRate(a, b);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateDiffBitmap_LiefertGueltigesBitmap()
    {
        using var a = CreateSolidBitmap(20, 20, SKColors.Red);
        using var b = CreateSolidBitmap(20, 20, SKColors.Blue);
        using var diff = VisualRegressionHelper.CreateDiffBitmap(a, b);

        diff.Width.Should().Be(20);
        diff.Height.Should().Be(20);
        // Erwartung: Alle Pixel sind rot (alle Pixel unterscheiden sich)
        diff.GetPixel(10, 10).Red.Should().BeGreaterThan(200);
    }

    [Fact]
    public void EncodeDecodePng_Roundtrip_LaeuftDurch()
    {
        using var original = CreateSolidBitmap(40, 40, SKColors.Green);
        var pngBytes = VisualRegressionHelper.EncodePng(original);
        pngBytes.Should().NotBeEmpty();

        using var decoded = VisualRegressionHelper.DecodePng(pngBytes);
        decoded.Width.Should().Be(40);
        decoded.Height.Should().Be(40);
        VisualRegressionHelper.ComputeDiffRate(original, decoded).Should().BeApproximately(0f, 0.05f);
    }

    [Fact]
    public void CreateTestSurface_LiefertSurfaceMitKorrektenDimensionen()
    {
        using var surface = VisualRegressionHelper.CreateTestSurface(640, 360);
        surface.Should().NotBeNull();
        // Auf das Canvas zeichnen
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var bitmap = VisualRegressionHelper.SnapshotToBitmap(surface);
        bitmap.Width.Should().Be(640);
        bitmap.Height.Should().Be(360);
    }

    private static SKBitmap CreateSolidBitmap(int width, int height, SKColor color)
    {
        var bmp = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(color);
        return bmp;
    }
}
