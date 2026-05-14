using SkiaSharp;

namespace BomberBlast.Tests.Visual;

/// <summary>
/// Visual-Regression-Helper (Phase 26b — T4).
///
/// <para>SkiaSharp-basiertes Screenshot-Diffing für CI-Visual-Regression.
/// Pattern: Renderer-Output in SKBitmap, Vergleich gegen Baseline-PNG, Pixel-Diff-Quote.
/// Mismatch über Threshold → Test fail mit Diff-Bitmap als Artifact.</para>
///
/// <para>Use-Cases:</para>
/// <list type="bullet">
///   <item>HUD-Layout-Regression: Score/Combo/Lives-Bereich rendert konsistent.</item>
///   <item>Boss-Sprite-Regression: nach Asset-Update prüfen ob Boss noch korrekt aussieht.</item>
///   <item>Floating-Text-Regression: Text-Bubble-Layout (Phase 22b Crit-Indicator).</item>
///   <item>Theme-Konsistenz: Classic vs. Neon-Style produzieren erwartete Pixel-Verteilung.</item>
/// </list>
///
/// <para>Schwellwert-Strategie: 1% Pixel-Toleranz für Anti-Aliasing-Random,
/// 5% für Floating-Point-Rendering-Drift zwischen Plattformen. Robust ohne fragil zu sein.</para>
/// </summary>
public static class VisualRegressionHelper
{
    /// <summary>
    /// Vergleicht zwei Bitmaps und gibt die Pixel-Diff-Rate [0..1] zurück.
    /// </summary>
    /// <param name="actual">Aktuelles Render-Output.</param>
    /// <param name="baseline">Erwartetes Baseline-Bitmap.</param>
    /// <param name="tolerance">RGB-Tolerance pro Pixel (0-255). 5 = Anti-Aliasing-typisch.</param>
    /// <returns>Anteil der differierenden Pixel (0=identisch, 1=alle anders).</returns>
    public static float ComputeDiffRate(SKBitmap actual, SKBitmap baseline, int tolerance = 5)
    {
        if (actual == null) throw new ArgumentNullException(nameof(actual));
        if (baseline == null) throw new ArgumentNullException(nameof(baseline));
        if (actual.Width != baseline.Width || actual.Height != baseline.Height)
            throw new InvalidOperationException(
                $"Bitmaps haben unterschiedliche Dimensionen: " +
                $"actual={actual.Width}x{actual.Height} baseline={baseline.Width}x{baseline.Height}");

        long total = (long)actual.Width * actual.Height;
        long differing = 0;

        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                var a = actual.GetPixel(x, y);
                var b = baseline.GetPixel(x, y);

                if (Math.Abs(a.Red - b.Red) > tolerance ||
                    Math.Abs(a.Green - b.Green) > tolerance ||
                    Math.Abs(a.Blue - b.Blue) > tolerance ||
                    Math.Abs(a.Alpha - b.Alpha) > tolerance)
                {
                    differing++;
                }
            }
        }

        return total > 0 ? (float)differing / total : 0f;
    }

    /// <summary>
    /// Erzeugt ein Diff-Bitmap (rot = unterschiedlich, transparent = identisch) für CI-Artifact.
    /// </summary>
    public static SKBitmap CreateDiffBitmap(SKBitmap actual, SKBitmap baseline, int tolerance = 5)
    {
        var diff = new SKBitmap(actual.Width, actual.Height);
        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                var a = actual.GetPixel(x, y);
                var b = baseline.GetPixel(x, y);

                bool differs = Math.Abs(a.Red - b.Red) > tolerance ||
                               Math.Abs(a.Green - b.Green) > tolerance ||
                               Math.Abs(a.Blue - b.Blue) > tolerance;

                diff.SetPixel(x, y, differs
                    ? new SKColor(255, 0, 0, 200)
                    : SKColors.Transparent);
            }
        }
        return diff;
    }

    /// <summary>
    /// Erstellt ein Test-Render-Surface mit gegebenen Dimensionen für Renderer-Tests.
    /// Caller ist für Dispose verantwortlich.
    /// </summary>
    public static SKSurface CreateTestSurface(int width = 800, int height = 480)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        return SKSurface.Create(info);
    }

    /// <summary>
    /// Schnappt das Surface zu einem SKBitmap (für Vergleich oder Speichern).
    /// </summary>
    public static SKBitmap SnapshotToBitmap(SKSurface surface)
    {
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    /// <summary>
    /// Speichert ein Bitmap als PNG (für Baseline-Capture oder Diff-Output).
    /// </summary>
    public static byte[] EncodePng(SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Lädt ein Bitmap aus PNG-Bytes (für Baseline-Vergleich).
    /// </summary>
    public static SKBitmap DecodePng(byte[] pngData)
    {
        return SKBitmap.Decode(pngData);
    }
}
